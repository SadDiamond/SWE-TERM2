using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using TMPro;

public static class ProjectStructureUIRoot
{
    public const string CanvasName = "ProjectStructureCanvas";
    public const string UIScalePrefKey = "ProjectStructure_UIScale";
    public const float MinUIScale = 0.5f;
    public const float MaxUIScale = 4f;
    public const float UIScaleStep = 0.5f;
    private static float cachedUIScale = -1f;
    private static TMP_FontAsset cachedDefaultFont;

    public static Canvas GetOrCreateCanvas()
    {
        Canvas canvas = FindCanvas();
        if (canvas != null)
        {
            ApplyCanvasScale(canvas);
            return canvas;
        }

        GameObject canvasGo = new GameObject(CanvasName);
        canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.pixelPerfect = false;
        canvas.sortingOrder = 0;

        canvasGo.AddComponent<CanvasScaler>();
        ApplyCanvasScale(canvas);

        canvasGo.AddComponent<GraphicRaycaster>();
        EnsureEventSystem();
        return canvas;
    }

    public static float GetUIScale()
    {
        if (cachedUIScale < 0f)
            cachedUIScale = NormalizeUIScale(PlayerPrefs.GetFloat(UIScalePrefKey, 1f));
        return cachedUIScale;
    }

    public static void SetUIScale(float scale, bool persist = true)
    {
        cachedUIScale = NormalizeUIScale(scale);
        if (persist)
        {
            PlayerPrefs.SetFloat(UIScalePrefKey, cachedUIScale);
            PlayerPrefs.Save();
        }

        ApplyUIScaleToAllCanvases();
    }

    public static float NormalizeUIScale(float scale)
    {
        float clamped = Mathf.Clamp(scale, MinUIScale, MaxUIScale);
        float steps = Mathf.Round((clamped - MinUIScale) / UIScaleStep);
        return Mathf.Clamp(MinUIScale + steps * UIScaleStep, MinUIScale, MaxUIScale);
    }

    public static void ApplyUIScaleToAllCanvases()
    {
        Canvas[] canvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include);
        for (int i = 0; i < canvases.Length; i++)
        {
            Canvas canvas = canvases[i];
            if (canvas == null) continue;
            ApplyCanvasScale(canvas);
        }
    }

    public static void ApplyCanvasScale(Canvas canvas)
    {
        if (canvas == null) return;

        canvas.pixelPerfect = false;

        CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
        if (scaler == null)
            scaler = canvas.gameObject.AddComponent<CanvasScaler>();

        ConfigureScaler(scaler);
    }

    public static void ConfigureScaler(CanvasScaler scaler)
    {
        if (scaler == null) return;

        float uiScale = GetUIScale();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(2560f / uiScale, 1440f / uiScale);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
    }

    public static Canvas FindCanvas()
    {
        GameObject named = GameObject.Find(CanvasName);
        if (named != null && named.TryGetComponent(out Canvas namedCanvas))
            return namedCanvas;

        Canvas[] canvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include);
        for (int i = 0; i < canvases.Length; i++)
        {
            Canvas canvas = canvases[i];
            if (canvas == null) continue;
            if (canvas.gameObject.name == "ArenaTerminalCanvas") continue;
            if (canvas.gameObject.name == "RuntimeKeypadCanvas") continue;
            if (canvas.gameObject.name == "RuntimeSwitchCanvas") continue;
            if (!canvas.isRootCanvas) continue;
            return canvas;
        }

        return null;
    }

    public static void EnsureEventSystem()
    {
        if (Object.FindAnyObjectByType<EventSystem>() != null) return;

        GameObject eventSystemGo = new GameObject("EventSystem");
        eventSystemGo.AddComponent<EventSystem>();
        eventSystemGo.AddComponent<InputSystemUIInputModule>();
    }

    public static void BringToFront(Transform target)
    {
        if (target == null) return;
        target.SetAsLastSibling();
    }

    public static void ApplyDefaultFont(TMP_Text text)
    {
        if (text == null || text.font != null)
            return;

        TMP_FontAsset font = GetDefaultFont();
        if (font != null)
            text.font = font;
    }

    private static TMP_FontAsset GetDefaultFont()
    {
        if (cachedDefaultFont != null)
            return cachedDefaultFont;

        cachedDefaultFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
        if (cachedDefaultFont == null)
            cachedDefaultFont = TMP_Settings.defaultFontAsset;

        return cachedDefaultFont;
    }
}
