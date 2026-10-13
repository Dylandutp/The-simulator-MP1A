using UnityEngine;

namespace HouseSim.Gym
{
    public class WaterDispenser : MonoBehaviour
    {
        private const float WaterPerBottle = 10f;

        public MachineStats dispenserStats;

        public void HarvestWater()
        {
            if (dispenserStats == null || dispenserStats.primaryStat == null ||
                dispenserStats.primaryStat.statName != "WaterReservoir")
            {
                Debug.LogError("Water dispenser must have a WaterReservoir primary stat.", this);
                return;
            }

            if (dispenserStats.primaryStatValue < WaterPerBottle)
            {
                Debug.Log("Not enough water remains to fill a bottle.", this);
                return;
            }

            dispenserStats.ModifyStat(dispenserStats.primaryStat, -WaterPerBottle);
            GymInventory.Instance?.Grant(GymItem.WaterBottle, 1);
            Debug.Log("Dispensed 1 Water Bottle.", this);
        }
    }
}