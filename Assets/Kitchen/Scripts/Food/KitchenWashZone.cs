using System.Collections.Generic;
using UnityEngine;

namespace HouseSim.Kitchen
{
    public class KitchenWashZone : MonoBehaviour
    {
        public KitchenToggle faucet;
        public TextMesh label;
        private readonly Dictionary<KitchenFood, float> lastWashedAt = new Dictionary<KitchenFood, float>();
        private KitchenFood lastFood;
        private float lastSeenAt = float.NegativeInfinity;
        private float nextLabelUpdate;

        private void Update()
        {
            if (Time.unscaledTime < nextLabelUpdate) return;
            nextLabelUpdate = Time.unscaledTime + 0.1f;
            RefreshLabel();
        }

        private void OnTriggerStay(Collider other)
        {
            var food = other.GetComponentInParent<KitchenFood>();
            if (food == null || !food.IsProduce || food.IsConsumed) return;
            lastFood = food;
            lastSeenAt = Time.time;
            if (faucet == null || !faucet.Value) return;
            if (lastWashedAt.TryGetValue(food, out float last) && last == Time.fixedTime) return;
            lastWashedAt[food] = Time.fixedTime;
            food.Wash(Time.fixedDeltaTime);
            RefreshLabel();
            KitchenSession.Instance?.Tutorial("wash", "Turn on the tap.\nRinse whole or chopped produce for 0.1 seconds.", transform.position);
            if (lastWashedAt.Count > 64)
            {
                var stale = new List<KitchenFood>();
                foreach (var entry in lastWashedAt)
                    if (entry.Key == null || Time.fixedTime - entry.Value > 10f) stale.Add(entry.Key);
                foreach (var key in stale) lastWashedAt.Remove(key);
            }
        }

        private void RefreshLabel()
        {
            if (label == null) return;
            bool running = faucet != null && faucet.Value;
            if (lastFood == null || lastFood.IsConsumed)
            {
                label.text = running ? "RINSE FRUIT & VEGETABLES\n0.1 SECOND TO WASH" : "WASH FRUIT & VEGETABLES\nTURN THE TAP ON";
                return;
            }
            string foodName = lastFood.definition != null ? lastFood.definition.kind.ToString().ToUpperInvariant() : "PRODUCE";
            int percent = lastFood.IsDirty ? Mathf.Min(99, Mathf.RoundToInt(lastFood.WashProgress * 100f)) : 100;
            if (!lastFood.IsDirty)
            {
                label.text = foodName + " - WASHED 100%\nCLEAN FOR SALAD";
                return;
            }
            string activity = !running ? "PAUSED - TAP OFF" : Time.time - lastSeenAt > 0.2f ? "PAUSED - RETURN TO WATER" : "RINSING";
            label.text = foodName + " - " + percent + "% (" + (lastFood.WashProgress * 0.1f).ToString("0.0") + "/0.1s)\n" + activity;
        }
    }
}
