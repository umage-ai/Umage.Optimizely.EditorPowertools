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

exit $fail
