using UnityEngine;

// Chain step that runs after WFC collapse. Subclasses can read the generator's grid
// via its public accessors and rewrite tiles to enforce invariants the local socket
// rules can't express (reachability, decoration density, theming).
public abstract class PostProcessor : MonoBehaviour
{
    public abstract void Process(WFCGenerator3D generator);
}
