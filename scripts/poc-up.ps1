param([ValidateSet('postgres','sqlserver')][string]$Provider='postgres')
$ErrorActionPreference='Stop'
. "$PSScriptRoot/poc-infrastructure.ps1"
$namespace="vm-acme-poc-$Provider"
$evidence=Join-Path $State "$Provider/up"
function Get-SourceProvenance {
    $entries=New-Object 'System.Collections.Generic.List[string]'
    foreach($directory in @('src','tools','migrations')) {
        $path=Join-Path $Root $directory
        if(Test-Path -LiteralPath $path) {
            foreach($file in Get-ChildItem -LiteralPath $path -Recurse -File) {
                $relative=$file.FullName.Substring($Root.Length).TrimStart('\','/').Replace('\','/')
                if($relative -match '(^|/)(bin|obj|\.git|generated|testresults|node_modules)/'){continue}
                $hash=(Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
                $entries.Add("$relative`t$hash")
            }
        }
    }
    foreach($name in @('Dockerfile','.dockerignore','Directory.Build.props')) {
        $path=Join-Path $Root $name
        if(Test-Path -LiteralPath $path) {
            $hash=(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
            $entries.Add("$name`t$hash")
        }
    }
    [string[]]$sorted=$entries.ToArray()
    [Array]::Sort($sorted,[StringComparer]::Ordinal)
    $canonical=[Text.Encoding]::UTF8.GetBytes(($sorted -join "`n"))
    $digest=[Security.Cryptography.SHA256]::Create()
    try { $sha=[BitConverter]::ToString($digest.ComputeHash($canonical)).Replace('-','').ToLowerInvariant() }
    finally { $digest.Dispose() }
    $status=@(Run 'git' @('status','--porcelain','--untracked-files=all','--','src','tools','migrations','Dockerfile','.dockerignore','Directory.Build.props'))
    return [pscustomobject]@{
        sourceSha=((Run 'git' @('rev-parse','HEAD')) -join '').Trim()
        sourceDirty=($status.Count -gt 0)
        sourceFingerprint=$sha
    }
}
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
    $config=@{namespace=$namespace;provider=$Provider;providerName=$providerName;registry=$registry.registry;project=$registry.project}
    $sourceProvenance=Get-SourceProvenance
    $buildId=$sourceProvenance.sourceFingerprint.Substring(0,12) + '-' + (Get-Date -Format 'yyyyMMddHHmmss')
    $config.sourceSha=$sourceProvenance.sourceSha
    $config.sourceDirty=$sourceProvenance.sourceDirty
    $config.sourceFingerprint=$sourceProvenance.sourceFingerprint
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
    Deploy-Application $config "v1-$buildId" (Todo-MigrationTarget $Provider)
    Migration-Logs $config "$evidence/migration-v1.txt" | Out-Null
    Write-Utf8 (Join-Path $State "$Provider.json") ($config|ConvertTo-Json)
    Write-Host "UP PASS provider=$Provider namespace=$namespace. Run poc-verify.sh; evidence: .poc/$Provider/up"
} catch {
    Diagnostics $namespace $evidence
    Write-Error $_.Exception.Message
    exit 1
}
