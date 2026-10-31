> **Version 0.7.0:** Windows and Linux share gameplay features and release numbering. New Linux native actions are **NOT TESTED IN-GAME**. A community tester confirmed the previous Linux preview connected and worked with **bbport 0.7 / bbprobe**; individual feature coverage was not specified.
> Nightwatch, the gem-editor overhaul, NG editing, shoulder camera and experimental photo/flight/noclip keep their existing pending live-test status.
> Bloodborne PC and shadPS4 remain officially supported for game 1.09. Experimental photo/flight/noclip remain hidden until explicitly enabled in Settings.

# BB Tools

**A Bloodborne PC practice companion for Windows and native Linux.** Edit your hunter, set up a
boss practice run, spawn items, and travel without juggling several tools.
Inspired by [ERTool](https://github.com/kh0nsu/EldenRingTool).

[Download latest release](https://github.com/JamesCicada/bb-tools/releases/latest) ·
[Report a bug](https://github.com/JamesCicada/bb-tools/issues) ·
[Contribute](CONTRIBUTING.md) · [Roadmap and test checklist](TODO.md) · [Launcher/emulator compatibility](docs/COMPATIBILITY.md)

## v0.7.0: Linux desktop and shared gameplay

- Native Linux now exposes player/Insight/NG edits, combat and parry practice, multipliers, discovery/free progression, durability, items/gems, lamps and boss revival, camera/movement tuning, retained enemy details, presets, library/save tools and Discord presence.
- Connect to **bbprobe** or **bb-probe** after launching through **bbport**, rather than the `bloodborne-bbpo` launcher. The picker still shows all processes.
- Configurable hotkeys, opt-in photo/flight/noclip and lossless photo filters use X11/XWayland on Linux. Native Wayland game input/capture is not supported.
- Windows and Linux downloads use **0.7.0**, with both platforms checked before one draft release is prepared. Each archive contains its executable and README.md.
- **New Linux native features need live testing.** See the [Linux guide](docs/linux-support.md) and [release notes](docs/releases/v0.7.0.md). Adjustable firearm-parry timing/auto-parry remain research tasks; parry practice is available.

### Earlier releases

## v0.6.10: Nightwatch interoperability — not tested in-game

- Support the documented item/gem observer wrapper while preserving Nightwatch’s hook and validating the original continuation.
- Validate selected workshop-menu factories instead of rejecting unrelated changes.
- Report exact chalice instruction mismatches; Nightwatch’s flag-remapping replacement remains unsupported.
- **NOT TESTED IN-GAME.** This does not establish full Nightwatch/co-op compatibility.

## v0.6.9: easier blood-gem editing — not tested in-game

- Built-in catalog with 12,487 named effects; no game-file import required.
- Normal mode offers four documented obtainable gem families, including Cursed Heavy Abyssal. Optional Advanced mode exposes custom/OP combinations and raw IDs.
- Preview properties, save builds and share recipe files. More natural recipes will be added.
- **Not tested in-game.** Automated checks do not confirm granted names, ratings, actual properties, workshop behavior or save/reload. See the [gem guide and test checklist](docs/gem-catalog.md).

## v0.6.8: NG editing and optional experimental exploration

- Player → NG cycle: load the current value, edit 0–999 and apply. Difficulty scaling caps at NG+6; this edits the counter without restarting quests or beginning a new journey.
- Settings → Experimental: opt into photo mode and Camera → Movement flight/noclip. Modes remain off until started manually. Rebind all camera keys in the same Settings tab.
- Camera → Photo studio: free camera, hunter lock, optional HUD hiding, PNG capture and saved-photo filters. No external patches/addons required. Navigating away exits photo mode.
- These additions are **not tested in-game**. See the [experimental guide](docs/experimental-exploration.md) and report runtime/version plus session logs.

## v0.6.7: shoulder camera and blood gems

- Camera → Position & distance: horizontal shoulder offset, centre reset and preset support.
- Items → Blood gems: five shapes, six effects, name/ID search and edited copies of carried gems. Import effect descriptions from your own game files.
- Both additions show **Not tested in-game** beside their controls. Please test and report your runtime and session log. See the [test guide](docs/shoulder-camera-and-blood-gems.md).
- Separate Linux beta remains **not tested in-game** and does not include these Windows features.

## v0.6.6: free progression, Insight and Linux beta

- Player: view and set Insight from 0–99.
- Workshop: separate free level-up and free weapon-upgrade toggles remove Echo and material costs; save them in presets, including startup presets.
- Validate only the native instructions needed by an action; log exact compatibility mismatches and item parameters.
- Active Windows features are tested and confirmed. Hidden photo/flight/noclip features stay outside that confirmation.
- Separate [Linux beta](https://github.com/JamesCicada/bb-tools/releases/tag/v0.6.8-linux-beta.1): native x64/glibc desktop and a read-only probe. **Not tested with a live Linux game; fewer features than Windows.**

## v0.6.4: translations and discovery

Choose an interface language in Settings → Preferences. Simplified Chinese has a
draft for the extracted UI/message catalog; the other community languages still
need contributions and fall back to English. Native speakers can help on
[Crowdin](https://crowdin.com/project/bb-tools) or through
[the translation guide](https://github.com/JamesCicada/bb-tools/blob/main/docs/translating.md).
No language is marked native-speaker reviewed merely because its catalog is complete.

Items → Discovery & drops adds an independent discovery bonus and a separate
guaranteed **eligible enemy-roll** option, including the shared chalice-dungeon
loot path. Live testing and feedback are still needed. They do not edit
Arcane or other attributes, guarantee gem quality/effects, create unavailable loot
or change chest/scripted rewards.

## v0.6.3 patch

- Restore Camera → Movement toggles and sliders when a character is loaded, including after loading screens.
- Add a Linux tester invitation in Settings → About. Interested? DM **JamesCicada** on Discord.

The movement fix has UI/native regression coverage; live retesting remains pending.

## v0.6.2 patch

- Correct Ludwig's separate defeat flag and reset his encounter/phase state.
- Add timed multiplier cleanup and recovery of recognizable effects left by older BB Tools sessions. Set both multipliers to **1** and click **Apply multipliers** to recover.

**Feedback requested:** these changes have not been thoroughly tested in-game.
Automated checks pass, but both fixes need live feedback on Bloodborne PC and shadPS4.
Persistence of Echoes scaling across a full game restart has not been reproduced.
Please include your runtime/version, reproduction steps and session log in reports.

### Linux development

A native Linux x64/glibc desktop beta and a separate read-only probe are available separately for bbport (`deadinside28/bloodborne_pc`). The preview starts read-only
and includes experimental data editing; it is not yet the full Windows feature set.
See the [Linux preview guide](https://github.com/JamesCicada/bb-tools/blob/main/docs/linux-support.md)
and [source research](https://github.com/JamesCicada/bb-tools/blob/main/docs/linux-compatibility-research.md).
Linux game compatibility remains unconfirmed; synthetic CI does not replace live testing.

## v0.6.1 patch

Fix: replaced placeholder Discord presence details with actual game data. Optional
area, level, total deaths, character name and NG cycle sharing is off by default.
Uses the selected Discord application’s Bloodborne logo and a BBTools GitHub button.
Tested and confirmed on Bloodborne PC.

Nexus Mods uploads now follow publication of a stable GitHub release.
See [Nexus CI setup](https://github.com/JamesCicada/bb-tools/blob/main/docs/nexusmods-ci.md).

## What changed in v0.6.0

**Combat → Practice** provides a one-click drill setup: hunter protection,
infinite bullets/stamina and 0.1× outgoing damage, with conflicting one-shot,
AI-disable and stealth selections switched off. Equip a firearm and carry bullets
first. Restore the previous combat setup with its button when finished.

Lock onto a living enemy, then enable **Keep practice enemy alive**. Ordinary HP
damage still applies, but the native cannot-die flag prevents ordinary lethal HP
damage. Unlocking retains protection; targeting a different enemy transfers it.
Loading, hunter death or invalid identity disarms protection. **Restore locked
enemy HP** heals a living target; it does not revive bosses or reset encounters.
Scripted deaths/phase transitions can still occur. These target actions never
activate through startup presets. Live testing is pending on both runtimes.

Automatic parry success detection/statistics, auto-parry and adjustable gun-parry
windows remain research tasks; no timing assistance is enabled by this build.

Compact top navigation replaces the sidebar. Combat, Travel, Camera and Settings
use section tabs, and process/item/lamp/preset/location searches sit inside their
selection menus. Connection selection lives in Settings → Connection.

**Enemy details** tracks the actual lock-on target every 200 ms. Unlocking retains
that enemy and continues updating while its identity remains valid. Loading or
unloading retains a clearly labeled last-known snapshot; another targeted enemy
replaces it. Includes HP/stamina, status buildup/limits, native effect IDs/states
and remaining durations, distance, and super-armor health/recovery/break state.
Unknown effects keep native IDs. Super armor is separate from boss limb health;
brief breaks can be missed by sampling. These additions await in-game testing.


**Travel → Bosses → Revive as first encounter** resets the encounter's met flag
alongside its defeat/start flags. Leave it off for repeat encounters. The boss
still follows its normal first-visit trigger and intro; fog may appear only after
that trigger. Disable No AI and invisibility when checking boss behavior. Revival
journals record the original flags, chosen mode and requested changes. First-
encounter revival is tested and confirmed on Bloodborne PC. Multi-phase and ending
boss profiles retain their individual test status.

**Combat → Controls → Keep Blood Echoes on death** enables the game's native
death-penalty skip flag. It does not refill the balance or change spending. The
selection survives loading and is included in presets; disabling restores the
captured flag. Death/respawn and bloodstain recovery still need live confirmation.

**Settings → Preferences → Discord Rich Presence** is on by default and can be
turned off. With Discord desktop running and a connected game, it shows Bloodborne,
the runtime and elapsed time, with the Discord application’s Bloodborne logo and a **BBTools** GitHub button.
Extra game details are off by default. Enable **Share extra game details** to choose
area, Blood Level, total deaths, character name and NG cycle individually. These
readings are shared with people who can view your activity; unavailable fields are
omitted. Area labels describe the loaded map, not exact coordinates. Disconnect,
disabling and app close clear the activity; Discord restarts are retried. The
public application ID is `1196921008809066616`; no bot token is used.
The member-list status requests the custom details text. Discord still takes the
profile card application title from the Developer Portal application name; rename
that application or use a dedicated BB Tools application for a fully branded title.

Photo mode/free camera and flight/noclip are disabled and hidden while movement
and HUD restoration issues are investigated. Ordinary FOV, camera distance/height,
movement speed, gravity and HUD controls remain available on Camera. FOV/camera
overrides can cause visual and performance issues. Other new features retain their
individual live-test status; see [the test plan](docs/1.0-test-plan.md).

Combat now includes **Ignore hits** beneath God mode. It uses native event
invincibility to avoid normal attack hits/reactions, separately from health
protection. It is included in combat presets and restores its original flag on
disable. God mode and Ignore hits together are tested and confirmed on Bloodborne
PC after the polling update. Grabs and scripted damage need separate testing.

The app also includes its emblem, Windows icon, **Made with 🖤 By James Cicada**
footer, GitHub shortcut and manual **Check for updates**.

Update checks run only when requested and use this project's public GitHub latest
stable release endpoint. A newer version exposes **View release**; the app does
not download or install updates automatically. Connection failures and GitHub rate
limits are shown without reporting a successful check. Details remain visible in
**Settings → About**.

## Compatibility

| Component | Supported profile |
| --- | --- |
| Host | Windows x64 |
| Runtime | Native [Bloodborne PC](https://github.com/Supermedo/bloodborne_pc), usually `bb-probe.exe` |
| Game profile | CUSA03173, version 1.09 |
| The Old Hunters | Ten named DLC lamp destinations and all five DLC boss entrances |
| shadPS4 | Officially supported for CUSA03173 1.09; tested and confirmed |
| Other regions, builds or conflicting patches | Not verified; instruction mismatches are rejected |

The downloadable build includes the .NET runtime. You do not need Visual Studio,
the .NET SDK, or a separate .NET runtime installation. No game files are included.

## Download and start

1. Download the Windows x64 ZIP from the
   [latest GitHub release](https://github.com/JamesCicada/bb-tools/releases/latest).
2. Extract the ZIP into its own folder and run `BBTools.exe`.
3. Start Bloodborne PC normally and load a character.
4. Click **Refresh**, select the native game process, then **Connect**.
5. Optionally enable **Settings → Find the best game process and connect automatically**.

Keep a save backup before experimenting: **Settings → Back up saves** creates a
ZIP from the save folder you choose. Close the game before backing up. Health,
Echoes, attributes and awarded items can persist through the game's normal saves;
disconnecting does not undo those changes.

## Included features

### Player and combat

- View current/max health and stamina; set HP, heal, or keep health full while alive.
- Infinite consumables and Quicksilver Bullets.
- Infinite stamina, god mode, one-shot kills, invisibility to enemies and silent footsteps.
- Disable character AI updates and prevent local-player fall damage.
- Selected practice toggles stay selected through loading screens and warps and
  resume once the player validates. New connections start with toggles off.
- Set Blood Echoes or add 10,000; supported balance range is 0–999,999,999.
- Set current Insight from 0–99 in Player. The game may save the new balance.
- Workshop: separate free level-up and free weapon-upgrade toggles remove Blood Echo and upgrade-material costs. Enable before opening the relevant menu; reopen it after changing the setting. Normal stat/upgrade limits remain. Both toggles support saved/startup presets and restore runtime costs when disabled or disconnected. Tested and confirmed.
- Edit Vitality, Endurance, Strength, Skill, Bloodtinge and Arcane from 1–99.
  Blood Level follows the attribute-point change, within 4–544. Apply while idle,
  then quit to title and reload to refresh derived stats.

### Items

- Spawn common goods/weapons or use custom IDs in Goods, Weapon, Armor and
  Accessory categories. Goods quantities: 1–99; equipment: 1.
- Enter a decimal base ID with its category, or select **Wiki hexadecimal Item ID**
  and paste the full ID, such as `400003E8`. A `0x` prefix also selects hex mode.
- Open the linked [item-ID reference](https://www.bloodborne-wiki.com/2018/01/datamined-item-ids.html)
  or [weapon-ID reference](https://www.bloodborne-wiki.com/2018/01/datamined-weapon-model-item-ids.html)
  directly from the Items page. Custom IDs must exist in your installed game;
  the game controls item and storage limits.

### Travel and practice

- Search and travel to **45 named lamp destinations**, including all **10 DLC lamps**.
- Use **22 boss-entrance presets**, including **all five DLC bosses**: select a boss,
  click **Travel to area**, wait for loading, then click **Go to entrance**.
- Save up to **100 named locations** with position and heading. Bookmarks persist
  between app sessions. Restore within the same loaded map; travel to its lamp
  first when coming from another map.

Boss entrance travel does not revive defeated bosses, open doors, or alter quest
flags. Gehrman and Moon Presence share their arena gate; normal story conditions apply.
Keep-health-full does not prevent a lethal hit; scripted deaths and boss phase
transitions may behave differently from ordinary combat.

## Connection and troubleshooting

- **No supported image:** load a character and confirm the native process and
  CUSA03173 1.09 profile. A similar process name does not prove compatibility.
- **Several processes:** automatic connection ranks validated loaded characters
  first. You can also select a specific PID manually.
- **Automatic connection stopped after Disconnect:** manual disconnect pauses
  automatic retries. Reconnect manually or toggle the setting to resume them.
- **Saved location is in another map:** use lamp travel to load that area, then restore.
- **Item request:** check the ID/category, inventory and storage. Do not repeatedly
  submit a timed-out request without checking the game.
- **Trainer was force-closed:** restart the game before reconnecting if the item/travel
  dispatcher is still installed. Close/disconnect BB Tools normally when possible.

Settings live in `%LOCALAPPDATA%\BBTools\settings.json`; named locations live in
`%LOCALAPPDATA%\BBTools\library.db` alongside presets. Existing `locations.json` bookmarks migrate automatically, retaining the original file. Save-backup ZIPs go to the destination you select.

## Additional practice features

The features below have individual implementation and confirmation statuses in
[TODO.md](TODO.md). The latest published download may differ from the current source.

- **Combat → Damage and Echoes multipliers:** enter 0.1–100 for outgoing damage and enemy
  Echoes rewards, then Apply. Set both to 1 or Turn off practice toggles to restore.
  Loading screens retain the desired values. In the development build, effects expire
  after five seconds of game time without trainer updates. To remove leftover effects
  from an older BB Tools session, set both values to **1** and click **Apply multipliers**
  with a character loaded. Recovery only removes recognizable BB Tools clones;
  avoid other mods that use SpEffect 9999. If scaling persists after a full game process
  restart with BB Tools closed, report the runtime/version, enabled mods and session log;
  that case has not yet been reproduced.
- **Workshop → Workshop menus:** level up, fortify, repair, storage, blood
  gems and Caryll runes. Close other game menus first. The native game handles
  normal prices, materials and closing; finish in the game.
- **Travel → Chalice dungeon travel:** refresh the six altars, choose an
  existing completed dungeon and one of the nine named seal/layer/heart lamps.
  Start from **Hunter's Dream**, then select **Travel to chalice lamp**. Locked
  lamps are rejected. This uses the character's generated dungeon; it does not
  create a ritual or unlock a lamp. Coordinate bookmarks
  support fixed maps; generated-dungeon bookmarks are rejected to prevent
  relocation into a different dungeon instance.
- **Boss entrances:** adds Micolash, Gehrman and Moon Presence. These presets do
  not reset defeated bosses or advance their story flags.

The checklist separates implementation, automated checks, and maintainer live-game
confirmations. v0.1.0's implemented gameplay features are marked tested and confirmed there;
this is not a claim that every destination, item ID or runtime fork has been tested.

## Help build BB Tools

**Contributions are welcome.** You do not need to reverse-engineer the game to help:
clear bug reports, compatibility results, UI improvements, documentation and test
cases are valuable. If you do research a feature, include the runtime/game build,
layout evidence and a reproducible test.

- [Open an issue](https://github.com/JamesCicada/bb-tools/issues) for a bug or feature request.
- [Read the contribution guide](CONTRIBUTING.md), fork the repository and send a pull request.
- Check [TODO.md](TODO.md) before starting a larger feature so work can be coordinated.
- If the project helps you, a GitHub star helps other players find it.

Please keep proprietary game files, saves and private dumps out of issues and pull requests.

## Build and checks

For source builds, install a .NET SDK with .NET 8 Windows targeting support:

```powershell
dotnet build BBTools.sln -c Release -m:1
dotnet run --project tests/BBTools.Checks -c Release --no-build
dotnet run --project src/BBTools -c Release --no-build
```

The check runner starts its own synthetic Windows child process; it does not change
an active game. **68 checks** cover memory transport, player validation, flag
restoration/loading, ID parsing, destination catalogs, bookmark persistence, and
native dispatcher ordering/recursion/detach, and update-check responses/version ordering. **25 instruction signatures** were
checked separately against the private supported 1.09 image. New UI layouts were
rendered and inspected.

Optional read-only profile verification against your own prepared ELF:

```powershell
dotnet run --project tests/BBTools.Checks -c Release -- --verify-elf 'C:\path\to\out\eboot.elf'
```

To build the downloadable ZIP and checksum:

```powershell
.\scripts\release.ps1
```

The ZIP contains only `BBTools.exe` and `README.md` in its app folder. The
checksum is a separate release asset.

For the next release, update `<Version>` in `src/BBTools/BBTools.csproj` and add
`docs/releases/vVERSION.md` in the same commit. Pushing the version bump to `main`
runs both build configurations and checks, verifies the packaged checksum, creates
an annotated `vVERSION` tag, and uploads the ZIP and checksum to a **draft** release.
Review and publish the draft on GitHub after live testing. Ordinary commits do not
create releases. To retry an interrupted release, run **Windows desktop CI**
manually with **draft_release** enabled at the tagged commit, or re-run the failed
release job from its original CI run. A tag is never moved to a different commit;
published releases are left unchanged.

See [memory research](docs/memory-research.md) for implementation facts and limits.
No original game instructions are patched and no remote threads are created.
Item/travel actions use a private player-vtable dispatcher on the verified update
call site. Its original table is restored on normal disconnect when identity still
matches; dispatcher allocations remain valid until the game exits.

## Credits and license

- [ERTool](https://github.com/kh0nsu/EldenRingTool) for the practice-tool inspiration.
- [Bloodborne PC](https://github.com/Supermedo/bloodborne_pc) for the native runtime.
- [Bloodborne Wiki](https://www.bloodborne-wiki.com/2018/01/datamined-item-ids.html)
  for linked item references.
- [TatsunaKyo's runtime cheat research](https://github.com/TatsunaKyo/bloodborne_pc/blob/windows/docs/cheats.md),
  [Windows trainer / Shiningami field research](https://github.com/yaonikaixin999999/bloodborne_pc_windows/blob/codex/windows-port/trainer_windows.py),
  and [bbhost layout research](https://github.com/droogie/bbhost) for address/layout leads,
  independently checked against the local game data. Further provenance is in the research notes.

BB Tools is independently implemented and released under the [MIT license](LICENSE).
It is a community project and is not affiliated with FromSoftware or Sony.

### Saved library and practice presets (v0.3.0)

The current source adds separate Player, Combat, Enemy details, Items, Travel, Camera, Workshop and Settings
pages. Combat includes named presets for toggles, health refill, stamina and applied
multipliers. Save current settings, then select a preset and apply it explicitly while
connected. Saving under the same name replaces that entry; presets do not edit stats
or the current Echoes balance.

Presets and saved warp locations use `%LOCALAPPDATA%\BBTools\library.db` (SQLite).
Existing `locations.json` bookmarks migrate once in a transaction; the original file
is retained. Keep that JSON when resolving a migration error. Location search matches
names and map labels, and restoring still requires the same loaded map. Database
migration, location search, item favorites, profiles and library import/export are
implemented, tested and confirmed.

The Camera page provides a live vertical FOV slider with an explicit override toggle
and original-value reset, ranging from 38 to 120 degrees. Above the game's normal
48-degree cap, BB Tools applies a verified four-byte clamp change in process memory;
no separate patch or edited game file is required. It restores that change when the
override is disabled or the app disconnects, while preserving an uncap patch that
was already present. It restores each camera row's captured value, preserves edits
made by other tools, and reapplies the selection after loading.
FOV and saved presets are tested and confirmed. Additional aiming, lock-on and
load/disconnect regression checks remain tracked in the checklist.

The process picker lists all enumerable processes and marks likely Bloodborne
runtimes with ★, with bb-probe.exe first. Filter by name, decimal PID, or a
0x-prefixed hexadecimal PID, then refresh when processes start or exit. Entries
marked restricted have unavailable creation-time identity and cannot be connected
until that identity is accessible. Any manually selected process must still pass
the supported game-image validation. Auto-connect considers only likely runtimes.

### Session logs

Each app launch creates a UTF-8 log in `logs` beside `BBTools.exe`. Only the five
newest BB Tools session logs are retained. Logs include app/runtime information,
connection and action status, handled errors and unhandled exception details.
Repeated identical errors are throttled. Logging errors do not stop game controls;
an unwritable app folder is reported in the app status. Logs are created at runtime
and are not included in the release ZIP.

### v0.4.0 feature guide

- **Startup preferences:** save the current camera FOV as a startup default in Settings.
  Combat can bind a saved preset to startup. Settings apply only after a living character
  is ready, once per connection. Presets now include camera tuning, durability, buildup
  controls and app/hotkey preferences. Startup never edits attributes/Echoes balances,
  spawns items, revives bosses or warps.
- **Boss revival/retry:** choose a supported boss in Travel from any loaded area, including mid-encounter.
  One click resets encounter flags and requests an area reload; the changes can be saved by the game;
  use a separate practice save. Retry additionally moves outside the fog,
  refills health/stamina and starts the timer. Micolash, Gehrman and Moon Presence
  remain unavailable. Original flag journals are kept in
  `%LOCALAPPDATA%\BBTools\boss-revival`; these are diagnostic snapshots, not full save backups.
- **Durability/status:** durability repair controls remain experimental and require further work.
  Status buildup controls are tested and confirmed. Clearing buildup does not remove
  active poison or other active effects, and polling does not guarantee hit immunity.
- **Camera tuning:** scale distance and look-at height 0.5–2.0; disable to restore
  individual originals. FOV and camera overrides are experimental and unstable: performance drops and visual issues are possible. These settings are included in presets.
- **Hotkeys/timer:** configure Ctrl+Alt+F1–F12 actions in Settings. Actions require
  the game/trainer to have focus and are ignored in app text fields/dialogs. The
  encounter timer pauses while loading/dead; finish manually to record a session best.
- **Library:** groups, preset rename/duplicate, custom item favorites and search,
  versioned JSON import/export and consistent SQLite database backups. Imports
  replace matching names in a transaction without applying anything to the game.
- **Save recovery:** choose a BB Tools backup folder in Settings. Restore requires
  the game/launcher closed, validates ZIP paths/sizes/CRC and retains the previous
  save folder plus a recovery ZIP. Test on a disposable folder copy first.
- **Diagnostics:** export version/config/status information with filesystem paths
  redacted. Saves and full session logs are not included in this export.

The [checklist](TODO.md) records tested and confirmed features, the outstanding
weapon durability issue and unimplemented native research. Do not assume the entire
future roadmap is included in the development build.
