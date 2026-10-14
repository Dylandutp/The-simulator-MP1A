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
        private KitchenAudioEmitter audioEmitter;
        private MaterialPropertyBlock washColors;
        private Transform contentsRoot;
        private readonly List<IngredientVisual> contents = new List<IngredientVisual>();
        public int DifferentKinds => kinds.Count;
        public int VisibleIngredientCount => contents.Count;
        public Transform ContentsRoot => contentsRoot;

        private sealed class IngredientVisual
        {
            public Transform root;
            public Bounds bounds;
            public Vector3 sourceScale;
            public readonly List<Mesh> meshes = new List<Mesh>();
            public readonly List<MeshRenderer> fadingRenderers = new List<MeshRenderer>();
            public float visibleWash;
            public float targetWash;
        }

        private void Awake()
        {
            if (!GetComponent<KitchenContactStabilizer>()) gameObject.AddComponent<KitchenContactStabilizer>();
            audioEmitter = KitchenAudioEmitter.Get(this);
        }
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
            // Transfer still consumes exactly one inventory portion. Keep an independent
            // visual copy: cut meshes belong to the food and are destroyed with it.
            IngredientVisual visual = CaptureIngredient(food);
            if (visual == null) return false;
            if (!food.TryTransferToBowl())
            {
                DisposeVisual(visual);
                return false;
            }
            contents.Add(visual);
            ArrangeContents();
            visual.root.gameObject.SetActive(true);
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
                ClearContents();
                UpdateLabel();
                session.Notify(dirty ? "Salad mixed, but dirty ingredients will cost 15 energy." : "Fresh salad ready: +20 energy.");
                audioEmitter.PlayCue(KitchenSound.Complete);
                return true;
            }
            finally { mixing = false; }
        }

        public void MixAction() { Mix(); }

        private IngredientVisual CaptureIngredient(KitchenFood food)
        {
            if (!contentsRoot)
            {
                contentsRoot = new GameObject("Bowl contents").transform;
                contentsRoot.SetParent(transform, false);
            }
            var visual = new IngredientVisual
            {
                root = new GameObject(food.definition.displayName + " chopped pieces").transform,
                sourceScale = DivideScale(food.transform.lossyScale, transform.lossyScale),
                visibleWash = food.VisibleWashProgress,
                targetWash = food.WashProgress
            };
            visual.root.SetParent(contentsRoot, false);
            visual.root.gameObject.SetActive(false);
            var copies = new Dictionary<Transform, Transform> { { food.transform, visual.root } };
            var properties = new MaterialPropertyBlock();
            bool hasBounds = false;
            foreach (MeshRenderer original in food.GetComponentsInChildren<MeshRenderer>())
            {
                MeshFilter filter = original.GetComponent<MeshFilter>();
                if (!original.enabled || !original.gameObject.activeInHierarchy || !filter || !filter.sharedMesh) continue;
                Transform surface = CopyTransform(original.transform, food.transform, visual.root, copies);
                Mesh mesh = Instantiate(filter.sharedMesh);
                mesh.name = "Bowl copy - " + filter.sharedMesh.name;
                visual.meshes.Add(mesh);
                surface.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
                MeshRenderer renderer = surface.gameObject.AddComponent<MeshRenderer>();
                renderer.sharedMaterials = original.sharedMaterials;
                renderer.shadowCastingMode = original.shadowCastingMode;
                renderer.receiveShadows = original.receiveShadows;
                original.GetPropertyBlock(properties);
                if (!properties.isEmpty) renderer.SetPropertyBlock(properties);
                for (int i = 0; i < renderer.sharedMaterials.Length; i++)
                {
                    original.GetPropertyBlock(properties, i);
                    if (!properties.isEmpty) renderer.SetPropertyBlock(properties, i);
                }
                if (food.visualRenderers != null && System.Array.IndexOf(food.visualRenderers, original) >= 0)
                    visual.fadingRenderers.Add(renderer);
                Matrix4x4 toRoot = visual.root.worldToLocalMatrix * surface.localToWorldMatrix;
                Bounds meshBounds = mesh.bounds;
                for (int corner = 0; corner < 8; corner++)
                {
                    Vector3 point = meshBounds.center + Vector3.Scale(meshBounds.extents,
                        new Vector3((corner & 1) == 0 ? -1f : 1f, (corner & 2) == 0 ? -1f : 1f, (corner & 4) == 0 ? -1f : 1f));
                    point = toRoot.MultiplyPoint3x4(point);
                    if (!hasBounds) { visual.bounds = new Bounds(point, Vector3.zero); hasBounds = true; }
                    else visual.bounds.Encapsulate(point);
                }
            }
            if (hasBounds) return visual;
            DisposeVisual(visual);
            KitchenSession.Instance?.Notify("This ingredient has no visible pieces to put in the bowl.");
            return null;
        }

        private static Transform CopyTransform(Transform source, Transform foodRoot, Transform destinationRoot,
            Dictionary<Transform, Transform> copies)
        {
            if (copies.TryGetValue(source, out Transform copy)) return copy;
            Transform parent = source.parent == foodRoot ? destinationRoot
                : CopyTransform(source.parent, foodRoot, destinationRoot, copies);
            copy = new GameObject(source.name).transform;
            copy.SetParent(parent, false);
            copy.localPosition = source.localPosition;
            copy.localRotation = source.localRotation;
            copy.localScale = source.localScale;
            copies.Add(source, copy);
            return copy;
        }

        private static Vector3 DivideScale(Vector3 source, Vector3 target) => new Vector3(
            Mathf.Abs(source.x) / Mathf.Max(.001f, Mathf.Abs(target.x)),
            Mathf.Abs(source.y) / Mathf.Max(.001f, Mathf.Abs(target.y)),
            Mathf.Abs(source.z) / Mathf.Max(.001f, Mathf.Abs(target.z)));

        private void Update()
        {
            foreach (IngredientVisual visual in contents)
            {
                if (Mathf.Approximately(visual.visibleWash, visual.targetWash)) continue;
                float previousFactor = .45f + .55f * visual.visibleWash;
                visual.visibleWash = Mathf.MoveTowards(visual.visibleWash, visual.targetWash, Time.deltaTime / .55f);
                float ratio = (.45f + .55f * visual.visibleWash) / previousFactor;
                if (washColors == null) washColors = new MaterialPropertyBlock();
                foreach (MeshRenderer renderer in visual.fadingRenderers)
                {
                    if (!renderer) continue;
                    FadeWashColor(renderer, ratio, -1);
                    for (int i = 0; i < renderer.sharedMaterials.Length; i++) FadeWashColor(renderer, ratio, i);
                }
            }
        }

        private void FadeWashColor(MeshRenderer renderer, float ratio, int materialIndex)
        {
            if (materialIndex < 0) renderer.GetPropertyBlock(washColors);
            else renderer.GetPropertyBlock(washColors, materialIndex);
            if (washColors.isEmpty) return;
            Color Brighten(Color color)
            {
                float alpha = color.a;
                color *= ratio;
                color.a = alpha;
                return color;
            }
            washColors.SetColor("_BaseColor", Brighten(washColors.GetColor("_BaseColor")));
            washColors.SetColor("_Color", Brighten(washColors.GetColor("_Color")));
            if (materialIndex < 0) renderer.SetPropertyBlock(washColors);
            else renderer.SetPropertyBlock(washColors, materialIndex);
        }

        private void ArrangeContents()
        {
            // Stable, non-physical pieces follow the held bowl. Fit the visible groups
            // inside its existing rim instead of piling rigid bodies into one another.
            float radius = contents.Count == 1 ? 0f : .095f;
            float width = contents.Count == 1 ? .25f : .16f;
            BoxCollider floor = GetComponent<BoxCollider>();
            float bottom = floor ? floor.center.y + floor.size.y * .5f + .006f : .036f;
            for (int i = 0; i < contents.Count; i++)
            {
                IngredientVisual visual = contents[i];
                Vector3 size = Vector3.Scale(visual.bounds.size, visual.sourceScale);
                float fit = Mathf.Min(1f, width / Mathf.Max(.001f, Mathf.Max(size.x, size.z)), .14f / Mathf.Max(.001f, size.y));
                Vector3 scale = visual.sourceScale * fit;
                float angle = contents.Count == 2 ? i * Mathf.PI : i * Mathf.PI * 2f / contents.Count - Mathf.PI * .5f;
                Vector3 center = new Vector3(Mathf.Cos(angle) * radius, bottom, Mathf.Sin(angle) * radius);
                visual.root.localScale = scale;
                visual.root.localRotation = Quaternion.identity;
                visual.root.localPosition = center - Vector3.Scale(new Vector3(visual.bounds.center.x, visual.bounds.min.y, visual.bounds.center.z), scale);
            }
        }

        private static void DisposeVisual(IngredientVisual visual)
        {
            if (visual.root)
            {
                visual.root.gameObject.SetActive(false);
                Destroy(visual.root.gameObject);
            }
            foreach (Mesh mesh in visual.meshes) if (mesh) Destroy(mesh);
            visual.meshes.Clear();
        }

        private void ClearContents()
        {
            foreach (IngredientVisual visual in contents) DisposeVisual(visual);
            contents.Clear();
            if (contentsRoot) { contentsRoot.gameObject.SetActive(false); Destroy(contentsRoot.gameObject); }
            contentsRoot = null;
        }

        private void OnDestroy() => ClearContents();

        private void UpdateLabel()
        {
            if (label != null) label.text = "SALAD BOWL\nIngredients: " + ingredientCount + "  Kinds: " + kinds.Count + "\nClean: " + cleanCount + " / " + ingredientCount;
        }
    }
}
