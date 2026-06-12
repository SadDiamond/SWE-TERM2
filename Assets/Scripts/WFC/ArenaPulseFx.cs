using UnityEngine;

public class ArenaPulseFx : MonoBehaviour
{
    public Vector3 baseScale = Vector3.one;
    public float scalePulse = 0.08f;
    public float pulseSpeed = 2.8f;
    public Vector3 rotationDegreesPerSecond = Vector3.zero;
    public Color emissionColor = Color.white;
    public float emissionStrength = 1.0f;
    public float emissionPulse = 0.45f;

    private Renderer cachedRenderer;
    private Material runtimeMaterial;
    private Vector3 initialLocalPosition;

    private void Awake()
    {
        initialLocalPosition = transform.localPosition;
        cachedRenderer = GetComponent<Renderer>();
        if (cachedRenderer != null)
        {
            runtimeMaterial = Application.isPlaying ? cachedRenderer.material : cachedRenderer.sharedMaterial;
            runtimeMaterial.EnableKeyword("_EMISSION");
        }

        if (baseScale == Vector3.one)
            baseScale = transform.localScale;
    }

    private void Update()
    {
        float pulse = 0.5f + Mathf.Sin(Time.time * pulseSpeed) * 0.5f;
        transform.localScale = baseScale * (1f + scalePulse * pulse);

        if (rotationDegreesPerSecond.sqrMagnitude > 0.001f)
            transform.Rotate(rotationDegreesPerSecond * Time.deltaTime, Space.Self);

        if (runtimeMaterial != null && runtimeMaterial.HasProperty("_EmissionColor"))
        {
            float strength = emissionStrength + emissionPulse * pulse;
            runtimeMaterial.SetColor("_EmissionColor", emissionColor * strength);
        }
    }

    public void SetBaseScale(Vector3 scale)
    {
        baseScale = scale;
        transform.localScale = scale;
    }

    public void ResetLocalPosition()
    {
        transform.localPosition = initialLocalPosition;
    }
}
