[CmdletBinding()]
param(
	[switch]$NoCompanion,
	[switch]$NoSpeech,
	[string]$CompanionRoot,
	[int]$BridgePort = 9998,
	[int]$AIConsolePort = 8787,
	[int]$WorldStudioPort = 8788,
	[switch]$ValidateOnly,
	[Parameter(Position = 0, ValueFromRemainingArguments = $true)]
	[string[]]$GameArguments
)

$ErrorActionPreference = "Stop"
$engineRoot = $PSScriptRoot
$game = Join-Path $engineRoot "bin\OpenRA.exe"
$launchPath = Join-Path $engineRoot "launch-game.cmd"

function Get-ArgumentValue([string]$Name, [string[]]$Arguments) {
	$prefix = "$Name="
	$argument = $Arguments | Where-Object { $_.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase) } |
		Select-Object -Last 1
	if ($null -eq $argument) {
		return $null
	}

	return $argument.Substring($prefix.Length).Trim('"')
}

function Add-DefaultArgument([Collections.Generic.List[string]]$Arguments, [string]$Name, [string]$Value) {
	if ($null -eq (Get-ArgumentValue $Name $Arguments)) {
		$Arguments.Insert(0, "$Name=$Value")
	}
}

function Test-LocalPortAvailable([int]$Port) {
	$listener = $null
	try {
		$listener = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback, $Port)
		$listener.Start()
		return $true
	}
	catch [Net.Sockets.SocketException] {
		return $false
	}
	finally {
		if ($null -ne $listener) {
			$listener.Stop()
		}
	}
}

function Get-AvailableLocalPort([int]$PreferredPort, [Collections.Generic.HashSet[int]]$ReservedPorts) {
	if (-not $ReservedPorts.Contains($PreferredPort) -and (Test-LocalPortAvailable $PreferredPort)) {
		[void]$ReservedPorts.Add($PreferredPort)
		return $PreferredPort
	}

	$listener = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback, 0)
	try {
		$listener.Start()
		$port = ([Net.IPEndPoint]$listener.LocalEndpoint).Port
	}
	finally {
		$listener.Stop()
	}

	[void]$ReservedPorts.Add($port)
	return $port
}

function Resolve-CompanionRuntime([string]$RequestedRoot) {
	$candidates = [Collections.Generic.List[string]]::new()
	if (-not [string]::IsNullOrWhiteSpace($RequestedRoot)) {
		$candidates.Add($RequestedRoot)
	}
	if (-not [string]::IsNullOrWhiteSpace($env:OPENRA_AI_ROOT)) {
		$candidates.Add($env:OPENRA_AI_ROOT)
	}
	$candidates.Add((Join-Path (Split-Path -Parent $engineRoot) "OpenRA-AI"))

	foreach ($candidate in $candidates) {
		$root = [IO.Path]::GetFullPath($candidate)
		$bundled = Join-Path $root "bin\openra-ai-companion.exe"
		$virtualEnvironmentExecutable = Join-Path $root ".venv\Scripts\openra-ai-companion.exe"
		$python = Join-Path $root ".venv\Scripts\python.exe"

		if (Test-Path -LiteralPath $bundled -PathType Leaf) {
			return [PSCustomObject]@{
				Root = $root
				Program = $bundled
				PrefixArguments = @()
				PythonPaths = @()
			}
		}

		if (Test-Path -LiteralPath $python -PathType Leaf) {
			$pythonPaths = @(
				Join-Path $root "services\companion\src"
				Join-Path $root "services\worldgen\src"
			) | Where-Object { Test-Path -LiteralPath $_ -PathType Container }
			return [PSCustomObject]@{
				Root = $root
				Program = $python
				PrefixArguments = @("-u", "-m", "openra_ai_companion.cli")
				PythonPaths = $pythonPaths
			}
		}

		if (Test-Path -LiteralPath $virtualEnvironmentExecutable -PathType Leaf) {
			return [PSCustomObject]@{
				Root = $root
				Program = $virtualEnvironmentExecutable
				PrefixArguments = @()
				PythonPaths = @()
			}
		}
	}

	return $null
}

if (-not (Test-Path -LiteralPath $game -PathType Leaf)) {
	throw "Missing OpenRA build output: $game. Run make.cmd all first."
}

if ($GameArguments -contains "--exit") {
	exit 0
}

$arguments = [Collections.Generic.List[string]]::new()
foreach ($argument in $GameArguments) {
	$arguments.Add($argument)
}

$mod = Get-ArgumentValue "Game.Mod" $arguments
while ([string]::IsNullOrWhiteSpace($mod)) {
	$mod = (Read-Host "Select mod (ra, cnc, d2k, ts) or --exit").Trim().ToLowerInvariant()
	if ($mod -eq "--exit") {
		exit 0
	}
	if ($mod -notin @("ra", "cnc", "d2k", "ts")) {
		Write-Host "Unknown mod: $mod" -ForegroundColor Yellow
		$mod = $null
	}
}

Add-DefaultArgument $arguments "Game.Mod" $mod
Add-DefaultArgument $arguments "Engine.LaunchPath" $launchPath
Add-DefaultArgument $arguments "Engine.EngineDir" $engineRoot

$companionRequested = $mod.Equals("ra", [StringComparison]::OrdinalIgnoreCase) -and -not $NoCompanion
$runtime = $null
$ports = $null
if ($companionRequested) {
	$runtime = Resolve-CompanionRuntime $CompanionRoot
	if ($null -eq $runtime) {
		throw "The OpenRA AI companion runtime was not found. Run ..\OpenRA-AI\scripts\setup.ps1 -SkipEngine, set OPENRA_AI_ROOT, or pass -NoCompanion to intentionally launch without it."
	}

	$reservedPorts = [Collections.Generic.HashSet[int]]::new()
	$ports = [PSCustomObject]@{
		Bridge = Get-AvailableLocalPort $BridgePort $reservedPorts
		Console = Get-AvailableLocalPort $AIConsolePort $reservedPorts
		WorldStudio = Get-AvailableLocalPort $WorldStudioPort $reservedPorts
	}
}

if ($ValidateOnly) {
	[PSCustomObject]@{
		Game = $game
		Mod = $mod
		Arguments = $arguments.ToArray()
		CompanionRequested = $companionRequested
		CompanionRoot = if ($null -eq $runtime) { $null } else { $runtime.Root }
		CompanionProgram = if ($null -eq $runtime) { $null } else { $runtime.Program }
		Ports = $ports
	} | ConvertTo-Json -Depth 4
	exit 0
}

$logDirectory = Join-Path $engineRoot "Support\Logs"
New-Item -ItemType Directory -Path $logDirectory -Force | Out-Null
$gameProcess = $null
$watcher = $null

try {
	if ($companionRequested) {
		$version = (Get-Content -LiteralPath (Join-Path $engineRoot "VERSION") -Raw).Trim()
		$supportRoot = if (Test-Path -LiteralPath (Join-Path $engineRoot "Support")) {
			Join-Path $engineRoot "Support"
		}
		else {
			Join-Path $env:APPDATA "OpenRA"
		}
		$missionOutput = Join-Path $supportRoot "GeneratedMissions"
		$missionInstall = Join-Path $supportRoot "maps\ra\$version"
		New-Item -ItemType Directory -Path $missionOutput -Force | Out-Null
		New-Item -ItemType Directory -Path $missionInstall -Force | Out-Null

		$env:DOTNET_ROLL_FORWARD = "Major"
		$env:OPENRA_AI_COMPANION = "1"
		$env:OPENRA_AI_GRPC_PORT = [string]$ports.Bridge
		$env:OPENRA_AI_CONSOLE_URL = "http://127.0.0.1:$($ports.Console)/"
		$env:OPENRA_AI_WORLD_STUDIO_URL = "http://127.0.0.1:$($ports.WorldStudio)/"
		$env:OPENRA_AI_ENGINE_DIR = $engineRoot
		if ([string]::IsNullOrWhiteSpace($env:OPENRA_AI_APP_LANGUAGE)) {
			$env:OPENRA_AI_APP_LANGUAGE = "en"
		}
		$env:PYTHONUNBUFFERED = "1"
		$env:PYTHONUTF8 = "1"
		$env:PYTHONIOENCODING = "utf-8"
		if ($runtime.PythonPaths.Count -gt 0) {
			$pythonPaths = [Collections.Generic.List[string]]::new()
			foreach ($path in $runtime.PythonPaths) {
				$pythonPaths.Add($path)
			}
			if (-not [string]::IsNullOrWhiteSpace($env:PYTHONPATH)) {
				$pythonPaths.Add($env:PYTHONPATH)
			}
			$env:PYTHONPATH = $pythonPaths -join [IO.Path]::PathSeparator
		}

		Write-Host "AI companion: enabled from $($runtime.Root)" -ForegroundColor Cyan
		Write-Host "AI controls: hold Ctrl+Space to ask; Ctrl+Shift+A toggles AUTO; Ctrl+Shift+M toggles voice." -ForegroundColor Cyan
	}

	$gameProcess = Start-Process -FilePath $game -ArgumentList $arguments.ToArray() `
		-WorkingDirectory $engineRoot -PassThru

	if ($companionRequested) {
		$watchArguments = [Collections.Generic.List[string]]::new()
		foreach ($argument in $runtime.PrefixArguments) {
			$watchArguments.Add($argument)
		}
		$watchArguments.Add("watch")
		$watchArguments.Add("--bridge")
		$watchArguments.Add("127.0.0.1:$($ports.Bridge)")
		$watchArguments.Add("--game-pid")
		$watchArguments.Add([string]$gameProcess.Id)
		$watchArguments.Add("--control-port")
		$watchArguments.Add([string]$ports.Console)
		$watchArguments.Add("--worldgen-port")
		$watchArguments.Add([string]$ports.WorldStudio)
		$watchArguments.Add("--mission-output")
		$watchArguments.Add($missionOutput)
		$watchArguments.Add("--mission-install")
		$watchArguments.Add($missionInstall)
		if ($NoSpeech) {
			$watchArguments.Add("--no-speak")
		}
		else {
			$watchArguments.Add("--speak")
			$watchArguments.Add("--voice-hotkeys")
		}

		$watcher = Start-Process -FilePath $runtime.Program -ArgumentList $watchArguments.ToArray() `
			-WorkingDirectory $runtime.Root -WindowStyle Hidden -PassThru `
			-RedirectStandardOutput (Join-Path $logDirectory "ai-companion.out.log") `
			-RedirectStandardError (Join-Path $logDirectory "ai-companion.err.log")

		Start-Sleep -Milliseconds 750
		if ($watcher.HasExited) {
			$watchError = Get-Content -LiteralPath (Join-Path $logDirectory "ai-companion.err.log") -Raw -ErrorAction SilentlyContinue
			throw "The AI companion watcher exited during startup. $watchError"
		}
	}

	$gameProcess.WaitForExit()
	exit $gameProcess.ExitCode
}
finally {
	if ($null -ne $watcher -and -not $watcher.HasExited) {
		if (-not $watcher.WaitForExit(2500)) {
			Stop-Process -Id $watcher.Id -ErrorAction SilentlyContinue
		}
	}
	if ($null -ne $gameProcess -and -not $gameProcess.HasExited) {
		Stop-Process -Id $gameProcess.Id -Force -ErrorAction SilentlyContinue
	}
}
