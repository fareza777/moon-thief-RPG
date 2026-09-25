using System.Collections.Generic;
using UnityEngine;

namespace MoonThief
{
    /// <summary>Shared layout constants. 1 unit = 16 px, matching the art pack.</summary>
    public static class G
    {
        public const float PPU = 16f;
        public const float Left = -9f;      // half of 288 px
        public const float Right = 9f;
        public const int RtWidth = 288;     // render target width in px
        public const float MinHalfHeight = 15f;
        public const float Pixel = 1f / 16f;

        public static float Snap(float v) => Mathf.Round(v / Pixel) * Pixel;
    }

    /// <summary>Sprite row order used by chara_* sheets.</summary>
    public enum Dir { Down = 0, Left = 1, Right = 2, Up = 3 }

    public static class DirVec
    {
        public static Vector2 Of(Dir d)
        {
            switch (d)
            {
                case Dir.Left: return Vector2.left;
                case Dir.Right: return Vector2.right;
                case Dir.Up: return Vector2.up;
                default: return Vector2.down;
            }
        }
        public static Dir From(Vector2 v)
        {
            if (Mathf.Abs(v.x) >= Mathf.Abs(v.y)) return v.x >= 0f ? Dir.Right : Dir.Left;
            return v.y >= 0f ? Dir.Up : Dir.Down;
        }
    }    /// <summary>Walkability of the ground layer. Trees and water block; decor is cosmetic.
    /// Floor, Wall and Void only exist inside a house: the room is floor, the masonry around it
    /// is wall, and everything past the masonry is void so the camera never shows grass in a
    /// bedroom.</summary>
    public enum Ground { Grass, Path, Water, Tree, Rock, Block, Floor, Wall, Void }

    /// <summary>Legacy atlas tile ids (16x16 each), verified against the PNG on disk
    /// by dumping pixels - the first guess set rendered fences as roads.</summary>
        public static class Tiles
        {
            public const int Grass = 668;         // solid #71aa34
            // Grass detail tiles. Every one of these was picked by measuring it against the
            // solid base tile: the average colour has to sit within a few units of it while a
            // small cluster of pixels differs, which is a tuft, a pebble or a sprig of flowers.
            // The two earlier entries (3222, 8285) averaged a noticeably different green, so
            // scattering them painted the meadow in visible square patches.
            public const int GrassDec1 = 8443;    // grass + tiny tuft
            public const int GrassDec2 = 8444;    // grass + tiny tuft
            public const int GrassDec3 = 8517;    // grass + tiny tuft
            public const int GrassDec4 = 3046;    // grass + small clump
            public const int GrassDec5 = 1001;    // grass + tall tuft
            public const int GrassDec6 = 3705;    // grass + wide clump
            public const int GrassDec7 = 4891;    // grass + sprig
            public const int GrassDec8 = 9152;    // grass + sprig
            public const int GrassDec9 = 3044;    // grass + clump
            public const int GrassDec10 = 5577;   // grass + low tuft
            public const int GrassDec11 = 8931;   // grass + clump
            public const int Path = 372;          // packed dirt with pebbles
            public const int PathDec = 298;       // dirt variant
            public const int Water = 325;         // night water fill
            public const int WaterEdgeL = 325;    // shore is drawn as a vertex shadow
            public const int WaterEdgeR = 325;    // (the old edge tiles are blank frames)
            public const int Tree = 4967;         // dense dark-green canopy
            // Every cell of the wood used to be the same tile, and that tile is a flat mass with
            // one lighter row along its top edge, so a wood came out as a dark rectangle ruled
            // with a line every 16 px. These are the pack's textured foliage tiles: a smooth
            // clump, two speckled ones and a couple of lighter leaves, all within a few units of
            // each other in brightness so the mass still reads as one canopy.
            public static readonly int[] Canopy = { 4967, 8536, 8544, 5190, 5042, 4969 };
            public const int Rock = 8679;         // gray boulder
            // The interior tiles live in the row TexArt appends to the world atlas, so their
            // ids follow the pack's own grid instead of being hard-coded magic numbers.
            public static readonly int Floor = TexArt.InteriorBase;
            public static readonly int FloorAlt = TexArt.InteriorBase + 1;
            public static readonly int Wall = TexArt.InteriorBase + 2;
            public static readonly int WallBase = TexArt.InteriorBase + 3;
            public static readonly int Void = TexArt.InteriorBase + 4;
            public static readonly int[] GrassDec =
            {
                GrassDec1, GrassDec2, GrassDec3, GrassDec4, GrassDec5, GrassDec6,
                GrassDec7, GrassDec8, GrassDec9, GrassDec10, GrassDec11
            };
        }

    /// <summary>
    /// The chapter world: 60 x 86 tiles, three vertical zones. Village at the bottom
    /// (safe, NPCs, no spawns), fields in the middle (slimes, chests), dark forest on top
    /// (shades, the way to the boss). Deterministic: built from a fixed seed.
    /// </summary>
    public class GameMap
    {
        public const int W = 60, H = 86;

        public readonly Ground[] Grounds = new Ground[W * H];
        public readonly int[] Deco = new int[W * H];   // -1 = none, else legacy atlas tile id
        // Collision lives here rather than in the ground layer. Marking a solid cell as
        // Ground.Block looked equivalent but was not: the renderer draws Block as grass, so a
        // table standing on a floor punched a square of meadow into the room, and furniture
        // that sat on Ground.Floor -- which Block never overwrote -- could be walked through.
        public readonly bool[] Solid = new bool[W * H];

        public GameMap()
        {
            // int[] defaults to 0 — without this every cell rendered atlas tile 0
            // (a dark UI fragment) on top of the ground: the mysterious "⌐" marks.
            for (int i = 0; i < Deco.Length; i++) Deco[i] = -1;
        }

        public Vector2 VillageCenter = new Vector2(30f, 8f);
        public Vector2 FieldEntry = new Vector2(30f, 15f);
        public Vector2 FieldCenter = new Vector2(30f, 41f);
        public Vector2 ForestEntry = new Vector2(30f, 59f);
        public Vector2 ForestCenter = new Vector2(30f, 74f);
        public Vector2 BossPos = new Vector2(30f, 81f);
        public Vector2 CristalPos = new Vector2(4.5f, 5.5f);

        // ---- a house interior is a map of its own (see BuildRoom) ----
        public bool Interior;
        public int HouseIndex;
        public string InteriorNameKey = "";
        public Vector2 ExitPos;                           // the doormat that leads back outside
        public readonly List<Vector2Int> Doors = new List<Vector2Int>();   // overworld front doors

        public readonly List<Vector2Int> Chests = new List<Vector2Int>();
        public readonly List<Vector2> TorchLights = new List<Vector2>();
        public readonly List<Vector2Int> Blocked = new List<Vector2Int>();  // house/prop footprints

        // ---- one registry for every standing tree, so no two crowns share a cell ----
        // The pack's crowns are up to 48 px wide - three world units on a one-unit cell - so a
        // *free cell* is not the question: a crown needs two clear cells around it, a shrub one.
        readonly Dictionary<Vector2Int, int> _trees = new Dictionary<Vector2Int, int>();

        /// <summary>Whether a tree of class `cls` (2 crown, 1 shrub) may stand on this cell.
        /// Every dresser and the world view ask this, so a wild tree, a wood crown and a bush can
        /// never be planted on top of each other. That is what turned the fields and the wood
        /// edge into piles of unrelated sprites: 42 random draws per chapter landed crowns on
        /// neighbouring cells and drew them in whatever order Unity happened to emit them.
        ///
        /// The clearance is the *pair's* class, not the newcomer's: a 43 px crown is 2.7 world
        /// units wide, so a 16 px shrub may not be tucked two cells under it either - that is one
        /// of the piles the layout audit reports as PILE.</summary>
        public bool TreeSlotFree(int x, int y, int cls)
        {
            if (_trees.ContainsKey(new Vector2Int(x, y))) return false;
            foreach (var t in _trees)
            {
                int d = Mathf.Max(Mathf.Abs(t.Key.x - x), Mathf.Abs(t.Key.y - y));
                if (d <= Mathf.Max(cls, t.Value)) return false;
            }
            return true;
        }

        public void ClaimTree(int x, int y, int cls) => _trees[new Vector2Int(x, y)] = cls;

        /// <summary>A prop the world view turns into a sprite. Art is the path under
        /// Resources/Pack/Props/ ("Trees/tree_04"), Frames the sheet to animate (0 = still).</summary>
        public struct PropDef
        {
            public string Art;
            public string Deco;     // "Bed" / "Table" / "Rug" / "Shelf" / "Window": drawn in code
            public Vector2 Pos;
            public float Scale;
            public int Frames;      // 0 = single sprite, else grid frames to cycle
            public int CellW, CellH;
        }

        public readonly List<PropDef> Props = new List<PropDef>();
        public readonly List<PropDef> Critters = new List<PropDef>();   // village cats, birds, mice

        public Ground At(Vector2Int c) => InBounds(c) ? Grounds[c.y * W + c.x] : Ground.Block;

        /// <summary>A 1px-per-cell picture of the night's ground for the journal's map page:
        /// the whole route reads at a glance - village green, field rows, the dark wood.</summary>
        public Texture2D MiniMapTex()
        {
            var t = new Texture2D(W, H, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            var px = new Color32[W * H];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                    px[y * W + x] = MiniCol(At(new Vector2Int(x, y)));
            t.SetPixels32(px);
            t.Apply(false);
            return t;
        }

        static Color32 MiniCol(Ground g)
        {
            switch (g)
            {
                case Ground.Grass: return new Color32(52, 98, 62, 255);
                case Ground.Path:  return new Color32(138, 96, 60, 255);
                case Ground.Water: return new Color32(52, 82, 148, 255);
                case Ground.Tree:  return new Color32(28, 74, 46, 255);
                case Ground.Rock:  return new Color32(122, 118, 132, 255);
                case Ground.Wall:  return new Color32(64, 50, 70, 255);
                case Ground.Floor: return new Color32(142, 100, 68, 255);
                default:           return new Color32(14, 14, 24, 255);
            }
        }

        /// <summary>Field chests a chapter's map places: two early nights, all six in the
        /// last night's wood. The quest book needs the same number to cap chest errands at
        /// what the night still holds shut.</summary>
        public static int ChestsPlaced(int chapter) => chapter >= 3 ? 6 : Mathf.Clamp(1 + chapter, 2, 4);

        /// <summary>Overworld sheet of the chapter's gatekeeper.</summary>
        public static string BossMapSheet(int chapter)
            => chapter <= 1 ? "Art/Mon/Monsters_03_0"
                : chapter == 2 ? "Pack/Monsters/Monsters_04_5"
                : "Art/Mon/Monsters_04_0";
        public int DecoAt(Vector2Int c) => InBounds(c) ? Deco[c.y * W + c.x] : -1;
        public bool InBounds(Vector2Int c) => c.x >= 0 && c.y >= 0 && c.x < W && c.y < H;
        public bool IsSolid(Vector2Int c) => InBounds(c) && Solid[c.y * W + c.x];

        public bool Walkable(Vector2Int c)
        {
            if (!InBounds(c)) return false;
            var g = At(c);
            if (g != Ground.Grass && g != Ground.Path && g != Ground.Floor) return false;
            return !Solid[c.y * W + c.x];
        }

        /// <summary>Turns the recorded footprints into collision. The Blocked list is filled by
        /// every house, column, statue, barrel, tree and piece of furniture in the game; this is
        /// the one place that reads it. It used to rewrite the ground cells to Ground.Block, which
        /// left the grass tile drawn underneath (fine outdoors) but turned a room's floor into
        /// meadow wherever a table stood.</summary>
        void ApplyBlocking()
        {
            foreach (var c in Blocked)
                if (InBounds(c)) Solid[c.y * W + c.x] = true;
        }

        bool TreeAt(int x, int y)
        {
            if (x < 0 || y < 0 || x >= W || y >= H) return false;
            return Grounds[y * W + x] == Ground.Tree;
        }

        Ground this[int x, int y]
        {
            get => Grounds[y * W + x];
            set => Grounds[y * W + x] = value;
        }

        void Fill(int x0, int y0, int x1, int y1, Ground g)
        {
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                    if (x >= 0 && y >= 0 && x < W && y < H) this[x, y] = g;
        }

        void DecoFill(int x0, int y0, int x1, int y1, int tile)
        {
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                    if (x >= 0 && y >= 0 && x < W && y < H) Deco[y * W + x] = tile;
        }

        /// <summary>Rough path of 1-2 tiles width from a to b (axis, then vertical).</summary>
        void Path(Vector2Int a, Vector2Int b, int seedJitter)
        {
            var rng = new System.Random(seedJitter);
            int x = a.x, y = a.y;
            while (x != b.x)
            {
                this[x, y] = Ground.Path;
                if (rng.Next(2) == 0 && y + 1 < H) this[x, y + 1] = Ground.Path;
                if (rng.Next(2) == 0 && y - 1 >= 0) this[x, y - 1] = Ground.Path;
                x += x < b.x ? 1 : -1;
            }
            while (y != b.y)
            {
                this[x, y] = Ground.Path;
                if (rng.Next(2) == 0 && x + 1 < W) this[x + 1, y] = Ground.Path;
                if (rng.Next(2) == 0 && x - 1 >= 0) this[x - 1, y] = Ground.Path;
                y += y < b.y ? 1 : -1;
            }
            this[x, y] = Ground.Path;
        }

        void ScatterGrassDeco(int x0, int y0, int x1, int y1, System.Random rng, float chance)
        {
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                    if (this[x, y] == Ground.Grass && rng.NextDouble() < chance)
                        Deco[y * W + x] = Tiles.GrassDec[rng.Next(Tiles.GrassDec.Length)];
        }

        /// <summary>Lets the roads wander. A road painted by two perpendicular rules has an
        /// edge that follows the tile grid exactly, which is the single most obvious sign that
        /// the world is made of squares: the verge is nibbled outward and the thin stretches
        /// are eaten back until the border is uneven. Deterministic, like the rest of the map.</summary>
        void RoughEdges(int chapter)
        {
            var rng = new System.Random(7000 + chapter * 31);
            var before = (Ground[])Grounds.Clone();
            for (int y = 2; y < H - 2; y++)
            {
                for (int x = 2; x < W - 2; x++)
                {
                    var here = before[y * W + x];
                    if (here != Ground.Grass && here != Ground.Path) continue;
                    int road = 0, grass = 0;
                    if (before[y * W + x - 1] == Ground.Path) road++; else grass++;
                    if (before[y * W + x + 1] == Ground.Path) road++; else grass++;
                    if (before[(y - 1) * W + x] == Ground.Path) road++; else grass++;
                    if (before[(y + 1) * W + x] == Ground.Path) road++; else grass++;
                    if (here == Ground.Grass)
                    {
                        if (road >= 2 && rng.NextDouble() < 0.36) Grounds[y * W + x] = Ground.Path;
                    }
                    else if (grass >= 3 && rng.NextDouble() < 0.24)
                    {
                        Grounds[y * W + x] = Ground.Grass;
                    }
                }
            }
        }

        /// <summary>The interior of one of the village houses: a room of floor with masonry
        /// around it, furniture, an occupant and a doormat back to the street. It lives in the
        /// same 60x86 grid as the overworld, so the camera, the HUD and the label plates all
        /// keep working exactly as they do outside; everything past the masonry is Void, which
        /// is both unwalkable and black.
        ///
        /// The layout is fixed per house so a return visit looks like the same room.</summary>
        public static GameMap BuildRoom(int houseIndex)
        {
            var m = new GameMap();
            m.Interior = true;
            m.HouseIndex = Mathf.Abs(houseIndex) % 6;
            m.InteriorNameKey = "house." + m.HouseIndex;
            // no boss, no crystal and no chest economy inside: the room has one cache of its own
            m.BossPos = new Vector2(-100f, -100f);
            m.CristalPos = new Vector2(-100f, -100f);

            m.Fill(0, 0, W - 1, H - 1, Ground.Void);
            m.Fill(0, 0, 17, 31, Ground.Wall);
            const int x0 = 3, y0 = 8, x1 = 15, y1 = 22;
            m.Fill(x0, y0, x1, y1, Ground.Floor);
            int mid = (x0 + x1) / 2;
            m.Fill(mid, y0 - 1, mid + 1, y0 - 1, Ground.Floor);   // the doorway alcove
            m.ExitPos = new Vector2(mid + 1f, y0 - 0.5f);

            int v = m.HouseIndex;
            // shared shell: a lit window in the back wall, a lamp by the door, a hearth
            m.AddRoomProp("Window", "", mid + 3, y1 + 1, 1f);
            m.AddRoomProp("", "Lamps/lamp_" + (1 + v * 3).ToString("00"), x0 + 1, y0 + 1, 1f, 16, 32);

            switch (v)
            {
                case 0:   // Mira's house: a quiet shrine room
                    m.AddRoomProp("Rug", "", 9, 14, 1f);
                    m.AddRoomProp("Shelf", "", 4, 20, 1f);
                    m.AddRoomProp("", "Statues/statue_0" + (1 + v % 3), 14, 20, 1f);
                    m.AddRoomProp("", "Fires/fire_camp_0" + (1 + v % 2), 4, 12, 1f, 16, 32);
                    m.AddRoomProp("", "Pots/" + Pots[v % Pots.Length], 14, 11, 1f);
                    break;
                case 1:   // the kid's room: toys everywhere
                    m.AddRoomProp("Bed", "", 4, 13, 1f);
                    m.AddRoomProp("Rug", "", 9, 11, 1f);
                    m.AddRoomProp("", "Crates/crate_0" + (1 + v % 8), 10, 20, 1f);
                    m.AddRoomProp("", "Crates/crate_0" + (2 + v % 7), 12, 19, 1f);
                    m.AddRoomProp("", "Pots/" + Pots[(v + 3) % Pots.Length], 14, 14, 1f);
                    m.AddRoomProp("", "Barrels/" + BarrelName(v), 14, 20, 1f);
                    break;
                case 2:   // the smith's forge
                    m.AddRoomProp("", "Fires/fire_camp_02", 4, 11, 1f, 16, 32);
                    m.AddRoomProp("", "Barrels/" + BarrelName(v + 1), 4, 20, 1f);
                    m.AddRoomProp("", "Barrels/" + BarrelName(v + 2), 5, 20, 1f);
                    m.AddRoomProp("", "Crates/crate_0" + (1 + v % 8), 12, 21, 0.9f);
                    m.AddRoomProp("Table", "", 10, 15, 1f);
                    break;
                case 3:   // the hunter's room: trophies and gear
                    m.AddRoomProp("Shelf", "", 4, 20, 1f);
                    m.AddRoomProp("Table", "", 12, 12, 1f);
                    m.AddRoomProp("", "Fires/fire_camp_01", 5, 12, 1f, 16, 32);
                    m.AddRoomProp("", "Pots/" + Pots[(v + 5) % Pots.Length], 14, 18, 1f);
                    break;
                case 4:   // the bard's room
                    m.AddRoomProp("Rug", "", 9, 12, 1f);
                    m.AddRoomProp("Shelf", "", 4, 21, 1f);
                    m.AddRoomProp("Bed", "", 13, 18, 1f);
                    // the pack ships eight crates - the index wraps, never crate_09
                    m.AddRoomProp("", "Crates/crate_0" + (1 + v % 8), 4, 10, 1f);
                    break;
                default:  // grandma's: a crowded kitchen
                    m.AddRoomProp("Bed", "", 4, 12, 1f);
                    m.AddRoomProp("Table", "", 11, 12, 1f);
                    m.AddRoomProp("", "Pots/" + Pots[(v + 8) % Pots.Length], 14, 17, 1f);
                    m.AddRoomProp("", "Barrels/" + BarrelName(v + 3), 14, 20, 1f);
                    m.AddRoomProp("Shelf", "", 8, 21, 1f);
                    break;
            }

            // the way out: the door sits in the alcove, and walking onto the mat or tapping it
            // leaves the house
            m.Props.Add(new PropDef
            {
                Art = "Anim/Door/door_1_16x16", Pos = new Vector2(mid + 1f, y0 - 0.5f),
                Scale = 1f, Frames = 4, CellW = 16, CellH = 16,
            });

            // one cache per room, at the back
            m.Chests.Add(new Vector2Int(x1 - 1, y1 - 1));
            m.Blocked.Add(new Vector2Int(x1 - 1, y1 - 1));
            m.ApplyBlocking();
            return m;
        }

        /// <summary>Furniture for a room. Deco names a sprite drawn in code (Bed, Table, Rug,
        /// Shelf, Window); art names a sprite from the pack.</summary>
        void AddRoomProp(string deco, string art, int x, int y, float scale, int cw = 16, int ch = 16)
        {
            Props.Add(new PropDef
            {
                Art = art, Deco = deco, Pos = new Vector2(x + 0.5f, y + 0.5f), Scale = scale,
                CellW = cw, CellH = ch,
            });
            // a rug and a window in the wall are things you walk over or past; everything else
            // stands in the way. Wide pieces claim their second cell too.
            if (deco == "Rug" || deco == "Window") return;
            Blocked.Add(new Vector2Int(x, y));
            if (deco == "Table" || deco == "Bed") Blocked.Add(new Vector2Int(x + 1, y));
        }

        /// <summary>Builds the whole chapter map. chapter: 1 fields, 2 deep fields, 3 forest.</summary>
        public static GameMap Build(int chapter)
        {
            var m = new GameMap();
            var rng = new System.Random(1000 + chapter * 77);

            // base: grass everywhere, tree wall around, water pond in the village corner
            m.Fill(0, 0, W - 1, H - 1, Ground.Grass);
            m.Fill(0, 0, W - 1, 1, Ground.Tree);
            m.Fill(0, H - 2, W - 1, H - 1, Ground.Tree);
            m.Fill(0, 0, 1, H - 1, Ground.Tree);
            m.Fill(W - 2, 0, W - 1, H - 1, Ground.Tree);

            // ---- village (y 2..13): houses, plaza, pond, the cristal ----
            m.Fill(3, 2, W - 4, 13, Ground.Grass);
            m.Fill(5, 2, 10, 6, Ground.Water);                       // pond NW
            m.DecoFill(5, 2, 10, 6, Tiles.Water);
            m.DecoFill(5, 2, 10, 2, Tiles.WaterEdgeL);
            m.Fill(22, 4, 37, 10, Ground.Path);                      // plaza
            // a dark grove on the east side of the village. This corner used to be filled with
            // Ground.Block and then immediately overwritten with grass, so the comment promised a
            // grove and the player got one more empty field. Now it is a real clump of trees.
            for (int y = 3; y <= 7; y++)
                for (int x = 44; x <= 55; x++)
                {
                    float gdx = (x - 49.5f) / 7f, gdy = (y - 5f) / 3.4f;
                    if (gdx * gdx + gdy * gdy <= 1f && (x * 3 + y * 5) % 9 != 0)
                        m[x, y] = Ground.Tree;
                }

            // ---- fields (y 14..58): open grass with path, sparse rocks, two clearings ----
            m.Fill(4, 14, W - 5, 27, Ground.Grass);
            m.Fill(4, 46, W - 5, 58, Ground.Grass);
            // forest wall between field 1 and 2, with a gate on the path
            m.Fill(2, 28, W - 3, 32, Ground.Tree);
            m.Fill(26, 28, 33, 32, Ground.Grass);
            // field 2
            m.Fill(4, 33, W - 5, 58, Ground.Grass);

            // ---- forest (y 59..83): dense trees, winding clearing toward the boss ----
            m.Fill(2, 59, W - 3, 83, Ground.Tree);
            m.Fill(6, 61, 20, 70, Ground.Grass);
            m.Fill(16, 68, 42, 76, Ground.Grass);
            m.Fill(36, 74, 46, 82, Ground.Grass);
            m.Fill(27, 78, 34, 83, Ground.Grass);                    // boss clearing

            // rocks sprinkled in the fields
            for (int i = 0; i < 26; i++)
            {
                int x = rng.Next(6, W - 6), y = rng.Next(15, 58);
                if (m[x, y] == Ground.Grass && Mathf.Abs(x - 30) > 4) m[x, y] = Ground.Rock;
            }

            // roads
            m.Path(new Vector2Int(30, 6), new Vector2Int(30, 83), 40 + chapter);   // spine
            m.Path(new Vector2Int(30, 22), new Vector2Int(12, 22), 41 + chapter);  // west fork
            m.Path(new Vector2Int(30, 48), new Vector2Int(46, 48), 42 + chapter);  // east fork

            // The spine the jittered path draws is one tile wide in places, and the tree wall
            // between the fields and the wood had no mouth at all: walking north at x=29.4
            // wedged the hero against the treeline. Widen the road and cut the gate open.
            m.Fill(29, 13, 31, 58, Ground.Path);    // village lane through both fields
            m.Fill(28, 59, 32, 62, Ground.Path);    // mouth of the wood
            m.Fill(28, 59, 32, 69, Ground.Path);    // corridor to the first clearing
            m.Fill(28, 70, 32, 80, Ground.Path);    // corridor on to the guard's clearing

            // Straight ruler edges are the clearest tell of a tile grid, so the roads are
            // allowed to wander before anything is dressed on top of them.
            m.RoughEdges(chapter);

            // grass decoration everywhere (subtle: fine speckle, not a repeating stamp)
            m.ScatterGrassDeco(2, 2, W - 3, 83, rng, 0.16f);

            // house footprints (visuals are placed by WorldView at these anchors)
            var houses = new[] {
                new Vector2Int(15, 9), new Vector2Int(21, 11), new Vector2Int(39, 9),
                new Vector2Int(45, 11), new Vector2Int(14, 4), new Vector2Int(41, 4)
            };
            foreach (var h in houses)
                for (int dy = 0; dy < 3; dy++)
                    for (int dx = 0; dx < 3; dx++)
                        m.Blocked.Add(new Vector2Int(h.x + dx, h.y + dy));

            // the Pale Guard stands across the road: the row it occupies is impassable, so
            // the road north is a real gate you have to fight through
            int guardRow = Mathf.RoundToInt(m.BossPos.y);
            for (int dx = -2; dx <= 2; dx++)
                m.Blocked.Add(new Vector2Int(Mathf.RoundToInt(m.BossPos.x) + dx, guardRow));

            // torches along the plaza
            m.TorchLights.Add(new Vector2(23.5f, 9.5f));
            m.TorchLights.Add(new Vector2(36.5f, 9.5f));
            m.TorchLights.Add(new Vector2(30f, 3.5f));

            // lamppost torches line the spine road through the fields, so the night
            // world reads as lit path -> dark wild instead of one flat wash
            for (int y = 14; y <= 56; y += 7)
            {
                m.TorchLights.Add(new Vector2(28.2f, y + 0.5f));
                m.TorchLights.Add(new Vector2(31.8f, y + 0.5f));
            }
            m.TorchLights.Add(new Vector2(13.5f, 22.5f));   // west fork end
            m.TorchLights.Add(new Vector2(45.5f, 48.5f));   // east fork end

            // chests: fields hold two per chapter, forest one
            var chestSpots = new[] {
                new Vector2Int(12, 22), new Vector2Int(46, 48),
                new Vector2Int(9, 40), new Vector2Int(52, 36),
                new Vector2Int(18, 66), new Vector2Int(44, 78)
            };
            // the last night's dark wood holds every spot the map knows - two of the
            // six were drawn but never stood, and the ledger quest below wants them
            int chests = ChestsPlaced(chapter);
            for (int i = 0; i < chests; i++) m.Chests.Add(chestSpots[i]);
            // A chest stands on its own cell. It used to be scenery: nothing marked the cell, so
            // the dressers were free to plant a tree or a boulder on top of it (a chest cut in
            // half by a crown was the "clipping" in every field shot), and the hero could walk
            // straight through the lid.
            foreach (var c in m.Chests)
            {
                m.Blocked.Add(c);
                // a chest is small but its lid is at eye height - register it in the tree
                // registry like a class-2 crown so no tree stands close enough for its own
                // crown to hang over the lid (the chest-under-crown PILE the audit caught)
                m.ClaimTree(c.x, c.y, 2);
            }

            // the crystal of dawn sits in the village, west of the plaza
            m.CristalPos = new Vector2(11.5f, 7.5f);

            m.DressVillage(houses, rng);
            m.DressFields(rng);
            m.DressForest(rng);
            m.DressRocks(rng);
            m.DressMargins(rng);
            // last: every footprint recorded above becomes real, unwalkable ground
            m.ApplyBlocking();

            // zone anchors
            m.FieldCenter = chapter == 1 ? new Vector2(30f, 41f) : new Vector2(30f, 41f);
            return m;
        }

        // ------------------------------------------------------------ set dressing

        /// <summary>Tree ids the wild is built from. The pack ships blue and violet crowns too;
        /// sprinkled through a night meadow they read as coloured balls, so the woods lean on the
        /// sober greens with a few early-autumn ones (19, 22, 24, 26) for warmth.</summary>
        // The pack's trees by pixel width: 16 px shrubs, 32-40 px crowns, 41-48 px giants.
        // Autumn re-skins exist in all three sizes, and a stand keeps to one size class so a
        // wood edge reads as a tree line instead of a shrub fighting a giant for the same tile.
        static readonly string[] ShrubTreeIds = { "05", "08", "14" };
        static readonly string[] MidTreeIds =
        {
            "01", "03", "06", "07", "09", "10", "12", "15", "16", "18", "20", "21", "23", "25", "26", "29", "30"
        };
        static readonly string[] BigTreeIds = { "02", "04", "11", "13", "17", "27", "32" };
        // The pack's boulders: up to 17 px wide one fits inside a world cell, the 24-49 px slabs
        // do not - the biggest is three units wide on a one-unit cell.
        static readonly string[] SmallRockIds =
        {
            "01", "04", "05", "06", "07", "08", "09", "10", "11", "12", "13", "14", "15", "16", "17",
            "21", "22", "23", "24", "28", "29", "30", "31", "32", "33", "34", "35", "37", "38", "39",
            "40", "42", "43", "44", "45"
        };
        static readonly string[] BigRockIds = { "02", "03", "18", "19", "20", "25", "26", "27", "36", "41", "46" };

        // Boulders that stay inside one cell in BOTH directions (16x16 or less): a stand of Rock
        // ground takes these, because a 15x29 boulder on the cell above a 14x11 one covers it
        // completely - a prop that is never seen.
        static readonly string[] FlatRockIds =
        {
            "01", "04", "05", "06", "12", "13", "14", "21", "22", "23", "24", "28", "29", "30", "31",
            "32", "33", "37", "38", "42", "43"
        };

        static readonly string[] AutumnShrubIds = { "19", "24" };
        static readonly string[] AutumnMidIds = { "26", "30" };
        static readonly string[] AutumnBigIds = { "22", "31" };

        /// <summary>Picks a wild tree and reports its class: 2 for a crown, 1 for a shrub. The
        /// class is what the caller asks the tree registry for space with.</summary>
        static string WildTree(System.Random rng, int autumnPercent, int shrubPercent, int bigPercent, out int cls)
        {
            bool autumn = rng.Next(100) < autumnPercent;
            int roll = rng.Next(100);
            if (roll < shrubPercent)
            {
                cls = 1;
                var pool = autumn ? AutumnShrubIds : ShrubTreeIds;
                return "Trees/tree_" + pool[rng.Next(pool.Length)];
            }
            if (roll < shrubPercent + bigPercent)
            {
                cls = 2;
                var pool = autumn ? AutumnBigIds : BigTreeIds;
                return "Trees/tree_" + pool[rng.Next(pool.Length)];
            }
            cls = 2;
            var mids = autumn ? AutumnMidIds : MidTreeIds;
            return "Trees/tree_" + mids[rng.Next(mids.Length)];
        }

        /// <summary>Plants one wild tree, if the cell is free and the crown has room. Solid
        /// props (rocks, crops, chests, houses) are avoided through Blocked, other trees through
        /// the tree registry, so a tree can no longer stand on a chest or inside another tree.</summary>
        bool PlantTree(System.Random rng, int x, int y, int autumnPercent, int shrubPercent,
            int bigPercent, float scaleMin, float scaleMax)
        {
            var cell = new Vector2Int(x, y);
            if (!InBounds(cell) || this[x, y] != Ground.Grass) return false;
            if (Blocked.Contains(cell)) return false;
            int cls;
            string art = WildTree(rng, autumnPercent, shrubPercent, bigPercent, out cls);
            if (!TreeSlotFree(x, y, cls)) return false;
            AddProp(art, x, y, scaleMin + (float)rng.NextDouble() * (scaleMax - scaleMin), cls == 2);
            ClaimTree(x, y, cls);
            return true;
        }

        /// <summary>Only barrel_01..barrel_05 exist in the pack. The rooms asked for barrel_06
        /// and beyond by counting up from the house index, so a third of the barrels in the houses
        /// were invisible; this maps any number onto the five that are there.</summary>
        static string BarrelName(int i) => "barrel_0" + (1 + ((i % 5) + 5) % 5);

        static readonly string[] Pots =
        {
            "pot_01", "pot_02", "pot_3", "pot_4", "pot_5", "pot_6", "pot_7", "pot_8", "pot_9",
            "pot_10", "pot_11", "pot_12", "pot_13", "pot_14", "pot_15", "pot_16", "pot_17",
            "pot_18", "pot_19", "pot_20", "pot_21", "pot_22", "pot_23", "pot_24", "pot_25",
        };

        void AddProp(string art, int x, int y, float scale, bool block, int frames = 0, int cw = 16, int ch = 16)
        {
            var cell = new Vector2Int(x, y);
            if (!InBounds(cell) || this[x, y] != Ground.Grass) return;
            Props.Add(new PropDef
            {
                Art = art, Pos = new Vector2(x + 0.5f, y + 0.5f), Scale = scale,
                Frames = frames, CellW = cw, CellH = ch,
            });
            if (block) Blocked.Add(cell);
        }

        void DressVillage(Vector2Int[] houses, System.Random rng)
        {
            // the six houses, six different facades so the street never repeats
            var facades = new[] { 2, 5, 8, 12, 16, 21 };
            for (int i = 0; i < houses.Length; i++)
                Props.Add(new PropDef
                {
                    Art = "Houses/house_" + facades[i % facades.Length].ToString("00"),
                    Pos = new Vector2(houses[i].x + 1.5f, houses[i].y + 1.5f), Scale = 1f,
                });

            // yard clutter: pots, barrels and crates tucked beside the houses. The pot files
            // are named unevenly (pot_01, pot_02, pot_3 ...), so the list is written out.
            int[] yard = { 3, 4, 7, 11, 14, 19, 23, 26, 31, 37, 44, 52 };
            for (int i = 0; i < yard.Length; i++)
            {
                int x = yard[i];
                int y = 3 + (i % 4) * 3;
                AddProp("Pots/" + Pots[i % Pots.Length], x, y, 1f, true);
            }
            int[] barrels = { 13, 20, 25, 38, 44, 48 };
            for (int i = 0; i < barrels.Length; i++)
                AddProp("Barrels/barrel_0" + (1 + (i % 5)), barrels[i], 6 + (i % 3) * 2, 1f, true);
            int[] crates = { 16, 18, 34, 42, 47 };
            for (int i = 0; i < crates.Length; i++)
                AddProp("Crates/crate_0" + (1 + (i % 8)), crates[i], 12 - (i % 2) * 4, 1f, true);

            // plaza: two columns and the old statue on the axis of the crystal
            AddProp("Columns/column_" + (1 + rng.Next(10)).ToString("00"), 22, 5, 1f, true);
            AddProp("Columns/column_" + (1 + rng.Next(10)).ToString("00"), 37, 5, 1f, true);
            AddProp("Statues/statue_" + (1 + rng.Next(3)).ToString("00"), 30, 12, 1f, true);

            // the torii marks the gate from the village into the fields
            AddProp("Torii/torii_" + (1 + rng.Next(6)).ToString("00"), 29, 14, 1f, true);

            // a campfire the villagers sit around, and lamps along the plaza edge
            AddProp("Fires/fire_camp_01", 26, 8, 1f, true, 3, 16, 32);
            int[] lamps = { 8, 12, 24, 35, 40, 44 };
            for (int i = 0; i < lamps.Length; i++)
                AddProp("Lamps/lamp_" + (1 + (i * 3 % 15)).ToString("00"), lamps[i], 4 + (i % 2) * 8, 1f, true,
                    0, 16, 32);

            // animals wander the village: cats by the houses, birds on the plaza, a mouse
            Critters.Add(Critter("cats/cat" + (1 + rng.Next(4)) + "_16x20", 24, 7));
            Critters.Add(Critter("cats/cat" + (1 + rng.Next(4)) + "_16x20", 40, 12));
            Critters.Add(Critter("birds/bird" + (1 + rng.Next(6)) + "_16x20", 31, 9));
            Critters.Add(Critter("mouses/mouse" + (1 + rng.Next(3)) + "_16x20", 18, 11));

            // a rabbit hops about the crops, a fox patrols the wood's edge
            Critters.Add(Critter("rabbits/bunny" + (1 + rng.Next(2)) + "_16x20", 12, 20));
            Critters.Add(Critter("rabbits/bunny" + (1 + rng.Next(2)) + "_16x20", 44, 24));
            Critters.Add(Critter("foxes/fox" + (1 + rng.Next(2)) + "_16x20", 20, 62));
            Critters.Add(Critter("foxes/fox" + (1 + rng.Next(2)) + "_16x20", 40, 70));

            // A front door on every house: the cell the hero walks onto to go inside. It used to
            // plant the pack's separate 16x16 door sprite here as well -- but the house art already
            // paints a door into its own facade, one row higher, so the street ended up showing a
            // second, detached door standing on its own in the grass in front of every wall.
            // The wall keeps its painted door; the step and the light are built in WorldView.
            for (int i = 0; i < houses.Length; i++)
            {
                int dx0 = houses[i].x + 1, dy0 = houses[i].y;
                if (this[dx0, dy0] != Ground.Grass) continue;
                Doors.Add(new Vector2Int(dx0, dy0));
            }
        }

        PropDef Critter(string art, int x, int y) => new PropDef
        {
            Art = art, Pos = new Vector2(x + 0.5f, y + 0.5f), Scale = 1f, Frames = 12, CellW = 16, CellH = 20,
        };

        void DressFields(System.Random rng)
        {
            // farmland strips: crop rows on the near fields, so the middle of the map reads
            // as worked land instead of empty grass
            for (int row = 0; row < 3; row++)
                for (int col = 0; col < 4; col++)
                {
                    int x = 6 + col * 3 + row * 9;
                    int y = 17 + row * 3;
                    AddProp("Crops/crop_" + (1 + ((row * 4 + col) % 22)).ToString("00"), x, y, 0.7f, false, 11, 18, 28);
                }
            // a second plot east of the lane, so both sides of the road read as worked land
            for (int row = 0; row < 3; row++)
                for (int col = 0; col < 4; col++)
                {
                    int x = 35 + col * 3;
                    int y = 20 + row * 3 + col;
                    AddProp("Crops/crop_" + (1 + ((row * 3 + col) % 22)).ToString("00"), x, y, 0.7f, false, 11, 18, 28);
                }

            // roadside lamps continue up the spine
            for (int y = 16; y <= 56; y += 9)
            {
                AddProp("Torches/Torch_" + (1 + (y / 9) % 17).ToString("00"), 27, y, 1f, true, 3, 16, 32);
                AddProp("Torches/Torch_" + (1 + (y / 9 + 3) % 17).ToString("00"), 33, y + 4, 1f, true, 3, 16, 32);
            }

            // trees and boulders fill the wild edges, never the road. The count is a *target*:
            // a draw that finds no home (the cell is taken, or the crown has no room) is not a
            // tree, and the loop keeps drawing until the target is met or it gives up.
            for (int i = 0, drawn = 0; i < 220 && drawn < 34; i++)
            {
                int x = 3 + rng.Next(W - 6);
                int y = 15 + rng.Next(43);
                if (Mathf.Abs(x - 30) < 5) continue;
                if (this[x, y] != Ground.Grass) continue;
                if (rng.Next(100) < 62)
                {
                    if (PlantTree(rng, x, y, 15, 30, 0, 0.95f, 1.15f)) drawn++;
                }
                else if (!Blocked.Contains(new Vector2Int(x, y)) && !RockWithin(x, y, 2))
                {
                    // scattered scenery, so a boulder that fits its cell: the slabs are for a
                    // stand of rock on their own, not for the middle of a field. The two cells of
                    // clearance keep a pebble out from under a slab's box, where it would never
                    // be seen (the audit reads that as PILE at 100%).
                    AddProp("Rocks/rock_" + FlatRockIds[rng.Next(FlatRockIds.Length)], x, y,
                        0.9f + 0.2f * (float)rng.NextDouble(), true);
                }
            }
        }

        void DressForest(System.Random rng)
        {
            // the wood: taller trees, boulders, and a torch every few steps so the road
            // through it is still readable at night
            for (int i = 0, drawn = 0; i < 260 && drawn < 38; i++)
            {
                int x = 3 + rng.Next(W - 6);
                int y = 60 + rng.Next(23);
                if (Mathf.Abs(x - 30) < 3) continue;
                if (this[x, y] != Ground.Grass) continue;
                if (rng.Next(100) < 74)
                {
                    // the deep wood is the one place the giants grow, and it gets the most
                    // shrubs: a dark floor with crowns standing out of it
                    if (PlantTree(rng, x, y, 20, 26, 24, 1.0f, 1.2f)) drawn++;
                }
                else if (!Blocked.Contains(new Vector2Int(x, y)) && !RockWithin(x, y, 2))
                {
                    AddProp("Rocks/rock_" + FlatRockIds[rng.Next(FlatRockIds.Length)], x, y,
                        0.9f + 0.2f * (float)rng.NextDouble(), true);
                }
            }
            for (int y = 62; y <= 80; y += 6)
                AddProp("Torches/Torch_" + (1 + (y % 17)).ToString("00"), 29, y, 1f, true, 3, 16, 32);

            // the camp the guard keeps at the top of the map
            AddProp("Fires/fire_camp_02", 26, 78, 1f, true, 3, 16, 32);
            AddProp("Columns/column_" + (1 + rng.Next(10)).ToString("00"), 23, 80, 1f, true);
            AddProp("Columns/column_" + (1 + rng.Next(10)).ToString("00"), 37, 80, 1f, true);
        }

        /// <summary>Every Ground.Rock cell gets a boulder sprite. The scattered rocks were real
        /// ground - the hero could not walk onto them - but nothing was drawn on them, so the
        /// fields were full of invisible walls. Tiles.Rock is a flat grey plate, which is why
        /// this draws a prop instead.
        ///
        /// The pack's set runs from a 7x5 pebble to a 49x34 slab, so "a boulder for every cell"
        /// drew 3-unit slabs on cells one unit apart until the field read as one pile of rock
        /// (the layout audit flags the pair as PILE). A cell that has Rock ground beside it now
        /// takes a boulder that fits inside its own tile; the slabs stand on their own.</summary>
        void DressRocks(System.Random rng)
        {
            for (int i = 0; i < Grounds.Length; i++)
            {
                if (Grounds[i] != Ground.Rock) continue;
                int x = i % W, y = i / W;
                // Two cells of clearance, not one: the biggest slab is three units wide, so a
                // slab two cells from another boulder already covers it whole (PILE at 100%).
                bool crowd = RockWithin(x, y, 2);
                string id = crowd ? FlatRockIds[rng.Next(FlatRockIds.Length)]
                                  : BigRockIds[rng.Next(BigRockIds.Length)];
                Props.Add(new PropDef
                {
                    Art = "Rocks/rock_" + id,
                    Pos = new Vector2(x + 0.5f, y + 0.5f),
                    Scale = crowd ? 0.9f + 0.18f * (float)rng.NextDouble() : 1f,
                });
            }
        }

        bool RockAt(int x, int y)
        {
            if (x < 0 || y < 0 || x >= W || y >= H) return false;
            return Grounds[y * W + x] == Ground.Rock;
        }

        bool RockWithin(int x, int y, int r)
        {
            for (int dy = -r; dy <= r; dy++)
                for (int dx = -r; dx <= r; dx++)
                {
                    if (dx == 0 && dy == 0) continue;
                    if (RockAt(x + dx, y + dy)) return true;
                }
            return false;
        }

        /// <summary>Individual trees along the margin of every wood. The canopy is a flat slab
        /// of tile art, so where it ends the world got a hard green edge; loose trees on the
        /// grass just outside it feather that edge and give the wood height and a silhouette.</summary>
        void DressMargins(System.Random rng)
        {
            int placed = 0;
            for (int y = 2; y < H - 2; y++)
            {
                for (int x = 2; x < W - 2; x++)
                {
                    if (this[x, y] != Ground.Grass) continue;
                    if (Mathf.Abs(x - 30) < 4) continue;              // never on the spine road
                    bool edge = TreeAt(x - 1, y) || TreeAt(x + 1, y) || TreeAt(x, y - 1) || TreeAt(x, y + 1);
                    if (!edge) continue;
                    if (rng.NextDouble() > 0.34) continue;
                    // a low tree line at the wood's foot: crowns on the rim already come from the
                    // world view, so these are the small and mid sizes that feather the edge
                    if (PlantTree(rng, x, y, 12, 40, 0, 0.9f, 1.05f)) placed++;
                }
            }
        }

        public Vector2 CellCenter(Vector2Int c) => new Vector2(c.x + 0.5f, c.y + 0.5f);
        public Vector2Int CellOf(Vector2 w) => new Vector2Int(Mathf.FloorToInt(w.x), Mathf.FloorToInt(w.y));
    }

    // -------------------------------------------------------------------- battle data

    public enum Side { Party, Enemy }

    public class Fighter
    {
        public string Id, Name;
        public Side Side;
        public int MaxHp, Hp, AtkMin, AtkMax;
        public float Speed;
        public bool Boss, Captured, Dead, Rare, Dazed;
        public bool Risen;          // a skeleton stands back up once - this flags it spent
        public bool Ward;           // a wisp's light: the next blow lands on nothing
        public bool WardGiven;      // a befriended wisp's one gift of light, spent
        public bool Announced;      // a friend's first turn already declared itself
        public int Poison;          // rounds of venom left - scorpion stings leave it
        public int Weaken;          // blows left at half strength - a magus's hex saps the arm
        public int Snare;           // turns held fast - a lamia's coil does not let go quickly
        public int Corrode;         // stacks of a worm's rot on the arm - each one dulls a blow
        public string BattlerPath;      // Resources path of the battler sprite
        public string Species;          // monster string key - set on wild foes and befriended allies
        public string ColorDir;         // party only: "color_1"
        public int Look = -1;           // party only: which headwear this friend wears (-1 = none)
        public int Style;               // party only: 0 strike (crit), 1 sweep (hits all), 2 mend (heals)
        public int Scale = 2;
        public bool Alive => !Dead && !Captured && Hp > 0;
        public float Hp01 => MaxHp > 0 ? Mathf.Clamp01((float)Hp / MaxHp) : 0f;
    }

    /// <summary>A wild species: one battler + one overworld sheet + stats + where it lives.</summary>
    public struct MonsterSpec
    {
        public string Name;        // string key
        public string Battler;     // "Art/Battlers/SlimeA"
        public string MapSheet;    // "Art/Mon/Monsters_01_0"
        public int Tier;           // 1..5
        public int Chapter;        // earliest chapter it appears in
        public int Hp, AtkMin, AtkMax;
        public float Speed;
        public bool Boss;
        public bool Rare;          // moonlit variant: silver, tougher, always drops gear
    }

    public static class BattleData
    {
        /// <summary>The three friends. color_1 amber, color_2 sea, color_3 moss.</summary>
        /// <summary>The three friends. They are one sprite in three palettes, so each of them also
        /// wears something of their own (Look) -- three recolours of the same child standing in a
        /// row is what a player reads as "the characters are just colour swaps".</summary>
        public struct HeroSpec
        {
            public string NameKey, ColorDir;
            public int Hp, AtkMin, AtkMax, Look;
            public float Speed;
            public int Style;   // the friend's fighting style: 0 strike, 1 sweep, 2 mend
        }

        public static readonly HeroSpec[] Party =
        {
            // amber lunges and finds weak seams (crit), sea sweeps the whole field,
            // moss keeps everyone standing: three buttons worth of tactics on one ATTACK row
            new HeroSpec{ NameKey="hero.amber", ColorDir="color_1", Hp=34, AtkMin=4, AtkMax=7, Speed=5.0f, Look=0, Style=0 },
            new HeroSpec{ NameKey="hero.sea",   ColorDir="color_2", Hp=28, AtkMin=5, AtkMax=9, Speed=4.4f, Look=1, Style=1 },
            new HeroSpec{ NameKey="hero.moss",  ColorDir="color_3", Hp=31, AtkMin=3, AtkMax=6, Speed=4.8f, Look=2, Style=2 },
        };

        /// <summary>The party actually in the fight: amber always, the others only after
        /// their recruiting talks have run. Reads Game.State.Joined so a solo opening night
        /// sends the thief in alone - the walkers still exist as world actors then, not
        /// as battle rigs.</summary>
        public static HeroSpec[] PartyActive
        {
            get
            {
                var list = new List<HeroSpec>();
                foreach (var h in Party)
                    if (h.NameKey == "hero.amber" || Game.State.Joined.Contains(h.NameKey))
                        list.Add(h);
                return list.ToArray();
            }
        }

        /// <summary>A hero's raw stats at a level: levels used to move only the journal number,
        /// now each one adds real health and edge, so grinding the fields actually pays.
        /// Equipment bonuses are added on top of this by the caller.</summary>
        public static void HeroStats(HeroSpec spec, int level, out int hp, out int atkMin, out int atkMax)
        {
            int lv = Mathf.Max(1, level) - 1;
            hp = spec.Hp + lv * 3;
            atkMin = spec.AtkMin + lv;
            atkMax = spec.AtkMax + lv;
        }

        public static string ClipPath(string colorDir, string clip) => "Art/Hero/hero/" + colorDir + "/" + clip;

        public static readonly MonsterSpec[] Bestiary =
        {
            // chapter 1 - the fields wake up
            new MonsterSpec{ Name="mon.slime",   Battler="Art/Battlers/SlimeA",   MapSheet="Art/Mon/Monsters_01_0", Tier=1, Chapter=1, Hp=16, AtkMin=2, AtkMax=4, Speed=3.2f },
            new MonsterSpec{ Name="mon.imp",     Battler="Art/Battlers/MushroomA", MapSheet="Art/Mon/Monsters_04_0", Tier=1, Chapter=1, Hp=20, AtkMin=3, AtkMax=5, Speed=3.6f },
            new MonsterSpec{ Name="mon.slime2",  Battler="Art/Battlers/SlimeB",   MapSheet="Art/Mon/Monsters_01_0", Tier=1, Chapter=1, Hp=18, AtkMin=3, AtkMax=4, Speed=3.4f },
            // chapter 2 - bolder things
            new MonsterSpec{ Name="mon.slimeB",  Battler="Art/Battlers/SlimeC",   MapSheet="Art/Mon/Monsters_03_0", Tier=2, Chapter=2, Hp=26, AtkMin=4, AtkMax=6, Speed=3.8f },
            new MonsterSpec{ Name="mon.impB",    Battler="Art/Battlers/MushroomB", MapSheet="Art/Mon/Monsters_04_0", Tier=2, Chapter=2, Hp=30, AtkMin=5, AtkMax=7, Speed=4.0f },
            new MonsterSpec{ Name="mon.wasp",    Battler="Art/Battlers/WaspA",    MapSheet="Art/Mon/Monsters_02_0", Tier=2, Chapter=2, Hp=22, AtkMin=5, AtkMax=8, Speed=5.2f },
            new MonsterSpec{ Name="mon.wasp2",   Battler="Art/Battlers/WaspB",    MapSheet="Art/Mon/Monsters_02_0", Tier=2, Chapter=2, Hp=24, AtkMin=5, AtkMax=9, Speed=5.4f },
            new MonsterSpec{ Name="mon.gloop",   Battler="Art/Battlers/SlimeD",   MapSheet="Art/Mon/Monsters_03_0", Tier=2, Chapter=2, Hp=30, AtkMin=4, AtkMax=7, Speed=3.0f },
            // chapter 3 - the dark wood
            new MonsterSpec{ Name="mon.shade",   Battler="Art/Battlers/GhostA",   MapSheet="Art/Mon/Monsters_02_0", Tier=3, Chapter=3, Hp=34, AtkMin=6, AtkMax=9, Speed=4.6f },
            new MonsterSpec{ Name="mon.bones",   Battler="Art/Battlers/SkeletonA", MapSheet="Art/Mon/Monsters_05_0", Tier=3, Chapter=3, Hp=40, AtkMin=6, AtkMax=10, Speed=4.2f },
            new MonsterSpec{ Name="mon.hob",     Battler="Art/Battlers/ScorpionA", MapSheet="Art/Mon/Monsters_03_0", Tier=3, Chapter=3, Hp=46, AtkMin=7, AtkMax=11, Speed=3.6f },
            new MonsterSpec{ Name="mon.wisp",    Battler="Art/Battlers/GeniusA",  MapSheet="Art/Mon/Monsters_05_0", Tier=3, Chapter=3, Hp=38, AtkMin=8, AtkMax=12, Speed=4.8f },
            new MonsterSpec{ Name="mon.revenant",Battler="Art/Battlers/SkeletonA", MapSheet="Pack/Monsters/Monsters_05_5", Tier=3, Chapter=3, Hp=44, AtkMin=7, AtkMax=10, Speed=3.4f },
            // the deeper-cut species: same family silhouettes in the pack's other palettes,
            // so the fields keep a face the player has not already befriended twice
            new MonsterSpec{ Name="mon.palebell",Battler="Art/Battlers/GhostA",    MapSheet="Pack/Monsters/Monsters_02_5", Tier=3, Chapter=3, Hp=30, AtkMin=5, AtkMax=8, Speed=5.0f },
            new MonsterSpec{ Name="mon.thick",   Battler="Art/Battlers/MushroomB", MapSheet="Pack/Monsters/Monsters_04_3", Tier=2, Chapter=2, Hp=36, AtkMin=6, AtkMax=9, Speed=3.4f },
            // the later dark learns new shapes: a serpent that holds its mark fast, a
            // worm whose bite rusts the arm it takes, and still stranger things in the
            // last night - a dead thing that feeds on the fallen, a pudding that parries
            new MonsterSpec{ Name="mon.lamia",   Battler="Art/Battlers/LamiaA",    MapSheet="Pack/Monsters/Monsters_03_6", Tier=2, Chapter=2, Hp=28, AtkMin=5, AtkMax=9, Speed=4.6f },
            new MonsterSpec{ Name="mon.worm",    Battler="Art/Battlers/WormA",     MapSheet="Pack/Monsters/Monsters_04_6", Tier=2, Chapter=2, Hp=26, AtkMin=4, AtkMax=7, Speed=4.0f },
            new MonsterSpec{ Name="mon.zombi",   Battler="Art/Battlers/ZombiA",    MapSheet="Pack/Monsters/Monsters_05_1", Tier=3, Chapter=3, Hp=50, AtkMin=6, AtkMax=9, Speed=2.6f },
            new MonsterSpec{ Name="mon.sword",   Battler="Art/Battlers/SlimeswordA", MapSheet="Pack/Monsters/Monsters_03_1", Tier=3, Chapter=3, Hp=44, AtkMin=8, AtkMax=12, Speed=4.2f },
            new MonsterSpec{ Name="mon.worm2",   Battler="Art/Battlers/WormB",       MapSheet="Pack/Monsters/Monsters_04_2", Tier=3, Chapter=3, Hp=38, AtkMin=6, AtkMax=10, Speed=4.4f },
            new MonsterSpec{ Name="mon.zombi2",  Battler="Art/Battlers/ZombiB",      MapSheet="Pack/Monsters/Monsters_05_3", Tier=4, Chapter=3, Hp=64, AtkMin=8, AtkMax=11, Speed=2.4f },
            new MonsterSpec{ Name="mon.sword2",  Battler="Art/Battlers/SlimeswordB", MapSheet="Pack/Monsters/Monsters_03_3", Tier=4, Chapter=3, Hp=56, AtkMin=9, AtkMax=13, Speed=4.6f },
            new MonsterSpec{ Name="mon.thirst",  Battler="Art/Battlers/SuccubusA",   MapSheet="Pack/Monsters/Monsters_02_7", Tier=3, Chapter=3, Hp=42, AtkMin=7, AtkMax=11, Speed=5.0f },
            new MonsterSpec{ Name="mon.magus",   Battler="Art/Battlers/BlackMagusA", MapSheet="Pack/Monsters/Monsters_02_6", Tier=3, Chapter=3, Hp=38, AtkMin=7, AtkMax=12, Speed=4.4f },
            new MonsterSpec{ Name="mon.swarrior",Battler="Art/Battlers/SkeletonwarriorA", MapSheet="Pack/Monsters/Monsters_05_4", Tier=3, Chapter=3, Hp=48, AtkMin=7, AtkMax=10, Speed=3.0f },
            // the gatekeepers live in the bestiary so the journal can picture them, but they
            // are not field spawns - every one carries the boss flag and Roll never deals it
            new MonsterSpec{ Name="mon.stalker", Battler="Art/Battlers/ScorpionA", MapSheet="Art/Mon/Monsters_03_0", Tier=2, Chapter=1, Hp=42, AtkMin=4, AtkMax=7, Speed=4.0f, Boss=true },
            new MonsterSpec{ Name="mon.thane",   Battler="Art/Battlers/MinotaurB", MapSheet="Pack/Monsters/Monsters_04_5", Tier=4, Chapter=2, Hp=62, AtkMin=6, AtkMax=11, Speed=3.8f, Boss=true },
            new MonsterSpec{ Name="mon.squire", Battler="Art/Battlers/GhostA",    MapSheet="Art/Mon/Monsters_02_0", Tier=2, Chapter=2, Hp=20, AtkMin=5, AtkMax=8, Speed=5.0f, Boss=true },
            // the Guard's own lantern counts as a creature of the night too: kept out of
            // the wild pool by the flag, but findable by Species() so the bell's fight can
            // deal it and a kind word can carry one home - before it lived only inside
            // BossFight(3), so the bell's promised fight found nothing and a tamed wisp
            // could never be rebuilt into the party
            new MonsterSpec{ Name="mon.wisp", Battler="Art/Battlers/GeniusA",   MapSheet="Art/Mon/Monsters_05_0", Tier=3, Chapter=3, Hp=28, AtkMin=6, AtkMax=10, Speed=5.2f, Boss=true },
        };

        public static readonly MonsterSpec Boss = new MonsterSpec
        {
            Name = "mon.minotaur", Battler = "Art/Battlers/MinotaurA", MapSheet = "Art/Mon/Monsters_04_0",
            Tier = 5, Chapter = 3, Hp = 92, AtkMin = 8, AtkMax = 13, Speed = 4.4f, Boss = true
        };

        /// <summary>Look a wild species up by its string key (befriended allies rebuild from it).</summary>
        public static MonsterSpec? Species(string key)
        {
            foreach (var s in Bestiary) if (s.Name == key) return s;
            return null;
        }

        /// <summary>The creature's family read off its battler file: SlimeA and SlimeD are the
        /// same kind of thing whatever their tier. Weaknesses hang off this, so a recolour or a
        /// named oddity (the palebell is a ghost) inherits the weakness its sprite promises.</summary>
        public static string FamilyOf(MonsterSpec s)
        {
            var b = s.Battler ?? "";
            var n = b.Substring(b.LastIndexOf('/') + 1);
            // every battler file ends in a single tier letter - SlimeA, SlimeD, GhostC -
            // so the family is the name with that last char dropped. Taking all leading
            // letters instead kept the suffix ("slimea"), which quietly silenced every
            // family-keyed trick and weakness in the game.
            return n.Length > 1 ? n.Substring(0, n.Length - 1).ToLowerInvariant() : "";
        }

        /// <summary>Style vs species: every friend's trick has a family it was made for. Amber's
        /// claws open fleshy beasts, sea's arc scatters swarm and sting, moss's moon-petals
        /// banish the dead. A true answer is worth half again the damage, so picking the right
        /// attacker matters more than picking the strongest.</summary>
        public static bool StyleBeats(int style, MonsterSpec foe)
        {
            switch (FamilyOf(foe))
            {
                case "slime":
                case "slimesword":
                case "mushroom": return style == 0;
                case "wasp":
                case "scorpion":
                case "lamia":
                case "worm": return style == 1;          // the field's own hunger parts the same
                case "zombi": return style == 2;         // dead flesh folds to the moon's fold
                case "ghost":
                case "skeleton":
                case "skeletonwarrior":
                case "succubus":
                case "blackmagus": return style == 2;   // the dark's own fold to the moon
                default: return false;   // genius and minotaur have no soft seam
            }
        }

        /// <summary>The style a tamed beast fights in - the one that answers its own family.
        /// A befriended ghost folds like moss, a befriended wasp arcs like sea; whatever
        /// shares no soft seam (genius, minotaur) just claws like amber.
        /// </summary>
        public static int StyleForFamily(string fam)
        {
            switch (fam)
            {
                case "slime":
                case "slimesword":
                case "mushroom": return 0;
                case "wasp":
                case "scorpion":
                case "lamia":
                case "worm": return 1;
                case "zombi":
                case "ghost":
                case "skeleton":
                case "skeletonwarrior":
                case "succubus":
                case "blackmagus": return 2;
                default: return 0;
            }
        }

        /// <summary>A random encounter for a chapter. Usually one foe, sometimes two.</summary>
        public static MonsterSpec[] Roll(int chapter, System.Random rng)
        {
            var pool = new List<MonsterSpec>();
            foreach (var s in Bestiary) if (s.Chapter <= chapter && !s.Boss) pool.Add(s);
            var a = pool[rng.Next(pool.Count)];
            if (rng.Next(100) < 10) a.Rare = true;
            if (chapter >= 2 && rng.Next(100) < 35)
            {
                var b = pool[rng.Next(pool.Count)];
                if (rng.Next(100) < 10) b.Rare = true;   // moonlit pairs happen too
                // the last night's dark hunts in packs: now and then the field answers
                // with three at once
                if (chapter >= 3 && rng.Next(100) < 22)
                    return new[] { a, b, pool[rng.Next(pool.Count)] };
                return new[] { a, b };
            }
            return new[] { a };
        }

        /// <summary>Each night's road ends in its own keeper. The first is a lone dusk
        /// stalker - a teaching fight, one target, every command already matters. The second
        /// is the Night Thane with a shade squire screening it: two targets, so MORSEL and
        /// BEFRIEND have a use under pressure. The third is the Pale Guard proper, screened
        /// by a lantern wisp - the night owes you a real wall before the cristal.</summary>
        public static MonsterSpec[] BossFight(int chapter)
        {
            if (chapter <= 1) return new[] { Species("mon.stalker").Value };
            if (chapter == 2)
            {
                // the squire screens the thane; it is not itself a boss, so a daze or a kind word
                // still lands on it. The flag in the bestiary only keeps it out of the wild pool.
                var squire = Species("mon.squire").Value;
                squire.Boss = false;
                return new[] { Species("mon.thane").Value, squire };
            }
            // the Pale Guard never walks alone: a lantern wisp screens it. The fight
            // used to be one big health bar, which made MORSEL and BEFRIEND pointless at the
            // climax - two targets keeps every command relevant to the last turn. The wisp
            // is not itself a keeper, so the word and the morsel can still reach it.
            var wisp = Species("mon.wisp").Value;
            wisp.Boss = false;
            return new[] { Boss, wisp };
        }

        /// <summary>Who the hero is talking to at the gate - name plate and taunt lines.</summary>
        public static string BossNameKey(int chapter)
            => chapter <= 1 ? "mon.stalker" : chapter == 2 ? "mon.thane" : "mon.minotaur";

        public static string[] BossTaunts(int chapter)
            => chapter <= 1 ? new[] { "boss1.t.1", "boss1.t.2" }
                : chapter == 2 ? new[] { "boss2.t.1", "boss2.t.2" }
                : new[] { "boss.taunt.1", "boss.taunt.2" };
    }

    // -------------------------------------------------------------------- npc + dialog

    public struct NpcDef
    {
        public int Chara;      // chara sheet index (used when Sheet is empty)
        public string Sheet;   // full Resources path of a chara sheet, for the wider cast
        public Vector2 Pos;    // world position (cell center)
        public string NameKey; // string key of the name
        public string[] Lines; // string keys spoken in order
        public bool Shop;      // tapping opens the shop instead of a dialog
        public bool Monster;   // portrait comes off a 48px monster cell, not a chara sheet
        // walk-anim folk: a Resources path to an animation bank instead of a chara sheet
        // (the companions are drawn by the same rigs that fight beside you)
        public string Walk;
        // folk who can join the walk: the battle-party key their recruiting talk turns on
        public string JoinKey;
    }

    public static class Folks
    {
        public const string FallbackSheet = "Art/Char/chara_0";

        /// <summary>The sheet a villager is drawn from: the classic six live in Art/Char (sliced
        /// for the dialogue portrait), the rest walk straight out of the pack's own Chara folder.
        /// Picking by path means a new villager is a name and a line of dialog, not a copied file.</summary>
        public static string Sheet(NpcDef def)
            => string.IsNullOrEmpty(def.Sheet) ? "Art/Char/chara_" + def.Chara : def.Sheet;

        /// <summary>The two wanderers who can join the walk. Sea waits at the village's
        /// north edge with her knives; Moss stands deeper in the fields where the wild is
        /// thicker. Both are drawn by their own hero rigs, not a villager sheet, so the
        /// one who talks is the one who fights beside you later. Their JoinKey gates the
        /// recruiting talk into a join, and once joined their wandering selves leave the
        /// map (the trail walkers take over). Positions sit inside the chest/boss ring so
        /// the night route stays one line: Mira -> Sea -> Moss -> work.</summary>
        public static NpcDef[] Companions()
        {
            return new[]
            {
                new NpcDef{ Chara = 0, Pos = new Vector2(27.5f, 16.5f),
                    NameKey = "npc.sea",
                    Lines = new[] { "dl.sea.1", "dl.sea.2" },
                    Walk = "Art/Hero/hero/color_2/walk/hero_walk_DOWN",
                    JoinKey = "hero.sea" },
                new NpcDef{ Chara = 0, Pos = new Vector2(30.5f, 38.5f),
                    NameKey = "npc.moss",
                    Lines = new[] { "dl.moss.1", "dl.moss.2" },
                    Walk = "Art/Hero/hero/color_3/walk/hero_walk_DOWN",
                    JoinKey = "hero.moss" },
            };
        }

        /// <summary>Who is at home in each of the six village houses. One per room, so opening a
        /// door always leads to somebody with something to say.</summary>
        public static NpcDef[] House(int houseIndex, int chapter)
        {
            int h = Mathf.Abs(houseIndex) % 6;
            // each hearth gets its own face out of the pack's drawer - the old chroma table
            // dressed the six residents in the same sheets the elder, the kid, the smith,
            // the hunter and the bard already wear in the street
            var sheets = new[] { "Pack/Chara/chara_0", "Pack/Chara/chara_9", "Pack/Chara/chara_13",
                                 "Pack/Chara/chara_15", "Pack/Chara/chara_17", "Pack/Chara/chara_24" };
            var names = new[] { "npc.house.0", "npc.house.1", "npc.house.2", "npc.house.3", "npc.house.4", "npc.house.5" };
            return new[]
            {
                new NpcDef
                {
                    Chara = 0, Sheet = sheets[h], Pos = new Vector2(9.5f, 12.5f), NameKey = names[h],
                    Lines = new[] { "dl.home." + h + ".1", "dl.home." + h + ".2" },
                }
            };
        }

        /// <summary>Village NPCs, fixed positions per chapter (newcomers arrive as the night lifts).</summary>
        public static NpcDef[] Village(int chapter)
        {
            int c = Mathf.Clamp(chapter, 1, 3);
            var all = new List<NpcDef>
            {
                // Mira carries the story: her three lines change with the night
                new NpcDef{ Chara=1, Pos=new Vector2(27.5f,7.5f), NameKey="npc.elder",
                    Lines=new[]{ "dl.elder." + c + ".1", "dl.elder." + c + ".2", "dl.elder." + c + ".3" } },
                new NpcDef{ Chara=2, Pos=new Vector2(33.5f,5.5f), NameKey="npc.kid",
                    Lines=new[]{ "dl.kid.1", "dl.kid.2", "dl.kid.3" } },
                new NpcDef{ Chara=3, Pos=new Vector2(35.5f,11.5f), NameKey="npc.smith",
                    Lines=new[]{ "dl.smith.1", "dl.smith.2" } },
                // The pack's 32 villager sheets are 14 silhouettes in several palettes, so a
                // newcomer has to be picked by shape: 7 and 15 are recolours of 5 and 6 (which the
                // village already fields) and 12 of 4. 8, 11, 20 and 21 are shapes of their own --
                // 11 walks under a long cloak, 20 has a hood over the face, 21 wears a crown.
                new NpcDef{ Chara=0, Sheet="Pack/Chara/chara_11", Pos=new Vector2(22.5f,8.5f),  NameKey="npc.finn",
                    Lines=new[]{ "dl.finn.1", "dl.finn.2" } },
                new NpcDef{ Chara=0, Sheet="Pack/Chara/chara_8",  Pos=new Vector2(31.5f,9.5f),  NameKey="npc.pip",
                    Lines=new[]{ "dl.pip.1", "dl.pip.2" } },
                new NpcDef{ Chara=0, Sheet="Pack/Chara/chara_21", Pos=new Vector2(28.5f,11.5f), NameKey="npc.prune",
                    Lines=new[]{ "dl.prune.1", "dl.prune.2" } },
                // Marn keeps the stall: gold finally has somewhere to go
                new NpcDef{ Chara=0, Sheet="Pack/Chara/chara_14", Pos=new Vector2(24.5f,9.5f), NameKey="npc.marn",
                    Lines=new[]{ "dl.marn.1", "dl.marn.2", "dl.marn.3" }, Shop=true },
                // the grandmother has always lived here - her mushroom errand is a first-night
                // task, so she cannot wait for the third night to exist. The white bonnet is
                // her own face: atlas slot 0 is the same sheet house five's resident wears,
                // and two villagers sharing a face is the thing the night was asked to avoid
                new NpcDef{ Chara=0, Sheet="Pack/Chara/chara_19", Pos=new Vector2(20.5f,6.5f), NameKey="npc.grandma",
                    Lines=new[]{ "dl.grandma.1", "dl.grandma.2" } },
            };
            if (chapter >= 2)
            {
                all.Add(new NpcDef{ Chara=4, Pos=new Vector2(24.5f,11.5f), NameKey="npc.hunter",
                    Lines=new[]{ "dl.hunter.1", "dl.hunter.2", "dl.hunter.3" } });
                all.Add(new NpcDef{ Chara=0, Sheet="Pack/Chara/chara_10", Pos=new Vector2(26.5f,13.5f), NameKey="npc.oda",
                    Lines=new[]{ "dl.oda.1", "dl.oda.2" } });
                // the bard walks out once the fields open - her rhyme errand is a second-night
                // task, so she has to exist on the second night
                all.Add(new NpcDef{ Chara=5, Pos=new Vector2(30.5f,12.5f), NameKey="npc.bard",
                    Lines=new[]{ "dl.bard.1", "dl.bard.2" } });
            }
            if (chapter >= 3)
            {
                all.Add(new NpcDef{ Chara=0, Sheet="Pack/Chara/chara_20", Pos=new Vector2(34.5f,6.5f), NameKey="npc.nail",
                    Lines=new[]{ "dl.nail.1", "dl.nail.2" } });
            }
            return all.ToArray();
        }
    }
}
