# Launcher and emulator compatibility

Last updated: 2026-10-31. Results apply to the tested BB Tools/game/runtime combination;
a launcher appearing in the process picker does not establish compatibility.

## Status key

| Status | Meaning |
| --- | --- |
| ✅ Perfectly fine | The recorded feature set works in live testing on this configuration. |
| ⚠️ Unstable | Works partially or intermittently; see the recorded symptoms. |
| ❌ Broken | A tested configuration regressed or produces incorrect behavior. |
| ⛔ Not working | A tested configuration cannot perform the named operation or connect. |
| ⌛ Not tested | No live result recorded. This is not a failure claim. |

## Tested launchers and emulators

| Launcher / emulator | Process | Game profile | BB Tools | Status | Test scope / limitations |
| --- | --- | --- | --- | --- | --- |
| [Supermedo Bloodborne PC](https://github.com/Supermedo/bloodborne_pc) native Windows launcher | `bb-probe.exe` (runtime), `Bloodborne.exe` (launcher) | CUSA03173 1.09 | 0.6.6 | ✅ Perfectly fine | Confirmed player/combat tools, spawning, travel, supported boss revival/retry, presets/startup FOV, hotkeys, library, save tools, logging and diagnostics. Active features, including durability, are tested and confirmed for 0.6.6. Exact launcher build was not recorded. |
| [shadPS4](https://github.com/shadps4-emu/shadPS4) | `shadPS4.exe` | CUSA03173 1.09 | 0.4.0 development | ✅ Perfectly fine | Tested and confirmed on 2026-10-18; officially supported. Manual and automatic connection require no experimental opt-in. Exact emulator build/patch list was not recorded. Noclip/free camera were disabled in the confirmed build; photo mode/noclip remain disabled and hidden in local 0.6.0. Active target tools are included in the 0.6.6 confirmation. |
| Native Linux bbport 0.7 | `bbprobe` (or `bb-probe`), not `bloodborne-bbpo` | Reported Bloodborne runtime; exact game profile/distro not supplied | Previous Linux preview | ✅ Reported working | Community report: launch through bbport and attach to bbprobe. Individual tested features were not specified. This does not confirm the new 0.7.0 native actions; those remain NOT TESTED IN-GAME. |
| Other Bloodborne PC forks / launchers | Varies | Varies | Upcoming test build | ⌛ Not tested | Test each fork/build separately, including loaded image checks and game-thread operations. Native launcher compatibility does not transfer automatically. |
| [Nightwatch Co-op](https://github.com/vladtepes22/NightwatchCoop) | Reported `bb-probe.exe` | 1.09; exact runtime release unknown | 0.6.10 | ⌛ Not tested | 0.6.9 logs show an item-grant detour rejected before submission. A strict observer-ABI adapter now supports item/gem grants and a Level-up tracing hook in code; menu checks are scoped to the requested factory. NOT TESTED IN-GAME. Direct chalice travel with a replaced flag reader remains unsupported. See [research and tests](nightwatch-compatibility.md). |

## Feature-specific exceptions

| Configuration / feature | Status | Notes |
| --- | --- | --- |
| Native PC 1.09: weapon repair, threshold auto-repair and keep-full durability | ✅ Perfectly fine | Tested and confirmed with the active Windows features in 0.6.6; the earlier report is superseded. |
| Native PC 1.09: FOV and camera distance/height overrides | ⚠️ Unstable | Controls are tested and confirmed, but overrides can cause performance drops and visual issues. Restore defaults if problems occur. |
| Native PC 1.09: Micolash, Gehrman and Moon Presence revival | ✅ Perfectly fine | Active boss controls are included in the 0.6.6 confirmation. Ending bosses modify story-route flags; use a separate practice save. |

## Version 0.6.0

God mode + Ignore hits together and first-encounter boss revival are ✅ tested
and confirmed on Bloodborne PC. The exact updated native launcher build was not
recorded. This confirmation does not extend new features to shadPS4 automatically.
Echoes death protection, target controls and Discord activity retain ⌛ pending
live-test status. Photo mode/free camera and flight/noclip remain disabled and hidden.

## Recording a test

Record the launcher/emulator name and exact version, Windows version, BB Tools
version/commit, game title ID/version, enabled mods/patches, tested feature and result.
Include load/warp, death/respawn, disable/reset and disconnect behavior where relevant.
For failures, include the app status and relevant session log, with personal paths
redacted. Only move a row to tested status after a live test; automated fixture checks
do not establish launcher/emulator compatibility.

Use [GitHub issues](https://github.com/JamesCicada/bb-tools/issues) for reports and
pull requests for additional verified configurations. Keep unsupported and not-tested
entries distinct from tested failures.

## Earlier Linux desktop preview — 2026-10-25 (historical scope)

| Runtime / environment | Status | Evidence / limitations |
| --- | --- | --- |
| Native Linux bbport (`deadinside28`, 0.6 / `8185b88`) | ⌛ Not tested live | Public loader/memory/hook source reviewed; experimental shared-data desktop preview and read-only probe. Native synthetic CI does not establish game compatibility. |
| ayozetr Linux bbport fork | ⌛ Not tested live | Candidate from public native/1.09 architecture; exact running instruction profile must validate. |
| bbhost / shadPS4 on Linux | ⌛ Not tested live | Separate runtime/profile milestones; no compatibility transferred from Windows results. |
| Ubuntu 22.04 / 24.04 x64 | ⌛ Game not tested; platform CI passes | 37 native/profile and five UI checks per OS; X11 startup, self-contained packaging and packaged startup pass. No game runs in CI. |
| Debian / Fedora / Arch / SteamOS x64 | ⌛ Not tested | Intended glibc/X11 or XWayland desktop targets; packaging, dependencies and process policy still require verification. |

See the [Linux preview guide](linux-support.md) and [source research](linux-compatibility-research.md).
The Windows release remains supported on its recorded configurations; Linux preview
scope does not change those results.

## Version 0.6.6 confirmation

All currently active Windows features are tested and confirmed as of 2026-10-26,
including Insight, free level-ups/upgrades, durability, target tools, discovery,
presets and existing player/combat/travel controls. Hidden photo/freecam/flight/
noclip and unimplemented roadmap entries are excluded. FOV/camera instability
warnings remain relevant. Runtime versions and mod lists were not recorded, so
this does not certify every launcher build or future runtime update.

**Linux 0.6.6-linux-beta.1 is BETA — NOT TESTED IN-GAME.** Ubuntu synthetic memory,
UI and startup checks do not establish game compatibility. It is a separate,
limited desktop build; no Linux game feature is marked tested and confirmed.


## Version 0.6.7 additions

The shoulder-camera offset and blood-gem editor/spawner are **not tested in-game**
on either Windows runtime. Their automated checks do not extend the 0.6.6 live
confirmation to these additions. Each control is labeled in the app. Linux remains
a separate untested beta at that version. The 0.7.0 port includes these tools, pending live Linux validation.


## Version 0.6.8 additions

NG-cycle editing, experimental photo/free camera and flight/noclip are **not tested
in-game** on Bloodborne PC or shadPS4. Exploration modes require the explicit
Settings → Experimental opt-in and start off. These controls do not extend earlier
live confirmations to the new behavior. Those features were absent from the Linux preview at the time. The 0.7.0 port now includes them; new Linux actions need live testing.
