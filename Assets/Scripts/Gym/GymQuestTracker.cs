using UnityEngine;

namespace HouseSim.Gym
{
    public class GymQuestTracker : MonoBehaviour
    {
        public PlayerResourceWallet wallet;
        public ResourceData gymCoinsResource;
        public bool armGainQuestCompleted { get; private set; }

        private void OnEnable()
        {
            if (wallet != null) wallet.ResourcesChanged += CheckArmGainQuest;
        }

        private void OnDisable()
        {
            if (wallet != null) wallet.ResourcesChanged -= CheckArmGainQuest;
        }

        public void BindWallet(PlayerResourceWallet playerWallet)
        {
            if (wallet != null) wallet.ResourcesChanged -= CheckArmGainQuest;
            wallet = playerWallet;
            if (isActiveAndEnabled && wallet != null) wallet.ResourcesChanged += CheckArmGainQuest;
            CheckArmGainQuest();
        }

        public void CheckArmGainQuest()
        {
            if (armGainQuestCompleted || wallet == null || wallet.GetValue("ArmGain") < 10f) return;
            if (gymCoinsResource == null)
            {
                Debug.LogError("Arm Gain quest is missing its GymCoins reward resource.", this);
                return;
            }

            armGainQuestCompleted = true;
            HouseEvents.ReportResourceGained(gymCoinsResource, 100);
        }
    }
}
