$ErrorActionPreference = 'Stop'
$project = Join-Path (Split-Path $PSScriptRoot -Parent) 'src/VmAcme.WebApi/VmAcme.WebApi.csproj'
$bytes = New-Object byte[] 48
$rng = [Security.Cryptography.RandomNumberGenerator]::Create()
try { $rng.GetBytes($bytes) } finally { $rng.Dispose() }
$key = [Convert]::ToBase64String($bytes)
dotnet user-secrets set 'Authentication:Jwt:SigningKey' $key --project $project | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'Local secret setup failed.' }
Write-Host 'Generated development signing key in .NET user secrets.'
