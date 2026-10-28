$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path $PSScriptRoot -Parent
$taskInstance = Join-Path $taskRoot 'instances/baseline'
$taskExe = Join-Path $taskInstance 'KSP_x64.exe'
if (Get-Process KSP_x64 -ErrorAction SilentlyContinue | Where-Object Path -eq $taskExe) { throw 'Baseline is already running.' }
$taskDirectory = Join-Path $taskRoot 'artifacts/admission'
New-Item -ItemType Directory -Path $taskDirectory -Force | Out-Null
$taskLog = Join-Path $taskDirectory ('gate-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '.log')
Copy-Item -LiteralPath (Join-Path $taskInstance '.jakobian-build.json') -Destination ($taskLog + '.deployment.json')
$taskProcess = Start-Process -FilePath $taskExe -WorkingDirectory $taskInstance -WindowStyle Hidden -ArgumentList @('-jakobianAdmissionProbe','-screen-fullscreen','0','-screen-width','960','-screen-height','540','-logFile',('"'+$taskLog+'"')) -PassThru
$taskArgs = @('-NoProfile','-File',('"'+(Join-Path $PSScriptRoot 'Watch-Probe.ps1')+'"'),'-ProbeId',$taskProcess.Id,'-ExpectedExecutable',('"'+$taskExe+'"'),'-StartTicks',$taskProcess.StartTime.Ticks,'-LogPath',('"'+$taskLog+'"'),'-TimeoutSeconds','900')
Start-Process -FilePath (Join-Path $PSHOME 'pwsh.exe') -WindowStyle Hidden -ArgumentList $taskArgs | Out-Null
@{processId=$taskProcess.Id;log=$taskLog;scope='guarded load/start routes; no live torus'} | ConvertTo-Json
