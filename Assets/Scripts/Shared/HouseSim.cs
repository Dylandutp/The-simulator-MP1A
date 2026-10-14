using System;
using UnityEngine;

namespace HouseSim
{
    [CreateAssetMenu(fileName = "NewResource", menuName = "House Sim/Resource")]
    public class ResourceData : ScriptableObject
    {
        public string resourceName;
        public float startingValue;
        public float minValue;
        public float maxValue = 100f;
        public Sprite icon;
    }

    [CreateAssetMenu(fileName = "NewStat", menuName = "House Sim/Stat")]
    public class StatData : ScriptableObject
    {
        public string statName;
        public float defaultValue;
        public float minValue;
        public float maxValue = 100f;
    }

    public static class HouseEvents
    {
        public static Action<ResourceData, int> OnResourceGained;
        public static Action<ResourceData, int> OnResourceSpent;
        public static Action<StatData, float, GameObject> OnStatChanged;

        public static void ReportResourceGained(ResourceData resource, int amount) => OnResourceGained?.Invoke(resource, amount);
        public static void ReportResourceSpent(ResourceData resource, int amount) => OnResourceSpent?.Invoke(resource, amount);
    }
}
