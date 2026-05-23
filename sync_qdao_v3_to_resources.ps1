# Sync the qdao v3 atlases (UI bg pieces, weapon icons, character portraits)
# from the FairyGUI source tree into Unity Resources, where the C# screens
# load them via `Resources.Load<Texture2D>("UI/qdao_v3/...")` (see
# `client/unity/Assets/Scripts/UI/V3Art.cs`).
#
# Why a parallel copy rather than referencing the FairyGUI source folder
# directly: Unity won't import textures from outside `Assets/`, and the
# FairyGUI free edition can't CLI-publish the new package entries into
# `qdao_fui.bytes`. Until somebody opens the editor and presses F8, this
# is the only way the new art shows up at runtime.
#
# Run:
#     pwsh -File client/unity/sync_qdao_v3_to_resources.ps1
#
# Idempotent: overwrites destination PNGs in place.

[CmdletBinding()]
param(
    [string]$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
)

$src = Join-Path $RepoRoot 'client\fairygui\qdao\assets\qdao\v3'
$dst = Join-Path $RepoRoot 'client\unity\Assets\Resources\UI\qdao_v3'

if (-not (Test-Path $src)) {
    Write-Error "FairyGUI v3 source not found: $src"
    exit 1
}

$subdirs = @('ui', 'icons_weapon', 'characters')
foreach ($d in $subdirs) {
    $target = Join-Path $dst $d
    New-Item -ItemType Directory -Force $target | Out-Null
    $copied = 0
    Get-ChildItem (Join-Path $src "$d\*.png") | ForEach-Object {
        Copy-Item $_.FullName -Destination $target -Force
        $copied++
    }
    Write-Host ("[{0,-13}] {1,3} png -> {2}" -f $d, $copied, ($target.Substring($RepoRoot.Length).TrimStart('\')))
}

$totalMB = '{0:N1}' -f ((Get-ChildItem $dst -Recurse -File | Measure-Object -Property Length -Sum).Sum / 1MB)
Write-Host "[done] total payload: $totalMB MB"
