using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using SmartSteward.Core.Snapshot;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace SmartSteward.Adapter
{
    /// <summary>
    /// Reads what the player's ongoing quests ask the party to hold (step 26, DESIGN §2.9, RESEARCH §29) into the snapshot's
    /// <see cref="QuestNeed"/>s — live, at every snapshot, nothing stored (save-safe). One small reader per vanilla quest type,
    /// matched by the quest class's full name; the fields are private, read by reflection. A field missing (another game version,
    /// a mod's quest class) logs ONE line and skips that quest — never throws. A <c>[quest]</c> log line lists what was read
    /// whenever it changes.
    /// </summary>
    internal static class QuestReader
    {
        private const string Issues = "TaleWorlds.CampaignSystem.Issues.";
        private const BindingFlags AnyInstance = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        private delegate void Reader(QuestBase quest, List<QuestNeed> into);

        /// <summary>The quest types read, by their class's full name (nested classes: <c>Outer+Inner</c>).</summary>
        private static readonly Dictionary<string, Reader> Readers = new Dictionary<string, Reader>(StringComparer.Ordinal)
        {
            [Issues + "HeadmanNeedsGrainIssueBehavior+HeadmanNeedsGrainIssueQuest"] =
                (q, into) => Item(q, into, DefaultItems.Grain, Int(q, "_neededGrainAmount")),
            [Issues + "ArmyNeedsSuppliesIssueBehavior+ArmyNeedsSuppliesIssueQuest"] = (q, into) =>
            {
                Item(q, into, DefaultItems.Grain, Int(q, "_requestedGrainAmount"));
                // The wine is an extra the commander pays for (a trade good: the Other goods line); its livestock is never
                // traded by the steward.
                Item(q, into, Game.Current?.ObjectManager?.GetObject<ItemObject>("wine"), Int(q, "_requestedWineAmount"));
            },
            [Issues + "LordNeedsHorsesIssueBehavior+LordNeedsHorsesIssueQuest"] =
                (q, into) => Item(q, into, Field<ItemObject>(q, "_mountObjectToBeDelivered"), Int(q, "_numMountsToBeDelivered")),
            [Issues + "HeadmanVillageNeedsDraughtAnimalsIssueBehavior+HeadmanVillageNeedsDraughtAnimalsIssueQuest"] =
                (q, into) => Item(q, into, Field<ItemObject>(q, "_requestedAnimal"), Int(q, "_requestedAnimalAmount")),
            [Issues + "VillageNeedsToolsIssueBehavior+VillageNeedsToolsIssueQuest"] =
                (q, into) => Item(q, into, Field<ItemObject>(q, "_requestedTradeGood"), Int(q, "_numberOfRequestedGood")),
            [Issues + "VillageNeedsCraftingMaterialsIssueBehavior+VillageNeedsCraftingMaterialsIssueQuest"] =
                (q, into) => Item(q, into, Field<ItemObject>(q, "_requestedItem"), Int(q, "_requestedItemAmount")),
            [Issues + "ArtisanCantSellProductsAtAFairPriceIssueBehavior+ArtisanCantSellProductsAtAFairPriceIssueQuest"] =
                (q, into) => Item(q, into, Field<ItemObject>(q, "_rawMaterialsToBeDelivered"),
                    Int(q, "_amountOfRawGoodsToBeDelivered") - Int(q, "_deliveredRawGoods")),
            [Issues + "ArtisanOverpricedGoodsIssueBehavior+ArtisanOverpricedGoodsIssueQuest"] =
                (q, into) => Item(q, into, Field<ItemObject>(q, "_requestedTradeGood"),
                    Int(q, "_requestedTradeGoodAmount") - Int(q, "_givenTradeGoods")),
            [Issues + "LandLordTheArtOfTheTradeIssueBehavior+LandLordTheArtOfTheTradeIssueQuest"] =
                (q, into) => Item(q, into, Field<ItemObject>(q, "_selectedItemObject"),
                    Int(q, "_selectedItemObjectCount") - Int(q, "_soldCount")),
            [Issues + "GangLeaderNeedsWeaponsIssueQuestBehavior+GangLeaderNeedsWeaponsIssueQuest"] = Weapons,
            [Issues + "LordNeedsGarrisonTroopsIssueQuestBehavior+LordNeedsGarrisonTroopsIssueQuest"] = (q, into) =>
            {
                var troop = Field<CharacterObject>(q, "_requestedTroopType");
                Add(q, into, QuestNeedKind.Troops, Int(q, "_requestedTroopAmount"), troop.Name?.ToString() ?? troop.StringId,
                    troop.StringId);
            },
            [Issues + "GangLeaderNeedsRecruitsIssueBehavior+GangLeaderNeedsRecruitsIssueQuest"] = (q, into) =>
                Add(q, into, QuestNeedKind.Troops, Int(q, "_requestedRecruitCount") - Int(q, "_deliveredRecruitCount"), Bandits(),
                    Bandits(MobileParty.MainParty?.MemberRoster)),
            [Issues + "LandLordNeedsManualLaborersIssueBehavior+LandLordNeedsManualLaborersIssueQuest"] = (q, into) =>
                Add(q, into, QuestNeedKind.Prisoners, Int(q, "_requestedPrisonerCount") - Int(q, "_deliveredPrisonerCount"),
                    Bandits(), Bandits(MobileParty.MainParty?.PrisonRoster)),
            [Issues + "LordWantsRivalCapturedIssueBehavior+LordWantsRivalCapturedIssueQuest"] = (q, into) =>
            {
                var rival = Field<Hero>(q, "_targetHero");
                if (rival.CharacterObject != null)
                    Add(q, into, QuestNeedKind.Prisoners, 1, rival.Name?.ToString() ?? rival.StringId, rival.CharacterObject.StringId);
            },
        };

        /// <summary>Quest types and fields already reported unreadable this session (one log line each).</summary>
        private static readonly HashSet<string> Reported = new HashSet<string>(StringComparer.Ordinal);

        private static string _lastLine = "";

        /// <summary>The needs of every ongoing quest the steward knows how to read; empty when none (or on any trouble).</summary>
        public static List<QuestNeed> Read()
        {
            var needs = new List<QuestNeed>();
            IEnumerable<QuestBase> quests;
            try
            {
                quests = Campaign.Current?.QuestManager?.Quests?.ToList() ?? new List<QuestBase>();
            }
            catch (Exception ex)
            {
                ReportOnce("list", () => ModLog.Error("quest", "reading the quest list", ex));
                return needs;
            }
            foreach (var quest in quests)
            {
                if (quest == null)
                    continue;
                string type = quest.GetType().FullName ?? "";
                if (!Readers.TryGetValue(type, out var reader))
                    continue;
                try
                {
                    if (!quest.IsOngoing)
                        continue;
                    var own = new List<QuestNeed>();
                    reader(quest, own);
                    needs.AddRange(own.Where(n => n.Amount > 0 && n.Ids.Count > 0));
                }
                catch (Exception ex)
                {
                    string why = ex is QuestFieldException ? ex.Message : ex.GetType().Name + ": " + ex.Message;
                    ReportOnce(type + "|" + why, () => ModLog.Info("quest",
                        "cannot read " + type + " (" + why + ") - its needs are not kept; the steward goes on without them"));
                }
            }
            LogIfChanged(needs);
            return needs;
        }

        /// <summary>The <c>[quest]</c> line: what was read, written when it differs from the last one.</summary>
        private static void LogIfChanged(List<QuestNeed> needs)
        {
            string line = needs.Count == 0
                ? "no quest asks for anything the steward trades"
                : string.Join("; ", needs.Select(n => n.Title + " (" + n.QuestId + "): " + n.Kind + " "
                                                       + n.Amount.ToString(CultureInfo.InvariantCulture) + " " + n.What + " ["
                                                       + string.Join(",", n.Ids) + "]"));
            if (line == _lastLine)
                return;
            _lastLine = line;
            ModLog.Info("quest", line);
        }

        private static void ReportOnce(string key, Action report)
        {
            if (Reported.Add(key))
                report();
        }

        // ── the pieces ───────────────────────────────────────────────────────────────────────────────────

        /// <summary>One item asked for (any modifier counts — RESEARCH §29).</summary>
        private static void Item(QuestBase quest, List<QuestNeed> into, ItemObject? item, int amount)
        {
            if (item == null || amount <= 0)
                return;
            Add(quest, into, QuestNeedKind.Items, amount, item.Name?.ToString() ?? item.StringId, item.StringId);
        }

        /// <summary>Gang Leader Needs Weapons: any held weapon of the class counts — the ids are the party's weapons of it now.</summary>
        private static void Weapons(QuestBase quest, List<QuestNeed> into)
        {
            var cls = Field<WeaponClass>(quest, "_requestedWeaponClass");
            int amount = Int(quest, "_requestedWeaponAmount");
            var roster = MobileParty.MainParty?.ItemRoster;
            var ids = new List<string>();
            if (roster != null)
                for (int i = 0; i < roster.Count; i++)
                {
                    var item = roster.GetElementCopyAtIndex(i).EquipmentElement.Item;
                    if (item?.WeaponComponent?.PrimaryWeapon != null && item.WeaponComponent.PrimaryWeapon.WeaponClass == cls
                        && !ids.Contains(item.StringId))
                        ids.Add(item.StringId);
                }
            // The quest's own words for vanilla's one class (GetNeededClassTextObject); ours for a mod's other class.
            string what = cls == WeaponClass.OneHandedAxe
                ? new TextObject("{=tza4micZ}one-handed axes").ToString()
                : UI.UiText.S("ss_quest_weapons", "weapons");
            Add(quest, into, QuestNeedKind.Items, amount, what, ids.ToArray());
        }

        /// <summary>The bandits of a roster (<c>Occupation.Bandit</c> — what the gang and the landowner take), heroes never.</summary>
        private static string[] Bandits(TaleWorlds.CampaignSystem.Roster.TroopRoster? roster)
        {
            if (roster == null)
                return Array.Empty<string>();
            return roster.GetTroopRoster()
                .Where(e => e.Character != null && !e.Character.IsHero && e.Character.Occupation == Occupation.Bandit && e.Number > 0)
                .Select(e => e.Character.StringId).Distinct().ToArray();
        }

        private static string Bandits() => UI.UiText.S("ss_quest_bandits", "bandits");

        private static void Add(QuestBase quest, List<QuestNeed> into, QuestNeedKind kind, int amount, string what,
            params string[] ids)
        {
            if (amount <= 0)
                return;
            into.Add(new QuestNeed
            {
                QuestId = quest.StringId ?? "",
                Title = quest.Title?.ToString() ?? quest.StringId ?? "",
                Kind = kind,
                Amount = amount,
                What = what,
                Ids = ids.Where(i => !string.IsNullOrEmpty(i)).ToList(),
            });
        }

        private static int Int(object quest, string field) => Field<int>(quest, field);

        /// <summary>A private field of the quest (the class or a base), typed; throws <see cref="QuestFieldException"/> when it is
        /// missing, null or of another type — the caller logs once and skips the quest.</summary>
        private static T Field<T>(object quest, string name)
        {
            for (var type = quest.GetType(); type != null; type = type.BaseType)
            {
                var field = type.GetField(name, AnyInstance | BindingFlags.DeclaredOnly);
                if (field == null)
                    continue;
                var value = field.GetValue(quest);
                if (value is T typed)
                    return typed;
                throw new QuestFieldException("field " + name + " is " + (value == null ? "null" : value.GetType().Name)
                                              + ", not " + typeof(T).Name);
            }
            throw new QuestFieldException("no field " + name);
        }

        private sealed class QuestFieldException : Exception
        {
            public QuestFieldException(string message)
                : base(message)
            {
            }
        }
    }
}
