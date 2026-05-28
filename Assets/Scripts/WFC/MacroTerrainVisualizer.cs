using UnityEngine;

/// <summary>
/// Post-processor that visualizes macro terrain features (pits, dips/terrain, hills)
/// with colored visual meshes for better readability of the macro blueprint.
/// </summary>
public class MacroTerrainVisualizer : PostProcessor
{
    [Header("Visualization")]
    public bool visualizePits = true;
    [SerializeField] private Color pitColor = new Color(1f, 1f, 0f, 0.4f); // Yellow
    
    public bool visualizeTerrainDips = true;
    [SerializeField] private Color terrainColor = new Color(1f, 0.65f, 0f, 0.4f); // Orange
    
    public bool visualizeHills = true;
    [SerializeField] private Color hillColor = new Color(0.65f, 0.4f, 0.2f, 0.4f); // Brown

    public string containerName = "_MacroTerrainVis";

    public override void Process(WFCGenerator3D generator)
    {
        if (generator == null) return;

        var blueprint = generator.CurrentBlueprint;
        if (blueprint == null) return;

        // Cleanup old visualization
        Transform old = generator.transform.Find(containerName);
        if (old != null)
            UnityEngine.Object.DestroyImmediate(old.gameObject);

        Transform container = new GameObject(containerName).transform;
        container.SetParent(generator.transform, false);

        int width = blueprint.GetLength(0);
        int length = blueprint.GetLength(1);

        for (int x = 0; x < width; x++)
        {
            for (int z = 0; z < length; z++)
            {
                MacroRegion region = blueprint[x, z];

                if (visualizePits && region == MacroRegion.Pit)
                {
                    CreateTerrainVisualizerAt(container, generator, x, z, pitColor, "Pit");
                }
                else if (visualizeTerrainDips && region == MacroRegion.Terrain)
                {
                    CreateTerrainVisualizerAt(container, generator, x, z, terrainColor, "Dip");
                }
                else if (visualizeHills && region == MacroRegion.Hill)
                {
                    CreateTerrainVisualizerAt(container, generator, x, z, hillColor, "Hill");
                }
            }
        }

        Debug.Log($"[MacroTerrainVisualizer] Visualized macro terrain at {container.name}");
    }

    private void CreateTerrainVisualizerAt(Transform parent, WFCGenerator3D generator, int x, int z, Color color, string label)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = $"Vis_{label}_{x}_{z}";
        
        // Remove collider (it's just for visualization)
        var collider = go.GetComponent<Collider>();
        if (collider != null)
            UnityEngine.Object.DestroyImmediate(collider);

        go.transform.SetParent(parent, false);

        // Position at the macro cell center
        Vector3 position = new Vector3(
            generator.transform.position.x + (x * generator.tileSizeXZ) + (generator.tileSizeXZ * 0.5f),
            generator.transform.position.y + 0.1f,
            generator.transform.position.z + (z * generator.tileSizeXZ) + (generator.tileSizeXZ * 0.5f)
        );

        go.transform.position = position;
        
        // Scale to fill the cell
        go.transform.localScale = new Vector3(generator.tileSizeXZ * 0.95f, 0.15f, generator.tileSizeXZ * 0.95f);

        // Set color
        var renderer = go.GetComponent<Renderer>();
        if (renderer != null)
        {
            // Create a simple material with the color
            var mat = new Material(Shader.Find("Standard"));
            mat.color = color;
            mat.SetFloat("_Mode", 3); // Transparent mode
            renderer.material = mat;
        }
    }
}
