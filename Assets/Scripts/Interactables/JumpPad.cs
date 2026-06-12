using UnityEngine;

public class JumpPad : MonoBehaviour
{
    [Header("Launch Settings")]
    [Tooltip("How many units high this pad will launch the player")]
    public float launchHeight = 12f;
    [Tooltip("How much forward speed to apply (0 for straight up)")]
    public float forwardMomentumBoost = 10f; 

    // Visual flair (Optional)
    public ParticleSystem launchFX;
    public AudioSource audioSource;
    public AudioClip bounceSound;

    private void OnTriggerEnter(Collider other)
    {
        // Try to find the PlayerController on whatever stepped on the pad
        PlayerController player = other.GetComponentInParent<PlayerController>();
        
        if (player != null)
        {
            // Play FX
            if (launchFX != null) launchFX.Play();
            if (audioSource != null && bounceSound != null) audioSource.PlayOneShot(bounceSound);

            // Calculate the exact upward velocity needed to reach 'launchHeight' given the player's gravity (-25f).
            // Formula: sqrt(height * -2 * gravity)
            float exactVelY = Mathf.Sqrt(launchHeight * -2f * player.gravity);

            // Calculate launch direction - usually jump pads launch relative to the pad's rotation!
            Vector3 launchVel = (transform.up * exactVelY) + (transform.forward * forwardMomentumBoost);
            player.LaunchPlayer(launchVel);
        }
    }
}
