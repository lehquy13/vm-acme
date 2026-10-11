param([ValidateSet('postgres','sqlserver')][string]$Provider='postgres')
$ErrorActionPreference='Stop'
. "$PSScriptRoot/poc-common.ps1"
$config=Get-Config $Provider
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
$currentSource=Get-SourceProvenance
if(-not $config.sourceFingerprint -or $config.sourceFingerprint -ne $currentSource.sourceFingerprint) {
    throw 'PoC config source fingerprint does not match current product inputs; rerun scripts/poc-up.sh before verification.'
}
$run=Get-Date -Format 'yyyyMMdd-HHmmss'
$evidence=Join-Path $State "$Provider/verify-$run"
New-Item -ItemType Directory -Force -Path $evidence | Out-Null
$forward=$null
$gates=New-Object 'System.Collections.Generic.List[string]'
function Assert-Gate([bool]$Condition,[string]$Name) {
    if(-not $Condition){ throw "Verification failed: $Name. Evidence: $evidence" }
    $gates.Add($Name)
    Write-Host "PASS $Name"
}
function Read-History {
    $sql=if($Provider -eq 'postgres'){'SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY "MigrationId";'}else{'SET NOCOUNT ON; SELECT MigrationId FROM __EFMigrationsHistory ORDER BY MigrationId;'}
    Database-Query $Provider $sql
}
function Api-Items { @(Invoke-RestMethod "$uri/api/todos" -Headers $apiHeaders -TimeoutSec 15) }
function Assert-Item([string]$Id,[string]$Title) {
    $item=Api-Items | Where-Object {$_.id -eq $Id}
    Assert-Gate ($item -and $item.description -eq $Title) 'persisted Todo description and generated identifier'
}
try {
    Assert-PocContext
    Deploy-Application $config "v1-$($config.buildId)" (Todo-MigrationTarget $Provider)
    $forward=Start-Forward $config.namespace 'vm-acme' 18080 8080
    $uri='http://127.0.0.1:18080'
    Wait-Http "$uri/ready" 120 | Out-Null
    $apiHeaders=@{Authorization="Bearer $(New-PocApiToken $uri)"}
    $version=Invoke-RestMethod "$uri/version" -TimeoutSec 15
    Assert-Gate ($version.version -like '1.0.0*') 'V1 API image deployed'
    $before=Read-History
    $schemaUpgradeExecuted=$before -notmatch '20261011000100_AddDeploymentRevision'
    Write-Utf8 "$evidence/history-v1.txt" $before
    $title="PoC $Provider $([guid]::NewGuid().ToString('N'))"
    $created=Invoke-RestMethod "$uri/api/todos" -Method Post -Headers $apiHeaders -ContentType 'application/json' -Body (@{description=$title}|ConvertTo-Json) -TimeoutSec 15
    Assert-Gate ($created.id -and -not $created.isCompleted) 'create Todo in V1'
    # Immutable historical revision migration still targets WorkItems. Verify it with a separate legacy fixture.
    $legacyId=[guid]::NewGuid().ToString()
    $legacyInsert=if($Provider -eq 'postgres'){'INSERT INTO "WorkItems" ("Id", "Title", "Status") VALUES ('''+$legacyId+''', ''Legacy sample'', ''Open'');'}else{'INSERT INTO WorkItems (Id, Title, Status) VALUES ('''+$legacyId+''', ''Legacy sample'', ''Open'');'}
    Database-Query $Provider $legacyInsert | Out-Null
    Write-Utf8 "$evidence/sample-created.json" ($created|ConvertTo-Json)
    Assert-Item $created.id $title
    Stop-Forward $forward; $forward=$null
    K @('-n',$config.namespace,'rollout','restart','deployment/vm-acme') | Out-Null
    K @('-n',$config.namespace,'rollout','status','deployment/vm-acme','--timeout=180s') | Out-Null
    $forward=Start-Forward $config.namespace 'vm-acme' 18080 8080
    Wait-Http "$uri/ready" 120 | Out-Null
    Assert-Item $created.id $title
    Stop-Forward $forward; $forward=$null
    Deploy-Application $config "v2-$($config.buildId)" ''
    $migration=Migration-Logs $config "$evidence/migration-v2.txt"
    if($schemaUpgradeExecuted){ Assert-Gate ($migration -match 'APPLIED_THIS_RUN=1') 'V2 artifact applies one pending upgrade migration' }
    $after=Read-History
    Write-Utf8 "$evidence/history-v2.txt" $after
    Assert-Gate ($after -match '20261010000100_InitialCreate' -and $after -match '20261011000100_AddDeploymentRevision') 'V2 migration history recorded'
    $sql=if($Provider -eq 'postgres'){
        'SELECT "DeploymentRevision" FROM "WorkItems" WHERE "Id" = '''+$legacyId+''';'
    }else{'SET NOCOUNT ON; SELECT DeploymentRevision FROM WorkItems WHERE Id = '''+$legacyId+''';'}
    $revision=Database-Query $Provider $sql
    Write-Utf8 "$evidence/backfill.txt" $revision
    $revisionGate=if($schemaUpgradeExecuted){'upgrade backfill preserves existing V1 row'}else{'existing V2 schema retained on rerun; V1 row has expected metadata'}
    Assert-Gate ($revision -eq 'v2') $revisionGate
    $forward=Start-Forward $config.namespace 'vm-acme' 18080 8080
    Wait-Http "$uri/ready" 120 | Out-Null
    $version=Invoke-RestMethod "$uri/version" -TimeoutSec 15
    Assert-Gate ($version.version -like '2.0.0*') 'V2 API image Ready'
    Assert-Item $created.id $title
    $v2created=Invoke-RestMethod "$uri/api/todos" -Method Post -Headers $apiHeaders -ContentType 'application/json' -Body (@{description="V2 $title"}|ConvertTo-Json) -TimeoutSec 15
    Assert-Gate ($v2created.id -and -not $v2created.isCompleted) 'create Todo in V2'
    $v2legacyId=[guid]::NewGuid().ToString()
    $v2insert=if($Provider -eq 'postgres'){'INSERT INTO "WorkItems" ("Id", "Title", "Status") VALUES ('''+$v2legacyId+''', ''Legacy V2'', ''Open'');'}else{'INSERT INTO WorkItems (Id, Title, Status) VALUES ('''+$v2legacyId+''', ''Legacy V2'', ''Open'');'}
    Database-Query $Provider $v2insert | Out-Null
    $v2sql=if($Provider -eq 'postgres'){'SELECT "DeploymentRevision" FROM "WorkItems" WHERE "Id" = '''+$v2legacyId+''';'}else{'SET NOCOUNT ON; SELECT DeploymentRevision FROM WorkItems WHERE Id = '''+$v2legacyId+''';'}
    Assert-Gate ((Database-Query $Provider $v2sql) -eq 'v2') 'new V2 inserts use upgraded column default'
    $completed=Invoke-RestMethod "$uri/api/todos/$($created.id)/complete" -Method Post -Headers $apiHeaders -TimeoutSec 15
    Assert-Gate $completed.isCompleted 'complete Todo on real database'
    $badStatus=0
    try { Invoke-RestMethod "$uri/api/todos" -Method Post -Headers $apiHeaders -ContentType 'application/json' -Body '{"description":""}' -TimeoutSec 15 | Out-Null } catch {
        if($_.Exception.Response){$badStatus=[int]$_.Exception.Response.StatusCode}
    }
    Assert-Gate ($badStatus -eq 400) 'invalid input rejected'
    $metrics=(Invoke-WebRequest "$uri/metrics" -UseBasicParsing -TimeoutSec 15).Content
    Write-Utf8 "$evidence/metrics.prom" $metrics
    Assert-Gate ($metrics -match 'vm_acme_http_requests_total [1-9]') 'emitted Prometheus counter inspected'
    Stop-Forward $forward; $forward=$null
    Deploy-Application $config "v2-$($config.buildId)" ''
    $rerun=Migration-Logs $config "$evidence/migration-rerun.txt"
    Assert-Gate ((Read-History) -eq $after -and $rerun -match 'APPLIED_THIS_RUN=0') 'repeat deployment applies zero completed migrations'
    $deployment=(K @('-n',$config.namespace,'get','deployment','vm-acme','-o','json')) -join "`n" | ConvertFrom-Json
    $templateBefore=$deployment.spec.template | ConvertTo-Json -Depth 30 -Compress
    $generationBefore=$deployment.metadata.generation
    $failed=$false
    try { Deploy-Application $config "v1-$($config.buildId)" '20261012000100_PocFailure' $true "v2-$($config.buildId)" } catch {$failed=$true}
    Assert-Gate $failed 'actual failing migration prevents Helm upgrade'
    $failureLog=Migration-Logs $config "$evidence/migration-expected-failure.txt"
    $exception=if($Provider -eq 'postgres'){'PostgresException'}else{'SqlException'}
    Assert-Gate ($failureLog -match $exception) 'failure is a database SQL exception'
    $job=(K @('-n',$config.namespace,'get','job','vm-acme-migrate','-o','json')) -join "`n" | ConvertFrom-Json
    Assert-Gate ($job.status.failed -eq 1) 'migration Job failed as expected'
    $deployment=(K @('-n',$config.namespace,'get','deployment','vm-acme','-o','json')) -join "`n" | ConvertFrom-Json
    $templateAfter=$deployment.spec.template | ConvertTo-Json -Depth 30 -Compress
    Assert-Gate ($templateAfter -eq $templateBefore -and $deployment.metadata.generation -eq $generationBefore) 'failed migration leaves API rollout unchanged'
    Assert-Gate ((Read-History) -eq $after) 'failed transaction leaves migration history unchanged'
    $rollbackSql=if($Provider -eq 'postgres'){"SELECT COUNT(*) FROM information_schema.tables WHERE table_name = 'vmacmepocrollbackprobe';"}else{"SET NOCOUNT ON; SELECT COUNT(*) FROM sys.tables WHERE name = 'VmAcmePocRollbackProbe';"}
    Assert-Gate ((Database-Query $Provider $rollbackSql).Trim() -eq '0') 'failed EF migration rolls back its schema change'
    Deploy-Application $config "v2-$($config.buildId)" ''
    Migration-Logs $config "$evidence/migration-recovery.txt" | Out-Null
    $forward=Start-Forward $config.namespace 'vm-acme' 18080 8080
    Wait-Http "$uri/ready" 120 | Out-Null
    Assert-Item $created.id $title
    $items=Api-Items
    Assert-Gate (($items|Where-Object{$_.id -eq $created.id}).isCompleted) 'completed Todo survives recovery'
    $podJson=(K @('-n',$config.namespace,'get','pods','-l','app.kubernetes.io/instance=vm-acme','-o','json')) -join "`n"
    Write-Utf8 "$evidence/pods.json" $podJson
    $pods=$podJson|ConvertFrom-Json
    $imageIds=@($pods.items|ForEach-Object{$_.status.containerStatuses}|ForEach-Object{$_.imageID})
    Assert-Gate ($imageIds.Count -gt 0 -and ($imageIds -join '') -match 'sha256:') 'Kubernetes node pulled published API image'
    Assert-Gate ($deployment.spec.template.spec.containers[0].image -eq "$($config.registry)/$($config.project)/vm-acme-api:v2-$($config.buildId)") 'API uses the published V2 release artifact'
    $jobPods=(K @('-n',$config.namespace,'get','pods','-l','job-name=vm-acme-migrate','-o','json')) -join "`n" | ConvertFrom-Json
    $migrationImageIds=@($jobPods.items|ForEach-Object{$_.status.containerStatuses}|ForEach-Object{$_.imageID})
    Assert-Gate (($migrationImageIds -join '') -match 'sha256:') 'Kubernetes node pulled published migration image'
    Assert-Gate ($deployment.spec.template.spec.serviceAccountName -ne $job.spec.template.spec.serviceAccountName) 'application and migration identities differ'
    $logs=(K @('-n',$config.namespace,'logs','deployment/vm-acme','--tail=100')) -join "`n"
    Write-Utf8 "$evidence/api.log" $logs
    Assert-Gate ($logs -match 'HTTP') 'emitted API request logs inspected'
    Diagnostics $config.namespace $evidence
    $report=@{status='PASS';provider=$Provider;schemaUpgradeExecuted=$schemaUpgradeExecuted;mechanism='Helm pre-install/pre-upgrade migration Job (Argo CD not executed)';sourceSha=$config.sourceSha;sourceDirty=$config.sourceDirty;sourceFingerprint=$config.sourceFingerprint;gates=$gates.ToArray();evidence=$evidence;initialHistory=$before;finalHistory=$after;apiImageIds=$imageIds;migratorImageIds=$migrationImageIds;timeUtc=[DateTime]::UtcNow.ToString('o')}
    Write-Utf8 "$evidence/report.json" ($report|ConvertTo-Json -Depth 12)
    Write-Utf8 (Join-Path $State "$Provider/latest-report.txt") $evidence
    Write-Host "VERIFY PASS provider=$Provider gates=$($gates.Count) evidence=$evidence"
} catch {
    Diagnostics $config.namespace $evidence
    Write-Utf8 "$evidence/report.json" (@{status='FAIL';provider=$Provider;passedGates=$gates.ToArray();error=$_.Exception.Message;timeUtc=[DateTime]::UtcNow.ToString('o')}|ConvertTo-Json -Depth 12)
    Write-Error $_.Exception.Message
    exit 1
} finally { Stop-Forward $forward }
