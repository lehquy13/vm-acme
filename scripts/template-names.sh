template_names() {
  local slug="$1" part
  [[ "$slug" == vm-* ]] && slug="${slug#vm-}"
  slug="${slug,,}"
  [[ "$slug" =~ ^[a-z][a-z0-9]*([-_][a-z0-9]+)*$ ]] || return 2
  (( ${#slug} <= 40 )) || return 2
  local brand
  brand="$(printf '%s' "$slug" | awk -F '-' '{ for (i = 1; i <= NF; i++) { n = split($i, parts, "_"); segment = ""; for (j = 1; j <= n; j++) { segment = segment toupper(substr(parts[j], 1, 1)) substr(parts[j], 2); if (j < n) segment = segment "_" } $i = segment } } 1' OFS='')"
  TEMPLATE_BRAND="$brand"
  TEMPLATE_BRAND_LOWER="${brand,,}"
  TEMPLATE_BRAND_UPPER="${brand^^}"
  TEMPLATE_DEPLOYMENT_SLUG="${slug//_/-}"
  TEMPLATE_DEPLOYMENT_NAME="vm-${TEMPLATE_DEPLOYMENT_SLUG}"
  TEMPLATE_ENVIRONMENT_PREFIX="VM_${TEMPLATE_DEPLOYMENT_SLUG//-/_}"
  TEMPLATE_ENVIRONMENT_PREFIX="${TEMPLATE_ENVIRONMENT_PREFIX^^}"
  TEMPLATE_METRIC_PREFIX="vm_${TEMPLATE_DEPLOYMENT_SLUG//-/_}"
  TEMPLATE_SOLUTION_FILE="vm-${TEMPLATE_BRAND_LOWER}.slnx"
}
