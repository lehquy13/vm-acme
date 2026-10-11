param([string]$BundleRuntime, [string]$EvidenceDirectory)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$suffix = [Guid]::NewGuid().ToString('N').Substring(0, 10)
$postgresName = "vm-acme-tests-pg-$suffix"
$sqlName = "vm-acme-tests-sql-$suffix"
$postgresPassword = [Guid]::NewGuid().ToString('N') + [Guid]::NewGuid().ToString('N')
$sqlPassword = "Aa1!$([Guid]::NewGuid().ToString('N'))$([Guid]::NewGuid().ToString('N'))z"
$oldPostgres = $env:VM_ACME_TEST_POSTGRESQL
$oldSql = $env:VM_ACME_TEST_SQLSERVER
$resultsDirectory = Join-Path ([IO.Path]::GetTempPath()) "vm-acme-test-results-$suffix"
if ($EvidenceDirectory) { $resultsDirectory = [IO.Path]::GetFullPath($EvidenceDirectory) }

function Invoke-ReadinessProbe([string[]]$Arguments) {
    # Windows PowerShell treats native stderr as an error even when redirected.
    # Failed readiness probes are expected while the databases are starting.
    $ErrorActionPreference = 'Continue'
    & docker @Arguments *> $null
    return $LASTEXITCODE -eq 0
}

function Assert-DatabaseTestResults([string]$Directory) {
    $trxFiles = @(Get-ChildItem -LiteralPath $Directory -Filter '*.trx' -File -Recurse)
    if ($trxFiles.Count -eq 0) { throw "dotnet test produced no TRX files in $Directory." }

    $requiredResults = @()
    foreach ($file in $trxFiles) {
        [xml]$trx = Get-Content -LiteralPath $file.FullName -Raw
        $results = $trx.SelectNodes("//*[local-name()='UnitTestResult']")
        foreach ($result in $results) {
            $testName = [string]$result.GetAttribute('testName')
            if ($testName -match '(?:^|\.)LiveDatabaseTests\.' -or
                $testName -match '(?:^|\.)StagedMigrationTests\.') {
                $requiredResults += [pscustomobject]@{
                    Name = $testName
                    Outcome = [string]$result.GetAttribute('outcome')
                    File = $file.Name
                }
            }
        }
    }

    if ($requiredResults.Count -eq 0) {
        throw 'TRX contains no LiveDatabaseTests or StagedMigrationTests results; database tests may have been skipped or omitted.'
    }
    $notPassed = @($requiredResults | Where-Object Outcome -ne 'Passed')
    if ($notPassed.Count -gt 0) {
        $details = ($notPassed | ForEach-Object { "$($_.Outcome): $($_.Name) [$($_.File)]" }) -join "`n"
        throw "Required database tests must all pass; non-passing results:`n$details"
    }

    foreach ($className in @('LiveDatabaseTests', 'StagedMigrationTests')) {
        $classResults = @($requiredResults | Where-Object { $_.Name -match "(?:^|\.)$className\." })
        if ($classResults.Count -eq 0) {
            throw "TRX contains no results for required test class '$className'."
        }
        foreach ($provider in @('PostgreSql', 'SqlServer')) {
            $providerResults = @($classResults | Where-Object { $_.Name -match [regex]::Escape($provider) })
            if ($providerResults.Count -eq 0) {
                throw "TRX contains no DataRow result for required test class '$className' and provider '$provider'."
            }
            Write-Host "PASS database test results class=$className provider=$provider rows=$($providerResults.Count)"
        }
    }
    Write-Host "PASS required database test rows=$($requiredResults.Count)"
}

try {
    # Disposable test-only credentials; no host volumes or existing databases are used.
    docker run -d --name $postgresName -p '127.0.0.1::5432' -e POSTGRES_USER=test -e "POSTGRES_PASSWORD=$postgresPassword" -e POSTGRES_DB=VmAcme postgres:17-alpine | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Could not start PostgreSQL test container.' }
    docker run -d --name $sqlName -p '127.0.0.1::1433' -e ACCEPT_EULA=Y -e "MSSQL_SA_PASSWORD=$sqlPassword" -e MSSQL_PID=Developer mcr.microsoft.com/mssql/server:2022-latest | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Could not start SQL Server test container.' }

    $deadline = [DateTime]::UtcNow.AddMinutes(3)
    do {
        $postgresReady = Invoke-ReadinessProbe -Arguments @('exec', $postgresName, 'pg_isready', '-U', 'test', '-d', 'VmAcme')
        $sqlReady = Invoke-ReadinessProbe -Arguments @('exec', $sqlName, '/opt/mssql-tools18/bin/sqlcmd', '-C', '-S', 'localhost', '-U', 'sa', '-P', $sqlPassword, '-Q', 'SELECT 1')
        if ($postgresReady -and $sqlReady) { break }
        if ([DateTime]::UtcNow -gt $deadline) { throw 'Database test containers did not become ready within 3 minutes.' }
        Start-Sleep -Seconds 2
    } while ($true)

    $postgresPort = (docker port $postgresName 5432/tcp).Split(':')[-1]
    $sqlPort = (docker port $sqlName 1433/tcp).Split(':')[-1]
    $env:VM_ACME_TEST_POSTGRESQL = "Host=127.0.0.1;Port=$postgresPort;Database=VmAcme;Username=test;Password=$postgresPassword"
    $env:VM_ACME_TEST_SQLSERVER = "Server=127.0.0.1,$sqlPort;Database=VmAcme;User Id=sa;Password=$sqlPassword;TrustServerCertificate=true"
    if ($BundleRuntime) {
        $oldConnection = $env:ConnectionStrings__DefaultConnection
        $oldMode = $env:Secrets__Mode
        try {
            $env:Secrets__Mode = 'AppSettings'
            foreach ($provider in @('postgresql', 'sqlserver')) {
                $connection = if ($provider -eq 'postgresql') { $env:VM_ACME_TEST_POSTGRESQL } else { $env:VM_ACME_TEST_SQLSERVER }
                $bundle = Join-Path $repoRoot "artifacts/bundles/$BundleRuntime/efbundle-$provider"
                if ($BundleRuntime -like 'win-*') { $bundle += '.exe' }
                $secretDirectory = Join-Path ([IO.Path]::GetTempPath()) "vm-acme-bundle-secrets-$suffix-$provider"
                New-Item -ItemType Directory -Force $secretDirectory | Out-Null
                $secretFile = Join-Path $secretDirectory 'appsettings.secrets.json'
                $oldFile=$env:Secrets__FallbackFile; $oldBase=$env:Secrets__BaseDirectory
                try {
                    @{ConnectionStrings=@{DefaultConnection=$connection}} | ConvertTo-Json -Depth 4 | Set-Content $secretFile
                    $env:ConnectionStrings__DefaultConnection=$null
                    $env:Secrets__FallbackFile='appsettings.secrets.json'
                    $env:Secrets__BaseDirectory=$secretDirectory
                    Push-Location ([IO.Path]::GetTempPath())
                    try {
                        & $bundle
                        if ($LASTEXITCODE -ne 0) { throw "Self-contained $provider bundle file resolution failed." }
                        $providerName=if($provider -eq 'postgresql'){'PostgreSql'}else{'SqlServer'}
                        dotnet run --project (Join-Path $repoRoot 'tools/VmAcme.DbMigrator') -- --provider $providerName
                        if ($LASTEXITCODE -ne 0) { throw "Custom $provider migrator file resolution failed." }
                    } finally { Pop-Location }
                    Write-Host "PASS bundle and custom migrator explicit file from unrelated working directory provider=$providerName"
                } finally {
                    $env:Secrets__FallbackFile=$oldFile; $env:Secrets__BaseDirectory=$oldBase
                    Remove-Item -LiteralPath $secretFile -Force
                    Remove-Item -LiteralPath $secretDirectory
                }
            }
        } finally {
            $env:ConnectionStrings__DefaultConnection = $oldConnection
            $env:Secrets__Mode = $oldMode
        }
    }
    Write-Host 'PostgreSQL and SQL Server ready. Running the full solution test suite.'
    New-Item -ItemType Directory -Force -Path $resultsDirectory | Out-Null
    dotnet test (Join-Path $repoRoot 'vm-acme.slnx') --verbosity minimal --logger trx --results-directory $resultsDirectory
    if ($LASTEXITCODE -ne 0) { throw 'Database verification failed.' }
    Assert-DatabaseTestResults $resultsDirectory
    if ($EvidenceDirectory) {
        $sourceSha = (git -C $repoRoot rev-parse HEAD).Trim()
        $dirty = @(git -C $repoRoot status --porcelain).Count -ne 0
        @{status='PASS';sourceSha=$sourceSha;cleanCheckout=(-not $dirty);command="scripts/test-databases.ps1 -BundleRuntime $BundleRuntime -EvidenceDirectory $EvidenceDirectory";requiredProviders=@('PostgreSql','SqlServer');timeUtc=[DateTime]::UtcNow.ToString('o')} |
            ConvertTo-Json -Depth 6 | Set-Content (Join-Path $resultsDirectory 'database-report.json')
    }
}
finally {
    $env:VM_ACME_TEST_POSTGRESQL = $oldPostgres
    $env:VM_ACME_TEST_SQLSERVER = $oldSql
    Invoke-ReadinessProbe -Arguments @('rm', '-f', $postgresName, $sqlName) | Out-Null
    if (-not $EvidenceDirectory -and (Test-Path -LiteralPath $resultsDirectory)) {
        $tempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
        if (-not $resultsDirectory.StartsWith($tempRoot, [StringComparison]::OrdinalIgnoreCase)) { throw 'Unsafe temporary results path.' }
        Remove-Item -LiteralPath $resultsDirectory -Recurse -Force
    }
}
