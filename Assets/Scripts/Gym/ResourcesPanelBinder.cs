using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace HouseSim.Gym
{
    [Serializable]
    public sealed class ResourceLabelBinding
    {
        public string resourceName;
        public string displayName;
        public TMP_Text label;
        public TMP_Text valueLabel;

        public ResourceLabelBinding(string name, string labelName = null)
        {
            resourceName = name;
            displayName = string.IsNullOrWhiteSpace(labelName) ? name : labelName;
        }
    }

    public sealed class ResourcesPanelBinder : MonoBehaviour
    {
        [SerializeField] PopupPanel popupPanel;
        [SerializeField] PlayerResources playerResources;
        [SerializeField] List<ResourceLabelBinding> labels = new List<ResourceLabelBinding>();

        void Awake()
        {
            if (playerResources == null) playerResources = FindAnyObjectByType<PlayerResources>();
            EnsureCardioBinding();
        }

        void Reset() => labels = CreateDefaultBindings();

        void Update()
        {
            if (playerResources == null) playerResources = FindAnyObjectByType<PlayerResources>();
            if ((popupPanel != null && !popupPanel.IsOpen) || playerResources == null) return;

            foreach (ResourceLabelBinding binding in labels)
            {
                if (binding == null || binding.label == null || string.IsNullOrWhiteSpace(binding.resourceName)) continue;
                string name = string.IsNullOrWhiteSpace(binding.displayName) ? binding.resourceName : binding.displayName;
                float value = playerResources.Get(binding.resourceName);
                if (binding.valueLabel != null)
                {
                    binding.label.text = name;
                    binding.valueLabel.text = $"<b>{Mathf.RoundToInt(value)}</b>";
                }
                else
                {
                    binding.label.text = $"{name}  <b>{Mathf.RoundToInt(value)}</b>";
                }
            }
        }

        static List<ResourceLabelBinding> CreateDefaultBindings()
        {
            (string resource, string label)[] names =
            {
                ("gymCoins", "Gym Coins"), ("energy", "Energy"), ("hydration", "Hydration"),
                ("health", "Health"), ("maxHealth", "Max Health"), ("stress", "Stress"),
                ("armGain", "Arm Gain"), ("backGain", "Back Gain"), ("chestGain", "Chest Gain"),
                ("legGain", "Leg Gain"), ("cardioLevel", "Cardio Level"), ("water", "Water"), ("powder", "Protein Powder"),
                ("energyStick", "Energy Stick"), ("milk", "Milk"), ("fruit", "Fruit")
            };

            var result = new List<ResourceLabelBinding>(names.Length);
            foreach ((string resource, string label) entry in names)
                result.Add(new ResourceLabelBinding(entry.resource, entry.label));
            return result;
        }

        void EnsureCardioBinding()
        {
            foreach (ResourceLabelBinding binding in labels)
                if (binding != null && binding.resourceName == "cardioLevel") return;

            ResourceLabelBinding source = labels.Find(binding => binding != null && binding.resourceName == "legGain");
            if (source == null || source.label == null) return;

            TMP_Text cardioLabel = Instantiate(source.label, source.label.transform.parent);
            cardioLabel.name = "Cardio Level Label";
            MoveDown(cardioLabel.rectTransform);
            TMP_Text cardioValue = null;
            if (source.valueLabel != null)
            {
                cardioValue = Instantiate(source.valueLabel, source.valueLabel.transform.parent);
                cardioValue.name = "Cardio Level Value";
                MoveDown(cardioValue.rectTransform);
            }
            labels.Add(new ResourceLabelBinding("cardioLevel", "Cardio Level")
            {
                label = cardioLabel,
                valueLabel = cardioValue
            });
        }

        static void MoveDown(RectTransform item)
        {
            if (item != null) item.anchoredPosition += Vector2.down * .098f;
        }
    }
}
