using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace HouseSim.Kitchen
{
    [RequireComponent(typeof(XRGrabInteractable))]
    public class KitchenSaladBowl : MonoBehaviour
    {
        public GameObject saladPrefab;
        public int ingredientCount;
        public int cleanCount;
        public TextMesh label;
        public Transform outputPoint;
        [Min(2)] public int capacity = 6;
        private readonly HashSet<FoodKind> kinds = new HashSet<FoodKind>();
        private bool mixing;
        public int DifferentKinds => kinds.Count;

        private void Start() { UpdateLabel(); }
        private void OnTriggerEnter(Collider other)
        {
            var food = other.GetComponentInParent<KitchenFood>();
            if (food != null && !food.IsHeld) TryAddIngredient(food);
        }

        private void OnTriggerStay(Collider other)
        {
            // Entering while held should not consume the food. Accept it once released inside the bowl.
            var food = other.GetComponentInParent<KitchenFood>();
            if (food != null && !food.IsHeld && food.IsProduce && food.IsChopped && !food.IsConsumed
                && ingredientCount < capacity && !kinds.Contains(food.definition.kind))
                TryAddIngredient(food);
        }

        public bool TryAddIngredient(KitchenFood food)
        {
            if (mixing || food == null || food.IsConsumed || !food.IsProduce) return false;
            var session = KitchenSession.Instance;
            if (!food.IsChopped)
            {
                session?.Notify("Chop this ingredient before adding it to the salad bowl.");
                return false;
            }
            if (kinds.Contains(food.definition.kind))
            {
                session?.Notify("This kind is already in the bowl. Add a different kind of produce.");
                return false;
            }
            if (ingredientCount >= capacity)
            {
                session?.Notify("The salad bowl is full. Mix this batch first.");
                return false;
            }
            bool clean = !food.IsDirty;
            FoodKind kind = food.definition.kind;
            if (!food.TryTransferToBowl()) return false;
            ingredientCount++;
            if (clean) cleanCount++;
            kinds.Add(kind);
            UpdateLabel();
            session?.Tutorial("salad", "Add chopped produce of at least two kinds.\nMix to make salad. Dirty ingredients spoil the batch.", transform.position);
            return true;
        }

        public bool Mix()
        {
            var session = KitchenSession.Instance;
            if (mixing || session == null) return false;
            if (ingredientCount < 2 || kinds.Count < 2)
            {
                session.Notify("Salad needs at least two different kinds of chopped produce.");
                return false;
            }
            if (saladPrefab == null || saladPrefab.GetComponent<KitchenFood>() == null)
            {
                session.Notify("A salad output prefab must be configured.");
                return false;
            }
            mixing = true;
            try
            {
                Transform spawnAt = outputPoint != null ? outputPoint : transform;
                var output = session.SpawnFood(saladPrefab, spawnAt);
                if (output == null) return false;
                if (outputPoint == null) output.transform.position += Vector3.up * 0.25f;
                if (output.definition == null || output.definition.kind != FoodKind.Salad)
                {
                    Destroy(output.gameObject);
                    session.Notify("The bowl output must use a Salad definition.");
                    return false;
                }
                bool dirty = cleanCount != ingredientCount;
                output.InitializeSalad(dirty);
                if (!output.Claim())
                {
                    Destroy(output.gameObject);
                    return false;
                }
                ingredientCount = 0;
                cleanCount = 0;
                kinds.Clear();
                UpdateLabel();
                session.Notify(dirty ? "Salad mixed, but dirty ingredients will cost 15 energy." : "Fresh salad ready: +20 energy.");
                return true;
            }
            finally { mixing = false; }
        }

        public void MixAction() { Mix(); }

        private void UpdateLabel()
        {
            if (label != null) label.text = "SALAD BOWL\nIngredients: " + ingredientCount + "  Kinds: " + kinds.Count + "\nClean: " + cleanCount + " / " + ingredientCount;
        }
    }
}
