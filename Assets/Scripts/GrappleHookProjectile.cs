using UnityEngine;

public class GrappleHookProjectile : MonoBehaviour
{
    private enum HookState
    {
        Flying,
        Retracting,
        Latched
    }

    private PlayerController owner;
    private float speed;
    private float radius;
    private float maxDistance;
    private LayerMask surfaceMask;
    private Vector3 direction;
    private Vector3 startPosition;
    private HookState state;
    private Transform anchorTransform;
    private Vector3 anchorLocalPoint;
    private Vector3 anchorNormal;
    private Vector3 retractPoint;
    private Vector3 bounceVelocity;
    private float retractTimer;
    private Renderer[] renderers;
    private readonly RaycastHit[] hitBuffer = new RaycastHit[16];
    private static Material sharedBodyMaterial;
    private static Material sharedAccentMaterial;

    public bool IsLatched => state == HookState.Latched;
    public bool IsRetracting => state == HookState.Retracting;
    public Vector3 AnchorNormal => anchorNormal;
    public float Travel01 => Mathf.Clamp01(Vector3.Distance(startPosition, transform.position) / Mathf.Max(0.01f, maxDistance));

    public Vector3 CurrentPoint
    {
        get
        {
            if (state == HookState.Latched && anchorTransform != null)
                return anchorTransform.TransformPoint(anchorLocalPoint);
            return transform.position;
        }
    }

    public void Initialize(PlayerController grappleOwner, Vector3 position, Vector3 forward, float projectileSpeed, float projectileRadius, float projectileRange, LayerMask grappleSurfaceMask, Color bodyColor, Color accentColor)
    {
        owner = grappleOwner;
        speed = Mathf.Max(1f, projectileSpeed);
        radius = Mathf.Max(0.02f, projectileRadius);
        maxDistance = Mathf.Max(1f, projectileRange);
        surfaceMask = grappleSurfaceMask;
        direction = forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;
        startPosition = position;
        state = HookState.Flying;
        bounceVelocity = Vector3.zero;
        retractPoint = position;

        transform.position = position;
        Vector3 initialTravel = GetTravelVelocity();
        transform.rotation = Quaternion.LookRotation(initialTravel.sqrMagnitude > 0.0001f ? initialTravel.normalized : direction);
        if (renderers == null || renderers.Length == 0)
            BuildVisual(bodyColor, accentColor);
    }

    private void Update()
    {
        if (state == HookState.Latched)
        {
            if (anchorTransform != null)
            {
                transform.position = anchorTransform.TransformPoint(anchorLocalPoint);
                Vector3 facing = anchorNormal.sqrMagnitude > 0.0001f ? anchorNormal : -direction;
                transform.rotation = Quaternion.LookRotation(facing, Vector3.up);
            }
            return;
        }

        if (state == HookState.Retracting)
        {
            UpdateRetract();
            return;
        }

        if (owner == null)
        {
            Destroy(gameObject);
            return;
        }

        Vector3 travelVelocity = GetTravelVelocity();
        if (travelVelocity.sqrMagnitude <= 0.0001f)
            travelVelocity = direction * speed;
        Vector3 castDirection = travelVelocity.normalized;
        float step = travelVelocity.magnitude * Time.deltaTime;
        Vector3 currentPosition = transform.position;
        Vector3 castOrigin = currentPosition - castDirection * radius * 0.5f;
        int hitCount = Physics.SphereCastNonAlloc(castOrigin, radius, castDirection, hitBuffer, step + radius, surfaceMask, QueryTriggerInteraction.Ignore);
        RaycastHit hit;
        if (TryGetValidHit(hitBuffer, hitCount, out hit))
        {
            transform.position = hit.point - castDirection * radius * 0.3f;
            if (!owner.NotifyGrappleHookHit(this, hit))
            {
                Vector3 bounceDirection = Vector3.Reflect(castDirection, hit.normal).normalized;
                owner.NotifyGrappleHookInvalidHit(this, hit, bounceDirection);
            }
            return;
        }

        transform.position = currentPosition + travelVelocity * Time.deltaTime;
        transform.rotation = Quaternion.LookRotation(castDirection);
        if (Vector3.Distance(startPosition, transform.position) >= maxDistance)
        {
            owner.NotifyGrappleHookExpired(this);
            gameObject.SetActive(false);
        }
    }

    public void LatchTo(Transform targetTransform, Vector3 worldPoint, Vector3 normal)
    {
        state = HookState.Latched;
        anchorTransform = targetTransform;
        anchorLocalPoint = targetTransform != null ? targetTransform.InverseTransformPoint(worldPoint) : worldPoint;
        anchorNormal = normal.sqrMagnitude > 0.0001f ? normal.normalized : -direction;
        transform.position = worldPoint;
        transform.rotation = Quaternion.LookRotation(anchorNormal, Vector3.up);
    }

    public void SetDirection(Vector3 newDirection)
    {
        if (newDirection.sqrMagnitude <= 0.0001f)
            return;
        direction = newDirection.normalized;
        if (state != HookState.Latched)
            transform.rotation = Quaternion.LookRotation(direction);
    }

    public void BeginRetract(Vector3 bouncePoint, Vector3 bounceDirection)
    {
        state = HookState.Retracting;
        anchorTransform = null;
        bounceVelocity = (bounceDirection.sqrMagnitude > 0.0001f ? bounceDirection.normalized : -direction) * speed * 0.22f;
        retractTimer = 0f;
        retractPoint = bouncePoint;
        transform.position = bouncePoint;
        transform.rotation = Quaternion.LookRotation(bounceVelocity.sqrMagnitude > 0.0001f ? bounceVelocity.normalized : -direction, Vector3.up);
    }

    private Vector3 GetTravelVelocity()
    {
        return direction * speed;
    }

    private bool TryGetValidHit(RaycastHit[] hits, int hitCount, out RaycastHit validHit)
    {
        validHit = default;
        if (hits == null || hitCount <= 0)
            return false;

        float bestDistance = float.MaxValue;
        Transform ownerRoot = owner != null ? owner.transform : null;
        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = hits[i];
            if (hit.collider == null)
                continue;
            if (ownerRoot != null && (hit.collider.transform == ownerRoot || hit.collider.transform.IsChildOf(ownerRoot)))
                continue;
            if (hit.distance >= bestDistance)
                continue;

            bestDistance = hit.distance;
            validHit = hit;
        }

        return bestDistance < float.MaxValue;
    }

    private void UpdateRetract()
    {
        if (owner == null)
        {
            Destroy(gameObject);
            return;
        }

        Vector3 returnPoint = owner.GetGrappleReturnPoint();
        retractTimer += Time.deltaTime;
        float tighten01 = Mathf.Clamp01(retractTimer / 0.18f);
        bounceVelocity = Vector3.Lerp(bounceVelocity, Vector3.zero, Time.deltaTime * Mathf.Lerp(5.5f, 14f, tighten01));
        retractPoint += bounceVelocity * Time.deltaTime;
        Vector3 toReturn = returnPoint - retractPoint;
        float returnDistance = toReturn.magnitude;
        if (returnDistance <= 0.12f)
        {
            owner.NotifyGrappleHookExpired(this);
            gameObject.SetActive(false);
            return;
        }

        Vector3 pullDir = toReturn / Mathf.Max(0.001f, returnDistance);
        float retractSpeed = speed * Mathf.Lerp(0.58f, 1.18f, tighten01);
        retractPoint += pullDir * retractSpeed * Time.deltaTime;
        transform.position = retractPoint;
        Vector3 facing = pullDir + bounceVelocity * 0.02f;
        transform.rotation = Quaternion.LookRotation(facing.sqrMagnitude > 0.0001f ? facing.normalized : pullDir, Vector3.up);
    }

    private void BuildVisual(Color bodyColor, Color accentColor)
    {
        GameObject core = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        core.name = "HookCore";
        core.transform.SetParent(transform, false);
        core.transform.localPosition = Vector3.zero;
        core.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        core.transform.localScale = new Vector3(0.032f, 0.095f, 0.032f);

        GameObject tip = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        tip.name = "HookTip";
        tip.transform.SetParent(transform, false);
        tip.transform.localPosition = new Vector3(0f, 0f, 0.105f);
        tip.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        tip.transform.localScale = new Vector3(0.014f, 0.04f, 0.014f);

        GameObject finLeft = GameObject.CreatePrimitive(PrimitiveType.Cube);
        finLeft.name = "HookClawLeft";
        finLeft.transform.SetParent(transform, false);
        finLeft.transform.localPosition = new Vector3(-0.036f, 0f, 0.075f);
        finLeft.transform.localRotation = Quaternion.Euler(0f, -34f, 0f);
        finLeft.transform.localScale = new Vector3(0.014f, 0.03f, 0.085f);

        GameObject finRight = GameObject.CreatePrimitive(PrimitiveType.Cube);
        finRight.name = "HookClawRight";
        finRight.transform.SetParent(transform, false);
        finRight.transform.localPosition = new Vector3(0.036f, 0f, 0.075f);
        finRight.transform.localRotation = Quaternion.Euler(0f, 34f, 0f);
        finRight.transform.localScale = new Vector3(0.014f, 0.03f, 0.085f);

        renderers = GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null)
                continue;
            Collider collider = renderers[i].GetComponent<Collider>();
            if (collider != null)
                Destroy(collider);

            renderers[i].sharedMaterial = i == 0
                ? GetSharedMaterial(ref sharedBodyMaterial, "RuntimeGrappleHookBody", bodyColor)
                : GetSharedMaterial(ref sharedAccentMaterial, "RuntimeGrappleHookAccent", accentColor);
        }
    }

    private static Material GetSharedMaterial(ref Material cachedMaterial, string materialName, Color color)
    {
        if (cachedMaterial != null)
            return cachedMaterial;

        cachedMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
        cachedMaterial.name = materialName;
        ApplyColor(cachedMaterial, color);
        return cachedMaterial;
    }

    private static void ApplyColor(Material material, Color color)
    {
        if (material == null)
            return;
        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color"))
            material.SetColor("_Color", color);
        if (material.HasProperty("_EmissionColor"))
            material.SetColor("_EmissionColor", color * 1.6f);
    }
}
