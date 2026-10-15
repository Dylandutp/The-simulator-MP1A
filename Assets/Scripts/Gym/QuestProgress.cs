using System;
using System.Collections.Generic;
using UnityEngine;

namespace HouseSim.Gym
{
    [Serializable]
    public sealed class QuestProgressEntry
    {
        public string questId;
        [Min(0)] public int progress;
        [HideInInspector] public int difficulty;
        [HideInInspector] public float baseline;
    }

    /// <summary>
    /// Five stable board slots advance through the requested twenty-quest list.
    /// Count quests start counting when they become active; stat quests use current values.
    /// </summary>
    public sealed class QuestProgress : MonoBehaviour
    {
        sealed class QuestDefinition
        {
            public readonly string title, objective, difficulty;
            public readonly int target, reward;
            public readonly bool absoluteValue;

            public QuestDefinition(string title, string objective, int target, int reward, string difficulty, bool absoluteValue = false)
            {
                this.title = title;
                this.objective = objective;
                this.target = target;
                this.reward = reward;
                this.difficulty = difficulty;
                this.absoluteValue = absoluteValue;
            }
        }

        static readonly QuestDefinition[] QuestList =
        {
            Q("COMPLETE 1 MACHINE SESSION", "sessions", 1, 4, "EASY"),
            Q("DRINK FROM THE DISPENSER ONCE", "drink", 1, 4, "EASY"),
            Q("PICK UP 3 WILD ITEMS", "wildItems", 3, 6, "EASY"),
            Q("REPAIR WITH THE WRENCH ONCE", "repair", 1, 4, "EASY"),
            Q("CRAFT 1 SHAKER RECIPE", "craft", 1, 6, "EASY"),
            Q("COMPLETE 3 MACHINE SESSIONS", "sessions", 3, 6, "MEDIUM"),
            Q("COMPLETE 5 MACHINE SESSIONS", "sessions", 5, 10, "MEDIUM"),
            Q("REPAIR WITH THE WRENCH 3 TIMES", "repair", 3, 10, "MEDIUM"),
            Q("DRINK FROM THE DISPENSER 3 TIMES", "drink", 3, 6, "MEDIUM"),
            Q("BUY 2 ITEMS ON THE WALL BOARD", "buy", 2, 8, "MEDIUM"),
            Q("CRAFT 3 SHAKER RECIPES", "craft", 3, 10, "MEDIUM"),
            Q("REACH 20 ARM GAIN", "armGain", 20, 10, "HARD", true),
            Q("REACH 20 CHEST GAIN", "chestGain", 20, 10, "HARD", true),
            Q("REACH 20 BACK GAIN", "backGain", 20, 12, "HARD", true),
            Q("REACH 20 LEG GAIN", "legGain", 20, 12, "HARD", true),
            Q("RUN 1 MILE ON THE TREADMILL", "treadmillMiles", 1, 16, "HARD", true),
            Q("COMPLETE 15 MACHINE SESSIONS", "sessions", 15, 16, "HARD"),
            Q("REACH 10 IN ALL FOUR GAINS", "allGains", 10, 20, "EXPERT", true),
            Q("CRAFT ALL 6 SHAKER RECIPES", "allRecipes", 6, 20, "EXPERT", true),
            Q("REACH 50 IN ALL FOUR GAINS", "allGains", 50, 40, "EXPERT", true),
        };

        static readonly Dictionary<string, int> SlotOffsets = new Dictionary<string, int>
        {
            { "sessions", 0 }, { "drink", 1 }, { "allGains", 2 }, { "repair", 3 }, { "craft", 4 }
        };

        [SerializeField] List<QuestProgressEntry> quests = new List<QuestProgressEntry>();
        readonly Dictionary<string, QuestProgressEntry> byId = new Dictionary<string, QuestProgressEntry>();
        readonly Dictionary<string, float> metrics = new Dictionary<string, float>();
        readonly HashSet<string> craftedRecipes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public event Action Changed;

        static QuestDefinition Q(string title, string objective, int target, int reward, string difficulty, bool absolute = false)
            => new QuestDefinition(title, objective, target, reward, difficulty, absolute);

        void Awake() => RebuildLookup();

        public void Increment(string objective, int amount)
        {
            if (amount <= 0 || string.IsNullOrWhiteSpace(objective)) return;
            metrics[objective] = Metric(objective) + amount;
            RefreshObjective(objective);
        }

        public void ReportValue(string objective, float value)
        {
            if (string.IsNullOrWhiteSpace(objective)) return;
            float clamped = Mathf.Max(0f, value);
            if (Mathf.Approximately(Metric(objective), clamped)) return;
            metrics[objective] = clamped;
            RefreshObjective(objective);
        }

        public void MarkRecipeCrafted(string recipeName)
        {
            if (string.IsNullOrWhiteSpace(recipeName) || !craftedRecipes.Add(recipeName)) return;
            ReportValue("allRecipes", craftedRecipes.Count);
        }

        public bool IsComplete(string questId)
        {
            if (!TryGet(questId, out QuestProgressEntry quest) || IsFinished(quest)) return false;
            return quest.progress >= DefinitionFor(quest).target;
        }

        public bool IsFinished(string questId) => TryGet(questId, out QuestProgressEntry quest) && IsFinished(quest);

        public bool ClaimAndAdvance(string questId)
        {
            if (!TryGet(questId, out QuestProgressEntry quest) || !IsComplete(questId)) return false;
            quest.difficulty++;
            quest.progress = 0;
            if (!IsFinished(quest))
            {
                QuestDefinition next = DefinitionFor(quest);
                quest.baseline = next.absoluteValue ? 0f : Metric(next.objective);
                SyncProgress(quest);
            }
            Changed?.Invoke();
            return true;
        }

        public int GetProgress(string questId) => TryGet(questId, out QuestProgressEntry quest) && !IsFinished(quest) ? quest.progress : 0;
        public int GetTarget(string questId) => TryGet(questId, out QuestProgressEntry quest) && !IsFinished(quest) ? DefinitionFor(quest).target : 1;
        public string GetDifficultyName(string questId) => TryGet(questId, out QuestProgressEntry quest) && !IsFinished(quest) ? DefinitionFor(quest).difficulty : "COMPLETE";
        public string GetQuestTitle(string questId) => TryGet(questId, out QuestProgressEntry quest) && !IsFinished(quest) ? DefinitionFor(quest).title : "QUEST LINE COMPLETE";
        public int GetRewardCoins(string questId) => TryGet(questId, out QuestProgressEntry quest) && !IsFinished(quest) ? DefinitionFor(quest).reward : 0;

        void RefreshObjective(string objective)
        {
            if (byId.Count == 0) RebuildLookup();
            bool changed = false;
            foreach (QuestProgressEntry quest in quests)
            {
                if (quest == null || IsFinished(quest) || DefinitionFor(quest).objective != objective) continue;
                int previous = quest.progress;
                SyncProgress(quest);
                changed |= previous != quest.progress;
            }
            if (changed) Changed?.Invoke();
        }

        void SyncProgress(QuestProgressEntry quest)
        {
            QuestDefinition definition = DefinitionFor(quest);
            float raw = definition.absoluteValue ? Metric(definition.objective) : Metric(definition.objective) - quest.baseline;
            quest.progress = Mathf.Clamp(Mathf.FloorToInt(raw + .0001f), 0, definition.target);
        }

        float Metric(string objective) => metrics.TryGetValue(objective, out float value) ? value : 0f;

        bool TryGet(string questId, out QuestProgressEntry quest)
        {
            quest = null;
            if (byId.Count == 0 && quests.Count > 0) RebuildLookup();
            return !string.IsNullOrWhiteSpace(questId) && byId.TryGetValue(questId, out quest);
        }

        void RebuildLookup()
        {
            byId.Clear();
            foreach (QuestProgressEntry quest in quests)
            {
                if (quest == null || !SlotOffsets.ContainsKey(quest.questId) || byId.ContainsKey(quest.questId)) continue;
                quest.difficulty = Mathf.Clamp(quest.difficulty, 0, 4);
                if (!IsFinished(quest)) SyncProgress(quest);
                byId.Add(quest.questId, quest);
            }
        }

        static bool IsFinished(QuestProgressEntry quest) => quest.difficulty >= 4;

        static QuestDefinition DefinitionFor(QuestProgressEntry quest)
        {
            int offset = SlotOffsets.TryGetValue(quest.questId, out int value) ? value : 0;
            return QuestList[Mathf.Min(QuestList.Length - 1, offset + Mathf.Clamp(quest.difficulty, 0, 3) * 5)];
        }
    }
}
