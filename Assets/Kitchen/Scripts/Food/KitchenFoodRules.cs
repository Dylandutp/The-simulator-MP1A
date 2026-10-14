using UnityEngine;

namespace HouseSim.Kitchen
{
    /// <summary>Side-effect-free rules shared by the physical food and the boundary checks.</summary>
    public static class KitchenFoodRules
    {
        public static FoodState GetState(FoodKind kind, float seconds, float goodAtSeconds, float burntAtSeconds)
        {
            if (kind == FoodKind.Salad) return FoodState.Good;
            if (kind != FoodKind.Chicken && kind != FoodKind.Steak) return FoodState.Raw;
            float good = Mathf.Max(0.01f, goodAtSeconds);
            float burnt = Mathf.Max(good + 0.01f, burntAtSeconds);
            return seconds >= burnt ? FoodState.Burnt : seconds >= good ? FoodState.Good : FoodState.Raw;
        }

        public static bool TryGetEnergyChange(FoodKind kind, FoodState state, bool dirty, out int change)
        {
            switch (kind)
            {
                case FoodKind.Chicken:
                    change = state == FoodState.Raw ? -15 : state == FoodState.Good ? 30 : -20;
                    return true;
                case FoodKind.Steak:
                    change = dirty ? -15 : state == FoodState.Raw ? 5 : state == FoodState.Good ? 25 : -15;
                    return true;
                case FoodKind.Salad:
                    change = dirty ? -15 : 20;
                    return true;
                default:
                    change = 0;
                    return false;
            }
        }

        public static float AdvanceWash(float progress, float seconds)
        {
            if (seconds <= 0f || float.IsNaN(seconds) || float.IsInfinity(seconds)) return progress;
            float next = Mathf.Clamp01(progress + seconds / 0.1f);
            // Compensate only for the float accumulation error of fixed-time wash ticks.
            return next >= 0.999999f ? 1f : next;
        }
    }
}
