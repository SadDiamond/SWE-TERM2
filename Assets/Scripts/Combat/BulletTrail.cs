using UnityEngine;

public class BulletTrail : MonoBehaviour
{
    public Color startColor = new Color(1f, 0.95f, 0.58f, 1f);
    public Color endColor = new Color(1f, 0.45f, 0.1f, 0f);
    public float trailTime = 0.055f;
    public float startWidth = 0.055f;

    private TrailRenderer trail;
    private static Material sharedTrailMaterial;

    void Awake()
    {
        EnsureTrail();
        ApplyVisuals();
    }

    public void Configure(Color color, float width, float time)
    {
        startColor = Color.Lerp(color, Color.white, 0.28f);
        endColor = new Color(color.r, color.g, color.b, 0f);
        startWidth = Mathf.Max(0.01f, width);
        trailTime = Mathf.Max(0.015f, time);
        EnsureTrail();
        ApplyVisuals();
    }

    private void EnsureTrail()
    {
        trail = GetComponent<TrailRenderer>();
        if (trail == null)
            trail = gameObject.AddComponent<TrailRenderer>();
    }

    private void ApplyVisuals()
    {
        if (trail == null) return;

        trail.time = trailTime;
        trail.startWidth = startWidth;
        trail.endWidth = 0f;
        trail.numCapVertices = 2;
        trail.alignment = LineAlignment.View;

        if (sharedTrailMaterial == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            sharedTrailMaterial = new Material(shader);
            if (sharedTrailMaterial.HasProperty("_EmissionColor"))
                sharedTrailMaterial.EnableKeyword("_EMISSION");
        }
        trail.sharedMaterial = sharedTrailMaterial;

        Material material = sharedTrailMaterial;
        if (material == null) return;

        trail.startColor = startColor;
        trail.endColor = endColor;
    }
}
