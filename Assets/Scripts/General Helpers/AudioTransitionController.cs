using UnityEngine;

class AudioTransitionController
{
    public AudioSource source;

    // Volume
    float volumeTarget;
    float volumeSpeed;
    bool volumeRunning;

    // Pitch
    float pitchTarget;
    float pitchSpeed;
    bool pitchRunning;

    float defaultVolume;
    float defaultPitch;

    public AudioTransitionController(AudioSource source)
    {
        this.source = source;
        defaultVolume = source.volume;
        defaultPitch = source.pitch;
    }

    public void SetVolumeOverTime(float target, float duration)
    {
        if (duration <= 0f)
        {
            source.volume = target;
            volumeRunning = false;
            return;
        }

        volumeTarget = target;
        volumeSpeed = (target - source.volume) / duration;
        volumeRunning = true;
    }

    public void SetPitchOverTime(float target, float duration)
    {
        if (duration <= 0f)
        {
            source.pitch = target;
            pitchRunning = false;
            return;
        }

        pitchTarget = target;
        pitchSpeed = (target - source.pitch) / duration;
        pitchRunning = true;
    }

    public void Update()
    {
        if (volumeRunning)
        {
            float delta = volumeSpeed * Time.deltaTime;
            float remaining = volumeTarget - source.volume;

            if (Mathf.Abs(delta) >= Mathf.Abs(remaining))
            {
                source.volume = volumeTarget;
                volumeRunning = false;

                if (volumeTarget == 0f)
                {
                    source.Stop();
                    source.volume = defaultVolume;
                }
            }
            else
            {
                source.volume += delta;
            }
        }

        if (pitchRunning)
        {
            float delta = pitchSpeed * Time.deltaTime;
            float remaining = pitchTarget - source.pitch;

            if (Mathf.Abs(delta) >= Mathf.Abs(remaining))
            {
                source.pitch = pitchTarget;
                pitchRunning = false;
            }
            else
            {
                source.pitch += delta;
            }
        }
    }
}