$ErrorActionPreference = 'Stop'
function Get-JakobianTextHash([string]$Path) {
    $taskAlgorithm = [Security.Cryptography.SHA256]::Create()
    try {
        $taskBytes = [Text.Encoding]::UTF8.GetBytes([IO.File]::ReadAllText($Path).Replace("`r`n","`n"))
        return ($taskAlgorithm.ComputeHash($taskBytes) | ForEach-Object { $_.ToString('x2') }) -join ''
    } finally { $taskAlgorithm.Dispose() }
}
$taskRoot = Split-Path $PSScriptRoot -Parent
$taskEditor = 'C:/Program Files/Unity/Editor/Unity.exe'
$taskLog = Join-Path $taskRoot 'artifacts/unity-build.log'
New-Item -ItemType Directory -Path (Join-Path $taskRoot 'artifacts') -Force | Out-Null
$taskArguments = @('-batchmode','-nographics','-quit','-projectPath',('"' + (Join-Path $taskRoot 'unity/Jakobian.Assets') + '"'),'-executeMethod','JakobianAssets.BuildBundles','-logFile',('"' + $taskLog + '"'))
$taskProcess = Start-Process -FilePath $taskEditor -WindowStyle Hidden -ArgumentList $taskArguments -PassThru -Wait
if ($taskProcess.ExitCode -ne 0) { throw "Unity asset build failed; inspect $taskLog" }
$taskOutput = Join-Path $taskRoot 'artifacts/unity/windows/jakobian-shaders'
if (-not (Test-Path -LiteralPath $taskOutput)) { throw 'Unity did not produce the shader bundle.' }
$taskAssets = Join-Path $taskRoot 'GameData/Jakobian/Assets'
New-Item -ItemType Directory -Path $taskAssets -Force | Out-Null
Copy-Item -LiteralPath $taskOutput -Destination $taskAssets -Force
$taskSources = @('unity/Jakobian.Assets/Assets/JakobianMappedTerrain.shader','unity/Jakobian.Assets/Assets/Editor/BuildBundles.cs')
$taskRecords = foreach ($taskSource in $taskSources) { @{ path = $taskSource; sha256 = Get-JakobianTextHash (Join-Path $taskRoot $taskSource) } }
@{ editor = '2019.4.18f1'; target = 'StandaloneWindows64'; sources = @($taskRecords); bundleSha256 = (Get-FileHash -LiteralPath $taskOutput).Hash.ToLowerInvariant() } | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $taskAssets 'shader-manifest.json')
Write-Output "Built and staged shader bundle. Log: $taskLog"
