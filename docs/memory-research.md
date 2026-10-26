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
reward soulRate +0xE0 change; the original implementation used duration -1 and
saveCategory -1. The 2026-10-22 follow-up below replaces the infinite duration.
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
- Poison: native removal `0x17E1D90`; the earlier 2/5 selector is superseded by verified slow/rapid states 2/6 below.
- New revival profiles were checked against local event scripts. Moon Presence enables route flag 9900; ending-boss actions can alter story progression.
- Chalice coordinate bookmarks capture character, connection token, altar UID and a bounded progress-flag snapshot. They intentionally expire across reconnect/progress changes.
- shadPS4 discovery scans committed private/mapped allocation candidates and requires the same independent game-image fingerprints. Experimental opt-in does not establish compatibility with emulator calling or memory behavior.

Independent action speed, global time scaling, direct weapon/gem mutation, jump height,
and persistent cross-session dungeon identity remain unverified and unimplemented.


## 0.6.0 standalone photo-mode path

The earlier follow-camera freeze prototype is superseded. No camera code patch is
applied by the new implementation. Independently verified against the private
CUSA03173 1.09 ELF:

- Correction: global 0x553E860 is the lock-on manager, not the map-step owner.
  The +0x13AC8 store at 0x19295D9 belongs to a different native task. The active
  0x5330480-vtable task stores its debug controller at +0x110 (0x193764B) and
  camera output at +0x128 (0x19379F0), mirrored into lock-on-manager +0x2830.
  Constructor 0x156FEC0 installs controller vtable 0x53267D0.
- Controller +0xC4 selects debug output; +0xC8 holds native debug input updates;
  +0xD0 is the camera frame. Native update 0x15729B6 skips its input-driven camera
  step when held, while renderer selection 0x193BEC5 still copies its frame.
- The ordinary camera owner's +0x10..+0x5F frame is copied by 0x193BE5A. Seed a
  private 0x60-byte frame from that owner (root +0x2830), then install it in the
  held debug output. Native output copy at 0x193BEE1 consumes projection and basis.
- No need for follow mode 3, no displaced game instructions and no overwritten
  debug-only constructors. Frame allocations are retained to avoid stale native readers.
- Native SetCompletelyNoMove at 0x1337878 reads the packed field starting actor
  +0x1E1 and sets bit 23: byte +0x1E3, mask 0x80. Photo mode optionally owns only
  this bit; independently restores it even if camera restoration is blocked.

Controller vtable, native instruction fingerprints, actor handle, map and camera
owner must validate. External debug camera ownership is rejected/preserved. Fixture
and static image checks cannot confirm renderer behavior on a running native port
or emulator; live validation remains pending.


## 0.6.0 read-only lock-on target inspection

Independently checked against the private CUSA03173 1.09 ELF; no game assets
are distributed. The target reader never writes process memory.

- Game root global 0x553E860 +0x2820 is the current lock flag (0x188EC30),
  +0x2830 owns the camera; camera +0x90 is only a mirror (0x1890F61) and was null on a live native-port
  lock-on. The authoritative active point list starts at manager +8 (0x188F583),
  links through point +0x80 (0x188F706) and marks selection at +0x90 bit 0x20
  (0x1890ACD). Require one selected entry; reject cycles/ambiguity.
- Point +0x90 bit 0x20 marks selection; point +0x88 points to a target record
  with tagged handle +0x64 (0x1890E48). Only character tag 0x10000000 is accepted.
- WorldChrMan 0x553E878 +0x850 + selector*8 resolves the set. Handle bits
  14..19 select the set, low 14 index entries of stride 0x38; actor +0x18 must
  refer back to the canonical entry (0x18908A9). Recheck context and identity.
- Resistance module +0x28, vtable 0x5335900: current integers +0x10, limits +0x24,
  five entries. Slow poison, legacy disease, rapid poison, frenzy, beasthood.
  Private armor parameters independently confirm the native blood slot means
  rapid poison, while disease is a legacy slot. Cure uses states 2 and 6.
- Super-armor module +0x48, vtable 0x5335990: +0x10 current, +0x14 previous
  limit, +0x18 reserve, +0x1C recovery, +0x20 broken (0x1A5D9D8/0x1A5DA77).
  This is not a verified boss limb-health reader; sampled breaks can be missed.
- Native effect entries: remaining time +0 (0x17EAA0D), flags +0x1C,
  ID +0x40, parameter row +0x48, next +0x58; active exclusion mask 0x800C0003.
  Parameter state is ushort +0x156. Negative time is labeled untimed, zero
  trigger/expiring; unknown states retain native IDs. Lists are bounded/cycle checked.
- HP +0xF8/+0xFC, stamina +0x134/+0x138 in the stats module. Internal model
  name +0x158 is bounded UTF-16, not a localized enemy name. Optional distance
  uses physics module +0x68, position +0x1E0.

The UI samples on a serialized worker every 200 ms. Unlock keeps updating the
validated retained actor; context loss freezes the last-known snapshot until a
new explicit lock-on selection. Synthetic read-only fixtures verify identity,
retention and corrupt-data rejection; live testing remains pending.


## 0.6.0 parry-practice target protection and research limits

- Independent private-ELF inspection: 0x1A2FDCF calls 0x1A577D0 before a lethal
  HP result. The true path clamps HP to 1. The latter checks stats +0x200 mask
  0x04 at 0x1A577E0, for both NPC and player actors. This differs from mask
  0x02 at 0x1A2FD81, which skips incoming negative HP deltas altogether.
- Target practice owns only mask 0x04, preserving other bits and the entry value.
  Full identity, world/player/map, module ownership and living-HP checks precede
  changes. Caller pauses game threads only for actual mutations; stable protected
  targets do not require repeated suspension. Unlock keeps the same actor; a new
  explicit lock-on restores the old actor where valid and protects the new one.
- Manual HP restoration accepts only the current locked living target, writing
  its validated max HP. It does not revive, reset boss flags or prevent scripts
  from killing/despawning the target. Context loss drops stale ownership safely.
- The private image contains native Parry AI action metadata and debug text for
  target parry timing regions. These are not verified gun-parry success events.
  Super-armor breaks, ordinary damage and paired throws are insufficient evidence
  to count a firearm parry: other mechanics can produce those states.
- Weapon parameter +0xC2 (parryDamageLife) and NPC +0x13E/+0x13F attack/defence
  fields are research leads. Their generic parameter consumers do not establish
  an adjustable firearm-parry window. No such parameter is changed by the tool.

Auto-parry, window adjustment, success indicators and automatic statistics remain
unimplemented pending reliable native event/timing attribution. No guessed
stagger-based parry counter, timing slider or simulated gun input is shipped.


## Live failure investigation — 2026-10-18

User-provided session log showed successful existing player actions and practice
setup, but FreeCamera.Resolve and ReadLockedEnemy failed through a generic pointer
message. Read-only examination of the loaded native port confirmed:

- Lock-on manager and player were valid; manager +0x2820 was 1. Camera output
  +0x90 was null. The selected active-list entry yielded canonical character
  c1170_0002, 123/123 HP, valid status/super-armor modules and native effect rows.
- Manager +0x13AC8 was zero. The actual active task linked both the current camera
  output and the 0x53267D0 controller. Its source frame had a finite orthonormal
  basis, projection and the current 70-degree FOV. The corrected C# readers passed
  with a read-only process handle; no feature was enabled or game data changed.
- Camera discovery searches only writable committed data in the output owner's
  allocation, bounded to 15 seconds with cancellation. Candidates require the
  native task vtable, exact active output pointer and controller vtable. The result
  is cached/revalidated; the lookup runs off the UI thread before enabling photo mode.

The synthetic fixtures now leave the incorrect root field and camera target mirror
null, covering the real failure rather than constructing the earlier assumed chain.
Full photo movement/rendering/capture and target-write validation remains pending.


## Photo studio movement gate correction

The initial event-only hunter lock did not stop movement in the reported live
build. It also returned early after claiming ownership, so game-cleared bits
were not reapplied. The native DisableMove callback at 0x132F786 loads actor
+0x58 and writes its +0x111 byte; this gate is now used alongside the packed
actor +0x1E3 mask 0x80. Both values are captured before changes and checked each
tick, with mutations only when needed. Controller replacement stops reapplication;
cleanup restores the event flag only on the same actor/handle and restores the
controller byte only when that exact controller is still bound to the actor.
Death/loading retain pending restoration where needed. Synthetic tests cover
reapplication, toggling off, existing locks and replacement-controller avoidance.
Full keyboard/controller movement behavior still requires live confirmation.

## Photo/HUD regression follow-up — 2026-10-19

The controller/event flags above still allowed movement in the reported live
test. Hunter locking now also shares MovementSpeed's verified 14-byte result
hook at 0x1A196BB. Its zero factor is applied per native execution rather than
depending on UI polling. A captured-actor restriction prevents this factor from
following a replacement player. Ordinary speed changes keep the photo guard;
photo exit restores the selected speed, or the original code at normal speed.
The debug-camera output itself still uses data only. No external addon is needed.
Native execution fixtures check zero results, foreign/replacement actors and
shared ownership. Actual keyboard/controller behavior remains pending a retest.

HUD cleanup now runs in a separate finally path and retries when options are
temporarily unavailable. The original visibility remains captured across options
replacement instead of treating a cloned hidden value as the new baseline.
Photo exit explicitly shows HUD unless the separate gameplay hide override is
selected. Restore HUD clears both hide selections and repairs older hidden state.

## Persistent native hit invincibility — 2026-10-19

The EnableInvincible binding resolves to 0x1332EF0. Its setter at 0x1332F13
sets bit 14 of the packed actor flags starting +0x1E1: byte +0x1E2, mask 0x40.
The native hit/invincibility getter checks mask 0x04004000 at 0x18CBFC7 before
animation invincibility. This persistent event bit avoids racing per-frame TAE
flags. Ignore hits owns only that bit, validates the full actor handle, restores
its captured value and participates in the existing preset/flag lifecycle.
An isolated fixture executes the native getter with and without the event bit,
independently of animation flags. Live hit reactions, grabs and scripts remain
unconfirmed. This does not disable world collision or change ordinary God mode.

## Parry investigation — 2026-10-19

Read-only sampling captured c2700 attack/animation/effect transitions. The TAE
module (+0x18, vtable 0x5335190) records recent animation IDs/times; these entries
must not be treated as ten simultaneously active animations. The action-flag
module is +0x08, vtable 0x53342C0. The native event dispatcher at 0x1A216B0 maps
flag 5 to +0x1BD mask 4, flag 63 to controller state +0x40 mask 4, and flag 73 to
state +0x41 mask 4. The flag-5 damage consumer at 0x1A2F2B0 explicitly checks
attack type 64, so it is not sufficient evidence for automatic firearm shots.

Primary format references: [DSAnimStudio Bloodborne TAE definitions](https://github.com/Meowmaritus/DSAnimStudio/blob/master/DSAnimStudioNETCore/Res/TAE.Template.BB.xml)
and [SoulsFormats TAE reader](https://github.com/Meowmaritus/SoulsFormats/blob/master/SoulsFormats/Formats/TAE/TAE.cs).
Private parameter inspection identifies effect 31 as thrust-counter damage,
separate from firearm-related effects 50–53. Its observed attack transitions do
not prove a firearm-parry timing gate. No public game strings/assets are bundled.
Auto-parry, firearm-window adjustment and success attribution remain unimplemented;
no generic parry flag, counter-damage effect or stagger is presented as completion.


## 0.6.0 first-encounter revival and native death-penalty skip

Current availability: photo/free camera and flight/noclip are disabled and hidden.
Their implementation notes above remain research history, not current availability.

First-encounter revival clears the ten-flag encounter family including the met
flag; repeat mode retains the existing met flag (+2 for base-game profiles, +1
for DLC profiles). Explicit door/access flags remain profile-specific. The local
boss-rush source confirms the existing met/skip-intro policy. Journals now record
FirstEncounter and requested Changes as well as original Flags. Fog and aggression
still depend on original encounter scripts and user AI/visibility overrides.
No new in-game revival confirmation is implied.

The private prepared 1.09 image exposes the script binding IsDeathPenaltySkip
(string at 0x492DF8A, registration at 0x134427E, getter at 0x13389B0).
The getter loads GameDataMan through image+0x553B130 and returns whether byte
manager+0xA8 is nonzero. A fingerprint checks the native getter instructions.
NoEchoesLoss owns bit 0 of this bool field, rejects values above 1 before enabling,
captures the original value, follows a validated replacement manager and never
restores through a retired manager. It performs no balance snapshot/refund writes.
Synthetic execution verifies the real getter false/true/restored behavior and
preservation of an existing flag. Death, bloodstain recovery and any additional
native penalty effects remain pending live tests on both runtimes.

Discord uses local desktop IPC and SET_ACTIVITY with the user-supplied public
application ID 1189594649954877620. Protocol reference:
[Discord RPC documentation](https://docs.discord.com/developers/topics/rpc).
The mock pipe checks handshake, nonce acknowledgement, ping/pong and activity clear;
real desktop publication/restart still needs user testing. Only game/runtime and
elapsed time are published, with no tokens or character/location payload.


## 2026-10-21 combat polling stability follow-up

Lag/intermittent crashes were reported with God mode and Ignore hits together.
The available C:/Games/bb-testing/last_run.log contains renderer flush stalls and
texture-cache pressure, and ends with Quit to Desktop. It does not establish a
crash cause or implicate the invincibility flags. No game settings were changed.
Combat and stamina polling now skips writes if the desired bits are already set,
while retaining identity/instruction validation, original-state ownership and
reapplication after cleared flags/loads. Counted native-memory fixtures verify
both protection modes plus stamina together, independent disable, preservation
of unrelated engine edits and preexisting invincibility. Live stability remains
unconfirmed; this optimization is not a demonstrated fix for the reported crash.


## Optional Discord detail reader — 2026-10-21

Extra sharing is off by default. Per-field choices read validated active character
identity and GameDataMan ownership without game writes. Area uses actor+0x3F8
and labels the loaded map; it does not claim the exact lamp/subarea. Level comes
from the existing active-character reader. Total deaths is unsigned GameDataMan
+0x88, confirmed by the saturating increment routine at image+0x14EB660 and the
primary bbhost engine/player_data.cpp source; NG cycle uses manager+0x68.
The character-name setter at image+0x14F31D0 bounds the input to 16 UTF-16 code
units, copies into record+0xA8 and writes its terminator. The reader checks that
instruction fingerprint and reads a bounded 34-byte UTF-16 buffer, rejecting
invalid encoding/controls and missing termination. New counters/name have extra
fingerprints, independent of gameplay write validation. Live display/data tests
remain pending.

Presence assets use the existing public PNG at a pinned repository commit;
no upload, token or account change is needed. A BBTools button links to the repo.
status_display_type=2 requests Details in the member-list status; this does not
rename the application on the profile card. Official protocol references:
[Activity fields](https://docs.discord.com/developers/events/gateway-events#activity-object),
[application-name restriction](https://docs.discord.com/developers/rich-presence/using-with-the-embedded-app-sdk).


Read-only live verification: bb-probe.exe on 2026-10-21 returned the loaded
character in Central Yharnam, with valid level/deaths/NG fields. The name matched
the supplied in-game name; personal field values are not recorded here. The handle was opened read-only; no game writes or
Discord publication occurred. This validates the reader, not the final Discord UI.

Discord application selection updated to 1196921008809066616 at the user’s request. The external PNG override was removed: the activity omits assets so Discord can use that application’s configured Bloodborne icon. The BBTools repository button remains. Earlier pinned-emblem notes describe the superseded payload.

## Multiplier cleanup and Ludwig revival — 2026-10-22

A third-party report describes compounding Echoes gain, including persistence after
restarting the game. This has not been reproduced live. The trainer's parameter
clone is process memory only, with saveCategory -1; it does not edit game files or
the save's base reward scaling. A full process restart should discard that private
allocation, so the cross-restart part requires the reporter's runtime, mods and
controlled baseline before a save-persistence diagnosis can be made.

The previous infinite node could outlive an interrupted trainer session. Ownership
was local to one GameActions instance and resetting 1× returned without checking
unowned leftovers. The development implementation now recognizes exact private
9999 clones (all bytes match the validated neutral row except the four outgoing
rates, soulRate and duration), restores their original table pointer and schedules
normal native expiration before attaching a replacement. Foreign or vanilla 9999
collisions reject the entire recovery before writes. No native node is freed by
the trainer. Both 1× values explicitly recover old infinite clones too.

New clones use a five-second game-time lease, renewed once per second while the
owned effect remains attached. The prepared 1.09 native node update at 0x17EA980
subtracts delta time from a positive node +0 remaining duration and marks +0x2D
for expiration on a subsequent update at zero. The helper initializes node +0/+4
to five seconds, keeps saveCategory -1 and ignores nodes already marked expired.
A paused/unloaded game expires the effect when its normal updates resume.

The local m34_00_00_00 event script independently uses defeat flag 9471 in event
13401800 to disable/kill both Ludwig forms (3400800/3400801), remove fog object
3401800 and its SFX, and end fog/intro/AI events 13404800/13401801/13404802.
Clearing only 13401800..9 therefore leaves the fight disabled. Ludwig's profile
now clears 9471 and its 13404800..49 entry/phase family (including phase switch
13404825). Repeat mode retains 13401801; first-encounter mode clears it. Reward
flag 6674, quest flags 3400..3403 and Laurence's family remain untouched.
Original values, including 9471, are included in the existing pre-write journal.

Regression fixtures cover multiple old/new clones, repeat resets, foreign-effect
refusal without partial writes, lease ownership and both Ludwig encounter modes.
Live retests remain pending; these checks do not establish that the reported full
restart persistence is resolved.

## Independent item discovery — feasibility, 2026-10-23

Read-only inspection of the private 1.09 image confirms the native discovery
calculation at RVA 0x1581830. It checks the player's effect-container category
bits (+0x21 mask 0x70), finds an active node whose row stateInfo (+0x156) is 0x42,
and sums active effects' itemDropRate floats (+0x1FC). It then adds that bonus to
the Arcane-derived discovery curve. The independent decompilation reference is
bbhost/src/decomp/player/player_data.cpp (preferred VA 0x1981830).

The local parameter data contains discovery-state rows 102000/102001/102002 with
bonuses 0.5/0.7/1.0, and 180060 with 0.1; rows 1928/6960 have zero. These are layout
leads, not IDs to blindly attach or overwrite. A temporary player-only discovery
bonus can therefore avoid all writes to Arcane, Blood Level or other base stats.

Implementation is pending. The existing multiplier helper inserts neutral row
9999 before redirecting its node to a clone; changing the clone's stateInfo alone
is insufficient proof that the container's native category bits will be updated.
Verify activation/bookkeeping and native cleanup first, preserve equipped effects
and use a finite owned lifetime with restoration/recovery checks. No actual game
writes or new discovery controls were added during this investigation.

## Discovery and eligible enemy rolls — development follow-up

The discovery implementation hooks the verified return at RVA 0x158191B (12 bytes
of native arithmetic, RET and complete eight-byte padding; 21-byte patch span).
It retains the native Arcane-plus-equipped-effect result and adds a private float
only when the incoming effect container matches GameDataMan +8 character record
+0x670. The kill reward caller at 0x18C54A0..0x18C5503 independently supplies that
record's container and Arcane. The bonus is stored as points/100, limited to +1000
points, with no attribute or shared parameter edits.

A separate hook at RVA 0x17CF52C spans complete instructions (17 bytes). RAX points
to the resolved temporary slot's weight; item ID/category are -8/-4 and quantity
is +0xD relative to it. Only the enemy-kill caller return 0x18C5503 is accepted.
The hook zeroes the weight when the native final-item validity test would reject
the slot (negative ID/category or zero quantity), before the original total-weight
addition. It preserves registers/flags, then replays the original instructions.
The normal RNG draw, valid relative weights, quantities and native flags remain
in the original engine. If no eligible positive weights remain, the native total
<=0 branch at 0x17CF567 skips the row. It is not an all-items or gem-quality patch.

The common enemy path has no map filter, so chalice dungeons are an explicit
intended target. Level-dependent category-15 slots resolve before the hook; the
fixture includes resolved category-8 gem/rune entries. This does not establish
live chalice compatibility or force gem quality/effect rolls. The independent
layout reference is bbhost/src/decomp/items/item_lots.cpp (preferred VA 0x1BCEAF0),
checked against the local 1.09 image; no decompiled implementation was copied.

Both hooks use a narrow process-memory write allowlist, reject foreign edits,
neutralize private controls on loading, and restore only owned instructions on
normal disable/disconnect. Private pages remain allocated until process exit to
avoid freeing code cached by native threads. Abnormal trainer termination is not
claimed to restore these hooks; full game process exit discards them.

## v0.6.5 multiplier/player diagnosis — 2026-10-25

- Reporter log: repeated multiplier failures with an unchanged actor and successful
  neutral-row lookup/recovery; this error alone did not establish detachment.
- The private neutral SpEffect 9999 row matches the existing full SHA-256 and has
  duration 0.0. Native initialization at RVA 0x17EA810 marks node +0x2D expired
  when that initial duration is zero. Insertion at 0x17E3130 returns a signed list
  index, computed at 0x17E3642..0x17E367E. The old helper skipped expired nodes and
  consequently rejected the very node it had just created.
- The replacement checks that index (0..2047), traverses to that exact node on the
  game thread, verifies ID 9999 and the original row pointer, then attaches the
  five-second private clone and clears expiration before native activation. It
  does not revive an older expired node or overwrite a foreign active effect.
  Separate negative stage codes distinguish ownership, collision, bounds,
  insertion, returned-node and row failures.
- Read-only Bloodborne PC sampling while the user attacked/dodged observed raw
  stamina down to -9 with stable valid player ownership. ReadPlayer previously
  rejected all negative stamina, disabling otherwise valid controls. It now permits
  bounded overdraft, presents an empty gauge without writing the raw value, and
  checks the full pointer/owner chain again after reading. Up to three read-only
  attempts tolerate a transient torn snapshot; persistent mismatches still reject.
- Isolated synthetic checks now model immediate zero-duration expiration and
  nonzero returned indices, with explicit collision/foreign-row protection. No
  native multiplier write was performed against the user's live game during this
  investigation. Live multiplier verification remains pending on both runtimes.


### Insight and free progression investigation — 2026-10-26

- Read-only inspection of Bloodborne PC verified Insight 72 at active character data
  +0x84, with getter instruction `8B B3 84 00 00 00` at image+0x1901FB4.
  Editing validates the image, current character ownership, range 0–99 and readback.
- The native level-up price-check path at 0x1B2F5E0 uses CalcCorrectGraph row 200,
  including four floats at +0x3C..+0x48. Its row getter resolves repository +0x970/
  +0x978. The live supported values were approximately (0.1, 1, 0.02, 92).
  Zeroing only these cost coefficients makes the native calculation zero while
  retaining its stat-cap and cancellation logic. This is a reversible RAM edit:
  record original coefficients, verify table identity on refresh/restore, and
  never overwrite a foreign edit or an unloaded table. No parameter file is saved.
- Native weapon reinforcement cost builder 0x1B5EEC0 reads weapon basicPrice at
  0x1B5F02F (`8B 58 18`) and material quantities at 0x1B5F0BE
  (`44 8B 64 8D B0`). Replacing these complete instructions with `31 DB 90` and
  `45 31 E4 90 90` sets the cost to zero and takes the existing nonpositive-quantity
  skip for each material. The game still builds/validates the weapon upgrade.
  Only these two whitelisted sites permit protected code replacement; exact
  fingerprints are required and externally changed instructions are preserved.
- Both options are off by default, independent and supported by saved/startup
  presets. Stable polls read only; suspension occurs for applying/restoring changes.
  Open/reopen the relevant native menu after changing the options to refresh costs.
- Public cross-check: [bbhost player-data analysis](https://github.com/droogie/bbhost/blob/566184a74f071166299bb656c4af479e5bbe2771/src/decomp/player/player_data.cpp)
  describes Insight and the level-price curve (its image convention differs by
  0x400000). The upgrade paths above were independently inspected in the local
  1.09 image. No source or game content from that image is distributed.
- 522 synthetic checks and 112 image fingerprints pass. Live inspection was
  read-only: zero-resource leveling, all reinforcement tiers, spending/reloading
  Insight, menu refresh and shadPS4 gameplay still need confirmation.
