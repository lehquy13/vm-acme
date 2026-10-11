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
    $solutionPath = Join-Path $scriptRoot "vm-$lowerPlaceholder.slnx"
    if (-not (Test-Path -LiteralPath $solutionPath)) {
        throw "Could not find the $placeholder template solution. Run this initializer before renaming the template."
    }

    if ($projectName -cne $placeholder) {
        $excludedPath = '(^|[\\/])(\.git|\.idea|\.superpowers|\.poc|bin|obj)([\\/]|$)'
        $textExtensions = @('.cs', '.csproj', '.slnx', '.props', '.json', '.http', '.yaml', '.yml', '.tpl', '.md', '.sh', '.ps1', '.txt')
        $files = Get-ChildItem -LiteralPath $scriptRoot -Force -Recurse -File |
            Where-Object {
                $_.FullName -notmatch $excludedPath -and
                ($textExtensions -contains $_.Extension -or $_.Name -in @('Dockerfile', '.dockerignore', '.gitignore')) -and
                $_.Name -notin @('init.ps1', 'init.sh')
            }

        foreach ($file in $files) {
            $content = [IO.File]::ReadAllText($file.FullName)
            $content = $content.Replace($placeholder, $projectName).Replace($lowerPlaceholder, $projectNameLower)
            [IO.File]::WriteAllText($file.FullName, $content, [Text.UTF8Encoding]::new($false))
        }

        $filesToRename = Get-ChildItem -LiteralPath $scriptRoot -Force -Recurse -File |
            Where-Object { $_.FullName -notmatch $excludedPath -and $_.Name.Contains($lowerPlaceholder, [StringComparison]::OrdinalIgnoreCase) }
        foreach ($file in $filesToRename) {
            $newName = $file.Name.Replace($placeholder, $projectName).Replace($lowerPlaceholder, $projectNameLower)
            Rename-Item -LiteralPath $file.FullName -NewName $newName
        }

        $directoriesToRename = Get-ChildItem -LiteralPath $scriptRoot -Force -Recurse -Directory |
            Where-Object { $_.FullName -notmatch $excludedPath -and $_.Name.Contains($lowerPlaceholder, [StringComparison]::OrdinalIgnoreCase) } |
            Sort-Object { $_.FullName.Length } -Descending
        foreach ($directory in $directoriesToRename) {
            $newName = $directory.Name.Replace($placeholder, $projectName).Replace($lowerPlaceholder, $projectNameLower)
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
