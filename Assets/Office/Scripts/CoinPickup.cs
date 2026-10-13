using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace OfficeSim
{
    public sealed class CoinPickup : MonoBehaviour
    {
        private bool collected;

        void Awake()
        {
            GetComponent<XRSimpleInteractable>().firstSelectEntered.AddListener(_ => Collect());
        }

        public void Collect()
        {
            if (collected)
            {
                return;
            }

            collected = true;
            OfficeGame.Instance.inventory.Add(Resource.Cash, 5);
            OfficeGame.Instance.Message("Coin collected: +$5");
            OfficeGame.Instance.PlayGrab();
            gameObject.SetActive(false);
            Destroy(gameObject);
        }
    }
}
