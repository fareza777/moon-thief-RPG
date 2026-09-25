using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace MoonThief
{
    /// <summary>
    /// The measurement pass that keeps the layout honest. Once per frame it walks every live
    /// label, every UI plate and every character sprite, and writes four machine-readable
    /// families of lines in frame pixels (x grows right, y grows down, 288x512):

    ///   [RECT]  tag|name|order|x0|y0|x1|y1|text        a text block's measured box
    ///   [LABEL] tag|name|scale|px|py|maxw|align|order|text   the same block's anchor + wrap width
    ///   [PANEL] tag|kind|name|order|x0|y0|x1|y1|alpha  a UI plate (kind: card = framed panel art,
    ///                                                  plate = a solid HUD quad or icon)
    ///   [ACTOR] tag|name|order|x0|y0|x1|y1             a character sprite in the world or arena

    /// tools/layout_audit.py reads those back and reports the four defects a compiler cannot
    /// see and a screenshot cannot measure: text running off the frame, text cut off by a plate
    /// drawn over it, two plates overlapping without one nesting inside the other, and a name
    /// plate landing on a character. All four reached a build before this existed.

    /// It lives in Assets/Scripts - not the Editor folder - because the player self-test runs the
    /// same pass on the frames a phone would show.
    /// </summary>
    public static class LayoutAudit
    {
        /// <summary>UI plates start here: world art (ground, canopy, props, the night dimmer and
        /// the light pools) is below, so nothing in this range is scenery.</summary>
        const int PanelOrderMin = 2500;

        /// <summary>A character sprite is 0.12 to 10 units2: a villager is 1x1.25 and the biggest
        /// monster on the map 2.25x2.25, so anything past 10 is scenery (a dim quad, a sky, a menu
        /// plate) and anything under 0.12 is a shadow or a spark.</summary>
        const float ActorAreaMin = 0.12f, ActorAreaMax = 10f;

        static readonly List<(Rect box, int order, string desc)> _text = new List<(Rect, int, string)>();
        static readonly List<(Rect box, int order, string desc)> _props = new List<(Rect, int, string)>();
        static readonly List<Transform> _paths = new List<Transform>();

        public static void Report(string tag, float halfH, Vector3 cam)
        {
            var labels = Object.FindObjectsOfType<PixelLabel>(true);
            var panels = new List<Plate>();
            var covers = new List<Plate>();          // every opaque sprite, whatever layer it is on
            var actors = new List<(Rect box, int order, string desc)>();
            _props.Clear();

            foreach (var sr in Object.FindObjectsOfType<SpriteRenderer>(true))
            {
                if (sr == null || !sr.enabled || !sr.gameObject.activeInHierarchy || sr.sprite == null) continue;
                if (sr.color.a <= 0.02f) continue;
                var box = WorldRect(sr, cam, halfH);
                var desc = Name(sr.transform);
                // Every glyph of a label is its own sprite. They are the text, not furniture and not
                // the cast: measuring a few thousand of them per frame buried the plates that
                // matter, and a name plate landing on another label is already a CLASH.
                if (desc.Contains("/glyphs/")) continue;
                if (sr.color.a >= 0.8f && box.width >= 0.5f && box.height >= 0.5f)
                    covers.Add(new Plate { box = box, order = sr.sortingOrder, alpha = sr.color.a, desc = desc });
                if (box.width < 0.3f || box.height < 0.15f) continue;
                float area = box.width * box.height;

                // A rig body -- the walking cast, the critters and the battle rigs all end in
                // "/spr" -- is an actor wherever it stands, and so is the Pale Guard's map
                // sprite. Sorting order can no longer tell a character from a prop: every world
                // object, a tree included, sorts by the row it stands on (see WorldView.WorldOrder),
                // so the name has to decide. The old order band 40..1199 was what made this pass
                // report trees as characters the moment the two shared a band.
                bool rig = desc.EndsWith("/spr") || desc.Contains("bossMap");

                // "card" is the framed panel art every box in this game is drawn with, and every
                // one of them is a sliced sprite: a card takes over part of the screen, a plate is
                // a bar, a chip or an icon. Only cards are held to the no-overlap rule -- bars and
                // icons are meant to sit on top of each other (the moon icon rides the HUD bar).
                bool card = sr.drawMode == SpriteDrawMode.Sliced;
                bool plate = card || desc.Contains("/menus/") || desc.Contains("battleStage/")
                          || desc.Contains("hudRoot/") || sr.sortingOrder >= PanelOrderMin;
                if (!rig && plate)
                {
                    if (box.width < 0.3f || box.height < 0.25f) continue;
                    // A full-screen dim is not a plate. The cut is at 0.95 of the frame: a tall card
                    // (the journal page is 16.4 x 27.75) is still a card, a "black this out" quad is not.
                    if (area > 0.95f * 18f * 32f) continue;
                    if (sr.color.a < 0.5f) continue;
                    panels.Add(new Plate { box = box, order = sr.sortingOrder, alpha = sr.color.a, desc = desc, card = card });
                }
                else if (rig)
                {
                    if (area < ActorAreaMin || area > ActorAreaMax) continue;
                    var p = desc.ToLowerInvariant();
                    bool deco = p.EndsWith("/sh") || p.Contains("glow") || p.Contains("halo")
                             || p.Contains("spark") || p.Contains("dim") || p.Contains("sky")
                             || p.Contains("star") || p.Contains("light");
                    if (deco) continue;
                    actors.Add((box, sr.sortingOrder, desc));
                }
                // Scenery: props, trees, bushes, chests. Nothing here is text and nothing here is
                // the cast, but two crowns planted on the same tile is the defect a player reports
                // as "the trees are stacked" - and no amount of reading the code caught it, only
                // the pair of boxes side by side in a log does. tools/layout_audit.py groups these
                // by family and reports an overlap as PILE, and any overlap at all on a chest as
                // LOOT (a chest with a boulder growing out of it was the "clipped" chest).
                else if (!plate)
                {
                    if (area < 0.35f || area > 40f) continue;
                    var q = desc.ToLowerInvariant();
                    if (q.EndsWith("/sh") || q.Contains("glow") || q.Contains("halo")
                        || q.Contains("spark") || q.Contains("dim") || q.Contains("sky")
                        || q.Contains("star") || q.Contains("light")
                        || q.Contains("firefly") || q.Contains("/fly")) continue;
                    _props.Add((box, sr.sortingOrder, desc));
                }
            }

            _text.Clear();
            foreach (var l in labels)
            {
                if (l == null || !l.gameObject.activeInHierarchy) continue;
                var txt = l.Text;
                if (string.IsNullOrEmpty(txt) || txt.Trim().Length == 0) continue;
                // Measure uses the label's glyph Scale, but callers may also shrink the
                // whole label transform to fit a slot (battle menu cells do) - fold that
                // in or the box reads wider than the glyphs on screen
                float ts = Mathf.Abs(l.transform.localScale.x);
                if (ts < 0.001f) continue;
                float w = l.MeasureWidth(txt) * ts, h = l.MeasureHeight(txt) * ts;
                if (w <= 0.02f || h <= 0.02f) continue;
                var p = l.transform.position;
                float x0 = l.Align == TextAlign.Left ? p.x
                         : l.Align == TextAlign.Center ? p.x - w * 0.5f : p.x - w;
                var box = new Rect(x0, p.y - h, w, h);
                string desc = Name(l.transform) + " s" + l.Scale + " [" + Short(txt) + "]";

                // Text under an opaque plate is not visible text: it must not be counted as an
                // overlap and the decoder must not be asked to read it off the frame.
                if (Covered(box, l.SortingOrder, covers)) continue;
                _text.Add((box, l.SortingOrder, desc));

                int s0 = Px(x0, cam.x), s1 = Px(x0 + w, cam.x);
                int sy0 = Py(p.y - h, cam.y, halfH), sy1 = Py(p.y, cam.y, halfH);
                Debug.Log("[RECT] " + tag + "|" + Name(l.transform) + "|" + l.SortingOrder + "|"
                          + s0 + "|" + sy0 + "|" + s1 + "|" + sy1 + "|" + txt.Replace("\n", "\\n"));

                // The label's own anchor in frame pixels, for the offline decoder. It has to be the
                // anchor (not the left edge of the box above): the decoder reads its glyphs forward
                // from here and uses the align field to work out where the text really starts, so a
                // centred label handed its own left edge decodes shifted by half its width.
                int ax = Px(p.x, cam.x);
                int py = Py(p.y, cam.y, halfH);
                Debug.Log("[LABEL] " + tag + "|" + Name(l.transform) + "|" + l.Scale + "|" + ax + "|" + py
                          + "|" + l.MaxWidthUnits.ToString("0.##", CultureInfo.InvariantCulture)
                          + "|" + l.Align + "|" + l.SortingOrder
                          + "|" + txt.Replace("\n", "\\n"));
            }

            foreach (var p in panels)
                Debug.Log("[PANEL] " + tag + "|" + (p.card ? "card" : "plate") + "|" + p.desc + "|" + p.order + "|"
                          + Px(p.box.xMin, cam.x) + "|" + Py(p.box.yMax, cam.y, halfH) + "|"
                          + Px(p.box.xMax, cam.x) + "|" + Py(p.box.yMin, cam.y, halfH) + "|"
                          + p.alpha.ToString("0.##", CultureInfo.InvariantCulture));

            foreach (var a in actors)
                Debug.Log("[ACTOR] " + tag + "|" + a.desc + "|" + a.order + "|"
                          + Px(a.box.xMin, cam.x) + "|" + Py(a.box.yMax, cam.y, halfH) + "|"
                          + Px(a.box.xMax, cam.x) + "|" + Py(a.box.yMin, cam.y, halfH));

            // Scenery boxes, so the python pass can prove that no two crowns share a tile and
            // that nothing grows out of a chest. The world is deterministic, so this is a
            // regression test of the map generator as much as of the render.
            foreach (var sc in _props)
                Debug.Log("[PROP] " + tag + "|" + sc.desc + "|" + sc.order + "|"
                          + Px(sc.box.xMin, cam.x) + "|" + Py(sc.box.yMax, cam.y, halfH) + "|"
                          + Px(sc.box.xMax, cam.x) + "|" + Py(sc.box.yMin, cam.y, halfH));

            // In-engine count as well, so a build log shows the verdict without the python pass.
            int clashes = 0;
            for (int i = 0; i < _text.Count; i++)
                for (int j = i + 1; j < _text.Count; j++)
                {
                    if (!_text[i].box.Overlaps(_text[j].box)) continue;
                    clashes++;
                    Debug.Log("[OVERLAP] " + tag + " :: " + _text[i].desc + "  <>  " + _text[j].desc);
                }
            Debug.Log("[OVERLAP] " + tag + " labels=" + _text.Count + " clashes=" + clashes
                      + " panels=" + panels.Count + " actors=" + actors.Count);
        }

        static string Short(string s)
        {
            var t = s.Replace("\n", " ");
            return t.Length > 26 ? t.Substring(0, 26) + ".." : t;
        }

        struct Plate
        {
            public Rect box;
            public int order;
            public float alpha;
            public string desc;
            public bool card;
        }

        /// <summary>True when an opaque sprite drawn after this text completely covers it. This is
        /// what keeps a hidden string out of the offline decoder: the battle command menu under the
        /// result card is still "visible" to the scene graph and would be read off a frame that
        /// does not show it.</summary>
        static bool Covered(Rect r, int order, List<Plate> panels)
        {
            for (int i = 0; i < panels.Count; i++)
            {
                var p = panels[i];
                if (p.order <= order || p.alpha < 0.80f) continue;
                if (p.box.xMin <= r.xMin && p.box.xMax >= r.xMax && p.box.yMin <= r.yMin && p.box.yMax >= r.yMax)
                    return true;
            }
            return false;
        }

        /// <summary>World-space box of a renderer, honouring sliced draw mode and lossy scale.</summary>
        static Rect WorldRect(SpriteRenderer sr, Vector3 cam, float halfH)
        {
            var size = sr.drawMode == SpriteDrawMode.Simple ? (Vector2)sr.sprite.bounds.size : sr.size;
            var s = sr.transform.lossyScale;
            float w = size.x * Mathf.Abs(s.x), h = size.y * Mathf.Abs(s.y);
            var p = sr.transform.position;
            return new Rect(p.x - w * 0.5f, p.y - h * 0.5f, w, h);
        }

        static int Px(float world, float camX) => Mathf.RoundToInt((world - camX - G.Left) * G.PPU);

        static int Py(float world, float camY, float halfH) => Mathf.RoundToInt((camY + halfH - world) * G.PPU);

        /// <summary>"world/npcName1" - the last four names of the chain, which is enough to tell
        /// the hero's label from a villager's without printing a whole hierarchy.</summary>
        static string Name(Transform t)
        {
            _paths.Clear();
            while (t != null && _paths.Count < 4) { _paths.Insert(0, t); t = t.parent; }
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < _paths.Count; i++)
            {
                if (i > 0) sb.Append('/');
                sb.Append(_paths[i].name);
            }
            return sb.ToString();
        }
    }
}
