#!/usr/bin/env bash
# Packs every package in both flavors (see EptCmsFlavor in src/Directory.Build.props):
#   main ids     net8.0 (CMS 12) + net10.0 (CMS 13)
#   ".Cms12" ids net8.0 only, for CMS 12 sites running on .NET 10
# Usage: scripts/pack-all.sh VERSION OUTDIR
set -euo pipefail
version=$1
out=$2

for flavor in "" Cms12; do
  for lib in src/EditorPowertools src/EditorPowertools.Forms src/EditorPowertools.Graph; do
    dotnet pack "$lib" -c Release -p:Version="$version" -p:EptCmsFlavor="$flavor" -o "$out"
  done
done
