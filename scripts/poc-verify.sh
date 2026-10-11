#!/usr/bin/env bash
set -euo pipefail
provider=postgres
while (($#)); do
  case "$1" in
    --provider) provider="${2:?--provider requires postgres or sqlserver}"; shift 2 ;;
    *) printf 'Unknown option: %s\n' "$1" >&2; exit 2 ;;
  esac
done
case "$provider" in postgres|sqlserver) ;; *) echo 'Provider must be postgres or sqlserver.' >&2; exit 2;; esac
script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
command -v powershell.exe >/dev/null || { echo 'Run in Windows Git Bash; PowerShell is required.' >&2; exit 2; }
exec powershell.exe -NoProfile -ExecutionPolicy Bypass -File "$(cygpath -w "$script_dir/poc-verify.ps1")" -Provider "$provider"
