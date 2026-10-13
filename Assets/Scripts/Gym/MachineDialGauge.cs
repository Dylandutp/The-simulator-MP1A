using UnityEngine;

namespace HouseSim.Gym
{
    public class MachineDialGauge : MonoBehaviour
    {
        public MachineStats machineStats;
        public TextMesh gaugeText;

        private void Update()
        {
            if (machineStats == null || gaugeText == null) return;
            string primary = machineStats.primaryStat == null
                ? "N/A"
                : $"{machineStats.primaryStat.statName}: {machineStats.primaryStatValue:0.0}";
            string secondary = machineStats.secondaryStat == null
                ? string.Empty
                : $"\n{machineStats.secondaryStat.statName}: {machineStats.secondaryStatValue:0.0}";
            gaugeText.text = $"{machineStats.machineName}\n{primary}{secondary}\nDurability: {machineStats.durability:0}%";
        }
    }
}
