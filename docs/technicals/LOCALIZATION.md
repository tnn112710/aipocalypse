# Localization

Every piece of text the player reads comes from string tables (Unity Localization 1.5). The game ships in **English (en)**, **Vietnamese (vi)** and **French (fr)**; English is the default and the fallback.

**The CSV files are the source of truth.** Edit text there, never in Unity's table window: the next import overwrites the tables from the CSVs.

## Where things live

| What | Where |
| --- | --- |
| The text (edit these) | `Assets/_Game/Data/Localization/Csv/<Table>.csv`, one file per table |
| String tables (generated from the CSVs, don't edit) | `Data/Localization/Tables/` |
| Locales, settings | `Data/Localization/Locales/`, `Data/Localization/LocalizationSettings.asset` |
| CSV import, editor helpers | `_Editor/Localization/LocalizationCsv.cs` (importer), `LocalizationKit.cs` (references, label binding, the label map) |
| Runtime | `_Core/LocalizationSystem/` (`Loc`, `LanguageService`, `LanguageCommands`, `LocalizationInstaller`), `UI/Menus/LanguageButton.cs`, event `LanguageChanged` |
| Setup and migration scripts | `AgentScripts/Localization/LocalizationSetup.cs` (settings, locales, selectors, Addressables), `LocalizationMigration.cs` (pointed assets and labels at the tables), `AgentScripts/UI/FontSetup.cs` (fonts) |
| Tests | `Tests/EditMode/LocalizationTests.cs` |

## Tables and keys

| Table | Holds |
| --- | --- |
| `UI` | Menus, save slots, confirm dialogs, inventory screen, save messages, loading screen, language names |
| `HUD` | Clock, raid banner, breach alert, death screen, sleep screen, QTE, compass, item-use refusals |
| `Items` | Item names, rarities, equipment slot captions |
| `Interaction` | Prompt verbs (`verb.*`), prompt targets, barrier and crate names, the "Hold:" prefix |
| `Creatures` | Animal and enemy names |

Keys are lower case and dotted, area first: `menu.resume`, `item.ak47`, `verb.pick_up`, `sleep.raid_tonight`.

## Editing text

1. Open the CSV in **VS Code, LibreOffice or Google Sheets** (import the file, then export it as CSV). **Not Excel:** it can save in the wrong encoding and rewrite quotes and numbers, which corrupts the `Id` column.
2. Columns: `Key`, `Id`, one per language (`en`, `vi`, `fr`), and `Context` (a note for translators; the import ignores it). Save as UTF-8.
3. Unity imports the file the next time it refreshes (when its window gets focus), or use **Aipocalypse > Localization > Import All CSVs**. The import adds, renames, updates and removes entries to match the file.

- **New text:** add a row and leave `Id` empty. The import gives it an Id and writes that Id back into the file.
- **Renaming a key:** change `Key` and keep `Id`. References in assets point at the Id, so they still work. Keys used in code (`Loc.UI("key")`) must be renamed in code too; `LocalizationTests` fails until they match.
- **Deleting a row** removes the entry. Anything still pointing at it fails `LocalizationTests`.
- Every key needs text in every language (tested). Keep the leading or trailing spaces some texts carry (suffixes like `" behind you"`, the `"Hold: "` prefix); the Context column says where they matter.
- **Line breaks** go inside a quoted cell. The import stores them as `\n` whatever the file's line endings (Git's `core.autocrlf` checks the CSVs out with CRLF on Windows), so every clone builds the same tables.
- **Placeholders** `{0}`, `{1}` are filled by code. Two formats exist, and the Context column says which one a text uses: .NET `string.Format` (`Loc.Format`), or TextMeshPro's `SetText`, where `{0:1}` means one decimal. Smart Strings are off. A plural is two keys (`sleep.detail_one`, `sleep.detail_many`).
- **Writing style:** short and natural, like a game HUD, not a literal translation. A translation should be about as long as the English. Vietnamese addresses the player as "bạn" and may use gamer slang ("Toi rồi!"); French uses "tu".

After adding text with **new characters** (a new letter or symbol), re-run `FontSetup.All` (see Fonts).

## Using text in code

- **Data on assets and components** (item names, style texts, crate labels): a `LocalizedString` field (`[SerializeField] LocalizedString _displayName;` with `public string DisplayName => _displayName.Text();`). Pick the entry in the Inspector, or from an editor script with `LocalizationKit.Set(property, table, key)` or `LocalizationKit.Ref(table, key)`. Both reference the entry by table GUID and Id.
- **Text owned by code** (verbs, refusals, formats): `static readonly LocalizedString NoRoom = Loc.UI("inventory.no_room");`, then `NoRoom.Text()` or `NoRoom.Format(a, b)`. `LocalizationTests` checks that every static `LocalizedString` in Core points at a real entry.
- **Fixed labels in prefabs and scenes** (menu buttons, titles): a `LocalizeStringEvent` on the label writes the entry into it at runtime and again on every language switch. Its call state is RuntimeOnly, so changing the editor's preview language never rewrites a prefab. The labels are listed in `LocalizationKit.Labels`, and `LocalizationKit.ApplyLabels()` binds them. The setup scripts that build UI (`SaveSetup`, `SleepSetup`, `InventorySetup`, `MinimapSetup`, `DefenseSetup`) call it at the end of their full runs. A new fixed label is a new line in `Labels`.
- **Not localized:** the dev console and cheats, logs, the game title ("Hasta la Vista, M.A.Y.B.E"), key names (the Input System's binding names), numbers, clock times, file paths, and OS error text inside "Save failed: …".

### Performance

- `Text()` caches each string per language. A lookup (~12 µs in the editor) runs once per language, and after that the same string instance comes back (~0.1 µs, no allocation). Views that skip unchanged text by reference (`InteractPromptView`, `ItemUseHud`, `ItemSlotView`, `ClockView`) keep skipping. Never call `LocalizedString.GetLocalizedString()` directly in a per-frame path.
- `Format` allocates: use it for text built on events. Per-frame numbers keep TMP `SetText(format, value)` with the cached format string (`_weight.SetText(WeightFormat.Text(), weight, max)`).
- Text composed from several strings is cached by the code that composes it, along with the language it was built in (`Loc.Current`). The cache is rebuilt when the language changes (`WorldItem`, `BarrierSegment`, `FenceGate`, `BarrierRegistry.RepairAllTarget`, `ClockHud`, `ClockView`).
- Views that set text once (menus, `LanguageButton`) listen to `LanguageChanged`. Screens that rebuild every time they open (inventory, slot list, death screen, loading screen) need nothing.
- Tables load synchronously at start (`InitializeSynchronously`, preload of the selected language and its fallback; `LanguageService.Initialize` waits for it). A switch loads the new language's tables in a few milliseconds.

## Switching language

- `LanguageService` (root): `Languages`, `Current`, `Select(locale or code)`, `Step(±1)`, `NameOf(locale)`. `NameOf` gives each language's own name from `UI/language.name` in that language ("Tiếng Việt").
- **Menus:** a Language button under Settings in the main menu and the pause menu ("LANGUAGE: TIẾNG VIỆT"). Each press moves to the next language, and everything updates at once.
- **Console** (editor and cheat builds): `lang.list`, `lang.set vi`, `lang.next`.
- **Saved** in PlayerPrefs under `selected-locale` (not in the save slots). At start the language comes from the command line (`-language=vi`), then the saved choice, then English. The OS language is never used.
- A missing entry in vi or fr falls back to English (the tests keep this from happening).

## Fonts

The UI fonts are static TMP atlases (`Data/UI/Fonts/SairaHeader`, `SairaBold`, `SairaBody`, `DeathScreen/DeathScreenFont`, `RepairQteFont`), so playing never rewrites a font asset (UI.md). `FontSetup.All` fills them with printable ASCII, the punctuation the game uses, the full Vietnamese and French alphabets, and **every character found in the CSVs**. It grows an atlas from 1024×1024 to 2048×1024 or 2048×2048 only when the characters don't fit (today: 2048×1024, 260 characters each). Then it resizes the material presets that draw each atlas. `LocalizationTests.Fonts_HoldEveryCharacterOfTheTables` fails when the CSVs contain a character a font lacks. The Saira Condensed and Varela Round font files cover Vietnamese and French (Saira lacks only ₫).

Vietnamese accents stack above (and below) the letters: check tight boxes (buttons, hotbar captions) when adding long or upper-case text. A language in another script (Chinese, Japanese, Korean, Cyrillic) needs its own font, swapped per language (a Localization asset table) or added as a fallback.

## Builds

The string tables and locales are Addressables (groups `Localization-*`, bundle names `NoHash`). **Autobuild** builds the Addressables content first and stops if that fails, then builds the player, which copies it in (BUILD.md). A build from **File > Build Profiles** builds them too (Addressables "Build with player" is on).

## Adding a language

1. Add it to `LocalizationSetup.Locales` (code and an ASCII asset name) and run `LocalizationSetup.All`.
2. Add a column named after its code to every CSV and fill every row, `language.name` included (its name in itself).
3. Run `FontSetup.All`. Add the code to `Languages` in `LocalizationTests`.
4. Check the menus and HUD in it (long words, accents).

## Tests

`LocalizationTests`: the CSVs match their tables, and every key has every language. Every code key resolves. Every `LocalizedString` on assets and prefabs points at an entry. Items and loot containers have text. The HUD labels are bound. The pause menu has its Language button. The fonts hold every character. Every rarity has a name. `Text()` keeps one string per language and follows a switch.
