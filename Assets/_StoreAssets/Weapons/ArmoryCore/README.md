# ArmoryCore: Low Poly Weapon System

ArmoryCore is a Unity weapon system with low-poly models, projectile ballistics, configurable weapon mechanics, and interchangeable attachments. The showcase scene lets you browse the weapons, take them to a firing range, and try different attachment combinations.

Weapon, ammunition, and attachment settings are stored in ScriptableObjects. The weapon controller handles firing, ammunition, reloading, heat, malfunctions, and presentation. You can use its keyboard and mouse controls or connect it to your own player and AI scripts.

## Package contents

| Content | Count |
| --- | ---: |
| Weapon prefabs and weapon definitions | 191 each |
| Attachment prefabs and attachment definitions | 59 each |
| Ammunition definitions | 29 |
| FBX model files | 244 |
| PNG textures | 243 |
| Weapon sounds (shots, suppressed shots, reloads, dry fire) | 299 |

The package also contains a showcase scene with a firing range, a first-person weapon rig, custom inspectors, damage components, and projectile simulation scripts.

| Folder | Contents |
| --- | --- |
| `Audio/Weapons` | Weapon sounds grouped by weapon category, with shared sounds in `Common`. |
| `Models` | Weapon and attachment meshes with their textures. |
| `Materials` | Materials used by the models. |
| `Prefabs/Weapons` | Weapon prefabs with controllers and attachment slots. |
| `Prefabs/Modules` | Sights, barrel attachments, grips, stocks, and other modules. |
| `Definitions/Weapons` | Weapon mechanics and handling settings. |
| `Definitions/Ammunition` | Projectile and ballistic settings. |
| `Definitions/Attachments` | Attachment compatibility and stat modifiers. |
| `Scripts` | Runtime components. |
| `Editor` | Inspectors for controllers, definitions, and modules. |
| `Showcase` | Demonstration scene, targets, and scene materials. |

## Project setup

The supplied project uses Unity 6.3 (`6000.3.0f1`) and Universal Render Pipeline 17.3. Use URP to display the supplied materials and showcase shader.

Input scripts support the Unity Input System and the legacy Input Manager. They use the Input System when `ENABLE_INPUT_SYSTEM` is defined and the legacy API otherwise. Targets and scene surfaces need 3D colliders for projectile hits.

## Try the showcase

Open `Showcase/WeaponShowcase.unity` and enter Play mode. Point at a weapon card and press **E** to equip it and enter the firing range. Press **E** or **Escape** to return to the gallery.

### Gallery controls

| Input | Action |
| --- | --- |
| W / A / S / D | Move the camera. |
| Space / Ctrl | Move up or down. |
| Shift | Move faster. |
| Hold right mouse button and move the mouse | Look around. |
| Mouse wheel | Adjust the camera field of view. |
| R | Reset the camera position and field of view. |
| E | Equip the weapon under the pointer. |

### Firing range controls

| Input | Action |
| --- | --- |
| W / A / S / D | Walk. |
| Shift | Sprint. |
| Space | Jump. |
| Left mouse button | Fire. |
| Right mouse button | Aim. |
| R | Reload. |
| B | Cycle fire mode. |
| C | Cycle a manual or pump action. |
| T | Clear a malfunction. |
| F | Toggle the flashlight. |
| Z | Toggle the bipod, or the stock when no bipod is available. |
| Mouse wheel | Change sight magnification. |
| 1 / 2 / 3 / 4 | Cycle sights, barrel attachments, grips, and stocks. |
| E / Escape | Put the weapon back and return to the gallery. |

Available fire modes and attachment actions depend on the selected weapon and its installed modules.

## Add a weapon to your scene

1. Drag a prefab from `Prefabs/Weapons` into the scene and place it under your weapon holder.
2. Select its `ArmoryCoreWeaponController`. On the **Weapon** tab, check the assigned **Definition** and **Muzzle**.
3. Position the muzzle at the barrel opening. Its forward axis determines the shot direction; the inspector draws an orange direction arrow in the Scene view.
4. On the **Control** tab, assign **Aim Camera**, or enable **Find Main Camera Automatically** to use the main camera.
5. Enable **Read Local Player Input** to use the built-in weapon controls. For your own input system, leave it disabled and call the controller methods described below.
6. Set the hit mask to the layers projectiles should hit. Give targets and surfaces colliders.
7. Configure presentation on the **Effects** tab, then enter Play mode.

The controller inspector has five tabs: **Weapon**, **Control**, **Effects**, **Mods**, and **Events**. You can edit the assigned weapon definition directly in the Weapon tab. During Play mode, the inspector displays ammunition, effective stats, heat, durability, fouling, and weapon state.

## Weapon settings

Create a definition with **Assets > Create > ArmoryCore Weapons > Weapon Definition**, or duplicate an existing definition to make a variant. Definitions are shared assets: editing one changes the settings used by every controller that references it.

The definition inspector groups settings into **Setup**, **Ballistics**, **Handling**, and **Systems**.

| Group | Settings |
| --- | --- |
| Identity | ID, display name, and weapon tags. |
| Mechanism | Closed-bolt, open-bolt, manual, pump, or break action; fire modes, rate of fire, burst size, and action cycle time. |
| Feeding | Ammunition definition, magazine and chamber capacity, and starting load. |
| Shot | Projectile count, damage per projectile, velocity multiplier, and accuracy. |
| Recoil | Vertical and horizontal recoil, randomness, recovery speed, and recovery delay. |
| Handling | Weapon mass, aim time, tactical reload time, empty reload time, and time per inserted round. |
| Reliability | Mean rounds between failures, heat gain and cooling, durability loss, and fouling. |
| Compatibility | Allowed attachment IDs and weapon tags. |

Fire modes are safe, semi-automatic, burst, and automatic. Reload types cover detachable magazines, internal magazines, single rounds, and belt-fed weapons. Capacity and chamber settings determine loaded ammunition; the controller stores reserve ammunition separately.

Accuracy is specified in minutes of angle. Hip fire, movement, and heat apply spread modifiers. Recoil values are measured in degrees. Reload and aim times are measured in seconds.

Firing increases heat and fouling and reduces durability according to the definition. These values affect reliability. Use `StartClearingMalfunction()` to begin clearing a stoppage and `ServiceWeapon()` to restore condition.

## Ammunition and ballistics

Create ammunition with **Assets > Create > ArmoryCore Weapons > Ammunition Definition** and assign it to a weapon definition.

An ammunition definition controls projectile mass, diameter, muzzle velocity, aerodynamic drag, gravity, maximum range, and flight time. It also defines minimum lethal velocity, retained damage at that velocity, penetration multiplier, and ricochet energy retention.

`ArmoryCoreBallisticProjectile` advances projectiles in segments and raycasts along their path. Gravity, drag, and wind affect flight. Add `ArmoryCoreBallisticsEnvironment` to the scene to set wind and air density.

**Maximum Segment Length Meters** and **Maximum Substeps Per Frame** control simulation detail. Shorter segments require more work; set the substep budget to suit projectile speed and the scene's collision detail.

### Rockets

Set an ammunition definition's **Projectile Type** to **Rocket** to fire it with `ArmoryCoreRocketProjectile` instead of the bullet simulation. The Ordan RG7, RG18, RG28, and RG84 launchers use rocket ammunition.

A rocket leaves the launcher at the ammunition's muzzle velocity, then its sustainer motor ignites after **Motor Ignition Delay** and accelerates it for **Motor Burn Time** up to **Maximum Rocket Speed**. Drag, gravity, and wind still apply. The rocket is drawn as a body with a motor flame and a smoke trail.

The warhead arms after **Arming Distance Meters**. An armed rocket explodes on impact: every target within **Explosion Radius Meters** that is not shielded by other geometry receives one hit, with damage falling from **Explosion Damage** at the centre to **Explosion Damage At Edge** at the rim. The directly hit target also receives the weapon's damage per projectile. Rigidbodies are pushed by **Explosion Force**. Explosion hits have `ArmoryCoreBallisticHit.IsExplosion` set. A rocket that hits something before arming does not explode and only deals the weapon's damage. With **Explode On Timeout**, a rocket that reaches its maximum flight time or range detonates in the air.

### Damage and surface response

Add `ArmoryCoreHealth` to a target to receive damage, track health, and expose damaged and killed events. To connect another health system, implement `IArmoryCoreBallisticTarget`:

```csharp
using ArmoryCore;
using UnityEngine;

public sealed class DamageReceiver : MonoBehaviour, IArmoryCoreBallisticTarget
{
    [SerializeField] private float health = 100f;

    public void ReceiveBallisticHit(ArmoryCoreBallisticHit hit)
    {
        health = Mathf.Max(0f, health - hit.Damage);
    }
}
```

`ArmoryCoreBallisticHit` provides the hit point, normal, projectile velocity, kinetic energy, damage, travel distance, ammunition definition, shooter, and collider.

Use `ArmoryCoreBallisticMaterial` on hit surfaces to set thickness, penetration energy cost, ricochet angle and chance, and received-damage multiplier.

## Attachments

Each attachment consists of a definition and a prefab component. Create a definition with **Assets > Create > ArmoryCore Weapons > Attachment Definition**. Choose its slot and set its modifiers and compatibility requirements.

Supported slots are **Sights**, **Barrel**, **Grip**, **Stock**, **Magazine**, and **Generic**. Weapon prefabs group their selectable options under matching roots:

```text
Weapon Root [ArmoryCoreWeaponController]
  Muzzle
  Sights_All
    Sight option A
    Sight option B
  Barrel_All
  Grip_All
  Stock_All
  Magazine_All
  Generic_All
```

Each direct child of a slot root is an option. Add `ArmoryCoreAttachmentModule` or a specialized module component to it, assign its definition, and activate the starting option.

An attachment must satisfy the weapon's **Compatible Attachment IDs** list when that list is populated. Every entry in the attachment's **Required Weapon Tags** must also be present on the weapon.

Definitions can change mass, recoil, spread, muzzle velocity, reload time, aim time, reliability, sound pressure, and muzzle flash. A multiplier of `1` leaves a value unchanged; `0.8` reduces it by 20%. For aim and reload times, a smaller multiplier means faster handling.

| Component | Purpose |
| --- | --- |
| `ArmoryCoreAttachmentModule` | Base component for attachment definitions and custom module behavior. |
| `ArmoryCoreSightModule` | Sight alignment, magnification steps, and zero distance. |
| `ArmoryCoreSuppressorModule` | Muzzle position and sound, flash, and recoil overrides. |
| `ArmoryCoreGripModule` | Grip modifiers and deployable support behavior. |
| `ArmoryCoreStockModule` | Folded and unfolded stock handling. |
| `ArmoryCoreFlashlightModule` | Control of attached lights. |

For a barrel module, assign **Muzzle Point** when it should replace the weapon's firing origin. For a sight, position **Aim Point** along the sight line and orient its forward axis toward the target. **Compensate Sight Zero** on the controller adjusts aiming for the sight's zero distance.

Use the **Mods** inspector tab or `SelectAttachment(slot, attachmentId)` to change attachments. After changing child activity directly from another script, call `RebuildInstalledAttachments()` to update installed modules and effective stats.

## First-person presentation

`ArmoryCoreFirstPersonWeaponRig` handles aiming and sprint poses, movement bob, look sway, recoil, and lowering the weapon near obstacles. It also handles procedural magazine and action-part movement for models with recognized parts. The showcase creates and configures this rig when you equip a weapon.

The controller's **Effects** tab connects muzzle particles, a muzzle light, casing ejection, visual recoil, an Animator, and an AudioSource. Set casing position, force, torque, and lifetime on the controller. Assign firing, suppressed firing, reload, and empty-click clips in the weapon definition, with volume, pitch variation, and distance settings.

Weapon sounds are stored in `Audio/Weapons/<Category>` and named `<Weapon>-Shot`, `<Weapon>-Shot-Suppressed`, and `<Weapon>-Reload` after the weapon that owns them; several weapons may share one set. The dry-fire click is `Audio/Weapons/Common/Dry-Fire`. Suppressor modules adjust playback volume through their sound-pressure settings.

For Animator integration, the default parameters are:

| Type | Names |
| --- | --- |
| Trigger | `Fire`, `Reload`, `Empty`, `Cycle`, `Malfunction` |
| Bool | `Aiming` |
| Int | `FireMode`: 0 = safe, 1 = semi-automatic, 2 = burst, 3 = automatic |

Parameter names can be changed in the controller inspector.

## Input and scripting

To drive a weapon through your own input layer, disable **Read Local Player Input** and forward input to its controller. For example:

```csharp
using ArmoryCore;
using UnityEngine;

public sealed class PlayerWeaponInput : MonoBehaviour
{
    [SerializeField] private ArmoryCoreWeaponController weapon;

    public void SetFire(bool held) => weapon.SetTriggerHeld(held);
    public void SetAim(bool held) => weapon.SetAiming(held);
    public void Reload() => weapon.StartReload();
    public void CycleFireMode() => weapon.CycleFireMode();
    public void ClearMalfunction() => weapon.StartClearingMalfunction();
}
```

Pass both press and release states to `SetTriggerHeld`. Use `SetMovementAmount` with a value from 0 to 1 to apply movement spread, and `SetReserveRounds` to synchronize ammunition with your inventory.

| Method | Use |
| --- | --- |
| `TryFire()` / `TryFire(worldDirection)` | Attempt one shot and return its outcome. |
| `SetFireMode(mode)` / `CycleFireMode()` | Select a supported fire mode. |
| `StartReload()` / `CancelReload()` | Begin or interrupt reloading. |
| `CycleAction()` | Operate a manual or pump action. |
| `StartClearingMalfunction()` | Begin clearing the current malfunction. |
| `ServiceWeapon()` | Restore durability, remove fouling, and clear the malfunction. |
| `SelectAttachment(slot, id)` / `ClearAttachmentSlot(slot)` | Change the installed configuration. |
| `ToggleFlashlight()` / `ToggleBipod()` / `ToggleStock()` | Operate active modules. |
| `CycleSightMagnification(direction)` | Step through the active sight's magnifications. |

### Events

The **Events** tab exposes UnityEvents for firing, empty attempts, reload start and completion, malfunctions and clearing, ammunition changes, and aiming changes.

C# subscribers can use `ShotAttempted` for shot outcomes, `RecoilApplied` for recoil feedback, and `ConfigurationChanged` when attachment configuration is rebuilt. These events can connect the weapon to a HUD, camera controller, or other game systems.

## Common setup issues

**The shot starts in the wrong place.** Check the weapon muzzle and the active barrel module's muzzle override. Position the transform at the barrel opening and align its forward axis with the intended shot direction.

**An attachment does not become active.** Check its slot root, module definition, allowed ID, and required tags. After changing hierarchy activity through your own code, rebuild installed attachments.

**A sight is misaligned.** Check its Aim Point position and forward axis, the assigned Aim Camera, and the sight zero setting.

**The weapon does not respond to input.** Enable Read Local Player Input for the local weapon or call the public input methods. Check the selected fire mode, loaded ammunition, and malfunction state in the runtime inspector.

**Projectiles pass through a target.** Check the target's 3D collider, the controller's hit mask, and the ammunition's simulation settings.
