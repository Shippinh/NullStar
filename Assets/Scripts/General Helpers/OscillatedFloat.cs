using UnityEngine;

[System.Serializable]
public class OscillatedFloat
{
    [Tooltip("Base value used when oscillator is disabled")]
    public float value = 0f;
    public Oscillator oscillator = new Oscillator();

    public float Evaluate(float t)
    {
        if (oscillator == null || !oscillator.enabled)
            return value;
        return oscillator.Evaluate(t, value);
    }
}
