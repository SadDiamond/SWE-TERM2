using UnityEngine;

/// <summary>
/// Manages the procedural room progression for Hermes Inc. runs.
/// Flow: Combat 1-5 -> Boss -> Shop -> repeat.
/// </summary>
public class RoomProgressionManager : MonoBehaviour
{
    [Header("Generation")]
    public WFCGenerator3D wfcGenerator;
    public HangarGenerator hangarGenerator;

    [Header("Progression State")]
    public int currentRoomIndex = 0;
    public int roomsPerTheme = 5;

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

        int cycleLength = roomsPerTheme + 2; // Rooms + Boss + Shop
        int position = currentRoomIndex % cycleLength;

        if (position < roomsPerTheme)
        {
            hangarGenerator.roomType = HangarGenerator.RoomType.Combat;
        }
        else if (position == roomsPerTheme)
        {
            hangarGenerator.roomType = HangarGenerator.RoomType.Boss;
        }
        else
        {
            hangarGenerator.roomType = HangarGenerator.RoomType.Shop;
        }
    }
}

