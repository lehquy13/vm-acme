#!/usr/bin/env bash
set -euo pipefail

script_root="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$script_root"
source "$script_root/scripts/template-names.sh"

placeholder='A''cme'
lower_placeholder="${placeholder,,}"
project_name="${1:-}"
if [[ -z "$project_name" ]]; then
  printf 'Repository/project name [acme]: '
  IFS= read -r project_name
fi
project_name="${project_name:-$lower_placeholder}"

if [[ -f .template-initialized ]]; then
  printf '%s\n' 'This checkout has already been initialized. The initializer is one-time only.' >&2
  exit 2
fi
if ! template_names "$project_name"; then
  printf '%s\n' 'Repository name must use segments of letters or digits separated by hyphens or underscores, up to 40 characters.' >&2
  exit 2
fi
project_name="$TEMPLATE_BRAND"
project_name_lower="$TEMPLATE_BRAND_LOWER"
project_name_upper="$TEMPLATE_BRAND_UPPER"
deployment_slug="$TEMPLATE_DEPLOYMENT_SLUG"
environment_prefix="$TEMPLATE_ENVIRONMENT_PREFIX"
metric_prefix="$TEMPLATE_METRIC_PREFIX"
deployment_name="$TEMPLATE_DEPLOYMENT_NAME"
if [[ ! -f "vm-${lower_placeholder}.slnx" ]]; then
  printf 'Could not find the %s template solution. Run this initializer before renaming the template.\n' "$placeholder" >&2
  exit 2
fi
if [[ "$deployment_name" != "vm-$lower_placeholder" ]]; then
  origin_url="$(git remote get-url origin 2>/dev/null || true)"
  if [[ "${GITHUB_REPOSITORY:-}" == 'lehquy13/vm-acme' || "$origin_url" =~ github\.com[:/]lehquy13/vm-acme([.]git)?$ ]]; then
    printf '%s\n' 'Refusing to initialize the original template repository.' >&2
    exit 2
  fi
fi

if [[ "$project_name" != "$placeholder" ]]; then
  sentinel_prefix_for() {
    local source="$1"
    local candidate
    while :; do
      candidate="__VM_TEMPLATE_${BASHPID}_${RANDOM}_${RANDOM}__"
      if [[ "$source" != *"$candidate"* && "$project_name$project_name_lower$project_name_upper$deployment_name" != *"$candidate"* ]]; then
        printf '%s' "$candidate"
        return
      fi
    done
  }

  replace_template_tokens() {
    local content="$1"
    local prefix
    prefix="$(sentinel_prefix_for "$content")"
    content="${content//vm-acme.slnx/${prefix}SOLUTION__}"
    content="${content//vm-acme/${prefix}DEPLOY__}"
    content="${content//vm_acme/${prefix}SNAKE_LOWER__}"
    content="${content//VM_ACME/${prefix}SNAKE_UPPER__}"
    content="${content//${placeholder}/${prefix}PASCAL__}"
    content="${content//${lower_placeholder}/${prefix}LOWER__}"
    content="${content//${prefix}SOLUTION__/vm-${project_name_lower}.slnx}"
    content="${content//${prefix}DEPLOY__/${deployment_name}}"
    content="${content//${prefix}SNAKE_LOWER__/${metric_prefix}}"
    content="${content//${prefix}SNAKE_UPPER__/${environment_prefix}}"
    content="${content//${prefix}PASCAL__/${project_name}}"
    content="${content//${prefix}LOWER__/${project_name_lower}}"
    printf '%s' "$content"
  }

  while IFS= read -r -d '' file; do
    sentinel_prefix="$(sentinel_prefix_for "")"
    while grep -Fq "$sentinel_prefix" "$file"; do
      sentinel_prefix="$(sentinel_prefix_for "$sentinel_prefix")"
    done
    sed -i \
      -e "s|vm-acme\\.slnx|${sentinel_prefix}SOLUTION__|g" \
      -e "s|vm-acme|${sentinel_prefix}DEPLOY__|g" \
      -e "s|vm_acme|${sentinel_prefix}SNAKE_LOWER__|g" \
      -e "s|VM_ACME|${sentinel_prefix}SNAKE_UPPER__|g" \
      -e "s|${placeholder}|${sentinel_prefix}PASCAL__|g" \
      -e "s|${lower_placeholder}|${sentinel_prefix}LOWER__|g" \
      -e "s|${sentinel_prefix}SOLUTION__|vm-${project_name_lower}.slnx|g" \
      -e "s|${sentinel_prefix}DEPLOY__|${deployment_name}|g" \
      -e "s|${sentinel_prefix}SNAKE_LOWER__|${metric_prefix}|g" \
      -e "s|${sentinel_prefix}SNAKE_UPPER__|${environment_prefix}|g" \
      -e "s|${sentinel_prefix}PASCAL__|${project_name}|g" \
      -e "s|${sentinel_prefix}LOWER__|${project_name_lower}|g" "$file"
  done < <(find . \
    -path './.git' -prune -o \
    -path '*/.codegraph' -prune -o \
    -path '*/artifacts' -prune -o \
    -path './.idea' -prune -o \
    -path './.superpowers' -prune -o \
    -path './.poc' -prune -o \
    -path '*/bin' -prune -o \
    -path '*/obj' -prune -o \
    -type f \( -name '*.cs' -o -name '*.csproj' -o -name '*.slnx' -o -name '*.props' -o -name '*.json' -o -name '*.http' -o -name '*.yaml' -o -name '*.yml' -o -name '*.tpl' -o -name '*.md' -o -name '*.sh' -o -name '*.ps1' -o -name '*.txt' -o -name 'Dockerfile' -o -name '.dockerignore' -o -name '.gitignore' \) \
    ! -name 'init.sh' ! -name 'init.ps1' ! -name 'bootstrap-template.ps1' ! -name 'test-template-naming.ps1' ! -name 'test-template-naming.sh' -print0)

  while IFS= read -r -d '' path; do
    directory="${path%/*}"
    file_name="${path##*/}"
    if [[ "$file_name" == "vm-${lower_placeholder}.slnx" ]]; then
      renamed_file_name="${deployment_name}.slnx"
    else
      renamed_file_name="$(replace_template_tokens "$file_name")"
    fi
    mv -- "$path" "$directory/$renamed_file_name"
  done < <(find . \
    -path './.git' -prune -o \
    -path '*/.codegraph' -prune -o \
    -path '*/artifacts' -prune -o \
    -path './.idea' -prune -o \
    -path './.superpowers' -prune -o \
    -path './.poc' -prune -o \
    -path '*/bin' -prune -o \
    -path '*/obj' -prune -o \
    -type f -iname "*${lower_placeholder}*" ! -name 'init.sh' ! -name 'init.ps1' ! -name 'bootstrap-template.ps1' ! -name 'test-template-naming.ps1' ! -name 'test-template-naming.sh' -print0)

  while IFS= read -r -d '' path; do
    directory="${path%/*}"
    directory_name="${path##*/}"
    renamed_directory_name="$(replace_template_tokens "$directory_name")"
    mv -- "$path" "$directory/$renamed_directory_name"
  done < <(find . \
    -path './.git' -prune -o \
    -path '*/.codegraph' -prune -o \
    -path '*/artifacts' -prune -o \
    -path './.idea' -prune -o \
    -path './.superpowers' -prune -o \
    -path './.poc' -prune -o \
    -path '*/bin' -prune -o \
    -path '*/obj' -prune -o \
    -type d -iname "*${lower_placeholder}*" -print0 | sort -z -r)

  expected_solution="$TEMPLATE_SOLUTION_FILE"
  slug_solution="${deployment_name}.slnx"
  if [[ "$slug_solution" != "$expected_solution" && -f "$slug_solution" ]]; then
    mv -- "$slug_solution" "$expected_solution"
  fi
fi

solution_file="$TEMPLATE_SOLUTION_FILE"
dotnet restore "$solution_file"
dotnet build "$solution_file" --no-restore
