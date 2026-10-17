# BB Tools

Windows Bloodborne practice companion inspired by ERTool. The first live adapter targets **Bloodborne PC (`bb-probe.exe`), CUSA03173 1.09**. Written independently; no upstream implementation code is included.

## Features

- Discover every similar shadPS4, Bloodborne, bbport and bb-probe process, including renamed build suffixes; select by PID.
- Explicit connect/disconnect with a relocated game-image lookup and instruction checks. Connecting enables no practice toggles.
- Optional **Settings → Find the best game process and connect automatically**. The saved setting starts off. Every three seconds it checks supported native-engine candidates, prefers an active validated character, and reconnects after engine restart. Manual disconnect pauses it until a manual connection or setting change.
- Live current/max health and stamina displays.
- Set health, restore full health, and keep health full while alive (200 ms polling; not protection from one-hit or scripted deaths).
- Infinite stamina using the local player's no-consumption flag; preserve unrelated flags and restore the previous bit on disable/normal disconnect while the component remains valid.
- God mode (local-player no-damage + no-dead flags), one-shot kills (Player Exterminate), stealth and silent footsteps. Previous flag values are restored when disabled/disconnected.
- Item spawning: 19 common goods/weapons plus custom base IDs in Goods, Weapon, Armor and Accessory categories. Goods quantities 1–99, equipment 1. Inventory/storage limits remain the game's responsibility.
- Warp to 18 selected lamps, including Hunter's Dream and major practice locations, through the return-point/stage-change functions. Warping turns practice toggles off first; it does not unlock lamps, bosses or quests.
- Set Blood Echoes or add 10,000, within 0–999,999,999.
- Edit Vitality, Endurance, Strength, Skill, Bloodtinge and Arcane within 1–99. Blood Level changes by the same attribute-point delta, within 4–544. Reload the character after applying to refresh derived stats.
- Save-folder ZIP backups, with external destination validation and rejection of links/junctions. Close the game first.
- Dark desktop UI with Player, Practice, Items & Travel, and Settings pages.

## Run

Windows x64 with .NET 8 SDK (or a newer compatible SDK):

```powershell
dotnet build BBTools.sln -m:1
dotnet run --project src/BBTools
```

Start Bloodborne PC normally, load a character, refresh processes, select `bb-probe.exe` and connect. Health/stamina toggles start off. Attribute drafts are retained during normal polling; **Reload current** refreshes them. Apply attributes while idle, then quit to title and reload the character. The user reports that the original resource and attribute controls work on this installation. The newly added combat flags, native item grants and warps still need live-game validation.

Game writes can persist through autosaves. Disconnecting does not undo health, echoes or attributes already changed. Back up saves before experimenting. Closing the trainer normally attempts to restore the stamina flag; if the character component has unloaded, it cannot safely restore the old component. Restarting the game clears transient runtime flags. An interrupted trainer process cannot perform cleanup.

Similar process names alone do not establish compatibility. shadPS4 candidates are listed, but the shadPS4 live adapter is not implemented yet. Unknown or modified instruction layouts are rejected. No original game instructions are patched and no remote threads are created. Item/warp requests lazily install a private copy of the current player's vtable, intercept its data getter only at the verified player-update call site, and run the requested functions there. The original table is restored on disconnect when the actor still matches. Dispatcher pages remain allocated until the engine exits to avoid freeing code a game thread could still be returning through. Each dispatcher uses a small data workspace and code page. Game/save files are not edited by the player controls.

## Checks

```powershell
dotnet run --project tests/BBTools.Checks
```

The dependency-free check runner launches its own synthetic Windows child process and exercises the real memory transport and player adapter: discovery, reads/writes, ranges, flag restoration, stale drafts, fingerprint mismatches, wrong/null pointers and process exit. It also executes the generated dispatcher against real synthetic SysV functions, checks call-site filtering, three-call ordering, recursive getter behavior and vtable restoration, and verifies candidate ranking and setting persistence. It contains no game data.

Optional read-only signature check against your privately prepared ELF:

```powershell
dotnet run --project tests/BBTools.Checks -- --verify-elf 'C:\path\to\out\eboot.elf'
```

Verified during development: build succeeds; 36 checks pass; 18 instruction signatures match the supplied CUSA03173 1.09 prepared image. New pages rendered and inspected. New gameplay features have not yet been tested in a running game. [Memory research and limitations](docs/memory-research.md).

## Next

Live-game validation of the new controls, derived-stat recalculation, shadPS4 adapter, position checkpoints, boss reset, a larger item/lamp catalog, hotkeys and named practice profiles.

## References

- [ERTool](https://github.com/kh0nsu/EldenRingTool): practice workflow reference.
- [Bloodborne PC](https://github.com/Supermedo/bloodborne_pc): native runtime target and relocation model.
- [TatsunaKyo runtime cheat documentation](https://github.com/TatsunaKyo/bloodborne_pc/blob/windows/docs/cheats.md): local-player identity and stamina semantics.
- [Windows trainer research](https://github.com/yaonikaixin999999/bloodborne_pc_windows/blob/codex/windows-port/trainer_windows.py): Shiningami community field/hook research.

Auto-connect preferences are saved to `%LOCALAPPDATA%\BBTools\settings.json`. Practice toggles are never automatically enabled.
