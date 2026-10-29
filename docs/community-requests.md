# Community requests — reviewed 2026-10-29

## Next-update review

Read the live [Nexus comments](https://www.nexusmods.com/bloodborne/mods/614?tab=posts)
in the browser on 2026-10-29. Search-engine copies were older; the live first
page contained the new 27–29 October requests. No comments were posted or edited.

| Priority | Remaining work | Feedback / current status |
| --- | --- | --- |
| P1 | Built-in gem catalog, named recipes and clearer editor | SilenceHeals (27 October), Chude and Itsmeyoboy (29 October). Gem creation exists; identifying and configuring gems is confusing. |
| P1 | Incoming damage multiplier | LazySlacker (29 October). Separate from the existing outgoing-damage multiplier and God mode. |
| P2 | Global slow motion | Itsmeyoboy (29 October), renewing the earlier request. Player movement speed is not world simulation speed. |
| P2 | Smooth freecam with FOV and configurable movement | Itsmeyoboy (29 October). Experimental freecam exists in 0.6.8; stability, live testing and motion polish remain. |
| P2 | Infinite temporary weapon buffs | paaulao (27 October). Already added to TODO; expire on weapon swap. |
| Later | Cinematic camera timeline, NPC allies, enemy-count changes, further parry assistance | Retain earlier requests below; larger engine/state investigations. |

NG-cycle editing and horizontal shoulder offset are already implemented in
0.6.8, with live testing still pending. Discovery and the completed UI-language
catalogs are existing features. Linux remains a separate unconfirmed beta.
New positive connection reports do not establish every launcher/build as supported.

## Gem editor: default Normal mode, optional Advanced mode

The initial gem overhaul is implemented for v0.6.9 and **NOT TESTED IN-GAME**; see the
[catalog guide and test checklist](gem-catalog.md). The remaining priorities are
still backlog items. Four documented recipe families and 12,487 named effects
ship in the development build. More natural families remain future work.

### Remove the import prerequisite

Ship a versioned JSON catalog as an embedded resource so browsing and preparing
a recipe work offline. It contains numeric effect IDs, authored property labels,
categories, values/units, aliases and named recipe definitions. Importing local
game text becomes optional. It must not be the source of the default catalog.
An active supported game and loaded character remain necessary to create a gem;
keep the existing native table, fingerprint, ownership and inventory readback checks.

Use two distinct data layers:

- **Effects:** GemeffectParam ID, property kind, numeric value/unit, conditions,
  beneficial/curse classification, supported game-profile revision and provenance.
- **Recipes/drop rules:** source template, allowed shapes, rank/quality, compatible
  primary/secondary/curse pools and evidence for those combinations. Give each
  named preset an explicit effect selection, not just a template or display name.

Do not bundle extracted FMG archives/text or game parameter files. Author labels
and mapping metadata from verified numeric facts; record sources and review any
third-party catalog's redistribution terms before using it. The
[Noxde save editor](https://github.com/Noxde/Bloodborne-save-editor) demonstrates
an existing standalone numeric catalog and effect-selection interface, but its
GPL-3.0 code/data must not simply be copied into this MIT project. It is a research
reference, not an adopted dependency.

### Normal mode: obtainable gems

Normal is the initial mode. Let users search a named gem family/recipe, choose
an eligible shape, then select a compatible secondary effect and curse. Show a
readable preview, including values, conditions, rank and source. Hide raw IDs
and arbitrary six-slot editing from this flow.

Validate the complete selected combination against a documented drop recipe/pool.
Checking that each effect ID exists, or that no single value exceeds a natural
maximum, is insufficient: individually legitimate effects can make an impossible
combination. Preserve requirements for cursed/abyssal variants, their quality,
secondary effects and source-specific shapes. Include rare out-of-shape drops
only where supported by evidence. Unknown combinations need Advanced mode;
never label them obtainable based on a heuristic.

Start with a small audited set rather than claiming complete coverage. Prioritize
the requested **Cursed Heavy Abyssal** family, then Tempering, Adept, elemental,
Nourishing and Bloodtinge families. Verify exact template, effects, rank and
in-game appearance before marking a recipe confirmed. No exact Heavy Abyssal
recipe had been established by the initial review; the implementation now maps
the documented +65 Strength/+15 physical build to stock 1.09 numeric metadata.
Its actual granted name, rating and properties still need gameplay confirmation.

### Advanced mode: deliberate custom gems

An explicit toggle, off by default, exposes custom effect combinations, shapes,
raw IDs and all six native slots. Mark the recipe as custom, not naturally
obtainable. Allow stronger-than-natural combinations of supported native effects;
do not imply the tool can invent arbitrary effects or magnitudes absent from
the game's tables. Native validity and ownership checks remain mandatory.

Opening a shared custom recipe must not silently enable Advanced or spawn it.
Returning to Normal requires a valid obtainable recipe before creation; never
silently truncate effects or claim an Advanced build became natural.

Save, name and share recipe definitions through the local library. Keep recipe
selection separate from item grants: selecting/loading a recipe only previews it.
Catalog updates can ship with BB Tools releases; browsing needs no network.

### Acceptance checks

- Fresh portable install: searchable named effects and supported recipes without
  any selected game folder, import, existing database or network connection.
- Normal-mode validation rejects impossible combinations even when all IDs exist;
  handles source-dependent shapes, curses, ranks and conditional effects.
- Advanced defaults off; shared/saved custom recipes cannot activate it or spawn
  automatically. Invalid native IDs are rejected in either mode.
- Versioned catalog records are unique, bounded and traceable; missing or modified
  live rows produce a clear compatibility result rather than guessed substitutions.
- Preview and granted gem agree on all configured properties; check workshop
  equip, game name/rank, save/reload, inventory capacity and both Windows runtimes.
- Keep implementation and tested-and-confirmed status separate.

## Earlier review — 2026-10-26

Reviewed all four pages of [Nexus comments](https://www.nexusmods.com/bloodborne/mods/614?tab=posts).
The shoulder-camera offset and blood-gem editor/spawner were selected for implementation.
Other entries remain candidates for a later update.

| Candidate | Feedback | Suggested priority |
| --- | --- | --- |
| Horizontal shoulder-camera offset | Caleb0812, 26 October: left/right adjustment, distinct from height/distance. | First |
| Blood-gem spawning/editor | jelliore and Suitedlanzer, 24 October: customization and access beyond petrified gems. | First, research required |
| Old Hunter / NPC ally summons | SeniorModds, 22 October. | Next |
| Freecam/photo tools | Itsmeyoboy and nattydread011: FOV, roll, time stop, slow motion and cinematic timeline. | Larger project; currently hidden |
| More parry assistance | Monkesolos and Stubbornx, 18–20 October. | Native behavior research |
| Increased enemy count | sj620, 20 October. | Larger spawning/world-state project |
| Linux / bbhost compatibility | Multiple reports and requests. | Linux beta exists; live testing and bbhost work remain |

Older FOV, discovery, Chinese, durability and Ignore-hits requests are already
covered. Ludwig revival has a positive follow-up. Several access-denied reports
were resolved by administrator launch; avoid assuming every compatibility report
has the same cause. All unsupported requests need feasibility checks before promises.
