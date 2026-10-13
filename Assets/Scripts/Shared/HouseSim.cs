using UnityEngine;
using System;

namespace HouseSim
{
    // These define the ScriptableObjects used to replace Enums/Strings
    [CreateAssetMenu(fileName = "NewResource", menuName = "House Sim/Resource")]
    public class ResourceData : ScriptableObject
    {
        public string resourceName;
        public float startingValue;
        public Sprite icon;
    }

    [CreateAssetMenu(fileName = "NewStat", menuName = "House Sim/Stat")]
    public class StatData : ScriptableObject
    {
        public string statName;
        public float defaultValue;
    }

    // The Event Bus for cross-room communication
    public static class HouseEvents
    {
        public static Action<ResourceData, int> OnResourceGained;
        public static Action<ResourceData, int> OnResourceSpent;
        public static Action<StatData, float, GameObject> OnStatChanged;

        public static void ReportResourceGained(ResourceData resource, int amount)
        {
            OnResourceGained?.Invoke(resource, amount);
        }

        public static void ReportResourceSpent(ResourceData resource, int amount)
        {
            OnResourceSpent?.Invoke(resource, amount);
        }
    }
}