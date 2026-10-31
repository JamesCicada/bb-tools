# BB Tools for Linux · v0.7.0

Native **Linux x86-64/glibc**, Bloodborne 1.09. Windows and Linux use the same
version and gameplay backend. The Linux download is part of the shared v0.7.0
release rather than a separately versioned preview.

**Community feedback:** the previous experimental desktop worked when launched
through **bbport 0.7**, connected to **bbprobe**, rather than **bloodborne-bbpo**.
The report did not identify the tested individual features or distribution.
New native features below are **NOT TESTED IN-GAME**; automated coverage is separate
from live game confirmation. Please send session logs with the runtime/version,
feature used and what happened when reporting a problem.

## Feature coverage

| Feature group | Linux implementation | Live status |
| --- | --- | --- |
| Process selection, validation, health, stamina, Echoes, attributes and combat flags | Shared game logic + native Linux memory I/O | Community report, unspecified individual coverage |
| Insight/NG cycle, multipliers, discovery, guaranteed eligible drops, free progression and durability | Shared Windows game logic | ⌛ New Linux actions need live testing |
| Item spawning, normal/custom gems, carried-gem inspection and recipes | Shared catalogs, native requests and SQLite library | ⌛ New Linux actions need live testing |
| Lamps/DLC, boss entrances, saved/chalice locations, boss revival/first encounter and menus | Shared game requests and validation | ⌛ New Linux actions need live testing |
| FOV 38–120, camera distance/height/shoulder, movement, gravity and HUD | Shared hooks, guarded native code writes | ⌛ New Linux actions need live testing |
| Retained target details, status effects/super armor and parry practice | Shared target/practice logic | ⌛ New Linux actions need live testing |
| Presets/startup FOV, favorites, library transfer and save backup/restore | Shared data formats and SQLite | Automated coverage; installation feedback needed |
| Discord presence and optional actual game details | Linux Unix-domain desktop IPC | ⌛ Real Discord client feedback needed |
| Configurable hotkeys, experimental photo/flight/noclip and PNG filters | X11/XWayland; explicit experimental opt-in | ⌛ Not tested in-game; native Wayland windows unsupported |
| Five-session logs, update check and shared locale catalogs | Native desktop | New Linux-specific labels may fall back to English |

The desktop starts **read-only** for every connection. Startup presets never turn
editing on. Once editing is explicitly enabled, the chosen startup preset or FOV
can apply. Game changes to Echoes, attributes, Insight, NG cycle, items and boss
flags may be saved by the game. Normal close/disconnect restores owned runtime
changes; a killed trainer cannot perform cleanup. Pending cleanup keeps the
connection available so it can be retried after loading.

## Run the desktop

Extract `BBTools-v0.7.0-linux-x64.tar.gz` and run:

```sh
chmod +x BBTools.LinuxDesktop
./BBTools.LinuxDesktop
```

The archive contains its executable and README.md only. The .NET runtime and native
Avalonia assets are bundled; no SDK is needed to run it. Linux system libraries are
still required: glibc, libstdc++, ICU, fontconfig, X11, ICE/SM and normal graphics
libraries. X11/XWayland is the first display target. A Wayland-only session without
XWayland is not yet a supported configuration; native Wayland setup is deferred.
Install fonts for the languages you use; bundling Inter does not supply all CJK or
Arabic glyphs. musl/Alpine and ARM64 packages remain out of scope for this milestone.

1. Start bbport and load a character.
2. Refresh the process list and select its **bbprobe** or **bb-probe** engine (not `bloodborne-bbpo`), then Connect.
3. If access is denied under Yama scope 1, use the launch card to start your installed
   AppImage, launcher or `run.sh` from the tool, then refresh/connect. AppImages
   commonly use `["--play"]`; `run.sh` commonly uses `[]`. The launcher must remain
   in the tool's descendant process tree. Arguments are a JSON array, not shell code.
4. Only after the displayed values match your character, enable game
   editing for that connection. Disable it to restore owned flags.

Launching as a parent is not a bypass for every policy. Yama scope 2 requires
administrative tracing capability; scope 3 disallows attachment. Same-user processes
can be denied by dumpability, security modules or namespaces. The tool does not
change sysctls, install capabilities, elevate itself or patch a launcher.
Flatpak, Steam pressure-vessel/Game Mode and other containers need separate checks.
Running the Windows WPF executable under Wine is not the native Linux backend.

Settings: `$XDG_CONFIG_HOME/bb-tools/linux-settings.json`, falling back to
`~/.config/bb-tools/linux-settings.json`. Logs prefer `logs/` beside the binary;
a read-only install falls back to `$XDG_STATE_HOME/bb-tools/logs` or
`~/.local/state/bb-tools/logs`. Editing locks are local to the game PID/start time.

## Read-only probe

```sh
chmod +x BBTools.LinuxProbe
./BBTools.LinuxProbe --diagnose
./BBTools.LinuxProbe --list
./BBTools.LinuxProbe --pid 12345 > bbtools-linux-probe.json
```

This executable never enables writes. It reports platform/access-policy metadata or
health, stamina, level, Echoes and attributes as JSON, without character names,
saves, environment values or credentials. It rejects ambiguous images and caps
scanning at 20 seconds/512 MiB. Image discovery uses a guest instruction anchor and
all seven core fingerprints; it does not assume the launcher ELF is the game image.
Different builds/hooks remain subject to strict validation.

## Compatibility assessment

| Runtime/environment | Evidence | Status |
| --- | --- | --- |
| Native bbport 0.7 | Community report: previous Linux preview worked via bbprobe | Reported working; individual features/distro unspecified |
| ayozetr/bloodborne_linux | Public fork documents native bbport and 1.09 | Candidate; exact running profile must validate |
| droogie/bbhost | Public project documents Linux and original 1.09 guest execution | Separate runtime; hooks/content profile require validation |
| shadPS4 Linux | Shared guest profile may apply, but its mapping/hooks differ | Separate milestone; not claimed supported |
| Ubuntu 22.04 / 24.04 x64 | Native synthetic process, headless UI and X11 startup CI matrix | Platform validation only; no game support claim |
| Debian/Fedora/Arch/SteamOS x64 | glibc desktop targets with matching dependencies | Intended targets; not individually verified |
| NixOS / Flatpak / Game Mode | Loader/library paths, namespaces and permissions vary | Packaging/access investigation pending |

## Build and test

Requires .NET 8 SDK for development:

```sh
dotnet run --project tests/BBTools.LinuxChecks -c Release
dotnet run --project tests/BBTools.LinuxDesktopChecks -c Release -- artifacts/ui
xvfb-run -a dotnet run --project src/BBTools.LinuxDesktop -c Release -- --smoke-test
bash scripts/package-linux.sh
```

CI gates the unified draft release on Windows and Ubuntu 22.04/24.04 and a Windows-host portable/UI job. Native checks create
an isolated synthetic game process with the real layout/fingerprints, exercise
process_vm_readv/writev, apply and restore data/flags, reject executable/cross-map
writes, verify the opt-in gate and reject competing editor ownership. They never
start Bloodborne or include private game data. UI checks verify initial read-only
state, localization/RTL and rendering; a separate Xvfb smoke test starts a real
X11 window, including from the packaged self-contained binary.

Validation on 2026-10-25: [Linux CI run 40225769893](https://github.com/JamesCicada/bb-tools/actions/runs/40225769893)
passes on Ubuntu 22.04 and 24.04, with 37 native/profile and five UI checks on each.
Both real X11 startup tests pass, and the Ubuntu 22.04 self-contained packages pass
their startup checks. The separate Windows regression run also passes. This is
platform/fixture evidence, not a tested-and-confirmed Linux game configuration.

Public information cannot validate the game's behavior end to end. Remaining work
is live game confirmation, additional runtime/distro profiles, native game actions
and the rest of the Windows feature set. Source research is recorded separately in
`docs/linux-compatibility-research.md` with primary sources.

## Beta downloads and reports

The desktop is available as `.tar.gz` and as a Nexus-friendly `.zip`; both contain
only `BBTools.LinuxDesktop` and `README.md`. The probe is a separate diagnostic
archive. **Beta — not tested in-game** applies to every Linux download, including
when native CI succeeds. Report distro, desktop/display server, runtime version,
connection result and relevant logs on GitHub. Native testers are needed.

Insight editing and free level-up/weapon-upgrade controls are Windows-only in this
release, alongside the other native-call/hook features listed as unavailable above.
