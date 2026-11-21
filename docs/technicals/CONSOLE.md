# Dev console

In-game console for commands and cheats, Unreal style, with the Unity logs on request. Editor and development builds only: release builds don't register it. Code in `_Core/_Debug/Console/`, script map in [CODE_STRUCTURE.md](CODE_STRUCTURE.md). The on-screen log and the debug hotkeys are in [DEBUG.md](DEBUG.md).

## Using it

Press the backquote key `` ` `` (left of 1, the same key on any keyboard layout) to open or close it. It drops down over the top 45% of the screen: the scrollback, the input line, and suggestions under it. The game keeps running (`ConsoleSettings.PauseWhileOpen` to pause it).

**Unity logs are off by default:** the scrollback shows only your commands and their results. `log.screen.error`, `log.screen.info` or `log.screen.all` start showing Unity logs from then on, in the scrollback and on screen ([DEBUG.md → Screen log](DEBUG.md#screen-log)); `log.screen.clear` stops them. Logs from before you turned them on aren't kept. The starting level is `ConsoleSettings.ScreenLogLevel`.

While it's open it holds `InputService`'s text input: the Player, UI and Debug maps are off, so typing never moves the player, fires a hotkey (typing `k` doesn't kill you) or confirms a menu. The cursor keeps its state.

| Key | Does |
| --- | --- |
| Enter | Run the line |
| Esc, or the console key | Close (the line is kept) |
| Tab | Complete the command name or argument; again to cycle the suggestions |
| ↑ / ↓ | Move through the suggestions while there's a choice (↓ like Tab, ↑ backwards); otherwise the history (saved between sessions in `persistentDataPath/console_history.txt`) |
| ← → Home End, Backspace Delete | Edit; with Ctrl, a whole word (`clock.set` is two words) |
| Ctrl+V | Paste the clipboard's first line |
| PageUp / PageDown, mouse wheel | Scroll the scrollback |

**Typing commands:** `name arg1 arg2`. Names ignore case. `"double quotes"` keep spaces in one argument (`\"` inside them); `;` runs several statements in a row (`time.scale 0.2; clock.phase dusk`). Arguments: numbers with a dot (`7.5`), `on`/`off` (also `true`/`false`, `1`/`0`), enum names or any unique start of one (`du` → `Dusk`), vectors `1,2,3`, colours `#ff8800`. A property command reads with no argument and sets with one (`time.scale`, `time.scale 0.5`).

**Results:** what a command returns prints in green, failures in red; logs a command writes come before its result. Identical lines in a row collapse into one with a count.

**From the command line:** a development build started with `-console "<commands>"` runs them once a few frames in (when every scene object has started), as if typed, `;` between them. For unattended runs, e.g. a night fight on autopilot for a profiler capture: `-console "app.background on; cheat.god on; cheat.infammo on; cheat.take ak47; clock.set 21; raid.start; cheat.autopilot on"` (`AgentScripts/Perf/PerfBuild.cs`).

## Commands

With nothing typed, the line under the input lists this scene's command groups (`afps  cheat  clear  clock  enemy  gfx  help  log  pause  player  raid  save  time`). `help` lists exactly what works in the current scene, `help clock` one group, `help clock.set` one command's usage. Arguments in `[ ]` are optional.

| Command | Does |
| --- | --- |
| `help`, `help <command>`, `help <group>` | Every command; one command's usage; a group's commands (`help clock`) |
| `clear` | Empty the scrollback |
| `log.screen.all` | Show every Unity log (logs, warnings, errors) in the scrollback and on screen |
| `log.screen.error` | Show Unity errors only (errors, exceptions, asserts) |
| `log.screen.info` | Show `Debug.Log` messages only |
| `log.screen.clear` | Stop showing Unity logs (the default) and remove the messages on screen |
| `log.screen` | Which level is on (Off, Error, Info, All) |
| `log.trace` | Stack trace of the last error Unity logged |
| `build` | Which build this is: name, commit, build date, config ([BUILD.md](BUILD.md#build-stamp)) |
| `app.background [on/off]` | Keep running while the window isn't focused (`Application.runInBackground`, this session only): unattended runs, profiling a build |
| `cheat.kill`, `cheat.revive` | Kill the player with a real hit (through `DamageService`, like K, even in god mode) / revive (like L) |
| `cheat.hp [amount]` | Heal to full, or by an amount |
| `cheat.stamina` | Refill stamina |
| `cheat.ammo [amount=100]` | Add rounds of each ammo type the guns in the hotbar use |
| `cheat.infammo [on/off]` | Reloads never use up the reserve (`AmmoReserve.Infinite`) |
| `cheat.god [on/off]` | Hits apply nothing to the player (`PlayerLife.Invulnerable`): no damage, no hurt feedback |
| `cheat.notarget [on/off]` | Enemies don't choose the player as a target (`IPlayer.IgnoredByEnemies`); hunters wander instead |
| `cheat.give <item> [count=1]` | Put an item in the inventory where a pickup would go (`cheat.give tokens 500`, `cheat.give medic`): the start of any loaded item's asset or display name |
| `cheat.take <item>` | Pick up the first item lying in the scene whose name starts with it (`cheat.take ak47`), wherever it is |
| `cheat.killall` | Kill every enemy in play with a real hit (scenes with an `EnemyInstaller` only) |
| `cheat.autopilot [on/off]` | The player fights by itself: aims at the nearest enemy in sight, fires, reloads, swaps guns (`RaidAutopilot`; scenes with an `EnemyInstaller` only) |
| `stat.list`, `stat.set <stat> <multiply> [add]`, `stat.remove <stat>`, `stat.clear` | The stat modifier layer (`StatModifiers`): every modifier in play; set, remove or clear the console's own (`stat.set SprintSpeedScale 1.5`, `stat.set MaxHealth 1 50`). Saved with the game like any modifier |
| `player.pos` | Where the player stands |
| `save.list`, `save.write [slot]`, `save.load [slot]`, `save.new [slot]`, `save.delete <slot>`, `save.deleteall`, `save.auto`, `save.path`, `save.menu` | Save slots ([SAVE.md](SAVE.md#console); 0 / no slot = the current game's, 99 = the editor slot): every slot's summary; save now (written in the background); load (the world reloads); new game in a slot (the first save overwrites its old one); delete one / all; autosave now (waits out a raid or death); the save folder (copied to the clipboard); back to the main menu |
| `lang.list`, `lang.set <code>`, `lang.next` | The game's language ([LOCALIZATION.md](LOCALIZATION.md)): every language with its code (`*` = current); switch to `en`, `vi` or `fr`; the next one. Kept for the next launch, like the menus' Language button |
| `time.scale [0–1]` | Game speed (slow motion); 1 drops the console's request |
| `pause` | Pause the game, or resume it |
| `gfx.shadows [metres]`, `gfx.startwinkle [on/off]` | The player's graphics settings (`GraphicsService`) |
| `afps.show`, `afps.hide` | Advanced FPS Counter overlay, off by default (`show` adds one to a scene without it) |
| `clock.now`, `clock.set <hour>`, `clock.phase <phase>`, `clock.nexthour`, `clock.nextphase` | World clock: read, jump (scenes with a `WorldTimeInstaller` only) |
| `clock.skip <hour> <seconds>`, `clock.speed [0–1]`, `clock.freeze <on/off>` | Fast-forward with every hour passing; slow or stop the clock |
| `highlight.light [on/off]`, `highlight.reset` | The enemies' night light ([DAYNIGHT.md](DAYNIGHT.md#enemies-at-night)): read, switch for this session, back to the settings asset (scenes with a `NightHighlight` on the `EnvironmentInstaller` only) |
| `weather.show`, `weather.set <sky> [wind] [seconds]`, `weather.wind <Calm/Breezy/Windy/Gale/auto>`, `weather.strike` | Weather ([WEATHER.md](WEATHER.md)): the sky weather, wind, blend and who decides it; change the sky (default wind, or the given one) over the settings' transition or `seconds` (0 = cut); set or clear the wind override; with the weather rules on, `set` and `wind` hold the weather until `weather.auto`; a lightning flash and its thunder now (scenes with Weather settings on the `EnvironmentInstaller` only) |
| `weather.auto`, `weather.forecast`, `weather.force <sky> <hours>`, `weather.force <sky> <wind> <hour> <hours>`, `weather.bloodmoon [day on]`, `weather.eclipse [day on]` | Weather rules (`WeatherSchedule`): release a held weather; the plan for the next 24 game hours; force a weather from now (wind rolled) or from the next time the clock shows `hour`; mark tonight / the night starting on `day` a Blood Moon, today / `day` an eclipse (`on` false unmarks) |
| `enemy.spawn <card> [count=1] [elite=false]`, `enemy.clear`, `enemy.count`, `enemy.hit <power> [radius=25]` | Spawn enemies of a card (`EnemySystemSettings.Cards`, any unique start of its name: `goblin`) 15 m in front of the player, as raiders (they go for the farm until they notice the player; `elite` makes every one elite), arriving within the spawn budget; despawn every enemy and drop the console's queued spawns (a raid's stay); enemies in play; hit every enemy within `radius` m with a push of `power` N·s away from the player and almost no damage, to try knockback and stagger (an AK bullet is 1.5, a shotgun blast about 6.4; 40 stands in for a rocket) (scenes with an `EnemyInstaller` only) |
| `fence.status`, `fence.damage <amount> [count=1]`, `fence.breach [count=1]`, `fence.repair`, `fence.upgrade`, `fence.gate <open>` | Fences and gates ([DEFENSE.md](DEFENSE.md)): health summary; dent or break the segments nearest the player (as world damage, not the player's); restore all; upgrade all one tier; open or close every gate (scenes with a `DefenseInstaller` only) |
| `animal.status`, `animal.release`, `animal.call`, `animal.hurry`, `animal.fill [tokens=10]`, `animal.collect` | Farm animals ([ANIMALS.md](ANIMALS.md)): how many, how many home, how many sent home, their orders and the Tokens held; open the pen gate; ring the bell; hurry every animal; give each animal Tokens to hand over; every animal hands its Tokens over now (scenes with an `AnimalInstaller` only) |
| `raid.start`, `raid.stop`, `raid.next`, `raid.status` | Start tonight's raid now (even by day); end it and despawn its raiders; start the next wave now; the night, wave, spawned / alive and how the last raid ended (scenes with a `RaidInstaller` only, [RAIDS.md](RAIDS.md)) |
| `spawn.stats`, `spawn.card <card> [count=1] [formation=scatter]`, `spawn.near <card> [count=1] [radius=60]`, `spawn.test <ground|air|water> [count=40] [radius=80]`, `spawn.clear` | Alive by category, queued and placing requests, withdrawn ones leaving, awake population areas; spawn any card in `SpawnSystemSettings.Cards` (goblin, fishschool, birdflock) 15 m in front of the player as scatter, line, column, wedge, ring or clusters; spawn it anywhere it can stand within `radius` m of the player, seen or not (finds water for fish, sky for birds); draw where test points of a medium land for 30 s, with how many fit and their heights; despawn what the console spawned (scenes with a `SpawnInstaller` only, [SPAWNING.md](SPAWNING.md)) |

Speed, pause and freeze are the console's own owner-keyed requests (`TimeService`, `WorldClock`), so other systems' requests still apply, and they're released when the scope that registered them ends.

## Adding a command

Each system owns its commands in `<System>/<System>Commands.cs`, a `ConsoleCommands` subclass registered as an entry point in the system's installer, inside its `Cheats.Enabled` check (editor and `ENABLE_CHEAT` builds, [BUILD.md](BUILD.md)). Add the new command to the table above in the same change.

```csharp
// WorldTimeSystem/WorldClockCommands.cs
[CommandGroup("clock")]
public sealed class WorldClockCommands : ConsoleCommands
{
    readonly WorldClock _clock;

    public WorldClockCommands(CommandRegistry registry, WorldClock clock) : base(registry) => _clock = clock;

    [Command("Jump to the start of a phase")]
    string Phase(DayPhase phase)           // → clock.phase <phase:DayPhase>
    {
        _clock.JumpToPhase(phase);
        return Now();                       // printed as the result
    }
}

// WorldTimeInstaller.Install
if (Cheats.Enabled)
    builder.RegisterEntryPoint<WorldClockCommands>();
```

- **Name:** the class's `[CommandGroup]` + the member name in lower case (`clock.phase`). `[Command(Name = "pause")]` sets the whole name. Members can be private.
- **Methods and properties:** a method's parameters are the arguments (defaults make them optional); a property reads with no argument and sets with one. Several methods with one name are one command, told apart by argument count.
- **Argument types:** `string`, `bool`, `int`, `long`, `float`, `double`, any enum, `Vector2`, `Vector3`, `Color`. A command with another type is skipped with a warning.
- **Output:** the return value prints (`null` / `void` prints nothing; collections one item per line). Return `CommandResult.Fail("why")` for a failure (`CommandResult.Ok(text)` for success). A command that throws is logged and reported, never rethrown.
- **Scope:** the commands exist while their scope does. Root systems' commands are always there; a scene system's (clock, enemies, raids) only in scenes that install it, so `help` lists what works in the current scene.
- **Owner-keyed requests:** pass `this` as the owner and release in an override of `Dispose` (call `base.Dispose()`), as `TimeCommands` and `WorldClockCommands` do.
- Commands go through the system's services, never around them: a kill is a `DamageService.Deal`, not a health write.
- Add EditMode tests through a `CommandRegistry` (`ConsoleTests`: `TimeCommands`, `WorldClockCommands`).

## How it's built

```text
InputService  (Console map: ` always on in dev builds; editing keys + typed characters only while text input is held)
   │
ConsoleService (root ITickable, dev builds)    LogBuffer: Application.logMessageReceivedThreaded (any thread) → ring of
   ├─ ConsoleLine + ConsoleHistory + KeyRepeat     ConsoleSettings.LogCapacity lines, moved in by Flush each tick, keeping
   │                                               only the kinds ScreenLog.MirrorLevel shows (none by default)
   ├─ CommandRegistry.Execute / Suggest  ◄── ConsoleCommands subclasses Add / Remove themselves (Initialize / Dispose)
   └─ ScreenLog.Mirror (the same logs, on screen)
ConsoleView (DebugConsole.prefab, created by ConsoleInstaller, DontDestroyOnLoad) draws the service in LateUpdate; ScreenLogView draws ScreenLog
```

- `ConsoleInstaller` (root, from `ProjectLifetimeScope`): `ScreenLog` always; in dev builds `CommandRegistry` (so systems can register commands), and with the root prefab's `ConsoleSettings` and overlay assigned, `LogBuffer`, `ConsoleService`, `BuiltInCommands` and the overlay. Without them it warns and the console is off (commands still register).
- **Overlay:** `_Core/_Debug/Console/Prefabs/DebugConsole.prefab`, its own Screen Space Overlay canvas, sort order 1000 (over the HUD and the death screen), 1920×1080 reference, no raycaster. It also draws the screen log. Built by `AgentScripts/DebugConsole/DebugConsoleSetup.All()` (rebuilds the prefab, keeps the assets' values). The panel height (45%), input bar and margins are on the prefab.
- **Look:** `Data/Debug/ConsoleStyle.asset`: TMP's LiberationSans SDF drawn monospaced with `<mspace>` (`Monospace`, em), colours per line kind, caret, suggestions, the screen log's band. Backgrounds need a high alpha: UI blends in linear space here, so 0.9 still lets a bright scene through.
- **Behaviour:** `Data/Debug/ConsoleSettings.asset`: scrollback size, history size, pause while open, screen log level and time, key repeat, wheel step.
- **Cost:** closed, the console checks one key and empties the log queue each frame; the views redraw only when something changed (version counters), with rich text built in reused `StringBuilder`s. Typed text is escaped with `<noparse>`, so a log containing `<b>` or `List<int>` prints as is.

Not built: key binds (`bind F5 "clock.phase dusk"`), live watches (`stat fps`), commands that run over several frames.
