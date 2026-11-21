# Debug tools

Tools for testing in the editor and development builds. Release builds register none of them, except a `ScreenLog` that does nothing. Script map: [CODE_STRUCTURE.md](CODE_STRUCTURE.md); code in `_Core/_Debug/`.

| Tool | What |
| --- | --- |
| Dev console | `` ` `` opens it: commands, cheats, and the Unity logs on request (`log.screen.*`). Usage, the command list and how to add one: [CONSOLE.md](CONSOLE.md) |
| Screen log | Messages on screen, Unreal style (below) |
| Debug hotkeys | N / M / K / L (below) |
| Movement readout | `player.speed true`: the player's speed and every multiplier on it, two screen-log lines a frame (`MovementReadout`) |
| `TargetDummy` | Shooting-range target: logs each hit, heals back when emptied, tips over from knockback ([WEAPONS.md](WEAPONS.md)) |
| Odin buttons | On views and components, e.g. `Player.Revive`, a gun's **Jam gun (test)** |

## Screen log

Unreal-style messages on screen, top-left, newest on top, also while the console is closed (hidden while it's open: they're in the scrollback too). Inject `ScreenLog` (root scope, always registered):

| Call | Shows |
| --- | --- |
| `Print(text, seconds = 3)`, `Print(text, color, seconds)` | A new line for that long |
| `Show(key, text, seconds = 0)`, `Show(key, text, color, seconds)` | A line that replaces the previous one with the same key; `0` = this frame only, so a value shown every frame stays one line |
| `Remove(key)`, `Clear()` | |

Unity logs show there too (and in the console's scrollback) only once you pick a level in the console: `log.screen.error` (errors), `log.screen.info` (`Debug.Log` only), `log.screen.all` (everything), `log.screen.clear` (off again, the default; also clears the screen). Each stays `ConsoleSettings.ScreenLogSeconds`; an error logged every frame stays one line. It runs on real time, so a pause doesn't freeze it. At most 24 lines. The console's overlay draws it ([CONSOLE.md → How it's built](CONSOLE.md#how-its-built)).

In builds without cheats `ScreenLog` does nothing and nothing draws it, but the string you build for it is still built: wrap per-frame `Show` calls in `if (Cheats.Enabled)`.

## Debug hotkeys

The `Debug` input map (editor and dev builds): **N** next hour, **M** next phase (`WorldClockCheats`), **K** kill the player, **L** revive (`PlayerCheats`). They're off while the console has text input. The console has the same actions and more (`clock.nexthour`, `cheat.kill`…).
