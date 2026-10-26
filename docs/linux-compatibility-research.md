# Linux compatibility research — 2026-10-25

## Result and limits

A standalone native Linux desktop path is feasible. We can share the validated
game-data reader and data-only edits, but not the Windows WPF shell, Win32 process
handles, allocation/protection APIs or thread suspension. Public code establishes
layout/architecture assumptions; it cannot establish a successful live attachment,
combat behavior, save persistence or compatibility with every runtime hook.

The implementation is an experimental desktop subset plus read-only probe. No
third-party runtime code is copied into BB Tools; the sources below are used for
facts and interface/packaging decisions. No game files are distributed.

## Primary sources inspected

| Source | Finding | Consequence for BB Tools |
| --- | --- | --- |
| [bbport upstream](https://github.com/deadinside28/bloodborne_pc/tree/8185b88d80573fa300595d5c54450183b4109935), `src/probe.c`, `src/runtime_memory.c`, `gpu/shim/bbport_guest_hooks.cpp` | Original x86-64 guest is separately allocated/mapped; source hooks refer to guest base `0x800000000`; mappings can be split, shared and protected | Locate the guest through signatures and validate pointers; do not treat the launcher ELF address as the game base or require private-only mappings |
| [bbport 0.6 release](https://github.com/deadinside28/bloodborne_pc/releases/tag/0.6) | Linux/AppImage/Steam Deck target, original 1.09 guest, changing renderer/memory model and runtime hooks | Native Linux is the first target; all seven core fingerprints remain mandatory regardless of release name |
| [ayozetr fork README](https://github.com/ayozetr/bloodborne_linux) | Native Linux bbport fork, 1.09, AppImage, bundled launcher/game options | Candidate runtime, not proof of compatibility; existing runtime cheats/hooks can overlap trainer changes |
| [bbhost](https://github.com/droogie/bbhost), local source snapshot `566184a` | Windows/Linux translation layer running original guest code, with its own hooks/plugins and content profiles | Separate runtime matrix entry; avoid promising shared native-call compatibility solely from the game version |
| [PINCE](https://github.com/korcankaraokcu/PINCE) | Native Linux memory/reverse-engineering tooling uses a Linux-specific debugger approach | Linux memory access is practical, but a Windows trainer binary is not a portable backend; no PINCE code is reused |
| [Linux process_vm_readv/writev documentation](https://man7.org/linux/man-pages/man2/process_vm_readv.2.html) | Cross-process transfers can be partial; access uses ptrace permission checks; read/write calls do not make transfers atomic | Check exact byte counts, reject partial reads, validate PID/start time, constrain edits to writable non-executable mappings and retain same-player guards |
| [Kernel Yama documentation](https://www.kernel.org/doc/html/latest/admin-guide/LSM/Yama.html) | Scope 1 normally permits descendants; scope 2 requires tracing capability; scope 3 forbids tracing | Provide launch-from-tool and accurate policy diagnostics, without changing sysctls, granting capabilities or auto-elevating |
| [Avalonia platform matrix](https://docs.avaloniaui.net/docs/supported-platforms) and [headless testing](https://docs.avaloniaui.net/docs/testing/setting-up-the-headless-platform) | Desktop Linux uses X11; native Wayland is a separate opt-in backend; glibc/native UI dependencies and headless rendering are documented | Pin Avalonia 12.1.4, target X11/XWayland and glibc x64, run headless plus real-X11 smoke tests; defer native Wayland/musl |

The bbport source revision was fetched on 2026-10-25. Searching the public hook
tables did not reveal explicit replacements of our seven core anchor sites, but
that is not a proof that no dynamic hook or third-party mod will touch them.
The runtime still verifies the bytes before enabling a connection/operation.

## Validation without a game tester

- Portable tests cover maps/identity parsing, process ranking, shell-free launcher
  arguments, bounded/cancellable image scans, mismatched and ambiguous images.
- Native Linux tests operate only on a child fixture: checked memory reads/writes,
  executable/cross-map rejection, original-byte restoration, shared health/Echoes/
  attributes, combat/resource flags, disabling edits and exclusive edit ownership.
- Desktop tests instantiate the real UI and catalogs, verify the initial write
  gate, Arabic direction/numeric fields, language switching and render at 860×680.
- Ubuntu 22.04/24.04 CI also starts a real X11 window with Xvfb; packaged app smoke
  testing checks that the archive includes a usable self-contained executable.
- Windows checks remain separate. Their passing results are not Linux syscall
  evidence; a successful native synthetic run is not a live Bloodborne result.

The existing local Debian and Docker WSL registrations refer to missing virtual
disks. They were inspected but not repaired or reinstalled. Linux-native validation
therefore uses isolated CI runners, not a claim that this Windows host ran the game.

## Next implementation priorities

1. Verify native bbport live when an actual Linux game environment becomes available;
   capture only runtime/profile outcomes and selected non-personal diagnostics.
2. Add a Linux game-thread execution/allocation service before porting item grants,
   menu actions, warp, boss revival and multiplier/native-hook features. Do not map
   Windows suspension/injection calls directly to ad hoc Linux signals.
3. Expand runtime profiles one at a time (ayozetr, bbhost, shadPS4 Linux), with
   per-feature fingerprints and hook-conflict checks.
4. Add Linux presets, save paths/backup, Unix-socket Discord IPC and desktop hotkeys;
   Wayland restrictions need explicit handling rather than X11 assumptions.
5. Add distro-native packaging and dependency verification for SteamOS, Fedora,
   Debian/Arch and NixOS before claiming those installations tested and confirmed.
