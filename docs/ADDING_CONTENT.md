# Adding New Content Guide

This document explains how to add new content to the game after the architecture refactoring. The goal is to enable content addition through data files and scenes, minimizing code changes.

---

## Quick Reference

| Content Type | Location | Requires Code? |
|--------------|----------|----------------|
| New Weapon | `data/weapons/*.tres` | No |
| New Enemy | `scenes/character/*.tscn` + `data/entities/*.tres` | Minimal |
| New Death Effect | `scenes/effects/*.tscn` | No |
| New Damageable Entity | Implement `IDamageable` | Yes |
| New Knockbackable Entity | Implement `IKnockbackable` | Yes |

---

## Adding a New Weapon

Weapons are fully data-driven using `WeaponData` resources. No code changes required.

### Step 1: Create the Resource File

Create a new `.tres` file in `data/weapons/`:

```
data/weapons/rifle.tres
```

### Step 2: Configure Properties

Open the file in Godot and set these properties:

**Basic:**
- `DisplayName`: "Assault Rifle"
- `WeaponId`: "rifle" (unique identifier)
- `AnimationSuffix`: "rifle" (for `walk_rifle`, `attack_rifle` animations)

**Combat:**
- `Damage`: 20
- `FireRate`: 10 (shots per second)
- `FireMode`: `Automatic` or `SemiAuto`

**Accuracy:**
- `Stability`: Base spread in degrees (0 = perfect)
- `Recoil`: Accuracy loss per shot
- `RecoilRecovery`: Time before accuracy resets
- `RecoilWarmup`: Free shots before recoil applies

**Ammunition:**
- `MagazineSize`: 30
- `ReloadTime`: 2.0
- `ReloadMode`: `Magazine` or `ShellByShell`

**Projectile:**
- `PelletCount`: 1 (or more for shotgun)
- `ProjectileSpeed`: 900
- `SpreadAngle`: Additional spread per pellet
- `SpawnOffsetX/Y`: Muzzle position offset

### Step 3: Add Animations (Optional)

If using a new `AnimationSuffix`, add these animations to the player's SpriteFrames:
- `walk_{suffix}` - Walking animation
- `attack_{suffix}` - Attack/fire animation

The animation system uses prefix matching, so no code changes needed.

### Step 4: Add to Starting Weapons (Testing)

In `scenes/character/Player.tscn`:
1. Find the `WeaponManager` node
2. Add your new weapon resource to `StartingWeapons` array

### Melee Weapon Example

```tres
DisplayName = "Machete"
WeaponId = "machete"
IsMelee = true
MeleeHitType = Swing  # or Instant
MeleeRange = 45.0
MeleeSpreadAngle = 60.0
SwingStartAngle = 60.0
SwingEndAngle = -60.0
DamageDelay = 0.1
StaminaCost = 15.0
```

---

## Adding a New Enemy

### Step 1: Create Entity Stats (Optional)

Create `data/entities/my_enemy_stats.tres`:

```tres
[gd_resource type="Resource" script_class="EntityStats" load_steps=2 format=3]

[ext_resource type="Script" path="res://scripts/Core/EntityStats.cs" id="1"]

[resource]
script = ExtResource("1")
MaxHealth = 75.0
KnockbackStrength = 100.0
KnockbackDamp = 300.0
HitFlashDuration = 0.15
HitFlashColor = Color(2, 1, 1, 1)
```

### Step 2: Create the Scene

Duplicate `scenes/character/Zombie.tscn` and modify:

1. Set root node script (extend `ZombieController` or create new)
2. Configure exported properties:
   - `CorpseScene`: Your death effect scene
   - `BloodSpatterScene`: Blood particle scene
3. Update collision layers/masks
4. Replace sprites in Body/Legs nodes

### Step 3: Implement Required Interfaces

For a new enemy controller, implement:

```csharp
public partial class MyEnemy : CharacterBody2D, IDamageable, IKnockbackable
{
    public void ApplyDamage(float amount, Vector2 fromPos, Vector2 hitPos, Vector2 hitNormal)
    {
        // Handle damage
    }

    public void ApplyKnockback(Vector2 direction, float strength)
    {
        // Handle knockback
    }
}
```

### Step 4: Add to Groups

In `_Ready()`:
```csharp
AddToGroup("enemies");
AddToGroup("damageable");
```

---

## Adding Death/Hit Effects

### Blood Spatter or Particles

1. Create scene in `scenes/effects/`
2. Extend appropriate base (GPUParticles2D, Node2D, etc.)
3. Assign to entity's `BloodSpatterScene` export property

### Corpse/Death Effect

1. Create scene (can be animated, physical, etc.)
2. Optionally implement velocity inheritance:

```csharp
public void SetVelocity(Vector2 velocity)
{
    // Apply momentum from dying entity
}
```

3. Assign to entity's `CorpseScene` export property

---

## Creating Reusable Components

### Using HitFlashController

Add hit flash to any entity:

```csharp
private HitFlashController? _hitFlash;

public override void _Ready()
{
    _hitFlash = new HitFlashController
    {
        FlashDuration = 0.1f,
        FlashColor = new Color(2f, 0.5f, 0.5f, 1f)
    };
    AddChild(_hitFlash);
    
    // Register sprites to flash
    if (GetNode<AnimatedSprite2D>("Sprite") is { } sprite)
    {
        _hitFlash.RegisterTarget(sprite);
    }
}

public void ApplyDamage(...)
{
    _hitFlash?.TriggerFlash();
    // ... rest of damage handling
}
```

---

## Interface Contracts

### IDamageable

Any entity that can receive damage must implement:

```csharp
void ApplyDamage(float amount, Vector2 fromPos, Vector2 hitPos, Vector2 hitNormal);
```

- `amount`: Damage points to apply
- `fromPos`: Origin of the attack (attacker position)
- `hitPos`: World position where hit occurred
- `hitNormal`: Surface normal for effects (blood spray direction)

Also add to `"damageable"` group.

### IKnockbackable

Any entity that can be pushed by attacks:

```csharp
void ApplyKnockback(Vector2 direction, float strength);
```

- `direction`: Normalized direction to push
- `strength`: Force in pixels/second

---

## Animation Naming Convention

The player animation system uses **prefix matching**:

| Animation Type | Pattern | Example |
|---------------|---------|---------|
| Walk | `walk` or `walk_*` | `walk`, `walk_pistol`, `walk_rifle` |
| Attack | `attack_*` | `attack_pistol`, `attack_machete` |

To add a new weapon animation:
1. Use `AnimationSuffix` in `WeaponData` (e.g., "machete")
2. Create animations: `walk_machete`, `attack_machete`
3. No code changes needed - prefix matching handles it

---

## Groups Reference

| Group | Purpose | Who Should Join |
|-------|---------|-----------------|
| `"player"` | Player lookup | Player only |
| `"enemies"` | Enemy targeting | All hostile NPCs |
| `"damageable"` | Damage system | Anything that takes damage |
| `"loot_container"` | Looting system | Containers, corpses |
| `"poi"` | Point of Interest | Buildings, locations |

---

## Testing New Content

1. **Build first**: Run `dotnet build` to catch compile errors
2. **Open Testbed**: Use `scenes/Testbed.tscn`
3. **Spawn with Z key**: Zombies spawn at mouse position
4. **Check console**: Error messages print to Godot console

### Quick Spawn for Custom Enemies

Add to `TestbedController.cs`:

```csharp
[Export]
public PackedScene? MyEnemyScene { get; set; }

// In HandleTestInputs():
if (Input.IsKeyPressed(Key.M))
{
    SpawnAtMouse(MyEnemyScene);
}
```

---

## Troubleshooting

### Weapon not working

- Check `WeaponId` is unique
- Verify `AnimationSuffix` matches sprite animations
- Ensure weapon is added to `StartingWeapons` array

### Enemy not taking damage

- Verify `IDamageable` is implemented
- Check entity is in `"damageable"` group
- Confirm collision layers allow hits

### Knockback not working

- Implement `IKnockbackable` interface
- Check weapon's `Knockback` value > 0
- Verify entity's `KnockbackDamp` isn't too high

### Effects not spawning

- Check scene path is correct
- Verify export property is assigned in inspector
- Look for error messages in console

