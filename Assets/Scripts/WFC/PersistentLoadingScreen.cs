using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class PersistentLoadingScreen : MonoBehaviour
{
    private static PersistentLoadingScreen instance;

    private Canvas canvas;
    private GameObject overlayRoot;
    private RubikCubeLoader cubeLoader;
    private Coroutine activeRoutine;
    private bool loadInProgress;

    public static bool IsActive => instance != null && instance.loadInProgress;

    public static void LoadArenaFromMenu(bool sandbox, bool heroArena)
    {
        if (instance == null)
        {
            GameObject go = new GameObject("PersistentLoadingScreen");
            instance = go.AddComponent<PersistentLoadingScreen>();
            DontDestroyOnLoad(go);
            instance.Build();
        }

        if (instance.activeRoutine != null)
            instance.StopCoroutine(instance.activeRoutine);

        instance.activeRoutine = instance.StartCoroutine(instance.LoadArenaRoutine(sandbox, heroArena));
    }

    private void Build()
    {
        GameObject canvasObject = new GameObject("PersistentLoadingCanvas");
        canvasObject.transform.SetParent(transform, false);
        canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.pixelPerfect = false;
        canvas.sortingOrder = 32760;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        canvasObject.AddComponent<GraphicRaycaster>();

        overlayRoot = new GameObject("Overlay");
        overlayRoot.transform.SetParent(canvas.transform, false);
        RectTransform overlayRect = overlayRoot.AddComponent<RectTransform>();
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = Vector2.zero;
        Image overlayImage = overlayRoot.AddComponent<Image>();
        overlayImage.color = Color.black;

        GameObject cubeObject = new GameObject("RubikCubePreview");
        cubeObject.transform.SetParent(overlayRoot.transform, false);
        RectTransform cubeRect = cubeObject.AddComponent<RectTransform>();
        cubeRect.anchorMin = cubeRect.anchorMax = new Vector2(1f, 0f);
        cubeRect.pivot = new Vector2(1f, 0f);
        cubeRect.anchoredPosition = new Vector2(-36f, 36f);
        cubeRect.sizeDelta = new Vector2(176f, 176f);

        cubeLoader = cubeObject.AddComponent<RubikCubeLoader>();
        cubeLoader.Configure("PersistentLoadingCubeTexture", "PersistentLoadingCubeCamera", "PersistentLoadingRubikCube");
        cubeLoader.SetVisible(true);
        overlayRoot.SetActive(false);
    }

    private IEnumerator LoadArenaRoutine(bool sandbox, bool heroArena)
    {
        loadInProgress = true;
        overlayRoot.SetActive(true);
        cubeLoader.SetVisible(true);
        StartMenuController.SetLaunchFlags(true, sandbox, heroArena);

        Coroutine cubeRoutine = StartCoroutine(cubeLoader.PlayLoopingSolveAndSpin());
        AsyncOperation sceneLoad = SceneManager.LoadSceneAsync("Arena");
        while (!sceneLoad.isDone)
            yield return null;

        while (!IsArenaLoadReady(sandbox, heroArena))
            yield return null;

        yield return new WaitForEndOfFrame();
        yield return new WaitForEndOfFrame();

        if (cubeRoutine != null)
            StopCoroutine(cubeRoutine);

        cubeLoader.SetVisible(false);
        overlayRoot.SetActive(false);
        loadInProgress = false;
        activeRoutine = null;
    }

    private static bool IsArenaLoadReady(bool sandbox, bool heroArena)
    {
        if (heroArena)
            return GameObject.Find("_HeroArena") != null;

        if (sandbox)
            return GameObject.Find("_SandboxArena") != null;

        CybergrindArenaGenerator generator = FindAnyObjectByType<CybergrindArenaGenerator>();
        return generator != null && !generator.IsGenerating && generator.CurrentArenaRoot != null;
    }
}
