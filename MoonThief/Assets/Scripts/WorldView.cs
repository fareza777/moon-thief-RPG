using System.Collections.Generic;
using UnityEngine;

namespace MoonThief
{
    /// <summary>
    /// The explorable world: tile meshes built from the legacy atlas, the walking hero,
    /// wandering wild monsters, villagers, chests, the crystal and the night light layer.
    /// All rendering is code-driven; no prefabs, no scene wiring.
    /// </summary>
    public class WorldView : MonoBehaviour
    {
        public class Actor
        {
            public Transform Root;      // world position (feet)
            public Transform Body;      // bob offset
            public SpriteRenderer Sr;
            public Anim Anim;
            public SpriteRenderer Shadow;
            public PixelLabel Name;
            public Dir Facing = Dir.Down;
            public float Speed = 3f;
            public Vector2 HomeCell;
            public bool Aggro;
            public float AggroT;      // windup under the "!" before the chase begins
            public float Pause;         // idle pause before moving again
            public Vector2Int Dest;
            public MonsterSpec Spec;
            public NpcDef Npc;
            public bool IsNpc;
            public string Art;          // the walk sheet this actor cycles (critters, see Strip)
            public SpriteRenderer Alert;   // "!" bubble shown while aggro-chasing
            public SpriteRenderer Rare;    // star a moonlit wild thing wears overhead
            public bool Asleep;            // some beasts doze: deaf to eyes, still hear steps
            public bool Sleeps;            // born a dozer - settles back down after a scare
            public float DozeT;            // countdown back to sleep once the coast is clear
            public SpriteRenderer SleepMark;  // "z" drifting off a sleeper
            public SpriteRenderer NameChip;   // the tag behind the name; hidden with the name
            public float FadeIn;        // respawn materialise: alpha ramps in over ~0.9s
        }

        public GameMap Map;
        public Actor Hero;
        public readonly List<Actor> Monsters = new List<Actor>();
        public readonly List<Actor> Npcs = new List<Actor>();
        public PixelLabel ZoneBanner;
        public PixelLabel ZoneBannerSub;
        public SpriteRenderer ZoneChip;

        /// <summary>Text the front end is currently showing in a corner of the screen (the toast).
        /// World name plates keep out of it: two texts in the same place is the one thing a
        /// bitmap font cannot survive.</summary>
        public Rect UiBlock;
        public Transform HudRoot;          // screen-fixed elements, follows the camera
        public int MapChapter { get; set; }

        Transform _root;
        MeshFilter _groundMf, _overMf;
        MeshRenderer _groundMr, _overMr;
        MeshFilter _decoMf;
        MeshRenderer _decoMr;

        readonly List<SpriteRenderer> _props = new List<SpriteRenderer>();
        readonly List<SpriteRenderer> _glows = new List<SpriteRenderer>();
        // the peak alpha of each glow, in the same order as _glows: the flicker loop drives
        // all of them from one curve, and a campfire, a lamppost and a chest halo must not
        // share one brightness or the field turns into a pond of light
        readonly List<float> _glowAmp = new List<float>();
        readonly List<Transform> _flies = new List<Transform>();
        readonly List<SpriteRenderer> _flySprites = new List<SpriteRenderer>();   // cached: no per-frame GetComponent
        readonly List<SpriteRenderer> _water = new List<SpriteRenderer>();
        readonly List<Actor> _critters = new List<Actor>();
        // the friends at the hero's heels and the breadcrumb path they walk: a crumb lands
        // under the hero every quarter tile and each friend marches to its own slot back
        // along the line, so the trail bends exactly the way the hero bent
        readonly List<Actor> _friends = new List<Actor>();
        readonly List<Vector3> _crumbs = new List<Vector3>();
        // pooled world puffs: footstep dust under the hero and the gold flecks a chest
        // throws when it pops. Same loop, different sprites and velocities.
        class Dust { public SpriteRenderer Sr; public Vector2 V; public float T; public float MaxT; public float A; }
        readonly List<Dust> _dust = new List<Dust>();
        // a loud step's tell: a thin ring that peels off the heel and fades before it
        // reaches the ears it would wake - only ever shown when a beast is near enough
        // to hear it, so the ripple is a warning, not wallpaper
        class Ripple { public SpriteRenderer Sr; public float T; }
        readonly List<Ripple> _ripples = new List<Ripple>();
        float _snoreT;   // the dozers breathe on one shared clock, not twenty rumbles at once
        SpriteRenderer _touchCue;
        // One direction strip per sheet and facing. A turn swaps to an array that already
        // exists, so actors of the same kind share sprites and no walk cycle is built twice.
        readonly Dictionary<string, Sprite[]> _clipCache = new Dictionary<string, Sprite[]>();
        SpriteRenderer _bossProp;
        SpriteRenderer _dimmer, _moonIcon, _cristalSr;
        Anim _cristalAnim;
        SpriteRenderer _cristalGlow;
        SpriteRenderer _vignette;
        ChestDef[] _chests;
        float _time;

        public struct ChestDef
        {
            public Vector2 Pos;
            public Vector2Int Cell;
            public SpriteRenderer Sr;
            public SpriteRenderer Glow;
            public Anim Anim;
            public int Variant;          // which of the seven chest sheets this one is
            public bool Opened;
            public string LootKey;
            public bool IsMimic;         // teeth, not treasure - the wild keeps chests too
            public MonsterSpec? Mimic;   // what unfolds when the lid is touched
        }

        /// <summary>A felled monster's ticket home: the species, the tile it guarded, and when
        /// it may come back. The wild used to repopulate the instant a fight ended, which made
        /// the fields feel bottomless - now the road breathes for a while first.</summary>
        struct Respawn
        {
            public MonsterSpec Spec;
            public Vector2 Home;
            public float T;
        }
        readonly List<Respawn> _respawns = new List<Respawn>();

        /// <summary>Seconds before a defeated monster's spot stirs again (a little random,
        /// and never while the hero is standing on it).</summary>
        public static float RespawnDelay => 30f + UnityEngine.Random.value * 45f;

        public bool Ready { get; private set; }

        /// <summary>How many hunters currently have the scent - the frontend reads the
        /// rising edge of this to give the company its nerves.</summary>
        public int AggroCount
        {
            get
            {
                int n = 0;
                foreach (var m in Monsters) if (m.Aggro) n++;
                return n;
            }
        }

        // ---------------------------------------------------------------- build

        public void Build(GameMap map, Transform parent, float halfH)
        {
            Map = map;
            HalfH = halfH;
            _textOn = true;   // a fresh world always starts with its text showing
            // parent under this component's host: Game toggles World.gameObject.SetActive,
            // which previously left the world (and its order-2000 night dimmer) visible
            // on top of the battle overlay.
            _root = new GameObject("world").transform;
            _root.SetParent(transform, false);

            BuildGroundMesh();
            BuildDecoMesh();

            // A house interior is its own map: floor, masonry, furniture, one occupant and a
            // doormat. None of the outdoor machinery (canopy, critters, wild monsters, the
            // night gradient over the whole map) belongs in a bedroom.
            if (map.Interior)
            {
                BuildProps();
                BuildChests();
                SpawnNpcs(Folks.House(map.HouseIndex, Game.State.Chapter));
                _dimmer = SpriteRendererUtil.Make(_root, "roomDim", TexArt.NightTex(), 2000);
                _dimmer.transform.localPosition = new Vector3(GameMap.W * 0.5f, GameMap.H * 0.5f, 0f);
                _dimmer.transform.localScale = new Vector3(GameMap.W + 8f, GameMap.H + 8f, 1f);
                _dimmer.transform.localEulerAngles = new Vector3(0f, 0f, 180f);
                _dimmer.color = new Color(1f, 0.9f, 0.72f, 0.42f);   // warm and much weaker
                BuildHud(halfH);
                Ready = true;
                return;
            }

            BuildWaterAnim();
            BuildOverheadMesh();
            BuildProps();
            BuildDoorways();
            BuildRimTrees();

            // night dimmer: a vertical gradient (dense at the horizon, thin overhead)
            // reads as atmosphere instead of a flat blue wash over everything
            _dimmer = SpriteRendererUtil.Make(_root, "nightDim", TexArt.NightTex(), 2000);
            _dimmer.transform.localPosition = new Vector3(GameMap.W * 0.5f, GameMap.H * 0.5f, 0f);
            _dimmer.transform.localScale = new Vector3(GameMap.W + 8f, GameMap.H + 8f, 1f);
            _dimmer.transform.localEulerAngles = new Vector3(0f, 0f, 180f);   // dark edge at the top
            _dimmer.color = new Color(1f, 1f, 1f, 0.92f);

            BuildCristal();
            BuildChests();
            BuildEventSpots();
            SpawnNpcs(Folks.Village(Game.State.Chapter));
            // the companions stand their ground until they say yes: after that their
            // wandering selves are gone and the walkers carry them instead
            SpawnNpcs(Folks.Companions());
            SyncParty();
            BuildCritters();
            SpawnMonsters();
            BuildFireflies();

            // 2020: above the light pools (2010) and the fireflies (2015). The vignette is the
            // camera's frame, so it has to be the last thing over the world - under the lights
            // it was lit up by them and stopped being a frame at all.
            _vignette = SpriteRendererUtil.Make(_root, "vignette", TexArt.Vignette(), 2020);
            _vignette.transform.localPosition = new Vector3(GameMap.W * 0.5f, GameMap.H * 0.5f, 0f);
            _vignette.transform.localScale = new Vector3(24f, 44f, 1f);
            _vignette.color = new Color(1f, 1f, 1f, 0.85f);

            BuildHud(halfH);
            Ready = true;
        }

        void BuildGroundMesh()
        {
            var go = new GameObject("ground");
            go.transform.SetParent(_root, false);
            _groundMf = go.AddComponent<MeshFilter>();
            _groundMr = go.AddComponent<MeshRenderer>();
            var mesh = new Mesh { name = "ground" };

            int W = GameMap.W, H = GameMap.H;
            var verts = new List<Vector3>(W * H * 4);
            var uvs = new List<Vector2>(W * H * 4);
            var tris = new List<int>(W * H * 6);
            var colors = new List<Color32>(W * H * 4);
            var tex = TexArt.WorldAtlas();
            float tw = 16f / tex.width, th = 16f / tex.height;
            int cols = tex.width / 16, rows = tex.height / 16;

            // per-tile shade: cheaper than real lights, gives the night depth
            for (int y = 0; y < H; y++)
            {
                for (int x = 0; x < W; x++)
                {
                    var g = Map.At(new Vector2Int(x, y));
                    int tile = TileIndex(x, y, g);
                    // vertex order: bl, br, tl, tr (Unity UV v grows upward)
                    var tileCol = tile % cols;
                    var tileRow = tile / cols;
                    // atlas row 0 is the TOP of the image
                    float u0 = tileCol * tw;
                    float u1 = u0 + tw;
                    float v1 = 1f - tileRow * th;
                    float v0 = v1 - th;

                    int b = verts.Count;
                    verts.Add(new Vector3(x, y, 0f));
                    verts.Add(new Vector3(x + 1, y, 0f));
                    verts.Add(new Vector3(x, y + 1, 0f));
                    verts.Add(new Vector3(x + 1, y + 1, 0f));
                    uvs.Add(new Vector2(u0, v0));
                    uvs.Add(new Vector2(u1, v0));
                    uvs.Add(new Vector2(u0, v1));
                    uvs.Add(new Vector2(u1, v1));
                    byte s = ShadeFor(x, y, g);
                    colors.Add(new Color32(s, s, s, 255));
                    colors.Add(new Color32(s, s, s, 255));
                    colors.Add(new Color32(s, s, s, 255));
                    colors.Add(new Color32(s, s, s, 255));
                    tris.Add(b); tris.Add(b + 2); tris.Add(b + 1);
                    tris.Add(b + 1); tris.Add(b + 2); tris.Add(b + 3);

                    // soft shore shadow where water meets land: darkens the land side
                    // so the pond edge reads without the (blank) edge tiles
                    if (g != Ground.Water && WaterNeighbor(x, y))
                    {
                        Overlay(verts, uvs, tris, colors, x, y, new Color32(6, 8, 20, 120));
                    }
                    // A road is not a rectangle cut out of a meadow: the verge is trodden and
                    // darker. The pack ships no blend tiles, so the shoulder is a tinted quad -
                    // the same trick as the shoreline above.
                    else if (g == Ground.Grass && PathNeighbor(x, y))
                    {
                        Overlay(verts, uvs, tris, colors, x, y, new Color32(52, 36, 22, 78));
                    }
                }
            }
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.SetColors(colors);
            mesh.RecalculateNormals();
            _groundMf.sharedMesh = mesh;
            _groundMr.sharedMaterial = SpriteRendererUtil.SpriteMat(tex);
            _groundMr.sortingOrder = 0;
        }

        int TileIndex(int x, int y, Ground g)
        {
            return g switch
            {
                Ground.Path => Tiles.Path,
                Ground.Water => Tiles.Water,
                Ground.Floor => Hash01(x, y) < 0.22f ? Tiles.FloorAlt : Tiles.Floor,
                // the wall tile that sits right above the floor gets the footing, so the
                // room reads as a wall standing on a floor instead of two flat sheets
                Ground.Wall => Map.At(new Vector2Int(x, y - 1)) == Ground.Floor
                    ? Tiles.WallBase : Tiles.Wall,
                Ground.Void => Tiles.Void,
                _ => Tiles.Grass
            };
        }

        /// <summary>Selftest only: kill every layer that could dim the room - the dimmer
        /// gradient, prop halos, contact shadows, touch cue - so a dark frame can be
        /// blamed on what is left standing (props, actors, the ground mesh itself).</summary>
        public void DebugStripLayers()
        {
            if (_dimmer != null) _dimmer.enabled = false;
            foreach (var g in _glows) if (g != null) g.enabled = false;
            if (_touchCue != null) _touchCue.enabled = false;
            foreach (var sr in _root.GetComponentsInChildren<SpriteRenderer>(true))
                if (sr != null && (sr.name.StartsWith("psh") || sr.name.StartsWith("csh") || sr.name.StartsWith("sh"))) sr.enabled = false;
        }

        /// <summary>Selftest only: kill EVERY sprite under the world root - what is left is
        /// the ground and deco meshes alone, the last suspects that cannot lie.</summary>
        public void DebugStripAllSprites()
        {
            foreach (var sr in _root.GetComponentsInChildren<SpriteRenderer>(true))
                if (sr != null) sr.enabled = false;
        }

        /// <summary>Selftest only: print the ground mesh's real vertex data for a few cells -
        /// position, uv, vertex color - so a dark cell can be blamed on uv or color directly.</summary>
        public void DumpGroundVerts()
        {
            if (_groundMf == null || _groundMf.sharedMesh == null) return;
            var m = _groundMf.sharedMesh;
            var vs = m.vertices; var us = m.uv; var cs = m.colors32;
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"counts v={vs.Length} uv={us.Length} c={cs.Length}");
            int dark = 0;
            for (int i = 0; i < vs.Length; i += 4)
            {
                if (cs.Length <= i || cs[i].r > 40) continue;
                var p = vs[i];
                int x = Mathf.RoundToInt(p.x), y = Mathf.RoundToInt(p.y);
                var g = Map != null ? Map.At(new Vector2Int(x, y)) : Ground.Block;
                sb.AppendLine($"({x},{y}) pos={p} uv={us[i]} c={cs[i]} g={g} tile={TileIndex(x, y, g)} shade={ShadeFor(x, y, g)}");
                if (++dark > 24) { sb.AppendLine("..."); break; }
            }
            if (dark == 0) sb.AppendLine("no dark verts");
            UnityEngine.Debug.Log("[groundverts]\n" + sb);
        }

        /// <summary>Diagnostics: one line per interior cell, tile id + shade - the dump the
        /// prop audit cannot fake, because it replays the mesh's own chooser.</summary>
        public void DumpRoomTiles()
        {
            var sb = new System.Text.StringBuilder();
            for (int yy = 24; yy >= 5; yy--)
            {
                for (int xx = 0; xx < 19; xx++)
                {
                    var g = Map.At(new Vector2Int(xx, yy));
                    int t = TileIndex(xx, yy, g) - TexArt.InteriorBase;
                    sb.Append(t < 0 ? '?' : (char)('0' + t));
                }
                sb.Append('|');
                for (int xx = 0; xx < 19; xx += 2)
                    sb.Append((char)('0' + Mathf.Clamp(ShadeFor(xx, yy, Map.At(new Vector2Int(xx, yy))) / 28, 0, 9)));
                sb.Append('\n');
            }
            UnityEngine.Debug.Log("[roomtiles] tile|shade\n" + sb);
        }

        byte ShadeFor(int x, int y, Ground g)
        {
            if (g == Ground.Water) return 190;
            // vignette: darker toward the map edges, plus a soft band per zone
            float dx = Mathf.Abs(x - GameMap.W * 0.5f) / (GameMap.W * 0.5f);
            float shade = 1f;
            if (y > 59) shade = 0.72f;                 // forest is gloomy even by day
            else if (y > 27) shade = 0.92f;
            shade *= Mathf.Lerp(0.78f, 1f, 1f - dx * 0.55f);

            // Two scales of mottling: a broad drift (11 tiles) plus a fine patch (4). One tile
            // of flat colour per cell is what made the fields look like painted card rather
            // than ground - the eye reads the slow light and shade as a rising meadow.
            float mott = 0.55f * Noise(x, y, 11) + 0.45f * Noise(x, y, 4);
            shade *= Mathf.Lerp(0.90f, 1.09f, mott);

            // dirt paths read warmer and slightly brighter than the grass
            if (g == Ground.Path) shade = Mathf.Clamp01(shade * 1.08f + 0.05f);

            // indoors the light comes from the lamps, not from a sky: no zone band, no edge
            // vignette, just a soft grain on the boards. The grain lerps past 1.0 on purpose -
            // but an unclamped *255 wraps the byte, so the brightest cells came out black.
            if (g == Ground.Floor)
            {
                float grain = Mathf.Lerp(0.90f, 1.04f, Noise(x, y, 3));
                // a one-board shade band along the masonry: reads as the walls casting onto
                // the floor, so the room has depth instead of one flat bright rectangle
                if (Map != null && (Map.At(new Vector2Int(x - 1, y)) == Ground.Wall ||
                                    Map.At(new Vector2Int(x + 1, y)) == Ground.Wall ||
                                    Map.At(new Vector2Int(x, y - 1)) == Ground.Wall ||
                                    Map.At(new Vector2Int(x, y + 1)) == Ground.Wall))
                    grain *= 0.74f;
                return (byte)(Mathf.Clamp01(grain) * 255f);
            }
            if (g == Ground.Wall || g == Ground.Void) return 255;

            // forest floor: the canopy quads above are nudged off the tile grid so the wood
            // has a ragged silhouette, and that opens slivers between the crowns. Bright
            // meadow grass showing through them read as holes punched in the tree tops.
            if (g == Ground.Tree) shade *= 0.42f;
            return (byte)(Mathf.Clamp01(shade) * 255f);
        }

        /// <summary>Deterministic value noise on the tile grid. Cheap, stable between runs and
        /// identical for the ground mesh and the decor mesh, which is what keeps them in step.</summary>
        static float Noise(int x, int y, int cell)
        {
            float fx = x / (float)cell, fy = y / (float)cell;
            int x0 = Mathf.FloorToInt(fx), y0 = Mathf.FloorToInt(fy);
            float tx = fx - x0, ty = fy - y0;
            tx = tx * tx * (3f - 2f * tx);
            ty = ty * ty * (3f - 2f * ty);
            return Mathf.Lerp(
                Mathf.Lerp(Hash01(x0, y0), Hash01(x0 + 1, y0), tx),
                Mathf.Lerp(Hash01(x0, y0 + 1), Hash01(x0 + 1, y0 + 1), tx), ty);
        }

        static float Hash01(int x, int y)
        {
            int h = x * 374761393 + y * 668265263;
            h = (h ^ (h >> 13)) * 1274126177;
            h ^= h >> 16;
            return (h & 0xFFFF) / 65535f;
        }

        /// <summary>Appends one flat-coloured quad on top of a ground cell. Used for the soft
        /// overlays (shoreline, road verge) that fake the blend tiles the pack does not ship.</summary>
        static void Overlay(List<Vector3> verts, List<Vector2> uvs, List<int> tris, List<Color32> colors,
            int x, int y, Color32 col)
        {
            int b = verts.Count;
            verts.Add(new Vector3(x, y, 0f));
            verts.Add(new Vector3(x + 1, y, 0f));
            verts.Add(new Vector3(x, y + 1, 0f));
            verts.Add(new Vector3(x + 1, y + 1, 0f));
            for (int k = 0; k < 4; k++) uvs.Add(new Vector2(0f, 0f));
            for (int k = 0; k < 4; k++) colors.Add(col);
            tris.Add(b); tris.Add(b + 2); tris.Add(b + 1);
            tris.Add(b + 1); tris.Add(b + 2); tris.Add(b + 3);
        }

        bool WaterNeighbor(int x, int y)
        {
            return IsWater(x - 1, y) || IsWater(x + 1, y) || IsWater(x, y - 1) || IsWater(x, y + 1);
        }

        bool PathNeighbor(int x, int y)
        {
            return IsPath(x - 1, y) || IsPath(x + 1, y) || IsPath(x, y - 1) || IsPath(x, y + 1);
        }

        bool IsWater(int x, int y)
        {
            if (Map == null || x < 0 || y < 0 || x >= GameMap.W || y >= GameMap.H) return false;
            return Map.At(new Vector2Int(x, y)) == Ground.Water;
        }

        bool IsPath(int x, int y)
        {
            if (Map == null || x < 0 || y < 0 || x >= GameMap.W || y >= GameMap.H) return false;
            return Map.At(new Vector2Int(x, y)) == Ground.Path;
        }

        void BuildDecoMesh()
        {
            var go = new GameObject("deco");
            go.transform.SetParent(_root, false);
            _decoMf = go.AddComponent<MeshFilter>();
            _decoMr = go.AddComponent<MeshRenderer>();
            var mesh = new Mesh { name = "deco" };

            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            var tex = TexArt.WorldAtlas();
            float tw = 16f / tex.width, th = 16f / tex.height;
            int cols = tex.width / 16;

            for (int y = 0; y < GameMap.H; y++)
            {
                for (int x = 0; x < GameMap.W; x++)
                {
                    int tile = Map.DecoAt(new Vector2Int(x, y));
                    if (tile < 0) continue;
                    int b = verts.Count;
                    var tileCol = tile % cols;
                    var tileRow = tile / cols;
                    float u0 = tileCol * tw, u1 = u0 + tw;
                    float v1 = 1f - tileRow * th, v0 = v1 - th;
                    verts.Add(new Vector3(x, y, 0f));
                    verts.Add(new Vector3(x + 1, y, 0f));
                    verts.Add(new Vector3(x, y + 1, 0f));
                    verts.Add(new Vector3(x + 1, y + 1, 0f));
                    uvs.Add(new Vector2(u0, v0));
                    uvs.Add(new Vector2(u1, v0));
                    uvs.Add(new Vector2(u0, v1));
                    uvs.Add(new Vector2(u1, v1));
                    tris.Add(b); tris.Add(b + 2); tris.Add(b + 1);
                    tris.Add(b + 1); tris.Add(b + 2); tris.Add(b + 3);
                }
            }
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            // match the ground's per-tile shade so deco never renders brighter
            var colors2 = new List<Color32>();
            for (int i = 0; i < verts.Count; i += 4)
            {
                int x = Mathf.FloorToInt(verts[i].x), y = Mathf.FloorToInt(verts[i].y);
                byte s = ShadeFor(x, y, Map.At(new Vector2Int(x, y)));
                for (int k = 0; k < 4; k++) colors2.Add(new Color32(s, s, s, 255));
            }
            mesh.SetColors(colors2);
            _decoMf.sharedMesh = mesh;
            _decoMr.sharedMaterial = SpriteRendererUtil.SpriteMat(tex);
            _decoMr.sortingOrder = 20;   // above ground, below actors' feet
        }

        /// <summary>Tree canopies that draw over the hero when he walks behind them.</summary>
        void BuildOverheadMesh()
        {
            var go = new GameObject("overhead");
            go.transform.SetParent(_root, false);
            _overMf = go.AddComponent<MeshFilter>();
            _overMr = go.AddComponent<MeshRenderer>();

            // per-tree quads, each its own mesh is overkill: use one static list of rects
            _overQuads.Clear();
            for (int y = 0; y < GameMap.H; y++)
                for (int x = 0; x < GameMap.W; x++)
                    if (Map.At(new Vector2Int(x, y)) == Ground.Tree)
                        _overQuads.Add(new Vector2Int(x, y));
            RebuildOverhead(0f);
        }

        readonly List<Vector2Int> _overQuads = new List<Vector2Int>();

        /// <summary>Rebuilds the canopy quads, optionally hiding the ones near the hero
        /// (only used in the editor preview where there is no walk). z-test free.</summary>
        void RebuildOverhead(float heroY)
        {
            var mesh = new Mesh { name = "overhead" };
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            var colors = new List<Color32>();
            var tex = TexArt.WorldAtlas();
            float tw = 16f / tex.width, th = 16f / tex.height;
            int cols = tex.width / 16;

            foreach (var c in _overQuads)
            {
                // The tile varies per cell: one repeating tile read as wallpaper, which is what
                // made the wood look like a filled rectangle rather than foliage.
                int pick = Mathf.Clamp((int)(Hash01(c.x * 7 + 3, c.y * 13 + 5) * Tiles.Canopy.Length),
                    0, Tiles.Canopy.Length - 1);
                int tile = Tiles.Canopy[pick];
                var tileCol = tile % cols;
                var tileRow = tile / cols;
                float u0 = tileCol * tw, u1 = u0 + tw;
                float v1 = 1f - tileRow * th, v0 = v1 - th;

                // Every crown was the same flat square on the same grid line, so a wood read as
                // one green rectangle with ruler edges. The offset is a low-frequency noise, not
                // a per-tile random: neighbours move together, so the mass warps like a canopy
                // instead of tearing open at every tile seam. The 1.16 tile size keeps them
                // overlapping, and the rim of the wood is darkened so the silhouette reads as
                // individual crowns.
                float jx = (Noise(c.x, c.y, 7) - 0.5f) * 0.46f;
                float jy = (Noise(c.x + 137, c.y + 61, 7) - 0.5f) * 0.46f;
                const float over = 0.58f;   // half extent: 1.16 tile, so neighbours always meet
                float cx = c.x + 0.5f + jx, cy = c.y + 0.5f + jy;
                verts.Add(new Vector3(cx - over, cy - over, 0f));
                verts.Add(new Vector3(cx + over, cy - over, 0f));
                verts.Add(new Vector3(cx - over, cy + over, 0f));
                verts.Add(new Vector3(cx + over, cy + over, 0f));
                uvs.Add(new Vector2(u0, v0));
                uvs.Add(new Vector2(u1, v0));
                uvs.Add(new Vector2(u0, v1));
                uvs.Add(new Vector2(u1, v1));
                tris.Add(verts.Count - 4); tris.Add(verts.Count - 2); tris.Add(verts.Count - 3);
                tris.Add(verts.Count - 3); tris.Add(verts.Count - 2); tris.Add(verts.Count - 1);

                bool rim = !IsTree(c.x - 1, c.y) || !IsTree(c.x + 1, c.y)
                        || !IsTree(c.x, c.y - 1) || !IsTree(c.x, c.y + 1);
                float sh = 0.70f + 0.34f * Noise(c.x, c.y, 5);
                if (rim) sh *= 0.62f;
                var col = (byte)(Mathf.Clamp01(sh) * 255f);
                for (int k = 0; k < 4; k++) colors.Add(new Color32(col, col, col, 255));
            }
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.SetColors(colors);
            _overMf.sharedMesh = mesh;
            _overMr.sharedMaterial = SpriteRendererUtil.SpriteMat(tex);
            // 40: the canopy is the floor of the wood, so it sits above the ground and the
            // decor and *under* every prop, actor and contact shadow. At 1200 it covered the
            // wild trees standing on the grass outside it, so a tree at the wood's edge came out
            // with its crown cut off - which is what made the woods read as a pile of sprites.
            _overMr.sortingOrder = 40;
        }

        bool IsTree(int x, int y)
        {
            if (Map == null || x < 0 || y < 0 || x >= GameMap.W || y >= GameMap.H) return false;
            return Map.At(new Vector2Int(x, y)) == Ground.Tree;
        }

        /// <summary>The whole village, field and wood set, straight from the pack's prop library.
        /// Every entry comes from the map, so the layout is data and the view just draws it.</summary>
        void BuildProps()
        {
            for (int i = 0; i < Map.Props.Count; i++)
            {
                var def = Map.Props[i];
                bool flame = def.Art.StartsWith("Torches/") || def.Art.StartsWith("Fires/");
                bool lamp = def.Art.StartsWith("Lamps/") || def.Deco == "Lamp";
                bool lit = flame || lamp;
                // The object carries the art it stands for ("prop12_tree_07"), so a log line or a
                // hierarchy dump says which tree a pile is made of - the layout audit reads the
                // family straight off this name.
                string artTag = !string.IsNullOrEmpty(def.Deco) ? def.Deco : Leaf(def.Art);
                var sr = SpriteRendererUtil.Make(_root, artTag + "#" + i, null, WorldOrder(def.Pos.y));
                sr.transform.localScale = Vector3.one * def.Scale;
                var anim = sr.gameObject.AddComponent<Anim>();
                anim.Setup(sr, false, 0f);

                // furniture drawn in code (interiors) or a sprite from the pack
                if (!string.IsNullOrEmpty(def.Deco))
                {
                    sr.sprite = Furniture(def.Deco);
                    if (sr.sprite == null) { Fx.Kill(sr.gameObject); continue; }
                    if (def.Deco == "Window")
                    {
                        // a lit window spills its light onto the floor
                        var winGlow = SpriteRendererUtil.Make(_root, "wglow" + i, TexArt.Glow(), 2010);
                        winGlow.transform.localPosition = new Vector3(def.Pos.x, def.Pos.y - 0.6f, 0f);
                        winGlow.transform.localScale = new Vector3(7f, 4.4f, 1f);
                        winGlow.color = new Color(1f, 0.86f, 0.55f, 0.4f);
                        AddGlow(winGlow, 0.24f);
                    }
                }
                // props live under Pack/Props/, animated sheets under Pack/Anim/. The animated
                // branch used to build "Pack/" + art for everything, which resolved crops,
                // roadside torches and campfires to Resources/Pack/Crops/... -- a folder that does
                // not exist. Every one of them silently vanished (and took its light with it),
                // which is a large part of why the fields looked bare.
                else if (def.Frames > 0)
                {
                    var frames = TexArt.Grid((def.Art.StartsWith("Anim/") ? "Pack/" : "Pack/Props/") + def.Art,
                        def.CellW, def.CellH);
                    if (frames.Length == 0) { Fx.Kill(sr.gameObject); continue; }
                    if (flame)
                    {
                        anim.Play(frames, 9f, true);      // torches and campfires live
                        anim.SetTint(new Color(1f, 0.96f, 0.86f));
                    }
                    else if (def.Art.StartsWith("Anim/Door"))
                    {
                        // door sheets run closed (frame 0) to fully open: a house wants closed
                        anim.Play(new[] { frames[0] }, 1f, true);
                    }
                    else
                    {
                        // crops are a sheet of growth stages: take the fully grown one
                        anim.Play(new[] { frames[frames.Length - 1] }, 1f, true);
                        anim.SetTint(new Color(0.92f, 0.98f, 0.86f));
                    }
                }
                else
                {
                    sr.sprite = TexArt.Whole((def.Art.StartsWith("Anim/") ? "Pack/" : "Pack/Props/") + def.Art);
                    if (sr.sprite == null) { Fx.Kill(sr.gameObject); continue; }
                }

                // stand the sprite on the bottom edge of its tile so tall props (houses,
                // trees, columns) sit on the ground instead of sinking through it
                float half = sr.sprite != null ? sr.sprite.bounds.extents.y * def.Scale : 0.5f;
                sr.transform.localPosition = new Vector3(def.Pos.x, def.Pos.y - 0.5f + half, 0f);
                _props.Add(sr);

                // a soft contact shadow under the solid props. The ground is flat tile art, so
                // without it trees, barrels and columns read as pasted onto the field rather
                // than standing in it. Animated sheets (crops, doors, flames) stay bare.
                if (def.Frames == 0 && def.Scale >= 0.9f)
                {
                    var sh = SpriteRendererUtil.Make(_root, "psh" + i, TexArt.Shadow(), 45);
                    sh.transform.localPosition = new Vector3(def.Pos.x, def.Pos.y - 0.58f, 0f);
                    sh.transform.localScale = new Vector3(0.85f * def.Scale, 0.62f * def.Scale, 1f);
                }

                if (lit)
                {
                    // 2010: ABOVE the night dimmer (2000). These used to sit at 1990, under the
                    // very layer they were meant to cut through, so the pool of light around a
                    // torch was dimmed like everything else and the village came out as one flat
                    // wash. Squashed on x and y it lies on the ground like lamplight instead of
                    // hovering as a ball of glow, and the lamps now light their street too.
                    float gs = flame ? (def.Art.StartsWith("Fires/") ? 6.4f : 5.4f) : 4.2f;
                    var glow = SpriteRendererUtil.Make(_root, "pglow" + i, TexArt.Glow(), 2010);
                    glow.transform.localPosition = new Vector3(def.Pos.x,
                        def.Pos.y + (lamp ? 1.05f : 0.35f), 0f);
                    glow.transform.localScale = new Vector3(gs * 1.25f, gs * 0.85f, 1f);
                    glow.color = new Color(1f, lamp ? 0.88f : 0.78f, 0.52f, 0.5f);
                    AddGlow(glow, flame ? 0.34f : 0.26f);
                }
            }

            // the Pale Guard waits on the road at the top of every chapter: the blocked row
            // under it is the only way north, so the fight is the gate to the next night
            {
                var b = Map.BossPos;
                var bSr = SpriteRendererUtil.Make(_root, "bossMap", null, WorldOrder(b.y));
                bSr.transform.localPosition = new Vector3(b.x, b.y, 0f);
                bSr.transform.localScale = Vector3.one * 0.85f;
                var bAnim = bSr.gameObject.AddComponent<Anim>();
                bAnim.Setup(bSr, true, 0f);
                bAnim.Play(MonsterClip(GameMap.BossMapSheet(MapChapter), Dir.Down), 3f, true);
                _bossProp = bSr;
                var bGlow = SpriteRendererUtil.Make(_root, "bossGlow", TexArt.Glow(), 2011);
                bGlow.transform.localPosition = new Vector3(b.x, b.y + 0.6f, 0f);
                bGlow.transform.localScale = new Vector3(8f * 1.25f, 8f * 0.85f, 1f);
                bGlow.color = new Color(1f, 0.4f, 0.35f, 0.4f);
                AddGlow(bGlow, 0.30f);
            }
        }

        /// <summary>Trees along the rim of every wood. The pack's canopy tile is a flat mass with
        /// a handful of speckles in it, so a wood drawn from that tile alone comes out as a dark
        /// rectangle lying on the meadow. Real crowns on the perimeter give the wood a silhouette
        /// you can read, and they are drawn just above the canopy layer so the hero still walks
        /// behind them. Deterministic, like the rest of the world.
        ///
        /// The crowns used to be rolled per cell out of a list that ran from a 16x32 shrub to a
        /// 48x48 giant, each at up to 1.35x, all on one sorting layer. A wood edge therefore came
        /// out as a pile of unrelated sprites sharing neither species nor height, and whichever
        /// one Unity happened to draw last won. Now a patch of wood (the same low frequency noise
        /// that shades the canopy) picks ONE species, the scale varies by a hair across that
        /// patch, one crown keeps a tile of air from the next, and each tree sorts by its own row
        /// so the crowns nearer the camera draw over the ones behind.</summary>
        void BuildRimTrees()
        {
            // medium crowns for the perimeter: a 43 pixel crown on a 1 unit border overhangs the
            // lane it lines, which is how a hero walking the edge of the wood vanishes behind one
            var rimIds = new[] { "01", "03", "06", "07", "09", "10", "13", "15", "16", "20", "21", "25", "26" };
            var innerIds = new[] { "02", "11", "12", "17", "18", "22", "23" };
            var bushIds = new[] { "05", "08", "14", "19", "24" };
            for (int y = 1; y < GameMap.H - 1; y++)
            {
                for (int x = 1; x < GameMap.W - 1; x++)
                {
                    if (Map.At(new Vector2Int(x, y)) != Ground.Tree) continue;
                    bool rim = !IsTree(x - 1, y) || !IsTree(x + 1, y)
                            || !IsTree(x, y - 1) || !IsTree(x, y + 1);
                    // one more cell in, so a thin stand of trees (the band that walls off the
                    // second field is five rows deep) is crowns all the way through instead of
                    // a ring of trees around a flat green middle
                    bool inner = !rim && Noise(x, y, 3) > 0.55f
                            && (!IsTree(x - 2, y) || !IsTree(x + 2, y)
                                || !IsTree(x, y - 2) || !IsTree(x, y + 2));
                    if (!rim && !inner) continue;
                    if (rim && Noise(x, y, 3) < 0.34f) continue;   // clumps, not a hedge
                    // Two clear cells around every crown, asked of the map's tree registry: the
                    // wild trees the chapter dressed its fields with claimed their own cells
                    // first, so a rim crown can no longer be planted inside one of them.
                    if (!Map.TreeSlotFree(x, y, 2)) continue;

                    float patch = Noise(x, y, 9);                  // species and size of this stand
                    var pool = inner ? innerIds : rimIds;
                    string id = pool[Mathf.Clamp((int)(patch * pool.Length), 0, pool.Length - 1)];
                    var sr = SpriteRendererUtil.Make(_root, "tree_" + id + "#r" + x + "_" + y,
                        TexArt.Whole("Pack/Props/Trees/tree_" + id), WorldOrder(y));
                    if (sr.sprite == null) { Fx.Kill(sr.gameObject); continue; }
                    float sc = (inner ? 1.0f : 0.98f) + patch * 0.1f;
                    sr.transform.localScale = Vector3.one * sc;
                    sr.transform.localPosition = new Vector3(x + 0.5f,
                        y + sr.sprite.bounds.extents.y * sc, 0f);
                    Map.ClaimTree(x, y, 2);
                }
            }

            // undergrowth: the small 16x32 bushes, tucked inside the wood where the canopy tile is
            // blank. They ask the same tree registry for a cell, so they read as the floor of the
            // wood instead of another thing stacked on the edge of it.
            int n = 0;
            for (int y = 2; y < GameMap.H - 2; y++)
            {
                for (int x = 2; x < GameMap.W - 2; x++)
                {
                    if (Map.At(new Vector2Int(x, y)) != Ground.Tree) continue;
                    if (!Map.TreeSlotFree(x, y, 1)) continue;
                    float b = Noise(x, y, 5);
                    if (b < 0.74f) continue;
                    string id = bushIds[(int)(b * 97f) % bushIds.Length];
                    var sr = SpriteRendererUtil.Make(_root, "tree_" + id + "#b" + x + "_" + y, TexArt.Whole("Pack/Props/Trees/tree_" + id), WorldOrder(y));
                    if (sr.sprite == null) { Fx.Kill(sr.gameObject); continue; }
                    float sc = 0.9f + b * 0.2f;
                    sr.transform.localScale = Vector3.one * sc;
                    sr.transform.localPosition = new Vector3(x + 0.5f,
                        y + sr.sprite.bounds.extents.y * sc, 0f);
                    Map.ClaimTree(x, y, 1);
                    n++;
                }
            }
        }

        /// <summary>One depth band for the whole world: props, chests, trees and the cast all
        /// sort by the row they stand on, so whatever is nearer the camera draws over whatever is
        /// behind it. Every prop used to share sorting order 30 and every actor a fixed offset
        /// (the hero 60, monsters 42+i), so two trees on neighbouring cells came out in whatever
        /// order Unity happened to emit them - a pile instead of a stand - and a house could not
        /// cover the hero walking behind its roof. `600 + 3*y` keeps the band clear of the title
        /// screen (90..100), the canopy mesh (40), the contact shadows (45) and the dimmer (2000).</summary>
        static int WorldOrder(float y) => 600 + Mathf.Clamp(Mathf.RoundToInt(y * 3f), 0, 480);

        /// <summary>The file name at the end of an art path ("Houses/house_02" -> "house_02"),
        /// used as the object name so a dump of the hierarchy says what a sprite is.</summary>
        static string Leaf(string path)
        {
            int i = path.LastIndexOf('/');
            return i < 0 ? path : path.Substring(i + 1);
        }

        /// <summary>Sorts one actor and its contact shadow by the row its feet are on.</summary>
        static void SortActor(Actor a)
        {
            if (a == null || a.Root == null || a.Sr == null) return;
            int order = WorldOrder(a.Root.localPosition.y);
            a.Sr.sortingOrder = order;
            if (a.Shadow != null) a.Shadow.sortingOrder = order - 2;
        }

        /// <summary>The way into every house, built as behaviour instead of as a second door. The
        /// pack's house art paints a door into its own facade, and the pack's separate door sprite
        /// was planted a whole tile lower -- so the street showed a door standing on its own in the
        /// grass in front of the wall. What a door needs outdoors is the two things that say "step
        /// here": a worn mat on the trigger cell and warm light spilling out of the doorway above
        /// it. The light rides above the night dimmer, which is what actually reveals the painted
        /// door in the first place.</summary>
        void BuildDoorways()
        {
            for (int i = 0; i < Map.Doors.Count; i++)
            {
                var c = Map.Doors[i];
                var mat = SpriteRendererUtil.Make(_root, "doorMat" + i, TexArt.DoorMat(), WorldOrder(c.y) - 1);
                mat.transform.localPosition = new Vector3(c.x + 0.5f, c.y + 0.42f, 0f);

                var glow = SpriteRendererUtil.Make(_root, "doorGlow" + i, TexArt.Glow(), 2010);
                glow.transform.localPosition = new Vector3(c.x + 0.5f, c.y + 1.05f, 0f);
                glow.transform.localScale = new Vector3(3.6f, 2.8f, 1f);
                glow.color = new Color(1f, 0.87f, 0.58f, 0.34f);
                AddGlow(glow, 0.20f);
            }
        }

        /// <summary>Ripples: one animated water sprite per pond tile, folding the flat fill
        /// into moving water. Sits between the ground and the decor layer.</summary>
        void BuildWaterAnim()
        {
            _water.Clear();
            var a = TexArt.Grid("Pack/Anim/Water/water_01_16x16", 16, 16);
            var b = TexArt.Grid("Pack/Anim/Water/water_02_16x16", 16, 16);
            if (a.Length == 0 && b.Length == 0) return;

            int n = 0;
            for (int y = 0; y < GameMap.H; y++)
            {
                for (int x = 0; x < GameMap.W; x++)
                {
                    if (Map.At(new Vector2Int(x, y)) != Ground.Water) continue;
                    var sr = SpriteRendererUtil.Make(_root, "ripple" + n, null, 5);
                    sr.transform.localPosition = new Vector3(x + 0.5f, y + 0.5f, 0f);
                    sr.color = new Color(0.72f, 0.8f, 1f, 0.85f);
                    var anim = sr.gameObject.AddComponent<Anim>();
                    anim.Setup(sr, false, 0f);
                    var set = (n % 2 == 0) ? a : b;
                    if (set.Length == 0) set = a.Length > 0 ? a : b;
                    anim.Play(set, 4.5f + (n % 3) * 0.7f, true);
                    _water.Add(sr);
                    n++;
                }
            }
        }

        /// <summary>Village life: cats, birds and a mouse that hop around their home tile.</summary>
        void BuildCritters()
        {
            for (int i = 0; i < Map.Critters.Count; i++)
            {
                var def = Map.Critters[i];
                var a = MakeActor(def.Pos, WorldOrder(def.Pos.y), isNpc: false);
                a.HomeCell = new Vector2(def.Pos.x - 0.5f, def.Pos.y - 0.5f);
                var clip = AnimalClip(def.Art, Dir.Down);
                if (clip == null) { Fx.Kill(a.Root.gameObject); continue; }
                a.Art = def.Art;
                a.Anim.Play(clip, 5f, true);
                a.Speed = 0.75f;
                a.Sr.transform.localScale = Vector3.one * 0.85f;
                a.Shadow.transform.localScale = new Vector3(0.6f, 0.7f, 1f);
                _critters.Add(a);
            }
        }

        void BuildCristal()
        {
            var p = Map.CristalPos;
            _cristalGlow = SpriteRendererUtil.Make(_root, "cristalGlow", TexArt.Glow(), 2011);
            _cristalGlow.transform.localPosition = new Vector3(p.x, p.y + 0.3f, 0f);
            _cristalGlow.transform.localScale = Vector3.one * 9f;
            _cristalGlow.color = new Color(0.7f, 0.85f, 1f, 0.45f);

            _cristalSr = SpriteRendererUtil.Make(_root, "cristal", null, WorldOrder(p.y));
            _cristalSr.transform.localPosition = new Vector3(p.x, p.y, 0f);
            _cristalAnim = _cristalSr.gameObject.AddComponent<Anim>();
            _cristalAnim.Setup(_cristalSr, true, 0f);
            var frames = TexArt.Grid("Art/Obj/cristal_0_16x32", 16, 32);
            _cristalAnim.Play(frames, 6f, true);
        }

        class EventSpot { public string Id; public SpriteRenderer Sr, Sr2; }
        readonly List<EventSpot> _eventSpots = new List<EventSpot>();
        float _evTick;

        /// <summary>Each unfired world event wears a quiet ember: a "something rests here"
        /// hint so the road's one-shot moments are found, not stumbled over. Cold light
        /// for the sad ones, warm for the rest; it lifts for good once the event fires.
        /// </summary>
        void BuildEventSpots()
        {
            foreach (var ev in Quests.Events)
            {
                if (ev.Chapter > MapChapter || Quests.FiredAlready(ev.Id)) continue;
                // halo + a bright hovering spark: the halo alone peaked at ~0.3 alpha and
                // vanished under canopy - the cue is meant to be seen from across the map
                var halo = ev.Tragic ? new Color(0.62f, 0.76f, 1f, 0.42f)
                    : new Color(1f, 0.84f, 0.42f, 0.48f);
                var spark = ev.Tragic ? new Color(0.82f, 0.88f, 1f, 0.7f)
                    : new Color(1f, 0.95f, 0.62f, 0.75f);
                var g = SpriteRendererUtil.Make(_root, "evGlow" + ev.Id, TexArt.Glow(), 2006);
                g.transform.localPosition = new Vector3(ev.Pos.x, ev.Pos.y + 0.15f, 0f);
                g.transform.localScale = Vector3.one * 2.8f;
                g.color = halo;
                var c = SpriteRendererUtil.Make(_root, "evCore" + ev.Id, TexArt.Glow(), 2007);
                c.transform.localPosition = new Vector3(ev.Pos.x, ev.Pos.y + 0.45f, 0f);
                c.transform.localScale = Vector3.one * 0.85f;
                c.color = spark;
                _eventSpots.Add(new EventSpot { Id = ev.Id, Sr = g, Sr2 = c });
                AddGlow(g, 0.5f);
                AddGlow(c, 0.9f);
            }
        }

        /// <summary>Fireflies: tiny drifting lights over the fields at night.
        /// Each night has its own kind - green flies on the fields, cold wisps deep
        /// in the forest, gold motes on the last march - and deeper nights swarm more.</summary>
        void BuildFireflies()
        {
            var rng = new System.Random(4242);
            var tint = MapChapter == 1 ? new Color(0.85f, 1f, 0.65f, 0f)
                     : MapChapter == 2 ? new Color(0.6f, 0.95f, 1f, 0f)
                     : new Color(1f, 0.95f, 0.7f, 0f);
            for (int i = 0; i < 26 + MapChapter * 6; i++)
            {
                var go = new GameObject("fly" + i);
                go.transform.SetParent(_root, false);
                float x = (float)rng.NextDouble() * GameMap.W;
                float y = 16f + (float)rng.NextDouble() * 58f;
                go.transform.localPosition = new Vector3(x, y, 0f);
                go.transform.localScale = Vector3.one * (0.8f + (float)rng.NextDouble() * 0.9f);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = TexArt.Glow();
                sr.sortingOrder = 2015;   // above the dimmer: a firefly has to be its own light
                sr.color = tint;
                _flies.Add(go.transform);
                _flySprites.Add(sr);
            }
        }

        void BuildChests()
        {
            var defs = new List<ChestDef>(Map.Chests.Count + 3);
            for (int i = 0; i < Map.Chests.Count; i++)
            {
                var c = Map.Chests[i];
                int variant = 1 + i % 7;
                var frames = TexArt.ChestFrames(ChestSheet(variant));
                var sr = SpriteRendererUtil.Make(_root, "chest" + i, frames.Length > 0 ? frames[0] : null, WorldOrder(c.y));
                var pos = Map.CellCenter(c);
                // the sprite is cropped to the art and pivoted on its base, so it stands on the
                // lower edge of its tile. It used to sit at pos.y - 0.35 with a 32 pixel cell
                // whose art only fills the bottom 20 rows, which left every chest sunk into the
                // grass with a slab of empty sprite above it.
                sr.transform.localPosition = new Vector3(pos.x, pos.y - 0.5f, 0f);
                var anim = sr.gameObject.AddComponent<Anim>();
                anim.Setup(sr, false, 0f);
                anim.Play(new[] { frames.Length > 0 ? frames[0] : null }, 1f, true);

                // a soft halo so a chest is never lost in the grass, and a contact shadow so it
                // reads as standing in it
                var glow = SpriteRendererUtil.Make(_root, "cglow" + i, TexArt.Glow(), 2005);
                glow.transform.localPosition = new Vector3(pos.x, pos.y - 0.15f, 0f);
                glow.transform.localScale = Vector3.one * 3.4f;
                glow.color = new Color(1f, 0.92f, 0.6f, 0.34f);
                AddGlow(glow, 0.24f);
                var sh = SpriteRendererUtil.Make(_root, "csh" + i, TexArt.Shadow(), 45);
                sh.transform.localPosition = new Vector3(pos.x, pos.y - 0.46f, 0f);
                sh.transform.localScale = new Vector3(0.8f, 0.66f, 1f);
                bool wasOpened = Map.Interior
                    ? Game.State.HasChestKey("h" + Map.HouseIndex + ":" + c.x + "," + c.y)
                    : Game.State.HasChest(MapChapter, c);
                defs.Add(new ChestDef
                {
                    Pos = pos, Cell = c, Sr = sr, Glow = glow, Anim = anim, Variant = variant,
                    Opened = wasOpened, LootKey = "loot.moonshard",
                });
                // a chest left open in an earlier visit still reads open - no halo, dark wood,
                // lid up on frame 3 - so the field cannot be looted twice by walking out and in
                if (wasOpened)
                {
                    if (frames.Length >= 4) sr.sprite = frames[3];
                    glow.gameObject.SetActive(false);
                    sr.color = new Color(0.76f, 0.78f, 0.86f);
                }
            }

            // the wild keeps chests of its own: deep-field boxes that are teeth, not
            // treasure. Their shimmer runs a shade colder than honest gold - close enough
            // to tempt, different enough to warn a careful eye
            if (!Map.Interior)
            {
                var mrng = new System.Random(909 + MapChapter * 31);
                int want = MapChapter >= 2 ? 2 : 1;
                string mimicName = MapChapter >= 3 ? "mon.zombi" : MapChapter == 2 ? "mon.thick" : "mon.imp";
                MonsterSpec? mimicSpec = null;
                foreach (var s in BattleData.Bestiary) if (s.Name == mimicName) mimicSpec = s;
                for (int tries = 0; tries < 80 && want > 0; tries++)
                {
                    int band = want;   // the first mimic lurks the near field, the second deeper
                    int yMin = band == 1 ? 30 : 58, yMax = band == 1 ? 55 : GameMap.H - 6;
                    var mc = new Vector2Int(mrng.Next(5, GameMap.W - 5), mrng.Next(yMin, yMax));
                    if (!Map.Walkable(mc)) continue;
                    if (Vector2.Distance(mc, new Vector2(30, 6)) < 14f) continue;
                    // a trap the thief already sprung leaves its wreck on the field -
                    // the splintered lid is the only gravestone a fake box gets
                    bool sprung = Game.State.HasChestKey("mimic:" + MapChapter + ":" + mc.x + "," + mc.y);
                    var mpos = Map.CellCenter(mc);
                    if (!sprung)
                    {
                        bool tooClose = false;
                        foreach (var d in defs)
                            if (Vector2.Distance(d.Pos, mpos) < 4f) { tooClose = true; break; }
                        if (tooClose) continue;
                    }
                    int mvar = 2 + (defs.Count % 6);
                    var mframes = TexArt.ChestFrames(ChestSheet(mvar));
                    var msr = SpriteRendererUtil.Make(_root, "chestM" + defs.Count,
                        mframes.Length > 0 ? (sprung ? mframes[mframes.Length - 1] : mframes[0]) : null,
                        WorldOrder(mc.y));
                    msr.transform.localPosition = new Vector3(mpos.x, mpos.y - 0.5f, 0f);
                    var manim = msr.gameObject.AddComponent<Anim>();
                    manim.Setup(msr, false, 0f);
                    // a wreck holds its lid-up frame; an unspoiled trap sits closed -
                    // through the anim, or the loop writes frame 0 back over the wreck
                    manim.Play(new[] { mframes.Length > 0 ? (sprung ? mframes[mframes.Length - 1] : mframes[0]) : null }, 1f, true);
                    if (sprung)
                    {
                        // the sprung wreck: lid thrown wide, wood gone grey, tilted like
                        // it fell mid-lunge - no halo, no touch, no second bite
                        msr.color = new Color(0.5f, 0.46f, 0.44f, 0.9f);
                        msr.transform.localRotation = Quaternion.Euler(0f, 0f, 13f);
                    }
                    var mglow = SpriteRendererUtil.Make(_root, "cglowM" + defs.Count, TexArt.Glow(), 2005);
                    mglow.transform.localPosition = new Vector3(mpos.x, mpos.y - 0.15f, 0f);
                    mglow.transform.localScale = Vector3.one * 3.4f;
                    // a shimmer a shade colder than honest gold: the only warning it gives
                    mglow.color = new Color(0.7f, 0.85f, 1f, 0.3f);
                    mglow.enabled = !sprung;
                    if (!sprung) AddGlow(mglow, 0.22f);
                    var msh = SpriteRendererUtil.Make(_root, "cshM" + defs.Count, TexArt.Shadow(), 45);
                    msh.transform.localPosition = new Vector3(mpos.x, mpos.y - 0.46f, 0f);
                    msh.transform.localScale = new Vector3(0.8f, 0.66f, 1f);
                    defs.Add(new ChestDef
                    {
                        Pos = mpos, Cell = mc, Sr = msr, Glow = mglow, Anim = manim,
                        Variant = mvar, Opened = sprung, LootKey = null,
                        IsMimic = true, Mimic = mimicSpec,
                    });
                    if (!sprung) want--;
                }
            }
            _chests = defs.ToArray();
        }

        static string ChestSheet(int variant) =>
            "Pack/Anim/Chest/chest_" + variant.ToString("00") + "_16x32";

        void AddGlow(SpriteRenderer sr, float amp)
        {
            _glows.Add(sr);
            _glowAmp.Add(amp);
        }

        /// <summary>Furniture drawn in code. The pack has no bed, table or rug, and a bedroom
        /// built only from barrels reads as a warehouse.</summary>
        static Sprite Furniture(string deco)
        {
            switch (deco)
            {
                case "Bed": return TexArt.Bed();
                case "Table": return TexArt.Table();
                case "Rug": return TexArt.Rug();
                case "Shelf": return TexArt.Shelf();
                case "Window": return TexArt.Window();
                default: return null;
            }
        }

        /// <summary>The guard is gone: clear its sprite so the road north reads as open.</summary>
        public void RemoveBoss()
        {
            if (_bossProp != null) _bossProp.enabled = false;
        }

        /// <summary>The line a chest just gave. Read by the game for the banner, so a real item
        /// name can appear in it instead of a generic "you found something".</summary>
        public string LastLootText = "";

        /// <summary>Is the hero at a front door? Returns the house it belongs to, so the room the
        /// game builds is the room behind that particular door.</summary>
        public bool NearDoor(Vector2 pos, out int houseIndex)
        {
            houseIndex = -1;
            if (Map == null || Map.Interior) return false;
            for (int i = 0; i < Map.Doors.Count; i++)
            {
                var c = Map.Doors[i];
                if (Vector2.Distance(pos, new Vector2(c.x + 0.5f, c.y + 0.5f)) > 1.5f) continue;
                houseIndex = i;
                return true;
            }
            return false;
        }

        /// <summary>Index of the nearest chest still shut, or -1. ChestDef is a struct, so
        /// callers trade in indexes - a returned copy could never flip Opened for real.</summary>
        public int NearestChest(Vector2 pos, float maxDist = 1.2f)
        {
            int best = -1;
            float bestD = maxDist;
            for (int i = 0; i < _chests.Length; i++)
            {
                if (_chests[i].Opened) continue;
                float d = Vector2.Distance(_chests[i].Pos, pos);
                if (d <= bestD) { best = i; bestD = d; }
            }
            return best;
        }

        public Vector2 ChestPos(int i) => _chests[i].Pos;

        /// <summary>Where the night's still-shut chests sit - the journal's world map drops
        /// a gold mote on each, so an unlooted cache reads off the card at a glance.</summary>
        public List<Vector2> ShutChestPos()
        {
            var list = new List<Vector2>();
            if (_chests == null) return list;
            for (int i = 0; i < _chests.Length; i++)
                if (!_chests[i].Opened && !_chests[i].IsMimic) list.Add(_chests[i].Pos);
            return list;
        }

        /// <summary>How many chests are still shut in this chapter (drives the quest line).</summary>
        public int ChestsLeft
        {
            get
            {
                int n = 0;
                if (_chests != null)
                    for (int i = 0; i < _chests.Length; i++)
                        if (!_chests[i].Opened && !_chests[i].IsMimic) n++;
                return n;
            }
        }

        /// <summary>The first three chests of a run always carry moonlight, so the shard
        /// count can always reach four (the fourth comes from the guard). The rest hold gold and a
        /// real item, rolled from the chapter's pool - a chest used to hand out a flag and a
        /// number that nothing read. Inside a house nothing ever counts as a shard: the room cache
        /// is pocket money, so the night's four shards stay where the story put them.</summary>
        /// <summary>True while the shut box at i is really a beast - it still answers
        /// taps, because a trap you can see coming is no trap at all.</summary>
        public bool ChestIsMimic(int i) => i >= 0 && i < _chests.Length && _chests[i].IsMimic && !_chests[i].Opened;

        /// <summary>Springs the trap: the box is spent for good (its quiet key joins the
        /// opened-chest list so a rebuild never re-seeds teeth where they already bit).
        /// Returns the beast that was folded inside.</summary>
        public MonsterSpec SpringMimic(int i)
        {
            var chest = _chests[i];
            chest.Opened = true;
            // the box doesn't vanish - it dies where it stood: lid thrown wide, wood
            // gone grey, tilted like it fell mid-lunge. The wreck outlasts the fight.
            if (chest.Sr != null)
            {
                var fr = TexArt.ChestFrames(ChestSheet(chest.Variant));
                // the reveal itself: the lid throws open in one step, like the real
                // lid-flip a spent chest plays - only this one keeps teeth behind it
                if (chest.Anim != null && fr.Length >= 4)
                    chest.Anim.Play(new[] { fr[0], fr[fr.Length - 1] }, 9f, false);
                else if (fr.Length > 0) chest.Sr.sprite = fr[fr.Length - 1];
                chest.Sr.color = new Color(0.5f, 0.46f, 0.44f, 0.9f);
                chest.Sr.transform.localRotation = Quaternion.Euler(0f, 0f, 13f);
            }
            if (chest.Glow != null) chest.Glow.enabled = false;
            Game.State.MarkChestKey("mimic:" + MapChapter + ":" + chest.Cell.x + "," + chest.Cell.y);
            _chests[i] = chest;
            return chest.Mimic ?? BattleData.Bestiary[0];
        }

        public void OpenChest(int i)
        {
            if (_chests[i].IsMimic) return;   // teeth are sprung, not opened
            _chests[i].Opened = true;
            var chest = _chests[i];
            // every cache remembers it was spent - field chests by night and cell,
            // house chests by the room they stand in - so no larder refills on a reload
            if (Map.Interior)
                Game.State.MarkChestKey("h" + Map.HouseIndex + ":" + chest.Cell.x + "," + chest.Cell.y);
            else Game.State.MarkChest(MapChapter, chest.Cell);
            bool shard = !Map.Interior && Game.State.ChestsOpened < 3;
            if (!Map.Interior) Game.State.ChestsOpened++;
            // moonlight out of a shard cache, gold out of the rest - the burst used to
            // paint every chest the same warm spray whatever it held
            BurstLoot(chest.Pos, shard);
            if (shard)
            {
                Game.State.MoonShards++;
                LastLootText = Strings.Get("loot.moonshard");
            }
            else
            {
                var rng = new System.Random(Game.State.ChestsOpened * 7919 + MapChapter * 31 + (int)(chest.Pos.x * 13f));
                if (rng.NextDouble() < 0.2f)
                {
                    // one cache in five is only a purse: more gold, no ware - the
                    // "old coins" line the strings table always carried
                    Game.State.Gold += Map.Interior ? 26 : 18;
                    LastLootText = Strings.Get("loot.oldcoin");
                }
                else
                {
                    int gold = Map.Interior ? 20 : 12;
                    Game.State.Gold += gold;
                    string item = Items.RollLoot(Game.State.Chapter, rng);
                    // a named errand can hide its thing in the next chest you open: while an
                    // item quest runs and the bag still lacks the piece, the cache coughs it
                    // up half the time - found, not handed, but never impossible
                    if (rng.NextDouble() < 0.5)
                        item = Quests.WantedQuestItem() ?? item;
                    Game.State.AddBag(item);
                    LastLootText = Strings.Get("loot.found", Strings.Get(item), gold);
                }
            }
            if (chest.Sr != null)
            {
                // The chest that was just opened opens -- its own sheet, its own design. It used to
                // be swapped for whatever sheet the run's chest counter landed on, so a chest
                // changed design the moment it was touched. Frame 3 of every sheet is the same
                // chest with the lid up, so the two frames make the flip in one step.
                var frames = TexArt.ChestFrames(ChestSheet(chest.Variant));
                if (chest.Anim != null && frames.Length >= 4)
                    chest.Anim.Play(new[] { frames[0], frames[3] }, 7f, false);
                else if (frames.Length > 0)
                    chest.Sr.sprite = frames[frames.Length - 1];
                // spent: the halo goes out and the wood reads darker, so a cleared corner of the
                // map looks cleared instead of asking the player to remember which chests they took
                if (chest.Glow != null) chest.Glow.gameObject.SetActive(false);
                chest.Sr.color = new Color(0.76f, 0.78f, 0.86f);
            }
        }

        // ---------------------------------------------------------------- actors

        /// <summary>A character's floating name with a fitted chip behind it. A label hangs
        /// DOWN from its anchor, and the pack's chara cell is 16x20 (the actor stands 1.25
        /// units on its own origin), so the anchor clears the head with a 2-pixel breath.
        /// The chip is measured from the text it carries, so a short name gets a short tag.</summary>
        void MakeNamePlate(Actor a, string label, Color32 chipColor)
        {
            a.Name = PixelLabelUtil.Make(_root, a.Root.name + "Name", 1, new Color(0.95f, 0.9f, 0.75f), TextAlign.Center, 2100);
            var at = (Vector2)a.Root.localPosition;
            a.Name.transform.localPosition = new Vector3(at.x, at.y + NameAnchorY, 0f);
            a.Name.Set(label);
            float textW = a.Name.MeasureWidth(label), textH = a.Name.MeasureHeight(label);
            var chip = SpriteRendererUtil.Make(a.Root, a.Root.name + "Chip", TexArt.Solid(), 2099);
            chip.transform.localPosition = new Vector3(0f, NameAnchorY - textH * 0.5f - 0.02f, 0f);
            chip.transform.localScale = new Vector3(Mathf.Max(0.9f, textW + 0.34f), textH + 0.24f, 1f);
            chip.color = chipColor;
            a.NameChip = chip;
        }

        void SpawnNpcs(NpcDef[] defs)
        {
            for (int i = 0; i < defs.Length; i++)
            {
                // The roster outgrew the six hand-placed spots, and a newcomer left standing in
                // a wall reads as a bug, so every villager snaps to the nearest open tile.
                var spot = FreeSpot(defs[i].Pos);
                if (!Map.Walkable(Map.CellOf(spot)))
                    Debug.LogWarning("[NPC] " + defs[i].NameKey + " stands on a blocked cell at " + spot);
                var a = MakeActor(spot, WorldOrder(spot.y), isNpc: true);
                a.Npc = defs[i];
                a.IsNpc = true;
                // walking-anim folk (the companions) have no chara sheet to strip - their
                // whole figure is one hero-style walk cycle, same as their battle rig
                if (!string.IsNullOrEmpty(defs[i].Walk))
                    a.Anim.Play(Bank.Frames(defs[i].Walk), 3f, true);
                else
                    a.Anim.Play(CharaClip(Folks.Sheet(defs[i]), Dir.Down), 3f, true);
                string label = Strings.Get(defs[i].NameKey);
                MakeNamePlate(a, label, new Color32(10, 8, 20, 205));
                Npcs.Add(a);
            }
        }

        /// <summary>Three frames of one facing out of the pack's character sheets. Every walk
        /// sheet here has the same shape: three frames per row, four rows, the front row first,
        /// which is also the order of the Dir enum. Only the front strip was ever drawn, so the
        /// villagers, the wild monsters and the critters all slid around the map looking at the
        /// camera while they walked away from it.</summary>
        Sprite[] Strip(string key, Sprite[] sheet, Dir dir, int cols = 3)
        {
            if (sheet == null || sheet.Length < cols * 4) return null;
            string k = key + "#" + (int)dir;
            if (_clipCache.TryGetValue(k, out var cached)) return cached;
            int row = Mathf.Clamp((int)dir, 0, 3) * cols;
            var clip = new Sprite[cols];
            for (int i = 0; i < cols; i++) clip[i] = sheet[row + i];
            _clipCache[k] = clip;
            return clip;
        }

        public Sprite[] CharaClip(string sheet, Dir dir)
            => Strip(sheet, TexArt.Grid(sheet, 16, 20), dir);

        public Sprite[] MonsterClip(string sheet, Dir dir)
            => Strip(sheet, TexArt.Grid(sheet, 48, 48), dir);

        public Sprite[] AnimalClip(string art, Dir dir)
            => Strip("animal:" + art, TexArt.Grid("Pack/Animals/" + art, 16, 20), dir);

        /// <summary>Points an actor's sprite the way it is walking. The strip is swapped only on
        /// a real turn, so a cycle keeps its phase instead of snapping back to frame zero every
        /// frame, and a frozen actor (an idle pause) stays frozen.</summary>
        void Face(Actor a, Dir dir, Sprite[] frames)
        {
            if (a == null || a.Anim == null || frames == null || frames.Length == 0) return;
            if (a.Facing == dir && ReferenceEquals(a.Anim.Frames, frames)) return;
            a.Facing = dir;
            float fps = a.Anim.Fps;
            a.Anim.Play(frames, fps > 0f ? fps : 1f, true);
            a.Anim.Fps = fps;
        }

        /// <summary>The nearest open tile centre to a wanted spot, searched outward in rings, so
        /// a villager can never stand inside a wall, in the water or on another villager.</summary>
        Vector2 FreeSpot(Vector2 want)
        {
            if (Map == null) return want;
            var origin = Map.CellOf(want);
            for (int r = 0; r <= 4; r++)
                for (int dy = -r; dy <= r; dy++)
                    for (int dx = -r; dx <= r; dx++)
                    {
                        if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) != r) continue;
                        var cell = new Vector2Int(origin.x + dx, origin.y + dy);
                        if (!Map.Walkable(cell)) continue;
                        var p = Map.CellCenter(cell);
                        bool taken = false;
                        for (int i = 0; i < Npcs.Count && !taken; i++)
                            taken = Vector2.Distance((Vector2)Npcs[i].Root.localPosition, p) < 0.9f;
                        if (!taken) return p;
                    }
            return want;
        }

        public Sprite[] HeroClip(Dir dir)
        {
            string d = dir switch { Dir.Up => "UP", Dir.Down => "DOWN", Dir.Left => "LEFT", _ => "RIGHT" };
            return Bank.Frames("Art/Hero/hero/color_1/walk/hero_walk_" + d);
        }

        Actor MakeActor(Vector2 pos, int sorting, bool isNpc)
        {
            var a = new Actor();
            var root = new GameObject(isNpc ? "npc" : "mon").transform;
            root.SetParent(_root, false);
            root.localPosition = new Vector3(pos.x, pos.y, 0f);
            a.Root = root;

            a.Shadow = SpriteRendererUtil.Make(root, "sh", TexArt.Shadow(), sorting - 2);
            a.Shadow.transform.localPosition = new Vector3(0f, 0.1f, 0f);
            a.Shadow.transform.localScale = new Vector3(0.9f, 1f, 1f);

            var body = new GameObject("body").transform;
            body.SetParent(root, false);
            a.Body = body;

            var go = new GameObject("spr");
            go.transform.SetParent(body, false);
            a.Sr = go.AddComponent<SpriteRenderer>();
            a.Sr.sortingOrder = sorting;
            a.Anim = go.AddComponent<Anim>();
            a.Anim.Setup(a.Sr, true, 0f);
            return a;
        }

        void SpawnMonsters()
        {
            int chapter = Game.State.Chapter;
            var rng = new System.Random(700 + chapter * 13);
            int forest = chapter == 3 ? 8 : chapter == 2 ? 5 : 3;
            int fields = chapter == 1 ? 7 : 5;

            for (int i = 0; i < fields; i++) SpawnOne(rng, 15, 57, chapter);
            for (int i = 0; i < forest; i++) SpawnOne(rng, 61, 76, 3);
        }

        void SpawnOne(System.Random rng, int yMin, int yMax, int chapter)
        {
            var pool = new List<MonsterSpec>();
            foreach (var s in BattleData.Bestiary)
                // gatekeepers are not field spawns: a wandering thane that dies as a wild
                // thing still ran the boss-defeat path and opened the ending early
                if (!s.Boss && s.Chapter <= chapter && (yMax > 59 ? s.Chapter >= 2 : true))
                    pool.Add(s);
            if (pool.Count == 0) return;
            var spec = pool[rng.Next(pool.Count)];

            for (int tries = 0; tries < 40; tries++)
            {
                int x = rng.Next(5, GameMap.W - 5);
                int y = rng.Next(yMin, yMax);
                var cell = new Vector2Int(x, y);
                if (!Map.Walkable(cell)) continue;
                if (Vector2Int.Distance(cell, new Vector2Int(30, 6)) < 10f) continue;   // village safe

                var a = MakeActor(Map.CellCenter(cell), WorldOrder(cell.y), isNpc: false);
                if (rng.Next(100) < 10) spec.Rare = true;   // moonlit: silver skin, worth hunting
                a.Spec = spec;
                // a fifth of the first field dozes off - blind to everything but still
                // within earshot, so a stomping thief wakes them into the chase anyway.
                // Deeper nights stay more awake: the wild learned the thief's gait too.
                int doze = MapChapter == 1 ? 20 : MapChapter == 2 ? 13 : 7;
                // on the cruel telling the wild naps with one eye open: half the
                // dozers a gentler night would have granted simply never settle
                if (Prefs.Hard) doze /= 2;
                a.Asleep = rng.Next(100) < doze;
                a.Sleeps = a.Asleep;
                a.Name = null;
                a.Speed = spec.Speed * 0.55f;
                a.HomeCell = new Vector2(x, y);
                a.Anim.Play(MonsterClip(spec.MapSheet, Dir.Down), 4f, true);
                // size telegraphs the risk: heavier tiers stand taller, moonlit
                // silver loom largest of all
                a.Sr.transform.localScale = Vector3.one * (0.68f + spec.Tier * 0.04f + (spec.Rare ? 0.1f : 0f));
                if (spec.Rare) a.Sr.color = new Color(0.72f, 0.84f, 1f);
                Monsters.Add(a);
                return;
            }
        }

        void BuildHud(float halfH)
        {
            HudRoot = new GameObject("hudRoot").transform;
            HudRoot.SetParent(_root, false);

            // A dark strip across the top. The zone and quest lines used to be printed straight
            // onto grass, roofs and treetops, which made them read as smudged rather than set.
            var bar = SpriteRendererUtil.Make(HudRoot, "hudBar", TexArt.Solid(), 4998);
            bar.transform.localPosition = new Vector3(0f, halfH - 1.9f, 0f);
            bar.transform.localScale = new Vector3(18f * 16f, 3.8f * 16f, 1f);
            bar.color = new Color(6f / 255f, 5f / 255f, 16f / 255f, 0.62f);

            var edge = SpriteRendererUtil.Make(HudRoot, "hudEdge", TexArt.Solid(), 4999);
            edge.transform.localPosition = new Vector3(0f, halfH - 3.8f, 0f);
            edge.transform.localScale = new Vector3(18f * 16f, 16f / 3f, 1f);
            edge.color = new Color(0.55f, 0.58f, 1f, 0.30f);

            // the "now entering" banner drops just below the bar with its own chip, so it can
            // never land on the quest line no matter how long either string is
            ZoneChip = SpriteRendererUtil.Make(HudRoot, "zoneChip", TexArt.Solid(), 4999);
            ZoneChip.transform.localPosition = new Vector3(0f, halfH - 4.95f, 0f);
            ZoneChip.transform.localScale = new Vector3(5f, 1.5f, 1f);
            ZoneChip.color = new Color32(10, 8, 20, 210);
            ZoneChip.enabled = false;

            ZoneBanner = PixelLabelUtil.Make(HudRoot, "zoneBanner", 2, new Color(1f, 0.95f, 0.8f), TextAlign.Center, 5001);
            ZoneBanner.transform.localPosition = new Vector3(0f, halfH - 4.45f, 0f);

            // the place name under the night number, at half the size: a zone title in one size of
            // voice, not a shout. Both lines are measured separately so the chip fits them,
            // instead of one 286-pixel line running the whole width of the screen.
            ZoneBannerSub = PixelLabelUtil.Make(HudRoot, "zoneBannerSub", 1, new Color(0.82f, 0.85f, 1f), TextAlign.Center, 5001);
            ZoneBannerSub.transform.localPosition = new Vector3(0f, halfH - 6.15f, 0f);

            _moonIcon = SpriteRendererUtil.Make(HudRoot, "moonHud", Game.State.Chapter >= 3 ? TexArt.MoonFull() : TexArt.MoonEmpty(), 5000);
            _moonIcon.transform.localPosition = new Vector3(G.Right - 0.9f, halfH - 0.9f, 0f);
            _moonIcon.transform.localScale = Vector3.one * 2f;
        }

        /// <summary>The corner moon brightens as shards come home: a faint ring while the
        /// night is young, full warm light once the set is complete. The hero's lantern
        /// drinks the same moonlight - every shard widens and warms the pool of light at
        /// his feet (the flicker loop owns the alpha, so this feeds its amp instead).</summary>
        SpriteRenderer _heroGlow;
        int _heroGlowIdx;
        // the thief muffles his own light when he creeps: the lantern pool thins to a
        // glow-worm's worth while a held breath or soft stick keeps him hidden
        float _sneakGlow = 1f;

        public void SetMoonFill(int shards, int needed)
        {
            if (_moonIcon == null) return;
            float t = needed <= 0 ? 1f : Mathf.Clamp01((float)shards / needed);
            _moonIcon.color = Color.Lerp(new Color(1f, 1f, 1f, 0.30f), new Color(1f, 0.95f, 0.75f, 1f), t);
            if (_heroGlow != null && _heroGlowIdx >= 0 && _heroGlowIdx < _glowAmp.Count)
            {
                _glowAmp[_heroGlowIdx] = Mathf.Lerp(0.22f, 0.40f, t);
                _heroGlow.transform.localScale = Vector3.Lerp(
                    new Vector3(4.4f, 3.2f, 1f), new Vector3(6.4f, 4.6f, 1f), t);
                var hc = _heroGlow.color;
                _heroGlow.color = new Color(1f, 0.85f + 0.10f * t, 0.5f + 0.18f * t, hc.a);
            }
        }

        /// <summary>Camera centre, pushed in by Game.SetCam: the guard uses it to keep plates
        /// out of the screen edges, where half a name used to hang off the frame.</summary>
        public Vector2 ViewCenter;
        public float HalfH = 16f;
        bool _textOn = true;

        /// <summary>Hides the world's own text: the zone and quest lines plus every actor
        /// name plate. A pause or settings card covers most of the screen, and a plate peeking
        /// out from behind an opaque panel reads as broken text, not as world detail.</summary>
        public void SetTextVisible(bool on)
        {
            _textOn = on;
            if (HudRoot != null) HudRoot.gameObject.SetActive(on);
            if (!on)
            {
                if (Hero != null && Hero.Name != null) Hero.Name.gameObject.SetActive(false);
                for (int i = 0; i < Npcs.Count; i++)
                {
                    if (Npcs[i].Name != null) Npcs[i].Name.gameObject.SetActive(false);
                    if (Npcs[i].NameChip != null) Npcs[i].NameChip.enabled = false;
                }
                // a bark mid-flight is world text too: a card opening under it would leave
                // the last word printed over the rows
                if (_bark != null) _bark.enabled = false;
                if (_barkChip != null) _barkChip.enabled = false;
            }
        }

        /// <summary>Hides the name plate of any world actor whose measured plate would sit on
        /// top of a plate earlier in the list (the hero is first, so it always wins), or whose
        /// plate would run off the edge of the screen. Plates are the only world text, and two
        /// villagers meeting on the road used to print through each other.</summary>
        // Name plate geometry, in world units above the actor's feet. The pack's chara cell is
        // 20 px tall, and the walking bob lifts the body by one more pixel, so a tag anchored at
        // 2.06 units clears the highest head (1.31) by about two pixels. At 1.87 the tag's own
        // bottom row grazed the villager's hair the moment they took a step.
        const float NameAnchorY = 2.06f;

        public void RefreshNamePlates()
        {
            _plates.Clear();
            _plateOk.Clear();
            _plateOwner.Clear();

            // Who gets a name at all: the two villagers nearest the hero. Every other plate was
            // noise - half of the village labelled at once, each tag floating over somebody else's
            // head as the crowd moved.
            int near0 = -1, near1 = -1;
            float d0 = float.MaxValue, d1 = float.MaxValue;
            for (int i = 0; i < Npcs.Count; i++)
            {
                if (Npcs[i].Name == null || Npcs[i].Root == null) continue;
                float d = Hero != null ? Vector2.Distance(Npcs[i].Root.localPosition, HeroPos) : 99f;
                if (d < d0) { d1 = d0; near1 = near0; d0 = d; near0 = i; }
                else if (d < d1) { d1 = d; near1 = i; }
            }
            for (int i = 0; i < Npcs.Count; i++)
            {
                if (Npcs[i].Name == null) continue;
                float d = Hero != null ? Vector2.Distance(Npcs[i].Root.localPosition, HeroPos) : 99f;
                _plates.Add(Npcs[i].Name);
                _plateOwner.Add(Npcs[i]);
                bool near = (i == near0 || i == near1) && d >= 1.6f && d <= 5.5f;
                _plateOk.Add(near);
            }
            // befriended beasts wear their warm tag whenever it can sit cleanly - it is how a
            // tamed slime reads different from the wild one drifting two tiles over
            for (int i = 0; i < _friends.Count; i++)
            {
                var fr = _friends[i];
                if (fr.Name == null || fr.Root == null) continue;
                float d = Hero != null ? Vector2.Distance(fr.Root.localPosition, HeroPos) : 99f;
                _plates.Add(fr.Name);
                _plateOwner.Add(fr);
                _plateOk.Add(d >= 0.9f && d <= 5.5f);
            }

            // quest bubbles: a villager with something to offer (or to hand in) wears a "!"
            // over the name plate, so the errands announce themselves from across the square.
            // Mira's bubble tracks the main line instead - she is the story's giver.
            for (int i = 0; i < Npcs.Count; i++)
            {
                var n = Npcs[i];
                if (n.Root == null) continue;
                bool ready = false;
                bool wants = n.Npc.NameKey == "npc.elder" && Quests.Step("mq.1") == 0;
                if (wants) ready = true;   // the story's giver gets the warm mark
                // a wanderer who has not said yes yet wears the cool mark: the recruiting
                // talk IS the errand, and the bubble is how the route out is learned
                if (n.Npc.JoinKey != null && !Game.State.Joined.Contains(n.Npc.JoinKey)) wants = true;
                if (!wants)
                {
                    // a giver mid-errand wears no mark - the bubble means "needs you now":
                    // a new offer, or a finished errand ready to hand in
                    var q = Quests.ForGiver(n.Npc.NameKey, out ready);
                    wants = q != null && (Quests.Step(q.Id) == 0 || ready);
                }
                if (!wants)
                {
                    // a soul an errand sends you to wears the cool mark too: the
                    // "find Wren" kind of errand only travels by word of mark
                    foreach (var tq in Quests.All)
                        if (tq.Kind == QuestKind.Talk && tq.Target == n.Npc.NameKey
                            && Quests.Step(tq.Id) == 1) { wants = true; break; }
                }
                if (n.Alert == null)
                {
                    var ago = new GameObject("questAlert");
                    ago.transform.SetParent(n.Root, false);
                    ago.transform.localPosition = new Vector3(0f, 2.95f, 0f);
                    ago.transform.localScale = Vector3.one * 0.8f;
                    n.Alert = ago.AddComponent<SpriteRenderer>();
                    n.Alert.sprite = TexArt.Alert();
                    n.Alert.sortingOrder = 2100;
                }
                n.Alert.enabled = wants && _textOn;
                // warm gold means "come collect": a finished errand or the story giver;
                // cool silver means "new work here"
                if (wants) n.Alert.color = ready
                    ? new Color(1f, 0.85f, 0.4f) : new Color(0.75f, 0.85f, 1f);
                if (wants)
                    n.Alert.transform.localPosition = new Vector3(0f, 2.95f + Mathf.Sin(_time * 5f + i) * 0.12f, 0f);
            }

            // Nothing is drawn on top of a character. A tag that covers the hero (or the monster
            // standing in front of the villager it names) reads as a mistake, so every plate is
            // tested against the sprite boxes of the whole cast before it is shown.
            _bodies.Clear();
            if (Hero != null && Hero.Sr != null && Hero.Sr.sprite != null) _bodies.Add(BodyBox(Hero));
            for (int i = 0; i < Monsters.Count; i++)
                if (Monsters[i].Sr != null && Monsters[i].Sr.sprite != null) _bodies.Add(BodyBox(Monsters[i]));
            for (int i = 0; i < Npcs.Count; i++)
                if (Npcs[i].Sr != null && Npcs[i].Sr.sprite != null) _bodies.Add(BodyBox(Npcs[i]));
            for (int i = 0; i < _friends.Count; i++)
                if (_friends[i].Sr != null && _friends[i].Sr.sprite != null) _bodies.Add(BodyBox(_friends[i]));

            _plateRects.Clear();
            for (int i = 0; i < _plates.Count; i++)
            {
                var l = _plates[i];
                float w = l.MeasureWidth(l.Text), h = l.MeasureHeight(l.Text);
                var p = l.transform.position;
                float x0 = l.Align == TextAlign.Left ? p.x
                         : l.Align == TextAlign.Center ? p.x - w * 0.5f : p.x - w;
                _plateRects.Add(new Rect(x0, p.y - h, w, h));
            }

            float vl = ViewCenter.x + G.Left, vr = ViewCenter.x + G.Right;
            float vb = ViewCenter.y - HalfH, vt = ViewCenter.y + HalfH;

            for (int i = 0; i < _plates.Count; i++)
            {
                bool clash = false;
                for (int j = 0; j < i && !clash; j++)
                    if (_plates[i].gameObject != _plates[j].gameObject
                        && _plates[i].gameObject.activeSelf && _plates[j].gameObject.activeSelf
                        && _plateRects[j].Overlaps(_plateRects[i])) clash = true;
                var r = _plateRects[i];
                // the HUD bar and the banner own the top of the screen, so a plate stops below them
                bool framed = r.xMin >= vl + 0.25f && r.xMax <= vr - 0.25f
                           && r.yMin >= vb + 0.2f && r.yMax <= vt - 4.8f;
                if (!_plateOk[i]) clash = true;
                if (UiBlock.width > 0f && UiBlock.Overlaps(r)) clash = true;   // the toast wins
                if (OnSomebody(r, _plateOwner[i])) clash = true;               // never on a character
                bool shown = _textOn && !clash && framed;
                _plates[i].gameObject.SetActive(shown);
                // the chip rides with the name: it is a sibling of the label, not a child, so a
                // hidden name used to leave its dark tag floating over the villager's head
                if (_plateOwner[i] != null && _plateOwner[i].NameChip != null)
                    _plateOwner[i].NameChip.enabled = shown;
            }
        }

        /// <summary>A character's own sprite box in world units, from its live sprite (the cast
        /// walks with a bob and the monsters are scaled, so the box is measured, not assumed).</summary>
        Rect BodyBox(Actor a)
        {
            var s = a.Sr.sprite.bounds.size;
            var p = a.Root.localPosition;
            var l = a.Sr.transform.localPosition;
            float w = s.x * Mathf.Abs(a.Sr.transform.lossyScale.x);
            float h = s.y * Mathf.Abs(a.Sr.transform.lossyScale.y);
            return new Rect(p.x + l.x - w * 0.5f, p.y + l.y - s.y * 0.5f, w, h);
        }

        /// <summary>Does this text box land on any character other than the one it belongs to?</summary>
        bool OnSomebody(Rect text, Actor owner)
        {
            for (int i = 0; i < _bodies.Count; i++)
            {
                var b = _bodies[i];
                if (owner != null)
                {
                    var o = BodyBox(owner);
                    if (Mathf.Abs(b.x - o.x) < 0.01f && Mathf.Abs(b.y - o.y) < 0.01f && Mathf.Abs(b.width - o.width) < 0.01f)
                        continue;   // the plate belongs above this head
                }
                if (b.Overlaps(text)) return true;
            }
            return false;
        }

        readonly List<Actor> _plateOwner = new List<Actor>();
        readonly List<Rect> _bodies = new List<Rect>();
        readonly List<bool> _plateOk = new List<bool>();

        readonly List<PixelLabel> _plates = new List<PixelLabel>();
        readonly List<Rect> _plateRects = new List<Rect>();

        /// <summary>Wipes the whole world (scene teardown before a new chapter mesh).</summary>
        public void Teardown()
        {
            Ready = false;
            if (_root != null) Fx.Kill(_root.gameObject);
            Monsters.Clear(); Npcs.Clear(); _props.Clear(); _glows.Clear(); _glowAmp.Clear(); _flies.Clear(); _flySprites.Clear(); _eventSpots.Clear();
            _water.Clear(); _critters.Clear(); _respawns.Clear();
            _friends.Clear(); _crumbs.Clear(); _dust.Clear();
            _ripples.Clear();   // the rings' sprites died with the root - the pool starts fresh
            _bossProp = null;
            Hero = null; Map = null; HudRoot = null; _vignette = null; _objArrow = null;
            _touchCue = null;
        }

        /// <summary>After a fight the world used to wipe every monster and roll a fresh herd on
        /// the spot - so a single kill brought the whole field back to life. Now a felled beast
        /// simply keeps its respawn ticket and comes back when the timer runs out, somewhere the
        /// hero is not looking at. Nothing here spawns instantly; the herd regrows off-screen.
        /// Kept name for the chapter-prep path, which only wants the wild to keep ticking.</summary>
        public void ResetForChapter()
        {
            // nothing instant: pending respawn tickets continue on their own clock
        }

        /// <summary>How many wild monsters the night currently fields (self-test + audit).</summary>
        public int MonsterCount => Monsters.Count;
        public int PendingRespawns => _respawns.Count;

        /// <summary>The villager actor matching a name key, e.g. for the objective compass.</summary>
        public Actor FindNpc(string nameKey)
        {
            foreach (var n in Npcs)
                if (n.Npc.NameKey == nameKey) return n;
            return null;
        }

        /// <summary>Turn a villager to look at a point - usually the hero they are talking to.</summary>
        public void FaceAt(Actor a, Vector2 toward)
        {
            if (a == null || a.Npc.NameKey == null || a.Root == null) return;
            var delta = toward - (Vector2)a.Root.localPosition;
            if (delta.sqrMagnitude < 0.01f) return;
            var d = DirVec.From(delta);
            Face(a, d, a.Npc.Walk != null ? Bank.Frames(a.Npc.Walk) : CharaClip(Folks.Sheet(a.Npc), d));
            if (a.Anim != null) a.Anim.Fps = 1f;
        }

        /// <summary>The hero's half of the same courtesy: turn to look at whoever is speaking.</summary>
        public void FaceHeroAt(Vector2 toward)
        {
            if (Hero == null || Hero.Root == null) return;
            var delta = toward - (Vector2)Hero.Root.localPosition;
            if (delta.sqrMagnitude < 0.01f) return;
            var d = DirVec.From(delta);
            Face(Hero, d, HeroClip(d));
            if (Hero.Anim != null) Hero.Anim.Fps = 1f;
        }

        /// <summary>True while the zone card owns the top of the frame ("NIGHT TWO" / "the long
        /// fields"). Game parks the notice while this is up: both are drawn in the same strip,
        /// and the runtime audit caught them printing over each other in the village.</summary>
        public bool BannerUp { get; private set; }
        Coroutine _bannerCo;

        /// <summary>A banner mid-fade dies with the coroutine when the world is tucked away
        /// (a house, a battle, the title card): pick its fade back up on return, or the
        /// frozen words hang over the next room forever.</summary>
        void OnEnable()
        {
            if (ZoneChip != null && ZoneChip.enabled) { BannerUp = true; StartBannerFade(); }
        }

        void OnDisable()
        {
            // a killed fade never runs its finally: drop the flag here or held toasts never flush
            _bannerCo = null;
            BannerUp = false;
        }

        void StartBannerFade()
        {
            // one fade at a time: a banner shown while the last one is still fading would
            // otherwise have its text wiped by the old fade's end (and its flag cleared early)
            if (_bannerCo != null) StopCoroutine(_bannerCo);
            _bannerCo = StartCoroutine(BannerFade());
        }

        public void ShowBanner(string text)
        {
            if (ZoneBanner == null) return;
            // stop the old fade first: if its finally runs on stop, it must not clear the
            // flag we are about to raise for this banner
            if (_bannerCo != null) StopCoroutine(_bannerCo);
            BannerUp = true;
            // "NIGHT 2\nthe long fields" is two lines: the night in the big face, the place in
            // the small one
            var parts = (text ?? "").Split('\n');
            string top = parts.Length > 0 ? parts[0] : "";
            string sub = parts.Length > 1 ? parts[1] : "";
            // the frame is 18 units wide and the label centres itself: a long line at the big
            // face bleeds past both edges, so it steps down to the small face to fit
            ZoneBanner.Scale = 2;
            if (ZoneBanner.MeasureWidth(top) > 15.8f) ZoneBanner.Scale = 1;
            ZoneBanner.Set(top, true);
            if (ZoneBannerSub != null) ZoneBannerSub.Set(sub, true);
            if (ZoneChip != null)
            {
                bool on = !string.IsNullOrEmpty(top) || !string.IsNullOrEmpty(sub);
                ZoneChip.enabled = on;
                float w = Mathf.Max(ZoneBanner.MeasureWidth(top),
                    ZoneBannerSub != null ? ZoneBannerSub.MeasureWidth(sub) : 0f);
                ZoneChip.transform.localScale = new Vector3(Mathf.Max(2.4f, w + 1.3f), 2.3f, 1f);
                ZoneChip.transform.localPosition = new Vector3(0f, HalfH - 5.55f, 0f);
            }
            if (Application.isPlaying) StartBannerFade();
        }

        System.Collections.IEnumerator BannerFade()
        {
            yield return Fx.Wait(2.2f);
            try
            {
                float e = 0f;
                while (e < 0.6f)
                {
                    // the view can be torn down mid-fade (rebuilt room, next chapter): the
                    // labels are gone by then, and touching them would fault - let it die
                    if (ZoneBanner == null) yield break;
                    e += Time.deltaTime;
                    float a = 1f - Mathf.Clamp01(e / 0.6f);
                    var c = ZoneBanner.Tint; c.a = a;
                    ZoneBanner.SetColor(c);
                    if (ZoneBannerSub != null) { var sc = ZoneBannerSub.Tint; sc.a = a * 0.9f; ZoneBannerSub.SetColor(sc); }
                    if (ZoneChip != null)
                    {
                        var cc = ZoneChip.color; cc.a = 210f / 255f * a; ZoneChip.color = cc;
                    }
                    yield return null;
                }
                if (ZoneBanner == null) yield break;
                ZoneBanner.Set("", true);
                ZoneBanner.SetColor(new Color(1f, 0.95f, 0.8f, 1f));
                if (ZoneBannerSub != null) { ZoneBannerSub.Set("", true); ZoneBannerSub.SetColor(new Color(0.82f, 0.85f, 1f, 1f)); }
                if (ZoneChip != null)
                {
                    ZoneChip.enabled = false;
                    ZoneChip.color = new Color32(10, 8, 20, 210);
                }
            }
            finally
            {
                // the flag outlives the hold AND the fade: a held toast flushing at fade
                // start would still land on the last visible glyphs
                BannerUp = false;
            }
        }

        // ---------------------------------------------------------------- hero control

        public void PlaceHero(Vector2 pos)
        {
            if (Hero == null) CreateHero();
            // never spawn the hero outside the painted map - the camera clamps to the
            // map bounds and an out-of-range spawn pushed the village shot half off-world
            float x = Mathf.Clamp(pos.x, 2f, GameMap.W - 2f);
            float y = Mathf.Clamp(pos.y, 1.5f, GameMap.H - 2f);
            var p = new Vector2(x, y);
            if (!CanStand(p)) p = NearestStandable(p);
            Hero.Root.localPosition = new Vector3(p.x, p.y, 0f);
            // the trail starts over under the hero: the friends gather at his heels and
            // the first crumb of the new walk is where he stands
            _crumbs.Clear();
            _crumbs.Add(Hero.Root.localPosition);
            for (int i = 0; i < _friends.Count; i++)
            {
                var f = _friends[i];
                var back = p + new Vector2(0f, -0.55f * (i + 1));
                f.Root.localPosition = CanStand(back) ? new Vector3(back.x, back.y, 0f)
                    : Hero.Root.localPosition;
                if (f.Body != null) f.Body.localPosition = Vector3.zero;
                SortActor(f);
            }
            RefreshNamePlates();
        }

        // Rings of half-tile steps around a blocked spot; the first cell the feet box fits
        // wins, so the hero lands beside whatever was in the way instead of inside it.
        Vector2 NearestStandable(Vector2 from)
        {
            Vector2 best = from; float bd = float.MaxValue;
            for (int r = 1; r <= 6; r++)
                for (int dx = -r; dx <= r; dx++)
                    for (int dy = -r; dy <= r; dy++)
                    {
                        if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) != r) continue;
                        var p = from + new Vector2(dx * 0.5f, dy * 0.5f);
                        if (p.x < 2f || p.y < 1.5f || p.x > GameMap.W - 2f || p.y > GameMap.H - 2f) continue;
                        if (!CanStand(p)) continue;
                        float d = (p - from).sqrMagnitude;
                        if (d < bd) { bd = d; best = p; }
                    }
            return best;
        }

        void CreateHero()
        {
            Hero = MakeActor(Vector2.zero, WorldOrder(0f), isNpc: false);
            Hero.Root.name = "hero";
            Hero.Speed = 4.6f;
            // The hero carries no name plate. "YOU" floating over the character the player is
            // steering is the one label that tells them nothing and covers the sprite doing it.
            Hero.Name = null;
            // A soft lantern aura: at night the dark sprite reads as grass without it, and the
            // light the thief carries is the one focal point in every frame it shares.
            var hglow = SpriteRendererUtil.Make(Hero.Root, "hglow", TexArt.Glow(), 2010);
            hglow.transform.localPosition = new Vector3(0f, 0.35f, 0f);
            hglow.transform.localScale = new Vector3(4.4f, 3.2f, 1f);
            hglow.color = new Color(1f, 0.85f, 0.5f, 0.30f);
            AddGlow(hglow, 0.22f);
            _heroGlow = hglow;
            _heroGlowIdx = _glows.Count - 1;

            // the friends the thief raises: sea and moss walk the road behind amber the
            // way they stand behind them in a fight. Only a down-facing frame exists in
            // the pack, so they hop like the critters do - the night reads them as company.
            for (int i = 0; i < FriendArt.Length; i++)
            {
                var f = MakeActor(Vector2.zero, WorldOrder(0f), isNpc: false);
                f.Root.name = "friend" + i;
                f.Name = null;
                f.Speed = 4.6f;
                var fr = Bank.Frames(FriendArt[i]);
                if (fr.Length > 0) f.Anim.Play(fr, 1f, true);
                // the thief starts the tale alone: a companion's walker only exists once
                // their wandering self has been talked into coming along
                f.Root.gameObject.SetActive(Game.State.Joined.Contains(FriendKeys[i]));
                _friends.Add(f);
            }
            SyncFriends();
        }

        static readonly string[] FriendArt =
        {
            "Art/Hero/hero/color_2/walk/hero_walk_DOWN",
            "Art/Hero/hero/color_3/walk/hero_walk_DOWN",
        };

        /// <summary>The walker order above, matched to the roster keys they belong to -
        /// sea first (color_2), then moss (color_3). A friend that has not joined yet is
        /// still an actor, just hidden: it stands in the world as the villager you meet.</summary>
        static readonly string[] FriendKeys = { "hero.sea", "hero.moss" };

        static readonly Color32 FriendChip = new Color32(58, 42, 14, 215);

        /// <summary>Beasts raised in battle walk the line too: a befriended monster joins the
        /// trail behind sea and moss on the first frame back in the world. Built here (and
        /// called again when a battle lets one go) so a saved game keeps its company.</summary>
        public void SyncFriends()
        {
            for (int i = 0; i < Game.State.Friends.Count; i++)
            {
                string key = Game.State.Friends[i];
                bool moonlit = key.StartsWith("moon.");
                var s = BattleData.Species(moonlit ? key.Substring(5) : key);
                if (!s.HasValue) continue;
                var spec = s.Value; spec.Rare = moonlit;
                bool have = false;
                for (int j = 0; j < _friends.Count; j++)
                    if (_friends[j].Spec.Name == spec.Name) { have = true; break; }
                if (have) continue;
                var f = MakeActor(Vector2.zero, WorldOrder(0f), isNpc: false);
                f.Root.name = "friend" + _friends.Count;
                f.Speed = 4.6f;
                f.Spec = spec;
                if (moonlit)
                {
                    f.Sr.color = new Color(0.72f, 0.84f, 1f);
                    // the star a wild moonlit wears follows it into the stable -
                    // a rare catch should read rare on the trail too
                    var rgo = new GameObject("rareMark");
                    rgo.transform.SetParent(f.Root, false);
                    rgo.transform.localPosition = new Vector3(0f, 1.55f, 0f);
                    rgo.transform.localScale = Vector3.one * 0.55f;
                    f.Rare = rgo.AddComponent<SpriteRenderer>();
                    f.Rare.sprite = TexArt.MenuIcon(21);
                    f.Rare.color = new Color(0.8f, 0.9f, 1f, 0.9f);
                    f.Rare.sortingOrder = 2100;
                }
                // a warm tag tells it apart from the wild look-alikes roaming the same fields
                MakeNamePlate(f, Strings.Get(spec.Name), FriendChip);
                var spr = TexArt.MapMonster(s.Value.MapSheet, 1);
                if (spr != null) f.Anim.Play(new[] { spr }, 1f, true);
                if (Hero?.Root != null)
                {
                    var hp = (Vector2)Hero.Root.localPosition;
                    var back = hp + new Vector2(0f, -0.55f * (_friends.Count + 1));
                    f.Root.localPosition = CanStand(back) ? new Vector3(back.x, back.y, 0f)
                        : new Vector3(hp.x, hp.y, 0f);
                }
                _friends.Add(f);
                SortActor(f);
            }
        }

        /// <summary>Re-reads who has joined and dresses the trail to match: a companion's
        /// walker lights up the moment they say yes, and their wandering self steps off
        /// the map. Called after every build (a load can land mid-company), and from the
        /// join moment itself so the trail gains them the second the dialog closes.</summary>
        public void SyncParty()
        {
            for (int i = 0; i < FriendKeys.Length && i < _friends.Count; i++)
            {
                var f = _friends[i];
                if (f == null || f.Root == null) continue;
                bool joined = Game.State.Joined.Contains(FriendKeys[i]);
                bool was = f.Root.gameObject.activeSelf;
                f.Root.gameObject.SetActive(joined);
                if (joined && !was && Hero?.Root != null)
                {
                    var hp = (Vector2)Hero.Root.localPosition;
                    var back = hp + new Vector2(0f, -0.55f * (i + 1));
                    f.Root.localPosition = CanStand(back) ? new Vector3(back.x, back.y, 0f)
                        : new Vector3(hp.x, hp.y, 0f);
                }
            }
            for (int i = Npcs.Count - 1; i >= 0; i--)
                if (Npcs[i].Npc.JoinKey != null && Game.State.Joined.Contains(Npcs[i].Npc.JoinKey))
                    RemoveNpc(Npcs[i]);
            // a soul an errand sent home stays home across saves: the cast follows quest
            // state, not the chapter's spawn list - unless some errand of his own still
            // holds him to the road
            foreach (var q in Quests.All)
            {
                if (q.Kind != QuestKind.Talk || string.IsNullOrEmpty(q.Target)
                    || Quests.Step(q.Id) != 2) continue;
                bool owed = false;
                foreach (var qq in Quests.All)
                    if (qq.Giver == q.Target && qq.Chapter <= Game.State.Chapter
                        && Quests.Step(qq.Id) != 3) { owed = true; break; }
                if (owed) continue;
                var a = FindNpc(q.Target);
                if (a != null) RemoveNpc(a);
            }
        }

        /// <summary>Takes a villager off the street: plate, chip, sprite and the list entry.
        /// A removed npc can no longer be found by FindNpc, talked to, or plate-refreshed.</summary>
        public void RemoveNpc(Actor a)
        {
            if (a == null) return;
            if (a.Name != null) a.Name.gameObject.SetActive(false);
            if (a.NameChip != null) a.NameChip.enabled = false;
            if (a.Root != null) Fx.Kill(a.Root.gameObject);
            Npcs.Remove(a);
        }

        /// <summary>Wish a friend back to the night: the journal asks, the wild gets one
        /// of its own back. The species clears from the stable list (both its normal and
        /// moonlit keys), its walker leaves the trail, and its name tag comes down.</summary>
        public void ReleaseFriend(string species)
        {
            Game.State.Friends.Remove(species);
            Game.State.Friends.Remove("moon." + species);
            for (int i = _friends.Count - 1; i >= 0; i--)
            {
                var f = _friends[i];
                if (f.Spec.Name != species) continue;      // sea & moss carry no Spec
                if (f.Root != null) f.Root.gameObject.SetActive(false);
                if (f.Name != null) f.Name.gameObject.SetActive(false);
                _friends.RemoveAt(i);          // out of the list so the trail leaves no gap
            }
        }

        bool _heroWalking;
        float _stepT;
        float _stepSfxT;

        /// <summary>One pixel up on the beat, or nothing at all: a walk bob that never lands
        /// between two pixels. Used by the hero, the villagers and the monsters alike, so the
        /// whole cast steps on the same grid.</summary>
        static float StepBob(float t, float hz, float phase)
            => Mathf.Abs(Mathf.Sin((t + phase) * hz)) > 0.45f ? Fx.Pixel : 0f;

        /// <summary>True while the hero creeps: a gentle joystick push (or a held Shift)
        /// drops his gait to a prowl - slower, dimmer, silent, and all but unseen until
        /// he is nearly standing on a beast. The shade on his sprite is the tell.</summary>
        public bool SneakHeld;
        public bool Sneaking { get; private set; }

        void SetHeroShade(bool sneak)
        {
            if (Hero == null || Hero.Sr == null) return;
            var c = Hero.Sr.color;
            c.a = sneak ? 0.62f : 1f;
            Hero.Sr.color = c;
            // a fading thief can't keep a full dark pool under his feet - the
            // shadow creeps with him or the whole trick reads wrong
            if (Hero.Shadow != null)
            {
                var sc = Hero.Shadow.color;
                sc.a = sneak ? 0.4f : 1f;
                Hero.Shadow.color = sc;
            }
            // the company creeps with him: a party at full glow while their leader
            // prowls would give the whole trick away
            for (int i = 0; i < _friends.Count; i++)
            {
                if (_friends[i].Sr != null)
                {
                    var fc = _friends[i].Sr.color;
                    fc.a = sneak ? 0.62f : 1f;
                    _friends[i].Sr.color = fc;
                }
                if (_friends[i].Shadow != null)
                {
                    var fs = _friends[i].Shadow.color;
                    fs.a = sneak ? 0.4f : 1f;
                    _friends[i].Shadow.color = fs;
                }
            }
        }

        public bool DriveHero(Vector2 input, float dt)
        {
            if (Hero == null) return false;
            if (input.sqrMagnitude < 0.001f)
            {
                Sneaking = SneakHeld;   // a held breath still counts as hiding
                SetHeroShade(Sneaking);
                if (_heroWalking)
                {
                    _heroWalking = false;
                    _stepT = 0f;
                    if (Hero.Body != null) Hero.Body.localPosition = Vector3.zero;
                    Hero.Anim.Play(HeroBreath(), 4f, true);
                }
                return false;
            }

            // the stick is analogue: a half-push walks softly on its own, and only a full
            // push is a stride. The flag watches the magnitude, so creeping needs no button.
            float mag = Mathf.Clamp01(input.magnitude);
            bool sneak = SneakHeld || mag < 0.55f;
            Sneaking = sneak;
            SetHeroShade(sneak);

            var dir = DirVec.From(input);
            if (!_heroWalking || Hero.Facing != dir)
            {
                _heroWalking = true;
                Hero.Facing = dir;
                Hero.Anim.Play(HeroClip(dir), 8f, true);
            }

            // A whole-pixel step on the body while walking, never a fraction of one. The old bob
            // was 0.06 units - just under the 1/16 unit a pixel is - so every frame of the walk
            // cycle rendered between two pixel rows and the hero shimmered instead of stepping.
            // One pixel up on the beat reads as weight and stays sharp.
            _stepT += dt;
            if (Hero.Body != null)
                Hero.Body.localPosition = new Vector3(0f, StepBob(_stepT, 7.5f, 0f), 0f);
            _stepSfxT -= dt;
            if (_stepSfxT <= 0f)
            {
                // the ground answers back: wood floors ring higher than packed road,
                // and grass is softest of the three
                var g = Map != null ? Map.At(new Vector2Int(
                    Mathf.RoundToInt(HeroPos.x), Mathf.RoundToInt(HeroPos.y))) : Ground.Grass;
                float surf = g == Ground.Floor ? 1.28f : g == Ground.Path ? 1.12f : 1f;
                Sfx.Play("step", (0.9f + UnityEngine.Random.value * 0.2f) * surf * (sneak ? 0.62f : 1f));
                if (!sneak)
                {
                    SpawnDust((Vector2)Hero.Root.localPosition - input.normalized * 0.35f);
                    // a heavy foot within earshot peels a warning ring off the heel -
                    // the hearing band made visible exactly when it matters
                    // the ring must tell the truth about the hearing band: it used to
                    // warn at 5.5 paces while the wild only heard at 3.6 - rings for
                    // footsteps nothing could hear taught the wrong lesson. The moonlit's
                    // keener ears get their own warning distance inside AnyCanHear.
                    if (AnyCanHear(HeroPos)) SpawnRipple(Hero.Root.localPosition);
                }
                _stepSfxT = sneak ? 0.34f : 0.24f;
            }

            var pos = (Vector2)Hero.Root.localPosition;
            var next = pos + input.normalized * Hero.Speed
                * (SneakHeld ? 0.55f : Mathf.Min(mag, 1f)) * dt;
            // axis-separated collision so sliding along walls feels right
            if (CanStand(new Vector2(next.x, pos.y))) pos.x = next.x;
            if (CanStand(new Vector2(pos.x, next.y))) pos.y = next.y;
            pos.x = Mathf.Clamp(pos.x, 2.5f, GameMap.W - 2.5f);
            pos.y = Mathf.Clamp(pos.y, 2.5f, GameMap.H - 2.5f);
            Hero.Root.localPosition = new Vector3(pos.x, pos.y, 0f);
            if (_friends.Count > 0 &&
                (_crumbs.Count == 0 || Vector3.Distance(Hero.Root.localPosition, _crumbs[0]) > 0.25f))
            {
                _crumbs.Insert(0, Hero.Root.localPosition);
                if (_crumbs.Count > 160) _crumbs.RemoveAt(_crumbs.Count - 1);
            }
            return true;
        }

        /// <summary>A dust puff under the hero's heel, borrowed from the pool when one is
        /// free. The glow sprite tinted warm and half-faded reads as kicked-up road.</summary>
        void SpawnDust(Vector2 at)
        {
            // kicking in place would stamp puff on puff: a fresh puff barely moved from
            // the last live one reads as a smudge, so it is skipped entirely. The check
            // runs on the jittered landing spot - comparing the heel instead let the
            // jitter stack two puffs on the same cell
            Vector2 land = at + new Vector2(UnityEngine.Random.Range(-0.3f, 0.3f), UnityEngine.Random.Range(-0.2f, 0.05f));
            foreach (var x in _dust)
                if (x.Sr.enabled && (x.Sr.transform.localPosition - new Vector3(land.x, land.y, 0f)).sqrMagnitude < 0.14f)
                    return;
            // a kick reads the ground it lands on: warm village dirt, greener field
            // chalk, a colder blue-grey stirring up the deep wood
            var c = HeroPos.y > 58f ? new Color(0.6f, 0.66f, 0.78f, 0.55f)
                : HeroPos.y > 26f ? new Color(0.82f, 0.86f, 0.6f, 0.55f)
                : new Color(0.95f, 0.9f, 0.74f, 0.55f);
            SpawnPuff(land, Vector2.up * 0.5f, TexArt.Glow(), 0.62f, c, 0.42f);
        }

        /// <summary>Flecks fan out over an opened chest and fall away - gold for purse and
        /// ware, silver-blue for moonlight; the banner says what, the spray says it too.</summary>
        public void BurstLoot(Vector2 at, bool moon = false)
        {
            var tint = moon ? new Color(0.7f, 0.85f, 1f, 0.9f)
                            : new Color(1f, 0.9f, 0.45f, 0.85f);
            for (int i = 0; i < 9; i++)
            {
                float ang = i * (Mathf.PI * 2f / 9f) + UnityEngine.Random.Range(-0.25f, 0.25f);
                var v = new Vector2(Mathf.Cos(ang), Mathf.Abs(Mathf.Sin(ang)) * 0.7f + 0.6f)
                    * UnityEngine.Random.Range(0.8f, 1.5f);
                SpawnPuff(at + new Vector2(0f, 0.25f), v, Tex.Spark(), 0.34f, tint, 0.62f);
            }
        }

        void SpawnPuff(Vector2 at, Vector2 vel, Sprite spr, float scale, Color tint, float life)
        {
            Dust d = null;
            foreach (var x in _dust) if (!x.Sr.enabled) { d = x; break; }
            if (d == null)
            {
                if (_dust.Count >= 26) return;
                // named puff not dust: loot flecks and heel kicks share this pool, and the pile
                // audit lets particles of one family crowd a cell without calling it a defect
                d = new Dust { Sr = SpriteRendererUtil.Make(_root, "puff" + _dust.Count, spr, 1955) };
                _dust.Add(d);
            }
            d.Sr.enabled = true;
            d.Sr.sprite = spr;
            d.Sr.transform.localScale = Vector3.one * scale;
            d.Sr.transform.localPosition = new Vector3(at.x, at.y, 0f);
            d.V = vel;
            d.T = life; d.MaxT = life; d.A = tint.a;
            d.Sr.color = tint;
        }

        /// <summary>Any unwarned beast that would hear a footfall right now - the ripple
        /// only exists to warn about these; village feet and already-chasing monsters
        /// get no rings, and a moonlit beast's keener ears warn a step sooner.</summary>
        bool AnyCanHear(Vector2 pos)
        {
            foreach (var m in Monsters)
            {
                if (m.Root == null || m.Aggro) continue;
                float hearR = m.Spec.Rare
                    ? (Prefs.Hard ? 5.4f : Prefs.Story ? 3.8f : 4.4f)
                    : (Prefs.Hard ? 4.6f : Prefs.Story ? 3.0f : 3.6f);
                if (Vector2.Distance((Vector2)m.Root.localPosition, pos) < hearR) return true;
            }
            return false;
        }

        void SpawnRipple(Vector3 at)
        {
            // standing in earshot used to stack ring on ring into one bright blob -
            // a live ring already marking the spot makes the next one redundant
            foreach (var x in _ripples)
                if (x.Sr != null && x.Sr.enabled
                    && (x.Sr.transform.localPosition - at).sqrMagnitude < 2.6f) return;
            Ripple r = null;
            foreach (var x in _ripples) if (!x.Sr.enabled) { r = x; break; }
            if (r == null)
            {
                if (_ripples.Count >= 6) return;
                r = new Ripple { Sr = SpriteRendererUtil.Make(_root, "ripple" + _ripples.Count, TexArt.Ring(), 1950) };
                _ripples.Add(r);
            }
            r.Sr.enabled = true;
            r.Sr.transform.localPosition = new Vector3(at.x, at.y, 0f);
            r.Sr.transform.localScale = Vector3.one * 0.16f;
            r.Sr.color = new Color(0.78f, 0.84f, 1f, 0.5f);
            r.T = 0.7f;
        }

        void DriveRipples(float dt)
        {
            foreach (var r in _ripples)
            {
                // the ring's sprite hangs under the world content root - a house
                // enter/exit wipes it mid-drift, leaving the pool holding a ghost
                if (r.Sr == null || !r.Sr.enabled) continue;
                r.T -= dt;
                if (r.T <= 0f) { r.Sr.enabled = false; continue; }
                float f = 1f - r.T / 0.7f;
                r.Sr.transform.localScale = Vector3.one * (0.16f + f * 1.4f);
                var c = r.Sr.color; c.a = 0.5f * (1f - f); r.Sr.color = c;
            }
        }

        void DriveDust(float dt)
        {
            foreach (var d in _dust)
            {
                if (!d.Sr.enabled) continue;
                d.T -= dt;
                if (d.T <= 0f) { d.Sr.enabled = false; continue; }
                var p = d.Sr.transform.localPosition;
                p += (Vector3)(d.V * dt);
                d.V *= 1f - 2.4f * dt;   // drag: flecks fling out then hang
                d.Sr.transform.localPosition = p;
                var c = d.Sr.color;
                c.a = d.A * (d.T / d.MaxT);
                d.Sr.color = c;
            }
        }

        /// <summary>A warm pulse under whatever would answer an interact tap right now:
        /// the closest villager, chest, or the boss in reach. Discovery without a prompt
        /// word - the floor itself says "this one".</summary>
        void DriveTouchCue()
        {
            Vector2? at = null;
            var npc = NearestNpc(HeroPos, 1.4f);
            if (npc.NameKey != null)
            {
                var a = FindNpc(npc.NameKey);
                if (a != null && a.Root != null) at = (Vector2)a.Root.localPosition;
            }
            else if (NearBoss && _bossProp != null && _bossProp.enabled)
            {
                at = (Vector2)_bossProp.transform.localPosition;
            }
            else
            {
                int chestAt = NearestChest(HeroPos, 1.2f);
                if (chestAt >= 0) at = _chests[chestAt].Pos;
                else if (NearDoor(HeroPos, out int doorAt))
                {
                    var c = Map.Doors[doorAt];
                    at = new Vector2(c.x + 0.5f, c.y + 0.5f);
                }
            }
            if (at.HasValue)
            {
                if (_touchCue == null)
                {
                    _touchCue = SpriteRendererUtil.Make(_root, "touchCue", TexArt.Glow(), 1900);
                    _touchCue.transform.localScale = new Vector3(1.7f, 0.65f, 1f);
                }
                _touchCue.enabled = true;
                _touchCue.transform.localPosition = new Vector3(at.Value.x, at.Value.y - 0.2f, 0f);
                _touchCue.color = new Color(1f, 0.9f, 0.6f, 0.45f + 0.3f * Mathf.Sin(_time * 6f));
            }
            else if (_touchCue != null) _touchCue.enabled = false;
        }

        /// <summary>Sea and moss keep pace a few crumbs back on the hero's own footprints,
        /// hopping between steps the way the critters do. Only a down-facing frame exists
        /// in the pack for them, so the trail reads as company, not as a mirror.</summary>
        void DriveFriends(float dt)
        {
            if (_crumbs.Count == 0) return;
            for (int i = 0; i < _friends.Count; i++)
            {
                var f = _friends[i];
                if (f == null || f.Root == null) continue;
                int idx = Mathf.Min((i + 1) * 6, _crumbs.Count - 1);
                var target = (Vector2)_crumbs[idx];
                var pos = (Vector2)f.Root.localPosition;
                float d = Vector2.Distance(pos, target);
                if (d > 3f)
                {
                    // a corner or a door left the friend too far back: cut straight to its
                    // crumb instead of walking a kilometre of wall
                    f.Root.localPosition = target;
                    if (f.Body != null) f.Body.localPosition = Vector3.zero;
                }
                else if (d <= 0.05f)
                {
                    if (f.Body != null) f.Body.localPosition = Vector3.zero;
                }
                else
                {
                    float step = Mathf.Min(d, 5.4f * dt);
                    pos = Vector2.MoveTowards(pos, target, step);
                    f.Root.localPosition = new Vector3(pos.x, pos.y, 0f);
                    if (f.Body != null)
                        f.Body.localPosition = new Vector3(0f, StepBob(_time, 8f, i * 1.7f), 0f);
                }
                if (f.Name != null)
                    f.Name.transform.localPosition = new Vector3(f.Root.localPosition.x, f.Root.localPosition.y + NameAnchorY, 0f);
                if (f.Rare != null)
                    f.Rare.transform.localPosition = new Vector3(0f,
                        1.55f + Mathf.Sin(_time * 3f + i) * 0.07f, 0f);
            }
        }

        Sprite[] _breath;
        Sprite[] HeroBreath() => _breath ??= Bank.Frames("Art/Hero/hero/color_1/breath_idle/hero_breath_idle_DOWN");

        bool CanStand(Vector2 pos)
        {
            // feet box slightly smaller than a tile
            var c0 = Map.CellOf(pos + new Vector2(-0.3f, -0.12f));
            var c1 = Map.CellOf(pos + new Vector2(0.3f, -0.12f));
            var c2 = Map.CellOf(pos + new Vector2(-0.3f, 0.18f));
            var c3 = Map.CellOf(pos + new Vector2(0.3f, 0.18f));
            return Map.Walkable(c0) && Map.Walkable(c1) && Map.Walkable(c2) && Map.Walkable(c3);
        }

        public Vector2 HeroPos => Hero != null ? (Vector2)Hero.Root.localPosition : Vector2.zero;



        public NpcDef NearestNpc(Vector2 pos, float maxDist = 1.4f)
        {
            NpcDef best = default; float bd = maxDist;
            foreach (var n in Npcs)
            {
                float d = Vector2.Distance((Vector2)n.Root.localPosition, pos);
                if (d < bd) { bd = d; best = n.Npc; }
            }
            return best;
        }

        /// <summary>Live position of the npc NearestNpc would find (a far point if none).
        /// The tap can't compare distances without it: spawn Pos isn't where a wanderer stands.
        /// </summary>
        public Vector2 NearestNpcPos(Vector2 pos, float maxDist = 1.4f)
        {
            Vector2 best = pos + Vector2.one * 999f; float bd = maxDist;
            foreach (var n in Npcs)
            {
                if (n.Root == null) continue;
                float d = Vector2.Distance((Vector2)n.Root.localPosition, pos);
                if (d < bd) { bd = d; best = n.Root.localPosition; }
            }
            return best;
        }

        /// <summary>The wild monster that bumped into the hero, if any.</summary>
        public Actor TouchedMonster()
        {
            foreach (var m in Monsters)
            {
                if (m.Root == null) continue;
                if (Vector2.Distance((Vector2)m.Root.localPosition, HeroPos) < 0.85f) return m;
            }
            return null;
        }

        /// <summary>Closest wild monster to the hero (self-test steering).</summary>
        public Vector2? NearestMonsterPos()
        {
            Actor best = null; float bd = float.MaxValue;
            foreach (var m in Monsters)
            {
                if (m.Root == null) continue;
                float d = Vector2.Distance((Vector2)m.Root.localPosition, HeroPos);
                if (d < bd) { bd = d; best = m; }
            }
            return best?.Root != null ? (Vector2?)(Vector2)best.Root.localPosition : null;
        }

        /// <summary>Takes a monster off the map - caught, fled or fought - and files its
        /// respawn ticket: same species, same patch of road, back in half a minute to a
        /// minute-and-a-half, and only once the hero has wandered off. This replaces the
        /// old wipe-and-fill, where one kill restocked the entire night in a frame.</summary>
        public void RemoveMonster(Actor m)
        {
            RemoveMonster(m, RespawnDelay);
        }

        public void RemoveMonster(Actor m, float delay)
        {
            if (m == null) return;
            _respawns.Add(new Respawn { Spec = m.Spec, Home = m.HomeCell, T = delay });
            Monsters.Remove(m);
            if (m.Root != null) Fx.Kill(m.Root.gameObject);
        }

        public bool NearBoss => Vector2.Distance(HeroPos, Map.BossPos) < 1.6f;

        // ---------------------------------------------------------------- objective compass

        SpriteRenderer _objArrow;

        /// <summary>The moon-chevron that points at the current objective from the screen edge.
        /// It lives under HudRoot (so it is fixed to the glass, not the world) and disappears
        /// when the target is underfoot or inside a house: a compass that says "walk" only
        /// ever shows while there is somewhere to walk to.</summary>
        public void SetObjective(Vector2? target)
        {
            if (HudRoot == null) return;
            if (_objArrow == null)
            {
                _objArrow = SpriteRendererUtil.Make(HudRoot, "objArrow", TexArt.Chevron(), 5002);
                _objArrow.transform.localScale = Vector3.one * 1.7f;
                _objArrow.color = new Color(1f, 0.93f, 0.55f, 0.95f);
            }
            // the zone card owns the same strip the arrow is pinned to: while a banner is
            // up the chevron sits over its words, so it steps out until the card is gone
            if (!target.HasValue || !_textOn || BannerUp)
            {
                _objArrow.enabled = false;
                return;
            }
            var dir = target.Value - HeroPos;
            float dist = dir.magnitude;
            // the arrow is a compass for what you cannot see: once the gate itself is in
            // frame its own "!" cue marks it, and a second pointer at the edge is noise
            if (dist < 3.5f || (Mathf.Abs(dir.x) < 8.2f && Mathf.Abs(dir.y) < HalfH - 2.5f))
            {
                _objArrow.enabled = false;
                return;
            }
            _objArrow.enabled = true;
            var n = dir.normalized;
            // place the chevron on an ellipse just inside the frame, below the HUD bar:
            // ray direction n scaled until it hits the ellipse edge
            float rx = 7.4f, ry = HalfH - 5.8f;
            float s = 1f / Mathf.Sqrt((n.x * n.x) / (rx * rx) + (n.y * n.y) / (ry * ry));
            var p = n * s;
            _objArrow.transform.localPosition = new Vector3(p.x, p.y + Mathf.Sin(_time * 4f) * 0.14f, 0f);
            // the chevron glyph points left (-x) unrotated, so aim it along n with a 180 offset
            _objArrow.transform.localEulerAngles = new Vector3(0f, 0f, Mathf.Atan2(n.y, n.x) * Mathf.Rad2Deg + 180f);
        }

        // ---------------------------------------------------------------- ambient barks

        // one shared bubble: the villager closest to the hero may mutter a one-liner
        // overhead (bark.<name> in the strings table), then everyone holds their tongue
        // a while so the street never babbles
        PixelLabel _bark;
        SpriteRenderer _barkChip;
        Actor _barkActor;
        float _barkT;
        float _barkCd;
        float _barkW;

        // the bubble may not hang off the frame: pull its centre back inside the camera
        // box (9 units half-width, the same bound FollowHero clamps the camera to)
        float BarkX(float x)
        {
            float pad = _barkW * 0.5f + 0.4f;
            return Mathf.Clamp(x, ViewCenter.x - 9f + pad, ViewCenter.x + 9f - pad);
        }

        /// <summary>The bubble's x, dodged off any nameplate it would sit on. The crowd
        /// parks its plates one row up at almost the bubble's band, so a sideways nudge
        /// usually finds air; a packed knot keeps the clamped spot and that's fine.
        /// _barkW is the raw text half-width - the chip rides a little wider.
        /// </summary>
        float BarkPlaceX(float x, float y)
        {
            float bxf = BarkX(x);
            for (int i = 0; i < Npcs.Count; i++)
            {
                var o = Npcs[i];
                if (o == _barkActor || o?.Name == null || !o.Name.gameObject.activeSelf) continue;
                var op = o.Name.transform.localPosition;
                if (Mathf.Abs(op.y - y) > 1.1f) continue;
                float ow = o.NameChip != null ? o.NameChip.transform.localScale.x : 2.2f;
                // compare chip to chip, not text to plate: the bark's dark back rides
                // 0.55 wider than its glyphs, and a plate that only abuts it still
                // reads as glued on ("PRUNEwatch the treeline")
                float overlap = (_barkW + 0.55f + ow) * 0.5f + 0.25f - Mathf.Abs(bxf - op.x);
                if (overlap > 0.05f)
                {
                    float dir = bxf <= op.x ? -1f : 1f;
                    bxf = BarkX(bxf + dir * (overlap + 0.55f));
                }
            }
            return bxf;
        }

        void TickBarks(float dt)
        {
            // world text hidden means a card owns the screen: no bubble keeps talking over it,
            // and none is born under it - the tongue rests while the panel is up
            if (!_textOn) return;
            if (_barkCd > 0f) _barkCd -= dt;
            if (_barkT > 0f)
            {
                _barkT -= dt;
                // the speaker strolls on: the bubble rides its head for as long as it shows
                if (_bark != null && _barkActor?.Root != null)
                {
                    var ap = (Vector2)_barkActor.Root.localPosition;
                    float bxf = BarkPlaceX(ap.x, ap.y + NameAnchorY + 1.15f);
                    _bark.transform.localPosition = new Vector3(bxf, ap.y + NameAnchorY + 1.15f, 0f);
                    _barkChip.transform.localPosition = new Vector3(
                        bxf, ap.y + NameAnchorY + 1.15f - _barkChip.transform.localScale.y * 0.32f, 0f);
                }
                if (_barkT <= 0f && _bark != null)
                {
                    _bark.gameObject.SetActive(false);
                    _barkChip.enabled = false;
                }
                return;
            }
            if (_barkCd > 0f) return;

            Actor who = null;
            float best = 2.6f;
            for (int i = 0; i < Npcs.Count; i++)
            {
                var a = Npcs[i];
                if (a?.Root == null || a.Npc.NameKey == null) continue;
                float d = Vector2.Distance((Vector2)a.Root.localPosition, HeroPos);
                if (d < best) { best = d; who = a; }
            }
            if (who == null) { _barkCd = 0.8f; return; }

            string tail = who.Npc.NameKey.StartsWith("npc.") ? who.Npc.NameKey.Substring(4) : who.Npc.NameKey;
            string key = "bark." + tail;
            if (!Strings.Has(key)) { _barkCd = 2f; return; }

            if (_bark == null)
            {
                _bark = PixelLabelUtil.Make(_root, "bark", 1, new Color(1f, 0.97f, 0.85f), TextAlign.Center, 2102);
                _barkChip = SpriteRendererUtil.Make(_root, "barkChip", TexArt.Solid(), 2101);
                _barkChip.color = new Color(0.07f, 0.06f, 0.12f, 0.85f);
            }
            string line = Strings.Get(key);
            _bark.Set(line);
            float w = _bark.MeasureWidth(line), h = _bark.MeasureHeight(line);
            _barkChip.transform.localScale = new Vector3(w + 0.55f, h + 0.34f, 1f);
            _barkW = w;
            _barkActor = who;
            var pos = (Vector2)who.Root.localPosition;
            // a full label-height over the name plate: at +0.7 the bubble's hung text
            // still came down onto the sprite's own bounds (and any friend beside it)
            float bx = BarkPlaceX(pos.x, pos.y + NameAnchorY + 1.15f);
            _bark.transform.localPosition = new Vector3(bx, pos.y + NameAnchorY + 1.15f, 0f);
            _barkChip.transform.localPosition = new Vector3(bx, pos.y + NameAnchorY + 1.15f - (h + 0.34f) * 0.32f, 0f);
            _bark.gameObject.SetActive(true);
            _barkChip.enabled = true;
            _barkT = 2.6f;
            _barkCd = 10f + (who.GetHashCode() % 5);
        }

        // ---------------------------------------------------------------- update

        void Update()
        {
            _time += Time.deltaTime;
            if (Map == null || !Ready) return;
            float dt = Time.deltaTime;

            // Everything that stands in the world is re-sorted by the row it stands on, every
            // frame, in one place: the hero, the villagers, the wild monsters and the critters.
            // Props get their order once, when they are built (they never move).
            SortActor(Hero);
            for (int i = 0; i < Monsters.Count; i++) SortActor(Monsters[i]);
            for (int i = 0; i < Npcs.Count; i++) SortActor(Npcs[i]);
            for (int i = 0; i < _critters.Count; i++) SortActor(_critters[i]);
            for (int i = 0; i < _friends.Count; i++) SortActor(_friends[i]);

            // fireflies: slow drift + blink over the fields, dimmer deep in the forest
            for (int i = 0; i < _flies.Count; i++)
            {
                var f = _flies[i];
                var p = f.localPosition;
                f.localPosition = new Vector3(
                    p.x + Mathf.Sin(_time * 0.35f + i * 1.7f) * dt * 0.55f,
                    p.y + Mathf.Cos(_time * 0.27f + i * 2.3f) * dt * 0.4f, p.z);
                float blink = 0.5f + 0.5f * Mathf.Sin(_time * 2.4f + i * 1.31f);
                var sr = _flySprites[i];
                var c = sr.color;
                c.a = blink * (p.y > 59f ? 0.22f : 0.55f);
                sr.color = c;
            }

            TickBarks(dt);

            // NPC idle life: locals stroll around their spot and pause
            for (int i = 0; i < Npcs.Count; i++)
            {
                var n = Npcs[i];
                if (n.Root == null || n.Npc.NameKey == null) continue;
                if (n.Pause > 0f)
                {
                    n.Pause -= dt;
                    if (n.Anim != null) n.Anim.Fps = 0f;
                    // standing still is not frozen: a slow one-pixel breath keeps the figure alive
                    if (n.Body != null)
                        n.Body.localPosition = new Vector3(0f, StepBob(_time + i, 1.1f, 0f), 0f);
                    continue;
                }
                if (n.Dest == default)
                {
                    var home = n.Npc.Pos;
                    for (int t = 0; t < 6; t++)
                    {
                        var cell = new Vector2Int(Mathf.RoundToInt(home.x - 0.5f) + Random.Range(-2, 3),
                                                  Mathf.RoundToInt(home.y - 0.5f) + Random.Range(-2, 3));
                        // the stroll used to check only the ground, so two villagers could
                        // pick the same corner and spend the pause stacked in one sprite -
                        // two heads, two name plates, one tile. A spot is free only while
                        // nobody else stands there or is already walking to it
                        if (!Map.Walkable(cell)) continue;
                        var center = Map.CellCenter(cell);
                        bool taken = false;
                        for (int j = 0; j < Npcs.Count; j++)
                        {
                            if (j == i || Npcs[j].Root == null) continue;
                            if (Vector2.Distance(Npcs[j].Root.localPosition, center) < 0.9f
                                || (Npcs[j].Dest != default && Map.CellCenter(Npcs[j].Dest) == center))
                            { taken = true; break; }
                        }
                        if (!taken) { n.Dest = cell; break; }
                    }
                    if (n.Dest == default) { n.Pause = 1f; continue; }
                }
                var npos = (Vector2)n.Root.localPosition;
                var target = Map.CellCenter(n.Dest);
                var delta = target - npos;
                if (delta.sqrMagnitude < 0.05f)
                {
                    n.Dest = default;
                    n.Pause = Random.Range(1.2f, 3.5f);
                    n.Root.localPosition = new Vector3(npos.x, npos.y, 0f);
                    continue;
                }
                var nstep = delta.normalized * 0.8f * dt;
                var nnx = npos + new Vector2(nstep.x, 0f);
                var nny = npos + new Vector2(0f, nstep.y);
                if (Map.Walkable(Map.CellOf(nnx))) npos.x = nnx.x;
                if (Map.Walkable(Map.CellOf(nny))) npos.y = nny.y;
                n.Root.localPosition = new Vector3(npos.x, npos.y, 0f);
                var ndir = DirVec.From(delta);
                Face(n, ndir, n.Npc.Walk != null ? Bank.Frames(n.Npc.Walk) : CharaClip(Folks.Sheet(n.Npc), ndir));
                if (n.Anim != null) n.Anim.Fps = 4.5f;
                if (n.Body != null) n.Body.localPosition = new Vector3(0f, StepBob(_time + i, 6.5f, 0f), 0f);
                if (n.Name != null)
                    n.Name.transform.localPosition = new Vector3(npos.x, npos.y + NameAnchorY, 0f);
            }

            // the embers under unfired events go out for good once their moment passes
            if (_eventSpots.Count > 0)
            {
                _evTick -= dt;
                if (_evTick <= 0f)
                {
                    _evTick = 0.5f;
                    foreach (var s in _eventSpots)
                        if (s.Sr != null && s.Sr.enabled && Quests.FiredAlready(s.Id))
                        {
                            if (s.Sr2 != null) s.Sr2.enabled = false;
                            // a last bright sigh, then nothing - a poof would read as a
                            // pickup, and the event itself already paid out its toast
                            s.Sr.color = new Color(1f, 0.95f, 0.75f, 0.8f);
                            s.Sr.enabled = false;
                        }
                }
            }

            // the hero's pool thins while he hides - a fading thief with a full lantern
            // under him would still read as a beacon to anything watching the field
            _sneakGlow = Mathf.MoveTowards(_sneakGlow, Sneaking ? 0.38f : 1f, dt * 3.2f);

            // torch flicker: one curve, one peak per light
            for (int i = 0; i < _glows.Count; i++)
            {
                var g = _glows[i];
                if (g == null) continue;
                float f = 0.84f + 0.11f * Mathf.Sin(_time * 7f + i * 2.1f) + 0.06f * Mathf.Sin(_time * 13.7f + i);
                float amp = i < _glowAmp.Count ? _glowAmp[i] : 0.3f;
                if (i == _heroGlowIdx) amp *= _sneakGlow;
                var c = g.color;
                c.a = Mathf.Clamp01(f * amp);
                g.color = c;
            }
            if (_cristalGlow != null)
            {
                float f = 0.30f + 0.09f * Mathf.Sin(_time * 2.2f);
                var c = _cristalGlow.color; c.a = f; _cristalGlow.color = c;
            }

            // critters hop around their home tile, never far from it
            for (int i = 0; i < _critters.Count; i++)
            {
                var a = _critters[i];
                if (a.Root == null) continue;
                if (a.Pause > 0f)
                {
                    a.Pause -= dt;
                    if (a.Anim != null) a.Anim.Fps = 0f;
                    continue;
                }
                var pos = (Vector2)a.Root.localPosition;
                if (a.Dest == default || Vector2.Distance(pos, Map.CellCenter(a.Dest)) < 0.12f)
                {
                    for (int t = 0; t < 6; t++)
                    {
                        var cell = new Vector2Int(
                            Mathf.RoundToInt(a.HomeCell.x) + Random.Range(-3, 4),
                            Mathf.RoundToInt(a.HomeCell.y) + Random.Range(-3, 4));
                        if (Map.Walkable(cell)) { a.Dest = cell; break; }
                    }
                    a.Pause = Random.Range(0.6f, 2.6f);
                    if (a.Dest == default) continue;
                }
                var target = Map.CellCenter(a.Dest);
                var delta = target - pos;
                if (delta.sqrMagnitude > 0.01f)
                {
                    var adir = DirVec.From(delta);
                    Face(a, adir, AnimalClip(a.Art, adir));
                    var step = delta.normalized * a.Speed * dt;
                    // small feet still go around the trunks, same as the wild do
                    var cnx = pos + new Vector2(step.x, 0f);
                    var cny = pos + new Vector2(0f, step.y);
                    if (Map.Walkable(Map.CellOf(cnx))) pos.x = cnx.x;
                    if (Map.Walkable(Map.CellOf(cny))) pos.y = cny.y;
                    a.Root.localPosition = new Vector3(pos.x, pos.y, 0f);
                    if (a.Anim != null) a.Anim.Fps = 5.5f;
                }
            }

            // the friends keep their slots on the breadcrumb line
            DriveFriends(dt);
            DriveDust(dt);
            DriveRipples(dt);
            DriveTouchCue();

            // while a talk is open the wild holds its breath: nothing hears, nothing
            // wakes, nothing walks up to wait at the hero's heels for the last line
            if (!Game.DialogOpen)
            {
            // respawn tickets: a felled monster comes back after its delay, and only while
            // the hero is somewhere else - nothing materialises on top of the player
            for (int i = _respawns.Count - 1; i >= 0; i--)
            {
                var r = _respawns[i];
                r.T -= dt;
                if (r.T > 0f) { _respawns[i] = r; continue; }
                if (Vector2.Distance(r.Home, HeroPos) < 7f) { r.T = 4f; _respawns[i] = r; continue; }
                var cell = Map.CellOf(r.Home);
                if (!Map.Walkable(cell)) { r.T = 6f; _respawns[i] = r; continue; }
                var a = MakeActor(Map.CellCenter(cell), WorldOrder(cell.y), isNpc: false);
                a.Spec = r.Spec;
                a.Speed = r.Spec.Speed * 0.55f;
                a.HomeCell = new Vector2(cell.x, cell.y);
                // a respawn used to come back wide awake every time - clear a patch of
                // dozers and the night forgot how to nap. The return rolls the same
                // doze dice the first spawn did
                int doze = MapChapter == 1 ? 20 : MapChapter == 2 ? 13 : 7;
                if (Prefs.Hard) doze /= 2;
                a.Asleep = UnityEngine.Random.Range(0, 100) < doze;
                a.Sleeps = a.Asleep;
                a.Anim.Play(MonsterClip(r.Spec.MapSheet, Dir.Down), 4f, true);
                a.Sr.transform.localScale = Vector3.one * (0.68f + r.Spec.Tier * 0.04f + (r.Spec.Rare ? 0.1f : 0f));
                if (r.Spec.Rare) a.Sr.color = new Color(0.72f, 0.84f, 1f);
                var c0 = a.Sr.color; c0.a = 0f; a.Sr.color = c0;
                a.FadeIn = 0.9f;
                Monsters.Add(a);
                _respawns.RemoveAt(i);
            }

            // wild monster wander - and the dozers' rumble, cooling off between snores
            _snoreT -= dt;
            foreach (var m in Monsters)
            {
                if (m.Root == null) continue;
                var mpos = (Vector2)m.Root.localPosition;
                if (m.FadeIn > 0f)
                {
                    // a respawned beast gathers out of the dark instead of blinking in
                    m.FadeIn -= dt;
                    var fc = m.Sr.color;
                    fc.a = 1f - Mathf.Clamp01(m.FadeIn / 0.9f);
                    m.Sr.color = fc;
                }

                // notice the hero: striding feet are HEARD through the dark (3.6u, walls
                // or no), and once close enough also seen outright. A creeping thief is
                // silent and near-invisible - a beast only spots one about to step on it.
                // The "!" holds a beat before the chase so the player gets a dodge window.
                float dh = Vector2.Distance(mpos, HeroPos);
                // a dozing beast is blind but not deaf: sight range collapses to nothing
                // while ears stay as sharp as any hunter's
                // difficulty tunes the senses both ways: the kind telling dulls
                // them, the cruel one sharpens them - and a creep stays a creep
                float hearR = Sneaking ? 0f : (Prefs.Hard ? 4.6f : Prefs.Story ? 3.0f : 3.6f);
                float seeR = m.Asleep ? 0f : (Sneaking ? (Prefs.Hard ? 1.1f : 0.7f)
                                                          : (Prefs.Hard ? 4.0f : Prefs.Story ? 2.6f : 3.2f));
                // the silver ones are keener still: moonlight in the blood means a
                // moonlit wild thing senses the thief nearly a pace sooner
                if (m.Spec.Rare) { hearR += 0.8f; seeR += 0.8f; }
                // a chase runs at full field speed; the 0.55 gait is only for wandering -
                // without this every hunter chases at a stroll the hero can simply outwalk
                if (!m.Aggro && (dh < hearR || (dh < seeR && ClearLineOfSight(mpos, HeroPos))))
                {
                    m.Asleep = false; m.Aggro = true;
                    // the "!" holds a beat before the chase: a dodge window, shorter
                    // on the cruel telling when hesitation costs more
                    m.AggroT = Prefs.Hard ? 0.55f : 0.85f;
                    m.Speed = m.Spec.Speed; Sfx.Play("alert");
                }

                // a moonlit thing wears a star overhead: the night's prize should read
                // from across the field, not only once the fight has started
                if (m.Spec.Rare)
                {
                    if (m.Rare == null)
                    {
                        var rgo = new GameObject("rareMark");
                        rgo.transform.SetParent(m.Root, false);
                        rgo.transform.localScale = Vector3.one * 0.55f;
                        m.Rare = rgo.AddComponent<SpriteRenderer>();
                        m.Rare.sprite = TexArt.MenuIcon(21);
                        m.Rare.color = new Color(0.8f, 0.9f, 1f, 0.9f);
                        m.Rare.sortingOrder = 2100;
                    }
                    m.Rare.transform.localPosition = new Vector3(0f,
                        1.55f + Mathf.Sin(_time * 3f + m.HomeCell.x) * 0.07f, 0f);
                }

                if (m.Asleep)
                {
                    // still and breathing; the z drifts up-right off its shoulder,
                    // and a thief in earshot can hear the dozer before he sees it
                    if (m.Anim != null) m.Anim.Fps = 0f;
                    if (dh < 4.5f && _snoreT <= 0f)
                    {
                        _snoreT = 5f + Random.value * 3f;
                        Sfx.Play("snore");
                    }
                    if (m.SleepMark == null)
                    {
                        var zgo = new GameObject("sleepMark");
                        zgo.transform.SetParent(m.Root, false);
                        zgo.transform.localScale = Vector3.one * 0.6f;
                        m.SleepMark = zgo.AddComponent<SpriteRenderer>();
                        m.SleepMark.sprite = TexArt.SleepZ();
                        m.SleepMark.color = new Color(0.7f, 0.78f, 0.95f, 0.9f);
                        m.SleepMark.sortingOrder = 2100;
                    }
                    // a dozer drifting back off reuses its old mark - waking disabled it,
                    // the second sleep has to switch it back on or the z stays gone
                    m.SleepMark.enabled = true;
                    float zt = ((_time * 0.55f) + m.HomeCell.x * 0.37f) % 1f;
                    m.SleepMark.transform.localPosition = new Vector3(0.3f + zt * 0.28f, 0.9f + zt * 0.55f, 0f);
                    var zc = m.SleepMark.color; zc.a = 0.9f - zt * 0.75f; m.SleepMark.color = zc;
                    continue;
                }
                else if (m.SleepMark != null) m.SleepMark.enabled = false;

                if (m.Aggro && dh > (Sneaking ? 4.5f : 6.5f))
                {
                    m.Aggro = false; m.Speed = m.Spec.Speed * 0.55f;
                    // a dozer that chased a shadow counts the quiet seconds and drifts
                    // back off - the field settles rather than staying spooked forever
                    if (m.Sleeps) m.DozeT = 15f;
                }

                // the drift back to sleep: a born dozer that lost the trail counts its
                // quiet seconds down and, once calm, curls up where it stands
                if (!m.Aggro && !m.Asleep && m.Sleeps && m.DozeT > 0f)
                {
                    m.DozeT -= dt;
                    if (m.DozeT <= 0f) m.Asleep = true;
                }

                if (m.Aggro)
                {
                    if (m.Alert == null)
                    {
                        var ago = new GameObject("alert");
                        ago.transform.SetParent(m.Root, false);
                        ago.transform.localPosition = new Vector3(0f, 1.05f, 0f);
                        ago.transform.localScale = Vector3.one * 0.8f;
                        m.Alert = ago.AddComponent<SpriteRenderer>();
                        m.Alert.sprite = TexArt.Alert();
                        m.Alert.sortingOrder = 2100;
                    }
                    m.Alert.enabled = true;
                    if (m.Alert != null)
                        m.Alert.transform.localPosition = new Vector3(0f, 1.05f + Mathf.Sin(_time * 9f) * 0.08f, 0f);
                    if (m.AggroT > 0f)
                    {
                        m.AggroT -= dt;
                        if (m.Anim != null) m.Anim.Fps = 0f;
                        var wdir = DirVec.From(HeroPos - mpos);
                        Face(m, wdir, MonsterClip(m.Spec.MapSheet, wdir));
                        continue;
                    }
                    if (m.Anim != null) m.Anim.Fps = 6f;
                    var cdir = DirVec.From(HeroPos - mpos);
                    Face(m, cdir, MonsterClip(m.Spec.MapSheet, cdir));
                    var chase = (HeroPos - mpos).normalized;
                    var astep = chase * m.Speed * dt;
                    var anx = mpos + new Vector2(astep.x, 0f);
                    var any = mpos + new Vector2(0f, astep.y);
                    if (Map.Walkable(Map.CellOf(anx))) mpos.x = anx.x;
                    if (Map.Walkable(Map.CellOf(any))) mpos.y = any.y;
                    m.Root.localPosition = new Vector3(mpos.x, mpos.y, 0f);
                    if (m.Body != null) m.Body.localPosition = new Vector3(0f, StepBob(_time, 9f, m.HomeCell.x), 0f);
                    continue;
                }
                if (m.Alert != null) m.Alert.enabled = false;

                if (m.Pause > 0f)
                {
                    m.Pause -= dt;
                    if (m.Anim != null) m.Anim.Fps = 0f;
                    continue;
                }
                if (m.Dest == default || Vector2.Distance((Vector2)m.Root.localPosition, Map.CellCenter(m.Dest)) < 0.15f)
                {
                    // choose a new nearby walkable destination, biased home - and not one
                    // already claimed: a pair of beasts paused on the same tile reads as a
                    // single fat sprite until one of them lunges
                    for (int t = 0; t < 8; t++)
                    {
                        int dx = Random.Range(-3, 4), dy = Random.Range(-3, 4);
                        var cell = new Vector2Int(Mathf.RoundToInt(m.HomeCell.x) + dx, Mathf.RoundToInt(m.HomeCell.y) + dy);
                        if (!Map.Walkable(cell)) continue;
                        var center = Map.CellCenter(cell);
                        bool taken = false;
                        for (int j = 0; j < Monsters.Count; j++)
                        {
                            if (Monsters[j] == m || Monsters[j].Root == null) continue;
                            if (Vector2.Distance(Monsters[j].Root.localPosition, center) < 0.9f)
                            { taken = true; break; }
                        }
                        if (!taken) { m.Dest = cell; break; }
                    }
                    if (Random.value < 0.45f) { m.Pause = Random.Range(0.8f, 2.4f); continue; }
                }
                var target = Map.CellCenter(m.Dest);
                var pos = mpos;
                var delta = target - pos;
                var mdir = DirVec.From(delta);
                Face(m, mdir, MonsterClip(m.Spec.MapSheet, mdir));
                var step = delta.normalized * m.Speed * dt;
                var nx = pos + new Vector2(step.x, 0f);
                var ny = pos + new Vector2(0f, step.y);
                if (Map.Walkable(Map.CellOf(nx))) pos.x = nx.x;
                if (Map.Walkable(Map.CellOf(ny))) pos.y = ny.y;
                m.Root.localPosition = new Vector3(pos.x, pos.y, 0f);
                if (m.Anim != null) m.Anim.Fps = 5f;
                if (m.Body != null) m.Body.localPosition = new Vector3(0f, StepBob(_time, 7f, m.HomeCell.x), 0f);
            }
            }

            RefreshNamePlates();
        }

        /// <summary>Whether a monster can see the hero from where it stands: the straight line
        /// between them must not cross a solid cell. Distance alone made walls transparent -
        /// a hunter would sound the alert through a house wall and then grind against it. The
        /// chase itself is still allowed to round corners; sight only gates noticing.</summary>
        bool ClearLineOfSight(Vector2 from, Vector2 to)
        {
            float dist = Vector2.Distance(from, to);
            int steps = Mathf.Max(1, Mathf.CeilToInt(dist * 2f));
            for (int i = 1; i < steps; i++)
            {
                var p = Vector2.Lerp(from, to, i / (float)steps);
                if (!Map.Walkable(Map.CellOf(p))) return false;
            }
            return true;
        }
    }

    /// <summary>Tiny helpers so views can create renderers/labels without repeating boilerplate.</summary>
    public static class SpriteRendererUtil
    {
        static readonly Dictionary<Texture2D, Material> _mats = new Dictionary<Texture2D, Material>();

        public static Material SpriteMat(Texture2D tex)
        {
            if (_mats.TryGetValue(tex, out var m)) return m;
            var shader = Shader.Find("Sprites/Default");
            m = new Material(shader) { name = "MoonMeshMat" };
            if (tex != null) m.mainTexture = tex;
            _mats[tex] = m;
            return m;
        }

        public static SpriteRenderer Make(Transform parent, string name, Sprite sprite, int sorting)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = sorting;
            return sr;
        }
    }

    public static class PixelLabelUtil
    {
        public static PixelLabel Make(Transform parent, string name, int scale, Color color, TextAlign align, int sorting)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var label = go.AddComponent<PixelLabel>();
            label.Configure(scale, color, align, sorting);
            return label;
        }
    }
}
