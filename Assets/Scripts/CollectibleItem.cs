using UnityEngine;

public abstract class CollectibleItem : Interactable
{
    [Header("Item Info")]
    public string itemName;
    public string itemDescription;
    public bool isCollected = false;
}
