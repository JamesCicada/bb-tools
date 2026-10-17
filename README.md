# BB Tools

**A Bloodborne PC practice companion for Windows.** Edit your hunter, set up a
boss practice run, spawn items, and travel without juggling several tools.
Inspired by [ERTool](https://github.com/kh0nsu/EldenRingTool).

[Download latest release](https://github.com/JamesCicada/bb-tools/releases/latest) ·
[Report a bug](https://github.com/JamesCicada/bb-tools/issues) ·
[Contribute](CONTRIBUTING.md) · [Roadmap and test checklist](TODO.md)

## v0.2.0 draft

The local **v0.2.0 test build** adds infinite consumables/bullets, player damage
and enemy Echoes reward multipliers, remote workshop menus, generated chalice altar
travel, and the remaining three boss entrance presets. James confirmed the
multipliers and workshop menus working. Chalice travel and the three additional
boss entrances still await live confirmation; see [the checklist](TODO.md).
The download link points to the latest published release; this draft stays private
until reviewed and published.

The app also includes its emblem, Windows icon, **Made with 🖤 By James Cicada**
footer, GitHub shortcut and manual **Check for updates**.

Update checks run only when requested and use this project's public GitHub latest
stable release endpoint. A newer version exposes **View release**; the app does
not download or install updates automatically. Connection failures and GitHub rate
limits are shown without reporting a successful check. Details remain visible in
**Settings → About & updates**.

## Compatibility

| Component | Supported profile |
| --- | --- |
| Host | Windows x64 |
| Runtime | Native [Bloodborne PC](https://github.com/Supermedo/bloodborne_pc), usually `bb-probe.exe` |
| Game profile | CUSA03173, version 1.09 |
| The Old Hunters | Ten named DLC lamp destinations and all five DLC boss entrances |
| shadPS4 | Process discovery only; the live adapter is **not implemented** |
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
`%LOCALAPPDATA%\BBTools\locations.json`. Save-backup ZIPs go to the destination you select.

## What is next?

The v0.2.0 additions below have individual test statuses in the checklist and are **not in
the public v0.1.0 download**. Results are tracked in [TODO.md](TODO.md).

- **Practice → Player multipliers:** enter 0.1–100 for outgoing damage and enemy
  Echoes rewards, then Apply. Set both to 1 or Turn off practice toggles to restore.
  Loading screens retain the desired values. Spending and bloodstain retrieval
  are not treated as rewards. Avoid other mods that use SpEffect 9999.
- **Items & travel → Workshop menus:** level up, fortify, repair, storage, blood
  gems and Caryll runes. Close other game menus first. The native game handles
  normal prices, materials and closing; finish in the game.
- **Items & travel → Chalice dungeon travel:** refresh the six altars, choose an
  existing completed dungeon and one of the nine named seal/layer/heart lamps.
  Start from **Hunter's Dream**, then select **Travel to chalice lamp**. Locked
  lamps are rejected. This uses the character's generated dungeon; it does not
  create a ritual or unlock a lamp. Coordinate bookmarks
  support fixed maps; generated-dungeon bookmarks are rejected to prevent
  relocation into a different dungeon instance.
- **Boss entrances:** adds Micolash, Gehrman and Moon Presence. These presets do
  not reset defeated bosses or advance their story flags.

The checklist separates implementation, automated checks, and maintainer live-game
confirmations. v0.1.0's implemented gameplay features are marked user tested there;
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
