using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class WeaponSandboxController : MonoBehaviour
{
    private const string SandboxRootName = "_SandboxArena";
    private const string TargetRootName = "_SandboxTargets";

    private PlayerController player;
    private Gun gun;
    private CybergrindArenaDirector director;
    private CybergrindArenaGenerator generator;
    private Transform sandboxRoot;
    private Transform targetRoot;
    private Material floorMaterial;
    private Material darkMaterial;
    private Material accentMaterial;
    private float rebuildTimer;

    private void Start()
    {
        player = FindAnyObjectByType<PlayerController>();
        gun = FindAnyObjectByType<Gun>();
        director = FindAnyObjectByType<CybergrindArenaDirector>();
        generator = FindAnyObjectByType<CybergrindArenaGenerator>();

        CybergrindRunState state = CybergrindRunState.GetOrCreate();
        for (int i = 0; i < 9; i++) state.UnlockWeapon(i);
        if (gun != null) gun.EquipPreset(0);

        BuildSandboxArena();
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

    private void BuildSandboxArena()
    {
        if (director != null)
            director.enabled = false;

        if (generator != null)
        {
            floorMaterial = generator.floorMaterial;
            darkMaterial = generator.darkMaterial;
            accentMaterial = generator.accentMaterial;
            generator.ClearArena();
            generator.enabled = false;
        }

        GameObject oldRoot = GameObject.Find(SandboxRootName);
        if (oldRoot != null) Destroy(oldRoot);

        sandboxRoot = new GameObject(SandboxRootName).transform;
        Vector3 origin = generator != null ? generator.transform.position : Vector3.zero;
        sandboxRoot.position = origin;

        CreateBlock("SandboxFloor", new Vector3(0f, -0.5f, 4f), new Vector3(46f, 1f, 48f), floorMaterial);
        CreateBlock("Backstop", new Vector3(0f, 4f, 27.5f), new Vector3(46f, 9f, 1f), darkMaterial);
        CreateBlock("LeftDeck", new Vector3(-13f, 1f, 10f), new Vector3(9f, 2f, 13f), darkMaterial);
        CreateBlock("RightDeck", new Vector3(13f, 1f, 10f), new Vector3(9f, 2f, 13f), darkMaterial);
        CreateBlock("CenterStep", new Vector3(0f, 0.5f, 17f), new Vector3(8f, 1f, 7f), darkMaterial);
        CreateRamp("LeftRamp", new Vector3(-13f, 0.45f, 0.8f), 2f, 8f, floorMaterial);
        CreateRamp("RightRamp", new Vector3(13f, 0.45f, 0.8f), 2f, 8f, floorMaterial);

        for (int side = -1; side <= 1; side += 2)
        {
            CreateBlock($"Boundary_{side}", new Vector3(side * 23f, 1.4f, 4f), new Vector3(0.5f, 3.8f, 48f), darkMaterial);
            CreateBlock($"DeckAccent_{side}", new Vector3(side * 13f, 2.04f, 10f), new Vector3(7.4f, 0.06f, 11.4f), accentMaterial);
        }

        PlacePlayer(origin + new Vector3(0f, 1.2f, -15f));
    }

    private void PlacePlayer(Vector3 position)
    {
        if (player == null) return;
        CharacterController controller = player.GetComponent<CharacterController>();
        bool wasEnabled = controller != null && controller.enabled;
        if (wasEnabled) controller.enabled = false;
        player.transform.SetPositionAndRotation(position, Quaternion.LookRotation(Vector3.forward));
        if (wasEnabled) controller.enabled = true;
        player.NotifySpawnPlacement(position);
    }

    private void CreateRamp(string name, Vector3 position, float rise, float run, Material material)
    {
        float angle = Mathf.Atan2(rise, run) * Mathf.Rad2Deg;
        float length = Mathf.Sqrt(rise * rise + run * run);
        GameObject ramp = CreateBlock(name, position, new Vector3(7f, 0.45f, length), material);
        ramp.transform.rotation = Quaternion.Euler(-angle, 0f, 0f);
    }

    private GameObject CreateBlock(string name, Vector3 localPosition, Vector3 scale, Material material)
    {
        GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
        block.name = name;
        block.transform.SetParent(sandboxRoot, false);
        block.transform.localPosition = localPosition;
        block.transform.localScale = scale;
        Renderer renderer = block.GetComponent<Renderer>();
        if (renderer != null && material != null)
            renderer.sharedMaterial = material;
        return block;
    }

    private void BuildRange()
    {
        if (targetRoot != null) Destroy(targetRoot.gameObject);
        targetRoot = new GameObject(TargetRootName).transform;
        targetRoot.SetParent(sandboxRoot, false);
        CreateTarget(new Vector3(-13f, 0f, 10f), new Vector3(1.2f, 2.4f, 1.2f), 500f, "LIGHT");
        CreateTarget(new Vector3(0f, 0f, 14f), new Vector3(1.8f, 3.2f, 1.8f), 1200f, "HEAVY");
        CreateTarget(new Vector3(13f, 0f, 10f), new Vector3(1.2f, 2.4f, 1.2f), 500f, "LIGHT");
        CreateTarget(new Vector3(-6f, 0f, 23f), Vector3.one * 1.4f, 700f, "RANGE");
        CreateTarget(new Vector3(0f, 0f, 23f), Vector3.one * 1.4f, 700f, "RANGE");
        CreateTarget(new Vector3(6f, 0f, 23f), Vector3.one * 1.4f, 700f, "RANGE");
    }

    private void CreateTarget(Vector3 localPosition, Vector3 scale, float health, string label)
    {
        float surfaceY = ResolveSandboxSurfaceY(localPosition);
        localPosition.y = surfaceY + scale.y * 0.5f;
        GameObject target = GameObject.CreatePrimitive(PrimitiveType.Cube);
        target.name = $"SandboxTarget_{label}";
        target.transform.SetParent(targetRoot, false);
        target.transform.localPosition = localPosition;
        target.transform.localScale = scale;
        Renderer renderer = target.GetComponent<Renderer>();
        if (renderer != null && darkMaterial != null) renderer.sharedMaterial = darkMaterial;
        Target damageTarget = target.AddComponent<Target>();
        damageTarget.maxHealth = health;
        damageTarget.damageColor = new Color(1f, 0.3f, 0.12f);
    }

    private static float ResolveSandboxSurfaceY(Vector3 localPosition)
    {
        if (Mathf.Abs(localPosition.x) <= 4f && localPosition.z >= 13.5f && localPosition.z <= 20.5f)
            return 1f;
        if (Mathf.Abs(Mathf.Abs(localPosition.x) - 13f) <= 4.5f && localPosition.z >= 3.5f && localPosition.z <= 16.5f)
            return 2f;
        return 0f;
    }

    private void BuildGuide()
    {
        Canvas canvas = ProjectStructureUIRoot.GetOrCreateCanvas();
        GameObject panel = new GameObject("SandboxGuide");
        panel.transform.SetParent(canvas.transform, false);
        RectTransform rect = panel.AddComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -18f);
        rect.sizeDelta = new Vector2(720f, 58f);
        panel.AddComponent<Image>().color = new Color(0.015f, 0.022f, 0.028f, 0.9f);
        TMP_Text text = new GameObject("SandboxGuideText").AddComponent<TextMeshProUGUI>();
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
        text.text = "SANDBOX   1/2/3 WEAPON   Q/E VARIANT   LMB FIRE   RMB ABILITY\nTargets rebuild automatically. Esc opens settings.";
    }

    private void BuildProjectileTurret()
    {
        if (player == null || sandboxRoot == null) return;
        GameObject turret = GameObject.CreatePrimitive(PrimitiveType.Cube);
        turret.name = "SandboxProjectileTurret";
        turret.transform.SetParent(sandboxRoot, false);
        turret.transform.localPosition = new Vector3(18f, 1f, 8f);
        turret.transform.localScale = new Vector3(1.2f, 2f, 1.2f);
        Renderer renderer = turret.GetComponent<Renderer>();
        if (renderer != null && accentMaterial != null) renderer.sharedMaterial = accentMaterial;
        SandboxProjectileTurret emitter = turret.AddComponent<SandboxProjectileTurret>();
        emitter.target = player;
    }
}
