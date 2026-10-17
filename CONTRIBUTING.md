# Contributing to BB Tools

Thanks for helping improve Bloodborne practice tools. Bug reports, compatibility
tests, clearer documentation, UI fixes and code contributions are all welcome.

## Report a problem

[Open an issue](https://github.com/JamesCicada/bb-tools/issues) with:

- BB Tools version, Windows version, native runtime/fork/build and game region/version.
- The feature, exact steps, expected result, actual result and status message.
- For item grants: item ID, decimal/hex mode, category and quantity.
- For travel: destination or saved map, and whether the issue followed a load/death.
- Whether the result changes with other trainers or relevant mods disabled.

Attach screenshots or a small, redacted log excerpt if useful. Do not upload game
binaries, game assets, saves, private memory dumps or personal credentials.

## Make a change

1. Check [the roadmap](TODO.md) and existing issues. Discuss large features first.
2. Fork the repository and create a focused branch.
3. Build on Windows with a .NET SDK supporting .NET 8 Windows targeting.
4. Make the change, update relevant docs, and add meaningful checks when behavior
   or memory handling changes.
5. Run the build and check runner, then open a pull request describing the problem,
   resulting behavior, validation and any remaining live-test limits.

```powershell
dotnet build BBTools.sln -c Release -m:1
dotnet run --project tests/BBTools.Checks -c Release --no-build
```

The checks use a synthetic process. They must not silently attach to or modify a
running game. Runtime layout changes need independently verified instruction/field
evidence, pointer/identity validation, and lifecycle/cleanup handling.

## Research and validation

- State the exact runtime, game region and game version used for new address facts.
- Cite research leads and distinguish confirmed layout facts from assumptions.
- Preserve original values for transient flags and reject invalid/stale pointers.
- Keep game assets, proprietary code and research dumps out of the repository.
- Contribute your own implementation compatible with this project's MIT license.
- Keep implementation completion separate from live-game confirmation in `TODO.md`.
  Mark user-tested only when someone has actually reported that feature working.

Current release support is native Bloodborne PC, CUSA03173 1.09. A shadPS4 adapter,
reward/damage multipliers and remote workshop menus are useful future contributions.
