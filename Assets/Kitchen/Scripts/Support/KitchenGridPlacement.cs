using System;
using UnityEngine;

namespace HouseSim.Kitchen
{
    /// <summary>An optional expansion grid. Existing kitchen appliances are never moved.</summary>
    public sealed class KitchenGridPlacement : MonoBehaviour
    {
        [Tooltip("Index 0 is the foundation. All subsequent prefabs require a foundation.")]
        public GameObject[] modulePrefabs = Array.Empty<GameObject>();
        public Transform[] cells = Array.Empty<Transform>();
        [Tooltip("Visible placement pads in BUILD-number order. Their current centers determine where modules appear; green accepts the selected module and red refuses it.")]
        public Renderer[] cellIndicators = Array.Empty<Renderer>();
        [Tooltip("Offset from each grid cell for equipment; foundations use the cell pose.")]
        public Vector3 equipmentOffset;
        public float[] equipmentYawDegrees = Array.Empty<float>();
        public TextMesh label;
        [SerializeField] int selected;
        GameObject[] foundations = Array.Empty<GameObject>();
        GameObject[] equipment = Array.Empty<GameObject>();
        string status = "Place a foundation first.";
        public int Selected => selected;

        void Start() { EnsureSlots(); RefreshLabel(); }

        void EnsureSlots()
        {
            int count = cells == null ? 0 : cells.Length;
            if (foundations.Length != count) Array.Resize(ref foundations, count);
            if (equipment.Length != count) Array.Resize(ref equipment, count);
        }

        public void Select(int index)
        {
            if (modulePrefabs == null || index < 0 || index >= modulePrefabs.Length || modulePrefabs[index] == null)
            {
                Feedback("That equipment is unavailable.", "Unavailable");
                return;
            }
            selected = index;
            Feedback("Selected " + modulePrefabs[index].name + ". Choose an expansion tile.", "Choose a tile");
            KitchenSession.Instance?.Tutorial("grid_select", "Choose a module, then touch a numbered tile. A foundation must be built first.", transform.position);
        }

        public void Place(int cellIndex)
        {
            EnsureSlots();
            if (cellIndex < 0 || cellIndex >= foundations.Length || cells[cellIndex] == null)
            {
                Feedback("Choose a valid expansion tile.", "Choose a tile");
                return;
            }
            if (modulePrefabs == null || selected < 0 || selected >= modulePrefabs.Length || modulePrefabs[selected] == null)
            {
                Feedback("Select a module first.", "Choose a module");
                return;
            }
            if (selected == 0 && foundations[cellIndex] != null)
            {
                Feedback("This tile already has a foundation.", "Foundation already present");
                return;
            }
            if (selected != 0 && foundations[cellIndex] == null)
            {
                Feedback("Build a foundation on this tile first.", "Needs a foundation");
                KitchenSession.Instance?.Tutorial("grid_foundation", "Equipment needs a foundation underneath it on the same tile.", cells[cellIndex].position);
                return;
            }
            if (selected != 0 && equipment[cellIndex] != null)
            {
                Feedback("This tile already has equipment.", "Tile occupied");
                return;
            }

            Transform cell = cells[cellIndex];
            Vector3 position = PlacementPosition(cellIndex) + (selected == 0 ? Vector3.zero : cell.TransformVector(equipmentOffset));
            float yaw = selected != 0 && cellIndex < equipmentYawDegrees.Length ? equipmentYawDegrees[cellIndex] : 0;
            GameObject instance = Instantiate(modulePrefabs[selected], position, cell.rotation * Quaternion.Euler(0,yaw,0), transform);
            instance.name = modulePrefabs[selected].name + " - Tile " + (cellIndex + 1);
            instance.SetActive(true);
            if (selected == 0) foundations[cellIndex] = instance;
            else equipment[cellIndex] = instance;
            Feedback("Built " + modulePrefabs[selected].name + " on tile " + (cellIndex + 1) + ".", "Built on tile " + (cellIndex + 1));
            KitchenSession.Instance?.Tutorial("grid_build", "Your new module is a working kitchen device. Empty tiles remain available for more equipment.", position + Vector3.up);
        }

        public bool HasFoundation(int cellIndex)
        {
            EnsureSlots();
            return cellIndex >= 0 && cellIndex < foundations.Length && foundations[cellIndex] != null;
        }

        Vector3 PlacementPosition(int cellIndex)
        {
            Transform cell = cells[cellIndex];
            if (cellIndicators == null || cellIndex >= cellIndicators.Length || cellIndicators[cellIndex] == null)
                return cell.position;

            // Pads may be repositioned independently of their original cell parents.
            // Keep the floor elevation, excluding the thin marker's visual lift.
            Vector3 markerCenter = cellIndicators[cellIndex].bounds.center;
            return cell.position + Vector3.ProjectOnPlane(markerCenter - cell.position, cell.up);
        }

        public bool HasEquipment(int cellIndex)
        {
            EnsureSlots();
            return cellIndex >= 0 && cellIndex < equipment.Length && equipment[cellIndex] != null;
        }

        void Feedback(string message, string shortStatus)
        {
            status = shortStatus;
            RefreshLabel();
            KitchenSession.Instance?.Notify(message);
        }

        void RefreshLabel()
        {
            RefreshIndicators();
            if (label == null) return;
            string name = modulePrefabs != null && selected >= 0 && selected < modulePrefabs.Length && modulePrefabs[selected] != null
                ? modulePrefabs[selected].name : "Foundation";
            if (selected == 0) name = "BASE";
            else if (name == "InductionHob") name = "HOB";
            label.text = "Selected: " + name.ToUpperInvariant() + "\n" + status;
        }

        void RefreshIndicators()
        {
            EnsureSlots();
            if (cellIndicators == null) return;
            var block = new MaterialPropertyBlock();
            bool available = modulePrefabs != null && selected >= 0 && selected < modulePrefabs.Length && modulePrefabs[selected] != null;
            for (int i = 0; i < cellIndicators.Length && i < foundations.Length; ++i)
            {
                Renderer indicator = cellIndicators[i];
                if (indicator == null) continue;
                bool valid = available && cells[i] != null && (selected == 0 ? foundations[i] == null : foundations[i] != null && equipment[i] == null);
                Color color = valid ? new Color(0.15f, 0.72f, 0.34f) : new Color(0.85f, 0.16f, 0.12f);
                indicator.GetPropertyBlock(block);
                block.SetColor("_BaseColor", color);
                block.SetColor("_Color", color);
                indicator.SetPropertyBlock(block);
                block.Clear();
            }
        }
    }
}
