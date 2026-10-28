param([switch]$Interactive)
$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path $PSScriptRoot -Parent
$taskInstance = Join-Path $taskRoot 'instances/baseline'
$taskExe = Join-Path $taskInstance 'KSP_x64.exe'
if (Get-Process KSP_x64 -ErrorAction SilentlyContinue | Where-Object Path -eq $taskExe) { throw 'Baseline is already running.' }
$taskDirectory = Join-Path $taskRoot 'artifacts/preview'
New-Item -ItemType Directory -Path $taskDirectory -Force | Out-Null
$taskLog = Join-Path $taskDirectory ('torus-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '.log')
Copy-Item -LiteralPath (Join-Path $taskInstance '.jakobian-build.json') -Destination ($taskLog + '.deployment.json')
$taskFlag = if ($Interactive) { '-jakobianTorusPreview' } else { '-jakobianTorusPreviewProbe' }
$taskArguments = @($taskFlag,'-screen-fullscreen','0','-screen-width','1280','-screen-height','720','-logFile',('"' + $taskLog + '"'))
$taskStyle = if ($Interactive) { 'Normal' } else { 'Hidden' }
$taskProcess = Start-Process -FilePath $taskExe -WorkingDirectory $taskInstance -WindowStyle $taskStyle -ArgumentList $taskArguments -PassThru
if (-not $Interactive) {
    $taskWatchArgs = @('-NoProfile','-File',('"' + (Join-Path $PSScriptRoot 'Watch-Probe.ps1') + '"'),'-ProbeId',$taskProcess.Id,'-ExpectedExecutable',('"' + $taskExe + '"'),'-StartTicks',$taskProcess.StartTime.Ticks,'-LogPath',('"' + $taskLog + '"'),'-TimeoutSeconds','900')
    Start-Process -FilePath (Join-Path $PSHOME 'pwsh.exe') -WindowStyle Hidden -ArgumentList $taskWatchArgs | Out-Null
}
@{ processId=$taskProcess.Id; log=$taskLog; interactive=[bool]$Interactive; scope='detached native prefab and renderer only; no live torus body' } | ConvertTo-Json
