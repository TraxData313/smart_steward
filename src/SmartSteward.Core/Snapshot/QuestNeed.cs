using System.Collections.Generic;

namespace SmartSteward.Core.Snapshot
{
    /// <summary>What a quest asks the party to hold (DESIGN §2.9, RESEARCH §29).</summary>
    public enum QuestNeedKind
    {
        /// <summary>Items in the party's inventory — <see cref="QuestNeed.Ids"/> are item ids.</summary>
        Items,

        /// <summary>Men of the party — troop ids (a garrison's troop type, any bandit for a gang).</summary>
        Troops,

        /// <summary>Prisoners — troop ids (bandits for the landowner's laborers, a lord's rival as his hero's character id).</summary>
        Prisoners,
    }

    /// <summary>
    /// One need of one of the player's ongoing quests (step 26, DESIGN §2.9): the game-free input the Module reads from the quest's
    /// own fields at every plan (RESEARCH §29) — never stored. The steward KEEPS what it asks for; a food it asks for is bought up
    /// to the need like a goal of the player's.
    /// </summary>
    public sealed class QuestNeed
    {
        /// <summary>The quest's id (<c>QuestBase.StringId</c>) — the order needs are served in, and the log's.</summary>
        public string QuestId { get; set; } = "";

        /// <summary>The quest's title in the game's own words (<c>QuestBase.Title</c>): <c>Ryibelet Needs Grain Seeds</c>.</summary>
        public string Title { get; set; } = "";

        public QuestNeedKind Kind { get; set; }

        /// <summary>The ids that satisfy the need — any of them counts (an item, every held weapon of a class, every bandit troop).</summary>
        public List<string> Ids { get; set; } = new List<string>();

        /// <summary>What it asks for, in words for the hover: <c>Grain</c>, <c>Aserai Horse</c>, <c>one-handed axes</c>, <c>bandits</c>.</summary>
        public string What { get; set; } = "";

        /// <summary>How many it still asks for (what was already handed over is not counted); 0 or less = nothing.</summary>
        public int Amount { get; set; }
    }
}
