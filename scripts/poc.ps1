param([ValidateSet('postgres','sqlserver')][string]$Provider='postgres')
& powershell.exe -NoProfile -ExecutionPolicy Bypass -File "$PSScriptRoot/poc-up.ps1" -Provider $Provider
if($LASTEXITCODE -ne 0){exit $LASTEXITCODE}
& powershell.exe -NoProfile -ExecutionPolicy Bypass -File "$PSScriptRoot/poc-verify.ps1" -Provider $Provider
exit $LASTEXITCODE
