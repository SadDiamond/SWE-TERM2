using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering;

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

        BuildSandboxArena();
        BuildRange();
        BuildProjectileTurret();
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

        BuildBackdrop();

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

    private void BuildBackdrop()
    {
        Color pulseColor = ProjectStructureThemePalette.ResolveAccent(CurrentThemeIndex);
        CreateBlock("SandboxRearBulkhead", new Vector3(0f, 9f, 31f), new Vector3(48f, 18f, 2.2f), darkMaterial);
        AddPulseFx(CreateBlock("SandboxRearGlow", new Vector3(0f, 12f, 29.9f), new Vector3(38f, 0.12f, 0.12f), accentMaterial), pulseColor, 1.6f, Vector3.zero);
        CreateBlock("SandboxNorthBridge", new Vector3(0f, 16f, 23f), new Vector3(34f, 0.36f, 1f), darkMaterial);
        AddPulseFx(CreateBlock("SandboxNorthBridgeGlow", new Vector3(0f, 16.12f, 22.3f), new Vector3(28f, 0.08f, 0.08f), accentMaterial), pulseColor, 1.8f, Vector3.zero);
        CreateBlock("SandboxUpperTruss", new Vector3(0f, 22f, 29f), new Vector3(42f, 0.42f, 1.2f), darkMaterial);
        AddPulseFx(CreateBlock("SandboxUpperTrussGlow", new Vector3(0f, 22.14f, 28.2f), new Vector3(30f, 0.08f, 0.08f), accentMaterial), pulseColor, 1.5f, Vector3.zero);

        for (int side = -1; side <= 1; side += 2)
        {
            float x = side * 20f;
            CreateBlock($"SandboxSideMass_{side}", new Vector3(x, 7f, 6f), new Vector3(2.4f, 14f, 44f), darkMaterial);
            AddPulseFx(CreateBlock($"SandboxSideGlow_{side}", new Vector3(x - side * 0.55f, 8f, 6f), new Vector3(0.12f, 10f, 32f), accentMaterial), pulseColor, 1.5f, Vector3.zero);
            BuildBackdropCluster($"SandboxFrameCluster_{side}", side * 26f, 30f, 12f, 8f, pulseColor);
        }

        BuildBackdropTower("SandboxTowerLeft", new Vector3(-16f, 0f, 25f), 16f);
        BuildBackdropTower("SandboxTowerRight", new Vector3(16f, 0f, 25f), 18f);
        BuildBackdropCluster("SandboxRearMachine", 0f, 37f, 16f, 18f, pulseColor);

        switch (Mathf.Abs(CurrentThemeIndex) % 4)
        {
            case 1:
                AddPulseFx(CreateBlock("SandboxSkyBand", new Vector3(0f, 20f, 18f), new Vector3(18f, 0.08f, 0.08f), accentMaterial), pulseColor, 2.1f, new Vector3(0f, 12f, 0f));
                CreateBlock("SandboxAirMastL", new Vector3(-20f, 18f, 24f), new Vector3(0.28f, 10f, 0.28f), darkMaterial);
                CreateBlock("SandboxAirMastR", new Vector3(20f, 19f, 24f), new Vector3(0.28f, 12f, 0.28f), darkMaterial);
                AddPulseFx(CreateBlock("SandboxAirBeaconL", new Vector3(-20f, 23.6f, 24f), new Vector3(0.36f, 0.8f, 0.36f), accentMaterial), pulseColor, 2.2f, Vector3.zero);
                AddPulseFx(CreateBlock("SandboxAirBeaconR", new Vector3(20f, 25.6f, 24f), new Vector3(0.36f, 0.8f, 0.36f), accentMaterial), pulseColor, 2.2f, Vector3.zero);
                break;
            case 2:
                CreateBlock("SandboxVentBandA", new Vector3(-12f, 13f, 28f), new Vector3(8f, 0.16f, 1.2f), accentMaterial);
                CreateBlock("SandboxVentBandB", new Vector3(12f, 11.5f, 28f), new Vector3(8f, 0.16f, 1.2f), accentMaterial);
                CreateBlock("SandboxHeatBulkheadL", new Vector3(-22f, 8f, 18f), new Vector3(2f, 10f, 12f), darkMaterial);
                CreateBlock("SandboxHeatBulkheadR", new Vector3(22f, 8f, 18f), new Vector3(2f, 10f, 12f), darkMaterial);
                AddPulseFx(CreateBlock("SandboxHeatSlitL", new Vector3(-20.9f, 8.8f, 18f), new Vector3(0.12f, 7.2f, 7.8f), accentMaterial), pulseColor, 1.8f, Vector3.zero);
                AddPulseFx(CreateBlock("SandboxHeatSlitR", new Vector3(20.9f, 8.8f, 18f), new Vector3(0.12f, 7.2f, 7.8f), accentMaterial), pulseColor, 1.8f, Vector3.zero);
                break;
            case 3:
                AddPulseFx(CreateBlock("SandboxSignalHalo", new Vector3(0f, 18f, 14f), new Vector3(14f, 0.08f, 14f), accentMaterial), pulseColor, 2.4f, new Vector3(0f, 16f, 0f));
                AddPulseFx(CreateBlock("SandboxSignalAxisL", new Vector3(-16f, 14f, 25f), new Vector3(0.12f, 8f, 0.12f), accentMaterial), pulseColor, 2f, Vector3.zero);
                AddPulseFx(CreateBlock("SandboxSignalAxisR", new Vector3(16f, 14f, 25f), new Vector3(0.12f, 8f, 0.12f), accentMaterial), pulseColor, 2f, Vector3.zero);
                CreateBlock("SandboxSignalBridge", new Vector3(0f, 22f, 20f), new Vector3(18f, 0.18f, 0.5f), darkMaterial);
                break;
            default:
                CreateBlock("SandboxCrossfireTowerL", new Vector3(-21f, 10f, 18f), new Vector3(1.6f, 12f, 8f), darkMaterial);
                CreateBlock("SandboxCrossfireTowerR", new Vector3(21f, 10f, 18f), new Vector3(1.6f, 12f, 8f), darkMaterial);
                AddPulseFx(CreateBlock("SandboxCrossfireSpineL", new Vector3(-20f, 10.8f, 18f), new Vector3(0.12f, 8.8f, 5.4f), accentMaterial), pulseColor, 1.7f, Vector3.zero);
                AddPulseFx(CreateBlock("SandboxCrossfireSpineR", new Vector3(20f, 10.8f, 18f), new Vector3(0.12f, 8.8f, 5.4f), accentMaterial), pulseColor, 1.7f, Vector3.zero);
                break;
        }
    }

    private void BuildBackdropTower(string name, Vector3 localPosition, float height)
    {
        CreateBlock(name + "_Core", localPosition + new Vector3(0f, height * 0.5f, 0f), new Vector3(4.8f, height, 4.8f), darkMaterial);
        CreateBlock(name + "_Cap", localPosition + new Vector3(0f, height + 0.45f, 0f), new Vector3(6.6f, 0.32f, 6.6f), darkMaterial);
        AddPulseFx(CreateBlock(name + "_Glow", localPosition + new Vector3(0f, height * 0.58f, 2.1f), new Vector3(0.1f, height * 0.46f, 0.1f), accentMaterial), ProjectStructureThemePalette.ResolveAccent(CurrentThemeIndex), 1.65f, Vector3.zero);
    }

    private void BuildBackdropCluster(string name, float x, float z, float height, float width, Color pulseColor)
    {
        CreateBlock(name + "_Core", new Vector3(x, height * 0.5f, z), new Vector3(width, height, 6f), darkMaterial);
        CreateBlock(name + "_ShoulderL", new Vector3(x - width * 0.38f, height * 0.36f, z + 1.4f), new Vector3(width * 0.22f, height * 0.68f, 3.4f), darkMaterial);
        CreateBlock(name + "_ShoulderR", new Vector3(x + width * 0.34f, height * 0.32f, z - 1.2f), new Vector3(width * 0.18f, height * 0.56f, 3.1f), darkMaterial);
        CreateBlock(name + "_Deck", new Vector3(x, height + 0.35f, z), new Vector3(width + 3f, 0.36f, 7.2f), darkMaterial);
        AddPulseFx(CreateBlock(name + "_Glow", new Vector3(x, height * 0.56f, z + 2.4f), new Vector3(0.12f, height * 0.44f, 0.12f), accentMaterial), pulseColor, 1.45f, Vector3.zero);
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
        damageTarget.grappleMassClass = label == "LIGHT" ? GrappleMassClass.Light : GrappleMassClass.Heavy;
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
        text.text = "SANDBOX   1/2 WEAPON   Q/E VARIANT   LMB FIRE   RMB ABILITY\nTargets reset automatically. ESC opens settings.";
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
        CreatePresentationLight("SandboxKeyLight", LightType.Directional, new Vector3(0f, 0f, 0f), Quaternion.Euler(46f, -24f, 0f), keyColor, 1.45f, 0f);
        CreatePresentationLight("SandboxFillRear", LightType.Point, new Vector3(0f, 10f, 22f), Quaternion.identity, fillColorA, 0.9f, 46f);
        CreatePresentationLight("SandboxFillDeck", LightType.Point, new Vector3(0f, 6f, 8f), Quaternion.identity, fillColorB, 0.64f, 34f);
    }

    private void BuildAmbientFx()
    {
        if (sandboxRoot == null)
            return;

        Transform existing = sandboxRoot.Find("SandboxAmbientFx");
        if (existing != null)
            Destroy(existing.gameObject);

        GameObject fxRoot = new GameObject("SandboxAmbientFx");
        fxRoot.transform.SetParent(sandboxRoot, false);
        fxRoot.transform.localPosition = new Vector3(0f, 4f, 8f);

        Color baseColor = ProjectStructureThemePalette.ResolveAccent(CurrentThemeIndex);
        ambientFx = CreateAmbientEmitter(
            fxRoot.transform,
            "SandboxMotes",
            Mathf.Abs(CurrentThemeIndex) % 4 == 2 ? 10f : 7f,
            Mathf.Abs(CurrentThemeIndex) % 4 == 2 ? 0.18f : 0.28f,
            new Color(baseColor.r, baseColor.g, baseColor.b, 0.14f),
            new Vector3(36f, 7f, 34f),
            Mathf.Abs(CurrentThemeIndex) % 4 != 2);

        switch (Mathf.Abs(CurrentThemeIndex) % 4)
        {
            case 1:
                CreateAmbientEmitter(fxRoot.transform, "SandboxLiftDust", 4f, 0.38f, new Color(baseColor.r, baseColor.g, baseColor.b, 0.1f), new Vector3(24f, 12f, 24f), true).transform.localPosition = new Vector3(0f, 8f, 14f);
                break;
            case 2:
                CreateAmbientEmitter(fxRoot.transform, "SandboxHeatAsh", 7f, 0.16f, new Color(baseColor.r, baseColor.g, baseColor.b, 0.14f), new Vector3(26f, 6f, 20f), false).transform.localPosition = new Vector3(0f, 2.5f, 16f);
                break;
            case 3:
                CreateAmbientEmitter(fxRoot.transform, "SandboxSignalMotes", 5f, 0.32f, new Color(baseColor.r, baseColor.g, baseColor.b, 0.1f), new Vector3(26f, 10f, 22f), true).transform.localPosition = new Vector3(0f, 7f, 12f);
                break;
            default:
                CreateAmbientEmitter(fxRoot.transform, "SandboxCrossfireGlints", 4f, 0.2f, new Color(baseColor.r, baseColor.g, baseColor.b, 0.1f), new Vector3(22f, 8f, 18f), false).transform.localPosition = new Vector3(0f, 6f, 14f);
                break;
        }
    }

    private void ResolveThemePresentation(out Color fogColor, out Color skyColor, out Color equatorColor, out Color groundColor, out Color keyColor, out Color fillColorA, out Color fillColorB, out float fogDensity)
    {
        ProjectStructureThemePalette.SupportPresentation presentation = ProjectStructureThemePalette.ResolveSandboxPresentation(CurrentThemeIndex);
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
        pulse.emissionStrength = 1.1f;
        pulse.emissionPulse = 0.22f;
        pulse.scalePulse = 0.008f;
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
        main.startSpeed = drifting ? 0.28f : 0.1f;
        main.startSize = drifting ? 0.12f : 0.07f;
        main.startColor = color;
        main.maxParticles = Mathf.Clamp(Mathf.RoundToInt(rate * particleLifetime * 18f), 36, drifting ? 160 : 100);

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
            velocityOverLifetime.x = new ParticleSystem.MinMaxCurve(-0.06f, 0.06f);
            velocityOverLifetime.y = new ParticleSystem.MinMaxCurve(0.05f, 0.16f);
            velocityOverLifetime.z = new ParticleSystem.MinMaxCurve(-0.06f, 0.06f);
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
        if (sandboxRoot == null)
            return;

        GameObject go = new GameObject(name);
        go.transform.SetParent(sandboxRoot, false);
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
}
