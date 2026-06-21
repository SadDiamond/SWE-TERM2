# Term 2 SWE project

## Project overview

This is a fast first-person arena roguelite made in Unity with C#. The player moves through procedurally generated floors, clears enemies and terminals, buys one upgrade in each shop, and keeps going until they die.

The final project focuses on:

- momentum movement: slide jumps, dashes, wall running, slams and grappling;
- four weapons with separate primary fire, abilities and upgrades;
- light, heavy, flying and boss enemies;
- runtime arena generation with combat, shop and boss floors;
- an infinite run with enemy count and health scaling;
- a compact HUD, start menu, settings, loading transitions and death screen.

The main scene is `Assets/Scenes/Arena.unity`. The active systems are under `Assets/Scripts`, especially `PlayerController.cs`, `Combat/Gun.cs`, `AI/BasicEnemyAI.cs` and the `WFC` folder.

## OOP structure

The project uses Unity components as objects with separate responsibilities. `PlayerController` handles movement and grappling, `Gun` handles weapons, `BasicEnemyAI` handles enemy states, and `CybergrindArenaDirector` controls floor progression. Shared behaviour is represented through abstract classes and interfaces:

- `Interactable` is the parent of terminals, shop stations and weapon rewards.
- `Terminal` is the parent of the current puzzle terminal.
- `PostProcessor` is the parent of generation repair and decoration passes.
- `IDamageable` lets guns damage players, enemies and targets through one method.
- `IGrappleMassTarget` lets the grapple treat light and heavy targets differently.

## Reconstructed development history

This history is based on dated Git commits and the files changed in each commit.

### 22 April - 10 May: setup and interaction foundation

- Created the Unity project and imported the base URP setup.
- Added `PlayerController` and the first `Interactable` system.
- Built early doors, keycards, terminals and switches to test inheritance and object interaction.

### 25 - 26 May: combat and movement prototype

- Built the first playable scene and movement test space.
- Added `Gun`, `Projectile`, `BulletTrail`, `Target` and `IDamageable`.
- Added `BasicEnemyAI` and the first enemy combat loop.
- Added the jump pad and continued changing player movement.

### 26 - 28 May: procedural arena generation

- Added the 3D WFC tile system and macro generators.
- Added post-processors for path repair, terrain, structures and room population.
- Created the Arena scene and the current arena generator/director structure.
- Added runtime terminals, pickups and floor progression.
- Removed Unity's generated `Library` folder from version control.

### 7 - 12 June: progression and combat expansion

- Expanded enemy behaviour, weapon behaviour and projectile handling.
- Added run state, shops, weapon rewards, boss UI and floor transitions.
- Reworked interactables and terminals around the current arena loop.
- Added debugging tools and a written game-intent file to separate active systems from old prototypes.

### 17 - 19 June: presentation and gameplay polish

- Added the start menu, settings, run HUD, shop preview and weapon status UI.
- Added weapon abilities, runtime weapon models and clearer combat feedback.
- Reworked loading and arena transitions.
- Added the grapple projectile and integrated grappling into player movement.
- Continued fixes to enemy AI, arena generation, movement feel and weapon feedback.

### Final refinement

- Reduced the weapon roster to four supported weapons.
- Changed the run to continue infinitely with scaling enemy health and count.
- Limited shops to one purchase per floor.
- Removed unused menu modes and old HUD elements.
- Simplified player-facing descriptions and renamed the game to `Term 2 SWE project`.

## Controls

| Input | Action |
|---|---|
| WASD | Move |
| Mouse | Look |
| Space | Jump |
| Shift | Dash |
| Ctrl / C | Slide or slam |
| LMB | Fire |
| RMB | Weapon ability |
| 1 / 2 | Switch weapon family |
| Q / E | Switch weapon variant or interact where shown |
| F | Grapple |
| F3 | Open debug mode |

## Tools

- Unity 6000.4.3f1
- C#
- Universal Render Pipeline
- Unity Input System
- Unity AI Navigation
- Git and GitHub Desktop

## Automated tests

Deterministic run rules are tested in `Assets/Tests/Editor/CybergrindRulesTests.cs`. The suite covers enemy scaling, weapon damage multipliers, the one-purchase shop lock and floor-timer calculations.

Run it in Unity through **Window > General > Test Runner > EditMode > Run All**.
