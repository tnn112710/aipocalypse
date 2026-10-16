# UI

How the in-game UI is built and how to extend or restyle it. Script locations and frame order are in [CODE_STRUCTURE.md](CODE_STRUCTURE.md).

## Rules

- **uGUI + TextMeshPro.** No UI Toolkit for the game HUD.
- **Art comes from Synty Interface Apocalypse HUD as sprites only** (`Assets/Synty/InterfaceApocalypseHUD/Sprites/`, font `Fonts/SairaCondensed/Font_Apocalypse_Bold`). Never use or nest its prefabs: they carry Synty's sample scripts and animators. Our widgets are our own prefabs under `_Core/UI/Prefabs/`.
- **Views are passive.** A widget (`StatBar`, `HotbarSlotView`) only displays the values it's given. A presenter (`PlayerVitalsHud`, `HotbarHud`) reads game state and feeds the widgets. Widgets never know about the player, items, stats or events.
- **Looks live in style assets, layout lives in prefabs.** Sprites, colors and fonts go in a `*Style` ScriptableObject (`Data/UI/`). Size, position and anchors stay on the prefab's RectTransforms.
- **HUD images are not raycast targets** and the HUD canvas has no `GraphicRaycaster`, because nothing on it is clickable. Menus that need clicks get their own canvas with a raycaster.
- **Zero allocations per frame.** Widgets skip values that didn't change. Labels use `TMP_Text.SetText("{0}", value)`, never string concatenation.

## Canvas and scaling

`_Core/UI/Prefabs/HUD.prefab`:

| Setting | Value | Why |
| --- | --- | --- |
| Render mode | Screen Space – Overlay, sort order 10 | Drawn over everything, needs no camera reference |
| Canvas Scaler | Scale With Screen Size, **1920×1080**, Match Width/Height **0.5** | 1080p is the most common Steam resolution, so art is authored at 1:1. 0.5 balances 16:10 (Steam Deck), 16:9 and 21:9 ultrawide without the HUD growing too much on wide screens |
| SafeFrame | Stretch, inset 48 px left/right, 40 px top/bottom | Keeps widgets off the screen edge. Change the inset in one place |

```text
HUD                          Canvas · CanvasScaler
└─ SafeFrame                 stretch, inset 48/40
   ├─ TopLeft                corner anchors (zero size): widgets anchor to these
   ├─ TopRight
   ├─ BottomLeft
   │  └─ Vitals              nested Canvas · VerticalLayoutGroup · ContentSizeFitter · PlayerVitalsHud
   │     ├─ HealthBar        StatBar (HealthBarStyle)
   │     └─ StaminaBar       StatBar (StaminaBarStyle)
   ├─ BottomCenter
   │  └─ Hotbar              nested Canvas · HorizontalLayoutGroup · ContentSizeFitter · HotbarHud
   │     └─ Slot1…SlotN      HotbarSlot.prefab, spawned at Start, one per hotbar slot
   ├─ BottomRight
   └─ Center
      ├─ Crosshair           6×6 dot (built-in Knob sprite, white 90 %, thin dark outline); placeholder, no script
      └─ Interact            nested Canvas · InteractPromptHud, 120 px below the centre
         └─ InteractPrompt   InteractPrompt.prefab (hidden while nothing is focused)
```

- **Region per corner.** New HUD info goes under the matching region: quest tracker in TopRight, currency in TopLeft, crosshair and interact prompt in Center, hotbar in BottomCenter, ammo in BottomRight.
- **A nested Canvas around anything that changes often.** `Vitals` has its own Canvas, so the bars changing every frame (stamina regen) only rebuild that group, not the whole HUD. Give each frequently updated group its own nested Canvas. Static decoration can share the root.

## Where the HUD lives

The HUD is **standalone**, not part of the player. It holds info that isn't the player's (time, objectives, currency), and it keeps working through death and respawn, or in a scene with no player.

- **Put `HUD.prefab` in the scene** next to the Player. In World the scene scope will spawn it.
- **`HudScope`** on the HUD root injects every view in it from the scene's `GameLifetimeScope`, or from the root scope if the scene has none. It works the same way as an entity's scope, so the HUD works dropped into any scene.
- **Player views derive from `PlayerHudView`.** Players register with the root `PlayerRegistry`, which publishes `ActivePlayerChanged` (MessagePipe) when the active player changes. The view's `Bind(player)` runs when a player appears and `Unbind(player)` when it leaves or is replaced. Build per-player state in `Bind` (the hotbar spawns its slot views there) and tear it down in `Unbind`.
- **Views about anything else** are plain MonoBehaviours that `[Inject]` their service (time, objectives) or subscribe to its MessagePipe events.

```text
PlayerRegistry (root) ── ActivePlayerChanged ──> PlayerHudView.Rebind → Unbind(old) / Bind(new)
   ▲ Register / Unregister                         ├─ PlayerVitalsHud   polls the bound player in LateUpdate
Player (Construct / OnDestroy)                    └─ HotbarHud         spawns slot views, listens to Hotbar / Equipment

## StatBar widget

`_Core/UI/Prefabs/Widgets/StatBar.prefab`, 368×41 at 1080p (label 28 pt):

```text
StatBar                      StatBar component
├─ IconFrame                 41×41, left; hidden when the style has no icon
│  └─ Icon                   preserve aspect
└─ Bar                       rest of the width
   ├─ Background
   ├─ Trail                  Filled, horizontal; lags behind drops (optional)
   ├─ Fill                   Filled, horizontal
   └─ Label                  TMP, current value rounded up
```

API, called by a presenter:

- `SetValue(current, max)`: updates the fill and label, and is safe to call every frame.
- `SetWarning(bool)`: swaps the fill to the style's `WarningColor`. Stamina uses it while exhausted.
- `ApplyStyle()`: repaints from the style. Runs in `Awake` and on edit (also an Odin button).

The trail (health only) holds for `TrailDelay`, then slides to the new value at `TrailSpeed`. The component's `Update` runs only while the trail is moving.

## Hotbar

`_Core/UI/Prefabs/Widgets/HotbarSlot.prefab`, 84×84 tile + 30×30 key cap at 1080p:

```text
HotbarSlot                   HotbarSlotView component
├─ Tile                      scaled by SelectedScale while selected
│  ├─ Background             9-sliced box; dimmer when empty
│  ├─ Icon                   item icon (ItemDefinition.Icon), preserve aspect; hidden when empty
│  ├─ Frame                  selection outline, only on the selected slot
│  └─ Ammo                   TMP, bottom-right: "30/90" (magazine / reserve) for guns; hidden for other items
└─ KeyCap                    key sprite + TMP label ("1", "2"… from the input binding)
```

- `HotbarSlotView` API: `SetKey(string)`, `SetIcon(Sprite)` (null = empty), `SetSelected(bool)`, `SetAmmo(rounds, reserve, infiniteReserve, warning)`, `HideAmmo()`, `ApplyStyle()`.
- `HotbarHud` (presenter on `Hotbar`) spawns one view per hotbar slot, so the HUD follows `EquipmentConfig` (up to 9). It's **event-driven**: `Hotbar.SlotChanged` → icon, `Equipment.SelectionChanged` → highlight, `GunInstance.Changed` / `AmmoReserve.Changed` → ammo counts. Nothing is polled.
- **Ammo count** (as in Escape from Duckov): magazine / reserve of that gun's ammo type, magazine only when the reserve is infinite, `AmmoWarningColor` when the magazine is empty or the gun is jammed.
- The highlight moves as soon as a slot is selected, not when the switch animation finishes.
- Behavior and switching rules: [EQUIPMENT.md](EQUIPMENT.md).

## Interact prompt

`_Core/UI/Prefabs/Widgets/InteractPrompt.prefab`: "[E] Pick up Rifle".

```text
InteractPrompt               InteractPromptView component
└─ Content                   HorizontalLayoutGroup + ContentSizeFitter: grows with the text; shown / hidden as a whole
   ├─ KeyCap                 36×36 key sprite + TMP label (from the Interact binding)
   └─ Text                   TMP: verb in VerbColor, target name in TargetColor
```

- `InteractPromptView` API: `SetKey(string)`, `Show(verb, target)` (rebuilds the text only when it changes), `Hide()`, `ApplyStyle()`.
- `InteractPromptHud` polls `Player.Interactor` in `LateUpdate`, because the focus changes as you look around.
- The text comes from the focused `IInteractable` ([EQUIPMENT.md → Pickup, swap and drop](EQUIPMENT.md#pickup-swap-and-drop)).

## Changing the look

| To change | Edit |
| --- | --- |
| Sprites, colors, font, font size, trail, label on/off | `Data/UI/HealthBarStyle.asset` / `StaminaBarStyle.asset`. Bars in the open scene repaint right away; everything else picks the change up in `Awake` |
| A new look for one bar | Duplicate a style asset and assign it to that bar's `StatBar._style` |
| Bar size, icon size, padding | `StatBar.prefab` RectTransforms. Every bar updates |
| Hotbar sprites, colors, key font, selected scale, ammo font / size / colors | `Data/UI/HotbarSlotStyle.asset` |
| Hotbar slot size, key cap size | `HotbarSlot.prefab` RectTransforms. Outline / box thickness: the images' Pixels Per Unit Multiplier |
| Spacing between hotbar slots | `Hotbar` → HorizontalLayoutGroup spacing |
| Item icons | the item's `Data/Items/*.asset` → `Icon` |
| Interact prompt sprite, fonts, colors, font size | `Data/UI/InteractPromptStyle.asset` |
| Interact prompt position | `HUD` → `Center/Interact` anchored position |
| Crosshair size, color | `HUD` → `Center/Crosshair` (RectTransform size, Image color, Outline) |
| Spacing between bars | `Vitals` → VerticalLayoutGroup spacing |
| Distance from the screen edge | `HUD` → `SafeFrame` offsets |
| Overall HUD scale | `HUD` → CanvasScaler reference resolution / match |

- Bar instances in the HUD must not override their RectTransform size. If one does, right-click Size → Revert, or it stops following `StatBar.prefab`.
- A style sprite left empty draws a flat color rectangle.
- Fill and Trail are always `Filled` images. A 9-sliced sprite will show stretched on them, so use sprites with a similar aspect ratio, or switch the widget to an anchor-based fill if sliced art is ever needed.
- A sprite set to `None` on the icon hides the icon frame, and the bar keeps its width.

## Adding HUD info

1. **A new meter** (hunger, thirst, oxygen): make a style asset in `Data/UI/`, drop a `StatBar.prefab` instance under `Vitals` (the layout group stacks it), assign the style, and feed it from a presenter.
2. **A new widget type** (counter, objective line, prompt): add a passive view under `_Core/UI/Widgets/` with a `*Style` asset if it has art, make its prefab in `Prefabs/Widgets/`, and place it in the right region.
3. **New data to show:** add a presenter under `_Core/UI/Hud/` that injects its source (a service; for player data derive from `PlayerHudView`) and pushes values into widgets. Use polling for values that change continuously. For rare changes (currency, objectives), subscribe to MessagePipe events.
4. Add the scripts to [CODE_STRUCTURE.md](CODE_STRUCTURE.md).


## Repair QTE

HUD.prefab has a nested Canvas at SafeFrame/Center/RepairQte. Its centre anchor is offset (-80, -60) at the 1920x1080 reference resolution, placing a compact widget beside the gun near the screen centre. The circle is 112x112; the bar is 210x20. A small R key cap sits below the selected view. The panel, title, hint and remaining-time strip are disabled. RepairQteHud shows Content only while a QTE is active; there is no initial prompt or result popup.

Circle and bar are TimingQteView components behind QteView. Their IDs match the challenge's Presentations list; leave one ID to force that view. The fill/marker show cycle progress and the green segment shows the success interval. A miss changes the fill color until the next pass. The QTE stays visible while repair continues, then closes immediately on completion/cancellation. Ignoring it has no effect on repair timing.

RepairQteStyle.asset owns Synty Apocalypse ring/bar art, the Interface Core key cap, colors and the first-party RepairQteFont.asset copy. The copy owns its material, atlas and generated glyph data. Layout stays in the HUD prefab. Graphics are not raycast targets; InputService owns the Reload confirmation input. Vendor prefabs and sample scripts are not used. Rules and tuning are in WEAPONS.md.
