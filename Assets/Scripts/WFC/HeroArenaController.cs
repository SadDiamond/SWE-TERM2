using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HeroArenaController : MonoBehaviour
{
    private const string HeroRootName = "_HeroArena";
    private const string HeroTargetRootName = "_HeroArenaTargets";

    private PlayerController player;
    private Gun gun;
    private CybergrindArenaDirector director;
    private CybergrindArenaGenerator generator;
    private Transform heroRoot;
    private Transform targetRoot;
    private Material floorMaterial;
    private Material darkMaterial;
    private Material accentMaterial;
    private Material hazardMaterial;
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

        BuildHeroArena();
        BuildTargets();
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
            if (targetRoot == null || targetRoot.childCount < 6)
                BuildTargets();
        }
    }

    private void BuildHeroArena()
    {
        if (director != null)
            director.enabled = false;

        if (generator != null)
        {
            floorMaterial = generator.floorMaterial;
            darkMaterial = generator.darkMaterial;
            accentMaterial = generator.accentMaterial;
            hazardMaterial = generator.hazardMaterial;
            generator.ClearArena();
            generator.enabled = false;
        }

        GameObject oldRoot = GameObject.Find(HeroRootName);
        if (oldRoot != null) Destroy(oldRoot);

        heroRoot = new GameObject(HeroRootName).transform;
        Vector3 origin = generator != null ? generator.transform.position : Vector3.zero;
        heroRoot.position = origin;

        BuildShell();
        BuildCoreFloor();
        BuildUpperRoutes();
        BuildTowers();
        BuildCrossLinks();
        BuildCombatAccents();

        PlacePlayer(origin + new Vector3(0f, 1.2f, -24f));
    }

    private void BuildShell()
    {
        CreateBlock("HeroFloor", new Vector3(0f, -0.5f, 4f), new Vector3(72f, 1f, 84f), floorMaterial);
        CreateBlock("NorthWall", new Vector3(0f, 11f, 46f), new Vector3(72f, 24f, 2f), darkMaterial);
        CreateBlock("SouthWall", new Vector3(0f, 11f, -38f), new Vector3(72f, 24f, 2f), darkMaterial);
        CreateBlock("WestWall", new Vector3(-36f, 11f, 4f), new Vector3(2f, 24f, 84f), darkMaterial);
        CreateBlock("EastWall", new Vector3(36f, 11f, 4f), new Vector3(2f, 24f, 84f), darkMaterial);
        CreateBlock("CeilingBraceNorth", new Vector3(0f, 21.5f, 36f), new Vector3(50f, 0.6f, 1.1f), accentMaterial);
        CreateBlock("CeilingBraceSouth", new Vector3(0f, 21.5f, -28f), new Vector3(50f, 0.6f, 1.1f), accentMaterial);
        CreateBlock("CeilingBraceWest", new Vector3(-26f, 21.5f, 4f), new Vector3(1.1f, 0.6f, 54f), accentMaterial);
        CreateBlock("CeilingBraceEast", new Vector3(26f, 21.5f, 4f), new Vector3(1.1f, 0.6f, 54f), accentMaterial);
    }

    private void BuildCoreFloor()
    {
        CreateBlock("CorePad", new Vector3(0f, 0.15f, 8f), new Vector3(26f, 0.3f, 26f), darkMaterial);
        CreateBlock("CoreInsetA", new Vector3(0f, 0.22f, 8f), new Vector3(12f, 0.08f, 12f), accentMaterial);
        CreateBlock("LaneNorth", new Vector3(0f, 0.02f, 23f), new Vector3(14f, 0.04f, 9f), darkMaterial);
        CreateBlock("LaneSouth", new Vector3(0f, 0.02f, -7f), new Vector3(14f, 0.04f, 9f), darkMaterial);
        CreateBlock("LaneWest", new Vector3(-17f, 0.02f, 8f), new Vector3(9f, 0.04f, 14f), darkMaterial);
        CreateBlock("LaneEast", new Vector3(17f, 0.02f, 8f), new Vector3(9f, 0.04f, 14f), darkMaterial);
    }

    private void BuildUpperRoutes()
    {
        CreateBlock("UpperWestRoute", new Vector3(-24f, 6f, 4f), new Vector3(10f, 0.8f, 54f), darkMaterial);
        CreateBlock("UpperEastRoute", new Vector3(24f, 6f, 4f), new Vector3(10f, 0.8f, 54f), darkMaterial);
        CreateBlock("UpperNorthBridge", new Vector3(0f, 9f, 28f), new Vector3(32f, 0.75f, 7f), darkMaterial);
        CreateBlock("UpperSouthBridge", new Vector3(0f, 9f, -16f), new Vector3(32f, 0.75f, 7f), darkMaterial);

        CreateRamp("WestNorthRamp", new Vector3(-20f, 2.8f, 24f), 6f, 12f, floorMaterial, 0f);
        CreateRamp("WestSouthRamp", new Vector3(-20f, 2.8f, -16f), 6f, 12f, floorMaterial, 180f);
        CreateRamp("EastNorthRamp", new Vector3(20f, 2.8f, 24f), 6f, 12f, floorMaterial, 0f);
        CreateRamp("EastSouthRamp", new Vector3(20f, 2.8f, -16f), 6f, 12f, floorMaterial, 180f);

        CreateBlock("RouteAccentWest", new Vector3(-24f, 6.42f, 4f), new Vector3(8.8f, 0.06f, 52f), accentMaterial);
        CreateBlock("RouteAccentEast", new Vector3(24f, 6.42f, 4f), new Vector3(8.8f, 0.06f, 52f), accentMaterial);
    }

    private void BuildTowers()
    {
        BuildTower("TowerNW", new Vector3(-14f, 0f, 28f), 12f);
        BuildTower("TowerNE", new Vector3(14f, 0f, 28f), 12f);
        BuildTower("TowerSW", new Vector3(-14f, 0f, -16f), 10f);
        BuildTower("TowerSE", new Vector3(14f, 0f, -16f), 10f);
    }

    private void BuildTower(string name, Vector3 localPosition, float height)
    {
        CreateBlock(name + "_Base", localPosition + new Vector3(0f, 1.4f, 0f), new Vector3(5.2f, 2.8f, 5.2f), darkMaterial);
        CreateBlock(name + "_Shaft", localPosition + new Vector3(0f, height * 0.5f + 2.8f, 0f), new Vector3(2.2f, height, 2.2f), darkMaterial);
        CreateBlock(name + "_Top", localPosition + new Vector3(0f, height + 5.4f, 0f), new Vector3(7.4f, 0.7f, 7.4f), darkMaterial);
        CreateBlock(name + "_Glow", localPosition + new Vector3(0f, height + 5.78f, 0f), new Vector3(6.2f, 0.08f, 6.2f), accentMaterial);
    }

    private void BuildCrossLinks()
    {
        CreateBlock("MidBridgeWest", new Vector3(-12f, 4f, 8f), new Vector3(8f, 0.55f, 4f), darkMaterial);
        CreateBlock("MidBridgeEast", new Vector3(12f, 4f, 8f), new Vector3(8f, 0.55f, 4f), darkMaterial);
        CreateBlock("SkyCatwalk", new Vector3(0f, 14f, 8f), new Vector3(18f, 0.5f, 3.5f), darkMaterial);
        CreateBlock("SkyCatwalkGlow", new Vector3(0f, 14.28f, 8f), new Vector3(16f, 0.05f, 2.1f), accentMaterial);
        CreateRamp("SkyRampWest", new Vector3(-6f, 11f, 8f), 5f, 10f, floorMaterial, 90f);
        CreateRamp("SkyRampEast", new Vector3(6f, 11f, 8f), 5f, 10f, floorMaterial, 270f);
    }

    private void BuildCombatAccents()
    {
        CreateBlock("CoverWestA", new Vector3(-8f, 1f, 18f), new Vector3(2.5f, 2f, 5f), darkMaterial);
        CreateBlock("CoverEastA", new Vector3(8f, 1f, -2f), new Vector3(2.5f, 2f, 5f), darkMaterial);
        CreateBlock("CoverWestB", new Vector3(-22f, 7f, -8f), new Vector3(2.2f, 2f, 6f), darkMaterial);
        CreateBlock("CoverEastB", new Vector3(22f, 7f, 20f), new Vector3(2.2f, 2f, 6f), darkMaterial);

        if (hazardMaterial != null)
        {
            CreateBlock("HazardLineNorth", new Vector3(0f, 0.05f, 36f), new Vector3(18f, 0.04f, 0.35f), hazardMaterial);
            CreateBlock("HazardLineSouth", new Vector3(0f, 0.05f, -24f), new Vector3(18f, 0.04f, 0.35f), hazardMaterial);
        }
    }

    private void BuildTargets()
    {
        if (targetRoot != null) Destroy(targetRoot.gameObject);
        targetRoot = new GameObject(HeroTargetRootName).transform;
        targetRoot.SetParent(heroRoot, false);

        CreateTarget(new Vector3(-14f, 13f, 28f), new Vector3(1.5f, 3f, 1.5f), 700f, "TOWER");
        CreateTarget(new Vector3(14f, 13f, 28f), new Vector3(1.5f, 3f, 1.5f), 700f, "TOWER");
        CreateTarget(new Vector3(-24f, 7f, 18f), new Vector3(1.4f, 2.8f, 1.4f), 650f, "ROUTE");
        CreateTarget(new Vector3(24f, 7f, -6f), new Vector3(1.4f, 2.8f, 1.4f), 650f, "ROUTE");
        CreateTarget(new Vector3(0f, 15f, 8f), new Vector3(1.7f, 3.4f, 1.7f), 1200f, "SKY");
        CreateTarget(new Vector3(0f, 0f, 28f), new Vector3(2f, 3.6f, 2f), 1600f, "CENTER");
        CreateTarget(new Vector3(-18f, 0f, -16f), new Vector3(1.4f, 2.8f, 1.4f), 650f, "SOUTH");
        CreateTarget(new Vector3(18f, 0f, 32f), new Vector3(1.4f, 2.8f, 1.4f), 650f, "NORTH");
    }

    private void CreateTarget(Vector3 localPosition, Vector3 scale, float health, string label)
    {
        float surfaceY = ResolveHeroSurfaceY(localPosition);
        localPosition.y = surfaceY + scale.y * 0.5f;
        GameObject target = GameObject.CreatePrimitive(PrimitiveType.Cube);
        target.name = $"HeroTarget_{label}";
        target.transform.SetParent(targetRoot, false);
        target.transform.localPosition = localPosition;
        target.transform.localScale = scale;
        Renderer renderer = target.GetComponent<Renderer>();
        if (renderer != null && darkMaterial != null) renderer.sharedMaterial = darkMaterial;
        Target damageTarget = target.AddComponent<Target>();
        damageTarget.maxHealth = health;
        damageTarget.damageColor = new Color(1f, 0.3f, 0.12f);
    }

    private static float ResolveHeroSurfaceY(Vector3 localPosition)
    {
        if (Mathf.Abs(localPosition.x + 24f) <= 5f && localPosition.z >= -23f && localPosition.z <= 31f)
            return 6.4f;
        if (Mathf.Abs(localPosition.x - 24f) <= 5f && localPosition.z >= -23f && localPosition.z <= 31f)
            return 6.4f;
        if (Mathf.Abs(localPosition.z - 28f) <= 3.5f && Mathf.Abs(localPosition.x) <= 16f)
            return 9.375f;
        if (Mathf.Abs(localPosition.z + 16f) <= 3.5f && Mathf.Abs(localPosition.x) <= 16f)
            return 9.375f;
        if (Mathf.Abs(localPosition.x) <= 9f && Mathf.Abs(localPosition.z - 8f) <= 1.75f)
            return 14.25f;
        if (Mathf.Abs(localPosition.x + 14f) <= 3.8f && Mathf.Abs(localPosition.z - 28f) <= 3.8f)
            return 17.75f;
        if (Mathf.Abs(localPosition.x - 14f) <= 3.8f && Mathf.Abs(localPosition.z - 28f) <= 3.8f)
            return 17.75f;
        if (Mathf.Abs(localPosition.x + 14f) <= 3.8f && Mathf.Abs(localPosition.z + 16f) <= 3.8f)
            return 15.75f;
        if (Mathf.Abs(localPosition.x - 14f) <= 3.8f && Mathf.Abs(localPosition.z + 16f) <= 3.8f)
            return 15.75f;
        return 0f;
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

    private void CreateRamp(string name, Vector3 position, float rise, float run, Material material, float yaw)
    {
        float angle = Mathf.Atan2(rise, run) * Mathf.Rad2Deg;
        float length = Mathf.Sqrt(rise * rise + run * run);
        GameObject ramp = CreateBlock(name, position, new Vector3(8f, 0.5f, length), material);
        ramp.transform.localRotation = Quaternion.Euler(-angle, yaw, 0f);
    }

    private GameObject CreateBlock(string name, Vector3 localPosition, Vector3 scale, Material material)
    {
        GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
        block.name = name;
        block.transform.SetParent(heroRoot, false);
        block.transform.localPosition = localPosition;
        block.transform.localScale = scale;
        Renderer renderer = block.GetComponent<Renderer>();
        if (renderer != null && material != null)
            renderer.sharedMaterial = material;
        return block;
    }

    private void BuildGuide()
    {
        Canvas canvas = ProjectStructureUIRoot.GetOrCreateCanvas();
        GameObject panel = new GameObject("HeroArenaGuide");
        panel.transform.SetParent(canvas.transform, false);
        RectTransform rect = panel.AddComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -18f);
        rect.sizeDelta = new Vector2(780f, 64f);
        panel.AddComponent<Image>().color = new Color(0.015f, 0.022f, 0.028f, 0.9f);

        TMP_Text text = new GameObject("HeroArenaGuideText").AddComponent<TextMeshProUGUI>();
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
        text.text =
            "HERO ARENA   benchmark space for scale, height, and route clarity\n" +
            $"{ProjectStructureBindings.GetDisplayString(ProjectStructureAction.Grapple)} grapple   " +
            $"{ProjectStructureBindings.GetDisplayString(ProjectStructureAction.Dash)} dash   " +
            $"{ProjectStructureBindings.GetDisplayString(ProjectStructureAction.Slide)} slide   " +
            $"{ProjectStructureBindings.GetDisplayString(ProjectStructureAction.Jump)} jump";
    }
}
