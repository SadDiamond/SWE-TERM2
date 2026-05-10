using UnityEngine;

public class Keycard : CollectibleItem
{
    [Header("Keycard Properties")]
    public int accessLevel = 1;

    protected override void Start()
    {
        base.Start(); // Let the base class set up the outline generator
        promptMessage = $"Press E to pick up {itemName}";
    }

    public override void OnInteract(PlayerController player)
    {
        if (!isCollected)
        {
            isCollected = true;
            player.PickUp(this);
            Debug.Log($"[Keycard] Picked up {itemName} with Access Level {accessLevel}");
            
            // Turn off the object so it disappears from the world
            gameObject.SetActive(false);
        }
    }
}
