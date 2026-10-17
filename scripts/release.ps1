param([string]$Configuration = 'Release')

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
[xml]$project = Get-Content -LiteralPath (Join-Path $projectRoot 'src/BBTools/BBTools.csproj')
$releaseVersion = [string]$project.Project.PropertyGroup.Version
if ($releaseVersion -notmatch '^\d+\.\d+\.\d+$') { throw 'Expected a three-part release version in BBTools.csproj.' }
$releaseName = "BBTools-v$releaseVersion-win-x64"
$releaseRoot = Join-Path $projectRoot 'artifacts'
# Unique staging prevents files from an earlier publish entering this archive.
$staging = Join-Path $releaseRoot ("stage-" + [guid]::NewGuid().ToString('N'))
$package = Join-Path $staging $releaseName
New-Item -ItemType Directory -Path $package -Force | Out-Null

dotnet publish (Join-Path $projectRoot 'src/BBTools/BBTools.csproj') -c $Configuration -r win-x64 --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:PublishTrimmed=false `
    -p:DebugType=None -p:DebugSymbols=false --output $package --nologo
if ($LASTEXITCODE -ne 0) { throw 'Release publish failed.' }

foreach ($document in @('README.md', 'LICENSE', 'CHANGELOG.md', 'CONTRIBUTING.md', 'TODO.md')) {
    Copy-Item -LiteralPath (Join-Path $projectRoot $document) -Destination $package
}
New-Item -ItemType Directory -Path (Join-Path $package 'docs') -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $projectRoot 'docs/memory-research.md') -Destination (Join-Path $package 'docs')
$archive = Join-Path $releaseRoot "$releaseName.zip"
Compress-Archive -LiteralPath $package -DestinationPath $archive -Force
$digest = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant()
$checksum = Join-Path $releaseRoot 'SHA256SUMS.txt'
[System.IO.File]::WriteAllText($checksum, "$digest  $releaseName.zip`n", [System.Text.UTF8Encoding]::new($false))
Write-Output "Package: $archive"
Write-Output "SHA256: $digest"
Write-Output "Publish directory: $package"
