param([Parameter(Mandatory)][ValidatePattern('^[a-z][a-z0-9-]{0,40}$')][string]$Name)
$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path $PSScriptRoot -Parent
$taskInstances = [IO.Path]::GetFullPath((Join-Path $taskRoot 'instances'))
$taskTarget = [IO.Path]::GetFullPath((Join-Path $taskInstances $Name))
if (-not $taskTarget.StartsWith($taskInstances + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Instance escapes workspace.' }
if (Test-Path -LiteralPath $taskTarget) { throw 'Instance exists; choose a new name.' }
$taskTemplate = Join-Path $taskRoot 'template_instance'
if (-not (Test-Path -LiteralPath (Join-Path $taskTemplate 'KSP_x64.exe'))) { throw 'KSP template missing.' }
New-Item -ItemType Directory -Path $taskTarget | Out-Null
& robocopy $taskTemplate $taskTarget /E /R:1 /W:1 /NFL /NDL /NJH /NJS /NP /XD (Join-Path $taskTemplate 'saves') /XF KSP.log Player.log
if ($LASTEXITCODE -ge 8) { throw "Instance copy failed: robocopy exit $LASTEXITCODE" }
New-Item -ItemType Directory -Path (Join-Path $taskTarget 'saves') -Force | Out-Null
@{ owner = 'Jakobian'; name = $Name; template = $taskTemplate } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $taskTarget '.jakobian-instance.json')
Write-Output "Created independent game copy: $taskTarget"
