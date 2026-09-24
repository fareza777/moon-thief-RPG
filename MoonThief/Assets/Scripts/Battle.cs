using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MoonThief
{
    /// <summary>
    /// Battle presentation, rebuilt for the full game: measured 2x2 command menu, animated
    /// lunges/flash/shakes, floating numbers, capture sparkles, and the same measured result
    /// cards that survived the slice. Layout is anchored to the portrait frame, not hardcoded.
    /// </summary>
    public class BattleView : MonoBehaviour
    {
        public class Rig
        {
            public Fighter F;
            public Transform Root, Body, SpriteT;
            public SpriteRenderer Sr;
            public Anim Anim;
            public SpriteRenderer Shadow, BarBg, BarFill, NameChip, Hat;
            public PixelLabel Name;
            public Vector3 Home;
            public float BodyHeight, BobPhase;
            public float HpShown = -1f, BarLeft, BarTop, BarMaxW, BarH;
            public Color BarCol;
            public bool CapturedFx;
        }

        public class MenuCell
        {
            public string Label;
            public SpriteRenderer Panel, Chevron, Icon;
            public PixelLabel Text;
            public Rect Hit;
            public bool Off;      // greyed-out affordance: a command that cannot fire right now
        }

        // ---- layout
        public float HalfH = 16f;
        // MsgH is the log box. It is 3.9 rather than 3.3 so a two line message at scale 2 has a
        // clear 0.45 units of plate above and below it instead of 0.19: on a phone the old box
        // read as text glued to the top border, which is the one thing a message box may not do.
        public const float HudH = 2.4f, MenuH = 6.2f, MsgH = 3.9f;
        public float Left, Right, Top, Bottom, HudBottom, MenuTop, MsgTop, ArenaTop, PartyFeet;

        public Transform Stage;
        public Fighter[] Party;
        public Fighter[] Enemies = new Fighter[0];
        public Rig[] PartyRigs = new Rig[0];
        public Rig[] EnemyRigs = new Rig[0];
        public readonly List<MenuCell> Menu = new List<MenuCell>();

        SpriteRenderer _backdrop, _floorTint, _hudPanel, _menuPanel, _msgPanel, _moonIcon, _targetChev, _turnChev;
        SpriteRenderer _autoChip;
        PixelLabel _hudNight, _hudRound, _msg, _hint, _autoLabel;
        Transform _overlayRoot;
        SpriteRenderer _ovDim, _ovPanel;
        PixelLabel _ovTitle;
        List<PixelLabel> _ovLines = new List<PixelLabel>();
        List<(SpriteRenderer panel, PixelLabel text, Rect rect, Action act)> _ovButtons = new List<(SpriteRenderer, PixelLabel, Rect, Action)>();
        float _time;
        bool _menuOn;
        int _selCell;

        public bool MenuOn => _menuOn;
        public int SelectedCell => _selCell;
        public int OverlayButtonCount => _ovButtons.Count;

        // ---------------------------------------------------------------- build

        public void Build(float halfH, Transform parent)
        {
            HalfH = halfH;
            Stage = new GameObject("battleStage").transform;
            // parent to OUR transform, not the game stage: toggling this GameObject
            // (title/explore/ending) must hide every battle element - menu, message,
            // party bars, overlay. Parenting to the stage leaked the menu onto the title.
            Stage.SetParent(transform, false);
            Left = G.Left; Right = G.Right; Top = halfH; Bottom = -halfH;
            HudBottom = Top - HudH;
            MenuTop = Bottom + MenuH;
            MsgTop = MenuTop + MsgH;
            ArenaTop = HudBottom - 0.4f;
            // 1.35, not 1.1: the party name chip hangs 1.15 units under the feet, and at 1.1 the
            // bottom of every chip sat one pixel off the top border of the log box
            PartyFeet = MenuTop + MsgH + 1.35f;

            _floorTint = SpriteRendererUtil.Make(Stage, "bfloor", TexArt.Solid(), -2);
            _floorTint.transform.localPosition = new Vector3(0f, (Top + Bottom) * 0.5f, 0f);
            _floorTint.transform.localScale = new Vector3(18f * 16f, (Top - Bottom) * 16f, 1f);
            _floorTint.color = new Color32(17, 14, 30, 255);

            _backdrop = SpriteRendererUtil.Make(Stage, "bbackdrop", null, -1);
            _backdrop.transform.localPosition = new Vector3(0f, HudBottom - 7.5f, 0f);

            _hudPanel = Sliced("hud", TexArt.Panel(), 46);
            Box(_hudPanel, Left, HudBottom, 18f, HudH, Color.white);

            _hudNight = Label("hudNight", 2, new Color(1f, 0.93f, 0.72f), TextAlign.Left, 50);
            _hudNight.transform.localPosition = new Vector3(Left + 0.45f, Top - 0.42f, 0f);

            _hudRound = Label("hudRound", 2, new Color(0.75f, 0.73f, 0.88f), TextAlign.Right, 50);
            // ends clear of the AUTO chip's left edge (the chip spans Right-4.5 .. Right-0.9):
            // the chip draws above text, so a round counter reaching into it was half-covered
            _hudRound.transform.localPosition = new Vector3(Right - 4.85f, Top - 0.42f, 0f);

            _moonIcon = SpriteRendererUtil.Make(Stage, "bmoon", Game.State.Chapter >= 3 ? TexArt.MoonFull() : TexArt.MoonEmpty(), 50);
            _moonIcon.transform.localPosition = new Vector3(Right - 0.9f, Top - 0.85f, 0f);
            _moonIcon.transform.localScale = Vector3.one * 2f;

            _msgPanel = Sliced("msgPanel", TexArt.Panel(), 51);
            Box(_msgPanel, Left + 0.3f, MenuTop + 0.12f, 17.4f, MsgH - 0.24f, new Color(1f, 1f, 1f, 0.9f));

            // the log box is taller than a single line on purpose: two lines are the common case
            // ("CAP CAP looks at you...") and three lines have to fit too, because SetMessage
            // scales the text down rather than letting it run over the border
            _msg = Label("bmsg", 2, new Color(1f, 0.96f, 0.82f), TextAlign.Left, 53);
            _msg.MaxWidthUnits = 15.2f;
            _msg.RevealSpeed = Prefs.RevealSpeed;
            _msg.transform.localPosition = new Vector3(Left + 0.75f, MenuTop + MsgH - 0.48f, 0f);

            // Control hint. It lives in the HUD bar, on its own line under NIGHT, instead of
            // inside the message box: there it sat on the same line as the battle message and
            // the two printed over each other whenever the message wrapped to two lines.
            _hint = Label("bhint", 1, new Color(0.76f, 0.82f, 1f), TextAlign.Left, 50);
            _hint.transform.localPosition = new Vector3(Left + 0.45f, Top - 1.62f, 0f);
            _hint.Set(Strings.Get("bt.hint"));
            _hint.gameObject.SetActive(false);

            // AUTO chip: a standing toggle at the right end of the HUD bar. The 2x2 command
            // grid has no room for a fifth cell, so auto-battle lives up here instead - one
            // tap and the party fights itself until tapped again.
            _autoChip = Sliced("bauto", TexArt.Panel(), 55);
            Box(_autoChip, Right - 4.5f, Top - 2.28f, 3.6f, 1.5f, new Color(1f, 1f, 1f, 0.5f));
            _autoLabel = Label("bautoLabel", 1, new Color(0.8f, 0.83f, 1f), TextAlign.Center, 56);
            _autoLabel.transform.localPosition = new Vector3(Right - 2.7f, Top - 2.13f, 0f);
            _autoLabel.Set("AUTO");

            _menuPanel = Sliced("bmenu", TexArt.Panel(), 54);
            Box(_menuPanel, Left + 0.3f, Bottom + 0.3f, 17.4f, MenuH - 0.5f, Color.white); // y Bottom+0.3 .. Bottom+5.9

            BuildMenu();
            BuildParty();

            _targetChev = SpriteRendererUtil.Make(Stage, "bchev", TexArt.Chevron(), 36);
            _targetChev.transform.localEulerAngles = new Vector3(0f, 0f, -90f);
            _targetChev.enabled = false;
            _turnChev = SpriteRendererUtil.Make(Stage, "bturn", TexArt.Chevron(), 36);
            _turnChev.transform.localEulerAngles = new Vector3(0f, 0f, 180f);
            _turnChev.color = new Color(1f, 0.85f, 0.4f);
            _turnChev.enabled = false;

            _overlayRoot = new GameObject("boverlay").transform;
            _overlayRoot.SetParent(Stage, false);
            _overlayRoot.gameObject.SetActive(false);

            Refresh();
        }

        void BuildMenu()
        {
            // two rows must fit INSIDE the menu panel (Bottom+0.3 .. Bottom+5.9):
            // row 1 spans Bottom+3.3..5.6, row 2 spans Bottom+0.5..2.8.
            string[] keys = { "menu.attack", "menu.befriend", "menu.morsel", "menu.run" };
            const float cellW = 8.1f, cellH = 2.3f;
            float x0 = Left + 0.75f, yTop = MenuTop - 0.6f;
            for (int i = 0; i < 4; i++)
            {
                int col = i % 2, row = i / 2;
                float x = x0 + col * (cellW + 0.55f);
                float y = yTop - row * (cellH + 0.5f);
                var cell = new MenuCell { Label = Strings.Get(keys[i]) };
                cell.Panel = Sliced("mcell" + i, TexArt.Panel(), 55);
                Box(cell.Panel, x, y - cellH, cellW, cellH, Color.white);
                cell.Chevron = SpriteRendererUtil.Make(Stage, "mchev" + i, TexArt.Chevron(), 60);
                cell.Chevron.transform.localPosition = new Vector3(x + 0.32f, y - cellH * 0.5f, 0f);
                cell.Chevron.transform.localScale = Vector3.one * 1.4f;
                cell.Chevron.enabled = false;
                cell.Icon = SpriteRendererUtil.Make(Stage, "micon" + i, TexArt.MenuIcon(i), 59);
                cell.Icon.transform.localPosition = new Vector3(x + 0.95f, y - cellH * 0.5f, 0f);
                cell.Icon.transform.localScale = Vector3.one * 1.5f;
                cell.Text = Label("mlabel" + i, 2, Color.white, TextAlign.Left, 58);
                cell.Text.transform.localPosition = new Vector3(x + 1.85f, y - (cellH - PixelFont.GlyphHUnits(2)) * 0.5f, 0f);
                cell.Text.Set(cell.Label);
                // the painted cell, plus a thin margin. HitMenu() also accepts a near miss
                // (nearest centre) so a thumb does not have to be pixel accurate
                cell.Hit = new Rect(x - 0.3f, y - cellH - 0.3f, cellW + 0.6f, cellH + 0.6f);
                Menu.Add(cell);
            }
        }

        /// Footer spots for a party of any size: three heroes spread wide, more squeeze in.
        static float[] PartyXs(int n)
        {
            if (n <= 3) return new[] { -4.2f, 0f, 4.2f };
            var xs = new float[n];
            for (int i = 0; i < n; i++) xs[i] = Mathf.Lerp(-5.6f, 5.6f, i / (n - 1f));
            return xs;
        }

        void BuildParty()
        {
            var specs = BattleData.Party;
            // befriended beasts stand behind the heroes, up to the two-heart cap
            var friends = new List<MonsterSpec>();
            foreach (var key in Game.State.Friends)
            {
                var s = BattleData.Species(key);
                if (s.HasValue) friends.Add(s.Value);
            }

            Party = new Fighter[specs.Length + friends.Count];
            PartyRigs = new Rig[Party.Length];
            float[] xs = PartyXs(Party.Length);
            for (int i = 0; i < Party.Length; i++)
            {
                bool friend = i >= specs.Length;
                if (!friend)
                {
                    var spec = specs[i];
                    // levels and worn gear both count, otherwise the journal pages are a museum:
                    // a level adds health and edge (HeroStats), a blade attack, cloth hp, a charm both
                    BattleData.HeroStats(spec, Game.State.Level, out int hp, out int aMin, out int aMax);
                    Party[i] = new Fighter
                    {
                        Id = "p" + i,
                        Name = Strings.Get(spec.NameKey),
                        Side = Side.Party,
                        MaxHp = hp + Game.State.BonusHp,
                        Hp = hp + Game.State.BonusHp,
                        AtkMin = aMin + Game.State.BonusAtk,
                        AtkMax = aMax + Game.State.BonusAtk,
                        Speed = spec.Speed,
                        ColorDir = spec.ColorDir,
                        Look = spec.Look,
                        Style = spec.Style,
                        BattlerPath = BattleData.ClipPath(spec.ColorDir, "breath_idle"),
                        Scale = 2
                    };
                }
                else
                {
                    var ms = friends[i - specs.Length];
                    int flv = (Game.State.Level - 1) * 2;
                    Party[i] = new Fighter
                    {
                        Id = "p" + i,
                        Name = Strings.Get(ms.Name),
                        Side = Side.Party,
                        Species = ms.Name,
                        MaxHp = ms.Hp + flv,
                        Hp = ms.Hp + flv,
                        AtkMin = ms.AtkMin,
                        AtkMax = ms.AtkMax,
                        Speed = ms.Speed,
                        Style = 0,
                        BattlerPath = ms.Battler,
                        Scale = FitScale(Bank.One(ms.Battler), 3.2f, 2)
                    };
                }
                var home = new Vector3(xs[i], PartyFeet, 0f);
                var rig = MakeRig(Party[i], home, 20 + i);
                // The party footer is a tight stack between the fighters' feet and the message
                // panel: HP bar first, then the name plate. The bar used to sit at
                // PartyFeet - 1.55, which is under the message panel, so nobody ever saw their
                // own HP in battle.
                rig.Name = Label("pname" + i, 1, new Color(0.92f, 0.94f, 1f), TextAlign.Center, 24);
                rig.Name.transform.localPosition = new Vector3(home.x, PartyFeet - 0.53f, 0f);
                rig.Name.Set(Party[i].Name);
                rig.NameChip = SpriteRendererUtil.Make(Stage, "pnameChip" + i, TexArt.Solid(), 23);
                Plate(rig.NameChip, rig.Name, Party[i].Name, false, Party[i].Species != null);
                rig.BarBg = SpriteRendererUtil.Make(Stage, "pbg" + i, TexArt.Solid(), 22);
                rig.BarFill = SpriteRendererUtil.Make(Stage, "pfill" + i, TexArt.Solid(), 23);
                rig.Anim.Play(friend
                    ? new[] { Bank.One(Party[i].BattlerPath) }
                    : Bank.Frames(BattleData.ClipPath(specs[i].ColorDir, "breath_idle")), 6f, true);
                // the turn chevron floats BodyHeight above the feet - enemy rigs set it from
                // their sprite bounds, party rigs forgot to and the arrow sank into the sprite
                rig.BodyHeight = rig.Sr.sprite != null ? rig.Sr.sprite.bounds.size.y * Party[i].Scale : 2f;
                PartyRigs[i] = rig;
            }
        }

        /// <summary>The line-up is rebuilt at every encounter: worn gear and levels shift
        /// the stats the heroes were created with, and befriended beasts join mid-run.
        /// ResetPartyHp runs right after and tops everyone off, so wounds do not carry.</summary>
        public void RebuildParty()
        {
            if (PartyRigs != null)
                foreach (var r in PartyRigs) DisposeRig(r);
            BuildParty();
        }

        void DisposeRig(Rig rig)
        {
            if (rig == null) return;
            if (rig.Root != null) UtilDestroy(rig.Root.gameObject);
            if (rig.Name != null) UtilDestroy(rig.Name.gameObject);
            if (rig.NameChip != null) UtilDestroy(rig.NameChip.gameObject);
            if (rig.BarBg != null) UtilDestroy(rig.BarBg.gameObject);
            if (rig.BarFill != null) UtilDestroy(rig.BarFill.gameObject);
        }

        Rig MakeRig(Fighter f, Vector3 home, int sorting)
        {
            var rig = new Rig { F = f, Home = home };
            var root = new GameObject(f.Id).transform;
            root.SetParent(Stage, false);
            root.localPosition = home;
            rig.Root = root;
            rig.BobPhase = UnityEngine.Random.Range(0f, 6.28f);

            rig.Shadow = SpriteRendererUtil.Make(root, "sh", TexArt.Shadow(), sorting - 1);
            rig.Shadow.transform.localPosition = new Vector3(0f, 0.12f, 0f);
            rig.Shadow.transform.localScale = new Vector3(1.2f, 1f, 1f);

            var body = new GameObject("body").transform;
            body.SetParent(root, false);
            rig.Body = body;

            var go = new GameObject("spr");
            go.transform.SetParent(body, false);
            go.transform.localScale = Vector3.one * f.Scale;
            rig.SpriteT = go.transform;
            rig.Sr = go.AddComponent<SpriteRenderer>();
            rig.Sr.sortingOrder = sorting;
            rig.Anim = go.AddComponent<Anim>();
            rig.Anim.Setup(rig.Sr, true, 0f);
            rig.Anim.AnchorScale = f.Scale;

            // The three friends are one sprite in three palettes, so each one wears something of
            // their own. It hangs off the sprite transform, which already carries the rig scale,
            // and it is anchored to the head from the bottom (the sprite is feet-anchored, so the
            // head sits a fixed distance above the ground, not a fixed distance from the top).
            if (f.Look >= 0)
            {
                var hat = SpriteRendererUtil.Make(go.transform, "headwear", TexArt.Headwear(f.Look), sorting + 1);
                hat.transform.localPosition = new Vector3(0f, 0.46f, 0f);
                rig.Hat = hat;
            }
            return rig;
        }

        static int FitScale(Sprite s, float maxW, int pref)
        {
            if (s == null) return 1;
            for (int k = pref; k >= 1; k--)
                if (s.bounds.size.x * k <= maxW) return k;
            return 1;
        }

        public void SetBackdrop(string path)
        {
            var sp = Bank.One(path);
            _backdrop.sprite = sp;
            if (sp != null)
            {
                // cover the WHOLE portrait frame (was: only the arena) so no flat band
                // is left above the art
                float kx = 18f / sp.bounds.size.x;
                float ky = (Top - Bottom) / sp.bounds.size.y;
                float k = Mathf.Max(kx, ky);
                _backdrop.transform.localScale = Vector3.one * k;
                _backdrop.transform.localPosition = new Vector3(0f, (Top + Bottom) * 0.5f, 0f);
            }
        }

        public void SetMoonIcon(bool full)
        {
            _moonIcon.sprite = full ? TexArt.MoonFull() : TexArt.MoonEmpty();
        }

        public void SetEncounter(MonsterSpec[] specs)
        {
            for (int i = 0; i < EnemyRigs.Length; i++)
            {
                if (EnemyRigs[i] == null) continue;
                if (EnemyRigs[i].Root != null) UtilDestroy(EnemyRigs[i].Root.gameObject);
                // bars and the name label are made directly under Stage - destroy them
                // explicitly or they orphan and pile up over the arena every encounter
                if (EnemyRigs[i].BarBg != null) UtilDestroy(EnemyRigs[i].BarBg.gameObject);
                if (EnemyRigs[i].BarFill != null) UtilDestroy(EnemyRigs[i].BarFill.gameObject);
                if (EnemyRigs[i].Name != null) UtilDestroy(EnemyRigs[i].Name.gameObject);
                if (EnemyRigs[i].NameChip != null) UtilDestroy(EnemyRigs[i].NameChip.gameObject);
            }

            Enemies = new Fighter[specs.Length];
            EnemyRigs = new Rig[specs.Length];
            for (int i = 0; i < specs.Length; i++)
            {
                var spec = specs[i];
                var battler = Bank.One(spec.Battler);
                // wild things get a little tougher as the party levels, so levelling
                // shortens a fight instead of making it meaningless
                int elv = (Game.State.Level - 1) * 2;
                var f = new Fighter
                {
                    Id = "e" + i,
                    Name = Strings.Get(spec.Name),
                    Side = Side.Enemy,
                    MaxHp = spec.Hp + elv, Hp = spec.Hp + elv,
                    AtkMin = spec.AtkMin, AtkMax = spec.AtkMax,
                    Speed = spec.Speed,
                    Boss = spec.Boss,
                    Species = spec.Name,
                    BattlerPath = spec.Battler,
                    Scale = FitScale(battler, spec.Boss ? 8.5f : 5.6f, 2)
                };
                Enemies[i] = f;
                float x = specs.Length == 1 ? 0f : (i == 0 ? -4.4f : 4.4f);
                float y = HudBottom - (spec.Boss ? 8.6f : 6.2f);
                var home = new Vector3(x, y, 0f);
                var rig = MakeRig(f, home, 10 + i);
                rig.Shadow.transform.localScale = new Vector3(Mathf.Max(1f, f.Scale * 0.8f), 1f, 1f);
                rig.Anim.Play(new[] { battler }, 1f, true);
                rig.Sr.enabled = battler != null;
                rig.BodyHeight = battler != null ? battler.bounds.size.y * f.Scale : 2f;

                rig.BarBg = SpriteRendererUtil.Make(Stage, "ebg" + i, TexArt.Solid(), 6);
                rig.BarFill = SpriteRendererUtil.Make(Stage, "efill" + i, TexArt.Solid(), 7);
                rig.Name = Label("ename" + i, 1, f.Boss ? new Color(1f, 0.6f, 0.52f) : new Color(1f, 0.86f, 0.86f), TextAlign.Center, 8);
                // a floating/tall foe's name would sit inside the HUD strip -- but lowering it
                // onto the sprite leaves the body covering the label, so it moves under the foe's
                // HP bar (the bar sits at home.y-0.55 .. -0.35) instead
                float aboveHead = home.y + rig.BodyHeight + 0.45f;
                float nameTop = aboveHead <= HudBottom - 0.2f ? aboveHead : home.y - 0.95f;
                rig.Name.transform.localPosition = new Vector3(home.x, nameTop, 0f);
                rig.Name.Set(f.Name);
                // dark plate behind the name: the arena art has flat bright patches and light
                // text lying straight on top of them read as a smear
                rig.NameChip = SpriteRendererUtil.Make(Stage, "enameChip" + i, TexArt.Solid(), 7);
                Plate(rig.NameChip, rig.Name, f.Name, f.Boss);
                EnemyRigs[i] = rig;
            }
            SetTarget(0);
            Refresh();
        }

        public void ResetPartyHp()
        {
            foreach (var rig in PartyRigs)
            {
                rig.F.Hp = rig.F.MaxHp;
                rig.F.Dead = false;
                rig.Root.gameObject.SetActive(true);
                rig.Root.localPosition = rig.Home;
                rig.Body.localPosition = Vector3.zero;
                rig.Sr.enabled = true;
                rig.Anim.SetTint(Color.white);
                rig.Anim.Play(rig.F.Species != null
                    ? new[] { Bank.One(rig.F.BattlerPath) }
                    : Bank.Frames(BattleData.ClipPath(rig.F.ColorDir, "breath_idle")), 6f, true);
            }
            Refresh();
        }

        /// <summary>Intro flourish: party and foes slide in from the frame edges
        /// (play mode only; the editor preview keeps the static layout).</summary>
        public void IntroSlide()
        {
            if (!Application.isPlaying) return;
            foreach (var rig in PartyRigs)
            {
                rig.Root.localPosition = rig.Home + new Vector3(-2.5f, 0f, 0f);
                StartCoroutine(Fx.MoveLocal(rig.Root, rig.Home, 0.35f));
            }
            for (int i = 0; i < EnemyRigs.Length; i++)
            {
                var off = new Vector3(i == 0 ? 4f : -4f, 0.8f, 0f);
                EnemyRigs[i].Root.localPosition = EnemyRigs[i].Home + off;
                StartCoroutine(Fx.MoveLocal(EnemyRigs[i].Root, EnemyRigs[i].Home, 0.4f));
            }
        }

        /// <summary>Golden sparkle shower over the result card panel. Sparks start just above
        /// the card's top edge and drift down over its face; spawning at the top of the frame
        /// left them twinkling across the HUD strip, a full card-height above the card.</summary>
        public void CardSparkle()
        {
            if (!Application.isPlaying) return;
            float cardTop = _ovPanel != null
                ? _ovPanel.transform.localPosition.y + _ovPanel.size.y * 0.5f
                : Top - 2f;
            for (int i = 0; i < 12; i++)
            {
                var go = new GameObject("cardspark");
                go.transform.SetParent(Stage, false);
                go.transform.localPosition = new Vector3(UnityEngine.Random.Range(Left + 1.5f, Right - 1.5f), cardTop + 0.5f, 0f);
                go.transform.localScale = Vector3.one * 2f;
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = TexArt.Spark();
                sr.color = new Color(1f, 0.95f, 0.7f, 1f);
                sr.sortingOrder = 90;
                StartCoroutine(CardSparkRoutine(go.transform, UnityEngine.Random.Range(0.9f, 1.6f)));
            }
        }

        IEnumerator CardSparkRoutine(Transform t, float dur)
        {
            var start = t.localPosition;
            float e = 0f;
            while (e < dur)
            {
                e += Time.deltaTime;
                float k = e / dur;
                t.localPosition = start + new Vector3(Mathf.Sin(k * 9f) * 0.3f, -2.5f * k, 0f);
                var c = t.GetComponent<SpriteRenderer>().color;
                c.a = 1f - k;
                t.GetComponent<SpriteRenderer>().color = c;
                yield return null;
            }
            UtilDestroy(t.gameObject);
        }

        public void SetNight(int chapter)
        {
            _hudNight.Set(Strings.Get("hud.nightshort", chapter));
        }

        public void SetRound(int round) => _hudRound.Set(Strings.Get("hud.round", round));

        public void SetMessage(string text)
        {
            // the text-speed setting applies mid-game too, so re-read it each message
            _msg.RevealSpeed = Prefs.RevealSpeed;
            // One panel, and the text picks the largest scale that fits inside it: two lines at
            // scale 2, three at scale 1. The old version always drew at scale 2 with the anchor a
            // flat 0.48 units under the plate's top edge, so a wrapped message sat glued to the
            // border above it -- which is exactly what a two line log looked like on a phone.
            float plateTop = MenuTop + MsgH - 0.12f;        // matches the Box() in Build()
            float inner = MsgH - 0.24f - 0.125f;            // inside the 1 px frame of the plate
            // The plate is drawn from its own measured text, so the padding has a floor: a single
            // short line ("AMBER - your move.") lands dead centre of a 3.9 unit box, not at the top.
            _msg.Scale = 2;
            _msg.Set(text);
            if (_msg.MeasureHeight(text) > inner + 0.02f)
            {
                _msg.Scale = 1;
                _msg.Set(text);
            }
            float th = _msg.MeasureHeight(text);
            float pad = Mathf.Max(0.45f, (inner - th) * 0.5f);
            if (pad * 2f + th > inner) pad = Mathf.Max(0.18f, (inner - th) * 0.5f);
            _msg.transform.localPosition = new Vector3(Left + 0.8f, Fx.Snap(plateTop - 0.125f - pad), 0f);
        }

        public void ShowHint(bool on)
        {
            if (_hint != null) _hint.gameObject.SetActive(on);
        }

        public void SetMenuVisible(bool on)
        {
            _menuOn = on;
            if (!on) ShowHint(false);
            _menuPanel.enabled = on;
            foreach (var c in Menu)
            {
                c.Panel.enabled = on;
                if (c.Icon != null) c.Icon.enabled = on;
                c.Text.gameObject.SetActive(on);
            }
            if (on) SetSelected(_selCell);
        }

        /// <summary>Marks a command usable or not: text + icon fade, and every later repaint
        /// keeps the grey - the dim survives cursor moves instead of being overwritten.</summary>
        public void SetCellEnabled(int i, bool on)
        {
            if (i < 0 || i >= Menu.Count) return;
            Menu[i].Off = !on;
            if (Menu[i].Icon != null)
                Menu[i].Icon.color = on ? Color.white : new Color(1f, 1f, 1f, 0.35f);
            SetSelected(_selCell);
        }

        public void SetSelected(int cell)
        {
            _selCell = Mathf.Clamp(cell, 0, Menu.Count - 1);
            for (int i = 0; i < Menu.Count; i++)
            {
                bool sel = i == _selCell && _menuOn;
                Menu[i].Chevron.enabled = sel;
                Menu[i].Text.SetColor(Menu[i].Off ? new Color(1f, 1f, 1f, 0.35f)
                    : sel ? new Color(1f, 0.95f, 0.7f) : new Color(0.85f, 0.86f, 0.96f));
                Menu[i].Panel.color = sel ? new Color(1f, 1f, 0.92f, 1f) : new Color(1f, 1f, 1f, 0.62f);
            }
        }

        int _target = -1;
        public int Target => _target;

        public void SetTarget(int i)
        {
            _target = i;
            bool ok = i >= 0 && i < EnemyRigs.Length && Enemies[i].Alive;
            _targetChev.enabled = ok;
            if (!ok) return;
            _targetChev.transform.localScale = Vector3.one * (Enemies[i].Boss ? 2.4f : 1.8f);
            // a cyan aim chevron means the acting friend's style cuts this foe's seam -
            // the weakness table becomes a thing you can aim with, not just a journal footnote
            _targetChev.color = WeakFor(AimStyle, Enemies[i])
                ? new Color(0.65f, 1f, 0.95f) : Color.white;
            PlaceTargetChev(EnemyRigs[i], 0f);
        }

        /// <summary>The style the acting hero fights with, set when the command menu opens.
        /// -1 when no weakness hint applies (enemy turns, cards).</summary>
        public int AimStyle = -1;

        static bool WeakFor(int style, Fighter f)
        {
            if (style < 0 || f?.Species == null) return false;
            var s = BattleData.Species(f.Species);
            return s.HasValue && BattleData.StyleBeats(style, s.Value);
        }

        /// <summary>The aim arrow sits beside the foe's feet, pointing at it. It used to sit 0.7
        /// units over its head, which is exactly where the name plate is: every encounter printed
        /// the arrow through the name of the foe it was aiming at.</summary>
        void PlaceTargetChev(Rig rig, float bob)
        {
            _targetChev.transform.localPosition = new Vector3(
                rig.Home.x - BodyWidth(rig) * 0.5f - 0.75f, rig.Home.y + 0.25f + bob, 0f);
        }

        Rig _turnRig;

        /// <summary>Marks whose move it is - a little gold chevron bobbing over their head.</summary>
        public void SetTurnRig(Rig rig)
        {
            _turnRig = rig;
            _turnChev.enabled = rig != null;
        }

        /// <summary>Draws a rig's hp fill at its displayed width, which may lag the real hp.</summary>
        void DrawFill(Rig rig)
        {
            if (rig?.BarFill == null || rig.F == null) return;
            float w = rig.BarMaxW * Mathf.Clamp01(rig.HpShown);
            rig.BarFill.enabled = rig.F.Alive && w > 0.03f;
            if (rig.BarFill.enabled)
                Box(rig.BarFill, rig.BarLeft + 0.0625f, rig.BarTop, w - 0.0625f, rig.BarH, rig.BarCol);
        }

        void TickBar(Rig rig)
        {
            if (rig == null || rig.HpShown < 0f) return;
            if (Mathf.Abs(rig.F.Hp01 - rig.HpShown) < 0.0015f)
            {
                if (rig.HpShown != rig.F.Hp01) { rig.HpShown = rig.F.Hp01; DrawFill(rig); }
                return;
            }
            rig.HpShown = Mathf.MoveTowards(rig.HpShown, rig.F.Hp01, Time.deltaTime * 0.9f);
            DrawFill(rig);
        }

        /// <summary>Rendered width of a rig, for anything that has to stand beside it.</summary>
        static float BodyWidth(Rig rig)
        {
            if (rig == null || rig.Sr == null || rig.Sr.sprite == null) return 2f;
            return rig.Sr.sprite.bounds.size.x * rig.F.Scale;
        }

        void OnDisable()
        {
            // coroutines freeze while the view is off between battles and resume mid-flight in
            // the next one -- last fight's sparks and damage numbers popping into a fresh arena.
            // Kill the routines and sweep the FX nodes they would have cleaned up themselves.
            StopAllCoroutines();
            if (Stage == null) return;
            for (int i = Stage.childCount - 1; i >= 0; i--)
            {
                var n = Stage.GetChild(i).name;
                if (n == "spark" || n == "cardspark" || n == "floatn") UtilDestroy(Stage.GetChild(i).gameObject);
            }
        }

        void Update()
        {
            _time += Time.deltaTime;
            // foes bob on the spot; the party breathes, so the arena is never a still frame
            foreach (var rig in EnemyRigs)
            {
                if (rig?.Body == null || !rig.F.Alive) continue;
                rig.Body.localPosition = new Vector3(0f, Mathf.Sin(_time * 2.1f + rig.BobPhase) * 0.06f, 0f);
            }
            foreach (var rig in PartyRigs)
            {
                if (rig?.Body == null || !rig.F.Alive) continue;
                rig.Body.localPosition = new Vector3(0f, Mathf.Sin(_time * 1.5f + rig.BobPhase) * 0.035f, 0f);
            }
            if (_targetChev.enabled && _target >= 0 && _target < EnemyRigs.Length)
                PlaceTargetChev(EnemyRigs[_target], Mathf.Sin(_time * 5f) * 0.08f);
            if (_turnChev.enabled && _turnRig != null)
            {
                var tr = _turnRig;
                _turnChev.transform.localPosition = new Vector3(tr.Home.x,
                    tr.Home.y + tr.BodyHeight + 0.5f + Mathf.Sin(_time * 5f) * 0.07f, 0f);
                _turnChev.transform.localScale = Vector3.one * 1.6f;
            }
            // hp bars bleed toward the real value instead of snapping
            foreach (var rig in PartyRigs) TickBar(rig);
            foreach (var rig in EnemyRigs) TickBar(rig);
        }

        public void Refresh()
        {
            var xs = PartyXs(PartyRigs.Length);
            for (int i = 0; i < PartyRigs.Length; i++)
            {
                var rig = PartyRigs[i];
                float left = xs[i] - 1.1f;
                bool alive = rig.F.Alive;
                rig.BarBg.enabled = alive;
                Box(rig.BarBg, left, PartyFeet - 0.44f, 2.2f, 0.28f, new Color32(12, 10, 22, 255));
                var c = rig.F.Hp01 > 0.5f ? new Color32(126, 226, 143, 255)
                    : rig.F.Hp01 > 0.22f ? new Color32(240, 208, 110, 255) : new Color32(232, 106, 106, 255);
                rig.BarLeft = left; rig.BarTop = PartyFeet - 0.415f; rig.BarMaxW = 2.2f; rig.BarH = 0.17f;
                rig.BarCol = c;
                if (rig.HpShown < 0f) rig.HpShown = rig.F.Hp01;
                DrawFill(rig);
                rig.Name.SetColor(alive ? new Color(0.92f, 0.94f, 1f) : new Color(0.5f, 0.46f, 0.56f));
                if (rig.NameChip != null) Plate(rig.NameChip, rig.Name, rig.F.Name, rig.F.Boss, rig.F.Species != null);
            }
            for (int i = 0; i < EnemyRigs.Length; i++)
            {
                var rig = EnemyRigs[i];
                if (rig == null) continue;
                bool show = rig.F.Alive;
                rig.BarBg.enabled = show;
                rig.BarFill.enabled = false;
                if (show)
                {
                    float left = rig.Home.x - 1.3f;
                    Box(rig.BarBg, left, rig.Home.y - 0.62f, 2.6f, 0.28f, new Color32(12, 10, 22, 255));
                    rig.BarLeft = left; rig.BarTop = rig.Home.y - 0.56f; rig.BarMaxW = 2.6f; rig.BarH = 0.16f;
                    rig.BarCol = rig.F.Boss ? new Color32(255, 150, 110, 255) : new Color32(232, 196, 120, 255);
                    if (rig.HpShown < 0f) rig.HpShown = rig.F.Hp01;
                    DrawFill(rig);
                }
            }
        }

        // ---------------------------------------------------------------- overlay cards

        /// <summary>Shows or hides the party footer (HP bar + name tag under each hero).
        /// A result card is opaque and its buttons land exactly where the footer sits, so the
        /// names showed through under the CONTINUE button - text on text, the one thing a player
        /// reads as a bug. The footer belongs to the fight, and the fight is over while a card
        /// is up.</summary>
        void SetFooterVisible(bool on)
        {
            for (int i = 0; i < PartyRigs.Length; i++)
            {
                var r = PartyRigs[i];
                if (r == null) continue;
                if (r.Name != null) r.Name.gameObject.SetActive(on);
                if (r.NameChip != null) r.NameChip.enabled = on;
                if (r.BarBg != null) r.BarBg.enabled = on;
                if (r.BarFill != null) r.BarFill.enabled = on;
            }
        }

        public void ShowCard(string title, string[] lines, string[] buttons, Action[] actions, Color titleColor)
        {
            HideCard();
            _overlayRoot.gameObject.SetActive(true);
            SetFooterVisible(false);

            if (_ovDim == null)
            {
                _ovDim = SpriteRendererUtil.Make(_overlayRoot, "ovDim", TexArt.Solid(), 80);
                _ovDim.transform.localPosition = new Vector3(0f, (Top + Bottom) * 0.5f, 0f);
                _ovDim.transform.localScale = new Vector3(18f * 16f, (Top - Bottom) * 16f, 1f);
                _ovDim.color = new Color32(8, 6, 18, 215);
            }
            if (_ovPanel == null) _ovPanel = SlicedUnder(_overlayRoot, "ovPanel", TexArt.Panel(), 81);

            float innerW = 15.0f;

            if (_ovTitle == null) _ovTitle = LabelUnder(_overlayRoot, "ovTitle", 3, Color.white, TextAlign.Center, 82);
            int scale = 3; float titleH;
            while (true)
            {
                _ovTitle.Configure(scale, titleColor, TextAlign.Center, 82);
                _ovTitle.MaxWidthUnits = innerW;
                _ovTitle.Set(title);
                titleH = _ovTitle.MeasureHeight(title);
                if (scale <= 2 || _ovTitle.LineCount(title) <= 2) break;
                scale--;
            }

            var lineH = new float[lines.Length];
            float linesH = 0f;
            while (_ovLines.Count < lines.Length)
                _ovLines.Add(LabelUnder(_overlayRoot, "ovLine" + _ovLines.Count, 2, new Color(0.92f, 0.94f, 1f), TextAlign.Center, 82));
            for (int i = 0; i < lines.Length; i++)
            {
                var l = _ovLines[i];
                l.Configure(2, new Color(0.92f, 0.94f, 1f), TextAlign.Center, 82);
                l.MaxWidthUnits = innerW;
                l.Set(lines[i]);
                lineH[i] = string.IsNullOrEmpty(lines[i]) ? 0f : l.MeasureHeight(lines[i]);
                if (lineH[i] > 0f) linesH += lineH[i] + (linesH > 0f ? 0.3f : 0f);
            }

            int btnN = buttons.Length;
            const float BtnW = 13.2f, BtnH = 1.75f, BtnGap = 0.5f;
            float buttonsH = btnN > 0 ? btnN * BtnH + (btnN - 1) * BtnGap : 0f;

            float total = 1.4f * 2f + titleH + linesH + buttonsH
                        + (titleH > 0f && (linesH > 0f || buttonsH > 0f) ? 0.8f : 0f)
                        + (linesH > 0f && buttonsH > 0f ? 1.0f : 0f);
            // The card lives between the HUD strip and the log box. It used to be allowed down to
            // the bottom of the frame, so a tall result card (night end, level up) overlapped the
            // message panel by a few pixels -- two framed cards fighting for the same strip, with
            // the last line of the fight half covered. The last log line stays readable instead.
            float room = (Top - 1.2f) - (MsgTop + 0.35f);
            float h = Mathf.Min(total, room);
            float panelBottom = Mathf.Clamp(0.4f - h * 0.5f, MsgTop + 0.35f, Top - 1.2f - h);
            Box(_ovPanel, Left + 0.9f, panelBottom, 16.2f, h, Color.white);

            float y = Fx.Snap(panelBottom + h - 1.4f);
            if (titleH > 0f)
            {
                _ovTitle.transform.localPosition = new Vector3(0f, y, 0f);
                y = Fx.Snap(y - titleH - 0.8f);
            }
            for (int i = 0; i < lines.Length; i++)
            {
                if (lineH[i] <= 0f) continue;
                _ovLines[i].transform.localPosition = new Vector3(0f, y, 0f);
                y = Fx.Snap(y - lineH[i] - 0.3f);
            }
            if (btnN > 0) y -= 1.0f;
            for (int i = 0; i < btnN; i++)
            {
                var panel = SlicedUnder(_overlayRoot, "ovBtn" + i, TexArt.Panel(), 83);
                Box(panel, -BtnW * 0.5f, y - BtnH, BtnW, BtnH, new Color(1f, 1f, 0.95f, 0.95f));
                var text = LabelUnder(_overlayRoot, "ovBtnT" + i, 2, new Color(1f, 0.96f, 0.8f), TextAlign.Center, 84);
                text.transform.localPosition = new Vector3(0f, y - (BtnH - PixelFont.GlyphHUnits(2)) * 0.5f, 0f);
                text.Set(buttons[i]);
                _ovButtons.Add((panel, text, new Rect(-BtnW * 0.5f, y - BtnH, BtnW, BtnH), actions[i]));
                y -= BtnH + BtnGap;
            }

        }

        public void HideCard()
        {
            foreach (var b in _ovButtons)
            {
                if (b.panel != null) UtilDestroy(b.panel.gameObject);
                if (b.text != null) UtilDestroy(b.text.gameObject);
            }
            _ovButtons.Clear();
            if (_ovTitle != null) _ovTitle.Set("");
            foreach (var l in _ovLines) l.Set("");
            _overlayRoot.gameObject.SetActive(false);
            SetFooterVisible(true);
        }

        // a tap just outside a cell still aims at a command: on a phone a thumb misses the
        // painted rectangle often, and "nothing happened" reads as a broken menu
        // 1.2 units is a ring a thumb can miss by (~4% of the screen height) while staying
        // well inside the gap between the two menu rows, so a tap in the gap commits nothing
        const float MenuTouchSlop = 1.2f;

        public int HitMenu(Vector2 w)
        {
            if (!_menuOn) return -1;
            // an exact cell wins outright, so the near-miss search can never steal a row
            for (int i = 0; i < Menu.Count; i++)
                if (Menu[i].Hit.Contains(w)) return i;
            int best = -1; float bd = MenuTouchSlop;
            for (int i = 0; i < Menu.Count; i++)
            {
                var c = Menu[i].Hit.center;
                float d = Mathf.Abs(c.x - w.x) + Mathf.Abs(c.y - w.y);
                if (d < bd) { bd = d; best = i; }
            }
            return best;
        }

        public int HitEnemy(Vector2 w)
        {
            int best = -1; float bd = float.MaxValue;
            for (int i = 0; i < EnemyRigs.Length; i++)
            {
                var rig = EnemyRigs[i];
                if (rig == null || !rig.F.Alive || rig.Sr.sprite == null) continue;
                var b = rig.Sr.bounds;
                var pad = new Bounds(b.center, b.size + new Vector3(0.6f, 0.6f, 0f));
                if (!pad.Contains(w)) continue;
                float d = Mathf.Abs(w.x - b.center.x) + Mathf.Abs(w.y - b.center.y);
                if (d < bd) { bd = d; best = i; }
            }
            return best;
        }

        public Action HitCardButton(Vector2 w)
        {
            foreach (var b in _ovButtons)
                if (b.rect.Contains(w)) return b.act;
            return null;
        }

        /// <summary>Self-test / automation hook: invoke a card button without hit-testing.</summary>
        public Action CardButtonAt(int i) => i >= 0 && i < _ovButtons.Count ? _ovButtons[i].act : null;

        /// <summary>Self-test hook: the screen rect of a card button, so the tap path that a
        /// real finger takes can be tested instead of the shortcut.</summary>
        public Rect CardButtonRect(int i) => i >= 0 && i < _ovButtons.Count ? _ovButtons[i].rect : new Rect(0f, 0f, 0f, 0f);

        /// <summary>The AUTO chip rect, matching the Box() it was built with.</summary>
        public bool HitAuto(Vector2 w)
        {
            return new Rect(Right - 4.7f, Top - 2.45f, 4.0f, 1.9f).Contains(w);
        }

        /// <summary>Brighten the chip while the party fights itself.</summary>
        public void SetAuto(bool on)
        {
            if (_autoChip != null)
                _autoChip.color = on ? new Color(1f, 0.95f, 0.6f, 0.95f) : new Color(1f, 1f, 1f, 0.5f);
            if (_autoLabel != null)
                _autoLabel.SetColor(on ? new Color(1f, 0.95f, 0.6f) : new Color(0.8f, 0.83f, 1f));
        }

        public Rig RigOf(Fighter f)
        {
            if (f.Side == Side.Party)
            {
                foreach (var pr in PartyRigs) if (pr.F == f) return pr;
            }
            else
            {
                foreach (var er in EnemyRigs) if (er != null && er.F == f) return er;
            }
            return null;
        }

        public Vector2 BodyCenter(Fighter f)
        {
            var r = RigOf(f);
            return r == null ? Vector2.zero : new Vector2(r.Home.x, r.Home.y + r.BodyHeight * 0.6f);
        }

        public void FloatNumber(Vector2 pos, string text, Color color)
        {
            var go = new GameObject("floatn");
            go.transform.SetParent(Stage, false);
            go.transform.localPosition = new Vector3(pos.x, pos.y, 0f);
            var label = go.AddComponent<PixelLabel>();
            label.Configure(2, color, TextAlign.Center, 40);
            label.SnapToPixelGrid = false;
            // damage numbers drift over whatever art the arena uses; without a shadow they
            // vanish into the light patches
            label.Shadow = true;
            label.Set(text, true);
            if (!Application.isPlaying) return;
            StartCoroutine(FloatRoutine(go.transform, label, color));
        }

        IEnumerator FloatRoutine(Transform t, PixelLabel label, Color color)
        {
            float e = 0f;
            var y0 = t.localPosition.y;
            while (e < 0.9f)
            {
                e += Time.deltaTime;
                float k = 1f - (1f - Mathf.Clamp01(e / 0.9f)) * (1f - Mathf.Clamp01(e / 0.9f));
                t.localPosition = new Vector3(t.localPosition.x, y0 + 1.5f * k, 0f);
                var c = color; c.a = 1f - Mathf.Clamp01((e / 0.9f - 0.55f) / 0.45f);
                label.SetColor(c);
                yield return null;
            }
            UtilDestroy(t.gameObject);
        }

        public void Sparkle(Vector2 pos, Color color, int count = 10)
        {
            if (!Application.isPlaying) return;
            for (int i = 0; i < count; i++)
            {
                var go = new GameObject("spark");
                go.transform.SetParent(Stage, false);
                go.transform.localPosition = new Vector3(pos.x, pos.y, 0f);
                go.transform.localScale = Vector3.one * 2f;
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = TexArt.Spark();
                sr.color = color;
                sr.sortingOrder = 42;
                float ang = Mathf.PI * 2f * i / count + UnityEngine.Random.Range(-0.2f, 0.2f);
                StartCoroutine(SparkRoutine(go.transform, sr, new Vector3(Mathf.Cos(ang), Mathf.Sin(ang) * 0.85f, 0f) * 1.7f, color));
            }
        }

        IEnumerator SparkRoutine(Transform t, SpriteRenderer sr, Vector3 off, Color color)
        {
            var start = t.localPosition;
            float e = 0f;
            while (e < 0.55f)
            {
                e += Time.deltaTime;
                float k = Mathf.Clamp01(e / 0.55f);
                t.localPosition = start + off * (1f - (1f - k) * (1f - k));
                var c = color; c.a = 1f - k;
                sr.color = c;
                yield return null;
            }
            UtilDestroy(t.gameObject);
        }

        // ---------------------------------------------------------------- helpers

        SpriteRenderer Sliced(string name, Sprite sprite, int sorting) => SlicedUnder(Stage, name, sprite, sorting);

        static SpriteRenderer SlicedUnder(Transform parent, string name, Sprite sprite, int sorting)
        {
            var sr = SpriteRendererUtil.Make(parent, name, sprite, sorting);
            sr.drawMode = SpriteDrawMode.Sliced;
            sr.size = new Vector2(1f, 1f);
            return sr;
        }

        /// <summary>Fits a dark plate to the measured name above it, so a short name and a
        /// long one both get a plate that matches.</summary>
        void Plate(SpriteRenderer chip, PixelLabel label, string text, bool boss = false, bool friend = false)
        {
            var at = label.transform.localPosition;
            float w = Mathf.Max(1.0f, label.MeasureWidth(text) + 0.46f);
            float h = PixelFont.GlyphHUnits(1) + 0.16f;
            // a befriended beast wears a warm plate, a boss a red one, everything else night-dark
            Box(chip, at.x - w * 0.5f, at.y - h + 0.05f, w, h,
                boss ? new Color32(48, 10, 18, 215)
                     : friend ? new Color32(58, 42, 14, 220)
                     : new Color32(10, 8, 20, 205));
        }

        static void Box(SpriteRenderer sr, float left, float bottom, float wUnits, float hUnits, Color color)
        {
            left = G.Snap(left); bottom = G.Snap(bottom);
            float w = Mathf.Max(G.Pixel, G.Snap(wUnits));
            float h = Mathf.Max(G.Pixel, G.Snap(hUnits));
            sr.drawMode = SpriteDrawMode.Sliced;
            sr.size = new Vector2(w, h);
            sr.transform.localPosition = new Vector3(left + w * 0.5f, bottom + h * 0.5f, 0f);
            sr.transform.localScale = Vector3.one;
            sr.color = color;
            sr.enabled = true;
        }

        // instance method: labels must live under Stage so hiding the battle view
        // hides them too (scene-root labels leaked onto the title/explore screens).
        PixelLabel Label(string name, int scale, Color color, TextAlign align, int sorting)
            => PixelLabelUtil.Make(Stage, name, scale, color, align, sorting);

        static PixelLabel LabelUnder(Transform parent, string name, int scale, Color color, TextAlign align, int sorting)
            => PixelLabelUtil.Make(parent, name, scale, color, align, sorting);

        static void UtilDestroy(GameObject go)
        {
            if (go == null) return;
            if (Application.isPlaying) Destroy(go); else DestroyImmediate(go);
        }
    }

    /// <summary>Turn-based flow: speed queue, attacks, befriend attempts, rewards, boss gate.</summary>
    public class BattleDirector : MonoBehaviour
    {
        public BattleView View;
        public Action OnBattleWon;          // normal encounter cleared
        public Action OnBossWon;            // chapter boss defeated
        public Action OnDefeat;             // party wiped

        enum Ph { Idle, Intro, Round, Acting, Card }
        Ph _ph = Ph.Idle;
        readonly List<Fighter> _queue = new List<Fighter>();
        int _qi, _round = 1;
        MonsterSpec[] _specs;
        bool _morselUsed;
        int _befriended;
        public bool AwaitingInput { get; private set; }

        /// <summary>Auto-battle: the party picks its own moves. Persists between fights on
        /// purpose - it is a stance, not a per-round wish.</summary>
        public bool Auto { get; private set; }
        int _levelAtStart = 1;

        public void ToggleAuto()
        {
            Auto = !Auto;
            Prefs.Auto = Auto;
            Prefs.Store();   // a stance, remembered for every fight after this one too
            View.SetAuto(Auto);
            Sfx.Play("autoon");
            View.SetMessage(Strings.Get(Auto ? "bt.auto.on" : "bt.auto.off"));
            // turning it on mid-turn should feel immediate: if it is our fighter's move and
            // nobody has picked yet, the party goes right away
            if (Auto && AwaitingInput && Application.isPlaying)
                StartCoroutine(Timer(0.35f, () => AutoPick(_queue[_qi])));
        }

        // self-test diagnostics
        public string DebugPhase => _ph.ToString();
        public int DebugQi => _qi;
        public int DebugRound => _round;
        public int DebugQueue => _queue.Count;

        public void StartBattle(MonsterSpec[] specs)
        {
            _specs = specs;
            _ph = Ph.Intro;
            _round = 1;
            _qi = 0;
            _morselUsed = false;
            _befriended = 0;
            _levelAtStart = Game.State.Level;
            AwaitingInput = false;
            // each night has its own ground: the hollow's woods, the long fields, the deep
            View.SetBackdrop(Game.State.Chapter >= 3 ? "Art/Backgrounds/DungeonA"
                : Game.State.Chapter == 2 ? "Art/Backgrounds/PlainA" : "Art/Backgrounds/ForestA");
            View.SetNight(Game.State.Chapter);
            View.SetMoonIcon(Game.State.Chapter >= 3);
            View.RebuildParty();
            View.ResetPartyHp();
            View.SetEncounter(specs);
            View.HideCard();
            View.SetMenuVisible(false);
            Auto = Prefs.Auto;
            View.SetAuto(Auto);
            View.IntroSlide();
            bool hasBoss = false;
            foreach (var s in specs) if (s.Boss) hasBoss = true;
            var first = Strings.Get(hasBoss ? "bt.boss" : specs.Length > 1 ? "bt.two" : "bt.one",
                View.Enemies[0].Name);
            View.SetMessage(first);
            Sfx.Mus.Play(hasBoss ? "boss" : "battle");
            Sfx.Play(hasBoss ? "boss" : "enemy");
            if (Application.isPlaying) StartCoroutine(Timer(1.4f, RoundStart));
        }

        IEnumerator Timer(float t, Action done)
        {
            yield return Fx.Wait(t);
            done();
        }

        void RoundStart()
        {
            _ph = Ph.Round;
            _queue.Clear();
            var all = new List<Fighter>(View.Party.Length + View.Enemies.Length);
            foreach (var f in View.Party) if (f.Alive) all.Add(f);
            foreach (var f in View.Enemies) if (f.Alive) all.Add(f);
            all.Sort((a, b) => b.Speed.CompareTo(a.Speed));
            _queue.AddRange(all);
            _qi = 0;
            View.SetRound(_round);
            NextTurn();
        }

        void NextTurn()
        {
            if (AllEnemiesGone()) { Win(); return; }
            if (PartyWiped()) { Lose(); return; }

            int guard = 0;
            while (guard++ < 20)
            {
                if (_qi >= _queue.Count)
                {
                    _round++;
                    RoundStart();
                    return;
                }
                var f = _queue[_qi];
                if (f.Alive) break;
                _qi++;
            }
            var cur = _queue[_qi];
            if (cur.Side == Side.Party) BeginPlayerTurn(cur);
            else if (Application.isPlaying) StartCoroutine(EnemyTurn(cur));
            else EnemyTurnImmediate(cur);
        }

        void BeginPlayerTurn(Fighter f)
        {
            View.SetTurnRig(View.RigOf(f));
            // a befriended beast acts on its own - no command menu, it just helps
            if (f.Species != null && Application.isPlaying)
            {
                StartCoroutine(FriendTurn(f));
                return;
            }
            AwaitingInput = true;
            View.SetMenuVisible(true);
            View.SetSelected(0);
            View.ShowHint(true);
            // the strike button names the move this hero actually does: amber strikes,
            // sea sweeps the whole field, moss mends the hurtest friend
            int style = f.Style;
            View.Menu[0].Text.Set(Strings.Get(style == 1 ? "menu.sweep" : style == 2 ? "menu.mend" : "menu.strike"));
            View.Menu[0].Icon.sprite = TexArt.MenuIcon(style == 1 ? 13 : style == 2 ? 29 : 0);
            // commands that cannot fire go grey: morsel needs bag food and a fresh
            // portion, befriend needs room in the two-heart stable
            View.SetCellEnabled(2, !_morselUsed && Game.State.BestFood() != null);
            View.SetCellEnabled(1, Game.State.Friends.Count < 2);
            View.AimStyle = f.Style;
            View.SetTarget(View.Target);
            View.SetMessage(Strings.Get("bt.yourturn", f.Name));
            // auto-battle acts after a short beat, so the player sees whose turn it was
            if (Auto && Application.isPlaying)
                StartCoroutine(Timer(0.45f, () => AutoPick(f)));
        }

        /// <summary>The auto-battle brain: mend anyone badly hurt if we still carry food,
        /// otherwise strike the weakest standing foe. Deliberately simple - it should feel
        /// like a sensible party, not a solver.</summary>
        void AutoPick(Fighter actor)
        {
            if (!AwaitingInput || !Auto) return;   // a hand got there first
            bool hurt = false;
            foreach (var p in View.Party) if (p.Alive && p.Hp01 < 0.45f) hurt = true;
            if (hurt && !_morselUsed && Game.State.BestFood() != null)
            {
                View.SetSelected(2);
                Confirm();
                return;
            }
            int ti = -1; float low = float.MaxValue;
            for (int i = 0; i < View.Enemies.Length; i++)
                if (View.Enemies[i].Alive && View.Enemies[i].Hp < low) { low = View.Enemies[i].Hp; ti = i; }
            if (ti >= 0) View.SetTarget(ti);
            View.SetSelected(0);
            Confirm();
        }

        /// <summary>Back to the idle breathing clip after an action animation.</summary>
        void PlayIdle(BattleView.Rig rig)
        {
            if (rig?.Anim == null) return;
            if (rig.F.Side != Side.Party) return;
            rig.Anim.Play(rig.F.Species != null
                ? new[] { Bank.One(rig.F.BattlerPath) }
                : Bank.Frames(BattleData.ClipPath(rig.F.ColorDir, "breath_idle")), 6f, true);
        }

        /// <summary>A befriended beast's turn: a quick lunge at the weakest standing foe, no
        /// menu in the way - it earned its spot in line.</summary>
        IEnumerator FriendTurn(Fighter f)
        {
            _ph = Ph.Acting;
            var aRig = View.RigOf(f);
            int ti = -1; float low = float.MaxValue;
            for (int i = 0; i < View.Enemies.Length; i++)
                if (View.Enemies[i].Alive && View.Enemies[i].Hp < low) { low = View.Enemies[i].Hp; ti = i; }
            if (ti < 0) { EndTurn(); yield break; }
            var target = View.Enemies[ti];
            var tRig = View.RigOf(target);
            View.SetMessage(Strings.Get("bt.friendturn", f.Name));
            yield return Fx.Wait(0.4f);
            yield return Lunge(aRig, tRig != null ? tRig.Home : aRig.Home, 0.3f);
            bool crit = UnityEngine.Random.value < 0.18f;
            bool weakHit = WeakTo(0, target);
            int dmg = Mathf.RoundToInt(UnityEngine.Random.Range(f.AtkMin, f.AtkMax + 1) * (crit ? 1.6f : 1f) * (weakHit ? 1.5f : 1f));
            HitFoe(target, dmg, crit, weakHit);
            View.Refresh();
            yield return Fx.Wait(0.45f);
            yield return Lunge(aRig, aRig.Home, 0.3f);
            PlayIdle(aRig);
            if (!target.Alive)
            {
                yield return FadeOut(tRig);
                View.SetMessage(Strings.Get("bt.fainted", target.Name));
                yield return Fx.Wait(0.6f);
            }
            EndTurn();
        }

        IEnumerator EnemyTurn(Fighter e)
        {
            _ph = Ph.Acting;
            // the guard does not swat like the wild things: now and then it rears up and
            // brings the whole weight of the night down on one hero
            bool slam = e.Boss && UnityEngine.Random.value < 0.35f;
            View.SetMessage(Strings.Get(slam ? "bt.slam" : "bt.enemyturn", e.Name));
            yield return Fx.Wait(slam ? 0.85f : 0.5f);

            // families hunt the way their sprites suggest: swarm and sting dive for the
            // weakest, the dead strike anywhere, and everything else swats the toughest
            Fighter target = null;
            var fam = e.Species != null && BattleData.Species(e.Species).HasValue
                ? BattleData.FamilyOf(BattleData.Species(e.Species).Value) : "";
            if (fam == "ghost" || fam == "skeleton")
            {
                var any = new List<Fighter>();
                foreach (var p in View.Party) if (p.Alive) any.Add(p);
                if (any.Count > 0) target = any[UnityEngine.Random.Range(0, any.Count)];
            }
            else foreach (var p in View.Party)
            {
                if (!p.Alive) continue;
                if (target == null) { target = p; continue; }
                bool frail = fam == "wasp" || fam == "scorpion";
                if (frail ? p.Hp < target.Hp : p.Hp > target.Hp) target = p;
            }
            if (target == null) { Lose(); yield break; }

            var eRig = View.RigOf(e);
            var tRig = View.RigOf(target);
            yield return Lunge(eRig, tRig.Home, 0.3f);

            int dmg = Mathf.RoundToInt(UnityEngine.Random.Range(e.AtkMin, e.AtkMax + 1) * (slam ? 1.6f : 1f));
            target.Hp = Mathf.Max(0, target.Hp - dmg);
            var stagger = Bank.Frames(BattleData.ClipPath(target.ColorDir, "hit"));
            if (stagger.Length > 0) tRig.Anim.Play(stagger, 14f, false);
            else StartCoroutine(Fx.FlashTint(tRig.Anim, new Color(1f, 0.45f, 0.45f), 2, 0.08f, 0.08f));
            StartCoroutine(Fx.Shake(tRig.Root, slam ? 0.22f : 0.14f, 0.25f));
            if (slam) StartCoroutine(Fx.Shake(View.Stage, 0.15f, 0.2f));
            View.FloatNumber(tRig.Home + new Vector3(0f, 1.4f, 0f), "-" + dmg,
                slam ? new Color(1f, 0.45f, 0.3f) : new Color(1f, 0.6f, 0.55f));
            Sfx.Play("hurt");
            View.Refresh();
            yield return Fx.Wait(0.4f);
            yield return Lunge(eRig, eRig.Home, 0.3f);

            if (!target.Alive)
            {
                yield return FadeOut(tRig, true);
                Sfx.Play("faint");
                View.SetMessage(Strings.Get("bt.herodown", target.Name));
                yield return Fx.Wait(0.8f);
            }
            else PlayIdle(tRig);
            EndTurn();
        }

        void EnemyTurnImmediate(Fighter e)
        {
            // deterministic path for static previews
            Fighter target = null;
            var fam = e.Species != null && BattleData.Species(e.Species).HasValue
                ? BattleData.FamilyOf(BattleData.Species(e.Species).Value) : "";
            bool frail = fam == "wasp" || fam == "scorpion";
            foreach (var p in View.Party)
            {
                if (!p.Alive) continue;
                if (target == null || (frail ? p.Hp < target.Hp : p.Hp > target.Hp)) target = p;
            }
            if (target != null)
            {
                int dmg = UnityEngine.Random.Range(e.AtkMin, e.AtkMax + 1);
                target.Hp = Mathf.Max(0, target.Hp - dmg);
            }
            EndTurn();
        }

        IEnumerator Lunge(BattleView.Rig rig, Vector3 toward, float dur)
        {
            if (rig?.Root == null) yield break;
            var from = rig.Root.localPosition;
            var dir = (toward - from) * 0.35f;
            yield return Fx.MoveLocal(rig.Root, from + dir, dur * 0.4f);
            yield return Fx.MoveLocal(rig.Root, from, dur * 0.6f);
        }

        // ------------------------------------------------------------ edit-mode driving

        /// <summary>Coroutines do not run outside play mode, so the timed round start that
        /// reveals the command menu never fires in editor previews. Drive it by hand.</summary>
        public void EditorTick()
        {
            if (Application.isPlaying || _ph != Ph.Intro) return;
            RoundStart();
        }

        // ------------------------------------------------------------ player actions

        /// <summary>True while the result card owns the screen.</summary>
        public bool CardUp => _ph == Ph.Card;

        public void TapAt(Vector2 w)
        {
            // the card is checked before the phase guard: while it is up it owns the
            // screen, and its buttons were unreachable when the guard came first
            var cardBtn = View.HitCardButton(w);
            if (cardBtn != null) { cardBtn(); return; }

            if (_ph == Ph.Idle || _ph == Ph.Card) return;
            // the AUTO chip is tappable whenever the fight is on screen
            if (View.HitAuto(w)) { ToggleAuto(); return; }
            if (_ph != Ph.Round && _ph != Ph.Acting) return;

            if (AwaitingInput)
            {
                int cell = View.HitMenu(w);
                if (cell >= 0) { View.SetSelected(cell); Confirm(); return; }
                int enemy = View.HitEnemy(w);
                if (enemy >= 0) View.SetTarget(enemy);
            }
        }

        public void SelectCell(int d) => View.SetSelected(View.SelectedCell + d);

        public void CycleTarget(int d)
        {
            int n = View.EnemyRigs.Length;
            for (int k = 1; k <= n; k++)
            {
                int i = (View.Target + d * k + n * 4) % n;
                if (View.Enemies[i].Alive) { View.SetTarget(i); return; }
            }
        }

        public void Confirm()
        {
            if (!AwaitingInput) return;
            AwaitingInput = false;
            View.SetMenuVisible(false);
            View.ShowHint(false);
            var actor = _queue[_qi];
            switch (View.SelectedCell)
            {
                case 0: StartCoroutine(PlayerAttack(actor)); break;
                case 1: StartCoroutine(PlayerBefriend(actor)); break;
                case 2: StartCoroutine(PlayerMorsel(actor)); break;
                default: EndTurn(); break;   // run: skip, village never traps you
            }
        }

        IEnumerator PlayerAttack(Fighter actor)
        {
            _ph = Ph.Acting;
            int ti = View.Target;
            if (ti < 0 || !View.Enemies[ti].Alive)
            {
                for (int i = 0; i < View.Enemies.Length; i++)
                    if (View.Enemies[i].Alive) { ti = i; break; }
            }
            if (ti < 0) { Win(); yield break; }
            var aRig = View.RigOf(actor);

            // the hero's own swing: the pack ships a 7 frame attack clip per colour
            if (aRig != null && aRig.Anim != null)
            {
                var swing = Bank.Frames(BattleData.ClipPath(actor.ColorDir, "attack"));
                if (swing.Length > 0) aRig.Anim.Play(swing, 15f, false);
            }

            // Each friend has a style, so ATTACK is three different moves on one button:
            // amber lunges and sometimes finds the weak seam (crit), sea sweeps the whole
            // field for less per hit, moss spends the turn mending the hurtest friend
            // (or throws a pebble when everyone is fine).
            if (actor.Style == 2)
            {
                Fighter weak = null;
                foreach (var p in View.Party)
                    if (p.Alive && p.Hp < p.MaxHp && (weak == null || p.Hp01 < weak.Hp01)) weak = p;
                if (weak != null && weak.Hp01 < 0.9f)
                {
                    var wRig = View.RigOf(weak);
                    yield return Lunge(aRig, wRig != null ? wRig.Home : aRig.Home, 0.3f);
                    int heal = Mathf.Max(4, (actor.AtkMin + actor.AtkMax) / 2 + Game.State.Level);
                    weak.Hp = Mathf.Min(weak.MaxHp, weak.Hp + heal);
                    View.SetMessage(Strings.Get("bt.attack.2", actor.Name, weak.Name));
                    if (wRig != null)
                    {
                        View.Sparkle(wRig.Home + new Vector3(0f, 1.1f, 0f), new Color(0.6f, 1f, 0.75f), 8);
                        View.FloatNumber(wRig.Home + new Vector3(0f, 1.2f, 0f), Strings.Get("bt.heal", heal), new Color(0.7f, 1f, 0.7f));
                    }
                    Sfx.Play("heal");
                    View.Refresh();
                    yield return Fx.Wait(0.6f);
                    yield return Lunge(aRig, aRig.Home, 0.3f);
                    PlayIdle(aRig);
                    EndTurn();
                    yield break;
                }
                // nobody needs mending: moss throws a pebble for half damage
                var target = View.Enemies[ti];
                var tRig = View.RigOf(target);
                View.SetMessage(Strings.Get("bt.attack.2b", actor.Name, target.Name));
                yield return Lunge(aRig, tRig.Home, 0.3f);
                bool weakHit = WeakTo(2, target);
                int pebble = Mathf.Max(1, Mathf.RoundToInt(UnityEngine.Random.Range(actor.AtkMin, actor.AtkMax + 1) * (weakHit ? 0.75f : 0.5f)));
                HitFoe(target, pebble, false, weakHit);
                yield return Fx.Wait(0.4f);
                yield return Lunge(aRig, aRig.Home, 0.3f);
                PlayIdle(aRig);
                if (!target.Alive)
                {
                    yield return FadeOut(tRig);
                    View.SetMessage(Strings.Get("bt.fainted", target.Name));
                    yield return Fx.Wait(0.6f);
                }
                EndTurn();
                yield break;
            }

            if (actor.Style == 1)
            {
                // sea sweeps the field: every standing foe takes 65% of a roll
                View.SetMessage(Strings.Get("bt.attack.1", actor.Name));
                var firstRig = View.RigOf(View.Enemies[ti]);
                yield return Lunge(aRig, firstRig != null ? firstRig.Home : aRig.Home, 0.35f);
                int roll = UnityEngine.Random.Range(actor.AtkMin, actor.AtkMax + 1);
                for (int i = 0; i < View.Enemies.Length; i++)
                {
                    if (!View.Enemies[i].Alive) continue;
                    bool weakHit = WeakTo(1, View.Enemies[i]);
                    HitFoe(View.Enemies[i], Mathf.Max(1, Mathf.RoundToInt(roll * (weakHit ? 0.975f : 0.65f))), false, weakHit);
                }
                View.Refresh();
                yield return Fx.Wait(0.5f);
                yield return Lunge(aRig, aRig.Home, 0.3f);
                PlayIdle(aRig);
                int felled = 0; string last = null;
                for (int i = 0; i < View.Enemies.Length; i++)
                {
                    var e = View.Enemies[i];
                    if (e.Alive) continue;
                    var er = View.RigOf(e);
                    if (er != null && er.Root.gameObject.activeSelf) { yield return FadeOut(er); felled++; last = e.Name; }
                }
                if (felled > 0)
                {
                    View.SetMessage(Strings.Get("bt.fainted", last));
                    yield return Fx.Wait(0.6f);
                }
                EndTurn();
                yield break;
            }

            // amber strikes: a single lunge with a quarter chance of a crit
            {
                var target = View.Enemies[ti];
                var tRig = View.RigOf(target);
                yield return Lunge(aRig, tRig.Home, 0.35f);
                bool crit = UnityEngine.Random.value < 0.25f;
                bool weakHit = WeakTo(0, target);
                int dmg = Mathf.RoundToInt(UnityEngine.Random.Range(actor.AtkMin, actor.AtkMax + 1) * (crit ? 1.7f : 1f) * (weakHit ? 1.5f : 1f));
                View.SetMessage(crit ? Strings.Get("bt.attack.crit", actor.Name, dmg) : Strings.Get("bt.attack.0", actor.Name));
                HitFoe(target, dmg, crit, weakHit);
                View.Refresh();
                yield return Fx.Wait(0.45f);
                yield return Lunge(aRig, aRig.Home, 0.3f);
                PlayIdle(aRig);
                if (!target.Alive)
                {
                    yield return FadeOut(tRig);
                    View.SetMessage(Strings.Get("bt.fainted", target.Name));
                    yield return Fx.Wait(0.6f);
                }
                EndTurn();
            }
        }

        /// <summary>Damage + feedback for one foe: tint flash, shake, the floating number.
        /// A weakness hit earns its own banner above the number so the table is learnable.</summary>
        void HitFoe(Fighter target, int dmg, bool crit, bool weak = false)
        {
            target.Hp = Mathf.Max(0, target.Hp - dmg);
            var tRig = View.RigOf(target);
            if (tRig == null) return;
            StartCoroutine(Fx.FlashTint(tRig.Anim, new Color(1f, 0.5f, 0.4f), 2, 0.07f, 0.07f));
            StartCoroutine(Fx.Shake(tRig.Root, crit || weak ? 0.2f : 0.12f, crit || weak ? 0.3f : 0.22f));
            if (crit) StartCoroutine(Fx.Shake(View.Stage, 0.13f, 0.18f));
            View.FloatNumber(tRig.Home + new Vector3(0f, 1.2f, 0f), "-" + dmg,
                crit ? new Color(1f, 0.85f, 0.3f) : weak ? new Color(0.65f, 1f, 0.95f) : new Color(1f, 0.95f, 0.75f));
            if (weak)
                View.FloatNumber(tRig.Home + new Vector3(0f, 1.9f, 0f), Strings.Get("bt.weak"), new Color(0.65f, 1f, 0.95f));
            Sfx.Play(crit ? "crit" : "hit");
        }

        /// <summary>True when the acting friend's style cuts this foe's family seam.</summary>
        static bool WeakTo(int style, Fighter foe)
        {
            if (foe.Species == null) return false;
            var s = BattleData.Species(foe.Species);
            return s.HasValue && BattleData.StyleBeats(style, s.Value);
        }

        IEnumerator FadeOut(BattleView.Rig rig, bool keepRoot = false)
        {
            yield return Fx.Fade(rig.Anim, new Color(1f, 1f, 1f, 0f), 0.45f);
            rig.Sr.enabled = false;
            rig.Anim.SetTint(Color.white);
            // the name plate is stage-level chrome, not part of the rig root: without this a
            // fainted fighter's name and HP bar hung in the air where the body used to be.
            // A KO'd hero keeps the bar - the empty slot still reads as part of the party.
            if (rig.Name != null) rig.Name.gameObject.SetActive(false);   // enabled=false only
            if (rig.NameChip != null) rig.NameChip.enabled = false;       // stops the component:
            if (rig.Hat != null) rig.Hat.enabled = false;                 // the glyph pool stays lit
            if (!keepRoot)                                                // and the hat is its own
            {                                                             // renderer off the sprite
                if (rig.BarBg != null) rig.BarBg.enabled = false;         // transform, not the body
                if (rig.BarFill != null) rig.BarFill.enabled = false;
                rig.Root.gameObject.SetActive(false);
            }
        }

        IEnumerator PlayerBefriend(Fighter actor)
        {
            _ph = Ph.Acting;
            int ti = View.Target;
            if (ti < 0 || !View.Enemies[ti].Alive) { EndTurn(); yield break; }
            var target = View.Enemies[ti];
            var tRig = View.RigOf(target);

            float chance = Mathf.Clamp01(0.12f + (1f - target.Hp01) * 0.55f + (_morselUsed ? 0.2f : 0f));
            // the odds print inside the beat so a whiff feels like a roll you saw coming,
            // and softening a foe visibly raises the number
            View.SetMessage(Game.State.Friends.Count >= 2
                ? Strings.Get("bt.stablefull")
                : Strings.Get("bt.trybefriend", target.Name, Mathf.RoundToInt(chance * 100f)));
            if (Game.State.Friends.Count >= 2)
            {
                Sfx.Play("fail");
                yield return Fx.Wait(0.7f);
                EndTurn();
                yield break;
            }
            View.Sparkle(tRig.Home + new Vector3(0f, tRig.BodyHeight * 0.5f, 0f), new Color(0.85f, 0.9f, 1f), 8);
            yield return Fx.Wait(0.9f);

            if (UnityEngine.Random.value < chance && !target.Boss
                && target.Species != null && !Game.State.Friends.Contains(target.Species)
                && Game.State.Friends.Count < 2)
            {
                target.Captured = true;
                _befriended++;
                Game.State.Befriended++;
                Game.State.Friends.Add(target.Species);
                View.Sparkle(tRig.Home + new Vector3(0f, tRig.BodyHeight * 0.5f, 0f), new Color(1f, 0.95f, 0.6f), 14);
                Sfx.Play("befriend");
                View.SetMessage(Strings.Get("bt.befriended", target.Name));
                yield return FadeOut(tRig);
                yield return Fx.Wait(0.5f);
            }
            else
            {
                View.SetMessage(Strings.Get("bt.befriendfail", target.Name));
                Sfx.Play("fail");
                StartCoroutine(Fx.Shake(tRig.Root, 0.1f, 0.2f));
                yield return Fx.Wait(0.8f);
            }
            EndTurn();
        }

        int PartyStanding()
        {
            int n = 0;
            foreach (var f in View.Party) if (f.Alive) n++;
            return n;
        }

        /// <summary>Morsel: the party shares the best food in the bag. It used to be a line of
        /// flavour text with no effect at all, which made the whole item list pointless.</summary>
        IEnumerator PlayerMorsel(Fighter actor)
        {
            _ph = Ph.Acting;
            if (_morselUsed)
            {
                View.SetMessage(Strings.Get("bt.nomore"));
                yield return Fx.Wait(0.7f);
                EndTurn();
                yield break;
            }
            string food = Game.State.BestFood();
            if (food == null)
            {
                View.SetMessage(Strings.Get("bt.nofood"));
                yield return Fx.Wait(0.8f);
                EndTurn();
                yield break;
            }
            _morselUsed = true;
            Game.State.RemoveBag(food);
            Game.State.MorselsUsed++;
            var def = Items.Get(food);
            View.SetMessage(Strings.Get("bt.morsel2", Strings.Get(food), def.Power));
            foreach (var p in View.Party)
            {
                if (!p.Alive || p.Hp >= p.MaxHp) continue;
                var rig = View.RigOf(p);
                p.Hp = Mathf.Min(p.MaxHp, p.Hp + def.Power);
                if (rig != null) View.FloatNumber(rig.Home + new Vector3(0f, 1.2f, 0f), "+" + def.Power,
                    new Color(0.7f, 1f, 0.7f));
            }
            View.Refresh();
            yield return Fx.Wait(0.9f);
            EndTurn();
        }

        void EndTurn()
        {
            _ph = Ph.Round;
            View.SetTurnRig(null);
            if (AllEnemiesGone()) { Win(); return; }
            if (PartyWiped()) { Lose(); return; }
            _qi++;
            NextTurn();
        }

        bool AllEnemiesGone()
        {
            foreach (var e in View.Enemies) if (e.Alive) return false;
            return true;
        }

        bool PartyWiped()
        {
            foreach (var p in View.Party) if (p.Alive) return false;
            return true;
        }

        void Win()
        {
            _ph = Ph.Card;
            AwaitingInput = false;
            View.SetMenuVisible(false);

            bool boss = false;
            foreach (var s in _specs) if (s.Boss) boss = true;

            int xp = 0, gold = 0;
            foreach (var e in View.Enemies)
            {
                xp += 45 * Mathf.Max(1, e.Boss ? 4 : 1);
                gold += UnityEngine.Random.Range(18, 40);
                Game.State.Defeats++;   // one step for the nightwatch
            }
            // the bestiary is keyed by the species, not its printed name
            foreach (var s in _specs) Game.State.MarkSeen(s.Name);
            Game.State.Xp += xp;
            Game.State.Gold += gold;

            var lines = new List<string>
            {
                Strings.Get("card.xp", xp),
                Strings.Get("card.gold", gold),
            };
            if (Game.State.Level > _levelAtStart)
            {
                lines.Add(Strings.Get("card.levelup", Game.State.Level));
                Sfx.Play("levelup");
            }
            // spoils: the Guard always leaves gear, the wild things sometimes do
            var rng = new System.Random();
            var drops = new List<string>();
            foreach (var s in _specs)
            {
                var key = s.Boss ? Items.BossDrop(rng) : Items.RollDrop(Game.State.Chapter, rng);
                if (key == null) continue;
                Game.State.AddBag(key);
                drops.Add(key);
            }
            if (drops.Count > 0)
            {
                var names = new List<string>();
                foreach (var d in drops) names.Add(Strings.Get(d));
                // one spoils line, however many things fell - the card's frame is not negotiable
                lines.Add(Strings.Get("card.drop", string.Join(" + ", names)));
            }

            if (_befriended > 0) lines.Add(Strings.Get("card.befriended", _befriended, _specs.Length));
            lines.Add(_befriended > 0 ? Strings.Get("card.joined") : Strings.Get("card.moon"));

            if (boss)
            {
                lines.Add(Strings.Get("card.bossline"));
                View.ShowCard(Strings.Get("card.bosstitle"), lines.ToArray(),
                    new[] { Strings.Get("btn.continue") },
                    new Action[] { () => OnBossWon?.Invoke() },
                    new Color(1f, 0.9f, 0.55f));
            }
            else
            {
                View.ShowCard(Strings.Get("card.wintitle"), lines.ToArray(),
                    new[] { Strings.Get("btn.continue") },
                    new Action[] { () => OnBattleWon?.Invoke() },
                    new Color(1f, 0.95f, 0.7f));
            }
            Sfx.Play("win");
            View.CardSparkle();
        }

        void Lose()
        {
            _ph = Ph.Card;
            AwaitingInput = false;
            View.SetMenuVisible(false);
            View.ShowCard(Strings.Get("card.losstitle"),
                new[] { Strings.Get("card.lossline") },
                new[] { Strings.Get("btn.retry"), Strings.Get("btn.flee") },
                new Action[] { () => StartBattle(_specs), () => OnDefeat?.Invoke() },
                new Color(1f, 0.6f, 0.6f));
        }
    }
}
