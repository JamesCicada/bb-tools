# Release checklist

`Implemented` means code and automated checks are complete. `Tested and confirmed` is
marked tested and confirmed only after explicit live confirmation of that specific feature in Bloodborne PC 1.09.
New features remain pending live validation even when automated checks pass.

## Reported regressions — v0.6.2

| Feature | Implemented | Tested and confirmed | Notes |
| --- | :---: | :---: | --- |
| Ludwig revival | ✔️ | ⌛ | Reset separate defeat gate 9471 and Ludwig entry/phase state. Check repeat and first-encounter modes, fog, both phases and post-fight behavior. |
| Multiplier cleanup and legacy-effect recovery | ✔️ | ⌛ | Finite renewable effects; setting both multipliers to 1 and applying removes identifiable old trainer effects. Check repeated 2× application, disconnect, force-close, warp, death and full process restart. Persistence across a full restart has not been reproduced. |

## Reported regressions — v0.6.3

| Feature | Implemented | Tested and confirmed | Notes |
| --- | :---: | :---: | --- |
| Camera → Movement controls ([#1](https://github.com/JamesCicada/bb-tools/issues/1)) | ✔️ | ⌛ | Re-enable speed/gravity toggles and sliders once a player validates, including after loading. UI/native checks cover selection, application and restoration; live retest pending. |

## Reported regressions — v0.6.5

- ✔️ Fix zero-duration native multiplier insertion by validating its returned node index; preserve expired/foreign effects and add distinct failure diagnostics.
- ✔️ Accept normal negative stamina without losing the player; retain owner checks and bounded read-only snapshot retries.
- ✔️ Suggest restarting as administrator after confirmed Windows access denial, including automatic discovery; distinguish loading from permissions.
- ✔️ Tested and confirmed on Bloodborne PC: multiplier and player-detection fixes.
- ⌛ Additional multiplier death/loading, preset and reconnect regression coverage, plus shadPS4-specific retesting. Permission guidance and native-speaker review retain their separate status.

## Item discovery and eligible enemy drops — v0.6.4

- ✔️ Verify a separate discovery-effect path in the private 1.09 image at RVA `0x1581830`: effect state `0x42`, bonus field `+0x1FC`, added to the Arcane-derived baseline. Base attributes do not need editing.
- ✔️ Implement a temporary player-only +0–1000 point bonus at the native discovery return, preserving Arcane/rune calculation and base stats. Use a separate eligible-enemy-roll hook to remove blank outcomes after level-dependent item resolution. Do not change shared loot rows, game files, gem quality/effects or chest/scripted rewards.
- ✔️ Add native helper and lifecycle regression checks for ownership, no compounding/stat edits, blank/invalid outcomes, non-enemy caller exclusion, preset persistence and restoration. Include chalice-map and resolved gem-type fixture cases.
- ⌛ Test both options in normal areas and chalice dungeons: mobs, bosses, generated/level-dependent drops, empty/ineligible tables, death, warp, reconnect, disabling and another runtime mod. Synthetic fixture checks do not establish real chalice compatibility or improved gem quality.

## Community translations — v0.6.4

- ✔️ Embed English source and all 28 configured locale catalogs; add live language switching, system detection, English fallback, RTL layout and a persisted UI language independent of gameplay presets.
- ✔️ Draft the extracted Simplified Chinese interface/message catalog. Other targets have starter labels. Add Preferences/About contribution links and file-based Crowdin mapping; preserve IDs, placeholders and numeric parsing.
- ✔️ Complete Arabic and French catalog drafts, joining Simplified Chinese at 100% extracted coverage in v0.6.5, including the new permission message. Native-speaker and live usability review remain pending.
- ✔️ Upload both drafts to Crowdin; verify 100% translated and 0% approved. Retain existing duplicate translations and keep native-speaker approval pending.
- ✔️ Add catalog/reference validation and placeholder-format checks to the existing CI test command.
- ⌛ Native-speaker review, translated-layout testing, Bloodborne terminology and completion of starter catalogs.
- ✔️ Link the app and contributor guide to [BB Tools on Crowdin](https://crowdin.com/project/bb-tools).
- ✔️ Verify Crowdin's GitHub integration through merged translation pull requests #2 and #3. Keep untranslated strings omitted from JSON exports so the app's English fallback preserves accurate coverage.

## Feature checklist

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

- ✔️ Earlier repair attempted owned-instance writes, but live feedback found unreliable right-hand repair. Superseded by the native inventory repair replacement below.
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

### Development build awaiting live tests

| Feature | Implemented | Tested and confirmed | Notes |
| --- | :---: | :---: | --- |
| Native inventory weapon repair | ✔️ | ⌛ | Replaces the unreliable cache writes. Test all four weapon slots, manual repair, threshold and keep-full modes. |
| Player movement multiplier | ✔️ | ⌛ | Player-only transient movement scale; one shared multiplier, not independent walk/run/sprint controls. |
| World gravity | ✔️ | ⌛ | Affects all actors and physics; restores captured values. |
| Active poison cure | ✔️ | ⌛ | Removes slow/rapid poison effects while preserving unrelated effects. |
| Loaded-character inspector | ✔️ | ⌛ | Live lock-on tracking and retained snapshots implemented; live validation pending. |
| Micolash, Gehrman and Moon Presence revival | ✔️ | ⌛ | Experimental event profiles; ending bosses can alter story/NG progression. |
| Game-owned item-name catalog import | ✔️ | ⌛ | Import English names from your own game files; search by name or ID. |
| HUD visibility and detached free camera | ✔️ | ⌛ | HUD available; photo mode disabled and hidden pending movement/HUD fixes. |
| Flight and noclip | ✔️ | ⌛ | Flight/noclip disabled and hidden pending further fixes. |
| Chalice coordinate bookmarks | ✔️ | ⌛ | Restricted to the same connection, character, altar and dungeon progress snapshot. |
| Expanded advanced presets | ✔️ | ⌛ | Movement, world gravity and HUD; flight/free camera are explicit session actions. |
| Official shadPS4 support | ✔️ | ✔️ | Tested and confirmed; no experimental opt-in. Image/player validation retained. |

See [the test plan](docs/1.0-test-plan.md) and [compatibility results](docs/COMPATIBILITY.md).
Earlier live confirmations remain valid only for their recorded feature/configuration.

### Research still pending

- ⌛ Independent walking/running/sprinting and action-animation speed.
- ⌛ Global simulation speed / slow motion.
- ⌛ Direct weapon reinforcement and gem editing with native inventory synchronization.
- ⌛ Camera shake and shoulder offset.
- ⌛ Jump-height control independent of world gravity.
- ⌛ Persistent generated-dungeon bookmarks across reconnection/progress changes.
- ✔️ Exact lock-on target inspection implemented; ⌛ live validation and full enemy defense/limb statistics.

These need verified engine paths. They are not represented by placeholder controls.

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

### Complete built-in item catalog — high priority

- ⌛ Map all base-game and DLC item names to their correct IDs and categories so users can search by name without third-party ID lookup or manual catalog import.
- ⌛ Cover consumables, weapons and variants, armor, Caryll/oath runes, blood gems, key items and chalice items; include common aliases and category filters.
- ⌛ Distinguish spawnable item IDs from rune/gem effect IDs and generated instance handles. Use the appropriate grant path for each category; do not treat effect IDs as ordinary Goods IDs.
- ⌛ Verify mappings, search results and actual grants in game before marking tested and confirmed. Keep manual ID entry available as an advanced option.

This roadmap includes implemented foundations and future proposals. Implementation
and tested-and-confirmed status are tracked separately; pending live checks remain ⌛.

| Feature idea                          | Implemented | Tested and confirmed | Purpose / constraints                                                                                                                                                                                          |
| ------------------------------------- | :---------: | :------------------: | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Configurable hotkeys                  |     ✔️      |          ✔️          | Selectable None/Ctrl/Alt/Ctrl+Alt modifiers plus configurable F1–F12 bindings for heal, god mode, quick bookmark save/restore and boss retry; only game/trainer focus, ignored in app text fields/dialogs.     |
| Named practice presets                |     ✔️      |          ✔️          | Save, replace, apply and delete combat settings, health refill, stamina and applied multipliers; explicit application after connecting.                                                                        |
| SQLite preset and warp database       |     ✔️      |          ✔️          | Schema-versioned local database; transactional one-time migration retains existing locations.json.                                                                                                             |
| Search saved warp locations           |     ✔️      |          ✔️          | Search saved bookmarks by name or map; existing same-map relocation checks still apply.                                                                                                                        |
| Library organization and profiles     |     ✔️      |          ✔️          | Preset rename/duplicate, named groups for presets/bookmarks, item favorites and settings profiles. Connection/character-bound generated-dungeon bookmarks implemented; cross-session persistence remains research.                               |
| Database backup and import/export     |     ✔️      |          ✔️          | Versioned JSON for presets, bookmarks and favorites, transactional validated imports and consistent SQLite backups.                                                                                            |
| Boss retry workflow                   |     ✔️      |          ✔️          | 22 base-game/DLC profiles (three experimental). From any loaded area: journal original encounter flags, revive, reload area, place outside fog, refill health/stamina and start timer. Micolash/Gehrman/Moon Presence await live tests. |
| Encounter timer                       |     ✔️      |          ✔️          | Manual start/finish or automatic retry start; pause while loading/dead; attempts, deaths and session best. Tested and confirmed.                                                                   |
| Enemy information panel               |     ✔️      |          ⌛          | Dedicated live target page with retained data, status effects and super-armor readings; live validation pending.                                                                                                                                           |
| Searchable common items and favorites |     ✔️      |          ✔️          | Search bundled common items and saved custom-ID favorites by name/ID; load favorites before explicit spawning. Optional full game-owned English name import implemented; live test pending.                                                |
| Weapon setup editor                   |     ⌛      |          ⌛          | Inspect reinforcement and gem slots with valid-value checks and preview before applying.                                                                                                                       |
| Unlimited weapon durability           |     ✔️      |          ⌛          | Native inventory repair replacement implemented; live both-hand validation pending. |
| Automatic weapon repair               |     ✔️      |          ⌛          | Native inventory repair replacement implemented; live both-hand validation pending. |
| Status-effect controls                |     ⌛      |          ⌛          | Clear poison/frenzy and optionally prevent buildup without changing unrelated effects.                                                                                                                         |
| Field-of-view (FOV) slider            |     ✔️      |          ✔️          | Live vertical FOV override and original-value restore. 38–120° vertical, with automatic reversible clamp removal above 48°.                                                                                    |
| Wider FOV without an external patch   |     ✔️      |          ✔️          | Verified four-byte in-memory clamp update; preserves preexisting uncap, restores owned changes and rejects unknown code.                                                                                       |
| Player movement speed                 |     ✔️      |          ⌛          | Shared player-only movement scale implemented; independent walk/run/sprint controls remain research.                                                                                                |
| Player action speed                   |     ⌛      |          ⌛          | Optional player animation speed multiplier; verify attacks, dodges and interactions independently of movement.                                                                                                 |
| Game speed and slow motion            |     ⌛      |          ⌛          | Adjustable simulation speed for practice with a quick reset; verify menus, audio and loading behavior.                                                                                                         |
| Camera distance and height            |     ✔️      |          ✔️          | Distance/look-at-height scales 0.5–2.0, original-value restoration, loading recovery and preset support. Shoulder offset remains unimplemented.                                                                |
| Camera and screenshot tools           |     ⌛      |          ⌛          | Hide HUD, toggle camera shake and add a verified free camera with adjustable speed and restoration.                                                                                                            |
| Gravity and jump controls             |     ⌛      |          ⌛          | Optional player gravity and jump-height settings for exploration; verify collision and restore normal values.                                                                                                  |
| Flight and noclip exploration         |     ✔️      |          ⌛          | Flight/noclip disabled and hidden pending further fixes.                                                                                                      |
| Chalice-aware location bookmarks      |     ✔️      |          ⌛          | Connection/character/altar/progress snapshot required; reconnect expires the bookmark.                                                                                                                       |
| Backup history and restore assistant  |     ✔️      |          ✔️          | List BB Tools ZIPs; verify paths/sizes/CRC, require game closed, retain prior folder and recovery ZIP before replacement.                                                                                      |
| Diagnostics export                    |     ✔️      |          ✔️          | App/runtime versions and redacted status/config summary, excluding save contents and personal filesystem paths.                                                                                                |
| Expanded runtime compatibility        |     ✔️      |          ⌛          | shadPS4 officially supported, tested and confirmed. Other runtime profiles remain unverified.                                                                                                                                     |

## Release automation

- ✔️ ZIP includes only `BBTools.exe` and `README.md`; packaging rejects unexpected files.
- ✔️ Main version bumps create an annotated tag and draft release after both CI builds pass.
- ✔️ First GitHub draft release run and manual asset review.


## v0.6.0 local test candidate

Current availability supersedes the photo/noclip implementation history below:
photo mode/free camera and flight/noclip are disabled and hidden. Their retained
code and automated checks do not establish live functionality.

| Feature | Implemented | Tested and confirmed | Notes |
| --- | :---: | :---: | --- |
| Top navigation and section tabs | ✔️ | ⌛ | Compact header; Combat/Travel/Camera/Settings section tabs. |
| Search inside selection menus | ✔️ | ⌛ | Processes, items, lamps, locations and presets; IDs/categories retained. |
| First-encounter boss revival | ✔️ | ✔️ | Tested and confirmed on Bloodborne PC. Optional met/intro reset; journal records mode and requested changes. Multi-phase/ending profiles retain separate test status. |
| Keep Blood Echoes on death | ✔️ | ⌛ | Native IsDeathPenaltySkip flag; no balance refill. Test respawn, bloodstains, loads and restoration. |
| Discord Rich Presence | ✔️ | ⌛ | Default on; Settings opt-out; desktop IPC, reconnect and explicit activity clear. |

| Feature | Implemented | Tested and confirmed | Notes |
| --- | :---: | :---: | --- |
| Standalone photo mode/free camera | ✔️ | ⌛ | Disabled and hidden; movement/HUD fixes need further work. |
| Configurable camera controls | ✔️ | ⌛ | WASD/EQ/arrows/ZC, Shift/Alt modifiers, reset/exit. Save/restart and custom bindings. |
| Photo FOV, roll and camera speeds | ✔️ | ⌛ | Separate 10–120° photo FOV; reset restores entry composition. |
| Photo HUD and hunter movement lock | ✔️ | ⌛ | Optional; restore original state on exit, death/loading and disconnect. World simulation remains running. |
| Flight/noclip | ✔️ | ⌛ | Disabled and hidden pending further work. |

Version 0.6.0 is approved for push. Unconfirmed features retain their individual test status.
Enemy details is available for live testing; unknown effects keep native IDs.

| Feature | Implemented | Tested and confirmed | Notes |
| --- | :---: | :---: | --- |
| Live target details and retention | ✔️ | ⌛ | Real lock-on selection, HP/stamina, distance, buildup/limits, effect IDs/states/timers and super armor. Keeps last readings across unload/disconnect. |
| Less crowded navigation | ✔️ | ⌛ | Top navigation replaces sidebar; section tabs replace crowded expanders; connections in Settings. |
| Rapid-poison cure mapping | ✔️ | ⌛ | Select native states 2/6; preserve legacy disease state 5. |


## Parry practice

| Feature | Priority | Implemented | Tested and confirmed | Notes |
| --- | --- | :---: | :---: | --- |
| Practice setup / restore previous combat setup | First | ✔️ | ✔️ | Tested and confirmed. Protection, infinite bullets/stamina, 0.1× damage; conflicting overrides off; rewards unchanged. |
| Keep targeted enemy alive / restore living target HP | First | ✔️ | ⌛ | Native cannot-die bit, preserve damage/reactions, transfer on new lock-on, disarm on context loss; no revival. |
| Verified firearm-parry success / visceral-opening indicator | High | ⌛ | ➖ | Requires a verified native event, distinct from ordinary stagger, backstab and limb break. |
| Automatic attempts/successes/streaks/visceral follow-ups | High | ⌛ | ➖ | Depends on reliable shot and parry attribution; no guessed counts. |
| Auto-parry | High | ⌛ | ➖ | Independent normal-play toggle, separate from practice setup; no automatic protection/resources/damage changes. Verify firearm eligibility, attack timing and native shot request first. |
| Adjustable firearm-parry window | High | ⌛ | ➖ | Trace the native firearm eligibility check; generic parry flags and thrust-counter effect 31 do not establish it. No guessed parameter slider. |

New target controls are manual per session, excluded from startup presets, and
remain unconfirmed until tested on Bloodborne PC and shadPS4.


## Photo and targeting fixes / quality controls

| Feature | Implemented | Tested and confirmed | Notes |
| --- | :---: | :---: | --- |
| Native camera task/controller resolution | ✔️ | ⌛ | Wrong owning object fixed. Actual Bloodborne PC task, controller and frame read-only validation passed; movement/exit/load tests pending. |
| Authoritative lock-on selection | ✔️ | ⌛ | Actual enemy identity, HP, effects and modules read successfully while the camera mirror was null. UI/protection writes still need live confirmation. |
| PNG capture and output folder | ✔️ | ⌛ | Capture key/default F12, countdown, physical displayed client resolution; original always preserved. |
| Photo filters and adjustments | ✔️ | ⌛ | Noir/Warm/Cool/Sepia, exposure, contrast, saturation, vignette, preview and separate edited PNGs. Defaults persist. |
| Larger internal render resolution / supersampling | ⌛ | ➖ | Separate renderer research; resizing a saved photo is not a substitute. |
| HDR photo export | ⌛ | ➖ | Current composed-client capture is SDR PNG. |


| Photo studio follow-up | Implemented | Tested and confirmed | Notes |
| --- | :---: | :---: | --- |
| Hunter movement lock repair | ✔️ | ⌛ | Flags alone still allowed movement. Added a captured-hunter per-frame movement hook shared with speed controls; preserve configured speed on exit. Live retest required. |
| Photo HUD restoration repair | ✔️ | ⌛ | Restore independently of camera errors, retry missing options, retain baseline across option copies, explicit Restore HUD recovery. Live retest required. |
| Ignore hits beneath God mode | ✔️ | ✔️ | Tested and confirmed on Bloodborne PC, including with God mode. Grabs and scripted events retain separate test coverage. |
| Dedicated, simpler Photo studio | ✔️ | ⌛ | Retained implementation; disabled and hidden in the current candidate. |


## 2026-10-21 stability investigation

| Feature | Implemented | Tested and confirmed | Notes |
| --- | :---: | :---: | --- |
| God mode + Ignore hits together | ✔️ | ✔️ | Tested and confirmed on Bloodborne PC after the polling update. Earlier lag/crash report does not establish a renderer crash cause. |
| Avoid redundant combat/stamina writes | ✔️ | ⌛ | Poll still validates/reapplies cleared flags and follows loading; stable flags produce no writes. Combined-toggle native regression checks pass; no claim of a crash fix. |


## v0.6.1 Discord branding and optional details

| Feature | Implemented | Tested and confirmed | Notes |
| --- | :---: | :---: | --- |
| Discord Bloodborne logo and GitHub button | ✔️ | ✔️ | Tested and confirmed on Bloodborne PC. Application 1196921008809066616 default logo with no image override, and BBTools button; member-list status uses details. Profile card title still follows the Discord application name. |
| Optional extra presence details | ✔️ | ✔️ | Tested and confirmed on Bloodborne PC. Off by default; individual area/level/total-deaths/character-name/NG selections. Read-only game data and bounded refresh; omit unavailable fields. |

Validation: 390 isolated checks in Debug and Release, 96 private 1.09 fingerprints and nine release-workflow checks. Live read-only character-name/area/data verification passed; Discord changes are tested and confirmed on Bloodborne PC.

| v0.6.1 release automation | Implemented | Tested and confirmed | Notes |
| --- | :---: | :---: | --- |
| Nexus Mods CI/CD | ✔️ | ⌛ | Official v3 uploader, stable GitHub release trigger, checksum/ownership checks, duplicate protection and read-only validation mode. 20 mocked release checks pass; live API validation and first upload are separate. |
