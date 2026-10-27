# Linux beta — NOT TESTED IN-GAME

BB Tools now has a native Linux desktop beta and a separate read-only connection
probe. Both target **Linux x86-64 with glibc and Bloodborne 1.09**. Live attachment to
Bloodborne has not been tested on Linux. No runtime or distribution is marked
**tested and confirmed** for the game on the strength of source review or CI alone.
Version: **0.6.8-linux-beta.1**. This beta has its own GitHub prerelease and separate Nexus optional-file package; it does not replace the stable Windows download.

## What's implemented

| Feature | Desktop preview | Live Linux game confirmation |
| --- | --- | --- |
| All-process picker, filtering, likely game processes first | Implemented | Pending |
| Instruction-profile and player/character validation | Shared Windows core + Linux process reads | Pending |
| Health, Echoes and attribute editing | Experimental opt-in | Pending |
| Health refill, infinite stamina, God mode, Ignore hits, no fall damage, infinite consumables/bullets, no Echoes loss | Experimental opt-in; separate flag profile validation | Pending |
| Normal disconnect/close cleanup and per-process edit ownership | Implemented | Pending |
| Shared 28-language catalogs and RTL/numeric-input separation | Implemented; Linux-specific preview explanations remain English | Pending native-speaker review |
| Per-session logs, five retained | App folder, with XDG state-directory fallback when read-only | Pending real installations |
| Spawn, menus, warp, boss revival, multipliers, discovery, FOV patches | Unavailable | Linux allocation/thread coordination/dispatcher still needed |
| Hotkeys, Discord, presets, save restore | Unavailable | Platform work still needed |
| Photo mode/freecam/noclip | Hidden | Same policy as Windows |

The preview starts **read-only** every connection. Editing is never enabled by a
startup preset. Changes to Echoes/attributes can be saved by the game. A killed
trainer cannot restore its flags; normal cleanup verifies the same player before
writing. If restoration is pending during loading, the connection/window stays
available so cleanup can be retried after the character loads.

## Run the desktop

Extract `BBTools-v0.6.8-linux-beta.1-linux-x64.tar.gz` and run:

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
2. Refresh the process list and select its **bb-probe** engine, then Connect.
3. If access is denied under Yama scope 1, use the launch card to start your installed
   AppImage, launcher or `run.sh` from the tool, then refresh/connect. AppImages
   commonly use `["--play"]`; `run.sh` commonly uses `[]`. The launcher must remain
   in the tool's descendant process tree. Arguments are a JSON array, not shell code.
4. Only after the displayed values match your character, enable experimental data
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
| deadinside28 native bbport 0.6, revision `8185b88` | Public loader/memory/hook source inspected; guest layout compatible in principle | First target; live confirmation pending |
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

CI uses Ubuntu 22.04/24.04 and a Windows-host portable/UI job. Native checks create
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
