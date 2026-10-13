using UnityEngine;

namespace HouseSim.Gym
{
    public class RustyDumbbells : MonoBehaviour
    {
        public StatData massStat;
        private Rigidbody body;
        private DumbbellFloorCollision floorCollision;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            floorCollision = GetComponent<DumbbellFloorCollision>();
            if (body != null)
            {
                body.isKinematic = true;
                body.useGravity = false;
            }
        }

        public void ReleaseFromHand()
        {
            if (body == null) body = GetComponent<Rigidbody>();
            if (floorCollision == null) floorCollision = GetComponent<DumbbellFloorCollision>();
            if (body == null || floorCollision == null)
            {
                Debug.LogError("Rusty Dumbbells need a Rigidbody and floor collision handler.", this);
                return;
            }

            floorCollision.ArmForDrop();
            body.isKinematic = false;
            body.useGravity = true;
        }
    }
}
