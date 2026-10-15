$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path $PSScriptRoot -Parent
$taskInstance = Join-Path $taskRoot 'instances/baseline'
$taskExe = Join-Path $taskInstance 'KSP_x64.exe'
if (Get-Process KSP_x64 -ErrorAction SilentlyContinue | Where-Object Path -eq $taskExe) { throw 'Baseline is already running.' }
$taskInputs = Join-Path $taskInstance '.dev/regression-inputs'
New-Item -ItemType Directory -Path $taskInputs -Force | Out-Null
$taskRecords = @()
foreach ($taskVersion in @('0.2.0','0.3.0')) {
    $taskRecord = Get-Content -LiteralPath (Join-Path $taskRoot "docs/development/v$taskVersion-validation.json") -Raw | ConvertFrom-Json
    foreach ($taskFlight in $taskRecord.native.flight) {
        $taskCandidates = @((Join-Path $taskInstance ('saves/' + $taskFlight.save + '/persistent.sfs')), (Join-Path $taskInstance ('.dev/older-probe-saves/' + $taskFlight.save + '/persistent.sfs')))
        $taskSource = $taskCandidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
        if (-not $taskSource) { throw "Missing historical fixture: $($taskFlight.save)" }
        $taskDestination = Join-Path $taskInputs ("v$taskVersion-" + $taskFlight.body + '.sfs')
        if (Test-Path -LiteralPath $taskDestination) {
            if ((Get-FileHash -LiteralPath $taskDestination).Hash -ne (Get-FileHash -LiteralPath $taskSource).Hash) { throw 'Historical input differs; preserve and investigate rather than replace.' }
        } else { Copy-Item -LiteralPath $taskSource -Destination $taskDestination }
        $taskRecords += @{ version = $taskVersion; body = $taskFlight.body; source = $taskSource; fixture = $taskDestination; sha256 = (Get-FileHash -LiteralPath $taskDestination).Hash.ToLowerInvariant() }
    }
}
$taskRecords | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $taskInputs 'manifest.json')
$taskLogDirectory = Join-Path $taskRoot 'artifacts/regression'
New-Item -ItemType Directory -Path $taskLogDirectory -Force | Out-Null
$taskLog = Join-Path $taskLogDirectory ('regression-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '.log')
Copy-Item -LiteralPath (Join-Path $taskInstance '.jakobian-build.json') -Destination ($taskLog + '.deployment.json')
$taskArguments = @('-jakobianRegressionProbe','-screen-fullscreen','0','-screen-width','960','-screen-height','540','-logFile',('"' + $taskLog + '"'))
$taskProcess = Start-Process -FilePath $taskExe -WorkingDirectory $taskInstance -WindowStyle Hidden -ArgumentList $taskArguments -PassThru
$taskWatchArgs = @('-NoProfile','-File',('"' + (Join-Path $PSScriptRoot 'Watch-Probe.ps1') + '"'),'-ProbeId',$taskProcess.Id,'-ExpectedExecutable',('"' + $taskExe + '"'),'-StartTicks',$taskProcess.StartTime.Ticks,'-LogPath',('"' + $taskLog + '"'),'-TimeoutSeconds','1200')
Start-Process -FilePath (Join-Path $PSHOME 'pwsh.exe') -WindowStyle Hidden -ArgumentList $taskWatchArgs | Out-Null
@{ processId = $taskProcess.Id; log = $taskLog; historicalFixtures = $taskRecords.Count } | ConvertTo-Json
