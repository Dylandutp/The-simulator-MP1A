using System;
using UnityEngine;

namespace HouseSim.Gym
{
    /// <summary>One clamped, observable runtime stat used by the gym simulation.</summary>
    [Serializable]
    public class Stat
    {
        public string name;
        public float value;
        public float min;
        public float max;
        public event Action<Stat> Changed;

        public Stat(string statName, float start, float minimum, float maximum)
        {
            name = statName;
            min = minimum;
            max = maximum;
            value = Mathf.Clamp(start, min, max);
        }

        public void Change(float amount)
        {
            float next = Mathf.Clamp(value + amount, min, max);
            if (Mathf.Approximately(next, value)) return;
            value = next;
            Changed?.Invoke(this);
        }

        public void Set(float next) => Change(next - value);
    }
}
