param(
    [Parameter(Mandatory)] [string]$RepositoryName,
    [Parameter(Mandatory)] [string]$RepositoryId
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
Import-Module (Join-Path $PSScriptRoot 'TemplateNaming.psm1') -Force

if ($RepositoryId -notmatch '^\d+$') { throw 'Repository ID must be numeric.' }
if ($RepositoryId -eq '1414443367' -or $RepositoryName -ieq 'vm-acme' -and $env:GITHUB_REPOSITORY -ieq 'lehquy13/vm-acme') {
    Write-Output 'Original template repository detected; bootstrap skipped.'
    exit 0
}

$markerPath = Join-Path $repositoryRoot '.template-initialized'
if (Test-Path -LiteralPath $markerPath) {
    Write-Output 'This repository has already been initialized; no changes made.'
    exit 0
}

# Validate all repository-derived names and template preconditions before invoking the mutating initializer.
$names = Get-TemplateNames $RepositoryName
$solutionPath = Join-Path $repositoryRoot 'vm-acme.slnx'
if (-not (Test-Path -LiteralPath $solutionPath)) {
    throw 'The template solution is missing; refusing to initialize a non-template checkout.'
}

Push-Location $repositoryRoot
try {
    & (Join-Path $repositoryRoot 'init.ps1') -Name $RepositoryName
    if ($LASTEXITCODE -and $LASTEXITCODE -ne 0) { throw "Initializer failed with exit code $LASTEXITCODE" }

    dotnet test $names.SolutionFile --no-build
    if ($LASTEXITCODE -ne 0) { throw "dotnet test failed with exit code $LASTEXITCODE" }

    $chartPath = Join-Path $repositoryRoot "deploy/helm/$($names.DeploymentName)"
    if (-not (Test-Path -LiteralPath $chartPath)) { throw "Expected Helm chart not found: $chartPath" }
    $helmOverrides = @(
        '--set-string', "image.repository=validation.invalid/$($names.DeploymentName)/api",
        '--set-string', 'image.tag=bootstrap-validation',
        '--set-string', "migration.image.repository=validation.invalid/$($names.DeploymentName)/migrator",
        '--set-string', 'migration.image.tag=bootstrap-validation',
        '--set', 'persistence.provider=PostgreSql',
        '--set', 'secrets.mode=AppSettings'
    )
    helm lint $chartPath -f (Join-Path $chartPath 'values-poc.yaml') @helmOverrides
    if ($LASTEXITCODE -ne 0) { throw "helm lint failed with exit code $LASTEXITCODE" }
    helm template $names.DeploymentName $chartPath -f (Join-Path $chartPath 'values-poc.yaml') @helmOverrides | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "helm template failed with exit code $LASTEXITCODE" }

    $marker = [ordered]@{
        repositoryId = $RepositoryId
        repositoryName = $RepositoryName
        initializedAt = [DateTime]::UtcNow.ToString('o')
    } | ConvertTo-Json
    [IO.File]::WriteAllText($markerPath, $marker + [Environment]::NewLine, [Text.UTF8Encoding]::new($false))
}
finally {
    Pop-Location
}
