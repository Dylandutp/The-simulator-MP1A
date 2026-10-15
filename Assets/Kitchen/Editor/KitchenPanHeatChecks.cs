using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HouseSim.Kitchen.Editor
{
    public static class KitchenPanHeatChecks
    {
        public static IEnumerator Run(Action<bool, string> check)
        {
            var created = new List<GameObject>();
            try
            {
                var station = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Kitchen/Prefabs/Stations/InductionHob.prefab"), new Vector3(210, 1, 210), Quaternion.identity);
                created.Add(station);
                foreach (var recovery in station.GetComponentsInChildren<KitchenRespawnTool>()) recovery.enabled = false;
                var hob = station.GetComponent<KitchenHob>();
                var pan = hob.pan;
                pan.transform.SetPositionAndRotation(hob.burner.position, hob.burner.rotation);
                var panBody = pan.GetComponent<Rigidbody>(); panBody.useGravity = false;
                panBody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative; panBody.isKinematic = true;
                var heat = pan.GetComponent<KitchenPanHeat>();
                hob.enabled = heat.enabled = false;
                var audio = KitchenAudioEmitter.Get(pan);
                var visual = pan.GetComponent<KitchenPanHeatVisual>();
                check(visual && visual.BottomRenderer && visual.Smoke, "pan-heat/thermal-visuals-created-on-pan");
                Material originalMaterial = visual.BottomRenderer.sharedMaterial;
                var properties = new MaterialPropertyBlock();
                visual.Refresh();
                Color coldColor = originalMaterial.GetColor("_BaseColor");
                var steakObject = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Kitchen/Prefabs/Food/Steak.prefab"));
                created.Add(steakObject);
                var steak = steakObject.GetComponent<KitchenFood>();
                foreach (var recovery in steak.GetComponentsInChildren<KitchenRespawnTool>()) recovery.enabled = false;
                var steakBody = steak.GetComponent<Rigidbody>(); steakBody.useGravity = false;
                steakBody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative; steakBody.isKinematic = true;
                void Seat() { steak.transform.SetPositionAndRotation(pan.cookingSurface.TransformPoint(Vector3.up * pan.supportedLocalHeight), pan.cookingSurface.rotation); Physics.SyncTransforms(); }
                void Power(float value) { hob.powerKnob.SetValue(Mathf.Lerp(hob.powerKnob.minValue, hob.powerKnob.maxValue, value)); }
                void Step(float seconds) { hob.Tick(seconds); heat.Tick(seconds); }
                Seat();
                yield return null;
                Power(1f); Step(4.99f);
                check(!heat.IsHot && Near(heat.ThermalLevel, .998f) && Near(steak.CookSeconds, 0f) && !audio.LoopSource.clip,
                    "pan-heat/full-power-before-five-seconds-no-cooking-or-sizzle");
                visual.Refresh(); visual.BottomRenderer.GetPropertyBlock(properties);
                check(!visual.SmokeActive && Vector4.Distance(properties.GetColor("_BaseColor"), coldColor) > .1f
                    && visual.BottomRenderer.sharedMaterial == originalMaterial,
                    "pan-heat/preheating-tints-bottom-without-smoke-or-mutating-material");
                Step(.01f);
                check(heat.IsHot && Near(heat.ThermalLevel, 1f) && Near(steak.CookSeconds, 0f), "pan-heat/full-power-five-seconds-ready");
                visual.Refresh();
                check(visual.SmokeActive && visual.Smoke.main.maxParticles <= 24, "pan-heat/only-ready-pan-emits-bounded-light-smoke");
                Step(.5f);
                check(Near(steak.CookSeconds, 1f) && heat.IsCooking && audio.LoopSource.clip == KitchenAudioEmitter.LoadClip(KitchenSound.Cooking),
                    "pan-heat/hot-pan-cooks-and-sizzles-at-full-power-rate");
                steak.SetDesktopHeld(true); Step(1f);
                check(Near(steak.CookSeconds, 1f) && !heat.IsCooking, "pan-heat/held-steak-does-not-cook");
                steak.SetDesktopHeld(false);
                Power(0); Step(7.5f);
                check(heat.IsHot && !heat.Heating && Near(heat.ThermalLevel, .5f) && Near(heat.ResidualSeconds, 7.5f)
                    && Near(heat.CookingRate, 2f) && Near(steak.CookSeconds, 16f), "pan-heat/off-retains-normal-rate-while-visuals-fade");
                visual.Refresh();
                check(visual.SmokeActive && Near(visual.DisplayedThermalLevel, .5f)
                    && visual.Smoke.emission.rateOverTime.constant > 0f && visual.Smoke.emission.rateOverTime.constant < 6f,
                    "pan-heat/residual-color-and-smoke-fade-with-temperature");
                Step(8f);
                check(!heat.IsHot && Near(heat.ThermalLevel, 0f) && Near(steak.CookSeconds, 31f) && !heat.IsCooking,
                    "pan-heat/residual-cooking-clamped-to-exactly-fifteen-seconds");
                visual.Refresh();
                check(!visual.SmokeActive && Near(visual.DisplayedThermalLevel, 0f), "pan-heat/cooled-pan-restores-cold-visual-state");
                Step(1f);
                check(Near(steak.CookSeconds, 31f), "pan-heat/cooled-pan-stays-inactive");

                Power(.5f); Step(9.99f);
                check(!heat.IsHot && Near(steak.CookSeconds, 31f), "pan-heat/half-power-still-preheating-before-ten-seconds");
                Step(.01f); Step(.2f);
                check(heat.IsHot && Near(heat.CookingRate, 1f) && Near(steak.CookSeconds, 31.2f), "pan-heat/half-power-ten-seconds-then-normal-cooking");
                pan.transform.position += Vector3.right * 2f; Seat(); Step(3f);
                check(heat.IsHot && !heat.Heating && Near(heat.ThermalLevel, .8f) && Near(steak.CookSeconds, 34.2f),
                    "pan-heat/lifted-away-pan-keeps-cooking-for-residual-duration");
                pan.transform.position = hob.burner.position; Seat(); Step(0f);
                check(heat.Heating && heat.IsHot && Near(heat.ThermalLevel, 1f) && Near(heat.ResidualSeconds, 15f),
                    "pan-heat/returning-hot-pan-immediately-restores-full-heat");
                Power(0); Step(15f);
                float before = steak.CookSeconds;
                Power(1); Step(2f); Power(0); Step(3f);
                check(!heat.IsHot && Near(heat.ThermalLevel, .2f) && Near(steak.CookSeconds, before),
                    "pan-heat/partly-warm-pan-cools-gradually-without-cooking");
                Power(1); Step(4f);
                check(heat.IsHot && Near(steak.CookSeconds, before), "pan-heat/preheat-continues-from-retained-partial-temperature");
                heat.MarkHeatingFrame(.5f, 1f); heat.MarkHeatingFrame(1f, 2f); heat.MarkHeatingFrame(.5f, 1f); heat.Tick(1f);
                check(Near(steak.CookSeconds, before + 2f), "pan-heat/multiple-burner-reports-never-double-cook");
                heat.Tick(1f);
                check(!heat.Heating && Near(heat.ResidualSeconds, 14f), "pan-heat/burner-input-consumed-once-not-sticky");

                Power(0); Step(20f);
                pan.transform.position += Vector3.right * 2f; Seat(); Power(1); Step(8f);
                check(!heat.IsHot && Near(heat.ThermalLevel, 0f), "pan-heat/cold-pan-away-from-powered-burner-cannot-preheat");
                pan.transform.position = hob.burner.position; Seat();
                var held = pan.GetComponent<KitchenDesktopHeld>();
                if (!held) held = pan.gameObject.AddComponent<KitchenDesktopHeld>();
                held.SetHeld(true); Step(8f);
                check(!heat.IsHot && Near(heat.ThermalLevel, 0f), "pan-heat/cold-held-pan-does-not-preheat");
                held.SetHeld(false); Step(5f);
                before = steak.CookSeconds;
                steak.transform.position = pan.cookingSurface.TransformPoint(Vector3.up * .05f); Physics.SyncTransforms(); Step(1f);
                check(Near(steak.CookSeconds, before) && !heat.IsCooking, "pan-heat/food-above-pan-surface-does-not-cook");
                Seat(); steakBody.isKinematic = false; held.SetHeld(true); pan.BeginCarry();
                check(pan.TryToss(.5f), "pan-heat/gentle-toss-fixture-launched");
                Step(1f);
                check(Near(steak.CookSeconds, before), "pan-heat/airborne-toss-does-not-cook");
                check(pan.TryToss(1.4f), "pan-heat/strong-toss-fixture-spilled");
                Step(1f);
                check(steak.IsDirty && Near(steak.CookSeconds, before), "pan-heat/spilled-steak-not-heated-even-before-clearing-pan-volume");
                held.SetHeld(false); steak.gameObject.SetActive(false);

                Power(0); Step(30f); Power(1f);
                hob.enabled = heat.enabled = true;
                yield return new WaitForSeconds(.2f);
                check(heat.Heating && heat.ThermalLevel > .015f && heat.ThermalLevel < .15f && !heat.IsHot,
                    "pan-heat/update-to-lateupdate-pipeline-preheats-once-per-frame");
                check(pan.GetComponents<KitchenPanHeat>().Length == 1, "pan-heat/pan-has-one-temperature-owner");
                CapturePreview(created, check);
            }
            finally
            {
                foreach (GameObject instance in created) if (instance) { instance.SetActive(false); Object.Destroy(instance); }
            }
            yield return null;
        }

        static bool Near(float a, float b) => Mathf.Abs(a - b) < .002f;

        static void CapturePreview(List<GameObject> created, Action<bool, string> check)
        {
            Vector3 origin = new Vector3(216f, 5f, 216f);
            var root = new GameObject("Pan thermal state preview");
            created.Add(root);
            var station = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Kitchen/Prefabs/Stations/InductionHob.prefab");
            KitchenPan source = station.GetComponentInChildren<KitchenPan>();
            var table = GameObject.CreatePrimitive(PrimitiveType.Cube);
            table.transform.SetParent(root.transform, false);
            table.transform.position = origin + new Vector3(0f, -.043f, -.14f);
            table.transform.localScale = new Vector3(4.1f, .05f, 1.35f);
            table.GetComponent<Collider>().enabled = false;
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.SetColor("_BaseColor", new Color(.065f, .095f, .125f));
            material.SetFloat("_Smoothness", .15f);
            table.GetComponent<Renderer>().sharedMaterial = material;
            string[] labels = { "COLD\n0%", "PREHEATING\n2.5 s / 50%", "HOT\n5 s at full power", "RESIDUAL\n7.5 s after lift" };
            try
            {
                for (int i = 0; i < 4; i++)
                {
                    Vector3 position = origin + Vector3.right * ((i - 1.5f) * .95f);
                    var pan = Object.Instantiate(source.gameObject, position, Quaternion.identity, root.transform).GetComponent<KitchenPan>();
                    foreach (var recovery in pan.GetComponentsInChildren<KitchenRespawnTool>()) recovery.enabled = false;
                    Rigidbody body = pan.GetComponent<Rigidbody>();
                    body.useGravity = false; body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative; body.isKinematic = true;
                    var heat = pan.GetComponent<KitchenPanHeat>(); heat.enabled = false;
                    var visual = pan.GetComponent<KitchenPanHeatVisual>();
                    if (i > 0)
                    {
                        heat.MarkHeatingFrame(1f, 2f); heat.Tick(i == 1 ? 2.5f : 5f);
                        if (i == 3) heat.Tick(7.5f);
                    }
                    visual.Refresh();
                    if (visual.SmokeActive) visual.Smoke.Simulate(.85f, false, true, false);
                    check(visual.BottomRenderer && Near(visual.DisplayedThermalLevel, i == 0 ? 0f : i == 2 ? 1f : .5f)
                        && visual.SmokeActive == (i >= 2), "pan-heat/preview-state-" + i);
                    var labelObject = new GameObject("Pan preview label");
                    labelObject.transform.SetParent(root.transform, false);
                    labelObject.transform.SetPositionAndRotation(position + new Vector3(0f, .006f, -.74f), Quaternion.Euler(90f, 0f, 0f));
                    var text = labelObject.AddComponent<TextMesh>();
                    text.text = labels[i]; text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                    text.fontSize = 64; text.characterSize = .009f; text.anchor = TextAnchor.MiddleCenter; text.alignment = TextAlignment.Center;
                    text.color = new Color(.94f, .95f, .90f);
                    text.GetComponent<Renderer>().sharedMaterial = text.font.material;
                }
                var light = new GameObject("Pan preview fill").AddComponent<Light>();
                light.transform.SetParent(root.transform, false); light.transform.position = origin + new Vector3(-.5f, 2.5f, -1.2f);
                light.type = LightType.Point; light.range = 7f; light.intensity = 5f;
                var camera = new GameObject("Pan preview camera").AddComponent<Camera>();
                camera.transform.SetParent(root.transform, false);
                camera.enabled = false; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.035f, .055f, .075f);
                camera.nearClipPlane = .02f; camera.farClipPlane = 10f;
                camera.transform.position = origin + new Vector3(0f, 2.5f, -2.7f);
                camera.transform.LookAt(origin + new Vector3(0f, .04f, -.20f));
                camera.orthographic = true; camera.orthographicSize = .90f;
                KitchenBuildTools.Capture(camera, "Builds/Kitchen-PanHeat.png", 2000, 700);
            }
            finally
            {
                root.SetActive(false);
                Object.Destroy(root);
                Object.Destroy(material);
            }
        }
    }
}
