using UnityEngine;

public abstract class Interactable : MonoBehaviour
{
    [Header("Interaction")]
    public string promptMessage;        
    public float interactionRange = 3f;

    [Header("Visual Feedback (Optional)")]
    public Renderer highlightRenderer; // Drag the 3D model (MeshRenderer) you want to outline here
    public Material outlineMaterial; // Drag your Custom Outline Material here

    private Material[] originalMaterials;
    private Material[] hoverMaterials;

    protected virtual void Start()
    {
        // If the user setup both a renderer and a material, we cache the arrays!
        if (highlightRenderer != null && outlineMaterial != null)
        {
            originalMaterials = highlightRenderer.materials;
            
            // Create a new array that is exactly 1 slot larger to hold the outline material
            hoverMaterials = new Material[originalMaterials.Length + 1];
            for (int i = 0; i < originalMaterials.Length; i++)
            {
                hoverMaterials[i] = originalMaterials[i];
            }
            hoverMaterials[hoverMaterials.Length - 1] = outlineMaterial; // Put outline at the end!
        }
    }

    // Abstract method — every child class MUST implement this
    // We pass the PlayerController so interactable objects know WHO is interacting with them
    public abstract void OnInteract(PlayerController player);

    // Called when player looks at the object
    public virtual void OnFocus()
    {
        Debug.Log($"[Interactable] OnFocus on {gameObject.name}");
        if (highlightRenderer != null && outlineMaterial != null)
        {
            highlightRenderer.materials = hoverMaterials; // Append the outline shader
        }
    }

    public virtual void OnLoseFocus()
    {
        Debug.Log($"[Interactable] OnLoseFocus on {gameObject.name}");
        if (highlightRenderer != null && outlineMaterial != null)
        {
            highlightRenderer.materials = originalMaterials; // Remove the outline shader
        }
    }
}