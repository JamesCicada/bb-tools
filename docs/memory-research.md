# Bloodborne PC 1.09 memory adapter

The adapter is independent C# code. It uses documented address/field facts as research leads, checked against the user's private prepared CUSA03173 01.09 ELF. Research checkouts, private analysis and previews live under ignored `.research/`; no game image or upstream implementation is distributed.

## Resolution

The native loader allocates the game image at runtime. Enumerate private committed allocation bases and require exactly one base matching seven independent instruction locations. This is an instruction-layout profile, not complete executable authentication. It intentionally rejects changed fingerprints, including incompatible patches at those locations. The process handle is held throughout the session, preventing PID reuse from redirecting writes. Selection also records process creation time. A named mutex prevents two BB Tools instances from owning the same process session.

All addresses below are relative to the relocated image or explicitly named objects, never the host EXE module.

| Field | Resolution |
| --- | --- |
| World manager | pointer at image + 0x553E878 |
| Local actor | world + 0x60 |
| Character modules | pointer at actor + 0x3B0 |
| Resource component | pointer at modules + 0x20; component + 8 must point back to actor |
| Health / max health | component + 0xF8 / 0xFC, signed int32 |
| Stamina / max stamina | component + 0x134 / 0x138, signed int32 |
| No stamina consumption | component + 0x200, bit 0x80 |
| Character data | pointer at actor + 0x3C0; must equal pointer at GameDataMan + 8 |
| GameDataMan | pointer at image + 0x553B130 |
| Blood Echoes | character data + 0x94, signed int32 |
| Blood Level | character data + 0x90, signed int32 |
| Vitality / Endurance | character data + 0x40 / 0x48 |
| Strength / Skill | character data + 0x58 / 0x60 |
| Bloodtinge / Arcane | character data + 0x68 / 0x70 |

The private ELF's local-player comparison at image + 0x18C2D00 resolves the world pointer through RIP-relative addressing. The resource synchronization routine at 0x18F7880 walks actor + 0x3B0 to modules + 0x20 and reads the health/stamina fields. Echo award code at 0x1900278 reads actor + 0x3C0, then accesses data + 0x94. The global echo setter at 0x1B63B80 resolves GameDataMan and its +8 player data. Attribute accessors starting at 0x203A4A6 read the six attribute fields. These disassembly findings are address/layout facts; private game bytes are not included as artifacts.

## Writes and lifecycle

- Resource/flag writes require committed, non-executable writable data pages. Native requests allocate their own code page as RW then seal it RX; no original game instructions or their protections are changed.
- Resolve and validate player identity immediately before writes. Pointers are never persisted across process sessions.
- Stop toggles when validation fails or the actor/component changes. Revalidate on each 200 ms poll; do not auto-resume toggles after loading.
- Health refill skips dead players. It can lose a race to a lethal hit; it is not an invulnerability hook.
- Stamina restores only the bit it owns and preserves other current bits. Restoration only targets the currently validated component. If it is temporarily unavailable, polling retries restoration once available. An unloaded or replaced component is not written through a stale pointer.
- Echo changes are single int32 writes with readback. Concurrent awards/spending can race a trainer write; apply while idle.
- Attribute editing checks that the draft still matches the active character and computes level from the attribute-point delta. One block write preserves the intervening data and excludes echoes. Readback verifies the result. It is not a game-thread transaction: apply while idle. The character must be reloaded to refresh derived values; actual gameplay/persistence still needs testing.
- On normal disconnect, attempt stamina restoration and release the process handle. Game writes already made are not rolled back.

## Verification and remaining work

36 checks use a synthetic process with an allocated relocated image, world/player/data pointers and resource fields. They exercise Windows OpenProcess, VirtualQueryEx, ReadProcessMemory and WriteProcessMemory, rather than only an in-memory mock. A separate read-only check confirms 18 signatures against the privately supplied ELF. The rendered WPF layout was visually inspected.

The user reports that the original health/stamina/echoes/attribute controls work on this installation. New flags, item grants and lamp warps still need live-game testing. Transitions across every area/character state and other game regions/builds remain unverified. shadPS4 has no live adapter yet.

## Research sources

- [Supermedo loader](https://github.com/Supermedo/bloodborne_pc/blob/windows/src/probe.c): image allocation and relocations.
- [TatsunaKyo runtime cheats](https://github.com/TatsunaKyo/bloodborne_pc/blob/windows/src/runtime_cheats.c): WorldChrMan local-player comparison, resource owner checks and no-consumption flag semantics.
- [Windows trainer](https://github.com/yaonikaixin999999/bloodborne_pc_windows/blob/codex/windows-port/trainer_windows.py): resource field/hook facts credited upstream to Shiningami.

No upstream trainer/trampoline implementation is incorporated; the repository's existing MIT license remains unchanged.

## Combat flags and native actions

Additional 1.09 layout facts checked in the private ELF:

| Feature | Location / function RVA |
| --- | --- |
| God mode | Resource component + 0x200, mask 0x03 (no dead / no damage) |
| Player Exterminate | image + 0x553E889, bit 0 |
| Player Hide / Silence | image + 0x553E88E / 0x553E88F, bit 0 |
| PlayerIns data getter | 0x18FD960; vtable slot +0x1C8 |
| Allowed getter return address | 0x18F789A, the PlayerIns data synchronization caller |
| MapItemMan | pointer at image + 0x553D6E0 |
| Award item batch | 0x17D89F0, (manager, batch) using SysV ABI |
| Set return-point entity | 0x156D110 |
| Enable return-point use | 0x156D0E0 |
| WarpNextStage_Bonfire | 0x132E050, (unused, return point entity) |

The debug menu's own Player Exterminate / Hide / Silence label bindings resolve to those global bytes. The damage path tests component +0x200 bit 2 before applying negative HP changes; the death guard tests bit 1. The game-owned values are captured per feature and only those bits are restored.

The native dispatcher copies this actor's vtable and RTTI prefix, after verifying the original getter and finding the end of the contiguous function-pointer table. The privately prepared relocation table has player-table candidates at 0x538F310 and 0x538FA80, each with 0x6D8 bytes of contiguous virtual methods; the runtime resolver derives its boundary instead of hard-coding these tables. Original game code pages stay untouched. The actor's table pointer is changed while the target process is suspended and revalidated.

The generated SysV dispatcher always calls the original getter and preserves its return value and ABI callee-saved registers. Only calls returning to the specific player-update site can claim a ready request using an atomic compare/exchange. It also verifies the actor, character data, and current WorldChrMan main player. Recursive getter calls and other call sites cannot consume the request. Up to three integer-argument calls run in order. Warp uses the same three return-point/stage functions as the normal lamp route. Item spawning uses the actual MapItemMan batch-award path, not the GetItem notification-only helper. One batch consists of count=1 followed by a tagged item ID, quantity, zero auxiliary field and -1 sentinel.

Publishing a request pauses the game briefly, fills descriptors/payload, then marks it ready last. No remote thread is created. Completion is acknowledged separately from mere submission; timeout does not automatically repeat a grant. If the request has not started it is cancelled on timeout. A running request cannot be safely cancelled; subsequent requests are blocked until it finishes.

Normal disconnect cancels pending requests and restores the actor's original vtable when identity still matches. Allocated code/data remain valid for the rest of the process lifetime: freeing them could invalidate a cached virtual call or a return address on a suspended game thread. An unloaded actor is not mutated through a stale address. Forced termination of BB Tools cannot restore its vtable; restart the engine before reconnecting if its getter is still intercepted.

The synthetic fixture now executes the real generated dispatcher, including an unauthorized getter call site, native SysV argument passing, item-batch payload forwarding, three-function ordering, recursion and detach. It is not an execution of the game's actual inventory/warp routines; those still need live testing.

Additional research: [bbhost native class layouts](https://github.com/droogie/bbhost/tree/main/include/bbhost/engine/sprj) and its [world/return-point implementation](https://github.com/droogie/bbhost/blob/main/src/engine/world_chr.cpp). Addresses and structure facts were checked independently against the local game image. No bbhost implementation code is included.

## Automatic connection

A separate read-only probe opens each name-matched native candidate, verifies the base image, and tries the current character structure. Loaded/validated characters rank first, then engine names, then most recent process creation time and PID. Unsupported candidates never enter the ranking. The process creation time and image are checked again when opening the writable session. Probes and connection attempts cannot overlap themselves. Automatic connections never enable any practice toggle. Manual disconnect pauses retries; engine exit permits reconnection. The preference persists separately from game/session state.
