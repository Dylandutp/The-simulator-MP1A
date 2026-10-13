using UnityEngine;

namespace HouseSim.Kitchen
{
    public sealed class KitchenTrash : MonoBehaviour
    {
        public TextMesh label;
        private int discardedCount;

        private void Start() => Refresh();
        private void OnTriggerEnter(Collider other) => TryDiscard(other);
        private void OnTriggerStay(Collider other) => TryDiscard(other);

        private void TryDiscard(Collider other)
        {
            KitchenFood food = other.GetComponentInParent<KitchenFood>();
            if (!food || food.IsHeld || food.IsConsumed) return;
            food.Discard();
            if (!food.IsConsumed) return;
            discardedCount++;
            Refresh();
        }

        private void Refresh()
        {
            if (label) label.text = "TRASH\nDROP FOOD HERE | " + discardedCount + " DISCARDED";
        }
    }
}
