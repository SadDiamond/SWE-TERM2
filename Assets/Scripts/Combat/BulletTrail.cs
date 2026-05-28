using UnityEngine;

public class BulletTrail : MonoBehaviour
{
    private TrailRenderer trail;

    void Awake()
    {
        trail = GetComponent<TrailRenderer>();
        if (trail == null)
        {
            trail = gameObject.AddComponent<TrailRenderer>();
        }

        // Configure a sleek, fast bullet trail
        trail.time = 0.05f; // Very short life so it looks like a streak, not a ribbon
        trail.startWidth = 0.05f;
        trail.endWidth = 0f;
        
        // Use a default material if none is assigned (prevents magenta squares)
        if (trail.material == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            trail.material = new Material(shader);
        }
        
        // Glowing yellow/white gradient
        trail.startColor = new Color(1f, 1f, 0.5f, 1f); 
        trail.endColor = new Color(1f, 0.5f, 0f, 0f);
    }
}
