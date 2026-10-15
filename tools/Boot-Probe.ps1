param([Parameter(Mandatory)][ValidatePattern('^[a-z][a-z0-9-]{0,40}$')][string]$Instance, [switch]$Graphics = $true, [ValidateRange(30,1800)][int]$TimeoutSeconds = 600)
$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path $PSScriptRoot -Parent
$taskInstance = Join-Path $taskRoot "instances/$Instance"
if (-not (Test-Path -LiteralPath (Join-Path $taskInstance '.jakobian-instance.json'))) { throw 'Create a development instance first.' }
if (-not (Test-Path -LiteralPath (Join-Path $taskInstance 'GameData/Jakobian/Plugins/Jakobian.dll'))) { throw 'Deploy the scaffold first.' }
$taskLogs = Join-Path $taskRoot 'artifacts/boot'
New-Item -ItemType Directory -Path $taskLogs -Force | Out-Null
$taskLog = Join-Path $taskLogs ("$Instance-" + (Get-Date -Format 'yyyyMMdd-HHmmss') + '.log')
Copy-Item -LiteralPath (Join-Path $taskInstance '.jakobian-build.json') -Destination ($taskLog + '.deployment.json')
# Returns immediately. The MainMenu scaffold records a marker and quits only this probe process.
$taskArguments = @('-jakobianBootProbe','-logFile',('"' + $taskLog + '"'))
if ($Graphics) { $taskArguments += @('-force-d3d11','-screen-fullscreen','0','-screen-width','640','-screen-height','360') } else { $taskArguments += @('-batchmode','-nographics') }
$taskProcess = Start-Process -FilePath (Join-Path $taskInstance 'KSP_x64.exe') -WorkingDirectory $taskInstance -WindowStyle Hidden -ArgumentList $taskArguments -PassThru
$taskWatcher = Join-Path $PSScriptRoot 'Watch-Probe.ps1'
$taskExecutable = Join-Path $taskInstance 'KSP_x64.exe'
$taskWatcherArguments = @('-NoProfile','-File',('"' + $taskWatcher + '"'),'-ProbeId',$taskProcess.Id,'-ExpectedExecutable',('"' + $taskExecutable + '"'),'-StartTicks',$taskProcess.StartTime.Ticks,'-LogPath',('"' + $taskLog + '"'),'-TimeoutSeconds',$TimeoutSeconds)
Start-Process -FilePath (Join-Path $PSHOME 'pwsh.exe') -WindowStyle Hidden -ArgumentList $taskWatcherArguments | Out-Null
@{ processId = $taskProcess.Id; log = $taskLog; instance = $Instance } | ConvertTo-Json
