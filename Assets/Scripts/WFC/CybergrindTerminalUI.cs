using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

public class CybergrindTerminalUI : MonoBehaviour
{
    public static CybergrindTerminalUI Instance { get; private set; }

    private CybergrindPuzzleTerminal activeTerminal;
    private PlayerController activePlayer;
    private Canvas canvas;
    private GameObject window;
    private Image backdropImage;
    private Image windowImage;
    private TextMeshProUGUI titleText;
    private TextMeshProUGUI modeText;
    private TextMeshProUGUI seedText;
    private TextMeshProUGUI statusText;
    private TextMeshProUGUI instructionText;
    private TextMeshProUGUI detailText;
    private TextMeshProUGUI footerText;
    private Image progressFill;
    private Button primaryButton;
    private Button secondaryButton;
    private Button increaseButton;
    private Button decreaseButton;
    private Button submitButton;
    private Button closeButton;
    private Image primaryButtonImage;
    private Image secondaryButtonImage;
    private Image submitButtonImage;
    private float closeTimer = -1f;
    private string transientMessage;
    private float transientMessageTimer = -1f;

    public static CybergrindTerminalUI GetOrCreate()
    {
        if (Instance != null) return Instance;

        GameObject go = new GameObject("RuntimeArenaTerminalUI");
        Instance = go.AddComponent<CybergrindTerminalUI>();
        if (Application.isPlaying)
            DontDestroyOnLoad(go);
        return Instance;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        if (Application.isPlaying)
            DontDestroyOnLoad(gameObject);
        BuildUI();
        EnsureEventSystem();
        HideUI();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void OpenTerminal(CybergrindPuzzleTerminal terminal, PlayerController player)
    {
        EnsureBuilt();
        activeTerminal = terminal;
        activePlayer = player;
        transientMessage = string.Empty;
        closeTimer = -1f;
        ShowUI();
        RefreshFromTerminal(terminal);
        if (activePlayer != null)
            activePlayer.ToggleUIMode(true);
    }

    public void CloseTerminal(CybergrindPuzzleTerminal terminal)
    {
        if (activeTerminal != terminal && activeTerminal != null) return;
        PlayerController player = activePlayer;
        activeTerminal = null;
        activePlayer = null;
        HideUI();
        if (player != null)
        {
            player.ToggleUIMode(false);
            player.enabled = true;
            var controller = player.GetComponent<CharacterController>();
            if (controller != null)
                controller.enabled = true;
            player.RefreshVitalsUI();
        }
    }

    public void NotifySolved(CybergrindPuzzleTerminal terminal)
    {
        if (activeTerminal != terminal) return;
        closeTimer = 0.75f;
        RefreshFromTerminal(terminal);
    }

    public void SetTransientMessage(string message)
    {
        transientMessage = message;
        transientMessageTimer = 1.25f;
        RefreshFromTerminal(activeTerminal);
    }

    private void Update()
    {
        if (activeTerminal == null || canvas == null || !canvas.gameObject.activeInHierarchy) return;
        if (Keyboard.current == null) return;

        if (Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            activeTerminal.CancelPuzzle();
            return;
        }

        if (Keyboard.current.eKey.wasPressedThisFrame || Keyboard.current.spaceKey.wasPressedThisFrame)
            activeTerminal.SubmitPrimaryAction();

        if (Keyboard.current.qKey.wasPressedThisFrame || Keyboard.current.leftArrowKey.wasPressedThisFrame)
            activeTerminal.SubmitSecondaryAction();

        if (Keyboard.current.upArrowKey.wasPressedThisFrame || Keyboard.current.equalsKey.wasPressedThisFrame)
            activeTerminal.SubmitIncrease();

        if (Keyboard.current.downArrowKey.wasPressedThisFrame || Keyboard.current.minusKey.wasPressedThisFrame)
            activeTerminal.SubmitDecrease();

        if (Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.numpadEnterKey.wasPressedThisFrame)
            activeTerminal.SubmitConfirm();

        if (closeTimer >= 0f)
        {
            closeTimer -= Time.deltaTime;
            if (closeTimer <= 0f && activeTerminal != null)
                activeTerminal.CancelPuzzle();
        }

        if (transientMessageTimer >= 0f)
        {
            transientMessageTimer -= Time.deltaTime;
            if (transientMessageTimer <= 0f)
            {
                transientMessage = string.Empty;
                transientMessageTimer = -1f;
            }
        }

        if (activeTerminal != null)
            RefreshFromTerminal(activeTerminal);
    }

    public void RefreshFromTerminal(CybergrindPuzzleTerminal terminal)
    {
        EnsureBuilt();
        if (terminal == null)
        {
            HideUI();
            return;
        }

        if (string.IsNullOrEmpty(transientMessage))
            transientMessageTimer = -1f;

        titleText.text = terminal.GetTerminalTitle();
        modeText.text = terminal.GetModeLabel();
        seedText.text = terminal.GetHintLabel();
        statusText.text = string.IsNullOrEmpty(transientMessage) ? terminal.GetStatusLine() : transientMessage;
        instructionText.text = terminal.GetInstructionLine();
        detailText.text = terminal.GetDetailLine();
        if (footerText != null)
            footerText.text = BuildFooterText(terminal);
        progressFill.fillAmount = terminal.GetProgress01();
        progressFill.color = ResolveProgressColor(terminal);
        ApplyChallengeVisuals(terminal);

        primaryButton.GetComponentInChildren<TextMeshProUGUI>().text = terminal.GetPrimaryActionLabel();
        secondaryButton.GetComponentInChildren<TextMeshProUGUI>().text = terminal.GetSecondaryActionLabel();
        increaseButton.GetComponentInChildren<TextMeshProUGUI>().text = "+";
        decreaseButton.GetComponentInChildren<TextMeshProUGUI>().text = "-";
        submitButton.GetComponentInChildren<TextMeshProUGUI>().text = terminal.GetSubmitLabel();
        closeButton.GetComponentInChildren<TextMeshProUGUI>().text = "CLOSE";

        increaseButton.gameObject.SetActive(terminal.challengeMode == CybergrindPuzzleTerminal.ChallengeMode.Calibration);
        decreaseButton.gameObject.SetActive(terminal.challengeMode == CybergrindPuzzleTerminal.ChallengeMode.Calibration);
        submitButton.gameObject.SetActive(terminal.CanSubmitNow() || terminal.challengeMode == CybergrindPuzzleTerminal.ChallengeMode.Delay);
    }

    private void BuildUI()
    {
        if (canvas != null && titleText != null && window != null)
            return;

        GameObject canvasObject = new GameObject("ArenaTerminalCanvas");
        canvasObject.transform.SetParent(transform, false);
        canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5000;
        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        ProjectStructureUIRoot.ConfigureScaler(scaler);
        canvasObject.AddComponent<GraphicRaycaster>();

        RectTransform canvasRect = canvas.GetComponent<RectTransform>();
        canvasRect.anchorMin = Vector2.zero;
        canvasRect.anchorMax = Vector2.one;
        canvasRect.offsetMin = Vector2.zero;
        canvasRect.offsetMax = Vector2.zero;

        GameObject backdrop = new GameObject("Backdrop");
        backdrop.transform.SetParent(canvasObject.transform, false);
        backdropImage = backdrop.AddComponent<Image>();
        backdropImage.color = new Color(0f, 0f, 0f, 0.28f);
        RectTransform backdropRect = backdrop.GetComponent<RectTransform>();
        backdropRect.anchorMin = Vector2.zero;
        backdropRect.anchorMax = Vector2.one;
        backdropRect.offsetMin = Vector2.zero;
        backdropRect.offsetMax = Vector2.zero;

        window = new GameObject("Window");
        window.transform.SetParent(backdrop.transform, false);
        windowImage = window.AddComponent<Image>();
        windowImage.color = new Color(0.04f, 0.07f, 0.09f, 0.96f);
        RectTransform windowRect = window.GetComponent<RectTransform>();
        windowRect.anchorMin = new Vector2(0.25f, 0.18f);
        windowRect.anchorMax = new Vector2(0.75f, 0.78f);
        windowRect.offsetMin = Vector2.zero;
        windowRect.offsetMax = Vector2.zero;

        CreateText(window.transform, "Title", out titleText, new Vector2(0.05f, 0.82f), new Vector2(0.55f, 0.95f), 38f, TextAlignmentOptions.Left, new Color(0.58f, 0.96f, 1f));
        CreateText(window.transform, "Mode", out modeText, new Vector2(0.62f, 0.82f), new Vector2(0.95f, 0.95f), 30f, TextAlignmentOptions.Right, new Color(0.35f, 0.95f, 0.48f));
        CreateText(window.transform, "Seed", out seedText, new Vector2(0.05f, 0.73f), new Vector2(0.95f, 0.8f), 24f, TextAlignmentOptions.Left, new Color(0.75f, 0.82f, 0.9f));
        CreateText(window.transform, "Status", out statusText, new Vector2(0.05f, 0.61f), new Vector2(0.95f, 0.72f), 28f, TextAlignmentOptions.Left, Color.white);
        CreateText(window.transform, "Instruction", out instructionText, new Vector2(0.05f, 0.49f), new Vector2(0.95f, 0.6f), 24f, TextAlignmentOptions.Left, new Color(0.88f, 0.9f, 0.95f));
        CreateText(window.transform, "Detail", out detailText, new Vector2(0.05f, 0.39f), new Vector2(0.95f, 0.47f), 20f, TextAlignmentOptions.Left, new Color(0.65f, 0.84f, 0.9f));
        CreateText(window.transform, "Footer", out footerText, new Vector2(0.05f, 0.01f), new Vector2(0.95f, 0.08f), 18f, TextAlignmentOptions.Center, new Color(0.72f, 0.82f, 0.9f));

        GameObject barBg = new GameObject("ProgressBackground");
        barBg.transform.SetParent(window.transform, false);
        Image barBgImage = barBg.AddComponent<Image>();
        barBgImage.color = new Color(0.11f, 0.14f, 0.16f, 1f);
        RectTransform barBgRect = barBg.GetComponent<RectTransform>();
        barBgRect.anchorMin = new Vector2(0.05f, 0.31f);
        barBgRect.anchorMax = new Vector2(0.95f, 0.36f);
        barBgRect.offsetMin = Vector2.zero;
        barBgRect.offsetMax = Vector2.zero;

        GameObject barFill = new GameObject("ProgressFill");
        barFill.transform.SetParent(barBg.transform, false);
        progressFill = barFill.AddComponent<Image>();
        progressFill.type = Image.Type.Filled;
        progressFill.fillMethod = Image.FillMethod.Horizontal;
        progressFill.fillOrigin = 0;
        progressFill.fillAmount = 0f;
        progressFill.color = new Color(0.26f, 0.95f, 0.44f, 1f);
        RectTransform barFillRect = progressFill.GetComponent<RectTransform>();
        barFillRect.anchorMin = Vector2.zero;
        barFillRect.anchorMax = Vector2.one;
        barFillRect.offsetMin = Vector2.zero;
        barFillRect.offsetMax = Vector2.zero;

        GameObject buttonRow = new GameObject("Buttons");
        buttonRow.transform.SetParent(window.transform, false);
        RectTransform buttonRowRect = buttonRow.AddComponent<RectTransform>();
        buttonRowRect.anchorMin = new Vector2(0.05f, 0.08f);
        buttonRowRect.anchorMax = new Vector2(0.95f, 0.27f);
        buttonRowRect.offsetMin = Vector2.zero;
        buttonRowRect.offsetMax = Vector2.zero;

        primaryButton = CreateButton(buttonRow.transform, "Primary", new Vector2(0.00f, 0.52f), new Vector2(0.31f, 1f), new Color(0.16f, 0.35f, 0.42f));
        primaryButtonImage = primaryButton.GetComponent<Image>();
        secondaryButton = CreateButton(buttonRow.transform, "Secondary", new Vector2(0.34f, 0.52f), new Vector2(0.65f, 1f), new Color(0.16f, 0.29f, 0.35f));
        secondaryButtonImage = secondaryButton.GetComponent<Image>();
        submitButton = CreateButton(buttonRow.transform, "Submit", new Vector2(0.68f, 0.52f), new Vector2(1f, 1f), new Color(0.16f, 0.42f, 0.18f));
        submitButtonImage = submitButton.GetComponent<Image>();
        increaseButton = CreateButton(buttonRow.transform, "+", new Vector2(0.00f, 0.00f), new Vector2(0.23f, 0.44f), new Color(0.28f, 0.28f, 0.38f));
        decreaseButton = CreateButton(buttonRow.transform, "-", new Vector2(0.25f, 0.00f), new Vector2(0.48f, 0.44f), new Color(0.28f, 0.28f, 0.38f));
        closeButton = CreateButton(buttonRow.transform, "Close", new Vector2(0.74f, 0.00f), new Vector2(1f, 0.44f), new Color(0.38f, 0.18f, 0.18f));

        primaryButton.onClick.AddListener(() => activeTerminal?.SubmitPrimaryAction());
        secondaryButton.onClick.AddListener(() => activeTerminal?.SubmitSecondaryAction());
        increaseButton.onClick.AddListener(() => activeTerminal?.SubmitIncrease());
        decreaseButton.onClick.AddListener(() => activeTerminal?.SubmitDecrease());
        submitButton.onClick.AddListener(() => activeTerminal?.SubmitConfirm());
        closeButton.onClick.AddListener(() => activeTerminal?.CancelPuzzle());
    }

    private void EnsureBuilt()
    {
        if (canvas != null && titleText != null && window != null)
            return;

        BuildUI();
        EnsureEventSystem();
        HideUI();
    }

    private Button CreateButton(Transform parent, string label, Vector2 anchorMin, Vector2 anchorMax, Color color)
    {
        GameObject buttonObject = new GameObject(label + "Button");
        buttonObject.transform.SetParent(parent, false);

        Image image = buttonObject.AddComponent<Image>();
        image.color = color;

        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = image;

        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = new Vector2(8f, 8f);
        rect.offsetMax = new Vector2(-8f, -8f);

        GameObject textObject = new GameObject("Label");
        textObject.transform.SetParent(buttonObject.transform, false);
        TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
        text.text = label;
        text.fontSize = 24f;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        text.enableAutoSizing = true;
        if (TMP_Settings.defaultFontAsset != null)
            text.font = TMP_Settings.defaultFontAsset;

        RectTransform textRect = text.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        return button;
    }

    private void CreateText(Transform parent, string name, out TextMeshProUGUI text, Vector2 anchorMin, Vector2 anchorMax, float fontSize, TextAlignmentOptions alignment, Color color)
    {
        GameObject textObject = new GameObject(name);
        textObject.transform.SetParent(parent, false);
        text = textObject.AddComponent<TextMeshProUGUI>();
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = color;
        text.enableAutoSizing = true;
        if (TMP_Settings.defaultFontAsset != null)
            text.font = TMP_Settings.defaultFontAsset;

        RectTransform rect = text.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private void EnsureEventSystem()
    {
        if (FindAnyObjectByType<EventSystem>() != null)
            return;

        GameObject eventSystem = new GameObject("RuntimeEventSystem");
        if (Application.isPlaying)
            DontDestroyOnLoad(eventSystem);
        eventSystem.AddComponent<EventSystem>();
        eventSystem.AddComponent<InputSystemUIInputModule>();
    }

    private void ShowUI()
    {
        if (canvas != null)
            canvas.gameObject.SetActive(true);
    }

    private void HideUI()
    {
        if (canvas != null)
            canvas.gameObject.SetActive(false);
        closeTimer = -1f;
        transientMessage = string.Empty;
        transientMessageTimer = -1f;
    }

    private string BuildFooterText(CybergrindPuzzleTerminal terminal)
    {
        if (terminal == null) return string.Empty;

        if (terminal.challengeMode == CybergrindPuzzleTerminal.ChallengeMode.Calibration)
            return "UP / DOWN or +/- adjust    ENTER confirm    ESC close";

        return "E / SPACE primary    Q / LEFT secondary    ENTER confirm    ESC close";
    }

    private Color ResolveProgressColor(CybergrindPuzzleTerminal terminal)
    {
        if (terminal == null)
            return new Color(0.26f, 0.95f, 0.44f, 1f);

        return terminal.challengeMode switch
        {
            CybergrindPuzzleTerminal.ChallengeMode.Hold => new Color(0.36f, 0.88f, 1f, 1f),
            CybergrindPuzzleTerminal.ChallengeMode.Burst => new Color(1f, 0.52f, 0.18f, 1f),
            CybergrindPuzzleTerminal.ChallengeMode.Lockstep => new Color(0.95f, 0.34f, 0.76f, 1f),
            CybergrindPuzzleTerminal.ChallengeMode.Calibration => new Color(0.86f, 0.76f, 0.24f, 1f),
            _ => new Color(0.26f, 0.95f, 0.44f, 1f)
        };
    }

    private void ApplyChallengeVisuals(CybergrindPuzzleTerminal terminal)
    {
        Color accent = ResolveProgressColor(terminal);

        if (windowImage != null)
            windowImage.color = Color.Lerp(new Color(0.04f, 0.07f, 0.09f, 0.9f), accent * new Color(1f, 1f, 1f, 0.9f), 0.12f);
        if (backdropImage != null)
            backdropImage.color = Color.Lerp(new Color(0f, 0f, 0f, 0.28f), accent * new Color(1f, 1f, 1f, 0.28f), 0.04f);
        if (titleText != null)
            titleText.color = Color.Lerp(new Color(0.58f, 0.96f, 1f), accent, 0.35f);
        if (modeText != null)
            modeText.color = Color.Lerp(new Color(0.35f, 0.95f, 0.48f), accent, 0.25f);
        if (primaryButtonImage != null)
            primaryButtonImage.color = Color.Lerp(new Color(0.16f, 0.35f, 0.42f), accent, 0.24f);
        if (secondaryButtonImage != null)
            secondaryButtonImage.color = Color.Lerp(new Color(0.16f, 0.29f, 0.35f), accent * new Color(0.9f, 0.9f, 1f, 1f), 0.18f);
        if (submitButtonImage != null)
            submitButtonImage.color = Color.Lerp(new Color(0.16f, 0.42f, 0.18f), accent, 0.3f);
    }
}
