. "$PSScriptRoot/poc-prerequisites.ps1"
function Bootstrap-PocInfrastructure {
    Ensure-PocCluster
    Ensure-Namespace $InfraNamespace
    Ensure-Namespace $RegistryNamespace
    $credentials=Get-PocCredentials
    $registry=((Run 'minikube' @('-p',$Profile,'ip')) -join '').Trim()+':30002'
    $project='vm-acme-poc'
    $external=[bool]$env:POC_REGISTRY_ENDPOINT
    if ($external) {
        if ($env:POC_REGISTRY_AUTHORIZED -ne 'true') { throw 'Supply POC_REGISTRY_AUTHORIZED=true and an explicitly authorized Harbor endpoint for shared infrastructure.' }
        foreach($variable in @('POC_REGISTRY_PROJECT','POC_REGISTRY_USERNAME','POC_REGISTRY_PASSWORD')) {
            if (-not [Environment]::GetEnvironmentVariable($variable)) { throw "Required external Harbor input: $variable" }
        }
        $registry=$env:POC_REGISTRY_ENDPOINT; $project=$env:POC_REGISTRY_PROJECT
    }
    $http=if($external){$env:POC_REGISTRY_HTTP -eq 'true'}else{$true}
    Set-PocDockerEngine $registry $env:POC_REGISTRY_CA $http
    if (-not $external) {
        Write-Host 'Bootstrapping isolated Harbor registry...'
        Set-Secret $RegistryNamespace 'vm-acme-harbor-admin' @{HARBOR_ADMIN_PASSWORD=$credentials.Harbor}
        Invoke-PocHelm @('repo','add','harbor','https://helm.goharbor.io','--force-update') | Out-Null
        Invoke-PocHelm @('repo','update') | Out-Null
        Invoke-PocHelm @('upgrade','--install','vm-acme-harbor','harbor/harbor','--version','1.19.2','-n',$RegistryNamespace,
            '-f',"$Root/deploy/poc/harbor-values.yaml",'--set-string',"externalURL=http://$registry",'--wait','--timeout','10m') | Out-Null
    }
    $user=if($external){$env:POC_REGISTRY_USERNAME}else{'admin'}
    $password=if($external){$env:POC_REGISTRY_PASSWORD}else{$credentials.Harbor}
    $prefix=if($http){'http'}else{'https'}
    $forward=$null
    try {
        if ($external) { $baseUri="${prefix}://$registry" } else {
            $forward=Start-Forward $RegistryNamespace 'harbor' 18082 80
            $baseUri='http://127.0.0.1:18082'
        }
        Wait-Http "$baseUri/api/v2.0/health" 120 | Out-Null
        $auth=[Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes("${user}:$password"))
        $headers=@{Authorization="Basic $auth"}
        $projects=Invoke-RestMethod "$baseUri/api/v2.0/projects?name=$project" -Headers $headers -TimeoutSec 20
        if (-not ($projects | Where-Object { $_.name -eq $project })) {
            # External targets: do not create projects on shared infrastructure.
            if ($external) { throw 'Authorized external Harbor project is missing; ask its owner to provision it.' }
            Invoke-RestMethod "$baseUri/api/v2.0/projects" -Method Post -Headers $headers -ContentType 'application/json' -Body (@{project_name=$project;metadata=@{public='false'}}|ConvertTo-Json) -TimeoutSec 20 | Out-Null
        }
    } finally { Stop-Forward $forward }
    Write-Host 'Provisioning PostgreSQL and SQL Server with persistent volumes...'
    Set-Secret $InfraNamespace 'vm-acme-poc-databases' @{postgresPassword=$credentials.Migrator;sqlAdminPassword=$credentials.Sa;apiPassword=$credentials.Api;migratorPassword=$credentials.Migrator}
    K @('apply','-f',"$Root/deploy/poc/databases.yaml") | Out-Null
    foreach($database in @('postgres','sqlserver')) {
        K @('-n',$InfraNamespace,'rollout','status',"statefulset/$database",'--timeout=600s') | Out-Null
    }
    # SQL text is mounted, passwords come from Pod environment. No command-line credentials.
    K @('-n',$InfraNamespace,'exec','postgres-0','--','sh','-c','export PGPASSWORD="$POSTGRES_PASSWORD"; psql -U acme_migrator -d acme -v ON_ERROR_STOP=1 -f /bootstrap/postgres.sql') | Out-Null
    K @('-n',$InfraNamespace,'exec','sqlserver-0','--','sh','-c','export SQLCMDPASSWORD="$MSSQL_SA_PASSWORD"; /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -C -b -i /bootstrap/sqlserver.sql') | Out-Null
    return @{registry=$registry;project=$project;registryUser=$user;registryPassword=$password;external=$external}
}
