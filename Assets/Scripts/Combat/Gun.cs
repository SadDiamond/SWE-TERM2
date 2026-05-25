using UnityEngine;
using UnityEngine.InputSystem;

public class Gun : MonoBehaviour
{
    [Header("Gun Stats")]
    public float fireRate = 0.15f; // Time between shots. Lower = faster.
    public float bulletSpeed = 50f;

    [Header("References")]
    public Transform gunBarrel; // Where the raycast starts
    public GameObject bulletPrefab;
    public ParticleSystem muzzleFlash;

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip shootSound;

    [Header("Sway Settings")]
    public float swaySmooth = 8f;
    public float swayMultiplier = 2f;
    public float maxSwayAmount = 5f;

    [Header("Recoil Settings")]
    public float recoilForce = 5f;
    public float recoilRecoverySpeed = 10f;
    
    // Optional: We can hook this up to a PlayerController later to prevent shooting if UI is open
    private PlayerController player;
    private float nextTimeToFire = 0f;
    private Quaternion initialLocalRotation;
    private Vector3 initialLocalPosition;
    private Vector3 currentRecoilPosition;

    void Start()
    {
        player = GetComponentInParent<PlayerController>();
        if (gunBarrel == null) 
        {
            // Fallback to exactly where the camera is if a barrel isn't assigned
            gunBarrel = Camera.main.transform; 
        }
        initialLocalRotation = transform.localRotation;
        initialLocalPosition = transform.localPosition;
        currentRecoilPosition = initialLocalPosition;
    }

    void Update()
    {
        HandleSwayAndRecoil();

        // Don't fire if the shop or a puzzle UI is open
        if (player != null && player.isUIActive) return;

        // Using Left Mouse Button to shoot (Manual / Semi-Auto)
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            if (Time.time >= nextTimeToFire)
            {
                nextTimeToFire = Time.time + fireRate;
                Shoot();
            }
        }
    }

    void HandleSwayAndRecoil()
    {
        // Recover recoil back to origin (returns to Z=0 relative to start)
        currentRecoilPosition = Vector3.Lerp(currentRecoilPosition, initialLocalPosition, Time.deltaTime * recoilRecoverySpeed);
        transform.localPosition = currentRecoilPosition;

        if (Mouse.current == null || (player != null && player.isUIActive)) return;

        Vector2 mouseDelta = Mouse.current.delta.ReadValue();

        float swayY = Mathf.Clamp(mouseDelta.x * swayMultiplier, -maxSwayAmount, maxSwayAmount);
        float swayX = Mathf.Clamp(-mouseDelta.y * swayMultiplier, -maxSwayAmount, maxSwayAmount);

        Quaternion targetRotation = Quaternion.Euler(swayX, swayY, 0f) * initialLocalRotation;
        transform.localRotation = Quaternion.Slerp(transform.localRotation, targetRotation, swaySmooth * Time.deltaTime);
    }

    void Shoot()
    {
        // Add Recoil Kick (push gun backwards on the local Z axis)
        currentRecoilPosition -= new Vector3(0, 0, recoilForce * 0.1f);

        // 1. Visual/Audio Flair
        if (muzzleFlash != null)
        {
            muzzleFlash.Play();
        }
        if (audioSource != null && shootSound != null)
        {
            audioSource.PlayOneShot(shootSound);
        }

        // Raycast from the center of the camera to find exactly what the crosshair is looking at
        Camera mainCam = Camera.main;
        Vector3 targetPoint;
        
        if (mainCam != null)
        {
            Ray ray = new Ray(mainCam.transform.position, mainCam.transform.forward);
            if (Physics.Raycast(ray, out RaycastHit hit, 1000f))
            {
                targetPoint = hit.point;
            }
            else
            {
                targetPoint = ray.GetPoint(100f); // Default to a point far in the distance
            }
        }
        else
        {
            targetPoint = gunBarrel.position + gunBarrel.forward * 100f; // Fallback
        }

        // Calculate the exact angle from the barrel to the crosshair's target
        Vector3 shootDirection = (targetPoint - gunBarrel.position).normalized;

        // 2. Spawn and fire physical bullet
        if (bulletPrefab != null && gunBarrel != null)
        {
            GameObject bullet = Instantiate(bulletPrefab, gunBarrel.position, Quaternion.LookRotation(shootDirection));
            
            // Give ownership so you don't shoot yourself if you walk into the bullet
            Projectile p = bullet.GetComponent<Projectile>();
            if (p != null) p.owner = player != null ? player.gameObject : gameObject;

            Rigidbody rb = bullet.GetComponent<Rigidbody>();
            if (rb != null)
            {
#pragma warning disable 0618
                rb.velocity = shootDirection * bulletSpeed;
#pragma warning restore 0618
            }
        }
    }
}
