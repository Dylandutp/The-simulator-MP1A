using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HouseSim.Kitchen.Editor
{
    /// <summary>Motion tests exercise the held utensils against actual saved prefab geometry.</summary>
    public static class KitchenGestureChecks
    {
        static readonly Vector3 Origin = new Vector3(100f, 4f, 100f);

        public static IEnumerator Run(Action<bool, string> check)
        {
            yield return KnifeGestures(check);
            yield return PanGestures(check);
            yield return CaptureCutting(check);
        }

        static KitchenFood MakeFood(string kind, Vector3 position, bool frozen = true)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Kitchen/Prefabs/Food/" + kind + ".prefab");
            var food = Object.Instantiate(prefab, position, Quaternion.identity).GetComponent<KitchenFood>();
            Freeze(food, frozen);
            return food;
        }

        static void Freeze(KitchenFood food, bool frozen = true)
        {
            if (!food) return;
            var recovery = food.GetComponent<KitchenRespawnTool>();
            if (recovery) recovery.enabled = false;
            var body = food.GetComponent<Rigidbody>();
            body.useGravity = !frozen;
            body.isKinematic = frozen;
            body.collisionDetectionMode = frozen ? CollisionDetectionMode.ContinuousSpeculative : CollisionDetectionMode.ContinuousDynamic;
        }

        static KitchenFood CurrentFood(Vector3 position, FoodKind kind)
        {
            return Object.FindObjectsByType<KitchenFood>(FindObjectsSortMode.None).FirstOrDefault(food =>
                !food.IsConsumed && food.definition.kind == kind && Vector3.Distance(food.transform.position, position) < .4f);
        }

        static void MoveEdge(KitchenKnife knife, Vector3 point)
        {
            knife.transform.position += point - knife.BladeEdge;
            Physics.SyncTransforms();
        }

        static IEnumerator KnifeGestures(Action<bool, string> check)
        {
            var station = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Kitchen/Prefabs/Stations/PrepStation.prefab");
            var knife = Object.Instantiate(station.GetComponentInChildren<KitchenKnife>().gameObject, Origin + Vector3.up, Quaternion.identity).GetComponent<KitchenKnife>();
            var knifeBody = knife.GetComponent<Rigidbody>();
            knifeBody.useGravity = false; knifeBody.isKinematic = true;
            knifeBody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            var marker = knife.gameObject.AddComponent<KitchenDesktopHeld>();
            var food = MakeFood("Apple", Origin);
            marker.SetHeld(true);
            MoveEdge(knife, Origin + new Vector3(-.4f, .08f, 0f));
            yield return null;
            // A fast sideways swipe overlaps the blade/food volumes without downward travel.
            for (int i = 0; i <= 16; i++)
            {
                MoveEdge(knife, Origin + new Vector3(-.4f + i * .05f, .08f, 0f));
                yield return new WaitForSeconds(.015f);
            }
            check(food && food.CutStage == 0, "knife/lateral-swipe-does-not-cut");
            marker.SetHeld(false);
            MoveEdge(knife, Origin + new Vector3(0f, -.08f, 0f));
            yield return null;
            marker.SetHeld(true);
            yield return null;
            for (int i = 0; i <= 18; i++)
            {
                MoveEdge(knife, Origin + new Vector3(0f, -.08f + i * .02f, 0f));
                yield return new WaitForSeconds(.015f);
            }
            check(food && food.CutStage == 0, "knife/upward-contact-does-not-cut");
            marker.SetHeld(false);
            MoveEdge(knife, Origin + new Vector3(0f, .08f, 0f));
            yield return null;
            marker.SetHeld(true);
            yield return new WaitForSeconds(.45f);
            check(food && food.CutStage == 0, "knife/resting-contact-does-not-cut");
            check(Physics.GetIgnoreCollision(knife.GetComponentsInChildren<Collider>().First(collider => collider.attachedRigidbody == knifeBody), food.GetComponent<Collider>()),
                "knife/contact-does-not-swat-food");

            // Move the held geometry itself, as a tracked controller would, with no C-key helper.
            for (int i = 0; i <= 12; i++)
            {
                MoveEdge(knife, Origin + new Vector3(0f, .08f + i * .02f, 0f));
                yield return new WaitForSeconds(.015f);
            }
            for (int i = 0; i <= 16; i++)
            {
                MoveEdge(knife, Origin + new Vector3(0f, .32f - i * .015f, 0f));
                yield return new WaitForSeconds(.015f);
                var current = CurrentFood(Origin, FoodKind.Apple);
                if (current) { food = current; Freeze(food); }
            }
            check(food && food.CutStage == 1 && food.GetComponent<KitchenCutVisual>().PieceCount == 2,
                "knife/held-downward-stroke-makes-two-halves");
            yield return new WaitForSeconds(.5f);
            check(food && food.CutStage == 1 && !knife.StrokeArmed, "knife/one-stroke-cannot-double-cut");
            // Lift without touching the food; the PC helper then performs its visible second stroke.
            for (int i = 0; i <= 8; i++)
            {
                MoveEdge(knife, Origin + new Vector3(0f, .08f + i * .02f, 0f));
                yield return new WaitForSeconds(.015f);
            }
            check(knife.StrokeArmed, "knife/lift-rearms-next-stroke");
            check(knife.TryDesktopChop(food), "knife/desktop-animation-starts-with-held-knife");
            yield return new WaitForSeconds(.75f);
            check(food && food.CutStage == 2 && food.GetComponent<KitchenCutVisual>().PieceCount == 4,
                "knife/second-downstroke-makes-four-grouped-pieces");
            check(!knife.IsDesktopCutting && !knife.TryDesktopChop(food), "knife/no-fifth-piece-or-repeat-animation");
            food.Discard();
            marker.SetHeld(false);
            Object.Destroy(knife.gameObject);
            yield return null;
        }

        static IEnumerator PanGestures(Action<bool, string> check)
        {
            var station = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Kitchen/Prefabs/Stations/InductionHob.prefab");
            var pan = Object.Instantiate(station.GetComponentInChildren<KitchenPan>().gameObject, Origin + Vector3.right * 2f, Quaternion.identity).GetComponent<KitchenPan>();
            var body = pan.GetComponent<Rigidbody>();
            body.useGravity = false; body.isKinematic = true; body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            var marker = pan.gameObject.AddComponent<KitchenDesktopHeld>();
            marker.SetHeld(true);
            Vector3 seat = pan.cookingSurface.TransformPoint(new Vector3(0f, pan.supportedLocalHeight, 0f));
            var steak = MakeFood("Steak", seat, false);
            Physics.SyncTransforms();
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            check(pan.SupportedSteakCount == 1, "pan/held-pan-supports-steak");
            float baseY = steak.GetComponent<Rigidbody>().position.y;
            check(pan.TryToss(.5f) && pan.IsTossing, "pan/gentle-toss-is-airborne");
            float highest = baseY;
            float largestDrift = 0f;
            float until = Time.time + 1.2f;
            while (pan.IsTossing && Time.time < until)
            {
                yield return new WaitForFixedUpdate();
                Vector3 at = steak.GetComponent<Rigidbody>().position;
                highest = Mathf.Max(highest, at.y);
                largestDrift = Mathf.Max(largestDrift, Vector3.ProjectOnPlane(at - seat, Vector3.up).magnitude);
            }
            check(highest - baseY > .025f && highest - baseY < .24f, "pan/toss-has-visible-but-limited-height");
            check(largestDrift < .25f && pan.CatchCount == 1 && !pan.IsTossing && pan.SupportedSteakCount == 1,
                "pan/gentle-toss-caught-near-pan");
            check(!steak.IsDirty && pan.EscapedSteakCount == 0, "pan/caught-steak-remains-clean");
            check(pan.TryToss(1.4f) && steak.IsDirty && pan.EscapedSteakCount == 1 && pan.SupportedSteakCount == 0,
                "pan/excessive-flick-escapes-and-contaminates");
            yield return new WaitForSeconds(.15f);
            check(Vector3.Distance(steak.GetComponent<Rigidbody>().position, seat) > .2f, "pan/spilled-steak-is-actually-thrown");
            steak.ApplyHeat(10f);
            var session = KitchenSession.Instance;
            int previousEnergy = session.Energy;
            session.ChangeEnergy(50 - session.Energy);
            check(steak.State == FoodState.Good && steak.TryConsume() && session.Energy == 35,
                "pan/perfectly-cooked-spilled-steak-still-costs-energy");
            session.ChangeEnergy(previousEnergy - session.Energy);
            marker.SetHeld(false);
            Object.Destroy(pan.gameObject);
            yield return null;
        }

        static IEnumerator CaptureCutting(Action<bool, string> check)
        {
            var specimens = new List<KitchenFood>();
            string[] names = { "Apple", "Tomato", "Lettuce" };
            var sceneRoot = new GameObject("Cut geometry preview");
            var table = GameObject.CreatePrimitive(PrimitiveType.Cube);
            table.transform.SetParent(sceneRoot.transform);
            table.transform.position = Origin + Vector3.down * .025f;
            table.transform.localScale = new Vector3(1.65f, .025f, 1.20f);
            var tableMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            tableMaterial.color = new Color(.13f, .18f, .20f);
            table.GetComponent<Renderer>().sharedMaterial = tableMaterial;
            for (int row = 0; row < 2; row++)
            for (int column = 0; column < 3; column++)
            {
                Vector3 location = Origin + new Vector3((column - 1) * .45f, 0f, (row - .5f) * .52f);
                var whole = MakeFood(names[column], location);
                whole.Wash(.1f);
                whole.Chop();
                var cut = CurrentFood(location, whole.definition.kind);
                Freeze(cut);
                if (row == 1) cut.Chop();
                specimens.Add(cut);
                var labelObject = new GameObject("Cut label");
                labelObject.transform.SetParent(sceneRoot.transform);
                labelObject.transform.SetPositionAndRotation(location + new Vector3(0f, .002f, -.16f), Quaternion.Euler(90f, 0f, 0f));
                var label = labelObject.AddComponent<TextMesh>();
                label.text = names[column] + " / " + KitchenRestaurantModelChecks.ExpectedPieceCount(cut, cut.CutStage);
                label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                label.fontSize = 64; label.characterSize = .006f; label.anchor = TextAnchor.MiddleCenter;
                label.color = new Color(.96f, .94f, .83f);
                label.GetComponent<Renderer>().sharedMaterial = label.font.material;
            }
            yield return new WaitForSeconds(.65f);
            check(specimens.All(food => food.GetComponent<KitchenCutVisual>().PieceCount == KitchenRestaurantModelChecks.ExpectedPieceCount(food, food.CutStage)
                && KitchenRestaurantModelChecks.ActiveRenderers(food).All(renderer => renderer.GetComponent<MeshFilter>()
                    && renderer.GetComponent<MeshFilter>().sharedMesh && renderer.GetComponent<MeshFilter>().sharedMesh.vertexCount > 0)),
                "cut/selected-model-pieces-have-visible-geometry-for-all-produce");
            check(specimens.Where(food => food.definition.kind == FoodKind.Apple).All(food =>
                KitchenRestaurantModelChecks.ActiveRenderers(food).Any(renderer => renderer.GetComponent<MeshFilter>().sharedMesh.subMeshCount == 2)),
                "cut/apple-procedural-halves-and-quarters-retain-closed-cut-faces");
            var cameraObject = new GameObject("Cut preview camera");
            cameraObject.transform.SetParent(sceneRoot.transform);
            var camera = cameraObject.AddComponent<Camera>();
            camera.enabled = false; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.06f, .09f, .11f);
            camera.transform.position = Origin + new Vector3(0f, 1.60f, -1.05f);
            camera.transform.LookAt(Origin + new Vector3(0f, 0f, .025f));
            camera.orthographic = true; camera.orthographicSize = .68f;
            var lightObject = new GameObject("Cut preview fill");
            lightObject.transform.SetParent(sceneRoot.transform);
            lightObject.transform.rotation = Quaternion.Euler(45f, -30f, 0f);
            var light = lightObject.AddComponent<Light>(); light.type = LightType.Directional; light.intensity = 1.2f;
            var target = new RenderTexture(1600, 1000, 24);
            camera.targetTexture = target;
            RenderTexture previous = RenderTexture.active;
            var pixels = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            try
            {
                camera.Render();
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); pixels.Apply();
                Directory.CreateDirectory("Builds");
                File.WriteAllBytes("Builds/Kitchen-Cutting.png", pixels.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous; camera.targetTexture = null;
                Object.Destroy(pixels); target.Release(); Object.Destroy(target);
                foreach (var food in specimens) if (food) food.Discard();
                Object.Destroy(tableMaterial); Object.Destroy(sceneRoot);
            }
            yield return null;
        }
    }
}
