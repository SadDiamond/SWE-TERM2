# SWE-TERM2

**Project Overview:**
A shooter/puzzle game using movement mechanics (slide-dashing, slamming).

## Week 3 (B Week) - Focus: Preliminary Planning and Research

4-5-2026 - 6-5-2026
- Did the beginning of the documentation (defining the core loop for a fast-paced arena shooter), also started doing some designs on the overall structure of the game and procedural map generation.
8-5-2026
- Made the foundational base player controller and set up a basic playground for testing physics.

9-5-2026
- Debugged and fixed Player raycasting (added center-camera Raycasts) to ensure precise crosshair aiming.
- Created basic hitscan tests and dummy target scripts.

10-5-2026
- Implemented core OOP structures for the combat system (`Gun` parent, `Projectile` class).
- Setup basic target dummies and `IDamageable` interfaces to test weapon damage outputs.

## Week 4 (A Week) - Focus: Identification of Classes, Objects, System Diagramming

11-5-2026
- Began cleanup pass on existing scripts now that the core combat loop is working. Stripped Debug.Log statements to keep console clean during playtesting.
- Reviewed the current combat hierarchy (`IDamageable` -> `BasicEnemyAI` / `Target`) in preparation for the class diagram.

13-5-2026
- Refactored `PlayerController`: Overhauled the physics to use a separated momentum vector. This was necessary to correctly implement velocity-based mechanics like slide-dashing.
- Hooked up slide and dash logic, ensuring dashed momentum carries over into standard physics.

14-5-2026
- Upgraded the advanced movement system: Added double jumping, mid-air slams, and speed caps.
- Added dynamic camera FX (FOV warping based on speed, camera dipping for slides) to enhance the feeling of speed.

15-5-2026

## Week 5 (B Week) - Focus: Programming, Asset Creation/Identification and Journaling

18-5-2026
- Removed hitscan combat in favor of physical projectiles utilizing `Rigidbody.linearVelocity`.
- Hooked up weapon sway, recoil, impact sparks, and trails to dramatically improve game feel.

20-5-2026
- Identified and fixed a bug where fast-moving players would shoot themselves. Made entities immune to their own projectiles using an `owner` ID reference framework.

22-5-2026
- Interfaced a functional `JumpPad` hazard to launch the player in 3D space, expanding vertical mobility in arenas.

## Week 6 (A Week) - Focus: Programming, Asset Creation/Identification and Journaling

25-5-2026
- Implemented NavMesh-driven Enemy AI (`BasicEnemyAI`) capable of effectively chasing the player and firing physical projectiles.

26-5-2026
- Implemented the core Wave Function Collapse (`WFCGenerator`) script to synthesize dynamic, modular arenas.
- Heavily upgraded the WFC generator: Inserted tile weight mapping to control room density and implemented `spawnRotation`.
- Added automatic generation of solid perimeter bounds and outer safety floors to the WFC output so the player is safely locked in the arena.

27-5-2026
28-5-2026
29-5-2026

## Week 7 (B Week) - Focus: Programming, Asset Creation/Identification and Journaling

1-6-2026
3-6-2026
5-6-2026

## Week 8 (A Week) - Focus: Programming, Journaling and Testing and Evaluating

8-6-2026
10-6-2026
11-6-2026
12-6-2026

## Week 9 (B Week) - Focus: Final Documentation, Creating Presentations and Testing and Evaluating

15-6-2026
17-6-2026
19-6-2026 - Assessment Submission Due

## Week 10 (A Week)

22-6-2026 - 25-6-2026 - Presentations
