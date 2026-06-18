using UnityEngine;

public class ShopStationPresentation : MonoBehaviour
{
    public Light displayLight;
    public Transform productRoot;
    public Renderer[] accentRenderers;

    private Vector3 productBasePosition;
    private float baseLightIntensity;
    private Color baseLightColor = Color.white;
    private Color[] accentBaseColors;
    private bool focused;
    private bool spent;
    private float deniedTimer;

    private void Start()
    {
        if (productRoot != null)
            productBasePosition = productRoot.localPosition;
        if (displayLight != null)
        {
            baseLightIntensity = displayLight.intensity;
            baseLightColor = displayLight.color;
        }

        if (accentRenderers != null && accentRenderers.Length > 0)
        {
            accentBaseColors = new Color[accentRenderers.Length];
            for (int i = 0; i < accentRenderers.Length; i++)
            {
                Renderer renderer = accentRenderers[i];
                accentBaseColors[i] = renderer != null && renderer.material != null ? renderer.material.color : Color.white;
            }
        }
    }

    private void Update()
    {
        deniedTimer = Mathf.Max(0f, deniedTimer - Time.deltaTime);

        if (displayLight != null)
        {
            displayLight.color = Color.Lerp(displayLight.color, deniedTimer > 0f ? new Color(1f, 0.12f, 0.08f) : baseLightColor, Time.deltaTime * 9f);
            float target = spent ? 0.15f : deniedTimer > 0f ? baseLightIntensity * 1.8f : focused ? baseLightIntensity * 1.55f : baseLightIntensity;
            displayLight.intensity = Mathf.MoveTowards(displayLight.intensity, target, Time.deltaTime * 4f);
        }

        if (accentRenderers != null && accentBaseColors != null)
        {
            float pulse = 0.88f + Mathf.Sin(Time.time * 4.8f) * 0.08f;
            float focusBoost = focused ? 1.18f : 1f;
            float denyBoost = deniedTimer > 0f ? 1.4f : 1f;
            float spentFade = spent ? 0.32f : 1f;
            for (int i = 0; i < accentRenderers.Length && i < accentBaseColors.Length; i++)
            {
                Renderer renderer = accentRenderers[i];
                if (renderer == null || renderer.material == null) continue;
                Color targetColor = deniedTimer > 0f
                    ? Color.Lerp(accentBaseColors[i], new Color(1f, 0.16f, 0.08f, accentBaseColors[i].a), 0.7f)
                    : accentBaseColors[i];
                renderer.material.color = Color.Lerp(renderer.material.color, targetColor * pulse * focusBoost * denyBoost * spentFade, Time.deltaTime * 7f);
            }
        }

        if (productRoot != null && !spent)
        {
            float lift = focused ? 0.035f : 0f;
            productRoot.localPosition = Vector3.Lerp(productRoot.localPosition, productBasePosition + Vector3.up * lift, Time.deltaTime * 8f);
            productRoot.localRotation = Quaternion.Slerp(productRoot.localRotation, Quaternion.Euler(0f, focused ? 6f : 0f, 0f), Time.deltaTime * 4.5f);
        }
    }

    public void SetFocused(bool value)
    {
        focused = value && !spent;
    }

    public void SetSpent()
    {
        spent = true;
        focused = false;
        if (productRoot != null)
            productRoot.gameObject.SetActive(false);
        if (accentRenderers != null)
        {
            for (int i = 0; i < accentRenderers.Length; i++)
            {
                Renderer renderer = accentRenderers[i];
                if (renderer == null || renderer.material == null) continue;
                renderer.material.color *= 0.32f;
            }
        }
    }

    public void FlashDenied()
    {
        if (!spent)
            deniedTimer = 0.45f;
    }
}
