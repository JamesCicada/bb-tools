# Launcher and emulator compatibility

Last updated: 2026-10-21. Results apply to the tested BB Tools/game/runtime combination;
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
| [Supermedo Bloodborne PC](https://github.com/Supermedo/bloodborne_pc) native Windows launcher | `bb-probe.exe` (runtime), `Bloodborne.exe` (launcher) | CUSA03173 1.09 | 0.4.0 | ✅ Perfectly fine | Confirmed player/combat tools, spawning, travel, supported boss revival/retry, presets/startup FOV, hotkeys, library, save tools, logging and diagnostics. Durability is a separate outstanding feature issue below. Exact launcher build was not recorded. |
| [shadPS4](https://github.com/shadps4-emu/shadPS4) | `shadPS4.exe` | CUSA03173 1.09 | 0.4.0 development | ✅ Perfectly fine | Tested and confirmed on 2026-10-18; officially supported. Manual and automatic connection require no experimental opt-in. Exact emulator build/patch list was not recorded. Noclip/free camera were disabled in the confirmed build; photo mode/noclip remain disabled and hidden in local 0.6.0. New live enemy tracking in 0.6.0 also awaits live validation. |
| Other Bloodborne PC forks / launchers | Varies | Varies | Upcoming test build | ⌛ Not tested | Test each fork/build separately, including loaded image checks and game-thread operations. Native launcher compatibility does not transfer automatically. |

## Feature-specific exceptions

| Configuration / feature | Status | Notes |
| --- | --- | --- |
| Native PC 1.09: weapon repair, threshold auto-repair and keep-full durability | ⛔ Not working | Reported to possibly repair only the left-hand gun; right-hand repair is unconfirmed. A native inventory repair-path replacement is implemented in the development build. Await the next live retest before changing this status. |
| Native PC 1.09: FOV and camera distance/height overrides | ⚠️ Unstable | Controls are tested and confirmed, but overrides can cause performance drops and visual issues. Restore defaults if problems occur. |
| Native PC 1.09: Micolash, Gehrman and Moon Presence revival | ⌛ Not tested | Experimental profiles are enabled in the development build. Ending bosses modify story-route flags; use a separate practice save. |

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

## Linux desktop preview — 2026-10-25

| Runtime / environment | Status | Evidence / limitations |
| --- | --- | --- |
| Native Linux bbport (`deadinside28`, 0.6 / `8185b88`) | ⌛ Not tested live | Public loader/memory/hook source reviewed; experimental shared-data desktop preview and read-only probe. Native synthetic CI does not establish game compatibility. |
| ayozetr Linux bbport fork | ⌛ Not tested live | Candidate from public native/1.09 architecture; exact running instruction profile must validate. |
| bbhost / shadPS4 on Linux | ⌛ Not tested live | Separate runtime/profile milestones; no compatibility transferred from Windows results. |
| Ubuntu 22.04 / 24.04 x64 | ⌛ Platform checks pending workflow verification | Native child-process data reads/writes, headless UI, X11 startup and self-contained packaging. No game runs in CI. |
| Debian / Fedora / Arch / SteamOS x64 | ⌛ Not tested | Intended glibc/X11 or XWayland desktop targets; packaging, dependencies and process policy still require verification. |

See the [Linux preview guide](linux-support.md) and [source research](linux-compatibility-research.md).
The Windows release remains supported on its recorded configurations; Linux preview
scope does not change those results.
