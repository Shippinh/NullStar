using UnityEngine;

public static class LerpFactorMethods
{
    public enum LerpFactor
    {
        None = 0,
        Linear = 1,
        SmoothDamp = 2,
        EaseInQuad = 3,
        EaseOutQuad = 4,
        EaseInOutCubic = 5,
        Elastic = 6
    }

    // Linear interpolation
    public static float Linear(float t)
    {
        return Mathf.Clamp01(t);
    }

    // Exponential / Smooth Damp (speed-based)
    public static float SmoothDamp(float speed, float t)
    {
        return 1f - Mathf.Exp(-speed * t);
    }

    // Quadratic Ease-in (slow start, fast end)
    public static float EaseInQuad(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t;
    }

    // Quadratic Ease-out (fast start, slow end)
    public static float EaseOutQuad(float t)
    {
        t = Mathf.Clamp01(t);
        return 1f - (1f - t) * (1f - t);
    }

    // Cubic Ease-in-out (smooth start and end)
    public static float EaseInOutCubic(float t)
    {
        t = Mathf.Clamp01(t);
        return t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) / 2f;
    }

    // Optional elastic / overshoot
    public static float Elastic(float t, float overshoot = 0.1f)
    {
        t = Mathf.Clamp01(t);
        return Mathf.Sin(t * Mathf.PI * 0.5f) + t * overshoot;
    }

    // Universal getter helper for cleaner usage
    public static float GetLerpFactor(LerpFactor type, float t, float speed = 0f)
    {
        switch (type)
        {
            case LerpFactor.None:
            case LerpFactor.Linear: return Linear(t);
            case LerpFactor.SmoothDamp: return SmoothDamp(speed, t);
            case LerpFactor.EaseInQuad: return EaseInQuad(t);
            case LerpFactor.EaseOutQuad: return EaseOutQuad(t);
            case LerpFactor.EaseInOutCubic: return EaseInOutCubic(t);
            case LerpFactor.Elastic: return Elastic(t);
            default: return t;
        }
    }
}