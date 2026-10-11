$ErrorActionPreference='Stop'
. "$PSScriptRoot/poc-common.ps1"
$evidence=Join-Path $State 'dev-validation'
New-Item -ItemType Directory -Force -Path $evidence | Out-Null
Assert-PocContext
Ensure-Namespace 'vm-acme-poc-validation'
$inputs=@('--set-string','image.repository=validation.invalid/project/api','--set-string','image.tag=validation-v2',
    '--set-string','migration.image.repository=validation.invalid/project/migrator','--set-string','migration.image.tag=validation-v2',
    '--set-string','application.connectionSecret=validation-api-db','--set-string','migration.connectionSecret=validation-migration-db',
    '--set-string','secrets.vaultUri=https://validation.vault.azure.net/',
    '--set-string','application.vaultConnectionSecret=validation-api-db','--set-string','migration.vaultConnectionSecret=validation-migration-db',
    '--set-string','application.vaultJwtSecret=validation-jwt',
    '--set-string','application.podLabels.azure\.workload\.identity/use=true',
    '--set-string','migration.podLabels.azure\.workload\.identity/use=true',
    '--set-string','application.serviceAccountAnnotations.azure\.workload\.identity/client-id=00000000-0000-0000-0000-000000000001',
    '--set-string','migration.serviceAccountAnnotations.azure\.workload\.identity/client-id=00000000-0000-0000-0000-000000000002',
    '--set-string','imagePullSecrets[0].name=validation-registry',
    '--set-string','ingress.host=validation.invalid','--set','ingress.enabled=true',
    '--set-string','ingress.className=validation','--set-string','ingress.tls[0].secretName=validation-tls',
    '--set-string','ingress.tls[0].hosts[0]=validation.invalid')
foreach($mode in @('KeyVaultRequired','KeyVaultWithFallback')){
  foreach($orchestration in @('helm','argocd')){
    foreach($provider in @('PostgreSql','SqlServer')){
        $settings=@('-f',"$Chart/values-dev.yaml")+$inputs+@('--set-string',"secrets.mode=$mode")
        if($mode -eq 'KeyVaultWithFallback'){$settings+=@('--set-string','application.fallbackSecretName=validation-api-fallback','--set-string','migration.fallbackSecretName=validation-migration-fallback')}
        $settings+=@('--set-string',"persistence.provider=$provider",'--set-string',"migration.orchestration=$orchestration")
        Invoke-PocHelm (@('lint',$Chart)+$settings) | Out-Null
        $render=(Invoke-PocHelm (@('template','vm-acme-dev',$Chart,'--namespace','vm-acme-poc-validation')+$settings)) -join "`n"
        $file=Join-Path $evidence "$mode-$provider-$orchestration.yaml"
        Write-Utf8 $file $render
        # Admission/schema validation on Minikube only; no resources created.
        Write-Utf8 "$file.dry-run.txt" ((K @('-n','vm-acme-poc-validation','apply','--dry-run=server','-f',$file)) -join "`n")
    }
  }
}
Write-Host 'DEV STATIC PASS: 2 secret modes x 2 providers x 2 orchestration modes; Helm lint/render + Minikube server dry-run. No company deployment or Argo sync was executed.'
