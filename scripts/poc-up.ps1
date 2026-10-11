param([ValidateSet('postgres','sqlserver')][string]$Provider='postgres')
$ErrorActionPreference='Stop'
. "$PSScriptRoot/poc-infrastructure.ps1"
$namespace="vm-acme-poc-$Provider"
$evidence=Join-Path $State "$Provider/up"
try {
    $registry=Bootstrap-PocInfrastructure
    Ensure-Namespace $namespace
    $credentials=Get-PocCredentials
    $providerName=Provider-Name $Provider
    if($Provider -eq 'postgres') {
        $apiConnection="Host=postgres.$InfraNamespace.svc.cluster.local;Database=acme;Username=acme_api;Password=$($credentials.Api);Timeout=15"
        $migrationConnection="Host=postgres.$InfraNamespace.svc.cluster.local;Database=acme;Username=acme_migrator;Password=$($credentials.Migrator);Timeout=15"
    } else {
        $apiConnection="Server=sqlserver.$InfraNamespace.svc.cluster.local;Database=acme;User Id=acme_api;Password=$($credentials.Api);Encrypt=True;TrustServerCertificate=True;Connect Timeout=15"
        $migrationConnection="Server=sqlserver.$InfraNamespace.svc.cluster.local;Database=acme;User Id=acme_migrator;Password=$($credentials.Migrator);Encrypt=True;TrustServerCertificate=True;Connect Timeout=15"
    }
    Set-Secret $namespace 'vm-acme-api-db' @{connection=$apiConnection}
    Set-Secret $namespace 'vm-acme-migrator-db' @{connection=$migrationConnection}
    Set-Secret $namespace 'vm-acme-jwt' @{'signing-key'='VmAcmePocJwtSigningKey-Only-For-Local-Testing-2026'}
    $auth=[Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes("$($registry.registryUser):$($registry.registryPassword)"))
    $dockerConfig=@{auths=@{$registry.registry=@{auth=$auth}}}|ConvertTo-Json -Depth 8 -Compress
    Set-Secret $namespace 'vm-acme-harbor' @{'.dockerconfigjson'=$dockerConfig} 'kubernetes.io/dockerconfigjson'
    Invoke-PocHelm @('upgrade','--install','vm-acme-identities',$Chart,'-n',$namespace,'-f',"$Chart/values-poc.yaml",'--set','phase=prerequisites','--wait','--timeout','2m') | Out-Null
    $config=@{namespace=$namespace;provider=$Provider;providerName=$providerName;registry=$registry.registry;project=$registry.project}
    $buildId=(Get-FileHash "$Root/Dockerfile" -Algorithm SHA256).Hash.Substring(0,8).ToLowerInvariant() + '-' + (Get-Date -Format 'yyyyMMddHHmmss')
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
    Write-Host "Deploying V1 ($Provider); migrations gate API startup..."
    Deploy-Application $config "v1-$buildId" '20261010000100_InitialCreate'
    Migration-Logs $config "$evidence/migration-v1.txt" | Out-Null
    Write-Utf8 (Join-Path $State "$Provider.json") ($config|ConvertTo-Json)
    Write-Host "UP PASS provider=$Provider namespace=$namespace. Run poc-verify.sh; evidence: .poc/$Provider/up"
} catch {
    Diagnostics $namespace $evidence
    Write-Error $_.Exception.Message
    exit 1
}
