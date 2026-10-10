$ErrorActionPreference = 'Stop'
$scriptRoot = $PSScriptRoot

Push-Location $scriptRoot
try {
    dotnet restore Template.slnx
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet restore failed with exit code $LASTEXITCODE"
    }

    dotnet build Template.slnx --no-restore
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet build failed with exit code $LASTEXITCODE"
    }
}
finally {
    Pop-Location
}
