# Changelog

## 0.6.5 — 2026-10-25

- Complete Arabic and French extracted interface/message catalog drafts, joining Simplified Chinese at 100% coverage. All three include the new permission guidance; native-speaker review and in-game usability checks remain pending.
- Preserve placeholders, format strings, numeric ranges, internal IDs and brand names. Add embedded-catalog checks for both languages; Arabic retains RTL layout and LTR numeric input.
- Correct mixed-script language-picker percentage ordering and shorten the French Voyage tab label to fit the minimum window size.
- Upload Arabic and French to the connected Crowdin source file as unapproved drafts. Crowdin reports 100% translated and 0% approved for both; existing duplicate translations are retained.

- Fix multiplier insertion rejecting the native-created neutral node because its zero-second duration immediately sets its expiration flag. Validate the exact returned node index before attaching the leased row; preserve older expired/foreign nodes and report distinct failure stages.
- Fix valid players being rejected during ordinary negative-stamina overdraft. Show an empty gauge and keep controls available; retry bounded read-only snapshots and verify ownership again before returning.
- Preserve Windows access error codes and suggest restarting as administrator only after confirmed access denial. Report permission failures in automatic connection without blocking other accessible supported processes.

Tested and confirmed on Bloodborne PC: multiplier and player-detection fixes. Read-only diagnosis found an unchanged actor during the reported failures and stamina down to −9. Further loading/preset/reconnect coverage and shadPS4-specific retesting remain welcome. Arabic and French Preferences/Discovery layouts were inspected at 860 × 680; native-speaker review and live permission-error testing retain their separate status.

Validation: 481 regression checks pass in both Debug and Release, plus 26 script checks and 103 supported-image fingerprints. Arabic, French and Simplified Chinese each contain all 544 current catalog entries.

## 0.6.4 — 2026-10-24

- Add an embedded JSON localization system with live interface-language selection, system-language detection, English fallback, preserved numeric-input culture and RTL layout support. Match the 28-language Crowdin list. Simplified Chinese has a complete extracted UI/message draft; other targets have starter labels and need community contributions. Game catalog names and technical diagnostics may remain English; no native-speaker review is claimed.
- Extract XAML text and UI messages into translation catalogs. Add file-based Crowdin configuration, [community translation links](https://crowdin.com/project/bb-tools) in Preferences/About, translator instructions and catalog/placeholder validation in CI. Crowdin synchronization still needs integration setup and a verified translation pull request.
- Add Items → Discovery & drops: a 0–1000 point discovery bonus without editing Arcane or other attributes, and a separate 100% eligible enemy-roll option. Includes the shared chalice-dungeon enemy loot path, removes blank outcomes only when valid weighted loot exists, and preserves relative item weights and quantities. Does not force gem quality/effects, grant every item or modify chest/scripted reward rolls. Both toggles support presets, loading pause and owned-code restoration; live testing, especially in chalice dungeons, is pending.

Validation: 467 isolated checks pass in Debug and Release, plus 26 script checks. Chinese and Arabic layouts were inspected at the minimum window size. These checks do not replace live discovery/chalice testing or native-speaker translation review.

## 0.6.3 — 2026-10-24

- Fix [#1](https://github.com/JamesCicada/bb-tools/issues/1): Camera → Movement remained disabled after connecting, preventing speed/gravity toggles and sliders from being used. Enable the panel for a validated loaded player, disable it during loading/disconnect and restore access after loading. Add UI/native regression coverage; live retest pending. Photo mode and flight/noclip remain hidden.
- Add a Linux tester invitation in Settings → About: “We need Linux testers. If interested, DM JamesCicada on Discord.”

Validation: 410 automated checks in Debug and Release and 22 release-workflow checks pass. The About layout was inspected at the minimum window size. Live movement retest remains pending.

## 0.6.2 — 2026-10-22

**Feedback requested:** these fixes have not been thoroughly tested in-game. Automated validation passes (404 checks in Debug and Release, plus 96 supported-game instruction fingerprints), but Ludwig revival and multiplier cleanup/recovery still need user feedback on Bloodborne PC and shadPS4. Please report the runtime/version, reproduction steps and session log for any remaining issues.

- Fix Ludwig revival's missing defeat flag (`9471`), which independently disables his boss forms and fog. Reset his entry/phase flags for both first-encounter and repeat modes. Live retest pending.
- Harden damage/Echoes multiplier cleanup: use a five-second renewable game-time effect instead of an infinite effect, and reject expired nodes during ownership checks. Effects stop after polling ends, including an interrupted trainer session.
- Applying multipliers removes recognizable leftover BB Tools effects before adding one replacement; applying both at `1×` also recovers legacy infinite effects. Preserve unrelated effects and reject foreign collisions. Add recovery logging and regression checks. The reported persistence across a full game restart is not yet reproduced; live validation remains pending.

## 0.6.1 — 2026-10-21

- Integrate the official Nexus Mods v3 upload API with CI/CD: publish the verified ZIP and changelog after a stable GitHub release is published, with validation-only runs and duplicate protection.

- Fix: replaced placeholder Discord presence details with actual game data.

- Use Discord application 1196921008809066616 and its default Bloodborne logo, with a BBTools button linking to GitHub; request custom details in the member-list status. The application title remains controlled by Discord application settings.
- Add opt-in area, level, total deaths, character name and NG cycle sharing, with individual field selections. Extra data is off by default, read without game writes, sampled at bounded intervals and omitted when unavailable.
- Coalesce frequent presence updates and clear extra details when sharing is disabled. Tested and confirmed on Bloodborne PC.

## 0.6.0 — 2026-10-21

Tested and confirmed on Bloodborne PC: God mode + Ignore hits together, and first-encounter boss revival. Other additions retain their individual live-test status.
Validation: 377 isolated checks in Debug and Release, 94 private 1.09 instruction fingerprints and nine release-workflow checks.

- Replace the sidebar with compact top navigation and section tabs for Combat, Travel, Camera and Settings. Move search inside process, item, lamp, location and preset menus.
- Add optional first-encounter boss revival: reset the met/intro flag and record mode, original flags and requested changes in the revival journal. Normal first-visit triggers still apply. Tested and confirmed on Bloodborne PC.
- Add Keep Blood Echoes on death through the native death-penalty skip flag, with loading persistence, presets and original-state restoration. No balance refund loop.
- Add optional Discord Rich Presence, on by default, using the supplied application ID. Show only game/runtime and elapsed time; clear on disable/disconnect/close and retry Discord restarts.
- Add Ignore hits beneath God mode using native event invincibility, with presets and original-state restoration. God mode + Ignore hits together are tested and confirmed after the polling update; no general renderer-crash fix is claimed.
- Avoid rewriting unchanged combat/stamina flags on each poll; continue validation and reapply if the game clears a flag. Combined-toggle checks cover independent restoration and unrelated-bit preservation.
- Add live target details with retained readings after unlocking and clearly marked stale snapshots after unloading. Show resources, status buildup, native effect IDs/durations, distance and super-armor state.
- Correct target selection when the camera mirror is empty by reading the authoritative lock-on selection. Improve pointer diagnostics and logging.
- Add parry practice setup/restore and manual target protection/HP restore. Practice setup is tested and confirmed; new target actions await testing. Auto-parry and adjustable firearm-parry windows remain research tasks.
- Disable and hide photo mode/free camera and flight/noclip while movement/HUD issues remain unresolved. Retain implementation and isolated regression coverage for future work.
- Correct rapid-poison cure mapping to native states 2/6, preserving legacy disease state 5; live retest pending.
- Update the app icon artwork.

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
