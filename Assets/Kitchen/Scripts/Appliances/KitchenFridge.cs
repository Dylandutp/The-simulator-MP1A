using UnityEngine;
using System.Collections.Generic;

namespace HouseSim.Kitchen
{
    public sealed class KitchenFridge : MonoBehaviour
    {
        public KitchenDoor door;
        public GameObject[] foodPrefabs;
        public Transform[] slots;
        public TextMesh label;
        private KitchenStockItem[] stock;
        private int[] prefabOrder;
        private readonly Dictionary<GameObject, Bounds> prefabBounds = new Dictionary<GameObject, Bounds>();
        private bool closedSinceLastOpening;
        private float nextStatusUpdate;
        private int lastDisplayedCount = -1;

        private void Start()
        {
            EnsureStockLayout();
            if (door) door.stateChanged.AddListener(OnDoorChanged);
            Refill();
            closedSinceLastOpening = door && door.OpenAmount <= 0.01f;
        }

        private void OnDestroy()
        {
            if (door) door.stateChanged.RemoveListener(OnDoorChanged);
        }

        private void OnDoorChanged(bool open) => ObserveDoorCycle();

        private void ObserveDoorCycle()
        {
            if (!door) return;
            // KitchenDoor's open/closed event has a much smaller hysteresis than a
            // deliberate restocking cycle. Check its continuous position as well:
            // near fully closed arms one refill, opening ~16 degrees consumes it.
            if (door.OpenAmount <= 0.01f) closedSinceLastOpening = true;
            else if (closedSinceLastOpening && door.OpenAmount >= 0.15f)
            {
                closedSinceLastOpening = false;
                Refill();
            }
        }

        private void Update()
        {
            ObserveDoorCycle();
            if (Time.time < nextStatusUpdate) return;
            nextStatusUpdate = Time.time + 0.15f;
            ReleaseTakenItems();
            RefreshLabel();
        }

        private void EnsureStockLayout()
        {
            int count = slots == null ? 0 : slots.Length;
            if (stock == null || stock.Length != count) stock = new KitchenStockItem[count];
            if (prefabOrder != null && prefabOrder.Length == count) return;
            prefabOrder = new int[count];
            int prefabCount = foodPrefabs == null ? 0 : foodPrefabs.Length;
            for (int i = 0; i < count; i++) prefabOrder[i] = prefabCount == 0 ? 0 : i % prefabCount;
            // Shuffle slots, preserving the exact quantity of every configured ingredient.
            for (int i = count - 1; i > 0; i--)
            {
                int other = Random.Range(0, i + 1);
                int previous = prefabOrder[i];
                prefabOrder[i] = prefabOrder[other];
                prefabOrder[other] = previous;
            }
        }

        private void ReleaseTakenItems()
        {
            if (stock == null) return;
            for (int i = 0; i < stock.Length; i++)
            {
                KitchenStockItem item = stock[i];
                if (!item || !item.Food || item.Food.IsConsumed) { stock[i] = null; continue; }
                if (item.IsReleased || item.Food.IsHeld || !slots[i] || Vector3.Distance(item.transform.position, slots[i].position) > 0.16f)
                {
                    item.Release();
                    stock[i] = null;
                }
            }
        }

        public void Refill()
        {
            if (slots == null || foodPrefabs == null || foodPrefabs.Length == 0) return;
            EnsureStockLayout();
            ReleaseTakenItems();
            Physics.SyncTransforms();
            for (int i = 0; i < slots.Length; i++)
            {
                if (stock[i] || !slots[i] || SlotIsOccupied(i)) continue;
                SpawnSlot(i);
            }
            RefreshLabel();
        }

        private void SpawnSlot(int index)
        {
            GameObject prefab = foodPrefabs[prefabOrder[index]];
            if (!prefab) return;
            GameObject instance = Instantiate(prefab, slots[index].position, slots[index].rotation, slots[index]);
            KitchenFood food = instance.GetComponent<KitchenFood>();
            if (!food) { Destroy(instance); return; }
            KitchenStockItem item = instance.GetComponent<KitchenStockItem>();
            if (!item) item = instance.AddComponent<KitchenStockItem>();
            item.Stock(food);
            stock[index] = item;
            Physics.SyncTransforms();
        }

        private void RefreshLabel()
        {
            int count = CountStock();
            if (!label || count == lastDisplayedCount) return;
            lastDisplayedCount = count;
            label.text = "FRIDGE STOCK  " + count + " / " + (slots == null ? 0 : slots.Length)
                + "\nCLOSE FULLY, THEN OPEN TO REFILL";
        }

        public int CountStock()
        {
            if (stock == null) return 0;
            int count = 0;
            foreach (KitchenStockItem item in stock)
                if (item && !item.IsReleased && item.Food && !item.Food.IsConsumed && !item.Food.IsHeld) count++;
            return count;
        }

        private bool SlotIsOccupied(int index)
        {
            Transform slot = slots[index];
            GameObject prefab = foodPrefabs[prefabOrder[index]];
            if (!prefab || !TryPrefabBounds(prefab, out Bounds bounds)) return true;
            Vector3 scale = Vector3.Scale(slot.lossyScale, prefab.transform.localScale);
            Vector3 center = slot.position + slot.rotation * Vector3.Scale(bounds.center, scale);
            Vector3 extents = Vector3.Scale(bounds.extents, new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z))) + Vector3.one * 0.003f;
            // Check the entire volume that the new ingredient will occupy, including offset
            // collider centers and compound children. A hand-held blocker is never deleted.
            foreach (Collider hit in Physics.OverlapBox(center, extents, slot.rotation, ~0, QueryTriggerInteraction.Ignore))
            {
                KitchenFood food = hit.GetComponentInParent<KitchenFood>();
                if (food && !food.IsConsumed) return true;
            }
            return false;
        }

        private bool TryPrefabBounds(GameObject prefab, out Bounds bounds)
        {
            if (prefabBounds.TryGetValue(prefab, out bounds)) return true;
            bool hasBounds = false;
            bounds = default;
            foreach (Collider collider in prefab.GetComponentsInChildren<Collider>(true))
            {
                if (!collider.enabled || collider.isTrigger) continue;
                Bounds local;
                if (collider is BoxCollider box) local = new Bounds(box.center, box.size);
                else if (collider is SphereCollider sphere) local = new Bounds(sphere.center, Vector3.one * sphere.radius * 2f);
                else if (collider is CapsuleCollider capsule)
                {
                    Vector3 size = Vector3.one * capsule.radius * 2f;
                    size[capsule.direction] = Mathf.Max(capsule.height, size[capsule.direction]);
                    local = new Bounds(capsule.center, size);
                }
                else if (collider is MeshCollider mesh && mesh.sharedMesh) local = mesh.sharedMesh.bounds;
                else continue;
                Matrix4x4 intoRoot = prefab.transform.worldToLocalMatrix * collider.transform.localToWorldMatrix;
                for (int corner = 0; corner < 8; corner++)
                {
                    Vector3 sign = new Vector3((corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1);
                    Vector3 point = intoRoot.MultiplyPoint3x4(local.center + Vector3.Scale(local.extents, sign));
                    if (!hasBounds) { bounds = new Bounds(point, Vector3.zero); hasBounds = true; }
                    else bounds.Encapsulate(point);
                }
            }
            if (hasBounds) prefabBounds[prefab] = bounds;
            return hasBounds;
        }
    }
}
