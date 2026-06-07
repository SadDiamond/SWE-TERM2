using UnityEngine;
using UnityEngine.InputSystem;

public class CybergrindDebugMode : MonoBehaviour
{
    public bool debugEnabled;
    public bool showOverlay = true;
    public bool invulnerable;
    public int coinsPerGrant = 10;

    private PlayerController player;
    private CybergrindArenaDirector director;
    private CybergrindArenaGenerator generator;

    private void Awake()
    {
        RefreshReferences();
    }

    private void Update()
    {
        if (Keyboard.current == null) return;

        if (Keyboard.current.f3Key.wasPressedThisFrame)
            debugEnabled = !debugEnabled;

        if (!debugEnabled) return;

        RefreshReferences();

        if (Keyboard.current.f4Key.wasPressedThisFrame)
            invulnerable = !invulnerable;

        if (Keyboard.current.f5Key.wasPressedThisFrame && player != null)
            player.AddCurrency(coinsPerGrant);

        if (Keyboard.current.f6Key.wasPressedThisFrame && director != null)
            director.ForceAdvanceFloor();

        if (Keyboard.current.f7Key.wasPressedThisFrame && generator != null)
            generator.GenerateArena();

        if (Keyboard.current.f8Key.wasPressedThisFrame)
            ClearCurrentEnemies();
    }

    private void LateUpdate()
    {
        if (!debugEnabled || !invulnerable || player == null) return;
        player.Heal(player.EffectiveMaxHealth);
    }

    private void RefreshReferences()
    {
        if (player == null)
            player = FindAnyObjectByType<PlayerController>();
        if (director == null)
            director = FindAnyObjectByType<CybergrindArenaDirector>();
        if (generator == null)
            generator = FindAnyObjectByType<CybergrindArenaGenerator>();
    }

    private void ClearCurrentEnemies()
    {
        Transform root = generator != null ? generator.CurrentArenaRoot : null;
        BasicEnemyAI[] enemies = root != null
            ? root.GetComponentsInChildren<BasicEnemyAI>(true)
            : FindObjectsByType<BasicEnemyAI>();

        for (int i = 0; i < enemies.Length; i++)
        {
            if (enemies[i] != null)
                Destroy(enemies[i].gameObject);
        }
    }

    private void OnGUI()
    {
        if (!debugEnabled || !showOverlay) return;

        GUI.color = Color.white;
        GUILayout.BeginArea(new Rect(12f, 12f, 360f, 180f), GUI.skin.box);
        GUILayout.Label("SYSTEM DEBUG");
        GUILayout.Label("F3 // Toggle channel");
        GUILayout.Label("F4 // Invulnerable: " + (invulnerable ? "ON" : "OFF"));
        GUILayout.Label("F5 // +" + coinsPerGrant + " coins");
        GUILayout.Label("F6 // Advance floor");
        GUILayout.Label("F7 // Rebuild arena");
        GUILayout.Label("F8 // Clear hostiles");

        if (player != null)
            GUILayout.Label("Hull: " + Mathf.CeilToInt(player.currentHealth) + "/" + Mathf.CeilToInt(player.EffectiveMaxHealth));
        if (director != null)
            GUILayout.Label("Route " + director.floor + " // " + director.CurrentThemeLabel);
        if (generator != null)
            GUILayout.Label("Mode " + generator.arenaMode + " // Seed " + generator.lastGeneratedSeed);

        GUILayout.EndArea();
    }
}
