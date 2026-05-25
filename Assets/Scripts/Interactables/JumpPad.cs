using UnityEngine;

public class JumpPad : MonoBehaviour
{
    [Header("Launch Settings")]
    public float launchForce = 35f;
    public float forwardMomentumBoost = 15f; 

    // Visual flair (Optional)
    public ParticleSystem launchFX;
    public AudioSource audioSource;
    public AudioClip bounceSound;

    private void OnTriggerEnter(Collider other)
    {
        // Try to find the PlayerController on whatever stepped on the pad
        PlayerController player = other.GetComponent<PlayerController>();
        
        if (player != null)
        {
            // Play FX
            if (launchFX != null) launchFX.Play();
            if (audioSource != null && bounceSound != null) audioSource.PlayOneShot(bounceSound);

            // Access the CharacterController to apply a massive upward force!
            // Wait, we need a public method on the PlayerController to accept external momentum.
            player.LaunchPlayer(Vector3.up * launchForce + player.transform.forward * forwardMomentumBoost);
        }
    }
}
