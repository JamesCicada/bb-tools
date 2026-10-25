#!/usr/bin/env bash
set -euo pipefail
project_root=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)
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
  archive="BBTools-$target-preview-linux-x64.tar.gz"
  tar -czf "$project_root/artifacts/$archive" -C "$package_stage/$target" "BBTools.$target" README.md
done > "$project_root/artifacts/linux-publish.log"
(cd "$project_root/artifacts" && sha256sum BBTools-LinuxDesktop-preview-linux-x64.tar.gz BBTools-LinuxProbe-preview-linux-x64.tar.gz > BBTools-Linux-SHA256SUMS.txt)
echo 'Packaged Linux desktop preview and read-only probe.'
