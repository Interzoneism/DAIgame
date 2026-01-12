# Architecture Analysis & Refactoring Plan

## Current State Assessment

### ✅ Good Patterns Already In Place

1. **Data-Driven Weapons**: `WeaponData` Resource is well-designed with all weapon properties exported, allowing new weapons via `.tres` files without code changes.

2. **IDamageable Interface**: Clean damage contract with consistent signature across Player and Zombie.

3. **Signal-Based Communication**: `WeaponManager` uses signals for weapon changes, firing, reload state - good decoupling.

4. **Group-Based Entity Finding**: Uses `"player"`, `"enemies"`, `"damageable"` groups for loose coupling.

5. **Singleton Pattern for GameManager**: Properly implemented with `TryGetInstance` for optional access.

---

## 🔴 Issues Identified

### 1. Tight Coupling in MeleeWeaponHandler

**File**: `scripts/Combat/MeleeWeaponHandler.cs` (lines 317-325)

```csharp
// Apply additional knockback to zombies
if (weapon.Knockback > 0f && target is ZombieController zombie)
{
    zombie.ApplyExternalKnockback(hitNormal, weapon.Knockback);
}
```

**Problem**: Direct type check for `ZombieController` couples combat to specific enemy type.

**Solution**: Create `IKnockbackable` interface.

---

### 2. Duplicated Code: Hit Flash Logic

**Files**: 
- `PlayerController.cs` (lines 954-986)
- `ZombieController.cs` (lines 849-879)

Both implement identical hit flash patterns with `_hitFlashTimer`, `ApplyHitFlash()`, `UpdateHitFlash()`.

**Solution**: Extract `HitFlashController` component.

---

### 3. Duplicated Code: Knockback Damping

**Files**:
- `PlayerController.cs` (lines 917-926)
- `ZombieController.cs` (lines 836-845)

Identical knockback damping logic duplicated.

**Solution**: Extract `KnockbackController` component or use composition.

---

### 4. Hardcoded Animation Names

**File**: `PlayerController.cs` (lines 817-828)

```csharp
private static bool IsAttackAnimation(StringName animation)
{
    return animation == "attack_pistol" ||
           animation == "attack_shotgun" ||
           animation == "attack_uzi" ||
           animation == "attack_bat";
}
```

Similar pattern for `IsWalkAnimation()`. Adding new weapons requires code changes.

**Solution**: Derive animation names from `WeaponData.AnimationSuffix` dynamically.

---

### 5. Hardcoded Zombie State Machine

**File**: `ZombieController.cs` (lines 257-316)

```csharp
switch (_state)
{
    case ZombieState.Idle:
        HandleIdle(deltaF);
        break;
    case ZombieState.Chasing:
        HandleChasing(deltaF);
        break;
    case ZombieState.Attacking:
        HandleAttacking(deltaF);
        break;
}
```

**Problem**: Adding new states (Patrol, Fleeing, Alerted) requires modifying the switch and adding methods.

**Solution**: State pattern with composable state classes.

---

### 6. Entity Stats Duplication

Both `PlayerController` and `ZombieController` have:
- `MaxHealth`, `CurrentHealth`
- `KnockbackStrength`, `KnockbackDamp`
- `HitFlashDuration`

**Solution**: Create `EntityStats` Resource for data-driven entity configuration.

---

### 7. Scene Path Hardcoding

**File**: `ZombieController.cs` (lines 192-195)

```csharp
_corpseScene ??= GD.Load<PackedScene>("res://scripts/Combat/ZombieCorpse.tscn");
_bloodSpatterScene ??= GD.Load<PackedScene>("res://scenes/effects/BloodSpatter.tscn");
```

**Solution**: Use `[Export]` properties to make scenes configurable.

---

## Refactoring Plan (Priority Order)

### Slice 1: IKnockbackable Interface
- Create interface for knockback-receiving entities
- Apply to ZombieController
- Update MeleeWeaponHandler to use interface
- **Risk**: Low, adds abstraction without breaking existing code

### Slice 2: EntityStats Resource
- Create data-driven stats resource
- Apply to ZombieController first (lower risk)
- Migrate Player later
- **Risk**: Medium, touches core gameplay values

### Slice 3: HitFlashController Component
- Extract hit flash logic to reusable Node
- Both Player and Zombie can add as child
- **Risk**: Low, pure extraction

### Slice 4: KnockbackController Component
- Extract knockback logic to reusable Node
- Apply to both entities
- **Risk**: Low, pure extraction

### Slice 5: Dynamic Animation Names
- Remove hardcoded animation checks
- Use WeaponData.AnimationSuffix pattern matching
- **Risk**: Low, improves extensibility

### Slice 6: ZombieStateMachine (Optional)
- Extract states to separate classes
- More complex, defer if time-constrained
- **Risk**: Medium, significant restructuring

---

## Post-Refactor Extension Points

After refactoring, adding new content should be:

| Content Type | How to Add |
|--------------|------------|
| New Weapon | Create `.tres` file with `WeaponData`, set `AnimationSuffix` |
| New Enemy | Extend base enemy class, add `EntityStats` resource |
| New Effect | Create scene, export property on relevant controller |
| New State | Add state class implementing `IZombieState` |

---

## Testing Strategy

Each slice must:
1. Compile without errors
2. Run Testbed without crashes
3. Preserve existing gameplay behavior:
   - Player moves/aims/shoots
   - Zombies chase and attack
   - Damage and health work correctly
   - Hit flash and knockback work

