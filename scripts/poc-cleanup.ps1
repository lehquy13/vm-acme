param([ValidateSet('postgres','sqlserver')][string]$Provider='postgres',[switch]$Reset,[switch]$Registry,[switch]$Stop)
$ErrorActionPreference='Stop'
. "$PSScriptRoot/poc-common.ps1"
if($Stop){ Run 'minikube' @('stop','-p',$Profile) | Out-Null; Write-Host 'PoC stopped; all persistent data retained.'; exit 0 }
Assert-PocContext
$namespace=if($Registry){$RegistryNamespace}else{"vm-acme-poc-$Provider"}
$existing=(K @('get','namespace',$namespace,'--ignore-not-found','-o','json')) -join ''
if($existing -and ($existing|ConvertFrom-Json).metadata.labels.'vm-acme.owner' -ne 'poc'){ throw 'Refusing cleanup of namespace without vm-acme.owner=poc.' }
if($Registry -and -not $Reset){throw 'Registry cleanup requires explicit --reset (deletes registry PVCs and images).'}
if($Registry){
    K @('delete','namespace',$RegistryNamespace,'--ignore-not-found','--wait=true','--timeout=180s') | Out-Null
}else{
    if($existing){
        Invoke-PocHelm @('uninstall','vm-acme','-n',$namespace,'--ignore-not-found','--wait','--timeout','180s') | Out-Null
        K @('-n',$namespace,'delete','job','vm-acme-migrate','--ignore-not-found') | Out-Null
    }
    if($Reset){
        $infraLabel=(K @('get','namespace',$InfraNamespace,'-o','jsonpath={.metadata.labels.vm-acme\.owner}')) -join ''
        if($infraLabel -ne 'poc'){throw 'Refusing database reset in namespace without ownership label.'}
        K @('delete','namespace',$namespace,'--ignore-not-found','--wait=true','--timeout=180s') | Out-Null
        K @('-n',$InfraNamespace,'delete','statefulset',$Provider,'--ignore-not-found','--wait=true','--timeout=180s') | Out-Null
        K @('-n',$InfraNamespace,'delete','pvc',"data-$Provider-0",'--ignore-not-found','--wait=true','--timeout=180s') | Out-Null
        $file=Join-Path $State "$Provider.json"
        if(Test-Path $file){[IO.File]::Delete($file)}
    }
}
Write-Host "CLEANUP PASS namespace=$namespace reset=$Reset. Other provider and company resources were not targeted."
