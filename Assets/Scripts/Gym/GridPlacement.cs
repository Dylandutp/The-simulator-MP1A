using UnityEngine;

namespace HouseSim.Gym
{
    public class GridPlacement : MonoBehaviour
    {
        public GymGridBuilder builder;
        public Vector2Int gridCell;

        private void OnDestroy()
        {
            if (builder != null) builder.ReleaseMachine(gridCell);
        }
    }
}
