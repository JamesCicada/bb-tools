# Release checklist

`Implemented` means code and automated checks are complete. `User tested` is
checked only after James confirms that specific feature in Bloodborne PC 1.09.
New features remain pending live validation even when automated checks pass.

| Feature                                              | Implemented | User tested | Notes                                                                                            |
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

| Feature                                  | Implemented | User tested | Notes                                                                 |
| ---------------------------------------- | :---------: | :---------: | --------------------------------------------------------------------- |
| Supplied PNG emblem and Windows app icon |     ✔️      |     ✔️      | Original PNG preserved; multi-size ICO generated for the executable.  |
| Footer credit and GitHub shortcut        |     ✔️      |     ✔️      | Visible on every page; layout inspected at minimum window size.       |
| Manual stable-release update check       |     ✔️      |     ✔️      | 11 service checks and a live GitHub check pass; manual download only. |

## Live test pass

For each implemented feature, report its name, result and any reproduction steps.
Test one feature at a time, then check loading, death/respawn, disabling, and normal
disconnect where applicable. Update only the reported feature's user-test checkbox.

## v0.2.0 live test status

James confirmed the working features in the table above. Those confirmations are
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

## Ideas for later versions

These are proposals, not implemented features. Each needs verified runtime behavior
and its own automated and live test pass before being marked complete.

| Feature idea | Implemented | User tested | Purpose / constraints |
| --- | :---: | :---: | --- |
| Configurable hotkeys | ⌛ | ⌛ | Heal, toggle combat options, save/restore a location; avoid triggering while typing. |
| Named practice presets | ⌛ | ⌛ | Save damage, reward and combat settings; apply explicitly after connecting. |
| Boss retry workflow | ⌛ | ⌛ | Retry from a saved entrance with resource refill; boss revival requires separate flag and quest-safety research. |
| Encounter timer | ⌛ | ⌛ | Track attempt time, deaths and personal bests with pause/loading detection. |
| Enemy information panel | ⌛ | ⌛ | Inspect the targeted enemy's health, resistances and active effects. |
| Searchable item catalog and favorites | ⌛ | ⌛ | Filter by category, remember frequently spawned IDs, retain custom-ID entry. |
| Weapon setup editor | ⌛ | ⌛ | Inspect reinforcement and gem slots with valid-value checks and preview before applying. |
| Status-effect controls | ⌛ | ⌛ | Clear poison/frenzy and optionally prevent buildup without changing unrelated effects. |
| Camera and screenshot tools | ⌛ | ⌛ | Hide HUD, adjust field of view and add a verified free camera with restoration. |
| Chalice-aware location bookmarks | ⌛ | ⌛ | Bind coordinates to the exact generated dungeon and character before permitting restore. |
| Backup history and restore assistant | ⌛ | ⌛ | Show dated backups, verify contents and restore only while the game is closed. |
| Diagnostics export | ⌛ | ⌛ | Export app/runtime versions and failed checks without save contents or personal paths. |
| Expanded runtime compatibility | ⌛ | ⌛ | Separate adapters and validation for additional native builds and shadPS4. |

## Release automation

- ✔️ ZIP includes only `BBTools.exe` and `README.md`; packaging rejects unexpected files.
- ✔️ Main version bumps create an annotated tag and draft release after both CI builds pass.
- ⌛ First GitHub draft release run and manual asset review.
