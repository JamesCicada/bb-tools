# Release checklist

`Implemented` means code and automated checks are complete. `Tested and confirmed` is
marked tested and confirmed only after explicit live confirmation of that specific feature in Bloodborne PC 1.09.
New features remain pending live validation even when automated checks pass.

| Feature                                              | Implemented | Tested and confirmed | Notes                                                                                            |
| ---------------------------------------------------- | :---------: | :------------------: | ------------------------------------------------------------------------------------------------ |
| Blood Echoes balance, health, stamina and attributes |     ✔️      |          ✔️          | Confirmed working.                                                                               |
| Native item spawning with constructed item handles   |     ✔️      |          ✔️          | confirmed the revised grant works.                                                               |
| Combat selections survive loads and warps            |     ✔️      |          ✔️          | Live confirmation recorded; loading regression checks also pass.                                 |
| Item-ID wiki reference under Spawn                   |     ✔️      |          ✔️          | Wiki links, decimal/base IDs, full hexadecimal IDs with category.                                |
| Complete base-game and Old Hunters lamp catalog      |     ✔️      |          ✔️          | 45 named destinations, including all 10 DLC lamps; local data verified.                          |
| Infinite consumables and Quicksilver Bullets         |     ✔️      |          ✔️          | Shared player-only consumption flag; loading and restoration checks pass.                        |
| Chalice lamp travel                                  |     ✔️      |          ✔️          | Six altar banks, nine named destinations; travel from Hunter's Dream. Locked lamps are rejected. |
| Save and restore named locations                     |     ✔️      |          ✔️          | Persistent named bookmarks; native engine relocation in the same loaded map.                     |
| Boss and important-location destinations             |     ✔️      |          ✔️          | 22 entrance presets tested and confirmed; revival support is tracked separately. |
| Disable enemy AI                                     |     ✔️      |          ✔️          | Verified All No Update AI flag; restore original state on disable.                               |
| Damage multiplier                                    |     ✔️      |          ✔️          | Private player-only native effect; range 0.1–100x.                                               |
| Blood Echoes gain multiplier                         |     ✔️      |          ✔️          | Native soulRate effect; range 0.1–100x, excludes balance edits.                                  |
| Level-up menu from anywhere                          |     ✔️      |          ✔️          | Native LevelUp step, attached and closed by MenuMan.                                             |
| Weapon upgrade menu from anywhere                    |     ✔️      |          ✔️          | Native Item/Reinforce menu; normal material/currency costs.                                      |
| Repair, storage, rune and gem menus                  |     ✔️      |          ✔️          | Native menu constructors and engine-owned lifecycle.                                             |
| No fall damage                                       |     ✔️      |          ✔️          | Verified local-player flag; restore original bit on disable.                                     |
| Release packaging and first-run instructions         |     ✔️      |          ✔️          | Self-contained Windows ZIP and checksum; packaged startup/shutdown checked.                      |

## v0.1.1 additions

| Feature                                  | Implemented | Tested and confirmed | Notes                                                                 |
| ---------------------------------------- | :---------: | :------------------: | --------------------------------------------------------------------- |
| Supplied PNG emblem and Windows app icon |     ✔️      |          ✔️          | Original PNG preserved; multi-size ICO generated for the executable.  |
| Footer credit and GitHub shortcut        |     ✔️      |          ✔️          | Visible on every page; layout inspected at minimum window size.       |
| Manual stable-release update check       |     ✔️      |          ✔️          | 11 service checks and a live GitHub check pass; manual download only. |

## Live test pass

For each implemented feature, report its name, result and any reproduction steps.
Test one feature at a time, then check loading, death/respawn, disabling, and normal
disconnect where applicable. Update only the reported feature's tested-and-confirmed status.

## v0.2.0 live test status

The working features in the table above are tested and confirmed. Those confirmations are
preserved; pending entries remain pending rather than being inferred from other tests.

- ✔️ Damage and enemy Echoes reward multipliers confirmed working.
- ✔️ Level-up and weapon fortification menus confirmed working.
- ✔️ Repair, storage, blood gem and rune menus confirmed working.
- ⌛ Additional multiplier regression pass: death/warp/load, normal disconnect,
  spending and bloodstain recovery.
- ⌛ Chalice travel from Hunter's Dream: two existing altars and unlocked layer lamps;
  empty/incomplete altar rejection and correct generated dungeon after loading.
- ⌛ Micolash entrance.
- ⌛ Gehrman and Moon Presence shared arena gate (normal story prerequisites).

The original 19 boss presets remain confirmed; the three new entrances are tracked
separately. Automated checks and package startup checks do not replace live results.

## Confirmed additions

- ✔️ FOV slider, including wide-angle support: tested and confirmed.
- ✔️ Saved practice presets: tested and confirmed.
- ✔️ All-process picker, filtering and likely-game ranking: tested and confirmed.

| Feature                        | Implemented | Tested and confirmed | Notes                                                                                                                                                                 |
| ------------------------------ | :---------: | :------------------: | --------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| All-process picker and filter  |     ✔️      |          ✔️          | All enumerable processes, restricted identity labels, name/decimal/hex PID search and likely-game ranking. Auto-connect remains limited to validated game candidates. |
| Per-session logs and retention |     ✔️      |          ✔️          | logs folder beside EXE; keep five newest session files; status/action/error records. Confirm multiple launches and deletion of older logs.                            |

## Current implementation batch · 2026-10-18

| Feature                     | Implemented | Tested and confirmed | Notes                                                                                                                                                       |
| --------------------------- | :---------: | :------------------: | ----------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Expanded settings presets   |     ✔️      |          ✔️          | Combat, FOV, camera distance/height, durability, buildup control, auto-connect, startup FOV and hotkey preferences. Older presets retain defaults.          |
| Startup FOV default         |     ✔️      |          ✔️          | Save the current FOV in Settings; opt in to application after a character loads.                                                                            |
| Startup preset              |     ✔️      |          ✔️          | Select a saved preset in Combat; applies once per connection after a living character validates. No stats, balance edits, spawns or revival run at startup. |
| Status buildup clear/refill |     ✔️      |          ✔️          | Clears the five buildup gauges and optionally repeats while loaded. Does not remove active effects or guarantee one-hit immunity.                           |

### Fixes after first live feedback

- ✔️ Repair now follows equipped inventory indices through both entry banks to owned instances, verifies writes and reports the repair count. Automatic repair retries transient failures.
- ✔️ Revival has no Dream/prior-encounter gate; one click resets flags and reloads the selected area. Retry waits for a loading transition before entrance placement.
- ✔️ Hotkeys use physical-state polling with a visible shortcut summary and selectable modifiers, including None for F-keys alone.
- ✔️ Camera page warns that FOV/distance/height overrides are unstable and can cause performance and visual issues.
- ✔️ Boss revival/retry, hotkeys and the other implemented additions are tested and confirmed.
- ⌛ Weapon durability remains unresolved: repair-now, threshold auto-repair and keep-full mode are deferred to the next development session.

### Test this batch

Current fixes: 209 automated checks passed in both Debug and Release; all 60 native-image
fingerprints verified. These checks use isolated synthetic processes. Live confirmation
is recorded for the implemented additions except weapon durability.

- ✔️ Startup: save FOV 100°, restart, connect/load; verify FOV resumes. Bind a full preset, reconnect and check all stored toggles and hotkeys.
- ⌛ Durability: damage an equipped weapon, test repair-now, threshold repair and full-durability mode; swap/transform weapons and load another area.
- ✔️ Revival: use a separate practice save. From any area select a supported boss, revive/retry, verify fog and boss, then test entrance placement/refill/timer. Original flag journals are in `%LOCALAPPDATA%\BBTools\boss-revival`.
- ✔️ Retry cancellation: cancel during loading; entrance placement must stay cancelled. Already requested area travel and saved revival flags remain applied.
- ✔️ Presets/library: rename, duplicate, group/search, export/import and SQLite backup; verify old presets still load and importing does not run game actions.
- ✔️ Hotkeys: select None and test F-keys alone, then Ctrl+Alt; enable/apply, test game focus, trainer focus, text entry and unrelated-app focus.
- ✔️ Timer: manual pause, loading, death/respawn and finish; compare displayed timing with expected attempt duration.
- ✔️ Camera: distance/height scales and restore, including reload and startup preset application.
- ✔️ Buildup: clear meters and keep-empty mode; existing active poison must remain governed by normal cure behavior.
- ✔️ Save restore: close game/launcher, use a disposable save-folder copy, restore a backup and check retained recovery folder/ZIP.
- ✔️ Session logs and redacted diagnostics export.

### Research still pending

Micolash and the two ending bosses require additional encounter/ending-state profiles.
Independent walk/run/sprint speed, action speed, global game speed, enemy inspection,
weapon/gem editing, active-effect cures, HUD/free camera, gravity/jumps, flight/noclip,
exact chalice instance bookmarks and additional runtime adapters remain unimplemented.
These need verified native fields or engine calls and lifecycle checks; they are not
represented by placeholder controls. The implemented item catalog is the bundled
common catalog plus custom favorites, not a complete datamined item-name database.

## Next development priorities

1. **P1 — Weapon durability (next session):** resolve repair-now, threshold
   auto-repair and keep-full mode with live validation. These remain known issues.
2. **P2 — Remaining boss revival profiles:** research Micolash, Gehrman and Moon Presence.
3. **P3 — Movement settings:** player walk/run/sprint speed with restoration and loading checks.
4. **P4 — Advanced practice and exploration:** action/game speed, enemy inspection,
   weapon/gem editing, free camera, gravity/jumps, flight and noclip.

FOV, expanded/startup presets, library tools, hotkeys, encounter timing and the
implemented boss retry workflow are tested and confirmed.

### Library and preset live checks

- ✔️ Start with existing bookmarks; verify migration and restart persistence.
- ✔️ Saved practice presets tested and confirmed.
- ✔️ Additional preset regression checks: replace/delete and apply across a load/warp.
- ✔️ Verify saving uses applied multipliers rather than unsubmitted text edits.
- ✔️ Search locations by name/map and restore one in the matching map.
- ✔️ Check the reorganized Player, Combat, Items, Travel, Camera, Workshop and Settings pages.
- ✔️ FOV tested and confirmed.
- ✔️ Additional FOV regression checks: lock-on, aiming, load/warp and disconnect cleanup.
- ✔️ Confirm 100° and 120° on a standard runtime; check reset/disconnect restores the clamp and an existing external uncap is preserved.

## Ideas for later versions

This roadmap includes implemented foundations and future proposals. Implementation
and tested-and-confirmed status are tracked separately; pending live checks remain ⌛.

| Feature idea                          | Implemented | Tested and confirmed | Purpose / constraints                                                                                                                                                                                          |
| ------------------------------------- | :---------: | :------------------: | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Configurable hotkeys                  |     ✔️      |          ✔️          | Selectable None/Ctrl/Alt/Ctrl+Alt modifiers plus configurable F1–F12 bindings for heal, god mode, quick bookmark save/restore and boss retry; only game/trainer focus, ignored in app text fields/dialogs.     |
| Named practice presets                |     ✔️      |          ✔️          | Save, replace, apply and delete combat settings, health refill, stamina and applied multipliers; explicit application after connecting.                                                                        |
| SQLite preset and warp database       |     ✔️      |          ✔️          | Schema-versioned local database; transactional one-time migration retains existing locations.json.                                                                                                             |
| Search saved warp locations           |     ✔️      |          ✔️          | Search saved bookmarks by name or map; existing same-map relocation checks still apply.                                                                                                                        |
| Library organization and profiles     |     ✔️      |          ✔️          | Preset rename/duplicate, named groups for presets/bookmarks, item favorites and settings profiles. Runtime/character-aware generated-dungeon bookmarks remain separate research.                               |
| Database backup and import/export     |     ✔️      |          ✔️          | Versioned JSON for presets, bookmarks and favorites, transactional validated imports and consistent SQLite backups.                                                                                            |
| Boss retry workflow                   |     ✔️      |          ✔️          | 19 base-game/DLC profiles. From any loaded area: journal original encounter flags, revive, reload area, place outside fog, refill health/stamina and start timer. Micolash/Gehrman/Moon Presence are excluded. |
| Encounter timer                       |     ✔️      |          ✔️          | Manual start/finish or automatic retry start; pause while loading/dead; attempts, deaths and session best. Tested and confirmed.                                                                   |
| Enemy information panel               |     ⌛      |          ⌛          | Inspect the targeted enemy's health, resistances and active effects.                                                                                                                                           |
| Searchable common items and favorites |     ✔️      |          ✔️          | Search bundled common items and saved custom-ID favorites by name/ID; load favorites before explicit spawning. Complete game-wide metadata remains future work.                                                |
| Weapon setup editor                   |     ⌛      |          ⌛          | Inspect reinforcement and gem slots with valid-value checks and preview before applying.                                                                                                                       |
| Unlimited weapon durability           |     ✔️      |          ⌛          | Known issue: not working reliably. Further investigation deferred to the next development session. |
| Automatic weapon repair               |     ✔️      |          ⌛          | Known issue: not working reliably. Further investigation deferred to the next development session. |
| Status-effect controls                |     ⌛      |          ⌛          | Clear poison/frenzy and optionally prevent buildup without changing unrelated effects.                                                                                                                         |
| Field-of-view (FOV) slider            |     ✔️      |          ✔️          | Live vertical FOV override and original-value restore. 38–120° vertical, with automatic reversible clamp removal above 48°.                                                                                    |
| Wider FOV without an external patch   |     ✔️      |          ✔️          | Verified four-byte in-memory clamp update; preserves preexisting uncap, restores owned changes and rejects unknown code.                                                                                       |
| Player movement speed                 |     ⌛      |          ⌛          | Separate walking, running and sprint speed multipliers; affect only the player and restore defaults on disable.                                                                                                |
| Player action speed                   |     ⌛      |          ⌛          | Optional player animation speed multiplier; verify attacks, dodges and interactions independently of movement.                                                                                                 |
| Game speed and slow motion            |     ⌛      |          ⌛          | Adjustable simulation speed for practice with a quick reset; verify menus, audio and loading behavior.                                                                                                         |
| Camera distance and height            |     ✔️      |          ✔️          | Distance/look-at-height scales 0.5–2.0, original-value restoration, loading recovery and preset support. Shoulder offset remains unimplemented.                                                                |
| Camera and screenshot tools           |     ⌛      |          ⌛          | Hide HUD, toggle camera shake and add a verified free camera with adjustable speed and restoration.                                                                                                            |
| Gravity and jump controls             |     ⌛      |          ⌛          | Optional player gravity and jump-height settings for exploration; verify collision and restore normal values.                                                                                                  |
| Flight and noclip exploration         |     ⌛      |          ⌛          | Research controlled flight and collision bypass with a return-to-bookmark action and map-boundary checks.                                                                                                      |
| Chalice-aware location bookmarks      |     ⌛      |          ⌛          | Bind coordinates to the exact generated dungeon and character before permitting restore.                                                                                                                       |
| Backup history and restore assistant  |     ✔️      |          ✔️          | List BB Tools ZIPs; verify paths/sizes/CRC, require game closed, retain prior folder and recovery ZIP before replacement.                                                                                      |
| Diagnostics export                    |     ✔️      |          ✔️          | App/runtime versions and redacted status/config summary, excluding save contents and personal filesystem paths.                                                                                                |
| Expanded runtime compatibility        |     ⌛      |          ⌛          | Separate adapters and validation for additional native builds and shadPS4.                                                                                                                                     |

## Release automation

- ✔️ ZIP includes only `BBTools.exe` and `README.md`; packaging rejects unexpected files.
- ✔️ Main version bumps create an annotated tag and draft release after both CI builds pass.
- ✔️ First GitHub draft release run and manual asset review.
