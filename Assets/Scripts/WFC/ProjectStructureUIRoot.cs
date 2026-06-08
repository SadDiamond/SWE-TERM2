using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

public static class ProjectStructureUIRoot
{
    public const string CanvasName = "ProjectStructureCanvas";

    public static Canvas GetOrCreateCanvas()
    {
        Canvas canvas = FindCanvas();
        if (canvas != null) return canvas;

        GameObject canvasGo = new GameObject(CanvasName);
        canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.pixelPerfect = false;
        canvas.sortingOrder = 0;

        CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        canvasGo.AddComponent<GraphicRaycaster>();
        EnsureEventSystem();
        return canvas;
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
}
