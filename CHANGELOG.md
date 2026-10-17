# Changelog

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
the maintainer's live-game confirmations separately.

### Known limits

- shadPS4 processes are listed, but a shadPS4 memory adapter is not included.
- Generated chalice travel, damage/Echoes multipliers, remote workshop menus,
  and Micolash/Gehrman/Moon Presence entrance presets are not included yet.
- Boss entrance travel does not revive bosses or modify quest/door flags.
- Restore bookmarked coordinates only in their loaded map; use lamp travel first
  when coming from elsewhere.
