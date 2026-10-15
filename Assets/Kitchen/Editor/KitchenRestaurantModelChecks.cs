using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using Object = UnityEngine.Object;

namespace HouseSim.Kitchen.Editor
{
    /// <summary>Checks model swaps without changing the gameplay roots or the saved kitchen.</summary>
    public static class KitchenRestaurantModelChecks
    {
        const string ArtRoot = "Assets/Kitchen/Art/RestaurantBits/";

        public static MeshRenderer[] ActiveRenderers(KitchenFood food) =>
            food.GetComponentsInChildren<MeshRenderer>().Where(r => r.enabled && r.gameObject.activeInHierarchy).ToArray();

        public static Color EffectiveColor(Renderer renderer, int slot)
        {
            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block, slot);
            if (block.isEmpty) renderer.GetPropertyBlock(block);
            return block.isEmpty ? MaterialColor(renderer.sharedMaterials[slot]) : block.GetColor("_BaseColor");
        }

        public static Color MaterialColor(Material material) =>
            material && material.HasProperty("_BaseColor") ? material.GetColor("_BaseColor")
            : material && material.HasProperty("_Color") ? material.GetColor("_Color") : Color.white;

        public static Color CleanColor(KitchenFood food, Renderer renderer, int slot) =>
            food.GetComponent<KitchenFoodModelVisual>() ? MaterialColor(renderer.sharedMaterials[slot]) : food.definition.rawColor;

        public static int ExpectedPieceCount(KitchenFood food, int stage)
        {
            if (stage <= 0) return 1;
            if (food.definition.kind == FoodKind.Apple) return stage == 1 ? 2 : 4;
            if (food.definition.kind == FoodKind.Lettuce) return stage == 1 ? 2 : 3;
            if (food.definition.kind == FoodKind.Tomato) return stage == 1 ? 2 : 3;
            return 0;
        }

        public static IEnumerator Run(Action<bool, string> check)
        {
            var spawned = new List<GameObject>();
            try
            {
                foreach (string name in new[] { "Chicken", "Steak", "Tomato", "Lettuce" })
                {
                    var food = Spawn(name, spawned);
                    yield return null;
                    var model = food.GetComponent<KitchenFoodModelVisual>();
                    check(model && model.ActiveVisual == model.rawVisual, "restaurant/" + name + "/raw-model-selected");
                    CheckRoots(food, check, name + "/raw");
                    CheckOriginalCollider(food, check);
                    CheckVisual(food, model.rawVisual, check, name + "/raw");
                    string whole = name == "Chicken" ? "ham" : name.ToLowerInvariant();
                    check(UsesSource(model.rawVisual, "food_ingredient_" + whole + ".fbx"),
                        "restaurant/" + name + "/requested-whole-asset");

                    if (!food.IsProduce)
                    {
                        Rigidbody body = food.GetComponent<Rigidbody>();
                        Collider collider = food.GetComponent<Collider>();
                        Mesh[] rawMeshes = ActiveRenderers(food).Select(r => r.GetComponent<MeshFilter>().sharedMesh).ToArray();
                        Color rawColor = EffectiveColor(ActiveRenderers(food)[0], 0);
                        food.ApplyHeat(food.definition.goodAtSeconds);
                        CheckVisual(food, model.cookedVisual, check, name + "/cooked");
                        Color cookedColor = EffectiveColor(ActiveRenderers(food)[0], 0);
                        if (name == "Chicken")
                            check(UsesSource(model.cookedVisual, "food_ingredient_ham_cooked.fbx"), "restaurant/chicken/cooked-ham-asset");
                        else
                        {
                            Mesh[] cookedMeshes = ActiveRenderers(food).Select(r => r.GetComponent<MeshFilter>().sharedMesh).ToArray();
                            check(rawMeshes.Length == cookedMeshes.Length && cookedMeshes.Select((mesh, i) =>
                                    mesh.vertices.SequenceEqual(rawMeshes[i].vertices) && mesh.normals.SequenceEqual(rawMeshes[i].normals)
                                    && mesh.triangles.SequenceEqual(rawMeshes[i].triangles)).All(equal => equal)
                                && Vector4.Distance(rawColor, cookedColor) > .05f,
                                "restaurant/steak/cooking-changes-tint-and-preserves-original-geometry");
                            check(UsesSource(model.cookedVisual, "Seared steak palette.asset") && cookedMeshes.Select((mesh, i) => {
                                Vector2[] rawUV = rawMeshes[i].uv;
                                Vector2[] cookedUV = mesh.uv;
                                return rawUV.Length == cookedUV.Length && rawUV.Length > 0
                                    && rawUV.Where((uv, vertex) => uv != cookedUV[vertex]).Any()
                                    && rawUV.Where((uv, vertex) => uv == cookedUV[vertex]).Any();
                            }).All(remapped => remapped), "restaurant/steak/cooked-meat-palette-remapped-with-original-detail-uvs-retained");
                        }
                        food.ApplyHeat(food.definition.burntAtSeconds);
                        CheckVisual(food, model.burntVisual, check, name + "/burnt");
                        if (name == "Chicken")
                            check(UsesSource(model.burntVisual, "food_ingredient_ham_trash.fbx"), "restaurant/chicken/burnt-ham-asset");
                        else
                        {
                            Color burntColor = EffectiveColor(ActiveRenderers(food)[0], 0);
                            check(rawMeshes.SequenceEqual(ActiveRenderers(food).Select(r => r.GetComponent<MeshFilter>().sharedMesh))
                                && burntColor.grayscale < cookedColor.grayscale * .65f,
                                "restaurant/steak/burnt-state-is-visibly-darker-with-same-geometry");
                        }
                        check(food.State == FoodState.Burnt && food.GetComponent<Rigidbody>() == body
                            && food.GetComponent<Collider>() == collider && food.ServingsRemaining == 1f,
                            "restaurant/" + name + "/cooking-preserves-one-physical-portion");
                        food.Discard();
                        yield return null;
                        check(rawMeshes.All(mesh => mesh && AssetDatabase.Contains(mesh)), "restaurant/" + name + "/food-destruction-preserves-imported-meshes");
                        continue;
                    }

                    MeshRenderer[] surfaces = ActiveRenderers(food);
                    Material[][] materials = surfaces.Select(r => r.sharedMaterials).ToArray();
                    Color[][] colorsBefore = surfaces.Select(r => Enumerable.Range(0, r.sharedMaterials.Length)
                        .Select(slot => EffectiveColor(r, slot)).ToArray()).ToArray();
                    check(SlotsMatch(surfaces, (r, slot) => TintEquals(EffectiveColor(r, slot), MaterialColor(r.sharedMaterials[slot]), .45f)),
                        "restaurant/" + name + "/dirty-tint-retains-authored-material-palette");
                    food.Wash(.1f);
                    check(!food.IsDirty && surfaces.Select((r, i) => Enumerable.Range(0, r.sharedMaterials.Length)
                        .All(slot => EffectiveColor(r, slot) == colorsBefore[i][slot])).All(v => v),
                        "restaurant/" + name + "/wash-rule-completes-before-visual-fade");
                    yield return new WaitForSeconds(.65f);
                    check(SlotsMatch(surfaces, (r, slot) => TintEquals(EffectiveColor(r, slot), MaterialColor(r.sharedMaterials[slot]), 1f))
                        && surfaces.Select((r, i) => r.sharedMaterials.SequenceEqual(materials[i])).All(v => v),
                        "restaurant/" + name + "/wash-restores-every-material-without-replacing-textures");

                    food = Cut(food, spawned);
                    model = food.GetComponent<KitchenFoodModelVisual>();
                    CheckVisual(food, model.firstCutVisual, check, name + "/first-cut");
                    CheckRoots(food, check, name + "/first-cut");
                    check(food.GetComponent<KitchenCutVisual>().PieceCount == ExpectedPieceCount(food, 1),
                        "restaurant/" + name + "/first-cut-piece-count");
                    if (name == "Tomato")
                        check(UsesSource(model.firstCutVisual, "food_ingredient_tomato_slice.fbx")
                            && UsesSource(model.firstCutVisual, "Tomato two-thirds.asset")
                            && ActiveRenderers(food).Length >= 2, "restaurant/tomato/first-cut-has-body-and-separate-slice");
                    else
                        check(UsesSource(model.firstCutVisual, "food_ingredient_lettuce_chopped.fbx"),
                            "restaurant/lettuce/first-cut-uses-requested-chopped-model");
                    var firstBox = food.GetComponent<BoxCollider>();
                    check(food.Chop(), "restaurant/" + name + "/second-cut-accepted");
                    CheckVisual(food, model.secondCutVisual, check, name + "/second-cut");
                    CheckRoots(food, check, name + "/second-cut");
                    check(food.GetComponent<KitchenCutVisual>().PieceCount == ExpectedPieceCount(food, 2)
                        && !food.IsDirty && food.ServingsRemaining == 1f && !food.Chop(),
                        "restaurant/" + name + "/second-cut-remains-one-clean-complete-portion");
                    if (name == "Tomato")
                        check(model.secondCutPieceCount == 3 && UsesSource(model.secondCutVisual, "food_ingredient_tomato_slices.fbx"),
                            "restaurant/tomato/second-cut-uses-multiple-slices-asset");
                    else
                    {
                        MeshRenderer[] leaves = ActiveRenderers(food);
                        check(leaves.Length == 3 && leaves.All(r => SourceName(r) == "food_ingredient_lettuce_slice.fbx")
                            && leaves.Select(r => r.bounds.center).Distinct().Count() == 3,
                            "restaurant/lettuce/three-separate-leaf-slices-share-one-grab-root");
                        check(leaves.Length == 3 && leaves.Any(a => leaves.Any(b => a != b && FootprintsOverlap(a.bounds, b.bounds))),
                            "restaurant/lettuce/slices-are-grouped-with-loose-overlap");
                    }
                    // Slices can spread without shrinking their meshes; keep the physical root.
                    check(food.GetComponent<BoxCollider>() == firstBox,
                        "restaurant/" + name + "/cut-stage-swap-retains-existing-collider-component");
                    food.Discard();
                    yield return null;
                }
            }
            finally
            {
                foreach (GameObject instance in spawned) if (instance) Object.Destroy(instance);
            }
            yield return null;
        }

        static KitchenFood Spawn(string name, List<GameObject> spawned)
        {
            GameObject instance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Kitchen/Prefabs/Food/" + name + ".prefab"), new Vector3(190f, 4f, 190f), Quaternion.identity);
            spawned.Add(instance);
            Freeze(instance);
            return instance.GetComponent<KitchenFood>();
        }

        static void Freeze(GameObject instance)
        {
            foreach (var recovery in instance.GetComponentsInChildren<KitchenRespawnTool>()) recovery.enabled = false;
            var body = instance.GetComponent<Rigidbody>();
            body.useGravity = false;
            body.isKinematic = true;
        }

        static KitchenFood Cut(KitchenFood whole, List<GameObject> spawned)
        {
            Vector3 position = whole.transform.position;
            var definition = whole.definition;
            if (!whole.Chop()) throw new InvalidOperationException("Restaurant model fixture could not chop produce.");
            var cut = Object.FindObjectsByType<KitchenFood>(FindObjectsSortMode.None).Single(f =>
                !f.IsConsumed && f.IsChopped && f.definition == definition && Vector3.Distance(f.transform.position, position) < .01f);
            spawned.Add(cut.gameObject);
            Freeze(cut.gameObject);
            return cut;
        }

        static bool SlotsMatch(IEnumerable<MeshRenderer> surfaces, Func<MeshRenderer, int, bool> matches) =>
            surfaces.All(r => r.sharedMaterials.Length > 0 && Enumerable.Range(0, r.sharedMaterials.Length).All(slot => matches(r, slot)));

        static bool TintEquals(Color actual, Color baseline, float factor)
        {
            Color expected = baseline * factor;
            expected.a = baseline.a;
            return Vector4.Distance(actual, expected) < .005f;
        }

        static string SourceName(MeshRenderer renderer) => System.IO.Path.GetFileName(
            AssetDatabase.GetAssetPath(renderer.GetComponent<MeshFilter>().sharedMesh));

        static bool UsesSource(GameObject visual, string file) => visual && visual.GetComponentsInChildren<MeshRenderer>(true)
            .Any(r => r.GetComponent<MeshFilter>() && SourceName(r) == file);

        static bool FootprintsOverlap(Bounds a, Bounds b) => a.min.x < b.max.x && a.max.x > b.min.x
            && a.min.z < b.max.z && a.max.z > b.min.z;

        static void CheckRoots(KitchenFood food, Action<bool, string> check, string label)
        {
            check(food.GetComponentsInChildren<Rigidbody>(true).Length == 1 && food.GetComponent<Rigidbody>()
                && food.GetComponentsInChildren<XRGrabInteractable>(true).Length == 1 && food.GetComponent<XRGrabInteractable>()
                && food.GetComponentsInChildren<KitchenFood>(true).Length == 1
                && food.GetComponentsInChildren<Collider>(true).Length == 1 && food.GetComponent<BoxCollider>(),
                "restaurant/" + label + "/one-grab-body-collider-and-food-root");
        }

        static void CheckVisual(KitchenFood food, GameObject expected, Action<bool, string> check, string label)
        {
            var model = food.GetComponent<KitchenFoodModelVisual>();
            var groups = new[] { model.rawVisual, model.cookedVisual, model.burntVisual, model.firstCutVisual, model.secondCutVisual }
                .Where(group => group).Distinct().ToArray();
            MeshRenderer[] renderers = ActiveRenderers(food);
            check(expected && model.ActiveVisual == expected && expected.activeSelf && groups.All(group => group.activeSelf == (group == expected))
                && renderers.Length > 0 && renderers.All(r => r.transform.IsChildOf(expected.transform)),
                "restaurant/" + label + "/only-requested-state-visible");
            Texture palette = AssetDatabase.LoadAssetAtPath<Texture>(ArtRoot + "restaurantbits_texture.png");
            check(palette && SlotsMatch(renderers, (r, slot) => r.sharedMaterials[slot]
                    && r.sharedMaterials[slot].HasProperty("_BaseMap") && r.sharedMaterials[slot].GetTexture("_BaseMap") == palette)
                && renderers.All(r => r.GetComponent<MeshFilter>().sharedMesh.HasVertexAttribute(UnityEngine.Rendering.VertexAttribute.TexCoord0)),
                "restaurant/" + label + "/palette-texture-and-uv-details-preserved");
            BoxCollider box = food.GetComponent<BoxCollider>();
            Bounds envelope = new Bounds(box.center, box.size + Vector3.one * .025f);
            Bounds visible = LocalBounds(food.transform, renderers);
            check(visible.size.sqrMagnitude > .0001f && envelope.Contains(visible.min) && envelope.Contains(visible.max),
                "restaurant/" + label + "/active-visual-fits-current-collision-envelope");
            if (food.IsChopped && model.useCutColliderBounds)
            {
                Bounds authored = food.CutStage == 1 ? model.firstCutColliderBounds : model.secondCutColliderBounds;
                check(Vector3.Distance(box.center, authored.center) < .00001f && Vector3.Distance(box.size, authored.size) < .00001f,
                    "restaurant/" + label + "/active-cut-stage-updates-authored-collision-envelope");
            }
        }

        static Bounds LocalBounds(Transform root, MeshRenderer[] surfaces)
        {
            bool started = false;
            Bounds result = default;
            foreach (MeshRenderer renderer in surfaces)
            {
                Bounds mesh = renderer.GetComponent<MeshFilter>().sharedMesh.bounds;
                Matrix4x4 matrix = root.worldToLocalMatrix * renderer.transform.localToWorldMatrix;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 point = matrix.MultiplyPoint3x4(mesh.center + Vector3.Scale(mesh.extents,
                        new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1)));
                    if (!started) { result = new Bounds(point, Vector3.zero); started = true; }
                    else result.Encapsulate(point);
                }
            }
            return result;
        }

        static void CheckOriginalCollider(KitchenFood food, Action<bool, string> check)
        {
            Vector3 size, center;
            switch (food.definition.kind)
            {
                case FoodKind.Chicken: size = new Vector3(.5139122f, .21f, .45632225f); center = new Vector3(0f, .09f, -.013161123f); break;
                case FoodKind.Steak: size = new Vector3(.285f, .0765f, .21f); center = new Vector3(0f, .03675f, 0f); break;
                case FoodKind.Tomato: size = new Vector3(.16f, .174f, .16f); center = new Vector3(0f, .0895f, 0f); break;
                default: size = new Vector3(.19110802f, .13f, .18240555f); center = new Vector3(-.00001962483f, .065f, .0037972257f); break;
            }
            BoxCollider box = food.GetComponent<BoxCollider>();
            check(Vector3.Distance(box.size, size) < .00001f && Vector3.Distance(box.center, center) < .00001f,
                "restaurant/" + food.definition.kind + "/original-physics-dimensions-preserved");
        }
    }
}
