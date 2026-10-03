# Roadmap

## Current milestone

Core feel prototypes — each mechanic in its own scene under `Assets/_Game/Scenes/Prototypes/` (see [ARCHITECTURE.md](technicals/ARCHITECTURE.md#prototypes--main-game)).

## Next

- [ ] Foundation: root `ProjectLifetimeScope` prefab, `PrototypeLifetimeScope`, `_Common/Combat` (Health, IDamageable)
- [ ] `Proto_Movement` — walk, sprint, crouch, jump, look
- [ ] `Proto_Shooting` — fire, switch, recoil, hit feedback, dummies (stand-ins: infinite ammo, base stats)
- [ ] `Proto_Enemies` — A* navigation, one archetype, attack, health, pooling
- [ ] `Proto_Ammo` — limited ammo, melee fallback, jams (real Inventory + Stats)

## Done

- Project setup, URP, store assets imported
