# Built-in blood-gem catalog — v0.6.9 — NOT TESTED IN-GAME

The Windows gem editor can now be browsed and configured without game files,
a prior import, a network connection or an existing library. Creation still
requires a living loaded character on the supported Bloodborne 1.09 profile.
Prepared for v0.6.9. Gameplay testing and feedback are required before these
changes can be marked tested and confirmed.

## Normal mode

Open **Items → Blood gems**, choose a named recipe, an offered shape and a curse,
then inspect the preview. Connect to the game and click **Create blood gem**.
Use the game's workshop to equip it.

The first catalog deliberately offers four documented families:

| Family | Primary | Secondary | Offered shapes | Source |
| --- | --- | --- | --- | --- |
| Cursed Heavy Abyssal | Strength scaling +65 | Physical damage +15 | Radial | Headless Bloodletting Beast |
| Cursed Tempering | Physical damage +27.2% | None | Radial, Triangle | Merciless Watchers |
| Cursed Adept, blunt | Blunt damage +32.6% | None | Radial, Triangle | Merciless Watchers |
| Cursed Adept, thrust | Thrust damage +32.6% | None | Radial, Triangle | Merciless Watchers |

Six native curse properties are offered. Values and combinations are checked
against the selected recipe; individual valid IDs do not make a new combination
eligible for Normal mode. Unlisted families and rare shapes are outside this
initial subset, not declared impossible in the game.

These are documented obtainable builds, **not tested and confirmed in-game**.
Normal validation is not a claim of complete coverage of the game's drop system.

## Advanced mode

Explicitly enable **Advanced: custom / overpowered gems** to expose the source
template, all five shapes and six custom effect slots. Search built-in descriptions
or decimal effect IDs, select a slot and click **Set selected slot**. `-1` is empty.
At least one functional effect is required. Stronger combinations can be made
from existing native effects; arbitrary new effects/magnitudes are not supported.

Advanced is off on a fresh launch and is not enabled by gameplay presets,
importing recipes or loading carried gems. Native validation applies in both modes.
Returning to Normal uses a documented Normal recipe, not the custom combination.
Loading an unrecognized build while in Normal blocks creation until a Normal
recipe is chosen or Advanced is explicitly enabled.

## Saved recipes and optional descriptions

Name and save builds in the local SQLite library. Load them for preview, export
or import `.bbgem.json` files, or use the full library export/import. Neither
loading nor importing a recipe grants an item. A copy leaves the original gem intact.

Under Advanced, **Optional: import a message archive** accepts the actual
`menu.msgbnd.dcx` file from any available game-language directory, including
`msg/engus` or `msg/zhotw`. It changes Advanced search descriptions; it is not
required to use the catalog or Normal recipes.

## Catalog maintenance and evidence

`src/BBTools/Assets/GemGuide.json` is an embedded, versioned metadata resource:

- Each effect tuple is `[id, propertyKey, numericValue, rank, categoryId,
  specialEffectId, attackSpecialEffectId]`.
- The 12,487 entries contain numeric facts and property keys. The 37 property
  label templates are authored in the app's localization catalogs. No FMG text,
  parameter archives, game images or another editor's code/data are bundled.
- Unknown live effect IDs remain available as technical IDs in Advanced after
  refreshing. No guessed human-readable property is assigned.
- Normal recipes fix the source generator, primary, allowed secondary/curse
  selections and offered shapes. A generator SHA-256 and selected native effect
  metadata are rechecked immediately before publishing a Normal creation request.
- This targets stock 1.09 parameter values. Mods that change linked SpEffect
  values may change actual properties; source/effect metadata checks are not a
  complete integrity check of every loaded game parameter.

Independent numeric verification used the locally supplied CUSA03173 1.09
parameters: all catalog IDs/ranks/categories/native links and all recipe source
fingerprints matched. Regression tests cover offline startup, impossible
combinations, mode transitions, modified profiles, saving/sharing and the existing
native allocation/award/release path. None replaces workshop/save/reload testing.

Public factual references for recipe properties and sources:

- [Best Blood Gems Setups](https://www.bloodborne-wiki.com/2015/10/best-blood-gems-setups.html)
- [Individual Blood Gems](https://www.bloodborne-wiki.com/2015/10/individual-blood-gems.html)
- [How Blood Gems Are Determined](https://www.bloodborne-wiki.com/2017/04/how-are-blood-gems-determined.html)

### Gameplay test checklist

1. Fresh launch without importing files: inspect all four Normal recipes and curses.
2. Create one Heavy Abyssal; confirm the name, rating, +65 scaling, +15 physical
   and chosen curse in carried inventory and the workshop. Equip and save/reload.
3. Repeat for Tempering and both Adept families and every offered shape/curse.
4. In Advanced, create a custom combination; verify all six effects, empty slots,
   original-gem preservation, full-inventory handling and no automatic retry.
5. Save/export/import builds; confirm no grants or automatic Advanced activation.
6. Test loading, character switches, disconnects, and both supported Windows runtimes.
