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
        if (isCollected) return;

        isCollected = true;
        player.PickUp(this);
        gameObject.SetActive(false);
    }
}
