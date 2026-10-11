param(
    [string]$Name
)

$ErrorActionPreference = 'Stop'
$scriptRoot = $PSScriptRoot
$placeholder = 'A' + 'cme'
$lowerPlaceholder = $placeholder.ToLowerInvariant()

Push-Location $scriptRoot
try {
    if ([string]::IsNullOrWhiteSpace($Name)) {
        $Name = Read-Host 'Project name [acme]'
    }

    $projectName = if ([string]::IsNullOrWhiteSpace($Name)) { $lowerPlaceholder } else { $Name.Trim() }
    if ($projectName -notmatch '^[A-Za-z][A-Za-z0-9_]*$') {
        throw 'Project name must start with a letter and contain only letters, digits, or underscores.'
    }

    $projectName = $projectName.Substring(0, 1).ToUpperInvariant() + $projectName.Substring(1)

    $projectNameLower = $projectName.ToLowerInvariant()
    $projectNameUpper = $projectName.ToUpperInvariant()
    if ($projectNameLower.EndsWith('_', [StringComparison]::Ordinal)) {
        throw 'Project name must not end with an underscore.'
    }
    $deploymentSlug = $projectNameLower.Replace('_', '-')
    $deploymentName = "vm-$deploymentSlug"
    if ($deploymentSlug.Length -gt 40) {
        throw 'Deployment slug must be 40 characters or fewer.'
    }
    $solutionPath = Join-Path $scriptRoot "vm-$lowerPlaceholder.slnx"
    if (-not (Test-Path -LiteralPath $solutionPath)) {
        throw "Could not find the $placeholder template solution. Run this initializer before renaming the template."
    }

    if ($projectName -cne $placeholder) {
        $excludedPath = '(^|[\\/])(\.git|\.codegraph|artifacts|\.idea|\.superpowers|\.poc|bin|obj)([\\/]|$)'
        $textExtensions = @('.cs', '.csproj', '.slnx', '.props', '.json', '.http', '.yaml', '.yml', '.tpl', '.md', '.sh', '.ps1', '.txt')
        $solutionFile = "vm-$projectNameLower.slnx"

        function Replace-TemplateTokens([string]$Value) {
            $evaluator = [System.Text.RegularExpressions.MatchEvaluator] {
                param($match)
                switch -CaseSensitive ($match.Value) {
                    'vm-acme.slnx' { $solutionFile }
                    'vm-acme' { $deploymentName }
                    'vm_acme' { "vm_$projectNameLower" }
                    'VM_ACME' { "VM_$projectNameUpper" }
                    'Acme' { $projectName }
                    'acme' { $projectNameLower }
                }
            }
            return [regex]::Replace($Value, 'vm-acme\.slnx|vm-acme|vm_acme|VM_ACME|Acme|acme', $evaluator)
        }

        $files = Get-ChildItem -LiteralPath $scriptRoot -Force -Recurse -File |
            Where-Object {
                $_.FullName -notmatch $excludedPath -and
                ($textExtensions -contains $_.Extension -or $_.Name -in @('Dockerfile', '.dockerignore', '.gitignore')) -and
                $_.Name -notin @('init.ps1', 'init.sh')
            }

        foreach ($file in $files) {
            $content = [IO.File]::ReadAllText($file.FullName)
            $content = Replace-TemplateTokens $content
            [IO.File]::WriteAllText($file.FullName, $content, [Text.UTF8Encoding]::new($false))
        }

        $filesToRename = Get-ChildItem -LiteralPath $scriptRoot -Force -Recurse -File |
            Where-Object { $_.FullName -notmatch $excludedPath -and $_.Name.IndexOf($lowerPlaceholder, [StringComparison]::OrdinalIgnoreCase) -ge 0 }
        foreach ($file in $filesToRename) {
            $newName = if ($file.Name -ceq "vm-$lowerPlaceholder.slnx") { $solutionFile } else { Replace-TemplateTokens $file.Name }
            Rename-Item -LiteralPath $file.FullName -NewName $newName
        }

        $directoriesToRename = Get-ChildItem -LiteralPath $scriptRoot -Force -Recurse -Directory |
            Where-Object { $_.FullName -notmatch $excludedPath -and $_.Name.IndexOf($lowerPlaceholder, [StringComparison]::OrdinalIgnoreCase) -ge 0 } |
            Sort-Object { $_.FullName.Length } -Descending
        foreach ($directory in $directoriesToRename) {
            $newName = Replace-TemplateTokens $directory.Name
            Rename-Item -LiteralPath $directory.FullName -NewName $newName
        }
    }

    $solutionFile = "vm-$projectNameLower.slnx"
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
