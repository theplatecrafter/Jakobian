param([ValidateSet('Debug','Release')][string]$Configuration = 'Release', [switch]$SkipDeploy)
$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path $PSScriptRoot -Parent
Push-Location $taskRoot
try {
    & python (Join-Path $PSScriptRoot 'Prepare-Runtime.py')
    if ($LASTEXITCODE -ne 0) { throw 'Runtime dependency preparation failed.' }
    & dotnet restore Jakobian.sln --locked-mode
    if ($LASTEXITCODE -ne 0) { throw 'Restore failed.' }
    & dotnet build Jakobian.sln -c $Configuration --no-restore
    if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
    & dotnet run --project tests/Jakobian.Core.Tests -c $Configuration --no-build
    if ($LASTEXITCODE -ne 0) { throw 'Analytic checks failed.' }
    if ($Configuration -eq 'Release' -and -not $SkipDeploy) { & (Join-Path $PSScriptRoot 'Deploy.ps1') -Instance baseline }
} finally { Pop-Location }
