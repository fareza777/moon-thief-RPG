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
        /// <summary>What a felled beast might leave behind: usually something to eat,
        /// sometimes gear a notch under the shop's shelf so finds feel like finds and
        /// the shop keeps its best stock. Later nights drop richer fare.</summary>
        public static string RollDrop(int chapter, System.Random rng)
        {
            if (rng.Next(100) >= 24) return null;
            if (rng.Next(100) < 72)
            {
                string[] food;
                switch (Mathf.Clamp(chapter, 1, 3))
                {
                    case 1: food = new[] { "item.crumb", "item.berry", "item.plum", "item.morsel",
                                           "item.nut", "item.apple", "item.bread", "item.toast" }; break;
                    case 2: food = new[] { "item.morsel", "item.soup", "item.roll", "item.biscuit",
                                           "item.jam", "item.pudding", "item.cheese", "item.broth",
                                           "item.cider", "item.pie" }; break;
                    default: food = new[] { "item.stew", "item.fish", "item.cake", "item.mead",
                                            "item.roast", "item.chowder", "item.moonpie",
                                            "item.dumpling", "item.honey" }; break;
                }
                return food[rng.Next(food.Length)];
            }
            string[] gear;
            switch (Mathf.Clamp(chapter, 1, 3))
            {
                case 1: gear = new[] { "item.fork", "item.spoon", "item.shiv", "item.dagger",
                                       "item.scarf", "item.apron", "item.cloak",
                                       "item.charm.acorn", "item.charm.bell", "item.charm.bead" }; break;
                case 2: gear = new[] { "item.knife", "item.machete", "item.cutter", "item.handaxe",
                                       "item.tunic", "item.jerkin", "item.vest", "item.pelt",
                                       "item.charm.leaf", "item.charm.thread", "item.charm.coin",
                                       "item.charm.feather" }; break;
                default: gear = new[] { "item.saber", "item.glaive", "item.pike", "item.moonedge",
                                        "item.brigandine", "item.scale", "item.moonweave",
                                        "item.charm.lantern", "item.charm.rune", "item.charm.star",
                                        "item.charm.moon" }; break;
            }
            return gear[rng.Next(gear.Length)];
        }

        /// <summary>The night's gatekeeper always drops real gear - a fight that big owes you
        /// something worth more than the stall's everyday shelf.</summary>
        public static string BossDrop(System.Random rng)
        {
            var gear = new[] { "item.sickle", "item.spear", "item.vest", "item.hauberk",
                               "item.charm.leaf", "item.charm.thread", "item.charm.owl",
                               "item.pike", "item.scale", "item.charm.lantern",
                               "item.moonedge", "item.moonweave", "item.charm.moon" };
            return gear[rng.Next(gear.Length)];
        }

        public static readonly ItemDef[] All =
        {
            // ---- food (35): heals the whole party by Power. Cheap pocket food first,
            // then hearth fare, then the night-baked things only the last night sees.
            new ItemDef{ Key="item.crumb",    Kind=ItemKind.Food, Power=4,  Price=1 },
            new ItemDef{ Key="item.berry",    Kind=ItemKind.Food, Power=6,  Price=2 },
            new ItemDef{ Key="item.plum",     Kind=ItemKind.Food, Power=5,  Price=2 },
            new ItemDef{ Key="item.nut",      Kind=ItemKind.Food, Power=5,  Price=2 },
            new ItemDef{ Key="item.toast",    Kind=ItemKind.Food, Power=6,  Price=2 },
            new ItemDef{ Key="item.egg",      Kind=ItemKind.Food, Power=7,  Price=3 },
            new ItemDef{ Key="item.apple",    Kind=ItemKind.Food, Power=7,  Price=3 },
            new ItemDef{ Key="item.milk",     Kind=ItemKind.Food, Power=8,  Price=3 },
            new ItemDef{ Key="item.bread",    Kind=ItemKind.Food, Power=8,  Price=3 },
            new ItemDef{ Key="item.fig",      Kind=ItemKind.Food, Power=8,  Price=3 },
            new ItemDef{ Key="item.soup",     Kind=ItemKind.Food, Power=9,  Price=5 },
            new ItemDef{ Key="item.roll",     Kind=ItemKind.Food, Power=9,  Price=4 },
            new ItemDef{ Key="item.biscuit",  Kind=ItemKind.Food, Power=10, Price=4 },
            new ItemDef{ Key="item.jam",      Kind=ItemKind.Food, Power=10, Price=4 },
            new ItemDef{ Key="item.pudding",  Kind=ItemKind.Food, Power=11, Price=5 },
            new ItemDef{ Key="item.cheese",   Kind=ItemKind.Food, Power=11, Price=5 },
            new ItemDef{ Key="item.morsel",   Kind=ItemKind.Food, Power=12, Price=4 },
            new ItemDef{ Key="item.broth",    Kind=ItemKind.Food, Power=12, Price=5 },
            new ItemDef{ Key="item.pie",      Kind=ItemKind.Food, Power=13, Price=6 },
            new ItemDef{ Key="item.cider",    Kind=ItemKind.Food, Power=13, Price=6 },
            new ItemDef{ Key="item.honey",    Kind=ItemKind.Food, Power=14, Price=6 },
            new ItemDef{ Key="item.dumpling", Kind=ItemKind.Food, Power=14, Price=7 },
            new ItemDef{ Key="item.stew",     Kind=ItemKind.Food, Power=15, Price=7 },
            new ItemDef{ Key="item.fish",     Kind=ItemKind.Food, Power=16, Price=8 },
            new ItemDef{ Key="item.cake",     Kind=ItemKind.Food, Power=16, Price=8 },
            new ItemDef{ Key="item.mead",     Kind=ItemKind.Food, Power=17, Price=9 },
            new ItemDef{ Key="item.roast",    Kind=ItemKind.Food, Power=18, Price=9 },
            new ItemDef{ Key="item.chowder",  Kind=ItemKind.Food, Power=19, Price=10 },
            new ItemDef{ Key="item.tea",      Kind=ItemKind.Food, Power=20, Price=9 },
            new ItemDef{ Key="item.moonpie",  Kind=ItemKind.Food, Power=22, Price=12 },
            new ItemDef{ Key="item.moontart", Kind=ItemKind.Food, Power=24, Price=13 },
            new ItemDef{ Key="item.feast",    Kind=ItemKind.Food, Power=26, Price=14 },
            new ItemDef{ Key="item.starlight",Kind=ItemKind.Food, Power=32, Price=18 },
            new ItemDef{ Key="item.ambrosia", Kind=ItemKind.Food, Power=40, Price=24 },

            // ---- blades (30): +attack to the whole party. Kitchen things first,
            // field tools next, then the moon-forged steel of the last night.
            new ItemDef{ Key="item.fork",       Kind=ItemKind.Blade, Power=1,  Price=3 },
            new ItemDef{ Key="item.peeler",     Kind=ItemKind.Blade, Power=1,  Price=4 },
            new ItemDef{ Key="item.spoon",      Kind=ItemKind.Blade, Power=2,  Price=6 },
            new ItemDef{ Key="item.shiv",       Kind=ItemKind.Blade, Power=2,  Price=5 },
            new ItemDef{ Key="item.dagger",     Kind=ItemKind.Blade, Power=3,  Price=9 },
            new ItemDef{ Key="item.knife",      Kind=ItemKind.Blade, Power=4,  Price=14 },
            new ItemDef{ Key="item.machete",    Kind=ItemKind.Blade, Power=4,  Price=12 },
            new ItemDef{ Key="item.cutter",     Kind=ItemKind.Blade, Power=5,  Price=16 },
            new ItemDef{ Key="item.handaxe",    Kind=ItemKind.Blade, Power=5,  Price=18 },
            new ItemDef{ Key="item.spear",      Kind=ItemKind.Blade, Power=6,  Price=20 },
            new ItemDef{ Key="item.falchion",   Kind=ItemKind.Blade, Power=6,  Price=22 },
            new ItemDef{ Key="item.sickle",     Kind=ItemKind.Blade, Power=7,  Price=28 },
            new ItemDef{ Key="item.saber",      Kind=ItemKind.Blade, Power=7,  Price=26 },
            new ItemDef{ Key="item.glaive",     Kind=ItemKind.Blade, Power=8,  Price=30 },
            new ItemDef{ Key="item.lance",      Kind=ItemKind.Blade, Power=8,  Price=34 },
            new ItemDef{ Key="item.pike",       Kind=ItemKind.Blade, Power=9,  Price=36 },
            new ItemDef{ Key="item.rapier",     Kind=ItemKind.Blade, Power=9,  Price=38 },
            new ItemDef{ Key="item.moonedge",   Kind=ItemKind.Blade, Power=9,  Price=40 },
            new ItemDef{ Key="item.cleaver",    Kind=ItemKind.Blade, Power=10, Price=44 },
            new ItemDef{ Key="item.fang",       Kind=ItemKind.Blade, Power=10, Price=46 },
            new ItemDef{ Key="item.windsword",  Kind=ItemKind.Blade, Power=11, Price=52 },
            new ItemDef{ Key="item.blade",      Kind=ItemKind.Blade, Power=11, Price=60 },
            new ItemDef{ Key="item.nightbrand", Kind=ItemKind.Blade, Power=12, Price=56 },
            new ItemDef{ Key="item.frostsaber", Kind=ItemKind.Blade, Power=12, Price=58 },
            new ItemDef{ Key="item.flamebrand", Kind=ItemKind.Blade, Power=13, Price=64 },
            new ItemDef{ Key="item.voidedge",   Kind=ItemKind.Blade, Power=13, Price=68 },
            new ItemDef{ Key="item.starmetal",  Kind=ItemKind.Blade, Power=14, Price=72 },
            new ItemDef{ Key="item.moonblade",  Kind=ItemKind.Blade, Power=15, Price=80 },
            new ItemDef{ Key="item.dawnbreaker",Kind=ItemKind.Blade, Power=16, Price=90 },

            // ---- cloth (26): +max hp to the whole party. Scarves and aprons first,
            // then leather and mail, then the woven-moon armour.
            new ItemDef{ Key="item.scarf",      Kind=ItemKind.Cloth, Power=2,  Price=4 },
            new ItemDef{ Key="item.apron",      Kind=ItemKind.Cloth, Power=3,  Price=6 },
            new ItemDef{ Key="item.cloak",      Kind=ItemKind.Cloth, Power=4,  Price=8 },
            new ItemDef{ Key="item.tunic",      Kind=ItemKind.Cloth, Power=5,  Price=10 },
            new ItemDef{ Key="item.robe",       Kind=ItemKind.Cloth, Power=6,  Price=12 },
            new ItemDef{ Key="item.shawl",      Kind=ItemKind.Cloth, Power=7,  Price=14 },
            new ItemDef{ Key="item.vest",       Kind=ItemKind.Cloth, Power=8,  Price=20 },
            new ItemDef{ Key="item.jerkin",     Kind=ItemKind.Cloth, Power=9,  Price=18 },
            new ItemDef{ Key="item.pelt",       Kind=ItemKind.Cloth, Power=10, Price=22 },
            new ItemDef{ Key="item.leather",    Kind=ItemKind.Cloth, Power=11, Price=24 },
            new ItemDef{ Key="item.hauberk",    Kind=ItemKind.Cloth, Power=12, Price=28 },
            new ItemDef{ Key="item.brigandine", Kind=ItemKind.Cloth, Power=13, Price=32 },
            new ItemDef{ Key="item.mail",       Kind=ItemKind.Cloth, Power=14, Price=48 },
            new ItemDef{ Key="item.scale",      Kind=ItemKind.Cloth, Power=15, Price=40 },
            new ItemDef{ Key="item.plate",      Kind=ItemKind.Cloth, Power=16, Price=44 },
            new ItemDef{ Key="item.moonweave",  Kind=ItemKind.Cloth, Power=17, Price=48 },
            new ItemDef{ Key="item.nightshroud",Kind=ItemKind.Cloth, Power=18, Price=52 },
            new ItemDef{ Key="item.stormcoat",  Kind=ItemKind.Cloth, Power=19, Price=56 },
            new ItemDef{ Key="item.ironhide",   Kind=ItemKind.Cloth, Power=20, Price=60 },
            new ItemDef{ Key="item.starcloak",  Kind=ItemKind.Cloth, Power=21, Price=64 },
            new ItemDef{ Key="item.voidmantle", Kind=ItemKind.Cloth, Power=22, Price=68 },
            new ItemDef{ Key="item.dawnplate",  Kind=ItemKind.Cloth, Power=23, Price=72 },
            new ItemDef{ Key="item.lunarplate", Kind=ItemKind.Cloth, Power=24, Price=76 },
            new ItemDef{ Key="item.aegis",      Kind=ItemKind.Cloth, Power=26, Price=88 },

            // ---- charms (24): a small mixed blessing - a little speed, a little edge,
            // a little health all at once. Pocket luck first, star-magic last.
            new ItemDef{ Key="item.charm.acorn",   Kind=ItemKind.Charm, Power=1,  Price=8 },
            new ItemDef{ Key="item.charm.bell",    Kind=ItemKind.Charm, Power=2,  Price=16 },
            new ItemDef{ Key="item.charm.bead",    Kind=ItemKind.Charm, Power=2,  Price=10 },
            new ItemDef{ Key="item.charm.coin",    Kind=ItemKind.Charm, Power=3,  Price=12 },
            new ItemDef{ Key="item.charm.feather", Kind=ItemKind.Charm, Power=3,  Price=14 },
            new ItemDef{ Key="item.charm.thread",  Kind=ItemKind.Charm, Power=4,  Price=18 },
            new ItemDef{ Key="item.charm.shell",   Kind=ItemKind.Charm, Power=4,  Price=16 },
            new ItemDef{ Key="item.charm.pebble",  Kind=ItemKind.Charm, Power=4,  Price=18 },
            new ItemDef{ Key="item.charm.fang",    Kind=ItemKind.Charm, Power=5,  Price=20 },
            new ItemDef{ Key="item.charm.ivy",     Kind=ItemKind.Charm, Power=5,  Price=22 },
            new ItemDef{ Key="item.charm.leaf",    Kind=ItemKind.Charm, Power=6,  Price=22 },
            new ItemDef{ Key="item.charm.owl",     Kind=ItemKind.Charm, Power=6,  Price=24 },
            new ItemDef{ Key="item.charm.lantern", Kind=ItemKind.Charm, Power=7,  Price=26 },
            new ItemDef{ Key="item.charm.rune",    Kind=ItemKind.Charm, Power=7,  Price=28 },
            new ItemDef{ Key="item.charm.ash",     Kind=ItemKind.Charm, Power=8,  Price=30 },
            new ItemDef{ Key="item.charm.moth",    Kind=ItemKind.Charm, Power=8,  Price=32 },
            new ItemDef{ Key="item.charm.moon",    Kind=ItemKind.Charm, Power=9,  Price=40 },
            new ItemDef{ Key="item.charm.star",    Kind=ItemKind.Charm, Power=9,  Price=36 },
            new ItemDef{ Key="item.charm.wisp",    Kind=ItemKind.Charm, Power=10, Price=40 },
            new ItemDef{ Key="item.charm.comet",   Kind=ItemKind.Charm, Power=10, Price=44 },
            new ItemDef{ Key="item.charm.halo",    Kind=ItemKind.Charm, Power=11, Price=48 },
            new ItemDef{ Key="item.charm.eclipse", Kind=ItemKind.Charm, Power=12, Price=54 },
            new ItemDef{ Key="item.charm.zodiac",  Kind=ItemKind.Charm, Power=13, Price=60 },
            new ItemDef{ Key="item.charm.moonstone",Kind=ItemKind.Charm, Power=14, Price=66 },

            // ---- things a quest wants (5): never sold, never bought
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

        /// <summary>The night's best shelf: the top tier of each kind. Lists star it so the
        /// rarest steel and the last night's baking stand out in a long bag.</summary>
        public static bool Rare(ItemDef d)
        {
            switch (d.Kind)
            {
                case ItemKind.Food:  return d.Power >= 20;
                case ItemKind.Blade: return d.Power >= 10;
                case ItemKind.Cloth: return d.Power >= 18;
                case ItemKind.Charm: return d.Power >= 11;
                default: return false;
            }
        }

        public static int SlotOf(ItemKind k) => k == ItemKind.Blade ? 0 : k == ItemKind.Cloth ? 1 : 2;

        public static string SlotKey(ItemKind k) =>
            k == ItemKind.Blade ? "jr.slot.blade" : k == ItemKind.Cloth ? "jr.slot.cloth" : "jr.slot.charm";

        /// <summary>Short effect line for a row, e.g. "+4 ATK". No translation needed: the
        /// numbers and the three stat words are the whole label.</summary>
        public static string Effect(ItemDef d)
        {
            switch (d.Kind)
            {
                case ItemKind.Food:
                    var fx = "+" + d.Power + " " + Strings.Get("jr.hp");
                    // every dish has a second comfort - say so where it is bought and bagged
                    if (d.Key == "item.honey" || d.Key == "item.starlight") fx += "+" + Strings.Get("jr.cure");
                    else if (d.Key == "item.tea" || d.Key == "item.mead") fx += "+" + Strings.Get("jr.wake");
                    else if (d.Key == "item.soup" || d.Key == "item.feast") fx += "+" + Strings.Get("jr.mom");
                    return fx;
                case ItemKind.Blade: return "+" + d.Power + " " + Strings.Get("jr.atk");
                case ItemKind.Cloth: return "+" + d.Power + " " + Strings.Get("jr.maxhp");
                case ItemKind.Charm: return "+" + d.Power + " " + Strings.Get("jr.all");
                default: return Strings.Get("jr.key");
            }
        }

        /// <summary>What a sealed cache holds. Chests are the night's promised find, so they
        /// draw from a wider shelf than a stray kill: roughly half gear, half fare, and the
        /// pick deepens with the night - a night-three box can hide the catalog's best steel.</summary>
        public static string RollLoot(int chapter, System.Random rng)
        {
            string[] pool;
            switch (Mathf.Clamp(chapter, 1, 3))
            {
                case 1:
                    pool = rng.Next(100) < 45
                        ? new[] { "item.fork", "item.spoon", "item.shiv", "item.dagger",
                                  "item.scarf", "item.apron", "item.cloak", "item.charm.acorn",
                                  "item.charm.bell", "item.charm.bead" }
                        : new[] { "item.crumb", "item.berry", "item.plum", "item.morsel",
                                  "item.nut", "item.apple", "item.bread", "item.toast",
                                  "item.honey" };
                    break;
                case 2:
                    pool = rng.Next(100) < 48
                        ? new[] { "item.knife", "item.machete", "item.cutter", "item.handaxe",
                                  "item.tunic", "item.jerkin", "item.vest", "item.pelt",
                                  "item.charm.leaf", "item.charm.thread", "item.charm.coin",
                                  "item.charm.feather" }
                        : new[] { "item.morsel", "item.soup", "item.roll", "item.biscuit",
                                  "item.jam", "item.pudding", "item.cheese", "item.broth",
                                  "item.cider", "item.pie" };
                    break;
                default:
                    pool = rng.Next(100) < 50
                        ? new[] { "item.saber", "item.glaive", "item.pike", "item.scale",
                                  "item.brigandine", "item.hauberk", "item.charm.lantern",
                                  "item.charm.rune", "item.charm.star", "item.charm.owl" }
                        : new[] { "item.stew", "item.fish", "item.cake", "item.mead",
                                  "item.roast", "item.chowder", "item.moonpie",
                                  "item.dumpling", "item.honey" };
                    break;
            }
            return pool[rng.Next(pool.Length)];
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

        /// <summary>Mains drive the whole run, so they count until actually done - a header
        /// reading "0 active" under a list of three main quests lies to the player.</summary>
        public static int MainLeft
        {
            get
            {
                int n = 0;
                foreach (var q in All) if (q.Main && Step(q.Id) != 3) n++;
                return n;
            }
        }

        public static int SideActive
        {
            get
            {
                int n = 0;
                foreach (var q in All) if (!q.Main && Step(q.Id) == 1) n++;
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
            : step == 2 ? "jr.ready" : step == 0 ? "jr.new" : "jr.active");

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
