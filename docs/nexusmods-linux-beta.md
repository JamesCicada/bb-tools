# Nexus Mods Linux file — ready-to-upload details

Destination: [BB TOOLS, Bloodborne mod 614](https://www.nexusmods.com/bloodborne/mods/614?tab=files).

- **File name:** BB Tools Linux x64 BETA — NOT TESTED
- **Version:** `0.6.8-linux-beta.1`
- **Category:** Optional files
- **Archive:** `BBTools-v0.6.8-linux-beta.1-linux-x64.zip`
- **Mod-manager download:** Disabled (standalone desktop tool)
- **Update the overall mod version:** No; Windows remains `0.6.8`
- **Replace/archive the Windows file:** No

## File description

**BETA — NOT TESTED IN-GAME. Linux testers needed.**

Experimental native Linux x64/glibc desktop for Bloodborne 1.09/native bbport.
X11/XWayland required. Extract, then run `chmod +x BBTools.LinuxDesktop` and
`./BBTools.LinuxDesktop`. Executable and README only; .NET is bundled.

Starts read-only. Includes process selection, permission diagnostics, player
resources and opt-in health/Echoes/attribute editing plus selected combat flags.
This beta has fewer features than Windows: no item spawning, native menus/warps,
boss revival, FOV, multipliers, Insight/free progression, presets or Discord yet.

CI checks builds, synthetic process access, UI and startup. **No live Linux game
compatibility is confirmed.** Windows test confirmations do not apply to Linux.
Back up your save before testing edits. Report distro, display server, runtime
version and relevant logs on [GitHub](https://github.com/JamesCicada/bb-tools/issues).

## Initial upload and automation

The official Nexus upload action adds versions to an **existing file group**. The
first Linux optional file must be created separately; never reuse the Windows main
file ID. The current Windows stable-release automation deliberately rejects
prereleases. This document and the CI-generated ZIP prepare that initial Linux
file without risking replacement of the Windows download.
