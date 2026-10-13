using UnityEngine;

namespace HouseSim.Gym
{
    public class NeighborInteraction : MonoBehaviour
    {
        public enum InteractionType { WaterBottleTreadmill, FanTreadmill, BenchChestFly }
        public InteractionType interactionType;

        public MachineStats sourceMachine;
        public MachineStats targetMachine;
        public ResourceData hydrationResource;

        public float waterDrainPerSecond = 2f;
        public float hydrationPerSecond = 1f;

        private float hydrationRemainder;
        private float defaultTreadmillSpeed;
        private float defaultGainMultiplier;
        private bool defaultsCaptured;

        private void CaptureDefaults()
        {
            if (defaultsCaptured || targetMachine == null) return;

            TreadmillDistance treadmill = targetMachine.GetComponent<TreadmillDistance>();
            WorkoutMachine workout = targetMachine.GetComponent<WorkoutMachine>();
            if (treadmill != null) defaultTreadmillSpeed = treadmill.speedMPH;
            if (workout != null) defaultGainMultiplier = workout.gainMultiplier;
            defaultsCaptured = true;
        }

        private void Update()
        {
            CaptureDefaults();
            if (sourceMachine == null || targetMachine == null)
            {
                ResetBonuses();
                return;
            }

            if (Vector3.Distance(sourceMachine.transform.position, targetMachine.transform.position) <= 2.0f)
            {
                switch (interactionType)
                {
                    case InteractionType.WaterBottleTreadmill:
                        ApplyWaterBottleBonus();
                        break;
                        
                    case InteractionType.FanTreadmill:
                        TreadmillDistance treadmill = targetMachine.GetComponent<TreadmillDistance>();
                        if (treadmill != null) treadmill.speedMPH = 8.0f;
                        break;
                        
                    case InteractionType.BenchChestFly:
                        WorkoutMachine workout = targetMachine.GetComponent<WorkoutMachine>();
                        if (workout != null) workout.gainMultiplier = 1.5f;
                        break;
                }
            }
            else
            {
                ResetBonuses();
            }
        }

        private void ApplyWaterBottleBonus()
        {
            TreadmillDistance treadmill = targetMachine.GetComponent<TreadmillDistance>();
            if (treadmill == null || !treadmill.isRunning ||
                sourceMachine.primaryStat == null || hydrationResource == null ||
                waterDrainPerSecond <= 0f || hydrationPerSecond < 0f)
            {
                return;
            }

            float availableTime = sourceMachine.primaryStatValue / waterDrainPerSecond;
            float elapsed = Mathf.Min(Time.deltaTime, availableTime);
            if (elapsed <= 0f) return;

            sourceMachine.ModifyStat(sourceMachine.primaryStat, -waterDrainPerSecond * elapsed);
            hydrationRemainder += hydrationPerSecond * elapsed;

            int hydrationGained = Mathf.FloorToInt(hydrationRemainder);
            if (hydrationGained > 0)
            {
                HouseEvents.ReportResourceGained(hydrationResource, hydrationGained);
                hydrationRemainder -= hydrationGained;
            }
        }

        private void ResetBonuses()
        {
            if (!defaultsCaptured || targetMachine == null) return;

            if (interactionType == InteractionType.FanTreadmill)
            {
                TreadmillDistance treadmill = targetMachine.GetComponent<TreadmillDistance>();
                if (treadmill != null) treadmill.speedMPH = defaultTreadmillSpeed;
            }
            else if (interactionType == InteractionType.BenchChestFly)
            {
                WorkoutMachine workout = targetMachine.GetComponent<WorkoutMachine>();
                if (workout != null) workout.gainMultiplier = defaultGainMultiplier;
            }
        }

        private void OnDisable()
        {
            ResetBonuses();
        }
    }
}