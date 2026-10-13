using UnityEngine;

namespace HouseSim.Gym
{
    public class WorkoutMachine : MonoBehaviour
    {
        public ResourceData targetGainResource; 
        public ResourceData energyResource;
        public ResourceData hydrationResource;
        public ResourceData stressResource;

        public MachineStats machineStats;
        public float gainMultiplier = 1.0f;

        public bool PerformExerciseRep()
        {
            if (machineStats == null || machineStats.IsBroken) return false;
            if (PlayerResourceWallet.Instance == null)
            {
                Debug.LogError("Cannot exercise because the player resource wallet is missing.", this);
                return false;
            }
            if (PlayerResourceWallet.Instance.IsDead) return false;

            bool hasEnergy = PlayerResourceWallet.Instance.TrySpend(energyResource, 5f);
            bool hasHydration = hasEnergy &&
                                PlayerResourceWallet.Instance.TrySpend(hydrationResource, 5f);
            if (!hasEnergy || !hasHydration)
            {
                if (PlayerResourceWallet.Instance.TryGetResource("Health", out ResourceData health))
                    HouseEvents.ReportResourceSpent(health, 10);
                return false;
            }

            int gainsYield = Mathf.RoundToInt(10 * gainMultiplier);
            HouseEvents.ReportResourceGained(targetGainResource, gainsYield);
            PlayerResourceWallet.Instance.TrySpend(stressResource, 10f);

            if (machineStats.secondaryStat != null &&
                machineStats.secondaryStat.statName.EndsWith("Durability", System.StringComparison.Ordinal))
                machineStats.ModifyStat(machineStats.secondaryStat, -5f);
            else
                machineStats.ModifyDurability(-5f);

            return true;
        }
    }
}