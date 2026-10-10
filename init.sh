#!/usr/bin/env sh
set -eu

cd "$(dirname "$0")"
dotnet restore Template.slnx
dotnet build Template.slnx --no-restore
