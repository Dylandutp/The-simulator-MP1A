using System;
using UnityEditor;
using UnityEngine;

namespace HouseSim.Kitchen.Editor
{
    /// <summary>Pure rule checks. Physical object/inventory lifetime checks require the Play Mode harness.</summary>
    public static class KitchenFoodChecks
    {
        [MenuItem("Kitchen/Validate Food Rules")]
        public static void Run()
        {
            ExpectMeal(FoodKind.Chicken, 0f, 20f, 40f, FoodState.Raw, -15);
            ExpectMeal(FoodKind.Chicken, 19.999f, 20f, 40f, FoodState.Raw, -15);
            ExpectMeal(FoodKind.Chicken, 20f, 20f, 40f, FoodState.Good, 30);
            ExpectMeal(FoodKind.Chicken, 39.999f, 20f, 40f, FoodState.Good, 30);
            ExpectMeal(FoodKind.Chicken, 40f, 20f, 40f, FoodState.Burnt, -20);
            ExpectMeal(FoodKind.Steak, 0f, 8f, 18f, FoodState.Raw, 5);
            ExpectMeal(FoodKind.Steak, 7.999f, 8f, 18f, FoodState.Raw, 5);
            ExpectMeal(FoodKind.Steak, 8f, 8f, 18f, FoodState.Good, 25);
            ExpectMeal(FoodKind.Steak, 17.999f, 8f, 18f, FoodState.Good, 25);
            ExpectMeal(FoodKind.Steak, 18f, 8f, 18f, FoodState.Burnt, -15);

            Check(KitchenFoodRules.TryGetEnergyChange(FoodKind.Salad, FoodState.Good, false, out int clean) && clean == 20, "Clean salad gives 20.");
            Check(KitchenFoodRules.TryGetEnergyChange(FoodKind.Salad, FoodState.Good, true, out int dirty) && dirty == -15, "Contaminated salad costs 15.");
            foreach (var produce in new[] { FoodKind.Tomato, FoodKind.Lettuce, FoodKind.Apple })
                Check(!KitchenFoodRules.TryGetEnergyChange(produce, FoodState.Raw, false, out _), "Raw produce cannot be eaten as a finished dish.");

            Check(KitchenFoodRules.AdvanceWash(0f, 2.99f) < 1f, "Washing under 3 seconds remains incomplete.");
            Check(KitchenFoodRules.AdvanceWash(0f, 3f) == 1f, "Exactly 3 seconds completes washing.");
            Check(KitchenFoodRules.AdvanceWash(KitchenFoodRules.AdvanceWash(0f, 1.5f), 1.5f) == 1f, "Washing can be split around chopping.");
            float continuous = 0f;
            for (int tick = 0; tick < 150; tick++) continuous = KitchenFoodRules.AdvanceWash(continuous, 0.02f);
            Check(continuous == 1f, "150 physics ticks complete 3 seconds of washing.");
            Check(KitchenFoodRules.AdvanceWash(1f, 12f) == 1f, "Wash progress cannot exceed 100 percent.");
            Check(KitchenFoodRules.AdvanceWash(0.5f, -1f) == 0.5f, "Negative time cannot undo washing.");
            Check(KitchenFoodRules.AdvanceWash(0.5f, float.NaN) == 0.5f, "NaN cannot corrupt washing.");
            Debug.Log("KITCHEN_FOOD_RULES_PASS: cooking boundaries, meal effects, and wash accumulation. Physical inventory conservation must additionally pass Play Mode checks.");
        }

        private static void ExpectMeal(FoodKind kind, float seconds, float good, float burnt, FoodState expectedState, int expectedEnergy)
        {
            var state = KitchenFoodRules.GetState(kind, seconds, good, burnt);
            Check(state == expectedState, kind + " state at " + seconds + " seconds.");
            Check(KitchenFoodRules.TryGetEnergyChange(kind, state, false, out int energy) && energy == expectedEnergy,
                kind + " energy at " + seconds + " seconds.");
        }

        private static void Check(bool success, string message)
        {
            if (!success) throw new InvalidOperationException("Kitchen food rule check failed: " + message);
        }
    }
}
