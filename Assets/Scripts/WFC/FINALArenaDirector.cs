using UnityEngine;

public class CybergrindArenaDirector : MonoBehaviour
{
    public CybergrindArenaGenerator generator;
    public int floor = 1;
    public float transitionDelay = 1.2f;
    public float exitActivationRange = 5f;
    public Transform player;

    private float transitionTimer = -1f;

    private void Start()
    {
        if (generator == null) generator = GetComponent<CybergrindArenaGenerator>();
        if (player == null)
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
            if (playerObject != null) player = playerObject.transform;
        }

        ApplyFloorMode();
    }

    private void Update()
    {
        if (generator == null) return;

        if (transitionTimer > 0f)
        {
            transitionTimer -= Time.deltaTime;
            if (transitionTimer <= 0f)
                AdvanceFloor();
            return;
        }

        if (!AreAllTerminalsSolved()) return;
        if (!IsPlayerAtExit()) return;

        transitionTimer = transitionDelay;
    }

    private bool AreAllTerminalsSolved()
    {
        Terminal[] terminals = FindObjectsByType<Terminal>();
        bool foundAny = false;
        for (int i = 0; i < terminals.Length; i++)
        {
            if (terminals[i] == null || !terminals[i].name.StartsWith("PuzzleTerminal")) continue;
            foundAny = true;
            if (!terminals[i].isSolved) return false;
        }

        return foundAny;
    }

    private bool IsPlayerAtExit()
    {
        if (player == null)
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
            if (playerObject != null) player = playerObject.transform;
        }

        if (player == null) return false;

        GameObject exit = GameObject.Find("Exit_" + (generator.width / 2) + "_" + (generator.length - 3));
        if (exit == null) return false;

        return Vector3.Distance(player.position, exit.transform.position) <= exitActivationRange;
    }

    private void AdvanceFloor()
    {
        floor++;
        ApplyFloorMode();
        generator.GenerateArena();
    }

    private void ApplyFloorMode()
    {
        if (generator == null) return;

        if (floor % 6 == 0)
            generator.arenaMode = CybergrindArenaGenerator.ArenaMode.Boss;
        else if (floor % 5 == 0)
            generator.arenaMode = CybergrindArenaGenerator.ArenaMode.Shop;
        else
            generator.arenaMode = CybergrindArenaGenerator.ArenaMode.Combat;
    }
}
