using UnityEngine;

public class CybergrindRunState : MonoBehaviour
{
    public const int StartingWeaponPreset = 0;
    public const int WeaponFamilySize = 3;
    public static CybergrindRunState Instance { get; private set; }

    [Header("Run")]
    public int bossesClearedThisRun;
    public bool shotgunUnlockedThisRun;
    public bool heavyUnlockedThisRun;
    public int currentRunSeed;
    public int currentFloorSeed;
    public int floorsClearedThisRun;
    public int enemiesDefeatedThisRun;
    public int terminalsSolvedThisRun;
    public int shopInteractionsThisRun;
    public float damageTakenThisRun;
    public float runStartRealtime;

    [Header("Persistence")]
    public bool persistBossUnlocks = false;
    public int maxTrackedWeaponPresets = 9;
    [SerializeField] private bool[] unlockedWeaponPresetsThisRun;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        EnsureStartingWeaponUnlocked();
        EnsureWeaponPresetTracking();
        EnsureRunSeed();
        if (runStartRealtime <= 0f)
            runStartRealtime = Time.realtimeSinceStartup;
    }

    public static CybergrindRunState GetOrCreate()
    {
        if (Instance != null) return Instance;

        CybergrindRunState existing = FindAnyObjectByType<CybergrindRunState>();
        if (existing != null) return existing;

        GameObject go = new GameObject("_ArenaRunState");
        return go.AddComponent<CybergrindRunState>();
    }

    public bool IsWeaponUnlocked(int presetIndex)
    {
        EnsureWeaponPresetTracking();
        if (presetIndex < 0 || presetIndex >= unlockedWeaponPresetsThisRun.Length)
            return false;

        return unlockedWeaponPresetsThisRun[presetIndex];
    }

    public int RegisterBossDefeated(int themeIndex)
    {
        bossesClearedThisRun++;
        Debug.Log($"[ArenaRunState] Boss cleared. Core progress {bossesClearedThisRun}.");
        return -1;
    }

    public void RegisterFloorCleared()
    {
        floorsClearedThisRun++;
    }

    public void RegisterEnemyDefeated()
    {
        enemiesDefeatedThisRun++;
    }

    public void RegisterTerminalSolved()
    {
        terminalsSolvedThisRun++;
    }

    public void RegisterShopInteraction()
    {
        shopInteractionsThisRun++;
    }

    public void RegisterDamageTaken(float amount)
    {
        if (amount <= 0f) return;
        damageTakenThisRun += amount;
    }

    public void ResetRunStats()
    {
        bossesClearedThisRun = 0;
        shotgunUnlockedThisRun = false;
        heavyUnlockedThisRun = false;
        floorsClearedThisRun = 0;
        enemiesDefeatedThisRun = 0;
        terminalsSolvedThisRun = 0;
        shopInteractionsThisRun = 0;
        damageTakenThisRun = 0f;
        runStartRealtime = Time.realtimeSinceStartup;
        EnsureStartingWeaponUnlocked();
    }

    public float GetRunDurationSeconds()
    {
        if (runStartRealtime <= 0f) return 0f;
        return Mathf.Max(0f, Time.realtimeSinceStartup - runStartRealtime);
    }

    public void UnlockWeapon(int presetIndex)
    {
        if (presetIndex < 0) return;

        EnsureWeaponPresetTracking();
        if (presetIndex >= unlockedWeaponPresetsThisRun.Length)
            return;

        unlockedWeaponPresetsThisRun[presetIndex] = true;
        if (presetIndex >= WeaponFamilySize * 2)
            heavyUnlockedThisRun = true;
        else if (presetIndex >= WeaponFamilySize)
            shotgunUnlockedThisRun = true;
    }

    public int GetFirstUnlockedPreset()
    {
        return StartingWeaponPreset;
    }

    public int CountUnlockedWeapons()
    {
        EnsureWeaponPresetTracking();
        int count = 0;
        for (int i = 0; i < unlockedWeaponPresetsThisRun.Length; i++)
        {
            if (unlockedWeaponPresetsThisRun[i])
                count++;
        }

        return Mathf.Max(1, count);
    }

    public bool IsFamilyUnlocked(Gun.WeaponFamily family)
    {
        return family switch
        {
            Gun.WeaponFamily.Pistol => true,
            Gun.WeaponFamily.Shotgun => shotgunUnlockedThisRun,
            Gun.WeaponFamily.Heavy => heavyUnlockedThisRun,
            _ => false
        };
    }

    private void EnsureStartingWeaponUnlocked()
    {
        EnsureWeaponPresetTracking();
        shotgunUnlockedThisRun = false;
        heavyUnlockedThisRun = false;

        for (int i = 0; i < unlockedWeaponPresetsThisRun.Length; i++)
            unlockedWeaponPresetsThisRun[i] = false;

        if (StartingWeaponPreset >= 0 && StartingWeaponPreset < unlockedWeaponPresetsThisRun.Length)
            unlockedWeaponPresetsThisRun[StartingWeaponPreset] = true;
    }

    private void EnsureWeaponPresetTracking()
    {
        int size = Mathf.Max(1, maxTrackedWeaponPresets);
        if (unlockedWeaponPresetsThisRun != null && unlockedWeaponPresetsThisRun.Length == size)
            return;

        bool[] previous = unlockedWeaponPresetsThisRun;
        unlockedWeaponPresetsThisRun = new bool[size];
        if (previous != null)
        {
            int copyCount = Mathf.Min(previous.Length, unlockedWeaponPresetsThisRun.Length);
            for (int i = 0; i < copyCount; i++)
                unlockedWeaponPresetsThisRun[i] = previous[i];
        }

        if (StartingWeaponPreset >= 0 && StartingWeaponPreset < unlockedWeaponPresetsThisRun.Length)
            unlockedWeaponPresetsThisRun[StartingWeaponPreset] = true;
    }

    private void EnsureRunSeed()
    {
        if (currentRunSeed != 0) return;

        unchecked
        {
            int sessionSeed = System.Environment.TickCount;
            sessionSeed ^= (int)System.DateTime.UtcNow.Ticks;
            sessionSeed ^= Random.Range(int.MinValue, int.MaxValue);
            currentRunSeed = sessionSeed;
        }
    }

    public void SetRunSeed(int seed)
    {
        currentRunSeed = seed;
    }

    public int GetFloorSeed(int floor, int themeIndex)
    {
        unchecked
        {
            int hash = currentRunSeed;
            hash = (hash * 397) ^ floor;
            hash = (hash * 397) ^ themeIndex;
            hash = (hash * 397) ^ bossesClearedThisRun;
            currentFloorSeed = hash;
            return hash;
        }
    }
}
