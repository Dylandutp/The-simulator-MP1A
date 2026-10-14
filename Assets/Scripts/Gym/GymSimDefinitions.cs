using System;
using UnityEngine;

namespace HouseSim.Gym
{
    [Serializable]
    public struct GymSimResourceDefinition
    {
        public string name;
        public float start;
        public float min;
        public float max;

        public GymSimResourceDefinition(string statName, float startingValue, float minimum, float maximum)
        {
            name = statName;
            start = startingValue;
            min = minimum;
            max = maximum;
        }
    }

    [Serializable]
    public struct GymSimPrefabDefinition
    {
        public string kind;
        public GameObject prefab;
    }
}
