#region Copyright & License Information
/*
 * Copyright (c) The OpenRA Developers and Contributors
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using OpenRA.Mods.Common.Lint;
using OpenRA.Widgets;

namespace OpenRA.Mods.Common.Widgets.Logic
{
	[ChromeLogicArgsHotkeys("AskKey", "AcceptKey", "RejectKey", "ToggleAutoKey", "ToggleVoiceKey")]
	public sealed class AIHotkeyLogic : ChromeLogic
	{
		readonly HotkeyReference askKey;
		readonly HotkeyReference acceptKey;
		readonly HotkeyReference rejectKey;
		readonly HotkeyReference toggleAutoKey;
		readonly HotkeyReference toggleVoiceKey;
		bool askHeld;
		volatile bool operationBusy;

		[ObjectCreator.UseCtor]
		public AIHotkeyLogic(Widget widget, ModData modData, Dictionary<string, MiniYaml> logicArgs)
		{
			askKey = Resolve(modData, logicArgs, "AskKey");
			acceptKey = Resolve(modData, logicArgs, "AcceptKey");
			rejectKey = Resolve(modData, logicArgs, "RejectKey");
			toggleAutoKey = Resolve(modData, logicArgs, "ToggleAutoKey");
			toggleVoiceKey = Resolve(modData, logicArgs, "ToggleVoiceKey");

			widget.Get<LogicKeyListenerWidget>("GLOBAL_KEYHANDLER").AddHandler(HandleKeyPress);
		}

		static HotkeyReference Resolve(ModData modData, Dictionary<string, MiniYaml> logicArgs, string name)
		{
			return logicArgs.TryGetValue(name, out var yaml) ? modData.Hotkeys[yaml.Value] : new HotkeyReference();
		}

		bool HandleKeyPress(KeyInput e)
		{
			var ask = askKey.GetValue();
			if (e.Event == KeyInputEvent.Down && askKey.IsActivatedBy(e))
			{
				if (!askHeld)
				{
					askHeld = true;
					_ = PostAsync("v1/voice/start");
				}

				return true;
			}

			if (e.Event == KeyInputEvent.Up && askHeld && e.Key == ask.Key)
			{
				askHeld = false;
				_ = PostAsync("v1/voice/stop");
				return true;
			}

			if (e.Event != KeyInputEvent.Down)
				return false;

			if (acceptKey.IsActivatedBy(e))
				return StartOperation(() => PostAsync("v1/actions/confirm"));
			if (rejectKey.IsActivatedBy(e))
				return StartOperation(() => PostAsync("v1/actions/cancel"));
			if (toggleAutoKey.IsActivatedBy(e))
				return StartOperation(() => ToggleAsync("auto_act_enabled", "auto_act"));
			if (toggleVoiceKey.IsActivatedBy(e))
				return StartOperation(() => ToggleAsync("voice_enabled", "muted", invertPostedValue: true));

			return false;
		}

		bool StartOperation(Func<Task> operation)
		{
			if (!operationBusy)
			{
				operationBusy = true;
				_ = RunOperationAsync(operation);
			}

			return true;
		}

		async Task RunOperationAsync(Func<Task> operation)
		{
			try
			{
				await operation();
			}
			catch { }
			finally
			{
				operationBusy = false;
			}
		}

		static async Task PostAsync(string path)
		{
			var baseUri = OpenRAAILocalClient.GetBaseUri("OPENRA_AI_CONSOLE_URL", "http://127.0.0.1:8787/");
			using var response = await OpenRAAILocalClient.PostAsync(baseUri, path, new { });
		}

		static async Task ToggleAsync(string stateField, string controlField, bool invertPostedValue = false)
		{
			var baseUri = OpenRAAILocalClient.GetBaseUri("OPENRA_AI_CONSOLE_URL", "http://127.0.0.1:8787/");
			using var state = await OpenRAAILocalClient.GetAsync(baseUri, "v1/state");
			var current = state.RootElement.GetProperty(stateField).GetBoolean();
			var next = invertPostedValue ? current : !current;
			var payload = new Dictionary<string, object> { { controlField, next } };
			using var response = await OpenRAAILocalClient.PostAsync(baseUri, "v1/control", payload);
		}
	}
}
