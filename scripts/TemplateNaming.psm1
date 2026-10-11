Set-StrictMode -Version Latest

function Get-TemplateNames([string]$RepositoryName) {
    if ([string]::IsNullOrWhiteSpace($RepositoryName)) {
        throw 'Repository name is required.'
    }

    $slug = $RepositoryName.Trim().ToLowerInvariant()
    if ($slug.StartsWith('vm-', [StringComparison]::Ordinal)) {
        $slug = $slug.Substring(3)
    }
    if ($slug -notmatch '^[a-z][a-z0-9]*(?:[-_][a-z0-9]+)*$') {
        throw 'Repository name must contain letter or digit segments separated by hyphens or underscores.'
    }
    if ($slug.Length -gt 40) {
        throw 'Repository name must be 40 characters or fewer after removing the vm- prefix.'
    }

    $hyphenParts = $slug -split '-'
    $brand = ($hyphenParts | ForEach-Object {
        (($_ -split '_') | ForEach-Object { $_.Substring(0, 1).ToUpperInvariant() + $_.Substring(1) }) -join '_'
    }) -join ''
    $deploymentSlug = $slug.Replace('_', '-')
    $environmentPrefix = ('vm-' + $deploymentSlug).Replace('-', '_').ToUpperInvariant()
    [pscustomobject]@{
        Brand = $brand
        BrandLower = $brand.ToLowerInvariant()
        BrandUpper = $brand.ToUpperInvariant()
        DeploymentSlug = $deploymentSlug
        DeploymentName = "vm-$deploymentSlug"
        EnvironmentPrefix = $environmentPrefix
        MetricPrefix = "vm_$($deploymentSlug.Replace('-', '_'))"
        SolutionFile = "vm-$($brand.ToLowerInvariant()).slnx"
    }
}

Export-ModuleMember -Function Get-TemplateNames
