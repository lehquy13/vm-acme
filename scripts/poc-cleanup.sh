#!/usr/bin/env bash
set -euo pipefail
provider=postgres
flags=()
while (($#)); do
  case "$1" in
    --provider) provider="${2:?--provider requires a value}"; shift 2 ;;
    --reset) flags+=(-Reset); shift ;;
    --registry) flags+=(-Registry); shift ;;
    --stop) flags+=(-Stop); shift ;;
    *) printf 'Unknown option: %s\n' "$1" >&2; exit 2 ;;
  esac
done
case "$provider" in postgres|sqlserver) ;; *) echo 'Provider must be postgres or sqlserver.' >&2; exit 2;; esac
script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
exec powershell.exe -NoProfile -ExecutionPolicy Bypass -File "$(cygpath -w "$script_dir/poc-cleanup.ps1")" -Provider "$provider" "${flags[@]}"
