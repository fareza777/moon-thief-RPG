using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MoonThief
{
    /// <summary>
    /// Entry point of THE MOON THIEF, the full game. Owns the render target (288 px wide,
    /// point-filtered), the game states (Splash / Menu / Cinema / Explore / Battle / End),
    /// touch + keyboard input, dialog boxes, the quest chain and the self-test that plays
    /// the whole loop by itself.
    /// </summary>
    public class Game : MonoBehaviour
    {
        public enum St { Splash, Menu, Cinema, Explore, Battle, End }

        // ------------------------------------------------------------ global run state
        public static class State
        {
            public static int Chapter = 1;
            public static int MoonShards;      // shards needed to call the moon back
            public static int Befriended;      // beasts that joined across the run
            public static int HeldItems;
            public static int MorselsUsed;
            public static int Gold;
            public static int Xp;
            public static int ChestsOpened;    // the first few drops are always shards
            public static int Defeats;         // wild beasts put down (drives the nightwatch quests)
            public static int NgPlus;          // how many times the tale has been told: each retelling bites deeper

            // ---- the journal: the bag, what is worn, what has been seen, what has been done ----
            public static readonly List<string> Bag = new List<string>();          // item keys, repeats allowed
            public static readonly List<string> Friends = new List<string>();      // befriended species keys (max 2)
            // the company: hero keys whose recruiting talks have run (amber is always in,
            // so it is not stored). Drives both the battle party and the trail walkers.
            public static readonly HashSet<string> Joined = new HashSet<string>();
            public static readonly string[] Worn = new string[3];                  // blade, cloth, charm
            public static readonly List<string> Zones = new List<string>();        // places walked into
            public static string CurZone = "village";                              // the zone the hero stands in now
            public static string ObjZone;                                          // the zone the compass points at
            public static readonly List<string> ChestsDone = new List<string>();   // chests already opened
            public static readonly Dictionary<string, int> Seen = new Dictionary<string, int>();

            public static int Level => 1 + Xp / 40;
            public static int NextLevelAt => 40 * Level;

            public static void NewRun()
            {
                Chapter = 1; MoonShards = 0; Befriended = 0; HeldItems = 0; MorselsUsed = 0;
                Gold = 0; Xp = 0; ChestsOpened = 0; Defeats = 0; NgPlus = 0;
                Bag.Clear(); Worn[0] = Worn[1] = Worn[2] = null;
                Zones.Clear(); Seen.Clear(); ChestsDone.Clear(); Friends.Clear();
                Joined.Clear();
                Quests.Reset();
            }

            // ---- bag helpers ----
            public static void AddBag(string key, int n = 1)
            {
                if (string.IsNullOrEmpty(key)) return;
                for (int i = 0; i < n; i++) Bag.Add(key);
                HeldItems = Bag.Count;
            }

            public static void RemoveBag(string key, int n = 1)
            {
                for (int i = 0; i < n; i++)
                {
                    int at = Bag.IndexOf(key);
                    if (at < 0) break;
                    Bag.RemoveAt(at);
                }
                HeldItems = Bag.Count;
            }

            public static int BagCount(string key)
            {
                int n = 0;
                foreach (var b in Bag) if (b == key) n++;
                return n;
            }

            /// <summary>A chest's resting place names it: the chapter it stands in and its cell.
            /// Chests stay open across a save, so the field cannot be farmed by reloading.</summary>
            public static string ChestKey(int chapter, Vector2Int cell) => chapter + ":" + cell.x + "," + cell.y;
            public static bool HasChest(int chapter, Vector2Int cell) => ChestsDone.Contains(ChestKey(chapter, cell));
            public static void MarkChest(int chapter, Vector2Int cell)
            {
                var k = ChestKey(chapter, cell);
                if (!ChestsDone.Contains(k)) ChestsDone.Add(k);
            }
            /// <summary>A house cache keys on its room, not the night: "h3:14,21". The shard
            /// economy stays chapter-scoped; the larder stays where it lives.</summary>
            public static bool HasChestKey(string key) => ChestsDone.Contains(key);
            public static void MarkChestKey(string key) { if (!ChestsDone.Contains(key)) ChestsDone.Add(key); }

            /// <summary>Field chests already spent inside one chapter (the "ch:x,y" keys - a
            /// house cache's "hN:x,y" key never matches the chapter prefix).</summary>
            public static int ChestsOpenedIn(int chapter)
            {
                string p = chapter + ":"; int n = 0;
                foreach (var k in ChestsDone) if (k.StartsWith(p)) n++;
                return n;
            }

            /// <summary>Records a place the hero has walked into (drives the bard's quest).
            /// True only on the first visit, so a zone banner can fire once, ever.</summary>
            public static bool NoteZone(string key)
            {
                if (!string.IsNullOrEmpty(key) && !Zones.Contains(key)) { Zones.Add(key); return true; }
                return false;
            }

            /// <summary>The most filling food in the bag, or null. The battle's Morsel command eats
            /// this one, so the party always gets the best of what they carry.</summary>
            public static string BestFood()
            {
                string best = null;
                int power = 0;
                foreach (var b in Bag)
                {
                    var d = Items.Get(b);
                    if (d.Kind != ItemKind.Food || d.Power <= power) continue;
                    best = b; power = d.Power;
                }
                return best;
            }

            /// <summary>Records a species in the book - true only the first time it is met.</summary>
            public static bool MarkSeen(string monKey)
            {
                if (string.IsNullOrEmpty(monKey)) return false;
                bool first = !Seen.ContainsKey(monKey);
                Seen[monKey] = first ? 1 : Seen[monKey] + 1;
                return first;
            }

            /// <summary>Equipment bonuses, read by the battle so the journal is not decoration.</summary>
            public static int BonusAtk
            {
                get
                {
                    int n = 0;
                    for (int i = 0; i < 3; i++)
                    {
                        if (string.IsNullOrEmpty(Worn[i])) continue;
                        var d = Items.Get(Worn[i]);
                        if (d.Kind == ItemKind.Blade) n += d.Power;
                        if (d.Kind == ItemKind.Charm) n += d.Power / 2;
                    }
                    return n;
                }
            }

            public static int BonusHp
            {
                get
                {
                    int n = 0;
                    for (int i = 0; i < 3; i++)
                    {
                        if (string.IsNullOrEmpty(Worn[i])) continue;
                        var d = Items.Get(Worn[i]);
                        if (d.Kind == ItemKind.Cloth) n += d.Power;
                        if (d.Kind == ItemKind.Charm) n += d.Power / 2;
                    }
                    return n;
                }
            }

            /// <summary>Charms quicken the wearer's hands a little — enough to matter at the turn queue.</summary>
            public static float BonusSpd
            {
                get
                {
                    int n = 0;
                    for (int i = 0; i < 3; i++)
                    {
                        if (string.IsNullOrEmpty(Worn[i])) continue;
                        var d = Items.Get(Worn[i]);
                        if (d.Kind == ItemKind.Charm) n += d.Power;
                    }
                    return n * 0.12f;
                }
            }

            public static SaveData Capture(float heroX, float heroY, bool bossDown) => new SaveData
            {
                chapter = Chapter, shards = MoonShards, befriended = Befriended,
                gold = Gold, xp = Xp, morsels = MorselsUsed, items = HeldItems,
                chestsOpened = ChestsOpened, heroX = heroX, heroY = heroY,
                defeats = Defeats, bossDown = bossDown, ngp = NgPlus,
                bag = Bag.ToArray(), worn = (string[])Worn.Clone(),
                friends = Friends.ToArray(), joined = JoinedArray(),
                zones = Zones.ToArray(), quests = Quests.Capture(),
                seen = SeenKeys(), chests = ChestsDone.ToArray(),
            };

            static string[] JoinedArray()
            {
                var list = new List<string>();
                foreach (var j in Joined) list.Add(j);
                return list.ToArray();
            }

            static string[] SeenKeys()
            {
                var list = new List<string>();
                foreach (var kv in Seen) list.Add(kv.Key + ":" + kv.Value);
                return list.ToArray();
            }

            public static void Apply(SaveData d)
            {
                if (d == null) return;
                Chapter = Mathf.Clamp(d.chapter, 1, 3);
                MoonShards = Mathf.Max(0, d.shards);
                Befriended = Mathf.Max(0, d.befriended);
                Gold = Mathf.Max(0, d.gold);
                Xp = Mathf.Max(0, d.xp);
                MorselsUsed = Mathf.Max(0, d.morsels);
                HeldItems = Mathf.Max(0, d.items);
                ChestsOpened = Mathf.Max(0, d.chestsOpened);
                Defeats = Mathf.Max(0, d.defeats);
                NgPlus = Mathf.Max(0, d.ngp);

                Bag.Clear();
                if (d.bag != null) foreach (var b in d.bag) if (!string.IsNullOrEmpty(b)) Bag.Add(b);
                HeldItems = Bag.Count;
                for (int i = 0; i < 3; i++)
                    Worn[i] = d.worn != null && i < d.worn.Length && !string.IsNullOrEmpty(d.worn[i]) ? d.worn[i] : null;
                Zones.Clear();
                if (d.zones != null) foreach (var z in d.zones) if (!string.IsNullOrEmpty(z) && !Zones.Contains(z)) Zones.Add(z);
                Seen.Clear();
                if (d.seen != null)
                    foreach (var raw in d.seen)
                    {
                        var p = raw.Split(':');
                        int n;
                        if (p.Length == 2 && int.TryParse(p[1], out n)) Seen[p[0]] = n;
                    }
                ChestsDone.Clear();
                if (d.chests != null) foreach (var k in d.chests) if (!string.IsNullOrEmpty(k) && !ChestsDone.Contains(k)) ChestsDone.Add(k);
                Friends.Clear();
                if (d.friends != null) foreach (var f in d.friends)
                    if (!string.IsNullOrEmpty(f) && Friends.Count < 2) Friends.Add(f);
                Joined.Clear();
                if (d.joined != null) foreach (var j in d.joined)
                    if (!string.IsNullOrEmpty(j)) Joined.Add(j);
                Quests.Apply(d.quests);
            }
        }

        public const int ShardsNeeded = 4;

        public St Phase { get; private set; } = St.Splash;
        public bool EditorMode;
        public static bool SelfTestMode;
        public static string SelfTestDir;

        public Camera Cam, BlitCam;
        public RenderTexture Target;
        public Transform StageRoot;
        public WorldView World;
        public BattleView BattleViewRef;
        public BattleDirector Director;
        public MenuView Menus;
        public float HalfH { get; private set; }

        bool _paused;
        /// <summary>Whether Mira's first talk has happened - read off the quest itself, so a
        /// save loaded mid-rung-one still shows rung one (the old flag was forced true on
        /// every load and skipped the opening step).</summary>
        bool MetMira => Quests.Step("mq.1") == 3;
        readonly HashSet<string> _metNpcs = new HashSet<string>();
        bool _hintTalk = true, _hintChest = true;
        float _hintT;                                            // countdown for the deferred chapter toast
        string _hintKey;
        float _barkT = 30f;                                      // companion banter: first quip half a minute in
        float _owlT = 16f;                                       // the far-off owl: first hoot quarter a minute in
        float _fireT = 2f;                                       // the hearth's crackle, for rooms that keep one
        bool _barkSwap;
        int _barkLine;
        PixelLabel _hudQuest;
        SpriteRenderer _hudQuestChip;
        Vector2? _resumePos;
        System.Action _afterScreen;

        Transform _titleRoot, _endRoot;
        PixelLabel _titleName, _titleTag, _titleTap, _titleEnd, _endLines, _endStats, _tapHint;
        Transform _titleMoon, _endMoon;
        SpriteRenderer _endGlow, _endSky;
        float _endRise;      // 1 once the moon has climbed and the bob can take over
        Transform _fadeRoot;
        SpriteRenderer _fade;
        Transform _joyRoot;
        SpriteRenderer _joyBase, _joyKnob;
        Vector2 _joyCur;
        readonly List<SpriteRenderer> _twinkles = new List<SpriteRenderer>();
        GameMap _map;
        Vector2 _joyCenter;
        bool _joyTouch;
        int _joyFinger = -1;
        float _joyRadius = 1.6f;
        Vector2 _joyVec;
        // a deliberate tap on the world: pressed and released without dragging
        bool _tapPending;
        Vector2 _tapStage;
        int _tapFinger = -1;
        Vector2 _tapStart;
        float _tapTime;
        bool _tapMoved;
        PixelLabel _dlgText, _dlgName, _dlgNext, _hudZone, _hudShards;
        Transform _dlgSheet;           // the box's contents: rises into place when a talk opens
        int _dlgChars;
        SpriteRenderer _dlgPanel, _dlgPanelName, _dlgPortrait, _dlgPortPlate;
        bool _dlgOpen;
        string[] _dlgLines;
        int _dlgIndex;
        System.Action _dlgThen;   // queued by a scripted talk (the boss taunt) to fire when it closes
        NpcDef _dlgNpc;
        float _encounterCooldown = 3f;
        bool _bossDown;
        bool _bossFocus;          // the gatekeeper talk pulls the camera toward it
        bool _ending;

        // ---- houses: a second WorldView for the room, kept alive while the street waits ----
        WorldView _houseView;
        Transform _houseRoot;
        GameMap _houseMap;
        bool _inHouse;
        // selftest only: boss walks must not be diverted through a front door - a hero who
        // ducks inside a house keeps steering for a BossPos that lives on the other map
        bool _testNoDoors;
        Vector2 _prevHero;      // selftest steering: where the hero stood last frame
        int _stuck;             // ...and how many frames it has gone nowhere
        readonly HashSet<int> _shotFight = new HashSet<int>();   // nights already photographed mid-fight
        bool _shotMoon;                                          // moonlit-label shot already taken
        int _houseIndex = -1;
        Vector2 _doorReturn;          // where to stand when the door closes behind you
        float _doorCooldown;
        string _questLineRaw;         // the last objective line, kept while indoors

        int _selDialog = 0;   // 0 = talk, 1 = close

        // ------------------------------------------------------------ lifecycle

        void Awake()
        {
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "-selftest") SelfTestMode = true;
                else if (args[i] == "-screenshotdir" && i + 1 < args.Length) SelfTestDir = args[i + 1];
            }
            if (SelfTestDir == null)
                SelfTestDir = System.IO.Path.Combine(Application.persistentDataPath, "SelfTest");
            Prefs.Load();
        }

        void Start()
        {
            if (EditorMode) return;
            // a phone's Unity default is 30fps - everything here reads sluggish at 30.
            // 60 is the target every animation is timed for.
            Application.targetFrameRate = 60;
            if (!Application.isMobilePlatform && !Application.isEditor)
                Screen.SetResolution(576, 1024, false);
            BuildAll(Application.isMobilePlatform ? ComputeHalfHeight() : 16f);
            ShowSplash();
            if (SelfTestMode) StartCoroutine(SelfTest());
        }

        /// <summary>Full-screen fade quad; scene changes dissolve through black.</summary>
        void BuildFade()
        {
            _fadeRoot = new GameObject("fade").transform;
            _fadeRoot.SetParent(StageRoot, false);
            _fade = SpriteRendererUtil.Make(_fadeRoot, "fadeQuad", TexArt.Solid(), 20000);
            _fade.transform.localPosition = Vector3.zero;
            _fade.transform.localScale = new Vector3(288f * 16f, 1024f * 16f, 1f);
            _fade.color = new Color(0f, 0f, 0f, 0f);
        }

        void SetFade(float a)
        {
            if (_fade == null) return;
            var c = _fade.color; c.a = Mathf.Clamp01(a); _fade.color = c;
        }

        /// <summary>How black the screen currently is, 0..1. The self-test waits this out
        /// before it takes a frame: a dissolve through black is a fine transition to ship and a
        /// useless screenshot, and one of the captured frames came out solid black.</summary>
        public float FadeAlpha => _fade != null ? _fade.color.a : 0f;

        IEnumerator CoFade(bool toBlack, float dur)
        {
            if (_fade == null) yield break;
            _fadeRoot.gameObject.SetActive(true);
            float start = _fade.color.a;
            float end = toBlack ? 1f : 0f;
            float e = 0f;
            while (e < dur)
            {
                e += Time.deltaTime;
                SetFade(Mathf.Lerp(start, end, Mathf.Clamp01(e / dur)));
                yield return null;
            }
            SetFade(end);
            if (!toBlack) _fadeRoot.gameObject.SetActive(false);
        }

        /// <summary>Dip to black, run the scene switch, dissolve back in. Transitions are
        /// serialized: two overlapping ones used to drive the same fade quad from two
        /// coroutines at once - black ramps fighting ramps reads as a strobe, and both
        /// middles still ran (which is how a queued battle's setup survived under a
        /// teardown fade). Callers keep their order; each waits for the one ahead.</summary>
        void DoTransition(System.Action middle, float outDur = 0.25f, float inDur = 0.35f)
        {
            if (!Application.isPlaying || _fadeRoot == null) { middle?.Invoke(); return; }
            _transQueue.Enqueue(new TransReq{ Middle = middle, Out = outDur, In = inDur });
            if (!_transRunning) StartCoroutine(CoPumpTransitions());
        }

        struct TransReq { public System.Action Middle; public float Out; public float In; }
        readonly Queue<TransReq> _transQueue = new Queue<TransReq>();
        bool _transRunning;

        IEnumerator CoPumpTransitions()
        {
            _transRunning = true;
            while (_transQueue.Count > 0)
            {
                var req = _transQueue.Dequeue();
                yield return CoFade(true, req.Out);
                req.Middle?.Invoke();
                yield return CoFade(false, req.In);
            }
            _transRunning = false;
        }

        /// <summary>Individual 3x3 stars at 1:1 pixel scale (a stretched star sheet renders
        /// each texel as a huge solid block — never do that). They twinkle in Update.</summary>
        void BuildStars(Transform root, int sorting)
        {
            var rng = new System.Random(99);
            for (int i = 0; i < 34; i++)
            {
                var sr = SpriteRendererUtil.Make(root, "star" + i, TexArt.Star(), sorting);
                float x = (float)rng.NextDouble() * 17.2f - 8.6f;
                float y = (float)rng.NextDouble() * (HalfH * 2f - 3f) - HalfH + 1.5f;
                sr.transform.localPosition = new Vector3(G.Snap(x), G.Snap(y), 0f);
                sr.color = new Color(1f, 1f, 1f, 0.35f + (float)rng.NextDouble() * 0.45f);
                _twinkles.Add(sr);
            }
        }

        void TwinkleStars()
        {
            for (int i = 0; i < _twinkles.Count; i++)
            {
                var s = _twinkles[i];
                if (s == null) continue;
                float ph = i * 12.9898f;
                var c = s.color;
                c.a = 0.3f + 0.4f * (0.5f + 0.5f * Mathf.Sin(Time.time * (1.1f + (i % 5) * 0.23f) + ph));
                s.color = c;
            }
        }

        /// <summary>Touch joystick visual: soft ring at the touch origin, knob that follows.</summary>
        void BuildJoy()
        {
            _joyRoot = new GameObject("joy").transform;
            _joyRoot.SetParent(StageRoot, false);
            _joyBase = SpriteRendererUtil.Make(_joyRoot, "joyBase", TexArt.Ring(), 4500);
            _joyBase.color = new Color(1f, 1f, 1f, 0.30f);
            _joyBase.transform.localScale = Vector3.one * 1.7f;
            _joyKnob = SpriteRendererUtil.Make(_joyRoot, "joyKnob", TexArt.Dot(), 4501);
            _joyKnob.color = new Color(1f, 1f, 1f, 0.55f);
            _joyKnob.transform.localScale = Vector3.one * 1.3f;
            _joyRoot.gameObject.SetActive(false);
        }

        void UpdateJoyVisual(bool active)
        {
            if (_joyRoot == null) return;
            bool show = active && _joyTouch;
            _joyRoot.gameObject.SetActive(show);
            if (!show) return;
            var cam = Cam.transform.localPosition;
            var basePos = cam + (Vector3)ScreenToStage(_joyCenter);
            _joyBase.transform.localPosition = new Vector3(basePos.x, basePos.y, 0f);
            var curPos = cam + (Vector3)ScreenToStage(_joyCur);
            var d = Vector2.ClampMagnitude(curPos - basePos, 1.6f);
            _joyKnob.transform.localPosition = new Vector3(basePos.x + d.x, basePos.y + d.y, 0f);
        }

        public static float ComputeHalfHeight()
        {
            float aspect = Screen.height > 0 ? (float)Screen.width / Screen.height : 0.5625f;
            int rtH = Mathf.Clamp(Mathf.RoundToInt(G.RtWidth / Mathf.Max(0.2f, aspect) / 2f) * 2, 480, 720);
            return Mathf.Max(G.MinHalfHeight, rtH / 32f);
        }

        // ------------------------------------------------------------ construction

        public void BuildAll(float halfH)
        {
            HalfH = halfH;
            var stage = new GameObject("Stage").transform;
            stage.SetParent(transform, false);
            StageRoot = stage;

            var camGo = new GameObject("MainCam");
            camGo.transform.SetParent(transform, false);
            camGo.transform.localPosition = new Vector3(0f, 0f, -10f);
            Cam = camGo.AddComponent<Camera>();
            Cam.orthographic = true;
            Cam.orthographicSize = halfH;
            Cam.clearFlags = CameraClearFlags.SolidColor;
            Cam.backgroundColor = new Color(0.03f, 0.025f, 0.06f, 1f);
            Cam.nearClipPlane = -20f;
            Cam.farClipPlane = 40f;
            int rtH = Mathf.RoundToInt(halfH * 32f);
            Cam.aspect = G.RtWidth / (float)rtH;

            var rt = new RenderTexture(G.RtWidth, rtH, 16, RenderTextureFormat.ARGB32)
            { name = "PortraitTarget", filterMode = FilterMode.Point, antiAliasing = 1 };
            rt.Create();
            Target = rt;
            Cam.targetTexture = rt;

            var blitGo = new GameObject("BlitCam");
            blitGo.transform.SetParent(transform, false);
            BlitCam = blitGo.AddComponent<Camera>();
            BlitCam.orthographic = true;
            BlitCam.orthographicSize = 1f;
            BlitCam.clearFlags = CameraClearFlags.SolidColor;
            BlitCam.backgroundColor = new Color(0.02f, 0.02f, 0.04f, 1f);
            BlitCam.cullingMask = 0;
            BlitCam.depth = -10f;

            BattleViewRef = new GameObject("BattleView").AddComponent<BattleView>();
            BattleViewRef.transform.SetParent(stage, false);
            BattleViewRef.Build(halfH, stage);
            BattleViewRef.gameObject.SetActive(false);

            World = new GameObject("WorldView").AddComponent<WorldView>();
            World.transform.SetParent(stage, false);
            World.gameObject.SetActive(false);

            Director = new GameObject("Director").AddComponent<BattleDirector>();
            Director.transform.SetParent(transform, false);
            Director.View = BattleViewRef;
            Director.OnBattleWon = OnEncounterWon;
            Director.OnBossWon = OnBossDefeated;
            Director.OnDefeat = OnRunLost;
            Director.OnFled = OnEncounterWon;   // same way back: the wild things settle again

            Sfx.Init(transform);
            Sfx.Mus.Init(transform);
            BuildFade();
            BuildJoy();
            BuildTitle();
            BuildEndScreen();
            BuildDialogBox();

            Menus = new GameObject("Menus").AddComponent<MenuView>();
            Menus.transform.SetParent(stage, false);
            Menus.Build(this, halfH, stage);
            Menus.OnStartNew = BeginRun;
            Menus.OnLoadSave = ContinueRun;
            Menus.OnResume = ClosePause;
            Menus.OnSaveGame = SaveRun;
            Menus.OnLeaveToTitle = LeaveToTitle;
            Menus.OnSplashDone = SplashDone;
            Menus.OnIntroDone = OnScreenDone;
            Menus.OnOnboardDone = OnboardDone;
            Menus.OnShopClosed = ClosePause;
            Menus.OnStory = ReplayStory;
            Menus.OnReleaseFriend = key => { if (World != null) World.ReleaseFriend(key); Medals.Grant("fleet"); SaveRun(); };
            // the journal's world map reads the live overworld through these three hooks;
            // indoors they hand back nothing and the card falls back to its last picture
            Menus.GetMap = () => World != null && World.Map != null && !World.Map.Interior ? World.Map : null;
            Menus.GetHeroPos = () => World != null && World.Map != null && !World.Map.Interior ? (Vector2?)World.HeroPos : null;
            Menus.GetObjectivePos = () => ObjectivePos();
            Menus.GetChests = () => World != null && World.Map != null && !World.Map.Interior ? World.ShutChestPos() : null;
        }

        void BuildTitle()
        {
            _titleRoot = new GameObject("Title").transform;
            _titleRoot.SetParent(StageRoot, false);

            var bg = SpriteRendererUtil.Make(_titleRoot, "bg", TexArt.Whole("Art/Backgrounds/ForestA"), 90);
            bg.transform.localPosition = new Vector3(0f, 0f, 0f);
            bg.transform.localScale = Vector3.one * (18f / 20f * 2f);
            bg.color = new Color(0.62f, 0.66f, 0.95f, 1f);

            var dim = SpriteRendererUtil.Make(_titleRoot, "dim", TexArt.Solid(), 91);
            dim.transform.localPosition = Vector3.zero;
            dim.transform.localScale = new Vector3(18f * 16f, (HalfH * 2f + 2f) * 16f, 1f);
            dim.color = new Color(0.04f, 0.03f, 0.09f, 0.62f);

            var moon = SpriteRendererUtil.Make(_titleRoot, "moonEmpty", TexArt.MoonEmpty(), 92);
            moon.transform.localPosition = new Vector3(0f, HalfH - 3.2f, 0f);
            moon.transform.localScale = Vector3.one * 4.5f;
            _titleMoon = moon.transform;

            var halo = SpriteRendererUtil.Make(_titleRoot, "moonHalo", TexArt.Glow(), 92);
            halo.transform.localPosition = moon.transform.localPosition;
            halo.transform.localScale = Vector3.one * 9f;
            halo.color = new Color(0.75f, 0.8f, 1f, 0.30f);

            BuildStars(_titleRoot, 93);

            _titleName = PixelLabelUtil.Make(_titleRoot, "tName", 3, new Color(1f, 0.95f, 0.75f), TextAlign.Center, 94);
            _titleName.transform.localPosition = new Vector3(0f, HalfH - 5.6f, 0f);
            _titleName.Set(Strings.Get("title.name"));

            _titleTag = PixelLabelUtil.Make(_titleRoot, "tTag", 2, new Color(0.88f, 0.9f, 1f), TextAlign.Center, 94);
            _titleTag.transform.localPosition = new Vector3(0f, HalfH - 9.6f, 0f);
            _titleTag.MaxWidthUnits = 15f;
            _titleTag.Set(Strings.Get("title.tagline"));

            _titleTap = PixelLabelUtil.Make(_titleRoot, "tTap", 2, new Color(1f, 0.88f, 0.5f), TextAlign.Center, 94);
            _titleTap.transform.localPosition = new Vector3(0f, -HalfH + 5.4f, 0f);
            _titleTap.Set(Strings.Get("title.tap"));

            _titleEnd = PixelLabelUtil.Make(_titleRoot, "tEnd", 1, new Color(0.62f, 0.6f, 0.75f), TextAlign.Center, 94);
            _titleEnd.transform.localPosition = new Vector3(0f, -HalfH + 1.4f, 0f);
            _titleEnd.Set(Strings.Get("title.footer"));
        }

        void BuildEndScreen()
        {
            _endRoot = new GameObject("EndScreen").transform;
            _endRoot.SetParent(StageRoot, false);

            var sky = SpriteRendererUtil.Make(_endRoot, "sky", TexArt.Solid(), 96);
            sky.transform.localPosition = new Vector3(0f, 0f, 0f);
            sky.transform.localScale = new Vector3(18f * 16f, (HalfH * 2f + 2f) * 16f, 1f);
            sky.color = new Color(0.09f, 0.09f, 0.2f, 1f);
            _endSky = sky;

            var moon = SpriteRendererUtil.Make(_endRoot, "moonFull", TexArt.MoonFull(), 98);
            moon.transform.localPosition = new Vector3(0f, HalfH - 4.2f, 0f);
            moon.transform.localScale = Vector3.one * 6f;
            _endMoon = moon.transform;

            var glow = SpriteRendererUtil.Make(_endRoot, "moonGlow", TexArt.Glow(), 97);
            glow.transform.localPosition = moon.transform.localPosition;
            glow.transform.localScale = Vector3.one * 14f;
            glow.color = new Color(1f, 0.95f, 0.75f, 0.5f);
            _endGlow = glow;

            BuildStars(_endRoot, 96);

            _endLines = PixelLabelUtil.Make(_endRoot, "endLines", 2, new Color(1f, 0.97f, 0.88f), TextAlign.Center, 100);
            _endLines.transform.localPosition = new Vector3(0f, HalfH - 10f, 0f);
            _endLines.MaxWidthUnits = 15.5f;

            // the run's ledger, set small under the epilogue: level, friends made, gold kept
            _endStats = PixelLabelUtil.Make(_endRoot, "endStats", 1, new Color(0.78f, 0.8f, 0.95f), TextAlign.Center, 100);
            // three lines now: stats + the "night dreams again" promise - kept above the tap hint
            _endStats.transform.localPosition = new Vector3(0f, -HalfH + 6.6f, 0f);

            _tapHint = PixelLabelUtil.Make(_endRoot, "endTap", 2, new Color(1f, 0.88f, 0.5f), TextAlign.Center, 100);
            _tapHint.MaxWidthUnits = 16f;   // an unbounded wrap box reads as text at the frame edge
            _tapHint.transform.localPosition = new Vector3(0f, -HalfH + 3.9f, 0f);
            _tapHint.Set(Strings.Get("end.tap"));
            _endRoot.gameObject.SetActive(false);
        }

        void BuildDialogBox()
        {
            var root = new GameObject("Dialog").transform;
            root.SetParent(StageRoot, false);
            // the box rides the camera on the root; the sheet under it is what rises into
            // place when a talk opens, so the cam write never fights the entrance
            _dlgSheet = new GameObject("sheet").transform;
            _dlgSheet.SetParent(root, false);
            _dlgPanel = SpriteRendererUtil.Make(_dlgSheet, "dlgPanel", TexArt.Panel(), 3000);
            _dlgPanel.drawMode = SpriteDrawMode.Sliced;
            _dlgPanel.transform.localScale = Vector3.one;
            _dlgPanel.size = new Vector2(17.4f, DialogMinH);

            _dlgName = PixelLabelUtil.Make(_dlgSheet, "dlgName", 1, new Color(1f, 0.9f, 0.6f), TextAlign.Left, 3002);
            _dlgText = PixelLabelUtil.Make(_dlgSheet, "dlgText", 2, new Color(1f, 0.97f, 0.88f), TextAlign.Left, 3002);
            // 14.4 units of body: 19 characters per line at scale 2, and no line ever reaches the
            // box edge. The old 13.2 packed 17 characters into every row and the box was a fixed
            // 5.6 units tall, so a four-line line of dialog ended exactly on the bottom border and
            // a five-line one ran off the frame.
            _dlgText.MaxWidthUnits = 14.4f;
            _dlgText.RevealSpeed = 55f;

            // speaker portrait: the NPC's own overworld sheet, scaled up inside the box,
            // framed by its own small plate and name tag instead of floating on the panel
            _dlgPortPlate = SpriteRendererUtil.Make(_dlgSheet, "dlgPortPlate", TexArt.Panel(), 3000);
            _dlgPortPlate.drawMode = SpriteDrawMode.Sliced;
            _dlgPortPlate.color = new Color(0.92f, 0.88f, 1f);
            _dlgPanelName = SpriteRendererUtil.Make(_dlgSheet, "dlgPanelName", TexArt.Panel(), 3001);
            _dlgPanelName.drawMode = SpriteDrawMode.Sliced;
            _dlgPanelName.color = new Color(0.78f, 0.72f, 0.95f);
            _dlgPortrait = SpriteRendererUtil.Make(_dlgSheet, "dlgPortrait", null, 3001);
            // 3.0, not 3.4: the pack's chara cell is 16 px wide, so 3.4 grew the portrait to 54 px
            // and its right edge landed 3 px *past* the first letter of the line it introduces.
            _dlgPortrait.transform.localScale = Vector3.one * 3f;

            // the "there is more" tick at the box's bottom corner - lit once a line is done
            // spelling itself out, blink-bobbing so a waiting tap is obvious
            _dlgNext = PixelLabelUtil.Make(_dlgSheet, "dlgNext", 1, new Color(1f, 0.85f, 0.5f), TextAlign.Right, 3002);
            _dlgNext.Set("v");

            LayoutDialogBox("");
            root.gameObject.SetActive(false);
            DialogRoot = root;
        }

        const float DialogMinH = 5.2f;

        /// <summary>Sizes the box to the line it is about to show, and stacks the name, the body
        /// and the portrait inside it. Called for every line, because the lines of one
        /// conversation are not all the same length.</summary>
        void LayoutDialogBox(string body)
        {
            float bottom = -HalfH + 0.4f;
            float bodyH = _dlgText.MeasureHeight(body);
            float h = Mathf.Max(DialogMinH, bodyH + 2.1f);          // name line + padding
            float top = bottom + h;
            _dlgPanel.size = new Vector2(17.4f, h);
            _dlgPanel.transform.localPosition = new Vector3(0f, bottom + h * 0.5f, 0f);
            // the portrait owns the left column (G.Left+0.3 .. G.Left+3.3) and the text starts
            // 0.2 units clear of its edge, whatever NPC is speaking
            _dlgName.transform.localPosition = new Vector3(G.Left + 3.9f, top - 0.5f, 0f);
            _dlgPanelName.size = new Vector2(_dlgName.MeasureWidth(_dlgName.Text) + 0.4f, 1.35f);
            _dlgPanelName.transform.localPosition = new Vector3(G.Left + 3.7f + _dlgPanelName.size.x * 0.5f, top - 0.5f, 0f);
            _dlgText.transform.localPosition = new Vector3(G.Left + 3.5f, top - 1.15f, 0f);
            _dlgPortrait.transform.localPosition = new Vector3(G.Left + 1.85f, bottom + 2.1f, 0f);
            _dlgPortPlate.size = new Vector2(3.0f, 3.6f);
            _dlgPortPlate.transform.localPosition = new Vector3(G.Left + 1.85f, bottom + 2.1f, 0f);
            // raised off the very bottom edge of the card: at +0.35 the blinking tip rode the
            // last two pixels of the screen and the frame audit calls that an EDGE defect
            _dlgNext.transform.localPosition = new Vector3(G.Right - 0.7f, bottom + 0.62f, 0f);
        }

        Transform DialogRoot { get; set; }

        // ------------------------------------------------------------ flow

        /// <summary>Studio card, shown once on boot.</summary>
        public void ShowSplash()
        {
            Phase = St.Splash;
            SetCam(0f, 0f);
            _titleRoot.gameObject.SetActive(false);
            _endRoot.gameObject.SetActive(false);
            BattleViewRef.gameObject.SetActive(false);
            World.gameObject.SetActive(false);
            DialogRoot.gameObject.SetActive(false);
            _dlgOpen = false;
            _paused = false;
            Menus.ShowSplash();
        }

        /// <summary>Splash is done. The three onboarding cards play exactly once ever -
        /// after that, the title screen is the door in.</summary>
        void SplashDone()
        {
            if (!Prefs.OnbSeen) Menus.ShowOnboard();
            else ShowTitle();
        }

        void OnboardDone()
        {
            Prefs.OnbSeen = true;
            Prefs.Store();
            ShowTitle();
        }

        /// <summary>STORY on the main menu replays the intro reel and returns to the menu.</summary>
        void ReplayStory()
        {
            Phase = St.Cinema;
            _afterScreen = ShowTitle;
            Sfx.Mus.Play("cinema");
            Menus.ShowCinema(0);
        }

        /// <summary>The main menu (title art + the row list).</summary>
        public void ShowTitle()
        {
            Phase = St.Menu;
            SetCam(0f, 0f);
            _titleRoot.gameObject.SetActive(true);
            _endRoot.gameObject.SetActive(false);
            BattleViewRef.gameObject.SetActive(false);
            World.gameObject.SetActive(false);
            DialogRoot.gameObject.SetActive(false);
            _dlgOpen = false;
            _paused = false;
            Time.timeScale = 1f;   // belt and suspenders: title is the universal unwind
            _titleTap.gameObject.SetActive(false);   // the menu rows replace the old tap hint
            _titleEnd.gameObject.SetActive(false);   // and the footer: the last row shears it
            Sfx.Mus.Play("title");
            Menus.ShowMain();
        }

        void SetCamY(float y) => SetCam(0f, y);

        void SetCam(float x, float y)
        {
            // The render target is exactly 16 px per unit, so a camera parked on a whole pixel
            // is what keeps the art sharp. Following the hero at its raw position left every
            // sprite - and every glyph of the bitmap font - straddling two pixels, which reads
            // as a soft, uneven, "cheap" image no amount of redrawing fixes.
            x = Fx.Snap(x);
            y = Fx.Snap(y);
            if (Cam != null) Cam.transform.localPosition = new Vector3(x, y, -10f);
            if (World != null && World.HudRoot != null)
                World.HudRoot.localPosition = new Vector3(x, y, 0f);
            if (World != null) World.ViewCenter = new Vector2(x, y);
            // the dialog box is laid out around the camera origin, so it rides along;
            // otherwise it is left behind whenever the exploration camera scrolls.
            if (DialogRoot != null) DialogRoot.localPosition = new Vector3(x, y, 0f);
        }

        float _camEx, _camEy;

        // a banner and a toast draw in the same top strip. holdNotice is only refreshed in
        // Update, so a toast fired in the same frame as the banner would see the stale flag
        // and land under the rising card - raise the hold right here, in the same call.
        void ShowZoneBanner(string text)
        {
            World.ShowBanner(text);
            if (Menus != null) Menus.HoldToasts = true;
        }

        void FollowHero()
        {
            // plain == on purpose: WorldView is a UnityEngine.Object, and ?. would ride
            // straight into members of a Destroy()'ed view instead of seeing it as null
            if (World == null || World.Map == null) return;
            float tx = Mathf.Clamp(World.HeroPos.x, 9f, GameMap.W - 9f);
            float ty = Mathf.Clamp(World.HeroPos.y, HalfH - 2f, GameMap.H - HalfH + 2f);
            if (_bossFocus && World.Map != null)
            {
                // while the gatekeeper speaks, the frame drifts to hold both of you -
                // the slow push-in does the work a cutscene border would
                var b = World.Map.BossPos;
                tx = Mathf.Clamp((World.HeroPos.x + b.x) * 0.5f, 9f, GameMap.W - 9f);
                ty = Mathf.Clamp((World.HeroPos.y + b.y) * 0.5f, HalfH - 2f, GameMap.H - HalfH + 2f);
            }
            // The camera trails the hero instead of being welded to her. A hard follow turns every
            // step into a screen-wide snap (the whole village jumps one pixel with her), and the
            // ease is what makes a walk read as walking. SetCam still snaps the result to the
            // pixel grid, so the ease can never soften a sprite; in the editor frames it is a pure
            // snap, so a still is always framed exactly where the scene asked for it.
            if (!Application.isPlaying) { _camEx = tx; _camEy = ty; }
            else
            {
                float k = 1f - Mathf.Exp(-9f * Time.deltaTime);
                _camEx = Mathf.Lerp(_camEx, tx, k);
                _camEy = Mathf.Lerp(_camEy, ty, k);
            }
            SetCam(_camEx, _camEy);
            // the camera moved, so which name plates are framed changed with it. Without this the
            // decision was only ever made at spawn or on a teleport, and a plate could stay lit
            // while its owner was off screen (or sit on the hero's head after a walk).
            World.UiBlock = Menus != null ? Menus.ToastRect : new Rect(0f, 0f, 0f, 0f);
            World.RefreshNamePlates();
        }

        /// <summary>The medal case's pulse: any system can Grant() into the queue (battle,
        /// chests, the ending); each frame drains one announcement into the toast lane so a
        /// fight that earns three medals reads as three moments, not a stack. The count-led
        /// medals (caches, coin, company, the full errand book) are judged here where the
        /// numbers live.</summary>
        void MedalTick()
        {
            string m;
            while ((m = Medals.Dequeue()) != null)
            {
                Menus.ShowToast(Strings.Get("md.earn", Strings.Get("md." + m)), 4f);
                Sfx.Play("questdone");
            }
            if (State.ChestsOpened >= 10) Medals.Grant("hoard");
            if (State.Gold >= 300) Medals.Grant("rich");
            if (State.Friends.Count >= 2) Medals.Grant("army");
            if (Medals.Has("boss1") && Medals.Has("boss2") && Medals.Has("boss3")) Medals.Grant("keeper");
            int sideLeft = 0;
            foreach (var q in Quests.All) if (!q.Main && Quests.Step(q.Id) != 3) sideLeft++;
            if (sideLeft == 0) Medals.Grant("warden");
        }

        void BeginRun()
        {
            State.NewRun();
            Medals.EarnedThisRun = 0;

            _hintTalk = true;
            _hintChest = true;
            _bossDown = false;
            _ending = false;
            _resumePos = null;
            Menus.Hide();
            SetCam(0f, 0f);
            World.gameObject.SetActive(false);

            // the self-test drives the world directly, it must not sit through the story
            if (SelfTestMode || !Application.isPlaying) { StartChapter(1); return; }

            // a new run always opens with the story, even if it was seen before
            Phase = St.Cinema;
            _afterScreen = () => StartChapter(1);
            Sfx.Mus.Play("cinema");
            Menus.ShowCinema(0);
        }

        /// <summary>Chapter intro card, then the world. The card is skipped in the editor
        /// preview and the self-test, which drive the world directly.</summary>
        void StartChapter(int chapter)
        {
            State.Chapter = chapter;
            _bossDown = false;
            _ending = false;

            // the main line accepts itself - nobody hands you these errands, so nothing
            // else ever set their step and the journal read NEW on all three all run long
            if (Quests.Step("mq.1") == 0) Quests.Accept(Quests.Find("mq.1"));
            if (chapter >= 3 && Quests.Step("mq.3") == 0) Quests.Accept(Quests.Find("mq.3"));

            if (SelfTestMode || EditorMode || !Application.isPlaying)
            {
                BuildChapterNow(chapter);
                return;
            }

            Menus.Hide();
            World.gameObject.SetActive(false);
            if (_hudZone != null) _hudZone.enabled = false;
            Phase = St.Cinema;
            SetCam(0f, 0f);
            _afterScreen = () => BuildChapterNow(chapter);
            Sfx.Mus.Play("cinema");
            Menus.ShowChapterCard(chapter);
        }

        void OnApplicationQuit()
        {
            // a clean shutdown must always be explainable; the stack says who asked for it
            Debug.Log("[game] quitting. phase=" + Phase + "\n" + System.Environment.StackTrace);
        }

        void BuildChapterNow(int chapter)
        {
            Debug.Log("[game] build chapter " + chapter);
            Phase = St.Explore;
            _paused = false;

            _titleRoot.gameObject.SetActive(false);
            _endRoot.gameObject.SetActive(false);
            Menus.Hide();
            BattleViewRef.gameObject.SetActive(false);
            World.gameObject.SetActive(true);

            if (World.Ready && World.MapChapter == chapter)
            {
                World.ResetForChapter();
            }
            else
            {
                if (World.Ready) World.Teardown();
                _map = GameMap.Build(chapter);
                World.Build(_map, StageRoot, HalfH);
                World.MapChapter = chapter;
                _overworld = World;
            }
            // a card can leave world text hushed when it hands control back (pause ->
            // leave to title -> continue reuses this same WorldView)
            World.SetTextVisible(true);
            World.PlaceHero(_resumePos ?? World.Map.VillageCenter);
            World.SyncFriends();
            _resumePos = null;

            // the arrive card already names the ground beneath the hero's feet: note that
            // zone quietly so the crossing banner does not repeat it a step later
            string zk0 = World.HeroPos.y > 58f ? "zone.wood"
                : World.HeroPos.y > 26f ? "zone.fields" : "zone.village";
            State.CurZone = zk0.Substring(5);
            State.NoteZone(zk0);

            ShowZoneBanner(Strings.Get("zone.arrive." + Mathf.Clamp(chapter, 1, 3)));
            Sfx.Mus.Duck = 1f; Sfx.Mus.Play("explore");
            MakeHud();
            RefreshHud();

            FollowHero();
            _encounterCooldown = 3.5f;
            SaveRun();

            // the banner owns the top band for its first couple of seconds - a toast
            // landing on the same beat was one text printed through another
            if (chapter == 1) { _hintT = 3.4f; _hintKey = "onb.move"; }
            else if (chapter == 3) { _hintT = 3.4f; _hintKey = "quest.4"; }
        }

        void RefreshHud()
        {
            if (_hudZone != null)
                _hudZone.Set(Strings.Get("hud.explore",
                    State.NgPlus > 0 ? State.Chapter + "+" : State.Chapter.ToString(),
                    State.MoonShards, ShardsNeeded, State.Gold));
            if (World != null) World.SetMoonFill(State.MoonShards, ShardsNeeded);
            RefreshQuest();
        }

        /// <summary>The goal line under the night counter. Recomputed from the world state,
        /// and only re-rendered when the text actually changes.</summary>
        void RefreshQuest()
        {
            if (_hudQuest == null) return;
            string raw = QuestText();
            if (raw == null) return;
            _questLineRaw = raw;
            string text = Strings.Get("hud.quest", raw);
            if (text == _questText) return;
            _questText = text;
            _hudQuest.Set(text);
            if (_hudQuestChip != null)
            {
                // the chip hugs the measured note: a one-line goal gets a one-line slab,
                // a wrapped one gets both lines covered
                float w = _hudQuest.MeasureWidth(text), h = _hudQuest.MeasureHeight(text);
                _hudQuestChip.size = new Vector2(w + 0.4f, h + 0.3f);
                _hudQuestChip.transform.localPosition = new Vector3(
                    _hudQuest.transform.localPosition.x + w * 0.5f,
                    _hudQuest.transform.localPosition.y - h * 0.5f - 0.06f, 0f);
            }
        }

        string QuestText()
        {
            // indoors the line would be recomputed from a room with one chest in it, so the
            // objective from the street is simply kept on the HUD -- unless the hero walked into
            // a house before any objective was set, and then it is better to work one out than to
            // go blank
            if (World != null && World.Map != null && World.Map.Interior && !string.IsNullOrEmpty(_questLineRaw))
                return _questLineRaw;
            if (!MetMira) return Strings.Get("quest.1");
            // the company forms before the work: Sea at the village edge, then Moss deeper
            // in the fields. The ladder reads one line - Mira -> Sea -> Moss -> the night's
            // errand - instead of three strangers appearing at your heels unasked.
            if (!State.Joined.Contains("hero.sea")) return Strings.Get("quest.sea");
            if (!State.Joined.Contains("hero.moss")) return Strings.Get("quest.moss");
            if (_bossDown && State.MoonShards >= ShardsNeeded) return Strings.Get("quest.5");
            if (State.MoonShards >= ShardsNeeded) return Strings.Get("quest.3");
            // only the first three caches hold shards - the fourth rides the Pale Guard.
            // Counting ShardsNeeded-MoonShards here promised chests that hold nothing.
            int shardChests = Mathf.Max(0, 3 - State.ChestsOpened);
            if (World != null && World.ChestsLeft > 0 && shardChests > 0)
                return Strings.Get("quest.2", shardChests, shardChests == 1 ? "chest" : "chests");
            // the corner names the night's real gatekeeper, not the finale's - a walkthrough
            // line that reads "Face the Pale Guard" in night one is steering the hero wrong
            return Strings.Get("quest.4", Strings.Get(BattleData.BossNameKey(State.Chapter)));
        }

        string _questText;

        /// <summary>Counter errands on the main line settle themselves the moment the count is
        /// met - there is no giver to hand them to, so without this the journal would read
        /// ACTIVE on them forever after the work was done.</summary>
        void CheckMains()
        {
            foreach (var q in Quests.All)
                // the Pale Guard settles its own account in OnBossDefeated - a field slime
                // must not close the last rung early
                if (q.Main && q.Kind != QuestKind.Talk && q.Id != "mq.3"
                    && Quests.Step(q.Id) == 1 && Quests.Progress(q) >= q.Need)
                {
                    Quests.Complete(q);
                    // a main rung deserves the same ceremony a side errand gets
                    Menus.ShowToast(Strings.Get("jr.questdone", Strings.Get(q.TitleKey)), 3.6f);
                    Sfx.Play("questdone");
                }
        }

        /// <summary>Where the compass arrow points tonight. It follows the same ladder the HUD's
        /// quest line does: find Mira, find the chests, find the boss, find the cristal.
        /// Indoors it is parked - a room that fits on one screen needs no compass.</summary>
        Vector2? ObjectivePos()
        {
            if (World == null || World.Map == null || World.Map.Interior) { State.ObjZone = null; return null; }
            Vector2? p = ObjectivePosInner();
            // the journal's map page reads the zone this falls in to mark the night's errand
            if (p.HasValue)
            {
                float y = p.Value.y;
                State.ObjZone = y >= GameMap.H - 8 ? "gate"
                    : y > 58f ? "wood" : y > 26f ? "fields" : "village";
            }
            else State.ObjZone = null;
            return p;
        }

        Vector2? ObjectivePosInner()
        {
            if (!MetMira)
            {
                var mira = World.FindNpc("npc.elder");
                if (mira != null && mira.Root != null) return mira.Root.localPosition;
            }
            // the compass walks the company together before any chest or boss: first Sea on
            // the road out, then Moss in the fields. Once joined their wandering selves are
            // gone and the same ladder step just falls through.
            if (!State.Joined.Contains("hero.sea"))
            {
                var sea = World.FindNpc("npc.sea");
                if (sea != null && sea.Root != null) return sea.Root.localPosition;
            }
            if (!State.Joined.Contains("hero.moss"))
            {
                var moss = World.FindNpc("npc.moss");
                if (moss != null && moss.Root != null) return moss.Root.localPosition;
            }
            if (State.MoonShards >= ShardsNeeded)
                return _bossDown ? (Vector2?)World.Map.CristalPos : World.Map.BossPos;
            // chests only carry three of the four shards - once those are found, the night's
            // gate is its boss, and pointing the compass at loot would walk the hero backwards
            int shardChests = Mathf.Max(0, 3 - State.ChestsOpened);
            if (shardChests > 0)
            {
                int chestAt = World.NearestChest(World.HeroPos, 999f);
                if (chestAt >= 0) return World.ChestPos(chestAt);
            }
            return World.Map.BossPos;
        }

        // ------------------------------------------------------------ main loop

        void Update()
        {
            if (EditorMode || !Application.isPlaying) return;
            Sfx.Mus.Tick();

            // ---- front-end screens (splash, menu, settings, credits, story cards)
            if (Phase == St.Splash || Phase == St.Menu || Phase == St.Cinema)
            {
                if (Phase == St.Menu) IdleTitle();
                Menus.Tick(Time.deltaTime, StagePos(), TapPressed(), KeyStep(), KeyConfirm(), KeyCancel());
                Menus.SetAnchor(Cam.transform.localPosition);
                UpdateJoyVisual(false);
                return;
            }

            if (Phase == St.End)
            {
                float a = 0.55f + 0.45f * Mathf.PingPong(Time.time * 0.9f, 1f);
                _tapHint.SetColor(new Color(1f, 0.88f, 0.5f, a));
                if (_endMoon != null && _endRise >= 1f)
                    _endMoon.localPosition = new Vector3(0f, HalfH - 4.2f + Mathf.Sin(Time.time * 0.7f) * 0.3f, 0f);
                if (_endGlow != null)
                {
                    var gc = _endGlow.color;
                    gc.a = 0.42f + 0.14f * Mathf.Sin(Time.time * 1.3f);
                    _endGlow.color = gc;
                }
                TwinkleStars();
                UpdateJoyVisual(false);
                if (_endRise >= 1f && (TapPressed() || KeyConfirm())) DoTransition(() => ShowTitle());
                return;
            }

            // ---- the pause card freezes the world
            if (_paused)
            {
                Menus.Tick(Time.unscaledDeltaTime, StagePos(), TapPressed(), KeyStep(), KeyConfirm(), KeyCancel());
                Menus.SetAnchor(Cam.transform.localPosition);
                UpdateJoyVisual(false);
                return;
            }

            // ---- battles: taps and the keyboard drive the command menu. Without this
            // the battle only ever ran from the self-test, so the menus looked dead.
            if (Phase == St.Battle)
            {
                HandleBattleInput();
                return;
            }

            if (_dlgOpen)
            {
                Menus.SetAnchor(Cam.transform.localPosition);
                UpdateDialog();
                UpdateJoyVisual(false);
                return;
            }

            // One lane, one message at a time. A notice now hangs off the HUD bar, and the zone
            // card (NIGHT TWO / the long fields) is drawn in that same strip for its two seconds,
            // so a notice that arrives while the card is up waits for it - exactly like the one
            // that arrives during a conversation. The runtime audit caught the pair printing over
            // each other on the first village frame, which no editor frame ever showed.
            // the held toast is a world notice: away from the street (a fight, the dawn,
            // the title) the world's own flag falls but the notice must keep waiting, or it
            // flushes onto a screen it does not belong to
            bool holdNotice = _dlgOpen || (World != null && World.BannerUp) || Phase != St.Explore;
            if (holdNotice != Menus.HoldToasts)
            {
                Menus.HoldToasts = holdNotice;
                if (!holdNotice) Menus.FlushToast();
            }
            // a banner arriving over a live toast is covered too: raising HoldToasts parks
            // the toast into the held slot the moment the flag flips, so nothing ever prints
            // through the card - the notice just plays after it
            if (Phase != St.Explore)
            {
                Menus.SetAnchor(Cam.transform.localPosition);
                UpdateJoyVisual(false);
                return;
            }

            Menus.SetAnchor(Cam.transform.localPosition);

            // the moon tile in the corner doubles as the pause button on touch screens
            if (KeyCancel()) { OpenPause(); return; }
            if (TapPressed() && StagePos().x > G.Right - 2.6f && StagePos().y > HalfH - 2.2f) { OpenPause(); return; }

            var move = ReadMoveInput();
            World.DriveHero(move, Time.deltaTime);
            UpdateJoyVisual(true);
            PollWorldTap();

            // doors. Walking into one opens it, and the doormat inside leads back out; the
            // cooldown stops the hero from bouncing straight back through the door he just used.
            _doorCooldown = Mathf.Max(0f, _doorCooldown - Time.deltaTime);
            if (_doorCooldown <= 0f)
            {
                if (_inHouse)
                {
                    if (Vector2.Distance(World.HeroPos, World.Map.ExitPos) < 0.85f) { LeaveHouse(); return; }
                }
                else if (World.NearDoor(World.HeroPos, out int doorIdx))
                {
                    EnterHouse(doorIdx);
                    return;
                }
            }

            // where we are, and the one-off things that happen when you get there
            if (_inHouse)
            {
                State.CurZone = "village";
                State.NoteZone("zone.house." + World.Map.HouseIndex);
            }
            else
            {
                string zk = World.HeroPos.y > 58f ? "zone.wood"
                    : World.HeroPos.y > 26f ? "zone.fields" : "zone.village";
                State.CurZone = zk.Substring(5);
                // first time crossing a border the place announces itself, once, ever -
                // a soft chime with the banner so the crossing lands on two senses
                if (State.NoteZone(zk))
                {
                    Sfx.Play("zone");
                    ShowZoneBanner(Strings.Get("zone.name." + zk.Substring(5))
                        + "\n" + Strings.Get("hud.nightshort", State.Chapter)
                        + (State.NgPlus > 0 ? "+" : ""));
                    // a company member reads the land too - the party talks, not just walks
                    string bark = State.Joined.Contains("hero.sea")
                        ? Strings.Get("bark.sea." + zk.Substring(5))
                        : State.Joined.Contains("hero.moss")
                            ? Strings.Get("bark.moss." + zk.Substring(5)) : "";
                    if (!string.IsNullOrEmpty(bark)) Menus.ShowToast(bark, 3.4f);
                }
                if (TryWorldEvent()) return;
            }

            // the tune follows the zone: warm hearth music inside the village wall and its
            // houses, a sparser watchful line under the old trees, the folk pulse in between
            Sfx.Mus.Play(_inHouse || World.HeroPos.y <= 26f ? "village"
                : World.HeroPos.y > 58f ? "wood" : "explore");
            // the village tune plays inside houses too, but the night bed does not
            Sfx.Mus.Ambient = !_inHouse;

            // camera follows the hero on both axes, clamped to the map; the HUD layer follows too
            FollowHero();
            World.SetObjective(ObjectivePos());
            MedalTick();

            // company banter: the people you walk with occasionally say what they see -
            // slow enough to stay flavor, never in a house and never while you are talking
            if (State.Joined.Count > 0 && !_inHouse && !_dlgOpen)
            {
                _barkT -= Time.deltaTime;
                if (_barkT <= 0f)
                {
                    _barkT = 26f + Random.value * 14f;
                    string who = State.Joined.Contains("hero.moss")
                        && (!State.Joined.Contains("hero.sea") || _barkSwap) ? "moss" : "sea";
                    _barkSwap = !_barkSwap;
                    Menus.ShowToast(Strings.Get("bark." + who + ".idle." + _barkLine++ % 4), 3.2f);
                }
            }

            // the night keeps its own voice too: an owl somewhere past the lamps,
            // sparse enough to stay a gift - never indoors and not over someone's words;
            // indoors, the hearth takes over with its small crackle
            if (!_dlgOpen)
            {
                if (_inHouse)
                {
                    _fireT -= Time.deltaTime;
                    if (_fireT <= 0f)
                    {
                        _fireT = 2.5f + Random.value * 3.5f;
                        Sfx.Play("fire", 0.8f + Random.value * 0.4f);
                    }
                }
                else
                {
                    _owlT -= Time.deltaTime;
                    if (_owlT <= 0f)
                    {
                        _owlT = 22f + Random.value * 16f;
                        Sfx.Play("owl", 0.9f + Random.value * 0.25f);
                    }
                }
            }

            // the deferred chapter toast fires once the banner has had its beat
            if (_hintT > 0f)
            {
                _hintT -= Time.deltaTime;
                if (_hintT <= 0f && _hintKey != null)
                {
                    Menus.ShowToast(Strings.Get(_hintKey), 4.2f);
                    _hintKey = null;
                }
            }

            // encounters
            _encounterCooldown -= Time.deltaTime;
            var touched = World.TouchedMonster();
            if (touched != null && _encounterCooldown <= 0f)
            {
                var spec = touched.Spec;
                World.RemoveMonster(touched);
                StartBattle(new[] { spec });
                return;
            }
            if (_encounterCooldown <= 0f && move.sqrMagnitude > 0.01f && World.HeroPos.y > 20f
                && Random.value < 0.0006f)
            {
                // rare ambient encounter while walking in the wild
                StartBattle(BattleData.Roll(State.Chapter, new System.Random()));
                return;
            }

            // first chest in reach: one nudge, then never again. The hint waits out the
            // zone banner - "Walk up to a villager" over "NIGHT ONE" was one text on another
            if (_hintChest && World.ChestsLeft > 0 && !World.BannerUp && World.NearestChest(World.HeroPos, 3f) >= 0)
            {
                _hintChest = false;
                Menus.ShowToast(Strings.Get("onb.chest"), 3.6f);
            }
            if (_hintTalk && !MetMira && !World.BannerUp && World.Npcs.Count > 0)
            {
                var npc = World.NearestNpc(World.HeroPos, 4f);
                if (npc.NameKey != null)
                {
                    _hintTalk = false;
                    Menus.ShowToast(Strings.Get("onb.talk"), 3.6f);
                }
            }

            // interactions: a deliberate short tap, never a joystick drag; on desktop the
            // confirm key does the same thing so the whole game can be played without a mouse
            if (_tapPending) { _tapPending = false; TryInteract(); }
            else if (KeyConfirm()) TryInteract();
        }

        /// <summary>One-off encounters on the road, from Quests.Events. Each fires once per run at
        /// its own spot; the tones run from a hen that has swallowed a coin to a hunter's dog that
        /// will not leave the grave.</summary>
        bool TryWorldEvent()
        {
            if (_dlgOpen || _paused || World == null || World.Map == null) return false;
            foreach (var ev in Quests.Events)
            {
                if (ev.Chapter > State.Chapter || Quests.FiredAlready(ev.Id)) continue;
                if (Vector2.Distance(World.HeroPos, ev.Pos) > ev.Radius) continue;
                Quests.MarkFired(ev.Id);
                if (ev.Gold > 0) State.Gold += ev.Gold;
                if (!string.IsNullOrEmpty(ev.Gift)) State.AddBag(ev.Gift);
                Sfx.Play(ev.Tragic ? "faint" : ev.Gold > 0 ? "coin" : "chest");
                Menus.ShowToast(Strings.Get(ev.TextKey), ev.Tragic ? 5.4f : 4.4f);
                _encounterCooldown = Mathf.Max(_encounterCooldown, 2.5f);
                RefreshHud();
                SaveRun();
                if (!string.IsNullOrEmpty(ev.Fight))
                {
                    foreach (var m in BattleData.Bestiary)
                        if (m.Name == ev.Fight) { StartBattle(new[] { m }); break; }
                }
                return true;
            }
            return false;
        }

        void OpenPause()
        {
            // a card replaces the screen, so nothing from the world stays behind it -- not the
            // dialog box, not a name plate, not the banner
            if (_dlgOpen) CloseDialog();
            _paused = true;
            // a real freeze: coroutines, wander, and the battle clock all stop while the
            // card is up - the menu still animates on unscaled time
            Time.timeScale = 0f;
            _joyTouch = false;
            _tapPending = false;
            _tapFinger = -1;
            Sfx.Play("ui");
            // the card covers most of the screen: world text behind an opaque panel is just
            // clutter, so the world's own text steps out of the way while a card is up
            if (World != null && World.Ready) World.SetTextVisible(false);
            Menus.ShowPause();
        }

        void ClosePause()
        {
            if (!_paused) return;
            _paused = false;
            Time.timeScale = 1f;
            Menus.Hide();
            if (World != null && World.Ready) World.SetTextVisible(true);
        }

        /// <summary>Marn's card pauses the world exactly like the pause card does: same freeze,
        /// same text hush, same way out through ClosePause.</summary>
        void OpenShop()
        {
            if (_dlgOpen) CloseDialog();
            _paused = true;
            Time.timeScale = 0f;
            _joyTouch = false;
            _tapPending = false;
            _tapFinger = -1;
            Sfx.Play("ui");
            if (World != null && World.Ready) World.SetTextVisible(false);
            Menus.ShowShop();
        }

        void IdleTitle()
        {
            // the empty moon hangs and sways behind the title
            if (_titleMoon != null)
                _titleMoon.localPosition = new Vector3(Mathf.Sin(Time.time * 0.5f) * 0.4f,
                    HalfH - 3.2f + Mathf.Sin(Time.time * 0.9f) * 0.25f, 0f);
            TwinkleStars();
        }

        // ------------------------------------------------------------ battle input

        void HandleBattleInput()
        {
            if (Director == null || BattleViewRef == null) return;

            // the pause card works mid-fight too - the clock freeze keeps the enemy
            // from acting while the menu is up
            if (KeyCancel()) { OpenPause(); return; }

            if (TapPressed())
            {
                Sfx.Play("ui");
                Director.TapAt(StagePos());     // menu cells, foe picking and card buttons
                return;
            }

            int step = BattleStep();
            // a cursor step ticks; the confirm still clicks - same split the cards got
            if (step != 0 && Director.AwaitingInput) { Sfx.Play("blip"); Director.SelectCell(step); return; }
            if (Input.GetKeyDown(KeyCode.Tab) && Director.AwaitingInput) { Sfx.Play("blip"); Director.CycleTarget(1); return; }
            if (KeyConfirm())
            {
                Sfx.Play("ui");
                if (Director.AwaitingInput) Director.Confirm();
                else if (BattleViewRef.OverlayButtonCount > 0) BattleViewRef.CardButtonAt(0)?.Invoke();
            }
        }

        /// <summary>The command grid is 2 x 2, so the keyboard walks it in both axes.</summary>
        int BattleStep()
        {
            if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) return -1;
            if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) return 1;
            if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W)) return -2;
            if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S)) return 2;
            return 0;
        }

        void TryInteract()
        {
            var npc = World.NearestNpc(World.HeroPos);
            int chestAt = World.NearestChest(World.HeroPos);

            // whoever is nearer wins the tap: the talk branch used to run first, so a
            // villager leaning on a chest could keep it shut forever
            if (npc.NameKey != null && chestAt >= 0 &&
                Vector2.Distance(World.HeroPos, World.ChestPos(chestAt)) <
                Vector2.Distance(World.HeroPos, World.NearestNpcPos(World.HeroPos)))
                npc = default;

            if (npc.NameKey != null)
            {

                TalkTo(npc);
                // meeting Mira is the first rung itself - once the words pass, the chest
                // errand is live without a second signature
                if (npc.NameKey == "npc.elder" && Quests.Step("mq.1") == 1)
                {
                    Quests.Complete(Quests.Find("mq.1"));
                    Quests.Accept(Quests.Find("mq.2"));
                }
                RefreshQuest();
                return;
            }
            if (chestAt >= 0)
            {
                int shardsBefore = State.MoonShards;
                World.OpenChest(chestAt);
                Sfx.Play(State.MoonShards > shardsBefore ? "shard" : "chest");
                ShowZoneBanner(World.LastLootText);
                CheckMains();
                RefreshHud();
                SaveRun();
                return;
            }

            // indoors the only way out is the mat by the door
            if (_inHouse)
            {
                if (Vector2.Distance(World.HeroPos, World.Map.ExitPos) < 1.6f) LeaveHouse();
                return;
            }

            if (World.NearDoor(World.HeroPos, out var doorAt))
            {
                EnterHouse(doorAt);
                return;
            }
            if (World.NearBoss && !_bossDown)
            {
                // the keeper gets two lines before it swings: a taunt, then the fight
                var boss = new NpcDef
                {
                    Sheet = GameMap.BossMapSheet(State.Chapter),
                    NameKey = BattleData.BossNameKey(State.Chapter),
                    Lines = BattleData.BossTaunts(State.Chapter), Monster = true,
                };
                _bossFocus = true;
                OpenDialog(boss, boss.Lines);
                _dlgThen = () => StartBattle(BattleData.BossFight(State.Chapter));
                return;
            }
            if (Vector2.Distance(World.HeroPos, World.Map.CristalPos) < 2f)
            {
                if (State.MoonShards >= ShardsNeeded) TriggerEnding();
                else ShowZoneBanner(Strings.Get("end.notyet", ShardsNeeded - State.MoonShards));
            }
        }

        // ------------------------------------------------------------ houses

        /// <summary>Walks in through the front door. The room is its own map in a WorldView of its
        /// own, so the street behind is left exactly as it was - position, opened chests and all -
        /// and stepping out again is instant.</summary>
        void EnterHouse(int houseIndex)
        {
            if (_inHouse || _doorCooldown > 0f || _testNoDoors) return;
            _houseIndex = houseIndex;
            _doorReturn = World.HeroPos + new Vector2(0f, -1.1f);
            _inHouse = true;
            _doorCooldown = 1.4f;
            Sfx.Play("door");
            DoTransition(() =>
            {
                World.gameObject.SetActive(false);
                if (_houseRoot == null)
                {
                    _houseRoot = new GameObject("house").transform;
                    _houseRoot.SetParent(StageRoot, false);
                }
                if (_houseView != null) Fx.Kill(_houseView.gameObject);
                _houseView = new GameObject("houseView").AddComponent<WorldView>();
                _houseView.transform.SetParent(_houseRoot, false);
                _houseMap = GameMap.BuildRoom(houseIndex);
                _houseView.Build(_houseMap, _houseRoot, HalfH);
                World = _houseView;
                World.PlaceHero(new Vector2(10.5f, 10.5f));
                MakeHud();
                State.NoteZone("zone.house." + _houseMap.HouseIndex);
                RefreshHud();
                ShowZoneBanner(Strings.Get(_houseMap.InteriorNameKey));
                FollowHero();
                Menus.ShowToast(Strings.Get("onb.door"), 3.2f);
            }, 0.2f, 0.3f);
        }

        void LeaveHouse()
        {
            if (!_inHouse) return;
            _inHouse = false;
            _doorCooldown = 1.4f;
            // stepping out must not be an ambush: a wild thing that followed you to the
            // doormat would otherwise touch you the first frame back on the street
            _encounterCooldown = Mathf.Max(_encounterCooldown, 1.5f);
            Sfx.Play("door");
            DoTransition(() =>
            {
                if (_houseView != null) Fx.Kill(_houseView.gameObject);
                _houseView = null;
                if (World != null && World != _houseView) World = null;
                World = FindOverworld();
                if (World != null)
                {
                    World.gameObject.SetActive(true);
                    World.PlaceHero(_doorReturn);
                }
                RefreshHud();
                FollowHero();
            }, 0.2f, 0.3f);
        }

        WorldView _overworld;

        /// <summary>The street view, remembered when the hero steps indoors. It is not torn down
        /// to enter a house: the room is a second WorldView, so the street keeps its position,
        /// its opened chests and its monsters while the hero is inside.</summary>
        WorldView FindOverworld() => _overworld != null ? _overworld : World;

        // ------------------------------------------------------------ conversation

        /// <summary>Talking is where the quest book meets the player: an NPC with something to
        /// offer says so, an NPC whose errand is done hands it over, and everyone else just talks.</summary>
        void TalkTo(NpcDef npc)
        {
            // the two of them turn to look at each other, like people would
            if (World != null)
            {
                var talker = World.FindNpc(npc.NameKey);
                World.FaceAt(talker, World.HeroPos);
                if (talker != null && talker.Root != null) World.FaceHeroAt(talker.Root.localPosition);
            }
            // a shopkeeper's dialogue IS his stall: no small talk, straight to the wares
            if (npc.Shop) { OpenShop(); return; }
            // an errand that names another soul advances when that soul is found and told -
            // the target's own line runs first; his own errands wait a talk
            foreach (var q in Quests.All)
                if (q.Kind == QuestKind.Talk && !string.IsNullOrEmpty(q.Target)
                    && q.Target == npc.NameKey && Quests.Step(q.Id) == 1)
                {
                    Quests.SetStep(q.Id, 2);
                    OpenDialog(npc, new[] { string.IsNullOrEmpty(q.MeetKey) ? "q.goal" : q.MeetKey });
                    // he said he would go home - so he does, once the talk closes. Unless
                    // some errand of his own still wants him on the road: a quest that
                    // needs this soul cannot lose him to someone else's story
                    var target = npc.NameKey;
                    _dlgThen = () =>
                    {
                        foreach (var qq in Quests.All)
                            if (qq.Giver == target && qq.Chapter <= Game.State.Chapter
                                && Quests.Step(qq.Id) != 3) return;
                        var a = World.FindNpc(target);
                        if (a != null) World.RemoveNpc(a);
                    };
                    SaveRun();
                    return;
                }
            var quest = Quests.ForGiver(npc.NameKey, out bool ready);
            if (quest != null)
            {
                if (Quests.Step(quest.Id) == 0)
                {
                    Quests.Accept(quest);
                    OpenDialog(npc, new[]
                    {
                        State.NgPlus > 0 && Strings.Has(quest.OfferKey + ".ng")
                            ? quest.OfferKey + ".ng" : quest.OfferKey,
                        "q.goal",
                    });
                    Menus.ShowToast(Strings.Get("jr.newquest", Strings.Get(quest.TitleKey)), 3.6f);
                    Sfx.Play("quest");
                    SaveRun();
                    return;
                }
                if (ready)
                {
                    Quests.Complete(quest);
                    OpenDialog(npc, new[] { quest.DoneKey, "q.reward" });
                    Menus.ShowToast(Strings.Get("jr.questdone", Strings.Get(quest.TitleKey)), 3.6f);
                    Sfx.Play("questdone");
                    RefreshHud();
                    SaveRun();
                    return;
                }
                {
                    // on a retold night the givers greet the hero as someone who has done
                    // this before - a .ng sibling of the offer that only ever speaks then
                    string offer = quest.OfferKey;
                    if (State.NgPlus > 0 && Strings.Has(offer + ".ng")) offer += ".ng";
                    OpenDialog(npc, new[] { offer, Quests.Line(quest) });
                }
                return;
            }
            OpenDialog(npc);
        }

        void OpenDialog(NpcDef npc, string[] lines)
        {
            _dlgNpc = npc;
            _dlgLines = lines;
            _dlgIndex = 0;
            _dlgOpen = true;
            _dlgThen = null;   // a fresh talk never runs whatever a previous close had queued
            DialogRoot.gameObject.SetActive(true);
            if (_dlgSheet != null && Application.isPlaying)
            {
                _dlgSheet.localPosition = new Vector3(0f, -0.55f, 0f);
                StartCoroutine(Fx.MoveLocal(_dlgSheet, Vector3.zero, 0.18f));
            }
            _dlgText.RevealSpeed = Prefs.RevealSpeed;
            _dlgName.Set(Strings.Get(npc.NameKey));
            string body = FormatLine(lines[0]);
            LayoutDialogBox(body);
            _dlgText.Set(body);
            if (_dlgPortrait != null)
            {
                if (npc.Monster)
                {
                    _dlgPortrait.sprite = TexArt.MapMonster(npc.Sheet, 1);
                    _dlgPortrait.transform.localScale = Vector3.one * 1.15f;
                }
                else if (!string.IsNullOrEmpty(npc.Walk))
                {
                    var frames = Bank.Frames(npc.Walk);
                    _dlgPortrait.sprite = frames.Length > 0 ? frames[0] : null;
                    _dlgPortrait.transform.localScale = Vector3.one * 3f;
                }
                else
                {
                    _dlgPortrait.sprite = TexArt.Chara(Folks.Sheet(npc), (int)Dir.Down, 1);
                    _dlgPortrait.transform.localScale = Vector3.one * 3f;
                }
                _dlgPortrait.enabled = _dlgPortrait.sprite != null;
            }
            if (!string.IsNullOrEmpty(npc.JoinKey) && !State.Joined.Contains(npc.JoinKey)
                && _dlgThen == null)
                _dlgThen = () => Join(npc);
            Sfx.Play("blip");
        }

        /// <summary>A dialog line is a string key, or a literal pair "key|arg" when a quest line
        /// needs a number in it ("3 more shards").</summary>
        string FormatLine(string key)
        {
            if (string.IsNullOrEmpty(key)) return "";
            int bar = key.IndexOf('|');
            if (bar < 0) return Strings.Get(key);
            var parts = key.Substring(bar + 1).Split(',');
            var args = new object[parts.Length];
            for (int i = 0; i < parts.Length; i++) args[i] = parts[i];
            return Strings.Get(key.Substring(0, bar), args);
        }

        // ------------------------------------------------------------ dialog

        void OpenDialog(NpcDef npc)
        {
            _dlgNpc = npc;
            _dlgLines = npc.Lines;
            // someone the hero has already talked to opens on a different quip instead of
            // reciting the same first line every tap
            _dlgIndex = _metNpcs.Add(npc.NameKey) || _dlgLines.Length < 2
                ? 0 : Random.Range(1, _dlgLines.Length);
            _dlgOpen = true;
            _dlgThen = null;
            DialogRoot.gameObject.SetActive(true);
            if (_dlgSheet != null && Application.isPlaying)
            {
                _dlgSheet.localPosition = new Vector3(0f, -0.55f, 0f);
                StartCoroutine(Fx.MoveLocal(_dlgSheet, Vector3.zero, 0.18f));
            }
            _dlgText.RevealSpeed = Prefs.RevealSpeed;
            _dlgName.Set(Strings.Get(npc.NameKey));
            LayoutDialogBox(Strings.Get(_dlgLines[_dlgIndex]));
            _dlgText.Set(Strings.Get(_dlgLines[_dlgIndex]));
            if (_dlgPortrait != null)
            {
                // walk-anim folk have no chara sheet to strip: the first frame of their own
                // rig is their face, the same figure that fights beside you later
                if (!string.IsNullOrEmpty(npc.Walk))
                {
                    var frames = Bank.Frames(npc.Walk);
                    _dlgPortrait.sprite = frames.Length > 0 ? frames[0] : null;
                }
                else
                    _dlgPortrait.sprite = TexArt.Face(Folks.Sheet(npc))
                        ?? TexArt.Chara(Folks.Sheet(npc), (int)Dir.Down, 1);
                _dlgPortrait.transform.localScale = Vector3.one * 3f;
                _dlgPortrait.enabled = _dlgPortrait.sprite != null;
            }
            // a recruiting talk ends in a yes: closing their dialog runs the join, which
            // pulls their wandering self off the map and lights their walker on the trail
            if (!string.IsNullOrEmpty(npc.JoinKey) && !State.Joined.Contains(npc.JoinKey))
                _dlgThen = () => Join(npc);
            Sfx.Play("blip");
        }

        /// <summary>A companion says yes: their key joins the company, their wandering self
        /// leaves the map, the trail gains a walker, and the whole thing is saved the same
        /// moment so a join can never be lost.</summary>
        void Join(NpcDef npc)
        {
            if (State.Joined.Contains(npc.JoinKey)) return;
            State.Joined.Add(npc.JoinKey);
            World.SyncParty();
            // the welcome gets the same golden flecks a chest earns - a companion is a find
            World.BurstLoot(npc.Pos);
            Menus.ShowToast(Strings.Get("jr.join", Strings.Get(npc.NameKey)), 3.6f);
            Sfx.Play("befriend");
            RefreshQuest();
            SaveRun();
        }

        void UpdateDialog()
        {
            if (_dlgPortrait != null && _dlgPortrait.enabled)
            {
                var pp = _dlgPortrait.transform.localPosition;
                pp.y = -HalfH + 0.4f + 2.1f + Mathf.Sin(Time.time * 2.2f) * 0.045f;
                _dlgPortrait.transform.localPosition = pp;
            }
            if (_dlgNext != null)
            {
                // the tick is for "the line is done" - it stays dark while the typewriter
                // runs (component.enabled can't hide its glyph pool, only alpha can)
                float a = _dlgText.IsRevealing ? 0f : 0.55f + 0.45f * Mathf.Sin(Time.time * 6f);
                _dlgNext.SetColor(new Color(1f, 0.85f, 0.5f, a));
            }
            if (_dlgText.IsRevealing)
            {
                // soft tick every few revealed characters, the typewriter chatter
                int vc = _dlgText.VisibleChars;
                if (vc < _dlgChars || vc - _dlgChars >= 4) { _dlgChars = vc; Sfx.Play("tick"); }
                if (TapPressed() || KeyConfirm()) _dlgText.Set(_dlgText.Text, true);
                return;
            }
            if (TapPressed() || KeyConfirm())
            {
                _dlgIndex++;
                if (_dlgIndex < _dlgLines.Length)
                {
                    string next = FormatLine(_dlgLines[_dlgIndex]);
                    LayoutDialogBox(next);
                    _dlgText.Set(next);
                }
                else CloseDialog();
            }
        }

        void CloseDialog()
        {
            _dlgOpen = false;
            _bossFocus = false;   // hand the frame back to the hero
            // The notice lane is released by the frame's own pass in Update(): it holds while a
            // dialog box is open or while the zone card is up, and a notice that waited is shown
            // the moment neither is on screen.
            DialogRoot.gameObject.SetActive(false);
            _encounterCooldown = Mathf.Max(_encounterCooldown, 1.5f);
            var then = _dlgThen;
            _dlgThen = null;
            then?.Invoke();
        }

        // ------------------------------------------------------------ battles

        public void StartBattle(MonsterSpec[] specs)
        {
            // a fight queued before the ending was called must not land after it: its
            // transition middle re-activates the stage over the dawn - caught on film by
            // the audit, party rigs standing under the ending prose
            if (_ending || Phase == St.End) return;
            Phase = St.Battle;
            // breathing room after a fight before the next wild touch can trigger
            _encounterCooldown = 6f;
            Menus.Hide();
            _paused = false;
            _tapPending = false;
            _tapFinger = -1;
            _joyTouch = false;
            // the arena blinks in rather than snapping: a fast dark beat covers the swap
            DoTransition(() =>
            {
                // the fade rides ahead of the middle: if the ending was called while this
                // battle's blackout was still running, the stage must stay down
                if (_ending || Phase == St.End) return;
                SetCamY(0f);
                World.gameObject.SetActive(false);
                if (_hudZone != null) _hudZone.enabled = false;
                SetHudQuestVisible(false);
                BattleViewRef.gameObject.SetActive(true);
                Director.StartBattle(specs);
            }, 0.16f, 0.3f);
            bool boss = false;
            foreach (var s in specs) if (s.Boss) boss = true;
            Sfx.Play(boss ? "boss" : "blip");
        }

        void OnEncounterWon()
        {
            // a stale director finishing under the dawn must not pull the phase back
            if (_ending || Phase == St.End) return;
            Sfx.Mus.Duck = 1f; Sfx.Mus.Play("explore");
            DoTransition(() =>
            {
                // the fade lands 0.22s after the phase flips: a fight that began inside
                // that gap owns the stage now - tearing it down mid-setup starved every
                // battle coroutine when the selftest staged its loss in exactly that window.
                // the card still dies, though: its buttons must not ghost over the new fight
                if (Phase == St.Battle) { BattleViewRef.HideCard(); return; }
                BattleViewRef.gameObject.SetActive(false);
                BattleViewRef.HideCard();
                World.gameObject.SetActive(true);
                FollowHero();
            }, 0.22f, 0.3f);
            Phase = St.Explore;
            if (_hudZone != null) _hudZone.enabled = true;
            SetHudQuestVisible(true);
            _paused = false;
            World.ResetForChapter();
            World.SyncFriends();
            World.SetTextVisible(true);
            RefreshHud();
            SaveRun();
        }

        void OnBossDefeated()
        {
            Sfx.Mus.Duck = 1f; Sfx.Mus.Play("explore");
            _bossDown = true;
            // the last rung of the main line is this kill itself
            if (State.Chapter >= 3 && Quests.Step("mq.3") == 1)
                Quests.Complete(Quests.Find("mq.3"));
            if (World != null) World.RemoveBoss();
            BattleViewRef.gameObject.SetActive(false);
            BattleViewRef.HideCard();
            Phase = St.Explore;
            World.gameObject.SetActive(true);
            if (_hudZone != null) _hudZone.enabled = true;
            SetHudQuestVisible(true);
            FollowHero();
            World.SetTextVisible(true);

            if (State.Chapter >= 3)
            {
                // the guard wore the last shard; the way to the cristal is open
                State.MoonShards = Mathf.Max(State.MoonShards, ShardsNeeded);
                CheckMains();   // four shards + the guard's fall finish their quests together
                ShowZoneBanner(Strings.Get("zone.bossdown"));
                RefreshHud();
                SaveRun();
            }
            else
            {
                ShowZoneBanner(Strings.Get("zone.chapdone"));
                int next = State.Chapter + 1;
                // the save rides the chapter change, not the fall - a quit inside the
                // dissolve resumes before the kill instead of half-advanced
                DoTransition(() => { StartChapter(next); SaveRun(); }, 2.2f, 0.4f);
            }
        }

        void OnRunLost()
        {
            if (_ending || Phase == St.End) return;
            Sfx.Mus.Duck = 1f; Sfx.Mus.Play("explore");
            // flip synchronously like the win path does: a new fight that begins inside the
            // fade re-marks the phase in its own body, so the middle can tell a stale teardown
            // (phase flipped back to Battle) from the defeat it belongs to
            Phase = St.Explore;
            DoTransition(() =>
            {
                // same window as a victory: a new fight inside the fade owns the stage -
                // but its leftover card buttons still have to go or they ghost over it
                if (Phase == St.Battle) { BattleViewRef.HideCard(); return; }
                BattleViewRef.HideCard();
                BattleViewRef.gameObject.SetActive(false);
                World.gameObject.SetActive(true);
                if (_hudZone != null) _hudZone.enabled = true;
                SetHudQuestVisible(true);
                _paused = false;
                World.ResetForChapter();
                World.PlaceHero(World.Map.VillageCenter);
                FollowHero();
                World.SetTextVisible(true);   // the banner lives under HudRoot: no text, no banner
                ShowZoneBanner(Strings.Get("zone.retreat"));
                RefreshHud();
                SaveRun();   // the retreat is where the night picks up again
            }, 0.3f, 0.4f);
        }

        void TriggerEnding()
        {
            DoTransition(() =>
            {
                _ending = true;
                // the ledger is snapped before the dawn hangs its own medals on the wall -
                // ENDER and NG+ belong to the retelling's count, not this one's farewell
                int runMedals = Medals.EarnedThisRun;
                Medals.Grant("ender");
                Phase = St.End;
                SetCamY(0f);
                World.gameObject.SetActive(false);
                // the battle stage is its own root: if the ending fires while a fight is still
                // up (a wild touch on the way to the cristal), its rigs outlive the tale
                BattleViewRef.HideCard();
                BattleViewRef.gameObject.SetActive(false);
                if (_hudZone != null) _hudZone.enabled = false;
                _endRoot.gameObject.SetActive(true);
                Menus.HideToast();   // the last notice of the night does not ride into the dawn
                // the payoff is a moon-rise, not a card: start it low and dim, the words
                // and the tap wait until it has climbed. The bob write in Update is gated
                // on _endRise so the two never fight over the same transform
                _endRise = 0f;
                _endSky.color = new Color(0.05f, 0.05f, 0.14f, 1f);
                _endMoon.localScale = Vector3.one * 4.2f;
                _endMoon.localPosition = new Vector3(0f, -HalfH + 3f, 0f);
                _endGlow.color = new Color(1f, 0.95f, 0.75f, 0f);
                _endLines.gameObject.SetActive(false);
                _endStats.gameObject.SetActive(false);
                _tapHint.gameObject.SetActive(false);
                StartCoroutine(CoMoonRise());
                Sfx.Mus.Duck = 1f; Sfx.Mus.Play("end");
                _endLines.RevealSpeed = 0f;
                // three tellings of the same dawn: alone, one companion, or a company.
                // Counted from the stable, not the run - a friend you let go does not
                // walk home beside you, and the ending should not say it does
                int company = State.Friends.Count;
                string textKey = company == 0 ? "end.text.lone"
                    : company == 1 ? "end.text.one" : "end.text";
                _endLines.Set(Strings.Get(textKey));
                _endStats.Set((company == 0
                    ? Strings.Get("end.stats.lone", State.Level, State.Gold,
                        State.Defeats, State.Defeats == 1 ? "BEAST" : "BEASTS")
                    : Strings.Get("end.stats", State.Level, company,
                        company == 1 ? "FRIEND" : "FRIENDS", State.Gold,
                        State.Defeats, State.Defeats == 1 ? "BEAST" : "BEASTS"))
                    + (runMedals > 0 ? "\n" + Strings.Get("end.medals", runMedals,
                        runMedals == 1 ? "MEDAL" : "MEDALS") : "")
                    + "\n" + Strings.Get("end.again"));
                // the ledger lives between poem and hint: a fixed y only held for the
                // shortest telling, so anchor it under the poem's measured bottom and
                // above the tap hint's band (position = the block's top edge)
                {
                    float poemBottom = HalfH - 10f - _endLines.MeasureHeight(_endLines.Text);
                    // the hint band is a FLOOR and the poem is a CEILING: the block's top
                    // must sit at or above 'aboveHint' to keep its bottom out of the tap
                    // hint, and as near under the poem as that allows - min() had it
                    // backwards and dropped the ledger through the hint whenever the
                    // company poem ran long
                    float aboveHint = -HalfH + 3.9f + _endStats.MeasureHeight(_endStats.Text) + 1.6f;
                    _endStats.transform.localPosition =
                        new Vector3(0f, Fx.Snap(Mathf.Max(aboveHint, poemBottom - 0.8f)), 0f);
                }
                // the company walks home on the screen's edge: up to three friends stand
                // as small silhouettes on the horizon line under the tap hint. Cleared
                // first - the tale can end more than once and the hill would collect ghosts
                for (int i = _endRoot.childCount - 1; i >= 0; i--)
                    if (_endRoot.GetChild(i).name.StartsWith("endFriend"))
                        Destroy(_endRoot.GetChild(i).gameObject);
                {
                    int shown = 0;
                    foreach (var key in State.Friends)
                    {
                        if (shown >= 3) break;
                        var sk = key.StartsWith("moon.") ? key.Substring(5) : key;
                        var spec = BattleData.Species(sk);
                        var spr = spec.HasValue ? TexArt.MapMonster(spec.Value.MapSheet, 1) : null;
                        if (spr == null) continue;
                        var fr = SpriteRendererUtil.Make(_endRoot, "endFriend" + shown, spr, 98);
                        fr.transform.localPosition =
                            new Vector3((shown - 1f) * 1.9f, -HalfH + 1.4f, 0f);
                        fr.transform.localScale = Vector3.one * 1.5f;
                        // the first light catches only their shape - not their faces;
                        // a moonlit one still shines a little silver out of the dark
                        fr.color = key.StartsWith("moon.")
                            ? new Color(0.62f, 0.72f, 1f, 0.95f)
                            : new Color(0.4f, 0.42f, 0.6f, 0.95f);
                        shown++;
                    }
                }
                // the tale ends but the night keeps you: CONTINUE walks it again with the
                // company's strength kept, its beasts grown bolder, its caches shut again,
                // its errands unwritten - every retelling of the night bites deeper
                State.NgPlus++;
                Medals.Grant("ngp");
                Medals.EarnedThisRun = 0;   // the retelling keeps its own ledger
                State.Chapter = 1; State.MoonShards = 0;
                State.ChestsOpened = 0; State.ChestsDone.Clear();
                State.Zones.Clear(); State.CurZone = "village"; State.ObjZone = null;
                Quests.Reset();
                // and the telling starts where every telling starts: the village square
                if (World != null && World.Ready) World.PlaceHero(World.Map.VillageCenter);
                _bossDown = false;
                SaveRun();
            }, 0.4f, 0.6f);
        }

        /// <summary>The shards leave the cristal: the moon climbs out of the low sky and the
        /// night remembers what colour it was. ~3.4s of rise, then the bob takes over and the
        /// epilogue shows itself.</summary>
        IEnumerator CoMoonRise()
        {
            var skyFrom = new Color(0.05f, 0.05f, 0.14f, 1f);
            var skyTo = new Color(0.09f, 0.09f, 0.2f, 1f);
            var posFrom = new Vector3(0f, -HalfH + 3f, 0f);
            var posTo = new Vector3(0f, HalfH - 4.2f, 0f);
            yield return Fx.Wait(0.35f);
            Sfx.Play("shard");
            float t = 0f;
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / 3.4f;
                float k = Mathf.Clamp01(t);
                k = k * k * (3f - 2f * k);   // smoothstep: a rise, not a lift
                _endMoon.localPosition = Vector3.LerpUnclamped(posFrom, posTo, k);
                _endMoon.localScale = Vector3.one * Mathf.Lerp(4.2f, 6f, k);
                _endSky.color = Color.Lerp(skyFrom, skyTo, k);
                var gc = _endGlow.color; gc.a = Mathf.Lerp(0f, 0.5f, k); _endGlow.color = gc;
                yield return null;
            }
            _endRise = 1f;
            _endLines.gameObject.SetActive(true);
            _endStats.gameObject.SetActive(true);
            _tapHint.gameObject.SetActive(true);
            Sfx.Play("win");
        }

        IEnumerator CoWait(float t, System.Action done)
        {
            yield return Fx.Wait(t);
            done();
        }

        // ------------------------------------------------------------ input

        bool TapPressed()
        {
            // only a finger that JUST landed counts. Any touch used to count, so a thumb
            // parked on the glass (walking, resting) swallowed presses meant for the
            // command menu, and the tap resolved to that thumb's position instead --
            // the reason the battle menus looked dead on a phone but worked in the self-test
            // (which only ever had one pointer).
            if (Input.touchCount > 0)
            {
                foreach (var t in Input.touches)
                    if (t.phase == TouchPhase.Began) return true;
                return false;
            }
            return Input.GetMouseButtonDown(0);
        }

        /// <summary>Where the pointer is, in stage units. The finger that just landed wins over
        /// any finger merely resting on the screen, and over the mouse, so the joystick thumb and
        /// the menu thumb never get mixed up.</summary>
        Vector2 StagePos()
        {
            Vector3 p = Input.mousePosition;
            if (Input.touchCount > 0)
            {
                bool found = false;
                foreach (var t in Input.touches)
                    if (t.phase == TouchPhase.Began) { p = t.position; found = true; break; }
                if (!found)
                    foreach (var t in Input.touches)
                        if (t.phase == TouchPhase.Moved || t.phase == TouchPhase.Stationary) { p = t.position; break; }
            }
            return ScreenToStage(p);
        }

        /// <summary>Book-keeping for a deliberate tap on the world: the finger has to stay put
        /// and lift quickly. Dragging the joystick no longer trips every NPC, chest and gate near
        /// the hero, which is what made walking around feel like it kept opening things.</summary>
        void PollWorldTap()
        {
            if (Input.touchCount > 0)
            {
                foreach (var t in Input.touches)
                {
                    if (t.phase == TouchPhase.Began)
                    {
                        _tapFinger = t.fingerId;
                        _tapStart = t.position;
                        _tapTime = Time.unscaledTime;
                        _tapMoved = false;
                    }
                    else if (t.fingerId == _tapFinger)
                    {
                        if (t.phase == TouchPhase.Moved || t.phase == TouchPhase.Stationary)
                        {
                            if (Vector2.Distance(t.position, _tapStart) > 18f) _tapMoved = true;
                        }
                        else if (t.phase == TouchPhase.Ended)
                        {
                            if (!_tapMoved && Time.unscaledTime - _tapTime < 0.45f)
                            {
                                _tapStage = ScreenToStage(t.position);
                                _tapPending = true;
                            }
                            _tapFinger = -1;
                        }
                        else if (t.phase == TouchPhase.Canceled) _tapFinger = -1;
                    }
                }
            }
            else
            {
                _tapFinger = -1;
                // desktop helper: a plain left click taps (the mouse joystick needs Ctrl or RMB)
                if (Input.GetMouseButtonDown(0) && !Input.GetKey(KeyCode.LeftControl))
                {
                    _tapStage = ScreenToStage(Input.mousePosition);
                    _tapPending = true;
                }
            }
        }

        bool KeyDown(KeyCode a) => Input.GetKeyDown(a);

        /// <summary>-1 / +1 for menu rows.</summary>
        int KeyStep()
        {
            if (KeyDown(KeyCode.UpArrow) || KeyDown(KeyCode.W)) return -1;
            if (KeyDown(KeyCode.DownArrow) || KeyDown(KeyCode.S)) return 1;
            if (KeyDown(KeyCode.LeftArrow) || KeyDown(KeyCode.A)) return -1;
            if (KeyDown(KeyCode.RightArrow) || KeyDown(KeyCode.D)) return 1;
            return 0;
        }

        bool KeyConfirm() => KeyDown(KeyCode.Return) || KeyDown(KeyCode.KeypadEnter)
            || KeyDown(KeyCode.Space) || KeyDown(KeyCode.Z);

        bool KeyCancel() => KeyDown(KeyCode.Escape) || KeyDown(KeyCode.Backspace);

        // ------------------------------------------------------------ screens & saves

        /// <summary>Called when a story card or chapter card is dismissed.</summary>
        void OnScreenDone()
        {
            var act = _afterScreen;
            _afterScreen = null;
            if (act != null) act();
            else ShowTitle();
        }

        public void SaveRun()
        {
            float hx = 0f, hy = 0f;
            if (World != null && World.Ready && World.Hero != null)
            {
                hx = World.HeroPos.x;
                hy = World.HeroPos.y;
                // a night saved indoors resumes on the doorstep, not inside the
                // little room's coordinates - those mean somewhere else outdoors
                if (_inHouse) { hx = _doorReturn.x; hy = _doorReturn.y; }
            }
            SaveSystem.Write(State.Capture(hx, hy, _bossDown));
        }

        public void ContinueRun()
        {
            var d = SaveSystem.Read();
            if (d == null) { BeginRun(); return; }
            State.Apply(d);
            _hintTalk = false;
            _hintChest = false;
            _ending = false;
            _resumePos = new Vector2(d.heroX, d.heroY);
            Menus.Hide();
            World.gameObject.SetActive(false);
            bool keepBoss = d.bossDown;
            DoTransition(() =>
            {
                StartChapter(State.Chapter);
                // the keeper you already felled does not climb back out of the save
                _bossDown = keepBoss;
                if (_bossDown && World != null) World.RemoveBoss();
            }, 0.25f, 0.45f);
        }

        public void LeaveToTitle()
        {
            Menus.Hide();
            _paused = false;
            if (World != null && World.Ready) World.SetTextVisible(true);
            SaveRun();   // leaving keeps the night where it stands - no lost walks
            DoTransition(() => ShowTitle(), 0.25f, 0.35f);
        }

        Vector2 ReadMoveInput()
        {
            // keyboard (desktop)
            var kb = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            if (kb.sqrMagnitude > 0.01f) return kb;

            // touch joystick anywhere on the lower 60% of the screen
            Vector2 vec = Vector2.zero;
            if (Input.touchCount > 0)
            {
                foreach (var t in Input.touches)
                {
                    if (t.phase == TouchPhase.Began && !_joyTouch && t.position.y < Screen.height * 0.75f)
                    {
                        _joyTouch = true; _joyFinger = t.fingerId; _joyCenter = t.position;
                    }
                    else if (_joyTouch && t.fingerId == _joyFinger)
                    {
                        _joyCur = t.position;
                        if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled) { _joyTouch = false; _joyFinger = -1; }
                        else
                        {
                            var d = (t.position - _joyCenter) / Mathf.Min(Screen.width, Screen.height) * 2.2f;
                            vec = Vector2.ClampMagnitude(d, 1f);
                        }
                    }
                }
            }
            else
            {
                // mouse drag as joystick (desktop testing)
                if (Input.GetMouseButtonDown(1) || (Input.GetMouseButton(0) && Input.GetKey(KeyCode.LeftControl)))
                {
                    _joyTouch = true; _joyCenter = Input.mousePosition;
                }
                if (_joyTouch && Input.GetMouseButton(0))
                {
                    _joyCur = Input.mousePosition;
                    var d = ((Vector2)Input.mousePosition - _joyCenter) / Mathf.Min(Screen.width, Screen.height) * 2.2f;
                    vec = Vector2.ClampMagnitude(d, 1f);
                    if (vec.sqrMagnitude < 0.003f) vec = Vector2.zero;
                }
                if (_joyTouch && Input.GetMouseButtonUp(0)) _joyTouch = false;
            }
            _joyVec = vec;
            return vec;
        }

        /// <summary>The inverse of ScreenToStage, for tests that need to aim at the screen the
        /// way a finger would instead of poking world coordinates directly.</summary>
        public Vector3 StageToScreen(Vector2 stage)
        {
            return new Vector3((stage.x / 18f + 0.5f) * Screen.width,
                (stage.y / (HalfH * 2f) + 0.5f) * Screen.height, 0f);
        }

        public Vector2 ScreenToStage(Vector3 screenPos)
        {
            float nx = Screen.width > 0 ? screenPos.x / Screen.width : 0.5f;
            float ny = Screen.height > 0 ? screenPos.y / Screen.height : 0.5f;
            return new Vector2((nx - 0.5f) * 18f, (ny - 0.5f) * (HalfH * 2f));
        }

        void OnGUI()
        {
            if (Target == null) return;
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Target, ScaleMode.StretchToFill, false);
        }

        // ------------------------------------------------------------ editor preview

        /// <summary>Snaps every text block to the pixel grid. LateUpdate does this while the
        /// game runs; the editor render never advances a frame, so the capture has to do it or
        /// the previews would show text that is half a pixel off while the game is not.</summary>
        void SnapTextLayer()
        {
            var labels = Object.FindObjectsOfType<PixelLabel>(true);
            foreach (var l in labels)
            {
                if (l == null || !l.gameObject.activeInHierarchy || !l.SnapToPixelGrid) continue;
                var w = l.transform.position;
                var s = new Vector3(Fx.Snap(w.x), Fx.Snap(w.y), w.z);
                if (s != w) l.transform.position = s;
            }
        }

        public void RenderToPng(string path)
        {
            if (Cam == null || Target == null) return;
            SnapTextLayer();
            var prev = RenderTexture.active;
            Cam.Render();
            RenderTexture.active = Target;
            var tex = new Texture2D(Target.width, Target.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, Target.width, Target.height), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            var bytes = tex.EncodeToPNG();
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
            System.IO.File.WriteAllBytes(path, bytes);
            Debug.Log("MoonThief preview written: " + path + " (" + bytes.Length + " bytes)");
        }

        // ------------------------------------------------------------ editor preview driving

        public void EditorSplash()
        {
            Menus.Hide();
            ShowSplash();
        }

        public void EditorMenu()
        {
            Menus.Hide();
            ShowTitle();
        }

        public void EditorSettings()
        {
            SetCam(0f, 0f);
            _titleRoot.gameObject.SetActive(false);
            World.gameObject.SetActive(false);
            Menus.Hide();
            Menus.ShowSettings(false);
        }

        public void EditorCredits()
        {
            SetCam(0f, 0f);
            _titleRoot.gameObject.SetActive(false);
            World.gameObject.SetActive(false);
            Menus.Hide();
            Menus.ShowCredits();
        }

        public void EditorCinema(int slide = 5)
        {
            SetCam(0f, 0f);
            _titleRoot.gameObject.SetActive(false);
            _endRoot.gameObject.SetActive(false);
            World.gameObject.SetActive(false);
            Menus.Hide();
            Menus.ShowCinema(slide);
        }

        public void EditorChapterCard(int chapter = 2)
        {
            SetCam(0f, 0f);
            _titleRoot.gameObject.SetActive(false);
            World.gameObject.SetActive(false);
            Menus.Hide();
            Menus.ShowChapterCard(chapter);
        }

        public void EditorPause()
        {
            Menus.Hide();
            if (World != null && World.Ready) World.SetTextVisible(false);
            Menus.ShowPause();
            // the pause card rides the camera; Update() is what normally moves it, and the
            // editor render never runs Update -- without this the card sat at the origin
            // while the camera was out in the fields, so the shot showed a bare world
            Menus.SetAnchor(Cam.transform.localPosition);
        }

        public void EditorExplore(int chapter)
        {
            State.NewRun();
            State.Chapter = chapter;
            Phase = St.Explore;
            Quests.SetStep("mq.1", 3);            // a staged run is past the first talk
            if (Quests.Step("mq.2") == 0) Quests.Accept(Quests.Find("mq.2"));
            _inHouse = false;          // staging the street cancels any room we stood in
            Menus.Hide();
            _titleRoot.gameObject.SetActive(false);
            _endRoot.gameObject.SetActive(false);
            BattleViewRef.gameObject.SetActive(false);
            World.gameObject.SetActive(true);

            if (World.Ready && World.MapChapter == chapter) World.ResetForChapter();
            else
            {
                if (World.Ready) World.Teardown();
                _map = GameMap.Build(chapter);
                World.Build(_map, StageRoot, HalfH);
                World.MapChapter = chapter;
            }
            if (World.Ready) World.SetTextVisible(true);
            World.PlaceHero(World.Map.VillageCenter + new Vector2(1.5f, 1.5f));
            MakeHud();
            _questText = null;
            FollowHero();   // both axes: x matters too, the map is 60 tiles wide
            RefreshHud();
        }

        /// <summary>The two HUD lines live on the world view, so they are rebuilt whenever the
        /// view is swapped (street to room and back).</summary>
        void MakeHud()
        {
            if (_hudZone == null)
                _hudZone = PixelLabelUtil.Make(World.HudRoot, "hudZone", 1, new Color(0.94f, 0.95f, 1f), TextAlign.Left, 5001);
            _hudZone.transform.localPosition = new Vector3(G.Left + 0.55f, HalfH - 0.5f, 0f);
            _hudZone.enabled = true;
            if (_hudQuest == null)
            {
                _hudQuest = PixelLabelUtil.Make(World.HudRoot, "hudQuest", 1, new Color(0.78f, 0.82f, 1f), TextAlign.Left, 5001);
                // The goal used to be allowed 15.2 units, which is 39 characters a line: the line
                // ran most of the way across the screen and a two-line goal was a slab of text
                // over the village. 11.4 units keeps it a two-line note in the corner.
                _hudQuest.MaxWidthUnits = 11.4f;
            }
            if (_hudQuestChip == null)
            {
                // a faint chip under the goal line: light pixel text over sunlit grass was
                // unreadable, and the zone banner already carries the same dark backing
                _hudQuestChip = SpriteRendererUtil.Make(World.HudRoot, "hudQuestChip", TexArt.Solid(), 5000);
                _hudQuestChip.drawMode = SpriteDrawMode.Sliced;
                _hudQuestChip.color = new Color32(10, 8, 20, 150);
            }
            _hudQuest.transform.localPosition = new Vector3(G.Left + 0.55f, HalfH - 1.7f, 0f);
            SetHudQuestVisible(true);
        }

        void SetHudQuestVisible(bool v)
        {
            // _hudQuest outlives the world it was parented under: a new chapter rebuilds the
            // WorldView and the old label is Unity-dead but the field still holds it. Guard or
            // the first battle of night two throws before MakeHud re-seats the references.
            if (_hudQuest != null) _hudQuest.enabled = v;
            if (_hudQuestChip != null && _hudQuest != null)
                _hudQuestChip.enabled = v && _hudQuest.gameObject.activeSelf;
        }

        public void EditorDialog()
        {
            OpenDialog(Folks.Village(State.Chapter)[0]);
        }

        /// <summary>Talking for real: the NPC accepts a quest, the dialog opens and the toast
        /// fires. That combination is what put a notification on top of the narration.</summary>
        public void EditorTalk()
        {
            EditorExplore(1);
            if (World.Npcs.Count == 0) return;
            TalkTo(World.Npcs[0].Npc);
        }

        public void EditorLootToast()
        {
            CloseDialog();
            EditorExplore(1);
            State.AddBag("item.honey");
            Menus.ShowToast(Strings.Get("loot.found", Strings.Get("item.honey"), 12), 6f);
            // the toast follows the camera through SetAnchor, which Update normally calls; the
            // editor render never runs Update, so without this the shot has the toast at the
            // world origin instead of where a player would see it
            Menus.SetAnchor(Cam.transform.localPosition);
        }

        /// <summary>The zone card, on the frame the notice lane it shares is empty. The notice is
        /// parked while the card is up (Game.Update holds the lane on World.BannerUp), so showing
        /// both here would print a shot of a state the game never reaches - and the card would come
        /// out half hidden under the notice plate.</summary>
        public void EditorBanner()
        {
            CloseDialog();
            EditorExplore(1);
            Menus.HideToast();
            ShowZoneBanner(Strings.Get("zone.arrive.2"));
            Menus.SetAnchor(Cam.transform.localPosition);
        }

        public void EditorCloseDialog() => CloseDialog();

        public void EditorPlaceHero(Vector2 pos)
        {
            World.PlaceHero(pos);
            FollowHero();
        }

        public void EditorBattle()
        {
            Phase = St.Battle;
            Menus.Hide();
            World.gameObject.SetActive(false);
            if (_hudZone != null) _hudZone.enabled = false;
            SetHudQuestVisible(false);
            SetCamY(0f);
            BattleViewRef.gameObject.SetActive(true);
            Director.StartBattle(BattleData.Roll(State.Chapter, new System.Random(7)));
            Director.EditorTick();   // edit mode: coroutines are dead, drive the round start by hand
        }

        public void EditorBattleMid()
        {
            var v = BattleViewRef;
            if (v.Enemies.Length > 0)
            {
                v.Enemies[0].Hp = Mathf.Max(1, v.Enemies[0].Hp * 2 / 3);
                v.FloatNumber(v.BodyCenter(v.Enemies[0]) + new Vector2(0f, 0.6f), "-7", new Color(1f, 0.95f, 0.75f));
                v.SetMessage(Strings.Get("bt.trybefriend", v.Enemies[0].Name));
            }
            if (v.Party.Length > 1) v.Party[1].Hp = 12;
            v.Refresh();
        }

        public void EditorWinCard()
        {
            BattleViewRef.ShowCard(Strings.Get("card.wintitle"),
                new[]
                {
                    Strings.Get("card.xp", 90),
                    Strings.Get("card.gold", 34),
                    Strings.Get("card.befriended", 1, 1),
                    Strings.Get("card.joined"),
                },
                new[] { Strings.Get("btn.continue") },
                new System.Action[] { () => { } },
                new Color(0.75f, 1f, 0.8f));
        }

        public void BackToExplore()
        {
            BattleViewRef.HideCard();
            BattleViewRef.gameObject.SetActive(false);
        }

        public void EditorEnding()
        {
            State.Befriended = 3;
            TriggerEnding();
        }

        // ---- interior and journal frames. Both are real states of the game, reached here
        // without the taps and the fade that would normally lead to them, so the renderer can
        // show a room and the pages the pause card opens.

        public void EditorInterior(int house)
        {
            State.NewRun();
            State.Chapter = 1;
            Phase = St.Explore;
            Quests.SetStep("mq.1", 3);
            if (Quests.Step("mq.2") == 0) Quests.Accept(Quests.Find("mq.2"));
            Menus.Hide();
            _titleRoot.gameObject.SetActive(false);
            _endRoot.gameObject.SetActive(false);
            BattleViewRef.gameObject.SetActive(false);
            // only remember the view we came FROM when we actually came from outside - a
            // chained interior visit would otherwise stash the about-to-be-killed house
            // view as the "overworld", and LeaveHouse would hand back a dead WorldView.
            // no Teardown: the overworld must stay whole so LeaveHouse can walk back into it
            if (World != null && !_inHouse)
            {
                _overworld = World;
                _overworld.gameObject.SetActive(false);   // EnterHouse hides it too - its canopy mesh reads as black patches inside the room
                _doorReturn = World.Map != null ? World.Map.VillageCenter : new Vector2(9.5f, 12.5f);
            }
            if (_houseRoot == null)
            {
                _houseRoot = new GameObject("house").transform;
                _houseRoot.SetParent(StageRoot, false);
            }
            if (_houseView != null) Fx.Kill(_houseView.gameObject);
            _houseView = new GameObject("houseView").AddComponent<WorldView>();
            _houseView.transform.SetParent(_houseRoot, false);
            _houseMap = GameMap.BuildRoom(house);
            _houseView.Build(_houseMap, _houseRoot, HalfH);
            World = _houseView;
            World.PlaceHero(new Vector2(9.5f, 12.5f));
            _inHouse = true;
            MakeHud();
            ShowZoneBanner(Strings.Get(_houseMap.InteriorNameKey));
            FollowHero();
            RefreshHud();
        }

        /// <summary>The pause card, the journal hub and one of its pages, with a run behind them
        /// that actually has something to show: levels, gear, quests in three different states
        /// and a bestiary with a few names filled in.</summary>
        public void EditorJournal(int page)
        {
            CloseDialog();
            State.NewRun();
            State.Chapter = 2;
            State.Gold = 41;
            State.Xp = 96;
            State.ChestsOpened = 5;
            State.Defeats = 4;
            State.Befriended = 2;
            State.MoonShards = 3;
            State.AddBag("item.morsel");
            State.AddBag("item.honey", 2);
            State.AddBag("item.knife");
            State.AddBag("item.cloak");
            State.AddBag("item.charm.bell");
            State.AddBag("item.axe");
            State.AddBag("item.mushroom", 3);
            State.Worn[0] = "item.knife";
            State.Worn[1] = "item.cloak";
            foreach (var m in BattleData.Bestiary)
                if (m.Chapter <= 2) State.MarkSeen(m.Name);
            Quests.Accept(Quests.Find("mq.2"));
            Quests.SetStep("mq.2", 1);
            Quests.Accept(Quests.Find("sq.mushroom"));
            Quests.SetStep("sq.mushroom", 1);
            Quests.Accept(Quests.Find("sq.kettles"));
            Quests.SetStep("sq.kettles", 1);
            Quests.SetStep("sq.chicken", 3);
            if (World != null && World.Ready) World.SetTextVisible(false);
            Menus.EditorJournal(page);
            Menus.SetAnchor(Cam.transform.localPosition);
        }

        /// <summary>The first-boot onboarding card, middle page so the dots show progress.</summary>
        public void EditorOnboard()
        {
            SetCam(0f, 0f);
            _titleRoot.gameObject.SetActive(false);
            World.gameObject.SetActive(false);
            Menus.Hide();
            Menus.ShowOnboard();
            Menus.SetAnchor(Cam.transform.localPosition);
        }

        /// <summary>Marn's shop card with a purse worth spending: stock rows plus gold in the sub.</summary>
        public void EditorShop()
        {
            CloseDialog();
            State.NewRun();
            State.Gold = 37;
            _titleRoot.gameObject.SetActive(false);
            if (World != null && World.Ready) World.SetTextVisible(false);
            Menus.ShowShop();
            Menus.SetAnchor(Cam.transform.localPosition);
        }

        // ------------------------------------------------------------ self test

        IEnumerator SelfTest()
        {
            System.IO.Directory.CreateDirectory(SelfTestDir);
            // A hidden player window renders as fast as the machine allows, so "400 frames" can be
            // a tenth of a second on one run and seven seconds on the next. Every budget below is
            // wall-clock seconds for that reason, and the frame rate is pinned so the amount of
            // simulated time per iteration stays sane.
            Application.targetFrameRate = 120;
            QualitySettings.vSyncCount = 0;
            Debug.Log("[selftest] start dir=" + SelfTestDir);
            yield return new WaitForSeconds(0.6f);
            Shot("09-splash");
            yield return new WaitForSeconds(2.0f);
            Shot("10-menu");
            Debug.Log("[selftest] front-end phase=" + Phase);

            // the first-boot cards: page one, then tap through to the last page and out
            Menus.ShowOnboard();
            yield return new WaitForSeconds(0.4f);
            Shot("10b-onboard");
            Menus.Tick(0.1f, Vector2.zero, false, 0, true, false);
            yield return new WaitForSeconds(0.3f);
            Shot("10b2-onboard-2");
            Menus.Tick(0.1f, Vector2.zero, false, 0, true, false);
            yield return new WaitForSeconds(0.3f);
            Shot("10c-onboard-3");
            Menus.Tick(0.1f, Vector2.zero, false, 0, true, false);   // page 4: GEAR UP
            yield return new WaitForSeconds(0.3f);
            Shot("10c3-onboard-4");
            Menus.Tick(0.1f, Vector2.zero, false, 0, true, false);   // BEGIN -> title
            yield return new WaitForSeconds(0.5f);
            Shot("10c2-menu");    // on a fresh boot shot 10 lands on onboarding, so re-take it here
            Debug.Log("[selftest] after onboard phase=" + Phase);

            // the chapter card only plays inside the night-2/night-3 dissolve - too fast
            // to catch live, so the editor hook stands it up on demand instead
            Menus.ShowChapterCard(2);
            yield return new WaitForSeconds(1.7f);   // night, then place, then the errand line
            Shot("10d-chcard");

            // the about card - the last room of the title screen no pass ever shot
            Menus.ShowCredits();
            yield return new WaitForSeconds(0.7f);
            Shot("10e-about");
            Menus.Tick(0.1f, Vector2.zero, false, 1, false, false);
            Menus.Tick(0.1f, Vector2.zero, false, 1, false, false);
            Menus.Tick(0.1f, Vector2.zero, false, 0, true, false);   // BACK -> title
            yield return new WaitForSeconds(0.3f);

            // the intro reel: every slide sits behind ci.N text and one of three backdrop
            // arts, none of which any shot has ever stood up. Walk the reel the way a player
            // taps it - finish-line then next-slide - through the plain, forest and dungeon
            // arts, then Hide drops the card without firing OnIntroDone (BeginRun still
            // opens the run below). Ten slides: 0-3 plain, 4-6 forest, 7-9 dungeon.
            Menus.ShowCinema(0);
            yield return new WaitForSeconds(1.0f);
            Shot("10f-cinema-1");
            for (int s = 0; s < 8; s++) { Menus.Tick(0.05f, Vector2.zero, false, 0, true, false); yield return new WaitForSeconds(0.25f); }
            Shot("10f-cinema-5");
            for (int s = 0; s < 10; s++) { Menus.Tick(0.05f, Vector2.zero, false, 0, true, false); yield return new WaitForSeconds(0.25f); }
            Shot("10f-cinema-9");
            Menus.Hide();
            yield return new WaitForSeconds(0.2f);

            BeginRun();
            yield return new WaitForSeconds(1.2f);
            Shot("11-village");
            Debug.Log("[selftest] hero=" + World.HeroPos + " mons=" + World.Monsters.Count
                + " respawns=" + World.PendingRespawns);

            // stand beside Mira a moment: passing a villager should bubble a bark
            World.PlaceHero(new Vector2(27.5f, 8.9f));
            yield return new WaitForSeconds(1.4f);
            Shot("11d-bark");

            // the dialog frame - portrait plate, name tag, typewriter - is the one
            // interactive surface every earlier pass left unphotographed
            EditorTalk();
            yield return new WaitForSeconds(0.9f);
            Shot("11b-dialog");
            Debug.Log("[selftest] dialog open=" + _dlgOpen);
            CloseDialog();
            yield return new WaitForSeconds(0.2f);

            // an interior: the one space no pass had ever photographed. Mira's house
            // (0) is the shrine room - rug, shelf, statue, hearth fire, lamp by the door
            EditorInterior(0);
            yield return new WaitForSeconds(0.9f);
            Shot("11c-interior");
            Debug.Log("[selftest] interior inHouse=" + _inHouse + " hero=" + World.HeroPos);
            // every room layout once, so the audit sees all six furniture sets
            for (int h = 1; h < 6; h++)
            {
                EditorInterior(h);
                yield return new WaitForSeconds(0.7f);
                Shot("11c" + h + "-interior");
            }
            // step back into the street before anything else: the hunt for a monster
            // steered inside the last room once, and rooms have no monsters to find
            if (_inHouse) LeaveHouse();
            yield return new WaitForSeconds(0.6f);

            // the menu pages fake a rich run so their shots have something to show -
            // EditorJournal's NewRun() alone would poison every later leg (it leaves
            // chapter 2, chestsOpened 5, quests re-seeded). Keep the real run: capture
            // now, apply after the last page, resync the world to it.
            var stash = State.Capture(World.HeroPos.x, World.HeroPos.y, _bossDown);

            // Marn's stall: open the shop card for real, buy one thing, leave
            State.Gold = 40;
            OpenShop();
            yield return new WaitForSecondsRealtime(0.5f);
            Shot("21-shop");
            Debug.Log("[selftest] shop rows=" + Menus.ActiveRowCount);
            Menus.Tick(0.1f, Vector2.zero, false, 0, true, false);   // buy first ware
            yield return new WaitForSecondsRealtime(0.2f);
            // the SELL card is a different layout (bag rows, its own footer hint, half prices):
            // step down to the SELL row - it sits second from last, before BACK - and open it
            for (int s = 0; s < Menus.ActiveRowCount - 2; s++) Menus.Tick(0.05f, Vector2.zero, false, 1, false, false);
            Menus.Tick(0.1f, Vector2.zero, false, 0, true, false);
            yield return new WaitForSecondsRealtime(0.4f);
            Shot("21b-shop-sell");
            ClosePause();
            yield return new WaitForSeconds(0.3f);

            // open the pause card and its settings page for real. The row layout used to be
            // wired to one shared list, so these two cards drew an empty frame on a device
            // while every singe-player shortcut test still passed -- capture them here.
            OpenPause();
            // realtime waits: the pause card now actually freezes the clock, so a scaled
            // WaitForSeconds here would hang forever
            yield return new WaitForSecondsRealtime(0.6f);
            Shot("19-pause");
            Debug.Log("[selftest] pause rows=" + Menus.ActiveRowCount);
            Menus.ShowSettings(true);
            yield return new WaitForSecondsRealtime(0.5f);
            Shot("20-settings");
            Debug.Log("[selftest] settings rows=" + Menus.ActiveRowCount);

            // the journal the pause card opens: hub, then a real page. Same reason as above -
            // the rows are built per page and a shared list made every page after the first draw
            // an empty frame.
            Menus.EditorJournal(-1);
            yield return new WaitForSecondsRealtime(0.5f);
            Shot("27-journal");
            Debug.Log("[selftest] journal rows=" + Menus.ActiveRowCount);
            Menus.EditorJournal(4);
            yield return new WaitForSecondsRealtime(0.5f);
            Shot("28-quests");
            Debug.Log("[selftest] quest rows=" + Menus.ActiveRowCount);
            Menus.EditorJournal(5);
            yield return new WaitForSecondsRealtime(0.5f);
            Shot("28b-map");
            Debug.Log("[selftest] map rows=" + Menus.ActiveRowCount);
            // the four pages nobody has photographed yet: character stats, the bag,
            // worn gear, and the bestiary with its FRIEND column
            Menus.EditorJournal(0);
            yield return new WaitForSecondsRealtime(0.5f);
            Shot("28c-character");
            // page two and three of the stat sheet: the icon row that only shows on
            // later pages has drifted before - photograph it now
            for (int i = 0; i < 6; i++) Menus.Tick(0.1f, Vector2.zero, false, 1, false, false);
            Menus.Tick(0.1f, Vector2.zero, false, 0, true, false);
            yield return new WaitForSecondsRealtime(0.4f);
            Shot("28c2-character-p2");
            for (int i = 0; i < 6; i++) Menus.Tick(0.1f, Vector2.zero, false, 1, false, false);
            Menus.Tick(0.1f, Vector2.zero, false, 0, true, false);
            yield return new WaitForSecondsRealtime(0.4f);
            Shot("28c3-character-p3");
            Menus.EditorJournal(1);
            yield return new WaitForSecondsRealtime(0.5f);
            Shot("28d-items");
            Menus.EditorJournal(2);
            yield return new WaitForSecondsRealtime(0.5f);
            Shot("28e-equipment");
            Menus.EditorJournal(3);
            yield return new WaitForSecondsRealtime(0.5f);
            Shot("28f-bestiary");
            // page two of the beast book: late-night species and the FRIEND column
            // live there, where no shot has ever reached them
            for (int i = 0; i < 6; i++) Menus.Tick(0.1f, Vector2.zero, false, 1, false, false);
            Menus.Tick(0.1f, Vector2.zero, false, 0, true, false);
            yield return new WaitForSecondsRealtime(0.4f);
            Shot("28f2-bestiary-p2");
            // the medal case: mostly "?????" this early - the trophy wall needs checking
            // for both its earned rows and its locked ones
            Menus.EditorJournal(6);
            yield return new WaitForSecondsRealtime(0.5f);
            Shot("28g-medals");
            // the back pages of the case: the late feats live there - film them too
            for (int i = 0; i < 6; i++) Menus.Tick(0.1f, Vector2.zero, false, 1, false, false);
            Menus.Tick(0.1f, Vector2.zero, false, 0, true, false);
            yield return new WaitForSecondsRealtime(0.4f);
            Shot("28g2-medals-p2");
            for (int i = 0; i < 6; i++) Menus.Tick(0.1f, Vector2.zero, false, 1, false, false);
            Menus.Tick(0.1f, Vector2.zero, false, 0, true, false);
            yield return new WaitForSecondsRealtime(0.4f);
            Shot("28g3-medals-p3");
            ClosePause();

            // the real run comes back before a single world leg touches it - the fake
            // journal state was only ever meant for the frames above
            State.Apply(stash);
            if (World != null && World.Ready) World.ResetForChapter();
            RefreshHud();

            // the company forms before the hunt, the same way a player forms it: find the
            // wanderer, take the talk, and let the talk's close run the join. After this the
            // wandering selves are gone, two walkers trail the hero, and every fight ahead
            // (including the three bosses, which were tuned for three) has the full party.
            var seaNpc = World.FindNpc("npc.sea");
            if (seaNpc != null)
            {
                TalkTo(seaNpc.Npc);
                yield return new WaitForSeconds(0.8f);
                Shot("12a-sea-talk");
                CloseDialog();
                yield return new WaitForSeconds(0.4f);
            }
            var mossNpc = World.FindNpc("npc.moss");
            if (mossNpc != null)
            {
                TalkTo(mossNpc.Npc);
                yield return new WaitForSeconds(0.4f);
                CloseDialog();
                yield return new WaitForSeconds(0.4f);
            }
            Debug.Log("[selftest] company=" + string.Join(",", State.Joined));
            Shot("12a-party");

            // the shard path itself, which the hunt legs never touch: walk to the nearest
            // unopened field chest and open it through TryInteract, the same call a tap
            // lands on. The first three chests must grant a moon shard.
            var shut = World.ShutChestPos();
            if (shut.Count > 0)
            {
                var chestAt = shut[0]; float cd = float.MaxValue;
                foreach (var c in shut)
                {
                    float d = Vector2.Distance(World.HeroPos, c);
                    if (d < cd) { cd = d; chestAt = c; }
                }
                int cguard = 0, cstuck = 0;
                var lastC = World.HeroPos;
                while (Vector2.Distance(World.HeroPos, chestAt) > 1.0f && cguard++ < 900)
                {
                    var cdir = (chestAt - World.HeroPos).normalized;
                    if (Vector2.Distance(World.HeroPos, lastC) < 0.02f)
                    {
                        if (++cstuck > 25) { cdir = new Vector2(Random.value < 0.5f ? -1f : 1f, 0.6f); cstuck = 0; }
                    }
                    else cstuck = 0;
                    lastC = World.HeroPos;
                    World.DriveHero(cdir, Time.deltaTime);
                    TickWorldForTest();
                    yield return null;
                }
                int shardsBeforeChest = State.MoonShards;
                // TryInteract would work too, but a villager drifting beside the chest
                // wins the interact first (NPCs are checked before chests) - the shard
                // path is what this leg exists to prove, so call the chest directly
                int cIdx = World.NearestChest(World.HeroPos);
                if (cIdx >= 0)
                {
                    World.OpenChest(cIdx);
                    Sfx.Play(State.MoonShards > shardsBeforeChest ? "shard" : "chest");
                    ShowZoneBanner(World.LastLootText);
                    CheckMains();
                    RefreshHud();
                }
                yield return new WaitForSeconds(0.7f);
                Debug.Log("[selftest] chest shards " + shardsBeforeChest + "->" + State.MoonShards
                    + " left=" + World.ChestsLeft + " opened=" + State.ChestsOpened
                    + " interior=" + (World.Map != null && World.Map.Interior));
                Shot("12b-chest-shard");
            }

            // hunt the nearest wild monster so an encounter is guaranteed, not lucky. Steering is
            // diagonal: the old axis-only version (straight east/west, then straight north) wedged
            // against a barrel or a house corner and stayed there, because the blocked axis was
            // the only one it ever pushed on.
            int guard = 0;
            var lastHuntPos = World.HeroPos;
            int huntStuck = 0;
            float huntStart = Time.time;
            while (Phase == St.Explore && Time.time - huntStart < 50f)
            {
                guard++;
                var target = World.NearestMonsterPos();
                if (target == null) break;                       // every monster already fought
                var to = target.Value - World.HeroPos;
                var dir = to.sqrMagnitude < 0.01f ? Vector2.up : to.normalized;
                if (Vector2.Distance(World.HeroPos, lastHuntPos) < 0.02f) huntStuck++;
                else huntStuck = 0;
                lastHuntPos = World.HeroPos;
                if (huntStuck > 25)
                {
                    dir = new Vector2(Random.value < 0.5f ? -1f : 1f, 0.6f);
                    huntStuck = 0;
                }
                World.DriveHero(dir, Time.deltaTime);
                TickWorldForTest();
                yield return null;
            }
            Debug.Log("[selftest] explore ended at y=" + World.HeroPos.y + " phase=" + Phase + " guard=" + guard);
            Shot("12-explore-far");

            int battles = 0;
            while (Phase == St.Battle && battles++ < 6)
            {
                // jump the moonflow the moment the encounter begins, before the first
                // hero turn draws its labels - a menu labelled STRIKE stays labelled
                // STRIKE if the flow lands after it opens
                if (battles == 1) Director.DebugFlow = 5;
                yield return new WaitForSeconds(1.6f);
                Shot("13-battle-" + battles);
                // run one encounter on auto-battle so the AUTO chip path is exercised end to end
                if (battles == 2 && !Director.Auto)
                {
                    Director.ToggleAuto();
                    Shot("13b-battle-auto");
                    Debug.Log("[selftest] auto battle on");
                }
                int t = 0;
                float turnStart = Time.time;
                while (Phase == St.Battle && Time.time - turnStart < 45f)
                {
                    t++;
                    if (Director.AwaitingInput)
                    {
                        // a hero turn at flow five carries the pale MOONSTRIKE labels - the
                        // menu grid itself must be up: AwaitingInput still reads true for a
                        // frame after a dazed monster's skipped turn hides the cells
                        if (battles == 1 && t >= 2 && !_shotMoon && BattleViewRef.MenuOn)
                        {
                            // MenuOn still flickers true for a frame when a dazed beast's
                            // skipped turn interrupts the open menu - hold a beat so the
                            // cells either settle in or the flag falls away
                            yield return new WaitForSeconds(0.3f);
                            if (BattleViewRef != null && BattleViewRef.MenuOn
                                && !BattleViewRef.MessageRevealing)
                            {
                                _shotMoon = true;
                                Shot("13c-battle-moon");
                            }
                        }
                        // one turn in three goes through the real tap path, so the hit test
                        // that a finger uses is exercised instead of only the shortcut. The
                        // point is round-tripped through the screen mapping a finger goes
                        // through, so a bad stage mapping now fails the test too.
                        if (t % 3 == 0 && BattleViewRef.Menu.Count > 0)
                        {
                            var cell = BattleViewRef.Menu[t % BattleViewRef.Menu.Count];
                            var stage = ScreenToStage(StageToScreen(cell.Hit.center));
                            int hit = BattleViewRef.HitMenu(stage);
                            Director.TapAt(stage);
                            Debug.Log(hit >= 0
                                ? "[selftest] TapAt menu cell (screen path) -> " + cell.Label
                                : "[selftest] MISSED menu cell -> " + cell.Label);
                        }
                        else
                        {
                            Director.SelectCell(t % 3);
                            Director.Confirm();
                        }
                        yield return new WaitForSeconds(0.7f);
                    }
                    else if (BattleViewRef.OverlayButtonCount > 0)
                    {
                        Shot("14-card");
                        var cr = BattleViewRef.CardButtonRect(0);
                        if (cr.width > 0f) { Director.TapAt(cr.center); Debug.Log("[selftest] TapAt card button"); }
                        else BattleViewRef.CardButtonAt(0)?.Invoke();
                        yield return new WaitForSeconds(1.0f);
                        break;
                    }
                    else TickWorldForTest();
                    if (t == 1499 || t == 2999)
                        Debug.Log("[selftest] stall phase=" + Phase + " dir=" + Director.DebugPhase
                            + " qi=" + Director.DebugQi + "/" + Director.DebugQueue + " round=" + Director.DebugRound
                            + " menuOn=" + BattleViewRef.MenuOn);
                    yield return null;
                }
            }
            Debug.Log("[selftest] after battles phase=" + Phase);

            // the one screen no win ever shows: losing. Stage the night's gatekeeper against
            // a party at one health apiece, let AUTO play it honestly, photograph the fall
            // card, then take the long walk home - the whole defeat path was a blind spot
            if (Phase == St.Explore)
            {
                EditorBattle();
                Director.StartBattle(BattleData.BossFight(State.Chapter));
                // venom ticks before its carrier acts, so AUTO can never mend these:
                // one queue pass and the whole party drops - the loss is certain, not hoped for
                foreach (var p in BattleViewRef.Party) { p.Hp = 1; p.Poison = 99; }
                if (!Director.Auto) Director.ToggleAuto();
                int loseGuard = 0;
                while (Phase == St.Battle && loseGuard++ < 4000)
                {
                    if (BattleViewRef.OverlayButtonCount > 0)
                    {
                        yield return new WaitForSeconds(0.9f);
                        Shot("12b-losscard");
                        BattleViewRef.CardButtonAt(BattleViewRef.OverlayButtonCount - 1)?.Invoke();
                        break;
                    }
                    TickWorldForTest();
                    yield return null;
                }
                Debug.Log("[selftest] defeat ended phase=" + Phase + " hero=" + World.HeroPos);
                yield return new WaitForSeconds(1.4f);
                Shot("12c-retreat");
                // however the staged fight ended, the night must be Explore before the boss
                // walk - a card still standing taps its way home instead of skipping the spine
                int sweep = 0;
                while (Phase == St.Battle && sweep++ < 600)
                {
                    if (BattleViewRef.OverlayButtonCount > 0)
                        // CONTINUE on a win card, FLEE HOME on a loss: both land on Explore
                        BattleViewRef.CardButtonAt(BattleViewRef.OverlayButtonCount - 1)?.Invoke();
                    else TickWorldForTest();
                    yield return null;
                }
            }

            // keep exploring to the boss if we are still alive
            // one loop per night: walk the map to its gatekeeper, let AUTO win the fight,
            // tap the fall card and ride the chapter dissolve into the next night. The whole
            // spine of the game is exercised every run - not just night one.
            _testNoDoors = true;   // a house door on the way north would swallow the walk whole
            if (_inHouse) LeaveHouse();   // step out before steering north
            yield return new WaitForSeconds(0.3f);
            for (int night = 1; night <= 3 && Phase == St.Explore; night++)
            {
                guard = 0;
                var lastPos = World.HeroPos;
                int stuck = 0;
                float walkStart = Time.time;
                while ((Phase == St.Explore || Phase == St.Battle) && Time.time - walkStart < 75f)
                {
                    guard++;
                    if (Phase == St.Battle)
                    {
                        // a stray wild fight on the road north: AUTO it, tap its card, keep walking
                        if (!Director.Auto) Director.ToggleAuto();
                        // the night-2 and night-3 fights are the only chances to photograph
                        // those arenas and their packs - the n1 hunt only ever sees night one
                        if (!_shotFight.Contains(night))
                        {
                            _shotFight.Add(night);
                            yield return new WaitForSeconds(1.2f);
                            Shot("13b-fight-n" + night);
                        }
                        if (BattleViewRef.OverlayButtonCount > 0)
                            BattleViewRef.CardButtonAt(0)?.Invoke();
                        else TickWorldForTest();
                        yield return null;
                        continue;
                    }
                    if (guard % 600 == 0)
                        Debug.Log("[selftest] walking north night " + night + " guard=" + guard + " t=" + (Time.time - walkStart).ToString("0.0")
                            + "s hero=" + World.HeroPos + " phase=" + Phase + " near=" + World.NearBoss);
                    if (World.NearBoss) break;
                    var toB = World.Map.BossPos - World.HeroPos;
                    // diagonal again, so a blocked axis still leaves the other one moving
                    var dirB = toB.sqrMagnitude < 0.01f ? Vector2.up : toB.normalized;
                    // a straight line to the boss can wedge on a tree: sidestep when stuck
                    if (Vector2.Distance(World.HeroPos, lastPos) < 0.01f) stuck++;
                    else stuck = 0;
                    lastPos = World.HeroPos;
                    if (stuck > 25)
                    {
                        dirB = new Vector2(Random.value < 0.5f ? -1f : 1f, 0.15f);
                        stuck = 0;
                    }
                    World.DriveHero(dirB, Time.deltaTime);
                    TickWorldForTest();
                    yield return null;
                }
                if (!World.NearBoss)
                {
                    // pathing can stall on trees; step next to the boss so the fight is still tested
                    World.PlaceHero(new Vector2(World.Map.BossPos.x + 0.5f, World.Map.BossPos.y - 1.5f));
                    yield return null;
                }
                Shot("15-bosszone-n" + night);
                Debug.Log("[selftest] boss zone night " + night + " at y=" + World.HeroPos.y + " nearBoss=" + World.NearBoss);

                if (World.NearBoss && !_bossDown)
                {
                    StartBattle(BattleData.BossFight(State.Chapter));
                    float bossStart = Time.time;
                    // let the real AUTO battle play the finale: it mends, spends a morsel when
                    // the party is hurt and aims for weak seams - the same hand a player has,
                    // and a better solver than raw ATTACK spam that can wipe and re-fight.
                    if (!Director.Auto) Director.ToggleAuto();
                    int lastRoundLogged = -1;
                    bool bossShot = false;
                    while (Phase == St.Battle && Time.time - bossStart < 170f)
                    {
                        if (!bossShot && Time.time - bossStart > 2.5f)
                        {
                            bossShot = true;
                            Shot("16b-bossfight-n" + night);
                        }
                        if (Director.DebugRound != lastRoundLogged)
                        {
                            lastRoundLogged = Director.DebugRound;
                            Debug.Log("[selftest] boss round " + lastRoundLogged + " at " + Mathf.RoundToInt(Time.time - bossStart) + "s night " + night);
                        }
                        if (BattleViewRef.OverlayButtonCount > 0)
                        {
                            Shot("16-bosscard-n" + night);
                            var cr = BattleViewRef.CardButtonRect(0);
                            if (cr.width > 0f) Director.TapAt(cr.center);
                            else BattleViewRef.CardButtonAt(0)?.Invoke();
                            yield return new WaitForSeconds(1.2f);
                            break;
                        }
                        else TickWorldForTest();
                        yield return null;
                    }
                }

                // the victory card rolls into the next night through a black dissolve; the old
                // fixed 0.8 s wait landed inside it and the frame came out solid black
                for (int fw = 0; fw < 600 && FadeAlpha > 0.04f; fw++) yield return null;
                yield return new WaitForSeconds(0.5f);
                Shot("17-after-boss-n" + night);
                Debug.Log("[selftest] after boss night " + night + " phase=" + Phase
                    + " chapter=" + State.Chapter + " shards=" + State.MoonShards);

                if (night < 3)
                {
                    // the dissolve schedules the next chapter - wait for the world to rebuild
                    float waitCh = Time.time;
                    while (State.Chapter != night + 1 && Time.time - waitCh < 8f) yield return null;
                    // and a beat for the new map's first frames
                    for (int fw = 0; fw < 30; fw++) yield return null;
                }
            }

            if (Phase == St.Explore)
            {
                // walk back to the cristal and end the game
                World.DriveHero(Vector2.down, 0.016f);
                int g3 = 0;
                _stuck = 0; _prevHero = World.HeroPos;
                float homeStart = Time.time;
                while ((Phase == St.Explore || Phase == St.Battle) && Time.time - homeStart < 45f)
                {
                    g3++;
                    if (Phase == St.Battle)
                    {
                        // a wild touch on the way home: AUTO it, tap its card, keep walking
                        if (!Director.Auto) Director.ToggleAuto();
                        if (BattleViewRef.OverlayButtonCount > 0)
                            BattleViewRef.CardButtonAt(0)?.Invoke();
                        else TickWorldForTest();
                        yield return null;
                        continue;
                    }
                    if (g3 % 600 == 0)
                        Debug.Log("[selftest] walking to cristal g3=" + g3 + " hero=" + World.HeroPos
                            + " shards=" + State.MoonShards);
                    var target = World.Map.CristalPos;
                    var delta = target - World.HeroPos;
                    World.DriveHero(delta, Time.deltaTime);
                    // a wall between the road and the ridge used to eat the whole 45s:
                    // the steer pushed into it forever. When the hero stops moving,
                    // slide along the wall - alternating sides so a corner can't trap it
                    if (Vector2.Distance(World.HeroPos, _prevHero) < 0.008f) _stuck++;
                    else _stuck = 0;
                    _prevHero = World.HeroPos;
                    if (_stuck > 12)
                    {
                        var perp = new Vector2(-delta.y, delta.x).normalized;
                        World.DriveHero(perp * ((_stuck / 40) % 2 == 0 ? 1f : -1f), Time.deltaTime * 1.4f);
                    }
                    TickWorldForTest();
                    if (Vector2.Distance(World.HeroPos, target) < 2f) break;
                    yield return null;
                }
                // a companion for the horizon line: without this the ending shot only
                // ever sees the lone telling
                if (State.Friends.Count == 0) { State.Friends.Add("slime"); State.Friends.Add("moon.wisp"); }
                TriggerEnding();
                _testNoDoors = false;
                yield return new WaitForSeconds(1.0f);
                Shot("18-ending");            // mid-rise: moon still low, words not up yet
                yield return new WaitForSeconds(3.4f);
                Shot("18b-ending-risen");     // the moon is up and the epilogue is on
                Debug.Log("[selftest] ending shown");

                // the night retells itself: out of the ending, back to the title, and
                // straight into CONTINUE - the retold world must read as night 1
                // again: caches shut, quests unwritten, beasts grown bolder (NG+)
                DoTransition(() => ShowTitle());
                yield return new WaitForSeconds(1.4f);
                ContinueRun();
                yield return new WaitForSeconds(2.2f);
                Debug.Log("[selftest] ngp=" + State.NgPlus + " ch=" + State.Chapter
                    + " shards=" + State.MoonShards + " chests=" + State.ChestsDone.Count
                    + " quests=" + Quests.DoneCount + " joined=" + State.Joined.Count
                    + " phase=" + Phase);
                Shot("19-ngplus");

                // the case by now: three keepers felled, the ending earned, the tale
                // retold - the medal page should read won icons, not '?????'. This is
                // the only shot in the pass that sees a medal as it is meant to look.
                Menus.EditorJournal(6);
                yield return new WaitForSecondsRealtime(0.5f);
                Shot("29-medals-earned");
                Debug.Log("[selftest] medals earned=" + Medals.Count);
                ClosePause();
                yield return new WaitForSecondsRealtime(0.3f);
            }

            Debug.Log("[selftest] done");
            yield return new WaitForSeconds(0.3f);
            Application.Quit();
        }

        void TickWorldForTest()
        {
            // drive the same logic Update() runs, without input.
            // Only in Explore: the hero freezes in contact range during a battle, so an
            // unguarded re-trigger rebuilt the encounter mid-fight and crashed the turn.
            if (Phase != St.Explore) return;
            if (World == null || !World.Ready) return;
            _encounterCooldown -= Time.deltaTime;
            var touched = World.TouchedMonster();
            if (touched != null && _encounterCooldown <= 0f)
            {
                var spec = touched.Spec;
                World.RemoveMonster(touched);
                StartBattle(new[] { spec });
            }
        }

        readonly System.Collections.Generic.Dictionary<string, int> _shotCounts =
            new System.Collections.Generic.Dictionary<string, int>();

        void Shot(string name)
        {
            if (Target == null) return;
            // A tag can be used more than once in a run - two battles both end on a result card -
            // and two moments written under one tag are read back as ONE frame by the layout
            // audit, which then reports every plate in it twice and invents clashes between a
            // label and itself. The second take gets its own suffix.
            int take = _shotCounts.TryGetValue(name, out var c) ? c + 1 : 1;
            _shotCounts[name] = take;
            if (take > 1) name = name + "-" + take;
            // the frames the phone would show are measured by the same pass the editor build uses,
            // so a runtime-only overlap (a notice arriving during a conversation, a plate landing
            // on a villager) reaches the log instead of the screenshot
            LayoutAudit.Report("R" + name, HalfH, Cam != null ? Cam.transform.localPosition : Vector3.zero);
            var prev = RenderTexture.active;
            RenderTexture.active = Target;
            var tex = new Texture2D(Target.width, Target.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, Target.width, Target.height), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            System.IO.File.WriteAllBytes(System.IO.Path.Combine(SelfTestDir, name + ".png"), tex.EncodeToPNG());
            Debug.Log("[selftest] shot " + name);
        }
    }
}
