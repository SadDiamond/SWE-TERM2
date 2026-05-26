using UnityEngine;

// Region kinds the macro pass paints onto a 2D blueprint. The WFC micro pass then uses
// these to pre-constrain its possibility space per cell.
public enum MacroRegion
{
    Open,       // unconstrained interior (let WFC fill freely)
    Wall,       // forced solid mass at y>=1
    Corridor,   // narrow walkable path; y>=1 is restricted to air-like tiles
    Spawn,      // player start (y=0 forced to spawn tile)
    Goal        // objective (y=0 forced to goal tile)
}

// Strategy pattern: concrete macro generators implement Generate() to paint a blueprint.
// Attach a concrete subclass component (e.g. RoomsAndCorridorsMacro) to a GameObject
// and wire it into WFCGenerator3D.macroGenerator.
public abstract class MacroGenerator : MonoBehaviour
{
    public abstract MacroRegion[,] Generate(int width, int length, int seed);
}
