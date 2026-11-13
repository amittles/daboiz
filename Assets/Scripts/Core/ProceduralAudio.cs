using System.Collections.Generic;
using UnityEngine;

namespace Daboiz.Core
{
    public enum Sfx { Shot, Laser, Pop, Squeak, Boom, Ding, Buzz, Splat, Click, Whoosh, Jump, Land, Zap }

    /// <summary>Tiny synthesizer: every sound in the demo is generated with AudioClip.Create.</summary>
    public static class ProceduralAudio
    {
        private const int Rate = 22050;
        private static readonly Dictionary<Sfx, AudioClip> Clips = new Dictionary<Sfx, AudioClip>();
        private static System.Random _rng = new System.Random(1234);
        public static float MasterVolume = 0.6f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { Clips.Clear(); }

        public static void Play(Sfx s, Vector3 position, float volume = 1f, float pitch = 1f, float spatial = 0.75f)
        {
            AudioClip clip = Get(s);
            if (clip == null) return;
            GameObject go = new GameObject("Sfx_" + s);
            go.transform.position = position;
            AudioSource src = go.AddComponent<AudioSource>();
            src.clip = clip;
            src.volume = Mathf.Clamp01(volume * MasterVolume);
            src.pitch = Mathf.Clamp(pitch, 0.1f, 3f);
            src.spatialBlend = spatial;
            src.minDistance = 4f;
            src.maxDistance = 80f;
            src.rolloffMode = AudioRolloffMode.Linear;
            src.Play();
            Object.Destroy(go, clip.length / src.pitch + 0.1f);
        }

        /// <summary>2D (non-spatial) UI sound.</summary>
        public static void PlayUI(Sfx s, float volume = 1f, float pitch = 1f)
        {
            Vector3 p = Camera.main != null ? Camera.main.transform.position : Vector3.zero;
            Play(s, p, volume, pitch, 0f);
        }

        public static AudioClip Get(Sfx s)
        {
            AudioClip c;
            if (Clips.TryGetValue(s, out c) && c != null) return c;
            c = Build(s);
            Clips[s] = c;
            return c;
        }

        private static float Noise() { return (float)(_rng.NextDouble() * 2.0 - 1.0); }

        private static AudioClip Build(Sfx s)
        {
            float len;
            switch (s)
            {
                case Sfx.Boom: len = 0.9f; break;
                case Sfx.Ding: len = 0.6f; break;
                case Sfx.Buzz: len = 0.7f; break;
                case Sfx.Squeak: len = 0.25f; break;
                case Sfx.Whoosh: len = 0.4f; break;
                default: len = 0.18f; break;
            }
            int n = Mathf.CeilToInt(len * Rate);
            float[] d = new float[n];
            float phase = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float u = t / len; // 0..1
                float v = 0f;
                switch (s)
                {
                    case Sfx.Shot:
                        v = Noise() * Mathf.Exp(-t * 30f) * 0.9f + Mathf.Sin(t * 2f * Mathf.PI * 120f) * Mathf.Exp(-t * 25f) * 0.5f;
                        break;
                    case Sfx.Laser:
                        phase += 2f * Mathf.PI * Mathf.Lerp(1800f, 300f, u) / Rate;
                        v = Mathf.Sign(Mathf.Sin(phase)) * 0.35f * (1f - u);
                        break;
                    case Sfx.Pop:
                        phase += 2f * Mathf.PI * Mathf.Lerp(900f, 200f, u) / Rate;
                        v = Mathf.Sin(phase) * Mathf.Exp(-t * 35f) + Noise() * Mathf.Exp(-t * 80f) * 0.3f;
                        break;
                    case Sfx.Squeak:
                        phase += 2f * Mathf.PI * (900f + Mathf.Sin(u * Mathf.PI) * 700f) / Rate;
                        v = Mathf.Sin(phase) * 0.5f * Mathf.Sin(u * Mathf.PI);
                        break;
                    case Sfx.Boom:
                        v = (Noise() * 0.8f + Mathf.Sin(t * 2f * Mathf.PI * 50f)) * Mathf.Exp(-t * 5f) * 0.9f;
                        break;
                    case Sfx.Ding:
                        v = (Mathf.Sin(t * 2f * Mathf.PI * 1320f) * 0.5f + Mathf.Sin(t * 2f * Mathf.PI * 1980f) * 0.25f) * Mathf.Exp(-t * 6f);
                        break;
                    case Sfx.Buzz:
                        phase += 2f * Mathf.PI * 110f / Rate;
                        v = (Mathf.Repeat(phase / (2f * Mathf.PI), 1f) * 2f - 1f) * 0.4f * (1f - u * 0.5f);
                        break;
                    case Sfx.Splat:
                        v = Noise() * Mathf.Exp(-t * 18f) * 0.7f * (1f + Mathf.Sin(t * 400f));
                        break;
                    case Sfx.Click:
                        v = Noise() * Mathf.Exp(-t * 120f);
                        break;
                    case Sfx.Whoosh:
                        v = Noise() * Mathf.Sin(u * Mathf.PI) * 0.35f;
                        break;
                    case Sfx.Jump:
                        phase += 2f * Mathf.PI * Mathf.Lerp(250f, 500f, u) / Rate;
                        v = Mathf.Sin(phase) * 0.2f * (1f - u);
                        break;
                    case Sfx.Land:
                        v = Noise() * Mathf.Exp(-t * 40f) * 0.4f;
                        break;
                    case Sfx.Zap:
                        phase += 2f * Mathf.PI * (600f + Noise() * 400f) / Rate;
                        v = Mathf.Sin(phase) * 0.3f * (1f - u);
                        break;
                }
                d[i] = Mathf.Clamp(v, -1f, 1f);
            }
            AudioClip clip = AudioClip.Create("Daboiz_" + s, n, 1, Rate, false);
            clip.SetData(d, 0);
            return clip;
        }
    }
}
