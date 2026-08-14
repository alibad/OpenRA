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
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace OpenRA.Launcher
{
	static class Program
	{
		[STAThread]
		static int Main(string[] args)
		{
			if (ShouldStartWithCompanion(args))
				return StartWithCompanion(args);

			if (Debugger.IsAttached || args.Contains("--just-die"))
			{
				try
				{
					return (int)Game.InitializeAndRun(args);
				}
				catch
				{
					// Flush logs before rethrowing, i.e. allowing the exception to go unhandled.
					// try-finally won't work - an unhandled exception kills our process without running the finally block!
					Log.Dispose();
					throw;
				}
				finally
				{
					Log.Dispose();
				}
			}

			AppDomain.CurrentDomain.UnhandledException += (_, e) => ExceptionHandler.HandleFatalError((Exception)e.ExceptionObject);

			try
			{
				return (int)Game.InitializeAndRun(args);
			}
			catch (Exception e)
			{
				ExceptionHandler.HandleFatalError(e);
				return (int)RunStatus.Error;
			}
			finally
			{
				// Flushing logs in finally block is okay here, as the catch block handles the exception.
				Log.Dispose();
			}
		}

		static bool ShouldStartWithCompanion(string[] args)
		{
			if (!OperatingSystem.IsWindows() || Debugger.IsAttached || args.Contains("--just-die"))
				return false;

			if (Environment.GetEnvironmentVariable("OPENRA_AI_COMPANION") == "1" ||
				Environment.GetEnvironmentVariable("OPENRA_AI_DISABLE_AUTOSTART") == "1")
				return false;

			var mod = args.LastOrDefault(a => a.StartsWith("Game.Mod=", StringComparison.OrdinalIgnoreCase));
			if (mod != null && !mod["Game.Mod=".Length..].Trim('"').Equals("ra", StringComparison.OrdinalIgnoreCase))
				return false;

			return File.Exists(GetCompanionLauncherPath());
		}

		static int StartWithCompanion(string[] args)
		{
			var startInfo = new ProcessStartInfo("powershell.exe")
			{
				UseShellExecute = false,
				CreateNoWindow = true,
				WorkingDirectory = Path.GetDirectoryName(GetCompanionLauncherPath())
			};

			startInfo.ArgumentList.Add("-NoLogo");
			startInfo.ArgumentList.Add("-NoProfile");
			startInfo.ArgumentList.Add("-ExecutionPolicy");
			startInfo.ArgumentList.Add("Bypass");
			startInfo.ArgumentList.Add("-File");
			startInfo.ArgumentList.Add(GetCompanionLauncherPath());
			foreach (var argument in args)
				startInfo.ArgumentList.Add(argument);

			using var process = Process.Start(startInfo);
			if (process == null)
				return (int)RunStatus.Error;

			process.WaitForExit();
			return process.ExitCode;
		}

		static string GetCompanionLauncherPath()
		{
			return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "launch-game.ps1"));
		}
	}
}
