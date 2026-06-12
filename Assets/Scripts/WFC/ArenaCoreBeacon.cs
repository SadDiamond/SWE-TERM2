using UnityEngine;

public class ArenaCoreBeacon : Interactable
{
    public CybergrindArenaDirector director;
    public float bobHeight = 0.22f;
    public float bobSpeed = 1.8f;
    public float spinSpeed = 36f;

    private Vector3 basePosition;
    private Renderer cachedRenderer;
    private Collider cachedCollider;
    private bool activated;
    private Transform lensRoot;
    private Light pulseLight;

    protected override void Start()
    {
        cachedRenderer = GetComponent<Renderer>();
        cachedCollider = GetComponent<Collider>();
        highlightRenderer = highlightRenderer != null ? highlightRenderer : cachedRenderer;
        interactionRange = 4.4f;
        promptMessage = "Enter the core";
        basePosition = transform.position;
        EnsurePulseLight();
        CacheLensRoot();
        base.Start();
    }

    protected override void Update()
    {
        base.Update();
        if (activated)
            return;

        float bob = Mathf.Sin(Time.time * bobSpeed) * bobHeight;
        transform.position = basePosition + Vector3.up * bob;
        transform.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.World);
        if (lensRoot != null)
        {
            float pulse = 1f + Mathf.Sin(Time.time * 4.8f) * 0.08f;
            lensRoot.localScale = Vector3.one * pulse;
        }
        if (pulseLight != null)
        {
            pulseLight.intensity = 1.9f + Mathf.Sin(Time.time * 5.2f) * 0.35f;
            pulseLight.range = 5.2f + Mathf.Sin(Time.time * 4f) * 0.25f;
        }
    }

    public override void OnInteract(PlayerController player)
    {
        if (activated)
            return;

        activated = true;
        promptMessage = "Core link open";

        if (cachedCollider != null)
            cachedCollider.enabled = false;

        if (cachedRenderer != null)
            cachedRenderer.enabled = false;

        for (int i = transform.childCount - 1; i >= 0; i--)
            transform.GetChild(i).gameObject.SetActive(false);

        if (director != null)
            director.NotifyCoreReached();
    }

    private void CacheLensRoot()
    {
        lensRoot = transform.Find("CoreLens");
    }

    private void EnsurePulseLight()
    {
        pulseLight = GetComponentInChildren<Light>();
        if (pulseLight != null)
            return;

        GameObject lightRoot = new GameObject("CorePulseLight");
        lightRoot.transform.SetParent(transform, false);
        lightRoot.transform.localPosition = new Vector3(0f, 2.2f, 0f);
        pulseLight = lightRoot.AddComponent<Light>();
        pulseLight.type = LightType.Point;
        pulseLight.intensity = 1.9f;
        pulseLight.range = 5.2f;
        pulseLight.color = new Color(0.56f, 0.94f, 1f, 1f);
    }
}
