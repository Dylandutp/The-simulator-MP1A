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
    public static class KitchenBowlVisualChecks
    {
        public static IEnumerator Run(Action<bool, string> check)
        {
            var spawned = new List<GameObject>();
            try
            {
                GameObject station = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Kitchen/Prefabs/Stations/PrepStation.prefab"), new Vector3(86f, 0f, 86f), Quaternion.identity);
                spawned.Add(station);
                KitchenSaladBowl bowl = station.GetComponentInChildren<KitchenSaladBowl>();
                foreach (var recovery in station.GetComponentsInChildren<KitchenRespawnTool>()) recovery.enabled = false;
                foreach (Rigidbody body in station.GetComponentsInChildren<Rigidbody>())
                { body.useGravity = false; body.isKinematic = true; }
                KitchenFood apple = Spawn("Apple", new Vector3(90f, 2f, 86f), spawned);
                KitchenFood tomato = Spawn("Tomato", new Vector3(91f, 2f, 86f), spawned);
                yield return null;
                apple.Wash(.1f);
                apple = Cut(apple, 2, spawned);
                tomato = Cut(tomato, 1, spawned);
                yield return null;
                var inventory = KitchenSession.Instance.inventory;
                int appleBefore = inventory.GetAmount(apple.CurrentResource);
                Mesh sourceAppleMesh = KitchenRestaurantModelChecks.ActiveRenderers(apple)[0].GetComponent<MeshFilter>().sharedMesh;
                check(bowl.TryAddIngredient(apple), "bowl-visual/accepts-chopped-apple");
                check(!bowl.TryAddIngredient(apple) && bowl.VisibleIngredientCount == 1,
                    "bowl-visual/consumed-portion-cannot-create-duplicate-contents");
                check(inventory.GetAmount(apple.CurrentResource) == appleBefore,
                    "bowl-visual/transfer-has-no-extra-inventory-portion");
                check(!bowl.Mix() && bowl.VisibleIngredientCount == 1 && bowl.ingredientCount == 1,
                    "bowl-visual/incomplete-recipe-keeps-pieces");

                var properties = new MaterialPropertyBlock();
                MeshRenderer appleCopy = bowl.ContentsRoot.GetComponentsInChildren<MeshRenderer>()[0];
                appleCopy.GetPropertyBlock(properties);
                Color partlyClean = properties.GetColor("_BaseColor");
                Color fullyClean = apple.definition.rawColor;
                yield return new WaitForSeconds(.65f);
                appleCopy.GetPropertyBlock(properties);
                check(Vector4.Distance(partlyClean, fullyClean) > .01f
                    && Vector4.Distance(properties.GetColor("_BaseColor"), fullyClean) < .01f,
                    "bowl-visual/partly-faded-clean-ingredient-continues-color-transition");

                MeshRenderer[] tomatoSurfaces = KitchenRestaurantModelChecks.ActiveRenderers(tomato);
                Mesh[] sourceTomatoMeshes = tomatoSurfaces.Select(r => r.GetComponent<MeshFilter>().sharedMesh).ToArray();
                Material[][] expectedMaterials = tomatoSurfaces.Select(r => r.sharedMaterials).ToArray();
                Color[][] expectedColors = tomatoSurfaces.Select(r => Enumerable.Range(0, r.sharedMaterials.Length)
                    .Select(slot => KitchenRestaurantModelChecks.EffectiveColor(r, slot)).ToArray()).ToArray();
                check(bowl.TryAddIngredient(tomato), "bowl-visual/accepts-second-kind");
                Transform tomatoCopy = bowl.ContentsRoot.GetChild(1);
                MeshRenderer[] copiedTomato = tomatoCopy.GetComponentsInChildren<MeshRenderer>();
                check(copiedTomato.Length == tomatoSurfaces.Length && copiedTomato.Length > 0,
                    "bowl-visual/copies-every-active-tomato-model-surface");
                check(copiedTomato.Select((r, i) => r.sharedMaterials.SequenceEqual(expectedMaterials[i])
                    && Enumerable.Range(0, r.sharedMaterials.Length).All(slot =>
                        Vector4.Distance(KitchenRestaurantModelChecks.EffectiveColor(r, slot), expectedColors[i][slot]) < .001f)).All(v => v),
                    "bowl-visual/preserves-all-materials-textures-and-indexed-cleanliness-colors");
                check(copiedTomato.Select((r, i) => {
                    Mesh copy = r.GetComponent<MeshFilter>().sharedMesh;
                    return copy != sourceTomatoMeshes[i] && copy.vertexCount == sourceTomatoMeshes[i].vertexCount
                        && copy.uv.SequenceEqual(sourceTomatoMeshes[i].uv);
                }).All(v => v), "bowl-visual/independent-model-copies-retain-palette-uvs");
                Mesh[] copiedMeshes = bowl.ContentsRoot.GetComponentsInChildren<MeshFilter>().Select(f => f.sharedMesh).ToArray();
                yield return null;
                yield return null;
                check(!apple && !tomato && !sourceAppleMesh && copiedMeshes.Length > 0 && copiedMeshes.All(m => m),
                    "bowl-visual/independent-meshes-survive-original-food-destruction");
                Transform[] pieces = bowl.ContentsRoot.GetComponentsInChildren<Transform>();
                check(pieces.Count(t => t.name.StartsWith("Quarter ")) == 4
                    && copiedTomato.All(r => r && r.enabled) && sourceTomatoMeshes.All(mesh => mesh),
                    "bowl-visual/retains-apple-quarters-and-authored-tomato-pieces-without-destroying-assets");
                check(bowl.ContentsRoot.GetComponentsInChildren<Collider>().Length == 0
                    && bowl.ContentsRoot.GetComponentsInChildren<Rigidbody>().Length == 0
                    && bowl.ContentsRoot.GetComponentsInChildren<XRGrabInteractable>().Length == 0
                    && bowl.ContentsRoot.GetComponentsInChildren<KitchenFood>().Length == 0,
                    "bowl-visual/contents-cannot-collide-be-collected-or-double-consumed");
                Vector3 localPosition = tomatoCopy.localPosition;
                Vector3 bowlPosition = bowl.transform.position;
                bowl.transform.position += Vector3.right * .2f;
                check(tomatoCopy.localPosition == localPosition && tomatoCopy.IsChildOf(bowl.transform),
                    "bowl-visual/contents-follow-bowl-without-physics");
                bowl.transform.position = bowlPosition;

                GameObject saladPrefab = bowl.saladPrefab;
                bowl.saladPrefab = null;
                check(!bowl.Mix() && bowl.ingredientCount == 2 && bowl.VisibleIngredientCount == 2
                    && bowl.ContentsRoot.gameObject.activeInHierarchy, "bowl-visual/failed-mix-retains-visible-ingredients");
                bowl.saladPrefab = saladPrefab;
                CaptureContents(bowl, spawned);

                ResourceData saladResource = saladPrefab.GetComponent<KitchenFood>().CurrentResource;
                int saladBefore = inventory.GetAmount(saladResource);
                var previousFoods = new HashSet<KitchenFood>(Object.FindObjectsByType<KitchenFood>(FindObjectsSortMode.None));
                check(bowl.Mix() && bowl.ingredientCount == 0 && bowl.VisibleIngredientCount == 0 && !bowl.ContentsRoot,
                    "bowl-visual/successful-mix-clears-original-contents");
                KitchenFood[] outputs = Object.FindObjectsByType<KitchenFood>(FindObjectsSortMode.None)
                    .Where(f => !previousFoods.Contains(f) && f.definition.kind == FoodKind.Salad).ToArray();
                foreach (KitchenFood output in outputs) { spawned.Add(output.gameObject); Freeze(output.gameObject); }
                check(outputs.Length == 1 && outputs[0].IsDirty && inventory.GetAmount(saladResource) == saladBefore + 1,
                    "bowl-visual/one-dirty-salad-output-with-correct-shared-inventory");
                check(!bowl.Mix() && inventory.GetAmount(saladResource) == saladBefore + 1,
                    "bowl-visual/repeated-mix-does-not-duplicate-output");
                yield return null;
                check(copiedMeshes.All(m => !m), "bowl-visual/mix-releases-owned-meshes");

                KitchenFood lettuce = Spawn("Lettuce", new Vector3(92f, 2f, 86f), spawned);
                yield return null;
                lettuce.Wash(.1f);
                lettuce = Cut(lettuce, 1, spawned);
                bowl.TryAddIngredient(lettuce);
                Mesh[] finalMeshes = bowl.ContentsRoot.GetComponentsInChildren<MeshFilter>().Select(f => f.sharedMesh).ToArray();
                MeshRenderer[] lettuceCopies = bowl.ContentsRoot.GetComponentsInChildren<MeshRenderer>();
                Color[] earlyLettuceColors = lettuceCopies.Select(r => KitchenRestaurantModelChecks.EffectiveColor(r, 0)).ToArray();
                yield return new WaitForSeconds(.65f);
                check(lettuceCopies.Select((r, i) => Vector4.Distance(earlyLettuceColors[i], KitchenRestaurantModelChecks.MaterialColor(r.sharedMaterials[0])) > .01f
                        && Enumerable.Range(0, r.sharedMaterials.Length).All(slot => Vector4.Distance(
                            KitchenRestaurantModelChecks.EffectiveColor(r, slot), KitchenRestaurantModelChecks.MaterialColor(r.sharedMaterials[slot])) < .01f)).All(v => v),
                    "bowl-visual/imported-model-indexed-tints-finish-wash-fade-after-transfer");
                Object.Destroy(station);
                yield return null;
                yield return null;
                check(finalMeshes.Length > 0 && finalMeshes.All(m => !m), "bowl-visual/destroyed-bowl-releases-owned-meshes");
            }
            finally
            {
                foreach (GameObject instance in spawned) if (instance) Object.Destroy(instance);
            }
            yield return null;
        }

        private static KitchenFood Spawn(string name, Vector3 position, List<GameObject> spawned)
        {
            GameObject instance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Kitchen/Prefabs/Food/" + name + ".prefab"), position, Quaternion.identity);
            spawned.Add(instance);
            Freeze(instance);
            return instance.GetComponent<KitchenFood>();
        }

        private static KitchenFood Cut(KitchenFood whole, int cuts, List<GameObject> spawned)
        {
            Vector3 position = whole.transform.position;
            KitchenFoodDefinition definition = whole.definition;
            if (!whole.Chop()) throw new InvalidOperationException("Bowl visual fixture could not chop produce.");
            KitchenFood replacement = Object.FindObjectsByType<KitchenFood>(FindObjectsSortMode.None).Single(f =>
                f.IsChopped && !f.IsConsumed && f.definition == definition && Vector3.Distance(f.transform.position, position) < .01f);
            spawned.Add(replacement.gameObject);
            Freeze(replacement.gameObject);
            if (cuts == 2) replacement.Chop();
            return replacement;
        }

        private static void Freeze(GameObject instance)
        {
            foreach (var recovery in instance.GetComponentsInChildren<KitchenRespawnTool>()) recovery.enabled = false;
            Rigidbody body = instance.GetComponent<Rigidbody>();
            if (body) { body.useGravity = false; body.isKinematic = true; }
        }

        private static void CaptureContents(KitchenSaladBowl bowl, List<GameObject> spawned)
        {
            var cameraObject = new GameObject("Bowl contents verification camera");
            spawned.Add(cameraObject);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.enabled = false;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.12f, .15f, .17f);
            camera.fieldOfView = 38f;
            camera.nearClipPlane = .02f;
            camera.transform.position = bowl.transform.position + new Vector3(.40f, .61f, -.63f);
            camera.transform.LookAt(bowl.transform.position + Vector3.up * .07f);
            var lightObject = new GameObject("Bowl contents verification light");
            spawned.Add(lightObject);
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 3f;
            light.intensity = 3f;
            light.transform.position = bowl.transform.position + new Vector3(-.3f, .8f, -.3f);
            KitchenBuildTools.Capture(camera, "Builds/Kitchen-BowlContents.png", 1100, 900);
            Object.Destroy(cameraObject);
            Object.Destroy(lightObject);
        }
    }
}
