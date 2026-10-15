using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HouseSim.Gym
{
    public enum ShopProductType
    {
        Supply,
        Equipment
    }

    /// <summary>One purchase path for both consumable supplies and placeable equipment.</summary>
    public sealed class ShopPurchaseButton : MonoBehaviour
    {
        [SerializeField] ShopProductType productType;
        [SerializeField] string productId;
        [SerializeField, Min(0)] int cost = 2;
        [SerializeField] PlayerResources playerResources;
        [SerializeField] GymSim gymSim;
        [SerializeField] QuestProgress questProgress;
        [SerializeField] AudioSource audioSource;
        [SerializeField] AudioClip purchaseSound;
        [SerializeField] AudioClip errorSound;
        [SerializeField] Button button;
        [SerializeField] TMP_Text nameLabel;
        [SerializeField] TMP_Text costLabel;

        float lastCoinValue = float.NaN;

        void Awake()
        {
            if (button == null) button = GetComponent<Button>();
            FindDependencies();
            Refresh();
        }

        void Update()
        {
            FindDependencies();
            if (playerResources == null) return;

            float coins = playerResources.Get("gymCoins");
            if (!Mathf.Approximately(coins, lastCoinValue)) Refresh();
        }

        public void Buy()
        {
            FindDependencies();
            if (!CanDeliverProduct() || playerResources == null || !playerResources.TrySpend("gymCoins", cost))
            {
                Play(errorSound);
                return;
            }

            bool delivered = productType == ShopProductType.Supply
                ? DeliverSupply()
                : gymSim.SpawnPurchasedEquipment(productId);

            if (!delivered)
            {
                playerResources.Add("gymCoins", cost);
                Play(errorSound);
                return;
            }

            Play(purchaseSound);
            questProgress?.Increment("buy", 1);
            Refresh();
        }

        bool DeliverSupply()
        {
            if (!playerResources.Contains(productId)) return false;
            playerResources.Add(productId, 1f);
            return true;
        }

        bool CanDeliverProduct()
        {
            if (string.IsNullOrWhiteSpace(productId)) return false;
            if (productType == ShopProductType.Supply)
                return playerResources != null &&
                       playerResources.Contains(productId) &&
                       playerResources.Get(productId) < playerResources[productId].max;
            return gymSim != null && gymSim.SupportsInventoryItem(productId);
        }

        void FindDependencies()
        {
            if (playerResources == null) playerResources = FindAnyObjectByType<PlayerResources>();
            if (gymSim == null) gymSim = FindAnyObjectByType<GymSim>();
            if (questProgress == null) questProgress = FindAnyObjectByType<QuestProgress>();
        }

        void Refresh()
        {
            if (costLabel != null) costLabel.text = $"{cost} COINS";
            if (playerResources == null) return;

            lastCoinValue = playerResources.Get("gymCoins");
            if (button != null) button.interactable = CanDeliverProduct() && lastCoinValue >= cost;
        }

        void Play(AudioClip clip)
        {
            if (audioSource != null && clip != null) audioSource.PlayOneShot(clip);
        }
    }
}
