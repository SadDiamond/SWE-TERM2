using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering;

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
    private readonly System.Collections.Generic.List<Light> presentationLights = new System.Collections.Generic.List<Light>();
    private ParticleSystem ambientFx;
    private int CurrentThemeIndex => director != null ? director.CurrentThemeIndex : (generator != null ? generator.themeIndex : 0);

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
        ApplyPresentation();
        BuildAmbientFx();
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
        BuildBackdrop();

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

    private void BuildBackdrop()
    {
        Color pulseColor = ProjectStructureThemePalette.ResolveAccent(CurrentThemeIndex);
        CreateBlock("HeroNorthBridge", new Vector3(0f, 25f, 42f), new Vector3(58f, 0.45f, 1.2f), darkMaterial);
        CreateBlock("HeroSouthBridge", new Vector3(0f, 24f, -34f), new Vector3(52f, 0.38f, 1.1f), darkMaterial);
        AddPulseFx(CreateBlock("HeroNorthGlow", new Vector3(0f, 25.15f, 41.2f), new Vector3(42f, 0.08f, 0.08f), accentMaterial), pulseColor, 1.8f, Vector3.zero);
        AddPulseFx(CreateBlock("HeroSouthGlow", new Vector3(0f, 24.15f, -33.2f), new Vector3(38f, 0.08f, 0.08f), accentMaterial), pulseColor, 1.6f, Vector3.zero);

        for (int side = -1; side <= 1; side += 2)
        {
            float x = side * 30f;
            CreateBlock($"HeroSideMass_{side}", new Vector3(x, 10f, 4f), new Vector3(3.2f, 18f, 70f), darkMaterial);
            AddPulseFx(CreateBlock($"HeroSideGlow_{side}", new Vector3(x - side * 0.9f, 11f, 4f), new Vector3(0.18f, 12f, 54f), accentMaterial), pulseColor, 1.4f, Vector3.zero);
            BuildBackdropCluster($"HeroFrameCluster_{side}", side * 39f, side > 0 ? 48f : -40f, 16f, 10f, pulseColor);
        }

        CreateBlock("HeroUpperTrussNorth", new Vector3(0f, 31f, 44f), new Vector3(66f, 0.5f, 1.4f), darkMaterial);
        CreateBlock("HeroUpperTrussSouth", new Vector3(0f, 30f, -36f), new Vector3(60f, 0.5f, 1.4f), darkMaterial);
        AddPulseFx(CreateBlock("HeroUpperTrussGlowNorth", new Vector3(0f, 31.18f, 43.1f), new Vector3(52f, 0.08f, 0.08f), accentMaterial), pulseColor, 1.55f, Vector3.zero);
        AddPulseFx(CreateBlock("HeroUpperTrussGlowSouth", new Vector3(0f, 30.18f, -35.1f), new Vector3(46f, 0.08f, 0.08f), accentMaterial), pulseColor, 1.45f, Vector3.zero);

        BuildBackdropTower("HeroBackdropNorthWest", new Vector3(-24f, 0f, 44f), 22f);
        BuildBackdropTower("HeroBackdropNorthEast", new Vector3(24f, 0f, 44f), 24f);
        BuildBackdropTower("HeroBackdropSouthWest", new Vector3(-26f, 0f, -36f), 18f);
        BuildBackdropTower("HeroBackdropSouthEast", new Vector3(26f, 0f, -36f), 20f);
        BuildBackdropCluster("HeroFarNorthCore", 0f, 54f, 24f, 18f, pulseColor);
        BuildBackdropCluster("HeroFarSouthCore", 0f, -46f, 20f, 16f, pulseColor);

        switch (Mathf.Abs(CurrentThemeIndex) % 4)
        {
            case 1:
                CreateBlock("HeroSkySpine", new Vector3(0f, 29f, 8f), new Vector3(14f, 0.3f, 2.2f), darkMaterial);
                AddPulseFx(CreateBlock("HeroSkySpineGlow", new Vector3(0f, 29.16f, 8f), new Vector3(10f, 0.06f, 0.12f), accentMaterial), pulseColor, 2.2f, new Vector3(0f, 14f, 0f));
                CreateBlock("HeroHighRiseMastL", new Vector3(-30f, 24f, 30f), new Vector3(0.32f, 11f, 0.32f), darkMaterial);
                CreateBlock("HeroHighRiseMastR", new Vector3(30f, 25f, -20f), new Vector3(0.32f, 13f, 0.32f), darkMaterial);
                AddPulseFx(CreateBlock("HeroHighRiseBeaconL", new Vector3(-30f, 29.8f, 30f), new Vector3(0.42f, 0.9f, 0.42f), accentMaterial), pulseColor, 2.5f, Vector3.zero);
                AddPulseFx(CreateBlock("HeroHighRiseBeaconR", new Vector3(30f, 31.8f, -20f), new Vector3(0.42f, 0.9f, 0.42f), accentMaterial), pulseColor, 2.3f, Vector3.zero);
                break;
            case 2:
                CreateBlock("HeroHeatVentA", new Vector3(-18f, 18f, 40f), new Vector3(7f, 0.22f, 2.2f), accentMaterial);
                CreateBlock("HeroHeatVentB", new Vector3(18f, 17f, -32f), new Vector3(7f, 0.22f, 2.2f), accentMaterial);
                CreateBlock("HeroHeatBulkheadL", new Vector3(-31f, 12f, 18f), new Vector3(2.4f, 12f, 18f), darkMaterial);
                CreateBlock("HeroHeatBulkheadR", new Vector3(31f, 11f, -10f), new Vector3(2.4f, 10f, 16f), darkMaterial);
                AddPulseFx(CreateBlock("HeroHeatSlitL", new Vector3(-29.7f, 13f, 18f), new Vector3(0.12f, 8f, 12f), accentMaterial), pulseColor, 1.9f, Vector3.zero);
                AddPulseFx(CreateBlock("HeroHeatSlitR", new Vector3(29.7f, 12f, -10f), new Vector3(0.12f, 7f, 10f), accentMaterial), pulseColor, 1.8f, Vector3.zero);
                break;
            case 3:
                AddPulseFx(CreateBlock("HeroSignalRing", new Vector3(0f, 27f, 8f), new Vector3(18f, 0.08f, 18f), accentMaterial), pulseColor, 2.6f, new Vector3(0f, 18f, 0f));
                AddPulseFx(CreateBlock("HeroSignalAxisNorth", new Vector3(0f, 23f, 41f), new Vector3(0.12f, 9f, 0.12f), accentMaterial), pulseColor, 2.1f, Vector3.zero);
                AddPulseFx(CreateBlock("HeroSignalAxisSouth", new Vector3(0f, 22f, -33f), new Vector3(0.12f, 8f, 0.12f), accentMaterial), pulseColor, 2.1f, Vector3.zero);
                CreateBlock("HeroSignalBridge", new Vector3(0f, 30f, 8f), new Vector3(24f, 0.22f, 0.6f), darkMaterial);
                break;
            default:
                CreateBlock("HeroCrossfireTowerL", new Vector3(-32f, 15f, 8f), new Vector3(1.8f, 16f, 8f), darkMaterial);
                CreateBlock("HeroCrossfireTowerR", new Vector3(32f, 15f, 8f), new Vector3(1.8f, 16f, 8f), darkMaterial);
                AddPulseFx(CreateBlock("HeroCrossfireSpineL", new Vector3(-30.9f, 16f, 8f), new Vector3(0.14f, 12f, 5.8f), accentMaterial), pulseColor, 1.7f, Vector3.zero);
                AddPulseFx(CreateBlock("HeroCrossfireSpineR", new Vector3(30.9f, 16f, 8f), new Vector3(0.14f, 12f, 5.8f), accentMaterial), pulseColor, 1.7f, Vector3.zero);
                break;
        }
    }

    private void BuildBackdropTower(string name, Vector3 localPosition, float height)
    {
        CreateBlock(name + "_Core", localPosition + new Vector3(0f, height * 0.5f, 0f), new Vector3(6.4f, height, 6.4f), darkMaterial);
        CreateBlock(name + "_ShoulderL", localPosition + new Vector3(-4.2f, height * 0.42f, 0f), new Vector3(2.4f, height * 0.78f, 3.4f), darkMaterial);
        CreateBlock(name + "_ShoulderR", localPosition + new Vector3(4.2f, height * 0.36f, 0f), new Vector3(2.0f, height * 0.66f, 3f), darkMaterial);
        CreateBlock(name + "_Cap", localPosition + new Vector3(0f, height + 0.7f, 0f), new Vector3(8.4f, 0.45f, 8.4f), darkMaterial);
        AddPulseFx(CreateBlock(name + "_Glow", localPosition + new Vector3(0f, height * 0.62f, 2.8f), new Vector3(0.12f, height * 0.52f, 0.12f), accentMaterial), ProjectStructureThemePalette.ResolveAccent(CurrentThemeIndex), 1.7f, Vector3.zero);
    }

    private void BuildBackdropCluster(string name, float x, float z, float height, float width, Color pulseColor)
    {
        CreateBlock(name + "_Core", new Vector3(x, height * 0.5f, z), new Vector3(width, height, 7.5f), darkMaterial);
        CreateBlock(name + "_ShoulderLeft", new Vector3(x - width * 0.42f, height * 0.38f, z + 1.8f), new Vector3(width * 0.24f, height * 0.72f, 4.2f), darkMaterial);
        CreateBlock(name + "_ShoulderRight", new Vector3(x + width * 0.36f, height * 0.34f, z - 1.4f), new Vector3(width * 0.18f, height * 0.6f, 3.8f), darkMaterial);
        CreateBlock(name + "_Deck", new Vector3(x, height + 0.45f, z), new Vector3(width + 4f, 0.42f, 8.8f), darkMaterial);
        AddPulseFx(CreateBlock(name + "_Glow", new Vector3(x, height * 0.58f, z + 3.2f), new Vector3(0.14f, height * 0.5f, 0.14f), accentMaterial), pulseColor, 1.5f, Vector3.zero);
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

    private void ApplyPresentation()
    {
        ResolveThemePresentation(out Color fogColor, out Color skyColor, out Color equatorColor, out Color groundColor, out Color keyColor, out Color fillColorA, out Color fillColorB, out float fogDensity);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = fogColor;
        RenderSettings.fogDensity = fogDensity;
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = skyColor;
        RenderSettings.ambientEquatorColor = equatorColor;
        RenderSettings.ambientGroundColor = groundColor;

        ClearPresentationLights();
        CreatePresentationLight("HeroKeyLight", LightType.Directional, new Vector3(0f, 0f, 0f), Quaternion.Euler(48f, -30f, 0f), keyColor, 1.55f, 0f);
        CreatePresentationLight("HeroFillNorth", LightType.Point, new Vector3(0f, 16f, 28f), Quaternion.identity, fillColorA, 1.1f, 72f);
        CreatePresentationLight("HeroFillCore", LightType.Point, new Vector3(0f, 12f, 8f), Quaternion.identity, fillColorB, 0.8f, 54f);
    }

    private void BuildAmbientFx()
    {
        if (heroRoot == null)
            return;

        Transform existing = heroRoot.Find("HeroAmbientFx");
        if (existing != null)
            Destroy(existing.gameObject);

        GameObject fxRoot = new GameObject("HeroAmbientFx");
        fxRoot.transform.SetParent(heroRoot, false);
        fxRoot.transform.localPosition = new Vector3(0f, 6f, 6f);

        Color baseColor = ProjectStructureThemePalette.ResolveAccent(CurrentThemeIndex);
        ambientFx = CreateAmbientEmitter(
            fxRoot.transform,
            "HeroMotes",
            Mathf.Abs(CurrentThemeIndex) % 4 == 2 ? 12f : 8f,
            Mathf.Abs(CurrentThemeIndex) % 4 == 2 ? 0.18f : 0.3f,
            new Color(baseColor.r, baseColor.g, baseColor.b, 0.16f),
            new Vector3(56f, 10f, 62f),
            Mathf.Abs(CurrentThemeIndex) % 4 != 2);

        switch (Mathf.Abs(CurrentThemeIndex) % 4)
        {
            case 1:
                CreateAmbientEmitter(fxRoot.transform, "HeroLiftSparks", 5f, 0.4f, new Color(baseColor.r, baseColor.g, baseColor.b, 0.11f), new Vector3(38f, 18f, 42f), true).transform.localPosition = new Vector3(0f, 10f, 8f);
                break;
            case 2:
                CreateAmbientEmitter(fxRoot.transform, "HeroHeatAsh", 9f, 0.16f, new Color(baseColor.r, baseColor.g, baseColor.b, 0.16f), new Vector3(44f, 8f, 46f), false).transform.localPosition = new Vector3(0f, 3f, 6f);
                break;
            case 3:
                CreateAmbientEmitter(fxRoot.transform, "HeroSignalDust", 6f, 0.34f, new Color(baseColor.r, baseColor.g, baseColor.b, 0.1f), new Vector3(42f, 16f, 44f), true).transform.localPosition = new Vector3(0f, 9f, 6f);
                break;
            default:
                CreateAmbientEmitter(fxRoot.transform, "HeroCrossfireShards", 5f, 0.22f, new Color(baseColor.r, baseColor.g, baseColor.b, 0.12f), new Vector3(36f, 12f, 38f), false).transform.localPosition = new Vector3(0f, 8f, 8f);
                break;
        }
    }

    private void ResolveThemePresentation(out Color fogColor, out Color skyColor, out Color equatorColor, out Color groundColor, out Color keyColor, out Color fillColorA, out Color fillColorB, out float fogDensity)
    {
        ProjectStructureThemePalette.SupportPresentation presentation = ProjectStructureThemePalette.ResolveHeroArenaPresentation(CurrentThemeIndex);
        fogColor = presentation.fogColor;
        skyColor = presentation.skyColor;
        equatorColor = presentation.equatorColor;
        groundColor = presentation.groundColor;
        keyColor = presentation.keyColor;
        fillColorA = presentation.fillColorA;
        fillColorB = presentation.fillColorB;
        fogDensity = presentation.fogDensity;
    }

    private void AddPulseFx(GameObject target, Color emissionColor, float pulseSpeed, Vector3 rotationSpeed)
    {
        if (target == null)
            return;

        ArenaPulseFx pulse = target.GetComponent<ArenaPulseFx>();
        if (pulse == null)
            pulse = target.AddComponent<ArenaPulseFx>();
        pulse.pulseSpeed = pulseSpeed;
        pulse.rotationDegreesPerSecond = rotationSpeed;
        pulse.emissionColor = emissionColor;
        pulse.emissionStrength = 1.15f;
        pulse.emissionPulse = 0.24f;
        pulse.scalePulse = 0.01f;
    }

    private ParticleSystem CreateAmbientEmitter(Transform parent, string name, float rate, float particleLifetime, Color color, Vector3 boxSize, bool drifting)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
        if (renderer != null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null) shader = Shader.Find("Particles/Standard Unlit");
            if (shader == null) shader = Shader.Find("Standard");
            Material mat = new Material(shader);
            mat.color = color;
            renderer.material = mat;
        }

        var main = ps.main;
        main.loop = true;
        main.playOnAwake = true;
        main.startLifetime = particleLifetime;
        main.startSpeed = drifting ? 0.35f : 0.12f;
        main.startSize = drifting ? 0.14f : 0.08f;
        main.startColor = color;
        main.maxParticles = Mathf.Clamp(Mathf.RoundToInt(rate * particleLifetime * 18f), 48, drifting ? 220 : 120);

        var emission = ps.emission;
        emission.rateOverTime = rate;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = boxSize;

        var velocityOverLifetime = ps.velocityOverLifetime;
        velocityOverLifetime.enabled = drifting;
        if (drifting)
        {
            velocityOverLifetime.space = ParticleSystemSimulationSpace.Local;
            velocityOverLifetime.x = new ParticleSystem.MinMaxCurve(-0.08f, 0.08f);
            velocityOverLifetime.y = new ParticleSystem.MinMaxCurve(0.08f, 0.22f);
            velocityOverLifetime.z = new ParticleSystem.MinMaxCurve(-0.08f, 0.08f);
        }

        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(color.r, color.g, color.b), 0f),
                new GradientColorKey(new Color(color.r, color.g, color.b), 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(color.a, 0.18f),
                new GradientAlphaKey(color.a * 0.65f, 0.72f),
                new GradientAlphaKey(0f, 1f)
            });
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);

        return ps;
    }

    private void CreatePresentationLight(string name, LightType type, Vector3 localPosition, Quaternion localRotation, Color color, float intensity, float range)
    {
        if (heroRoot == null)
            return;

        GameObject go = new GameObject(name);
        go.transform.SetParent(heroRoot, false);
        go.transform.localPosition = localPosition;
        go.transform.localRotation = localRotation;
        Light light = go.AddComponent<Light>();
        light.type = type;
        light.color = color;
        light.intensity = intensity;
        if (type != LightType.Directional)
            light.range = range;
        light.shadows = LightShadows.None;
        presentationLights.Add(light);
    }

    private void ClearPresentationLights()
    {
        for (int i = 0; i < presentationLights.Count; i++)
        {
            if (presentationLights[i] != null)
                Destroy(presentationLights[i].gameObject);
        }
        presentationLights.Clear();
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
