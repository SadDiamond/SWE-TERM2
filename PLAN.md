# SWE-TERM2 Master Plan (Unity Arena Shooter)

## Project Intent
This project is a movement-heavy Unity arena shooter where each run progresses through structured cycles:

- 5 combat floors (same theme)
- 1 shop floor
# SWE-TERM2 Master Plan — Production-Grade Roadmap

This document transforms the current prototype into a high-polish roguelike arena shooter with clear milestones, engineering scoping, and validation criteria aligned to a deliverable that would be suitable for public demos or storefront-quality prototypes.

Key user requirements emphasized in this revision:
- Roguelike structure: curated run cadence (5 combat floors → shop → boss) with seeded/trackable generation.
- Distinct boss-locked weapon unlocks: the player ends runs with 2–4 unique gun types unlocked by bosses.
- Dynamic, in-place level transitions with cinematic animation (no load-screen gate jumps).
- Higher-fidelity procedural maps: layered decoration, props, lighting, and readable navigation.
- Steam-quality polish targets: robust VFX/SFX, responsive UI, consistent animations, accessibility and performance tuning.

This file is both the development roadmap and the handoff document for engineers, designers, and artists.

---

## High-level Delivery Goals

- Deliver a playable, repeatable roguelike run that feels fair and satisfying.
- Provide 2–4 distinct weapon archetypes unlocked across boss encounters.
- Procedural arenas that read well visually and mechanically (cover, sight-lines, movement flow).
- Seamless dynamic transitions between floors with camera/tween/VFX choreography.
- Deliver a polished demo route with performance budgets and QA steps.

---

## Major Workstreams & Milestones (Production-First)

1) Production Plan + Roadmap (this document) — status: done

2) Tech Spike: Generator Visuals & Readability — deliverables:
	- Tile blending, prop population, ambient lighting variants, and nav-safe placement rules.
	- Macro-to-micro rule set for eye-guiding composition (entrances, combat arenas, cover placement).
	- Exportable debug visualization for pathing, sightlines, and spawn zones.

3) Tech Spike: Dynamic Level Transition System — deliverables:
	- In-place level swap choreography (camera tween, room dissolve shader, particle burst, UI transition).
	- Seamless state migration (player, enemies, pickups, spawn logic) with undo-safe operations.
	- Transition timeline templates to iterate on timings.

4) Weapon System & Boss Unlocks — deliverables:
	- Weapon archetype framework (melee/primary/secondary/utility patterns as needed).
	- 2–4 boss-locked unique weapons (distinct visuals, fire patterns, upgrades).
	- Weapon pickup/purchase/upgrade hooks and UI preview screens in shop.

5) Roguelike Systems & Meta-Progression — deliverables:
	- Deterministic run seeds + run replayability controls.
	- Shop, relics, and lightweight meta unlocks (permanent cosmetics or variants optional).
	- Save/export for unlocked weapons between runs.

6) Combat & Encounter Design — deliverables:
	- Per-theme enemy mixes with per-floor scaling.
	- Encounter templates (ambush, wave, escort, puzzle-combat hybrids).
	- Clear telegraph and readout system for enemy attacks.

7) Polish Pass — deliverables:
	- VFX (impact, muzzle, screen-space), sound SFX/MUSIC cues per theme, and motion-anchored particles.
	- Animations for player and enemies (hit react, death, idle, weapon-specific reload/charge).
	- UI/UX polish for shop, HUD, transitions, and accessibility (contrast, font sizes).

8) QA, Balancing, and Release Prep — deliverables:
	- Playtest script and telemetry probes (damage taken, clear times, failure reasons).
	- Balancing passes for weapon unlock pacing, enemy counts, and shop economy.
	- Demo build with checklist and release notes.

---

## Expanded Technical Specs (selected)

Generator Visual & Readability Checklist
- Tile blending: Implement adjacency rules so floor/wall seams are blended via detail props and decals.
- Visual hierarchy: Always mark entry/exit and major sight-lines with distinct lighting or props.
- Combat pocketing: Create small, medium, and large combat pocket templates to guarantee cover variety and movement flow.
- Props & clutter: Use probabilistic prop placement with rule-sets that avoid blocking nav or objectives.
- Lighting: Theme-based ambient color + baked probe presets per theme variant.

Dynamic Level Transition System (technical notes)
- Transition must be deterministic and atomic: either succeeds fully or rolls back cleanly.
- Use a two-phase process: (1) Capture current floor snapshot + disable new spawns, (2) Spawn/animate new floor elements off-screen, (3) Play swap animation (camera + dissolve), (4) Swap references and enable actors.
- Visuals: combine screen-space shader dissolve + lens particle burst + audio riser.
- Performance: pre-warm pooled objects for the next floor during the tail of combat to avoid hitches.

Weapon System & Unlock Flow
- Weapon archetype data-driven: ScriptableObjects or serializable configs with: fire pattern, recoil, spread, visual prefab, audio profile, upgrade path.
- Boss unlock flow: on boss defeat, present weapon preview cinematic + one-time unlock toast + attach to shop purchases.
- Balance: each boss should introduce a new tactical option not strictly 'better' but changing playstyle.

Roguelike & Meta Systems
- Run seed: expose a run seed (64-bit int) that reconstructs generation for debugging and sharing.
- Shop effects: persistent transient economy (currency per run) and optional soft meta progression (cosmetics or variant unlocks).
- Permadeath: default run resets most progress except boss-weapon unlocks and non-gameplay cosmetics.

---

## Implementation Roadmap (iterative sprints)

Sprint 0 (1 week): Research & tech spikes
- Create generator visual proof-of-concept and transition proof-of-concept.
- Deliverables: small scenes showcasing new generator visuals and a working transition animation between two sample floors.

Sprint 1 (2 weeks): Core systems & weapon framework
- Implement weapon archetype data and base UI integration.
- Implement boss unlock stub flow and persistence.

Sprint 2 (2–3 weeks): Generator integration & level polish
- Integrate visual generator into macro pipeline and add prop placement rules.
- Add lighting presets per theme.

Sprint 3 (2–3 weeks): Dynamic transitions + performance
- Finalize level transition system and pre-warm pools.

Sprint 4 (3 weeks): Content pass — weapons, bosses, encounters
- Implement 2–4 weapon prototypes and 3 boss fights as gating content.

Sprint 5 (2 weeks): Polish, audio, UX, QA
- SFX, music, HUD polish, accessibility adjustments, and release checklist.

---

## Validation & Acceptance Criteria (concrete)

- Generation: 90% of generated rooms must have reachable exit and valid nav mesh. (Use automated checks / editor tools.)
- Transitions: level-swap animation runs at 60fps on target hardware without frame spikes >40ms. (Profile with Unity Profiler.)
- Weapons: each boss unlocks a unique weapon with a distinct tactical identity; playtests show >30% players try new weapon after unlock.
- Roguelike: run seed reproduces the same floor layout and selected props across machines.
- Polish: no game-crashing errors in 100 consecutive runs (smoke test), and critical UI flows are accessible with keyboard/controller.

---

## Delivery Checklist (pre-demo)

- [ ] Generator visual proof-of-concept scene
- [ ] Transition proof-of-concept scene
- [ ] Weapon archetype framework & 2 weapon prototypes
- [ ] Boss unlock cinematic & persistence
- [ ] 3 fully playable cycles through combat/shop/boss
- [ ] HUD polished and wired in-scene
- [ ] Audio and VFX baseline
- [ ] Playtest script and telemetry enabled
- [ ] Demo build created and verified across target hardware

---

## Risks and Mitigations (short)

- Risk: Large content work (weapons, bosses, VFX) can push timeline.
  Mitigation: Release vertical slices early; prioritize one complete weapon + boss early.

- Risk: Generator changes could break existing pipelines.
  Mitigation: Keep generator modular, expose debug flags, and maintain fallbacks to the old generator.

---