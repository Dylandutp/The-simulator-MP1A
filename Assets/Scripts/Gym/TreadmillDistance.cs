using UnityEngine;

namespace HouseSim.Gym
{
    public class TreadmillDistance : MonoBehaviour
    {
        public MachineStats machineStats;
        public StatData distanceStat; // Drag your 'MilesRun' SO here
        public bool isRunning;
        public float speedMPH = 5.0f; // Miles per hour
        public Renderer beltRenderer;
        public float beltScrollSpeed = 1.5f;
        public WorkoutMachine workoutMachine;
        private float exerciseTimer;

        private void Update()
        {
            if (!isRunning || machineStats == null || distanceStat == null ||
                machineStats.IsBroken || speedMPH <= 0f ||
                (PlayerResourceWallet.Instance != null && PlayerResourceWallet.Instance.IsDead))
            {
                return;
            }

            float deltaTime = Time.deltaTime;
            // Euler integration converts the stat's mph value to miles per second.
            float distanceGained = (speedMPH / 3600f) * deltaTime;
            machineStats.ModifyStat(distanceStat, distanceGained);

            exerciseTimer += deltaTime;
            if (exerciseTimer >= 1f)
            {
                exerciseTimer -= 1f;
                workoutMachine?.PerformExerciseRep();
            }

            if (beltRenderer != null)
            {
                Vector2 offset = beltRenderer.material.mainTextureOffset;
                offset.y += beltScrollSpeed * deltaTime;
                beltRenderer.material.mainTextureOffset = offset;
            }
        }

        public void ToggleRunning() => isRunning = !isRunning;
    }
}