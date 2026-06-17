using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class WeaponSandboxController : MonoBehaviour
{
    private const string TargetRootName = "_WeaponLabTargets";
    private PlayerController player;
    private Gun gun;
    private Transform targetRoot;
    private float rebuildTimer;

    private void Start()
    {
        player = FindAnyObjectByType<PlayerController>();
        gun = FindAnyObjectByType<Gun>();
        CybergrindRunState state = CybergrindRunState.GetOrCreate();
        for (int i = 0; i < 9; i++) state.UnlockWeapon(i);
        if (gun != null) gun.EquipPreset(0);
        ClearProgressionEnemies();
        BuildRange();
        BuildProjectileTurret();
        BuildGuide();
    }

    private void Update()
    {
        if (player != null)
            player.Heal(player.EffectiveMaxHealth);

        rebuildTimer -= Time.deltaTime;
        if (rebuildTimer <= 0f)
        {
            rebuildTimer = 1.5f;
            if (targetRoot == null || targetRoot.childCount < 5)
                BuildRange();
        }
    }

    private void ClearProgressionEnemies()
    {
        BasicEnemyAI[] enemies = FindObjectsByType<BasicEnemyAI>(FindObjectsSortMode.None);
        for (int i = 0; i < enemies.Length; i++)
            if (enemies[i] != null) Destroy(enemies[i].gameObject);
    }

    private void BuildRange()
    {
        if (targetRoot != null) Destroy(targetRoot.gameObject);
        targetRoot = new GameObject(TargetRootName).transform;
        Vector3 origin = player != null ? player.transform.position : Vector3.zero;
        Vector3 forward = player != null ? Vector3.ProjectOnPlane(player.transform.forward, Vector3.up).normalized : Vector3.forward;
        if (forward.sqrMagnitude < 0.1f) forward = Vector3.forward;
        Vector3 right = Vector3.Cross(Vector3.up, forward);

        CreateTarget(origin + forward * 12f - right * 4f, new Vector3(1.2f, 2.4f, 1.2f), 500f, "LIGHT");
        CreateTarget(origin + forward * 15f, new Vector3(1.8f, 3.2f, 1.8f), 1200f, "HEAVY");
        CreateTarget(origin + forward * 12f + right * 4f, new Vector3(1.2f, 2.4f, 1.2f), 500f, "LIGHT");
        CreateTarget(origin + forward * 22f - right * 5f, new Vector3(1f, 2f, 1f), 700f, "RANGE");
        CreateTarget(origin + forward * 22f, new Vector3(1f, 2f, 1f), 700f, "RANGE");
        CreateTarget(origin + forward * 22f + right * 5f, new Vector3(1f, 2f, 1f), 700f, "RANGE");
    }

    private void CreateTarget(Vector3 position, Vector3 scale, float health, string label)
    {
        if (Physics.Raycast(position + Vector3.up * 15f, Vector3.down, out RaycastHit floorHit, 40f, ~0, QueryTriggerInteraction.Ignore))
            position.y = floorHit.point.y + scale.y * 0.5f;
        GameObject target = GameObject.CreatePrimitive(PrimitiveType.Cube);
        target.name = $"LabTarget_{label}";
        target.transform.SetParent(targetRoot, true);
        target.transform.position = position;
        target.transform.localScale = scale;
        Renderer renderer = target.GetComponent<Renderer>();
        renderer.material.color = label == "HEAVY" ? new Color(0.28f, 0.3f, 0.34f) : new Color(0.16f, 0.2f, 0.23f);
        Target damageTarget = target.AddComponent<Target>();
        damageTarget.maxHealth = health;
        damageTarget.damageColor = new Color(1f, 0.3f, 0.12f);
    }

    private void BuildGuide()
    {
        Canvas canvas = ProjectStructureUIRoot.GetOrCreateCanvas();
        GameObject panel = new GameObject("WeaponLabGuide");
        panel.transform.SetParent(canvas.transform, false);
        RectTransform rect = panel.AddComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -18f);
        rect.sizeDelta = new Vector2(680f, 58f);
        panel.AddComponent<Image>().color = new Color(0.015f, 0.022f, 0.028f, 0.88f);
        TMP_Text text = new GameObject("WeaponLabText").AddComponent<TextMeshProUGUI>();
        text.transform.SetParent(panel.transform, false);
        RectTransform textRect = text.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(14f, 8f);
        textRect.offsetMax = new Vector2(-14f, -8f);
        ProjectStructureUIRoot.ApplyDefaultFont(text);
        text.alignment = TextAlignmentOptions.Center;
        text.fontSize = 12f;
        text.color = new Color(0.84f, 0.94f, 0.98f);
        text.text = "WEAPON LAB   1/2/3 FAMILY   Q/E VARIANT   LMB PRIMARY   RMB ABILITY\nTargets regenerate automatically. Damage and progression are disabled.";
    }

    private void BuildProjectileTurret()
    {
        if (player == null) return;
        Vector3 forward = Vector3.ProjectOnPlane(player.transform.forward, Vector3.up).normalized;
        if (forward.sqrMagnitude < 0.1f) forward = Vector3.forward;
        Vector3 right = Vector3.Cross(Vector3.up, forward);
        GameObject turret = GameObject.CreatePrimitive(PrimitiveType.Cube);
        turret.name = "WeaponLabProjectileTurret";
        turret.transform.position = player.transform.position + forward * 18f + right * 9f + Vector3.up;
        turret.transform.localScale = new Vector3(1.2f, 2f, 1.2f);
        turret.GetComponent<Renderer>().material.color = new Color(0.28f, 0.08f, 0.06f);
        WeaponLabProjectileTurret emitter = turret.AddComponent<WeaponLabProjectileTurret>();
        emitter.target = player;
    }
}
