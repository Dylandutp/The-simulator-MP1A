using UnityEngine;

namespace HouseSim.Gym
{
    public class CoinCrate : MonoBehaviour
    {
        public ResourceData gymCoinsResource;
        public int coinAmount = 25;
        private bool collected;

        public void Collect()
        {
            if (collected) return;
            if (gymCoinsResource == null)
            {
                Debug.LogError("Coin crate is missing its GymCoins resource.", this);
                return;
            }

            collected = true;
            HouseEvents.ReportResourceGained(gymCoinsResource, coinAmount);
            Destroy(gameObject);
        }
    }
}
