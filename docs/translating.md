# Translate BB Tools

Help make BB Tools usable in your language. You do not need to change game code
or install the game to contribute text. Native-speaker review is welcome,
especially for the Simplified Chinese draft and Bloodborne terminology.

Translate or review text on [BB Tools on Crowdin](https://crowdin.com/project/bb-tools),
or contribute a GitHub pull request using the instructions below.

## Translation status

English is the source. Simplified Chinese (`zh-Hans`) has a draft for the complete
extracted interface/message catalog; it still needs native-speaker review and
in-game usability checks. Other languages have starter navigation/control labels
and fall back to English for missing text. A language being selectable does **not**
mean it is fully translated or reviewed. Settings → Preferences shows coverage.

The app matches the project's 28-language list: English, Arabic, Catalan, Czech,
Danish, Dutch, Finnish, French, German, Greek, Hungarian, Italian, Japanese,
Korean, Norwegian Bokmål, Polish, European Portuguese, Brazilian Portuguese,
Romanian, Russian, Serbian Cyrillic, Spanish, Swedish, Turkish, Ukrainian,
Vietnamese, Simplified Chinese and Traditional Chinese.

Game-owned item/boss/area catalogs and technical memory diagnostics can still
appear in English. User-created preset/location names and internal game IDs are
never translated. The app reports catalog coverage, not a promise that every
game-data name or diagnostic is localized.

## Contribute with a GitHub pull request

1. Find `src/BBTools/Localization/Locales/en.json` and the file for your language.
2. Copy a missing key into your language file and translate **only its value**.
   Empty/missing values intentionally use the English source.
3. Preserve every `{0}`, `{1:N0}`, `{0:0.0}`, date/time format and escaped brace.
   Placeholders may be reordered, but do not rename, remove or reformat them.
4. Keep `BB Tools`, `Bloodborne`, `JamesCicada`, IDs, hotkey names, numeric ranges,
   addresses and filenames recognizable. Translate explanations, not identifiers.
5. Run `node scripts/check-translations.cjs` (or let CI check your pull request).
6. If possible, select your language in the app and include a screenshot of long
   labels, warnings and the minimum-size window. Mention whether you checked the
   text in-game or only reviewed the wording.

The values are plain text, not XAML/HTML. Do not introduce markup. Use the usual
Bloodborne terms in your language and avoid making uncertain features sound tested.
Arabic uses RTL layout; numeric/ID inputs remain LTR. Numeric parsing culture is
not changed when selecting a UI language. Switching language does not apply a
gameplay preset or change game IDs.

The `source-context.json` file gives UI locations for the initial extraction;
searching a resource key in the source also shows its live usage. New English text
belongs in `en.json`; use `{DynamicResource L.key}` in XAML and
`Translations.T("key")` / `Translations.Format("key", arguments)` in code.

## Set up Crowdin — file-based

Use a **file-based project**, with English as the source language. The repository's
`crowdin.yml` maps the English JSON to each target file. It maps Crowdin Chinese
`zh-CN`/`zh-TW` to `zh-Hans`/`zh-Hant`, Norwegian to `nb`, Serbian to `sr-Cyrl`, and
keeps Portuguese variants separate. English is excluded as a translation target
so an export cannot replace the source catalog.

Connect the project to this GitHub repository through Crowdin's GitHub integration,
select `crowdin.yml`, and send exported translations to a dedicated branch/pull
request. Keep code review and the translation checks enabled. Upload the starter
translation files as unreviewed translations; do not mark drafts as approved.

For CLI use, provide `CROWDIN_PROJECT_ID` and `CROWDIN_PERSONAL_TOKEN` through your
environment or CI secrets. Never commit a token to `crowdin.yml`. The public project
is https://crowdin.com/project/bb-tools and the app's contribution button opens it.
The owner still needs to enable the GitHub integration (or configure the numeric
project ID for CLI use). No remote upload or synchronization is claimed by merely
adding the config; verify the first exported translation pull request.

Official reference: [Crowdin configuration](https://support.crowdin.com/developer/configuration-file/).

## Testing and packaging

Translation validation runs with `node --test scripts/*.test.cjs` in desktop CI.
It rejects unknown keys, malformed placeholders and changed numeric/date formats.
Runtime fallback also protects against missing entries. Language selection is
stored by locale code, independently of translated labels and gameplay presets.

Catalogs are embedded in the executable. Translators edit repository JSON files;
users do not need to copy language packs or install Crowdin. Windows release ZIPs
continue to contain only `BBTools.exe` and `README.md`.
