using HouseSim;
using UnityEngine;

namespace HouseSim.Gym
{
    [CreateAssetMenu(fileName = "GymSimCatalog", menuName = "Gym Sim/Content Catalog")]
    public sealed class GymSimCatalog : ScriptableObject
    {
        public ResourceData[] sharedResources;
        public StatData[] sharedStats;
        public GymSimPrefabDefinition[] prefabs;

        public GameObject FindPrefab(string kind)
        {
            if (prefabs == null) return null;
            foreach (GymSimPrefabDefinition definition in prefabs)
                if (definition.kind == kind) return definition.prefab;
            return null;
        }
    }
}
