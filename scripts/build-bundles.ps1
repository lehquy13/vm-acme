param([string]$Runtime = 'linux-x64')
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$output = Join-Path $repo "artifacts/bundles/$Runtime"
$toolDirectory = Join-Path $repo 'artifacts/ef-tools'
New-Item -ItemType Directory -Force -Path $output | Out-Null
if (-not (Test-Path (Join-Path $toolDirectory 'dotnet-ef.exe')) -and -not (Test-Path (Join-Path $toolDirectory 'dotnet-ef'))) {
    dotnet tool install dotnet-ef --version 10.0.12 --tool-path $toolDirectory
    if ($LASTEXITCODE -ne 0) { throw 'Could not install pinned EF tool.' }
}
$tool = if ($env:OS -eq 'Windows_NT') { Join-Path $toolDirectory 'dotnet-ef.exe' } else { Join-Path $toolDirectory 'dotnet-ef' }
$oldConnection = $env:ConnectionStrings__DefaultConnection
$oldMode = $env:Secrets__Mode
try {
    $env:Secrets__Mode = 'Environment'
    foreach ($provider in @('PostgreSql', 'SqlServer')) {
        # Design-time model creation only; no database access or secret in bundle artifacts.
        $env:ConnectionStrings__DefaultConnection = if ($provider -eq 'PostgreSql') { 'Host=localhost;Database=bundle' } else { 'Server=localhost;Database=bundle;Integrated Security=True' }
        $project = Join-Path $repo "migrations/VmAcme.Migrations.$provider"
        dotnet restore $project --runtime $Runtime
        if ($LASTEXITCODE -ne 0) { throw "Runtime restore failed for $provider." }
        $file = Join-Path $output "efbundle-$($provider.ToLowerInvariant())"
        if ($Runtime -like 'win-*') { $file += '.exe' }
        & $tool migrations bundle --project $project --startup-project $project --self-contained --target-runtime $Runtime --configuration Release --output $file --force
        if ($LASTEXITCODE -ne 0) { throw "Bundle build failed for $provider." }
        Get-FileHash -LiteralPath $file -Algorithm SHA256 | Select-Object Hash, Path
    }
}
finally {
    $env:ConnectionStrings__DefaultConnection = $oldConnection
    $env:Secrets__Mode = $oldMode
}
