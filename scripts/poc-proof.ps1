param([string]$Workspace=(Join-Path (Split-Path $PSScriptRoot -Parent) '.poc/proof-workspaces'))
$ErrorActionPreference='Stop'
. "$PSScriptRoot/poc-common.ps1"

# Source-template authoring harness for the two required naming cases; run from the original template only.
# Proof starts from a committed, clean source tree. Workspaces are kept for audit.
$root=$Root
$status=@(Run 'git' @('-C',$root,'status','--porcelain','--untracked-files=all'))
if($status.Count){throw 'Proof requires a clean source checkout. Commit or discard changes before running scripts/poc-proof.ps1.'}
$preInitSourceSha=((Run 'git' @('-C',$root,'rev-parse','HEAD')) -join '').Trim()
$baseCredentials=Get-PocCredentials
$env:POC_PROFILE='vm-acme-poc'
$env:POC_INFRA_NAMESPACE='vm-acme-poc-infra'
$env:POC_REGISTRY_NAMESPACE='vm-acme-poc-registry'
$env:POC_TOOLS_PATH=(Join-Path $env:LOCALAPPDATA 'vm-acme-poc/bin')
$env:POC_API_DB_USER='acme_api'
$env:POC_MIGRATOR_DB_USER='acme_migrator'
$env:POC_API_DB_PASSWORD=$baseCredentials.Api
$env:POC_MIGRATOR_DB_PASSWORD=$baseCredentials.Migrator
$env:POC_SQL_ADMIN_PASSWORD=$baseCredentials.Sa
$env:POC_HARBOR_PASSWORD=$baseCredentials.Harbor
$env:POC_REGISTRY_PROJECT=if($env:POC_REGISTRY_PROJECT){$env:POC_REGISTRY_PROJECT}else{'vm-acme-poc'}
$env:POC_REGISTRY_USERNAME=if($env:POC_REGISTRY_USERNAME){$env:POC_REGISTRY_USERNAME}else{'admin'}
$workspacePath=[IO.Path]::GetFullPath($Workspace)
New-Item -ItemType Directory -Force -Path $workspacePath | Out-Null
$matrix=New-Object 'System.Collections.Generic.List[object]'
$runId=[guid]::NewGuid().ToString('N').Substring(0,8)

function Invoke-ProofCommand([string]$Directory,[string[]]$Arguments) {
    Push-Location $Directory
    try {
        & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $Arguments[0] @($Arguments | Select-Object -Skip 1)
        if($LASTEXITCODE -ne 0){throw "Proof command failed ($LASTEXITCODE): $($Arguments -join ' ')"}
    } finally { Pop-Location }
}

foreach($project in @('orders','order-service')) {
    $name="vm-$project"
    $deploymentName=$name
    $clone=Join-Path $workspacePath $deploymentName
    if(Test-Path -LiteralPath $clone){throw "Refusing to overwrite existing proof clone: $clone"}
    Run 'git' @('-C',$root,'clone','--no-hardlinks','--no-checkout','.',$clone) | Out-Null
    Run 'git' @('-C',$clone,'checkout','--detach',$preInitSourceSha) | Out-Null
    $cloneStatus=@(Run 'git' @('-C',$clone,'status','--porcelain','--untracked-files=all'))
    if($cloneStatus.Count){throw "New proof clone is not clean: $clone"}
    Invoke-ProofCommand $clone @('init.ps1',$name)
    $validationDirectory=Join-Path $clone '.poc/proof-validation'
    New-Item -ItemType Directory -Force -Path $validationDirectory|Out-Null
    Push-Location $clone
    try {
        $solution=Get-ChildItem -LiteralPath $clone -Filter '*.slnx' -File|Select-Object -First 1
        $testOutput=Run 'dotnet' @('test',$solution.Name,'--no-restore') 1800
        Write-Utf8 (Join-Path $validationDirectory 'dotnet-test.txt') ($testOutput -join "`n")
        $chart=Join-Path $clone "deploy/helm/$deploymentName"
        foreach($providerCase in @(@{key='postgres';name='PostgreSql'},@{key='sqlserver';name='SqlServer'})) {
            $suffix=$providerCase.key
            $imageRegistry=if($env:POC_REGISTRY_ENDPOINT){$env:POC_REGISTRY_ENDPOINT}else{'127.0.0.1:30002'}
            $imageProject=$env:POC_REGISTRY_PROJECT
            $apiImage="$imageRegistry/$imageProject/$deploymentName-api"
            $migrationImage="$imageRegistry/$imageProject/$deploymentName-migrator"
            $common=@('-f',"$chart/values-poc.yaml",'--set-string',"persistence.provider=$($providerCase.name)",'--set-string',"image.repository=$apiImage",'--set-string','image.tag=proof-validation','--set-string',"migration.image.repository=$migrationImage",'--set-string','migration.image.tag=proof-validation')
            Write-Utf8 (Join-Path $validationDirectory "helm-lint-$suffix.txt") ((Run 'helm' (@('lint',$chart)+$common)) -join "`n")
            Write-Utf8 (Join-Path $validationDirectory "helm-render-$suffix.yaml") ((Run 'helm' (@('template',$deploymentName,$chart,'-n',"$($deploymentName)-poc-$suffix-proof")+$common)) -join "`n")
        }
    } finally { Pop-Location }
    Run 'git' @('-C',$clone,'config','user.name','VM Acme Proof Bot') | Out-Null
    Run 'git' @('-C',$clone,'config','user.email','vm-acme-proof@localhost') | Out-Null
    Run 'git' @('-C',$clone,'add','-A') | Out-Null
    Run 'git' @('-C',$clone,'commit','-m',"chore: initialize $name proof checkout") | Out-Null
    $generatedSourceSha=((Run 'git' @('-C',$clone,'rev-parse','HEAD')) -join '').Trim()
    $initializedStatus=@(Run 'git' @('-C',$clone,'status','--porcelain','--untracked-files=all'))
    if($initializedStatus.Count){throw "Initialized proof checkout is not clean: $clone"}
    $cloneRoot=$clone
    $entries=New-Object 'System.Collections.Generic.List[string]'
    foreach($file in Get-ChildItem -LiteralPath $cloneRoot -Recurse -File) {
        $relative=$file.FullName.Substring($cloneRoot.Length).TrimStart('\','/').Replace('\','/')
        if($relative -match '(^|/)(\.git|\.poc|\.codegraph|\.idea|\.superpowers|bin|obj|artifacts|generated|testresults|node_modules)(/|$)'){continue}
        $hash=(Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
        $entries.Add("$relative`t$hash")
    }
    [string[]]$sorted=$entries.ToArray();[Array]::Sort($sorted,[StringComparer]::Ordinal)
    $shaAlg=[Security.Cryptography.SHA256]::Create()
    try{$treeFingerprint=[BitConverter]::ToString($shaAlg.ComputeHash([Text.Encoding]::UTF8.GetBytes(($sorted -join "`n")))).Replace('-','').ToLowerInvariant()}finally{$shaAlg.Dispose()}
    $pocDirectory=Join-Path $cloneRoot '.poc';New-Item -ItemType Directory -Force -Path $pocDirectory|Out-Null
    Write-Utf8 (Join-Path $pocDirectory 'proof-manifest.json') (@{sourceSha=$generatedSourceSha;preInitSourceSha=$preInitSourceSha;cleanCheckout=$true;initializedName=$name;proofRunId=$runId;generatedTreeFingerprint=$treeFingerprint;profile=$env:POC_PROFILE;infraNamespace=$env:POC_INFRA_NAMESPACE;registryNamespace=$env:POC_REGISTRY_NAMESPACE;scenarioMatrix=@('fresh-v1-to-v2','preexisting-v2-rerun','failure-blocked-rollout-recovery')}|ConvertTo-Json -Depth 8)
    foreach($provider in @('postgres','sqlserver')) {
        $id="$project-$runId"
        $databaseSlug=$project.Replace('-','_')
        $providerSlug=if($provider -eq 'postgres'){'pg'}else{'ss'}
        $db="p_${databaseSlug}_${providerSlug}_$runId"
        Invoke-ProofCommand $clone @('scripts/poc-up.ps1','-Provider',$provider,'-ProofId',$id,'-DatabaseName',$db,'-UseExistingInfrastructure')
        Invoke-ProofCommand $clone @('scripts/poc-verify.ps1','-Provider',$provider)
        $latest=Get-Content -LiteralPath (Join-Path $cloneRoot ".poc/$provider/latest-report.txt") -Raw
        $firstEvidence=if([IO.Path]::IsPathRooted($latest.Trim())){$latest.Trim()}else{Join-Path $cloneRoot $latest.Trim()}
        $firstReportPath=Join-Path $firstEvidence 'report.json'
        $firstReport=Get-Content -LiteralPath $firstReportPath -Raw|ConvertFrom-Json
        if($firstReport.scenario -ne 'fresh-v1-to-v2'){throw "Expected genuine fresh V1-to-V2 scenario for $project/$provider; got $($firstReport.scenario)."}
        $firstReport|Add-Member -NotePropertyName initializedName -NotePropertyValue $name -Force
        $firstReport|Add-Member -NotePropertyName preInitSourceSha -NotePropertyValue $preInitSourceSha -Force
        $firstReport|Add-Member -NotePropertyName generatedTreeFingerprint -NotePropertyValue $treeFingerprint -Force
        Write-Utf8 $firstReportPath ($firstReport|ConvertTo-Json -Depth 16)
        Invoke-ProofCommand $clone @('scripts/poc-verify.ps1','-Provider',$provider)
        $rerunPath=(Get-Content -LiteralPath (Join-Path $cloneRoot ".poc/$provider/latest-report.txt") -Raw).Trim()
        $rerunEvidence=if([IO.Path]::IsPathRooted($rerunPath)){$rerunPath}else{Join-Path $cloneRoot $rerunPath}
        $rerunReportPath=Join-Path $rerunEvidence 'report.json'
        $rerun=Get-Content -LiteralPath $rerunReportPath -Raw|ConvertFrom-Json
        if($rerun.scenario -ne 'preexisting-v2-rerun'){throw "Expected existing V2 rerun for $project/$provider; got $($rerun.scenario)."}
        $matrix.Add([pscustomobject]@{name=$name;provider=$provider;preInitSourceSHA=$preInitSourceSha;generatedSourceSHA=$generatedSourceSha;generatedTreeFingerprint=$treeFingerprint;freshReport=$firstReportPath;rerunReport=$rerunReportPath;status='PASS'})
    }
}
$summary=Join-Path $workspacePath 'proof-summary.json'
Write-Utf8 $summary (@{status='PASS';preInitSourceSHA=$preInitSourceSha;proofRunId=$runId;commands=@('init.ps1 vm-orders','init.ps1 vm-order-service','scripts/poc-up.ps1 -Provider <provider> -ProofId <unique-name> -DatabaseName <new-db> -UseExistingInfrastructure','scripts/poc-verify.ps1 -Provider <provider> (fresh V1 to V2)','scripts/poc-verify.ps1 -Provider <provider> (preexisting V2 rerun)');scenarios=@('fresh V1 to V2','preexisting V2 rerun','failed migration / blocked rollout / transaction rollback / recovery');results=$matrix.ToArray();timeUtc=[DateTime]::UtcNow.ToString('o')}|ConvertTo-Json -Depth 16)
Write-Host "PROOF PASS source=$preInitSourceSha summary=$summary"
