using UnityEngine;

public class HitFlashController : MonoBehaviour
{
    [Header("References")]
    public EntityHealthController healthControllerRef;
    public Renderer flashRenderer;
    public bool flashExtraRenderers;
    public Renderer[] extraRenderers;

    [Header("Flash Settings")]
    public Color flashColor = Color.white;
    public float flashInDuration = 0.05f;
    public float flashHoldDuration = 0f;
    public float flashOutDuration = 0.1f;

    [Header("Internals")]
    [SerializeField] private float flashTimer = 0f;
    private enum FlashPhase { Idle, In, Hold, Out }
    [SerializeField] private FlashPhase flashPhase = FlashPhase.Idle;

    private static readonly int FlashColorID = Shader.PropertyToID("_FlashColor");
    private static readonly int FlashPowerID = Shader.PropertyToID("_FlashPower");

    private MaterialPropertyBlock mpb;

    void Awake()
    {
        if (!healthControllerRef)
            healthControllerRef = GetComponent<EntityHealthController>();

        mpb = new MaterialPropertyBlock();
        flashRenderer = GetComponent<Renderer>();
        healthControllerRef.TookHit += HandleTookHit;
    }

    void Update()
    {
        UpdateFlash();
    }

    private void HandleTookHit()
    {
        flashPhase = FlashPhase.In;
        flashTimer = 0f;
    }

    private void UpdateFlash()
    {
        if (flashRenderer == null || flashPhase == FlashPhase.Idle) return;

        flashTimer += Time.deltaTime;

        switch (flashPhase)
        {
            case FlashPhase.In:
                SetFlashPower(flashInDuration > 0f ? Mathf.Clamp01(flashTimer / flashInDuration) : 1f);
                if (flashTimer >= flashInDuration) { flashPhase = FlashPhase.Hold; flashTimer = 0f; }
                break;

            case FlashPhase.Hold:
                SetFlashPower(1f);
                if (flashTimer >= flashHoldDuration) { flashPhase = FlashPhase.Out; flashTimer = 0f; }
                break;

            case FlashPhase.Out:
                SetFlashPower(flashOutDuration > 0f ? 1f - Mathf.Clamp01(flashTimer / flashOutDuration) : 0f);
                if (flashTimer >= flashOutDuration)
                {
                    SetFlashPower(0f);
                    flashPhase = FlashPhase.Idle;
                    flashTimer = 0f;
                }
                break;
        }
    }

    private void SetFlashPower(float amount)
    {
        ApplyFlashToRenderer(flashRenderer, amount);

        if (flashExtraRenderers && extraRenderers != null)
        {
            for (int i = 0; i < extraRenderers.Length; i++)
            {
                ApplyFlashToRenderer(extraRenderers[i], amount);
            }
        }
    }

    private void ApplyFlashToRenderer(Renderer targetRenderer, float amount)
    {
        if (!targetRenderer) return;
        targetRenderer.GetPropertyBlock(mpb);
        mpb.SetColor(FlashColorID, flashColor);
        mpb.SetFloat(FlashPowerID, amount);
        targetRenderer.SetPropertyBlock(mpb);
    }
}