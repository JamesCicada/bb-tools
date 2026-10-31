# Nightwatch compatibility — v0.6.10, NOT TESTED IN-GAME

Investigated 2026-10-31 against Nightwatch source revision
[`fd7ba2aa04faab6dda16ccbb393209577281f565`](https://github.com/vladtepes22/NightwatchCoop/tree/fd7ba2aa04faab6dda16ccbb393209577281f565).
The reporting user's exact Nightwatch release is not known. These changes do not
establish support for every release, co-op action or gameplay mod.

## Supported observer shape

Nightwatch installs a `HookPrologue` observer on the item-grant entry at
image+`0x17D89F0`. It replaces 13 bytes with `E9 rel32` and eight NOPs, preserves
arguments across a native observer callback, executes the original prologue and
resumes at entry+13. This matches the structure of the reported failure.

The new read-only adapter recognizes this specific observer ABI at two audited
entry points: item grants and the Level-up menu factory (`0x1BB7F60`, a Nightwatch
test/tracing hook). It verifies all fixed wrapper instructions, the unchanged
entry suffix, relocated prologue, exact continuation and executable entry,
wrapper and callback pages. It checks entry/wrapper stability and never caches
acceptance. Callback addresses may vary with ASLR.

This is structural compatibility, not authentication of the callback publisher
or verification of its entire behavior. Unknown jumps, replacement wrappers,
altered preservation/continuation code and unaudited entry sites remain rejected.
No Nightwatch code is removed, overwritten or bypassed; calls still enter the
runtime's observer. The validator uses independently authored instruction
signatures; Nightwatch's GPL implementation is not incorporated into BB Tools.

Both ordinary item grants and gem grants use the adapter. Instructions are
checked again while game threads are suspended, before publication. Ordinary
grants also recheck their captured item/handle manager pointers. No automatic
retry is introduced.

## Other compatibility improvements and limits

- Workshop menus validate the selected factory plus shared attach/cleanup code,
  rather than rejecting Storage because an unrelated Level-up factory changed.
  Selected-menu instructions and the menu manager are rechecked before publication.
- Nightwatch's chalice `GetValueHook` replaces `0x13CFD80` and remaps flag IDs.
  This is semantically different from a transparent observer and remains blocked
  for BB Tools' direct chalice travel. Diagnostics now report the failing address
  and bytes, with a specific hint for a redirected chalice flag reader.
- Other runtime overrides, discovery/drop interactions, multiplayer rewards,
  boss/story flags and host/guest save behavior are not established compatible
  by this change. Existing ownership/fingerprint checks remain in force.
- In this source revision, startup calls `PartyInit` only when `PartyRequested`
  is true. An in-game offline setting or playing alone is not proof that no party
  hooks are loaded. A fresh launcher Single-player session normally skips that
  subsystem; exact build, launcher mode and debug environment settings matter.

## Verification

Automated checks cover valid and malformed observers, every fixed wrapper-byte
mutation, executable-page constraints, exact continuation, unsupported sites,
item/gem validation and selected versus unrelated menu failures. A native Windows
fixture executes an observer whose callback deliberately clobbers an argument;
the original function still receives the correct argument after restoration.
No live game was modified during these checks.

**NOT TESTED IN-GAME.** Test Flamesprayer and ordinary goods, inventory versus
storage, gem grants, menus, death/loading and reconnect on a recorded Nightwatch
release. Record launcher Single-player/Host/Join mode separately from the in-game
offline setting. Check intended recipients/quantities in co-op; synthetic tests do
not establish synchronization behavior. Preserve logs containing `COMPAT` entries
and any remaining expected/actual instruction mismatches.

## Source references

- [Item observer installation](https://github.com/vladtepes22/NightwatchCoop/blob/fd7ba2aa04faab6dda16ccbb393209577281f565/engine/gpu/shim/party/party_items.cpp#L3047-L3079)
- [Observer ABI](https://github.com/vladtepes22/NightwatchCoop/blob/fd7ba2aa04faab6dda16ccbb393209577281f565/engine/gpu/shim/party/coop_hooks.cpp#L350-L372)
- [Conditional startup](https://github.com/vladtepes22/NightwatchCoop/blob/fd7ba2aa04faab6dda16ccbb393209577281f565/engine/gpu/shim/bbgpu.cpp#L500-L511)
- [Chalice flag redirection](https://github.com/vladtepes22/NightwatchCoop/blob/fd7ba2aa04faab6dda16ccbb393209577281f565/engine/gpu/shim/party/party_chalice.cpp#L547-L575)
