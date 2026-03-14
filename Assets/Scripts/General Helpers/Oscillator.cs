using UnityEngine;

[System.Serializable]
public class Oscillator
{
    public enum Waveform { Sine, Triangle, Square, Sawtooth, Noise }

    public bool enabled = false;
    public Waveform waveform = Waveform.Sine;
    public float amplitude = 0f;
    public float frequency = 1f;
    public float offset = 0f;

    public bool useRange = false;
    public float minValue = -1f;
    public float maxValue = 1f;

    public float Evaluate(float t, float baseValue)
    {
        if (!enabled) return baseValue;

        float cycle = (t + offset) * frequency;
        float raw = 0f;

        switch (waveform)
        {
            case Waveform.Sine: raw = Mathf.Sin(cycle * Mathf.PI * 2f); break;
            case Waveform.Triangle: raw = 2f * Mathf.Abs(2f * (cycle - Mathf.Floor(cycle + 0.5f))) - 1f; break;
            case Waveform.Square: raw = Mathf.Sign(Mathf.Sin(cycle * Mathf.PI * 2f)); break;
            case Waveform.Sawtooth: raw = 2f * (cycle - Mathf.Floor(cycle + 0.5f)); break;
            case Waveform.Noise: raw = UnityEngine.Random.Range(-1f, 1f); break;
        }

        return useRange
            ? Mathf.Lerp(minValue, maxValue, (raw + 1f) * 0.5f)
            : baseValue + amplitude * raw;
    }

    public void SetRange(float min, float max)
    {
        useRange = true;
        minValue = min;
        maxValue = max;
    }
}
