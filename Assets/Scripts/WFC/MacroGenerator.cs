using UnityEngine;

// Region kinds the macro pass paints onto a 2D blueprint. The WFC micro pass then uses
// these to pre-constrain its possibility space per cell.
public enum MacroRegion
{
    Open,           // unconstrained interior (let WFC fill freely)
    Wall,           // forced solid mass at y>=1
    Corridor,       // narrow walkable path; y>=1 is restricted to air-like tiles
    Spawn,          // player start (y=0 forced to spawn tile)
    Goal,           // objective (y=0 forced to goal tile)
    ExitPit,        // one-way exit portal (goal marker, nothing above it)
    CombatRoom,     // tagged combat encounter area (for procedural extensions)
    BossRoom,       // preset boss arena (handled specially)
    Shop,           // shop/rest area
    Platform,       // raised central platform (y=0 and y=1 are solid)
    Bridge,         // elevated bridge/path with supports below (y=0 is air, y=1 is floor)
    Pit,            // floor is air/missing (y=0 is air)
    HighCover,      // Tall cover (y=0 floor, y=1 solid)
    LowCover,       // Half cover (y=0 floor, y=1 decoration)
    Hazard,         // Dangerous area (visualized red/orange)
    Terrain,        // 3D elevation (y=0 is solid mass, y=1 is floor)
    Hill,           // Larger 3D elevation (y=0,1 solid, y=2 floor)
    MicroDetail,    // Region reserved for micro-generation (consoles, machinery)
    MicroCrate      // Region reserved for crates/storage
}

// Strategy pattern: concrete macro generators implement Generate() to paint a blueprint.
// Attach a concrete subclass component (e.g. RoomsAndCorridorsMacro) to a GameObject
// and wire it into WFCGenerator3D.macroGenerator.
public abstract class MacroGenerator : MonoBehaviour
{
    public abstract MacroRegion[,] Generate(int width, int length, int seed);
}
