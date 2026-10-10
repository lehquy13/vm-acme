#!/usr/bin/env bash
set -euo pipefail

script_root="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$script_root"

placeholder='A''cme'
lower_placeholder="${placeholder,,}"
project_name="${1:-}"
if [[ -z "$project_name" ]]; then
  printf 'Project name [Acme]: '
  IFS= read -r project_name
fi
project_name="${project_name:-$placeholder}"

if [[ ! "$project_name" =~ ^[A-Za-z][A-Za-z0-9_]*$ ]]; then
  printf '%s\n' 'Project name must start with a letter and contain only letters, digits, or underscores.' >&2
  exit 2
fi

project_name_lower="${project_name,,}"
if [[ ! -f "$placeholder.slnx" ]]; then
  printf 'Could not find the %s template solution. Run this initializer before renaming the template.\n' "$placeholder" >&2
  exit 2
fi

if [[ "$project_name" != "$placeholder" ]]; then
  while IFS= read -r -d '' file; do
    sed -i "s|${placeholder}|${project_name}|g; s|${lower_placeholder}|${project_name_lower}|g" "$file"
  done < <(find . \
    -path './.git' -prune -o \
    -path './.idea' -prune -o \
    -path './.superpowers' -prune -o \
    -path '*/bin' -prune -o \
    -path '*/obj' -prune -o \
    -type f \( -name '*.cs' -o -name '*.csproj' -o -name '*.slnx' -o -name '*.props' -o -name '*.json' -o -name '*.http' -o -name '*.yaml' -o -name '*.yml' -o -name '*.tpl' -o -name '*.md' -o -name '*.txt' -o -name 'Dockerfile' -o -name '.dockerignore' -o -name '.gitignore' \) \
    ! -name 'init.sh' ! -name 'init.ps1' -print0)

  while IFS= read -r -d '' path; do
    directory="${path%/*}"
    file_name="${path##*/}"
    mv -- "$path" "$directory/${project_name}${file_name#${placeholder}}"
  done < <(find . \
    -path './.git' -prune -o \
    -path './.idea' -prune -o \
    -path './.superpowers' -prune -o \
    -path '*/bin' -prune -o \
    -path '*/obj' -prune -o \
    -type f -name "${placeholder}*" -print0)

  for path in src/"${placeholder}".* tools/"${placeholder}".* migrations/"${placeholder}".* tests/"${placeholder}".*; do
    [[ -d "$path" ]] || continue
    directory="${path%/*}"
    directory_name="${path##*/}"
    mv -- "$path" "$directory/${project_name}${directory_name#${placeholder}}"
  done

  if [[ -d deploy/helm/acme ]]; then
    mv -- deploy/helm/acme "deploy/helm/${project_name_lower}"
  fi
fi

dotnet restore "${project_name}.slnx"
dotnet build "${project_name}.slnx" --no-restore
