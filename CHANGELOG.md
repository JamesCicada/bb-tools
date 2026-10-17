# Changelog

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
