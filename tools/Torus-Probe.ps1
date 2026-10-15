$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path $PSScriptRoot -Parent
$taskInstance = Join-Path $taskRoot 'instances/baseline'
$taskExe = Join-Path $taskInstance 'KSP_x64.exe'
if (Get-Process KSP_x64 -ErrorAction SilentlyContinue | Where-Object Path -eq $taskExe) { throw 'Baseline is already running.' }
$taskDirectory = Join-Path $taskRoot 'artifacts/torus'
New-Item -ItemType Directory -Path $taskDirectory -Force | Out-Null
$taskLog = Join-Path $taskDirectory ('streaming-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '.log')
Copy-Item -LiteralPath (Join-Path $taskInstance '.jakobian-build.json') -Destination ($taskLog + '.deployment.json')
$taskArguments = @('-jakobianTorusStreamingProbe','-screen-fullscreen','0','-screen-width','960','-screen-height','540','-logFile',('"' + $taskLog + '"'))
$taskProcess = Start-Process -FilePath $taskExe -WorkingDirectory $taskInstance -WindowStyle Hidden -ArgumentList $taskArguments -PassThru
$taskWatchArgs = @('-NoProfile','-File',('"' + (Join-Path $PSScriptRoot 'Watch-Probe.ps1') + '"'),'-ProbeId',$taskProcess.Id,'-ExpectedExecutable',('"' + $taskExe + '"'),'-StartTicks',$taskProcess.StartTime.Ticks,'-LogPath',('"' + $taskLog + '"'),'-TimeoutSeconds','900')
Start-Process -FilePath (Join-Path $PSHOME 'pwsh.exe') -WindowStyle Hidden -ArgumentList $taskWatchArgs | Out-Null
@{ processId=$taskProcess.Id; log=$taskLog; instance='baseline'; scope='isolated native torus contact scene, no celestial body or player saves' } | ConvertTo-Json
