using System;
using System.Collections.Generic;
using UnityEngine;

namespace HouseSim
{
    /// <summary>Shared room inventory. Batch exchanges validate all costs before any change.</summary>
    public class PlayerInventory : MonoBehaviour
    {
        readonly Dictionary<ResourceData, int> inventory = new Dictionary<ResourceData, int>();
        public event Action<ResourceData, int> Changed;
        void OnEnable()
        {
            HouseEvents.OnResourceGained += HandleResourceGained;
            HouseEvents.OnResourceSpent += HandleResourceSpent;
        }
        void OnDisable()
        {
            HouseEvents.OnResourceGained -= HandleResourceGained;
            HouseEvents.OnResourceSpent -= HandleResourceSpent;
        }
        void HandleResourceGained(ResourceData resource, int amount)
        {
            if (resource == null || amount <= 0) return;
            SetAmount(resource, (int)Math.Min(int.MaxValue, (long)GetAmount(resource) + amount));
        }
        void HandleResourceSpent(ResourceData resource, int amount)
        {
            if (amount <= 0 || !HasEnoughResource(resource, amount)) return;
            SetAmount(resource, GetAmount(resource) - amount);
        }
        public int GetAmount(ResourceData resource) => resource != null && inventory.TryGetValue(resource, out int count) ? count : 0;
        public Dictionary<ResourceData, int> GetSnapshot() => new Dictionary<ResourceData, int>(inventory);
        public bool HasResource(ResourceData resource) => resource != null && inventory.ContainsKey(resource);
        public bool HasEnoughResource(ResourceData resource, int requiredAmount) => resource != null && requiredAmount >= 0 && GetAmount(resource) >= requiredAmount;
        public void SetAmount(ResourceData resource, int amount)
        {
            if (resource == null) return;
            amount = Mathf.Max(0, amount);
            if (HasResource(resource) && GetAmount(resource) == amount) return;
            inventory[resource] = amount;
            Changed?.Invoke(resource, amount);
        }
        public bool TryExchange(Dictionary<ResourceData, int> costs, Dictionary<ResourceData, int> rewards)
        {
            if (costs != null)
                foreach (var pair in costs)
                    if (pair.Key == null || pair.Value < 0 || !HasEnoughResource(pair.Key, pair.Value)) return false;
            if (rewards != null)
                foreach (var pair in rewards)
                    if (pair.Key == null || pair.Value < 0 || (long)GetAmount(pair.Key) - (costs != null && costs.TryGetValue(pair.Key, out int c) ? c : 0) + pair.Value > int.MaxValue) return false;
            var changed = new HashSet<ResourceData>();
            if (costs != null) foreach (var pair in costs) { inventory[pair.Key] = GetAmount(pair.Key) - pair.Value; changed.Add(pair.Key); }
            if (rewards != null) foreach (var pair in rewards) { inventory[pair.Key] = GetAmount(pair.Key) + pair.Value; changed.Add(pair.Key); }
            foreach (var resource in changed) Changed?.Invoke(resource, GetAmount(resource));
            return true;
        }
    }
}
