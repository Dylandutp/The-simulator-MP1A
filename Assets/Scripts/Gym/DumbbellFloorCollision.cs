using UnityEngine;

namespace HouseSim.Gym
{
    [RequireComponent(typeof(Rigidbody))]
    public class DumbbellFloorCollision : MonoBehaviour
    {
        private bool landed;
        private bool armed;

        public void ArmForDrop()
        {
            landed = false;
            armed = true;
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (!armed || landed || collision.contactCount == 0 ||
                collision.GetContact(0).normal.y < 0.6f)
            {
                return;
            }

            if (collision.collider.GetComponentInParent<RubberMatFoundation>() != null)
            {
                landed = true;
                armed = false;
                return;
            }

            landed = true;
            Destroy(gameObject);
        }
    }
}
