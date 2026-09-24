using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MoonThief
{
    /// <summary>English string table (Assets/Resources/Text/strings.txt is the single source).</summary>
    public static class Strings
    {
        static Dictionary<string, string> _map;

        static void Load()
        {
            if (_map != null) return;
            _map = new Dictionary<string, string>();
            var asset = Resources.Load<TextAsset>("Text/strings");
            if (asset == null) { Debug.LogError("Strings: Resources/Text/strings.txt missing"); return; }
            foreach (var raw in asset.text.Split('\n'))
            {
                var line = raw.TrimEnd('\r');
                if (line.Length == 0 || line[0] == '#') continue;
                int bar = line.IndexOf('|');
                if (bar < 1) continue;
                var value = line.Substring(bar + 1).Replace("\\n", "\n");
                _map[line.Substring(0, bar).Trim()] = value;
            }
        }

        public static string Get(string key)
        {
            Load();
            if (key == null) return "";
            if (_map.TryGetValue(key, out var s)) return s;
            // A missing key used to render as "[jr.beast.sub]", brackets and all, which reads as a
            // typo on screen. Fall back to the last word of the key, upper-cased, and say so once
            // in the log: tools/strings_audit.py is what finds these before a build.
            Debug.LogWarning("Strings: no entry for \"" + key + "\"");
            return Fallback(key);
        }

        /// <summary>"jr.slot.blade" -> "BLADE", so a key that slipped through the table still
        /// prints a word a player can read.</summary>
        static string Fallback(string key)
        {
            int dot = key.LastIndexOf('.');
            var tail = dot >= 0 && dot + 1 < key.Length ? key.Substring(dot + 1) : key;
            return tail.ToUpperInvariant();
        }

        public static string Get(string key, params object[] args)
        {
            var pattern = Get(key);
            try { return string.Format(pattern, args); }
            catch { return pattern; }
        }

        /// <summary>Is this key in the table? Anything that walks a numbered family (the story
        /// slides are ci.1, ci.2, ... until one is missing) has to ask, because a missing key now
        /// returns a readable word instead of a bracketed note -- so "the text starts with ["
        /// stopped being a way to detect the end of a sequence.</summary>
        public static bool Has(string key)
        {
            Load();
            return _map != null && key != null && _map.ContainsKey(key);
        }
    }

    /// <summary>Loads sprite frames out of Resources with natural frame ordering.</summary>
    public static class Bank
    {
        static readonly Dictionary<string, Sprite[]> _cache = new Dictionary<string, Sprite[]>();

        public static Sprite[] Frames(string path)
        {
            if (_cache.TryGetValue(path, out var cached)) return cached;

            var all = Resources.LoadAll<Sprite>(path);
            if (all == null || all.Length == 0)
            {
                Debug.LogWarning("Bank: no sprites at Resources/" + path);
                _cache[path] = new Sprite[0];
                return _cache[path];
            }
            Array.Sort(all, (a, b) => FrameIndex(a.name).CompareTo(FrameIndex(b.name)));
            _cache[path] = all;
            return all;
        }

        static int FrameIndex(string name)
        {
            int us = name.LastIndexOf('_');
            if (us >= 0 && us + 1 < name.Length)
            {
                int n;
                if (int.TryParse(name.Substring(us + 1), out n)) return n;
            }
            return 0;
        }

        public static Sprite One(string path)
        {
            var frames = Frames(path);
            return frames.Length > 0 ? frames[0] : null;
        }

        public static Sprite Frame(string path, int index)
        {
            var frames = Frames(path);
            if (frames.Length == 0) return null;
            return frames[Mathf.Clamp(index, 0, frames.Length - 1)];
        }
    }

    /// <summary>Procedurally generated sprites. The art pack ships no UI at all, so panels, bars,
    /// shadows and the moonless-sky icon are drawn here in code and tinted at runtime.</summary>
    public static class Tex
    {
        static Sprite _solid, _panel, _shadow, _moon, _spark, _chevron;

        static Sprite Make(string name, int w, int h, Func<int, int, Color32> pixel, Vector2 pivot, Vector4 border)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            { name = name, filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    px[y * w + x] = pixel(x, y);
            tex.SetPixels32(px);
            tex.Apply(false, false);
            return Sprite.Create(tex, new Rect(0, 0, w, h), pivot, PixelFont.PPU, 0, SpriteMeshType.FullRect, border);
        }

        public static Sprite Solid()
        {
            if (_solid == null)
                _solid = Make("solid", 1, 1, (x, y) => new Color32(255, 255, 255, 255), new Vector2(0.5f, 0.5f), Vector4.zero);
            return _solid;
        }

        /// <summary>9-sliced panel: translucent dark fill with a 1px bright border.</summary>
        public static Sprite Panel()
        {
            if (_panel == null)
            {
                _panel = Make("panel", 8, 8, (x, y) =>
                {
                    bool edge = x == 0 || y == 0 || x == 7 || y == 7;
                    return edge ? new Color32(196, 214, 255, 255) : new Color32(16, 14, 32, 232);
                }, new Vector2(0.5f, 0.5f), new Vector4(1, 1, 1, 1));
            }
            return _panel;
        }

        public static Sprite Shadow()
        {
            if (_shadow == null)
            {
                _shadow = Make("shadow", 16, 6, (x, y) =>
                {
                    float dx = (x - 7.5f) / 8f, dy = (y - 2.5f) / 3f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(1f - d);
                    a = a * a * 0.55f;
                    return new Color32(0, 0, 0, (byte)(a * 255f));
                }, new Vector2(0.5f, 0.5f), Vector4.zero);
            }
            return _shadow;
        }

        /// <summary>Empty night sky marker: dark disc with a dashed rim.</summary>
        public static Sprite Moon()
        {
            if (_moon == null)
            {
                _moon = Make("nomoon", 13, 13, (x, y) =>
                {
                    float dx = x - 6f, dy = y - 6f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    if (d > 6f) return new Color32(0, 0, 0, 0);
                    if (d > 4.6f)
                    {
                        bool dash = ((x + y) % 3) == 0;
                        return dash ? new Color32(150, 140, 190, 255) : new Color32(46, 42, 74, 255);
                    }
                    return new Color32(20, 18, 36, 255);
                }, new Vector2(0.5f, 0.5f), Vector4.zero);
            }
            return _moon;
        }

        public static Sprite Spark()
        {
            if (_spark == null)
            {
                _spark = Make("spark", 5, 5, (x, y) =>
                {
                    bool on = x == 2 || y == 2 || (Mathf.Abs(x - 2) == 1 && Mathf.Abs(y - 2) == 1);
                    return on ? new Color32(255, 255, 255, 255) : new Color32(255, 255, 255, 0);
                }, new Vector2(0.5f, 0.5f), Vector4.zero);
            }
            return _spark;
        }

        public static Sprite Chevron()
        {
            if (_chevron == null)
            {
                _chevron = Make("chevron", 5, 7, (x, y) =>
                {
                    int[] ink = { 4, 3, 2, 1, 2, 3, 4 };
                    return x == ink[y] - 1 ? new Color32(255, 255, 255, 255) : new Color32(255, 255, 255, 0);
                }, new Vector2(0.5f, 0.5f), Vector4.zero);
            }
            return _chevron;
        }
    }

    /// <summary>Frame animator for a SpriteRenderer, with optional bottom ("feet") anchoring so
    /// frames with different trimmed heights keep standing on the same ground line.</summary>
    public class Anim : MonoBehaviour
    {
        public SpriteRenderer Target;
        public Sprite[] Frames;
        public float Fps = 8f;
        public bool Loop = true;
        public bool AnchorFeet;
        public float GroundY;
        public float AnchorScale = 1f;   // transform scale of the renderer, used when anchoring feet
        public Action Done;

        float _t;
        int _index;
        bool _playing;
        Color _tint = Color.white;

        public bool Finished { get; private set; }
        public int Index => _index;

        public void Setup(SpriteRenderer target, bool anchorFeet, float groundY)
        {
            Target = target;
            AnchorFeet = anchorFeet;
            GroundY = groundY;
            _tint = target != null ? target.color : Color.white;
        }

        public void Play(Sprite[] frames, float fps, bool loop = true, Action onDone = null)
        {
            Frames = frames;
            Fps = fps;
            Loop = loop;
            Done = onDone;
            _t = 0f;
            _index = 0;
            _playing = frames != null && frames.Length > 0;
            Finished = false;
            Apply();
        }

        public void ShowFrame(int index)
        {
            _index = index;
            if (Frames != null && Frames.Length > 0) _index = Mathf.Clamp(index, 0, Frames.Length - 1);
            Apply();
        }

        public void SetTint(Color c)
        {
            _tint = c;
            if (Target != null) Target.color = c;
        }

        public Color Tint => _tint;

        void Apply()
        {
            if (Target == null) return;
            if (Frames == null || Frames.Length == 0) return;
            var s = Frames[Mathf.Clamp(_index, 0, Frames.Length - 1)];
            Target.sprite = s;
            Target.color = _tint;
            if (AnchorFeet && s != null)
            {
                var p = Target.transform.localPosition;
                Target.transform.localPosition = new Vector3(p.x, GroundY + s.bounds.extents.y * AnchorScale, p.z);
            }
        }

        void Update()
        {
            if (!_playing || Frames == null || Frames.Length == 0) return;
            _t += Time.deltaTime * Fps;
            while (_t >= 1f)
            {
                _t -= 1f;
                _index++;
                if (_index >= Frames.Length)
                {
                    if (Loop) _index = 0;
                    else
                    {
                        _index = Frames.Length - 1;
                        _playing = false;
                        Finished = true;
                        Apply();
                        var cb = Done;
                        Done = null;
                        if (cb != null) cb();
                        return;
                    }
                }
            }
            Apply();
        }
    }

    /// <summary>Small coroutine helpers. Every offset is snapped to a whole pixel (1/16 unit)
    /// so nothing ever renders between pixels.</summary>
    public static class Fx
    {
        public const float Pixel = 1f / 16f;

        public static float Snap(float v) => Mathf.Round(v / Pixel) * Pixel;

        /// <summary>Destroy that also works while previews are rendered from the editor.
        /// A plain Destroy is deferred to the end of the frame, and the editor render never
        /// advances a frame - so torn-down worlds survived and their text labels painted on
        /// top of the next chapter, which reads on screen as doubled, muddy type.</summary>
        public static void Kill(UnityEngine.Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(o);
            else UnityEngine.Object.DestroyImmediate(o);
        }

        public static IEnumerator Wait(float seconds)
        {
            float t = 0f;
            while (t < seconds) { t += Time.deltaTime; yield return null; }
        }

        public static IEnumerator MoveLocal(Transform t, Vector3 to, float duration)
        {
            if (t == null) yield break;
            var from = t.localPosition;
            float e = 0f;
            while (e < duration)
            {
                e += Time.deltaTime;
                if (t == null) yield break;   // rig destroyed mid-move (encounter swap)
                t.localPosition = Vector3.Lerp(from, to, Mathf.SmoothStep(0f, 1f, e / duration));
                yield return null;
            }
            if (t != null) t.localPosition = to;
        }

        public static IEnumerator BumpLocal(Transform t, Vector3 from, Vector3 to, float duration)
        {
            var original = from;
            yield return MoveLocal(t, to, duration * 0.35f);
            yield return MoveLocal(t, original, duration * 0.65f);
        }

        public static IEnumerator FlashTint(Anim anim, Color flash, int times, float onTime, float offTime)
        {
            if (anim == null) yield break;
            var baseColor = anim.Tint;
            for (int i = 0; i < times; i++)
            {
                if (anim == null) yield break;
                anim.SetTint(flash);
                yield return Wait(onTime);
                if (anim == null) yield break;
                anim.SetTint(baseColor);
                yield return Wait(offTime);
            }
        }

        public static IEnumerator Shake(Transform t, float pixels, float duration, float hz = 30f)
        {
            if (t == null || !Prefs.Shake) yield break;
            var basePos = t.localPosition;
            float e = 0f;
            while (e < duration)
            {
                e += Time.deltaTime;
                if (t == null) yield break;   // rig destroyed mid-shake (encounter swap)
                float amp = pixels * (1f - e / duration);
                var off = new Vector3(
                    Snap(UnityEngine.Random.Range(-amp, amp)),
                    Snap(UnityEngine.Random.Range(-amp, amp)), 0f);
                t.localPosition = basePos + off;
                yield return Wait(1f / hz);
            }
            if (t != null) t.localPosition = basePos;
        }

        public static IEnumerator Fade(Anim anim, Color to, float duration)
        {
            if (anim == null) yield break;
            var from = anim.Tint;
            float e = 0f;
            while (e < duration)
            {
                e += Time.deltaTime;
                if (anim == null) yield break;
                anim.SetTint(Color.Lerp(from, to, e / duration));
                yield return null;
            }
            if (anim != null) anim.SetTint(to);
        }

        /// <summary>A bright diagonal streak that flashes over the point a hit landed:
        /// pops small, swells, dies - a quarter second of impact.</summary>
        public static IEnumerator Slash(Transform parent, Vector3 pos, Color tint, float scale = 1f)
        {
            if (parent == null) yield break;
            var sr = SpriteRendererUtil.Make(parent, "slash", TexArt.Slash(), 120);
            sr.transform.localPosition = pos;
            sr.transform.localEulerAngles = new Vector3(0f, 0f, UnityEngine.Random.Range(-55f, -35f));
            sr.color = tint;
            float e = 0f;
            while (e < 0.17f)
            {
                e += Time.deltaTime;
                float k = Mathf.Clamp01(e / 0.17f);
                if (sr == null) yield break;
                sr.transform.localScale = Vector3.one * (0.5f + k * 1.9f) * scale;
                var c = sr.color; c.a = (1f - k) * tint.a; sr.color = c;
                yield return null;
            }
            Kill(sr);
        }

        public static IEnumerator Tween(float duration, Action<float> step)
        {
            float e = 0f;
            while (e < duration)
            {
                e += Time.deltaTime;
                step(Mathf.Clamp01(e / duration));
                yield return null;
            }
            step(1f);
        }
    }
}
