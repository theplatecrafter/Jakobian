param([int]$ProbeId, [string]$ExpectedExecutable, [long]$StartTicks, [string]$LogPath, [int]$TimeoutSeconds = 600)
$ErrorActionPreference = 'Stop'
$taskDeadline = (Get-Date).AddSeconds($TimeoutSeconds)
$taskResult = 'exited'
while ($true) {
    $taskProbe = Get-Process -Id $ProbeId -ErrorAction SilentlyContinue
    if (-not $taskProbe) { break }
    if ($taskProbe.Path -ne $ExpectedExecutable -or $taskProbe.StartTime.Ticks -ne $StartTicks) { $taskResult = 'identity-changed'; break }
    if ((Get-Date) -ge $taskDeadline) { Stop-Process -Id $ProbeId; $taskResult = 'timed-out'; break }
    Start-Sleep -Seconds 3
}
$taskMarker = (Test-Path -LiteralPath $LogPath) -and [IO.File]::ReadAllText($LogPath).Contains('[Jakobian] BOOT_PROBE_PASS')
$taskFlightMarker = (Test-Path -LiteralPath $LogPath) -and [IO.File]::ReadAllText($LogPath).Contains('[Jakobian] FLIGHT_SUITE_PASS')
$taskRegressionMarker = (Test-Path -LiteralPath $LogPath) -and [IO.File]::ReadAllText($LogPath).Contains('[Jakobian] REGRESSION_SUITE_PASS')
@{ processDisposition = $taskResult; bootstrapMarker = $taskMarker; flightSuiteMarker = $taskFlightMarker; regressionSuiteMarker = $taskRegressionMarker; log = $LogPath } | ConvertTo-Json | Set-Content -LiteralPath ($LogPath + '.result.json')
