#!/usr/bin/env bash
set -euo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/template-names.sh"

assert_names() {
  local input="$1" brand="$2" deploy="$3" env="$4" metric="$5"
  template_names "$input"
  [[ "$TEMPLATE_BRAND" == "$brand" && "$TEMPLATE_DEPLOYMENT_NAME" == "$deploy" && \
     "$TEMPLATE_ENVIRONMENT_PREFIX" == "$env" && "$TEMPLATE_METRIC_PREFIX" == "$metric" ]] || {
    printf 'Naming mismatch for %s\n' "$input" >&2; exit 1;
  }
}

assert_names vm-orders Orders vm-orders VM_ORDERS vm_orders
assert_names vm-order-service OrderService vm-order-service VM_ORDER_SERVICE vm_order_service
assert_names Billing_Events Billing_Events vm-billing-events VM_BILLING_EVENTS vm_billing_events
assert_names acme Acme vm-acme VM_ACME vm_acme

for invalid in '' -orders vm- orders--service orders_ aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa order.service; do
  if template_names "$invalid"; then
    printf 'Expected invalid repository name %q to fail.\n' "$invalid" >&2
    exit 1
  fi
done

printf '%s\n' 'Passed 4 naming cases and 7 invalid-input checks.'
