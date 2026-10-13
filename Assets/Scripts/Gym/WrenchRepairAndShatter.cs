using UnityEngine;

namespace HouseSim.Gym
{
    public class WrenchRepairAndShatter : MonoBehaviour
    {
        public ResourceData gymCoinsResource;
        public StatData repairPowerStat;

        private void OnTriggerEnter(Collider other)
        {
            RustyDumbbells dumbbells = other.GetComponentInParent<RustyDumbbells>();
            if (dumbbells != null)
            {
                AwardCoins(5);
                Destroy(dumbbells.gameObject);
                return;
            }

            MachineStats machine = other.GetComponentInParent<MachineStats>();
            if (machine == null || machine.secondaryStat == null) return;

            if (machine.IsBroken)
            {
                AwardCoins(10);
                Destroy(machine.gameObject);
                return;
            }

            if (machine.durability < 100f)
                machine.RepairToMaximum();
        }

        private void AwardCoins(int amount)
        {
            if (gymCoinsResource == null)
            {
                Debug.LogError("Wrench is missing its GymCoins resource.", this);
                return;
            }

            HouseEvents.ReportResourceGained(gymCoinsResource, amount);
        }
    }
}