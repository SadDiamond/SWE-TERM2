using System.Collections.Generic;
using UnityEngine;

// Concrete macro strategy: carves non-overlapping rectangular rooms into a wall-filled
// blueprint, then connects them with L-shaped corridors. Spawn drops in the first room
// carved, goal in the last — so the macro pass guarantees a top-level start→end intent
// before WFC fills in details.
public class RoomsAndCorridorsMacro : MacroGenerator
{
    [Header("Room Parameters")]
    public int minRoomSize = 3;
    public int maxRoomSize = 6;
    public int maxRooms = 5;
    public int maxPlacementAttempts = 30;

    public override MacroRegion[,] Generate(int width, int length, int seed)
    {
        var rng = new System.Random(seed);
        var map = new MacroRegion[width, length];

        // Start as solid wall everywhere; rooms and corridors are carved out.
        for (int x = 0; x < width; x++)
            for (int z = 0; z < length; z++)
                map[x, z] = MacroRegion.Wall;

        var rooms = new List<RectInt>();
        int attempts = 0;
        while (rooms.Count < maxRooms && attempts < maxPlacementAttempts)
        {
            attempts++;
            int rw = rng.Next(minRoomSize, maxRoomSize + 1);
            int rh = rng.Next(minRoomSize, maxRoomSize + 1);
            if (rw >= width - 2 || rh >= length - 2) continue;

            int rx = rng.Next(1, width - rw);
            int rz = rng.Next(1, length - rh);
            var candidate = new RectInt(rx, rz, rw, rh);

            // Reject if too close to an existing room (1-cell buffer).
            bool overlaps = false;
            foreach (var existing in rooms)
            {
                var padded = new RectInt(existing.x - 1, existing.y - 1,
                                          existing.width + 2, existing.height + 2);
                if (padded.Overlaps(candidate)) { overlaps = true; break; }
            }
            if (overlaps) continue;

            rooms.Add(candidate);
            for (int x = candidate.x; x < candidate.xMax; x++)
                for (int z = candidate.y; z < candidate.yMax; z++)
                    map[x, z] = MacroRegion.Open;
        }

        // Chain rooms with L-corridors so every room is reachable from spawn.
        for (int i = 1; i < rooms.Count; i++)
        {
            var a = RoomCenter(rooms[i - 1]);
            var b = RoomCenter(rooms[i]);
            CarveCorridor(map, a.x, a.y, b.x, b.y, rng);
        }

        // Tag spawn and goal cells inside the first and last rooms respectively.
        if (rooms.Count > 0)
        {
            var s = RoomCenter(rooms[0]);
            var g = RoomCenter(rooms[rooms.Count - 1]);
            map[s.x, s.y] = MacroRegion.Spawn;
            map[g.x, g.y] = MacroRegion.Goal;
        }

        return map;
    }

    private static Vector2Int RoomCenter(RectInt r)
        => new Vector2Int(r.x + r.width / 2, r.y + r.height / 2);

    private static void CarveCorridor(MacroRegion[,] map, int x1, int z1, int x2, int z2, System.Random rng)
    {
        // Randomly choose corridor shape: ⌐ or ⌐-flipped (avoids predictable layouts).
        if (rng.Next(2) == 0)
        {
            CarveHorizontal(map, x1, x2, z1);
            CarveVertical(map, z1, z2, x2);
        }
        else
        {
            CarveVertical(map, z1, z2, x1);
            CarveHorizontal(map, x1, x2, z2);
        }
    }

    private static void CarveHorizontal(MacroRegion[,] map, int x1, int x2, int z)
    {
        for (int x = Mathf.Min(x1, x2); x <= Mathf.Max(x1, x2); x++)
            if (map[x, z] == MacroRegion.Wall) map[x, z] = MacroRegion.Corridor;
    }

    private static void CarveVertical(MacroRegion[,] map, int z1, int z2, int x)
    {
        for (int z = Mathf.Min(z1, z2); z <= Mathf.Max(z1, z2); z++)
            if (map[x, z] == MacroRegion.Wall) map[x, z] = MacroRegion.Corridor;
    }
}
