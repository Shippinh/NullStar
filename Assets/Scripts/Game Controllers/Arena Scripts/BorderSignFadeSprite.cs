using UnityEngine;

public class BorderSignFadeSprite : MonoBehaviour
{
    public SpaceShooterController player;

    [Header("Distance Fade")]
    public float showDistance = 10f;
    public float fadeSpeed = 4f;

    [Header("Scale To Screen Size")]
    public float scaleFactor = 0.05f;
    public float scaleDistanceMin = 100f;

    private SpriteRenderer spriteRendererRef;
    private float currentAlpha = 0f;
    private Color overrideColor;

    public float distance = 0f;
    public float scale = 1f;
    public float followSpeed = 2f;

    private Transform spriteTransform;
    private Vector3 initialSpriteScale;

    void Awake()
    {
        spriteRendererRef = GetComponentInChildren<SpriteRenderer>();
        spriteTransform = spriteRendererRef.transform;

        overrideColor = spriteRendererRef.color;
        overrideColor.a = 0f;
        spriteRendererRef.color = overrideColor;

        initialSpriteScale = spriteTransform.localScale;

        if (!player)
            player = FindObjectOfType<SpaceShooterController>();
    }

    void LateUpdate()
    {
        if (!player)
            return;

        distance = Vector3.Distance(player.transform.position, spriteTransform.position);

        UpdateFade();
        UpdateScaleDistance();
        UpdatePosition();
    }

    void UpdateFade()
    {
        // Alpha is 1 at showDistance, fades toward 0 beyond showDistance
        float distanceFactor = Mathf.Clamp01(1f - ((distance * 1.5f - showDistance) / showDistance));

        currentAlpha = distanceFactor;

        overrideColor.a = currentAlpha;
        spriteRendererRef.color = overrideColor;
    }

    void UpdatePosition()
    {
        // Vector from sprite to player in world space
        Vector3 toPlayer = player.transform.position - spriteTransform.position;

        // Project the vector onto the sprite's local axes
        Vector3 right = spriteTransform.right; // local X
        Vector3 up = spriteTransform.up;       // local Y

        // Compute how much to move along each local axis
        float moveX = Vector3.Dot(toPlayer, right);
        float moveY = Vector3.Dot(toPlayer, up);

        // Compose local-axis movement
        Vector3 localMove = right * moveX + up * moveY;

        // Move sprite smoothly along its local axes
        spriteTransform.position = Vector3.Lerp(spriteTransform.position, spriteTransform.position + localMove, followSpeed * Time.deltaTime * 0.5f);
    }

    void UpdateScaleDistance()
    {
        if(distance > scaleDistanceMin)
            scale = distance * scaleFactor;

        spriteTransform.localScale = initialSpriteScale * scale;
    }
}
