using UnityEngine;

public class DeathTriggerParticleDistanceFade : MonoBehaviour
{
    [Header("References")]
    public ParticleSystem particleSystemRef;   // Particle system for this death trigger
    public SpaceShooterController player;
    public GameObject invisibleFollower;       // Follower object to track trigger edge

    [Header("Distance Fade")]
    public float showDistance = 10f;

    [Header("Follower Constraints")]
    public bool constrainDepth = true;  // Enable depth constraint
    public bool topConstraint = true;
    public float constrainPositionY;

    public Vector3 initialFollowerPosition;
    float disableTimer = -1f;   // -1 = not counting

    public float distance = 0f;

    // FIX CONSTRAINT TO BE SINGLE
    void Awake()
    {
        if (!player)
            player = FindObjectOfType<SpaceShooterController>();

        if (!particleSystemRef)
            particleSystemRef = GetComponentInChildren<ParticleSystem>();

        // Initially disable the particle system GameObject
        if (particleSystemRef != null)
            particleSystemRef.gameObject.SetActive(false);

        if (invisibleFollower)
            initialFollowerPosition = invisibleFollower.transform.position;

        SetParticleShapeY(constrainPositionY);
    }

    void FixedUpdate()
    {
        if (!player || !invisibleFollower || !particleSystemRef)
            return;

        UpdateFollowerPosition();
        AttachParticleSystemToPlayer();
        UpdateFade();
    }

    void UpdateFollowerPosition()
    {
        Vector3 toPlayer = player.transform.position - invisibleFollower.transform.position;

        // Move freely along X/Z
        Vector3 move = new Vector3(toPlayer.x, 0f, toPlayer.z);

        // Depth constraint along Y
        float moveY = toPlayer.y;
        float newY = invisibleFollower.transform.position.y + moveY;

        if (constrainDepth)
        {
            if (topConstraint)
            {
                moveY = Mathf.Min(moveY, initialFollowerPosition.y - invisibleFollower.transform.position.y);
            }
            else
            {
                moveY = Mathf.Max(moveY, initialFollowerPosition.y - invisibleFollower.transform.position.y);
            }
        }

        move.y = moveY;
        invisibleFollower.transform.position += move;
    }

    void AttachParticleSystemToPlayer()
    {
        if (!player) return;

        // Move particle system to the player's position (similar to AttachHitbox)
        particleSystemRef.transform.position = player.transform.position;
    }

    void SetParticleShapeY(float y)
    {
        var shape = particleSystemRef.shape;
        shape.position = new Vector3(shape.position.x, y, shape.position.z);
    }

    void UpdateFade()
    {
        distance = Vector3.Distance(player.transform.position, invisibleFollower.transform.position);

        float newAlpha = Mathf.Clamp01(
            1f - ((distance * 1.5f - showDistance) / showDistance)
        );

        var main = particleSystemRef.main;
        float currentAlpha = main.startColor.color.a;

        // --- ENABLE IF NEEDED ---
        if (newAlpha > 0f && !particleSystemRef.gameObject.activeSelf)
        {
            particleSystemRef.gameObject.SetActive(true);
            disableTimer = -1f;   // cancel disable
        }

        // --- APPLY ALPHA IF ACTIVE ---
        if (particleSystemRef.gameObject.activeSelf)
        {
            Color c = main.startColor.color;
            c.a = newAlpha;
            main.startColor = c;
        }

        // --- HANDLE DISABLE DELAY ---
        if (newAlpha <= 0f)
        {
            // start countdown if not already
            if (disableTimer < 0f)
                disableTimer = main.startLifetime.constantMax;

            disableTimer -= Time.fixedDeltaTime;

            if (disableTimer <= 0f && particleSystemRef.gameObject.activeSelf)
                particleSystemRef.gameObject.SetActive(false);
        }
        else
        {
            // visible again → cancel countdown
            disableTimer = -1f;
        }
    }
}
