param([Parameter(Mandatory)][string]$CloneRoot,[ValidateSet('postgres','sqlserver')][string]$Provider='postgres')
$ErrorActionPreference='Stop'
$authoringRoot=Split-Path $PSScriptRoot -Parent
. (Join-Path $authoringRoot 'scripts/poc-common.ps1')
Assert-PocContext
$config=Get-Content (Join-Path $CloneRoot ".poc/$Provider.json") -Raw|ConvertFrom-Json
$brand=$config.initializedName
if($brand -notin @('vm-orders','vm-order-service')){throw 'This proof expects one of the two clean initialized PoC names.'}
$chart=Join-Path $CloneRoot "deploy/helm/$brand"
$evidence=Join-Path $State "secret-files-$brand-$Provider"
New-Item -ItemType Directory -Force $evidence|Out-Null
function Read-PocSecret([string]$Name,[string]$Key){
    $secret=((K @('-n',$config.namespace,'get','secret',$Name,'-o','json'))-join "`n")|ConvertFrom-Json
    return [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($secret.data.$Key))
}
$apiConnection=Read-PocSecret "$brand-api-db" 'connection'
$migrationConnection=Read-PocSecret "$brand-migrator-db" 'connection'
$signing=Read-PocSecret "$brand-jwt" 'signing-key'
$apiFile=@{ConnectionStrings=@{DefaultConnection=$apiConnection};Authentication=@{Jwt=@{SigningKey=$signing}}}|ConvertTo-Json -Depth 6
$migrationFile=@{ConnectionStrings=@{DefaultConnection=$migrationConnection}}|ConvertTo-Json -Depth 6
Set-Secret $config.namespace "$brand-api-file-proof" @{'appsettings.secrets.json'=$apiFile}
Set-Secret $config.namespace "$brand-migration-file-proof" @{'appsettings.secrets.json'=$migrationFile}
$results=New-Object 'System.Collections.Generic.List[object]'
foreach($mode in @('AppSettings','KeyVaultWithFallback','KeyVaultRequired')){
    $suffix=if($mode -eq 'AppSettings'){'settings'}elseif($mode -eq 'KeyVaultRequired'){'required'}else{'fallback'}
    $release="$brand-file-$suffix"
    $args=@('upgrade','--install',$release,$chart,'-n',$config.namespace,'-f',"$chart/values-poc.yaml",'--set-string',"fullnameOverride=$release",
        '--set-string',"persistence.provider=$($config.providerName)",'--set-string',"secrets.mode=$mode",
        '--set-string',"application.fallbackSecretName=$brand-api-file-proof",'--set-string',"migration.fallbackSecretName=$brand-migration-file-proof",
        '--set-string',"image.repository=$($config.registry)/$($config.project)/$brand-api",'--set-string',"image.tag=v2-$($config.buildId)",
        '--set-string',"migration.image.repository=$($config.registry)/$($config.project)/$brand-migrator",'--set-string',"migration.image.tag=v2-$($config.buildId)")
    if($mode -ne 'AppSettings'){
        $args+=@('--set-string','secrets.vaultUri=https://unavailable.vault.azure.net/','--set-string','application.vaultConnectionSecret=unavailable-api-db',
            '--set-string','application.vaultJwtSecret=unavailable-jwt','--set-string','migration.vaultConnectionSecret=unavailable-migration-db',
            '--set-string','application.podLabels.azure\.workload\.identity/use=true','--set-string','migration.podLabels.azure\.workload\.identity/use=true',
            '--set-string','application.serviceAccountAnnotations.azure\.workload\.identity/client-id=00000000-0000-0000-0000-000000000001',
            '--set-string','migration.serviceAccountAnnotations.azure\.workload\.identity/client-id=00000000-0000-0000-0000-000000000002')
    }
    if($mode -eq 'KeyVaultRequired'){
        $failed=$false
        try{Invoke-PocHelm ($args+@('--wait','--timeout','2m'))|Out-Null}catch{$failed=$true}
        if(-not $failed){throw 'Required mode unexpectedly accepted unavailable workload identity.'}
        $job=((K @('-n',$config.namespace,'get','job',"$release-migrate",'-o','json'))-join "`n")|ConvertFrom-Json
        if($job.status.failed -ne 1){throw 'Required mode migration did not fail.'}
        $deployment=(K @('-n',$config.namespace,'get','deployment',$release,'--ignore-not-found','-o','name'))-join ''
        if($deployment){throw 'Required mode failure did not block API deployment.'}
        $log=(K @('-n',$config.namespace,'logs',"job/$release-migrate"))-join "`n"
        if($log -match 'SECRET_FALLBACK_ACTIVE=1'){throw 'Required mode activated fallback.'}
        Write-Utf8 (Join-Path $evidence "$mode-migration.log") $log
        $results.Add(@{mode=$mode;status='PASS';expectedMigrationFailure=$true;blockedApi=$true;fallbackActivated=$false})
        Invoke-PocHelm @('uninstall',$release,'-n',$config.namespace)|Out-Null
        continue
    }
    Invoke-PocHelm ($args+@('--wait','--timeout','3m'))|Out-Null
    $deployment=((K @('-n',$config.namespace,'get','deployment',$release,'-o','json'))-join "`n")|ConvertFrom-Json
    $job=((K @('-n',$config.namespace,'get','job',"$release-migrate",'-o','json'))-join "`n")|ConvertFrom-Json
    $mount=$deployment.spec.template.spec.containers[0].volumeMounts|Where-Object name -eq 'fallback-secrets'
    if(-not $mount.readOnly){throw 'Fallback file was not mounted read-only.'}
    if(@($job.spec.template.spec.containers[0].env|Where-Object name -match 'Jwt').Count){throw 'Migrator requests JWT configuration.'}
    $apiLog=(K @('-n',$config.namespace,'logs',"deployment/$release"))-join "`n"
    $migrationLog=(K @('-n',$config.namespace,'logs',"job/$release-migrate"))-join "`n"
    foreach($secretValue in @($apiConnection,$migrationConnection,$signing)+(Get-PocCredentials).Values){
        if($secretValue -and ($apiLog+$migrationLog).Contains($secretValue)){throw 'Secret values were found in executable logs.'}
    }
    Write-Utf8 (Join-Path $evidence "$mode-api.log") $apiLog
    Write-Utf8 (Join-Path $evidence "$mode-migration.log") $migrationLog
    $forward=Start-Forward $config.namespace $release 18082 8080
    try{
        Wait-Http 'http://127.0.0.1:18082/ready' 60|Out-Null
        $metrics=Invoke-WebRequest 'http://127.0.0.1:18082/metrics' -UseBasicParsing -TimeoutSec 15
        $expected=if($mode -eq 'AppSettings'){'0'}else{'1'}
        if($metrics.Content -notmatch "secret_fallback_active $expected"){throw 'API fallback indicator is incorrect.'}
        if($mode -eq 'KeyVaultWithFallback' -and ($apiLog -notmatch 'SECRET_FALLBACK_ACTIVE=1' -or $migrationLog -notmatch 'SECRET_FALLBACK_ACTIVE=1')){throw 'Observable sanitized fallback warning missing.'}
        Write-Utf8 (Join-Path $evidence "$mode-metrics.txt") $metrics.Content
    }finally{Stop-Forward $forward}
    $results.Add(@{mode=$mode;status='PASS';readOnlyMount=$true;noJwtForMigration=$true;logsRedacted=$true;apiImage=$deployment.spec.template.spec.containers[0].image;migrationImage=$job.spec.template.spec.containers[0].image})
    Invoke-PocHelm @('uninstall',$release,'-n',$config.namespace)|Out-Null
}
$report=@{status='PASS';sourceSha=$config.sourceSha;preInitSourceSHA=$config.preInitSourceSha;sourceFingerprint=$config.sourceFingerprint;namespace=$config.namespace;provider=$Provider;results=$results.ToArray();vaultFailure='Missing workload-identity configuration; no live cloud federation attempted';timeUtc=[DateTime]::UtcNow.ToString('o')}
Write-Utf8 (Join-Path $evidence 'report.json') ($report|ConvertTo-Json -Depth 10)
Write-Host "PASS Kubernetes explicit secret files and fallback: $brand/$Provider; evidence=$evidence"
