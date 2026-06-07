using UnityEngine;

/// <summary>
/// Manages the procedural room progression for Hermes Inc. runs.
/// Flow per cycle: Combat 1-5 (same theme) -> Shop -> Boss -> next theme.
/// </summary>
public class RoomProgressionManager : MonoBehaviour
{
    [Header("Generation")]
    public WFCGenerator3D wfcGenerator;
    public HangarGenerator hangarGenerator;

    [Header("Progression State")]
    public int currentRoomIndex = 0;
    public int roomsPerTheme = 5;
    public int currentThemeIndex = 0;

    public enum ProgressionPhase
    {
        Combat,
        Shop,
        Boss
    }

    public ProgressionPhase currentPhase { get; private set; } = ProgressionPhase.Combat;

    void Start()
    {
        if (wfcGenerator == null)
            wfcGenerator = GetComponent<WFCGenerator3D>();
        if (hangarGenerator == null)
            hangarGenerator = GetComponent<HangarGenerator>();
    }

    public void GenerateNextRoom()
    {
        DetermineRoomType();
        
        if (wfcGenerator != null)
        {
            // The WFC generator will use the assigned hangarGenerator (MacroGenerator)
            wfcGenerator.GenerateLevel();
        }

        currentRoomIndex++;
    }

    private void DetermineRoomType()
    {
        if (hangarGenerator == null) return;

        int cycleLength = roomsPerTheme + 2; // Rooms + Shop + Boss
        int position = currentRoomIndex % cycleLength;
        currentThemeIndex = currentRoomIndex / cycleLength;

        if (position < roomsPerTheme)
        {
            hangarGenerator.roomType = HangarGenerator.RoomType.Combat;
            currentPhase = ProgressionPhase.Combat;
        }
        else if (position == roomsPerTheme)
        {
            hangarGenerator.roomType = HangarGenerator.RoomType.Shop;
            currentPhase = ProgressionPhase.Shop;
        }
        else
        {
            hangarGenerator.roomType = HangarGenerator.RoomType.Boss;
            currentPhase = ProgressionPhase.Boss;
        }
    }
}

