param([Parameter(Mandatory)][ValidatePattern('^[a-z][a-z0-9-]{0,40}$')][string]$Instance)
$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path $PSScriptRoot -Parent
$taskBase = [IO.Path]::GetFullPath((Join-Path $taskRoot 'instances'))
$taskTarget = [IO.Path]::GetFullPath((Join-Path $taskBase $Instance))
if (-not $taskTarget.StartsWith($taskBase + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Target escapes instances.' }
if (-not (Test-Path -LiteralPath (Join-Path $taskTarget '.jakobian-instance.json'))) { throw 'Target is not a managed Jakobian development instance.' }
$taskMarker = Get-Content -LiteralPath (Join-Path $taskTarget '.jakobian-instance.json') | ConvertFrom-Json
if ($taskMarker.owner -ne 'Jakobian') { throw 'Wrong instance owner.' }
$taskRunning = Get-Process -Name KSP_x64 -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq (Join-Path $taskTarget 'KSP_x64.exe') }
if ($taskRunning) { throw 'Close this development instance before deploying.' }
$taskDlls = @('src/Jakobian.KSP/bin/Release/net472/Jakobian.dll','src/Jakobian.Core/bin/Release/net472/Jakobian.Core.dll')
foreach ($taskDll in $taskDlls) { if (-not (Test-Path -LiteralPath (Join-Path $taskRoot $taskDll))) { throw 'Build Release first.' } }
$taskPlugins = Join-Path $taskTarget 'GameData/Jakobian/Plugins'
New-Item -ItemType Directory -Path $taskPlugins -Force | Out-Null
foreach ($taskDll in $taskDlls) { Copy-Item -LiteralPath (Join-Path $taskRoot $taskDll) -Destination $taskPlugins }
$taskFixtures = Join-Path $taskRoot 'GameData/JakobianTestBodies'
Copy-Item -LiteralPath $taskFixtures -Destination (Join-Path $taskTarget 'GameData') -Recurse -Force
$taskDev = Join-Path $taskTarget '.dev'
New-Item -ItemType Directory -Path $taskDev -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $taskRoot 'template_instance/saves/training/C_Orbit101.sfs') -Destination (Join-Path $taskDev 'FlightSeed.sfs') -Force
$taskInstalledFiles = @('GameData/Jakobian/Plugins/Jakobian.dll','GameData/Jakobian/Plugins/Jakobian.Core.dll','GameData/JakobianTestBodies/Bodies.cfg')
$taskFileRecords = foreach ($taskInstalledFile in $taskInstalledFiles) {
    $taskInstalledPath = Join-Path $taskTarget $taskInstalledFile
    @{ path = $taskInstalledFile; sha256 = (Get-FileHash -LiteralPath $taskInstalledPath -Algorithm SHA256).Hash.ToLowerInvariant() }
}
@{ version = [Reflection.AssemblyName]::GetAssemblyName((Join-Path $taskPlugins 'Jakobian.dll')).Version.ToString(); deployedUtc = (Get-Date).ToUniversalTime().ToString('o'); files = @($taskFileRecords) } | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $taskTarget '.jakobian-build.json')
Write-Output "Deployed Jakobian and test bodies to $taskTarget"
