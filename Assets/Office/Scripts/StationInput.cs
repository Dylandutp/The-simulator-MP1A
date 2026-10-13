using UnityEngine;

namespace OfficeSim
{
    public class StationInput : MonoBehaviour
    {
        public Workstation station;

        private void OnTriggerEnter(Collider other)
        {
            Accept(other);
        }

        private void OnTriggerStay(Collider other)
        {
            Accept(other);
        }

        void Accept(Collider other)
        {
            var item = other.GetComponentInParent<OfficeItem>();
            if (item && !item.Held)
            {
                station.Accept(item);
            }
        }
    }
}
