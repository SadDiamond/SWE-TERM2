using UnityEngine;

public abstract class Interactable : MonoBehaviour
{
    [Header("Interaction")]
    public string promptMessage;        
    public float interactionRange = 3f;

    // Abstract method — every child class MUST implement this
    public abstract void OnInteract();

    // Called when player looks at the object
    public virtual void OnFocus()
    {
        Debug.Log("Looking at: " + promptMessage);
    }

    // Called when player looks away
    public virtual void OnLoseFocus()
    {
        Debug.Log("Stopped looking at: " + promptMessage);
    }
}