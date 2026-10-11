#!/usr/bin/env bash
set -euo pipefail

script_root="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$script_root"

placeholder='A''cme'
lower_placeholder="${placeholder,,}"
project_name="${1:-}"
if [[ -z "$project_name" ]]; then
  printf 'Project name [acme]: '
  IFS= read -r project_name
fi
project_name="${project_name:-$lower_placeholder}"

if [[ ! "$project_name" =~ ^[A-Za-z][A-Za-z0-9_]*$ ]]; then
  printf '%s\n' 'Project name must start with a letter and contain only letters, digits, or underscores.' >&2
  exit 2
fi

project_name="${project_name^}"
project_name_lower="${project_name,,}"
project_name_upper="${project_name^^}"
if [[ "$project_name_lower" == *_ ]]; then
  printf '%s\n' 'Project name must not end with an underscore.' >&2
  exit 2
fi
deployment_slug="${project_name_lower//_/-}"
deployment_name="vm-${deployment_slug}"
if (( ${#deployment_slug} > 40 )); then
  printf '%s\n' 'Deployment slug must be 40 characters or fewer.' >&2
  exit 2
fi
if [[ ! -f "vm-${lower_placeholder}.slnx" ]]; then
  printf 'Could not find the %s template solution. Run this initializer before renaming the template.\n' "$placeholder" >&2
  exit 2
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
    content="${content//${prefix}SNAKE_LOWER__/vm_${project_name_lower}}"
    content="${content//${prefix}SNAKE_UPPER__/VM_${project_name_upper}}"
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
      -e "s|${sentinel_prefix}SNAKE_LOWER__|vm_${project_name_lower}|g" \
      -e "s|${sentinel_prefix}SNAKE_UPPER__|VM_${project_name_upper}|g" \
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
    ! -name 'init.sh' ! -name 'init.ps1' -print0)

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
    -type f -iname "*${lower_placeholder}*" ! -name 'init.sh' ! -name 'init.ps1' -print0)

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

  expected_solution="vm-${project_name_lower}.slnx"
  slug_solution="${deployment_name}.slnx"
  if [[ "$slug_solution" != "$expected_solution" && -f "$slug_solution" ]]; then
    mv -- "$slug_solution" "$expected_solution"
  fi
fi

solution_file="vm-${project_name_lower}.slnx"
dotnet restore "$solution_file"
dotnet build "$solution_file" --no-restore
