using System;
using System.Collections.Generic;
using UnityEngine;

namespace HouseSim.Gym
{
    [Serializable]
    public sealed class PlayerResourceEntry
    {
        public string resourceName;
        public float startingValue;
        public float minValue;
        public float maxValue = 100f;

        public PlayerResourceEntry(string name, float start, float minimum, float maximum)
        {
            resourceName = name;
            startingValue = start;
            minValue = minimum;
            maxValue = maximum;
        }
    }

    /// <summary>Runtime store for the gym's named, clamped resources.</summary>
    [DefaultExecutionOrder(-100)]
    public sealed class PlayerResources : MonoBehaviour
    {
        [SerializeField] List<PlayerResourceEntry> definitions = new List<PlayerResourceEntry>();

        readonly Dictionary<string, Stat> values = new Dictionary<string, Stat>();
        bool initialized;

        public event Action<string, float> Changed;

        public Stat this[string resourceName]
        {
            get
            {
                EnsureInitialized();
                return values[resourceName];
            }
        }

        void Awake() => EnsureInitialized();

        void Reset()
        {
            definitions = CreateDefaults();
            initialized = false;
        }

        public float Get(string resourceName)
        {
            EnsureInitialized();
            return values.TryGetValue(resourceName, out Stat stat) ? stat.value : 0f;
        }

        public bool Contains(string resourceName)
        {
            EnsureInitialized();
            return values.ContainsKey(resourceName);
        }

        public bool Has(string resourceName, float amount)
        {
            return amount >= 0f && Get(resourceName) >= amount;
        }

        public bool TrySpend(string resourceName, float amount)
        {
            if (!Has(resourceName, amount)) return false;
            values[resourceName].Change(-amount);
            return true;
        }

        public void Add(string resourceName, float amount)
        {
            EnsureInitialized();
            if (values.TryGetValue(resourceName, out Stat stat)) stat.Change(amount);
        }

        public void Define(string resourceName, float start, float minimum, float maximum)
        {
            EnsureInitialized();
            if (string.IsNullOrWhiteSpace(resourceName) || values.ContainsKey(resourceName)) return;

            var definition = new PlayerResourceEntry(resourceName, start, minimum, maximum);
            definitions.Add(definition);
            AddRuntimeValue(definition);
        }

        void EnsureInitialized()
        {
            if (initialized) return;
            initialized = true;

            EnsureRequiredDefinitions();
            values.Clear();
            foreach (PlayerResourceEntry definition in definitions)
            {
                if (definition == null || string.IsNullOrWhiteSpace(definition.resourceName) || values.ContainsKey(definition.resourceName))
                    continue;
                AddRuntimeValue(definition);
            }
        }

        void AddRuntimeValue(PlayerResourceEntry definition)
        {
            var stat = new Stat(definition.resourceName, definition.startingValue, definition.minValue, definition.maxValue);
            stat.Changed += changed => Changed?.Invoke(changed.name, changed.value);
            values.Add(definition.resourceName, stat);
        }

        void EnsureRequiredDefinitions()
        {
            List<PlayerResourceEntry> defaults = CreateDefaults();
            foreach (PlayerResourceEntry required in defaults)
            {
                bool exists = false;
                foreach (PlayerResourceEntry current in definitions)
                {
                    if (current != null && current.resourceName == required.resourceName)
                    {
                        exists = true;
                        break;
                    }
                }
                if (!exists) definitions.Add(required);
            }
        }

        static List<PlayerResourceEntry> CreateDefaults()
        {
            return new List<PlayerResourceEntry>
            {
                new PlayerResourceEntry("gymCoins", 500f, 0f, 999f),
                new PlayerResourceEntry("energy", 100f, 0f, 100f),
                new PlayerResourceEntry("hydration", 80f, 0f, 100f),
                new PlayerResourceEntry("health", 50f, 0f, 100f),
                new PlayerResourceEntry("maxHealth", 100f, 100f, 1000f),
                new PlayerResourceEntry("stress", 50f, 15f, 100f),
                new PlayerResourceEntry("armGain", 0f, 0f, 100f),
                new PlayerResourceEntry("backGain", 0f, 0f, 100f),
                new PlayerResourceEntry("chestGain", 0f, 0f, 100f),
                new PlayerResourceEntry("legGain", 0f, 0f, 100f),
                new PlayerResourceEntry("cardioLevel", 0f, 0f, 100f),
                new PlayerResourceEntry("water", 0f, 0f, 99f),
                new PlayerResourceEntry("powder", 0f, 0f, 99f),
                new PlayerResourceEntry("energyStick", 0f, 0f, 99f),
                new PlayerResourceEntry("milk", 0f, 0f, 99f),
                new PlayerResourceEntry("fruit", 0f, 0f, 99f),
            };
        }
    }
}
