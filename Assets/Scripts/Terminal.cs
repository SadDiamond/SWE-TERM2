using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using TMPro;

public class Terminal : Interactable
{
    [Header("Terminal State")]
    public bool isSolved = false;
    public string overridePrompt = "Breach terminal node";
    [Min(0.5f)] public float terminalInteractRange = 4.5f;

    [Header("Connected Systems")]
    public Door connectedDoor; // The door this terminal will unlock
    public bool autoOpenConnectedDoor = true; // If false, only unlocks — player still has to open it

    // UnityEvents allow you to drag-and-drop anything in the Inspector
    // Example: turning off lights, playing sounds, etc.
    public UnityEvent onPuzzleSolved;

    private Renderer screenRenderer;
    private Renderer statusLightRenderer;
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
        if (collider != null) Destroy(collider);
        Renderer renderer = part.GetComponent<Renderer>();
        if (renderer != null) renderer.material = material;
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

        // Fallback: try to find the exit pit (goal marker tile location)
        Vector3 exitPos = FindExitWorldPosition();
        if (exitPos != Vector3.zero)
        {
            lineRenderer.useWorldSpace = true;
            lineRenderer.positionCount = 4;
            Vector3 start = transform.position + Vector3.up * 0.08f;
            Vector3 midA = Vector3.Lerp(start, exitPos, 0.33f) + Vector3.up * 0.15f;
            Vector3 midB = Vector3.Lerp(start, exitPos, 0.66f) + Vector3.up * 0.15f;
            lineRenderer.SetPosition(0, start);
            lineRenderer.SetPosition(1, midA);
            lineRenderer.SetPosition(2, midB);
            lineRenderer.SetPosition(3, exitPos + Vector3.up * 0.08f);
            RefreshCircuitLineVisual();
        }
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
