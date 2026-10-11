#!/usr/bin/env bash
set -euo pipefail
script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
bash "$script_dir/poc-up.sh" "$@"
bash "$script_dir/poc-verify.sh" "$@"
