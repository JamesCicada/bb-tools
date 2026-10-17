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

Copy-Item -LiteralPath (Join-Path $projectRoot 'README.md') -Destination $package
# Standalone README links must still work without bundling the source documents.
$readmePath = Join-Path $package 'README.md'
$readme = [System.IO.File]::ReadAllText($readmePath)
foreach ($document in @('CONTRIBUTING.md', 'TODO.md', 'LICENSE', 'docs/memory-research.md')) {
    $readme = $readme.Replace("]($document)", "](https://github.com/JamesCicada/bb-tools/blob/v$releaseVersion/$document)")
}
[System.IO.File]::WriteAllText($readmePath, $readme, [System.Text.UTF8Encoding]::new($false))
# Fail rather than silently ship publish sidecars or development documents.
$expected = @('BBTools.exe', 'README.md')
$entries = @(Get-ChildItem -LiteralPath $package -Force)
if ($entries.Count -ne 2 -or @($entries | Where-Object { $_.PSIsContainer -or $_.Name -notin $expected }).Count -ne 0) {
    throw 'Release package must contain only BBTools.exe and README.md.'
}
$archive = Join-Path $releaseRoot "$releaseName.zip"
Compress-Archive -LiteralPath (Join-Path $package 'BBTools.exe'), (Join-Path $package 'README.md') -DestinationPath $archive -Force
$digest = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant()
$checksum = Join-Path $releaseRoot 'SHA256SUMS.txt'
[System.IO.File]::WriteAllText($checksum, "$digest  $releaseName.zip`n", [System.Text.UTF8Encoding]::new($false))
Write-Output "Package: $archive"
Write-Output "SHA256: $digest"
Write-Output "Publish directory: $package"
