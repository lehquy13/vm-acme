. "$PSScriptRoot/poc-common.ps1"
function Install-PocTools {
    if ($env:OS -ne 'Windows_NT') { throw 'This bootstrap targets Windows + Docker Desktop. Run these entrypoints from Git Bash on Windows.' }
    New-Item -ItemType Directory -Force -Path $Tools | Out-Null
    $ProgressPreference='SilentlyContinue'
    if (-not (Get-Command minikube -ErrorAction SilentlyContinue)) {
        Write-Host 'Installing Minikube (per-user)...'
        Invoke-WebRequest 'https://github.com/kubernetes/minikube/releases/download/v1.39.0/minikube-windows-amd64.exe' -OutFile "$Tools/minikube.exe" -TimeoutSec 300
    }
    if (-not (Get-Command helm -ErrorAction SilentlyContinue)) {
        Write-Host 'Installing Helm (per-user)...'
        Invoke-WebRequest 'https://get.helm.sh/helm-v3.19.0-windows-amd64.zip' -OutFile "$Tools/helm.zip" -TimeoutSec 300
        Expand-Archive "$Tools/helm.zip" -DestinationPath $Tools -Force
        Copy-Item "$Tools/windows-amd64/helm.exe" "$Tools/helm.exe" -Force
    }
    if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
        if (-not (Get-Command winget -ErrorAction SilentlyContinue)) { throw 'Docker is missing and winget is unavailable. Install Docker Desktop with WSL 2 backend.' }
        Run 'winget' @('install','--id','Docker.DockerDesktop','--exact','--silent','--accept-package-agreements','--accept-source-agreements') | Out-Null
        $env:PATH="C:\Program Files\Docker\Docker\resources\bin;$env:PATH"
    }
    if (-not (Test-DockerEngine)) {
        $desktop='C:\Program Files\Docker\Docker\Docker Desktop.exe'
        if (-not (Test-Path $desktop)) { throw 'Docker Desktop installation did not provide an engine. A WSL 2 setup/reboot may be required.' }
        Start-Process $desktop -WindowStyle Hidden
        $deadline=(Get-Date).AddMinutes(3)
        do { Start-Sleep 3; $running=Test-DockerEngine } while (-not $running -and (Get-Date) -lt $deadline)
        if (-not $running) { throw 'Docker Linux engine failed to start in 180s. Check Docker Desktop, WSL 2, firmware virtualization and pending restart.' }
    }
    $osType=(Run 'docker' @('info','--format','{{.OSType}}')) -join ''
    if ($osType -ne 'linux') { throw 'Switch Docker Desktop to Linux containers.' }
}
function Test-DockerEngine {
    try { Run 'docker' @('info') 20 | Out-Null; return $true } catch {return $false}
}
function Ensure-PocCluster {
    Install-PocTools
    $running=$false
    try {
        $status=((Run 'minikube' @('status','-p',$Profile,'--output=json') 30) -join '')|ConvertFrom-Json
        $running=$status.Host -eq 'Running' -and $status.APIServer -eq 'Running' -and $status.Kubelet -eq 'Running'
    }catch{}
    if(-not $running){
        Write-Host 'Starting isolated Minikube cluster...'
        Run 'minikube' @('start','-p',$Profile,'--driver=docker','--container-runtime=docker','--cpus=4','--memory=7168','--kubernetes-version=v1.37.0','--wait-timeout=8m') | Out-Null
    }else{ Write-Host 'Reusing Ready isolated Minikube cluster.' }
    Assert-PocContext
    K @('wait','node','--all','--for=condition=Ready','--timeout=180s') | Out-Null
}
function Set-PocDockerEngine([string]$Registry,[string]$CaPath='',[bool]$Http=$true) {
    if ($Registry -notmatch '^[A-Za-z0-9.:-]+$') { throw 'Registry endpoint must be host:port without paths or shell characters.' }
    if ($Http) {
        # Minikube supplies insecure registries as dockerd flags. Adding the same
        # directive to daemon.json prevents Docker from starting; extend the unit.
        $unit=(Run 'minikube' @('-p',$Profile,'ssh','--','sudo systemctl cat docker')) -join "`n"
        $start=@($unit -split "`n" | Where-Object{$_ -like 'ExecStart=*' -and $_ -ne 'ExecStart='})[-1]
        if($start -notmatch [regex]::Escape("--insecure-registry $Registry")) {
            $override="[Service]`nExecStart=`n$start --insecure-registry $Registry`n"
            $encoded=[Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($override))
            Run 'minikube' @('-p',$Profile,'ssh','--',"sudo mkdir -p /etc/systemd/system/docker.service.d && echo $encoded | base64 -d | sudo tee /etc/systemd/system/docker.service.d/vm-acme-registry.conf >/dev/null && sudo systemctl daemon-reload && sudo systemctl restart docker && sudo systemctl restart kubelet") | Out-Null
        }
    } elseif ($CaPath) {
        if (-not (Test-Path -LiteralPath $CaPath)) { throw 'Registry CA file is missing.' }
        $encoded=[Convert]::ToBase64String([IO.File]::ReadAllBytes($CaPath))
        Run 'minikube' @('-p',$Profile,'ssh','--',"sudo mkdir -p /etc/docker/certs.d/$Registry && echo $encoded | base64 -d | sudo tee /etc/docker/certs.d/$Registry/ca.crt >/dev/null") | Out-Null
    }
    # Build/push/pull are performed by the same isolated engine reachable by kubelet.
    $environment=(Run 'minikube' @('-p',$Profile,'docker-env','--shell','powershell')) -join "`n"
    Invoke-Expression $environment
    $deadline=(Get-Date).AddSeconds(180)
    do {
        try { K @('get','nodes','--request-timeout=5s') | Out-Null; $ready=$true } catch { $ready=$false; Start-Sleep 3 }
    } while(-not $ready -and (Get-Date) -lt $deadline)
    if(-not $ready){throw 'Cluster API did not recover within 180s after updating isolated registry trust.'}
    K @('wait','node','--all','--for=condition=Ready','--timeout=180s') | Out-Null
}
