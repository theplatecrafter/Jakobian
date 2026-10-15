param([Parameter(Mandatory)][ValidatePattern('^[a-z][a-z0-9-]{0,40}$')][string]$Instance)
$ErrorActionPreference = 'Stop'
function Get-JakobianTextHash([string]$Path) {
    $taskAlgorithm = [Security.Cryptography.SHA256]::Create()
    try {
        $taskBytes = [Text.Encoding]::UTF8.GetBytes([IO.File]::ReadAllText($Path).Replace("`r`n","`n"))
        return ($taskAlgorithm.ComputeHash($taskBytes) | ForEach-Object { $_.ToString('x2') }) -join ''
    } finally { $taskAlgorithm.Dispose() }
}
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
$taskBundle = Join-Path $taskRoot 'GameData/Jakobian/Assets/jakobian-shaders'
if (-not (Test-Path -LiteralPath $taskBundle)) { throw 'Missing shader asset bundle. Run tools/Build-Assets.ps1, inspect its result, then copy artifacts/unity/windows/jakobian-shaders to GameData/Jakobian/Assets/.' }
$taskAssetManifest = Get-Content -LiteralPath (Join-Path $taskRoot 'GameData/Jakobian/Assets/shader-manifest.json') -Raw | ConvertFrom-Json
if ((Get-FileHash -LiteralPath $taskBundle).Hash.ToLowerInvariant() -ne $taskAssetManifest.bundleSha256) { throw 'Shader bundle differs from its build manifest.' }
foreach ($taskSource in $taskAssetManifest.sources) {
    if ((Get-JakobianTextHash (Join-Path $taskRoot $taskSource.path)) -ne $taskSource.sha256) { throw 'Shader sources changed. Run tools/Build-Assets.ps1 first.' }
}
$taskPlugins = Join-Path $taskTarget 'GameData/Jakobian/Plugins'
New-Item -ItemType Directory -Path $taskPlugins -Force | Out-Null
foreach ($taskDll in $taskDlls) { Copy-Item -LiteralPath (Join-Path $taskRoot $taskDll) -Destination $taskPlugins }
$taskAssets = Join-Path $taskTarget 'GameData/Jakobian/Assets'
New-Item -ItemType Directory -Path $taskAssets -Force | Out-Null
Copy-Item -LiteralPath $taskBundle -Destination $taskAssets -Force
Copy-Item -LiteralPath (Join-Path $taskRoot 'GameData/Jakobian/Assets/shader-manifest.json') -Destination $taskAssets -Force
$taskFixtures = Join-Path $taskRoot 'GameData/JakobianTestBodies'
Copy-Item -LiteralPath $taskFixtures -Destination (Join-Path $taskTarget 'GameData') -Recurse -Force
$taskDev = Join-Path $taskTarget '.dev'
New-Item -ItemType Directory -Path $taskDev -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $taskRoot 'template_instance/saves/training/C_Orbit101.sfs') -Destination (Join-Path $taskDev 'FlightSeed.sfs') -Force
$taskInstalledFiles = @('GameData/Jakobian/Plugins/Jakobian.dll','GameData/Jakobian/Plugins/Jakobian.Core.dll','GameData/Jakobian/Assets/jakobian-shaders','GameData/Jakobian/Assets/shader-manifest.json')
$taskInstalledFiles += Get-ChildItem -LiteralPath $taskFixtures -Recurse -File | Sort-Object FullName | ForEach-Object { 'GameData/JakobianTestBodies/' + [IO.Path]::GetRelativePath($taskFixtures,$_.FullName).Replace('\','/') }
$taskFileRecords = foreach ($taskInstalledFile in $taskInstalledFiles) {
    $taskInstalledPath = Join-Path $taskTarget $taskInstalledFile
    @{ path = $taskInstalledFile; sha256 = (Get-FileHash -LiteralPath $taskInstalledPath -Algorithm SHA256).Hash.ToLowerInvariant() }
}
@{ version = [Reflection.AssemblyName]::GetAssemblyName((Join-Path $taskPlugins 'Jakobian.dll')).Version.ToString(); deployedUtc = (Get-Date).ToUniversalTime().ToString('o'); files = @($taskFileRecords) } | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $taskTarget '.jakobian-build.json')
Write-Output "Deployed Jakobian and test bodies to $taskTarget"
