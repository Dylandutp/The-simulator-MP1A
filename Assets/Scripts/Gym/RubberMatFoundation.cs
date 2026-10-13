using UnityEngine;

namespace HouseSim.Gym
{
    public class RubberMatFoundation : MonoBehaviour
    {
        public Vector2Int gridCell;
        public GymGridBuilder builder;

        private void OnDestroy()
        {
            if (builder != null) builder.ReleaseMat(gridCell);
        }
    }
}
