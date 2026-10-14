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
│  │  ├─ ProjectLifetimeScope.cs            root scope: MessagePipe, InputInstaller, PlayerInstaller
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
│  │  ├─ Events/PlayerEvents.cs             ActivePlayerChanged (read PlayerRegistry.Current)
│  │  ├─ Interaction/IInteractable.cs       Aipocalypse.Core.Interaction — TryGetPrompt / Interact(actor) + InteractionPrompt
│  │  └─ Stats/IStatProvider.cs             Aipocalypse.Core.Stats — StatId enum + stat seam
│  ├─ EquipmentSystem/                      Aipocalypse.Core.EquipmentSystem — rules and how-tos in EQUIPMENT.md
│  │  ├─ Items/ItemCategory.cs              [Flags] Gun, Melee: what a hotbar slot accepts
│  │  ├─ Items/ItemDefinition.cs            abstract SO: name, icon, Category, WorldPrefab, CreateInstance
│  │  ├─ Items/EquippableDefinition.cs      abstract SO: held prefab, equip / holster time
│  │  ├─ Items/ItemInstance.cs              one owned item (per-copy state goes in subclasses)
│  │  ├─ Hotbar.cs                          ≤ 9 slots, category check, SlotChanged, FindNext, FindFreeSlot
│  │  ├─ EquipmentSwitcher.cs               holster → swap → equip timing (Raise 0..1, SwitchPhase)
│  │  ├─ Equipment.cs                       selected slot ↔ switcher target; Select / Cycle; pick up (store / swap), DropSelected
│  │  ├─ PickUpAction.cs                    None / Store / Swap
│  │  ├─ IEquipmentOwner.cs                 on an actor's root: exposes its Equipment to world items
│  │  ├─ WorldItem.cs                       item lying in the world: IInteractable, picks up / swaps; Spawn for drops
│  │  ├─ EquipmentConfig.cs                 SO: SwitchSpeed, slots (accepts, starting item) → CreateHotbar
│  │  ├─ HeldItem.cs                        root of an item's first-person prefab; Show / Hide / OnReady hooks
│  │  └─ EquipmentRig.cs                    spawns HeldItems, shows the held one, temporary lower/raise pose
│  ├─ InputSystem/                          Aipocalypse.Input
│  │  ├─ InputService.cs                    the only owner of actions, map switching, cursor lock; hotbar keys + cycling, interact, drop
│  │  ├─ InputInstaller.cs                  registers ControlSettings + InputService (entry point)
│  │  ├─ ControlSettings.cs                 player-facing control options
│  │  └─ GameInputActions.cs                GENERATED from Settings/InputSystem_Actions.inputactions — don't edit
│  ├─ PlayerSystem/                         Aipocalypse.Core.PlayerSystem
│  │  ├─ Player.cs                          view MonoBehaviour: input → intent, ticks the parts, IDamageable
│  │  ├─ PlayerScope.cs                     EntityScope on the prefab root; default IStatProvider
│  │  ├─ PlayerInstaller.cs                 root-scope services: PlayerRegistry
│  │  ├─ PlayerRegistry.cs                  the active player (Current); players register themselves, publishes ActivePlayerChanged
│  │  ├─ PlayerIntent.cs                    per-frame wish: Move, LookDegrees, Sprint, Jump, SelectSlot, CycleSlot, Interact, Drop
│  │  ├─ PlayerMotor.cs                     CharacterController movement, jump, slopes, landing (+ LandingInfo)
│  │  ├─ PlayerLook.cs                      yaw on body, pitch on camera pivot
│  │  ├─ PlayerHeadBob.cs                   footstep bob + landing dip on the camera root
│  │  ├─ PlayerInteractor.cs                look sphere cast → focused IInteractable + prompt, Interact; drop point
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
│  │  ├─ Widgets/InteractPromptView.cs      passive "[E] Pick up Rifle" view: SetKey / Show / Hide / ApplyStyle
│  │  ├─ Widgets/InteractPromptStyle.cs     ScriptableObject: key cap sprite, fonts, verb / target colors
│  │  ├─ HudScope.cs                        EntityScope on the HUD root: injects the HUD's views in any scene
│  │  ├─ Hud/PlayerHudView.cs               base presenter: follows PlayerRegistry, Bind / Unbind per active player
│  │  ├─ Hud/PlayerVitalsHud.cs             PlayerHudView → health + stamina bars (LateUpdate)
│  │  ├─ Hud/HotbarHud.cs                   PlayerHudView → one slot view per slot of Player.Equipment (events, no polling)
│  │  ├─ Hud/InteractPromptHud.cs           PlayerHudView → prompt from Player.Interactor (LateUpdate)
│  │  ├─ Prefabs/HUD.prefab                 standalone overlay canvas 1920×1080, SafeFrame + regions, HudScope (placed in the scene)
│  │  ├─ Prefabs/Widgets/StatBar.prefab     the bar widget
│  │  ├─ Prefabs/Widgets/HotbarSlot.prefab  the hotbar slot widget
│  │  └─ Prefabs/Widgets/InteractPrompt.prefab  the interact prompt widget
│  └─ WeaponSystem/                         Aipocalypse.Core.WeaponSystem
│     ├─ GunDefinition.cs                   EquippableDefinition, Category Gun (gun data lands here with shooting)
│     ├─ MeleeDefinition.cs                 EquippableDefinition, Category Melee
│     ├─ Prefabs/DummyRifle, DummyPistol, DummyFireAxe.prefab   held prefabs: HeldItem root + store model
│     └─ Prefabs/Dummy*Pickup.prefab        world prefabs: WorldItem + trigger box + model lying flat
├─ Data/
│  ├─ Player/PlayerTuning.asset             the tuning values used in every scene
│  ├─ Equipment/EquipmentConfig.asset       3 slots "Weapon 1–3", each takes Gun | Melee, empty at spawn
│  ├─ Items/Rifle, Pistol, FireAxe.asset     item definitions (held + world prefab)
│  └─ UI/HealthBarStyle.asset, StaminaBarStyle.asset, HotbarSlotStyle.asset, InteractPromptStyle.asset
└─ Tests/EditMode/                          asmdef Aipocalypse.Tests.EditMode
   ├─ PlayerRulesTests.cs                   Stamina, Health, FallDamage, directional speed
   └─ EquipmentTests.cs                     Hotbar, EquipmentSwitcher timing, Equipment select / cycle, pick up / swap / drop
```

## Who depends on whom

```text
ProjectLifetimeScope (root) ──registers──> InputService, ControlSettings, MessagePipe brokers, PlayerRegistry
                                                │
PlayerScope : EntityScope ── parent: GameLifetimeScope if the scene has one, else root
   │ registers default IStatProvider → BaseStats (only if no ancestor provides one)
   └─ injects the Player GameObject (whole hierarchy)
         Player.Construct(InputService, IStatProvider, IPublisher<DamageDealt>, IPublisher<PlayerDied>, PlayerRegistry)
           creates (plain C#, owned by Player):
             Health, Stamina, PlayerMotor(CharacterController, tuning, stats, stamina),
             PlayerLook(body, cameraPitch), PlayerHeadBob(cameraRoot), PlayerInteractor(cameraPitch, gameObject),
             Equipment(EquipmentConfig.CreateHotbar()) → EquipmentRig.Bind(Equipment)
           last: PlayerRegistry.Register(this) → ActivePlayerChanged   (OnDestroy: Unregister)

HudScope : EntityScope (HUD.prefab root, separate from the Player) ── same parent lookup
   └─ injects the HUD GameObject
         PlayerHudView (PlayerRegistry, ISubscriber<ActivePlayerChanged>) → Bind / Unbind the active player
           PlayerVitalsHud, HotbarHud, InteractPromptHud (+ InputService for key labels)
```

- Only `Player` gets injected. The parts are plain classes it creates and owns. They get Unity components through their constructors, not through DI.
- `PlayerSystem` → `EquipmentSystem` ← `WeaponSystem`: the player owns an `Equipment`; weapon kinds derive from equipment's definitions. `EquipmentSystem` knows neither the player nor weapons: `WorldItem` finds the picker's hotbar through `IEquipmentOwner` (Player implements it).
- Interaction: `_Common/Interaction.IInteractable` is the only contract. `PlayerInteractor` finds one with a look cast; the interactable decides what happens (`WorldItem` → `Equipment.TryPickUp`).
- Inside the player system, plain C# events are used (`PlayerMotor.Landed` / `Jumped`). Anything crossing systems goes through MessagePipe (`DamageDealt`, `PlayerDied`).

## Frame order (Player.Update)

```text
intent = dead or gameplay input disabled ? default : read InputService
PlayerLook.Tick      yaw + pitch                    (movement uses the new facing this frame)
PlayerMotor.Tick     velocity, jump, CharacterController.Move, landing → Landed event
                       └─ Player.OnLanded: head-bob dip + fall damage → Health → DamageDealt / PlayerDied
Stamina.Tick         regen (spending happened inside the motor)
PlayerHeadBob.Tick   camera root offset
PlayerInteractor     look cast → focus + prompt; intent.Interact → IInteractable.Interact (pickup / swap)
Equipment            intent.Drop → DropSelected → WorldItem.Spawn at the drop point; else SelectSlot / CycleSlot → Select / Cycle; EquipmentSwitcher.Tick (holster → swap → equip)
                       └─ HeldChanged → EquipmentRig shows/hides models; SelectionChanged → HotbarHud highlight
EquipmentRig.Tick    hand pose from Raise
LateUpdate           CinemachineBrain moves PlayerCamera to CameraPitch
                     PlayerVitalsHud reads Health / Stamina → StatBar.SetValue (skips unchanged values)
                     InteractPromptHud reads Player.Interactor → InteractPromptView
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
└─ PlayerCinemachineCamera   CinemachineCamera (FOV 70) · HardLockToTarget · RotateWithFollowTarget
```

The HUD is not in this prefab: `HUD.prefab` sits next to it in the scene ([UI.md → Where the HUD lives](UI.md#where-the-hud-lives)).

## Adding a script

- **New system:** folder under `_Core/` (or `Features/` / `_Experimental/`), namespace `Aipocalypse.Core.<System>`, one `<System>Installer : IInstaller` registered from the right scope. Add it to the map above.
- **Rules go in plain C#** (testable, EditMode tests in `Tests/EditMode/`). MonoBehaviours are views: they wire components and forward to the rules.
- **Numbers go in a ScriptableObject** type next to the system, with the asset in `Data/<System>/`.
- **New `*LifetimeScope.cs`:** `DisableScriptModifier` is on, but check that VContainer didn't replace the file with its template.
- **Generated code** (`GameInputActions.cs`): change the `.inputactions` asset, never the file.
