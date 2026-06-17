using UnityEngine;

public class ShopStationPresentation : MonoBehaviour
{
    public Light displayLight;
    public Transform productRoot;
    public Renderer[] accentRenderers;

    private Vector3 productBasePosition;
    private float baseLightIntensity;
    private Color baseLightColor = Color.white;
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
    }

    private void Update()
    {
        if (displayLight != null)
        {
            deniedTimer = Mathf.Max(0f, deniedTimer - Time.deltaTime);
            displayLight.color = Color.Lerp(displayLight.color, deniedTimer > 0f ? new Color(1f, 0.12f, 0.08f) : baseLightColor, Time.deltaTime * 9f);
            float target = spent ? 0.15f : deniedTimer > 0f ? baseLightIntensity * 1.8f : focused ? baseLightIntensity * 1.55f : baseLightIntensity;
            displayLight.intensity = Mathf.MoveTowards(displayLight.intensity, target, Time.deltaTime * 4f);
        }

        if (productRoot != null && !spent)
        {
            float lift = focused ? 0.035f : 0f;
            productRoot.localPosition = Vector3.Lerp(productRoot.localPosition, productBasePosition + Vector3.up * lift, Time.deltaTime * 8f);
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
    }

    public void FlashDenied()
    {
        if (!spent)
            deniedTimer = 0.45f;
    }
}
