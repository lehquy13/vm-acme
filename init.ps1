param(
    [string]$Name
)

$ErrorActionPreference = 'Stop'
$scriptRoot = $PSScriptRoot
$placeholder = 'A' + 'cme'
$lowerPlaceholder = $placeholder.ToLowerInvariant()
Import-Module (Join-Path $scriptRoot 'scripts/TemplateNaming.psm1') -Force

Push-Location $scriptRoot
try {
    if (Test-Path -LiteralPath (Join-Path $scriptRoot '.template-initialized')) {
        throw 'This checkout has already been initialized. The initializer is one-time only.'
    }
    if ([string]::IsNullOrWhiteSpace($Name)) { $Name = Read-Host 'Repository/project name [acme]' }
    $requestedName = if ([string]::IsNullOrWhiteSpace($Name)) { $lowerPlaceholder } else { $Name.Trim() }
    $names = Get-TemplateNames $requestedName
    $projectName = $names.Brand
    $projectNameLower = $names.BrandLower
    $projectNameUpper = $names.BrandUpper
    $deploymentName = $names.DeploymentName
    $solutionFile = $names.SolutionFile
    $solutionPath = Join-Path $scriptRoot "vm-$lowerPlaceholder.slnx"
    if (-not (Test-Path -LiteralPath $solutionPath)) {
        throw "Could not find the $placeholder template solution. Run this initializer before renaming the template."
    }

    if ($projectName -cne $placeholder) {
        $originUrl = git -C $scriptRoot remote get-url origin 2>$null
        if (($env:GITHUB_REPOSITORY -and $env:GITHUB_REPOSITORY.Equals('lehquy13/vm-acme', [StringComparison]::OrdinalIgnoreCase)) -or
            ($originUrl -match 'github\.com[:/]lehquy13/vm-acme(?:\.git)?$')) {
            throw 'Refusing to initialize the original template repository.'
        }
        $excludedPath = '(^|[\\/])(\.git|\.codegraph|artifacts|\.idea|\.superpowers|\.poc|bin|obj)([\\/]|$)'
        $textExtensions = @('.cs', '.csproj', '.slnx', '.props', '.json', '.http', '.yaml', '.yml', '.tpl', '.md', '.sh', '.ps1', '.txt')
        function Replace-TemplateTokens([string]$Value) {
            $evaluator = [System.Text.RegularExpressions.MatchEvaluator] {
                param($match)
                switch -CaseSensitive ($match.Value) {
                    'vm-acme.slnx' { $solutionFile }
                    'vm-acme' { $deploymentName }
                    'vm_acme' { $names.MetricPrefix }
                    'VM_ACME' { $names.EnvironmentPrefix }
                    'Acme' { $projectName }
                    'acme' { $projectNameLower }
                }
            }
            return [regex]::Replace($Value, 'vm-acme\.slnx|vm-acme|vm_acme|VM_ACME|Acme|acme', $evaluator)
        }

        $files = Get-ChildItem -LiteralPath $scriptRoot -Force -Recurse -File |
            Where-Object {
                ($_.FullName.Substring($scriptRoot.Length)) -notmatch $excludedPath -and
                ($textExtensions -contains $_.Extension -or $_.Name -in @('Dockerfile', '.dockerignore', '.gitignore')) -and
                $_.Name -notin @('init.ps1', 'init.sh', 'bootstrap-template.ps1', 'TemplateNaming.psm1', 'test-template-naming.ps1', 'test-template-naming.sh')
            }

        foreach ($file in $files) {
            $content = [IO.File]::ReadAllText($file.FullName)
            $content = Replace-TemplateTokens $content
            [IO.File]::WriteAllText($file.FullName, $content, [Text.UTF8Encoding]::new($false))
        }

        $filesToRename = Get-ChildItem -LiteralPath $scriptRoot -Force -Recurse -File |
            Where-Object { ($_.FullName.Substring($scriptRoot.Length)) -notmatch $excludedPath -and $_.Name.IndexOf($lowerPlaceholder, [StringComparison]::OrdinalIgnoreCase) -ge 0 }
        foreach ($file in $filesToRename) {
            $newName = if ($file.Name -ceq "vm-$lowerPlaceholder.slnx") { $solutionFile } else { Replace-TemplateTokens $file.Name }
            Rename-Item -LiteralPath $file.FullName -NewName $newName
        }

        $directoriesToRename = Get-ChildItem -LiteralPath $scriptRoot -Force -Recurse -Directory |
            Where-Object { ($_.FullName.Substring($scriptRoot.Length)) -notmatch $excludedPath -and $_.Name.IndexOf($lowerPlaceholder, [StringComparison]::OrdinalIgnoreCase) -ge 0 } |
            Sort-Object { $_.FullName.Length } -Descending
        foreach ($directory in $directoriesToRename) {
            $newName = Replace-TemplateTokens $directory.Name
            Rename-Item -LiteralPath $directory.FullName -NewName $newName
        }
    }

    dotnet restore $solutionFile
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet restore failed with exit code $LASTEXITCODE"
    }

    dotnet build $solutionFile --no-restore
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet build failed with exit code $LASTEXITCODE"
    }
}
finally {
    Pop-Location
}
