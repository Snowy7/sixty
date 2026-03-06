## 1. High-Level Vision

**I.A Framework** (I.A) is a **3D Unity game framework** designed to:

- Speed up prototyping and full game dev
- Be modular and data-driven
- Enforce consistent architecture and workflow
- Offer a clean, centralized **update orchestration layer**

Core pillars:

1. Core & Update Layer
2. Characters & Input
3. Camera & Movement
4. Combat & Abilities
5. Inventory & Equipment
6. AI & Enemies
7. Interaction & World Systems
8. UI & UX
9. Persistence & Progression
10. Workflow & Editor Tools
11. Samples & Templates
12. (Future) Networking

---

## 2. Folder & Assembly Structure (Global)

Under `Assets/IaFramework/`:

- `Core/`
    - `Runtime/`
        - `Update/` (IaBehaviour, IaUpdateManager)
        - `Events/`
        - `Utility/`
        - `Data/` (base SOs, global config)
    - `Editor/`
        - Inspectors, windows, setup wizard
- `Gameplay/`
    - `Runtime/`
        - `Characters/`
        - `Combat/`
        - `Abilities/`
        - `Inventory/`
    - `Editor/`
- `Systems/`
    - `Runtime/`
        - `Camera/`
        - `AI/`
        - `Interaction/`
        - `World/` (spawners, pooling)
    - `Editor/`
- `UI/`
    - `Runtime/`
        - `HUD/`
        - `Menus/`
        - `Debug/`
    - `Editor/`
- `Samples/`
    - `Scenes/`
    - `Prefabs/`
    - `ScriptableObjects/`
    - `Docs/`

Assembly Definitions (example):

- `Ia.Core`
- `Ia.Gameplay`
- `Ia.Systems`
- `Ia.UI`
- `Ia.Core.Editor`
- `Ia.Gameplay.Editor`
- `Ia.Systems.Editor`
- `Ia.UI.Editor`

`Gameplay` / `Systems` / `UI` depend on `Core`; editors depend on their runtime asmdefs.

---

## 3. Core & IaBehaviour / Update Layer

### 3.1 IaBehaviour: The Base Class

**Purpose:**

- Provide a consistent base instead of raw `MonoBehaviour`
- Inject into the I.A Update system
- Offer common utilities and hooks

**Key Features:**

- Derives from `MonoBehaviour`
- Defines overridable hooks:
    - `OnIaAwake()`
    - `OnIaStart()`
    - `OnIaUpdate(float deltaTime)`
    - `OnIaFixedUpdate(float fixedDeltaTime)`
    - `OnIaLateUpdate(float deltaTime)`
- Automatically registers/unregisters itself with `IaUpdateManager`.

**Configuration on each behaviour:**

- `protected virtual IaUpdateGroup UpdateGroup => IaUpdateGroup.World;`
- `protected virtual IaUpdatePhase UpdatePhase => IaUpdatePhase.Update;`
- Optionally: `protected virtual int UpdatePriority => 0;`

Usage pattern:

```csharp
public class PlayerCharacterController : IaBehaviour
{
    protected override IaUpdateGroup UpdateGroup => IaUpdateGroup.Player;
    protected override IaUpdatePhase UpdatePhase => IaUpdatePhase.Update;

    protected override void OnIaUpdate(float dt)
    {
        // movement & input
    }
}
```

### 3.2 Update Groups & Phases

Enums:

- `IaUpdateGroup`:
    - `Player`
    - `AI`
    - `World`
    - `UI`
    - `FX`
    - `Custom1`
    - `Custom2`

- `IaUpdatePhase`:
    - `Update`
    - `FixedUpdate`
    - `LateUpdate`

Each IaBehaviour chooses one main phase + group.

### 3.3 IaUpdateManager

A singleton-style manager (per scene, but easiest via one global):

**Responsibilities:**

- Maintain lists of registered behaviours:
    - Organized by (phase, group)
    - Optionally sorted by `UpdatePriority`
- Drive updates:
    - In `Update()`:
        - For each group in a configured order:
            - If group enabled and not paused → call `OnIaUpdate(dt)` on its behaviours
    - In `FixedUpdate()` / `LateUpdate()` similarly
- Manage state:
    - Enable/disable groups: e.g. disable `AI` group
    - Pause/resume groups (like time-freeze)
    - Expose API for global pause modes (e.g. gameplay paused vs full pause)

**Public API examples:**

- `SetGroupEnabled(IaUpdateGroup group, bool enabled)`
- `SetGroupPaused(IaUpdateGroup group, bool paused)`
- `SetPhaseOrder(IaUpdatePhase phase, IaUpdateGroup[] order)`
- Introspection methods for debug (counts, etc.).

### 3.4 Benefits for the Rest of the Framework

- Central pause mode:
    - Pause world & AI, keep UI separate.
- Performance tuning:
    - Disable entire groups during heavy UI or cutscenes.
- Clear lifecycle & behaviour style:
    - All “real” framework components follow a consistent structure.

---

## 4. Other Core Components

Besides IaBehaviour & Update:

### 4.1 Core Utility

- Math helpers (remap, clamp, smooth, grid utils)
- Transform helpers (move-to, rotate-to, flatten)
- Extension methods (for collections, Vector3, etc.)

### 4.2 Events & Messaging

- Simple static event bus for cross-system signals:
    - `OnGameStateChanged`
    - `OnPlayerDied`
- Possibly ScriptableObject-based event channels for decoupled references.

### 4.3 Global Settings

- `IaGlobalSettings` ScriptableObject:
    - References to:
        - Default layers, masks
        - Default update group orders
        - Debug toggles
    - Simple editor window to find/edit it quickly.

---

## 5. Gameplay: Characters, Combat, Inventory

### 5.1 Characters & Input

**Content:**

- `BaseEntity`:
    - ID, team/faction
    - Health, shields, basic stats
    - C# events for health changed, death
- Stat system:
    - Base stats + additive/multiplicative modifiers
    - Sources: items, buffs, abilities
- Player controller:
    - Inherits from `IaBehaviour`
    - Uses IaUpdatePhase.Update & IaUpdateGroup.Player
- Input abstraction:
    - Wrapper around Unity input
    - Later easily swappable to new Input System

### 5.2 Camera & Movement

**Content:**

- Third-person camera:
    - Orbit, zoom, collision
    - Uses IaBehaviour and UpdateGroup.World or Player
- Camera profiles:
    - SOs with FOV, distance, offsets
    - Can switch profiles (combat vs exploration)
- Future: first-person camera & cinematic camera.

### 5.3 Combat & Abilities

**Content:**

- Damage model:
    - Damage types, crit chance/multiplier
    - Resistances handled as stats/modifiers
- Ability System:
    - Base Ability:
        - Cooldown, cost, cast time, duration
        - Hooks: `CanActivate`, `OnActivate`, `OnUpdate`, `OnEnd`
    - Ability data:
        - SO-driven: base stats, icons, description
- Effect system:
    - Effects: Damage, Heal, Buff, Debuff, Spawn Projectile, Play VFX
    - Abilities composed of multiple effects
- Templates:
    - Basic melee slash ability
    - Ranged projectile ability

### 5.4 Inventory, Items & Equipment

**Content:**

- Item definitions (SOs):
    - ID, icon, type, stack size, weight, effects on use
- Inventory:
    - Slot-based
    - Stacking, split/combine
    - Events for UI
- Equipment:
    - Slots:
        - Weapon, Head, Chest, etc.
    - Integration with stats
- Loot:
    - Pickup prefabs
    - Loot tables & drop-on-death components

---

## 6. Systems: AI, Interaction, World

### 6.1 AI & Enemies

**Content:**

- AI Controller:
    - Inherits `IaBehaviour` (group AI)
    - State machine:
        - Idle, Patrol, Chase, Attack, Flee
- Perception:
    - Vision (FOV + line of sight)
    - Hearing radius
- Integration with:
    - NavMesh
    - Combat/abilities (AI uses same ability system)
- Enemy templates:
    - Melee grunt
    - Ranged shooter

### 6.2 Interaction & World

**Content:**

- Interaction:
    - `IInteractable` interface
    - Player interaction behaviour (raycast or sphere cast)
    - Prompt display system
- Common interactables:
    - Doors, buttons, levers, chests, talk triggers
- World systems:
    - Spawn points, enemy spawners, wave spawners
    - Respawn logic
- Object pooling:
    - Pool manager & `Poolable` component
    - Used for projectiles, VFX, temporary objects

---

## 7. UI / UX

### 7.1 HUD

- Health/stamina bars
- Ability bar (abilities, cooldown visuals)
- Ammo/weapon widget (for shooter context)
- Buff/debuff icons

### 7.2 Menus

- Main menu
- Pause menu
- Options menu:
    - Volume sliders
    - Mouse sensitivity
    - Placeholder for key binds

### 7.3 Inventory & Equipment UI

- Item grid with drag & drop
- Equipment slots visual
- Tooltips:
    - Show stats, description, rarity, effects

### 7.4 Debug UI

- Debug panel:
    - Toggle (e.g. F1)
    - Group toggles:
        - Enable/disable AI, FX, World, etc.
    - Show:
        - Entity counts
        - Active AI count
        - Current frame rate
    - Optionally:
        - Live-tweak values (player speed, enemy damage) for tuning

---

## 8. Persistence & Progression

### 8.1 Save/Load

- Saveable interface:
    - Unique ID
    - Methods to serialize/deserialize state
- Central SaveManager:
    - Save to JSON or binary
    - Stores:
        - Player stats, inventory, equipment
        - Position/scene
        - Simple world state (e.g. chest opened flags)
- Basic multi-slot structure:
    - Save slot metadata: name, timestamp, playtime

### 8.2 Progression

- XP & level:
    - XP thresholds per level
    - Stat scaling
- Unlockables:
    - Skills / abilities unlocked at certain levels
    - Simple skill point structure

---

## 9. Workflow, Editor Tools, and Refinements

This layer heavily benefits from the update group concept and the framework’s structure.

### 9.1 Setup Wizard

Menu: `I.A Framework / Setup / Create Basic Game Setup`

Wizard does:

- Creates:
    - `IaGlobalSettings` asset
    - Base folders (if missing)
- Optionally:
    - Creates a sample scene with:
        - Player prefab
        - Camera rig
        - Ground
        - IaUpdateManager in scene
- Sets default IaUpdateManager group orders and toggles.

### 9.2 Asset Creation Tools

Menu: `I.A Framework / Create / ...`

Windows or context menus for:

- New Item Definition
- New Ability Definition
- New Enemy Template (prefab + data SO)
- New Camera Profile

Each:
- Auto names
- Sets sensible defaults
- Puts them into recommended folders (`Samples/ScriptableObjects` or `Game/Defs`).

### 9.3 Custom Inspectors

For:

- `IaUpdateManager`
    - See all groups + counts
    - Toggle enable/paused
    - Reorder groups per phase

- `BaseEntity`
    - Grouped health/stats
    - Buttons: “Kill”, “Heal full” in editor

- Abilities
    - Inline effect lists
    - Preview of damage ranges, cooldowns

- AI Controller
    - Current state visualization
    - Debug forces: “Force Chase”, “Force Idle”

### 9.4 Validation / Health Check Window

Menu: `I.A Framework / Tools / Validator`

Checks for:

- Duplicate IDs (items, abilities, entities)
- Missing references (prefabs missing data, controllers missing camera, etc.)
- Basic config mismatches (e.g., IaUpdateManager not in scene).

### 9.5 Documentation Integration

- A `Docs` folder with:
    - **Quickstart**: from empty project to character moving
    - **How to**:
        - Create item
        - Create ability
        - Make an AI enemy
    - **Architecture overview**:
        - Diagram of Core, Gameplay, Systems, UI, samples

---

## 10. Phased Roadmap (Refined with Update Layer)

### Phase 0 – Core Skeleton & IaBehaviour

**Goal:** Solid foundation; no gameplay yet.

- Implement:
    - Folder structure
    - Assembly definitions
    - `IaUpdateGroup`, `IaUpdatePhase`
    - `IaBehaviour`
    - `IaUpdateManager`
    - Core utilities & events
    - `IaGlobalSettings` asset type
- Simple debug UI to show groups & counts.

**Deliverable:** Empty scene where some dummy IaBehaviours tick through the central manager.

---

### Phase 1 – Character + Camera

**Goal:** Player moves in 3D world using I.A Update system.

- Implement:
    - `BaseEntity` with health
    - Player character controller (inherits `IaBehaviour`)
    - Third-person camera (inherits `IaBehaviour`)
- Use:
    - `IaUpdateGroup.Player` for controller
    - `IaUpdateGroup.World` or `Player` for camera
- Sample scene:
    - Ground, player, camera, IaUpdateManager

**Deliverable:** Basic third-person demo powered by IaBehaviour + update groups.

---

### Phase 2 – Combat & Health System

**Goal:** Combat between player and dummy enemies.

- Implement:
    - Damage model and HitInfo
    - Basic ability base
    - Melee attack ability
    - Simple enemy dummy that takes damage & dies
- Integrate:
    - Abilities tick via IaBehaviour (group: Player / AI)
    - Health bars or temporary debug text
- Combat scene:
    - Player vs dummy targets

**Deliverable:** Reusable combat foundation with ability-driven attacks.

---

### Phase 3 – Inventory & Items

**Goal:** Pick up, equip, consume items and see effect.

- Implement:
    - ItemDefinition SO
    - Inventory component
    - Equipment component
- Integrate:
    - Items influence stats via modifiers
    - Inventory UI (basic version)
- Create simple sample:
    - Sword, bow, potion, light armor
    - Chest that drops items

**Deliverable:** Core RPG-like item system working end-to-end.

---

### Phase 4 – AI & Enemies

**Goal:** Real enemies that act on their own.

- Implement:
    - AIController (IaBehaviour, group AI)
    - Perception (sight/hearing)
    - States: Idle, Patrol, Chase, Attack
- Integrate:
    - AI uses same ability system for attacks
    - Spawner system for waves
- Arena scene:
    - Player vs multiple AI enemies

**Deliverable:** Reusable AI foundation integrated with combat and update groups.

---

### Phase 5 – Interaction & World Systems

**Goal:** World feels responsive, interactive.

- Implement:
    - Interaction system (IInteractable)
    - Doors, levers, chests, NPC talk triggers
    - Object pooling system
- Integrate:
    - Pooling used for projectiles and VFX
    - Spawners & interactables in demo scenes

**Deliverable:** Player can explore, open, trigger, fight in a semi-real world.

---

### Phase 6 – UI & UX Polish

**Goal:** It looks like a real game skeleton.

- Implement:
    - HUD (health, stamina, abilities)
    - Inventory / equipment UI
    - Buff/debuff display
    - Menus (main, pause, options)
- Add:
    - Hit markers, damage numbers, screen flash on damage (optional)

**Deliverable:** Visually coherent, user-friendly frontend.

---

### Phase 7 – Persistence & Progression

**Goal:** Sessions can persist; characters progress.

- Implement:
    - Save/load system
    - XP & level progression
    - Simple quest/goal structure (optional, even if minimal)
- Integrate with:
    - Inventory, stats, world state

**Deliverable:** Start game → play → save → quit → load → continue.

---

### Phase 8 – Tools, Wizards & Validation

**Goal:** Make it trivial to start new projects with I.A.

- Implement:
    - Setup wizard (create base scene + settings)
    - Asset creation wizards (Item/Ability/Enemy/Camera profile)
    - Custom inspectors (Update manager, Entities, AI, Abilities)
    - Validation window

**Deliverable:** “New project + I.A Framework” to “playable base game” in minutes.

---

### Phase 9 – Samples & Documentation

**Goal:** Treat I.A as a product.

- Finalize:
    - Docs
    - Sample scenes & prefabs
- Optional:
    - Package as Unity package / UPM
    - Public release-ready structure

---