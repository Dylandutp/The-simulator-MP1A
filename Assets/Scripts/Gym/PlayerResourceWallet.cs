using System;
using System.Collections.Generic;
using UnityEngine;

namespace HouseSim.Gym
{
    public class PlayerResourceWallet : MonoBehaviour
    {
        public static PlayerResourceWallet Instance { get; private set; }
        public event Action ResourcesChanged;
        public event Action PlayerDied;
        public bool IsDead => isDead;

        private readonly Dictionary<ResourceData, float> values = new Dictionary<ResourceData, float>();
        private ResourceData healthResource;
        private ResourceData maxHealthResource;
        private bool isDead;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        private void OnEnable()
        {
            HouseEvents.OnResourceGained += HandleResourceGained;
            HouseEvents.OnResourceSpent += HandleResourceSpent;
        }

        private void OnDisable()
        {
            HouseEvents.OnResourceGained -= HandleResourceGained;
            HouseEvents.OnResourceSpent -= HandleResourceSpent;
            if (Instance == this) Instance = null;
        }

        public void Initialize(GymContentCatalog catalog)
        {
            if (catalog == null)
            {
                Debug.LogError("Cannot initialize the wallet without a gym content catalog.", this);
                return;
            }

            if (catalog.playerResources == null)
            {
                Debug.LogError("The gym content catalog has no player resource list.", catalog);
                return;
            }

            values.Clear();
            foreach (ResourceData resource in catalog.playerResources)
            {
                if (resource == null)
                {
                    Debug.LogError("The gym content catalog contains a missing player resource.", catalog);
                    continue;
                }

                values[resource] = resource.startingValue;
                if (resource.resourceName == "Health") healthResource = resource;
                if (resource.resourceName == "MaxHealth") maxHealthResource = resource;
            }

            if (healthResource == null || maxHealthResource == null)
            {
                Debug.LogError("The gym content catalog must include Health and MaxHealth resources.", catalog);
                return;
            }

            ClampHealth();
            ResourcesChanged?.Invoke();
        }

        public bool TryGetResource(string resourceName, out ResourceData resource)
        {
            resource = null;
            foreach (ResourceData candidate in values.Keys)
            {
                if (candidate.resourceName != resourceName) continue;
                resource = candidate;
                return true;
            }

            return false;
        }

        public float GetValue(ResourceData resource)
        {
            if (resource == null || !values.TryGetValue(resource, out float value)) return 0f;
            return value;
        }

        public float GetValue(string resourceName)
        {
            return TryGetResource(resourceName, out ResourceData resource) ? GetValue(resource) : 0f;
        }

        public bool TrySpend(ResourceData resource, float amount)
        {
            if (resource == null || amount < 0f || !values.TryGetValue(resource, out float current))
            {
                Debug.LogError("Cannot spend an unconfigured or invalid player resource.", this);
                return false;
            }

            if (current < amount) return false;
            HouseEvents.ReportResourceSpent(resource, Mathf.CeilToInt(amount));
            return true;
        }

        private void HandleResourceGained(ResourceData resource, int amount)
        {
            if (resource == null || amount < 0)
            {
                Debug.LogError("Received an invalid resource gain event.", this);
                return;
            }

            if (!values.ContainsKey(resource))
            {
                Debug.LogError($"Resource '{resource.resourceName}' is not configured in the gym catalog.", this);
                return;
            }

            values[resource] += amount;
            ClampHealth();
            PublishChange();
        }

        private void HandleResourceSpent(ResourceData resource, int amount)
        {
            if (resource == null || amount < 0)
            {
                Debug.LogError("Received an invalid resource spend event.", this);
                return;
            }

            if (!values.TryGetValue(resource, out float current))
            {
                Debug.LogError($"Resource '{resource.resourceName}' is not configured in the gym catalog.", this);
                return;
            }

            values[resource] = Mathf.Max(0f, current - amount);
            ClampHealth();
            PublishChange();
        }

        private void ClampHealth()
        {
            if (healthResource == null || maxHealthResource == null ||
                !values.ContainsKey(healthResource) || !values.ContainsKey(maxHealthResource))
            {
                return;
            }

            values[healthResource] = Mathf.Min(
                values[healthResource], Mathf.Max(0f, values[maxHealthResource]));

            if (!isDead && values[healthResource] <= 0f)
            {
                isDead = true;
                PlayerDied?.Invoke();
            }
        }

        private void PublishChange()
        {
            ResourcesChanged?.Invoke();
        }
    }
}
