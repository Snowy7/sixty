## Game Design Document: **SIXTY**

**Version:** 1.0**Engine:** Unity 2022.3 LTS**Scope:** 2-week solo dev**Target:** PC (Steam/Itch), $5.99

* * *

## 1\. Elevator Pitch

> Every run lasts exactly 60 seconds. Die, and your next run gets +10 seconds. How long until you beat the boss?

A top-down roguelite where time is health, currency, and tension.

* * *

## 2\. Core Loop

```
Enter Room → Kill Enemies → Exit Unlocks → Grab Pickups (risk/reward) → 
Next Room → Boss at Room 10 → Win or Die (+10s, restart)
```

* * *

## 3\. Player Systems

### 3.1 Movement

<table style="min-width: 50px;"><colgroup><col style="min-width: 25px;"><col style="min-width: 25px;"></colgroup><tbody><tr><th colspan="1" rowspan="1"><p>Property</p></th><th colspan="1" rowspan="1"><p>Value</p></th></tr><tr><td colspan="1" rowspan="1"><p>Base speed</p></td><td colspan="1" rowspan="1"><p>8 m/s</p></td></tr><tr><td colspan="1" rowspan="1"><p>Dash speed</p></td><td colspan="1" rowspan="1"><p>25 m/s</p></td></tr><tr><td colspan="1" rowspan="1"><p>Dash duration</p></td><td colspan="1" rowspan="1"><p>0.15s</p></td></tr><tr><td colspan="1" rowspan="1"><p>Dash cooldown</p></td><td colspan="1" rowspan="1"><p>2.0s</p></td></tr><tr><td colspan="1" rowspan="1"><p>Dash i-frames</p></td><td colspan="1" rowspan="1"><p>0.12s</p></td></tr></tbody></table>

**Input:** WASD move, mouse aim, LMB shoot, Space dash

### 3.2 Weapons (3 base, unlock 6 more)

<table style="min-width: 100px;"><colgroup><col style="min-width: 25px;"><col style="min-width: 25px;"><col style="min-width: 25px;"><col style="min-width: 25px;"></colgroup><tbody><tr><th colspan="1" rowspan="1"><p>Weapon</p></th><th colspan="1" rowspan="1"><p>Fire Rate</p></th><th colspan="1" rowspan="1"><p>Damage</p></th><th colspan="1" rowspan="1"><p>Notes</p></th></tr><tr><td colspan="1" rowspan="1"><p>Pulse Rifle</p></td><td colspan="1" rowspan="1"><p>8/s</p></td><td colspan="1" rowspan="1"><p>12</p></td><td colspan="1" rowspan="1"><p>Reliable, mid-range</p></td></tr><tr><td colspan="1" rowspan="1"><p>Shotgun</p></td><td colspan="1" rowspan="1"><p>1.5/s</p></td><td colspan="1" rowspan="1"><p>8×5</p></td><td colspan="1" rowspan="1"><p>Close burst, falloff</p></td></tr><tr><td colspan="1" rowspan="1"><p>Charge Beam</p></td><td colspan="1" rowspan="1"><p>Hold 1s</p></td><td colspan="1" rowspan="1"><p>80</p></td><td colspan="1" rowspan="1"><p>Pierces, slow</p></td></tr></tbody></table>

**Upgrades (pick 1 per room clear):** Fire rate, damage, multishot, piercing, explode on hit

### 3.3 Passives (1 per run, unlockable pool)

<table style="min-width: 50px;"><colgroup><col style="min-width: 25px;"><col style="min-width: 25px;"></colgroup><tbody><tr><th colspan="1" rowspan="1"><p>Passive</p></th><th colspan="1" rowspan="1"><p>Effect</p></th></tr><tr><td colspan="1" rowspan="1"><p>Adrenaline</p></td><td colspan="1" rowspan="1"><p>+20% speed under 10s</p></td></tr><tr><td colspan="1" rowspan="1"><p>Borrowed Time</p></td><td colspan="1" rowspan="1"><p>First fatal hit heals 30HP, costs 5s</p></td></tr><tr><td colspan="1" rowspan="1"><p>Overclock</p></td><td colspan="1" rowspan="1"><p>+30% damage, -10 max HP</p></td></tr><tr><td colspan="1" rowspan="1"><p>Second Wind</p></td><td colspan="1" rowspan="1"><p>Dash resets on kill</p></td></tr></tbody></table>

* * *

## 4\. Time System

<table style="min-width: 50px;"><colgroup><col style="min-width: 25px;"><col style="min-width: 25px;"></colgroup><tbody><tr><th colspan="1" rowspan="1"><p>Mechanic</p></th><th colspan="1" rowspan="1"><p>Details</p></th></tr><tr><td colspan="1" rowspan="1"><p>Base time</p></td><td colspan="1" rowspan="1"><p>60s + (deaths × 10s), cap 300s</p></td></tr><tr><td colspan="1" rowspan="1"><p>Clock pickups</p></td><td colspan="1" rowspan="1"><p>+5s, rare spawn, risky position</p></td></tr><tr><td colspan="1" rowspan="1"><p>Time cost</p></td><td colspan="1" rowspan="1"><p>Taking damage = -2s (not HP)</p></td></tr><tr><td colspan="1" rowspan="1"><p>Visual</p></td><td colspan="1" rowspan="1"><p>Giant center clock, red pulse under 10s, audio pitch rise</p></td></tr></tbody></table>

**Death:** Time hits 0. No traditional HP.

* * *

## 5\. Enemy Design

<table style="min-width: 100px;"><colgroup><col style="min-width: 25px;"><col style="min-width: 25px;"><col style="min-width: 25px;"><col style="min-width: 25px;"></colgroup><tbody><tr><th colspan="1" rowspan="1"><p>Type</p></th><th colspan="1" rowspan="1"><p>Behavior</p></th><th colspan="1" rowspan="1"><p>HP</p></th><th colspan="1" rowspan="1"><p>Threat</p></th></tr><tr><td colspan="1" rowspan="1"><p>Drone</p></td><td colspan="1" rowspan="1"><p>Charge player</p></td><td colspan="1" rowspan="1"><p>30</p></td><td colspan="1" rowspan="1"><p>Low</p></td></tr><tr><td colspan="1" rowspan="1"><p>Turret</p></td><td colspan="1" rowspan="1"><p>Stationary, aim delay</p></td><td colspan="1" rowspan="1"><p>50</p></td><td colspan="1" rowspan="1"><p>Medium</p></td></tr><tr><td colspan="1" rowspan="1"><p>Hunter</p></td><td colspan="1" rowspan="1"><p>Flank, burst fire</p></td><td colspan="1" rowspan="1"><p>40</p></td><td colspan="1" rowspan="1"><p>Medium</p></td></tr><tr><td colspan="1" rowspan="1"><p>Tank</p></td><td colspan="1" rowspan="1"><p>Slow, high HP, AoE slam</p></td><td colspan="1" rowspan="1"><p>120</p></td><td colspan="1" rowspan="1"><p>High</p></td></tr><tr><td colspan="1" rowspan="1"><p>Boss (Room 10)</p></td><td colspan="1" rowspan="1"><p>Phase shifts at 66%/33%</p></td><td colspan="1" rowspan="1"><p>800</p></td><td colspan="1" rowspan="1"><p>Extreme</p></td></tr></tbody></table>

**Spawning:** 3-6 enemies per room, scaled by room number + time remaining (faster spawns if slow)

* * *

## 6\. Room Generation

### Layout

- Linear chain: 10 rooms + boss arena

- Room size: 15×15m to 25×25m

- Connectors: 5m corridors

### Room Types

<table style="min-width: 75px;"><colgroup><col style="min-width: 25px;"><col style="min-width: 25px;"><col style="min-width: 25px;"></colgroup><tbody><tr><th colspan="1" rowspan="1"><p>Type</p></th><th colspan="1" rowspan="1"><p>Frequency</p></th><th colspan="1" rowspan="1"><p>Notes</p></th></tr><tr><td colspan="1" rowspan="1"><p>Combat</p></td><td colspan="1" rowspan="1"><p>70%</p></td><td colspan="1" rowspan="1"><p>Standard encounter</p></td></tr><tr><td colspan="1" rowspan="1"><p>Reward</p></td><td colspan="1" rowspan="1"><p>20%</p></td><td colspan="1" rowspan="1"><p>Free upgrade, no enemies</p></td></tr><tr><td colspan="1" rowspan="1"><p>Risk</p></td><td colspan="1" rowspan="1"><p>10%</p></td><td colspan="1" rowspan="1"><p>Heavy spawn, guaranteed clock pickup</p></td></tr></tbody></table>

### Modular Kit (Unity prefabs)

- Floor tile (5×5m)

- Wall segment (5×2.5m)

- Door frame (trigger-based unlock)

- Pillar cover (2×2m)

- Exit portal (emissive cylinder)

* * *

## 7\. Progression

### Run Progression

- Weapon upgrades (3 tiers max)

- Passive selection (1 per run)

- Time remaining (carry over nothing)

### Meta Progression

<table style="min-width: 75px;"><colgroup><col style="min-width: 25px;"><col style="min-width: 25px;"><col style="min-width: 25px;"></colgroup><tbody><tr><th colspan="1" rowspan="1"><p>Unlock</p></th><th colspan="1" rowspan="1"><p>Condition</p></th><th colspan="1" rowspan="1"><p>Effect</p></th></tr><tr><td colspan="1" rowspan="1"><p>New weapon</p></td><td colspan="1" rowspan="1"><p>5 deaths</p></td><td colspan="1" rowspan="1"><p>Adds to drop pool</p></td></tr><tr><td colspan="1" rowspan="1"><p>New passive</p></td><td colspan="1" rowspan="1"><p>10 deaths</p></td><td colspan="1" rowspan="1"><p>Adds to selection</p></td></tr><tr><td colspan="1" rowspan="1"><p>Starting bonus</p></td><td colspan="1" rowspan="1"><p>25 deaths</p></td><td colspan="1" rowspan="1"><p>+5s base time</p></td></tr></tbody></table>

**Save:** JSON file, deaths, unlocks, best time to boss kill.

* * *

## 8\. Visual Style

<table style="min-width: 75px;"><colgroup><col style="min-width: 25px;"><col style="min-width: 25px;"><col style="min-width: 25px;"></colgroup><tbody><tr><th colspan="1" rowspan="1"><p>Element</p></th><th colspan="1" rowspan="1"><p>Approach</p></th><th colspan="1" rowspan="1"><p>Unity Implementation</p></th></tr><tr><td colspan="1" rowspan="1"><p>Player</p></td><td colspan="1" rowspan="1"><p>Capsule, emissive accent</p></td><td colspan="1" rowspan="1"><p>URP lit shader, bloom</p></td></tr><tr><td colspan="1" rowspan="1"><p>Enemies</p></td><td colspan="1" rowspan="1"><p>Primitive variants, color-coded</p></td><td colspan="1" rowspan="1"><p>Material color swap</p></td></tr><tr><td colspan="1" rowspan="1"><p>Environment</p></td><td colspan="1" rowspan="1"><p>Corporate brutalism, clean lines</p></td><td colspan="1" rowspan="1"><p>Modular kit, tileable textures</p></td></tr><tr><td colspan="1" rowspan="1"><p>VFX</p></td><td colspan="1" rowspan="1"><p>Particle systems, minimal meshes</p></td><td colspan="1" rowspan="1"><p>URP particles, trails</p></td></tr><tr><td colspan="1" rowspan="1"><p>UI</p></td><td colspan="1" rowspan="1"><p>Minimal, clock is hero</p></td><td colspan="1" rowspan="1"><p>TextMeshPro, world-space for pickups</p></td></tr></tbody></table>

**Palette:** Desaturated blues/greys, high-contrast enemy orange, time pickup gold, danger red.

* * *

## 9\. Audio

<table style="min-width: 75px;"><colgroup><col style="min-width: 25px;"><col style="min-width: 25px;"><col style="min-width: 25px;"></colgroup><tbody><tr><th colspan="1" rowspan="1"><p>Category</p></th><th colspan="1" rowspan="1"><p>Approach</p></th><th colspan="1" rowspan="1"><p>Tool</p></th></tr><tr><td colspan="1" rowspan="1"><p>Music</p></td><td colspan="1" rowspan="1"><p>None. Ambient drone only.</p></td><td colspan="1" rowspan="1"><p>Freesound, 1-2 layers</p></td></tr><tr><td colspan="1" rowspan="1"><p>SFX</p></td><td colspan="1" rowspan="1"><p>Synthesized, punchy</p></td><td colspan="1" rowspan="1"><p>sfxr or bfxr</p></td></tr><tr><td colspan="1" rowspan="1"><p>Clock tick</p></td><td colspan="1" rowspan="1"><p>Pitch-mapped to remaining time</p></td><td colspan="1" rowspan="1"><p>AudioSource.pitch</p></td></tr></tbody></table>

**Critical SFX:**

- Gun fire (distinct per weapon)

- Dash whoosh

- Time pickup chime

- Damage tick (time loss)

- Death sting

- Boss phase shift

* * *

## 10\. Unity Architecture

### Scene Structure

```
Main
├── Player (CharacterController or Rigidbody)
│   ├── WeaponMount
│   └── VFX spawn points
├── RoomManager (spawns room chain)
├── EnemyManager (pools, spawns)
├── TimeManager (global clock, events)
├── UIManager (clock HUD, upgrade screen)
└── PostProcessing (bloom, chromatic aberration on low time)
```

### Key Scripts

```csharp
// TimeManager.cs
public class TimeManager : MonoBehaviour
{
    public static TimeManager Instance;
    public float TimeRemaining { get; private set; }
    public int DeathCount { get; private set; }
    
    public event Action OnTimeOut;
    public event Action<float> OnTimeChanged; // UI hook
    
    void Update() => Tick(Time.deltaTime);
    public void AddTime(float amount) => TimeRemaining += amount;
    public void TakeDamage() => TimeRemaining -= 2f;
    
    void Die()
    {
        DeathCount++;
        MetaSave.AddDeath(DeathCount);
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}
```

```csharp
// RoomGenerator.cs
public class RoomGenerator : MonoBehaviour
{
    [SerializeField] GameObject[] roomPrefabs;
    [SerializeField] int roomCount = 10;
    [SerializeField] GameObject bossRoomPrefab;
    
    void Generate()
    {
        for (int i = 0; i < roomCount; i++)
            SpawnRoom(i, i == roomCount - 1 ? bossRoomPrefab : null);
    }
}
```

```csharp
// Weapon.cs (ScriptableObject)
[CreateAssetMenu]
public class Weapon : ScriptableObject
{
    public float fireRate;
    public float damage;
    public float projectileSpeed;
    public GameObject projectilePrefab;
    public AudioClip fireSound;
    
    public void Fire(Vector3 origin, Vector3 direction) { }
}
```

### Recommended Packages

<table style="min-width: 50px;"><colgroup><col style="min-width: 25px;"><col style="min-width: 25px;"></colgroup><tbody><tr><th colspan="1" rowspan="1"><p>Package</p></th><th colspan="1" rowspan="1"><p>Use</p></th></tr><tr><td colspan="1" rowspan="1"><p>Input System</p></td><td colspan="1" rowspan="1"><p>New input, action maps</p></td></tr><tr><td colspan="1" rowspan="1"><p>Universal RP</p></td><td colspan="1" rowspan="1"><p>Lighting, post-processing</p></td></tr><tr><td colspan="1" rowspan="1"><p>TextMeshPro</p></td><td colspan="1" rowspan="1"><p>UI text</p></td></tr><tr><td colspan="1" rowspan="1"><p>Cinemachine</p></td><td colspan="1" rowspan="1"><p>Camera follow, shake</p></td></tr></tbody></table>

* * *

## 11\. 2-Week Sprint

<table style="min-width: 75px;"><colgroup><col style="min-width: 25px;"><col style="min-width: 25px;"><col style="min-width: 25px;"></colgroup><tbody><tr><th colspan="1" rowspan="1"><p>Day</p></th><th colspan="1" rowspan="1"><p>Focus</p></th><th colspan="1" rowspan="1"><p>Deliverable</p></th></tr><tr><td colspan="1" rowspan="1"><p>1</p></td><td colspan="1" rowspan="1"><p>Player controller, shooting</p></td><td colspan="1" rowspan="1"><p>Move, aim, shoot in test scene</p></td></tr><tr><td colspan="1" rowspan="1"><p>2</p></td><td colspan="1" rowspan="1"><p>Dash, time system</p></td><td colspan="1" rowspan="1"><p>Dash i-frames, clock ticks, death restarts</p></td></tr><tr><td colspan="1" rowspan="1"><p>3</p></td><td colspan="1" rowspan="1"><p>Enemy AI, spawning</p></td><td colspan="1" rowspan="1"><p>1 enemy type, basic nav, spawner</p></td></tr><tr><td colspan="1" rowspan="1"><p>4</p></td><td colspan="1" rowspan="1"><p>Room kit, generation</p></td><td colspan="1" rowspan="1"><p>3 room layouts, linear chain</p></td></tr><tr><td colspan="1" rowspan="1"><p>5</p></td><td colspan="1" rowspan="1"><p><strong>Pivot checkpoint</strong></p></td><td colspan="1" rowspan="1"><p>Core loop playable, fun?</p></td></tr><tr><td colspan="1" rowspan="1"><p>6</p></td><td colspan="1" rowspan="1"><p>2 more enemies, weapon variants</p></td><td colspan="1" rowspan="1"><p>3 weapons, 3 enemies</p></td></tr><tr><td colspan="1" rowspan="1"><p>7</p></td><td colspan="1" rowspan="1"><p>Boss fight</p></td><td colspan="1" rowspan="1"><p>3-phase boss, telegraphed attacks</p></td></tr><tr><td colspan="1" rowspan="1"><p>8</p></td><td colspan="1" rowspan="1"><p>Juice pass</p></td><td colspan="1" rowspan="1"><p>Screenshake, particles, hit stop, clock audio</p></td></tr><tr><td colspan="1" rowspan="1"><p>9</p></td><td colspan="1" rowspan="1"><p>Upgrade system, UI</p></td><td colspan="1" rowspan="1"><p>Upgrade selection, meta save</p></td></tr><tr><td colspan="1" rowspan="1"><p>10</p></td><td colspan="1" rowspan="1"><p>Passives, unlocks</p></td><td colspan="1" rowspan="1"><p>3 passives, unlock progression</p></td></tr><tr><td colspan="1" rowspan="1"><p>11</p></td><td colspan="1" rowspan="1"><p>Content: rooms, balance</p></td><td colspan="1" rowspan="1"><p>5 room variants, difficulty curve</p></td></tr><tr><td colspan="1" rowspan="1"><p>12</p></td><td colspan="1" rowspan="1"><p>Polish, bugs, audio</p></td><td colspan="1" rowspan="1"><p>All SFX, music drone, bug pass</p></td></tr><tr><td colspan="1" rowspan="1"><p>13</p></td><td colspan="1" rowspan="1"><p>Steam page, trailer, build</p></td><td colspan="1" rowspan="1"><p>30s trailer, store page live</p></td></tr><tr><td colspan="1" rowspan="1"><p>14</p></td><td colspan="1" rowspan="1"><p>Ship Itch, social push</p></td><td colspan="1" rowspan="1"><p>r/playmygame, Twitter, TikTok clips</p></td></tr></tbody></table>

* * *

## 12\. Success Metrics

<table style="min-width: 50px;"><colgroup><col style="min-width: 25px;"><col style="min-width: 25px;"></colgroup><tbody><tr><th colspan="1" rowspan="1"><p>Metric</p></th><th colspan="1" rowspan="1"><p>Target</p></th></tr><tr><td colspan="1" rowspan="1"><p>Time to first playable</p></td><td colspan="1" rowspan="1"><p>Day 2</p></td></tr><tr><td colspan="1" rowspan="1"><p>Core loop fun</p></td><td colspan="1" rowspan="1"><p>Day 5</p></td></tr><tr><td colspan="1" rowspan="1"><p>Boss killable</p></td><td colspan="1" rowspan="1"><p>Day 7</p></td></tr><tr><td colspan="1" rowspan="1"><p>No game-breaking bugs</p></td><td colspan="1" rowspan="1"><p>Day 12</p></td></tr><tr><td colspan="1" rowspan="1"><p>Wishlists (week 1)</p></td><td colspan="1" rowspan="1"><p>100</p></td></tr><tr><td colspan="1" rowspan="1"><p>Copies sold (month 1)</p></td><td colspan="1" rowspan="1"><p>500</p></td></tr></tbody></table>

* * *

## 13\. Post-Launch (If It Pops)

- Daily challenge mode (seeded runs)

- Leaderboards

- 2 more weapon packs

- Hard mode (no time pickups, -20% base time)

* * *

**Document locked. Build the thing.**
