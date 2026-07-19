using UnityEngine;
using UnityEngine.UI;

public class SpaceShooterFeedbackController : MonoBehaviour
{
    [Header("References")]
    public SpaceShooterController playerControllerRef;
    public Image damageFlashOverlay;
    public AudioSource hitAudioSource;
    public AudioClip[] hitSounds;

    [Header("Flash Colors")]
    public Color damageFlashColor = new Color(1f, 0f, 0f, 0.5f); // alpha here = max flash intensity
    public Color healFlashColor = new Color(0f, 1f, 0f, 0.5f);

    [Header("Damage Flash Settings")]
    [Range(0f, 1f)] public float damageFlashInDuration = 0.03f;
    public float damageFlashHoldDuration = 0.05f;
    public float damageFlashOutDuration = 0.25f;

    [Header("Heal Flash Settings")]
    [Range(0f, 1f)] public float healFlashInDuration = 0.05f;
    public float healFlashHoldDuration = 0.08f;
    public float healFlashOutDuration = 0.35f;

    [Header("Shake Settings")]
    public float hitShakeDuration = 0.15f;
    public float hitShakeMagnitude = 0.08f;

    [Header("Internals")]
    [SerializeField] private float flashTimer = 0f;
    [SerializeField] private enum FlashPhase { Idle, In, Hold, Out }
    [SerializeField] private FlashPhase flashPhase = FlashPhase.Idle;

    private Color activeColor;
    private float activeInDuration;
    private float activeHoldDuration;
    private float activeOutDuration;

    private int alternateHitSource = 0;

    void Awake()
    {
        if (!playerControllerRef)
            playerControllerRef = GetComponent<SpaceShooterController>();

        playerControllerRef.healthController.TookHit += HandleTookHit;
        playerControllerRef.healthController.Healed += HandleHealed;

        if (damageFlashOverlay)
            damageFlashOverlay.color = Color.clear;
    }

    void Update()
    {
        UpdateFlash();
    }

    private void HandleTookHit()
    {
        StartFlash(damageFlashColor, damageFlashInDuration, damageFlashHoldDuration, damageFlashOutDuration);
        playerControllerRef.cameraControllerRef.TriggerShake(hitShakeDuration, hitShakeMagnitude);
        PlayHitSound();
    }

    private void HandleHealed()
    {
        StartFlash(healFlashColor, healFlashInDuration, healFlashHoldDuration, healFlashOutDuration);
    }

    private void StartFlash(Color color, float inDuration, float holdDuration, float outDuration)
    {
        activeColor = color;
        activeInDuration = inDuration;
        activeHoldDuration = holdDuration;
        activeOutDuration = outDuration;

        flashPhase = FlashPhase.In;
        flashTimer = 0f;
    }

    private void UpdateFlash()
    {
        if (!damageFlashOverlay || flashPhase == FlashPhase.Idle) return;

        flashTimer += Time.deltaTime;

        switch (flashPhase)
        {
            case FlashPhase.In:
                damageFlashOverlay.color = Color.Lerp(Color.clear, activeColor, flashTimer / activeInDuration);
                if (flashTimer >= activeInDuration)
                {
                    flashPhase = FlashPhase.Hold;
                    flashTimer = 0f;
                }
                break;

            case FlashPhase.Hold:
                damageFlashOverlay.color = activeColor;
                if (flashTimer >= activeHoldDuration)
                {
                    flashPhase = FlashPhase.Out;
                    flashTimer = 0f;
                }
                break;

            case FlashPhase.Out:
                damageFlashOverlay.color = Color.Lerp(activeColor, Color.clear, flashTimer / activeOutDuration);
                if (flashTimer >= activeOutDuration)
                {
                    damageFlashOverlay.color = Color.clear;
                    flashPhase = FlashPhase.Idle;
                    flashTimer = 0f;
                }
                break;
        }
    }

    private void PlayHitSound()
    {
        if (hitSounds == null || hitSounds.Length == 0 || !hitAudioSource) return;

        hitAudioSource.PlayOneShot(hitSounds[alternateHitSource]);
        alternateHitSource = (alternateHitSource + 1) % hitSounds.Length;
    }
}