using System.Collections.Generic;
using UnityEngine;

namespace TrainTrain
{
    public enum AudioCue
    {
        Button,
        Switch,
        Spawn,
        Deliver,
        Wrong,
        Crash,
        Brake,
        Win,
        Lose
    }

    public sealed class SynthAudio : MonoBehaviour
    {
        private const int SampleRate = 44100;
        private readonly Dictionary<AudioCue, AudioClip> clips = new Dictionary<AudioCue, AudioClip>();
        private AudioSource source;
        private AudioSource ambient;
        private AudioClip ambientClip;

        private void Awake()
        {
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.volume = 0.34f;

            ambient = gameObject.AddComponent<AudioSource>();
            ambient.playOnAwake = false;
            ambient.loop = true;
            ambient.volume = 0.07f;
            ambient.clip = CreateAmbient();
            ApplyMute();
        }

        public void Play(AudioCue cue)
        {
            if (PlayerProgress.Muted || source == null)
            {
                return;
            }

            if (!clips.TryGetValue(cue, out AudioClip clip))
            {
                clip = BuildCue(cue);
                clips[cue] = clip;
            }

            if (clip != null)
            {
                source.PlayOneShot(clip);
            }
        }

        public void SetPlaying(bool playing)
        {
            if (ambient == null)
            {
                return;
            }

            if (playing && !PlayerProgress.Muted)
            {
                if (!ambient.isPlaying)
                {
                    ambient.Play();
                }
            }
            else if (ambient.isPlaying)
            {
                ambient.Pause();
            }
        }

        public void ToggleMute()
        {
            PlayerProgress.Muted = !PlayerProgress.Muted;
            ApplyMute();
        }

        private void ApplyMute()
        {
            if (source != null)
            {
                source.mute = PlayerProgress.Muted;
            }

            if (ambient != null)
            {
                ambient.mute = PlayerProgress.Muted;
            }
        }

        private static AudioClip BuildCue(AudioCue cue)
        {
            switch (cue)
            {
                case AudioCue.Button:
                    return MakeTone("Button", new[] { 420f }, 0.055f, 0.22f, false);
                case AudioCue.Switch:
                    return MakeTone("Switch", new[] { 260f, 390f }, 0.09f, 0.28f, true);
                case AudioCue.Spawn:
                    return MakeTone("Spawn", new[] { 180f, 230f }, 0.13f, 0.17f, true);
                case AudioCue.Deliver:
                    return MakeTone("Deliver", new[] { 523.25f, 659.25f, 783.99f }, 0.22f, 0.27f, false);
                case AudioCue.Wrong:
                    return MakeTone("Wrong", new[] { 190f, 135f }, 0.27f, 0.25f, true);
                case AudioCue.Crash:
                    return MakeNoise("Crash", 0.34f, 0.34f);
                case AudioCue.Brake:
                    return MakeTone("Brake", new[] { 620f, 310f }, 0.15f, 0.19f, true);
                case AudioCue.Win:
                    return MakeTone("Win", new[] { 392f, 523.25f, 659.25f, 783.99f }, 0.65f, 0.25f, false);
                case AudioCue.Lose:
                    return MakeTone("Lose", new[] { 220f, 174.61f, 130.81f }, 0.62f, 0.25f, true);
                default:
                    return null;
            }
        }

        private static AudioClip MakeTone(string name, float[] notes, float duration, float volume, bool descending)
        {
            int count = Mathf.CeilToInt(duration * SampleRate);
            float[] data = new float[count];
            float noteLength = duration / notes.Length;
            for (int i = 0; i < count; i++)
            {
                float time = i / (float)SampleRate;
                int noteIndex = Mathf.Min(notes.Length - 1, Mathf.FloorToInt(time / noteLength));
                float local = (time - noteIndex * noteLength) / noteLength;
                float attack = Mathf.Clamp01(local / 0.08f);
                float release = 1f - Mathf.Clamp01((local - 0.45f) / 0.55f);
                float envelope = attack * release;
                float frequency = notes[noteIndex];
                if (descending)
                {
                    frequency *= Mathf.Lerp(1f, 0.75f, local);
                }

                float sine = Mathf.Sin(time * frequency * Mathf.PI * 2f);
                float harmonic = Mathf.Sin(time * frequency * 2.01f * Mathf.PI * 2f) * 0.18f;
                data[i] = (sine + harmonic) * envelope * volume;
            }

            AudioClip clip = AudioClip.Create(name, count, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip MakeNoise(string name, float duration, float volume)
        {
            int count = Mathf.CeilToInt(duration * SampleRate);
            float[] data = new float[count];
            System.Random random = new System.Random(404);
            float filtered = 0f;
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)count;
                float envelope = Mathf.Pow(1f - t, 2f);
                float noise = (float)(random.NextDouble() * 2.0 - 1.0);
                filtered = Mathf.Lerp(filtered, noise, 0.24f);
                data[i] = (filtered + Mathf.Sin(i * 0.045f) * 0.22f) * envelope * volume;
            }

            AudioClip clip = AudioClip.Create(name, count, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip CreateAmbient()
        {
            const float duration = 4f;
            int count = Mathf.CeilToInt(duration * SampleRate);
            float[] data = new float[count];
            System.Random random = new System.Random(917);
            float filteredNoise = 0f;
            for (int i = 0; i < count; i++)
            {
                float time = i / (float)SampleRate;
                float pulse = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(time * Mathf.PI * 2f * 1.5f)), 7f);
                float rumble = Mathf.Sin(time * Mathf.PI * 2f * 48f) * 0.1f;
                float noise = (float)(random.NextDouble() * 2.0 - 1.0);
                filteredNoise = Mathf.Lerp(filteredNoise, noise, 0.006f);
                data[i] = rumble * (0.32f + pulse * 0.8f) + filteredNoise * 0.11f;
            }

            AudioClip clip = AudioClip.Create("RailAmbient", count, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
