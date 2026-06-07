# SWE-TERM2 Successive Game Regenerations

This log tracks concrete rebuild passes from `PLAN.md`. Each regeneration should leave the project more playable than before and record the systems touched.

## Regeneration 1 - Roguelike Weapon Loop

Goal: turn the roadmap's combat/shop/boss cadence into an actual reward loop.

Implemented:
- Added `CybergrindRunState` as the run/meta state holder for boss-cleared progress and persistent weapon unlocks.
- Boss floor completion now unlocks the next weapon preset before advancing into the next cycle.
- `Gun` now respects unlocked presets, exposes equip/display helpers, and skips locked variants when cycling.
- Shop floors now spawn interactive `CybergrindWeaponShop` stalls that equip unlocked weapons for currency.
- `PlayerController` gained `TrySpendCurrency` so shops can participate in the run economy.

Validation target:
- Start with only the first weapon available.
- Clear a boss floor to unlock the next preset.
- Enter a shop floor and spend coins to equip an unlocked preset.
- Locked shop displays should report that the weapon is boss locked.

Next regeneration candidates:
- Add a transition controller with camera shake/fade/dissolve timing.
- Replace the boss placeholder with a boss actor and explicit defeat event.
- Add run seed UI and a reproducible seed input path.
- Add telemetry counters for deaths, clear time, damage taken, and weapon usage.

## Regeneration 2 - Floor Transition Choreography

Goal: replace the plain floor-advance delay with a visible transition layer that can grow into the planned cinematic in-place room swap.

Implemented:
- Added `CybergrindTransitionController` with an auto-created screen overlay, fade timing, camera FOV pulse, and UnityEvents for start/swap/finish hooks.
- `CybergrindArenaDirector` now uses the transition controller when objectives are complete and the player reaches the exit.
- The actual arena regeneration happens at the transition swap moment, so later dissolve, particles, and audio can attach without changing progression code.

Validation target:
- Complete a floor objective and reach the exit.
- Screen fades out, the next floor generates, then fades back in.
- The player should not trigger repeated floor advances during the transition.

Validation note:
- Source checks passed with `git diff --check`.
- Unity batchmode compiled successfully after Regeneration 1. A later Regeneration 2 batchmode retry was blocked before import by Unity licensing channel initialization, so this pass still needs an in-editor play check.

Correction:
- The intended transition is not a fade-to-black. It is diegetic tile movement: old arena pieces drop/scatter away and the next floor rises into place.
- `CybergrindTransitionController` has been refactored to animate generated arena renderers directly instead of using a screen fade.

## Regeneration 3 - Enemy Readability Pass

Goal: replace placeholder enemy blobs with stronger silhouettes and clearer combat roles.

Implemented:
- Rebuilt `BasicEnemyAI` procedural visuals around three distinct low-poly silhouettes:
  - Shooter: slim ranged unit with visor, wings/fins, and a visible rifle barrel.
  - Grunt: fast chaser with claw blades, narrow body, and aggressive eye slash.
  - Tank: bulky armored unit with shoulder mass, reactor core, arm cannon, and tread-like feet.
- Hidden the prefab's original capsule renderer so generated models are not fighting a default blob mesh.
- Added reusable enemy body/dark/glow materials and type-specific shoot-point positions.

Validation target:
- Spawn each enemy type and confirm they read differently at a glance.
- Shooter and tank projectiles should originate from the visible weapon points.
- Existing colliders/NavMeshAgent should continue to come from the base enemy prefab.

Validation note:
- Source checks passed with `git diff --check`.
- Unity batchmode was blocked before import by licensing channel initialization.

## Regeneration 4 - Boss Champion Gate

Goal: make boss floors feel like actual boss encounters instead of a normal enemy wave with a placeholder object.

Implemented:
- Added boss identity fields to `BasicEnemyAI` so a generated enemy can become a scaled champion with higher health, faster pressure, and a crown/halo silhouette.
- Boss floors now spawn a named tank-style champion at arena center before adding supporting enemies.
- Reduced boss-floor support enemy count when a champion is present so the boss reads as the main objective.
- Replaced the central boss placeholder cube with low floor/reactor warning markers that frame the champion spawn area.

Validation target:
- Enter a boss floor and confirm a large crowned champion appears at the center.
- Killing the boss and support enemies should allow the director to unlock the next weapon and advance floors.
- The boss should fire from its visible cannon and remain visually distinct from normal tanks.

## Regeneration 5 - Playable Entry Point

Goal: make Unity launch/build the actual arena shooter loop rather than the starter sample scene.

Implemented:
- Changed build settings so `Assets/Scenes/Arena.unity` is the enabled first scene.
- Kept `SampleScene.unity` listed but disabled, preserving it as a reference scene without making it the game entry point.

Validation target:
- Press Play or make a build and confirm the project starts in the generated arena scene.
- `CybergrindArenaDirector`, `CybergrindArenaGenerator`, player, HUD, enemies, pickups, shops, boss floors, and transitions should now belong to the same main scene path.

## Regeneration 6 - Enemy Grounding And Behavior

Goal: fix the immediate feel bugs the playtest called out: enemies floating, not chasing, and not reading like distinct combat roles.

Implemented:
- Lowered enemy spawn placement closer to the actual tile top so the collider sits on the floor instead of visibly hovering.
- Reworked `BasicEnemyAI` to use direct steering when NavMesh is unusable, which keeps enemies moving toward the player in the procedural arena.
- Added distinct movement logic per enemy role:
  - Shooter: strafes and fires from a visible rifle.
  - Grunt: rushes forward and lunges in close range.
  - Tank: lumbers forward with heavier body motion and slower pressure.
  - Flying: orbit-hover behavior as a separate role for future content.
- Added procedural bob/sway motion to the enemy body so they read as animated even before final animator clips arrive.

Validation target:
- Enemies should no longer hover above the floor on spawn.
- They should actively close distance to the player even if NavMesh is missing or unusable.
- Shooter, grunt, tank, and flying variants should be visually and behaviorally different at a glance.

## Regeneration 7 - Deterministic Run Seed

Goal: make the run loop reproducible so every generated floor can be replayed and debugged from a shared seed.

Implemented:
- Added a persistent run seed to `CybergrindRunState` and derived a deterministic floor seed from `runSeed + floor + theme + boss count`.
- `CybergrindArenaDirector` now pushes a stable seed into the generator before each floor is built.
- `RunStatusHUD` now displays the current run seed and the current floor seed for debugging and sharing.

Validation target:
- Starting a run should generate the same floor sequence when the same seed is used.
- The HUD should show a readable seed value for the current run and current floor.
- Boss unlocks and floor progression should still advance normally while remaining seeded.

## Regeneration 8 - Terminal Console And Floor Gate Hardening

Goal: make puzzle terminals read like actual hardware and stop floor progression from depending on a fragile placeholder setup.

Implemented:
- Rebuilt `Terminal` visuals into a layered console form with a base, stand, back housing, screen frame, glowing display, keypad, antenna, and cable bundle.
- Gave the terminal screen a distinct emissive highlight so focused terminals read as interactable devices rather than plain cubes.
- Added a guard in `SolvePuzzle` so solved terminals cannot re-fire their completion logic.
- Tightened the floor-complete gate in `CybergrindArenaDirector` to scan terminals with the explicit Unity 6 sort-mode overload.
- Lowered and reshaped spawned puzzle terminals so they sit more naturally on the floor instead of floating above the arena.

Validation target:
- Puzzle terminals should now look like dedicated consoles.
- Solving all puzzle terminals on a floor should reliably allow the exit check to pass and the next floor to generate.
- The terminal prompt should not keep re-solving or double-invoking completion logic.

## Regeneration 9 - Interaction Raycast Rewrite

Goal: make interactables reliable in a dense procedural arena instead of depending on a single collider hit.

Implemented:
- Reworked `PlayerController` interaction targeting to scan all ray hits, sort them by distance, and resolve the first `Interactable` found in the collider hierarchy.
- This lets terminals, doors, and future nested interactables be targeted even when the first collider in view is a child mesh or another blocking surface.

Validation target:
- Looking at a terminal and pressing `E` should now reliably trigger it as long as the player is in range.
- Nested or oddly built interactables should still be usable without hand-tuning every collider layout.

## Regeneration 10 - Terminal Status Feedback

Goal: make terminals read like active hardware with clear solved and unsolved states.

Implemented:
- Added a configurable terminal interaction radius so the player can use terminals without pixel-perfect positioning.
- Expanded the terminal build into a more expressive console with a dedicated status light and screen state.
- Solved terminals now visually shift to a powered-down green state so completion is readable at a glance.

Validation target:
- Unsolved terminals should glow and feel interactable.
- Solved terminals should clearly read as offline after completion.

## Regeneration 11 - Active Arena Scoping

Goal: stop stale scene objects from interfering with floor progression checks.

Implemented:
- Updated `CybergrindArenaDirector` to evaluate terminals and enemies from the current generated arena root when available.
- Exit checks now prefer the current arena’s exit object before falling back to the broader scene search.

Validation target:
- Previous floors or stray scene objects should no longer block progression on the active run.
- The current floor should be the only one that matters for terminal/enemy completion checks.

## Regeneration 12 - Puzzle Terminal Reachability

Goal: make the puzzle terminals sit in the arena like intended access points instead of floating props.

Implemented:
- Lowered puzzle terminal spawn height and tightened their scale so they sit on the floor more naturally.
- Kept the terminal model itself layered and legible, with the interactable geometry now reading as a console instead of a loose cube.

Validation target:
- The player should be able to approach terminals naturally instead of fighting awkward vertical placement.
- The terminals should look like deliberate floor objects, not leftovers from a placeholder pass.

## Regeneration 13 - Floor Loop Clarity

Goal: keep the roguelike loop understandable while the arena becomes more complete.

Implemented:
- Preserved the generator-driven loop with deterministic seeds, boss unlocks, and tile-based transitions.
- Tightened the progression loop so floor advancement now depends on the active arena’s terminals, enemies, and exit state instead of ad hoc global scene state.

Validation target:
- The 5-combat-floor cadence, shop floor, and boss floor should continue to advance as a single coherent run loop.
- Terminal completion should be the clear gate into floor progression, not an incidental side effect.

## Regeneration 14 - Enemy Combat Roles

Goal: make regular enemies behave like actual combat units instead of drifting decorations.

Implemented:
- Reworked `BasicEnemyAI` so grunts stop at melee range and use a real attack cooldown instead of endlessly hovering into the player.
- Added explicit ground anchoring for non-flying enemies so they keep contact with the floor instead of appearing suspended in midair.
- Tightened face-targeting and attack range logic so shooter, grunt, tank, and flying units each behave differently.

## Regeneration 15 - Terminal Variant Spawning

Goal: make the arena spawn different terminal types instead of the same puzzle shell every time.

Implemented:
- Updated arena generation to spawn a mix of relay, keypad, and switch terminals.
- Gave terminal variants deterministic prompts and generated passcodes/switch patterns so they feel distinct floor-to-floor.
- Added soft fallback behavior for keypad and switch terminals when their UI canvases are not present, so progression does not hard-block.

## Regeneration 16 - Transition Cleanup

Goal: stop old-floor pickups, terminals, and enemies from surviving the floor swap.

Implemented:
- Added explicit transient-content cleanup during the tile-shift transition.
- Old floor terminals, pickups, and enemies are now removed at the swap moment instead of lingering into the next floor.
- Kept the tile-shift animation focused on the actual arena surfaces.
- The run HUD now scopes objective counts to the active arena root so stale scene objects do not make the floor look solved too early.

## Regeneration 17 - Arena Atmosphere FX

Goal: give the generated arena a stronger sense of place with in-world effects.

Implemented:
- Added runtime atmospheric particle effects to the arena generator.
- Floors now get drifting dust, and boss floors get more aggressive spark-style ambience.
- The FX live under the arena root so they regenerate and clean up with the floor.

## Regeneration 18 - Transition Speed Tuning

Goal: make the floor-shift transition read as fast and decisive rather than sluggish.

Implemented:
- Shortened the old-tile drop, new-tile rise, and swap hold timings.
- Reduced cascade delay and movement distances so the swap lands more quickly while still reading as a physical shift.

Validation target:
- Enemies should now stop and attack instead of drifting forever.
- The next floor should not inherit old pickups or terminals.
- Different terminal types should be visible in the same run.
- The transition should feel snappier than before.

## Regeneration 19 - Capsule Shift Transition

Goal: make floor swaps feel like a physical launch instead of a dissolve or a rebuild.

Implemented:
- Reworked the transition to launch old floor props upward and outward rather than dropping them away.
- Added a translucent transition capsule around the player during the swap so the arena can be seen shifting inside a visible boundary.
- Kept the new floor rising in from below so the swap reads as one continuous handoff instead of a teardown/rebuild.

## Regeneration 20 - Grounded Terminal Lines

Goal: make terminal wiring feel anchored to the arena floor instead of floating through empty space.

Implemented:
- Added ground-path generation support to the arena generator so interactable lines can route over walkable tiles.
- Updated terminal circuit rendering to use the generated ground path when available.
- Kept the fallback wire rendering in place for scenes or edge cases where the path query cannot resolve.

## Regeneration 21 - Ten Terminal Challenges

Goal: expand terminal gameplay from a couple of shells into a larger set of readable minigame behaviors.

Implemented:
- Expanded `CybergrindPuzzleTerminal` into a 10-mode challenge system:
  - relay
  - burst
  - rhythm
  - delay
  - double tap
  - hold
  - alternating cadence
  - calibration
  - pulse
  - lockstep
- The arena generator now assigns these challenge modes deterministically from the run seed so floors can present a wider range of terminal behavior.

## Regeneration 22 - Enemy Animation Pass

Goal: make enemies feel like combat creatures instead of floating targets.

Implemented:
- Added attack pulse and hurt pulse motion to `BasicEnemyAI`.
- Melee enemies now trigger a visible attack pop when they strike.
- Damage now produces a more readable body reaction instead of only a color flash.

## Regeneration 23 - Terminal Runtime UI Recovery

Goal: make terminal puzzles usable even when the scene has no authored UI canvases.

Implemented:
- Added runtime fallback overlays for keypad and switch terminals so they can create their own UI panels and text when no scene UI is present.
- Keypad terminals now accept numeric keyboard input, backspace, enter, and escape directly in the popup.
- Switch terminals now expose a runtime status panel and keyboard-driven toggle input so they can be solved without a prebuilt UI hierarchy.

## Regeneration 24 - Seeded Terminal Identity

Goal: give each terminal its own deterministic identity instead of treating all terminals like identical shells.

Implemented:
- Added a per-terminal seed to puzzle terminals and used it to drive challenge mode selection and puzzle parameters.
- Terminal challenge behavior is now generated from the floor seed path, which keeps a run reproducible while still giving each terminal its own flavor.

## Regeneration 25 - True Player Death

Goal: make the health system matter by allowing the player to actually die instead of always auto-respawning.

Implemented:
- Added a true death state to `PlayerController`.
- Damage now kills the player when health reaches zero unless respawn-on-death is explicitly enabled.
- Death disables movement, reveals the cursor, hides the crosshair, and marks the player as dead.

## Regeneration 26 - Terminal Wire Readability

Goal: make terminal wiring feel grounded and clearly readable when a terminal is solved.

Implemented:
- Terminal circuit lines now use the generated ground path when available and sit closer to the floor surface.
- Solved terminals turn their wire to a bright green state and update their status label to OFFLINE.

## Regeneration 27 - Environment Bloom And Skybox

Goal: make the generated arena feel more refined and less raw.

Implemented:
- Added a runtime environment volume with bloom, vignette, color adjustments, film grain, white balance, and lens distortion.
- Added a procedural skybox and stronger ambient color tuning for the arena.
- Kept the fog/directional-light setup while layering the post-processing on top so the scene reads cleaner and more atmospheric.

## Regeneration 28 - Runtime Terminal UI

Goal: give the seeded puzzle terminals a real in-game interface instead of relying on invisible logic.

Implemented:
- Recreated the puzzle terminal script as a seeded runtime challenge controller.
- Added a reusable runtime overlay with title, mode, seed, status, instruction, detail, progress bar, and action buttons.
- Wired the UI to ten challenge modes so terminals now present visible, playable minigame states instead of instantly solving in the background.
- Added keyboard shortcuts and runtime EventSystem fallback so the UI works even when the scene does not author a terminal canvas ahead of time.
