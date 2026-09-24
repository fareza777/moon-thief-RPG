using System.Collections.Generic;
using UnityEngine;

namespace MoonThief
{
    /// <summary>
    /// Bitmap font loader. Reads Assets/Resources/Text/font5x8.txt (the single source of truth
    /// for glyph shapes) and builds one small atlas texture + one Sprite per glyph.
    /// Everything is white so the renderer colour can tint it.
    ///
    /// The glyph box is 5x8: capitals and digits fill rows 0..6, lowercase sits on rows 2..6
    /// and its descenders use row 7. The extra row is what keeps g/p/q/y from riding a pixel
    /// above the baseline, which is the classic tell of a cheap bitmap font.
    /// </summary>
    public static class PixelFont
    {
        public const int GlyphW = 5;
        public const int GlyphH = 8;
        public const int CellW = 6;   // glyph + 1px gap
        public const int CellH = 9;   // glyph + 1px leading between lines
        public const int Cols = 8;
        public const float PPU = 16f; // same pixels-per-unit as the art pack

        static Dictionary<char, Sprite> _glyphs;
        static Texture2D _atlas;

        public static Sprite Get(char c)
        {
            Ensure();
            if (_glyphs.Count == 0) return null;
            if (_glyphs.TryGetValue(c, out var s) && s != null) return s;
            if (_glyphs.TryGetValue('?', out var q) && q != null) return q;
            if (_glyphs.TryGetValue(' ', out var blank)) return blank;
            foreach (var pair in _glyphs) return pair.Value;
            return null;
        }

        public static float Advance(int scale) => (CellW / PPU) * scale;
        public static float LineHeight(int scale) => (CellH / PPU) * scale;
        public static float GlyphWUnits(int scale) => (GlyphW / PPU) * scale;
        public static float GlyphHUnits(int scale) => (GlyphH / PPU) * scale;

        /// <summary>Width of the longest line, in world units.</summary>
        public static Vector2 Measure(string text, int scale)
        {
            if (string.IsNullOrEmpty(text)) return Vector2.zero;
            float widest = 0f;
            int lines = 1, col = 0;
            foreach (var c in text)
            {
                if (c == '\n') { widest = Mathf.Max(widest, col); lines++; col = 0; continue; }
                col++;
            }
            widest = Mathf.Max(widest, col);
            float w = widest <= 0 ? 0f : widest * Advance(scale) - (Advance(scale) - GlyphWUnits(scale));
            return new Vector2(w, lines * LineHeight(scale));
        }

        static void Ensure()
        {
            if (_glyphs != null) return;
            _glyphs = new Dictionary<char, Sprite>();

            var asset = Resources.Load<TextAsset>("Text/font5x8");
            if (asset == null)
            {
                Debug.LogError("PixelFont: Resources/Text/font5x8.txt not found.");
                return;
            }

            var defs = new List<KeyValuePair<char, string[]>>();
            foreach (var rawLine in asset.text.Split('\n'))
            {
                var line = rawLine.TrimEnd('\r').Trim();
                if (line.Length == 0 || line[0] == '#') continue;
                int bar = line.IndexOf('|');
                if (bar < 1) continue;   // single character glyphs are valid (A|..., ?|...)
                string key = line.Substring(0, bar);
                var rows = line.Substring(bar + 1).Split('/');
                if (rows.Length != GlyphH) continue;
                char ch = key == "space" ? ' ' : key[0];
                defs.Add(new KeyValuePair<char, string[]>(ch, rows));
            }

            int rowsCount = Mathf.CeilToInt(defs.Count / (float)Cols);
            int w = Cols * CellW;
            int h = Mathf.Max(rowsCount, 1) * CellH;
            _atlas = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                name = "PixelFontAtlas",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };

            var px = new Color32[w * h];
            var clear = new Color32(255, 255, 255, 0);
            for (int i = 0; i < px.Length; i++) px[i] = clear;

            var white = new Color32(255, 255, 255, 255);
            for (int i = 0; i < defs.Count; i++)
            {
                int cx = i % Cols, cy = i / Cols;
                var rows = defs[i].Value;
                for (int r = 0; r < GlyphH; r++)
                {
                    for (int c = 0; c < GlyphW; c++)
                    {
                        if (c >= rows[r].Length || rows[r][c] != '#') continue;
                        int texX = cx * CellW + c;
                        int texY = h - 1 - (cy * CellH + r);
                        if (texX < 0 || texX >= w || texY < 0 || texY >= h) continue;
                        px[texY * w + texX] = white;
                    }
                }
            }
            _atlas.SetPixels32(px);
            _atlas.Apply(false, false);

            for (int i = 0; i < defs.Count; i++)
            {
                int cx = i % Cols, cy = i / Cols;
                var rect = new Rect(cx * CellW, h - (cy * CellH) - GlyphH, GlyphW, GlyphH);
                var sprite = Sprite.Create(_atlas, rect, new Vector2(0f, 1f), PPU, 0, SpriteMeshType.FullRect);
                sprite.name = "g_" + (defs[i].Key == ' ' ? "space" : defs[i].Key.ToString());
                _glyphs[defs[i].Key] = sprite;
            }

            if (!_glyphs.ContainsKey(' '))
                Debug.LogError("PixelFont: the space glyph is missing from font5x8.txt");
            Debug.Log("PixelFont: " + _glyphs.Count + " glyphs loaded from Text/font5x8.txt");
        }
    }

    public enum TextAlign { Left, Center, Right }

    /// <summary>
    /// Draws a string with pooled SpriteRenderers using the bitmap font. The transform position is
    /// the anchor: top-left, top-centre or top-right depending on Align. Supports typewriter reveal
    /// and simple greedy word wrapping. Nothing here depends on any font asset or UI package.
    /// </summary>
    public class PixelLabel : MonoBehaviour
    {
        public string Text = "";
        public int Scale = 2;
        public Color Tint = Color.white;
        public TextAlign Align = TextAlign.Left;
        public int SortingOrder = 50;
        public float MaxWidthUnits = 0f;   // 0 = no wrapping
        public float RevealSpeed = 0f;     // chars per second, 0 = instant
        /// <summary>Keep the label on the pixel grid so no glyph row is ever squeezed away.
        /// Turn off for labels that are animated smoothly (floating numbers).</summary>
        public bool SnapToPixelGrid = true;
        /// <summary>One pixel drop shadow behind every glyph. Text that lands on art rather
        /// than on a panel (damage numbers, world banners) needs it or it turns to mush on a
        /// bright patch of background.</summary>
        public bool Shadow;
        public Color ShadowColor = new Color(0f, 0f, 0f, 0.78f);

        readonly List<SpriteRenderer> _pool = new List<SpriteRenderer>();
        readonly List<SpriteRenderer> _shadowPool = new List<SpriteRenderer>();
        Transform _root;
        string _shown = "";
        float _revealTimer;
        int _visible;

        public bool IsRevealing => RevealSpeed > 0f && _visible < _shown.Length;
        public int VisibleChars => _visible;

        public void Set(string text, bool instant = false)
        {
            Text = text ?? "";
            _shown = Wrap(Text);
            bool instantNow = instant || RevealSpeed <= 0f || !Application.isPlaying;
            _visible = instantNow ? _shown.Length : 0;
            _revealTimer = 0f;
            Rebuild();
        }

        public void SetColor(Color c)
        {
            Tint = c;
            for (int i = 0; i < _pool.Count; i++) _pool[i].color = c;
        }

        // ---- measurement ----
        // The transform position is the top of the first line, so callers can stack blocks of text
        // without knowing how many lines each one wrapped into.

        /// <summary>Number of lines the text occupies once wrapped at MaxWidthUnits.</summary>
        public int LineCount(string text)
        {
            string wrapped = Wrap(text ?? "");
            if (string.IsNullOrEmpty(wrapped)) return 0;
            int n = 1;
            for (int i = 0; i < wrapped.Length; i++) if (wrapped[i] == '\n') n++;
            return n;
        }

        /// <summary>Height in world units the wrapped text occupies, measured down from the anchor.</summary>
        public float MeasureHeight(string text)
        {
            string wrapped = Wrap(text ?? "");
            return string.IsNullOrEmpty(wrapped) ? 0f : PixelFont.Measure(wrapped, Scale).y;
        }

        /// <summary>Width in world units of the widest wrapped line.</summary>
        public float MeasureWidth(string text)
        {
            string wrapped = Wrap(text ?? "");
            return string.IsNullOrEmpty(wrapped) ? 0f : PixelFont.Measure(wrapped, Scale).x;
        }

        public PixelLabel Configure(int scale, Color tint, TextAlign align, int sortingOrder)
        {
            Scale = scale; Tint = tint; Align = align; SortingOrder = sortingOrder;
            return this;
        }

        void Awake() { EnsureRoot(); }

        void EnsureRoot()
        {
            if (_root != null) return;
            var go = new GameObject("glyphs");
            _root = go.transform;
            _root.SetParent(transform, false);
        }

        void Update()
        {
            if (!IsRevealing) return;
            _revealTimer += Time.deltaTime * RevealSpeed;
            int want = Mathf.Min(_shown.Length, Mathf.FloorToInt(_revealTimer));
            if (want != _visible)
            {
                _visible = want;
                Rebuild();
            }
        }

        void LateUpdate()
        {
            if (!SnapToPixelGrid) return;
            // Snap the WORLD position, not the local one: most labels ride a parent that is
            // itself moving (the world, the camera, a battle rig), so a local snap left the
            // glyphs sitting between two pixels - strokes came out one pixel thicker on one
            // row and thinner on the next, which is what makes bitmap text look cheap.
            var w = transform.position;
            var snapped = new Vector3(Fx.Snap(w.x), Fx.Snap(w.y), w.z);
            if (snapped != w) transform.position = snapped;
        }

        public void Rebuild()
        {
            EnsureRoot();

            string text = _shown;
            var lines = new List<string>();
            int start = 0;
            for (int i = 0; i <= text.Length; i++)
            {
                if (i == text.Length || text[i] == '\n')
                {
                    lines.Add(text.Substring(start, i - start));
                    start = i + 1;
                }
            }

            float advance = PixelFont.Advance(Scale);
            float lineH = PixelFont.LineHeight(Scale);

            int used = 0;
            int visibleLeft = Mathf.Clamp(_visible, 0, text.Length);
            int consumed = 0;            float shadowStep = Fx.Pixel * Scale;   // exactly one font pixel, never a fraction

            for (int li = 0; li < lines.Count; li++)
            {
                string line = lines[li];
                int lineVisible = Mathf.Clamp(visibleLeft - consumed, 0, line.Length);
                consumed += line.Length + 1; // +newline

                float lineW = line.Length <= 0 ? 0f : line.Length * advance - (advance - PixelFont.GlyphWUnits(Scale));
                // Snap the line origin to the pixel grid: centring a line with an odd character
                // count otherwise lands on a half pixel and the glyph strokes come out uneven.
                float x0 = Fx.Snap(Align == TextAlign.Left ? 0f : Align == TextAlign.Center ? -lineW * 0.5f : -lineW);
                float y0 = -li * lineH;

                for (int ci = 0; ci < line.Length; ci++)
                {
                    int slot = used++;
                    var sr = Get(slot);
                    bool on = ci < lineVisible;
                    bool ink = on && line[ci] != ' ';
                    sr.enabled = ink;
                    var shadow = Shadow ? ShadowGet(slot) : null;
                    if (shadow != null) shadow.enabled = ink;
                    if (!on) continue;
                    sr.sprite = PixelFont.Get(line[ci]);
                    sr.color = Tint;
                    sr.sortingOrder = SortingOrder;
                    var at = new Vector3(x0 + ci * advance, y0, 0f);
                    sr.transform.localPosition = at;
                    sr.transform.localScale = new Vector3(Scale, Scale, 1f);
                    if (shadow != null)
                    {
                        shadow.sprite = sr.sprite;
                        shadow.color = ShadowColor;
                        shadow.sortingOrder = SortingOrder - 1;
                        shadow.transform.localPosition = new Vector3(at.x + shadowStep, at.y - shadowStep, 0f);
                        shadow.transform.localScale = new Vector3(Scale, Scale, 1f);
                    }
                }
            }

            for (int i = used; i < _pool.Count; i++) _pool[i].enabled = false;
            if (Shadow)
                for (int i = used; i < _shadowPool.Count; i++) _shadowPool[i].enabled = false;
        }

        SpriteRenderer Get(int index)
        {
            while (_pool.Count <= index)
            {
                var go = new GameObject("c" + _pool.Count);
                go.transform.SetParent(_root, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sortingOrder = SortingOrder;
                _pool.Add(sr);
            }
            return _pool[index];
        }

        SpriteRenderer ShadowGet(int index)
        {
            while (_shadowPool.Count <= index)
            {
                var go = new GameObject("sh" + _shadowPool.Count);
                go.transform.SetParent(_root, false);
                go.transform.SetSiblingIndex(0);   // shadows always draw behind their glyphs
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sortingOrder = SortingOrder - 1;
                _shadowPool.Add(sr);
            }
            return _shadowPool[index];
        }

        string Wrap(string text)
        {
            if (MaxWidthUnits <= 0f || string.IsNullOrEmpty(text)) return text;
            float advance = PixelFont.Advance(Scale);
            int maxChars = Mathf.Max(1, Mathf.FloorToInt((MaxWidthUnits + (advance - PixelFont.GlyphWUnits(Scale))) / advance));
            var sb = new System.Text.StringBuilder();
            foreach (var raw in text.Split('\n'))
            {
                var words = raw.Split(' ');
                int lineLen = 0;
                for (int i = 0; i < words.Length; i++)
                {
                    var w = words[i];
                    int extra = lineLen == 0 ? w.Length : w.Length + 1;
                    if (lineLen > 0 && lineLen + extra > maxChars)
                    {
                        sb.Append('\n');
                        lineLen = 0;
                    }
                    if (lineLen > 0) { sb.Append(' '); lineLen++; }
                    sb.Append(w);
                    lineLen += w.Length;
                }
                sb.Append('\n');
            }
            var result = sb.ToString();
            return result.EndsWith("\n") ? result.Substring(0, result.Length - 1) : result;
        }
    }
}
