# Changelog

## Unreleased

- Fix v0.5.0 draft-release CI by supplying its version-specific release notes and checking required notes before artifact or GitHub operations.

- Cache item search data between library changes, avoiding SQLite reads and catalog deserialization for each keystroke while preserving names, aliases and category-specific IDs.
- Move auto-connect process enumeration off the UI thread and avoid per-region candidate-array allocations during emulator image scanning.
- Automatically remove publish staging folders after success or failure; add a previewable workspace cleanup script that retains release archives and research sources.
- Refresh contributor guidance to reflect supported runtimes.


## 0.5.0 - 2026-10-18 (draft)

- Officially support shadPS4 for CUSA03173 1.09: tested and confirmed. Remove the experimental opt-in from manual and automatic connection; retain image/player validation.
- Replace weapon repair with game-thread inventory repair and per-slot before/after reports. Both-hand live retest pending.
- Add player movement speed, world gravity, flight and HUD visibility. Noclip and detached free camera are disabled and hidden for now.
- Add active slow/rapid poison cure. Hide entity inspection pending realtime targeted-enemy updates.
- Add experimental Micolash, Gehrman and Moon Presence revival profiles.
- Import searchable item names from game-owned files into the local library.
- Add connection-bound chalice bookmarks and advanced movement/gravity/HUD presets.
- Rewrite Nexus descriptions and add compatibility results and a live test plan.
- Validation: 247 isolated checks pass in Debug and Release; six release-automation checks pass. shadPS4 support is tested and confirmed; other new features retain their individual live-test status.


## 0.4.0 - 2026-10-18 (draft)

- Add one-click revival and area reload for 19 base-game/DLC bosses from any loaded area, including mid-encounter. Retry also places the player outside the fog, refills health/stamina and starts the encounter timer. Original flags are journaled locally; Micolash and the two ending bosses remain unsupported for revival.
- Expand presets to cover FOV, camera distance/height, combat, buildup controls and app/hotkey preferences. Add startup preset and independent startup FOV options, plus preset groups, rename and duplicate.
- Add configurable function-key hotkeys with selectable modifiers and a visible shortcut summary. Use physical-key polling and retain game/trainer focus and text-entry guards.
- Add camera distance/height overrides and a visible warning that FOV/camera overrides are unstable and may cause performance and visual issues.
- Add status buildup clearing/keep-empty controls and a loading/death-aware encounter timer.
- Add item favorites/search, library import/export and SQLite backups, save backup history and verified restore with retained recovery copies.
- Add per-session logs beside the EXE, retaining the five newest, and diagnostics export with filesystem-path redaction.
- Tested and confirmed: the implemented additions above, including boss revival, startup presets and hotkeys.
- Known issue: weapon repair, threshold auto-repair and keep-full durability controls are experimental and not working reliably. Further work is deferred to the next development session; the native Workshop repair menu is separate.
- Validation: 209 automated checks in Debug and Release, 60 installed 1.09 instruction signatures, and self-contained startup/shutdown. The ZIP contains only BBTools.exe and README.md.

## 0.3.0 - 2026-10-17 (draft)

- Show all enumerable processes with likely Bloodborne runtimes ranked first; add name, decimal PID and hexadecimal PID filtering. Preserve restricted entries in the list while requiring a verified process identity to connect. Auto-connect still scans only likely game candidates.

- Split navigation into Player, Combat, Items, Travel, Camera, Workshop and Settings; reset scroll on navigation.
- Add a local SQLite library for saved practice presets and warp bookmarks, with schema versioning and transactional migration from locations.json. The original JSON is retained.
- Save, replace, apply and delete named combat presets. Presets include toggles, health refill, stamina and applied damage/Echoes multipliers; applying requires a game connection.
- Search saved locations by name or map.
- Extend the live vertical FOV slider to 38–120° without an external patch. A verified, reversible four-byte runtime clamp update enables wide angles; reset restores owned changes and preserves preexisting uncap patches. Camera defaults restore individually and the override resumes after loading.
- Prioritize the roadmap and use neutral tested-and-confirmed wording throughout the documentation.
- FOV, saved practice presets and process selection/filtering are tested and confirmed. Database migration, location search, navigation and remaining edge cases retain separate pending checks.
- Validation: 128 automated app checks, six release-automation checks, installed 1.09 instruction validation and self-contained startup/shutdown.

## 0.2.0 - 2026-10-17 (draft)

- Infinite consumables and Quicksilver Bullets, retained through loading screens.
- Player-only outgoing damage and enemy Echoes reward multipliers (0.1–100x),
  with owned-effect cleanup and automatic reattachment after loads.
- Native level-up, fortify, repair, storage, blood gem and Caryll rune menu shortcuts.
- Chalice altar selection using the current character's six existing ritual banks;
  select any of nine named seal/layer/heart lamps from Hunter's Dream. Locked
  lamps and mismatched generated-dungeon contexts are rejected.
- Micolash, Gehrman and Moon Presence entrance presets (22 total). The final two
  share their arena gate and still require normal story conditions.
- Two-file release ZIP: BBTools.exe and README.md. Checksum remains a separate asset.
- CI creates an annotated tag and a draft release on main-branch version bumps;
  no automatic public publishing. Retry support preserves published releases.

The multipliers and remote workshop menus are tested and confirmed. Chalice travel
and the three additional boss entrances remain pending live confirmation in TODO.md.
Validation: 87 app checks in Debug/Release, six release-automation checks, private
1.09 instruction checks, and self-contained package startup/shutdown.

- Preserve emoji status indicators (✔️ / ⌛) in the release checklist.
- Add a future-feature roadmap covering hotkeys, presets, boss retries, encounter
  timing, enemy inspection, item search, weapon setups, camera tools and diagnostics.

## 0.1.1 - Development build

- Use the supplied PNG emblem in the sidebar and window icon, with a multi-size
  Windows icon embedded in the executable.
- Add the footer credit: Made with 🖤 By James Cicada.
- Add a GitHub shortcut and manual update checks against the latest stable release.
  Newer releases open on GitHub; no automatic downloads or installation occur.
- Display the app version from assembly metadata rather than duplicated UI strings.
- Add update-service regression coverage for version ordering, trusted release URLs,
  draft/prerelease rejection, cancellation and API failures.

## 0.1.0 - 2026-10-17

First public release for Windows x64 and native Bloodborne PC
(`bb-probe.exe`), CUSA03173 game version 1.09.

- Manual process selection and optional automatic connection to the best
  validated native game process.
- Health, infinite stamina, Blood Echoes and six character attribute controls.
- God mode, one-shot kills, enemy invisibility, silent footsteps, No AI and
  no fall damage. Selected toggles resume after loading screens and warps.
- Item spawning with native item-handle creation, common-item presets,
  decimal/base or full hexadecimal IDs, and linked item-ID references.
- 45 named lamp destinations, including all ten Old Hunters lamps, with search.
- 19 boss-entrance presets, including all five DLC bosses.
- Persistent named location bookmarks with same-map coordinate restoration.
- Save-folder ZIP backups and a desktop UI with four pages.
- Release package includes the .NET runtime; no SDK or separate runtime install
  is required for the downloadable Windows build.

Validation: 57 automated synthetic-process checks and 25 instruction signatures
checked against the private supported 1.09 image. The release checklist records
tested-and-confirmed results separately.

### Known limits

- shadPS4 processes are listed, but a shadPS4 memory adapter is not included.
- Generated chalice travel, damage/Echoes multipliers, remote workshop menus,
  and Micolash/Gehrman/Moon Presence entrance presets are not included yet.
- Boss entrance travel does not revive bosses or modify quest/door flags.
- Restore bookmarked coordinates only in their loaded map; use lamp travel first
  when coming from elsewhere.
