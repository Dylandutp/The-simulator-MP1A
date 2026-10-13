using UnityEngine;

namespace HouseSim.Gym
{
    public class MachineStats : MonoBehaviour
    {
        public string machineName;
        public StatData primaryStat;
        public float primaryStatValue = 100f;

        public StatData secondaryStat;
        public float secondaryStatValue = 100f;
        [Range(0f, 100f)] public float durability = 100f;

        private void Awake()
        {
            ResetValues();
        }

        public void Configure(string displayName, StatData primary, StatData secondary)
        {
            machineName = displayName;
            primaryStat = primary;
            secondaryStat = secondary;
            ResetValues();
        }

        private void ResetValues()
        {
            if (primaryStat != null) primaryStatValue = primaryStat.defaultValue;
            if (secondaryStat != null) secondaryStatValue = secondaryStat.defaultValue;
            durability = 100f;
        }

        public void ModifyStat(StatData stat, float delta)
        {
            if (stat == primaryStat)
            {
                primaryStatValue = ClampStatValue(stat, primaryStatValue + delta);
                HouseEvents.OnStatChanged?.Invoke(primaryStat, primaryStatValue, gameObject);
            }
            else if (stat == secondaryStat)
            {
                secondaryStatValue = ClampStatValue(stat, secondaryStatValue + delta);
                if (IsDurabilityStat(stat)) durability = secondaryStatValue;
                HouseEvents.OnStatChanged?.Invoke(secondaryStat, secondaryStatValue, gameObject);
            }
        }

        public bool IsBroken => durability <= 0f;

        public void ModifyDurability(float delta)
        {
            durability = Mathf.Clamp(durability + delta, 0f, 100f);
            if (IsDurabilityStat(secondaryStat))
            {
                secondaryStatValue = durability;
                HouseEvents.OnStatChanged?.Invoke(secondaryStat, durability, gameObject);
            }
        }

        public void RepairToMaximum()
        {
            if (IsDurabilityStat(secondaryStat))
            {
                float repairAmount = secondaryStat.defaultValue - secondaryStatValue;
                if (repairAmount > 0f) ModifyStat(secondaryStat, repairAmount);
                return;
            }

            ModifyDurability(100f - durability);
        }

        private static bool IsDurabilityStat(StatData stat) =>
            stat != null && stat.statName.EndsWith("Durability", System.StringComparison.Ordinal);

        private static float ClampStatValue(StatData stat, float value)
        {
            float nonNegativeValue = Mathf.Max(0f, value);
            return stat.statName == "MilesRun"
                ? nonNegativeValue
                : Mathf.Min(stat.defaultValue, nonNegativeValue);
        }
    }
}