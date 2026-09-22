using System.Collections.Generic;
using UnityEngine;

namespace MoonThief
{
    /// <summary>
    /// Tiny chiptune synthesizer: every sound effect is generated in code at first use,
    /// so the game ships with zero audio files. Square/triangle/saw voices, short and
    /// punchy, matched to the pixel-art tone. Safe to call from anywhere; silently
    /// does nothing in edit mode.
    /// </summary>
    public static class Sfx
    {
        const int Rate = 22050;

        static AudioSource _src;
        static readonly Dictionary<string, AudioClip> _cache = new Dictionary<string, AudioClip>();

        /// <summary>Silenced by the settings screen without tearing the source down.</summary>
        public static bool Muted;

        /// <summary>Creates the one AudioListener + player source under the game root.</summary>
        public static void Init(Transform parent)
        {
            if (_src != null) return;
            var host = new GameObject("sfx");
            host.transform.SetParent(parent, false);
            host.AddComponent<AudioListener>();
            _src = host.AddComponent<AudioSource>();
            _src.playOnAwake = false;
            _src.spatialBlend = 0f;
        }

        public static void Play(string name)
        {
            if (_src == null || Muted || !Application.isPlaying) return;
            if (!_cache.TryGetValue(name, out var clip)) { clip = Build(name); _cache[name] = clip; }
            if (clip != null) _src.PlayOneShot(clip);
        }

        static AudioClip Build(string name)
        {
            switch (name)
            {
                case "hit":      return Segs(new[] { (520f, 0.05f), (196f, 0.09f) }, Wave.Square, 0.40f);
                case "hurt":     return Segs(new[] { (240f, 0.05f), (110f, 0.11f) }, Wave.Saw, 0.40f);
                case "faint":    return Slide(620f, 90f, 0.38f, Wave.Tri, 0.45f);
                case "befriend": return Arp(new[] { 523f, 659f, 784f, 1047f }, 0.07f, Wave.Square, 0.38f);
                case "fail":     return Slide(320f, 170f, 0.22f, Wave.Square, 0.32f);
                case "chest":    return Arp(new[] { 392f, 494f, 587f, 784f }, 0.06f, Wave.Tri, 0.38f);
                case "blip":     return Segs(new[] { (880f, 0.03f) }, Wave.Square, 0.18f);
                case "ui":       return Segs(new[] { (660f, 0.045f) }, Wave.Square, 0.22f);
                case "win":      return Arp(new[] { 523f, 659f, 784f, 659f, 784f, 1047f }, 0.09f, Wave.Square, 0.38f);
                case "boss":     return Slide(110f, 45f, 0.55f, Wave.Saw, 0.5f);
                default:         return null;
            }
        }

        enum Wave { Square, Tri, Saw }

        static float Sample(Wave w, float phase)
        {
            switch (w)
            {
                case Wave.Tri: return 4f * Mathf.Abs(phase - Mathf.Floor(phase) - 0.5f) - 1f;
                case Wave.Saw: return 2f * (phase - Mathf.Floor(phase + 0.5f));
                default: return phase - Mathf.Floor(phase) < 0.5f ? 1f : -1f;
            }
        }

        /// <summary>A sequence of constant-pitch segments, each with a linear decay.</summary>
        static AudioClip Segs((float f, float t)[] segs, Wave w, float vol)
        {
            int n = 0;
            foreach (var s in segs) n += Mathf.CeilToInt(s.t * Rate);
            var buf = new float[n];
            int i = 0;
            foreach (var s in segs)
            {
                int len = Mathf.CeilToInt(s.t * Rate);
                for (int k = 0; k < len && i < n; k++, i++)
                    buf[i] = Sample(w, s.f * k / Rate) * vol * (1f - (float)k / len);
            }
            return Commit(buf);
        }

        /// <summary>A pitch glide from f0 down/up to f1.</summary>
        static AudioClip Slide(float f0, float f1, float dur, Wave w, float vol)
        {
            int n = Mathf.CeilToInt(dur * Rate);
            var buf = new float[n];
            float phase = 0f;
            for (int i = 0; i < n; i++)
            {
                float k = i / (float)n;
                phase += Mathf.Lerp(f0, f1, k) / Rate;
                buf[i] = Sample(w, phase) * vol * (1f - k * 0.6f);
            }
            return Commit(buf);
        }

        /// <summary>Quick note run (victory beeps, capture jingles).</summary>
        static AudioClip Arp(float[] notes, float step, Wave w, float vol)
        {
            int per = Mathf.CeilToInt(step * Rate);
            var buf = new float[per * notes.Length];
            for (int s = 0; s < notes.Length; s++)
                for (int k = 0; k < per; k++)
                    buf[s * per + k] = Sample(w, notes[s] * k / Rate) * vol * (1f - (float)k / per * 0.7f);
            return Commit(buf);
        }

        static AudioClip Commit(float[] buf)
        {
            var clip = AudioClip.Create("sfx", buf.Length, 1, Rate, false);
            clip.SetData(buf, 0);
            return clip;
        }
    }
}
