# Scripts Organization

This directory contains all game logic scripts organized by system.

## Folder Structure

- **Core/** - GameManager, global helpers, constants, utility classes
- **Player/** - Player controller, health, inventory hooks, player-specific logic
- **Combat/** - Weapons, hitscan, projectiles, damage interfaces
- **AI/** - Enemy AI (zombies, etc.)
- **World/** - Day/night system, temperature, weather, environment
- **DAI/** - Deployable AI system, placement, build credits
- **UI/** - HUD, inventory UI, menus, UI controllers

## Guidelines

- Keep scripts focused and single-purpose
- Prefer composition over deep inheritance
- Use Godot groups for loose coupling (see AGENTS.md section 6)
- Follow the naming conventions and patterns in AGENTS.md
- Scripts should work with delta time and respect `Engine.TimeScale` for slow motion

## Integration

Scripts should integrate with scenes in `scenes/` directory. The main testing scene is `scenes/Testbed.tscn`.

For detailed architecture and coding standards, see [AGENTS.md](../AGENTS.md).
