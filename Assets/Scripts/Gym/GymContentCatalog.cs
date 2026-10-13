using System;
using UnityEngine;

namespace HouseSim.Gym
{
    [Serializable]
    public class GymMachineDefinition
    {
        public string machineName;
        public StatData primaryStat;
        public StatData secondaryStat;
        public ResourceData gainResource;
        public GameObject prefab;
        public int buildCost = 25;
        public Vector3 dimensions = new Vector3(1.2f, 1.5f, 1.2f);
    }

    [CreateAssetMenu(fileName = "GymContentCatalog", menuName = "House Sim/Gym Content Catalog")]
    public class GymContentCatalog : ScriptableObject
    {
        public ResourceData[] playerResources;
        public StatData[] utilityStats;
        public GameObject[] utilityPrefabs;
        public GymMachineDefinition[] workoutMachines;

        public ResourceData GetResource(string resourceName)
        {
            if (playerResources == null) return null;
            foreach (ResourceData resource in playerResources)
            {
                if (resource != null && resource.resourceName == resourceName) return resource;
            }

            return null;
        }

        public StatData GetStat(string statName)
        {
            if (workoutMachines != null)
            {
                foreach (GymMachineDefinition machine in workoutMachines)
                {
                    if (machine.primaryStat != null && machine.primaryStat.statName == statName)
                        return machine.primaryStat;
                    if (machine.secondaryStat != null && machine.secondaryStat.statName == statName)
                        return machine.secondaryStat;
                }
            }

            if (utilityStats != null)
            {
                foreach (StatData stat in utilityStats)
                {
                    if (stat != null && stat.statName == statName) return stat;
                }
            }

            return null;
        }
    }
}
