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
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using SDL2;

namespace OpenRA.WindowsLauncher
{
	sealed class WindowsLauncher
	{
		[DllImport("user32.dll")]
		static extern bool SetForegroundWindow(IntPtr hWnd);

		[DllImport("user32.dll")]
		static extern bool AllowSetForegroundWindow(int dwProcessId);

		static Process gameProcess;
		static string modID;
		static string displayName;
		static string faqUrl;
		static bool companionBootstrap;

		static int Main(string[] args)
		{
			// The modID, displayName, and faqUrl variables are embedded in the assembly metadata by defining
			// -p:ModID="mymod", -p:DisplayName="My Mod", -p:FaqUrl="https://my.tld/faq" when compiling the project
			var attributes = Assembly.GetExecutingAssembly().GetCustomAttributes();
			foreach (var a in attributes)
			{
				if (a is AssemblyMetadataAttribute metadata)
				{
					switch (metadata.Key)
					{
						case "ModID": modID = metadata.Value; break;
						case "DisplayName": displayName = metadata.Value; break;
						case "FaqUrl": faqUrl = metadata.Value; break;
						case "CompanionBootstrap":
							companionBootstrap = metadata.Value.Equals("true", StringComparison.OrdinalIgnoreCase);
							break;
					}
				}
			}

			// The branded OpenRA AI executable is also a supported user entry point. Route a
			// direct launch through the product bootstrap so the local companion, voice, and
			// control services cannot be accidentally skipped. The bootstrap sets this marker
			// before it starts the game, which prevents a launch loop.
			if (companionBootstrap && Environment.GetEnvironmentVariable("OPENRA_AI_COMPANION") != "1")
				return RunCompanionBootstrap(args);

			if (Array.Exists(args, x => x.StartsWith("Engine.LaunchPath=", StringComparison.Ordinal)))
				return RunGame(args);

			return RunInnerLauncher(args);
		}

		static int RunCompanionBootstrap(string[] args)
		{
			var directory = new DirectoryInfo(Path.GetDirectoryName(Environment.ProcessPath));
			string bootstrap = null;
			var encodeArguments = false;
			for (var depth = 0; directory != null && depth < 8; depth++, directory = directory.Parent)
			{
				var candidate = Path.Combine(directory.FullName, "apps", "launcher", "Start-OpenRAAI.ps1");
				if (File.Exists(candidate))
				{
					bootstrap = candidate;
					encodeArguments = true;
					break;
				}
			}

			// Developer builds live in the canonical OpenRA checkout instead of the packaged
			// OpenRA AI directory. Use its companion-aware launcher when the product bootstrap
			// is not present, so the branded executable remains the one local entry point.
			if (bootstrap == null)
			{
				directory = new DirectoryInfo(Path.GetDirectoryName(Environment.ProcessPath));
				for (var depth = 0; directory != null && depth < 4; depth++, directory = directory.Parent)
				{
					var candidate = Path.Combine(directory.FullName, "launch-game.ps1");
					if (File.Exists(candidate))
					{
						bootstrap = candidate;
						break;
					}
				}
			}

			if (bootstrap == null)
				return (int)RunStatus.Error;

			var powershell = Path.Combine(
				Environment.GetFolderPath(Environment.SpecialFolder.System),
				"WindowsPowerShell", "v1.0", "powershell.exe");
			var psi = new ProcessStartInfo
			{
				FileName = powershell,
				UseShellExecute = false,
				WorkingDirectory = encodeArguments
					? Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(bootstrap)))
					: Path.GetDirectoryName(bootstrap)
			};
			psi.ArgumentList.Add("-NoLogo");
			psi.ArgumentList.Add("-NoProfile");
			psi.ArgumentList.Add("-ExecutionPolicy");
			psi.ArgumentList.Add("Bypass");
			psi.ArgumentList.Add("-File");
			psi.ArgumentList.Add(bootstrap);
			if (encodeArguments && args.Length > 0)
			{
				var encodedArgs = Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(args)));
				psi.ArgumentList.Add("-EncodedGameArguments");
				psi.ArgumentList.Add(encodedArgs);
			}
			else
			{
				foreach (var argument in args)
					psi.ArgumentList.Add(argument);
			}

			using var process = Process.Start(psi);
			if (process == null)
				return (int)RunStatus.Error;

			process.WaitForExit();
			return process.ExitCode;
		}

		static int RunGame(string[] args)
		{
			var launcherPath = Assembly.GetExecutingAssembly().Location;
			var directory = Path.GetDirectoryName(launcherPath);
			Directory.SetCurrentDirectory(directory);

			AppDomain.CurrentDomain.UnhandledException += (_, e) => ExceptionHandler.HandleFatalError((Exception)e.ExceptionObject);

			try
			{
				return (int)Game.InitializeAndRun(args);
			}
			catch (Exception e)
			{
				// We must grant permission for the launcher process to bring the error dialog to the foreground.
				// Finding the parent process id is unreasonably difficult on Windows, so instead pass -1 to enable for all processes.
				AllowSetForegroundWindow(-1);
				ExceptionHandler.HandleFatalError(e);
				return (int)RunStatus.Error;
			}
			finally
			{
				// Flushing logs in finally block is okay here, as the catch block handles the exception.
				Log.Dispose();
			}
		}

		static int RunInnerLauncher(string[] args)
		{
			var launcherPath = Environment.ProcessPath;
			var launcherArgs = args.ToList();

			if (!launcherArgs.Exists(x => x.StartsWith("Engine.LaunchPath=", StringComparison.Ordinal)))
				launcherArgs.Add("Engine.LaunchPath=\"" + launcherPath + "\"");

			if (!launcherArgs.Exists(x => x.StartsWith("Game.Mod=", StringComparison.Ordinal)))
				launcherArgs.Add("Game.Mod=" + modID);

			var psi = new ProcessStartInfo(launcherPath, string.Join(" ", launcherArgs));

			try
			{
				gameProcess = Process.Start(psi);
			}
			catch
			{
				return 1;
			}

			if (gameProcess == null)
				return 1;

			gameProcess.EnableRaisingEvents = true;
			gameProcess.Exited += GameProcessExited;
			gameProcess.WaitForExit();

			return 0;
		}

		static void ShowErrorDialog()
		{
			var viewLogs = new SDL.SDL_MessageBoxButtonData
			{
				buttonid = 2,
				text = "View Logs",
				flags = SDL.SDL_MessageBoxButtonFlags.SDL_MESSAGEBOX_BUTTON_RETURNKEY_DEFAULT
			};

			var viewFaq = new SDL.SDL_MessageBoxButtonData
			{
				buttonid = 1,
				text = "View FAQ"
			};

			var quit = new SDL.SDL_MessageBoxButtonData
			{
				buttonid = 0,
				text = "Quit",
				flags = SDL.SDL_MessageBoxButtonFlags.SDL_MESSAGEBOX_BUTTON_ESCAPEKEY_DEFAULT
			};

			var dialog = new SDL.SDL_MessageBoxData
			{
				flags = SDL.SDL_MessageBoxFlags.SDL_MESSAGEBOX_ERROR,
				title = "Fatal Error",
				message = displayName + " has encountered a fatal error and must close.\nRefer to the crash logs and FAQ for more information.",
				buttons = [quit, viewFaq, viewLogs],
				numbuttons = 3
			};

			// SDL_ShowMessageBox may create the error dialog behind other windows.
			// We want to bring it to the foreground, but can't do it from the main thread
			// because SDL_ShowMessageBox blocks until the user presses a button.
			// HACK: Spawn a thread to raise it to the foreground after a short delay.
			Task.Run(() =>
			{
				Thread.Sleep(1000);
				SetForegroundWindow(Process.GetCurrentProcess().MainWindowHandle);
			});

			if (SDL.SDL_ShowMessageBox(ref dialog, out var buttonid) < 0)
				Exit();

			switch (buttonid)
			{
				case 0: Exit(); break;
				case 1:
				{
					try
					{
						SDL.SDL_OpenURL(faqUrl);
					}
					catch { }
					break;
				}

				case 2:
				{
					try
					{
						SDL.SDL_OpenURL(Path.Combine(Platform.SupportDir, "Logs"));
					}
					catch { }
					break;
				}
			}
		}

		static void GameProcessExited(object sender, EventArgs e)
		{
			if (gameProcess.ExitCode != (int)RunStatus.Success)
				ShowErrorDialog();

			Exit();
		}

		static void Exit()
		{
			Environment.Exit(0);
		}
	}
}
