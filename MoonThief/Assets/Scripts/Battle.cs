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
            public SpriteRenderer Shadow, BarBg, BarFill, NameChip, Hat, Stun, WardMark, PoisonMark,
                SnareMark, WeakenMark, CorrodeMark;
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

        SpriteRenderer _backdrop, _floorTint, _hudPanel, _menuPanel, _msgPanel, _moonIcon, _targetChev, _turnChev, _nextChev;
        bool _autoOn;   // the AUTO chip's steering state, for the breathe in Update
        SpriteRenderer _bvig;
        float _hurtPulseT;   // HurtPulse owns the vignette color while it runs
        SpriteRenderer _autoChip;
        PixelLabel _hudNight, _hudRound, _hudFlow, _msg, _hint, _autoLabel;
        Transform _overlayRoot;
        SpriteRenderer _ovDim, _ovPanel;
        Transform _ovCard;             // panel + title + lines + buttons ride this; the dim snaps
        PixelLabel _ovTitle;
        List<PixelLabel> _ovLines = new List<PixelLabel>();
        List<(SpriteRenderer panel, PixelLabel text, Rect rect, Action act)> _ovButtons = new List<(SpriteRenderer, PixelLabel, Rect, Action)>();
        float _time;
        bool _menuOn;
        int _selCell;

        public bool MenuOn => _menuOn;
        // true while the battle line is still typing itself out - the menu can read "on"
        // underneath a message plate, so a flag alone never tells you what the eye sees
        public bool MessageRevealing => _msg != null && _msg.IsRevealing;
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

            // the same soft frame the night world wears, sized to the arena: it pulls
            // the eye off the edges without touching the HUD above it
            _bvig = SpriteRendererUtil.Make(Stage, "bvig", TexArt.Vignette(), 35);
            _bvig.transform.localPosition = new Vector3(0f, (ArenaTop + MenuTop) * 0.5f, 0f);
            _bvig.transform.localScale = new Vector3(18f, ArenaTop - MenuTop, 1f);
            _bvig.color = new Color(1f, 1f, 1f, 0.7f);

            _hudPanel = Sliced("hud", TexArt.Panel(), 46);
            Box(_hudPanel, Left, HudBottom, 18f, HudH, Color.white);

            _hudNight = Label("hudNight", 2, new Color(1f, 0.93f, 0.72f), TextAlign.Left, 50);
            _hudNight.transform.localPosition = new Vector3(Left + 0.45f, Top - 0.42f, 0f);

            _hudRound = Label("hudRound", 2, new Color(0.75f, 0.73f, 0.88f), TextAlign.Left, 50);
            // anchored LEFT at a fixed x right after NIGHT: a right-aligned round label
            // floated toward the night text whenever the flow suffix stretched it, so the
            // two ran together as "NIGHT 2R6 FLOW x2"
            _hudRound.transform.localPosition = new Vector3(Left + 6.3f, Top - 0.42f, 0f);
            // flow gets the HUD's spare second line instead of stretching the round counter:
            // "R1 FLOW x5" at scale 2 ran its tail into the AUTO chip
            _hudFlow = Label("hudFlow", 1, new Color(0.66f, 0.72f, 0.95f), TextAlign.Left, 50);
            // midway between the scale-2 round glyphs (~Top-1.35) and the hint row
            // (Top-2.13): the audit caught -1.72 shearing through both neighbours
            _hudFlow.transform.localPosition = new Vector3(Left + 6.3f, Top - 1.56f, 0f);

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
            // the spare third line of the bar: the scale-2 round glyphs hang to about
            // Top-1.4 and the flow counter owns Top-1.72, so -1.62 sheared through both
            _hint.transform.localPosition = new Vector3(Left + 0.45f, Top - 2.13f, 0f);
            _hint.MaxWidthUnits = 10.5f;
            _hint.Set(Strings.Get("bt.hint"));
            _hint.gameObject.SetActive(false);

            // AUTO chip: a standing toggle at the right end of the HUD bar. The 2x2 command
            // grid has no room for a fifth cell, so auto-battle lives up here instead - one
            // tap and the party fights itself until tapped again.
            _autoChip = Sliced("bauto", TexArt.Panel(), 55);
            Box(_autoChip, Right - 4.5f, Top - 2.28f, 3.6f, 1.5f, new Color(1f, 1f, 1f, 0.5f));
            _autoLabel = Label("bautoLabel", 1, new Color(0.8f, 0.83f, 1f), TextAlign.Center, 56);
            // clear of the moon icon: at Right-2.7 the label's top-right letter bled under
            // the icon's bottom-left rim and the O read as an 8
            _autoLabel.transform.localPosition = new Vector3(Right - 3.3f, Top - 2.13f, 0f);
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
            _nextChev = SpriteRendererUtil.Make(Stage, "bnext", TexArt.Chevron(), 36);
            _nextChev.transform.localEulerAngles = new Vector3(0f, 0f, 180f);
            _nextChev.color = new Color(0.75f, 0.78f, 0.9f, 0.85f);
            _nextChev.enabled = false;

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
            // centred rows, not a fixed grid: the thief alone stands mid-field before the
            // company forms, a pair splits the old edge slots, three keep the classic line
            if (n <= 1) return new[] { 0f };
            if (n == 2) return new[] { -2.4f, 2.4f };
            if (n == 3) return new[] { -4.2f, 0f, 4.2f };
            var xs = new float[n];
            for (int i = 0; i < n; i++) xs[i] = Mathf.Lerp(-5.6f, 5.6f, i / (n - 1f));
            return xs;
        }

        void BuildParty()
        {
            // the company as it stands tonight: amber always, sea and moss only after their
            // recruiting talks - a first-night ambush can find the thief still walking alone
            var specs = BattleData.PartyActive;
            // befriended beasts stand behind the heroes, up to the two-heart cap
            var friends = new List<MonsterSpec>();
            foreach (var key in Game.State.Friends)
            {
                // a moonlit catch keeps its shimmer: "moon." prefix, silver skin, a little
                // more bulk than the wild kind
                bool moonlit = key.StartsWith("moon.");
                var s = BattleData.Species(moonlit ? key.Substring(5) : key);
                if (s.HasValue) { var v = s.Value; v.Rare = moonlit; friends.Add(v); }
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
                        Speed = spec.Speed + Game.State.BonusSpd,
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
                        Rare = ms.Rare,
                        MaxHp = Mathf.RoundToInt(ms.Hp * (ms.Rare ? 1.2f : 1f)) + flv,
                        AtkMin = ms.AtkMin,
                        AtkMax = ms.AtkMax,
                        Speed = ms.Speed,
                        Style = 0,
                        BattlerPath = ms.Battler,
                        Scale = FitScale(Bank.One(ms.Battler), 3.2f, 2.8f, 2)
                    };
                    Party[i].Hp = Party[i].MaxHp;
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
                if (friend && Party[i].Rare) rig.Name.SetColor(new Color(0.72f, 0.9f, 1f));
                rig.NameChip = SpriteRendererUtil.Make(Stage, "pnameChip" + i, TexArt.Solid(), 23);
                Plate(rig.NameChip, rig.Name, Party[i].Name, false, Party[i].Species != null);
                rig.BarBg = SpriteRendererUtil.Make(Stage, "pbg" + i, TexArt.Solid(), 22);
                rig.BarFill = SpriteRendererUtil.Make(Stage, "pfill" + i, TexArt.Solid(), 23);
                rig.Anim.Play(friend
                    ? new[] { Bank.One(Party[i].BattlerPath) }
                    : Bank.Frames(BattleData.ClipPath(specs[i].ColorDir, "breath_idle")), 6f, true);
                if (friend && Party[i].Rare) rig.Sr.color = new Color(0.72f, 0.84f, 1f);
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
            if (rig.Stun != null) UtilDestroy(rig.Stun.gameObject);
            if (rig.WardMark != null) UtilDestroy(rig.WardMark.gameObject);
            if (rig.PoisonMark != null) UtilDestroy(rig.PoisonMark.gameObject);
            if (rig.SnareMark != null) UtilDestroy(rig.SnareMark.gameObject);
            if (rig.WeakenMark != null) UtilDestroy(rig.WeakenMark.gameObject);
            if (rig.CorrodeMark != null) UtilDestroy(rig.CorrodeMark.gameObject);
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
            // the daze star lives off the stage like the name plate: a KO'd fighter's
            // effects die with the rig, and this one must not linger in the air
            rig.Stun = SpriteRendererUtil.Make(Stage, "stun", TexArt.MenuIcon(21), 70);
            rig.Stun.enabled = false;
            // and the ward shield beside it: same chrome rules, a different omen
            rig.WardMark = SpriteRendererUtil.Make(Stage, "ward", TexArt.MenuIcon(18), 70);
            rig.WardMark.enabled = false;
            // venom gets the third omen: a green droplet off the right shoulder
            rig.PoisonMark = SpriteRendererUtil.Make(Stage, "poison", TexArt.MenuIcon(29), 70);
            rig.PoisonMark.enabled = false;
            // three more omens for the newer hexes: coil for snare, pale arrow for weaken,
            // rust fleck for corrode - same row over the name plate, their own slots
            rig.SnareMark = SpriteRendererUtil.Make(Stage, "snare", TexArt.MenuIcon(30), 70);
            rig.SnareMark.enabled = false;
            rig.WeakenMark = SpriteRendererUtil.Make(Stage, "weaken", TexArt.MenuIcon(31), 70);
            rig.WeakenMark.enabled = false;
            rig.CorrodeMark = SpriteRendererUtil.Make(Stage, "corrode", TexArt.MenuIcon(32), 70);
            rig.CorrodeMark.enabled = false;
            return rig;
        }

        static int FitScale(Sprite s, float maxW, float maxH, int pref)
        {
            // height caps matter as much as width: a wide-and-tall sheet (MushroomB)
            // hit its width limit and still filled the arena like a boss
            if (s == null) return 1;
            for (int k = pref; k >= 1; k--)
                if (s.bounds.size.x * k <= maxW && s.bounds.size.y * k <= maxH) return k;
            return 1;
        }

        public void SetBackdrop(string path, bool boss = false)
        {
            var sp = Bank.One(path);
            _backdrop.sprite = sp;
            // a gatekeeper casts the whole field into its cold violet
            _backdrop.color = boss ? new Color(0.62f, 0.55f, 0.8f) : Color.white;
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
                if (EnemyRigs[i].Stun != null) UtilDestroy(EnemyRigs[i].Stun.gameObject);
                if (EnemyRigs[i].WardMark != null) UtilDestroy(EnemyRigs[i].WardMark.gameObject);
                if (EnemyRigs[i].PoisonMark != null) UtilDestroy(EnemyRigs[i].PoisonMark.gameObject);
                if (EnemyRigs[i].SnareMark != null) UtilDestroy(EnemyRigs[i].SnareMark.gameObject);
                if (EnemyRigs[i].WeakenMark != null) UtilDestroy(EnemyRigs[i].WeakenMark.gameObject);
                if (EnemyRigs[i].CorrodeMark != null) UtilDestroy(EnemyRigs[i].CorrodeMark.gameObject);
            }

            Enemies = new Fighter[specs.Length];
            EnemyRigs = new Rig[specs.Length];
            for (int i = 0; i < specs.Length; i++)
            {
                var spec = specs[i];
                var battler = Bank.One(spec.Battler);
                // wild things get a little tougher as the party levels, so levelling
                // shortens a fight instead of making it meaningless; a retold night
                // (CONTINUE after the ending) bites a little deeper on top of that
                int elv = (Game.State.Level - 1) * 2;
                float nmul = 1f + 0.45f * Game.State.NgPlus;
                var f = new Fighter
                {
                    Id = "e" + i,
                    Name = Strings.Get(spec.Name),
                    Side = Side.Enemy,
                    MaxHp = Mathf.RoundToInt(spec.Hp * (spec.Rare ? 1.45f : 1f) * nmul) + elv,
                    AtkMin = Mathf.RoundToInt(spec.AtkMin * nmul),
                    AtkMax = Mathf.RoundToInt(spec.AtkMax * nmul),
                    Speed = spec.Speed,
                    Boss = spec.Boss,
                    Rare = spec.Rare,
                    Species = spec.Name,
                    BattlerPath = spec.Battler,
                    Scale = FitScale(battler,
                        spec.Boss ? 8.5f : spec.Rare ? 6.4f : 5.6f,
                        spec.Boss ? 7.8f : spec.Rare ? 5.0f : 3.6f, 2)
                };
                f.Hp = f.MaxHp;
                Enemies[i] = f;
                float x = specs.Length == 1 ? 0f
                    : specs.Length == 2 ? (i == 0 ? -4.4f : 4.4f)
                    : (i - 1) * 5.2f;   // three abreast: left, centre, right
                float y = HudBottom - (spec.Boss ? 8.6f : 6.2f);
                var home = new Vector3(x, y, 0f);
                var rig = MakeRig(f, home, 10 + i);
                if (f.Rare) rig.Sr.color = new Color(0.72f, 0.84f, 1f);
                rig.Shadow.transform.localScale = new Vector3(Mathf.Max(1f, f.Scale * 0.8f), 1f, 1f);
                rig.Anim.Play(new[] { battler }, 1f, true);
                rig.Sr.enabled = battler != null;
                rig.BodyHeight = battler != null ? battler.bounds.size.y * f.Scale : 2f;

                rig.BarBg = SpriteRendererUtil.Make(Stage, "ebg" + i, TexArt.Solid(), 6);
                rig.BarFill = SpriteRendererUtil.Make(Stage, "efill" + i, TexArt.Solid(), 7);
                rig.Name = Label("ename" + i, 1, f.Boss ? new Color(1f, 0.6f, 0.52f)
                    : f.Rare ? new Color(0.72f, 0.9f, 1f) : new Color(1f, 0.86f, 0.86f), TextAlign.Center, 8);
                // a floating/tall foe's name would sit inside the HUD strip -- but lowering it
                // onto the sprite leaves the body covering the label, so it moves under the foe's
                // HP bar (the bar sits at home.y-0.55 .. -0.35) instead
                float aboveHead = home.y + rig.BodyHeight + 1.35f;
                // a tall foe's head nearly touches the HUD strip, and the turn/next chevrons
                // hover a half-unit over it - a nameplate parked at +0.45 overlapped both, and
                // at +1.05 the plate's hanging text still brushed the chevron tops; it rides
                // well clear when there is room and drops under the sprite when not
                float nameTop = aboveHead <= HudBottom - 0.4f ? aboveHead : home.y - 1.35f;
                rig.Name.transform.localPosition = new Vector3(home.x, nameTop, 0f);
                rig.Name.Set(f.Name);
                // dark plate behind the name: the arena art has flat bright patches and light
                // text lying straight on top of them read as a smear
                rig.NameChip = SpriteRendererUtil.Make(Stage, "enameChip" + i, TexArt.Solid(), 7);
                Plate(rig.NameChip, rig.Name, f.Name, f.Boss);
                if (f.Rare) rig.Name.SetColor(new Color(0.72f, 0.84f, 1f));
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
                // a new duel is a clean slate: last fight's venom, hexes and coils stay there
                rig.F.Poison = 0;
                rig.F.Dazed = false;
                rig.F.Weaken = 0;
                rig.F.Snare = 0;
                rig.F.Corrode = 0;
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
                Co(Fx.MoveLocal(rig.Root, rig.Home, 0.35f));
            }
            for (int i = 0; i < EnemyRigs.Length; i++)
            {
                var off = new Vector3(i == 0 ? 4f : -4f, 0.8f, 0f);
                EnemyRigs[i].Root.localPosition = EnemyRigs[i].Home + off;
                // a pack arrives in order, not as a wall: each wild thing lands a
                // breath after the last
                Co(SlideLate(EnemyRigs[i].Root, EnemyRigs[i].Home, 0.4f, i * 0.13f));
            }
        }

        /// <summary>View-side routine gate: a queued battle callback (a float, a spark, a
        /// slide) can land after the ending or a teardown has already switched this stage
        /// off, and Unity logs an engine error for a coroutine started on a dead object.
        /// The coroutine never starts, so callers that spawned a visual must clean it up
        /// themselves when the gate says no.</summary>
        public Coroutine Co(System.Collections.IEnumerator r)
        {
            return gameObject.activeSelf ? StartCoroutine(r) : null;
        }

        IEnumerator SlideLate(Transform t, Vector3 to, float dur, float delay)
        {
            if (delay > 0f) yield return Fx.Wait(delay);
            yield return Fx.MoveLocal(t, to, dur);
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
                Co(CardSparkRoutine(go.transform, UnityEngine.Random.Range(0.9f, 1.6f)));
            }
        }

        IEnumerator CardSparkRoutine(Transform t, float dur)
        {
            var start = t.localPosition;
            var sr = t.GetComponent<SpriteRenderer>();
            float e = 0f;
            while (e < dur)
            {
                e += Time.deltaTime;
                float k = e / dur;
                t.localPosition = start + new Vector3(Mathf.Sin(k * 9f) * 0.3f, -2.5f * k, 0f);
                var c = sr.color;
                c.a = 1f - k;
                sr.color = c;
                yield return null;
            }
            UtilDestroy(t.gameObject);
        }

        public void SetNight(int chapter)
        {
            _hudNight.Set(Strings.Get("hud.nightshort", chapter)
                + (Game.State.NgPlus > 0 ? "+" : ""));
            // the ground answers the night too: violet dark in the wood, steel cold on
            // the plain, a drowned pale under the keep
            _floorTint.color = chapter >= 3 ? new Color32(14, 26, 30, 255)
                : chapter == 2 ? new Color32(15, 19, 36, 255)
                : new Color32(17, 14, 30, 255);
        }

        public void SetRound(int round, int flow = 0)
        {
            _hudRound.Set(Strings.Get("hud.round", round));
            _hudFlow.Set(flow >= 2 ? Strings.Get("hud.roundf", flow) : "");
            _flow = flow;   // the corner moon charges with it - see Tick's breathing tint
        }

        int _flow;

        /// <summary>The corner moon reads the moonflow, not just the night: dark and still
        /// at zero, warming toward gold as momentum builds, and breathing a brighter pulse
        /// once the MOON* commands are armed (flow >= 4). Sprite never swaps - it is the
        /// same moon the night number sits under, glowing by the same rules the fight does.</summary>
        void MoonflowTint()
        {
            if (_moonIcon == null) return;
            float warm = Mathf.Clamp01(_flow / 4f);
            float pulse = _flow >= 4 ? 0.5f + 0.5f * Mathf.Sin(Time.time * 5f) : 0f;
            float glow = 0.35f + 0.55f * warm + 0.3f * pulse;
            _moonIcon.color = new Color(1f, 0.92f - 0.12f * pulse, 0.62f + 0.3f * warm, Mathf.Min(1f, glow));
            _moonIcon.transform.localScale = Vector3.one * (2f + 0.15f * pulse);
        }

        /// <summary>The arena's edge bleeds red for a beat: a hit that lands on the
        /// party reads on the whole frame, not just the sprite that took it.</summary>
        public void HurtPulse()
        {
            if (_bvig == null || !Application.isPlaying) return;
            _hurtPulseT = 0.3f;
            Co(Fx.Tween(0.3f, k =>
            {
                if (_bvig != null)
                    _bvig.color = Color.Lerp(new Color(1f, 0.3f, 0.26f, 0.92f), new Color(1f, 1f, 1f, 0.7f), k);
            }));
        }

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

        Rig _turnRig, _nextRig;

        /// <summary>Marks whose move it is - a little chevron bobbing over their head,
        /// gold for ours, red for theirs.</summary>
        public void SetTurnRig(Rig rig, bool hostile = false)
        {
            _turnRig = rig;
            _turnChev.enabled = rig != null;
            _turnChev.color = hostile ? new Color(1f, 0.5f, 0.45f) : new Color(1f, 0.85f, 0.4f);
        }

        /// <summary>A dimmer silver chevron on whoever strikes next - the queue's edge
        /// is information a player can plan around.</summary>
        public void SetNextRig(Rig rig)
        {
            _nextRig = rig;
            _nextChev.enabled = rig != null;
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

        void TickStun(Rig rig)
        {
            if (rig?.Stun == null) return;
            bool on = rig.F != null && rig.F.Alive && rig.F.Dazed;
            rig.Stun.enabled = on;
            if (!on) return;
            // same altitude as the ward/poison omens: above the name plate, clear of it
            rig.Stun.transform.localPosition = new Vector3(rig.Home.x,
                rig.Home.y + rig.BodyHeight + 1.9f + Mathf.Sin(_time * 6f) * 0.08f, 0f);
            rig.Stun.transform.localEulerAngles = new Vector3(0f, 0f, _time * 240f);
            rig.Stun.transform.localScale = Vector3.one * (1.5f + Mathf.Sin(_time * 8f) * 0.15f);
        }

        void TickPoison(Rig rig)
        {
            if (rig?.PoisonMark == null) return;
            bool on = rig.F != null && rig.F.Alive && rig.F.Poison > 0;
            rig.PoisonMark.enabled = on;
            if (!on) return;
            // the omens ride above the name plate (+1.05 is the label's anchor, its text
            // hangs below it): at +0.5 the marks chewed through the plate and the chevrons
            rig.PoisonMark.transform.localPosition = new Vector3(rig.Home.x + 0.85f,
                rig.Home.y + rig.BodyHeight + 1.9f + Mathf.Sin(_time * 7f) * 0.11f, 0f);
            rig.PoisonMark.transform.localScale = Vector3.one * (1.2f + Mathf.Sin(_time * 9f) * 0.14f);
        }

        void TickWard(Rig rig)
        {
            if (rig?.WardMark == null) return;
            bool on = rig.F != null && rig.F.Alive && rig.F.Ward;
            rig.WardMark.enabled = on;
            if (!on) return;
            // it hovers off the shoulder, breathing - opposite the daze star so a fighter
            // could in principle carry both omens at once. Above the name plate (+1.9):
            // the PALE GUARD's shield used to sit inside its own label
            rig.WardMark.transform.localPosition = new Vector3(rig.Home.x - 0.85f,
                rig.Home.y + rig.BodyHeight + 1.9f + Mathf.Sin(_time * 5f) * 0.09f, 0f);
            rig.WardMark.transform.localScale = Vector3.one * (1.35f + Mathf.Sin(_time * 7f) * 0.12f);
        }

        /// <summary>Coil/hex/rust ride the same omen row as the rest, each at its own
        /// x so any mix of marks can stand together without stacking.</summary>
        void TickSnare(Rig rig)
        {
            if (rig?.SnareMark == null) return;
            bool on = rig.F != null && rig.F.Alive && rig.F.Snare > 0;
            rig.SnareMark.enabled = on;
            if (!on) return;
            rig.SnareMark.transform.localPosition = new Vector3(rig.Home.x + 1.3f,
                rig.Home.y + rig.BodyHeight + 1.9f + Mathf.Sin(_time * 7f) * 0.1f, 0f);
            rig.SnareMark.transform.localScale = Vector3.one * (1.25f + Mathf.Sin(_time * 8f) * 0.12f);
        }

        void TickWeaken(Rig rig)
        {
            if (rig?.WeakenMark == null) return;
            bool on = rig.F != null && rig.F.Alive && rig.F.Weaken > 0;
            rig.WeakenMark.enabled = on;
            if (!on) return;
            rig.WeakenMark.transform.localPosition = new Vector3(rig.Home.x - 0.35f,
                rig.Home.y + rig.BodyHeight + 1.9f + Mathf.Sin(_time * 6f) * 0.09f, 0f);
            rig.WeakenMark.transform.localScale = Vector3.one * (1.3f + Mathf.Sin(_time * 8f) * 0.12f);
        }

        void TickCorrode(Rig rig)
        {
            if (rig?.CorrodeMark == null) return;
            bool on = rig.F != null && rig.F.Alive && rig.F.Corrode > 0;
            rig.CorrodeMark.enabled = on;
            if (!on) return;
            rig.CorrodeMark.transform.localPosition = new Vector3(rig.Home.x + 0.35f,
                rig.Home.y + rig.BodyHeight + 1.9f + Mathf.Sin(_time * 8f) * 0.1f, 0f);
            rig.CorrodeMark.transform.localScale = Vector3.one * (1.25f + Mathf.Sin(_time * 9f) * 0.12f);
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
                if (n == "spark" || n == "cardspark" || n == "floatn" || n == "slash") UtilDestroy(Stage.GetChild(i).gameObject);
            }
        }

        void Update()
        {
            _time += Time.deltaTime;
            MoonflowTint();
            // the chip breathes while it steers: a static toggle can read as forgotten,
            // a warm pulse says the party is still fighting itself
            if (_autoOn && _autoChip != null && _autoChip.enabled)
                _autoChip.color = new Color(1f, 0.95f, 0.6f,
                    0.75f + 0.2f * Mathf.Sin(_time * 2.2f));
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
            {
                // a felled mark is no mark at all: the aim slides to whoever still stands
                if (!Enemies[_target].Alive)
                {
                    for (int i = 0; i < Enemies.Length; i++)
                        if (Enemies[i].Alive) { SetTarget(i); break; }
                }
                else PlaceTargetChev(EnemyRigs[_target], Mathf.Sin(_time * 5f) * 0.08f);
            }
            if (_turnChev.enabled && _turnRig != null)
            {
                var tr = _turnRig;
                _turnChev.transform.localPosition = new Vector3(tr.Home.x,
                    tr.Home.y + tr.BodyHeight + 0.5f + Mathf.Sin(_time * 5f) * 0.07f, 0f);
                _turnChev.transform.localScale = Vector3.one * 1.6f;
            }
            if (_nextRig != null && (_nextRig.F == null || !_nextRig.F.Alive))
                _nextChev.enabled = false;   // it fell before its turn came
            if (_nextChev.enabled && _nextRig != null)
            {
                var nr = _nextRig;
                _nextChev.transform.localPosition = new Vector3(nr.Home.x,
                    nr.Home.y + nr.BodyHeight + 0.34f + Mathf.Sin(_time * 4.2f) * 0.05f, 0f);
                _nextChev.transform.localScale = Vector3.one * 1.0f;
            }
            // the daze star spins over whoever took a slam last turn
            foreach (var rig in EnemyRigs) { TickStun(rig); TickWard(rig); TickPoison(rig); TickSnare(rig); TickWeaken(rig); TickCorrode(rig); }
            foreach (var rig in PartyRigs) { TickStun(rig); TickWard(rig); TickPoison(rig); TickSnare(rig); TickWeaken(rig); TickCorrode(rig); }
            // low blood: the arena's edge keeps a slow red breathe while a party member
            // is close to dropping, so the danger reads before the hp bar is even looked at
            if (_hurtPulseT > 0f) _hurtPulseT -= Time.deltaTime;
            else if (_bvig != null)
            {
                float worst = 1f;
                if (Party != null)
                    foreach (var f in Party) if (f.Alive) worst = Mathf.Min(worst, f.Hp / (float)f.MaxHp);
                var target = worst < 0.3f
                    ? new Color(1f, 0.45f, 0.38f, 0.62f + 0.22f * Mathf.Sin(_time * 3.1f))
                    : new Color(1f, 1f, 1f, 0.7f);
                _bvig.color = Color.Lerp(_bvig.color, target, Time.deltaTime * 2.6f);
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
                rig.Name.SetColor(!alive ? new Color(0.5f, 0.46f, 0.56f)
                    : rig.F.Hp01 <= 0.22f ? new Color(1f, 0.62f, 0.55f)   // hurt enough to worry: the name warms
                    : rig.F.Poison > 0 ? new Color(0.62f, 1f, 0.6f)       // venom green while it runs
                    : new Color(0.92f, 0.94f, 1f));
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
                    // the bar sells the body: a gatekeeper burns orange, a moonlit
                    // thing gleams the pale blue it wears in the dark
                    rig.BarCol = rig.F.Boss ? new Color32(255, 150, 110, 255)
                        : rig.F.Rare ? new Color32(140, 170, 255, 255)
                        : new Color32(232, 196, 120, 255);
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

        /// <summary>The NIGHT/ROUND strip is part of the fight too: a result card owns the
        /// whole stage, and a label or the moon icon left hanging over the card's edge is
        /// exactly the kind of text-on-plate seam a player reads as a bug.</summary>
        void SetHudVisible(bool on)
        {
            if (_hudPanel != null) _hudPanel.enabled = on;
            if (_hudNight != null) _hudNight.gameObject.SetActive(on);
            if (_hudRound != null) _hudRound.gameObject.SetActive(on);
            if (_hudFlow != null) _hudFlow.gameObject.SetActive(on);
            if (_moonIcon != null) _moonIcon.enabled = on;
            if (_autoChip != null) _autoChip.enabled = on;
            if (_autoLabel != null) _autoLabel.gameObject.SetActive(on);
        }

        public void ShowCard(string title, string[] lines, string[] buttons, Action[] actions, Color titleColor)
        {
            HideCard();
            _overlayRoot.gameObject.SetActive(true);
            SetFooterVisible(false);
            SetHudVisible(false);

            if (_ovDim == null)
            {
                _ovDim = SpriteRendererUtil.Make(_overlayRoot, "ovDim", TexArt.Solid(), 80);
                _ovDim.transform.localPosition = new Vector3(0f, (Top + Bottom) * 0.5f, 0f);
                _ovDim.transform.localScale = new Vector3(18f * 16f, (Top - Bottom) * 16f, 1f);
                _ovDim.color = new Color32(8, 6, 18, 215);
            }
            if (_ovCard == null)
            {
                _ovCard = new GameObject("ovCard").transform;
                _ovCard.SetParent(_overlayRoot, false);
            }
            if (_ovPanel == null) _ovPanel = SlicedUnder(_ovCard, "ovPanel", TexArt.Panel(), 81);

            float innerW = 15.0f;

            if (_ovTitle == null) _ovTitle = LabelUnder(_ovCard, "ovTitle", 3, Color.white, TextAlign.Center, 82);
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

            int btnN = buttons.Length;
            const float BtnW = 13.2f, BtnH = 1.75f, BtnGap = 0.5f;
            float buttonsH = btnN > 0 ? btnN * BtnH + (btnN - 1) * BtnGap : 0f;
            // The card lives between the HUD strip and the log box, so its room is fixed:
            // a card that outgrows it shrinks its body text to scale 1 rather than
            // letting the buttons spill past the panel.
            float room = (Top - 1.2f) - (MsgTop + 0.35f);

            var lineH = new float[lines.Length];
            float linesH = 0f, total = 0f;
            int bodyScale = 2;
            while (_ovLines.Count < lines.Length)
                _ovLines.Add(LabelUnder(_ovCard, "ovLine" + _ovLines.Count, 2, new Color(0.92f, 0.94f, 1f), TextAlign.Center, 82));
            while (true)
            {
                linesH = 0f;
                for (int i = 0; i < lines.Length; i++)
                {
                    var l = _ovLines[i];
                    l.Configure(bodyScale, new Color(0.92f, 0.94f, 1f), TextAlign.Center, 82);
                    l.MaxWidthUnits = innerW;
                    l.Set(lines[i]);
                    lineH[i] = string.IsNullOrEmpty(lines[i]) ? 0f : l.MeasureHeight(lines[i]);
                    if (lineH[i] > 0f) linesH += lineH[i] + (linesH > 0f ? 0.3f : 0f);
                }
                total = 1.4f * 2f + titleH + linesH + buttonsH
                            + (titleH > 0f && (linesH > 0f || buttonsH > 0f) ? 0.8f : 0f)
                            + (linesH > 0f && buttonsH > 0f ? 1.0f : 0f);
                if (total <= room || bodyScale == 1) break;
                bodyScale = 1;
            }
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
                var panel = SlicedUnder(_ovCard, "ovBtn" + i, TexArt.Panel(), 83);
                Box(panel, -BtnW * 0.5f, y - BtnH, BtnW, BtnH, new Color(1f, 1f, 0.95f, 0.95f));
                var text = LabelUnder(_ovCard, "ovBtnT" + i, 2, new Color(1f, 0.96f, 0.8f), TextAlign.Center, 84);
                text.transform.localPosition = new Vector3(0f, y - (BtnH - PixelFont.GlyphHUnits(2)) * 0.5f, 0f);
                text.Set(buttons[i]);
                _ovButtons.Add((panel, text, new Rect(-BtnW * 0.5f, y - BtnH, BtnW, BtnH), actions[i]));
                y -= BtnH + BtnGap;
            }

            // the verdict lands the way a card should: the dim snaps, the sheet settles
            if (Application.isPlaying && _ovCard != null)
            {
                _ovCard.localPosition = new Vector3(0f, 0.6f, 0f);
                Co(Fx.MoveLocal(_ovCard, Vector3.zero, 0.22f));
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
            SetHudVisible(true);
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
            // buttons ride the settling card: their rects are card-local, so the tap is
            // measured in the card's frame until it has finished dropping in
            if (_ovCard != null) w -= (Vector2)_ovCard.localPosition;
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
            _autoOn = on;
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

        public void FloatNumber(Vector2 pos, string text, Color color, int scale = 2)
        {
            var go = new GameObject("floatn");
            go.transform.SetParent(Stage, false);
            var label = go.AddComponent<PixelLabel>();
            // above the burst sparks (42): "WARDED" used to render behind its own shower
            label.Configure(scale, color, TextAlign.Center, 48);
            label.SnapToPixelGrid = false;
            // damage numbers drift over whatever art the arena uses; without a shadow they
            // vanish into the light patches
            label.Shadow = true;
            label.Set(text, true);
            // a fighter at the frame's edge used to spill the tail of a long word
            // ("PHASED THROUGH!") off-screen: centered text only needs half its width
            float halfW = label.MeasureWidth(text) * 0.5f;
            go.transform.localPosition = new Vector3(
                Mathf.Clamp(pos.x, Left + halfW + 0.1f, Right - halfW - 0.1f), pos.y, 0f);
            if (!Application.isPlaying) return;
            if (!gameObject.activeSelf) { UtilDestroy(go); return; }   // stage went dark mid-callback
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
                // numbers punch in swollen and settle in a blink - reads as impact,
                // not as a label drifting past
                float pop = 1f - Mathf.Clamp01(e / 0.16f);
                float s = 1f + pop * 0.55f;
                t.localScale = new Vector3(s, s, 1f);
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
                if (!gameObject.activeSelf) { UtilDestroy(go); continue; }   // stage went dark mid-callback
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
        public Action OnFled;               // party slipped out of a wild fight

        enum Ph { Idle, Intro, Round, Acting, Card }
        Ph _ph = Ph.Idle;
        readonly List<Fighter> _queue = new List<Fighter>();
        int _qi, _round = 1;
        MonsterSpec[] _specs;
        bool _morselUsed;
        bool _hinted;         // the pick-a-command line earns its keep once per fight
        bool _autoTame;       // auto-battle already spent its one catch try
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
        /// <summary>Selftest only: jump the moonflow so the pale MOONSTRIKE labels get
        /// photographed - real fights reach 4 so rarely the tint never made a shot.</summary>
        public int DebugFlow
        {
            set
            {
                _flow = Mathf.Clamp(value, 0, 9);
                View.SetRound(_round, _flow);
            }
        }

        public void StartBattle(MonsterSpec[] specs)
        {
            // a stale invocation - a ghost card's TRY AGAIN, a queued callback landing after
            // the stage came down - must not build rigs on a dead view: every coroutine it
            // launches would error against the inactive game object
            if (!View.gameObject.activeSelf) return;
            // every coroutine here belongs to the fight that was: a stale EnemyTurn resuming
            // after the roster is swapped dereferences rigs that no longer exist
            StopAllCoroutines();
            _specs = specs;
            _ph = Ph.Intro;
            _round = 1;
            _qi = 0;
            _morselUsed = false;
            _hinted = false;
            _autoTame = false;
            _befriended = 0;
            _enraged = false;
            _bossSpoke = false;
            _partySpoke = false;
            _flow = 0;
            _levelAtStart = Game.State.Level;
            AwaitingInput = false;
            // each night has its own ground: the hollow's woods, the long fields, the deep
            View.SetBackdrop(Game.State.Chapter >= 3 ? "Art/Backgrounds/DungeonA"
                : Game.State.Chapter == 2 ? "Art/Backgrounds/PlainA" : "Art/Backgrounds/ForestA",
                _hasBoss);
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
            _hasBoss = hasBoss;
            bool anyRare = false;
            foreach (var en in View.Enemies) if (en.Rare) anyRare = true;
            string introKey;
            if (hasBoss) introKey = "bt.boss";
            else if (anyRare) introKey = "bt.moonlit";
            else if (specs.Length > 2) introKey = "bt.three";
            else if (specs.Length > 1) introKey = "bt.two";
            else
            {
                // one wild thing introduces itself the way its kind would
                var fam0 = BattleData.FamilyOf(specs[0]);
                introKey = Strings.Has("bt.fam." + fam0) ? "bt.fam." + fam0 : "bt.one";
            }
            var first = Strings.Get(introKey, View.Enemies[0].Name);
            View.SetMessage(first);
            Sfx.Mus.Intensity = 1f;             // whatever the last fight left behind
            Sfx.Mus.Duck = 1f;                  // and the band comes back up for the fight
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
            // a card owns the fight once it is up: a queued round-start timer that fires
            // late (a second battle was staged over the first's timers) must not re-open play
            if (_ph == Ph.Card) return;
            _ph = Ph.Round;
            _queue.Clear();
            var all = new List<Fighter>(View.Party.Length + View.Enemies.Length);
            foreach (var f in View.Party) if (f.Alive) all.Add(f);
            foreach (var f in View.Enemies) if (f.Alive) all.Add(f);
            all.Sort((a, b) => b.Speed.CompareTo(a.Speed));
            _queue.AddRange(all);
            _qi = 0;
            View.SetRound(_round, _flow);
            NextTurn();
        }

        void NextTurn()
        {
            if (_ph == Ph.Card) return;
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
                if (f.Alive)
                {
                    if (!f.Dazed && f.Snare <= 0) break;
                    // a dazed fighter loses its turn once; a coiled one is held fast a while
                    bool snared = !f.Dazed && f.Snare > 0;
                    if (f.Dazed) f.Dazed = false; else f.Snare--;
                    var dRig = View.RigOf(f);
                    if (dRig != null)
                        View.FloatNumber(dRig.Home + new Vector3(0f, 1.6f, 0f),
                            Strings.Get(snared ? "bt.coiled" : "bt.dazed"),
                            snared ? new Color(0.75f, 0.95f, 0.6f) : new Color(1f, 0.9f, 0.5f));
                    _qi++;
                    continue;
                }
                _qi++;
            }
            var cur = _queue[_qi];
            // the silver chevron answers "who's after this one" before the turn plays out
            Fighter nxt = null;
            for (int i = _qi + 1; i < _queue.Count; i++)
                if (_queue[i].Alive && !_queue[i].Dazed && _queue[i].Snare <= 0) { nxt = _queue[i]; break; }
            View.SetNextRig(nxt != null ? View.RigOf(nxt) : null);
            if (cur.Side == Side.Party) BeginPlayerTurn(cur);
            else if (Application.isPlaying) StartCoroutine(EnemyTurn(cur));
            else EnemyTurnImmediate(cur);
        }

        void BeginPlayerTurn(Fighter f)
        {
            if (_ph == Ph.Card) return;   // the fight is over; the menu must stay down
            // a stung fighter bleeds before they can act - venom does not wait for the menu
            if (f.Poison > 0 && Application.isPlaying)
            {
                StartCoroutine(PoisonTick(f, () =>
                {
                    if (f.Alive) BeginPlayerTurn(f);   // survived the tick: act as normal
                    else EndTurn();                     // venom spent the turn (EndTurn spots a wipe)
                }));
                return;
            }
            View.SetTurnRig(View.RigOf(f));
            // the friends talk in a fight: the party's first turn of a fight always
            // carries one line so every brawl opens with a voice; after that it's
            // one in three - chatter, not a second log
            if (f.Species == null && Application.isPlaying
                && (!_partySpoke || UnityEngine.Random.value < 0.3f))
            {
                var bRig = View.RigOf(f);
                if (bRig != null)
                {
                    string who = f.Style == 0 ? "amber" : f.Style == 1 ? "sea" : "moss";
                    // same voice never says the same line twice running - a repeat reads
                    // as a stutter, not a personality
                    int bi = UnityEngine.Random.Range(0, 3);
                    if (who == _lastBarkWho && bi == _lastBarkIdx) bi = (bi + 1) % 3;
                    _lastBarkWho = who; _lastBarkIdx = bi;
                    View.FloatNumber(bRig.Home + new Vector3(0f, 2.15f, 0f),
                        Strings.Get("bk." + who + "." + bi), new Color(1f, 0.96f, 0.72f), 1);
                }
                _partySpoke = true;
            }
            // a befriended beast acts on its own - no command menu, it just helps
            if (f.Species != null && Application.isPlaying)
            {
                StartCoroutine(FriendTurn(f));
                return;
            }
            AwaitingInput = true;
            View.SetMenuVisible(true);
            View.SetSelected(0);
            // the how-to line rides the first menu of the fight, then yields the top
            // strip to the flow counter that later rounds light up there
            View.ShowHint(!_hinted);
            _hinted = true;
            // the strike button names the move this hero actually does: amber strikes,
            // sea sweeps the whole field, moss mends the hurtest friend. At high flow the
            // moon is already lending its weight (FlowMul) - say so on the button
            int style = f.Style;
            bool moonlit = _flow >= 4;
            string word = Strings.Get(
                moonlit ? (style == 1 ? "menu.msweep" : style == 2 ? "menu.mmend" : "menu.mstrike")
                        : (style == 1 ? "menu.sweep" : style == 2 ? "menu.mend" : "menu.strike"));
            View.Menu[0].Text.Set(word);
            // a MOON- word outgrows the cell's text span (MOON STRIKE is ~8.1 units into a
            // 6.25-unit slot and used to run into the neighbour's icon) - shrink the label's
            // transform just enough to stay inside its own cell
            float tw = PixelFont.Measure(word, View.Menu[0].Text.Scale).x;
            View.Menu[0].Text.transform.localScale = Vector3.one * Mathf.Min(1f, 6.1f / Mathf.Max(0.1f, tw));
            View.Menu[0].Text.SetColor(moonlit ? new Color(0.72f, 0.82f, 1f) : Color.white);
            View.Menu[0].Icon.sprite = TexArt.MenuIcon(style == 1 ? 13 : style == 2 ? 29 : 0);
            // commands that cannot fire go grey: morsel needs bag food and a fresh
            // portion, befriend needs room in the two-heart stable. The morsel cell
            // counts the portion it would serve - 'MORSEL x3' answers 'how many left'
            var food = Game.State.BestFood();
            View.SetCellEnabled(2, !_morselUsed && food != null);
            var morselWord = Strings.Get("menu.morsel")
                + (food != null ? " +" + Items.Get(food).Power + " x" + Game.State.BagCount(food) : "");
            float mw = PixelFont.Measure(morselWord, View.Menu[2].Text.Scale).x;
            View.Menu[2].Text.transform.localScale = Vector3.one * Mathf.Min(1f, 6.1f / Mathf.Max(0.1f, mw));
            View.Menu[2].Text.Set(morselWord);
            View.SetCellEnabled(1, Game.State.Friends.Count < 2 && !_hasBoss);
            View.SetCellEnabled(3, !_hasBoss);   // the Guard bars every way out
            View.AimStyle = f.Style;
            View.SetTarget(View.Target);
            View.SetMessage(Strings.Get("bt.yourturn", f.Name));
            // auto-battle acts after a short beat, so the player sees whose turn it was
            if (Auto && Application.isPlaying)
                StartCoroutine(Timer(0.45f, () => AutoPick(f)));
        }

        /// <summary>The auto-battle brain: mend anyone badly hurt if we still carry food,
        /// otherwise strike the weakest standing foe. A mender's ATTACK is its mend, so
        /// picking ATTACK already tends the party. Deliberately simple - it should feel
        /// like a sensible party, not a solver.</summary>
        void AutoPick(Fighter actor)
        {
            if (_ph == Ph.Card) return;   // the card ended the round while the pick timer was in flight
            if (!AwaitingInput || !Auto) return;   // a hand got there first
            bool hurt = false;
            foreach (var p in View.Party)
                // a full sting drains like a wound: mend it out before it ticks the fighter down
                if (p.Alive && (p.Hp01 < 0.45f || p.Poison >= 3)) hurt = true;
            // a mender's own ATTACK is its mend - it tends the party for free, so the
            // food stays in the bag
            if (hurt && actor.Style != 2 && !_morselUsed && Game.State.BestFood() != null)
            {
                View.SetSelected(2);
                Confirm();
                return;
            }
            // a moonlit wild thing is the night's prize: while a stable slot stands open
            // and the foe is softened, the party tries for the catch - once per fight
            if (!_autoTame && Game.State.Friends.Count < 2)
                for (int i = 0; i < View.Enemies.Length; i++)
                    if (View.Enemies[i].Alive && View.Enemies[i].Rare && View.Enemies[i].Hp01 < 0.6f
                        // a species already kept can't be caught twice - don't spend the one try on it
                        && !Game.State.Friends.Contains(View.Enemies[i].Species)
                        && !Game.State.Friends.Contains("moon." + View.Enemies[i].Species))
                    {
                        _autoTame = true;
                        View.SetTarget(i);
                        View.SetSelected(1);
                        Confirm();
                        return;
                    }
            int ti = -1; float low = float.MaxValue;
            // a ward drinks the whole swing: while a free target stands, spend hits there
            bool freeTarget = false;
            foreach (var e in View.Enemies) if (e.Alive && !e.Ward) freeTarget = true;
            // cut the foe this hero is built against first; else the most wounded
            for (int i = 0; i < View.Enemies.Length; i++)
                if (View.Enemies[i].Alive && WeakTo(actor.Style, View.Enemies[i])
                    && (!View.Enemies[i].Ward || !freeTarget)) { ti = i; break; }
            if (ti < 0)
                for (int i = 0; i < View.Enemies.Length; i++)
                    if (View.Enemies[i].Alive && View.Enemies[i].Hp < low
                        && (!View.Enemies[i].Ward || !freeTarget)) { low = View.Enemies[i].Hp; ti = i; }
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
            if (_ph == Ph.Card) yield break;
            _ph = Ph.Acting;
            var aRig = View.RigOf(f);
            View.SetTurnRig(aRig);
            // a befriended beast cannot talk, but it can chatter: a rare small sound
            // over its head so the pet reads as alive, not a spare weapon - same
            // no-repeat rule as the voices
            if (aRig != null && UnityEngine.Random.value < 0.18f)
            {
                int bi = UnityEngine.Random.Range(0, 3);
                if (_lastBarkWho == "pet" && bi == _lastBarkIdx) bi = (bi + 1) % 3;
                _lastBarkWho = "pet"; _lastBarkIdx = bi;
                View.FloatNumber(aRig.Home + new Vector3(0f, 2.15f, 0f),
                    Strings.Get("bk.pet." + bi),
                    new Color(0.8f, 1f, 0.9f), 1);
            }
            int ti = -1; float low = float.MaxValue;
            for (int i = 0; i < View.Enemies.Length; i++)
                if (View.Enemies[i].Alive && View.Enemies[i].Hp < low) { low = View.Enemies[i].Hp; ti = i; }
            if (ti < 0) { EndTurn(); yield break; }
            var target = View.Enemies[ti];
            var tRig = View.RigOf(target);
            // the first turn each fight introduces the pet; every later one just acts -
            // the chevron over its head already says whose go it is
            if (!f.Announced)
            {
                f.Announced = true;
                View.SetMessage(Strings.Get("bt.friendturn", f.Name));
                yield return Fx.Wait(0.4f);
            }
            yield return Lunge(aRig, tRig != null ? tRig.Home : aRig.Home, 0.3f);
            bool crit = UnityEngine.Random.value < 0.18f;
            bool weakHit = WeakTo(0, target);
            int dmg = Mathf.RoundToInt(UnityEngine.Random.Range(f.AtkMin, f.AtkMax + 1) * (crit ? 1.6f : 1f) * (weakHit ? 1.5f : 1f) * FlowMul());
            // a hex saps the arm it fell on: blows come out dull until it lifts
            if (f.Weaken > 0) { dmg = Mathf.Max(1, dmg - 4); f.Weaken--; }
            // a worm's rot keeps rusting the arm it bit: each stack stays until the fight ends
            if (f.Corrode > 0) dmg = Mathf.Max(1, dmg - f.Corrode);
            var fFam = f.Species != null && BattleData.Species(f.Species).HasValue
                ? BattleData.FamilyOf(BattleData.Species(f.Species).Value) : "";
            int hpBefore = target.Hp;
            HitFoe(target, dmg, crit, weakHit, f);
            // a tame thirstling drinks for you too: whatever the blow actually landed,
            // half of it comes home to the drinker
            if (fFam == "succubus" && f.Alive && target.Hp < hpBefore)
            {
                int sip = Mathf.Max(1, (hpBefore - target.Hp) / 2);
                f.Hp = Mathf.Min(f.MaxHp, f.Hp + sip);
                if (aRig != null) View.FloatNumber(aRig.Home + new Vector3(0f, 1.7f, 0f),
                    "+" + sip, new Color(0.55f, 1f, 0.6f));
            }
            View.Refresh();
            // a scorpion friend carries its sting over to your side: its bite
            // can leave the same venom the wild ones leave in you
            if (fFam == "scorpion" && target.Alive && UnityEngine.Random.value < 0.4f)
            {
                target.Poison = 3;
                View.FloatNumber(tRig.Home + new Vector3(0f, 1.75f, 0f),
                    Strings.Get("bt.poisoned"), new Color(0.55f, 1f, 0.5f));
            }
            // a tame worm's fangs still rust: its bite rots the foe's arm the same way
            if (fFam == "worm" && target.Alive && UnityEngine.Random.value < 0.35f)
            {
                target.Corrode++;
                Sfx.Play("venom");
                View.FloatNumber(tRig.Home + new Vector3(0f, 1.9f, 0f),
                    Strings.Get("bt.corroded"), new Color(0.8f, 0.55f, 0.3f));
            }
            // a tame slime still smothers: its goo can take the foe's footing too
            if (fFam == "slime" && target.Alive && !target.Dazed && UnityEngine.Random.value < 0.2f)
            {
                target.Dazed = true;
                View.FloatNumber(tRig.Home + new Vector3(0f, 1.9f, 0f),
                    Strings.Get("bt.tripped"), new Color(0.6f, 0.85f, 1f));
            }
            // a mushroom friend breathes its spores into the wound instead of waiting
            // to be struck: lighter venom than a scorpion's, but on your side
            if (fFam == "mushroom" && target.Alive && target.Poison <= 0 && UnityEngine.Random.value < 0.25f)
            {
                target.Poison = 2;
                View.FloatNumber(tRig.Home + new Vector3(0f, 1.75f, 0f),
                    Strings.Get("bt.poisoned"), new Color(0.55f, 1f, 0.5f));
            }
            // a tame magus keeps its hex: its mark saps the foe's arm for a while
            if (fFam == "blackmagus" && target.Alive && target.Weaken <= 0 && UnityEngine.Random.value < 0.3f)
            {
                target.Weaken = 2;
                View.FloatNumber(tRig.Home + new Vector3(0f, 1.9f, 0f),
                    Strings.Get("bt.hexed"), new Color(0.8f, 0.6f, 1f));
            }
            // a befriended lamia still coils: it holds the mark fast past a turn or two
            if (fFam == "lamia" && target.Alive && target.Snare <= 0 && UnityEngine.Random.value < 0.3f)
            {
                target.Snare = target.Boss ? 1 : 2;
                View.FloatNumber(tRig.Home + new Vector3(0f, 1.9f, 0f),
                    Strings.Get("bt.coiled"), new Color(0.75f, 0.95f, 0.6f));
            }
            // a wisp keeps its lantern habit: its first turn each duel wraps a thin
            // ward around the frailest friend standing - one light, one drink
            // (the family of "GeniusA" is "genius" - the wisp's own kind)
            if (fFam == "genius" && !f.WardGiven)
            {
                f.WardGiven = true;   // one gift of light per fight
                Fighter frail = null;
                foreach (var p in View.Party)
                    if (p.Alive && p != f && !p.Ward && (frail == null || p.Hp01 < frail.Hp01)) frail = p;
                if (frail != null)
                {
                    frail.Ward = true;
                    var wRig = View.RigOf(frail);
                    if (wRig != null)
                        View.FloatNumber(wRig.Home + new Vector3(0f, 1.9f, 0f),
                            Strings.Get("bt.warded"), new Color(1f, 0.9f, 0.5f));
                    Sfx.Play("befriend");
                }
            }
            yield return Fx.Wait(0.45f);
            yield return Lunge(aRig, aRig.Home, 0.3f);
            PlayIdle(aRig);
            if (!target.Alive)
            {
                yield return FadeOut(tRig);
                View.SetMessage(Strings.Get("bt.fainted", target.Name));
                yield return Fx.Wait(0.6f);
                OnFighterDown(target);
                TrySpore(f, target);   // pets get the same lungful of spores as heroes
            }
            EndTurn();
        }

        IEnumerator EnemyTurn(Fighter e)
        {
            if (_ph == Ph.Card) yield break;
            // a turn queued by a retired encounter must not act in this one - its fighter
            // is not on the roster, so its rig lookups come back null
            bool current = false;
            foreach (var f in View.Enemies) if (f == e) { current = true; break; }
            if (!current) yield break;
            // venom works on the wild things too: a befriended stinger turns
            // their own trick on them, ticking before the creature can act
            if (e.Poison > 0 && Application.isPlaying)
            {
                StartCoroutine(PoisonTick(e, () =>
                {
                    if (e.Alive) StartCoroutine(EnemyTurn(e));
                    else EndTurn();
                }));
                yield break;
            }
            _ph = Ph.Acting;
            View.SetTurnRig(View.RigOf(e), true);
            // every keeper opens its mouth once: the first turn always carries a line,
            // after that a growl lands one time in three - and never the same growl twice
            if (e.Boss && (!_bossSpoke || UnityEngine.Random.value < 0.3f))
            {
                _bossSpoke = true;
                var kRig = View.RigOf(e);
                int bi = UnityEngine.Random.Range(0, 3);
                if (bi == _lastBarkIdx && _lastBarkWho == "boss") bi = (bi + 1) % 3;
                _lastBarkWho = "boss"; _lastBarkIdx = bi;
                if (kRig != null)
                    View.FloatNumber(kRig.Home + new Vector3(0f, 2.3f, 0f),
                        Strings.Get("bk.boss." + Mathf.Clamp(Game.State.Chapter, 1, 3) + "." + bi),
                        new Color(1f, 0.75f, 0.7f), 1);
            }
            // a wasp on its last legs would rather live elsewhere: under a quarter of
            // its bar it may quit the field entirely - dive-bomber, not a martyr
            var fam0 = e.Species != null && BattleData.Species(e.Species).HasValue
                ? BattleData.FamilyOf(BattleData.Species(e.Species).Value) : "";
            if (fam0 == "wasp" && !e.Boss && e.Hp01 < 0.25f && UnityEngine.Random.value < 0.35f)
            {
                var wRig = View.RigOf(e);
                View.SetMessage(Strings.Get("bt.slipped", e.Name));
                yield return Fx.Wait(0.35f);
                e.Captured = true;   // gone like a catch, minus the pet: it left on its own
                e.Hp = 0;
                if (wRig != null) yield return FadeOut(wRig);
                View.Refresh();
                yield return Fx.Wait(0.5f);
                EndTurn();
                yield break;
            }
            // cornered, the guard loses its patience: under a third of its bar it rears
            // up far more often and the blows land heavier
            if (e.Boss && e.Hp01 < 0.35f && !_enraged)
            {
                _enraged = true;
                Sfx.Mus.Intensity = 1.14f;      // the track quickens with the guard's temper
                View.SetMessage(Strings.Get("bt.enraged", e.Name));
                var rig = View.RigOf(e);
                if (rig != null)
                {
                    StartCoroutine(Fx.FlashTint(rig.Anim, new Color(1f, 0.3f, 0.2f), 3, 0.1f, 0.1f));
                    StartCoroutine(Fx.Shake(View.Stage, 0.2f, 0.35f));
                }
                Sfx.Play("boss");
                yield return Fx.Wait(1.1f);
                // the heat does not fade with the flash: the guard stays reddened until it falls
                if (rig != null && rig.Anim != null)
                {
                    var c = rig.Anim.Tint;
                    rig.Anim.SetTint(new Color(Mathf.Min(1f, c.r + 0.25f), c.g * 0.55f, c.b * 0.55f));
                }
            }
            // the shade squire is no brawler: while its thane stands it weaves health
            // back into the split helm - killing the page first IS the night-two fight
            if (e.Species == "mon.squire")
            {
                Fighter liege = null;
                foreach (var f in View.Enemies)
                    if (f.Alive && f.Species == "mon.thane") liege = f;
                if (liege != null && liege.Hp < liege.MaxHp && UnityEngine.Random.value < 0.7f)
                {
                    var sRig = View.RigOf(e);
                    var mRig = View.RigOf(liege);
                    View.SetMessage(Strings.Get("bt.squiremend", e.Name));
                    yield return Fx.Wait(0.4f);
                    if (sRig != null && mRig != null)
                        yield return Lunge(sRig, mRig.Home, 0.3f);
                    int mend = UnityEngine.Random.Range(6, 11);
                    liege.Hp = Mathf.Min(liege.MaxHp, liege.Hp + mend);
                    if (mRig != null)
                    {
                        View.FloatNumber(mRig.Home + new Vector3(0f, 1.5f, 0f),
                            "+" + mend, new Color(0.6f, 1f, 0.7f));
                        StartCoroutine(Fx.FlashTint(mRig.Anim, new Color(0.55f, 1f, 0.6f), 2, 0.09f, 0.09f));
                    }
                    Sfx.Play("heal");
                    View.Refresh();
                    yield return Fx.Wait(0.5f);
                    if (sRig != null) yield return Lunge(sRig, sRig.Home, 0.3f);
                    EndTurn();
                    yield break;
                }
            }
            // the lantern wisp is the Guard's lantern: it wreathes its keeper in light
            // that swallows the next blow whole - break the wisp to break the ward
            if (e.Species == "mon.wisp")
            {
                // the wisp's light goes where it matters: to the Guard first, and in
                // a wild pack to whichever companion still stands
                Fighter guard = null;
                foreach (var f in View.Enemies)
                    if (f.Alive && f.Species == "mon.minotaur") guard = f;
                if (guard == null)
                {
                    var others = new List<Fighter>();
                    foreach (var f in View.Enemies)
                        if (f.Alive && f != e && !f.Ward) others.Add(f);
                    if (others.Count > 0) guard = others[UnityEngine.Random.Range(0, others.Count)];
                }
                if (guard != null && !guard.Ward && UnityEngine.Random.value < 0.55f)
                {
                    var wRig0 = View.RigOf(e);
                    var gRig = View.RigOf(guard);
                    View.SetMessage(Strings.Get(guard.Boss ? "bt.ward" : "bt.ward2", e.Name, guard.Name));
                    yield return Fx.Wait(0.4f);
                    if (wRig0 != null && gRig != null)
                        yield return Lunge(wRig0, gRig.Home, 0.3f);
                    guard.Ward = true;
                    if (gRig != null)
                    {
                        View.FloatNumber(gRig.Home + new Vector3(0f, 1.9f, 0f),
                            Strings.Get("bt.warded"), new Color(1f, 0.9f, 0.5f));
                        View.Sparkle(gRig.Home + new Vector3(0f, 1.1f, 0f), new Color(1f, 0.9f, 0.45f), 10);
                    }
                    Sfx.Play("heal");
                    View.Refresh();
                    yield return Fx.Wait(0.5f);
                    if (wRig0 != null) yield return Lunge(wRig0, wRig0.Home, 0.3f);
                    EndTurn();
                    yield break;
                }
            }
            bool slam = e.Boss && UnityEngine.Random.value < (_enraged ? 0.45f : 0.35f);
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
                bool frail = fam == "wasp" || fam == "scorpion" || fam == "succubus";
                if (frail ? p.Hp < target.Hp : p.Hp > target.Hp) target = p;
            }
            if (target == null) { Lose(); yield break; }

            var eRig = View.RigOf(e);
            var tRig = View.RigOf(target);
            // the target's own bloodline answers too: a befriended ghost keeps the
            // sideways step it had when it was wild
            var tFam = target.Species != null && BattleData.Species(target.Species).HasValue
                ? BattleData.FamilyOf(BattleData.Species(target.Species).Value) : "";
            // a blink of warning before the blow: the chosen one flushes cold for a beat
            if (tRig?.Anim != null)
                StartCoroutine(Fx.FlashTint(tRig.Anim, new Color(0.55f, 0.7f, 1f), 1, 0.14f, 0.14f));
            yield return Fx.Wait(0.18f);
            yield return Lunge(eRig, tRig.Home, 0.3f);

            int dmg = Mathf.RoundToInt(UnityEngine.Random.Range(e.AtkMin, e.AtkMax + 1)
                * (slam ? 1.6f : 1f) * (_enraged ? 1.25f : 1f) * (Prefs.Story ? 0.65f : 1f));
            // a hexed enemy strikes dull too: a tame magus's mark works both ways
            if (e.Weaken > 0) { dmg = Mathf.Max(1, dmg - 4); e.Weaken--; }
            if (e.Corrode > 0) dmg = Mathf.Max(1, dmg - e.Corrode);
            if (UnityEngine.Random.value < (tFam == "ghost" ? 0.2f : 0.06f))
            {
                // the hero slips aside: the lunge lands on empty air
                View.FloatNumber(tRig.Home + new Vector3(0f, 1.4f, 0f),
                    Strings.Get(tFam == "ghost" ? "bt.phased" : "bt.miss"), new Color(0.8f, 0.85f, 0.95f));
                Sfx.Play("whoosh");
                yield return Fx.Wait(0.35f);
                yield return Lunge(eRig, eRig.Home, 0.3f);
                EndTurn();
                yield break;
            }
            // a befriended blade pudding keeps its swordsman's edge: now and then it
            // turns the whole blow aside and nicks the striker back
            if (tFam == "slimesword" && UnityEngine.Random.value < 0.22f)
            {
                e.Hp = Mathf.Max(1, e.Hp - 2);   // a graze, never a kill
                if (eRig != null) View.FloatNumber(eRig.Home + new Vector3(0f, 1.6f, 0f),
                    "-2", new Color(0.85f, 0.9f, 1f));
                View.FloatNumber(tRig.Home + new Vector3(0f, 1.4f, 0f),
                    Strings.Get("bt.parried"), new Color(0.85f, 0.9f, 1f));
                Sfx.Play("whoosh");
                yield return Fx.Wait(0.35f);
                yield return Lunge(eRig, eRig.Home, 0.3f);
                EndTurn();
                yield break;
            }
            // a befriended warden keeps its plate on your side too
            if (tFam == "skeletonwarrior" && dmg > 1)
            {
                dmg = Mathf.Max(1, dmg - 2);
                View.FloatNumber(tRig.Home + new Vector3(0f, 1.75f, 0f),
                    Strings.Get("bt.plated"), new Color(0.8f, 0.85f, 0.9f));
            }
            target.Hp = Mathf.Max(0, target.Hp - dmg);
            // a thirstling keeps half of whatever it takes: the kiss closes its own wounds
            if (fam == "succubus" && dmg > 0 && e.Alive)
            {
                int sip = Mathf.Max(1, dmg / 2);
                e.Hp = Mathf.Min(e.MaxHp, e.Hp + sip);
                if (eRig != null) View.FloatNumber(eRig.Home + new Vector3(0f, 1.7f, 0f),
                    "+" + sip, new Color(0.55f, 1f, 0.6f));
            }
            if (_flow != 0) { _flow = 0; View.SetRound(_round); }   // momentum breaks on a hit taken
            // hero bodies carry a hit clip; befriended monsters don't - they flash instead
            var stagger = target.ColorDir != null
                ? Bank.Frames(BattleData.ClipPath(target.ColorDir, "hit"))
                : new Sprite[0];
            if (stagger.Length > 0) tRig.Anim.Play(stagger, 14f, false);
            else StartCoroutine(Fx.FlashTint(tRig.Anim, new Color(1f, 0.45f, 0.45f), 2, 0.08f, 0.08f));
            StartCoroutine(Fx.Shake(tRig.Root, slam ? 0.22f : 0.14f, 0.25f));
            StartCoroutine(Fx.Slash(View.Stage, tRig.Home + new Vector3(0f, 0.85f, 0f),
                new Color(1f, 0.55f, 0.45f, 0.85f), slam ? 1.4f : 1f));
            if (slam) StartCoroutine(Fx.Shake(View.Stage, 0.15f, 0.2f));
            if (target.ColorDir != null) View.HurtPulse();   // heroes bleed the frame edge
            if (slam && target.Alive && UnityEngine.Random.value < 0.15f)
            {
                target.Dazed = true;
                View.FloatNumber(tRig.Home + new Vector3(0f, 1.9f, 0f),
                    Strings.Get("bt.dazed"), new Color(1f, 0.9f, 0.5f));
            }
            // a scorpion's sting keeps working after the blow: three rounds of venom
            if (fam == "scorpion" && target.Alive && UnityEngine.Random.value < 0.4f)
            {
                target.Poison = 3;
                View.FloatNumber(tRig.Home + new Vector3(0f, 1.75f, 0f),
                    Strings.Get("bt.poisoned"), new Color(0.55f, 1f, 0.5f));
            }
            // a slime's goo clings where it lands: the victim's footing goes, and a
            // tripped fighter loses their next turn to the stars
            if (fam == "slime" && target.Alive && !target.Dazed && UnityEngine.Random.value < 0.2f)
            {
                target.Dazed = true;
                View.FloatNumber(tRig.Home + new Vector3(0f, 1.9f, 0f),
                    Strings.Get("bt.tripped"), new Color(0.6f, 0.85f, 1f));
            }
            // a magus's mark saps the arm it lands on: blows come out dull for a while
            if (fam == "blackmagus" && target.Alive && target.Weaken <= 0 && UnityEngine.Random.value < 0.3f)
            {
                target.Weaken = 2;
                View.FloatNumber(tRig.Home + new Vector3(0f, 1.9f, 0f),
                    Strings.Get("bt.hexed"), new Color(0.8f, 0.6f, 1f));
            }
            // a lamia's coil holds fast: the marked fighter loses turns to the squeeze
            if (fam == "lamia" && target.Alive && target.Snare <= 0 && UnityEngine.Random.value < 0.3f)
            {
                target.Snare = target.Boss ? 1 : 2;
                View.FloatNumber(tRig.Home + new Vector3(0f, 1.9f, 0f),
                    Strings.Get("bt.coiled"), new Color(0.75f, 0.95f, 0.6f));
            }
            // a tunnel worm's fangs rust what they pierce: each stack dulls the victim's
            // blows until the fight is done
            if (fam == "worm" && target.Alive && UnityEngine.Random.value < 0.35f)
            {
                target.Corrode++;
                Sfx.Play("venom");
                View.FloatNumber(tRig.Home + new Vector3(0f, 1.9f, 0f),
                    Strings.Get("bt.corroded"), new Color(0.8f, 0.55f, 0.3f));
            }
            View.FloatNumber(tRig.Home + new Vector3(0f, 1.4f, 0f), "-" + dmg,
                slam ? new Color(1f, 0.45f, 0.3f) : new Color(1f, 0.6f, 0.55f));
            Sfx.Play("hurt");
            View.Refresh();
            yield return Fx.Wait(0.4f);
            yield return Lunge(eRig, eRig.Home, 0.3f);

            if (!target.Alive)
            {
                // a befriended skeleton remembers its own road too: down once, up once
                if (IsUnrisenSkeleton(target)) { yield return FriendRise(target, tRig); }
                else
                {
                    OnFighterDown(target);
                    yield return FadeOut(tRig, true);
                    Sfx.Play("faint");
                    View.SetMessage(Strings.Get(target.Species != null ? "bt.fainted" : "bt.herodown", target.Name));
                    yield return Fx.Wait(0.8f);
                }
            }
            else PlayIdle(tRig);
            EndTurn();
        }

        /// <summary>The venom tick at a party fighter's turn start: it always costs the
        /// turn's opening beat, and it can drop a fighter before they ever act.</summary>
        IEnumerator PoisonTick(Fighter f, System.Action done)
        {
            if (_ph == Ph.Card) { done?.Invoke(); yield break; }
            f.Poison--;
            var rig = View.RigOf(f);
            if (rig != null)
            {
                StartCoroutine(Fx.FlashTint(rig.Anim, new Color(0.5f, 1f, 0.5f), 2, 0.1f, 0.1f));
                View.FloatNumber(rig.Home + new Vector3(0f, 1.4f, 0f), "-2", new Color(0.55f, 1f, 0.5f));
                View.FloatNumber(rig.Home + new Vector3(0f, 1.9f, 0f),
                    Strings.Get("bt.poison"), new Color(0.55f, 1f, 0.5f));
            }
            Sfx.Play("venom");   // a sizzle, not a smack: the venom does its own work
            f.Hp = Mathf.Max(0, f.Hp - 2);
            View.Refresh();
            yield return Fx.Wait(0.5f);
            if (!f.Alive)
            {
                // the bones keep their promise even to venom: down once, up once
                var pfam = f.Species != null && BattleData.Species(f.Species).HasValue
                    ? BattleData.FamilyOf(BattleData.Species(f.Species).Value) : "";
                if (pfam == "skeleton" && !f.Boss && !f.Risen)
                {
                    f.Risen = true;
                    f.Hp = Mathf.Max(1, Mathf.RoundToInt(f.MaxHp * 0.4f));
                    if (rig != null)
                        View.FloatNumber(rig.Home + new Vector3(0f, 1.9f, 0f),
                            Strings.Get("bt.rises"), new Color(0.9f, 0.9f, 1f));
                    Sfx.Play("enemy");
                    View.Refresh();
                    yield return Fx.Wait(0.7f);
                    done();
                    yield break;
                }
                OnFighterDown(f);
                if (rig != null) yield return FadeOut(rig, true);
                Sfx.Play("faint");
                View.SetMessage(Strings.Get(f.Species != null ? "bt.fainted" : "bt.herodown", f.Name));
                yield return Fx.Wait(0.7f);
            }
            done();
        }

        void EnemyTurnImmediate(Fighter e)
        {
            // deterministic path for static previews
            Fighter target = null;
            var fam = e.Species != null && BattleData.Species(e.Species).HasValue
                ? BattleData.FamilyOf(BattleData.Species(e.Species).Value) : "";
            // the swarm-minded finish the weak, the thirsty take the easiest drink:
            // wasp, scorpion and thirstling all hunt the lowest bar
            bool frail = fam == "wasp" || fam == "scorpion" || fam == "succubus";
            foreach (var p in View.Party)
            {
                if (!p.Alive) continue;
                if (target == null || (frail ? p.Hp < target.Hp : p.Hp > target.Hp)) target = p;
            }
            if (target != null)
            {
                int dmg = UnityEngine.Random.Range(e.AtkMin, e.AtkMax + 1);
                if (Prefs.Story) dmg = Mathf.Max(1, Mathf.RoundToInt(dmg * 0.65f));
                target.Hp = Mathf.Max(0, target.Hp - dmg);
                _flow = 0;
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

        bool _enraged;
        bool _hasBoss;
        bool _bossSpoke;   // the keeper's guaranteed first line is spent
        bool _partySpoke;  // the party's opening line is spent
        string _lastBarkWho = "";
        int _lastBarkIdx = -1;   // bark memory: no voice repeats its own last line

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
                default: StartCoroutine(PlayerFlee(actor)); break;
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
                    bool mendCrit = UnityEngine.Random.value < 0.2f;
                    int heal = Mathf.Max(4, (actor.AtkMin + actor.AtkMax) / 2 + Game.State.Level);
                    if (mendCrit) heal = Mathf.RoundToInt(heal * 1.6f);
                    weak.Hp = Mathf.Min(weak.MaxHp, weak.Hp + heal);
                    bool cured = weak.Poison > 0;
                    weak.Poison = 0;    // a mender's touch draws the venom out with the wound
                    View.SetMessage(Strings.Get(mendCrit ? "bt.attack.2c" : cured ? "bt.attack.2x" : "bt.attack.2", actor.Name, weak.Name));
                    if (wRig != null)
                    {
                        View.Sparkle(wRig.Home + new Vector3(0f, 1.1f, 0f), new Color(0.6f, 1f, 0.75f), mendCrit ? 14 : 8);
                        View.FloatNumber(wRig.Home + new Vector3(0f, 1.2f, 0f), Strings.Get("bt.heal", heal), new Color(0.7f, 1f, 0.7f), mendCrit ? 3 : 2);
                    }
                    Sfx.Play("heal");
                    View.Refresh();
                    yield return Fx.Wait(0.6f);
                    yield return Lunge(aRig, aRig.Home, 0.3f);
                    PlayIdle(aRig);
                    EndTurn();
                    yield break;
                }
                // nobody bleeding: a stung friend still needs moss before the venom ticks again
                Fighter ill = null;
                foreach (var p in View.Party)
                    if (p.Alive && p.Poison > 0) { ill = p; break; }
                if (ill != null)
                {
                    var iRig = View.RigOf(ill);
                    yield return Lunge(aRig, iRig != null ? iRig.Home : aRig.Home, 0.3f);
                    ill.Poison = 0;
                    View.SetMessage(Strings.Get("bt.cleanse", actor.Name, ill.Name));
                    if (iRig != null)
                        View.Sparkle(iRig.Home + new Vector3(0f, 1.1f, 0f), new Color(0.6f, 1f, 0.75f), 8);
                    Sfx.Play("heal");
                    View.Refresh();
                    yield return Fx.Wait(0.55f);
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
                int pebble = Mathf.Max(1, Mathf.RoundToInt(UnityEngine.Random.Range(actor.AtkMin, actor.AtkMax + 1) * (weakHit ? 0.75f : 0.5f) * FlowMul()));
                HitFoe(target, pebble, false, weakHit, actor);
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
                    HitFoe(View.Enemies[i], Mathf.Max(1, Mathf.RoundToInt(roll * (weakHit ? 0.975f : 0.65f) * FlowMul())), false, weakHit, actor);
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
                    View.SetMessage(felled > 1
                        ? Strings.Get("bt.fellpack", felled)
                        : Strings.Get("bt.fainted", last));
                    yield return Fx.Wait(0.6f);
                }
                for (int i = 0; i < View.Enemies.Length; i++)
                    if (!View.Enemies[i].Alive) TrySpore(actor, View.Enemies[i]);
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
                int dmg = Mathf.RoundToInt(UnityEngine.Random.Range(actor.AtkMin, actor.AtkMax + 1) * (crit ? 1.7f : 1f) * (weakHit ? 1.5f : 1f) * FlowMul());
                View.SetMessage(crit ? Strings.Get("bt.attack.crit", actor.Name, dmg) : Strings.Get("bt.attack.0", actor.Name));
                int hpBefore = target.Hp;
                HitFoe(target, dmg, crit, weakHit, actor);
                View.Refresh();
                yield return Fx.Wait(0.45f);
                yield return Lunge(aRig, aRig.Home, 0.3f);
                PlayIdle(aRig);
                if (!target.Alive)
                {
                    yield return FadeOut(tRig);
                    View.SetMessage(Strings.Get("bt.fainted", target.Name));
                    yield return Fx.Wait(0.6f);
                    TrySpore(actor, target);
                }
                else if (target.Boss && target.Hp < hpBefore && target.Hp01 < 0.5f && UnityEngine.Random.value < 0.25f)
                {
                    // a wounded gatekeeper does not suffer a blow in silence: it answers
                    // on the spot, and only once it is hurt - the lesson is to press anyway
                    View.SetMessage(Strings.Get("bt.riposte", target.Name));
                    yield return Fx.Wait(0.3f);
                    yield return Lunge(tRig, aRig.Home, 0.28f);
                    int rep = Mathf.Max(1, Mathf.RoundToInt(UnityEngine.Random.Range(target.AtkMin, target.AtkMax + 1) * 0.5f));
                    actor.Hp = Mathf.Max(0, actor.Hp - rep);
                    StartCoroutine(Fx.FlashTint(aRig.Anim, new Color(1f, 0.5f, 0.4f), 2, 0.07f, 0.07f));
                    View.FloatNumber(aRig.Home + new Vector3(0f, 1.2f, 0f), "-" + rep, new Color(1f, 0.6f, 0.5f));
                    Sfx.Play("hurt");
                    yield return Lunge(tRig, tRig.Home, 0.28f);
                    View.Refresh();
                    yield return Fx.Wait(0.5f);
                    if (!actor.Alive)
                    {
                        if (IsUnrisenSkeleton(actor)) { yield return FriendRise(actor, aRig); }
                        else
                        {
                            yield return FadeOut(aRig, true);
                            View.SetMessage(Strings.Get(actor.Species != null ? "bt.fainted" : "bt.herodown", actor.Name));
                            yield return Fx.Wait(0.6f);
                        }
                    }
                }
                EndTurn();
            }
        }

        /// <summary>True for a befriended skeleton that has not yet spent its one rise.
        /// Heroes and enemies stay out: enemies run their own version inside HitFoe.</summary>
        static bool IsUnrisenSkeleton(Fighter f) =>
            f.Species != null && BattleData.Species(f.Species).HasValue
            && BattleData.FamilyOf(BattleData.Species(f.Species).Value) == "skeleton"
            && !f.Boss && !f.Risen;

        /// <summary>The party-side rise: the bones click back together where they fell.</summary>
        IEnumerator FriendRise(Fighter f, BattleView.Rig rig)
        {
            f.Risen = true;
            f.Hp = Mathf.Max(1, Mathf.RoundToInt(f.MaxHp * 0.4f));
            if (rig != null)
                View.FloatNumber(rig.Home + new Vector3(0f, 1.9f, 0f),
                    Strings.Get("bt.rises"), new Color(0.9f, 0.9f, 1f));
            Sfx.Play("enemy");
            View.Refresh();
            yield return Fx.Wait(0.7f);
            PlayIdle(rig);
        }

        int _hitStop;

        /// <summary>A heartbeat of near-frozen time on a crit - the classic hit-stop that makes
        /// a lucky hit land heavier. Restores the clock only if nothing else (the pause card)
        /// took it while the stop ran.</summary>
        IEnumerator HitStop(float t)
        {
            _hitStop++;
            if (Time.timeScale > 0.3f) Time.timeScale = 0.3f;
            float e = 0f;
            while (e < t) { e += Time.unscaledDeltaTime; yield return null; }
            if (--_hitStop <= 0)
            {
                _hitStop = 0;
                if (Time.timeScale > 0.2f && Time.timeScale < 0.9f) Time.timeScale = 1f;
            }
        }

        /// <summary>Damage + feedback for one foe: tint flash, shake, the floating number.
        /// A weakness hit earns its own banner above the number so the table is learnable.</summary>
        void HitFoe(Fighter target, int dmg, bool crit, bool weak = false, Fighter striker = null)
        {
            var tRig = View.RigOf(target);
            if (tRig == null) return;
            var fam = target.Species != null && BattleData.Species(target.Species).HasValue
                ? BattleData.FamilyOf(BattleData.Species(target.Species).Value) : "";
            // a ward drinks the whole blow first - the light winks out, the skin is safe
            if (target.Ward)
            {
                target.Ward = false;
                View.FloatNumber(tRig.Home + new Vector3(0f, 1.2f, 0f),
                    Strings.Get("bt.warded"), new Color(1f, 0.9f, 0.5f));
                Sfx.Play("whoosh");
                return;
            }
            // the dead step sideways out of the world: ghosts phase through blows far
            // more often than a normal dodge
            if (UnityEngine.Random.value < (fam == "ghost" ? 0.2f : 0.07f))
            {
                View.FloatNumber(tRig.Home + new Vector3(0f, 1.2f, 0f),
                    Strings.Get(fam == "ghost" ? "bt.phased" : "bt.miss"), new Color(0.8f, 0.85f, 0.95f));
                Sfx.Play("whoosh");
                return;
            }
            // a pudding that kept a swordsman's edge answers steel with steel: now and
            // then the whole blow is turned aside and nicked back
            if (fam == "slimesword" && striker != null && striker.Alive
                && UnityEngine.Random.value < 0.22f)
            {
                striker.Hp = Mathf.Max(1, striker.Hp - 2);   // a graze, never a kill
                var sRig2 = View.RigOf(striker);
                if (sRig2 != null) View.FloatNumber(sRig2.Home + new Vector3(0f, 1.6f, 0f),
                    "-2", new Color(0.85f, 0.9f, 1f));
                View.FloatNumber(tRig.Home + new Vector3(0f, 1.2f, 0f),
                    Strings.Get("bt.parried"), new Color(0.85f, 0.9f, 1f));
                Sfx.Play("whoosh");
                return;
            }
            // a bone warden's plate drinks part of every blow that lands
            if (fam == "skeletonwarrior" && dmg > 1)
            {
                dmg = Mathf.Max(1, dmg - 2);
                View.FloatNumber(tRig.Home + new Vector3(0f, 1.9f, 0f),
                    Strings.Get("bt.plated"), new Color(0.8f, 0.85f, 0.9f));
            }
            target.Hp = Mathf.Max(0, target.Hp - dmg);
            StartCoroutine(Fx.FlashTint(tRig.Anim, new Color(1f, 0.5f, 0.4f), 2, 0.07f, 0.07f));
            StartCoroutine(Fx.Shake(tRig.Root, crit || weak ? 0.2f : 0.12f, crit || weak ? 0.3f : 0.22f));
            StartCoroutine(Fx.Slash(View.Stage, tRig.Home + new Vector3(0f, 0.85f, 0f),
                crit ? new Color(1f, 0.9f, 0.45f, 0.95f) : weak ? new Color(0.7f, 1f, 0.95f, 0.9f) : new Color(1f, 1f, 1f, 0.85f),
                crit ? 1.5f : 1f));
            if (crit) StartCoroutine(Fx.Shake(View.Stage, 0.13f, 0.18f));
            // a crit earns its weight: the world itself holds still for a heartbeat
            if (crit) StartCoroutine(HitStop(0.06f));
            View.FloatNumber(tRig.Home + new Vector3(0f, 1.2f, 0f), "-" + dmg,
                crit ? new Color(1f, 0.85f, 0.3f) : weak ? new Color(0.65f, 1f, 0.95f) : new Color(1f, 0.95f, 0.75f),
                crit || weak ? 3 : 2);   // payoff hits read bigger than ordinary ones
            if (weak)
                View.FloatNumber(tRig.Home + new Vector3(0f, 1.9f, 0f), Strings.Get("bt.weak"), new Color(0.65f, 1f, 0.95f));
            // a ringing crit can knock the sense out of a lesser foe — the Guard shrugs it off
            if (crit && target.Alive && !target.Boss && UnityEngine.Random.value < 0.2f)
            {
                target.Dazed = true;
                View.FloatNumber(tRig.Home + new Vector3(0f, 2.35f, 0f),
                    Strings.Get("bt.dazed"), new Color(1f, 0.9f, 0.5f));
            }
            // old bones remember the road: once a fight a skeleton pulls itself back
            // together, joints clicking into place
            if (!target.Alive && fam == "skeleton" && !target.Boss && !target.Risen)
            {
                target.Risen = true;
                target.Hp = Mathf.Max(1, Mathf.RoundToInt(target.MaxHp * 0.4f));
                View.FloatNumber(tRig.Home + new Vector3(0f, 1.9f, 0f),
                    Strings.Get("bt.rises"), new Color(0.85f, 0.9f, 1f));
                Sfx.Play("enemy");
            }
            Sfx.Play(crit ? "crit" : "hit");
        }

        /// <summary>True when the acting friend's style cuts this foe's family seam.</summary>
        static bool WeakTo(int style, Fighter foe)
        {
            if (foe.Species == null) return false;
            var s = BattleData.Species(foe.Species);
            return s.HasValue && BattleData.StyleBeats(style, s.Value);
        }

        /// <summary>Family of a live fighter, "" when its species isn't in the bestiary.</summary>
        static string FamOf(Fighter f)
        {
            if (f.Species == null) return "";
            var s = BattleData.Species(f.Species);
            return s.HasValue ? BattleData.FamilyOf(s.Value) : "";
        }

        /// <summary>A puffball's last breath is spores: whatever stood close enough to
        /// strike breathes them in. Ranged hits - the pebble - never reach this far.</summary>
        void TrySpore(Fighter striker, Fighter target)
        {
            if (target.Alive || FamOf(target) != "mushroom") return;
            if (striker == null || !striker.Alive || striker.Poison > 0) return;
            var sRig = View.RigOf(striker);
            striker.Poison = 3;
            if (sRig != null)
                View.FloatNumber(sRig.Home + new Vector3(0f, 1.75f, 0f),
                    Strings.Get("bt.poisoned"), new Color(0.55f, 1f, 0.5f));
        }

        /// <summary>Feeding time for the grave-born: every graverot left standing swells
        /// a little when anything falls - friend, foe, or its own kin.</summary>
        void OnFighterDown(Fighter fallen)
        {
            if (fallen.Alive) return;
            FeedZombis(View.Enemies);
            FeedZombis(View.Party);
        }

        void FeedZombis(System.Collections.Generic.IEnumerable<Fighter> side)
        {
            foreach (var z in side)
            {
                if (!z.Alive || FamOf(z) != "zombi") continue;
                z.Hp = Mathf.Min(z.MaxHp, z.Hp + 5);
                Sfx.Play("heal");
                var zr = View.RigOf(z);
                if (zr != null)
                    View.FloatNumber(zr.Home + new Vector3(0f, 1.9f, 0f),
                        Strings.Get("bt.fed"), new Color(0.6f, 1f, 0.55f));
            }
        }

        /// <summary>What a fighter is worth at the moment it falls: gatekeepers pay four
        /// shares, moonlit two - the same table the win card totals.</summary>
        static int XpOf(Fighter e) => Mathf.RoundToInt(45 * Mathf.Max(1, e.Boss ? 4 : 1)
            * (e.Rare ? 2 : 1) * (1f + 0.3f * Game.State.NgPlus));

        IEnumerator FadeOut(BattleView.Rig rig, bool keepRoot = false)
        {
            // a gatekeeper's fall is the night's climax: the world holds still longer
            // than a common crit before the body goes
            if (rig.F != null && rig.F.Boss) yield return HitStop(0.14f);
            // what leaves a body should be seen leaving: a small pale burst rises
            // where the fighter stood as it goes
            View.Sparkle(rig.Home + new Vector3(0f, 0.9f, 0f), new Color(0.85f, 0.9f, 1f, 0.9f), 8);
            // every kill pays its due where it falls, not just in the card at the end
            if (rig.F != null && rig.F.Side == Side.Enemy && rig.F.MaxHp > 0)
                View.FloatNumber(rig.Home + new Vector3(0f, 1.6f, 0f),
                    "+" + XpOf(rig.F) + " XP", new Color(0.95f, 0.85f, 0.5f), 1);
            yield return Fx.Fade(rig.Anim, new Color(1f, 1f, 1f, 0f), 0.45f);
            rig.Sr.enabled = false;
            rig.Anim.SetTint(Color.white);
            // the name plate is stage-level chrome, not part of the rig root: without this a
            // fainted fighter's name and HP bar hung in the air where the body used to be.
            // A KO'd hero keeps the bar - the empty slot still reads as part of the party.
            if (rig.Name != null) rig.Name.gameObject.SetActive(false);   // enabled=false only
            if (rig.NameChip != null) rig.NameChip.enabled = false;       // stops the component:
            if (rig.Hat != null) rig.Hat.enabled = false;                 // the glyph pool stays lit
            if (rig.Stun != null) rig.Stun.enabled = false;               // and its marker is stage chrome too
            if (rig.WardMark != null) rig.WardMark.enabled = false;
            if (rig.PoisonMark != null) rig.PoisonMark.enabled = false;
            if (!keepRoot)                                                // and the hat is its own
            {                                                             // renderer off the sprite
                if (rig.BarBg != null) rig.BarBg.enabled = false;         // transform, not the body
                if (rig.BarFill != null) rig.BarFill.enabled = false;
                rig.Root.gameObject.SetActive(false);
            }
        }

        /// <summary>Slipping out of a wild fight: a beat, then the world takes you back
        /// where you stood. Bosses bar the way - a blocked try still spends the turn,
        /// same as reaching for a friend with a full stable.</summary>
        IEnumerator PlayerFlee(Fighter actor)
        {
            _ph = Ph.Acting;
            if (_hasBoss)
            {
                Sfx.Play("fail");
                View.SetMessage(Strings.Get("bt.noflee"));
                yield return Fx.Wait(0.9f);
                EndTurn();
                yield break;
            }
            View.SetMessage(Strings.Get("bt.flee"));
            Sfx.Play("whoosh");
            // the party itself melts back into the dark before the world takes over
            foreach (var p in View.Party)
                if (p.Alive)
                {
                    var r = View.RigOf(p);
                    if (r != null && r.Sr != null && r.Sr.enabled) StartCoroutine(FadeOut(r));
                }
            yield return Fx.Wait(0.6f);
            Sfx.Mus.Intensity = 1f;
            OnFled?.Invoke();
        }

        IEnumerator PlayerBefriend(Fighter actor)
        {
            _ph = Ph.Acting;
            int ti = View.Target;
            if (ti < 0 || !View.Enemies[ti].Alive) { EndTurn(); yield break; }
            var target = View.Enemies[ti];
            var tRig = View.RigOf(target);

            // the Guard is not for taming, and neither is a friend you already keep:
            // say so plainly instead of rolling a number that can never land
            bool untameable = target.Boss
                || (target.Species != null && (Game.State.Friends.Contains(target.Species)
                    || Game.State.Friends.Contains("moon." + target.Species)));
            if (untameable)
            {
                View.SetMessage(Strings.Get("bt.notame", target.Name));
                Sfx.Play("fail");
                yield return Fx.Wait(0.7f);
                EndTurn();
                yield break;
            }

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

            if (UnityEngine.Random.value < chance && Game.State.Friends.Count < 2)
            {
                target.Captured = true;
                _befriended++;
                Game.State.Befriended++;
                Game.State.Friends.Add(target.Rare ? "moon." + target.Species : target.Species);
                Medals.Grant("friend");
                if (target.Rare) Medals.Grant("luck");
                View.Sparkle(tRig.Home + new Vector3(0f, tRig.BodyHeight * 0.5f, 0f), new Color(1f, 0.95f, 0.6f), 14);
                Sfx.Play("befriend");
                View.SetMessage(Strings.Get(target.Rare ? "bt.befriended.rare" : "bt.befriended", target.Name));
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
            Sfx.Play("heal");
            foreach (var p in View.Party)
            {
                if (!p.Alive || p.Hp >= p.MaxHp) continue;
                var rig = View.RigOf(p);
                p.Hp = Mathf.Min(p.MaxHp, p.Hp + def.Power);
                if (rig != null) View.FloatNumber(rig.Home + new Vector3(0f, 1.2f, 0f), "+" + def.Power,
                    new Color(0.7f, 1f, 0.7f));
            }
            // each dish carries a second comfort: honey draws out venom, hot tea puts a
            // dazed friend back on their feet, a bowl of soup steadies the momentum
            switch (food)
            {
                case "item.honey":
                case "item.starlight":
                    foreach (var p in View.Party)
                    {
                        if (!p.Alive || p.Poison <= 0) continue;
                        p.Poison = 0;
                        var rig = View.RigOf(p);
                        if (rig != null) View.FloatNumber(rig.Home + new Vector3(0f, 1.9f, 0f),
                            Strings.Get("bt.cleansed"), new Color(0.85f, 1f, 0.6f));
                    }
                    break;
                case "item.tea":
                case "item.mead":
                    foreach (var p in View.Party)
                    {
                        if (!p.Alive || !p.Dazed) continue;
                        p.Dazed = false;
                        var rig = View.RigOf(p);
                        if (rig != null) View.FloatNumber(rig.Home + new Vector3(0f, 1.9f, 0f),
                            Strings.Get("bt.warmed"), new Color(1f, 0.85f, 0.6f));
                    }
                    break;
                default:
                    // every bowl and feast steadies the table's momentum - the same kitchen
                    // bonus the shop prints as +FLOW on the label
                    if (Items.SoupKeys.Contains(food))
                    {
                        _flow = Mathf.Min(9, _flow + 1);
                        View.SetRound(_round, _flow);
                    }
                    break;
            }
            View.Refresh();
            yield return Fx.Wait(0.9f);
            EndTurn();
        }

        /// <summary>Party momentum: each clean party turn adds +6% edge, up to nine stacks;
        /// any hit a hero takes drops the streak back to nothing.</summary>
        float FlowMul() => 1f + 0.06f * _flow;
        int _flow;

        void EndTurn()
        {
            if (_ph == Ph.Card) return;   // a card settled the fight while the action resolved
            _ph = Ph.Round;
            View.SetTurnRig(null);
            if (_qi < _queue.Count && _queue[_qi].Side == Side.Party && _flow < 9)
            {
                _flow++;
                View.SetRound(_round, _flow);
            }
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
            // win once: a second entry (a queued turn or timer landing on an already
            // ended fight) would stack a fresh ghost card over the real one
            if (_ph == Ph.Card) return;
            _ph = Ph.Card;
            Sfx.Mus.Intensity = 1f;
            Sfx.Mus.Duck = 0.5f;            // the band steps back while the card has the floor
            AwaitingInput = false;
            View.SetMenuVisible(false);

            bool boss = false;
            foreach (var s in _specs) if (s.Boss) boss = true;

            int xp = 0, gold = 0;
            foreach (var e in View.Enemies)
            {
                // a foe that slipped away - or chose you - never fell: no purse, no
                // nightwatch credit, no share of the spoils
                if (e.Captured) continue;
                xp += XpOf(e);
                gold += UnityEngine.Random.Range(18, 40) * (e.Boss ? 3 : 1);
                Game.State.Defeats++;   // one step for the nightwatch
                if (e.Rare) Medals.Grant("moonlit");
            }
            // the bestiary is keyed by the species, not its printed name - and the card
            // gets a line for whichever of them the book meets tonight for the first time
            var firsts = new List<string>();
            foreach (var s in _specs)
                if (Game.State.MarkSeen(s.Name)) firsts.Add(Strings.Get(s.Name));
            Game.State.Xp += xp;
            Game.State.Gold += gold;

            // everyone breathes again the moment the field is quiet - the card says so
            // when the quiet was earned through bandages
            bool hurt = false;
            foreach (var p in View.Party) if (p.Hp < p.MaxHp) hurt = true;
            // nobody fell: the night pays a little extra for a clean fight
            bool flawless = true;
            foreach (var p in View.Party) if (!p.Alive) flawless = false;
            if (flawless)
            {
                int bonus = 20 + Game.State.Chapter * 10;
                Game.State.Gold += bonus;
                gold += bonus;
                // and nobody even bled: a clean read pays half again in experience
                if (!hurt) { int xb = Mathf.Max(6, xp / 2); Game.State.Xp += xb; xp += xb; }
            }
            var lines = new List<string>
            {
                Strings.Get("card.xp", xp),
                Strings.Get("card.gold", gold),
            };
            if (firsts.Count > 0)
                lines.Add(Strings.Get("card.newseen", string.Join(", ", firsts)));
            if (flawless) lines.Add(Strings.Get(hurt ? "card.flawless" : "card.untouch"));
            else if (hurt) lines.Add(Strings.Get("card.healall"));
            if (Game.State.Level > _levelAtStart)
            {
                lines.Add(Strings.Get("card.levelup", Game.State.Level));
                Sfx.Play("levelup");
            }
            // spoils: the Guard always leaves gear, the wild things sometimes do
            var rng = new System.Random();
            var drops = new List<string>();
            foreach (var e in View.Enemies)
            {
                if (e.Captured) continue;   // the gone and the joined keep what they carry
                var spec = e.Species != null ? BattleData.Species(e.Species) : null;
                if (!spec.HasValue) continue;
                // a beast may leave what it is - the pudding's kept sword, the graverot's
                // grave goods - before the wild table ever gets its say
                var key = e.Boss || e.Rare ? Items.BossDrop(rng)
                    : Items.SpeciesDrop(BattleData.FamilyOf(spec.Value), rng) ?? Items.RollDrop(Game.State.Chapter, rng);
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
                // each night's keeper falls to its own card
                int ch = Mathf.Clamp(Game.State.Chapter, 1, 3);
                Medals.Grant("boss" + ch);
                lines.Add(Strings.Get("card.bossline." + ch));
                View.ShowCard(Strings.Get("card.bosstitle." + ch), lines.ToArray(),
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
            // lose once: a stray post-card tick re-showing the card stacked ghost
            // buttons over a dead view, and TRY AGAIN on a ghost restarted the fight
            if (_ph == Ph.Card) return;
            _ph = Ph.Card;
            Sfx.Mus.Intensity = 1f;
            Sfx.Mus.Duck = 0.5f;
            AwaitingInput = false;
            View.SetMenuVisible(false);
            // slinking home costs a handful of gold: standing back up for another
            // try is the free path, and the card says so
            int tithe = Mathf.Min(Game.State.Gold, 15 + Game.State.Chapter * 5);
            var lines = new List<string> { Strings.Get("card.lossline") };
            if (tithe > 0) lines.Add(Strings.Get("card.tithe", tithe));
            View.ShowCard(Strings.Get("card.losstitle"),
                lines.ToArray(),
                new[] { Strings.Get("btn.retry"), Strings.Get("btn.flee") },
                new Action[] { () => StartBattle(_specs), () => { Game.State.Gold -= tithe; OnDefeat?.Invoke(); } },
                new Color(1f, 0.6f, 0.6f));
        }
    }
}
