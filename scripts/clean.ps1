[CmdletBinding(SupportsShouldProcess)]
param([switch]$IncludeBuildOutputs)

$ErrorActionPreference = 'Stop'
$projectRoot = [System.IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$projectPrefix = $projectRoot + [System.IO.Path]::DirectorySeparatorChar
$targets = [System.Collections.Generic.List[string]]::new()
foreach ($area in @('artifacts', '.research')) {
    $folder = Join-Path $projectRoot $area
    if (-not (Test-Path -LiteralPath $folder)) { continue }
    foreach ($entry in Get-ChildItem -LiteralPath $folder -Directory -Force) {
        $generated = if ($area -eq 'artifacts') { $entry.Name -match '^stage-[0-9a-f]{32}$' }
            else { $entry.Name -match '^release-smoke(?:-[0-9a-f]{32})?$' -or $entry.Name -eq '__pycache__' }
        if ($generated) { $targets.Add($entry.FullName) }
    }
}
if ($IncludeBuildOutputs) {
    foreach ($relative in @('src/BBTools/bin', 'src/BBTools/obj', 'tests/BBTools.Checks/bin', 'tests/BBTools.Checks/obj')) {
        $target = Join-Path $projectRoot $relative
        if (Test-Path -LiteralPath $target) { $targets.Add($target) }
    }
}
# Validate every absolute path and reject junctions/symlinks before any recursive removal.
foreach ($target in $targets) {
    $resolved = [System.IO.Path]::GetFullPath($target)
    if (-not $resolved.StartsWith($projectPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Cleanup target escaped the project: $resolved"
    }
    $root = Get-Item -LiteralPath $resolved -Force
    if (($root.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0 -or
        @(Get-ChildItem -LiteralPath $resolved -Recurse -Force -Attributes ReparsePoint).Count -ne 0) {
        throw "Cleanup refuses linked files/folders: $resolved"
    }
}
$removed = 0
$bytes = 0L
foreach ($target in $targets) {
    if ($PSCmdlet.ShouldProcess($target, 'Remove generated build/test folder')) {
        $size = (Get-ChildItem -LiteralPath $target -Recurse -File -Force | Measure-Object Length -Sum).Sum
        Remove-Item -LiteralPath $target -Recurse -Force
        $bytes += [long]$size
        $removed++
    }
}
Write-Output ("Removed {0} generated folders; reclaimed {1:N1} MiB. Release ZIPs and research sources retained." -f $removed, ($bytes / 1MB))
