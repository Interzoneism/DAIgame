# Agent Brief (Initial Scope)

This repo is a Godot 4.x **C#** project for a top-down shooter/survival prototype.
Your job is to implement small, testable systems that integrate cleanly into the existing project.

If a ticket conflicts with this document, follow the ticket. Otherwise follow this doc.

---

## 1) MVP Goals (Initial Scope)

We are building a **Hotline Miami-feel** top-down prototype.

### Must-have systems (in scope now)
- Top-down movement (WASD), **snappy** (no floaty accel)
- Mouse aim: player faces the mouse cursor at all times
- Gunplay: hitscan primary; projectiles only when asked later
- Player health + healing item
- Zombies (basic chase + melee damage)
- Lighting: simple day/night tint; placeable lights later
- Deployable AI (DAI) **later in MVP**: deployment zone + build credits + limited placeables
- Crafting/placement (via DAI): spike trap, door barricade, window boarding, heat element, light fixture
- Build credits per deployment
- Recipes define placeables (minimal early)
- Inventory: small grid + equipment slots:
  - Usable 1, Usable 2
  - Hand Left, Hand Right
  - Outfit, Shoes, Head
- Handbuilt POIs randomly placed (simple placement)
- Looting system
- Day/night system
- Temperature (cold exposure rises at night, reduced indoors/heat later)

### Out of scope for now (do NOT implement unless ticket asks)
- Procedural/modular POIs
- Multiple enemy types
- Sound alertness system
- Visual alertness system
- Night/day modifiers
- End goals / narrative progression
- Horde night system (structured phases)
- Hunger and thirst

---

## 2) Golden Rule: Keep the game runnable

**Testbed must always work.**
- Do not break `scenes/Testbed.tscn`.
- New work should be testable in Testbed via hotkeys/buttons.

If you add a new system, you must add a minimal way to trigger/test it in Testbed.

---

## 3) Control feel requirements (Hotline Miami style)

- WASD movement must be immediate/snappy.
- Player rotates to face cursor every frame.
- Shooting direction is cursor direction.
- No heavy physics reliance; avoid jitter, avoid large inertia values.
- Keep motion stable under slow motion (see below).

---

## 4) Slow Motion (must exist early and remain compatible)

We use global slow motion via `Engine.TimeScale`.

Rules:
- Slow-mo toggled by input action `ToggleSlowMo`.
- Slow-mo must affect movement, zombies, cooldowns, day/night tick, and cold exposure consistently.
- Do not implement real-time timers based on wall-clock time.
- Prefer delta-based logic and/or Godot `Timer` nodes (default timers respect TimeScale).

One source of truth for slow-mo values:
- `GameManager` (or an existing single controller script).

---

## 5) Repo structure (follow this)

Keep edits contained to the relevant module folder:

- `scripts/Core/` (GameManager, global helpers, constants)
- `scripts/Player/` (player controller, health, inventory hooks)
- `scripts/Combat/` (weapons, hitscan, damage interfaces)
- `scripts/AI/` (zombies)
- `scripts/World/` (day/night, temperature)
- `scripts/DAI/` (deployable AI + placement later)
- `scripts/UI/` (HUD, inventory UI)
- `scenes/` (Testbed + any reusable scenes)
- `data/` (later: item defs, recipes, loot tables)

If these folders don’t exist yet, create them when needed, but don’t reorganize existing files unless asked.

---

## 6) Integration contracts (keep stable)

Do not invent competing patterns. Prefer simple C# + Godot nodes/signals.

### Damage
- Damageable targets should expose:
  - `ApplyDamage(float amount, Vector2 fromPos, Vector2 hitPos, Vector2 hitNormal)`
- If a collider is a child node, it may forward to the parent.

### Groups (use groups to avoid hard references)
- `"player"`
- `"enemies"`
- `"damageable"`
- `"loot_container"`
- `"poi"` (later)

### Input actions (don’t rename)
- `MoveUp`, `MoveDown`, `MoveLeft`, `MoveRight`
- `Fire`
- `Reload`
- `UseQuickSlot1`
- `UseQuickSlot2`
- `Interact`
- `Crouch`
- `Dive`
- `OpenInventory`


Add new actions only if the ticket requires it.

---

## 7) Performance rules (early guardrails)

- Avoid `_Process()` on hundreds of nodes doing heavy work.
- Zombie AI should be simple; expensive decisions should run on timers or throttled updates.
- Prefer pooling only when asked; keep MVP simple and stable first.

---

## 8) What a PR/ticket delivery must include

Every delivery must include:

1) **What changed**
- List files modified/added.

2) **No collateral refactors**
- Don’t rename or reformat unrelated files.
- Don’t introduce new architecture.

---

## 9) Style rules (so merges don’t suck)

- Keep scripts short and readable.
- Prefer explicit names over cleverness.
- Avoid deep inheritance. Use composition and groups.
- Avoid “global singletons everywhere”. Use GameManager only for global state.
- Add comments only when behavior is non-obvious.
- Do not add .md instructions files to test or how to use the new feature.

---

## 10) Current MVP milestone definition

MVP is done when:
- You can run Testbed and:
  - move/aim/shoot (feels like Hotline Miami)
  - kill zombies
  - take damage + heal
  - toggle night tint
  - see cold exposure rise at night
  - toggle slow-mo and everything still functions
- Later MVP extension:
  - deploy DAI, spend build credits, place limited defenses/utilities

---

## 11) If you’re uncertain

If something is ambiguous:
- Choose the simplest implementation that satisfies the ticket and keeps Testbed working.
- Do not add optional features “because it might be needed later”.

---

## 12) Development Environment Setup

### Prerequisites
- **Godot 4.5+** installed with .NET support
- **.NET SDK** (version specified in `global.json`)
- **GODOT environment variable** must be set:
  - macOS/Linux: Add to `.zshrc` or `.bashrc`: `export GODOT="/path/to/Godot"`
  - Windows: Set system environment variable `GODOT` to Godot executable path

### Building the Project
```sh
cd /path/to/DAIgame
dotnet build
```

### Build Warnings
- Ignore build warnings about style formatting

### Running the Game
- **VSCode**: Use launch configuration "🕹 Debug Game"
- **Command line**: `${GODOT}` (run Godot with project loaded)

### Running Tests
- **VSCode**: Use launch configuration "🧪 Debug Tests"
- **Command line**: `${GODOT} --run-tests --quit-on-finish`

### Project Entry Point
- Main entry: `Main/Main.tscn` and `Main/Main.cs`
- Main.cs handles test mode detection (GoDotTest integration)
- In normal mode, switches to `scenes/Testbed.tscn`
- **Do not modify Main.cs/Main.tscn** unless working on test infrastructure

### Code Coverage
```sh
chmod +x ./coverage.sh
./coverage.sh
```

### Debug Configurations (VSCode)
- **🕹 Debug Game** - Run game normally
- **🎭 Debug Current Scene** - Debug scene matching current .cs file name
- **🧪 Debug Tests** - Run all tests
- **🔬 Debug Current Test** - Run test matching current .cs file name

### Folder Structure
Follow the structure defined in section 5. Create folders as needed:
- `scripts/Core/`, `scripts/Player/`, `scripts/Combat/`, `scripts/AI/`, `scripts/World/`, `scripts/DAI/`, `scripts/UI/`
- `scenes/` (Testbed + reusable scenes)
- `data/` (item definitions, recipes, loot tables - later)
