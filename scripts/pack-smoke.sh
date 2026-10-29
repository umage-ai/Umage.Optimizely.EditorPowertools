#!/usr/bin/env bash
# Installs freshly packed packages into throwaway consumer sites and checks that each supported
# combination restores, builds and receives its module zips, and that the unsupported one fails.
# Usage: scripts/pack-smoke.sh NUPKG_DIR VERSION   (pack with scripts/pack-all.sh first)
# Use a version no earlier run has packed: NuGet caches packages by id + version.
set -euo pipefail
# pwd -W gives the native path under Git Bash on Windows, where NuGet cannot read /c/... paths.
nupkgs=$(cd "$1" && (pwd -W 2>/dev/null || pwd))
version=$2
work=$(mktemp -d)
trap 'rm -rf "$work"' EXIT
fail=0

# --- nuspec contract -------------------------------------------------------------------------
nuspec() { unzip -p "$nupkgs/$1.$version.nupkg" '*.nuspec'; }
for id in UmageAI.Optimizely.EditorPowerTools UmageAI.Optimizely.EditorPowerTools.Forms UmageAI.Optimizely.EditorPowerTools.Graph; do
  for pkg in "$id" "$id.Cms12"; do
    spec=$(nuspec "$pkg") || { echo "FAIL $pkg: package missing"; fail=1; continue; }
    if echo "$spec" | grep -E '<dependency id="(EPiServer|Optimizely)\.' | grep -vq 'version="\['; then
      echo "FAIL $pkg: unbounded CMS dependency"; fail=1
    fi
    if [[ $pkg == *.Cms12 ]]; then
      echo "$spec" | grep -q 'targetFramework="net10.0"' && { echo "FAIL $pkg: has a net10.0 group"; fail=1; }
      if [[ $id != UmageAI.Optimizely.EditorPowerTools ]] \
        && ! echo "$spec" | grep -q '<dependency id="UmageAI.Optimizely.EditorPowerTools.Cms12"'; then
        echo "FAIL $pkg: does not depend on the .Cms12 core package"; fail=1
      fi
    fi
  done
done

# --- consumer sites --------------------------------------------------------------------------
cat > "$work/nuget.config" <<EOF
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="local" value="$nupkgs" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
    <add key="optimizely" value="https://api.nuget.optimizely.com/v3/index.json" />
  </packageSources>
</configuration>
EOF

# consumer NAME TFM CMS_VERSION SUFFIX PACKAGES...
consumer() {
  local name=$1 tfm=$2 cms=$3 suffix=$4; shift 4
  local dir="$work/$name" refs=""
  mkdir -p "$dir"
  for p in "$@"; do refs+="    <PackageReference Include=\"UmageAI.Optimizely.EditorPowerTools$p$suffix\" Version=\"$version\" />"$'\n'; done
  cat > "$dir/$name.csproj" <<EOF
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <TargetFramework>$tfm</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="EPiServer.CMS" Version="$cms" />
$refs  </ItemGroup>
</Project>
EOF
  cat > "$dir/Program.cs" <<'EOF'
using UmageAI.Optimizely.EditorPowerTools.Forms.Infrastructure;
using UmageAI.Optimizely.EditorPowerTools.Graph.Infrastructure;
using UmageAI.Optimizely.EditorPowerTools.Infrastructure;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddEditorPowertools();
builder.Services.AddEditorPowertoolsForms();
builder.Services.AddEditorPowertoolsGraph();
var app = builder.Build();
app.UseEditorPowertools();
app.Run();
EOF
  cp "$work/nuget.config" "$dir/"
  echo "$dir"
}

# expect_build NAME DIR: builds and checks every module zip landed in modules/_protected.
expect_build() {
  if ! dotnet build "$2" -v q -nologo > "$2/build.log" 2>&1; then
    echo "FAIL $1: build failed"; grep -E "error" "$2/build.log" | head -20; fail=1; return
  fi
  local missing=0
  for zip in EditorPowertools EditorPowertoolsForms EditorPowertoolsGraph; do
    [ -f "$2/modules/_protected/$zip/$zip.zip" ] || { echo "FAIL $1: $zip.zip not copied"; missing=1; fail=1; }
  done
  [ $missing -eq 0 ] && echo "ok   $1"
}

all=("" .Forms .Graph)
addons=(.Forms .Graph)

expect_build "cms12-net8"  "$(consumer cms12-net8  net8.0  12.34.4 ""     "${all[@]}")"
expect_build "cms13-net10" "$(consumer cms13-net10 net10.0 13.2.0  ""     "${all[@]}")"
# Add-ons only: core arrives transitively and must still deliver its module zip.
expect_build "cms12-net10" "$(consumer cms12-net10 net10.0 12.34.4 .Cms12 "${addons[@]}")"

# The reason the .Cms12 packages exist: on a CMS 12 / .NET 10 site the main ids hand NuGet our
# net10.0 (CMS 13) dependency group, which drags CMS 13 UI packages into a CMS 12 site with only
# NU1608 warnings. If this ever stops happening, the split may no longer be needed.
dir=$(consumer cms12-net10-main net10.0 12.34.4 "" "${all[@]}")
dotnet restore "$dir" -v q > "$dir/restore.log" 2>&1 || true
if grep -qE "NU1605|NU1608.*EPiServer.Cms.UI.Core 13." "$dir/restore.log"; then
  echo "ok   cms12-net10-main mixes CMS 12 and 13 packages, as expected without .Cms12"
else
  echo "FAIL cms12-net10-main: main ids no longer pull CMS 13 into a CMS 12 / .NET 10 site; revisit the split"; fail=1
fi

exit $fail
