# Control Rig Pro - User Guide

## Overview

Control Rig Pro is a comprehensive Unity animation rigging toolkit that simplifies the creation and management of character rigs. It provides intuitive tools for setting up IK/FK controls, physics-based constraints, and procedural animations for both humanoid and non-humanoid characters.

---

## Getting Started

### Quick Start for Humanoid Characters

1. **Select your character** with an Animator component
2. Go to **Tools > Control Rig Pro > Humanoid Rig Builder**
3. Drag your character's Animator into the "Target Animator" field
4. Click **"Build Rig"** - a complete rig will be created automatically

### Quick Start for Any Character

1. **Select a bone** in your character's hierarchy
2. **Right-click** on the Transform component
3. Choose **Control Rig Pro** from the context menu
4. Select the appropriate constraint type

---

## Main Tools

### Humanoid Rig Builder

The Humanoid Rig Builder creates a complete animation rig for humanoid characters with a single click.

**How to Use:**
1. Open the tool: **Tools > Control Rig Pro > Humanoid Rig Builder**
2. Assign your character's Animator component
3. Click "Build Rig"

**What it Creates:**
- Hip control for body movement
- Spine controls with twist deformation
- IK controls for both arms and legs
- Head aim control for look-at functionality
- Properly configured Rig Builder component

### Quick Constraint Setup for any character

Quick Constraint Setup provides context menu shortcuts for rapidly adding constraints to selected bones.

**Access Methods:**
- Right-click on any bone Transform component or gameobject (or multiselect gameobjects then right click) then go to Control Rig Pro Menu

**Available Quick Setups:**
- 2-Bone IK (arms, legs) : should select the last bone for 2 bone ik(example : hand, foot).
- Chain IK (tails, tentacles) : should select top bone in tail bone hierarchy.
- Multi-Parent (hip, spine) : select the same bone you want to control.
- Multi-Aim (head, eyes) : select the same bone you want to control.
- Twist Chain (spine, neck) : select first bone (like spline) then hold CTRL then select another bone (like neck) then right click on any of the 2 gameojects then go to control rig pro and add Twist chain.
- Jiggle Bone/Chain (hair, accessories): Select the bone or chain of bones you want to add physics-based secondary motion to
- Hand Finger Controls: Select the hand bone to create finger curl controls for all fingers
- Damped Transform: Select the bone that should follow another transform with smooth delay
- Override Transform: Select the bone you want to directly control with an external transform

### FootGrounder

The FootGrounder component automatically adjusts character feet to match ground surfaces, preventing floating or clipping. you can add the script to the character and give foot references.

**Key Settings:**
- **Ground Layers**: Which layers to detect as ground
- **Raycast Distance**: How far to check for ground
- **Foot Offset**: Distance to maintain above ground
- **Smoothing**: Enable for natural transitions
- **Slope Adaptation**: Rotate feet to match slopes

---

### Physics Constraints

#### Jiggle Bone
Adds physics-based secondary motion to individual bones.

**Setup:**
- Select bones for jiggle effect
- Right-click > Control Rig Pro > Add Jiggle Bone
- Adjust stiffness, damping, and mass

**Parameters:**
- **Stiffness** (0-200): How quickly bone returns to rest
- **Damping** (0-50): Reduces oscillation
- **Mass**: Weight of the bone
- **Max Offset**: Limits maximum displacement
- **Gravity**: Additional gravity force

**Use Cases:**
- Hair strands
- Ears
- Accessories
- Belly jiggle

#### Jiggle Chain
Applies connected physics simulation across multiple bones.

**Setup:**
- Select start and end bones of chain
- Right-click > Control Rig Pro > Add Jiggle Chain
- Configure physics parameters

**Parameters:**
- **Mass**: Overall chain weight
- **Stiffness** (0-1): Spring tension
- **Damping** (0-1): Motion resistance
- **Dynamic Offset**: Simulation radius
- **Motion Decay**: Gradual speed reduction

**Use Cases:**
- Ponytails
- Tails
- Cloth strips
- Antenna

---

*Control Rig Pro - Making character rigging accessible to everyone*