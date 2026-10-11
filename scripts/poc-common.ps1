$ErrorActionPreference = 'Stop'
$script:Root = Split-Path $PSScriptRoot -Parent
$script:Profile = if($env:POC_PROFILE){$env:POC_PROFILE}else{'vm-acme-poc'}
$script:InfraNamespace = if($env:POC_INFRA_NAMESPACE){$env:POC_INFRA_NAMESPACE}else{'vm-acme-poc-infra'}
$script:RegistryNamespace = if($env:POC_REGISTRY_NAMESPACE){$env:POC_REGISTRY_NAMESPACE}else{'vm-acme-poc-registry'}
$script:Chart = Join-Path $Root 'deploy/helm/vm-acme'
$script:State = Join-Path $Root '.poc'
$script:Tools = if($env:POC_TOOLS_PATH){$env:POC_TOOLS_PATH}else{Join-Path $env:LOCALAPPDATA 'vm-acme-poc/bin'}
$script:ApiDbUser = if($env:POC_API_DB_USER){$env:POC_API_DB_USER}else{'acme_api'}
$script:MigratorDbUser = if($env:POC_MIGRATOR_DB_USER){$env:POC_MIGRATOR_DB_USER}else{'acme_migrator'}
$env:PATH = "$Tools;$env:PATH"
New-Item -ItemType Directory -Force -Path $State | Out-Null

function Write-Utf8([string]$Path, [string]$Content) {
    [IO.File]::WriteAllText($Path, $Content, [Text.UTF8Encoding]::new($false))
}
function Get-SourceProvenance {
    $head=((Run 'git' @('-C',$Root,'rev-parse','HEAD')) -join '').Trim()
    $status=@(Run 'git' @('-C',$Root,'status','--porcelain','--untracked-files=all'))
    $entries=New-Object 'System.Collections.Generic.List[string]'
    foreach($file in Get-ChildItem -LiteralPath $Root -Recurse -File) {
        $relative=$file.FullName.Substring($Root.Length).TrimStart('\','/').Replace('\','/')
        if($relative -match '(^|/)(\.git|\.poc|\.codegraph|\.idea|\.superpowers|bin|obj|artifacts|generated|testresults|node_modules)(/|$)'){continue}
        $hash=(Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
        $entries.Add("$relative`t$hash")
    }
    [string[]]$sorted=$entries.ToArray(); [Array]::Sort($sorted,[StringComparer]::Ordinal)
    $bytes=[Text.Encoding]::UTF8.GetBytes(($sorted -join "`n")); $algorithm=[Security.Cryptography.SHA256]::Create()
    try{$sha=[BitConverter]::ToString($algorithm.ComputeHash($bytes)).Replace('-','').ToLowerInvariant()}finally{$algorithm.Dispose()}
    return [pscustomobject]@{sourceSha=$head;sourceDirty=($status.Count -gt 0);sourceFingerprint=$sha;generatedTreeFingerprint=$sha;gitStatus=$status}
}
function Quote-NativeArgument([string]$Argument) {
    # Windows CommandLineToArgvW/C runtime escaping; JSON is not shell quoting.
    $escaped=[regex]::Replace($Argument, '(\\*)"', '$1$1\"')
    $escaped=[regex]::Replace($escaped, '(\\+)$', '$1$1')
    return '"' + $escaped + '"'
}
function Run([string]$Exe, [string[]]$Arguments, [int]$TimeoutSeconds=900, [string]$InputText) {
    $info=New-Object System.Diagnostics.ProcessStartInfo
    $info.FileName=(Get-Command $Exe -ErrorAction Stop).Source
    $info.Arguments=($Arguments | ForEach-Object{Quote-NativeArgument $_}) -join ' '
    $info.UseShellExecute=$false
    $info.WorkingDirectory=(Get-Location).ProviderPath
    $info.CreateNoWindow=$true
    $info.RedirectStandardOutput=$true
    $info.RedirectStandardError=$true
    $info.RedirectStandardInput=$true
    $process=New-Object System.Diagnostics.Process
    $process.StartInfo=$info
    $null=$process.Start()
    $stdout=$process.StandardOutput.ReadToEndAsync()
    $stderr=$process.StandardError.ReadToEndAsync()
    if($InputText){$process.StandardInput.WriteLine($InputText)}
    $process.StandardInput.Close()
    if(-not $process.WaitForExit($TimeoutSeconds*1000)) {
        $ErrorActionPreference='Continue'
        & "$env:WINDIR/System32/taskkill.exe" /PID $process.Id /T /F *> $null
        $process.Dispose()
        throw "$Exe exceeded ${TimeoutSeconds}s; its owned process tree was terminated. Check Docker/network/Pod readiness."
    }
    $exitCode=$process.ExitCode
    $result=@($stdout.Result,$stderr.Result) | Where-Object{$_} | ForEach-Object{$_ -split "`r?`n"} | Where-Object{$_}
    $process.Dispose()
    if ($exitCode -ne 0) {
        $safe=$result -join "`n"
        foreach($value in (Get-PocCredentials).Values) { $safe=$safe.Replace($value,'[REDACTED]') }
        if($env:POC_REGISTRY_PASSWORD){$safe=$safe.Replace($env:POC_REGISTRY_PASSWORD,'[REDACTED]')}
        Write-Utf8 (Join-Path $State 'native-failure.txt') $safe
        throw "$Exe failed (exit $exitCode). Sanitized diagnostics: .poc/native-failure.txt. Inspect PoC Pod events/logs."
    }
    return $result
}
function K([string[]]$Arguments) {
    Run 'minikube' (@('-p', $Profile, 'kubectl', '--', '--context', $Profile) + $Arguments)
}
function Invoke-PocHelm([string[]]$Arguments) { Run 'helm' (@('--kube-context', $Profile) + $Arguments) }
function Assert-PocContext {
    $name = (K @('get','nodes','-o','jsonpath={.items[0].metadata.name}')) -join ''
    if ($name -ne $Profile) { throw "Refusing operations: cluster node is '$name', expected '$Profile'." }
}
function Ensure-Namespace([string]$Name) {
    if ($Name -notlike 'vm-acme-poc-*' -and $Name -notlike 'vm-orders-poc-*' -and $Name -notlike 'vm-order-service-poc-*') { throw 'Refusing non-PoC namespace.' }
    $existing=(K @('get','namespace',$Name,'--ignore-not-found','-o','json')) -join ''
    if($existing -and (($existing|ConvertFrom-Json).metadata.labels.'vm-acme.owner' -ne 'poc')) {
        throw "Refusing namespace ${Name}: it already exists without PoC ownership label."
    }
    $manifest = @{apiVersion='v1';kind='Namespace';metadata=@{name=$Name;labels=@{'vm-acme.owner'='poc'}}} | ConvertTo-Json -Depth 8
    Apply-Json $manifest
}
function Apply-Json([string]$Json) {
    $file = Join-Path $State ('apply-' + [guid]::NewGuid().ToString('N') + '.json')
    try { Write-Utf8 $file $Json; K @('apply','-f',$file) | Out-Null }
    finally { if (Test-Path -LiteralPath $file) { [IO.File]::Delete($file) } }
}
function Set-Secret([string]$Namespace,[string]$Name,[hashtable]$Data,[string]$Type='Opaque') {
    $manifest = @{apiVersion='v1';kind='Secret';metadata=@{name=$Name;namespace=$Namespace};type=$Type;stringData=$Data}
    Apply-Json ($manifest | ConvertTo-Json -Depth 12)
}
function Wait-Http([string]$Uri,[int]$Seconds=180) {
    $deadline = (Get-Date).AddSeconds($Seconds)
    do {
        try { return Invoke-RestMethod -Uri $Uri -TimeoutSec 5 } catch { Start-Sleep -Seconds 2 }
    } while ((Get-Date) -lt $deadline)
    throw "Endpoint not ready within ${Seconds}s: $Uri. Check Pods, events and port-forward process."
}
function Todo-MigrationTarget([string]$Provider) {
    if($Provider -eq 'postgres'){ return '20261011121557_AddTodoAccounts' }
    return '20261011121718_AddTodoAccounts'
}
function New-PocApiToken([string]$Uri) {
    $identity=[guid]::NewGuid().ToString('N')
    $email="poc-$identity@example.com"
    $password="PoC-password-$identity!"
    Invoke-RestMethod "$Uri/api/users/register" -Method Post -ContentType 'application/json' -Body (@{email=$email;firstName='PoC';lastName='Verifier';password=$password}|ConvertTo-Json) -TimeoutSec 15 | Out-Null
    $login=Invoke-RestMethod "$Uri/api/users/login" -Method Post -ContentType 'application/json' -Body (@{email=$email;password=$password}|ConvertTo-Json) -TimeoutSec 15
    return $login.accessToken
}
function Start-Forward([string]$Namespace,[string]$Service,[int]$Port,[int]$TargetPort) {
    # Always owns exactly this process; bounded readiness check; callers stop it in finally.
    # Run the matching cached client directly so stopping the tunnel also stops
    # the port-forward process, rather than orphaning a minikube child process.
    $exe = Join-Path $env:USERPROFILE '.minikube/cache/windows/amd64/v1.37.0/kubectl.exe'
    if(-not (Test-Path -LiteralPath $exe)){throw 'Matching kubectl cache is missing; rerun PoC prerequisite bootstrap.'}
    $log = Join-Path $State ("forward-$Namespace-$Port-"+[guid]::NewGuid().ToString('N'))
    $process = Start-Process -FilePath $exe -ArgumentList @('--context',$Profile,'-n',$Namespace,'port-forward',"svc/$Service","${Port}:${TargetPort}",'--address=127.0.0.1') -PassThru -WindowStyle Hidden -RedirectStandardOutput "$log.out" -RedirectStandardError "$log.err"
    return $process
}
function Stop-Forward($Process) {
    if ($Process -and -not $Process.HasExited) {
        Stop-Process -Id $Process.Id -ErrorAction SilentlyContinue
        $null=$Process.WaitForExit(5000)
    }
}
function Diagnostics([string]$Namespace,[string]$Directory) {
    New-Item -ItemType Directory -Force -Path $Directory | Out-Null
    foreach ($item in @(@('pods',@('get','pods','-o','wide')),@('events',@('get','events','--sort-by=.lastTimestamp')))) {
        try { Write-Utf8 (Join-Path $Directory "$($item[0]).txt") ((K (@('-n',$Namespace)+$item[1])) -join "`n") } catch { }
    }
}
function Get-PocCredentials {
    # Deliberately public, isolated test credentials. Never used for company dev.
    return @{ Harbor=$(if($env:POC_HARBOR_PASSWORD){$env:POC_HARBOR_PASSWORD}else{'VmAcmePoc!Harbor2026'}); Migrator=$(if($env:POC_MIGRATOR_DB_PASSWORD){$env:POC_MIGRATOR_DB_PASSWORD}else{'VmAcmePoc!Migration2026'}); Api=$(if($env:POC_API_DB_PASSWORD){$env:POC_API_DB_PASSWORD}else{'VmAcmePoc!Api2026'}); Sa=$(if($env:POC_SQL_ADMIN_PASSWORD){$env:POC_SQL_ADMIN_PASSWORD}else{'VmAcmePoc!SqlAdmin2026'}) }
}
function Database-Query([string]$Provider,[string]$Sql,[string]$Database) {
    if(-not $Database){$Database=if($script:ActivePocDatabase){$script:ActivePocDatabase}else{'acme'}}
    if($Database -notmatch '^[a-z][a-z0-9_]{0,30}$'){throw 'Refusing unsafe PoC database name.'}
    $encoded=[Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($Sql))
    if($Provider -eq 'postgres') {
        $command="echo $encoded | base64 -d | PGPASSWORD=`$POSTGRES_PASSWORD psql -U $MigratorDbUser -d $Database -At -v ON_ERROR_STOP=1"
        return ((K @('-n',$InfraNamespace,'exec','postgres-0','--','sh','-c',$command)) -join "`n").Trim()
    }
    $command="export SQLCMDPASSWORD=`$MSSQL_SA_PASSWORD; echo $encoded | base64 -d | /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -C -b -d $Database -W -h -1"
    return ((K @('-n',$InfraNamespace,'exec','sqlserver-0','--','sh','-c',$command)) -join "`n").Trim()
}
function Initialize-PocDatabase([string]$Provider,[string]$Database) {
    if($Database -notmatch '^[a-z][a-z0-9_]{0,30}$' -or $Database -eq 'acme'){throw 'Proof database must be a new safe name other than acme.'}
    if($Provider -eq 'postgres') {
        $exists=(Database-Query $Provider "SELECT 1 FROM pg_database WHERE datname = '$Database';" 'postgres').Trim()
        if($exists){throw "Refusing to reuse existing proof database '$Database'; choose a new name."}
        $script="CREATE DATABASE $Database OWNER $MigratorDbUser;`nGRANT CONNECT ON DATABASE $Database TO $ApiDbUser;"
        $encoded=[Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($script))
        $command="echo $encoded | base64 -d | PGPASSWORD=`$POSTGRES_PASSWORD psql -U $MigratorDbUser -d postgres -v ON_ERROR_STOP=1"
        K @('-n',$InfraNamespace,'exec','postgres-0','--','sh','-c',$command) | Out-Null
        Database-Query $Provider "GRANT USAGE ON SCHEMA public TO $ApiDbUser; ALTER DEFAULT PRIVILEGES FOR ROLE $MigratorDbUser IN SCHEMA public GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO $ApiDbUser;" $Database | Out-Null
        return
    }
    $exists=(Database-Query $Provider "SELECT CASE WHEN DB_ID('$Database') IS NULL THEN 0 ELSE 1 END;" 'master').Trim()
    if($exists -ne '0'){throw "Refusing to reuse existing proof database '$Database'; choose a new name."}
    $sql="CREATE DATABASE [$Database];`nGO`nUSE [$Database];`nGO`nIF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name='$MigratorDbUser') CREATE USER [$MigratorDbUser] FOR LOGIN [$MigratorDbUser];`nIF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name='$ApiDbUser') CREATE USER [$ApiDbUser] FOR LOGIN [$ApiDbUser];`nALTER ROLE db_owner ADD MEMBER [$MigratorDbUser];`nALTER ROLE db_datareader ADD MEMBER [$ApiDbUser];`nALTER ROLE db_datawriter ADD MEMBER [$ApiDbUser];"
    $encoded=[Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($sql))
    $command="export SQLCMDPASSWORD=`$MSSQL_SA_PASSWORD; echo $encoded | base64 -d | /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -C -b"
    K @('-n',$InfraNamespace,'exec','sqlserver-0','--','sh','-c',$command) | Out-Null
}
function Provider-Name([string]$Provider) {
    if ($Provider -eq 'postgres') { return 'PostgreSql' }
    if ($Provider -eq 'sqlserver') { return 'SqlServer' }
    throw 'Provider must be postgres or sqlserver.'
}
function Get-Config([string]$Provider) {
    $file = Join-Path $State "$Provider.json"
    if (-not (Test-Path $file)) { throw "Run scripts/poc-up.sh --provider $Provider first." }
    $config=Get-Content -LiteralPath $file -Raw | ConvertFrom-Json
    $script:ActivePocDatabase=if($config.databaseName){$config.databaseName}else{'acme'}
    return $config
}
function Deploy-Application($Config,[string]$Tag,[string]$Target='',[bool]$FailMigration=$false,[string]$MigrationTag='') {
    if(-not $MigrationTag){$MigrationTag=$Tag}
    $args = @('upgrade','--install','vm-acme',$Chart,'--namespace',$Config.namespace,
        '-f',"$Chart/values-poc.yaml",'--set-string',"persistence.provider=$($Config.providerName)",
        '--set-string',"image.repository=$($Config.registry)/$($Config.project)/vm-acme-api",
        '--set-string',"image.tag=$Tag",'--set-string',"migration.image.repository=$($Config.registry)/$($Config.project)/vm-acme-migrator",
        '--set-string',"migration.image.tag=$MigrationTag",'--set-string',"migration.target=$Target",
        '--set',"migration.failForPoc=$($FailMigration.ToString().ToLowerInvariant())",'--wait','--timeout','6m')
    Invoke-PocHelm $args | Out-Null
    K @('-n',$Config.namespace,'rollout','status','deployment/vm-acme','--timeout=180s') | Out-Null
}
function Migration-Logs($Config,[string]$File) {
    $logs = (K @('-n',$Config.namespace,'logs','job/vm-acme-migrate')) -join "`n"
    Write-Utf8 $File $logs
    return $logs
}
