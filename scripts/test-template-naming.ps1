$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'TemplateNaming.psm1') -Force

$cases = @(
    @{ Input = 'vm-orders'; Brand = 'Orders'; Deploy = 'vm-orders'; Environment = 'VM_ORDERS'; Metric = 'vm_orders'; Solution = 'vm-orders.slnx' },
    @{ Input = 'vm-order-service'; Brand = 'OrderService'; Deploy = 'vm-order-service'; Environment = 'VM_ORDER_SERVICE'; Metric = 'vm_order_service'; Solution = 'vm-orderservice.slnx' },
    @{ Input = 'Billing_Events'; Brand = 'Billing_Events'; Deploy = 'vm-billing-events'; Environment = 'VM_BILLING_EVENTS'; Metric = 'vm_billing_events'; Solution = 'vm-billing_events.slnx' },
    @{ Input = 'acme'; Brand = 'Acme'; Deploy = 'vm-acme'; Environment = 'VM_ACME'; Metric = 'vm_acme'; Solution = 'vm-acme.slnx' }
)

foreach ($case in $cases) {
    $actual = Get-TemplateNames $case.Input
    if ($actual.Brand -cne $case.Brand -or $actual.DeploymentName -cne $case.Deploy -or
        $actual.EnvironmentPrefix -cne $case.Environment -or $actual.MetricPrefix -cne $case.Metric -or
        $actual.SolutionFile -cne $case.Solution) {
        throw "Naming mismatch for '$($case.Input)': $($actual | ConvertTo-Json -Compress)"
    }
}

foreach ($invalid in @('', '-orders', 'vm-', 'orders--service', 'orders_', ('a' * 41), 'order.service')) {
    try { Get-TemplateNames $invalid | Out-Null; throw "Expected invalid repository name '$invalid' to fail." }
    catch { if ($_.Exception.Message -like 'Expected invalid repository name*') { throw } }
}

Write-Output "Passed $($cases.Count) naming cases and 7 invalid-input checks."
