# Save and load

Five save slots, an autosave every morning, a pause menu and a main menu. Saves are Easy Save 3 JSON files; each system saves its own part. Code in `Assets/_Game/_Core/SaveSystem/` (script map: [CODE_STRUCTURE.md](CODE_STRUCTURE.md)).

## How it plays

- **A slot is one game.** Starting a New Game picks a slot (an occupied one asks first; its old save stays on disk until the new game's first save overwrites it, then remains as the `.bak`). From then on the game saves to that slot: the morning autosave, Save Game in the pause menu, or `save.write`.
- **Autosave:** when the **Morning** phase starts, from **day 2** on (`SaveSettings.AutosavePhase`, `FirstAutosaveDay`). It waits while a raid runs or the player is down, then saves; a write that fails is retried every 30 s, never dropped. "Autosaved · Save N" shows bottom right for 2 s.
- **Saving needs** a living player, no raid running and no load in progress (`GameSession.CanSave`). The pause menu's Save Game is greyed out with the reason under the buttons.
- **Loading** reloads the world scene behind a black loading screen and puts the save back on the scene's first frame.
- **Editor:** pressing Play in WorldMap (or any world scene) starts a new game on day 1 in `SaveSettings.EditorSlot` (Save 1). Nothing is written until the day 2 morning or a manual save. Pressing Play in `MainMenu.unity` runs the real flow.
- **Builds** start in `MainMenu.unity` (first in Build Settings), then WorldMap.

## Menus

| Where | What |
| --- | --- |
| Esc in play (gamepad Start) | **Pause menu** (`PauseMenu`, in `HUD.prefab`): pauses the game (also during a raid; not while the player is down, where the death screen owns the click). Resume, Save Game, Load Game, Settings (disabled until a settings menu exists), Main Menu, Quit Game; the last two ask first ("Progress since your last save will be lost"). Esc goes back one step: dialog → slot list → menu → game |
| `MainMenu.unity` | **Main menu** (`MainMenuScreen`): Continue (the most recent save; its slot and summary under it), New Game, Load Game, Settings (disabled), Quit |
| Save / Load / New Game | **Select Save Slot** (`SaveSlotsPanel`, after Escape from Duckov): one row per slot, "Save N" with the day, hour, play time and the date it was saved, or Empty / New Game. The current game's slot is orange. Delete on each saved slot (asks first), Copy Save File Path (to the clipboard). Overwriting another game's slot and loading while playing ask first |

Look: `Data/UI/MenuStyle.asset` (font, rounded sprite, colours). Layout: rebuilt by `AgentScripts/Save/SaveSetup` (`Hud()` for the pause menu, `MainMenu()` for the scene); edit there, not in the prefab.

## What a save holds

| Key | Section (system) | Saved | Not saved |
| --- | --- | --- | --- |
| `random` | `RandomSaveSection` (RandomSystem, root) | Master seed, every named stream's position | |
| `clock` | `ClockSaveSection` (WorldTimeSystem) | The time (day + hour) | Speed requests, a skip in progress |
| `weather` | `WeatherSaveSection` (EnvironmentSystem) | Sky, wind override | The schedule's plan (it plans on from the restored sky), console holds |
| `raids` | `RaidSaveSection` (RaidSystem) | Nights already handled | A raid in progress (saving is refused during one) |
| `defense` | `DefenseSaveSection` (DefenseSystem) | Each segment / gate: health, broken, gate open | Upgrade tiers |
| `loot` | `LootSaveSection` (EquipmentSystem, root) | Scene crates: items by slot, which slots are still hidden | Enemy bodies and their loot |
| `worldItems` | `WorldItemSaveSection` (EquipmentSystem, root) | Every item lying in the world, scene-placed or dropped (a picked-up item stays picked up) | Mid-flight physics (thrown items load where they were) |
| `player` | `PlayerSaveSection` (PlayerSystem, root) | Position, facing, look pitch, health, hotbar / backpack / gear (ammo included: it's items), the selected slot; per item: count, a gun's rounds and jam, a kit's uses left | Stamina, timed effects (buffs, healing), a pose sequence |
| `_header` | `SaveService` | Version, saved at (UTC), play time, scene, build, day and hour for the slot list | |

Also not saved: live enemies and population timers (the population director fills the world again), effects, bullets, QTEs.

## How it works

```text
Root scope:  SaveService (files) · SaveRegistry (sections alive now) · GameSession (slot, play time, save / load / new game) · SceneLoader (+ loading screen)
Scene scope: WorldSaveInstaller → WorldSaver: first frame → GameSession.EnterWorld (restore the pending save, or start fresh); Morning → RequestAutosave
Each system: <System>SaveSection registered by its own installer, joins SaveRegistry while its scope lives
```

- **Save:** `GameSession.Save(slot)` → `SaveService.Create(carry)` (a copy of the loaded save, so keys no section wrote this time survive) → every section's `Capture` → header → `SaveService.Write`: the previous readable file becomes `slot_N.es3.bak`, then Easy Save writes `.tmp` and swaps it in.
- **Load:** `GameSession.Load(slot)` reads the file (or its `.bak` when the file is damaged; a file from another save version is refused, never swapped for its older backup), loads the scene named in the header through `SceneLoader`, and `WorldSaver.PostStart` (after every Start, before the first Update) calls each section's `Restore` in `SaveOrder`: random, clock, weather, raids, world (defense, loot), world items, player. A section that throws is logged and skipped; the rest still load.
- **New game:** `GameSession.NewGame(slot)` reseeds `RandomService` and loads the world scene; nothing is restored. The slot's old file is only replaced by the first save.
- **Failures:** a section that throws while saving is logged and skipped (its entry from the last save is kept, the toast message says so); one that throws while loading keeps its new-game state. A scene load that fails leaves the current world running and playable. The pause menu releases its pause and UI requests before anything else, also when a load destroys it.
- **Missing key = new-game state.** A system added after a save, or switched off, finds nothing and keeps its defaults; a switched-off system's key is carried over unchanged.
- **Files:** `Application.persistentDataPath/Saves/slot_1.es3` … `slot_5.es3` (+ `.bak`), plain JSON (`save.path` prints and copies the folder). Main thread only; a save is ~17 KB and takes a few ms.

## Ids

A save never stores a Unity object reference.

- **Items:** by asset GUID through `Data/Equipment/ItemCatalog.asset` (on `ProjectLifetimeScope.prefab`). **After adding an item, select the catalog and press "Collect every item"**; `SaveTests.ItemCatalog_ListsEveryItemDefinition_ByItsAssetGuid` fails until you do. An id the catalog no longer knows is dropped on load with a warning.
- **Objects placed in a scene** (fence segments, gates, crates): a `SaveId` component holding a GUID, on the prefab (`WoodFence`, `WoodGate_Double`, `LootCrate`) with the id per scene instance. It's generated when the object is placed, and again when one is duplicated; prefab assets and objects spawned at runtime keep it empty and aren't saved through it. Sections skip objects without an id, and an object placed after a save keeps its authored state.
- **Objects spawned at runtime** (dropped items, later animals) are saved as records (item id, position) and spawned again on load.

## Adding a system to the save

1. A `[Serializable]` data class with public fields: ids, numbers, strings, enums, `List<>`, `Dictionary<,>`, `Vector3` / `Quaternion`. No `MonoBehaviour`, `ScriptableObject` or other Unity object references: store an id (catalog GUID, `SaveId`) and resolve it on load.
2. A `<System>SaveSection : SaveSection` with a unique key, an `Order` from `SaveOrder`, `Capture` (`save.Set(key, data)`) and `Restore` (`save.TryGet(key, out data)`; missing → leave the new-game state). Restore silently: no sounds, effects or gameplay events (see `BarrierSegment.RestoreSaved`).
3. Register it in the system's installer: `builder.RegisterEntryPoint<XSaveSection>();`.
4. A round-trip EditMode test of the data (`SaveTests` shows the pattern), and a row in the table above.

**Changing saved data:** Easy Save writes each value's type name (`Namespace.Class+Nested, Assembly`) and enums as numbers. Renaming or moving a saved class, or reordering an enum, makes old saves fail that section; renaming a field loses its value. During development: bump `SaveService.Version` when a shape changes (older saves then show as "Old save format" and can't be loaded); no migrations yet.

## Console

Editor and cheat builds (`save` group, root: also in the main menu). Slots are 1-based; 0 = the current game's slot.

| Command | Does |
| --- | --- |
| `save.list` | Current slot and play time, then every slot's summary |
| `save.write [slot]` | Save now |
| `save.load [slot]` | Load (the world reloads) |
| `save.new [slot]` | New game in the slot (its old save is overwritten by the first save) |
| `save.delete slot` / `save.deleteall` | Delete saves |
| `save.auto` | Autosave now, as the morning does (waits out a raid or death) |
| `save.path` | The save folder, also copied to the clipboard |
| `save.menu` | Back to the main menu |

## Tuning

| What | Where |
| --- | --- |
| Slot count, folder, autosave on / phase / first day, scene names, editor slot, loading screen font and text | `Data/Save/SaveSettings.asset` |
| Menu look | `Data/UI/MenuStyle.asset` |
| Toast time | `HUD` → `SaveToast` → `SaveToastHud` |

## Setup and checks

- `AgentScripts/Save/SaveSetup.cs` (`unity command script run --file AgentScripts/Save/SaveSetup.cs --entry SaveSetup.All`): assets, the root scope's references, the HUD's PauseMenu and SaveToast, `WorldSaveInstaller` and the `SaveId`s in WorldMap, `MainMenu.unity`, Build Settings. Safe to re-run; `World()` reopens WorldMap from disk and saves it.
- `AgentScripts/Save/SavePlayTest.cs`: play-mode probes (`Status`, `Jump <phase>`, `Mutate`, `Save n`, `Load n`, `Pause`, `Slots Save|Load`, `MainMenu`, `DeleteAll`).
- `SaveTests` (EditMode): file round trip, backup fallback, a damaged file never replacing the last good backup, version refusal, carried keys, slot info, the item catalog, item and container round trips, random streams, section order and failure isolation.

## Known gaps

- No settings file yet (Settings is disabled in both menus); settings will live in their own file, not in a slot.
- Raid state, enemies, bodies, timed effects and stamina aren't saved; save points avoid raids, and the morning autosave comes after the night.
- No migrations: a version bump makes older saves unloadable (fine during development).
- Quitting doesn't save: progress since the last save (morning or manual) is lost, as the menus warn.
- The pause and main menus have no controller navigation polish and no sounds.
