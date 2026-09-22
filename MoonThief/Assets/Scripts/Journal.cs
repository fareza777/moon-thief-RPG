using System.Collections.Generic;
using UnityEngine;

namespace MoonThief
{
    // ------------------------------------------------------------------ items

    public enum ItemKind { Food, Blade, Cloth, Charm, Key }

    public struct ItemDef
    {
        public string Key;      // string key of the name
        public ItemKind Kind;
        public int Power;       // food: hp restored, blade: +atk, cloth: +hp, charm: mixed
        public int Price;
        public bool Known;      // the journal only lists things the run has found
    }

    /// <summary>Everything that can sit in the bag. One table, so the journal, the loot tables and
    /// the battle bonuses all read the same numbers.</summary>
    public static class Items
    {
        public static readonly ItemDef[] All =
        {
            // food - heals the whole party by Power
            new ItemDef{ Key="item.morsel", Kind=ItemKind.Food, Power=12, Price=4 },
            new ItemDef{ Key="item.berry",  Kind=ItemKind.Food, Power=6,  Price=2 },
            new ItemDef{ Key="item.honey",  Kind=ItemKind.Food, Power=14, Price=6 },
            new ItemDef{ Key="item.soup",   Kind=ItemKind.Food, Power=9,  Price=5 },
            new ItemDef{ Key="item.tea",    Kind=ItemKind.Food, Power=20, Price=9 },
            // blades - +attack to the whole party
            new ItemDef{ Key="item.spoon",  Kind=ItemKind.Blade, Power=2,  Price=6 },
            new ItemDef{ Key="item.knife",  Kind=ItemKind.Blade, Power=4,  Price=14 },
            new ItemDef{ Key="item.sickle", Kind=ItemKind.Blade, Power=7,  Price=28 },
            new ItemDef{ Key="item.blade",  Kind=ItemKind.Blade, Power=11, Price=60 },
            // cloth - +max hp to the whole party
            new ItemDef{ Key="item.cloak",  Kind=ItemKind.Cloth, Power=4,  Price=8 },
            new ItemDef{ Key="item.vest",   Kind=ItemKind.Cloth, Power=8,  Price=20 },
            new ItemDef{ Key="item.mail",   Kind=ItemKind.Cloth, Power=14, Price=48 },
            // charms - a small mixed blessing
            new ItemDef{ Key="item.charm.bell",   Kind=ItemKind.Charm, Power=2, Price=16 },
            new ItemDef{ Key="item.charm.leaf",   Kind=ItemKind.Charm, Power=6, Price=22 },
            new ItemDef{ Key="item.charm.moon",   Kind=ItemKind.Charm, Power=9, Price=40 },
            new ItemDef{ Key="item.charm.thread", Kind=ItemKind.Charm, Power=4, Price=18 },
            // things a quest wants, never sold
            new ItemDef{ Key="item.axe",      Kind=ItemKind.Key, Price=0 },
            new ItemDef{ Key="item.doll",     Kind=ItemKind.Key, Price=0 },
            new ItemDef{ Key="item.note",     Kind=ItemKind.Key, Price=0 },
            new ItemDef{ Key="item.mushroom", Kind=ItemKind.Key, Price=0 },
            new ItemDef{ Key="item.keepsake", Kind=ItemKind.Key, Price=0 },
        };

        public static ItemDef Get(string key)
        {
            foreach (var d in All) if (d.Key == key) return d;
            return new ItemDef { Key = key, Kind = ItemKind.Key };
        }

        public static bool IsEquip(ItemKind k) => k == ItemKind.Blade || k == ItemKind.Cloth || k == ItemKind.Charm;

        public static int SlotOf(ItemKind k) => k == ItemKind.Blade ? 0 : k == ItemKind.Cloth ? 1 : 2;

        public static string SlotKey(ItemKind k) =>
            k == ItemKind.Blade ? "jr.slot.blade" : k == ItemKind.Cloth ? "jr.slot.cloth" : "jr.slot.charm";

        /// <summary>Short effect line for a row, e.g. "+4 ATK". No translation needed: the
        /// numbers and the three stat words are the whole label.</summary>
        public static string Effect(ItemDef d)
        {
            switch (d.Kind)
            {
                case ItemKind.Food: return "+" + d.Power + " " + Strings.Get("jr.hp");
                case ItemKind.Blade: return "+" + d.Power + " " + Strings.Get("jr.atk");
                case ItemKind.Cloth: return "+" + d.Power + " " + Strings.Get("jr.maxhp");
                case ItemKind.Charm: return "+" + d.Power + " " + Strings.Get("jr.all");
                default: return Strings.Get("jr.key");
            }
        }

        /// <summary>Where a wild chest's loot comes from: better gear the further north you are.</summary>
        public static string RollLoot(int chapter, System.Random rng)
        {
            var pool = new List<string>();
            pool.Add("item.berry");
            pool.Add("item.morsel");
            if (chapter >= 1) { pool.Add("item.honey"); pool.Add("item.cloak"); pool.Add("item.spoon"); }
            if (chapter >= 2) { pool.Add("item.knife"); pool.Add("item.vest"); pool.Add("item.soup"); pool.Add("item.charm.thread"); }
            if (chapter >= 3) { pool.Add("item.sickle"); pool.Add("item.mail"); pool.Add("item.charm.leaf"); pool.Add("item.tea"); }
            return pool[rng.Next(pool.Count)];
        }
    }

    // ------------------------------------------------------------------ quests

    public enum QuestKind { Talk, Chests, Defeats, Item, Pay, Zones }

    public class QuestDef
    {
        public string Id;
        public string TitleKey;     // quest log title
        public string StepKey;      // the objective line, with {0} for the remaining count
        public string OfferKey;     // the giver's line when the quest is offered
        public string DoneKey;      // the giver's line when it is handed in
        public string Giver;        // name key of the NPC who owns it ("" = a world event)
        public bool Main;
        public int Chapter;
        public QuestKind Kind;
        public int Need;
        public int Reward;          // gold
        public string Gift;         // item key handed over on completion ("" = none)
    }

    /// <summary>A one-shot thing that happens on the road: a position, a line, and what it does.
    /// The tones are deliberately mixed - some are jokes, some are odd, one or two are sad.</summary>
    public class WorldEvent
    {
        public string Id;
        public string TextKey;
        public Vector2 Pos;
        public float Radius = 2.2f;
        public int Chapter;         // earliest chapter it can happen in
        public int Gold;
        public string Gift;
        public int Heal;
        public string Fight;        // monster name key to fight ("" = none)
        public bool Tragic;
    }

    /// <summary>The quest book: every quest, its state (0 unseen, 1 active, 2 ready, 3 done) and
    /// the world events that hand them out. State lives here rather than in each NPC so the log,
    /// the dialog and the events all agree.</summary>
    public static class Quests
    {
        public static readonly QuestDef[] All =
        {
            // ---- the main line: three nights, four shards, one thief ----
            new QuestDef{ Id="mq.1", Main=true, Chapter=1, Kind=QuestKind.Talk, Need=1, Giver="npc.elder",
                TitleKey="q.mq1.title", StepKey="q.mq1.step", OfferKey="q.mq1.offer", DoneKey="q.mq1.done" },
            new QuestDef{ Id="mq.2", Main=true, Chapter=1, Kind=QuestKind.Chests, Need=4, Giver="npc.elder",
                TitleKey="q.mq2.title", StepKey="q.mq2.step", OfferKey="q.mq2.offer", DoneKey="q.mq2.done" },
            new QuestDef{ Id="mq.3", Main=true, Chapter=3, Kind=QuestKind.Defeats, Need=1, Giver="",
                TitleKey="q.mq3.title", StepKey="q.mq3.step", OfferKey="q.mq3.offer", DoneKey="q.mq3.done" },

            // ---- side quests. funny, odd, and one or two that are simply sad ----
            new QuestDef{ Id="sq.axe", Chapter=1, Kind=QuestKind.Item, Need=1, Giver="npc.house.3", Reward=18, Gift="item.charm.thread",
                TitleKey="q.axe.title", StepKey="q.axe.step", OfferKey="q.axe.offer", DoneKey="q.axe.done" },
            new QuestDef{ Id="sq.mushroom", Chapter=1, Kind=QuestKind.Item, Need=3, Giver="npc.grandma", Reward=14, Gift="item.tea",
                TitleKey="q.mush.title", StepKey="q.mush.step", OfferKey="q.mush.offer", DoneKey="q.mush.done" },
            new QuestDef{ Id="sq.doll", Chapter=1, Kind=QuestKind.Item, Need=1, Giver="npc.kid", Reward=10, Gift="item.honey",
                TitleKey="q.doll.title", StepKey="q.doll.step", OfferKey="q.doll.offer", DoneKey="q.doll.done" },
            new QuestDef{ Id="sq.chicken", Chapter=1, Kind=QuestKind.Talk, Need=1, Giver="npc.house.5", Reward=9, Gift="item.berry",
                TitleKey="q.chicken.title", StepKey="q.chicken.step", OfferKey="q.chicken.offer", DoneKey="q.chicken.done" },
            new QuestDef{ Id="sq.toll", Chapter=2, Kind=QuestKind.Pay, Need=8, Giver="npc.hunter", Reward=0, Gift="item.charm.bell",
                TitleKey="q.toll.title", StepKey="q.toll.step", OfferKey="q.toll.offer", DoneKey="q.toll.done" },
            new QuestDef{ Id="sq.kettles", Chapter=2, Kind=QuestKind.Chests, Need=3, Giver="npc.smith", Reward=24, Gift="item.mail",
                TitleKey="q.kettle.title", StepKey="q.kettle.step", OfferKey="q.kettle.offer", DoneKey="q.kettle.done" },
            new QuestDef{ Id="sq.rhyme", Chapter=2, Kind=QuestKind.Zones, Need=3, Giver="npc.bard", Reward=20, Gift="item.charm.leaf",
                TitleKey="q.rhyme.title", StepKey="q.rhyme.step", OfferKey="q.rhyme.offer", DoneKey="q.rhyme.done" },
            new QuestDef{ Id="sq.nightwatch", Chapter=2, Kind=QuestKind.Defeats, Need=5, Giver="npc.house.4", Reward=30, Gift="item.sickle",
                TitleKey="q.watch.title", StepKey="q.watch.step", OfferKey="q.watch.offer", DoneKey="q.watch.done" },
            new QuestDef{ Id="sq.grave", Chapter=3, Kind=QuestKind.Talk, Need=1, Giver="npc.house.2", Reward=26, Gift="item.keepsake",
                TitleKey="q.grave.title", StepKey="q.grave.step", OfferKey="q.grave.offer", DoneKey="q.grave.done" },
            new QuestDef{ Id="sq.ledger", Chapter=3, Kind=QuestKind.Chests, Need=6, Giver="npc.house.1", Reward=40, Gift="item.blade",
                TitleKey="q.ledger.title", StepKey="q.ledger.step", OfferKey="q.ledger.offer", DoneKey="q.ledger.done" },
        };

        public static readonly WorldEvent[] Events =
        {
            // funny
            new WorldEvent{ Id="ev.hen", Chapter=1, Pos=new Vector2(20.5f, 21.5f), TextKey="ev.hen", Gift="item.berry" },
            new WorldEvent{ Id="ev.toll", Chapter=1, Pos=new Vector2(45.5f, 20.5f), TextKey="ev.toll", Gold=6 },
            new WorldEvent{ Id="ev.laundry", Chapter=2, Pos=new Vector2(12.5f, 34.5f), TextKey="ev.laundry" },
            // odd
            new WorldEvent{ Id="ev.rock", Chapter=1, Pos=new Vector2(37.5f, 40.5f), TextKey="ev.rock", Heal=8 },
            new WorldEvent{ Id="ev.moth", Chapter=2, Pos=new Vector2(48.5f, 44.5f), TextKey="ev.moth", Gift="item.charm.moon" },
            new WorldEvent{ Id="ev.bell", Chapter=3, Pos=new Vector2(24.5f, 62.5f), TextKey="ev.bell", Fight="mon.wisp" },
            // sad
            new WorldEvent{ Id="ev.grave", Chapter=2, Pos=new Vector2(9.5f, 46.5f), TextKey="ev.grave", Tragic=true },
            new WorldEvent{ Id="ev.lantern", Chapter=3, Pos=new Vector2(36.5f, 74.5f), TextKey="ev.lantern", Tragic=true, Gold=15 },
            new WorldEvent{ Id="ev.dog", Chapter=1, Pos=new Vector2(52.5f, 30.5f), TextKey="ev.dog", Tragic=true, Heal=6 },
        };

        // step: 0 not seen, 1 active, 2 ready to hand in, 3 done
        static readonly Dictionary<string, int> Steps = new Dictionary<string, int>();
        static readonly Dictionary<string, int> Base = new Dictionary<string, int>();
        static readonly HashSet<string> Fired = new HashSet<string>();

        public static void Reset()
        {
            Steps.Clear();
            Base.Clear();
            Fired.Clear();
        }

        public static int Step(string id) => Steps.TryGetValue(id, out var v) ? v : 0;

        public static void SetStep(string id, int v) => Steps[id] = v;

        public static bool FiredAlready(string id) => Fired.Contains(id);

        public static void MarkFired(string id) => Fired.Add(id);

        public static QuestDef Find(string id)
        {
            foreach (var q in All) if (q.Id == id) return q;
            return null;
        }

        /// <summary>The counter a quest is measured against.</summary>
        static int Counter(QuestKind k)
        {
            switch (k)
            {
                case QuestKind.Chests: return Game.State.ChestsOpened;
                case QuestKind.Defeats: return Game.State.Defeats;
                case QuestKind.Zones: return Game.State.Zones.Count;
                case QuestKind.Pay: return Game.State.Gold;
                default: return 0;
            }
        }

        public static void Accept(QuestDef q)
        {
            if (q == null || Step(q.Id) != 0) return;
            Steps[q.Id] = 1;
            Base[q.Id] = Counter(q.Kind);
        }

        /// <summary>How much of the quest is done. Key/item quests are all or nothing.</summary>
        public static int Progress(QuestDef q)
        {
            if (q == null) return 0;
            if (q.Kind == QuestKind.Item) return Game.State.BagCount(GoalItem(q));
            if (q.Kind == QuestKind.Talk) return 1;
            int b = Base.TryGetValue(q.Id, out var v) ? v : 0;
            return Mathf.Max(0, Counter(q.Kind) - b);
        }

        /// <summary>Which key item a quest wants. Carried on the quest itself so the offer can
        /// name it without a second table.</summary>
        public static string GoalItem(QuestDef q)
        {
            switch (q.Id)
            {
                case "sq.axe": return "item.axe";
                case "sq.mushroom": return "item.mushroom";
                case "sq.doll": return "item.doll";
                default: return "";
            }
        }

        public static bool ReadyToHand(QuestDef q) => q != null && Step(q.Id) == 1 && Progress(q) >= q.Need;

        /// <summary>Hands the quest in: gold, the gift item, and the step moves to done.</summary>
        public static void Complete(QuestDef q)
        {
            if (q == null) return;
            Steps[q.Id] = 3;
            Game.State.Gold += q.Reward;
            if (!string.IsNullOrEmpty(q.Gift)) Game.State.AddBag(q.Gift);
            string goal = GoalItem(q);
            if (!string.IsNullOrEmpty(goal))
                for (int i = 0; i < q.Need; i++) Game.State.RemoveBag(goal);
        }

        /// <summary>The quest this NPC has something to say about right now, if any.</summary>
        public static QuestDef ForGiver(string nameKey, out bool ready)
        {
            ready = false;
            if (string.IsNullOrEmpty(nameKey)) return null;
            foreach (var q in All)
            {
                if (q.Giver != nameKey || q.Main) continue;
                if (Step(q.Id) == 0 && q.Chapter <= Game.State.Chapter) return q;
                if (Step(q.Id) == 1)
                {
                    ready = ReadyToHand(q);
                    return q;
                }
            }
            return null;
        }

        public static int ActiveCount
        {
            get
            {
                int n = 0;
                foreach (var q in All) if (Step(q.Id) == 1) n++;
                return n;
            }
        }

        public static int DoneCount
        {
            get
            {
                int n = 0;
                foreach (var q in All) if (Step(q.Id) == 3) n++;
                return n;
            }
        }

        /// <summary>The line the quest log shows for a quest.</summary>
        public static string Line(QuestDef q)
        {
            int left = Mathf.Max(0, q.Need - Progress(q));
            return Strings.Get(q.StepKey, left);
        }

        public static string StateWord(int step) => Strings.Get(step == 3 ? "jr.done"
            : step == 2 ? "jr.ready" : "jr.active");

        // ---- serialisation (the journal has to survive a save) ----

        public static string[] Capture()
        {
            var list = new List<string>();
            foreach (var kv in Steps) list.Add(kv.Key + ":" + kv.Value + ":" + (Base.TryGetValue(kv.Key, out var b) ? b : 0));
            foreach (var f in Fired) list.Add("f:" + f);
            return list.ToArray();
        }

        public static void Apply(string[] data)
        {
            Reset();
            if (data == null) return;
            foreach (var raw in data)
            {
                if (raw.StartsWith("f:")) { Fired.Add(raw.Substring(2)); continue; }
                var p = raw.Split(':');
                if (p.Length < 2) continue;
                int step;
                if (!int.TryParse(p[1], out step)) continue;
                Steps[p[0]] = step;
                int b;
                Base[p[0]] = p.Length > 2 && int.TryParse(p[2], out b) ? b : 0;
            }
        }
    }
}
