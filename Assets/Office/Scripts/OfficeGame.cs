using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace OfficeSim
{
    public class OfficeGame : MonoBehaviour
    {
        public static OfficeGame Instance { get; private set; }

        public ResourceInventory inventory = new ResourceInventory();
        public OfficeItem[] itemPrefabs = new OfficeItem[8];
        public Workstation[] machinePrefabs;
        public ConveyorBelt[] conveyorPrefabs;
        public DeliveryTray delivery;
        public readonly List<ConveyorBelt> conveyors = new List<ConveyorBelt>();
        public GridPlacement placement;
        public Transform supplyCounter;
        public readonly List<Workstation> stations = new List<Workstation>();
        private readonly int[] physical = new int[8];
        public bool HasCompletedFiles => physical[(int)Resource.CompletedFiles] > 0;
        private readonly HashSet<string> seenTutorials = new HashSet<string>();
        public Text inventoryText;
        public Text messageText;
        private TutorialTrigger activeTutorial;
        [Header("Audio feedback")]
        public AudioSource audioSource;
        public AudioClip buttonClickClip;
        public AudioClip grabClip;
        [Range(0, 1)] public float buttonVolume = 0.4f;
        [Range(0, 1)] public float grabVolume = 0.3f;
        public AudioClip completedClip;

        void Awake()
        {
            Instance = this;
            inventory.Initialize();
        }

        void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        void Update()
        {
            inventoryText.text = $"Cash  ${inventory[Resource.Cash]}\n" +
                $"Paper {inventory[Resource.Paper]}   Ink {inventory[Resource.Ink]}   Stamp Pads {inventory[Resource.StampPads]}   Folders {inventory[Resource.Folders]}\n" +
                $"Drafts {inventory[Resource.Drafts]}   Approved {inventory[Resource.ApprovedDocuments]}   Completed {inventory[Resource.CompletedFiles]}";
        }

        public int Available(Resource resource)
        {
            return inventory[resource] - physical[(int)resource];
        }

        public void RegisterPhysical(Resource resource, bool credit)
        {
            if (credit)
            {
                inventory.Add(resource, 1);
            }

            physical[(int)resource]++;
        }

        public bool ConsumePhysical(Resource resource)
        {
            if (physical[(int)resource] <= 0 || !inventory.Spend(resource, 1))
            {
                return false;
            }

            physical[(int)resource]--;
            return true;
        }

        public OfficeItem SpawnItem(Resource resource, Vector3 position, bool credit)
        {
            var prefab = itemPrefabs[(int)resource];
            var item = Instantiate(prefab, position, Quaternion.identity);
            item.name = OfficeRules.Label(resource);
            item.Initialize(resource, credit);
            return item;
        }

        public void Dispense(Resource resource)
        {
            if (Available(resource) <= 0)
            {
                Message("No stored " + OfficeRules.Label(resource) + ". Buy more from the item store.");
                return;
            }

            SpawnItem(resource, supplyCounter.position + Vector3.up * (.04f * (physical[(int)resource] % 5)), false);
            Message("Pick up " + OfficeRules.Label(resource) + " from the supply counter.");
        }

        public void Buy(Resource resource, int price)
        {
            if (!inventory.Spend(Resource.Cash, price))
            {
                Message("Not enough Cash. Deliver a Completed File first.");
                return;
            }

            inventory.Add(resource, 1);
            Message(OfficeRules.Label(resource) + " purchased. Select Take to collect it.");
        }

        public void Message(string text)
        {
            messageText.text = text;
        }

        public void PlayButtonClick()
        {
            audioSource.PlayOneShot(buttonClickClip, buttonVolume);
        }

        public void PlayGrab()
        {
            audioSource.PlayOneShot(grabClip, grabVolume);
        }

        public void Ping()
        {
            audioSource.PlayOneShot(completedClip);
        }

        public void ShowTutorial(TutorialTrigger tutorial, bool force = false)
        {
            if (!force && seenTutorials.Contains(tutorial.tutorialId))
            {
                return;
            }

            if (activeTutorial && activeTutorial.panel.activeInHierarchy)
            {
                if (!force)
                {
                    return;
                }

                activeTutorial.panel.SetActive(false);
            }

            seenTutorials.Add(tutorial.tutorialId);
            activeTutorial = tutorial;
            tutorial.panel.SetActive(true);
        }
    }
}
