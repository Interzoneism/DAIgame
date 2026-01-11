# DAIgame

A top-down shooter/survival game prototype built with Godot 4.x and C#.

## Overview

DAIgame is a **Hotline Miami-style** top-down shooter with survival mechanics. The game features:
- Snappy WASD movement with mouse aim
- Gunplay
- Zombie enemies with chase AI
- Day/night cycle with temperature mechanics
- Deployable AI (DAI) system for building defenses
- Inventory and crafting systems

## For Developers

**👉 See [AGENTS.md](AGENTS.md) for complete development guidelines, architecture decisions, and how to contribute.**

The AGENTS.md file contains:
- MVP goals and scope
- Architecture and code organization
- Development environment setup
- Build, test, and debug instructions
- Coding standards and best practices

## Quick Start

### Prerequisites
- Godot 4.5+ with .NET support
- .NET SDK (see `global.json`)
- Set `GODOT` environment variable to your Godot executable path

### Build
```sh
dotnet build
```

### Run
Open the project in Godot or use VSCode launch configurations.

### Test
```sh
# Run tests
${GODOT} --run-tests --quit-on-finish

# Generate coverage
./coverage.sh
```

## Project Structure
- `Main/` - Entry point and test infrastructure (GoDotTest integration)
- `scenes/` - Game scenes (Testbed.tscn is the main game scene)
- `scripts/` - Game logic organized by system (Player, Combat, AI, etc.)
- `test/` - Unit and integration tests
- `data/` - Game data (items, recipes, etc.)

## License

See [LICENSE](LICENSE) for details.

---

Built with [Chickensoft](https://chickensoft.games) template and tools.
