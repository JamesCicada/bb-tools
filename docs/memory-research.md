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
- Retain selected toggles when validation fails during loading. Revalidate on each 200 ms poll before resuming writes. When the component changes, discard old component ownership without writing to stale memory and capture the new component's original bits. Explicit stop or disconnect clears selections.
- Health refill skips dead players. It can lose a race to a lethal hit; it is not an invulnerability hook.
- Stamina restores only the bit it owns and preserves other current bits. Restoration only targets the currently validated component. If it is temporarily unavailable, polling retries restoration once available. An unloaded or replaced component is not written through a stale pointer.
- Echo changes are single int32 writes with readback. Concurrent awards/spending can race a trainer write; apply while idle.
- Attribute editing checks that the draft still matches the active character and computes level from the attribute-point delta. One block write preserves the intervening data and excludes echoes. Readback verifies the result. It is not a game-thread transaction: apply while idle. The character must be reloaded to refresh derived values; actual gameplay/persistence still needs testing.
- On normal disconnect, attempt stamina restoration and release the process handle. Game writes already made are not rolled back.

## Verification and remaining work

57 checks use a synthetic process with an allocated relocated image, world/player/data pointers and resource fields. They exercise Windows OpenProcess, VirtualQueryEx, ReadProcessMemory and WriteProcessMemory, rather than only an in-memory mock. A separate read-only check confirms 25 signatures against the privately supplied ELF. The rendered WPF layout was visually inspected.

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

The generated SysV dispatcher always calls the original getter and preserves its return value and ABI callee-saved registers. Only calls returning to the specific player-update site can claim a ready request using an atomic compare/exchange. It also verifies the actor, character data, and current WorldChrMan main player. Recursive getter calls and other call sites cannot consume the request. Up to three integer-argument calls run in order. Warp uses the same three return-point/stage functions as the normal lamp route. Item spawning uses the actual MapItemMan batch-award path, not the GetItem notification-only helper. One batch consists of count=1 followed by a 16-byte entry: owned inventory handle at +4 (constructed by the game before awarding), tagged catalog ID at +8, quantity at +12, and -1 sentinel at +16. The original implementation omitted the handle field, putting quantity in the catalog-ID field and zero in the quantity field. The private ELF's award routine at 0x17D8A70-0x17D8A8D and item-lot batch producer at 0x17DE1CF-0x17DE1E6 establish these distinct fields; the consumer field-copy instructions are now fingerprinted. A catalog ID must not be passed as an inventory handle. Leaving the handle zero also fails: it permits a pickup/storage message while the queued inventory operation has no valid item instance. Before awarding, call 0x1A86FF0(out handle, ItemMan, pointer to catalog ID), with ItemMan at image +0x553E990. This converts goods to their internal 0xB0000000-based handles and allocates equipment instances through the engine. After the award retains the handle for its queued work, call 0x1A8BC00 on the temporary handle to release its reference. Construction, award and release run in the same three-call dispatcher request.

Publishing a request pauses the game briefly, fills descriptors/payload, then marks it ready last. No remote thread is created. Completion is acknowledged separately from mere submission; timeout does not automatically repeat a grant. If the request has not started it is cancelled on timeout. A running request cannot be safely cancelled; subsequent requests are blocked until it finishes.

Normal disconnect cancels pending requests and restores the actor's original vtable when identity still matches. Allocated code/data remain valid for the rest of the process lifetime: freeing them could invalidate a cached virtual call or a return address on a suspended game thread. An unloaded actor is not mutated through a stale address. Forced termination of BB Tools cannot restore its vtable; restart the engine before reconnecting if its getter is still intercepted.

The synthetic fixture now executes the real generated dispatcher, including an unauthorized getter call site, native SysV argument passing, item-batch payload forwarding, three-function ordering, recursion and detach. It is not an execution of the game's actual inventory/warp routines; see TODO.md for separate tested-and-confirmed results.

Additional research: [bbhost native class layouts](https://github.com/droogie/bbhost/tree/main/include/bbhost/engine/sprj) and its [world/return-point implementation](https://github.com/droogie/bbhost/blob/main/src/engine/world_chr.cpp). Addresses and structure facts were checked independently against the local game image. No bbhost implementation code is included.

## Automatic connection

A separate read-only probe opens each name-matched native candidate, verifies the base image, and tries the current character structure. Loaded/validated characters rank first, then engine names, then most recent process creation time and PID. Unsupported candidates never enter the ranking. The process creation time and image are checked again when opening the writable session. Probes and connection attempts cannot overlap themselves. Automatic connections never enable any practice toggle. Manual disconnect pauses retries; engine exit permits reconnection. The preference persists separately from game/session state.

## Expanded travel and practice options

The named lamp list is now derived from ReturnPointParam, the map events' lamp
registrations and the matching English lamp-name IDs (`70000000 + area*100 +
block*10 + index`). There are 45 named non-chalice destinations including the Dream
and ten Old Hunters lamps. This corrects the old Moonside Lake row: `3202952`, not
`3202951` (Lecture Building). Extra unnamed/debug return-point rows are excluded.
Chalice destinations require the character's generated-dungeon context; v0.2.0
uses native altar selection rather than extending this fixed return-point list.

Position reads follow actor +0x3B0 -> modules +0x68 -> transform; yaw is +0x1D4,
XYZ is +0x1E0, and the current packed map/block is actor +0x3F8. Relocation calls
`0x154B110(WorldRes, block*, positionVec4*, rotationVec4*)` through the same
validated game-thread queue. WorldRes is reached through image +0x553B148.
Payload vectors are 16-byte aligned; yaw occupies rotation lane 1. Saved locations
are validated for finite coordinates and bounded values. Restores reject a map
mismatch before installing a dispatcher; cross-map travel uses a lamp first.

Nineteen boss standpoints use the actual nearest fog/entrance object placements in
the private MSBs, checked against the research location leads in
[bbhost boss-rush data](https://github.com/droogie/bbhost/blob/main/plugins/boss_rush/boss_rush.cpp).
The standpoints are independently computed one metre behind each object's heading
and 0.15 metres above its floor position. No boss/door/quest flag manipulation is
included. Tested-and-confirmed results are recorded separately in TODO.md; automated checks do not establish every waypoint's behavior on every runtime fork.

The debug binding at `0x1921374` identifies All No Update AI at image +0x553E895,
bit 0. The fall path at `0x1A35B75` tests resource component +0x200 mask 0x08.
Both sites are fingerprinted and original bits are restored using the same owned
flag lifecycle as existing practice controls. This is a character AI update switch;
physics and scripted map behavior continue.

Item IDs accept either decimal base ID + category, or the wiki's complete
hexadecimal Item ID. An explicit Wiki hexadecimal mode handles digits-only hex
without ambiguity. The reference remains a link to the
[wiki's item table](https://www.bloodborne-wiki.com/2018/01/datamined-item-ids.html),
not a copied catalog. IDs must exist in the installed game's parameter tables.

Implementation and user validation are tracked separately in [TODO.md](../TODO.md).

## v0.2.0 native effects, menus and chalices

Consumable use at RVA `0x18F9814` tests resource word +0x200 bit 9; the normal
bullet path at `0x18F9F31` tests byte +0x201 mask 2. They share a player-owned
toggle. Restoration preserves unrelated bits and the selection survives loads.

Multipliers clone neutral SpEffectParam row 9999 into a private data allocation.
The original parameter table is not edited. Its full neutral-row SHA-256 is
validated before activation, rejecting altered rows from other data mods. Damage fields +0x38..+0x44 and enemy
reward soulRate +0xE0 change; duration becomes -1 and saveCategory stays -1.
Native row lookup `0x1F28AA0`, insertion `0x17E3130` and node update `0x17EA980`
run through the player update dispatcher. The container is actor +0x1C8,
its owner is +0x18, head +8; nodes have ID +0x40, row +0x48 and next +0x58.
Only native insertion allocates the node. An existing ID 9999 is rejected.
Disconnect restores the original row and expires the owned node through its
normal engine update; no VirtualAlloc page is passed to the game's heap free.

Remote menu constructors are LevelUp `0x1BB7F60`, Item/Reinforce `0x1BB9D00`,
Item/Repair `0x1BB9780`, Depository `0x1BBA4F0`, GemEquip/Weapon `0x1BBAA90`,
and GemEquip/Body `0x1BBB050`. Their names, factory dispatch call sites and ABIs
were checked in the private image. The menu root is image +0x5562870; constructor
context is root +0x48. Native attachment `0x1BB27A0` takes a step reference and an
empty completion functor. Temporary references are released on the game thread;
MenuMan owns the active wrapper (+0xA88) and the normal close lifecycle. Requests
reject an existing menu and a replaced manager. The old MenuMan at +0x5562878
and Util_RequestLevelUp are not used as workshop menu openers.

Chalice owner image +0x553E9E8 has seven native gates, array +8, each 0x6A8 bytes
with bank +0x100 and helper +0x108. Ordinary banks are 1..6. Helper +0xE0 holds
the generated map UID, +0x25C status, +0x260/+0x264 pending/current commands.
Native SelectDungeonAltar `0x13B89B0(EventFlagMan, bank)` selects/remaps the bank;
RequestResume `0x1AA40E0(helper)` resumes a suspended altar. Completed statuses
4/5 permit travel. The travel UI has nine named destinations from ReturnPointParam
2902950..2902958 and English messages 70002900..70002908. Unlock flags match
TalkESD t210060 (12907830 then increments of 20; the Seal has no unlock condition).
Native GetEventFlagValue `0x13CFD80` verifies the selected bank's map UID at
`94000000 + bank*100000`, its seven-bit selector, and the requested lamp's unlock.
A final game-thread helper rechecks bank/UID, sets the return point/use-return
latch, requests the ordinary respawn-point warp and sets destination bit
`9001 + lampSlot`, matching m21 event 12107200. This is available only from
Hunter's Dream so remapping a different dungeon's progress cannot affect a
currently loaded dungeon. Native Channel/Top alone is not used for travel:
its normal flow depends on the altar's local TalkESD script. No ritual is
created and no lamp progress is unlocked.
Generated-map coordinate bookmarks are rejected without instance identity.

Additional standpoints use m26 o260900 (Micolash) and m21 o000300_0000
(Gehrman/Moon Presence shared gate), using the same one-metre/0.15-metre offsets
as the existing catalog. Story prerequisites remain native.

These additions have synthetic execution and private-image checks. The live
gameplay checklist remains separate and must be completed before publication.

## Camera FOV override (unreleased)

The camera implementation in the inspected bbhost source documents the game's
`ChrExFollowCam::Update`; its preferred guest addresses include a 0x400000 slide.
The installed 1.09 image independently matches FOV reads at RVAs 0x143AF1E and
0x143AF5E. The clamp at RVA 0x143AF4E is `48 0F 46 C6`; the recognized external
uncap variant is `48 89 F0 90`. The slider now supports 38–120 degrees of vertical
FOV. Above 48 degrees, BB Tools changes only this four-byte instruction to the
known uncap variant while target threads are suspended. It validates surrounding
instructions and the expected old bytes, temporarily changes page protection,
flushes the instruction cache and restores protection. Generic memory writes still
reject executable pages. No game file is edited. This is not a horizontal-FOV setting.

The override tracks whether it installed the uncap. Disable/disconnect restores
an owned change, including when parameter restoration is waiting for a loaded
player. An existing uncap is left intact; unknown code is never overwritten.
Synthetic-process checks exercise real read-execute page protection transitions,
100/120-degree parameter edits, reset, ownership and preexisting-patch preservation.

SoloParamRepository is reached at RVA 0x5540340, with LockCamParam at slot 0x21
(`+0x70 + slot*0x48`). A unique resource cap must have the UTF-16 name LockCamParam.
The cap's `*(*(cap+0x70)+0x70)` is validated as LOCK_CAM_PARAM_ST, including row
records, unique IDs, finite original angles and 0x20-byte row strides. Only each
row's camFovY at +0x14 is changed. Multirow changes run while game threads are
paused. Restoration is limited to the current table and values still owned by
this override; loading invalidates captured addresses. Synthetic-process tests
cover limits, individual defaults, foreign changes and rejected layouts. Live
camera behavior remains pending confirmation.

## Development batch: revival, durability, gauges and camera tuning

Boss revival uses a fixed allowlist of 19 base-game/DLC profiles from the inspected
boss-rush reference and independently checked defeat/met flag IDs in the local 1.09
EMEVD files. It requires a loaded living character, journals the original
ten flags before mutation, then revalidates the character, flag manager and original
values while threads are suspended. Only that encounter family is reset; the
repeat-entrance met bit is enabled. The bounded event-flag tree walk supports the
native pointer/pool storage kinds and preserves adjacent bits. This does not roll
back all quest/world state: revival is a persistent practice-save operation, not a
full save-state restore. Micolash and ending bosses remain excluded.

Repair follows the native inventory path, not cached/display references at +0x94.
EquipGameData is character +0x1D0. Its eight equipment indices at +0x24 select
16-byte entries in EquipInventoryData (+0x158): first-bank count +0x24, inclusive
last index +0x88, first bank pointer +0x58 and second bank pointer +0x48. The
entry's +0 handle resolves the actual owned durability instance. Instruction
sites 0x14D270D, 0x14D2717 and 0x14D2724 verify this selection. Cached equipment
references can resolve a different instance and must never be used for repair.
The instance manager at RVA 0x553E990 resolves allocated handles through the low
16-bit index and checks the full handle at instance +8. Weapon-only vtable RVA
0x53362D0, getter/setter RVAs 0x1A93B70/0x1A93B80, and current durability +0x18
are verified against the private image. EquipParamWeapon is repository slot zero;
the native lookup normalizes catalog IDs to multiples of 100. Its +0xBE u16 maximum
bounds repairs. Identity/type/range checks and thread suspension precede writes.
Full-durability mode polls repairs; it is not a wear-instruction patch.

The status module at module-container +0x28 must have owner +8 and vtable RVA
0x5335900. Its five signed buildup gauges at +0x10 are bounded by five limits at
+0x24; load/size function fingerprints are checked. Only current gauges are cleared.
Active effects are neither removed nor reclassified; polling is not single-hit immunity.

Camera tuning scales LockCamParam distance +0 and look-at height +0xC, with reader
fingerprints at RVAs 0x143AE68 and 0x143AEEF. Individual originals are retained,
multiplier changes do not compound, and ownership-aware restoration preserves
foreign edits. FOV remains a separate override and retains its clamp ownership.

All new native behavior has synthetic-process coverage and private-image checks;
live confirmation is still separate in TODO.md.


## Unreleased native exploration and repair research

All RVAs below target the locally verified CUSA03173 1.09 prepared image. They are
fingerprinted before use; fixture checks are not live runtime confirmation.

- Repair: native EquipGameData repair `0x14D24E0`, read durability `0x14D26D0`, inventory resolver `0x1A89070`, weapon parameter lookup `0x1F29AA0`. Enumerate eight equipment slots and verify native post-repair values. Reinforcement IDs increment by 100, not one.
- Movement: transient player-only result hook at `0x1A196BB`; preserve original instructions and foreign actors. This is not independently verified action-animation speed.
- Gravity: world root `0x553D700`, wrapper +0x28 then +8, Havok gravity vector +0x5B0. This affects the whole world.
- Flight: module +0x20, component +0x200 bit 0x40; collision controller actor +0x58, +0x114 bit 2. Restore only owned actor/component state.
- HUD: GameDataMan `0x553B130`, options +0x48, visible byte +9. Hides frontend as well as HUD.
- Free camera: root `0x553E860`, owner +0x2830, camera +0x60; freeze verified follow update `0x143AC60`, restore code before attempting old-camera cleanup.
- Poison: native removal `0x17E1D90`; only slow/rapid poison parameter states 2/5 are selected.
- New revival profiles were checked against local event scripts. Moon Presence enables route flag 9900; ending-boss actions can alter story progression.
- Chalice coordinate bookmarks capture character, connection token, altar UID and a bounded progress-flag snapshot. They intentionally expire across reconnect/progress changes.
- shadPS4 discovery scans committed private/mapped allocation candidates and requires the same independent game-image fingerprints. Experimental opt-in does not establish compatibility with emulator calling or memory behavior.

Independent action speed, global time scaling, direct weapon/gem mutation, jump height,
and persistent cross-session dungeon identity remain unverified and unimplemented.
