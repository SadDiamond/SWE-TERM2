using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ProjectStructureSettingsMenu : MonoBehaviour
{
    public PlayerController player;
    public KeyCode toggleKey = KeyCode.Escape;

    private GameObject panelRoot;
    private TMP_Text titleText;
    private TMP_Text bodyText;
    private Image panelImage;
    private int selectedIndex;
    private bool isOpen;

    private float sensitivity;
    private float baseFov;
    private float masterVolume;
    private float previousTimeScale = 1f;
    private bool previousUiState;
    private ProjectStructurePresentation presentation;

    private readonly string[] optionLabels =
    {
        "Look Gain",
        "Base FOV",
        "Signal Volume",
        "Reset Link",
        "Restart Descent",
        "Return To Title"
    };

    private void Start()
    {
        if (player == null)
            player = FindAnyObjectByType<PlayerController>();
        if (presentation == null)
            presentation = FindAnyObjectByType<ProjectStructurePresentation>();

        CacheValues();
        BuildOverlay();
        RefreshText();
        SetVisible(false);
    }

    private void Update()
    {
        if (UnityEngine.InputSystem.Keyboard.current == null) return;

        if (UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            Toggle();
            return;
        }

        if (!isOpen) return;

        if (UnityEngine.InputSystem.Keyboard.current.upArrowKey.wasPressedThisFrame)
            selectedIndex = (selectedIndex + optionLabels.Length - 1) % optionLabels.Length;
        else if (UnityEngine.InputSystem.Keyboard.current.downArrowKey.wasPressedThisFrame)
            selectedIndex = (selectedIndex + 1) % optionLabels.Length;

        float delta = 0f;
        if (UnityEngine.InputSystem.Keyboard.current.leftArrowKey.wasPressedThisFrame)
            delta = -1f;
        else if (UnityEngine.InputSystem.Keyboard.current.rightArrowKey.wasPressedThisFrame)
            delta = 1f;

        if (Mathf.Abs(delta) > 0.01f)
            AdjustSelected(delta);

        if (UnityEngine.InputSystem.Keyboard.current.enterKey.wasPressedThisFrame ||
            UnityEngine.InputSystem.Keyboard.current.spaceKey.wasPressedThisFrame)
            ActivateSelected();

        RefreshText();
    }

    private void Toggle()
    {
        if (isOpen)
        {
            ApplyAndClose();
            return;
        }

        if (presentation == null)
            presentation = FindAnyObjectByType<ProjectStructurePresentation>();

        CacheValues();
        isOpen = true;
        previousTimeScale = Time.timeScale;
        previousUiState = player != null && player.isUIActive;
        Time.timeScale = 0f;
        if (player != null)
            player.ToggleUIMode(true);
        SetVisible(true);
        RefreshText();
    }

    private void ApplyAndClose()
    {
        if (player != null)
            player.ApplySettings(sensitivity, baseFov, masterVolume);

        isOpen = false;
        Time.timeScale = previousTimeScale;
        if (player != null)
            player.ToggleUIMode(previousUiState);
        SetVisible(false);
    }

    private void CacheValues()
    {
        if (player == null) return;
        sensitivity = player.mouseSensitivity;
        baseFov = player.GetBaseFov();
        masterVolume = player.GetMasterVolume();
    }

    private void AdjustSelected(float direction)
    {
        if (selectedIndex == optionLabels.Length - 1) return;

        switch (selectedIndex)
        {
            case 0:
                sensitivity = Mathf.Clamp(sensitivity + direction * 5f, 20f, 220f);
                break;
            case 1:
                baseFov = Mathf.Clamp(baseFov + direction * 2f, 70f, 120f);
                break;
            case 2:
                masterVolume = Mathf.Clamp01(masterVolume + direction * 0.05f);
                break;
        }

        if (player != null)
            player.ApplySettings(sensitivity, baseFov, masterVolume, false);
    }

    private void ActivateSelected()
    {
        switch (selectedIndex)
        {
            case 3:
                sensitivity = 100f;
                baseFov = 90f;
                masterVolume = 1f;
                if (player != null)
                    player.ApplySettings(sensitivity, baseFov, masterVolume, false);
                break;
            case 4:
                if (presentation != null)
                    presentation.RestartRunFromMenu();
                isOpen = false;
                SetVisible(false);
                break;
            case 5:
                if (presentation != null)
                    presentation.ReturnToTitleFromMenu();
                isOpen = false;
                SetVisible(false);
                break;
        }
    }

    private void BuildOverlay()
    {
        Canvas canvas = ProjectStructureUIRoot.GetOrCreateCanvas();
        if (canvas == null) return;

        panelRoot = new GameObject("ProjectStructureSettingsOverlay");
        panelRoot.transform.SetParent(canvas.transform, false);

        RectTransform rootRect = panelRoot.AddComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;

        panelImage = panelRoot.AddComponent<Image>();
        panelImage.color = new Color(0.01f, 0.03f, 0.05f, 0.92f);

        titleText = CreateText(panelRoot.transform, "SettingsTitle", 44f, TextAlignmentOptions.Center, new Vector2(0.5f, 0.72f));
        bodyText = CreateText(panelRoot.transform, "SettingsBody", 24f, TextAlignmentOptions.Center, new Vector2(0.5f, 0.44f));
    }

    private TMP_Text CreateText(Transform parent, string name, float size, TextAlignmentOptions alignment, Vector2 anchor)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        RectTransform rect = go.AddComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(1100f, 260f);

        TMP_Text text = go.AddComponent<TextMeshProUGUI>();
        text.fontSize = size;
        text.alignment = alignment;
        text.color = Color.white;
        text.textWrappingMode = TextWrappingModes.Normal;
        return text;
    }

    private void SetVisible(bool visible)
    {
        if (panelRoot != null)
        {
            panelRoot.SetActive(visible);
            if (visible)
                ProjectStructureUIRoot.BringToFront(panelRoot.transform);
        }
    }

    private void RefreshText()
    {
        if (titleText == null || bodyText == null) return;

        if (presentation == null)
            presentation = FindAnyObjectByType<ProjectStructurePresentation>();

        titleText.text = presentation != null && presentation.IsTitleVisible
            ? "SYSTEM LINK // STANDBY"
            : "SETTINGS // SYSTEM LINK";
        titleText.color = ResolveAccentColor();
        if (panelImage != null)
            panelImage.color = ResolvePanelColor();

        bool actionLine = selectedIndex >= 3;
        string footer = selectedIndex == 3
            ? "UP / DOWN select   LEFT / RIGHT adjust   ENTER restore defaults   ESC close channel"
            : actionLine
                ? "UP / DOWN select   ENTER confirm   ESC close channel"
                : "UP / DOWN select   LEFT / RIGHT adjust   ESC close channel";
        string restartLabel = presentation != null && presentation.IsTitleVisible ? "Begin Descent" : "Restart Descent";
        bodyText.text =
            BuildStatusLine() + "\n\n" +
            $"{GetLine(0, $"Look Gain          {Mathf.RoundToInt(sensitivity),3}   {BuildMeter(Mathf.InverseLerp(20f, 220f, sensitivity), 12)}")}\n" +
            $"{GetLine(1, $"Base FOV           {Mathf.RoundToInt(baseFov),3}   {BuildMeter(Mathf.InverseLerp(70f, 120f, baseFov), 12)}")}\n" +
            $"{GetLine(2, $"Signal Volume      {Mathf.RoundToInt(masterVolume * 100f),3}%  {BuildMeter(masterVolume, 12)}")}\n" +
            $"{GetLine(3, "Reset Link")}\n" +
            $"{GetLine(4, restartLabel)}\n" +
            $"{GetLine(5, "Return To Title")}\n\n" +
            footer;
    }

    private string GetLine(int index, string label)
    {
        return selectedIndex == index ? $"> {label} <" : label;
    }

    private string BuildMeter(float normalized, int segments)
    {
        int filled = Mathf.RoundToInt(Mathf.Clamp01(normalized) * segments);
        string meter = string.Empty;
        for (int i = 0; i < segments; i++)
            meter += i < filled ? "|" : ".";
        return meter;
    }

    private string BuildStatusLine()
    {
        CybergrindArenaDirector director = FindAnyObjectByType<CybergrindArenaDirector>();
        if (presentation != null && presentation.IsTitleVisible)
            return "Standby channel open. Set the rig, then cut into the route when you are ready.";
        if (director == null || director.generator == null)
            return "System link active. Adjust the descent rig before the channel resumes.";

        string mode = director.generator.arenaMode switch
        {
            CybergrindArenaGenerator.ArenaMode.Shop => "INTERCHANGE",
            CybergrindArenaGenerator.ArenaMode.Boss => "CHAMPION CHAMBER",
            _ => $"FLOOR {director.floor:00}"
        };
        return $"{director.CurrentThemeLabel.ToUpperInvariant()} // {mode} // {director.CurrentDirectiveTitle}";
    }

    private Color ResolveAccentColor()
    {
        CybergrindArenaDirector director = FindAnyObjectByType<CybergrindArenaDirector>();
        int themeIndex = director != null ? director.CurrentThemeIndex : 0;
        switch (Math.Abs(themeIndex) % 4)
        {
            case 1:
                return new Color(0.62f, 0.78f, 1f, 1f);
            case 2:
                return new Color(1f, 0.72f, 0.34f, 1f);
            case 3:
                return new Color(0.64f, 0.98f, 0.72f, 1f);
            default:
                return new Color(0.78f, 0.94f, 1f, 1f);
        }
    }

    private Color ResolvePanelColor()
    {
        CybergrindArenaDirector director = FindAnyObjectByType<CybergrindArenaDirector>();
        if (presentation != null && presentation.IsTitleVisible)
            return new Color(0.015f, 0.03f, 0.05f, 0.94f);
        if (director == null || director.generator == null)
            return new Color(0.01f, 0.03f, 0.05f, 0.92f);

        return director.generator.arenaMode switch
        {
            CybergrindArenaGenerator.ArenaMode.Shop => new Color(0.015f, 0.055f, 0.05f, 0.94f),
            CybergrindArenaGenerator.ArenaMode.Boss => new Color(0.06f, 0.02f, 0.025f, 0.95f),
            _ => new Color(0.015f, 0.035f, 0.055f, 0.93f)
        };
    }
}
