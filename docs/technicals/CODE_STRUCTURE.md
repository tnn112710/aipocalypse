# Code structure

Map of the scripts that exist today: where each one lives, what it owns, and how they connect. The *why* (layers, scopes, Core vs Features) is in [ARCHITECTURE.md](ARCHITECTURE.md). **Update this file whenever you add, move or remove a script.**

## Assemblies and namespaces

| Assembly | Folder | References |
| --- | --- | --- |
| `Aipocalypse.Core` | `Assets/_Game/_Core/` | VContainer, MessagePipe (+ .VContainer), UniTask, Unity.InputSystem, Unity.Cinemachine, UnityEngine.UI, Unity.TextMeshPro; precompiled DLLs (Odin) auto-referenced |
| `Aipocalypse.Tests.EditMode` | `Assets/_Game/Tests/EditMode/` | `Aipocalypse.Core`, VContainer, test runner, NUnit. Editor only |

Namespaces follow the folder: `Aipocalypse.Core.<Folder>` (e.g. `Aipocalypse.Core.PlayerSystem`, `Aipocalypse.Core.Combat`). One exception: input uses **`Aipocalypse.Input`**, because a namespace ending in `InputSystem` shadows `UnityEngine.InputSystem.InputSystem`.

## Script map

```text
Assets/_Game/
├─ _Core/                                   asmdef Aipocalypse.Core
│  ├─ _Bootstrap/                           Aipocalypse.Core.Bootstrap
│  │  ├─ ProjectLifetimeScope.cs            root scope: MessagePipe, InputInstaller
│  │  ├─ ProjectLifetimeScope.prefab        holds ControlSettings (mouse sensitivity, invert Y…)
│  │  ├─ VContainerSettings.asset           Preloaded Asset → spawns the root prefab before any scene
│  │  └─ GameLifetimeScope.cs               per-scene scope (stub), Strict flag, execution order -5000
│  ├─ _Common/
│  │  ├─ Entities/EntityScope.cs            Aipocalypse.Core.Entities — base child scope for unique entities, RegisterDefault
│  │  ├─ Combat/                            Aipocalypse.Core.Combat
│  │  │  ├─ IDamageable.cs                  anything that can take damage
│  │  │  ├─ DamageInfo.cs                   DamageInfo struct + DamageType enum
│  │  │  └─ Health.cs                       plain HP meter: Damage / Heal / Restore
│  │  ├─ Events/CombatEvents.cs             Aipocalypse.Core.Events — DamageDealt, PlayerDied
│  │  └─ Stats/IStatProvider.cs             Aipocalypse.Core.Stats — StatId enum + stat seam
│  ├─ EquipmentSystem/                      Aipocalypse.Core.EquipmentSystem — rules and how-tos in EQUIPMENT.md
│  │  ├─ Items/ItemCategory.cs              [Flags] Gun, Melee: what a hotbar slot accepts
│  │  ├─ Items/ItemDefinition.cs            abstract SO: name, icon, Category, CreateInstance
│  │  ├─ Items/EquippableDefinition.cs      abstract SO: held prefab, equip / holster time
│  │  ├─ Items/ItemInstance.cs              one owned item (per-copy state goes in subclasses)
│  │  ├─ Hotbar.cs                          ≤ 9 slots, category check, SlotChanged, FindNext
│  │  ├─ EquipmentSwitcher.cs               holster → swap → equip timing (Raise 0..1, SwitchPhase)
│  │  ├─ Equipment.cs                       selected slot ↔ switcher target; Select / Cycle
│  │  ├─ EquipmentConfig.cs                 SO: SwitchSpeed, slots (accepts, starting item) → CreateHotbar
│  │  ├─ HeldItem.cs                        root of an item's first-person prefab; Show / Hide / OnReady hooks
│  │  └─ EquipmentRig.cs                    spawns HeldItems, shows the held one, temporary lower/raise pose
│  ├─ InputSystem/                          Aipocalypse.Input
│  │  ├─ InputService.cs                    the only owner of actions, map switching, cursor lock; hotbar keys + cycling
│  │  ├─ InputInstaller.cs                  registers ControlSettings + InputService (entry point)
│  │  ├─ ControlSettings.cs                 player-facing control options
│  │  └─ GameInputActions.cs                GENERATED from Settings/InputSystem_Actions.inputactions — don't edit
│  ├─ PlayerSystem/                         Aipocalypse.Core.PlayerSystem
│  │  ├─ Player.cs                          view MonoBehaviour: input → intent, ticks the parts, IDamageable
│  │  ├─ PlayerScope.cs                     EntityScope on the prefab root; default IStatProvider, registers its Player
│  │  ├─ PlayerIntent.cs                    per-frame wish: Move, LookDegrees, Sprint, Jump, SelectSlot, CycleSlot
│  │  ├─ PlayerMotor.cs                     CharacterController movement, jump, slopes, landing (+ LandingInfo)
│  │  ├─ PlayerLook.cs                      yaw on body, pitch on camera pivot
│  │  ├─ PlayerHeadBob.cs                   footstep bob + landing dip on the camera root
│  │  ├─ Stamina.cs                         stamina meter: spend, regen delay, exhaustion
│  │  ├─ FallDamage.cs                      drop height → damage
│  │  ├─ PlayerTuning.cs                    ScriptableObject type for every player number
│  │  ├─ Defaults/BaseStats.cs              IStatProvider default: returns base values
│  │  └─ Prefabs/Player.prefab              the player entity (see below)
│  ├─ UI/                                   Aipocalypse.Core.UI — rules and how-tos in UI.md
│  │  ├─ Widgets/StatBar.cs                 passive meter view: SetValue / SetWarning / ApplyStyle, optional trail
│  │  ├─ Widgets/StatBarStyle.cs            ScriptableObject: sprites, colors, font for a StatBar
│  │  ├─ Widgets/HotbarSlotView.cs          passive slot view: SetKey / SetIcon / SetSelected / ApplyStyle
│  │  ├─ Widgets/HotbarSlotStyle.cs         ScriptableObject: tile, frame, key cap sprites + colors, key font
│  │  ├─ Hud/PlayerVitalsHud.cs             presenter: injected Player → health + stamina bars (LateUpdate)
│  │  ├─ Hud/HotbarHud.cs                   presenter: Player.Equipment → one slot view per slot (events, no polling)
│  │  ├─ Prefabs/HUD.prefab                 overlay canvas 1920×1080, SafeFrame + corner regions (nested in Player)
│  │  ├─ Prefabs/Widgets/StatBar.prefab     the bar widget
│  │  └─ Prefabs/Widgets/HotbarSlot.prefab  the hotbar slot widget
│  └─ WeaponSystem/                         Aipocalypse.Core.WeaponSystem
│     ├─ GunDefinition.cs                   EquippableDefinition, Category Gun (gun data lands here with shooting)
│     ├─ MeleeDefinition.cs                 EquippableDefinition, Category Melee
│     └─ Prefabs/DummyRifle, DummyPistol, DummyFireAxe.prefab   held prefabs: HeldItem root + store model
├─ Data/
│  ├─ Player/PlayerTuning.asset             the tuning values used in every scene
│  ├─ Equipment/EquipmentConfig.asset       3 slots: Primary (Gun) Rifle, Secondary (Gun) Pistol, Melee (Melee) Fire Axe
│  ├─ Items/Rifle, Pistol, FireAxe.asset     item definitions
│  └─ UI/HealthBarStyle.asset, StaminaBarStyle.asset, HotbarSlotStyle.asset
└─ Tests/EditMode/                          asmdef Aipocalypse.Tests.EditMode
   ├─ PlayerRulesTests.cs                   Stamina, Health, FallDamage, directional speed
   └─ EquipmentTests.cs                     Hotbar, EquipmentSwitcher timing, Equipment select / cycle
```

## Who depends on whom

```text
ProjectLifetimeScope (root) ──registers──> InputService, ControlSettings, MessagePipe brokers
                                                │
PlayerScope : EntityScope ── parent: GameLifetimeScope if the scene has one, else root
   │ registers default IStatProvider → BaseStats (only if no ancestor provides one)
   │ registers the Player component as an instance (for HUD views)
   └─ injects the Player GameObject (whole hierarchy, HUD included)
         Player.Construct(InputService, IStatProvider, IPublisher<DamageDealt>, IPublisher<PlayerDied>)
           creates (plain C#, owned by Player):
             Health, Stamina, PlayerMotor(CharacterController, tuning, stats, stamina),
             PlayerLook(body, cameraPitch), PlayerHeadBob(cameraRoot),
             Equipment(EquipmentConfig.CreateHotbar()) → EquipmentRig.Bind(Equipment)
         PlayerVitalsHud.Construct(Player)            (in the nested HUD)
         HotbarHud.Construct(Player, InputService)    (in the nested HUD; builds its slots in Start)
```

- Only `Player` gets injected. The parts are plain classes it creates and owns. They get Unity components through their constructors, not through DI.
- `PlayerSystem` → `EquipmentSystem` ← `WeaponSystem`: the player owns an `Equipment`; weapon kinds derive from equipment's definitions. `EquipmentSystem` knows neither the player nor weapons.
- Inside the player system, plain C# events are used (`PlayerMotor.Landed` / `Jumped`). Anything crossing systems goes through MessagePipe (`DamageDealt`, `PlayerDied`).

## Frame order (Player.Update)

```text
intent = dead or gameplay input disabled ? default : read InputService
PlayerLook.Tick      yaw + pitch                    (movement uses the new facing this frame)
PlayerMotor.Tick     velocity, jump, CharacterController.Move, landing → Landed event
                       └─ Player.OnLanded: head-bob dip + fall damage → Health → DamageDealt / PlayerDied
Stamina.Tick         regen (spending happened inside the motor)
PlayerHeadBob.Tick   camera root offset
Equipment            intent.SelectSlot / CycleSlot → Select / Cycle; EquipmentSwitcher.Tick (holster → swap → equip)
                       └─ HeldChanged → EquipmentRig shows/hides models; SelectionChanged → HotbarHud highlight
EquipmentRig.Tick    hand pose from Raise
LateUpdate           CinemachineBrain moves PlayerCamera to CameraPitch
                     PlayerVitalsHud reads Health / Stamina → StatBar.SetValue (skips unchanged values)
StatBar.Update       only while a trail is catching up, otherwise disabled
```

## Player prefab

```text
Player                       CharacterController (h 1.8, r 0.35, step 0.35, slope 45°) · Player · PlayerScope
├─ CameraRoot                y 1.65 (eye height); PlayerHeadBob offsets it
│  └─ CameraPitch            PlayerLook pitches it; Cinemachine tracking target
│     └─ HeldItems           EquipmentRig
│        └─ Hand             (0.17, -0.21, 0.62); held prefabs spawn here, posed by the rig
├─ PlayerCamera              Camera (tag MainCamera) · URP camera data · CinemachineBrain · AudioListener
├─ PlayerCinemachineCamera   CinemachineCamera (FOV 70) · HardLockToTarget · RotateWithFollowTarget
└─ HUD                       nested HUD.prefab (overlay canvas; hierarchy in UI.md)
```

## Adding a script

- **New system:** folder under `_Core/` (or `Features/` / `_Experimental/`), namespace `Aipocalypse.Core.<System>`, one `<System>Installer : IInstaller` registered from the right scope. Add it to the map above.
- **Rules go in plain C#** (testable, EditMode tests in `Tests/EditMode/`). MonoBehaviours are views: they wire components and forward to the rules.
- **Numbers go in a ScriptableObject** type next to the system, with the asset in `Data/<System>/`.
- **New `*LifetimeScope.cs`:** `DisableScriptModifier` is on, but check that VContainer didn't replace the file with its template.
- **Generated code** (`GameInputActions.cs`): change the `.inputactions` asset, never the file.
