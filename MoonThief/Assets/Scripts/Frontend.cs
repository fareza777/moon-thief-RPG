using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace MoonThief
{
    /// <summary>Player settings. Kept in PlayerPrefs so they survive between runs.</summary>
    public static class Prefs
    {
        public const float SpeedSlow = 26f, SpeedNormal = 55f, SpeedFast = 95f, SpeedInstant = 0f;

        public static int SpeedIndex = 1;      // 0 slow, 1 normal, 2 fast, 3 instant
        public static int SoundLevel = 4;      // 0 off .. 4 full; the settings row steps it
        public static bool Shake = true;
        public static bool Story;               // story difficulty: hits land softer
        public static bool IntroSeen;
        public static int MusicLevel = 4;      // 0 off .. 4 full
        public static bool OnbSeen;            // the three onboarding cards only run once
        public static bool Auto;               // the party's standing battle stance

        public static float RevealSpeed => SpeedIndex switch
        {
            0 => SpeedSlow,
            2 => SpeedFast,
            3 => SpeedInstant,
            _ => SpeedNormal,
        };

        public static string SpeedName => Strings.Get(SpeedIndex switch
        {
            0 => "set.speed.slow",
            2 => "set.speed.fast",
            3 => "set.speed.instant",
            _ => "set.speed.normal",
        });

        public static void Load()
        {
            SpeedIndex = Mathf.Clamp(PlayerPrefs.GetInt("mt.speed", 1), 0, 3);
            // the old on/off keys feed the level default once, so existing saves keep their choice
            SoundLevel = Mathf.Clamp(PlayerPrefs.GetInt("mt.soundlvl", PlayerPrefs.GetInt("mt.sound", 1) * 4), 0, 4);
            Shake = PlayerPrefs.GetInt("mt.shake", 1) == 1;
            Story = PlayerPrefs.GetInt("mt.story", 0) == 1;
            IntroSeen = PlayerPrefs.GetInt("mt.intro", 0) == 1;
            MusicLevel = Mathf.Clamp(PlayerPrefs.GetInt("mt.muslvl", PlayerPrefs.GetInt("mt.music", 1) * 4), 0, 4);
            OnbSeen = PlayerPrefs.GetInt("mt.onb", 0) == 1;
            Auto = PlayerPrefs.GetInt("mt.auto", 0) == 1;
            Sfx.Volume = SoundLevel * 0.25f;
            Sfx.Muted = SoundLevel <= 0;
            Sfx.Mus.Volume = MusicLevel * 0.25f;
            Sfx.Mus.Muted = MusicLevel <= 0;
        }

        public static void Store()
        {
            PlayerPrefs.SetInt("mt.speed", SpeedIndex);
            PlayerPrefs.SetInt("mt.soundlvl", SoundLevel);
            PlayerPrefs.SetInt("mt.shake", Shake ? 1 : 0);
            PlayerPrefs.SetInt("mt.story", Story ? 1 : 0);
            PlayerPrefs.SetInt("mt.intro", IntroSeen ? 1 : 0);
            PlayerPrefs.SetInt("mt.muslvl", MusicLevel);
            PlayerPrefs.SetInt("mt.onb", OnbSeen ? 1 : 0);
            PlayerPrefs.SetInt("mt.auto", Auto ? 1 : 0);
            PlayerPrefs.Save();
            Sfx.Volume = SoundLevel * 0.25f;
            Sfx.Muted = SoundLevel <= 0;
            Sfx.Mus.Volume = MusicLevel * 0.25f;
            Sfx.Mus.Muted = MusicLevel <= 0;
        }
    }

    /// <summary>One saved night: where the party stood, what they carried, which night it was.</summary>
    [Serializable]
    public class SaveData
    {
        public int version = 1;
        public int chapter = 1;
        public int shards;
        public int befriended;
        public int gold;
        public int xp;
        public int morsels;
        public int items;
        public int chestsOpened;
        public int defeats;
        public bool bossDown;
        public float heroX, heroY;
        public string stamp = "";
        // the journal: bag, worn gear, places walked, beasts seen, quests in flight
        public string[] bag;
        public string[] worn;
        public string[] zones;
        public string[] seen;
        public string[] friends;
        public string[] quests;
        public string[] chests;
    }

    /// <summary>JSON save file in the platform's persistent data folder.</summary>
    public static class SaveSystem
    {
        const string FileName = "moonthief-save.json";

        public static string Path
        {
            get
            {
                try { return System.IO.Path.Combine(Application.persistentDataPath, FileName); }
                catch { return FileName; }
            }
        }

        public static bool Exists()
        {
            try { return File.Exists(Path); } catch { return false; }
        }

        public static SaveData Read()
        {
            try
            {
                if (!File.Exists(Path)) return null;
                var d = JsonUtility.FromJson<SaveData>(File.ReadAllText(Path));
                if (d == null || d.version != 1) return null;
                return d;
            }
            catch (Exception e)
            {
                Debug.LogWarning("SaveSystem: could not read save - " + e.Message);
                return null;
            }
        }

        public static void Write(SaveData d)
        {
            try
            {
                d.version = 1;
                d.stamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
                // a crash mid-write must not eat the last good night: fill a temp file
                // first, then move it whole over the old save
                var tmp = Path + ".tmp";
                File.WriteAllText(tmp, JsonUtility.ToJson(d, true));
                if (File.Exists(Path)) File.Delete(Path);
                File.Move(tmp, Path);
                Debug.Log("[save] written to " + Path);
            }
            catch (Exception e)
            {
                Debug.LogWarning("SaveSystem: could not write save - " + e.Message);
            }
        }

        public static void Erase()
        {
            try { if (File.Exists(Path)) File.Delete(Path); } catch { }
        }
    }

    /// <summary>
    /// Every screen that is not the world or a battle: the studio splash, the main menu,
    /// settings, credits, the pause card and the story slides. Built in code, no prefabs,
    /// portrait frame only. Screens are lists of rows so keyboard and touch share one path.
    /// </summary>
    public class MenuView : MonoBehaviour
    {
        public enum Sc { None, Splash, Main, Settings, Credits, Pause, Cinema, ChapterCard, Journal, Page, Onboard, Shop, Confirm }

        /// <summary>Everything the pause card can open. One enum keeps the hub, the back stack
        /// and the self-test in agreement about what is on screen.</summary>
        public enum Page2 { Character, Items, Equipment, Bestiary, Quests, Map }

        public float HalfH = 16f;

        // wired by Game
        public Action OnStartNew, OnLoadSave, OnResume, OnSaveGame, OnLeaveToTitle, OnIntroDone, OnSplashDone, OnOnboardDone, OnShopClosed, OnStory;
        public Action<string> OnReleaseFriend;   // a tamed species wished back to the wild

        /// <summary>The store page SHARE and RATE point at. Application.identifier is the same
        /// value the builder sets, so the link can never drift from the shipped package.</summary>
        public static string StoreUrl => "https://play.google.com/store/apps/details?id="
            + (string.IsNullOrEmpty(Application.identifier) ? "com.fajargames.moonthief" : Application.identifier);

        const float RowW = 14.6f, RowH = 2.0f, RowGap = 0.55f;

        // Row columns, measured inward from the row's own left edge. The chevron used to sit at
        // 0.55 with a 1.5x scale, so its ink reached 0.42 units past the left border *and* 0.3
        // units into the first letter of the word it selects -- a selected row read "CHARCTER".
        // Now it lives inside the padding and the label starts clear of its ink.
        const float ChevInset = 0.95f, ChevScale = 2f;
        const float TextInset = 1.45f, ValInset = 0.7f;

        // Vertical furniture of a card: the title block above the first row, the footnote lane
        // under the last one, and the hairline that keeps a row off the border. Cards are sized
        // from these instead of being pinned by hand -- a hand-pinned card is how the journal's
        // BACK row ended up hanging 1.25 units below its own frame.
        const float CardHead = 5.25f, CardFoot = 1.7f, CardPad = 0.95f;

        class Row
        {
            public SpriteRenderer Panel, Chev, Icon;
            public PixelLabel Text, ValLabel;
            public Rect Hit;
            public Action Act;
            public bool Enabled = true;
        }

        Sc _sc = Sc.None;
        float _t;
        Transform _root, _splashRoot, _mainRoot, _setRoot, _credRoot, _pauseRoot, _ciRoot, _ccRoot, _toastRoot;
        Transform _onbRoot, _shopRoot;

        // one row list per card. A single shared list looked simpler, but each BuildRows()
        // appended to it while LayRows() only ever activated the first four entries -- the
        // main menu's. The pause and settings cards then drew a bare frame (their rows stayed
        // inactive) and their taps resolved against the main menu's rectangles and actions.
        List<Row> _mainRows = new List<Row>();
        List<Row> _setRows = new List<Row>();
        List<Row> _pauseRows = new List<Row>();
        List<Row> _credRows = new List<Row>();
        List<Row> _jrRows = new List<Row>();
        List<Row> _pageRows = new List<Row>();
        List<Row> _onbRows = new List<Row>();
        List<Row> _shopRows = new List<Row>();

        /// <summary>The rows of the card that is up.</summary>
        List<Row> Rows
        {
            get
            {
                switch (_sc)
                {
                    case Sc.Pause: return _pauseRows;
                    case Sc.Confirm: return _pauseRows;   // the confirm card borrows the pause card's furniture
                    case Sc.Settings: return _setRows;
                    case Sc.Credits: return _credRows;
                    case Sc.Journal: return _jrRows;
                    case Sc.Page: return _pageRows;
                    case Sc.Shop: return _shopRows;
                    case Sc.Onboard: return _onbRows;
                    default: return _mainRows;
                }
            }
        }

        // ---- the journal: a hub plus one reusable paged list ----
        Transform _jrRoot, _pageRoot;
        PixelLabel _jrTitle, _jrSub, _jrFoot, _pageTitle, _pageSub, _pageFoot, _setFoot;
        SpriteRenderer _jrPanel, _pagePanel;
        // the world-map page's chrome and the live-map feed Game wires in
        public Func<GameMap> GetMap;
        public Func<Vector2?> GetHeroPos;
        public Func<Vector2?> GetObjectivePos;
        SpriteRenderer _mapSr, _mapHeroDot, _mapQuestDot;
        readonly PixelLabel[] _mapZoneLbl = new PixelLabel[4];
        Sprite _mapSpr;
        string[] _pageLabels = new string[0], _pageVals = new string[0];
        int[] _pageIcons;
        Sprite[] _pageIconSprites;
        Action[] _pageActs = new Action[0];
        float _cardTop;                 // the card laid out last: cards are centred on the origin
        string _pageTitleKey;
        string _pageSubKey;
        int _pageIndex;
        const int PageRowsPerView = 6;
        Sc _pageBack = Sc.Journal;
        int _sel;
        readonly List<SpriteRenderer> _stars = new List<SpriteRenderer>();

        PixelLabel _splashTop, _splashSub, _splashPres, _setTitle, _pauseTitle, _pauseSub, _hint, _toast;
        PixelLabel _credTitle, _credSub, _credText, _credThanks;
        PixelLabel _ciText, _ciSkip, _ciCount, _ccNight, _ccPlace, _ccGoal;
        PixelLabel _onbTitle, _onbBody, _shopTitle, _shopSub, _shopFoot;
        SpriteRenderer _onbPanel, _shopPanel;
        readonly List<SpriteRenderer> _onbDots = new List<SpriteRenderer>();
        int _onbPage;
        SpriteRenderer _ciPlate;
        SpriteRenderer _ciArt, _ciDim, _splashBg, _splashMoon, _ccDim, _pauseDim, _pausePanel, _setPanel, _credPanel;
        SpriteRenderer _toastPanel;
        float _toastT;

        int _ciIndex;
        float _ciHold;
        float _ciRead;                 // seconds the finished line has been readable
        bool _settingsFromPause;
        bool _wipeArmed;        // ERASE SAVE arms itself for one tap instead of asking twice

        public bool IsUp => _sc != Sc.None;
        public Sc Current => _sc;
        public bool IsCinema => _sc == Sc.Cinema || _sc == Sc.ChapterCard;

        // ------------------------------------------------------------------ build

        public void Build(Game gm, float halfH, Transform parent)
        {
            HalfH = halfH;
            _root = new GameObject("menus").transform;
            _root.SetParent(parent, false);
            BuildSplash();
            BuildMain();
            BuildSettings();
            BuildCredits();
            BuildPause();
            BuildCinema();
            BuildJournal();
            BuildPage();
            BuildOnboard();
            BuildShop();
            BuildToast();
            HideAll();
        }

        Transform Root(string name, int sorting)
        {
            var t = new GameObject(name).transform;
            t.SetParent(_root, false);
            return t;
        }

        SpriteRenderer FullQuad(Transform parent, string name, int sorting, Color color)
        {
            var sr = SpriteRendererUtil.Make(parent, name, TexArt.Solid(), sorting);
            float hh = HalfH + 2.5f;
            sr.transform.localScale = new Vector3(18f * 16f, hh * 2f * 16f, 1f);
            sr.color = color;
            return sr;
        }

        void BuildSplash()
        {
            _splashRoot = Root("splash", 0);
            _splashBg = FullQuad(_splashRoot, "bg", 9000, new Color(0.02f, 0.02f, 0.05f, 1f));
            // A studio card on flat black reads as a texture that failed to load. A scatter of
            // stars and the moon cost nothing and say what kind of story this is before the
            // title screen does.
            var rng = new System.Random(11);
            for (int i = 0; i < 24; i++)
            {
                var st = SpriteRendererUtil.Make(_splashRoot, "sstar" + i, TexArt.Star(), 9001);
                st.transform.localPosition = new Vector3(
                    G.Snap((float)rng.NextDouble() * 17f - 8.5f),
                    G.Snap((float)rng.NextDouble() * (HalfH * 2f - 5f) - HalfH + 2.5f), 0f);
                st.color = new Color(1f, 1f, 1f, 0.16f + (float)rng.NextDouble() * 0.4f);
            }
            _splashMoon = SpriteRendererUtil.Make(_splashRoot, "smoon", TexArt.MoonFull(), 9001);
            _splashMoon.transform.localPosition = new Vector3(0f, 5.2f, 0f);
            _splashMoon.transform.localScale = Vector3.one * 3.4f;

            _splashTop = PixelLabelUtil.Make(_splashRoot, "studio", FitText(Strings.Get("splash.studio"), 15.6f, 3),
                new Color(1f, 0.95f, 0.78f), TextAlign.Center, 9002);
            _splashTop.transform.localPosition = new Vector3(0f, 1.5f, 0f);
            _splashTop.Set(Strings.Get("splash.studio"));
            _splashSub = PixelLabelUtil.Make(_splashRoot, "made", 1, new Color(0.72f, 0.75f, 0.94f), TextAlign.Center, 9002);
            _splashSub.transform.localPosition = new Vector3(0f, -1.5f, 0f);
            _splashSub.Set(Strings.Get("splash.made"));
            _splashPres = PixelLabelUtil.Make(_splashRoot, "presents", 1, new Color(0.6f, 0.63f, 0.84f), TextAlign.Center, 9002);
            _splashPres.transform.localPosition = new Vector3(0f, -0.1f, 0f);
            _splashPres.Set(Strings.Get("splash.presents"));
            SplashRule(2.7f);
            SplashRule(-2.4f);
        }

        /// <summary>A one pixel hairline across the studio card, the same furniture the credits
        /// page uses so the two read as one studio's screens.</summary>
        void SplashRule(float y)
        {
            var sr = SpriteRendererUtil.Make(_splashRoot, "srule" + y, TexArt.Solid(), 9001);
            sr.transform.localPosition = new Vector3(0f, y, 0f);
            sr.transform.localScale = new Vector3(9.4f * 16f, 16f / 4f, 1f);
            sr.color = new Color(0.6f, 0.65f, 1f, 0.4f);
        }

        /// <summary>Fades the studio card up as one block. Set to 1 in the editor render: that
        /// frame is captured without ever running Tick, so a card that starts transparent would
        /// come out empty.</summary>
        void SetSplashAlpha(float a)
        {
            var c = _splashTop.Tint; c.a = a; _splashTop.SetColor(c);
            var s = _splashSub.Tint; s.a = a * 0.95f; _splashSub.SetColor(s);
            var p = _splashPres.Tint; p.a = a * 0.9f; _splashPres.SetColor(p);
            var m = _splashMoon.color; m.a = a; _splashMoon.color = m;
        }

        void BuildMain()
        {
            _mainRoot = Root("main", 0);
            _hint = PixelLabelUtil.Make(_mainRoot, "hint", 1, new Color(0.72f, 0.74f, 0.9f), TextAlign.Center, 6008);
            // seven rows stack to -14.7; the hint parks just under them, off the last row's panel
            _hint.transform.localPosition = new Vector3(0f, -15.1f, 0f);
            _hint.Set(Strings.Get("menu.hint"));
            _mainRows = BuildRows(_mainRoot);
        }

        void BuildSettings()
        {
            _setRoot = Root("settings", 0);
            FullQuad(_setRoot, "dim", 6000, new Color(6f / 255f, 5f / 255f, 16f / 255f, 0.88f));
            // the sheet drops in while the dim snaps: the card and its furniture ride one
            // transform so SlideIn moves them together and the dim stays put
            _setCard = new GameObject("card").transform;
            _setCard.SetParent(_setRoot, false);
            _setPanel = Panel(_setCard, "setPanel", 6002, 16.4f, 15.2f, 0.2f);
            _setTitle = PixelLabelUtil.Make(_setCard, "setTitle", 3, new Color(1f, 0.95f, 0.78f), TextAlign.Center, 6006);
            _setTitle.Set(Strings.Get("set.title"));
            _setFoot = PixelLabelUtil.Make(_setCard, "setFoot", 1, new Color(0.6f, 0.64f, 0.86f), TextAlign.Center, 6007);
            _setFoot.Set(Strings.Get("set.hint"));
            _setRows = BuildRows(_setCard);
        }

        void BuildCredits()
        {
            _credRoot = Root("credits", 0);
            FullQuad(_credRoot, "dim", 6000, new Color(6f / 255f, 5f / 255f, 16f / 255f, 0.9f));
            _credCard = new GameObject("card").transform;
            _credCard.SetParent(_credRoot, false);
            _credPanel = Panel(_credCard, "credPanel", 6002, 15.6f, 17.4f, 0f);

            // the moon over the card, a hairline under the title block - a page this empty
            // reads as unfinished without a little furniture
            var moon = SpriteRendererUtil.Make(_credCard, "credMoon", TexArt.MoonFull(), 6004);
            moon.transform.localPosition = new Vector3(0f, 6.05f, 0f);
            moon.transform.localScale = Vector3.one * 2.6f;

            // fitted to the card: at scale 3 the title ran edge to edge of the panel border
            _credTitle = PixelLabelUtil.Make(_credCard, "credTitle", FitText(Strings.Get("cred.title"), 13.2f, 3),
                new Color(1f, 0.95f, 0.78f), TextAlign.Center, 6006);
            _credTitle.transform.localPosition = new Vector3(0f, 3.9f, 0f);
            _credTitle.Set(Strings.Get("cred.title"));

            _credSub = PixelLabelUtil.Make(_credCard, "credSub", FitText(Strings.Get("cred.sub"), 13.2f, 2),
                new Color(0.76f, 0.8f, 1f), TextAlign.Center, 6006);
            _credSub.transform.localPosition = new Vector3(0f, 2.0f, 0f);
            _credSub.Set(Strings.Get("cred.sub"));

            Rule("credRule", 1.0f);

            // the body is the one block whose height depends on the string table, so it is
            // measured and placed by LayoutCredits() instead of being pinned by hand
            _credText = PixelLabelUtil.Make(_credCard, "credText", 1, new Color(0.93f, 0.95f, 1f), TextAlign.Center, 6006);
            _credText.MaxWidthUnits = 15.2f;
            _credText.Set(Strings.Get("cred.body", Application.version));

            _credThanks = PixelLabelUtil.Make(_credCard, "credThanks", 2, new Color(0.88f, 0.96f, 0.86f), TextAlign.Center, 6006);
            _credThanks.Set(Strings.Get("cred.thanks"));

            // the lower hairline sits above the thanks line, in the body band's tail space -
            // any lower and it crosses the thanks line's cap row
            Rule("credRule2", -2.95f);

            _credRows = BuildRows(_credCard);
        }

        /// <summary>The largest text scale whose measured width still fits the given space.
        /// A long title should step down a size instead of touching the card border.</summary>
        static int FitText(string text, float maxWidthUnits, int preferred)
        {
            for (int k = preferred; k >= 1; k--)
                if (PixelFont.Measure(text, k).x <= maxWidthUnits) return k;
            return 1;
        }

        /// <summary>A one pixel scene rule across the credits card.</summary>
        void Rule(string name, float y)
        {
            var sr = SpriteRendererUtil.Make(_credCard, name, TexArt.Solid(), 6005);
            sr.transform.localPosition = new Vector3(0f, y, 0f);
            sr.transform.localScale = new Vector3(13.6f * 16f, 16f / 4f, 1f);
            sr.color = new Color(0.62f, 0.66f, 1f, 0.42f);
        }

        void BuildPause()
        {
            _pauseRoot = Root("pause", 0);
            _pauseDim = FullQuad(_pauseRoot, "dim", 6000, new Color(6f / 255f, 5f / 255f, 16f / 255f, 0.72f));
            _pauseCard = new GameObject("card").transform;
            _pauseCard.SetParent(_pauseRoot, false);
            _pausePanel = Panel(_pauseCard, "pausePanel", 6002, 16.4f, 15.6f, 0.2f);
            _pauseTitle = PixelLabelUtil.Make(_pauseCard, "pauseTitle", 3, new Color(1f, 0.95f, 0.78f), TextAlign.Center, 6006);
            _pauseTitle.transform.localPosition = new Vector3(0f, 5.8f, 0f);
            _pauseTitle.Set(Strings.Get("pause.title"));
            _pauseSub = PixelLabelUtil.Make(_pauseCard, "pauseSub", 1, new Color(0.72f, 0.74f, 0.9f), TextAlign.Center, 6006);
            _pauseSub.transform.localPosition = new Vector3(0f, -7.2f, 0f);
            _pauseSub.Set("");
            _pauseRows = BuildRows(_pauseCard);
        }

        void BuildCinema()
        {
            _ciRoot = Root("cinema", 0);
            FullQuad(_ciRoot, "black", 7000, new Color(0.02f, 0.02f, 0.05f, 1f));
            _ciArt = SpriteRendererUtil.Make(_ciRoot, "art", null, 7001);
            _ciDim = FullQuad(_ciRoot, "dim", 7002, new Color(4f / 255f, 4f / 255f, 12f / 255f, 0.55f));
            // a frame of stars so the intro sky is never flat black
            var rng = new System.Random(7);
            for (int i = 0; i < 30; i++)
            {
                var sr = SpriteRendererUtil.Make(_ciRoot, "cstar" + i, TexArt.Star(), 7003);
                sr.transform.localPosition = new Vector3(
                    G.Snap((float)rng.NextDouble() * 17f - 8.5f),
                    G.Snap((float)rng.NextDouble() * (HalfH * 2f - 4f) - HalfH + 2f), 0f);
                sr.color = new Color(1f, 1f, 1f, 0.25f + (float)rng.NextDouble() * 0.5f);
                _stars.Add(sr);
            }
            // the narration sits on its own measured plate: the slides play over painted
            // backgrounds, and a paragraph of bright text straight on art is the muddiest thing
            // in an otherwise crisp frame
            _ciPlate = SpriteRendererUtil.Make(_ciRoot, "ciPlate", TexArt.Panel(), 7003);
            _ciPlate.drawMode = SpriteDrawMode.Sliced;
            _ciPlate.size = new Vector2(16f, 4f);
            _ciPlate.color = new Color(1f, 1f, 1f, 0.82f);

            // two hairline bars top and bottom: the cheapest way to say "this is a film, not a
            // menu", and they also hide the tops of the backdrop art where it stretches
            var barTop = SpriteRendererUtil.Make(_ciRoot, "barTop", TexArt.Solid(), 7005);
            barTop.color = new Color(0.01f, 0.01f, 0.03f, 0.96f);
            barTop.transform.localScale = new Vector3(18f * 16f, 1.5f * 16f, 1f);
            barTop.transform.localPosition = new Vector3(0f, HalfH - 0.75f, 0f);
            var barBottom = SpriteRendererUtil.Make(_ciRoot, "barBottom", TexArt.Solid(), 7005);
            barBottom.color = barTop.color;
            barBottom.transform.localScale = barTop.transform.localScale;
            barBottom.transform.localPosition = new Vector3(0f, -HalfH + 0.75f, 0f);

            _ciText = PixelLabelUtil.Make(_ciRoot, "ciText", 2, new Color(1f, 0.97f, 0.88f), TextAlign.Center, 7004);
            _ciText.transform.localPosition = new Vector3(0f, 2.2f, 0f);
            _ciText.MaxWidthUnits = 15.2f;
            // the narration types itself in; a wall of text that appears at once reads as a slide
            // deck, one line at a time reads as a story being told
            _ciText.RevealSpeed = 46f;
            _ciSkip = PixelLabelUtil.Make(_ciRoot, "ciSkip", 1, new Color(0.78f, 0.8f, 0.95f), TextAlign.Center, 7004);
            _ciSkip.transform.localPosition = new Vector3(0f, -HalfH + 2.2f, 0f);
            _ciSkip.Set(Strings.Get("ci.skip"));
            _ciCount = PixelLabelUtil.Make(_ciRoot, "ciCount", 1, new Color(0.7f, 0.73f, 0.9f), TextAlign.Right, 7004);
            _ciCount.transform.localPosition = new Vector3(G.Right - 0.6f, -HalfH + 2.2f, 0f);

            _ccRoot = Root("chaptercard", 0);
            _ccDim = FullQuad(_ccRoot, "black", 7100, new Color(0.02f, 0.02f, 0.05f, 1f));
            _ccNight = PixelLabelUtil.Make(_ccRoot, "night", 3, new Color(1f, 0.95f, 0.78f), TextAlign.Center, 7102);
            _ccNight.transform.localPosition = new Vector3(0f, 1.6f, 0f);
            _ccPlace = PixelLabelUtil.Make(_ccRoot, "place", 1, new Color(0.78f, 0.8f, 0.95f), TextAlign.Center, 7102);
            _ccPlace.transform.localPosition = new Vector3(0f, -0.6f, 0f);
            _ccGoal = PixelLabelUtil.Make(_ccRoot, "goal", 1, new Color(0.92f, 0.85f, 0.62f), TextAlign.Center, 7102);
            _ccGoal.transform.localPosition = new Vector3(0f, -2.4f, 0f);
            _ccGoal.MaxWidthUnits = 15.5f;
        }

        /// <summary>Builds the journal hub and the paged list card it opens. Both are laid out
        /// like every other card: a title, a stack of rows, and a way back.</summary>
        void BuildJournal()
        {
            _jrRoot = Root("journal", 0);
            _jrPanel = SpriteRendererUtil.Make(_jrRoot, "jrPanel", TexArt.Panel(), 5990);
            _jrPanel.drawMode = SpriteDrawMode.Sliced;
            _jrPanel.size = new Vector2(16.4f, 19f);
            _jrPanel.transform.localPosition = new Vector3(0f, 0.6f, 0f);
            _jrTitle = PixelLabelUtil.Make(_jrRoot, "jrTitle", 2, new Color(1f, 0.94f, 0.74f), TextAlign.Center, 6007);
            _jrTitle.transform.localPosition = new Vector3(0f, 8.2f, 0f);
            _jrTitle.Set(Strings.Get("jr.title"));
            _jrSub = PixelLabelUtil.Make(_jrRoot, "jrSub", 1, new Color(0.72f, 0.76f, 0.92f), TextAlign.Center, 6007);
            _jrFoot = PixelLabelUtil.Make(_jrRoot, "jrFoot", 1, new Color(0.6f, 0.64f, 0.86f), TextAlign.Center, 6007);
            _jrRows = BuildRows(_jrRoot);
        }

        void BuildPage()
        {
            _pageRoot = Root("journalPage", 0);
            _pagePanel = SpriteRendererUtil.Make(_pageRoot, "pgPanel", TexArt.Panel(), 5990);
            _pagePanel.drawMode = SpriteDrawMode.Sliced;
            _pagePanel.size = new Vector2(16.4f, 22f);
            _pagePanel.transform.localPosition = new Vector3(0f, 0.2f, 0f);
            _pageTitle = PixelLabelUtil.Make(_pageRoot, "pgTitle", 2, new Color(1f, 0.94f, 0.74f), TextAlign.Center, 6007);
            _pageTitle.transform.localPosition = new Vector3(0f, 9.4f, 0f);
            _pageSub = PixelLabelUtil.Make(_pageRoot, "pgSub", 1, new Color(0.72f, 0.76f, 0.92f), TextAlign.Center, 6007);
            _pageSub.MaxWidthUnits = 15f;
            _pageFoot = PixelLabelUtil.Make(_pageRoot, "pgFoot", 1, new Color(0.6f, 0.64f, 0.86f), TextAlign.Center, 6007);
            _pageRows = BuildRows(_pageRoot);

            // the world-map page's chrome: a real pixel minimap of the night's ground
            // with a dot where the hero stands and one where the compass points
            _mapSr = SpriteRendererUtil.Make(_pageRoot, "pgMapImg", null, 6002);
            _mapSr.enabled = false;
            _mapHeroDot = SpriteRendererUtil.Make(_pageRoot, "pgMapHero", TexArt.Dot(), 6005);
            _mapHeroDot.transform.localScale = Vector3.one * 0.085f;
            _mapHeroDot.color = new Color(1f, 0.82f, 0.4f);
            _mapHeroDot.enabled = false;
            _mapQuestDot = SpriteRendererUtil.Make(_pageRoot, "pgMapQuest", TexArt.Spark(), 6005);
            _mapQuestDot.transform.localScale = Vector3.one * 0.16f;
            _mapQuestDot.color = new Color(0.74f, 0.62f, 1f);
            _mapQuestDot.enabled = false;
            for (int i = 0; i < 4; i++)
            {
                _mapZoneLbl[i] = PixelLabelUtil.Make(_pageRoot, "pgMapZone" + i, 1, new Color(0.86f, 0.86f, 0.96f), TextAlign.Left, 6007);
                _mapZoneLbl[i].gameObject.SetActive(false);
            }
        }

        /// <summary>The three first-boot cards. One card, one line of dots, a NEXT row that
        /// becomes BEGIN on the last page: three screens in one layout, run once ever.</summary>
        void BuildOnboard()
        {
            _onbRoot = Root("onboard", 0);
            FullQuad(_onbRoot, "dim", 6000, new Color(6f / 255f, 5f / 255f, 16f / 255f, 0.94f));
            _onbCard = new GameObject("card").transform;
            _onbCard.SetParent(_onbRoot, false);
            _onbPanel = Panel(_onbCard, "onbPanel", 6002, 15.6f, 13.6f, 0.4f);
            var moon = SpriteRendererUtil.Make(_onbCard, "onbMoon", TexArt.MoonFull(), 6004);
            moon.transform.localPosition = new Vector3(0f, 6.4f, 0f);
            moon.transform.localScale = Vector3.one * 2.4f;
            _onbTitle = PixelLabelUtil.Make(_onbCard, "onbTitle", 3, new Color(1f, 0.95f, 0.78f), TextAlign.Center, 6006);
            _onbTitle.transform.localPosition = new Vector3(0f, 4.3f, 0f);
            _onbTitle.MaxWidthUnits = 14.2f;   // FIGHT & BEFRIEND stacks to two lines rather than clip
            _onbBody = PixelLabelUtil.Make(_onbCard, "onbBody", 1, new Color(0.93f, 0.95f, 1f), TextAlign.Center, 6006);
            _onbBody.MaxWidthUnits = 14.2f;
            _onbBody.transform.localPosition = new Vector3(0f, 0.4f, 0f);
            for (int i = 0; i < 3; i++)
            {
                var dot = SpriteRendererUtil.Make(_onbCard, "onbDot" + i, TexArt.Dot(), 6006);
                dot.transform.localPosition = new Vector3((i - 1) * 1.1f, -2.6f, 0f);
                dot.transform.localScale = Vector3.one * 0.35f;
                _onbDots.Add(dot);
            }
            _onbRows = BuildRows(_onbCard);
        }

        /// <summary>Marn's stall as a menu card: wares with prices in the value column,
        /// the purse total under the title. Rebuilt on every buy so gold and rows stay true.</summary>
        void BuildShop()
        {
            _shopRoot = Root("shop", 0);
            FullQuad(_shopRoot, "dim", 6000, new Color(6f / 255f, 5f / 255f, 16f / 255f, 0.9f));
            _shopCard = new GameObject("card").transform;
            _shopCard.SetParent(_shopRoot, false);
            _shopPanel = Panel(_shopCard, "shopPanel", 6002, 16.4f, 20f, 0.2f);
            _shopTitle = PixelLabelUtil.Make(_shopCard, "shopTitle", 3, new Color(1f, 0.95f, 0.78f), TextAlign.Center, 6006);
            _shopTitle.Set(Strings.Get("shop.title"));
            _shopSub = PixelLabelUtil.Make(_shopCard, "shopSub", 1, new Color(0.9f, 0.9f, 0.6f), TextAlign.Center, 6006);
            _shopFoot = PixelLabelUtil.Make(_shopCard, "shopFoot", 1, new Color(0.6f, 0.64f, 0.86f), TextAlign.Center, 6007);
            _shopFoot.Set(Strings.Get("shop.hint"));
            _shopRows = BuildRows(_shopCard);
        }

        void BuildToast()
        {
            _toastRoot = Root("toast", 0);
            _toastPanel = SpriteRendererUtil.Make(_toastRoot, "toastPanel", TexArt.Panel(), 6100);
            _toastPanel.drawMode = SpriteDrawMode.Sliced;
            _toastPanel.size = new Vector2(14f, 1.6f);
            _toastPanel.color = new Color(1f, 1f, 1f, 0.92f);
            _toast = PixelLabelUtil.Make(_toastRoot, "toastText", 1, new Color(0.95f, 0.97f, 1f), TextAlign.Center, 6102);
            _toast.MaxWidthUnits = 13.4f;
            // A notice now lives in a lane of its own directly UNDER the HUD bar, the way an
            // objective line works in every game that has one: it hangs off the bar like a tab,
            // it is far above the hero (who stands at the centre of the frame), and it is out of
            // the way of the two things that own the foot of the screen - the narration box and
            // the command menu. The lane is pinned by its *top* edge, so a two line notice grows
            // downward into the sky instead of jumping up the screen. Geometry, in stage units
            // from the centre:
            //   HUD bar  halfH .. halfH-3.8        notice  halfH-4.05 .. down
            //   dialog box  -halfH+0.4 .. about -10  hero  about -1.3 .. 0
            // The old toast was a fixed slab at the foot of the frame, which on a phone puts a
            // hint about villagers right under the hero's feet, in the middle of the playfield.
            _toastTopY = HalfH - 4.05f;
            _toastRoot.gameObject.SetActive(false);
        }

        float _toastTopY;              // the lane's ceiling, which the plate hangs down from
        float _toastY;                 // the plate's centre, which is what ToastRect reports
        float _toastTextY;             // the text's anchor (top of the first line)
        float _toastLift;              // the slide-in offset, in whole-ish pixels, settling at 0
        float _toastRise;              // 0 the frame it appears, 1 once it has settled
        string _toastHeld;             // a notice that arrived while a dialog box was up
        float _toastHeldT;
        Transform _cardSlide;          // the card currently dropping in; null once settled
        Transform _setCard, _credCard, _pauseCard, _shopCard, _onbCard;   // card content under the dim
        float _cardSlideT;             // settle progress 0..1

        /// <summary>While this is true a toast is parked instead of drawn. Game sets it while a
        /// dialog box is open: a notification over narration is the one overlap a player reads as
        /// a bug rather than as decoration.</summary>
        public bool HoldToasts
        {
            get { return _holdToasts; }
            set
            {
                _holdToasts = value;
                // The notice already up is parked too, not just the next one. Notices now live at
                // the foot of the frame, which is where the dialog box opens: a live toast plus
                // narration would be the same overlap in the same lane. It is not dropped -- it
                // waits for the box to close, like every other notice.
                if (value && _toastRoot != null && _toastRoot.gameObject.activeSelf)
                {
                    _toastHeld = _toast.Text;
                    _toastHeldT = Mathf.Max(1.2f, _toastT);
                    _toastRoot.gameObject.SetActive(false);
                }
            }
        }
        bool _holdToasts;

        /// <summary>The toast's rectangle in stage units, for whoever has to keep text out from
        /// under it. Empty when there is no toast.</summary>
        public Rect ToastRect
        {
            get
            {
                if (_toastRoot == null || !_toastRoot.gameObject.activeSelf) return new Rect(0f, 0f, 0f, 0f);
                var s = _toastPanel.size;
                float cy = _root.localPosition.y + _toastY + _toastLift;
                return new Rect(_root.localPosition.x - s.x * 0.5f, cy - s.y * 0.5f, s.x, s.y);
            }
        }

        SpriteRenderer Panel(Transform parent, string name, int sorting, float w, float h, float y)
        {
            var sr = SpriteRendererUtil.Make(parent, name, TexArt.Panel(), sorting);
            sr.drawMode = SpriteDrawMode.Sliced;
            sr.size = new Vector2(w, h);
            sr.transform.localPosition = new Vector3(0f, y, 0f);
            return sr;
        }

        List<Row> BuildRows(Transform parent)
        {
            var rows = new List<Row>();
            for (int i = 0; i < 9; i++)
            {
                var r = new Row();
                r.Panel = SpriteRendererUtil.Make(parent, "row" + i, TexArt.Panel(), 6004);
                r.Panel.drawMode = SpriteDrawMode.Sliced;
                r.Chev = SpriteRendererUtil.Make(parent, "rowChev" + i, TexArt.Chevron(), 6007);
                r.Chev.transform.localScale = Vector3.one * 1.5f;
                r.Icon = SpriteRendererUtil.Make(parent, "rowIcon" + i, null, 6007);
                r.Icon.transform.localScale = Vector3.one * 1.4f;
                r.Icon.enabled = false;
                r.Text = PixelLabelUtil.Make(parent, "rowText" + i, 2, Color.white, TextAlign.Left, 6006);
                r.ValLabel = PixelLabelUtil.Make(parent, "rowVal" + i, 2, new Color(0.85f, 0.88f, 1f), TextAlign.Right, 6006);
                rows.Add(r);
            }
            return rows;
        }

        void HideAll()
        {
            // The toast used to survive every screen change: it was not in this list, so a
            // "NEW QUEST" notification stayed on top of the pause card, the journal and the
            // ending. A notification belongs to the world; a card replaces the world.
            if (_toastRoot != null) _toastRoot.gameObject.SetActive(false);
            _splashRoot.gameObject.SetActive(false);
            _mainRoot.gameObject.SetActive(false);
            _setRoot.gameObject.SetActive(false);
            _credRoot.gameObject.SetActive(false);
            _pauseRoot.gameObject.SetActive(false);
            _ciRoot.gameObject.SetActive(false);
            _ccRoot.gameObject.SetActive(false);
            if (_jrRoot != null) _jrRoot.gameObject.SetActive(false);
            if (_pageRoot != null) _pageRoot.gameObject.SetActive(false);
            if (_onbRoot != null) _onbRoot.gameObject.SetActive(false);
            if (_shopRoot != null) _shopRoot.gameObject.SetActive(false);
            if (_mapSr != null)
            {
                _mapSr.enabled = false;
                _mapHeroDot.enabled = false;
                _mapQuestDot.enabled = false;
                for (int i = 0; i < 4; i++) _mapZoneLbl[i].gameObject.SetActive(false);
            }
        }

        // ------------------------------------------------------------------ screens

        public void ShowSplash()
        {
            HideAll();
            _sc = Sc.Splash;
            _t = 0f;
            SetSplashAlpha(Application.isPlaying ? 0f : 1f);
            _splashRoot.gameObject.SetActive(true);
        }

        public void ShowMain()
        {
            HideAll();
            _sc = Sc.Main;
            _t = 0f;
            _sel = 0;
            _mainRoot.gameObject.SetActive(true);

            bool hasSave = SaveSystem.Exists();
            // seven rows, one each for the things a finished game does at its front door:
            // play, carry on, re-watch the story, tune it, meet it, tell a friend, rate it
            string[] labels =
            {
                Strings.Get("menu.new"), Strings.Get("menu.continue"), Strings.Get("menu.story"),
                Strings.Get("menu.settings"), Strings.Get("menu.about"),
                Strings.Get("menu.share"), Strings.Get("menu.rate"),
            };
            var acts = new Action[]
            {
                () => { if (hasSave) ShowConfirm(() => OnStartNew?.Invoke()); else OnStartNew?.Invoke(); },
                () => OnLoadSave?.Invoke(),
                () => OnStory?.Invoke(),
                () => ShowSettings(false),
                () => ShowCredits(),
                () => DoShare(),
                () => DoRate(),
            };
            var vals = new string[] { "", hasSave ? SaveStamp() : Strings.Get("menu.nosave"), "", "", "", "", "" };
            LayRows(_mainRows, labels, acts, vals, 2.6f, 7, new[] { 24, 4, 5, 7, 23, 22, 21 });
            for (int i = 0; i < 7; i++) _mainRows[i].Enabled = i != 1 || hasSave;
            _sel = hasSave ? 1 : 0;
            Select(_sel);
        }

        /// <summary>Copy the store link so a player can paste it anywhere. A proper share sheet
        /// needs a plugin; the clipboard version works on every platform and costs nothing.</summary>
        void DoShare()
        {
            GUIUtility.systemCopyBuffer = Strings.Get("share.text", StoreUrl);
            Sfx.Play("ui");
            ShowToast(Strings.Get("share.copied"), 3.2f);
        }

        /// <summary>market:// on a phone opens the Play Store app; everywhere else the web page
        /// is the same page.</summary>
        void DoRate()
        {
            Sfx.Play("ui");
            ShowToast(Strings.Get("rate.thanks"), 3.0f);
#if UNITY_ANDROID && !UNITY_EDITOR
            Application.OpenURL("market://details?id=" + Application.identifier);
#else
            Application.OpenURL(StoreUrl);
#endif
        }

        string SaveStamp()
        {
            var d = SaveSystem.Read();
            return d == null || string.IsNullOrEmpty(d.stamp) ? "" : Strings.Get("hud.nightshort", d.chapter);
        }

        public void ShowSettings(bool fromPause)
        {
            HideAll();
            _sc = Sc.Settings;
            _settingsFromPause = fromPause;
            _sel = 0;
            _setRoot.gameObject.SetActive(true);
            SlideIn(_setCard);
            RefreshSettingsRows();
            Select(_sel);
        }

        void RefreshSettingsRows() => RefreshSettingsRows(false);

        void RefreshSettingsRows(bool keepArm)
        {
            if (!keepArm) _wipeArmed = false;
            var labels = new List<string>
            {
                Strings.Get("set.textspeed"),
                Strings.Get("set.music"),
                Strings.Get("set.sound"),
                Strings.Get("set.shake"),
                Strings.Get("set.autobattle"),
                Strings.Get("set.difficulty"),
            };
            var acts = new List<Action>
            {
                () => { Prefs.SpeedIndex = (Prefs.SpeedIndex + 1) % 4; Prefs.Store(); RefreshSettingsRows(); Select(_sel); },
                () => { Prefs.MusicLevel = (Prefs.MusicLevel + 4) % 5; Prefs.Store(); RefreshSettingsRows(); Select(_sel); },
                () => { Prefs.SoundLevel = (Prefs.SoundLevel + 4) % 5; Prefs.Store(); RefreshSettingsRows(); Select(_sel); },
                () => { Prefs.Shake = !Prefs.Shake; Prefs.Store(); RefreshSettingsRows(); Select(_sel); },
                () => { Prefs.Auto = !Prefs.Auto; Prefs.Store(); RefreshSettingsRows(); Select(_sel); },
                () => { Prefs.Story = !Prefs.Story; Prefs.Store(); RefreshSettingsRows(); Select(_sel); },
            };
            var vals = new List<string>
            {
                Prefs.SpeedName,
                Prefs.MusicLevel <= 0 ? Strings.Get("set.off") : (Prefs.MusicLevel * 25) + "%",
                Prefs.SoundLevel <= 0 ? Strings.Get("set.off") : (Prefs.SoundLevel * 25) + "%",
                Prefs.Shake ? Strings.Get("set.on") : Strings.Get("set.off"),
                Prefs.Auto ? Strings.Get("set.on") : Strings.Get("set.off"),
                Prefs.Story ? Strings.Get("set.diff.story") : Strings.Get("set.diff.normal"),
            };

            var icons = new List<int> { 9, 10, 11, 12, 13, 18 };
            // the wipe lives only on the title-side card: erasing mid-run would be
            // rewritten by the next autosave, which reads as the button doing nothing
            if (!_settingsFromPause)
            {
                labels.Add(_wipeArmed ? Strings.Get("set.erase.sure") : Strings.Get("set.erase"));
                acts.Add(() =>
                {
                    if (!_wipeArmed) { _wipeArmed = true; RefreshSettingsRows(true); Select(_sel); return; }
                    _wipeArmed = false;
                    SaveSystem.Erase();
                    ShowMain();
                    ShowToast(Strings.Get("set.erased"), 3f);
                });
                vals.Add("");
                icons.Add(15);
            }
            labels.Add(Strings.Get("menu.back"));
            acts.Add(() => { if (_settingsFromPause) ShowPause(); else ShowMain(); });
            vals.Add("");
            icons.Add(14);

            float rowsTop = LayoutCard(_setPanel, 16.4f, labels.Count, true);
            _setTitle.transform.localPosition = new Vector3(0f, _cardTop - 2.15f, 0f);
            float bottom = LayRows(_setRows, labels.ToArray(), acts.ToArray(), vals.ToArray(), rowsTop, labels.Count, icons.ToArray());
            _setFoot.transform.localPosition = new Vector3(0f, FootY(bottom), 0f);
            Select(_sel);
        }

        /// <summary>About = the credits card plus the two things a player wants from it:
        /// a way to rate and a way to share. The rows sit under the story text.</summary>
        public void ShowCredits()
        {
            HideAll();
            _sc = Sc.Credits;
            _sel = 0;
            _credRoot.gameObject.SetActive(true);
            SlideIn(_credCard);
            var labels = new[] { Strings.Get("menu.rate"), Strings.Get("menu.share"), Strings.Get("menu.back") };
            var acts = new Action[] { () => DoRate(), () => DoShare(), (Action)ShowMain };
            var vals = new[] { "", "", "" };
            LayRows(_credRows, labels, acts, vals, -5.4f, 3, new[] { 21, 22, 14 });
            LayoutCredits();
            Select(0);
        }

        /// <summary>The first-boot onboarding: one card, three pages of "what is this game",
        /// dots underneath, a row that reads NEXT until the last page where it reads BEGIN.
        /// It runs once ever; OnOnboardDone lands on the title screen.</summary>
        public void ShowOnboard()
        {
            HideAll();
            _sc = Sc.Onboard;
            _onbPage = 0;
            _sel = 0;
            _onbRoot.gameObject.SetActive(true);
            SlideIn(_onbCard);
            RefreshOnboard();
            Select(0);
        }

        void RefreshOnboard()
        {
            _onbTitle.Set(Strings.Get("onb.title." + (_onbPage + 1)));
            _onbBody.Set(Strings.Get("onb.body." + (_onbPage + 1)));
            for (int i = 0; i < _onbDots.Count; i++)
                _onbDots[i].color = i == _onbPage
                    ? new Color(1f, 0.93f, 0.55f)
                    : new Color(0.5f, 0.55f, 0.8f, 0.45f);
            bool last = _onbPage >= 2;
            LayRows(_onbRows, new[] { Strings.Get(last ? "onb.start" : "onb.next") },
                new Action[] { NextOnboard }, new[] { "" }, -4.2f, 1);
            Select(0);
        }

        void NextOnboard()
        {
            Sfx.Play("ui");
            if (_onbPage < 2) { _onbPage++; RefreshOnboard(); return; }
            OnOnboardDone?.Invoke();
        }

        /// <summary>Marn's stall: a pause-like card the shopkeeper opens instead of dialogue.
        /// Rows are his wares; the value column is the price; BACK hands control to Game.</summary>
        bool _shopSell;

        public void ShowShop()
        {
            HideAll();
            _sc = Sc.Shop;
            _sel = 0;
            _shopSell = false;
            _shopRoot.gameObject.SetActive(true);
            SlideIn(_shopCard);
            RefreshShop();
            Select(0);
        }

        /// <summary>What Marn stocks tonight: the pantry plus whatever gear this chapter sells.</summary>
        static string[] ShopStock()
        {
            switch (Mathf.Clamp(Game.State.Chapter, 1, 3))
            {
                case 1: return new[] { "item.berry", "item.morsel", "item.honey",
                                       "item.spoon", "item.cloak", "item.knife", "item.charm.bell" };
                case 2: return new[] { "item.berry", "item.morsel", "item.honey", "item.soup",
                                       "item.knife", "item.vest", "item.charm.bell", "item.charm.thread" };
                default: return new[] { "item.morsel", "item.honey", "item.soup", "item.tea",
                                        "item.sickle", "item.mail", "item.blade", "item.charm.moon" };
            }
        }

        void RefreshShop()
        {
            _shopSub.Set(Strings.Get("shop.sub", Game.State.Gold));
            var labels = new List<string>();
            var vals = new List<string>();
            var acts = new List<Action>();
            var icons = new List<int>();
            int iconOf(ItemKind kind) => kind == ItemKind.Food ? 2
                : kind == ItemKind.Blade ? 0 : kind == ItemKind.Cloth ? 18 : 25;
            if (_shopSell)
            {
                // Marn buys anything that isn't already on your back or tied to a
                // quest: half her shelf price, she says, and a story thrown in free.
                var keys = new List<string>();
                foreach (var b in Game.State.Bag) if (!keys.Contains(b)) keys.Add(b);
                int shown = 0;
                foreach (var key in keys)
                {
                    var def = Items.Get(key);
                    if (def.Kind == ItemKind.Key) continue;          // quest things stay
                    if (Items.IsEquip(def.Kind) && IsWorn(key)) continue;   // on your back
                    int n = Game.State.BagCount(key);
                    labels.Add(Strings.Get(key) + (n > 1 ? " x" + n : ""));
                    vals.Add(Mathf.Max(1, def.Price / 2) + " G");
                    icons.Add(iconOf(def.Kind));
                    var k = key;
                    acts.Add(() => Sell(k));
                    shown++;
                }
                if (shown == 0) { AddK(labels, vals, acts, "jr.empty", ""); icons.Add(-1); }
                labels.Add(Strings.Get("shop.buymode"));
                vals.Add("");
                acts.Add(() => { _shopSell = false; RefreshShop(); Select(0); });
                icons.Add(28);
            }
            else
            {
                foreach (var key in ShopStock())
                {
                    var def = Items.Get(key);
                    labels.Add(Strings.Get(key) + "  " + Items.Effect(def));
                    bool owned = Items.IsEquip(def.Kind)
                        && (Game.State.BagCount(key) > 0 || Array.IndexOf(Game.State.Worn, key) >= 0);
                    vals.Add(owned ? Strings.Get("shop.owned") : def.Price + " G");
                    var k = key;
                    acts.Add(() => Buy(k));
                    icons.Add(iconOf(def.Kind));
                }
                labels.Add(Strings.Get("shop.sellmode"));
                vals.Add("");
                acts.Add(() => { _shopSell = true; RefreshShop(); Select(0); });
                icons.Add(28);
                labels.Add(Strings.Get("menu.back"));
                vals.Add("");
                acts.Add(() => OnShopClosed?.Invoke());
                icons.Add(14);
            }
            float rowsTop = LayoutCard(_shopPanel, 16.4f, labels.Count, true);
            // placed AFTER the card is sized: read _cardTop before LayoutCard rewrote it and
            // the sign and the gold line floated down into the middle of the wares
            _shopTitle.transform.localPosition = new Vector3(0f, _cardTop - 1.7f, 0f);
            _shopSub.transform.localPosition = new Vector3(0f, _cardTop - 3.6f, 0f);
            float bottom = LayRows(_shopRows, labels.ToArray(), acts.ToArray(), vals.ToArray(), rowsTop, labels.Count, icons.ToArray());
            _shopFoot.transform.localPosition = new Vector3(0f, FootY(bottom), 0f);
        }

        void Sell(string key)
        {
            var def = Items.Get(key);
            int got = Mathf.Max(1, def.Price / 2);
            Game.State.RemoveBag(key);
            Game.State.Gold += got;
            Sfx.Play("coin");
            ShowToast(Strings.Get("shop.soldout", got), 2.6f);
            RefreshShop();
            Select(0);
        }

        void Buy(string key)
        {
            var def = Items.Get(key);
            if (Items.IsEquip(def.Kind)
                && (Game.State.BagCount(key) > 0 || Array.IndexOf(Game.State.Worn, key) >= 0))
            {
                Sfx.Play("fail");
                ShowToast(Strings.Get("shop.have"), 2.6f);
                return;
            }
            if (Game.State.Gold < def.Price)
            {
                Sfx.Play("fail");
                ShowToast(Strings.Get("shop.poor"), 2.6f);
                return;
            }
            Game.State.Gold -= def.Price;
            Game.State.AddBag(key);
            Sfx.Play("buy");
            ShowToast(Strings.Get("shop.sold"), 2.6f);
            RefreshShop();
            Select(_sel);
        }

        /// <summary>Credits flow: fixed title block, then the body and the closing line are
        /// stacked from their measured heights. Hard placed, a longer body printed straight
        /// through the thank-you line and the BACK button.</summary>
        void LayoutCredits()
        {
            const float backRowTop = -5.4f;                         // the first of RATE/SHARE/BACK
            const float bandTop = 0.9f, bandBottom = -3.0f;         // between the two hairlines
            float floor = backRowTop + 1.15f;                       // clear of the row panels
            float thanksH = PixelFont.LineHeight(_credThanks.Scale);
            float thanksTop = floor + thanksH;
            _credThanks.transform.localPosition = new Vector3(0f, Fx.Snap(thanksTop), 0f);

            // the body starts at scale 2 and only steps down when it would not fit the band
            int scale = 2;
            float h = MeasuredBodyHeight(scale);
            while (scale > 1 && (bandTop - h < bandBottom || bandTop - h < thanksTop + 0.45f))
            {
                scale--;
                _credText.Scale = scale;
                _credText.Set(_credText.Text);
                h = MeasuredBodyHeight(scale);
            }
            _credText.Scale = scale;
            _credText.Set(_credText.Text);
            // centred in its band: hugging the top hairline left a hole under the block
            _credText.transform.localPosition = new Vector3(0f, Fx.Snap((bandTop + bandBottom + h) * 0.5f), 0f);
        }

        float MeasuredBodyHeight(int scale)
        {
            _credText.Scale = scale;
            return _credText.MeasureHeight(_credText.Text);
        }

        /// <summary>A yes/no card over the pause furniture, for choices that should not be
        /// one tap away from a save (START OVER rewrites the night). The yes action is left
        /// as a delegate so the same card can ask other questions later.</summary>
        public void ShowConfirm(Action yes) => ShowConfirm(yes, null, null, null, null);

        /// <summary>The one-tap-away card: wording and the no-path vary with what is being
        /// asked - a save overwrite, a friend set free - but the safe answer always
        /// selects first.</summary>
        public void ShowConfirm(Action yes, string title, string sub, string yesLabel, Action no)
        {
            HideAll();
            _sc = Sc.Confirm;
            _sel = 1;
            _t = 0f;
            _pauseRoot.gameObject.SetActive(true);
            SlideIn(_pauseCard);
            _pauseTitle.Set(title ?? Strings.Get("conf.title"));
            _pauseSub.Set(sub ?? Strings.Get("conf.sub"));
            var acts = new Action[] { () => yes?.Invoke(), () => { if (no != null) no(); else ShowMain(); } };
            float rowsTop = LayoutCard(_pausePanel, 16.4f, 2, true);
            _pauseTitle.transform.localPosition = new Vector3(0f, _cardTop - 2.15f, 0f);
            float bottom = LayRows(_pauseRows,
                new[] { yesLabel ?? Strings.Get("conf.yes"), Strings.Get("conf.no") },
                acts, new[] { "", "" }, rowsTop, 2, new[] { 26, 27 });
            _pauseSub.transform.localPosition = new Vector3(0f, FootY(bottom), 0f);
            Select(1);   // the safe answer is selected first
        }

        public void ShowPause()
        {
            HideAll();
            _sc = Sc.Pause;
            _sel = 0;
            _t = 0f;
            _pauseRoot.gameObject.SetActive(true);
            SlideIn(_pauseCard);
            _pauseTitle.Set(Strings.Get("pause.title"));
            // the footnote of the pause card is both its status line ("saved") and, before that,
            // the one hint a player needs at the moment they stop playing
            _pauseSub.Set(Strings.Get("pause.hint"));
            string[] labels =
            {
                Strings.Get("pause.resume"), Strings.Get("pause.journal"),
                Strings.Get("pause.save"), Strings.Get("pause.settings"),
                Strings.Get("pause.totitle"),
            };
            var acts = new Action[]
            {
                () => OnResume?.Invoke(),
                () => ShowJournal(),
                () => { OnSaveGame?.Invoke(); _pauseSub.Set(Strings.Get("set.saved")); },
                () => ShowSettings(true),
                () => OnLeaveToTitle?.Invoke(),
            };
            // just the count: "QUESTS 0/10" in the value column forced the value down a size while
            // its neighbours stayed at 2, and the row it labels is already called JOURNAL
            var vals = new string[]
            {
                "", Quests.ActiveCount + "/" + (Quests.All.Length - 3), "", "", "",
            };
            float rowsTop = LayoutCard(_pausePanel, 16.4f, 5, true);
            _pauseTitle.transform.localPosition = new Vector3(0f, _cardTop - 2.15f, 0f);
            float bottom = LayRows(_pauseRows, labels, acts, vals, rowsTop, 5, new[] { 4, 5, 6, 7, 8 });
            _pauseSub.transform.localPosition = new Vector3(0f, FootY(bottom), 0f);
            Select(0);
        }

        /// <summary>The story slides. intro slides run once per new run; the chapter cards
        /// are handled by ShowChapterCard.</summary>
        public void ShowCinema(int from)
        {
            HideAll();
            _sc = Sc.Cinema;
            _ciIndex = from;
            _t = 0f;
            _ciHold = 0f;
            _ciRoot.gameObject.SetActive(true);
            RefreshCinemaSlide();
        }

        public void ShowChapterCard(int chapter)
        {
            HideAll();
            _sc = Sc.ChapterCard;
            _t = 0f;
            _ccRoot.gameObject.SetActive(true);
            // the card carries two lines: night title and place name
            var parts = Strings.Get("ci.ch." + Mathf.Clamp(chapter, 1, 3)).Split('\n');
            _ccNight.Set(parts.Length > 0 ? parts[0] : "");
            _ccPlace.Set(parts.Length > 1 ? parts[1] : "");
            // and what tonight wants: a goal line so the card is a briefing, not a poster
            _ccGoal.Set(Strings.Has("ci.goal." + chapter) ? Strings.Get("ci.goal." + chapter) : "");
        }

        public void Hide()
        {
            HideAll();
            _sc = Sc.None;
        }

        // ------------------------------------------------------------------ the journal

        /// <summary>The hub the pause card opens: character sheet, bag, equipment, bestiary and
        /// the quest log. Everything on these pages is read from the run's real state.</summary>
        public void ShowJournal()
        {
            HideAll();
            _sc = Sc.Journal;
            _sel = 0;
            _t = 0f;
            _jrRoot.gameObject.SetActive(true);
            SlideIn(_jrRoot);
            float rowsTop = LayoutCard(_jrPanel, 16.4f, 7, true);
            _jrTitle.transform.localPosition = new Vector3(0f, _cardTop - 1.9f, 0f);
            _jrSub.transform.localPosition = new Vector3(0f, _cardTop - 3.5f, 0f);
            _jrSub.Set(Strings.Get("jr.sub", Game.State.Level, Game.State.Gold));
            var labels = new[]
            {
                Strings.Get("jr.character"), Strings.Get("jr.items"), Strings.Get("jr.equip"),
                Strings.Get("jr.bestiary"), Strings.Get("jr.quests"), Strings.Get("jr.map"),
                Strings.Get("menu.back"),
            };
            var acts = new Action[]
            {
                () => ShowPage(Page2.Character), () => ShowPage(Page2.Items),
                () => ShowPage(Page2.Equipment), () => ShowPage(Page2.Bestiary),
                () => ShowPage(Page2.Quests), () => ShowPage(Page2.Map), () => ShowPause(),
            };
            // the value column is a column of counts and short states, matching the shape of the
            // rows: a worn blade's name ("KITCHEN KNIFE") forced both cells down a size and the
            // hub came out in two typefaces. Slots worn is the same fact, one glance wide.
            var vals = new[]
            {
                "L" + Game.State.Level, Game.State.Bag.Count.ToString(), WornCount() + "/3",
                Game.State.Seen.Count + "/" + (BattleData.Bestiary.Length + 1),
                Quests.ActiveCount + "/" + (Quests.All.Length - 3),
                Strings.Get("zone.short." + Game.State.CurZone), "",
            };
            float bottom = LayRows(_jrRows, labels, acts, vals, rowsTop, 7, new[] { 16, 17, 18, 19, 20, 24, 14 });
            _jrFoot.transform.localPosition = new Vector3(0f, FootY(bottom), 0f);
            _jrFoot.Set(Strings.Get("jr.hint"));
            Select(0);
        }

        /// <summary>Slots with something worn in them, for the hub's value column.</summary>
        string WornCount()
        {
            int n = 0;
            for (int i = 0; i < 3; i++) if (!string.IsNullOrEmpty(Game.State.Worn[i])) n++;
            return n.ToString();
        }

        string WornWord(int slot) => string.IsNullOrEmpty(Game.State.Worn[slot])
            ? Strings.Get("jr.none") : Strings.Get(Game.State.Worn[slot]);

        bool IsWorn(string key)
        {
            for (int i = 0; i < 3; i++) if (Game.State.Worn[i] == key) return true;
            return false;
        }

        /// <summary>Cycles a slot through everything of that kind in the bag. One tap per change,
        /// which is all a slot needs on a phone.</summary>
        void CycleWorn(ItemKind kind)
        {
            int slot = Items.SlotOf(kind);
            var options = new List<string>();
            foreach (var b in Game.State.Bag)
                if (Items.Get(b).Kind == kind && !options.Contains(b)) options.Add(b);
            if (options.Count == 0) { Game.State.Worn[slot] = null; return; }
            int at = options.IndexOf(Game.State.Worn[slot]);
            Game.State.Worn[slot] = at + 1 >= options.Count ? null : options[at + 1];
            Sfx.Play("ui");
        }

        void Equip(string key)
        {
            var def = Items.Get(key);
            int slot = Items.SlotOf(def.Kind);
            Game.State.Worn[slot] = Game.State.Worn[slot] == key ? null : key;
            Sfx.Play("ui");
        }

        /// <summary>Builds one journal page. Every list is paginated through the same layout, so a
        /// bag with twenty things in it is still a card with six rows.</summary>
        void ShowPage(Page2 kind)
        {
            if (kind == Page2.Map) { ShowMapCard(); return; }
            var labels = new List<string>();
            var vals = new List<string>();
            var acts = new List<Action>();
            List<int> icons = null;
            List<Sprite> sprites = null;
            string title;
            string sub = "";

            switch (kind)
            {
                case Page2.Character:
                    title = "jr.character";
                    sub = Strings.Get("jr.sub", Game.State.Level, Game.State.Gold);
                    AddK(labels, vals, acts, "jr.level", "L" + Game.State.Level);
                    AddK(labels, vals, acts, "jr.xp", Game.State.Xp + "/" + Game.State.NextLevelAt);
                    AddK(labels, vals, acts, "jr.shards", Game.State.MoonShards + "/" + Game.ShardsNeeded);
                    AddK(labels, vals, acts, "jr.befriended", Game.State.Befriended.ToString());
                    AddK(labels, vals, acts, "jr.felled", Game.State.Defeats.ToString());
                    AddK(labels, vals, acts, "jr.chests", Game.State.ChestsOpened.ToString());
                    AddK(labels, vals, acts, "jr.gold", Game.State.Gold.ToString());
                    AddK(labels, vals, acts, "jr.atk", "+" + Game.State.BonusAtk);
                    AddK(labels, vals, acts, "jr.maxhp", "+" + Game.State.BonusHp);
                    Add(labels, vals, acts, Strings.Get("jr.slot.blade"), WornWord(0),
                        () => { CycleWorn(ItemKind.Blade); ShowPage(Page2.Character); });
                    Add(labels, vals, acts, Strings.Get("jr.slot.cloth"), WornWord(1),
                        () => { CycleWorn(ItemKind.Cloth); ShowPage(Page2.Character); });
                    Add(labels, vals, acts, Strings.Get("jr.slot.charm"), WornWord(2),
                        () => { CycleWorn(ItemKind.Charm); ShowPage(Page2.Character); });
                    // a little pictogram per stat: level/star, xp/scroll, shards/moon,
                    // friends/paw, felled/sword, chests/bag, gold/coin, atk/bolt, hp/shield
                    icons = new List<int> { 21, 20, 24, 19, 0, 17, 28, 13, 18, 0, 18, 25 };
                    break;

                case Page2.Items:
                    title = "jr.items";
                    sub = Strings.Get("jr.items.sub", Game.State.Bag.Count);
                    {
                        icons = new List<int>();
                        var keys = new List<string>();
                        foreach (var b in Game.State.Bag) if (!keys.Contains(b)) keys.Add(b);
                        if (keys.Count == 0) { AddK(labels, vals, acts, "jr.empty", ""); icons.Add(-1); }
                        foreach (var key in keys)
                        {
                            var def = Items.Get(key);
                            int n = Game.State.BagCount(key);
                            bool worn = IsWorn(key);
                            labels.Add(Strings.Get(key) + (n > 1 ? " x" + n : ""));
                            vals.Add(worn ? Strings.Get("jr.worn") : Items.Effect(def));
                            icons.Add(def.Kind == ItemKind.Food ? 2
                                : def.Kind == ItemKind.Blade ? 0
                                : def.Kind == ItemKind.Cloth ? 18 : 25);
                            string k = key;
                            var d = def;
                            acts.Add(() =>
                            {
                                if (Items.IsEquip(d.Kind)) { Equip(k); ShowPage(Page2.Items); }
                                else if (d.Kind == ItemKind.Key) ShowToast(Strings.Get("jr.keyitem"), 2.6f);
                                else ShowToast(Strings.Get("jr.usefight"), 2.8f);
                            });
                        }
                    }
                    break;

                case Page2.Equipment:
                    title = "jr.equip";
                    sub = Strings.Get("jr.equip.sub");
                    Add(labels, vals, acts, Strings.Get("jr.slot.blade"), WornWord(0),
                        () => { CycleWorn(ItemKind.Blade); ShowPage(Page2.Equipment); });
                    Add(labels, vals, acts, Strings.Get("jr.slot.cloth"), WornWord(1),
                        () => { CycleWorn(ItemKind.Cloth); ShowPage(Page2.Equipment); });
                    Add(labels, vals, acts, Strings.Get("jr.slot.charm"), WornWord(2),
                        () => { CycleWorn(ItemKind.Charm); ShowPage(Page2.Equipment); });
                    AddK(labels, vals, acts, "jr.atk", "+" + Game.State.BonusAtk);
                    AddK(labels, vals, acts, "jr.maxhp", "+" + Game.State.BonusHp);
                    icons = new List<int> { 0, 18, 25, 13, 18 };
                    break;

                case Page2.Bestiary:
                    title = "jr.bestiary";
                    sub = Strings.Get("jr.bestiary.sub", Game.State.Seen.Count, BattleData.Bestiary.Length + 1);
                    sprites = new List<Sprite>();
                    foreach (var spec in BattleData.Bestiary)
                    {
                        AddBeast(labels, vals, acts, spec);
                        sprites.Add(Game.State.Seen.ContainsKey(spec.Name)
                            ? TexArt.MapMonster(spec.MapSheet, 1) : null);
                    }
                    AddBeast(labels, vals, acts, BattleData.Boss);
                    sprites.Add(Game.State.Seen.ContainsKey(BattleData.Boss.Name)
                        ? TexArt.MapMonster(BattleData.Boss.MapSheet, 1) : null);
                    break;

                default:
                    title = "jr.quests";
                    sub = Strings.Get("jr.quests.sub", Quests.ActiveCount, Quests.DoneCount);
                    icons = new List<int>();
                    foreach (var q in Quests.All)
                    {
                        int step = Quests.Step(q.Id);
                        if (!q.Main && step == 0) continue;   // side quests list only once taken on
                        labels.Add(Strings.Get(q.TitleKey));
                        // an active errand shows its count, not just the word ACTIVE - the
                        // journal is where the night's order lives, so it should say how far along
                        vals.Add(q.Main && step < 3 ? Strings.Get("jr.main")
                            : step == 1 ? Strings.Get("jr.prog", Mathf.Min(Quests.Progress(q), q.Need), q.Need)
                            : Quests.StateWord(step));
                        icons.Add(step == 3 ? 26 : 20);
                        var quest = q;
                        acts.Add(() => ShowToast(Quests.Line(quest), 4.2f));
                    }
                    break;
            }

            _pageLabels = labels.ToArray();
            _pageVals = vals.ToArray();
            _pageActs = acts.ToArray();
            _pageIcons = icons?.ToArray();
            _pageIconSprites = sprites?.ToArray();

            HideAll();
            _sc = Sc.Page;
            _pageIndex = 0;
            _sel = 0;
            _t = 0f;
            _pageRoot.gameObject.SetActive(true);
            SlideIn(_pageRoot);
            _pageTitle.Set(Strings.Get(title));
            _pageSub.Set(sub ?? "");
            LayoutPage();
            Select(0);
        }

        /// <summary>The frame renderer opens the journal and one page directly: the pause card and
        /// a tap are what normally get a player here, and neither exists in edit mode.</summary>
        public void EditorJournal(int page)
        {
            if (page < 0) { HideAll(); ShowJournal(); return; }
            HideAll();
            ShowPage((Page2)Mathf.Clamp(page, 0, 5));
        }

        // ---------------------------------------------------------------- world map card

        /// <summary>The map page is not a list: it is the night's actual ground drawn at one
        /// pixel a cell, zone names down the side, an amber dot where the hero stands and a
        /// violet spark where the night's errand is. The route south-to-north reads whole.</summary>
        void ShowMapCard()
        {
            HideAll();
            _sc = Sc.Page;
            _pageIndex = 0;
            _sel = 0;
            _t = 0f;
            _pageRoot.gameObject.SetActive(true);
            SlideIn(_pageRoot);
            _pageTitle.Set(Strings.Get("jr.map"));
            _pageSub.Set(Strings.Get("jr.map.sub"));

            GameMap m = GetMap != null ? GetMap() : null;
            if (m != null && m != _mapObj)
            {
                _mapObj = m;
                if (_mapSpr != null) { Destroy(_mapSpr.texture); Destroy(_mapSpr); _mapSpr = null; }
                var tex = m.MiniMapTex();
                _mapSpr = Sprite.Create(tex, new Rect(0f, 0f, GameMap.W, GameMap.H), new Vector2(0.5f, 0.5f), 8f);
            }

            bool haveMap = _mapSpr != null;
            float rowsTop = LayoutCard(_pagePanel, 16.4f, haveMap ? 7 : 6, true);
            _pageTitle.transform.localPosition = new Vector3(0f, _cardTop - 1.9f, 0f);
            _pageSub.transform.localPosition = new Vector3(0f, _cardTop - 3.5f, 0f);
            _pageFoot.Set(Strings.Get("jr.pagehint"));

            if (haveMap)
            {
                float upc = _mapSpr.bounds.size.y / GameMap.H;   // units per map cell (0.125)
                float mapCx = 2.6f, mapCy = rowsTop - 0.45f - _mapSpr.bounds.size.y * 0.5f;
                _mapSr.sprite = _mapSpr;
                _mapSr.transform.localPosition = new Vector3(mapCx, mapCy, 0f);
                _mapSr.enabled = true;

                // zone names ride the left gutter, each level with its stretch of the road
                string boss = Strings.Get(BattleData.BossNameKey(Game.State.Chapter));
                string[] zNames =
                {
                    Strings.Get("zone.name.village"), Strings.Get("zone.name.fields"),
                    Strings.Get("zone.name.wood"), boss,
                };
                float[] zYs = { 13f, 42f, 72f, GameMap.H - 5f };
                for (int i = 0; i < 4; i++)
                {
                    _mapZoneLbl[i].Set(zNames[i]);
                    _mapZoneLbl[i].transform.localPosition = new Vector3(-7.05f, mapCy + (zYs[i] - GameMap.H * 0.5f) * upc - 0.25f, 0f);
                    _mapZoneLbl[i].gameObject.SetActive(true);
                }

                var hp = GetHeroPos != null ? GetHeroPos() : null;
                if (hp.HasValue)
                {
                    _mapHeroDot.transform.localPosition = new Vector3(
                        mapCx + (hp.Value.x - GameMap.W * 0.5f) * upc,
                        mapCy + (hp.Value.y - GameMap.H * 0.5f) * upc, 0f);
                    _mapHeroDot.enabled = true;
                }
                var op = GetObjectivePos != null ? GetObjectivePos() : null;
                if (op.HasValue)
                {
                    _mapQuestDot.transform.localPosition = new Vector3(
                        mapCx + (op.Value.x - GameMap.W * 0.5f) * upc,
                        mapCy + (op.Value.y - GameMap.H * 0.5f) * upc, 0f);
                    _mapQuestDot.enabled = true;
                }
            }
            else _mapObj = null;

            float bottom = LayRows(_pageRows, new[] { Strings.Get("menu.back") },
                new Action[] { ShowJournal }, new[] { "" },
                haveMap ? rowsTop - _mapSpr.bounds.size.y - 0.9f : rowsTop, 1, new[] { 14 });
            _pageFoot.transform.localPosition = new Vector3(0f, FootY(bottom), 0f);
            Select(0);
        }

        GameMap _mapObj;

        void AddBeast(List<string> labels, List<string> vals, List<Action> acts, MonsterSpec spec)
        {
            bool known = Game.State.Seen.ContainsKey(spec.Name);
            // a species that walks with the party carries its mark on the page
            bool tamed = Game.State.Friends.Contains(spec.Name) || Game.State.Friends.Contains("moon." + spec.Name);
            labels.Add(known ? Strings.Get(spec.Name) : Strings.Get("jr.unknown"));
            // a kept friend's mark owns the value column: the stats line is long enough
            // that it crowds the name down to three letters, and the tag never printed
            vals.Add(known
                ? tamed ? Strings.Get("jr.friend")
                    : Strings.Get("jr.beast", spec.Hp, spec.AtkMin, spec.AtkMax, WeaknessOf(spec))
                : "?");
            var s2 = spec;
            if (tamed)
                // a kept friend can be wished back to the night: the mark clears, the
                // stable opens, and the page redraws without its tag
                acts.Add(() => ShowConfirm(
                    () => { OnReleaseFriend?.Invoke(s2.Name); ShowPage(Page2.Bestiary); },
                    Strings.Get("conf.reltitle"), Strings.Get("conf.relsub", Strings.Get(s2.Name)),
                    Strings.Get("conf.rel"), () => ShowPage(Page2.Bestiary)));
            else
                acts.Add(() => ShowToast(known ? Strings.Get(s2.Name + ".d") : Strings.Get("jr.unseen"), 2.6f));
        }

        /// <summary>Which friend this species is soft against, named on the card so the
        /// weakness table is a thing you can learn instead of a thing you guess at.</summary>
        static string WeaknessOf(MonsterSpec spec)
        {
            if (BattleData.StyleBeats(0, spec)) return Strings.Get("hero.amber");
            if (BattleData.StyleBeats(1, spec)) return Strings.Get("hero.sea");
            if (BattleData.StyleBeats(2, spec)) return Strings.Get("hero.moss");
            return "-";
        }

        static void Add(List<string> labels, List<string> vals, List<Action> acts,
            string label, string val, Action act = null)
        {
            labels.Add(label);
            vals.Add(val);
            acts.Add(act);
        }

        /// <summary>Same as Add, but the label is a string key. The character sheet used plain
        /// Add with keys in it, so the page printed "jr.level", "jr.xp", "jr.chests" -- the keys
        /// themselves -- down the left column of a finished card.</summary>
        static void AddK(List<string> labels, List<string> vals, List<Action> acts,
            string labelKey, string val, Action act = null)
        {
            labels.Add(Strings.Get(labelKey));
            vals.Add(val);
            acts.Add(act);
        }

        void LayoutPage()
        {
            int total = _pageLabels.Length;
            int pages = Mathf.Max(1, Mathf.CeilToInt(total / (float)PageRowsPerView));
            _pageIndex = Mathf.Clamp(_pageIndex, 0, pages - 1);
            int start = _pageIndex * PageRowsPerView;
            int count = Mathf.Min(PageRowsPerView, total - start);

            var labels = new List<string>();
            var vals = new List<string>();
            var acts = new List<Action>();
            var icons = _pageIcons == null ? null : new List<int>();
            var sprites = _pageIconSprites == null ? null : new List<Sprite>();
            for (int i = 0; i < count; i++)
            {
                labels.Add(_pageLabels[start + i]);
                vals.Add(_pageVals[start + i]);
                acts.Add(_pageActs[start + i]);
                icons?.Add(_pageIcons[start + i]);
                sprites?.Add(_pageIconSprites[start + i]);
            }
            if (pages > 1)
            {
                labels.Add(Strings.Get("jr.page", _pageIndex + 1, pages));
                vals.Add("");
                acts.Add(() => { _pageIndex = (_pageIndex + 1) % pages; LayoutPage(); Select(0); });
                icons?.Add(-1);
                sprites?.Add(null);
            }
            labels.Add(Strings.Get("menu.back"));
            vals.Add("");
            acts.Add(ShowJournal);
            icons?.Add(14);
            sprites?.Add(null);

            float rowsTop = LayoutCard(_pagePanel, 16.4f, labels.Count, true);
            _pageTitle.transform.localPosition = new Vector3(0f, _cardTop - 1.9f, 0f);
            _pageSub.transform.localPosition = new Vector3(0f, _cardTop - 3.5f, 0f);
            float bottom = LayRows(_pageRows, labels.ToArray(), acts.ToArray(), vals.ToArray(), rowsTop, labels.Count, icons?.ToArray(), sprites?.ToArray());
            _pageFoot.transform.localPosition = new Vector3(0f, FootY(bottom), 0f);
            _pageFoot.Set(Strings.Get("jr.pagehint"));
        }

        public void ShowToast(string text, float seconds = 3.2f)
        {
            if (HoldToasts) { _toastHeld = text; _toastHeldT = seconds; return; }
            DrawToast(text, seconds);
        }

        /// <summary>Takes the notice off screen. Used by the frame renderer, which has to stage a
        /// state the running game reaches on its own (the zone card owns the lane the notice
        /// shares, so the two are never up together).</summary>
        public void HideToast()
        {
            _toastHeld = null;
            _toastT = 0f;
            if (_toastRoot != null) _toastRoot.gameObject.SetActive(false);
        }

        /// <summary>Shows a notice that was parked while the dialog box was up. Game calls this
        /// the moment the box closes, so nothing a player earned is silently dropped.</summary>
        public void FlushToast()
        {
            if (string.IsNullOrEmpty(_toastHeld)) return;
            var text = _toastHeld;
            var secs = _toastHeldT;
            _toastHeld = null;
            DrawToast(text, secs);
        }

        void DrawToast(string text, float seconds)
        {
            _toastRoot.gameObject.SetActive(true);
            _toast.Set(text);
            // measured, not fixed: a short hint gets a slim card, a long one gets a taller one
            float tw = _toast.MeasureWidth(text), th = _toast.MeasureHeight(text);
            float w = Mathf.Clamp(tw + 1.5f, 4.5f, 15.6f);
            float h = Mathf.Max(1.6f, th + 0.95f);
            _toastY = _toastTopY - h * 0.5f;
            _toastPanel.size = new Vector2(w, h);
            // The text is centred in the plate. It used to be pinned a flat 0.3 units under the
            // plate's top edge, which is what made a two line notice look glued to the box.
            _toastTextY = Fx.Snap(_toastTopY - Mathf.Max(0.32f, (h - th) * 0.5f));
            // the editor render never ticks, so the card is shown settled there instead of
            // half way through its slide
            _toastRise = Application.isPlaying ? 0f : 1f;
            _toastLift = 0f;
            float shown = Application.isPlaying ? 0f : 1f;
            var c = _toastPanel.color; c.a = 0.92f * shown; _toastPanel.color = c;
            var t = _toast.Tint; t.a = shown; _toast.SetColor(t);
            _toastT = seconds;
            ApplyToastLift();
        }

        /// <summary>Fades one label in over its own delay on the chapter card.</summary>
        static void SetCardLine(PixelLabel l, Color c, float t)
        {
            c.a = Mathf.Clamp01(t * 2.2f);
            l.SetColor(c);
        }

        /// <summary>The card sheet rides a short drop into place; the dim under it snaps. The
        /// drop is short enough that it reads as weight, not as an animation you wait on.</summary>
        void SlideIn(Transform card)
        {
            _cardSlide = card;
            _cardSlideT = 0f;
            if (card != null) card.localPosition = new Vector3(0f, 0.55f, 0f);
        }

        void ApplyToastLift()
        {
            _toastPanel.transform.localPosition = new Vector3(0f, _toastY + _toastLift, 0f);
            _toast.transform.localPosition = new Vector3(0f, _toastTextY + _toastLift, 0f);
        }

        /// <summary>The whole card layer rides the camera so it stays on screen while walking.
        /// Anchoring only the pause card left the settings page it opens sitting at the world
        /// origin: opened out in the fields the player got an empty screen, because every card
        /// is laid out around its own origin and the camera was somewhere else entirely.</summary>
        public void SetAnchor(Vector3 camPos)
        {
            if (_root != null) _root.localPosition = camPos;
            // the two cards that used to carry the offset themselves are pinned back to the
            // origin, otherwise the camera offset would now be applied twice
            if (_pauseRoot != null) _pauseRoot.localPosition = Vector3.zero;
            if (_toastRoot != null) _toastRoot.localPosition = Vector3.zero;
        }

        // ------------------------------------------------------------------ rows

        /// <summary>Sizes a card around its own contents and returns the y its first row starts
        /// at. Cards used to be pinned by hand, which is how the journal's BACK row ended up
        /// hanging 1.25 units below its frame: the row count grew and the card did not.</summary>
        float LayoutCard(SpriteRenderer panel, float width, int rowCount, bool foot)
        {
            float rowsH = rowCount * RowH + Mathf.Max(0, rowCount - 1) * RowGap;
            float h = CardHead + rowsH + CardPad + (foot ? CardFoot : 0.55f);
            _cardTop = h * 0.5f;
            panel.size = new Vector2(width, h);
            panel.transform.localPosition = Vector3.zero;
            return _cardTop - CardHead;
        }

        /// <summary>Where a card's footnote goes: under the last row, inside the border. Every
        /// card that has one calls this with the value LayRows returned.</summary>
        static float FootY(float lastRowBottom) => lastRowBottom - 1.12f;

        float LayRows(List<Row> rows, string[] labels, Action[] acts, string[] vals, float topY, int count, int[] icons = null, Sprite[] sprites = null)
        {
            // One pitch for the whole list: evenly spaced rows read as a table, and the returned
            // bottom edge is what the card and its footnote are placed from.
            float pitch = RowH + RowGap;

            // The value column gets one type size for the whole list before any row is laid out:
            // a price/count column reads as a column, and two rows in a different size look like
            // a misprint (the shop used to show "8 G" big and "14 G" small side by side).
            const float colGap = 0.7f;
            float colLabelX = -RowW * 0.5f + TextInset;
            float colRoom = RowW * 0.5f - ValInset - colLabelX;
            int valCol = 2;
            for (int i = 0; i < count && i < vals.Length; i++)
            {
                if (string.IsNullOrEmpty(vals[i])) continue;
                if (PixelFont.Measure(labels[i], 2).x + colGap + PixelFont.Measure(vals[i], 2).x > colRoom) { valCol = 1; break; }
            }

            for (int i = 0; i < rows.Count; i++)
            {
                var r = rows[i];
                bool used = i < count;
                int icon = icons != null && i < icons.Length ? icons[i] : -1;
                Sprite spr = !used ? null
                    : sprites != null && i < sprites.Length && sprites[i] != null ? sprites[i]
                    : icon >= 0 ? TexArt.MenuIcon(icon) : null;
                bool hasIcon = spr != null;
                r.Icon.sprite = spr;
                r.Panel.gameObject.SetActive(used);
                r.Text.gameObject.SetActive(used);
                r.Chev.gameObject.SetActive(used);
                r.ValLabel.gameObject.SetActive(used && i < vals.Length && !string.IsNullOrEmpty(vals[i]));
                r.Icon.enabled = false;
                if (!used) continue;

                float y = topY - i * pitch;

                string val = i < vals.Length ? vals[i] : "";
                string label = labels[i];

                // Two columns, laid out as columns: the chevron owns its own lane inside the row's
                // padding, the label starts clear of it and the value is right aligned against the
                // inner edge. Values use the list-wide column size computed above; the label alone
                // steps down when the pair still overflows, so the column stays one size.
                const float gap = 0.7f;
                float iconW = hasIcon ? 1.25f : 0f;
                float labelX = -RowW * 0.5f + TextInset + iconW;
                float valX = RowW * 0.5f - ValInset;
                float room = valX - labelX;
                bool hasVal = !string.IsNullOrEmpty(val);

                int labelScale = 2, valScale = hasVal ? valCol : 2;
                if (hasVal)
                {
                    if (PixelFont.Measure(label, 2).x + gap + PixelFont.Measure(val, valScale).x > room) labelScale = 1;
                }
                else if (PixelFont.Measure(label, 2).x > room) labelScale = 1;

                int guard = 0;
                while (label.Length > 3
                       && PixelFont.Measure(label, labelScale).x + (hasVal ? gap + PixelFont.Measure(val, valScale).x : 0f) > room
                       && guard++ < 40)
                    label = label.Substring(0, label.Length - 1);   // last resort only

                r.Panel.size = new Vector2(RowW, RowH);
                r.Panel.transform.localPosition = new Vector3(0f, y - RowH * 0.5f, 0f);
                if (hasIcon)
                {
                    // big sprites (the 48px bestiary portraits) shrink to fit the row;
                    // the 9px pictograms keep their fixed size
                    r.Icon.transform.localScale = spr.bounds.size.y > 1.2f
                        ? Vector3.one * (1.5f / spr.bounds.size.y) : Vector3.one * 1.4f;
                    r.Icon.transform.localPosition = new Vector3(labelX - iconW + 0.6f, y - RowH * 0.5f, 0f);
                    r.Icon.enabled = true;
                }
                r.Text.Scale = labelScale;
                r.Text.transform.localPosition = new Vector3(labelX, y - (RowH - PixelFont.GlyphHUnits(labelScale)) * 0.5f, 0f);
                r.Text.Set(label);
                r.Chev.transform.localScale = Vector3.one * ChevScale;
                r.Chev.transform.localPosition = new Vector3(-RowW * 0.5f + ChevInset, y - RowH * 0.5f, 0f);
                r.ValLabel.Scale = valScale;
                r.ValLabel.transform.localPosition = new Vector3(valX,
                    y - (RowH - PixelFont.GlyphHUnits(valScale)) * 0.5f, 0f);
                r.ValLabel.Set(val);
                r.Hit = new Rect(-RowW * 0.5f - 0.2f, y - RowH, RowW + 0.4f, RowH);
                r.Act = acts[i];
                r.Enabled = true;
            }
            return topY - (count - 1) * pitch - RowH;
        }

        void Select(int index)
        {
            var rows = Rows;
            // a card with fewer rows than the one before it would leave the cursor past the end
            _sel = rows.Count > 0 ? Mathf.Clamp(index, 0, rows.Count - 1) : 0;
            for (int i = 0; i < rows.Count; i++)
            {
                var r = rows[i];
                if (!r.Panel.gameObject.activeSelf) continue;
                bool sel = i == index && r.Enabled;
                r.Chev.enabled = sel;
                r.Text.SetColor(!r.Enabled ? new Color(0.5f, 0.5f, 0.6f)
                    : sel ? new Color(1f, 0.96f, 0.72f) : new Color(0.9f, 0.92f, 1f));
                r.Panel.color = !r.Enabled ? new Color(1f, 1f, 1f, 0.35f)
                    : sel ? new Color(1f, 1f, 0.94f, 1f) : new Color(1f, 1f, 1f, 0.66f);
                if (r.ValLabel != null && r.ValLabel.gameObject.activeSelf)
                    r.ValLabel.SetColor(sel ? new Color(1f, 0.96f, 0.8f) : new Color(0.8f, 0.84f, 0.98f));
            }
        }

        int RowCount()
        {
            int n = 0;
            foreach (var r in Rows) if (r.Panel.gameObject.activeSelf) n++;
            return n;
        }

        /// <summary>Rows the card that is up is showing. The self-test reads this: a card that
        /// paints its frame but none of its buttons is exactly the failure a still frame misses.</summary>
        public int ActiveRowCount => RowCount();

        void Move(int d)
        {
            int n = RowCount();
            if (n <= 0) return;
            int i = _sel;
            for (int k = 0; k < n; k++)
            {
                i = (i + d + n) % n;
                if (Rows[i].Enabled) { Select(i); Sfx.Play("ui"); return; }
            }
        }

        void Activate()
        {
            if (_sel < 0 || _sel >= Rows.Count) return;
            var r = Rows[_sel];
            if (!r.Enabled || r.Act == null) return;
            Sfx.Play("ui");
            r.Act();
        }

        // ------------------------------------------------------------------ cinema

        const int CinemaSlides = 9;

        void RefreshCinemaSlide()
        {
            string key = "ci." + _ciIndex;
            if (!Strings.Has(key)) { FinishCinema(); return; }
            var text = Strings.Get(key);
            // instan in the editor preview (the still frame has to show the whole line) and typed
            // out in the player, where a tap finishes the line before it moves on
            _ciText.Set(text, !Application.isPlaying);
            _ciRead = 0f;
            _ciHold = 0f;
            // plate and counter follow the text, so a one-line slide and a three-line slide both
            // get a card that fits them
            float th = _ciText.MeasureHeight(text), tw = _ciText.MeasureWidth(text);
            if (_ciPlate != null)
            {
                _ciPlate.size = new Vector2(Mathf.Max(6f, tw + 2.2f), th + 1.3f);
                _ciPlate.transform.localPosition = new Vector3(0f, 2.2f - th * 0.5f, 0f);
            }
            if (_ciCount != null) _ciCount.Set((_ciIndex + 1) + "/" + CinemaSlides);
            // the fade-in is driven from Tick, which never runs in the editor preview:
            // start fully visible there or the still frame comes out blank
            var c = _ciText.Tint; c.a = Application.isPlaying ? 0f : 1f; _ciText.SetColor(c);
            if (_ciPlate != null)
            {
                var p = _ciPlate.color; p.a = Application.isPlaying ? 0f : 0.82f; _ciPlate.color = p;
            }

            // each slide gets its own backdrop so the sequence is not one flat colour
            string art = _ciIndex <= 3 ? "Art/Backgrounds/PlainA"
                : _ciIndex <= 6 ? "Art/Backgrounds/ForestA" : "Art/Backgrounds/DungeonA";
            var sprite = TexArt.Whole(art);
            if (sprite != null)
            {
                _ciArt.sprite = sprite;
                _ciArt.enabled = true;
                float scale = Mathf.Max(18f / sprite.bounds.size.x, (HalfH * 2f) / sprite.bounds.size.y);
                _ciArt.transform.localScale = Vector3.one * scale;
                _ciArt.transform.localPosition = Vector3.zero;
                _ciArt.color = new Color(0.55f, 0.58f, 0.8f, 1f);
            }
            _ciHold = 0f;
        }

        void NextCinemaSlide()
        {
            _ciIndex++;
            var probe = Strings.Get("ci." + _ciIndex);
            if (!Strings.Has("ci." + _ciIndex)) FinishCinema();
            else RefreshCinemaSlide();
        }

        void FinishCinema()
        {
            Hide();
            Prefs.IntroSeen = true;
            Prefs.Store();
            OnIntroDone?.Invoke();
        }

        // ------------------------------------------------------------------ tick

        /// <summary>Drives the active screen. dir: -1/1 keyboard step, confirm: enter/space,
        /// cancel: escape/back. tap+stage come from the pointer.</summary>
        public void Tick(float dt, Vector2 stage, bool tap, int dir, bool confirm, bool cancel)
        {
            if (_toastRoot != null && _toastRoot.gameObject.activeSelf)
            {
                _toastT -= dt;
                // it slides down out of the HUD bar and fades in together: the card hangs off the
                // bar, so it should arrive the way a tab drops, not pop into place
                _toastRise = Mathf.Min(1f, _toastRise + dt * 6f);
                float k = 1f - _toastRise;
                _toastLift = k * k * 0.5f;
                ApplyToastLift();
                float a = Mathf.Clamp01(_toastT * 1.4f) * Mathf.Min(1f, _toastRise * 3f);
                var c = _toastPanel.color; c.a = a * 0.92f; _toastPanel.color = c;
                var t = _toast.Tint; t.a = a; _toast.SetColor(t);
                if (_toastT <= 0f) _toastRoot.gameObject.SetActive(false);
            }

            _t += dt;
            // the errand marker on the world map breathes so the eye finds it first
            if (_mapQuestDot != null && _mapQuestDot.enabled)
            {
                var qc = _mapQuestDot.color;
                qc.a = 0.5f + 0.5f * Mathf.PingPong(_t * 1.7f, 1f);
                _mapQuestDot.color = qc;
            }
            // cards arrive by dropping the last half unit into place instead of popping -
            // the dim behind them still snaps, only the sheet the eye follows settles
            if (_cardSlide != null)
            {
                _cardSlideT += dt;
                float k = Mathf.Clamp01(_cardSlideT / 0.22f);
                var sp = _cardSlide.localPosition;
                sp.y = 0.55f * (1f - k) * (1f - k);
                _cardSlide.localPosition = sp;
                if (k >= 1f) _cardSlide = null;
            }
            switch (_sc)
            {
                case Sc.None:
                    return;

                case Sc.Splash:
                    // the card rises out of black instead of cutting in: a mark at full
                    // brightness on the first frame reads as a slide, not as a title
                    SetSplashAlpha(Mathf.Clamp01(_t * 1.5f));
                    if (_t > 2.1f || tap || confirm) { Sfx.Play("ui"); OnSplashDone?.Invoke(); }
                    return;

                case Sc.Cinema:
                    {
                        // the plate and the text fade up together, then the line types itself in
                        var c = _ciText.Tint;
                        c.a = Mathf.Clamp01(c.a + dt * 1.6f);
                        _ciText.SetColor(c);
                        if (_ciPlate != null)
                        {
                            var p = _ciPlate.color;
                            p.a = Mathf.Clamp01(p.a + dt * 1.2f) * 0.82f;
                            _ciPlate.color = p;
                        }
                        // the backdrop drifts by whole pixels only: a sub-pixel step resamples all
                        // 288 columns of the art and the frame turns soft
                        if (_ciArt != null && _ciArt.sprite != null)
                            _ciArt.transform.localPosition = new Vector3(0f, G.Snap(Mathf.Sin(Time.time * 0.3f) * 5f / G.PPU), 0f);
                        _ciHold += dt;
                        for (int i = 0; i < _stars.Count; i++)
                        {
                            var s = _stars[i];
                            var sc = s.color;
                            sc.a = 0.2f + 0.45f * (0.5f + 0.5f * Mathf.Sin(Time.time * (0.9f + (i % 5) * 0.27f) + i));
                            s.color = sc;
                        }
                        if (_ciText.IsRevealing) _ciRead = 0f; else _ciRead += dt;
                        if (tap || confirm)
                        {
                            // first tap: finish the line. second tap: the next slide.
                            if (_ciText.IsRevealing) { _ciText.Set(_ciText.Text, true); _ciRead = 0f; }
                            else NextCinemaSlide();
                            return;
                        }
                        if (_ciRead > 3.6f) NextCinemaSlide();
                        return;
                    }

                case Sc.ChapterCard:
                    // the night arrives a line at a time: its name, the place, then what it
                    // wants - one breath each, so the card reads as a briefing not a poster
                    SetCardLine(_ccNight, new Color(1f, 0.95f, 0.78f), _t - 0.10f);
                    SetCardLine(_ccPlace, new Color(0.78f, 0.8f, 0.95f), _t - 0.50f);
                    SetCardLine(_ccGoal, new Color(0.92f, 0.85f, 0.62f), _t - 0.90f);
                    if (_t > 2.9f || tap || confirm) { Hide(); OnIntroDone?.Invoke(); }
                    return;
            }

            // row screens share one path
            if (dir != 0) Move(dir);
            if (cancel)
            {
                if (_sc == Sc.Settings) { Sfx.Play("ui"); if (_settingsFromPause) ShowPause(); else ShowMain(); return; }
                if (_sc == Sc.Pause) { Sfx.Play("ui"); OnResume?.Invoke(); return; }
                if (_sc == Sc.Confirm) { Sfx.Play("ui"); ShowMain(); return; }
                if (_sc == Sc.Credits) { Sfx.Play("ui"); ShowMain(); return; }
                if (_sc == Sc.Journal) { Sfx.Play("ui"); ShowPause(); return; }
                if (_sc == Sc.Page) { Sfx.Play("ui"); ShowJournal(); return; }
                if (_sc == Sc.Shop) { Sfx.Play("ui"); OnShopClosed?.Invoke(); return; }
                if (_sc == Sc.Onboard) { NextOnboard(); return; }
            }
            if (confirm) { Activate(); return; }
            if (!tap) return;

            // rows live inside the sliding card; their hit rects are card-local, so the tap
            // must be measured in the card's frame too while it is still settling
            var pt = stage;
            if (_cardSlide != null) pt -= (Vector2)_cardSlide.localPosition;
            var rows = Rows;
            for (int i = 0; i < rows.Count; i++)
            {
                var r = rows[i];
                if (!r.Panel.gameObject.activeSelf) continue;
                if (!r.Hit.Contains(pt)) continue;
                if (!r.Enabled) return;
                Select(i);
                Activate();
                return;
            }
        }
    }
}
