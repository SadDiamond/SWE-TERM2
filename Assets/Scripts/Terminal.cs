using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using TMPro;

public class Terminal : Interactable
{
    [Header("Terminal State")]
    public bool isSolved = false;
    public string overridePrompt = "Use terminal";
    [Min(0.5f)] public float terminalInteractRange = 4.5f;

    [Header("Connected Systems")]
    public Door connectedDoor; // The door this terminal will unlock
    public bool autoOpenConnectedDoor = true; // If false, only unlocks — player still has to open it

    // UnityEvents allow you to drag-and-drop anything in the Inspector
    // Example: turning off lights, playing sounds, etc.
    public UnityEvent onPuzzleSolved;

    private Renderer screenRenderer;
    private Renderer statusLightRenderer;
    private Renderer beaconRenderer;
    private LineRenderer circuitLineRenderer;
    private TextMeshPro statusLabel;

    protected override void Start()
    {
        // Clear any old visual children before building
        Transform oldVisual = transform.Find("_TerminalVisual");
        if (oldVisual != null)
            Destroy(oldVisual.gameObject);

        base.Start();
        interactionRange = terminalInteractRange;

        // Always build the proper terminal model
        BuildTerminalModel();

        UpdatePrompt();
        ApplyTerminalVisualState();
        BuildStatusLabel();

        // Add circuit line renderer pointing to the exit
        if (GetComponent<LineRenderer>() == null)
        {
            AddCircuitLineToExit();
        }
    }

    private void BuildTerminalModel()
    {
        // Root for the terminal visual
        GameObject model = new GameObject("_TerminalVisual");
        model.transform.SetParent(transform, false);

        Material frameMat = CreateTerminalMaterial(new Color(0.08f, 0.08f, 0.1f), new Color(0.02f, 0.02f, 0.03f));
        Material panelMat = CreateTerminalMaterial(new Color(0.12f, 0.12f, 0.16f), new Color(0.04f, 0.04f, 0.06f));
        Material accentMat = CreateTerminalMaterial(new Color(0.02f, 0.5f, 0.65f), new Color(0.1f, 0.9f, 1f));
        Material screenMat = CreateTerminalMaterial(new Color(0.02f, 0.22f, 0.28f), new Color(0.06f, 0.85f, 1f));
        Material keyMat = CreateTerminalMaterial(new Color(0.18f, 0.18f, 0.2f), new Color(0.05f, 0.05f, 0.06f));

        CreatePart(model.transform, "Base", new Vector3(0f, -0.36f, 0f), new Vector3(0.92f, 0.16f, 0.64f), frameMat);
        CreatePart(model.transform, "Stand", new Vector3(0f, -0.08f, -0.16f), new Vector3(0.22f, 0.42f, 0.18f), frameMat);
        CreatePart(model.transform, "BackHousing", new Vector3(0f, 0.22f, -0.08f), new Vector3(0.82f, 0.54f, 0.32f), frameMat);
        CreatePart(model.transform, "ScreenFrame", new Vector3(0f, 0.26f, 0.1f), new Vector3(0.7f, 0.46f, 0.08f), panelMat);

        GameObject screen = CreatePart(model.transform, "Screen", new Vector3(0f, 0.26f, 0.145f), new Vector3(0.55f, 0.32f, 0.03f), screenMat);
        screenRenderer = screen.GetComponent<Renderer>();

        CreatePart(model.transform, "SideAccentLeft", new Vector3(-0.39f, 0.08f, 0.03f), new Vector3(0.05f, 0.52f, 0.1f), accentMat);
        CreatePart(model.transform, "SideAccentRight", new Vector3(0.39f, 0.08f, 0.03f), new Vector3(0.05f, 0.52f, 0.1f), accentMat);
        CreatePart(model.transform, "KeyboardDeck", new Vector3(0f, -0.02f, 0.14f), new Vector3(0.64f, 0.12f, 0.28f), panelMat);
        GameObject statusLight = CreatePart(model.transform, "StatusLight", new Vector3(0f, 0.48f, 0.14f), new Vector3(0.09f, 0.09f, 0.04f), accentMat);
        statusLightRenderer = statusLight.GetComponent<Renderer>();
        ArenaPulseFx statusPulse = statusLight.AddComponent<ArenaPulseFx>();
        statusPulse.SetBaseScale(statusLight.transform.localScale);
        statusPulse.scalePulse = 0.18f;
        statusPulse.pulseSpeed = 3.8f;
        statusPulse.emissionColor = new Color(0.1f, 0.9f, 1f);
        statusPulse.emissionStrength = 0.85f;

        for (int i = -2; i <= 2; i++)
        {
            for (int j = 0; j < 2; j++)
            {
                CreatePart(model.transform, $"Key_{i}_{j}", new Vector3(i * 0.09f, -0.01f, 0.19f + (j * 0.06f)), new Vector3(0.05f, 0.03f, 0.04f), keyMat);
            }
        }

        CreatePart(model.transform, "AntennaLeft", new Vector3(-0.25f, 0.74f, -0.03f), new Vector3(0.03f, 0.28f, 0.03f), accentMat);
        CreatePart(model.transform, "AntennaRight", new Vector3(0.25f, 0.74f, -0.03f), new Vector3(0.03f, 0.24f, 0.03f), accentMat);
        CreatePart(model.transform, "CableBundle", new Vector3(0f, -0.42f, -0.22f), new Vector3(0.16f, 0.08f, 0.16f), frameMat);

        GameObject floorRing = CreatePart(model.transform, "FloorSignal", new Vector3(0f, -0.47f, 0f), new Vector3(1.2f, 0.025f, 1.2f), accentMat);
        beaconRenderer = floorRing.GetComponent<Renderer>();
        ArenaPulseFx floorPulse = floorRing.AddComponent<ArenaPulseFx>();
        floorPulse.SetBaseScale(floorRing.transform.localScale);
        floorPulse.scalePulse = 0.16f;
        floorPulse.pulseSpeed = 2.25f;
        floorPulse.rotationDegreesPerSecond = new Vector3(0f, 24f, 0f);
        floorPulse.emissionColor = new Color(0.1f, 0.9f, 1f);
        floorPulse.emissionStrength = 0.7f;

        GameObject beacon = CreatePart(model.transform, "VerticalSignal", new Vector3(0f, 0.68f, -0.18f), new Vector3(0.055f, 1.35f, 0.055f), accentMat);
        ArenaPulseFx beaconPulse = beacon.AddComponent<ArenaPulseFx>();
        beaconPulse.SetBaseScale(beacon.transform.localScale);
        beaconPulse.scalePulse = 0.22f;
        beaconPulse.pulseSpeed = 3.2f;
        beaconPulse.emissionColor = new Color(0.1f, 0.9f, 1f);
        beaconPulse.emissionStrength = 0.9f;

        highlightRenderer = screenRenderer;
    }

    private void ApplyTerminalVisualState()
    {
        if (screenRenderer != null)
        {
            screenRenderer.material = CreateTerminalMaterial(
                isSolved ? new Color(0.06f, 0.18f, 0.08f) : new Color(0.02f, 0.22f, 0.28f),
                isSolved ? new Color(0.15f, 1f, 0.25f) : new Color(0.15f, 1f, 1f));
        }

        if (statusLightRenderer != null)
        {
            statusLightRenderer.material = CreateTerminalMaterial(
                isSolved ? new Color(0.12f, 0.3f, 0.12f) : new Color(0.08f, 0.5f, 0.65f),
                isSolved ? new Color(0.2f, 1f, 0.25f) : new Color(0.1f, 0.9f, 1f));
        }

        if (beaconRenderer != null)
        {
            beaconRenderer.material = CreateTerminalMaterial(
                isSolved ? new Color(0.06f, 0.22f, 0.08f) : new Color(0.02f, 0.22f, 0.28f),
                isSolved ? new Color(0.2f, 1f, 0.25f) : new Color(0.1f, 0.9f, 1f));
        }

        if (statusLabel != null)
        {
            statusLabel.text = isSolved ? "SEALED" : "LIVE";
            statusLabel.color = isSolved ? new Color(0.35f, 1f, 0.45f) : new Color(0.28f, 0.9f, 1f);
        }

        RefreshCircuitLineVisual();
    }

    private GameObject CreatePart(Transform parent, string name, Vector3 localPosition, Vector3 localScale, Material material)
    {
        GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
        part.name = name;
        part.transform.SetParent(parent, false);
        part.transform.localPosition = localPosition;
        part.transform.localScale = localScale;
        Collider collider = part.GetComponent<Collider>();
        if (collider != null)
        {
            if (Application.isPlaying) Destroy(collider);
            else DestroyImmediate(collider);
        }
        Renderer renderer = part.GetComponent<Renderer>();
        if (renderer != null)
        {
            if (Application.isPlaying) renderer.material = material;
            else renderer.sharedMaterial = material;
        }
        return part;
    }

    private Material CreateTerminalMaterial(Color baseColor, Color emissionColor)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");

        Material material = new Material(shader);
        material.color = baseColor;
        if (material.HasProperty("_EmissionColor"))
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", emissionColor * 0.5f);
        }

        return material;
    }

    private void AddCircuitLineToExit()
    {
        circuitLineRenderer = gameObject.AddComponent<LineRenderer>();
        circuitLineRenderer.positionCount = 0;
        circuitLineRenderer.startWidth = 0.08f;
        circuitLineRenderer.endWidth = 0.08f;
        circuitLineRenderer.material = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default"));
        circuitLineRenderer.sortingOrder = -1; // Draw behind most objects

        // Find exit and draw line to it
        StartCoroutine(DrawCircuitLineCoroutine(circuitLineRenderer));
    }

    private System.Collections.IEnumerator DrawCircuitLineCoroutine(LineRenderer lineRenderer)
    {
        // Wait a frame for the exit to be created
        yield return null;

        CybergrindArenaGenerator cybergrindGenerator = FindAnyObjectByType<CybergrindArenaGenerator>();
        if (cybergrindGenerator != null && cybergrindGenerator.TryBuildGroundPath(transform.position, FindExitWorldPosition(), out List<Vector3> path))
        {
            lineRenderer.useWorldSpace = true;
            lineRenderer.positionCount = path.Count;
            for (int i = 0; i < path.Count; i++)
                lineRenderer.SetPosition(i, path[i] + Vector3.up * 0.03f);
            RefreshCircuitLineVisual();
            yield break;
        }

        Vector3 exitPos = FindExitWorldPosition();
        if (exitPos != Vector3.zero && TryBuildGroundCircuitFallback(transform.position, exitPos, out List<Vector3> fallbackPath))
        {
            lineRenderer.useWorldSpace = true;
            lineRenderer.positionCount = fallbackPath.Count;
            for (int i = 0; i < fallbackPath.Count; i++)
                lineRenderer.SetPosition(i, fallbackPath[i] + Vector3.up * 0.03f);
            RefreshCircuitLineVisual();
        }
    }

    private bool TryBuildGroundCircuitFallback(Vector3 startWorld, Vector3 endWorld, out List<Vector3> path)
    {
        path = new List<Vector3>();

        Vector3 start = SampleGroundPoint(startWorld, 0.08f);
        Vector3 end = SampleGroundPoint(endWorld, 0.08f);
        Vector3 midA = new Vector3(end.x, start.y, start.z);
        Vector3 midB = new Vector3(end.x, end.y, start.z);
        AddGroundPathSegment(path, start, midA);
        AddGroundPathSegment(path, midA, midB);
        AddGroundPathSegment(path, midB, end);

        return path.Count >= 2;
    }

    private void AddGroundPathSegment(List<Vector3> path, Vector3 start, Vector3 end)
    {
        if (path.Count == 0)
            path.Add(SampleGroundPoint(start, 0.08f));

        int steps = Mathf.Clamp(Mathf.RoundToInt(Vector3.Distance(start, end) / 1.35f), 3, 18);
        for (int i = 1; i <= steps; i++)
        {
            float t = i / (float)steps;
            Vector3 sample = Vector3.Lerp(start, end, t);
            sample = SampleGroundPoint(sample, 0.05f);
            if (path.Count == 0 || Vector3.Distance(path[path.Count - 1], sample) > 0.35f)
                path.Add(sample);
        }
    }

    private Vector3 SampleGroundPoint(Vector3 worldPoint, float verticalOffset)
    {
        Vector3 origin = worldPoint + Vector3.up * 6f;
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 16f, ~0, QueryTriggerInteraction.Ignore))
            return hit.point + Vector3.up * verticalOffset;

        return worldPoint + Vector3.up * verticalOffset;
    }

    private Vector3 FindExitWorldPosition()
    {
        foreach (GameObject obj in FindObjectsByType<GameObject>())
        {
            if (obj.name.StartsWith("Exit_"))
                return obj.transform.position;
        }

        CybergrindArenaGenerator cybergrindGenerator = FindAnyObjectByType<CybergrindArenaGenerator>();
        if (cybergrindGenerator != null)
        {
            Transform arenaRoot = cybergrindGenerator.CurrentArenaRoot;
            if (arenaRoot != null)
            {
                Transform exitTransform = arenaRoot.Find($"Exit_{cybergrindGenerator.width / 2}_{cybergrindGenerator.length - 3}");
                if (exitTransform != null)
                    return exitTransform.position;
            }
        }

        return Vector3.zero;
    }

    private void BuildStatusLabel()
    {
        GameObject labelObject = new GameObject("TerminalStatusLabel");
        labelObject.transform.SetParent(transform, false);
        labelObject.transform.localPosition = new Vector3(0f, 1.3f, 0f);
        labelObject.transform.localRotation = Quaternion.identity;
        labelObject.transform.localScale = Vector3.one * 0.1f;

        statusLabel = labelObject.AddComponent<TextMeshPro>();
        statusLabel.text = "LIVE";
        statusLabel.fontSize = 3.2f;
        statusLabel.alignment = TextAlignmentOptions.Center;
        statusLabel.enableAutoSizing = true;
        if (TMP_Settings.defaultFontAsset != null)
            statusLabel.font = TMP_Settings.defaultFontAsset;
        statusLabel.color = new Color(0.28f, 0.9f, 1f);
        statusLabel.outlineColor = Color.black;
        statusLabel.outlineWidth = 0.2f;
        statusLabel.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
    }

    private void RefreshCircuitLineVisual()
    {
        if (circuitLineRenderer == null) return;

        Color solvedStart = new Color(0.2f, 1f, 0.3f, 0.95f);
        Color solvedEnd = new Color(0.15f, 0.8f, 0.28f, 0.6f);
        Color activeStart = new Color(0.0f, 0.9f, 1.0f, 0.8f);
        Color activeEnd = new Color(0.0f, 0.6f, 1.0f, 0.4f);

        circuitLineRenderer.startColor = isSolved ? solvedStart : activeStart;
        circuitLineRenderer.endColor = isSolved ? solvedEnd : activeEnd;
        circuitLineRenderer.startWidth = isSolved ? 0.12f : 0.08f;
        circuitLineRenderer.endWidth = isSolved ? 0.12f : 0.08f;
    }

    public override void OnInteract(PlayerController player)
    {
        if (isSolved) return;
        TriggerPuzzle(player);
    }

    // Virtual method so child classes (Keypad, Riddle, etc.) can override it with their own puzzle logic
    public virtual void TriggerPuzzle(PlayerController player)
    {
        // Base terminal behaviour: instantly solves if no puzzle is defined.
        SolvePuzzle(player);
    }

    // Called when the UI puzzle is completed
    public void SolvePuzzle(PlayerController player)
    {
        if (isSolved) return;
        isSolved = true;
        ApplyTerminalVisualState();
        UpdatePrompt();

        if (connectedDoor != null)
        {
            connectedDoor.isLocked = false;
            if (autoOpenConnectedDoor) connectedDoor.isOpen = true;
        }

        onPuzzleSolved?.Invoke();
    }

    // Update the text that the player sees when looking at it
    public override void OnFocus()
    {
        UpdatePrompt();
        base.OnFocus();
    }

    protected void UpdatePrompt()
    {
        promptMessage = isSolved ? "Terminal sealed" : overridePrompt;
    }
}
