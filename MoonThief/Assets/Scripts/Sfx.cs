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

        /// <summary>Master loudness for effects, 0..1; the settings row steps it in quarters.</summary>
        public static float Volume = 1f;

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
            if (clip != null) _src.PlayOneShot(clip, Volume);
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
                case "tick":     return Segs(new[] { (1500f, 0.012f) }, Wave.Square, 0.07f);
                case "ui":       return Segs(new[] { (660f, 0.045f) }, Wave.Square, 0.22f);
                case "win":      return Arp(new[] { 523f, 659f, 784f, 659f, 784f, 1047f }, 0.09f, Wave.Square, 0.38f);
                case "boss":     return Slide(110f, 45f, 0.55f, Wave.Saw, 0.5f);
                case "levelup":  return Arp(new[] { 440f, 554f, 659f, 880f, 1109f, 1319f }, 0.08f, Wave.Tri, 0.4f);
                case "coin":     return Arp(new[] { 988f, 1319f }, 0.055f, Wave.Square, 0.3f);
                case "shard":    return Arp(new[] { 659f, 880f, 1109f, 1760f }, 0.1f, Wave.Tri, 0.4f);
                case "door":     return Segs(new[] { (330f, 0.04f), (262f, 0.08f) }, Wave.Tri, 0.35f);
                case "step":     return Segs(new[] { (196f, 0.03f) }, Wave.Square, 0.10f);
                case "heal":     return Arp(new[] { 523f, 659f, 784f }, 0.11f, Wave.Tri, 0.32f);
                case "crit":     return Segs(new[] { (740f, 0.04f), (988f, 0.04f), (392f, 0.1f) }, Wave.Square, 0.42f);
                case "enemy":    return Slide(880f, 330f, 0.18f, Wave.Saw, 0.3f);
                case "buy":      return Arp(new[] { 784f, 988f, 1175f }, 0.06f, Wave.Square, 0.32f);
                case "autoon":   return Arp(new[] { 659f, 988f }, 0.06f, Wave.Square, 0.3f);
                default:         return null;
            }
        }

        internal enum Wave { Square, Tri, Saw }

        internal static float Sample(Wave w, float phase)
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

        internal static AudioClip Commit(float[] buf)
        {
            var clip = AudioClip.Create("sfx", buf.Length, 1, Rate, false);
            clip.SetData(buf, 0);
            return clip;
        }

        // ------------------------------------------------------------------ music

        /// <summary>
        /// Background music: short chiptune loops composed in code and crossfaded between
        /// scenes. No audio files, no DSP graph - one AudioSource per loop with a baked clip.
        /// </summary>
        public static class Mus
        {
            static AudioSource _a, _b;      // ping-pong crossfade pair
            static AudioSource _cur;
            static readonly Dictionary<string, AudioClip> _clips = new Dictionary<string, AudioClip>();
            static Transform _host;
            static string _track;
            static float _fade;             // seconds left on the current crossfade
            static bool _muted;

            static float _vol = 1f;

            /// <summary>Music loudness, 0..1, folded into the crossfade target volume. Applies to
            /// the playing track immediately, not just on the next Play().</summary>
            public static float Volume
            {
                get => _vol;
                set
                {
                    _vol = Mathf.Clamp01(value);
                    if (_cur != null && _fade <= 0f) _cur.volume = 0.55f * _vol;
                }
            }

            public static bool Muted
            {
                get => _muted;
                set
                {
                    _muted = value;
                    if (_cur != null) _cur.mute = _muted;
                }
            }

            public static string Current => _track;

            public static void Init(Transform parent)
            {
                if (_a != null) { _host = parent; return; }
                _host = parent;
                _a = NewSource("musA");
                _b = NewSource("musB");
            }

            static AudioSource NewSource(string name)
            {
                var go = new GameObject(name);
                go.transform.SetParent(_host, false);
                var s = go.AddComponent<AudioSource>();
                s.playOnAwake = false;
                s.spatialBlend = 0f;
                s.loop = true;
                s.volume = 0f;
                return s;
            }

            /// <summary>Fade into a track; a no-op if it is already playing.</summary>
            public static void Play(string track)
            {
                if (_a == null || !Application.isPlaying || track == _track) return;
                _track = track;
                var next = _cur == _a ? _b : _a;
                var clip = Clip(track);
                next.clip = clip;
                next.volume = 0f;
                if (clip != null)
                {
                    next.mute = _muted;
                    next.Play();
                }
                _fade = 0.45f;
                // the fading-out side keeps winding down inside Tick()
                _cur = next;
            }

            public static void Tick()
            {
                if (_a == null || _fade <= 0f) return;
                _fade -= Time.deltaTime;
                float k = 1f - Mathf.Clamp01(_fade / 0.45f);
                if (_cur != null) _cur.volume = Mathf.Lerp(_cur.volume, 0.55f * Volume, k * 0.35f);
                var other = _cur == _a ? _b : _a;
                other.volume = Mathf.Max(0f, other.volume - Time.deltaTime * 1.6f);
                if (other.volume <= 0.001f) other.Stop();
            }

            static AudioClip Clip(string track)
            {
                if (!_clips.TryGetValue(track, out var clip)) { clip = Compose(track); _clips[track] = clip; }
                return clip;
            }

            // ---- tiny tracker: semitone steps over a shared pulse, rendered once per track ----

            const int MRate = 22050;

            /// <summary>midi number -> Hz, A4 = 69.</summary>
            static float F(int midi) => 440f * Mathf.Pow(2f, (midi - 69) / 12f);

            /// <summary>Per-track compositions. Every row is a 16th-note step; -1 is a rest,
            /// -2 holds the previous note. Loops are 4-8 seconds of night music.</summary>
            static AudioClip Compose(string track)
            {
                switch (track)
                {
                    case "title":
                        // slow A-minor lullaby: sparse lead over a heartbeat bass
                        return Render(new Song
                        {
                            Step = 0.185f, BassWave = Wave.Tri, LeadWave = Wave.Square,
                            LeadVol = 0.16f, BassVol = 0.22f, HatVol = 0f, Pad = 0.10f,
                            Bass = Seq(57, -2, -2, -2, -1, -2, -2, -2, 53, -2, -2, -2, -1, -2, -2, -2,
                                       55, -2, -2, -2, -1, -2, -2, -2, 52, -2, -2, -2, -1, -2, -2, -2),
                            Lead = Seq(69, -1, -2, -1, 72, -1, -2, -1, 76, -1, -2, -1, 74, -1, 72, -1,
                                       69, -1, -2, -1, 67, -1, -2, -1, 64, -1, -2, -1, -1, -1, -1, -1)
                        });
                    case "explore":
                        // gentle folk pulse in D minor
                        return Render(new Song
                        {
                            Step = 0.16f, BassWave = Wave.Tri, LeadWave = Wave.Square,
                            LeadVol = 0.12f, BassVol = 0.2f, HatVol = 0.05f, Pad = 0.08f,
                            Bass = Seq(50, -2, -1, 50, -2, -1, 46, -2, 48, -2, -1, 48, -2, 45, -2, -1,
                                       50, -2, -1, 50, -2, -1, 53, -2, 48, -2, -1, 45, -2, 43, -2, -1),
                            Lead = Seq(-1, -1, 62, -1, -1, 65, -1, -1, -1, 64, -1, -1, 62, -1, -1, -1,
                                       -1, -1, 65, -1, -1, 69, -1, 67, -1, -1, 65, -1, -1, -1, -1, -1)
                        });
                    case "village":
                        // hearth-warm C major under the lamplight: open fifths in the bass,
                        // a music-box lead - home against the wandering folk pulse outside
                        return Render(new Song
                        {
                            Step = 0.18f, BassWave = Wave.Tri, LeadWave = Wave.Square,
                            LeadVol = 0.14f, BassVol = 0.2f, HatVol = 0.03f, Pad = 0.1f,
                            Bass = Seq(48, -2, -2, -1, 45, -2, -2, -1, 41, -2, -2, -1, 43, -2, -2, -1,
                                       48, -2, -2, -1, 45, -2, -2, -1, 43, -2, -2, -1, 40, -2, -2, -1),
                            Lead = Seq(72, -1, -2, 76, -1, -1, -2, -1, 74, -1, -2, 72, -1, -1, -2, -1,
                                       76, -1, -2, 79, -1, -1, -2, -1, 76, -1, -2, -1, -1, -1, -2, -1)
                        });
                    case "wood":
                        // the older trees close over the road: sparse low fifths and a wary
                        // little lead that answers only sometimes - the folk pulse at its
                        // most alone
                        return Render(new Song
                        {
                            Step = 0.16f, BassWave = Wave.Tri, LeadWave = Wave.Square,
                            LeadVol = 0.11f, BassVol = 0.18f, HatVol = 0.02f, Pad = 0.09f,
                            Bass = Seq(45, -2, -2, -2, -1, -2, -2, -1, 43, -2, -2, -2, -1, -2, -2, -1,
                                       45, -2, -2, -2, -1, -2, -2, -1, 40, -2, -2, -2, -1, -2, -2, -1),
                            Lead = Seq(-1, -1, -2, -1, 69, -1, -2, -1, -1, -1, -2, -1, -1, -1, -2, -1,
                                       -1, -1, -2, -1, 72, -1, -2, -1, 69, -1, -2, -1, -1, -1, -2, -1)
                        });
                    case "battle":
                        // driving D minor, eighth-note bass, short lead stabs
                        return Render(new Song
                        {
                            Step = 0.125f, BassWave = Wave.Square, LeadWave = Wave.Saw,
                            LeadVol = 0.15f, BassVol = 0.2f, HatVol = 0.07f, Pad = 0.05f,
                            Bass = Seq(38, -1, 38, -1, 38, -1, 41, -1, 38, -1, 38, -1, 43, -1, 41, -1,
                                       38, -1, 38, -1, 38, -1, 41, -1, 46, -1, 45, -1, 43, -1, 41, -1),
                            Lead = Seq(62, -1, -1, 62, -1, -1, 65, -1, 62, -1, -1, 69, -1, 67, -1, -1,
                                       62, -1, -1, 62, -1, -1, 65, -1, 70, -1, 69, -1, 67, -1, 65, -1)
                        });
                    case "boss":
                        // E minor, faster, chromatic drop at the end of every bar
                        return Render(new Song
                        {
                            Step = 0.115f, BassWave = Wave.Saw, LeadWave = Wave.Square,
                            LeadVol = 0.16f, BassVol = 0.22f, HatVol = 0.08f, Pad = 0.06f,
                            Bass = Seq(40, -1, 40, -1, 40, -1, 40, -1, 39, -1, 40, -1, 43, -1, 42, -1,
                                       40, -1, 40, -1, 40, -1, 40, -1, 44, -1, 43, -1, 42, -1, 39, -1),
                            Lead = Seq(64, -1, 67, -1, -1, 64, -1, -1, 63, -1, 64, -1, 67, -1, -1, -1,
                                       64, -1, 67, -1, -1, 71, -1, -1, 70, -1, 67, -1, 64, -1, -1, -1)
                        });
                    case "cinema":
                        // near-ambient: one swelling pad chord a bar, a lone bell
                        return Render(new Song
                        {
                            Step = 0.22f, BassWave = Wave.Tri, LeadWave = Wave.Tri,
                            LeadVol = 0.11f, BassVol = 0.16f, HatVol = 0f, Pad = 0.16f,
                            Bass = Seq(45, -2, -2, -2, -2, -2, -2, -2, 41, -2, -2, -2, -2, -2, -2, -2,
                                       43, -2, -2, -2, -2, -2, -2, -2, 38, -2, -2, -2, -2, -2, -2, -2),
                            Lead = Seq(-1, -1, -1, -1, 69, -2, -1, -1, -1, -1, -1, -1, 72, -2, -1, -1,
                                       -1, -1, -1, -1, 67, -2, -1, -1, -1, -1, -1, -1, 64, -2, -1, -1)
                        });
                    case "end":
                        // the resolve: warm F major, slow and bright
                        return Render(new Song
                        {
                            Step = 0.2f, BassWave = Wave.Tri, LeadWave = Wave.Square,
                            LeadVol = 0.14f, BassVol = 0.2f, HatVol = 0f, Pad = 0.12f,
                            Bass = Seq(41, -2, -2, -2, -1, -2, -2, -2, 48, -2, -2, -2, -1, -2, -2, -2,
                                       46, -2, -2, -2, -1, -2, -2, -2, 43, -2, -2, -2, -1, -2, -2, -2),
                            Lead = Seq(65, -1, -2, -1, 69, -1, -2, -1, 72, -1, -2, -1, 70, -1, 69, -1,
                                       65, -1, -2, -1, 72, -1, -2, -1, 74, -1, -2, -1, -1, -1, -1, -1)
                        });
                    default: return null;
                }
            }

            class Song
            {
                public float Step;
                public int[] Bass, Lead;
                public Wave BassWave, LeadWave;
                public float LeadVol, BassVol, HatVol, Pad;
            }

            static int[] Seq(params int[] notes) => notes;

            /// <summary>Renders a Song to a seamless AudioClip: lead voice, bass voice,
            /// a soft pad shadowing the bass line two octaves up, and a noise hat on offbeats.</summary>
            static AudioClip Render(Song s)
            {
                int steps = s.Bass.Length;
                int stepLen = Mathf.CeilToInt(s.Step * MRate);
                int n = stepLen * steps;
                var buf = new float[n];
                float lpv = 0f;                      // one-pole lowpass to take the edge off
                for (int i = 0; i < n; i++)
                {
                    int st = i / stepLen;
                    int k = i - st * stepLen;
                    float env = 1f - (float)k / stepLen;
                    env *= env;
                    float v = 0f;

                    float fB = NoteFreq(s.Bass, st);
                    if (fB > 0f)
                    {
                        v += Sfx.Sample(s.BassWave, fB * i / MRate) * s.BassVol * Mathf.Max(0.35f, env);
                        v += Sfx.Sample(Wave.Tri, fB * 4f * i / MRate) * s.Pad * env;   // pad shimmer
                    }
                    float fL = NoteFreq(s.Lead, st);
                    if (fL > 0f)
                        v += Sfx.Sample(s.LeadWave, fL * i / MRate) * s.LeadVol * env;

                    if (s.HatVol > 0f && st % 4 == 2 && k < stepLen * 0.4f)
                        v += (Mathf.Sin(i * 12.9898f) * 43758.5453f % 1f) * s.HatVol * env;

                    lpv += (v - lpv) * 0.42f;         // soften the square edges into "chiptune"
                    buf[i] = Mathf.Clamp(v * 0.7f + lpv * 0.3f, -0.9f, 0.9f);
                }
                // crossfade the tail into the head so the loop point does not click
                int xf = Mathf.Min(stepLen / 2, n / 4);
                for (int i = 0; i < xf; i++)
                {
                    float k = i / (float)xf;
                    buf[n - xf + i] = buf[n - xf + i] * (1f - k) + buf[i] * k;
                }
                var clip = AudioClip.Create("mus", n, 1, MRate, false);
                clip.SetData(buf, 0);
                return clip;
            }

            static float NoteFreq(int[] seq, int st)
            {
                int m = seq[st];
                if (m == -1) return 0f;
                while (m == -2 && st > 0) m = seq[--st];
                if (m < 0) return 0f;
                return F(m);
            }
        }
    }
}
