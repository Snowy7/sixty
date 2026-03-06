# SIXTY

A fast-paced top-down roguelite where every run lasts exactly 60 seconds and death makes you stronger.

## Overview
- Purpose: Build and ship the first playable vertical slice of SIXTY in Unity.
- Owner: `Snowy7`
- Audience: Unity developers, indie game collaborators, and players interested in the project.

## Tech Stack
- Engine: Unity `2022.3 LTS`
- Rendering: URP
- Input: Unity Input System (`com.unity.inputsystem`)

## Current Gameplay Slice
Implemented from Week 1 high-priority tasks:
- Player movement (`8 m/s`) and top-down mouse/gamepad aiming
- Dash (`25 m/s`, `0.15s`, `2.0s` cooldown, `0.12s` i-frames)
- Shooting loop + projectile pipeline
- Pulse Rifle data model (`8/s`, `12` damage default values in `WeaponDefinition`)
- Global time system (base `60s`, `+10s` per death, cap `300s`)
- Damage-to-time loss (default `-2s`) and clock pickup (`+5s`)

## Quick Scene Setup (SampleScene)
1. Create an empty `GameObject` named `TimeManager` and add `TimeManager` component.
2. Create a `Player` object with:
- `Rigidbody` (gravity off, freeze rotation on X/Y/Z)
- Collider (Capsule or Box)
- `PlayerController`
- Child object `WeaponMount` with `WeaponController` on it (assign to `PlayerController.weaponController`)
3. Create a `WeaponDefinition` asset from `Create > Sixty > Combat > Weapon Definition` and set Pulse Rifle values.
4. Create a simple projectile prefab:
- Small primitive + trigger collider
- Add `Projectile` script
- Assign this prefab to the `WeaponDefinition`.
5. Assign input actions:
- In `PlayerController`, set `Input Actions` to `Assets/InputSystem_Actions.inputactions`.
6. Add one test enemy/hazard:
- Collider marked trigger
- `ContactTimeDamage` script
7. Add one pickup:
- Trigger collider
- `ClockPickup` script (`timeGranted = 5`).

## Script Map
- `Assets/Scripts/Core/TimeManager.cs`
- `Assets/Scripts/Player/PlayerController.cs`
- `Assets/Scripts/Combat/WeaponDefinition.cs`
- `Assets/Scripts/Combat/WeaponController.cs`
- `Assets/Scripts/Combat/Projectile.cs`
- `Assets/Scripts/Combat/IDamageable.cs`
- `Assets/Scripts/Combat/Health.cs`
- `Assets/Scripts/Combat/ContactTimeDamage.cs`
- `Assets/Scripts/World/ClockPickup.cs`

## Contributing
1. Create a branch for your change.
2. Keep commits focused and descriptive.
3. Open a pull request with context and test evidence.

## License
Specify your license in this repository (for example: MIT).
