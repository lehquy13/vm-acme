param()
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$suffix = [Guid]::NewGuid().ToString('N').Substring(0, 10)
$postgresName = "vm-acme-tests-pg-$suffix"
$sqlName = "vm-acme-tests-sql-$suffix"
$oldPostgres = $env:VM_ACME_TEST_POSTGRESQL
$oldSql = $env:VM_ACME_TEST_SQLSERVER

function Invoke-ReadinessProbe([string[]]$Arguments) {
    # Windows PowerShell treats native stderr as an error even when redirected.
    # Failed readiness probes are expected while the databases are starting.
    $ErrorActionPreference = 'Continue'
    & docker @Arguments *> $null
    return $LASTEXITCODE -eq 0
}

try {
    # Disposable test-only credentials; no host volumes or existing databases are used.
    docker run -d --name $postgresName -p '127.0.0.1::5432' -e POSTGRES_USER=test -e POSTGRES_PASSWORD=test -e POSTGRES_DB=VmAcme postgres:17-alpine | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Could not start PostgreSQL test container.' }
    docker run -d --name $sqlName -p '127.0.0.1::1433' -e ACCEPT_EULA=Y -e 'MSSQL_SA_PASSWORD=Tests-only-Password-123!' -e MSSQL_PID=Developer mcr.microsoft.com/mssql/server:2022-latest | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Could not start SQL Server test container.' }

    $deadline = [DateTime]::UtcNow.AddMinutes(3)
    do {
        $postgresReady = Invoke-ReadinessProbe -Arguments @('exec', $postgresName, 'pg_isready', '-U', 'test', '-d', 'VmAcme')
        $sqlReady = Invoke-ReadinessProbe -Arguments @('exec', $sqlName, '/opt/mssql-tools18/bin/sqlcmd', '-C', '-S', 'localhost', '-U', 'sa', '-P', 'Tests-only-Password-123!', '-Q', 'SELECT 1')
        if ($postgresReady -and $sqlReady) { break }
        if ([DateTime]::UtcNow -gt $deadline) { throw 'Database test containers did not become ready within 3 minutes.' }
        Start-Sleep -Seconds 2
    } while ($true)

    $postgresPort = (docker port $postgresName 5432/tcp).Split(':')[-1]
    $sqlPort = (docker port $sqlName 1433/tcp).Split(':')[-1]
    $env:VM_ACME_TEST_POSTGRESQL = "Host=127.0.0.1;Port=$postgresPort;Database=VmAcme;Username=test;Password=test"
    $env:VM_ACME_TEST_SQLSERVER = "Server=127.0.0.1,$sqlPort;Database=VmAcme;User Id=sa;Password=Tests-only-Password-123!;TrustServerCertificate=true"
    Write-Host 'PostgreSQL and SQL Server ready. Running the full solution test suite.'
    dotnet test (Join-Path $repoRoot 'vm-acme.slnx') --verbosity minimal
    if ($LASTEXITCODE -ne 0) { throw 'Database verification failed.' }
}
finally {
    $env:VM_ACME_TEST_POSTGRESQL = $oldPostgres
    $env:VM_ACME_TEST_SQLSERVER = $oldSql
    Invoke-ReadinessProbe -Arguments @('rm', '-f', $postgresName, $sqlName) | Out-Null
}
