#!/usr/bin/env bash
# Mechanical convention checks (run by CI). Exits non-zero on the first violated rule.
set -u
fail=0

# Module paths: resolve via Paths.ToResource() / window.EPT_*_BASE_URL, never a literal /EPiServer/ prefix.
hits=$(grep -rIn --include='*.cs' --include='*.js' --include='*.cshtml' '/EPiServer/' src \
  --exclude-dir=bin --exclude-dir=obj --exclude-dir=node_modules --exclude-dir=modules || true)
if [ -n "$hits" ]; then
  echo "Hardcoded /EPiServer/ path (use Paths.ToResource or window.EPT_BASE_URL):"; echo "$hits"; fail=1
fi

# Packed libraries: floating versions (12.*) pack as ">= newest at pack time" and silently raise
# the minimum CMS version every release. Use the ranges in src/Directory.Build.props.
hits=$(grep -n -E "PackageReference[^>]*Version=\"[^\"]*\*" src/EditorPowertools/*.csproj src/EditorPowertools.*/*.csproj \
  | grep -v -E "SampleSite|Tests" || true)
if [ -n "$hits" ]; then
  echo "Floating package version in a packed library (use a range from src/Directory.Build.props):"; echo "$hits"; fail=1
fi

exit $fail
