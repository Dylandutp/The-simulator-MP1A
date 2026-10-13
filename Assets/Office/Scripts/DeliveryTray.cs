using UnityEngine;

namespace OfficeSim
{
    public class DeliveryTray : MonoBehaviour
    {
        void OnTriggerEnter(Collider other)
        {
            Accept(other);
        }

        void OnTriggerStay(Collider other)
        {
            Accept(other);
        }

        void Accept(Collider other)
        {
            var item = other.GetComponentInParent<OfficeItem>();
            if (!item || item.Held || item.resource != Resource.CompletedFiles || !item.Consume())
            {
                return;
            }

            Deliver();
        }

        public void Deliver()
        {
            OfficeGame.Instance.inventory.Add(Resource.Cash, 20);
            OfficeGame.Instance.Message("Delivered! +$20");
            OfficeGame.Instance.Ping();
        }
    }
}
