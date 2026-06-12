using UnityEngine;

public class BulletTrail : MonoBehaviour
{
    public Color startColor = new Color(1f, 0.95f, 0.58f, 1f);
    public Color endColor = new Color(1f, 0.45f, 0.1f, 0f);
    public float trailTime = 0.055f;
    public float startWidth = 0.055f;

    private TrailRenderer trail;

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

        if (trail.sharedMaterial == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            trail.sharedMaterial = new Material(shader);
        }

        Material material = trail.sharedMaterial;
        if (material == null) return;

        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", startColor);
        if (material.HasProperty("_Color"))
            material.SetColor("_Color", startColor);
        if (material.HasProperty("_EmissionColor"))
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", startColor * 1.8f);
        }

        trail.startColor = startColor;
        trail.endColor = endColor;
    }
}
