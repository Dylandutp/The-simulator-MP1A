using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HouseSim.Kitchen.Editor
{
    /// <summary>Checks action-to-sound wiring without relying on a batch machine's audio output device.</summary>
    public static class KitchenAudioChecks
    {
        static readonly Vector3 Origin = new Vector3(150f, 4f, 150f);
        static readonly Dictionary<KitchenSound, string> Clips = new Dictionary<KitchenSound, string>
        {
            { KitchenSound.DoorOpen, "door-open" }, { KitchenSound.DoorClose, "door-close" },
            { KitchenSound.TapWater, "tap-water" }, { KitchenSound.Cooking, "cooking" },
            { KitchenSound.Cut, "cut" }, { KitchenSound.Complete, "complete" },
            { KitchenSound.StoveTurn, "stove-turning-on-96102" }, { KitchenSound.OvenSpin, "oven-spin" },
            { KitchenSound.ClockTick, "clock-ticking-down" }, { KitchenSound.OvenDone, "oven-done" },
            { KitchenSound.LightSwitch, "light-switch-382712" },
            { KitchenSound.Trash, "trashcan" }, { KitchenSound.Eating, "eating" },
            { KitchenSound.Nice, "nice" }, { KitchenSound.Spew, "spew" }
        };

        public static IEnumerator Run(Action<bool, string> check)
        {
            var created = new List<GameObject>();
            try
            {
                foreach (var pair in Clips)
                {
                    AudioClip clip = Clip(pair.Key);
                    check(clip && clip.length > 0f && clip.samples > 0 && KitchenAudioEmitter.LoadClip(pair.Key) == clip,
                        "audio/assets/" + pair.Key);
                }
                GameObject fridge = Station("Fridge", Origin, created);
                fridge.GetComponent<KitchenFridge>().enabled = false; // No test-only stock is needed.
                var oven = Station("Oven", Origin + Vector3.right * 3f, created).GetComponent<KitchenOven>();
                var hob = Station("InductionHob", Origin + Vector3.right * 6f, created).GetComponent<KitchenHob>();
                var wash = Station("WashStation", Origin + Vector3.right * 9f, created).GetComponentInChildren<KitchenWashZone>();
                var prep = Station("PrepStation", Origin + Vector3.right * 12f, created);
                yield return null; // Oven timer listeners are registered in Start.

                Doors(check, fridge.GetComponent<KitchenFridge>().door, "fridge");
                Doors(check, oven.door, "oven");
                yield return Knob(check, oven.timerKnob, KitchenSound.OvenSpin, "oven");
                oven.timerKnob.SetValue(0f);
                yield return Knob(check, hob.powerKnob, KitchenSound.StoveTurn, "hob");
                hob.powerKnob.SetValue(0f);
                Faucet(check, wash.faucet);
                Oven(check, oven);
                Hob(check, hob, created);
                Salad(check, prep.GetComponentInChildren<KitchenSaladBowl>(), created);
                yield return Knife(check, prep.GetComponentInChildren<KitchenKnife>(), created);

                var switchObject = new GameObject("Audio check light switch");
                created.Add(switchObject);
                switchObject.transform.position = Origin;
                var lights = switchObject.AddComponent<KitchenPendantLights>();
                var lightAudio = KitchenAudioEmitter.Get(lights);
                check(lightAudio.CueCount(KitchenSound.LightSwitch) == 0, "audio/lights/startup-silent");
                lights.Toggle(); lights.Toggle(); lights.Apply();
                check(lightAudio.CueCount(KitchenSound.LightSwitch) == 2 && lightAudio.OneShotSource.clip == Clip(KitchenSound.LightSwitch),
                    "audio/lights/each-toggle-not-apply");
                check(lightAudio.OneShotSource.spatialBlend == 1f && !lightAudio.OneShotSource.playOnAwake
                    && lightAudio.OneShotSource.dopplerLevel == 0f, "audio/emitter/spatial-and-no-startup-or-motion-pitch");
            }
            finally
            {
                // Includes replacement cut portions and the bowl output, which are spawned by gameplay.
                foreach (var food in Object.FindObjectsByType<KitchenFood>(FindObjectsSortMode.None))
                    if (Vector3.Distance(food.transform.position, Origin) < 20f && !food.IsConsumed) food.Discard();
                foreach (GameObject item in created)
                    if (item) { item.SetActive(false); Object.Destroy(item); }
            }
            yield return null;
        }

        static AudioClip Clip(KitchenSound sound) => Resources.Load<AudioClip>("KitchenAudio/" + Clips[sound]);
        static bool Loop(KitchenAudioEmitter emitter, KitchenSound sound) => emitter.LoopSource && emitter.LoopSource.clip == Clip(sound);
        static bool SilentLoop(KitchenAudioEmitter emitter) => !emitter.LoopSource || !emitter.LoopSource.clip;

        static GameObject Station(string name, Vector3 position, List<GameObject> created)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Kitchen/Prefabs/Stations/" + name + ".prefab");
            GameObject station = Object.Instantiate(prefab, position, Quaternion.identity);
            created.Add(station);
            foreach (var recovery in station.GetComponentsInChildren<KitchenRespawnTool>()) recovery.enabled = false;
            foreach (var body in station.GetComponentsInChildren<Rigidbody>()) Freeze(body);
            return station;
        }

        static KitchenFood Food(string name, Vector3 position, List<GameObject> created)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Kitchen/Prefabs/Food/" + name + ".prefab");
            GameObject item = Object.Instantiate(prefab, position, Quaternion.identity);
            created.Add(item);
            foreach (var recovery in item.GetComponentsInChildren<KitchenRespawnTool>()) recovery.enabled = false;
            Freeze(item.GetComponent<Rigidbody>());
            return item.GetComponent<KitchenFood>();
        }

        static void Freeze(Rigidbody body)
        {
            body.useGravity = false;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            body.isKinematic = true;
        }

        static void Doors(Action<bool, string> check, KitchenDoor door, string name)
        {
            var audio = KitchenAudioEmitter.Get(door);
            check(audio.CueCount(KitchenSound.DoorOpen) == 0 && audio.CueCount(KitchenSound.DoorClose) == 0,
                "audio/" + name + "/door-startup-silent");
            float span = door.openAngle - door.closedAngle;
            door.DragByDegrees(span * .02f); door.DragByDegrees(-span * .02f);
            check(audio.CueCount(KitchenSound.DoorOpen) == 0 && audio.CueCount(KitchenSound.DoorClose) == 0,
                "audio/" + name + "/closed-door-jitter-silent");
            door.DragByDegrees(span * .12f);
            door.DragByDegrees(span * .12f); door.DragByDegrees(-span * .1f);
            check(audio.CueCount(KitchenSound.DoorOpen) == 1 && audio.CueCount(KitchenSound.DoorClose) == 0
                && audio.OneShotSource.clip == Clip(KitchenSound.DoorOpen),
                "audio/" + name + "/opening-once-despite-moving-open-door");
            door.SetOpenImmediately(false); door.SetOpenImmediately(false);
            check(audio.CueCount(KitchenSound.DoorClose) == 1 && audio.OneShotSource.clip == Clip(KitchenSound.DoorClose),
                "audio/" + name + "/closing-once");
        }

        static IEnumerator Knob(Action<bool, string> check, KitchenKnob knob, KitchenSound sound, string name)
        {
            var audio = KitchenAudioEmitter.Get(knob);
            bool oven = sound == KitchenSound.OvenSpin;
            check(audio.CueCount(sound) == 0 && SilentLoop(audio), "audio/" + name + "/knob-startup-silent");
            knob.SetValue(Mathf.Lerp(knob.minValue, knob.maxValue, .3f));
            knob.SetValue(Mathf.Lerp(knob.minValue, knob.maxValue, .2f));
            check(audio.CueCount(sound) == 0 && SilentLoop(audio), "audio/" + name + "/programmatic-turn-silent");
            knob.BeginDrag(knob.Pivot.position + Vector3.up * .1f);
            knob.BeginDrag(knob.Pivot.position + Vector3.up * .1f);
            yield return new WaitForSeconds(.25f);
            check(oven ? Loop(audio, sound) && audio.LoopStartCount(sound) == 1 && audio.CueCount(sound) == 0
                : audio.CueCount(sound) == 1 && audio.OneShotSource.clip == Clip(sound) && SilentLoop(audio),
                "audio/" + name + "/press-starts-sound-once-even-without-motion");
            knob.DragByDegrees(10f);
            for (int i = 0; i < 10; ++i) knob.DragByDegrees(1f);
            yield return new WaitForSeconds(.25f);
            knob.DragByDegrees(-5f);
            knob.SetValue(knob.maxValue); knob.DragByDegrees(10f); knob.DragByDegrees(0f);
            check(oven ? Loop(audio, sound) && audio.LoopStartCount(sound) == 1 && audio.CueCount(sound) == 0
                : audio.CueCount(sound) == 1 && SilentLoop(audio), "audio/" + name + "/long-drag-never-retriggers-sound");
            knob.EndDrag();
            check(!knob.IsDragging && SilentLoop(audio), "audio/" + name + "/release-ends-gesture-and-loop");
            knob.BeginDrag(knob.Pivot.position + Vector3.up * .1f);
            check(oven ? audio.LoopStartCount(sound) == 2 && Loop(audio, sound)
                : audio.CueCount(sound) == 2 && SilentLoop(audio), "audio/" + name + "/new-press-starts-one-new-sound");
            knob.enabled = false;
            check(!knob.IsDragging && SilentLoop(audio), "audio/" + name + "/disable-cancels-gesture-and-loop");
            knob.enabled = true;
        }

        static void Faucet(Action<bool, string> check, KitchenToggle faucet)
        {
            var audio = KitchenAudioEmitter.Get(faucet);
            check(SilentLoop(audio), "audio/tap/startup-off-silent");
            faucet.SetValue(true);
            check(Loop(audio, KitchenSound.TapWater), "audio/tap/open-uses-water-clip");
            int starts = audio.LoopStartCount(KitchenSound.TapWater);
            faucet.SetValue(true); faucet.DragByDegrees(-1f); faucet.DragByDegrees(1f);
            check(audio.LoopStartCount(KitchenSound.TapWater) == starts, "audio/tap/stable-open-does-not-restart");
            faucet.SetValue(false);
            check(SilentLoop(audio), "audio/tap/closing-stops-loop");
            faucet.SetValue(true); faucet.enabled = false;
            check(SilentLoop(audio), "audio/tap/disable-stops-loop");
            faucet.SetValue(false); faucet.enabled = true;
        }

        static void Oven(Action<bool, string> check, KitchenOven oven)
        {
            var audio = KitchenAudioEmitter.Get(oven);
            var knobAudio = KitchenAudioEmitter.Get(oven.timerKnob);
            int turnCues = knobAudio.CueCount(KitchenSound.OvenSpin);
            int doneCues = audio.CueCount(KitchenSound.OvenDone);
            oven.door.SetOpenImmediately(false); oven.timerKnob.SetValue(10f); oven.Tick(1f);
            check(Loop(audio, KitchenSound.ClockTick), "audio/oven/countdown-uses-clock-clip");
            oven.timerKnob.BeginDrag(oven.timerKnob.Pivot.position + Vector3.up * .1f);
            check(SilentLoop(audio) && Loop(knobAudio, KitchenSound.OvenSpin) && !oven.IsHeating,
                "audio/oven/hold-immediately-stops-clock-before-winding-loop");
            float beforeSetting = oven.RemainingSeconds;
            oven.Tick(2f);
            check(Mathf.Approximately(beforeSetting, oven.RemainingSeconds) && SilentLoop(audio)
                && Loop(knobAudio, KitchenSound.OvenSpin), "audio/oven/stationary-hold-pauses-countdown");
            oven.timerKnob.DragByDegrees(9f);
            check(Mathf.Approximately(oven.RemainingSeconds, oven.timerKnob.Value) && SilentLoop(audio),
                "audio/oven/setting-value-does-not-start-clock-before-release");
            oven.timerKnob.EndDrag();
            check(SilentLoop(knobAudio) && Loop(audio, KitchenSound.ClockTick),
                "audio/oven/release-stops-winding-then-starts-clock");
            int windingStarts = knobAudio.LoopStartCount(KitchenSound.OvenSpin);
            int starts = audio.LoopStartCount(KitchenSound.ClockTick);
            oven.Tick(.1f); oven.Tick(.1f);
            check(audio.LoopStartCount(KitchenSound.ClockTick) == starts && knobAudio.CueCount(KitchenSound.OvenSpin) == turnCues
                && knobAudio.LoopStartCount(KitchenSound.OvenSpin) == windingStarts && SilentLoop(knobAudio),
                "audio/oven/countdown-no-loop-restart-or-knob-cue");
            oven.door.SetOpenImmediately(true); float remaining = oven.RemainingSeconds; oven.Tick(1f);
            check(SilentLoop(audio) && Mathf.Approximately(remaining, oven.RemainingSeconds), "audio/oven/open-door-pauses-clock");
            oven.door.SetOpenImmediately(false); oven.Tick(.1f);
            check(Loop(audio, KitchenSound.ClockTick), "audio/oven/close-resumes-clock");
            oven.timerKnob.SetValue(0f); oven.Tick(1f);
            check(SilentLoop(audio) && audio.CueCount(KitchenSound.OvenDone) == doneCues, "audio/oven/manual-cancel-no-bell");
            oven.timerKnob.SetValue(1f); oven.Tick(2f); oven.Tick(2f);
            check(SilentLoop(audio) && audio.CueCount(KitchenSound.OvenDone) == doneCues + 1
                && audio.OneShotSource.clip == Clip(KitchenSound.OvenDone), "audio/oven/natural-expiry-one-bell");
            oven.timerKnob.SetValue(5f); oven.enabled = false;
            check(SilentLoop(audio), "audio/oven/disable-stops-clock");
            oven.timerKnob.SetValue(0f); oven.enabled = true;
        }

        static void Hob(Action<bool, string> check, KitchenHob hob, List<GameObject> created)
        {
            KitchenPan pan = hob.pan;
            var audio = KitchenAudioEmitter.Get(pan);
            var heat = pan.GetComponent<KitchenPanHeat>();
            heat.Tick(0f); heat.Tick(30f);
            void Step(float seconds) { hob.Tick(seconds); heat.Tick(seconds); }
            Vector3 Seat() => pan.cookingSurface.TransformPoint(Vector3.up * pan.supportedLocalHeight);
            pan.transform.SetPositionAndRotation(hob.burner.position, hob.burner.rotation);
            hob.powerKnob.SetValue(Mathf.Lerp(hob.powerKnob.minValue, hob.powerKnob.maxValue, .5f));
            Physics.SyncTransforms(); Step(5f);
            check(SilentLoop(audio), "audio/hob/preheating-empty-pan-silent");
            KitchenFood steak = Food("Steak", Seat(), created);
            Physics.SyncTransforms(); Step(4.99f);
            check(SilentLoop(audio), "audio/hob/cold-pan-with-steak-silent");
            Step(.01f);
            check(Loop(audio, KitchenSound.Cooking), "audio/hob/actual-steak-starts-sizzle");
            int starts = audio.LoopStartCount(KitchenSound.Cooking);
            steak.gameObject.AddComponent<BoxCollider>().size = Vector3.one * .04f;
            KitchenFood second = Food("Steak", Seat(), created);
            Physics.SyncTransforms(); Step(.01f); Step(.01f);
            check(audio.LoopStartCount(KitchenSound.Cooking) == starts, "audio/hob/multiple-steaks-and-colliders-one-loop");
            second.gameObject.SetActive(false);
            steak.SetDesktopHeld(true); Step(.01f);
            check(SilentLoop(audio), "audio/hob/held-steak-stops-sizzle");
            steak.SetDesktopHeld(false); Step(.01f);
            var marker = pan.GetComponent<KitchenDesktopHeld>();
            if (!marker) marker = pan.gameObject.AddComponent<KitchenDesktopHeld>();
            marker.SetHeld(true); Step(.01f);
            check(Loop(audio, KitchenSound.Cooking), "audio/hob/lifted-hot-pan-retains-sizzle");
            marker.SetHeld(false); pan.transform.position += Vector3.right;
            steak.transform.position = Seat();
            Physics.SyncTransforms(); Step(.01f);
            check(Loop(audio, KitchenSound.Cooking), "audio/hob/residual-sizzle-follows-pan-away-from-ring");
            pan.transform.position = hob.burner.position; hob.powerKnob.SetValue(0f);
            steak.transform.position = Seat();
            Physics.SyncTransforms(); Step(.01f);
            check(Loop(audio, KitchenSound.Cooking), "audio/hob/switched-off-hot-pan-retains-sizzle");
            heat.Tick(15f);
            check(SilentLoop(audio), "audio/hob/cooled-pan-stops-sizzle");
            hob.powerKnob.SetValue(hob.powerKnob.maxValue); Step(5f); hob.enabled = false;
            check(Loop(audio, KitchenSound.Cooking), "audio/hob/hob-disable-preserves-pan-residual-sizzle");
            steak.gameObject.SetActive(false); heat.Tick(0f);
            check(SilentLoop(audio), "audio/hob/hot-empty-pan-silent");
            steak.gameObject.SetActive(true); Physics.SyncTransforms(); heat.Tick(0f);
            heat.enabled = false;
            check(SilentLoop(audio), "audio/hob/pan-heat-disable-stops-owned-sizzle");
            heat.Tick(15f); heat.enabled = true;
            hob.powerKnob.SetValue(0f); hob.enabled = true;
            steak.Discard();
        }

        static void Salad(Action<bool, string> check, KitchenSaladBowl bowl, List<GameObject> created)
        {
            var audio = KitchenAudioEmitter.Get(bowl);
            int cues = audio.CueCount(KitchenSound.Complete);
            check(!bowl.Mix() && audio.CueCount(KitchenSound.Complete) == cues, "audio/salad/empty-mix-silent");
            foreach (string kind in new[] { "Tomato", "Lettuce" })
            {
                Vector3 position = Origin + new Vector3(12f, 1f, 2f);
                var whole = Food(kind, position, created);
                whole.Chop();
                var chopped = CurrentProduce(position);
                check(chopped && bowl.TryAddIngredient(chopped), "audio/salad/ingredient-" + kind);
            }
            check(bowl.Mix() && audio.CueCount(KitchenSound.Complete) == cues + 1 && audio.OneShotSource.clip == Clip(KitchenSound.Complete),
                "audio/salad/successful-output-one-complete-cue");
            check(!bowl.Mix() && audio.CueCount(KitchenSound.Complete) == cues + 1, "audio/salad/repeated-empty-click-silent");
            foreach (var output in Object.FindObjectsByType<KitchenFood>(FindObjectsSortMode.None))
            {
                if (output.IsConsumed || output.definition.kind != FoodKind.Salad || Vector3.Distance(output.transform.position, Origin) >= 20f) continue;
                foreach (var recovery in output.GetComponentsInChildren<KitchenRespawnTool>()) recovery.enabled = false;
                output.Discard();
            }
        }

        static KitchenFood CurrentProduce(Vector3 at) => Object.FindObjectsByType<KitchenFood>(FindObjectsSortMode.None)
            .FirstOrDefault(food => food.IsProduce && !food.IsConsumed && food.IsChopped && Vector3.Distance(food.transform.position, at) < .3f);

        static IEnumerator Knife(Action<bool, string> check, KitchenKnife knife, List<GameObject> created)
        {
            var audio = KitchenAudioEmitter.Get(knife);
            int cues = audio.CueCount(KitchenSound.Cut);
            Vector3 at = Origin + new Vector3(12f, 1f, -2f);
            var food = Food("Apple", at, created);
            knife.transform.rotation = Quaternion.identity;
            knife.transform.position += at + Vector3.up * .3f - knife.BladeEdge;
            var held = knife.GetComponent<KitchenDesktopHeld>();
            if (!held) held = knife.gameObject.AddComponent<KitchenDesktopHeld>();
            held.SetHeld(true);
            yield return null;
            check(!knife.TryDesktopChop(null) && audio.CueCount(KitchenSound.Cut) == cues, "audio/knife/missed-action-silent");
            check(knife.TryDesktopChop(food), "audio/knife/first-downstroke-starts");
            float until = Time.time + .8f;
            while (Time.time < until)
            {
                yield return null;
                var replacement = CurrentProduce(at);
                if (replacement)
                {
                    food = replacement;
                    foreach (var recovery in food.GetComponentsInChildren<KitchenRespawnTool>()) recovery.enabled = false;
                    Freeze(food.GetComponent<Rigidbody>());
                }
            }
            check(food && food.CutStage == 1 && audio.CueCount(KitchenSound.Cut) == cues + 1
                && audio.OneShotSource.clip == Clip(KitchenSound.Cut), "audio/knife/first-successful-cut-one-cue");
            check(knife.TryDesktopChop(food), "audio/knife/second-downstroke-starts");
            yield return new WaitForSeconds(.8f);
            check(food && food.CutStage == 2 && audio.CueCount(KitchenSound.Cut) == cues + 2, "audio/knife/second-successful-cut-one-more-cue");
            check(!knife.TryDesktopChop(food) && audio.CueCount(KitchenSound.Cut) == cues + 2, "audio/knife/third-cut-silent");
            held.SetHeld(false);
            if (food) food.Discard();
        }
    }
}
