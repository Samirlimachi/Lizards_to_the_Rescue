using UnityEngine;

namespace Lizards
{
    /// <summary>Sonidos provisionales sintetizados por código (sustituibles por clips reales).</summary>
    public static class Sfx
    {
        const int Rate = 22050;

        public static readonly AudioClip Jump = Sweep("jump", 0.12f, 300f, 700f, 0.5f);
        public static readonly AudioClip Land = Noise("land", 0.08f, 0.6f, 0.15f);
        public static readonly AudioClip TongueOut = Sweep("tongue", 0.1f, 900f, 300f, 0.3f);
        public static readonly AudioClip Stick = Noise("stick", 0.06f, 0.5f, 0.6f);
        public static readonly AudioClip Eat = Sweep("gulp", 0.18f, 260f, 120f, 0.6f);
        public static readonly AudioClip Eject = Sweep("blorp", 0.2f, 150f, 420f, 0.6f);
        public static readonly AudioClip Acid = Noise("acid", 0.2f, 0.5f, 0.9f);
        public static readonly AudioClip Hurt = Sweep("hurt", 0.22f, 900f, 350f, 0.5f);
        public static readonly AudioClip Transform = Sweep("transform", 0.4f, 200f, 900f, 0.4f);
        public static readonly AudioClip Help = Beeps("help");
        public static readonly AudioClip Break = Noise("break", 0.35f, 0.9f, 0.2f);
        public static readonly AudioClip Dissolve = Noise("sizzle", 0.3f, 0.35f, 0.95f);

        public static void Play(AudioClip clip, Vector3 at, float volume = 0.8f)
        {
            if (clip && Application.isPlaying) AudioSource.PlayClipAtPoint(clip, at, volume);
        }

        static AudioClip Sweep(string name, float dur, float f0, float f1, float vol)
        {
            int n = (int)(dur * Rate); var d = new float[n]; float phase = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n;
                phase += 2f * Mathf.PI * Mathf.Lerp(f0, f1, t) / Rate;
                d[i] = Mathf.Sin(phase) * vol * (1f - t) * Mathf.Min(1f, t * 40f);
            }
            return Make(name, d);
        }

        static AudioClip Noise(string name, float dur, float vol, float brightness)
        {
            int n = (int)(dur * Rate); var d = new float[n]; float last = 0f;
            var rng = new System.Random(name.GetHashCode());
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n;
                float w = (float)rng.NextDouble() * 2f - 1f;
                last = Mathf.Lerp(last, w, brightness);
                d[i] = last * vol * (1f - t) * (1f - t);
            }
            return Make(name, d);
        }

        static AudioClip Beeps(string name)
        {
            int n = (int)(0.4f * Rate); var d = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                bool on = t < 0.12f || (t > 0.2f && t < 0.32f);
                float f = t < 0.15f ? 880f : 1175f;
                d[i] = on ? Mathf.Sin(2f * Mathf.PI * f * t) * 0.35f : 0f;
            }
            return Make(name, d);
        }

        static AudioClip Make(string name, float[] data)
        {
            var c = AudioClip.Create(name, data.Length, 1, Rate, false);
            c.SetData(data, 0);
            return c;
        }
    }
}
