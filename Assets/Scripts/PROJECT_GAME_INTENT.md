# Game Intent

This project is a fast first-person cyber arena roguelite.

The current main direction is:
- Momentum FPS movement: dash, slide, slam, double jump, jump pads, speed feedback.
- Procedural floating arenas generated at runtime.
- Combat floors, shop floors, and boss floors.
- Enemy clearing plus short terminal micro-puzzles to unlock the exit.
- Coins, health pickups, and weapon preset unlocks as run progression.

Older door, keycard, keypad, and generic WFC prototypes may still exist in the project. Treat them as legacy/scaffolding unless a task explicitly asks to revive them. Prefer extending the active Cybergrind arena systems:
- `Assets/Scripts/WFC/FINALArenaGenerator.cs`
- `Assets/Scripts/WFC/FINALArenaDirector.cs`
- `Assets/Scripts/WFC/FINALPuzzleTerminal.cs`
- `Assets/Scripts/PlayerController.cs`
- `Assets/Scripts/Combat/Gun.cs`
- `Assets/Scripts/AI/BasicEnemyAI.cs`
