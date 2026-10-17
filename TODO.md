# Release checklist

`Implemented` means code and automated checks are complete. `User tested` is
checked only after James confirms that specific feature in Bloodborne PC 1.09.
New features remain pending live validation even when automated checks pass.

| Feature                                              | Implemented | User tested | Notes                                                                                   |
| ---------------------------------------------------- | :---------: | :---------: | --------------------------------------------------------------------------------------- |
| Blood Echoes balance, health, stamina and attributes |     ✔️      |     ✔️      | Confirmed working by James.                                                             |
| Native item spawning with constructed item handles   |     ✔️      |     ✔️      | James confirmed the revised grant works.                                                |
| Combat selections survive loads and warps            |     ✔️      |     ✔️      | Live confirmation recorded; loading regression checks also pass.                        |
| Item-ID wiki reference under Spawn                   |     ✔️      |     ✔️      | Wiki links, decimal/base IDs, full hexadecimal IDs with category.                       |
| Complete base-game and Old Hunters lamp catalog      |     ✔️      |     ✔️      | 45 named destinations, including all 10 DLC lamps; local data verified.                 |
| Chalice lamp travel                                  |     ⌛      |     ⌛      | Dungeon destinations depend on the character's generated dungeon.                       |
| Save and restore named locations                     |     ✔️      |     ✔️      | Persistent named bookmarks; native engine relocation in the same loaded map.            |
| Boss and important-location destinations             |     ✔️      |     ✔️      | 19 fog entrance presets, all five DLC bosses; custom important spots can be bookmarked. |
| Disable enemy AI                                     |     ✔️      |     ✔️      | Verified All No Update AI flag; restore original state on disable.                      |
| Damage multiplier                                    |     ⌛      |     ⌛      | Requires verified player-only damage interception.                                      |
| Blood Echoes gain multiplier                         |     ⌛      |     ⌛      | Scale rewards, excluding spending and bloodstain recovery.                              |
| Level-up menu from anywhere                          |     ⌛      |     ⌛      | Verify menu request and close lifecycle.                                                |
| Weapon upgrade menu from anywhere                    |     ⌛      |     ⌛      | Preserve normal material/currency costs.                                                |
| Repair, storage, rune and gem menus                  |     ⌛      |     ⌛      | Additional useful workshop shortcuts.                                                   |
| No fall damage                                       |     ✔️      |     ✔️      | Verified local-player flag; restore original bit on disable.                            |
| Release packaging and first-run instructions         |     ✔️      |     ⌛      | Self-contained Windows ZIP and checksum; packaged startup/shutdown checked.             |

## v0.1.1 additions

| Feature | Implemented | User tested | Notes |
| --- | :---: | :---: | --- |
| Supplied PNG emblem and Windows app icon | [x] | [ ] | Original PNG preserved; multi-size ICO generated for the executable. |
| Footer credit and GitHub shortcut | [x] | [ ] | Visible on every page; layout inspected at minimum window size. |
| Manual stable-release update check | [x] | [ ] | 11 service checks and a live GitHub check pass; manual download only. |

## Live test pass

For each implemented feature, report its name, result and any reproduction steps.
Test one feature at a time, then check loading, death/respawn, disabling, and normal
disconnect where applicable. Update only the reported feature's user-test checkbox.

## Remaining implementation order

1. Verify the damage and reward interception paths for player-only multipliers.
   Damage research lead: 1.09 RVA `0x1A2BEC1`; the applied damage, rally bookkeeping
   and displayed damage must remain consistent. Avoid a polling-based approximation.
2. Verify remote level-up / upgrade menu requests and menu ownership/close state.
   The `Util_RequestLevelUp` binding found during research changes requested stats;
   it is **not** a verified menu opener and must not be used as one.
3. Resolve generated chalice destinations through the active character's dungeon
   state, rather than guessing that a fixed m29 return point identifies a dungeon.
4. Add the remaining story boss entrances (Micolash, Gehrman and Moon Presence),
   with coordinates verified against their actual map objects.
5. Collect v0.1.0 release feedback and check compatibility reports before expanding the supported profiles.
