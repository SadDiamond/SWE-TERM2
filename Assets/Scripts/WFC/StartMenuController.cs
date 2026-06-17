using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class StartMenuController : MonoBehaviour
{
    public static bool LaunchingArena { get; private set; }
    public static bool LaunchingSandbox { get; private set; }

    private void Start()
    {
        BuildCamera();
        BuildMenu();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void Update()
    {
        if (UnityEngine.InputSystem.Keyboard.current == null) return;
        if (UnityEngine.InputSystem.Keyboard.current.sKey.wasPressedThisFrame)
        {
            LaunchingSandbox = true;
            LaunchingArena = true;
            SceneManager.LoadScene("Arena");
        }
        else if (UnityEngine.InputSystem.Keyboard.current.enterKey.wasPressedThisFrame ||
            UnityEngine.InputSystem.Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            LaunchingArena = true;
            SceneManager.LoadScene("Arena");
        }
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

    private void BuildCamera()
    {
        if (Camera.main != null) return;
        GameObject cameraObject = new GameObject("MenuCamera");
        cameraObject.tag = "MainCamera";
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.006f, 0.01f, 0.014f);
    }

    private void BuildMenu()
    {
        Canvas canvas = ProjectStructureUIRoot.GetOrCreateCanvas();
        GameObject root = new GameObject("StartMenu");
        root.transform.SetParent(canvas.transform, false);
        RectTransform rootRect = root.AddComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;
        Image background = root.AddComponent<Image>();
        background.color = new Color(0.008f, 0.014f, 0.019f, 1f);

        CreateBlock(root.transform, "Rail", new Vector2(0.075f, 0.55f), new Vector2(5f, 430f), new Color(0.18f, 0.88f, 1f));
        CreateBlock(root.transform, "Horizon", new Vector2(0.31f, 0.77f), new Vector2(590f, 2f), new Color(0.18f, 0.88f, 1f, 0.5f));
        TMP_Text title = CreateText(root.transform, "PROJECT\nSTRUCTURE", 64f, new Vector2(0.1f, 0.66f), new Vector2(820f, 170f), TextAlignmentOptions.Left, Color.white);
        TMP_Text mode = CreateText(root.transform, "MOMENTUM / COMBAT / DESCENT", 14f, new Vector2(0.1f, 0.52f), new Vector2(600f, 30f), TextAlignmentOptions.Left, new Color(0.38f, 0.86f, 0.96f));
        TMP_Text objective = CreateText(root.transform, "CLEAR THE SECTOR. BUILD SPEED. REACH THE CORE.", 18f, new Vector2(0.1f, 0.39f), new Vector2(680f, 42f), TextAlignmentOptions.Left, new Color(0.82f, 0.86f, 0.88f));
        CreateBlock(root.transform, "StartPlate", new Vector2(0.21f, 0.27f), new Vector2(300f, 44f), new Color(0.025f, 0.055f, 0.068f, 1f));
        TMP_Text start = CreateText(root.transform, "ENTER  START RUN     S  WEAPON LAB", 16f, new Vector2(0.1f, 0.27f), new Vector2(520f, 30f), TextAlignmentOptions.Center, Color.white);
        TMP_Text footer = CreateText(root.transform, "WASD MOVE   SHIFT DASH   CTRL SLIDE   SPACE JUMP   F MELEE", 11f, new Vector2(0.1f, 0.08f), new Vector2(760f, 28f), TextAlignmentOptions.Left, new Color(0.55f, 0.62f, 0.66f));
    }

    private TMP_Text CreateText(Transform parent, string value, float size, Vector2 anchor, Vector2 bounds, TextAlignmentOptions alignment, Color color)
    {
        GameObject go = new GameObject("MenuText");
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
        return text;
    }

    private void CreateBlock(Transform parent, string name, Vector2 anchor, Vector2 size, Color color)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        RectTransform rect = go.AddComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = anchor;
        rect.sizeDelta = size;
        Image image = go.AddComponent<Image>();
        image.color = color;
    }
}
