using System.Collections.Generic;
using UnityEngine;

namespace MoonThief
{
    /// <summary>
    /// Loads textures from Resources and slices them in code. The pack's importer metadata
    /// ships with empty sprite sheets, so slicing happens here instead of in the editor.
    /// Every query is cached. Procedural UI art (panels, glows, the moon) is built here too.
    /// </summary>
    public static class TexArt
    {
        static readonly Dictionary<string, Texture2D> _tex = new Dictionary<string, Texture2D>();
        static readonly Dictionary<string, Sprite> _sprite = new Dictionary<string, Sprite>();
        static readonly Dictionary<string, Sprite[]> _grid = new Dictionary<string, Sprite[]>();

        // ---------------------------------------------------------------- textures

        public static Texture2D Tex(string path)
        {
            if (_tex.TryGetValue(path, out var t)) return t;
            var load = Resources.Load<Texture2D>(path);
            if (load == null)
            {
                Debug.LogError("TexArt: missing texture Resources/" + path);
                _tex[path] = null;
                return null;
            }
            Debug.Log("[TexArt] " + path + " imported " + load.width + "x" + load.height);
            // the pack ships non-readable imports; grab a readable copy for slicing at runtime
            var rt = RenderTexture.GetTemporary(load.width, load.height, 0, RenderTextureFormat.ARGB32);
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            Graphics.Blit(load, rt);
            var readable = new Texture2D(load.width, load.height, TextureFormat.RGBA32, false)
            { name = load.name, filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            readable.ReadPixels(new Rect(0, 0, load.width, load.height), 0, 0);
            readable.Apply(false, false);
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            _tex[path] = readable;
            return readable;
        }

        /// <summary>One named frame out of a square grid sheet.</summary>
        public static Sprite Cell(string path, int cellPx, int col, int row) => Cell(path, cellPx, cellPx, col, row);

        /// <summary>One frame out of a sheet whose rows are taller than they are wide. The chara
        /// sheets are 3x4 cells of 16x20, and slicing them with a square cell walks the window
        /// down the sheet 4 px per row: the second facing came out with its head cut off and the
        /// feet of the facing below it. The pack sets the art flush to the bottom of every strip,
        /// which is why the window has to be the strip.</summary>
        public static Sprite Cell(string path, int cellW, int cellH, int col, int row)
        {
            string key = path + "#" + cellW + "x" + cellH + ":" + col + "," + row;
            if (_sprite.TryGetValue(key, out var s)) return s;
            var t = Tex(path);
            if (t == null) { _sprite[key] = null; return null; }
            int x = col * cellW, y = t.height - (row + 1) * cellH;
            s = Sprite.Create(t, new Rect(x, y, cellW, cellH), new Vector2(0.5f, 0.5f), G.PPU, 0, SpriteMeshType.FullRect);
            s.name = path + "_" + col + "_" + row;
            _sprite[key] = s;
            return s;
        }

        /// <summary>Every cell of a grid sheet, left-to-right top-to-bottom, as sprites.</summary>
        public static Sprite[] Grid(string path, int cellW, int cellH)
        {
            string key = path + "@" + cellW + "x" + cellH;
            if (_grid.TryGetValue(key, out var cached)) return cached;
            var t = Tex(path);
            if (t == null) { _grid[key] = new Sprite[0]; return _grid[key]; }
            int cols = t.width / cellW, rows = t.height / cellH;
            var list = new List<Sprite>(cols * rows);
            for (int r = 0; r < rows; r++)
                for (int c = 0; c < cols; c++)
                {
                    var s = Sprite.Create(t, new Rect(c * cellW, t.height - (r + 1) * cellH, cellW, cellH),
                        new Vector2(0.5f, 0.5f), G.PPU, 0, SpriteMeshType.FullRect);
                    s.name = path + "_" + (r * cols + c);
                    list.Add(s);
                }
            var arr = list.ToArray();
            _grid[key] = arr;
            return arr;
        }

        /// <summary>The chest sheets are 72x32: four 18x32 frames of one chest family -- three
        /// closed designs and the same chest with its lid up. They were sliced 24 wide, read off
        /// the file name ("chest_01_16x32"), so every frame carried four columns of the *next*
        /// chest and every chest in the world was drawn with half a box sitting beside it.
        /// The crop keeps the art and drops the empty rows above it, and the pivot sits on the
        /// bottom edge: all four frames put their base on the last row, so a chest with a bottom
        /// pivot stands on the ground line it is placed at.</summary>
        public static Sprite[] ChestFrames(string path)
        {
            string key = path + "@chest";
            if (_grid.TryGetValue(key, out var cached)) return cached;
            var t = Tex(path);
            if (t == null) { _grid[key] = new Sprite[0]; return _grid[key]; }
            const int CellW = 18, ArtH = 22;      // the tallest lid in the set is 22 rows
            int cols = t.width / CellW;
            var list = new List<Sprite>(cols);
            for (int c = 0; c < cols; c++)
            {
                var s = Sprite.Create(t, new Rect(c * CellW, 0, CellW, ArtH),
                    new Vector2(0.5f, 0f), G.PPU, 0, SpriteMeshType.FullRect);
                s.name = path + "_" + c;
                list.Add(s);
            }
            var arr = list.ToArray();
            _grid[key] = arr;
            return arr;
        }

        /// <summary>A whole PNG as one sprite (battlers, houses, backdrops).</summary>
        public static Sprite Whole(string path)
        {
            if (_sprite.TryGetValue(path, out var s)) return s;
            var t = Tex(path);
            if (t == null) { _sprite[path] = null; return null; }
            s = Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, 0.5f), G.PPU, 0, SpriteMeshType.FullRect);
            s.name = path;
            _sprite[path] = s;
            return s;
        }

        // ---------------------------------------------------------------- world art

        /// <summary>Legacy atlas tile id -> sprite (16 px grid, row-major from the top).</summary>
        public static Sprite Tile(int id)
        {
            return Cell("Art/Env/legacy_atlas", 16, id % AtlasCols, id / AtlasCols);
        }

        // ------------------------------------------------------- world atlas + interiors

        public const int AtlasCols = 74;      // 1184 / 16: the pack's ground atlas grid
        public const int AtlasRows = 130;
        /// <summary>First tile id of the row appended to the world atlas. Appending a ROW (not a
        /// column) keeps every existing tile id valid: the ids are row-major, so extra columns
        /// would renumber the whole pack.</summary>
        public const int InteriorBase = AtlasRows * AtlasCols;

        static Texture2D _world;

        /// <summary>The world atlas: the pack's ground atlas with one extra row of tiles drawn in
        /// code. The ground and decor meshes are single meshes on one texture, so the interior
        /// floor, wall and void tiles have to live in the same image as the outdoor tiles.</summary>
        public static Texture2D WorldAtlas()
        {
            if (_world != null) return _world;
            var a = Tex("Art/Env/legacy_atlas");
            if (a == null) return null;

            int w = a.width;
            int h = a.height + 16;
            var src = a.GetPixels32();
            var px = new Color32[w * h];
            // Unity's pixel rows run bottom-up: index 0 is the bottom of the image, which is
            // where the appended row goes (atlas row AtlasRows, counted from the top).
            for (int y = 0; y < 16; y++)
                for (int x = 0; x < w; x++)
                    px[y * w + x] = InteriorPixel(x % 16, 15 - y, x / 16);
            for (int y = 0; y < a.height; y++)
                for (int x = 0; x < a.width; x++)
                    px[(y + 16) * w + x] = src[y * a.width + x];

            _world = new Texture2D(w, h, TextureFormat.RGBA32, false)
            { name = "worldAtlas", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            _world.SetPixels32(px);
            _world.Apply(false, false);
            return _world;
        }

        /// <summary>The five procedural tiles, in id order from InteriorBase: two floorboards,
        /// the wall and its footing, and the void the interior sits in.
        /// <paramref name="index"/> is the tile within the row.</summary>
        static Color32 InteriorPixel(int x, int y, int index)
        {
            switch (index)
            {
                case 0:   // floorboard, planks running horizontally
                case 1:
                {
                    int off = index == 1 ? 5 : 0;
                    int band = (y + off) / 4;
                    int xo = (x + (band % 2) * 7) % 16;
                    bool seam = ((y + off) % 4) == 3 || xo == 15;
                    if (seam) return new Color32(58, 41, 25, 255);
                    int grain = ((x * 5 + y * 11 + index * 3) % 13) == 0 ? -14 : 0;
                    return new Color32((byte)(124 + grain), (byte)(92 + grain), (byte)(58 + grain), 255);
                }
                case 2:   // plastered wall with a beam
                case 3:
                {
                    if (index == 3 && y < 4) return new Color32(46, 34, 30, 255);   // footing
                    if (y % 8 == 0) return new Color32(58, 44, 38, 255);           // beam
                    int speck = ((x * 7 + y * 3) % 17) == 0 ? -10 : 0;
                    int tint = (y / 8) % 2 == 0 ? 0 : -6;
                    return new Color32((byte)(120 + speck + tint), (byte)(104 + speck + tint),
                        (byte)(92 + speck + tint), 255);
                }
                default:  // the void beyond the masonry
                {
                    int n = ((x * 13 + y * 29) % 23) == 0 ? 6 : 0;
                    return new Color32((byte)(7 + n), (byte)(7 + n), (byte)(13 + n), 255);
                }
            }
        }

        // ------------------------------------------------------- interior furniture

        static Sprite _bed, _table, _rug, _shelf, _window;

        /// <summary>A bed, 16x32: frame, blanket with a turned edge, and a pillow.</summary>
        public static Sprite Bed()
        {
            if (_bed == null)
            {
                _bed = Make("bed", 16, 32, (x, y) =>
                {
                    var frame = new Color32(96, 66, 40, 255);
                    var blanket = new Color32(126, 74, 96, 255);
                    var blanketHi = new Color32(150, 92, 112, 255);
                    var sheet = new Color32(214, 206, 186, 255);
                    if (x == 0 || x == 15 || y == 0 || y == 31) return frame;
                    if (y < 9) return new Color32(198, 190, 172, 255);            // pillow end
                    if (y == 9 || y == 10) return blanketHi;
                    return ((x + y) % 7 == 0) ? blanketHi : blanket;
                }, Vector4.zero);
            }
            return _bed;
        }

        /// <summary>A table, 32x16: plank top on four legs, with a shadow line under the top.</summary>
        public static Sprite Table()
        {
            if (_table == null)
            {
                _table = Make("table", 32, 16, (x, y) =>
                {
                    if (y >= 12)                                              // legs
                        return (x == 2 || x == 3 || x == 28 || x == 29)
                            ? new Color32(84, 56, 34, 255) : new Color32(0, 0, 0, 0);
                    if (y >= 10) return new Color32(70, 47, 28, 255);          // top edge in shadow
                    var top = ((x * 3 + y) % 9) == 0 ? new Color32(132, 96, 60, 255) : new Color32(116, 84, 52, 255);
                    return (x == 0 || x == 31) ? new Color32(84, 58, 34, 255) : top;
                }, Vector4.zero);
            }
            return _table;
        }

        /// <summary>A rug, 32x24: dark border, woven pattern inside.</summary>
        public static Sprite Rug()
        {
            if (_rug == null)
            {
                _rug = Make("rug", 32, 24, (x, y) =>
                {
                    var border = new Color32(78, 40, 44, 255);
                    var mid = new Color32(112, 58, 62, 255);
                    var knot = new Color32(148, 86, 78, 255);
                    bool edge = x < 2 || y < 2 || x > 29 || y > 21;
                    if (edge) return border;
                    bool ring = x == 4 || y == 4 || x == 27 || y == 21;
                    if (ring) return knot;
                    return ((x + y * 2) % 6 == 0) ? knot : mid;
                }, Vector4.zero);
            }
            return _rug;
        }

        /// <summary>A shelf, 16x24: two boards of books with coloured spines.</summary>
        public static Sprite Shelf()
        {
            if (_shelf == null)
            {
                var spines = new[]
                {
                    new Color32(150, 74, 62, 255), new Color32(86, 104, 150, 255),
                    new Color32(120, 140, 78, 255), new Color32(178, 146, 74, 255),
                };
                _shelf = Make("shelf", 16, 24, (x, y) =>
                {
                    var board = new Color32(92, 62, 38, 255);
                    if (x == 0 || x == 15) return board;
                    if (y % 12 == 0 || y % 12 == 11) return board;                  // two boards
                    int shelf = y / 12;
                    int book = (x / 3) + shelf * 5;
                    bool gap = (x % 3) == 2;
                    if (gap) return new Color32(58, 40, 26, 255);
                    return spines[book % spines.Length];
                }, Vector4.zero);
            }
            return _shelf;
        }

        /// <summary>A lit window, 16x16: frame, panes and a warm sill.</summary>
        public static Sprite Window()
        {
            if (_window == null)
            {
                _window = Make("window", 16, 16, (x, y) =>
                {
                    var frame = new Color32(88, 60, 38, 255);
                    if (x < 2 || x > 13 || y < 2 || y > 12) return frame;
                    if (x == 7 || x == 8 || y == 7) return frame;                    // mullions
                    int glow = (x * 3 + y * 5) % 7 == 0 ? 18 : 0;
                    return new Color32((byte)(206 + glow / 2), (byte)(174 + glow), (byte)(104 + glow), 255);
                }, Vector4.zero);
            }
            return _window;
        }

        /// <summary>One frame of a villager/monster sheet. The pack stores the four facings as four
        /// 16x20 strips, top to bottom: front, left, right, back. Two things were wrong here and
        /// both were visible on every villager in the village: the square-cell slice above, and the
        /// facing order -- Dir.Down is 0 and was reading the *bottom* strip, so the whole cast
        /// walked backwards (their back to the camera while walking towards the player). The strips
        /// are counted from the bottom here because that is how Sprite.Create measures y.</summary>
        public static Sprite Chara(string sheet, int dir, int frame)
        {
            // Dir: Down 0, Left 1, Right 2, Up 3  ->  strips from the bottom: Up, Right, Left, Down
            int row = 3 - Mathf.Clamp(dir, 0, 3);
            return Cell(sheet, 16, 20, frame, row);
        }

        public static Sprite MapMonster(string sheet, int frame)
            => Cell(sheet, 48, frame % 3, frame / 3);

        /// <summary>The speaker's face: the head of the first Down frame of their own chara
        /// sheet. A portrait is a person, not a weather vane - cropping the head means every
        /// villager keeps the face the pack drew for them, and no two speakers look alike.</summary>
        public static Sprite Face(string sheet)
        {
            string key = sheet + "@face";
            if (_sprite.TryGetValue(key, out var s)) return s;
            var t = Tex(sheet);
            if (t == null) { _sprite[key] = null; return null; }
            const int CellW = 16, CellH = 20, HeadH = 12;
            // the Down strip is the bottom row of cells; the head is the top of that cell
            float cellY = Mathf.Max(0, t.height - 4 * CellH);
            s = Sprite.Create(t, new Rect(0, cellY + CellH - HeadH, CellW, HeadH),
                new Vector2(0.5f, 0.45f), G.PPU, 0, SpriteMeshType.FullRect);
            s.name = key;
            _sprite[key] = s;
            return s;
        }

        /// <summary>The step in front of a house door: a worn mat lying flat on the trigger cell.
        /// The house art paints its own door in the facade, so this - not a second door sprite - is
        /// what marks the tile a house is entered from.</summary>
        public static Sprite DoorMat()
        {
            if (_mat == null)
            {
                var border = new Color32(178, 152, 106, 255);
                var weave = new Color32(88, 66, 44, 255);
                var dark = new Color32(60, 44, 28, 255);
                var clear = new Color32(0, 0, 0, 0);
                _mat = Make("doormat", 16, 10, (x, y) =>
                {
                    if (x == 0 || y == 0 || x == 15 || y == 9) return border;
                    return ((x + y) & 3) == 0 ? dark : weave;
                }, Vector4.zero);
            }
            return _mat;
        }

        /// <summary>Headwear for the three friends -- three shapes, not three colours. A straw hat,
        /// a hood and a feathered cap; at 16 px wide it covers the head of a hero frame (the head
        /// fills the top 11 of its 22 rows) at whatever scale the rig runs.</summary>
        public static Sprite Headwear(int kind)
        {
            if (_wear == null) _wear = new Sprite[3];
            if (kind < 0 || kind >= 3) return null;
            if (_wear[kind] != null) return _wear[kind];

            var straw = new Color32(224, 196, 124, 255);
            var strawDark = new Color32(168, 132, 72, 255);
            var band = new Color32(120, 74, 44, 255);
            var hood = new Color32(58, 82, 118, 255);
            var hoodRim = new Color32(196, 214, 236, 255);
            var cap = new Color32(92, 132, 78, 255);
            var capDark = new Color32(58, 90, 52, 255);
            var plume = new Color32(204, 72, 68, 255);
            var clear = new Color32(0, 0, 0, 0);

            _wear[kind] = Make("headwear" + kind, 16, 8, (x, y) =>
            {
                switch (kind)
                {
                    case 0:   // straw hat: wide brim, then a narrow crown over it
                        if (y <= 1) return (y == 0 || x < 2 || x > 13) ? strawDark : straw;
                        if (y == 2) return (x >= 4 && x <= 11) ? band : clear;
                        return (x >= 4 && x <= 11) ? straw : clear;
                    case 1:   // hood: an arch with a pale rim, worn back so the face still shows
                        if (y <= 1) return (x >= 1 && x <= 14) ? hood : clear;
                        else
                        {
                            int inset = 4 - Mathf.Min(3, y - 2);
                            if (x < inset || x > 15 - inset) return clear;
                            return (y == 2 || x == inset || x == 15 - inset) ? hoodRim : hood;
                        }
                    default:  // feathered cap: brim, crown and one plume leaning right
                        if (y <= 1) return (x >= 3 && x <= 12) ? capDark : clear;
                        if (x >= 10 && y >= 4) return plume;
                        return (x >= 4 && x <= 11) ? cap : clear;
                }
            }, Vector4.zero);
            return _wear[kind];
        }

        // ---------------------------------------------------------------- procedural UI

        static Sprite _solid, _panel, _shadow, _glow, _moon, _spark, _chevron, _moonFull, _ring, _dot, _slash;
        static Sprite _mat;
        static Sprite[] _wear;
        static Sprite _night, _vignette, _stars, _star, _alert;

        static Sprite Make(string name, int w, int h, System.Func<int, int, Color32> pixel, Vector4 border)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            { name = name, filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    px[y * w + x] = pixel(x, y);
            tex.SetPixels32(px);
            tex.Apply(false, false);
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), G.PPU, 0, SpriteMeshType.FullRect, border);
        }

        public static Sprite Solid()
        {
            if (_solid == null)
                _solid = Make("solid", 1, 1, (x, y) => new Color32(255, 255, 255, 255), Vector4.zero);
            return _solid;
        }

        /// <summary>Dark 9-sliced panel with a 1 px parchment border, used for every box.
        /// Fill is fully opaque so backdrop/busy scenes can never ghost through text.</summary>
        public static Sprite Panel()
        {
            if (_panel == null)
            {
                _panel = Make("panel", 8, 8, (x, y) =>
                {
                    bool edge = x == 0 || y == 0 || x == 7 || y == 7;
                    return edge ? new Color32(196, 214, 255, 255) : new Color32(16, 14, 32, 255);
                }, new Vector4(1, 1, 1, 1));
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
                    return new Color32(0, 0, 0, (byte)(a * a * 0.5f * 255f));
                }, Vector4.zero);
            }
            return _shadow;
        }

        /// <summary>Radial warm glow for torches / windows / the returning moonlight.</summary>
        public static Sprite Glow()
        {
            if (_glow == null)
            {
                _glow = Make("glow", 32, 32, (x, y) =>
                {
                    float dx = (x - 15.5f) / 16f, dy = (y - 15.5f) / 16f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(1f - d);
                    return new Color32(255, 214, 140, (byte)(a * a * 190f));
                }, Vector4.zero);
            }
            return _glow;
        }

        /// <summary>A thin bright diagonal streak - the instant a hit lands.</summary>
        public static Sprite Slash()
        {
            if (_slash == null)
            {
                _slash = Make("slash", 16, 16, (x, y) =>
                {
                    int d = Mathf.Abs(x + y - 15);   // distance off the hot diagonal
                    if (d == 0) return new Color32(255, 255, 255, 235);
                    if (d == 1) return new Color32(255, 238, 190, 140);
                    return new Color32(0, 0, 0, 0);
                }, Vector4.zero);
            }
            return _slash;
        }

        /// <summary>The empty-sky icon: dark disc, dashed rim.</summary>
        public static Sprite MoonEmpty()
        {
            if (_moon == null)
            {
                _moon = Make("moonEmpty", 13, 13, (x, y) =>
                {
                    float dx = x - 6f, dy = y - 6f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    if (d > 6f) return new Color32(0, 0, 0, 0);
                    if (d > 4.6f)
                        return ((x + y) % 3) == 0 ? new Color32(150, 140, 190, 255) : new Color32(46, 42, 74, 255);
                    return new Color32(20, 18, 36, 255);
                }, Vector4.zero);
            }
            return _moon;
        }

        /// <summary>The full moon for the ending.</summary>
        public static Sprite MoonFull()
        {
            if (_moonFull == null)
            {
                _moonFull = Make("moonFull", 13, 13, (x, y) =>
                {
                    float dx = x - 6f, dy = y - 6f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    if (d > 6.2f) return new Color32(0, 0, 0, 0);
                    if (d > 5.2f) return new Color32(255, 244, 200, 255);
                    // round maria: fixed centres + radii instead of the old hash speckle
                    var craters = new (float cx, float cy, float r)[]
                    {
                        (4.2f, 4.0f, 1.7f),
                        (8.1f, 7.6f, 1.2f),
                        (5.6f, 8.9f, 0.8f),
                        (8.8f, 3.8f, 0.6f),
                    };
                    foreach (var c in craters)
                    {
                        float cd = Mathf.Sqrt((x - c.cx) * (x - c.cx) + (y - c.cy) * (y - c.cy));
                        if (cd <= c.r) return new Color32(222, 202, 164, 255);
                        if (cd <= c.r + 0.9f) return new Color32(242, 226, 186, 255);
                    }
                    return new Color32(255, 240, 196, 255);
                }, Vector4.zero);
            }
            return _moonFull;
        }

        public static Sprite Spark()
        {
            if (_spark == null)
            {
                _spark = Make("spark", 5, 5, (x, y) =>
                {
                    bool on = x == 2 || y == 2 || (Mathf.Abs(x - 2) == 1 && Mathf.Abs(y - 2) == 1);
                    return on ? new Color32(255, 255, 255, 255) : new Color32(255, 255, 255, 0);
                }, Vector4.zero);
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
                }, Vector4.zero);
            }
            return _chevron;
        }

        static Sprite _icoAtk, _icoFriend, _icoFood, _icoRun, _icoPlay, _icoBook, _icoSave, _icoGear, _icoDoor,
            _icoText, _icoNote, _icoSpeaker, _icoShake, _icoAuto, _icoBack, _icoTrash,
            _icoPerson, _icoBag, _icoShield, _icoPaw, _icoScroll,
            _icoStar, _icoShare, _icoInfo, _icoMoon, _icoBell, _icoCheck, _icoX, _icoCoin, _icoPlus, _icoDrop;

        /// <summary>Tiny pictogram beside each battle command so the four cells read at a
        /// glance: sword for strike, heart for befriend, apple for morsel, boot for run —
        /// then the pause rows: play, book, floppy, gear, door.</summary>
        public static Sprite MenuIcon(int kind)
        {
            switch (kind)
            {
                case 0: return _icoAtk ??= MaskIcon("icoAtk", MaskSword);
                case 1: return _icoFriend ??= MaskIcon("icoFriend", MaskHeart);
                case 2: return _icoFood ??= MaskIcon("icoFood", MaskApple);
                case 3: return _icoRun ??= MaskIcon("icoRun", MaskBoot);
                case 4: return _icoPlay ??= MaskIcon("icoPlay", MaskPlay);
                case 5: return _icoBook ??= MaskIcon("icoBook", MaskBook);
                case 6: return _icoSave ??= MaskIcon("icoSave", MaskSave);
                case 7: return _icoGear ??= MaskIcon("icoGear", MaskGear);
                case 8: return _icoDoor ??= MaskIcon("icoDoor", MaskDoor);
                case 9: return _icoText ??= MaskIcon("icoText", MaskText);
                case 10: return _icoNote ??= MaskIcon("icoNote", MaskNote);
                case 11: return _icoSpeaker ??= MaskIcon("icoSpeaker", MaskSpeaker);
                case 12: return _icoShake ??= MaskIcon("icoShake", MaskShake);
                case 13: return _icoAuto ??= MaskIcon("icoAuto", MaskAuto);
                case 14: return _icoBack ??= MaskIcon("icoBack", MaskBack);
                case 15: return _icoTrash ??= MaskIcon("icoTrash", MaskTrash);
                case 16: return _icoPerson ??= MaskIcon("icoPerson", MaskPerson);
                case 17: return _icoBag ??= MaskIcon("icoBag", MaskBag);
                case 18: return _icoShield ??= MaskIcon("icoShield", MaskShield);
                case 19: return _icoPaw ??= MaskIcon("icoPaw", MaskPaw);
                case 20: return _icoScroll ??= MaskIcon("icoScroll", MaskScroll);
                case 21: return _icoStar ??= MaskIcon("icoStar", MaskStar);
                case 22: return _icoShare ??= MaskIcon("icoShare", MaskShare);
                case 23: return _icoInfo ??= MaskIcon("icoInfo", MaskInfo);
                case 24: return _icoMoon ??= MaskIcon("icoMoon", MaskMoon);
                case 25: return _icoBell ??= MaskIcon("icoBell", MaskBell);
                case 26: return _icoCheck ??= MaskIcon("icoCheck", MaskCheck);
                case 27: return _icoX ??= MaskIcon("icoX", MaskX);
                case 28: return _icoCoin ??= MaskIcon("icoCoin", MaskCoin);
                case 29: return _icoDrop ??= MaskIcon("icoDrop", MaskDrop);
                default: return _icoPlus ??= MaskIcon("icoPlus", MaskPlus);
            }
        }

        static Sprite MaskIcon(string name, string[] rows)
        {
            int h = rows.Length, w = rows[0].Length;
            return Make(name, w, h, (x, y) =>
            {
                switch (rows[h - 1 - y][x])
                {
                    case 'w': return new Color32(235, 240, 250, 255);
                    case 'g': return new Color32(255, 214, 120, 255);
                    case 'r': return new Color32(240, 110, 110, 255);
                    case 'p': return new Color32(255, 150, 170, 255);
                    case 'b': return new Color32(150, 105, 70, 255);
                    case 'd': return new Color32(90, 65, 45, 255);
                    case 'n': return new Color32(130, 220, 150, 255);
                    default: return new Color32(0, 0, 0, 0);
                }
            }, Vector4.zero);
        }

        static readonly string[] MaskSword = {
            ".....w...",
            "....ww...",
            "...ww....",
            "..ww.....",
            ".ww......",
            "gggw.....",
            ".bb......",
            ".bb......",
            ".gg......",
        };
        static readonly string[] MaskHeart = {
            ".........",
            ".pp..pp..",
            "pppppppp.",
            "pppppppp.",
            ".pppppp..",
            "..pppp...",
            "...pp....",
            ".........",
            ".........",
        };
        static readonly string[] MaskApple = {
            "...n.....",
            "..nn.....",
            ".rrrrr...",
            "rrrrrrr..",
            "rrrrrrr..",
            "rrrrrrr..",
            ".rrrrr...",
            "..rrr....",
            ".........",
        };
        static readonly string[] MaskBoot = {
            ".........",
            ".bb......",
            ".bb......",
            ".bb......",
            ".bbb.....",
            ".bbbbbb..",
            ".bbbbbb..",
            ".dddddd..",
            ".........",
        };
        static readonly string[] MaskPlay = {
            ".........",
            "..gg.....",
            "..gggg...",
            "..gggggg.",
            "..gggg...",
            "..gg.....",
            ".........",
            ".........",
            ".........",
        };
        static readonly string[] MaskBook = {
            ".........",
            ".bwwww...",
            ".bwwww...",
            ".bwwww...",
            ".bwwww...",
            ".bwwww...",
            ".bwwww...",
            ".........",
            ".........",
        };
        static readonly string[] MaskSave = {
            ".........",
            ".dddddd..",
            ".dg..gd..",
            ".dddddd..",
            ".dddddd..",
            ".dwwwwd..",
            ".dddddd..",
            ".........",
            ".........",
        };
        static readonly string[] MaskGear = {
            ".........",
            "..w.w.w..",
            ".wwwwwww.",
            ".ww...ww.",
            ".ww...ww.",
            ".wwwwwww.",
            "..w.w.w..",
            ".........",
            ".........",
        };
        static readonly string[] MaskDoor = {
            ".........",
            "..ddddd..",
            "..d...d..",
            "..d.gg...",
            "..d..ggg.",
            "..d.gg...",
            "..d...d..",
            "..ddddd..",
            ".........",
        };
        static readonly string[] MaskText = {
            ".........",
            "...www...",
            "..w...w..",
            "..w...w..",
            "..wwwww..",
            "..w...w..",
            "..w...w..",
            ".........",
            ".........",
        };
        static readonly string[] MaskNote = {
            ".........",
            ".....wwg.",
            "....w..g.",
            "....w...g",
            "....w....",
            "..www....",
            ".www.....",
            ".........",
            ".........",
        };
        static readonly string[] MaskSpeaker = {
            ".........",
            "...w.....",
            "..ww.....",
            ".wwww..w.",
            ".wwww.w..",
            ".wwww..w.",
            "..ww.....",
            "...w.....",
            ".........",
        };
        static readonly string[] MaskShake = {
            ".........",
            "w..www..w",
            "...w.w...",
            "...w.w...",
            "...www...",
            "w..www..w",
            ".........",
            ".........",
            ".........",
        };
        static readonly string[] MaskAuto = {
            ".........",
            "....gg...",
            "...gg....",
            "..ggg....",
            ".ggg.....",
            "..ggg....",
            "...gg....",
            "...gg....",
            ".........",
        };
        static readonly string[] MaskBack = {
            ".........",
            "...w.....",
            "..ww.....",
            ".wwwwww..",
            "wwwwww...",
            ".wwwwww..",
            "..ww.....",
            "...w.....",
            ".........",
        };
        static readonly string[] MaskTrash = {
            ".........",
            "...ggg...",
            ".ggggg...",
            "..d.d....",
            "..d.d....",
            "..ddd....",
            ".........",
            ".........",
            ".........",
        };
        static readonly string[] MaskPerson = {
            ".........",
            "...www...",
            "..wwwww..",
            "..wwwww..",
            "...www...",
            "..w...w..",
            ".ww...ww.",
            ".wwwwwww.",
            ".........",
        };
        static readonly string[] MaskBag = {
            ".........",
            "...gg....",
            "..g..g...",
            "..gggg...",
            ".bbbbbb..",
            ".b.bb.b..",
            ".bbbbbb..",
            "..bbbb...",
            ".........",
        };
        static readonly string[] MaskShield = {
            ".........",
            "..wwwww..",
            "..w...w..",
            "..w.w.w..",
            "..w...w..",
            "..w...w..",
            "...www...",
            "....w....",
            ".........",
        };
        static readonly string[] MaskPaw = {
            ".........",
            ".w..w..w.",
            ".w..w..w.",
            ".........",
            "..wwwww..",
            ".wwwwwww.",
            "..wwwww..",
            ".........",
            ".........",
        };
        static readonly string[] MaskScroll = {
            ".........",
            "..ggggg..",
            ".g.g.g.g.",
            ".g.....g.",
            ".g.www.g.",
            ".g.....g.",
            "..ggggg..",
            ".........",
            ".........",
        };
        static readonly string[] MaskDrop = {
            ".........",
            "....n....",
            "...nnn...",
            "..nnnnn..",
            ".nnnnnnn.",
            ".nnnnnnn.",
            "..nnnnn..",
            "...nnn...",
            ".........",
        };
        static readonly string[] MaskStar = {
            ".........",
            "....w....",
            "...www...",
            ".wwwwwww.",
            "..wwwww..",
            "...w.w...",
            "..w...w..",
            ".........",
            ".........",
        };
        static readonly string[] MaskShare = {
            ".........",
            "....ww...",
            "...w.w...",
            "..w..w...",
            ".....w...",
            "...www...",
            "..w.w.w..",
            "..wwwww..",
            ".........",
        };
        static readonly string[] MaskInfo = {
            ".........",
            "...w.....",
            "...w.....",
            ".........",
            "..www....",
            "...w.....",
            "...w.....",
            "..www....",
            ".........",
        };
        static readonly string[] MaskMoon = {
            ".........",
            "....gg...",
            "...gg....",
            "..gg.....",
            "..gg.....",
            "..gg.....",
            "...gg....",
            "....gg...",
            ".........",
        };
        static readonly string[] MaskBell = {
            ".........",
            "...gg....",
            "..gggg...",
            "..gggg...",
            ".gggggg..",
            ".gggggg..",
            "..gggg...",
            "...gg....",
            ".........",
        };
        static readonly string[] MaskCheck = {
            ".........",
            "......w..",
            ".....ww..",
            "w...ww...",
            "ww.ww....",
            ".www.....",
            ".........",
            ".........",
            ".........",
        };
        static readonly string[] MaskX = {
            ".........",
            ".r...r...",
            "..r.r....",
            "...r.....",
            "..r.r....",
            ".r...r...",
            ".........",
            ".........",
            ".........",
        };
        static readonly string[] MaskCoin = {
            ".........",
            "..gggg...",
            ".gwwwdg..",
            ".gwwddg..",
            ".gwdddg..",
            ".gddddg..",
            "..gggg...",
            ".........",
            ".........",
        };
        static readonly string[] MaskPlus = {
            ".........",
            "...nn....",
            "...nn....",
            ".nnnnnn..",
            ".nnnnnn..",
            "...nn....",
            "...nn....",
            ".........",
            ".........",
        };

        /// <summary>Joystick base ring.</summary>
        public static Sprite Ring()
        {
            if (_ring == null)
            {
                _ring = Make("ring", 40, 40, (x, y) =>
                {
                    float dx = x - 19.5f, dy = y - 19.5f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    if (d > 19f || d < 15.5f) return new Color32(255, 255, 255, 0);
                    return new Color32(255, 255, 255, 150);
                }, Vector4.zero);
            }
            return _ring;
        }

        /// <summary>Joystick knob.</summary>
        public static Sprite Dot()
        {
            if (_dot == null)
            {
                _dot = Make("dot", 14, 14, (x, y) =>
                {
                    float dx = x - 6.5f, dy = y - 6.5f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    return d > 6.4f ? new Color32(255, 255, 255, 0) : new Color32(255, 255, 255, 220);
                }, Vector4.zero);
            }
            return _dot;
        }

        // ------------------------------------------------------------ atmosphere

        /// <summary>Vertical night gradient (dark top, lighter bottom, transparent band at
        /// the top for a horizon fade). Replaces the flat blue dimmer over the world.</summary>
        public static Sprite NightTex()
        {
            if (_night == null)
            {
                _night = Make("night", 16, 64, (x, y) =>
                {
                    float k = y / 63f;   // 0 = top of the texture, drawn upward in UV space
                    var c = Color32Lerp(new Color32(8, 6, 30, 190), new Color32(26, 18, 58, 60), k);
                    return c;
                }, Vector4.zero);
            }
            return _night;
        }

        static Color32 Color32Lerp(Color32 a, Color32 b, float t)
        {
            return new Color32(
                (byte)Mathf.RoundToInt(Mathf.Lerp(a.r, b.r, t)),
                (byte)Mathf.RoundToInt(Mathf.Lerp(a.g, b.g, t)),
                (byte)Mathf.RoundToInt(Mathf.Lerp(a.b, b.b, t)),
                (byte)Mathf.RoundToInt(Mathf.Lerp(a.a, b.a, t)));
        }

        /// <summary>Radial vignette frame: transparent center, soft dark corners. 64x64,
        /// stretched over the whole screen for a cinematic explore frame.</summary>
        public static Sprite Vignette()
        {
            if (_vignette == null)
            {
                _vignette = Make("vignette", 64, 64, (x, y) =>
                {
                    float dx = (x - 31.5f) / 31.5f, dy = (y - 31.5f) / 31.5f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01((d - 0.55f) / 0.75f);
                    return new Color32(6, 5, 16, (byte)(a * a * 165f));
                }, Vector4.zero);
            }
            return _vignette;
        }

        /// <summary>Wide star sheet: deterministic pseudo-random dots for title/ending skies.</summary>
        public static Sprite StarField()
        {
            if (_stars == null)
            {
                _stars = Make("stars", 96, 64, (x, y) =>
                {
                    int h = (x * 73856093) ^ (y * 19349663);
                    h = (h ^ (h >> 13)) * 1274126177;
                    h ^= h >> 16;
                    if ((h & 255) < 9)
                    {
                        byte b = (byte)(150 + (h >> 8) % 106);
                        return new Color32(b, b, (byte)Mathf.Min(255, b + 40), (byte)(120 + (h >> 4) % 136));
                    }
                    return new Color32(0, 0, 0, 0);
                }, Vector4.zero);
            }
            return _stars;
        }

        /// <summary>One 3x3 twinkling star (cross with dim diagonals) for animated singles.</summary>
        public static Sprite Star()
        {
            if (_star == null)
            {
                _star = Make("star", 3, 3, (x, y) =>
                {
                    bool cross = x == 1 || y == 1;
                    return cross ? new Color32(255, 250, 220, 235) : new Color32(255, 250, 220, 90);
                }, Vector4.zero);
            }
            return _star;
        }

        /// <summary>"!" aggro bubble: bar with the dot at the bottom (y=0 row).</summary>
        public static Sprite Alert()
        {
            if (_alert == null)
            {
                _alert = Make("alert", 5, 7, (x, y) =>
                {
                    bool ink = x == 2 && (y == 0 || y >= 2);
                    return ink ? new Color32(255, 238, 150, 255) : new Color32(0, 0, 0, 0);
                }, Vector4.zero);
            }
            return _alert;
        }

        public static void ClearCache()
        {
            _tex.Clear(); _sprite.Clear(); _grid.Clear();
            _solid = _panel = _shadow = _glow = _moon = _spark = _chevron = _moonFull = _ring = _dot = null;
            _night = _vignette = _stars = _star = _alert = null;
        }
    }
}
