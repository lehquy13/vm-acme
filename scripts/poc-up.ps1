param([ValidateSet('postgres','sqlserver')][string]$Provider='postgres',[string]$ProofId='',[string]$DatabaseName='acme',[switch]$UseExistingInfrastructure)
$ErrorActionPreference='Stop'
. "$PSScriptRoot/poc-infrastructure.ps1"
$namespace=if($ProofId){"vm-acme-poc-$Provider-$ProofId"}else{"vm-acme-poc-$Provider"}
if($namespace -notmatch '^vm-acme-poc-[a-z0-9-]{1,40}$'){throw 'ProofId must produce a safe PoC namespace.'}
$evidence=Join-Path $State "$Provider/up"
try {
    if($UseExistingInfrastructure) {
        Assert-PocContext
        if($env:POC_REGISTRY_ENDPOINT -and $env:POC_REGISTRY_AUTHORIZED -ne 'true'){throw 'Using an external Harbor requires POC_REGISTRY_AUTHORIZED=true.'}
        $endpoint=if($env:POC_REGISTRY_ENDPOINT){$env:POC_REGISTRY_ENDPOINT}else{(((Run 'minikube' @('-p',$Profile,'ip')) -join '').Trim()+':30002')}
        $registryUser=if($env:POC_REGISTRY_USERNAME){$env:POC_REGISTRY_USERNAME}else{'admin'}
        $registryPassword=if($env:POC_REGISTRY_PASSWORD){$env:POC_REGISTRY_PASSWORD}else{(Get-PocCredentials).Harbor}
        $project=if($env:POC_REGISTRY_PROJECT){$env:POC_REGISTRY_PROJECT}else{'vm-acme-poc'}
        # Existing infrastructure mode reuses the already configured node trust;
        # it must not restart the shared Docker daemon or kubelet.
        $dockerEnvironment=(Run 'minikube' @('-p',$Profile,'docker-env','--shell','powershell')) -join "`n"
        Invoke-Expression $dockerEnvironment
        $registry=@{registry=$endpoint;project=$project;registryUser=$registryUser;registryPassword=$registryPassword}
    } else { $registry=Bootstrap-PocInfrastructure }
    if($DatabaseName -ne 'acme'){Initialize-PocDatabase $Provider $DatabaseName}
    Ensure-Namespace $namespace
    $credentials=Get-PocCredentials
    $providerName=Provider-Name $Provider
    if($Provider -eq 'postgres') {
        $apiConnection="Host=postgres.$InfraNamespace.svc.cluster.local;Database=$DatabaseName;Username=$ApiDbUser;Password=$($credentials.Api);Timeout=15"
        $migrationConnection="Host=postgres.$InfraNamespace.svc.cluster.local;Database=$DatabaseName;Username=$MigratorDbUser;Password=$($credentials.Migrator);Timeout=15"
    } else {
        $apiConnection="Server=sqlserver.$InfraNamespace.svc.cluster.local;Database=$DatabaseName;User Id=$ApiDbUser;Password=$($credentials.Api);Encrypt=True;TrustServerCertificate=True;Connect Timeout=15"
        $migrationConnection="Server=sqlserver.$InfraNamespace.svc.cluster.local;Database=$DatabaseName;User Id=$MigratorDbUser;Password=$($credentials.Migrator);Encrypt=True;TrustServerCertificate=True;Connect Timeout=15"
    }
    Set-Secret $namespace 'vm-acme-api-db' @{connection=$apiConnection}
    Set-Secret $namespace 'vm-acme-migrator-db' @{connection=$migrationConnection}
    $randomBytes=New-Object byte[] 48
    $randomGenerator=[Security.Cryptography.RandomNumberGenerator]::Create()
    try { $randomGenerator.GetBytes($randomBytes) }
    finally { $randomGenerator.Dispose() }
    $jwtSigningKey=[Convert]::ToBase64String($randomBytes)
    Set-Secret $namespace 'vm-acme-jwt' @{'signing-key'=$jwtSigningKey}
    $auth=[Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes("$($registry.registryUser):$($registry.registryPassword)"))
    $dockerConfig=@{auths=@{$registry.registry=@{auth=$auth}}}|ConvertTo-Json -Depth 8 -Compress
    Set-Secret $namespace 'vm-acme-harbor' @{'.dockerconfigjson'=$dockerConfig} 'kubernetes.io/dockerconfigjson'
    Invoke-PocHelm @('upgrade','--install','vm-acme-identities',$Chart,'-n',$namespace,'-f',"$Chart/values-poc.yaml",'--set','phase=prerequisites','--wait','--timeout','2m') | Out-Null
    $config=@{namespace=$namespace;provider=$Provider;providerName=$providerName;registry=$registry.registry;project=$registry.project;databaseName=$DatabaseName;proofId=$ProofId}
    $sourceProvenance=Get-SourceProvenance
    $buildId=$sourceProvenance.sourceFingerprint.Substring(0,12) + '-' + (Get-Date -Format 'yyyyMMddHHmmss')
    $config.sourceSha=$sourceProvenance.sourceSha
    $config.sourceDirty=$sourceProvenance.sourceDirty
    $config.sourceFingerprint=$sourceProvenance.sourceFingerprint
    $config.generatedTreeFingerprint=$sourceProvenance.generatedTreeFingerprint
    $manifestPath=Join-Path $State 'proof-manifest.json'
    if($ProofId) {
        if(-not (Test-Path -LiteralPath $manifestPath)){throw 'Proof run requires the clean-clone manifest created by scripts/poc-proof.ps1.'}
        $manifest=Get-Content -LiteralPath $manifestPath -Raw|ConvertFrom-Json
        if(-not $manifest.cleanCheckout -or $manifest.sourceSha -ne $sourceProvenance.sourceSha -or $sourceProvenance.sourceDirty -or $manifest.generatedTreeFingerprint -ne $sourceProvenance.generatedTreeFingerprint){throw 'Proof manifest does not match the clean initialized source SHA and generated tree.'}
        $config.preInitSourceSha=$manifest.preInitSourceSha
        $config.initializedName=$manifest.initializedName
        $config.proofRunId=$manifest.proofRunId
        $config.initializedTreeDirty=$false
    }
    $config.buildId=$buildId
    Write-Host "Building and publishing API and migration images ($Provider)..."
    Run 'docker' @('login',$registry.registry,'--username',$registry.registryUser,'--password-stdin') 120 $registry.registryPassword | Out-Null
    New-Item -ItemType Directory -Force -Path $evidence | Out-Null
    foreach($version in @('1','2')) {
        foreach($target in @('api','migrator')) {
            $image="$($registry.registry)/$($registry.project)/vm-acme-${target}:v${version}-$buildId"
            $build=Run 'docker' @('build','--target',$target,'--build-arg',"RELEASE_VERSION=$version.0.0",'--build-arg',"MIGRATION_RELEASE=$version",'-t',$image,$Root)
            Write-Utf8 "$evidence/build-$target-v$version.txt" ($build -join "`n")
            Write-Utf8 "$evidence/push-$target-v$version.txt" ((Run 'docker' @('push',$image)) -join "`n")
            Write-Utf8 "$evidence/pull-$target-v$version.txt" ((Run 'docker' @('pull',$image)) -join "`n")
        }
    }
    $config=[pscustomobject]$config
    $config.upCommand="scripts/poc-up.ps1 -Provider $Provider -ProofId $ProofId -DatabaseName $DatabaseName$(if($UseExistingInfrastructure){' -UseExistingInfrastructure'})"
    $config.verifyCommand="scripts/poc-verify.ps1 -Provider $Provider"
    Write-Host "Deploying V1 ($Provider); migrations gate API startup..."
    Deploy-Application $config "v1-$buildId" (Todo-MigrationTarget $Provider)
    Migration-Logs $config "$evidence/migration-v1.txt" | Out-Null
    Write-Utf8 (Join-Path $State "$Provider.json") ($config|ConvertTo-Json)
    Write-Host "UP PASS provider=$Provider namespace=$namespace database=$DatabaseName. Run poc-verify.sh; evidence: .poc/$Provider/up"
} catch {
    Diagnostics $namespace $evidence
    Write-Error $_.Exception.Message
    exit 1
}
