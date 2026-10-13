using System.Collections.Generic;
using UnityEngine;

namespace HouseSim.Gym
{
    public enum GymItem
    {
        WaterBottle,
        EnergyBar,
        EnergyDrink,
        MetalScraps,
        ProteinShake
    }

    public enum GymRecipe
    {
        WaterBottle,
        EnergyBar,
        EnergyDrink,
        ProteinShake,
        MetalScraps
    }

    public class GymInventory : MonoBehaviour
    {
        private struct Recipe
        {
            public readonly GymItem inputA;
            public readonly int amountA;
            public readonly GymItem inputB;
            public readonly int amountB;
            public readonly GymItem output;
            public readonly int outputAmount;

            public Recipe(GymItem inputA, int amountA, GymItem inputB, int amountB,
                GymItem output, int outputAmount)
            {
                this.inputA = inputA;
                this.amountA = amountA;
                this.inputB = inputB;
                this.amountB = amountB;
                this.output = output;
                this.outputAmount = outputAmount;
            }
        }

        public static GymInventory Instance { get; private set; }
        private readonly Dictionary<GymItem, int> amounts = new Dictionary<GymItem, int>();
        private readonly Dictionary<GymRecipe, Recipe> recipes = new Dictionary<GymRecipe, Recipe>
        {
            { GymRecipe.WaterBottle, new Recipe(GymItem.MetalScraps, 2, GymItem.MetalScraps, 0, GymItem.WaterBottle, 1) },
            { GymRecipe.EnergyBar, new Recipe(GymItem.MetalScraps, 1, GymItem.WaterBottle, 1, GymItem.EnergyBar, 1) },
            { GymRecipe.EnergyDrink, new Recipe(GymItem.EnergyBar, 1, GymItem.WaterBottle, 1, GymItem.EnergyDrink, 1) },
            { GymRecipe.ProteinShake, new Recipe(GymItem.EnergyDrink, 1, GymItem.EnergyBar, 1, GymItem.ProteinShake, 1) },
            { GymRecipe.MetalScraps, new Recipe(GymItem.EnergyDrink, 1, GymItem.WaterBottle, 0, GymItem.MetalScraps, 2) }
        };

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            foreach (GymItem item in System.Enum.GetValues(typeof(GymItem))) amounts[item] = 0;
            amounts[GymItem.MetalScraps] = 4;
            amounts[GymItem.WaterBottle] = 2;
            amounts[GymItem.EnergyBar] = 2;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public int GetAmount(GymItem item) => amounts.TryGetValue(item, out int amount) ? amount : 0;

        public void Grant(GymItem item, int amount)
        {
            if (amount <= 0)
            {
                Debug.LogError("Inventory grants must have a positive amount.", this);
                return;
            }

            amounts[item] = GetAmount(item) + amount;
        }

        public bool TrySpend(GymItem item, int amount)
        {
            if (amount <= 0 || GetAmount(item) < amount) return false;
            amounts[item] -= amount;
            return true;
        }

        public bool Craft(GymRecipe recipeName)
        {
            if (!recipes.TryGetValue(recipeName, out Recipe recipe))
            {
                Debug.LogError($"No crafting recipe is configured for '{recipeName}'.", this);
                return false;
            }

            if (GetAmount(recipe.inputA) < recipe.amountA || GetAmount(recipe.inputB) < recipe.amountB)
                return false;

            TrySpend(recipe.inputA, recipe.amountA);
            if (recipe.amountB > 0) TrySpend(recipe.inputB, recipe.amountB);
            Grant(recipe.output, recipe.outputAmount);
            return true;
        }
    }
}
