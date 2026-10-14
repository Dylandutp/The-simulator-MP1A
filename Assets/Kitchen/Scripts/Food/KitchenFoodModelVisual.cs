using System.Collections.Generic;
using UnityEngine;

namespace HouseSim.Kitchen
{
    /// <summary>Authored food models share one gameplay root, collider, and inventory portion.</summary>
    [DisallowMultipleComponent]
    public sealed class KitchenFoodModelVisual : MonoBehaviour
    {
        public GameObject rawVisual;
        public GameObject cookedVisual;
        public GameObject burntVisual;
        public GameObject firstCutVisual;
        public GameObject secondCutVisual;
        [Min(1)] public int firstCutPieceCount = 2;
        [Min(1)] public int secondCutPieceCount = 4;
        public bool useCutColliderBounds;
        public Bounds firstCutColliderBounds;
        public Bounds secondCutColliderBounds;

        public GameObject ActiveVisual { get; private set; }
        public bool HasAuthoredCuts => IsVisualChild(firstCutVisual) && IsVisualChild(secondCutVisual);

        private sealed class Surface
        {
            public Renderer renderer;
            public Color[] baseColors;
        }

        private readonly Dictionary<GameObject, Surface[]> groups = new Dictionary<GameObject, Surface[]>();
        private readonly List<Renderer> activeRenderers = new List<Renderer>();
        private MaterialPropertyBlock properties;
        private KitchenFood food;
        private BoxCollider foodCollider;
        private bool contaminated;
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorProperty = Shader.PropertyToID("_Color");

        /// <summary>
        /// Uses each imported material's own color as the tint baseline. Textures,
        /// submaterials, and transparent parts retain their authored appearance.
        /// </summary>
        public bool Apply(KitchenFood target, bool isContaminated)
        {
            if (!target || !target.definition) return false;
            food = target;
            contaminated = isContaminated;
            GameObject selected;
            if (target.IsProduce && target.CutStage > 0)
            {
                // A model with no authored cuts can still use the legacy clipping path.
                if (!HasAuthoredCuts) return false;
                selected = target.CutStage == 1 ? firstCutVisual : secondCutVisual;
            }
            else
            {
                selected = target.State == FoodState.Burnt && IsVisualChild(burntVisual) ? burntVisual
                    : target.State != FoodState.Raw && IsVisualChild(cookedVisual) ? cookedVisual : rawVisual;
            }
            if (!IsVisualChild(selected)) return false;
            SelectVisual(selected);
            if (target.IsProduce && target.CutStage > 0) ApplyCutCollider(target.CutStage);
            ApplyMaterialColors();
            return true;
        }

        public int GetPieceCount(int stage) => stage <= 0 ? 1
            : Mathf.Max(1, stage == 1 ? firstCutPieceCount : secondCutPieceCount);

        /// <summary>Returns zero when this food should continue using procedural cuts.</summary>
        public int ApplyCutStage(int stage)
        {
            if (!HasAuthoredCuts || stage < 1) return 0;
            if (!food) food = GetComponent<KitchenFood>();
            SelectVisual(stage == 1 ? firstCutVisual : secondCutVisual);
            ApplyCutCollider(stage);
            ApplyMaterialColors();
            return GetPieceCount(stage);
        }

        private bool IsVisualChild(GameObject visual) => visual && visual != gameObject
            && visual.transform.IsChildOf(transform);

        private void ApplyCutCollider(int stage)
        {
            if (!useCutColliderBounds || stage < 1) return;
            Bounds bounds = stage == 1 ? firstCutColliderBounds : secondCutColliderBounds;
            Vector3 center = bounds.center;
            Vector3 size = bounds.size;
            if (!IsFinite(center) || !IsFinite(size) || size.x <= 0f || size.y <= 0f || size.z <= 0f) return;
            if (!foodCollider) foodCollider = GetComponent<BoxCollider>();
            if (!foodCollider || (foodCollider.center == center && foodCollider.size == size)) return;
            // Spreading authored slices can require a wider envelope without shrinking
            // their meshes. Keep the existing single root collider and refresh once.
            foodCollider.center = center;
            foodCollider.size = size;
            GetComponent<KitchenContactStabilizer>()?.RefreshColliders();
        }

        private static bool IsFinite(Vector3 value) => !float.IsNaN(value.x) && !float.IsInfinity(value.x)
            && !float.IsNaN(value.y) && !float.IsInfinity(value.y)
            && !float.IsNaN(value.z) && !float.IsInfinity(value.z);

        private void SelectVisual(GameObject selected)
        {
            SetActive(rawVisual, selected);
            SetActive(cookedVisual, selected);
            SetActive(burntVisual, selected);
            SetActive(firstCutVisual, selected);
            SetActive(secondCutVisual, selected);
            ActiveVisual = selected;
            activeRenderers.Clear();
            foreach (Surface surface in Surfaces(selected))
                if (surface.renderer && surface.renderer.enabled && surface.renderer.gameObject.activeInHierarchy)
                    activeRenderers.Add(surface.renderer);
            if (!food) return;
            bool changed = food.visualRenderers == null || food.visualRenderers.Length != activeRenderers.Count;
            if (!changed)
                for (int i = 0; i < activeRenderers.Count; i++)
                    if (food.visualRenderers[i] != activeRenderers[i]) { changed = true; break; }
            if (changed) food.visualRenderers = activeRenderers.ToArray();
        }

        private void SetActive(GameObject visual, GameObject selected)
        {
            if (IsVisualChild(visual) && visual.activeSelf != (visual == selected)) visual.SetActive(visual == selected);
        }

        private Surface[] Surfaces(GameObject visual)
        {
            if (groups.TryGetValue(visual, out Surface[] surfaces)) return surfaces;
            Renderer[] renderers = visual.GetComponentsInChildren<Renderer>(true);
            surfaces = new Surface[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
            {
                Material[] materials = renderers[i].sharedMaterials;
                var colors = new Color[materials.Length];
                for (int slot = 0; slot < materials.Length; slot++)
                {
                    Material material = materials[slot];
                    colors[slot] = material && material.HasProperty(BaseColor) ? material.GetColor(BaseColor)
                        : material && material.HasProperty(ColorProperty) ? material.GetColor(ColorProperty) : Color.white;
                }
                surfaces[i] = new Surface { renderer = renderers[i], baseColors = colors };
            }
            groups.Add(visual, surfaces);
            return surfaces;
        }

        private void ApplyMaterialColors()
        {
            if (!food || !food.definition || !ActiveVisual) return;
            if (properties == null) properties = new MaterialPropertyBlock();
            float cleanliness = food.IsProduce ? Mathf.Lerp(.45f, 1f, food.VisibleWashProgress) : 1f;
            bool unclean = contaminated || (food.definition.kind == FoodKind.Salad && food.IsDirty);
            Color dirt = unclean ? new Color(.58f, .45f, .28f, 1f) : Color.white;
            foreach (Surface surface in Surfaces(ActiveVisual))
            {
                Renderer renderer = surface.renderer;
                if (!renderer || !renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
                for (int slot = 0; slot < surface.baseColors.Length; slot++)
                {
                    Color authored = surface.baseColors[slot];
                    Color tint = authored * dirt * cleanliness;
                    tint.a = authored.a;
                    renderer.GetPropertyBlock(properties, slot);
                    properties.SetColor(BaseColor, tint);
                    properties.SetColor(ColorProperty, tint);
                    renderer.SetPropertyBlock(properties, slot);
                }
            }
        }
    }
}
