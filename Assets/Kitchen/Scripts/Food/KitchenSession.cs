using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace HouseSim.Kitchen
{
    public class KitchenSession : MonoBehaviour
    {
        public static KitchenSession Instance { get; private set; }
        public PlayerInventory inventory;
        public ResourceData energyResource;
        public ResourceData[] visibleResources;
        public TextMesh statusText;
        public TextMesh resourceText;
        public TextMesh energyText;
        public Transform mouthAnchor;
        public Transform recoveryAnchor;
        [Range(0, 100)] public int initialEnergy = 70;

        private readonly HashSet<string> shownTutorials = new HashSet<string>();
        private float nextDisplayUpdate;
        private int fallbackEnergy = 70;
        private bool createdInventory;
        public int Energy => inventory != null && energyResource != null
            ? Mathf.Clamp(inventory.GetAmount(energyResource), 0, 100) : fallbackEnergy;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogError("Only one KitchenSession may be active.", this);
                enabled = false;
                return;
            }
            Instance = this;
            if (inventory == null)
                inventory = FindFirstObjectByType<PlayerInventory>(FindObjectsInactive.Include);
            if (inventory == null)
            {
                var shared = new GameObject("House Shared Inventory");
                var master = UnityEngine.SceneManagement.SceneManager.GetSceneByName("MasterScene");
                if (master.IsValid() && master.isLoaded)
                    UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(shared, master);
                inventory = shared.AddComponent<PlayerInventory>();
                // A temporary room ledger must not also receive house-wide resource events while
                // MasterScene is loading its own inventory. Direct food transactions still work.
                if (!master.IsValid() || !master.isLoaded) inventory.enabled = false;
                createdInventory = true;
            }
            fallbackEnergy = Mathf.Clamp(initialEnergy, 0, 100);
            if (energyResource != null)
            {
                if (!inventory.HasResource(energyResource)) inventory.SetAmount(energyResource, fallbackEnergy);
                else fallbackEnergy = Mathf.Clamp(inventory.GetAmount(energyResource), 0, 100);
            }
        }

        private void Start() { RefreshDisplays(); }

        /// <summary>Called after an additive MasterScene load. Keeps the shared ledger outside this room.</summary>
        public bool AttachInventoryToMasterScene()
        {
            if (Instance != this || inventory == null) return false;
            var master = UnityEngine.SceneManagement.SceneManager.GetSceneByName("MasterScene");
            if (!master.IsValid() || !master.isLoaded) return false;
            if (inventory.gameObject.scene == master) return true;
            if (!createdInventory)
            {
                Debug.LogWarning("Kitchen cannot move an inventory owned by another scene object.", this);
                return false;
            }

            PlayerInventory existing = null;
            foreach (var candidate in FindObjectsByType<PlayerInventory>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (candidate != inventory && candidate.gameObject.scene == master) { existing = candidate; break; }

            if (existing == null)
            {
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(inventory.gameObject, master);
                inventory.enabled = true;
                return true;
            }

            // Reconcile the temporary ledger only after validating the whole addition. A pre-existing
            // energy value (including zero) belongs to the house and must not get another initial 70.
            var previous = inventory;
            var snapshot = previous.GetSnapshot();
            var rewards = new Dictionary<ResourceData, int>();
            foreach (var entry in snapshot)
            {
                if (entry.Key == null || (entry.Key == energyResource && existing.HasResource(energyResource))) continue;
                rewards.Add(entry.Key, entry.Value);
            }
            if (!existing.TryExchange(new Dictionary<ResourceData, int>(), rewards))
            {
                Debug.LogError("Kitchen could not transfer its temporary inventory into MasterScene.", this);
                return false;
            }
            inventory = existing;
            foreach (var food in FindObjectsByType<KitchenFood>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                food.RebindClaimInventory(previous, existing);
            foreach (var entry in snapshot) previous.SetAmount(entry.Key, 0);
            createdInventory = false;
            Destroy(previous.gameObject);
            if (energyResource != null)
            {
                if (!inventory.HasResource(energyResource)) inventory.SetAmount(energyResource, Mathf.Clamp(initialEnergy, 0, 100));
                fallbackEnergy = Mathf.Clamp(inventory.GetAmount(energyResource), 0, 100);
            }
            RefreshDisplays();
            return true;
        }

        private void Update()
        {
            if (Time.unscaledTime < nextDisplayUpdate) return;
            nextDisplayUpdate = Time.unscaledTime + 0.15f;
            RefreshDisplays();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void Notify(string message)
        {
            if (statusText != null) statusText.text = message;
            Debug.Log("[Kitchen] " + message, this);
        }

        public void Tutorial(string id, string message, Vector3 location)
        {
            if (string.IsNullOrEmpty(id) || !shownTutorials.Add(id)) return;
            var popup = new GameObject("Kitchen Tutorial - " + id);
            popup.transform.position = location + Vector3.up * 0.22f;
            var text = popup.AddComponent<TextMesh>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            popup.GetComponent<MeshRenderer>().sharedMaterial = text.font.material;
            text.text = message;
            text.fontSize = 48;
            text.characterSize = 0.012f;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.color = new Color(1f, 0.94f, 0.65f);
            popup.AddComponent<KitchenTutorialBillboard>();
            Destroy(popup, 7f);
        }

        public KitchenFood SpawnFood(GameObject prefab, Transform point, bool claimed = false)
        {
            if (prefab == null || point == null) return null;
            var spawned = Instantiate(prefab, point.position, point.rotation);
            var food = spawned.GetComponent<KitchenFood>();
            if (food == null)
            {
                Notify("Food prefab is missing KitchenFood.");
                Destroy(spawned);
                return null;
            }
            if (claimed && !food.Claim())
            {
                Destroy(spawned);
                return null;
            }
            return food;
        }

        public void ChangeEnergy(int amount)
        {
            int before = Energy;
            fallbackEnergy = Mathf.Clamp(before + amount, 0, 100);
            if (inventory != null && energyResource != null)
                inventory.SetAmount(energyResource, fallbackEnergy);
            RefreshDisplays();
        }

        public bool ExchangeOne(ResourceData from, ResourceData to)
        {
            if (inventory == null) return false;
            if (from == to) return true;
            var costs = new Dictionary<ResourceData, int>();
            var rewards = new Dictionary<ResourceData, int>();
            if (from != null) costs.Add(from, 1);
            if (to != null) rewards.Add(to, 1);
            bool success = inventory.TryExchange(costs, rewards);
            if (success) RefreshDisplays();
            return success;
        }

        public void RefreshDisplays()
        {
            if (energyText != null) energyText.text = "ENERGY  " + Energy + " / 100";
            if (resourceText == null || inventory == null) return;
            var lines = new StringBuilder("PLAYER INVENTORY\n");
            var unique = new HashSet<ResourceData>();
            if (visibleResources != null)
                foreach (var resource in visibleResources)
                    if (resource != null && unique.Add(resource))
                        lines.Append(resource.DisplayName).Append(": ").Append(inventory.GetAmount(resource)).Append('\n');
            resourceText.text = lines.ToString();
        }
    }

    internal class KitchenTutorialBillboard : MonoBehaviour
    {
        private void LateUpdate()
        {
            if (Camera.main != null) transform.rotation = Camera.main.transform.rotation;
        }
    }
}
