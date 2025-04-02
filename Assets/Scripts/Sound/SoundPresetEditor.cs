#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(SoundPreset))]
public class SoundPresetEditor : Editor
{
    public override void OnInspectorGUI()
    {
        SoundPreset preset = (SoundPreset)target;

        // Audio Clip and Audio Settings Section
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Audio Settings", EditorStyles.boldLabel);
        preset.audioClip = (AudioClip)EditorGUILayout.ObjectField("Audio Clip", preset.audioClip, typeof(AudioClip), false);
        preset.volume = EditorGUILayout.Slider("Volume", preset.volume, 0f, 1f);
        EditorGUILayout.Space();
        preset.pitch = EditorGUILayout.Slider("Pitch", preset.pitch, -3f, 3f);
        preset.isVariablePitch = EditorGUILayout.Toggle("Is Variable Pitch", preset.isVariablePitch);
        if(preset.isVariablePitch)
        {
            preset.variationRange = EditorGUILayout.Slider("Variation Range", preset.variationRange, 0f, 1f);
            preset.variationFrequency = EditorGUILayout.Slider("Variation Frequency", preset.variationFrequency, 0.2f, 1f);
            preset.randomiseFrequency = EditorGUILayout.Toggle("Randomise Frequency", preset.randomiseFrequency);
        }
        EditorGUILayout.Space();
        preset.loop = EditorGUILayout.Toggle("Loop", preset.loop);
        preset.spatialBlend = EditorGUILayout.Slider("Spatial Blend", preset.spatialBlend, 0f, 1f);
        preset.rolloffMode = (AudioRolloffMode)EditorGUILayout.EnumPopup("Rolloff Mode", preset.rolloffMode);
        preset.minDistance = EditorGUILayout.FloatField("Min Distance", preset.minDistance);
        preset.maxDistance = EditorGUILayout.FloatField("Max Distance", preset.maxDistance);

        // Filter Types Section
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Filter Types", EditorStyles.boldLabel);
        preset.filterTypes = (AudioFilterType)EditorGUILayout.EnumFlagsField("Filter Types", preset.filterTypes);

        // Reverb Filter Settings Section
        if ((preset.filterTypes & AudioFilterType.Reverb) != 0)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Reverb Filter Settings", EditorStyles.boldLabel);

            EditorGUILayout.LabelField($"Reverb Preset: User", EditorStyles.label);

            // Dry Level
            preset.dryLevel = EditorGUILayout.Slider("Dry Level", preset.dryLevel, -10000f, 0f);

            // Room Size (Unity's reverb room size range is typically from -10000 to 0)
            preset.room = EditorGUILayout.Slider("Room", preset.room, -10000f, 0f);

            // Room HF (High Frequency Level)
            preset.roomHF = EditorGUILayout.Slider("Room HF", preset.roomHF, -10000f, 0f);

            // Room LF (Low Frequency Level)
            preset.roomLF = EditorGUILayout.Slider("Room LF", preset.roomLF, -10000f, 0f);

            // Decay Time (from 0.1 to 20 seconds for reverberation decay)
            preset.decayTime = EditorGUILayout.Slider("Decay Time", preset.decayTime, 0.1f, 20f);

            // Decay HF Ratio (controls the balance of high frequency decay)
            preset.decayHFRatio = EditorGUILayout.Slider("Decay HF Ratio", preset.decayHFRatio, 0.1f, 2f);

            // Reflections Level (the level of early reflections)
            preset.reflectionsLevel = EditorGUILayout.Slider("Reflections Level", preset.reflectionsLevel, -10000f, 1000f);

            // Reflections Delay (controls the delay before reflections)
            preset.reflectionsDelay = EditorGUILayout.Slider("Reflections Delay", preset.reflectionsDelay, 0f, 0.3f);

            // Reverb Level
            preset.reverbLevel = EditorGUILayout.Slider("Reverb Level", preset.reverbLevel, -10000f, 2000f);

            // Reverb Delay (controls the delay before reverberation starts)
            preset.reverbDelay = EditorGUILayout.Slider("Reverb Delay", preset.reverbDelay, 0f, 0.1f);

            // HF Reference
            preset.hfReference = EditorGUILayout.Slider("HF Reference", preset.hfReference, 1000f, 20000f);

            // LF Reference
            preset.lfReference = EditorGUILayout.Slider("LF Reference", preset.lfReference, 20f, 1000f);

            // Diffusion (the amount of scattering of sound waves)
            preset.diffusion = EditorGUILayout.Slider("Diffusion", preset.diffusion, 0f, 100f);

            // Density (controls the perceived density of the reverberation)
            preset.density = EditorGUILayout.Slider("Density", preset.density, 0f, 100f);
        }

        // Echo Filter Settings Section
        if ((preset.filterTypes & AudioFilterType.Echo) != 0)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Echo Filter Settings", EditorStyles.boldLabel);
            
            // Echo Delay (Delay time for the echo in seconds)
            preset.echoDelay = EditorGUILayout.FloatField("Delay", preset.echoDelay);

            // Decay Ratio (How much the echo signal decays per repetition)
            preset.echoDecayRatio = EditorGUILayout.FloatField("Decay Ratio", preset.echoDecayRatio);

            preset.echoDryMix = EditorGUILayout.FloatField("Dry Mix", preset.echoDryMix);

            // Wet Mix (Controls the mix of the original signal and the echo effect)
            preset.echoWetMix = EditorGUILayout.FloatField("Wet Mix", preset.echoWetMix);
        }

        // Distortion Filter Settings Section
        if ((preset.filterTypes & AudioFilterType.Distortion) != 0)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Distortion Filter Settings", EditorStyles.boldLabel);
            
            // Distortion Level (Intensity of the distortion effect)
            preset.distortionLevel = EditorGUILayout.FloatField("Distortion Level", preset.distortionLevel);
        }

        // Chorus Filter Settings Section
        if ((preset.filterTypes & AudioFilterType.Chorus) != 0)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Chorus Filter Settings", EditorStyles.boldLabel);
            
            // Chorus Wet Mix (Controls the mix of the original signal and the chorus effect)
            preset.chorusDryMix = EditorGUILayout.FloatField("Dry Mix", preset.chorusDryMix);

            // Chorus Wet Mix (Controls the mix of the original signal and the chorus effect)
            preset.chorusWetMix1 = EditorGUILayout.FloatField("Wet Mix 1", preset.chorusWetMix1);

            // Chorus Wet Mix (Controls the mix of the original signal and the chorus effect)
            preset.chorusWetMix2 = EditorGUILayout.FloatField("Wet Mix 2", preset.chorusWetMix2);

            // Chorus Wet Mix (Controls the mix of the original signal and the chorus effect)
            preset.chorusWetMix3 = EditorGUILayout.FloatField("Wet Mix 3", preset.chorusWetMix3);

            preset.chorusRate = EditorGUILayout.FloatField("Delay", preset.chorusDelay);

            // Chorus Rate (The speed of the chorus effect in Hz)
            preset.chorusRate = EditorGUILayout.FloatField("Rate", preset.chorusRate);

            // Chorus Depth (The depth of the chorus modulation)
            preset.chorusDepth = EditorGUILayout.FloatField("Depth", preset.chorusDepth);
        }

        // Low-Pass Filter Settings Section
        if ((preset.filterTypes & AudioFilterType.LowPass) != 0)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Low-Pass Filter Settings", EditorStyles.boldLabel);
            
            // Cutoff Frequency (Low-pass filter cutoff in Hz)
            preset.lowPassCutoffFrequency = EditorGUILayout.Slider("Cutoff Frequency", preset.lowPassCutoffFrequency, 10f, 22000f);

            // Resonance (Controls the resonance at the cutoff frequency)
            preset.lowPassResonanceQ = EditorGUILayout.FloatField("Resonance", preset.lowPassResonanceQ);
        }

        // High-Pass Filter Settings Section
        if ((preset.filterTypes & AudioFilterType.HighPass) != 0)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("High-Pass Filter Settings", EditorStyles.boldLabel);
            
            // Cutoff Frequency (High-pass filter cutoff in Hz)
            preset.highPassCutoffFrequency = EditorGUILayout.Slider("Cutoff Frequency", preset.highPassCutoffFrequency, 10f, 22000f);

            // Resonance (Controls the resonance at the cutoff frequency)
            preset.highPassResonanceQ = EditorGUILayout.FloatField("Resonance", preset.highPassResonanceQ);
        }


        // Apply changes to the target object
        EditorUtility.SetDirty(preset);
    }
}
#endif