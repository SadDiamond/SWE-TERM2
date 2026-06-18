using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

public class StartMenuController : MonoBehaviour
{
    private const string SensitivityKey = "project_structure.mouse_sensitivity";
    private const string FovKey = "project_structure.base_fov";
    private const string VolumeKey = "project_structure.master_volume";

    public static bool LaunchingArena { get; private set; }
    public static bool LaunchingSandbox { get; private set; }
    public static bool LaunchingHeroArena { get; private set; }

    private GameObject menuRoot;
    private GameObject settingsPanel;
    private Button startButton;
    private bool isLaunching;

    private void Start()
    {
        BuildCamera();
        EnsureEventSystem();
        BuildMenu();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        EventSystem.current.SetSelectedGameObject(startButton.gameObject);
    }

    private void Update()
    {
        if (UnityEngine.InputSystem.Keyboard.current == null) return;
        if (settingsPanel != null && settingsPanel.activeSelf)
        {
            if (UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame)
                ShowSettings(false);
            return;
        }

        if (isLaunching)
            return;

        if (UnityEngine.InputSystem.Keyboard.current.sKey.wasPressedThisFrame)
            Launch(true);
        else if (UnityEngine.InputSystem.Keyboard.current.hKey.wasPressedThisFrame)
            LaunchHeroArena();
        else if (UnityEngine.InputSystem.Keyboard.current.enterKey.wasPressedThisFrame ||
            UnityEngine.InputSystem.Keyboard.current.spaceKey.wasPressedThisFrame)
            Launch(false);
    }

    public static bool ConsumeArenaLaunch()
    {
        bool value = LaunchingArena;
        LaunchingArena = false;
        return value;
    }

    public static bool ConsumeSandboxLaunch()
    {
        bool value = LaunchingSandbox;
        LaunchingSandbox = false;
        return value;
    }

    public static bool ConsumeHeroArenaLaunch()
    {
        bool value = LaunchingHeroArena;
        LaunchingHeroArena = false;
        return value;
    }

    public static void SetLaunchFlags(bool arena, bool sandbox, bool heroArena = false)
    {
        LaunchingArena = arena;
        LaunchingSandbox = sandbox;
        LaunchingHeroArena = heroArena;
    }

    private void Launch(bool sandbox)
    {
        if (isLaunching)
            return;

        isLaunching = true;
        PersistentLoadingScreen.LoadArenaFromMenu(sandbox, false);
    }

    private void LaunchHeroArena()
    {
        if (isLaunching)
            return;

        isLaunching = true;
        PersistentLoadingScreen.LoadArenaFromMenu(false, true);
    }

    private void BuildCamera()
    {
        if (Camera.main != null) return;
        GameObject cameraObject = new GameObject("MenuCamera");
        cameraObject.tag = "MainCamera";
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.006f, 0.01f, 0.014f);
    }

    private void EnsureEventSystem()
    {
        if (EventSystem.current != null) return;
        GameObject eventSystemObject = new GameObject("EventSystem");
        eventSystemObject.AddComponent<EventSystem>();
        InputSystemUIInputModule inputModule = eventSystemObject.AddComponent<InputSystemUIInputModule>();
        inputModule.AssignDefaultActions();
    }

    private void BuildMenu()
    {
        Canvas canvas = CreateMenuCanvas();
        menuRoot = CreateFullScreenRoot(canvas.transform, "StartMenu", new Color(0.006f, 0.011f, 0.016f, 1f));

        CreateBlock(menuRoot.transform, "TopRule", new Vector2(0.5f, 0.91f), new Vector2(0f, 0f), new Vector2(0.86f, 0.003f), new Color(0.16f, 0.78f, 0.9f, 0.65f));
        CreateBlock(menuRoot.transform, "LeftRail", new Vector2(0.08f, 0.5f), new Vector2(0f, 0f), new Vector2(0.004f, 0.72f), new Color(0.16f, 0.78f, 0.9f, 1f));
        CreateBlock(menuRoot.transform, "SectorA", new Vector2(0.78f, 0.69f), new Vector2(0f, 0f), new Vector2(0.22f, 0.16f), new Color(0.025f, 0.065f, 0.078f, 0.8f));
        CreateBlock(menuRoot.transform, "SectorB", new Vector2(0.84f, 0.48f), new Vector2(0f, 0f), new Vector2(0.12f, 0.12f), new Color(0.055f, 0.035f, 0.025f, 0.9f));
        CreateBlock(menuRoot.transform, "SectorC", new Vector2(0.73f, 0.31f), new Vector2(0f, 0f), new Vector2(0.29f, 0.05f), new Color(0.025f, 0.065f, 0.078f, 0.7f));

        CreateText(menuRoot.transform, "PROJECT\nSTRUCTURE", 68f, new Vector2(0.13f, 0.72f), new Vector2(720f, 180f), TextAlignmentOptions.Left, Color.white);
        CreateText(menuRoot.transform, "FAST ARENA SHOOTER", 15f, new Vector2(0.13f, 0.59f), new Vector2(420f, 30f), TextAlignmentOptions.Left, new Color(0.35f, 0.86f, 0.95f));
        CreateText(menuRoot.transform, "Reach the core. Everything else is in the way.", 18f, new Vector2(0.13f, 0.51f), new Vector2(620f, 42f), TextAlignmentOptions.Left, new Color(0.78f, 0.84f, 0.87f));

        startButton = CreateButton(menuRoot.transform, "START RUN", new Vector2(0.13f, 0.38f), new Vector2(310f, 52f), () => Launch(false), true);
        CreateButton(menuRoot.transform, "HERO ARENA", new Vector2(0.13f, 0.30f), new Vector2(310f, 48f), LaunchHeroArena, false);
        CreateButton(menuRoot.transform, "SANDBOX", new Vector2(0.13f, 0.22f), new Vector2(310f, 48f), () => Launch(true), false);
        CreateButton(menuRoot.transform, "SETTINGS", new Vector2(0.13f, 0.14f), new Vector2(310f, 48f), () => ShowSettings(true), false);
        CreateButton(menuRoot.transform, "QUIT", new Vector2(0.13f, 0.06f), new Vector2(310f, 48f), Quit, false);

        CreateText(menuRoot.transform, "ENTER  START     H  HERO ARENA     S  SANDBOX", 11f, new Vector2(0.13f, 0.01f), new Vector2(640f, 26f), TextAlignmentOptions.Left, new Color(0.48f, 0.56f, 0.6f));
        BuildSettingsPanel(canvas.transform);
    }

    private Canvas CreateMenuCanvas()
    {
        GameObject canvasObject = new GameObject("StartMenuCanvas");
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.pixelPerfect = false;
        canvas.sortingOrder = 20;
        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        canvasObject.AddComponent<GraphicRaycaster>();
        return canvas;
    }

    private GameObject CreateFullScreenRoot(Transform parent, string name, Color color)
    {
        GameObject root = new GameObject(name);
        root.transform.SetParent(parent, false);
        RectTransform rect = root.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        Image image = root.AddComponent<Image>();
        image.color = color;
        return root;
    }

    private Button CreateButton(Transform parent, string label, Vector2 anchor, Vector2 size, UnityEngine.Events.UnityAction action, bool primary)
    {
        GameObject go = new GameObject(label.Replace(" ", string.Empty) + "Button");
        go.transform.SetParent(parent, false);
        RectTransform rect = go.AddComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = new Vector2(0f, 0.5f);
        rect.sizeDelta = size;
        Image image = go.AddComponent<Image>();
        Color baseColor = primary ? new Color(0.08f, 0.46f, 0.56f, 0.95f) : new Color(0.025f, 0.055f, 0.066f, 0.95f);
        image.color = baseColor;
        Button button = go.AddComponent<Button>();
        button.targetGraphic = image;
        ColorBlock colors = button.colors;
        colors.fadeDuration = 0f;
        colors.normalColor = Color.white;
        colors.highlightedColor = Color.white;
        colors.selectedColor = Color.white;
        colors.pressedColor = Color.white;
        button.colors = colors;
        button.onClick.AddListener(action);

        TMP_Text text = CreateText(go.transform, label, 16f, new Vector2(0.06f, 0.5f), new Vector2(size.x - 28f, size.y), TextAlignmentOptions.Left, Color.white);
        text.raycastTarget = false;
        text.fontStyle = FontStyles.UpperCase;

        GameObject accent = new GameObject("Accent");
        accent.transform.SetParent(go.transform, false);
        RectTransform accentRect = accent.AddComponent<RectTransform>();
        accentRect.anchorMin = new Vector2(0f, 0f);
        accentRect.anchorMax = new Vector2(0f, 1f);
        accentRect.pivot = new Vector2(0f, 0.5f);
        accentRect.sizeDelta = new Vector2(0f, 0f);
        Image accentImage = accent.AddComponent<Image>();
        accentImage.color = new Color(0.62f, 0.94f, 1f, 0.95f);
        accentImage.raycastTarget = false;

        StartMenuButtonFx fx = go.AddComponent<StartMenuButtonFx>();
        fx.Configure(rect, image, text, accentRect, baseColor, primary);
        return button;
    }

    private void BuildSettingsPanel(Transform parent)
    {
        settingsPanel = CreateFullScreenRoot(parent, "StartSettings", new Color(0.006f, 0.011f, 0.016f, 0.98f));
        CreateText(settingsPanel.transform, "SETTINGS", 42f, new Vector2(0.5f, 0.78f), new Vector2(540f, 70f), TextAlignmentOptions.Center, Color.white).rectTransform.pivot = new Vector2(0.5f, 0.5f);
        CreateSlider(settingsPanel.transform, "SENSITIVITY", 0.62f, 0f, 200f, PlayerPrefs.GetFloat(SensitivityKey, 100f), value => PlayerPrefs.SetFloat(SensitivityKey, value), "0");
        CreateSlider(settingsPanel.transform, "FIELD OF VIEW", 0.51f, 70f, 120f, PlayerPrefs.GetFloat(FovKey, 90f), value => PlayerPrefs.SetFloat(FovKey, value), "0");
        CreateSlider(settingsPanel.transform, "VOLUME", 0.40f, 0f, 1f, PlayerPrefs.GetFloat(VolumeKey, 1f), value => { PlayerPrefs.SetFloat(VolumeKey, value); AudioListener.volume = value; }, "0%");
        CreateSlider(settingsPanel.transform, "UI SCALE", 0.29f, ProjectStructureUIRoot.MinUIScale, ProjectStructureUIRoot.MaxUIScale, ProjectStructureUIRoot.GetUIScale(), value => ProjectStructureUIRoot.SetUIScale(value), "0.00");
        CreateButton(settingsPanel.transform, "BACK", new Vector2(0.5f, 0.15f), new Vector2(220f, 48f), () => ShowSettings(false), true).GetComponent<RectTransform>().pivot = new Vector2(0.5f, 0.5f);
        settingsPanel.SetActive(false);
    }

    private void CreateSlider(Transform parent, string label, float y, float min, float max, float value, UnityEngine.Events.UnityAction<float> action, string format)
    {
        CreateText(parent, label, 14f, new Vector2(0.32f, y + 0.035f), new Vector2(220f, 30f), TextAlignmentOptions.Left, new Color(0.72f, 0.9f, 0.95f));
        TMP_Text valueText = CreateText(parent, value.ToString(format), 14f, new Vector2(0.68f, y + 0.035f), new Vector2(120f, 30f), TextAlignmentOptions.Right, Color.white);
        valueText.rectTransform.pivot = new Vector2(1f, 0.5f);

        GameObject sliderObject = new GameObject(label.Replace(" ", string.Empty) + "Slider");
        sliderObject.transform.SetParent(parent, false);
        RectTransform rect = sliderObject.AddComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, y);
        rect.sizeDelta = new Vector2(520f, 18f);
        Image background = sliderObject.AddComponent<Image>();
        background.color = new Color(0.04f, 0.08f, 0.095f, 1f);
        Slider slider = sliderObject.AddComponent<Slider>();
        slider.minValue = min;
        slider.maxValue = max;
        slider.value = value;

        GameObject fill = new GameObject("Fill");
        fill.transform.SetParent(sliderObject.transform, false);
        RectTransform fillRect = fill.AddComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = new Vector2(3f, 3f);
        fillRect.offsetMax = new Vector2(-3f, -3f);
        Image fillImage = fill.AddComponent<Image>();
        fillImage.color = new Color(0.15f, 0.75f, 0.88f, 1f);
        slider.fillRect = fillRect;
        slider.onValueChanged.AddListener(newValue =>
        {
            valueText.text = format == "0%" ? Mathf.RoundToInt(newValue * 100f) + "%" : newValue.ToString(format);
            action(newValue);
            PlayerPrefs.Save();
        });
    }

    private void ShowSettings(bool visible)
    {
        if (isLaunching)
            return;

        settingsPanel.SetActive(visible);
        menuRoot.SetActive(!visible);
        if (!visible)
            EventSystem.current.SetSelectedGameObject(startButton.gameObject);
    }

    private TMP_Text CreateText(Transform parent, string value, float size, Vector2 anchor, Vector2 bounds, TextAlignmentOptions alignment, Color color)
    {
        GameObject go = new GameObject("Text");
        go.transform.SetParent(parent, false);
        RectTransform rect = go.AddComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = new Vector2(0f, 0.5f);
        rect.sizeDelta = bounds;
        TMP_Text text = go.AddComponent<TextMeshProUGUI>();
        ProjectStructureUIRoot.ApplyDefaultFont(text);
        text.text = value;
        text.fontSize = size;
        text.alignment = alignment;
        text.color = color;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        return text;
    }

    private void CreateBlock(Transform parent, string name, Vector2 anchor, Vector2 anchoredPosition, Vector2 normalizedSize, Color color)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        RectTransform rect = go.AddComponent<RectTransform>();
        rect.anchorMin = anchor - normalizedSize * 0.5f;
        rect.anchorMax = anchor + normalizedSize * 0.5f;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = Vector2.zero;
        Image image = go.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
    }

    private void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}

public class StartMenuButtonFx : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler, IPointerDownHandler, IPointerUpHandler
{
    private RectTransform rect;
    private Image background;
    private TMP_Text label;
    private RectTransform accent;
    private Color baseColor;
    private Color hoverColor;
    private Color pressedColor;
    private Vector2 baseSize;
    private Vector2 hoverSize;
    private bool hovered;
    private bool selected;
    private bool pressed;

    public void Configure(RectTransform targetRect, Image targetBackground, TMP_Text targetLabel, RectTransform targetAccent, Color targetBaseColor, bool primary)
    {
        rect = targetRect;
        background = targetBackground;
        label = targetLabel;
        accent = targetAccent;
        baseColor = targetBaseColor;
        hoverColor = primary ? new Color(0.16f, 0.62f, 0.72f, 0.98f) : new Color(0.055f, 0.12f, 0.145f, 0.98f);
        pressedColor = primary ? new Color(0.1f, 0.38f, 0.45f, 0.98f) : new Color(0.03f, 0.085f, 0.105f, 0.98f);
        baseSize = rect.sizeDelta;
        hoverSize = baseSize + new Vector2(18f, 0f);
        ApplyImmediate(false);
    }

    private void Update()
    {
        if (rect == null || background == null || label == null || accent == null)
            return;

        bool active = hovered || selected;
        Color targetColor = pressed ? pressedColor : active ? hoverColor : baseColor;
        Vector2 targetSize = active ? hoverSize : baseSize;
        float targetAccentWidth = active ? 10f : 0f;
        Color targetLabelColor = active ? new Color(0.86f, 0.98f, 1f) : Color.white;

        background.color = Color.Lerp(background.color, targetColor, Time.unscaledDeltaTime * 16f);
        rect.sizeDelta = Vector2.Lerp(rect.sizeDelta, targetSize, Time.unscaledDeltaTime * 16f);
        accent.sizeDelta = Vector2.Lerp(accent.sizeDelta, new Vector2(targetAccentWidth, accent.sizeDelta.y), Time.unscaledDeltaTime * 18f);
        label.color = Color.Lerp(label.color, targetLabelColor, Time.unscaledDeltaTime * 20f);
    }

    public void OnPointerEnter(PointerEventData eventData) => hovered = true;
    public void OnPointerExit(PointerEventData eventData) => hovered = false;
    public void OnSelect(BaseEventData eventData) => selected = true;
    public void OnDeselect(BaseEventData eventData) => selected = false;
    public void OnPointerDown(PointerEventData eventData) => pressed = true;
    public void OnPointerUp(PointerEventData eventData) => pressed = false;

    private void ApplyImmediate(bool active)
    {
        if (background != null)
            background.color = active ? hoverColor : baseColor;
        if (rect != null)
            rect.sizeDelta = active ? hoverSize : baseSize;
        if (accent != null)
            accent.sizeDelta = new Vector2(active ? 10f : 0f, 0f);
        if (label != null)
            label.color = active ? new Color(0.86f, 0.98f, 1f) : Color.white;
    }
}
