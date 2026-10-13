using UnityEngine;

namespace HouseSim.Gym
{
    public class ProteinBlenderCrafting : MonoBehaviour
    {
        public ResourceData hydrationResource;
        public ResourceData stressResource;

        public bool Craft(GymRecipe recipe)
        {
            if (GymInventory.Instance == null)
            {
                Debug.LogError("Cannot craft because the gym inventory is missing.", this);
                return false;
            }

            return GymInventory.Instance.Craft(recipe);
        }

        public void ConsumeProteinShake()
        {
            if (GymInventory.Instance == null || !GymInventory.Instance.TrySpend(GymItem.ProteinShake, 1))
                return;

            HouseEvents.ReportResourceGained(hydrationResource, 25);
            HouseEvents.ReportResourceSpent(stressResource, 20);
        }
    }
}