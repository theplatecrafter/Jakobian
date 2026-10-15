param([ValidateSet('all','JakobianControl','JakobianEllipsoid','JakobianCube','JakobianPeanut')][string]$Body = 'all')
$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path $PSScriptRoot -Parent
$taskInstance = Join-Path $taskRoot 'instances/baseline'
$taskExe = Join-Path $taskInstance 'KSP_x64.exe'
if (Get-Process -Name KSP_x64 -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $taskExe }) { throw 'Baseline instance is already running.' }
$taskLogDirectory = Join-Path $taskRoot 'artifacts/flight'
New-Item -ItemType Directory -Path $taskLogDirectory -Force | Out-Null
$taskLog = Join-Path $taskLogDirectory ($Body + '-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '.log')
$taskArguments = @("-jakobianFlightProbe=$Body",'-screen-fullscreen','0','-screen-width','960','-screen-height','540','-logFile',('"' + $taskLog + '"'))
$taskProcess = Start-Process -FilePath $taskExe -WorkingDirectory $taskInstance -WindowStyle Hidden -ArgumentList $taskArguments -PassThru
$taskWatchArgs = @('-NoProfile','-File',('"' + (Join-Path $PSScriptRoot 'Watch-Probe.ps1') + '"'),'-ProbeId',$taskProcess.Id,'-ExpectedExecutable',('"' + $taskExe + '"'),'-StartTicks',$taskProcess.StartTime.Ticks,'-LogPath',('"' + $taskLog + '"'),'-TimeoutSeconds','1200')
Start-Process -FilePath (Join-Path $PSHOME 'pwsh.exe') -WindowStyle Hidden -ArgumentList $taskWatchArgs | Out-Null
@{ processId = $taskProcess.Id; log = $taskLog; body = $Body } | ConvertTo-Json
