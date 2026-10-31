#!/usr/bin/env bash
set -euo pipefail
project_root=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)
version=$(sed -n 's/.*<Version>\([^<]*\)<\/Version>.*/\1/p' "$project_root/src/BBTools.LinuxDesktop/BBTools.LinuxDesktop.csproj")
if [[ ! $version =~ ^[0-9]+\.[0-9]+\.[0-9]+(-linux-beta\.[1-9][0-9]*)?$ ]]; then
  echo 'Expected a three-part release or explicit Linux beta version.' >&2; exit 1
fi
node -e "require('$project_root/scripts/linux-release-metadata.cjs')('$project_root')"
mkdir -p "$project_root/artifacts"
package_stage=$(mktemp -d "$project_root/artifacts/linux-package.XXXXXX")
cleanup() {
  case "$package_stage" in "$project_root"/artifacts/linux-package.*) rm -rf -- "$package_stage";; *) echo 'Invalid package staging path' >&2; exit 1;; esac
}
trap cleanup EXIT
for target in LinuxDesktop LinuxProbe; do
  dotnet publish "$project_root/src/BBTools.$target/BBTools.$target.csproj" -c Release -r linux-x64 --self-contained true \
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -p:DebugSymbols=false \
    -o "$package_stage/$target" --nologo
  cp "$project_root/docs/linux-support.md" "$package_stage/$target/README.md"
  chmod +x "$package_stage/$target/BBTools.$target"
  if [[ $(find "$package_stage/$target" -mindepth 1 -maxdepth 1 | wc -l) -ne 2 ]]; then
    echo "Unexpected $target sidecars: packaging must contain its executable and README.md only." >&2
    exit 1
  fi
  if [[ $target == LinuxDesktop ]]; then archive="BBTools-v$version-linux-x64.tar.gz"; else archive="BBTools-LinuxProbe-v$version-linux-x64.tar.gz"; fi
  tar -czf "$project_root/artifacts/$archive" -C "$package_stage/$target" "BBTools.$target" README.md
  if [[ $target == LinuxDesktop ]]; then
    python3 - "$package_stage/$target" "$project_root/artifacts/BBTools-v$version-linux-x64.zip" <<'PY'
import pathlib, sys, zipfile
source = pathlib.Path(sys.argv[1])
with zipfile.ZipFile(sys.argv[2], 'w', compression=zipfile.ZIP_DEFLATED, compresslevel=9) as archive:
    for name in ('BBTools.LinuxDesktop', 'README.md'):
        archive.write(source / name, name)
PY
  fi
done > "$project_root/artifacts/linux-publish.log"
(cd "$project_root/artifacts" && sha256sum "BBTools-v$version-linux-x64.tar.gz" "BBTools-v$version-linux-x64.zip" "BBTools-LinuxProbe-v$version-linux-x64.tar.gz" > SHA256SUMS-linux.txt)
echo 'Packaged Linux desktop, Nexus ZIP and read-only probe.'
