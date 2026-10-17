# Release checklist

`Implemented` means code and automated checks are complete. `Tested and confirmed` is
marked tested and confirmed only after explicit live confirmation of that specific feature in Bloodborne PC 1.09.
New features remain pending live validation even when automated checks pass.

| Feature                                              | Implemented | Tested and confirmed | Notes                                                                                            |
| ---------------------------------------------------- | :---------: | :---------: | ------------------------------------------------------------------------------------------------ |
| Blood Echoes balance, health, stamina and attributes |     ✔️      |     ✔️      | Confirmed working.                                                                               |
| Native item spawning with constructed item handles   |     ✔️      |     ✔️      | confirmed the revised grant works.                                                               |
| Combat selections survive loads and warps            |     ✔️      |     ✔️      | Live confirmation recorded; loading regression checks also pass.                                 |
| Item-ID wiki reference under Spawn                   |     ✔️      |     ✔️      | Wiki links, decimal/base IDs, full hexadecimal IDs with category.                                |
| Complete base-game and Old Hunters lamp catalog      |     ✔️      |     ✔️      | 45 named destinations, including all 10 DLC lamps; local data verified.                          |
| Infinite consumables and Quicksilver Bullets         |     ✔️      |     ✔️      | Shared player-only consumption flag; loading and restoration checks pass.                        |
| Chalice lamp travel                                  |     ✔️      |     ⌛      | Six altar banks, nine named destinations; travel from Hunter's Dream. Locked lamps are rejected. |
| Save and restore named locations                     |     ✔️      |     ✔️      | Persistent named bookmarks; native engine relocation in the same loaded map.                     |
| Boss and important-location destinations             |     ✔️      |     ✔️      | Original 19 confirmed; 22 presets now available. Three new entrances await the v0.2.0 test pass. |
| Disable enemy AI                                     |     ✔️      |     ✔️      | Verified All No Update AI flag; restore original state on disable.                               |
| Damage multiplier                                    |     ✔️      |     ✔️      | Private player-only native effect; range 0.1–100x.                                               |
| Blood Echoes gain multiplier                         |     ✔️      |     ✔️      | Native soulRate effect; range 0.1–100x, excludes balance edits.                                  |
| Level-up menu from anywhere                          |     ✔️      |     ✔️      | Native LevelUp step, attached and closed by MenuMan.                                             |
| Weapon upgrade menu from anywhere                    |     ✔️      |     ✔️      | Native Item/Reinforce menu; normal material/currency costs.                                      |
| Repair, storage, rune and gem menus                  |     ✔️      |     ✔️      | Native menu constructors and engine-owned lifecycle.                                             |
| No fall damage                                       |     ✔️      |     ✔️      | Verified local-player flag; restore original bit on disable.                                     |
| Release packaging and first-run instructions         |     ✔️      |     ✔️      | Self-contained Windows ZIP and checksum; packaged startup/shutdown checked.                      |

## v0.1.1 additions

| Feature                                  | Implemented | Tested and confirmed | Notes                                                                 |
| ---------------------------------------- | :---------: | :---------: | --------------------------------------------------------------------- |
| Supplied PNG emblem and Windows app icon |     ✔️      |     ✔️      | Original PNG preserved; multi-size ICO generated for the executable.  |
| Footer credit and GitHub shortcut        |     ✔️      |     ✔️      | Visible on every page; layout inspected at minimum window size.       |
| Manual stable-release update check       |     ✔️      |     ✔️      | 11 service checks and a live GitHub check pass; manual download only. |

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

| Feature | Implemented | Tested and confirmed | Notes |
| --- | :---: | :---: | --- |
| All-process picker and filter | ✔️ | ✔️ | All enumerable processes, restricted identity labels, name/decimal/hex PID search and likely-game ranking. Auto-connect remains limited to validated game candidates. |

## Next development priorities

1. **P1 — Community-requested FOV slider (tested and confirmed):**
   38–120° vertical without an external patch; reversible runtime clamp ownership,
   individual camera defaults and load recovery. Additional edge-case checks remain below.
2. **P1 — Library foundation (implemented, confirmation pending):** SQLite storage,
   migration, named practice presets and saved-location search. These support future
   hotkeys and boss practice workflows without new game-memory research.
3. **P2 — Data portability and quick access:** library backup/import/export, favorites,
   groups and configurable hotkeys. Validate imported destinations before use.
4. **P3 — Movement and camera offsets:** player movement speed and camera offsets first;
   verify runtime pointers, default restoration and loading behavior before shipping.
5. **P4 — Advanced practice and exploration:** encounter timer, boss retries, action/game
   speed, flight and noclip. These need more state handling and live regression tests.

### Library and preset live checks

- ⌛ Start with existing bookmarks; verify migration and restart persistence.
- ✔️ Saved practice presets tested and confirmed.
- ⌛ Additional preset regression checks: replace/delete and apply across a load/warp.
- ⌛ Verify saving uses applied multipliers rather than unsubmitted text edits.
- ⌛ Search locations by name/map and restore one in the matching map.
- ⌛ Check the reorganized Player, Combat, Items, Travel, Camera, Workshop and Settings pages.
- ✔️ FOV tested and confirmed.
- ⌛ Additional FOV regression checks: lock-on, aiming, load/warp and disconnect cleanup.
- ⌛ Confirm 100° and 120° on a standard runtime; check reset/disconnect restores the clamp and an existing external uncap is preserved.

## Ideas for later versions

This roadmap includes implemented foundations and future proposals. Implementation
and tested-and-confirmed status are tracked separately; pending live checks remain ⌛.

| Feature idea                          | Implemented | Tested and confirmed | Purpose / constraints                                                                                            |
| ------------------------------------- | :---------: | :---------: | ---------------------------------------------------------------------------------------------------------------- |
| Configurable hotkeys                  |     ⌛      |     ⌛      | Heal, toggle combat options, save/restore a location; avoid triggering while typing.                             |
| Named practice presets                |     ✔️      |     ✔️      | Save, replace, apply and delete combat settings, health refill, stamina and applied multipliers; explicit application after connecting. |
| SQLite preset and warp database       |     ✔️      |     ⌛      | Schema-versioned local database; transactional one-time migration retains existing locations.json. |
| Search saved warp locations           |     ✔️      |     ⌛      | Search saved bookmarks by name or map; existing same-map relocation checks still apply. |
| Library organization and profiles     |     ⌛      |     ⌛      | Add dedicated rename/duplicate actions, groups, item favorites and settings profiles; richer runtime/character context for future travel adapters. |
| Database backup and import/export      |     ⌛      |     ⌛      | Back up local user data and share selected presets or locations through a versioned export; validate imports and resolve duplicate entries. |
| Boss retry workflow                   |     ⌛      |     ⌛      | Retry from a saved entrance with resource refill; boss revival requires separate flag and quest-safety research. |
| Encounter timer                       |     ⌛      |     ⌛      | Track attempt time, deaths and personal bests with pause/loading detection.                                      |
| Enemy information panel               |     ⌛      |     ⌛      | Inspect the targeted enemy's health, resistances and active effects.                                             |
| Searchable item catalog and favorites |     ⌛      |     ⌛      | Filter by category, remember frequently spawned IDs, retain custom-ID entry.                                     |
| Weapon setup editor                   |     ⌛      |     ⌛      | Inspect reinforcement and gem slots with valid-value checks and preview before applying.                         |
| Status-effect controls                |     ⌛      |     ⌛      | Clear poison/frenzy and optionally prevent buildup without changing unrelated effects.                           |
| Field-of-view (FOV) slider             |     ✔️      |     ✔️      | Live vertical FOV override and original-value restore. 38–120° vertical, with automatic reversible clamp removal above 48°. |
| Wider FOV without an external patch  |     ✔️      |     ✔️      | Verified four-byte in-memory clamp update; preserves preexisting uncap, restores owned changes and rejects unknown code. |
| Player movement speed                 |     ⌛      |     ⌛      | Separate walking, running and sprint speed multipliers; affect only the player and restore defaults on disable.    |
| Player action speed                   |     ⌛      |     ⌛      | Optional player animation speed multiplier; verify attacks, dodges and interactions independently of movement.     |
| Game speed and slow motion             |     ⌛      |     ⌛      | Adjustable simulation speed for practice with a quick reset; verify menus, audio and loading behavior.             |
| Camera distance and offsets            |     ⌛      |     ⌛      | Adjust camera distance, height and shoulder offset; preserve lock-on behavior and provide a default reset.          |
| Camera and screenshot tools           |     ⌛      |     ⌛      | Hide HUD, toggle camera shake and add a verified free camera with adjustable speed and restoration.                |
| Gravity and jump controls              |     ⌛      |     ⌛      | Optional player gravity and jump-height settings for exploration; verify collision and restore normal values.      |
| Flight and noclip exploration           |     ⌛      |     ⌛      | Research controlled flight and collision bypass with a return-to-bookmark action and map-boundary checks.          |
| Chalice-aware location bookmarks      |     ⌛      |     ⌛      | Bind coordinates to the exact generated dungeon and character before permitting restore.                         |
| Backup history and restore assistant  |     ⌛      |     ⌛      | Show dated backups, verify contents and restore only while the game is closed.                                   |
| Diagnostics export                    |     ⌛      |     ⌛      | Export app/runtime versions and failed checks without save contents or personal paths.                           |
| Expanded runtime compatibility        |     ⌛      |     ⌛      | Separate adapters and validation for additional native builds and shadPS4.                                       |

## Release automation

- ✔️ ZIP includes only `BBTools.exe` and `README.md`; packaging rejects unexpected files.
- ✔️ Main version bumps create an annotated tag and draft release after both CI builds pass.
- ⌛ First GitHub draft release run and manual asset review.
