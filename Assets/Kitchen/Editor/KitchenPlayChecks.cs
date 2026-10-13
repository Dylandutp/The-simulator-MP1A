using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Object = UnityEngine.Object;

namespace HouseSim.Kitchen.Editor
{
    /// <summary>Runs the generated scenes and prefabs through their public gameplay APIs.</summary>
    [InitializeOnLoad]
    public static class KitchenPlayChecks
    {
        private const string ActiveKey = "Kitchen.PlayChecks.Active";
        private const string CompleteKey = "Kitchen.PlayChecks.Complete";
        private const string ExitKey = "Kitchen.PlayChecks.ExitCode";
        private const string DiagnosticsKey = "Kitchen.PlayChecks.EditorDiagnostics";
        private const string TeardownErrorKey = "Kitchen.PlayChecks.TeardownError";
        private const string Entry = "Assets/Kitchen/Scenes/KitchenEntry.unity";
        private static readonly List<string> Passed = new List<string>();
        private static readonly List<string> Observations = new List<string>();
        private static int phase;
        private static int spawnNumber;
        private static double started;
        private static double trashStarted;
        private static string runtimeError;
        private static KitchenSession session;
        private static KitchenFood trashFood;
        private static ResourceData trashResource;
        private static int trashInventoryBefore;
        private static int originalEnergy;
        private static KitchenHob gravityHob;
        private static KitchenFood gravitySteak;
        private static float gravityStarted;
        private static KitchenDesktopController desktop;
        private static Keyboard testKeyboard, previousKeyboard;
        private static Mouse testMouse, previousMouse;
        private static InputSettings originalInputSettings;
        private static InputSettings.EditorInputBehaviorInPlayMode originalEditorInputBehavior;
        private static InputSettings.BackgroundBehavior originalBackgroundBehavior;
        private static KitchenDoor inputDoor;
        private static KitchenKnob inputKnob;
        private static KitchenFood inputFood;
        private static CharacterController inputMotor;
        private static bool motorWasEnabled;
        private static Vector3 oldPlayerPosition, oldCameraPosition;
        private static Quaternion oldPlayerRotation, oldCameraRotation;
        private static bool oldAutoDetectXR;
        private static int inputPhase, inputQueuedFrame;
        private static float inputQueuedTime;
        private static PlayerInventory retainedInventory;
        private static KitchenFood retainedFood;
        private static ResourceData retainedResource, retainedEnergyResource;
        private static int retainedBefore;

        [Serializable]
        private sealed class Report
        {
            public bool passed;
            public string timestampUtc;
            public int assertionCount;
            public string[] assertions;
            public string failure;
            public string[] editorDiagnostics;
            public string[] observations;
            public bool editorTeardownVerified;
        }

        [Serializable] private sealed class Diagnostics { public List<string> entries = new List<string>(); }

        static KitchenPlayChecks()
        {
            EditorApplication.playModeStateChanged += OnPlayMode;
            EditorApplication.update += Update;
            Application.logMessageReceived += OnLog;
        }

        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Start KitchenPlayChecks from Edit Mode.");
            if (!File.Exists(Entry)) throw new FileNotFoundException("Generate the kitchen before running Play Mode checks.", Entry);
            SessionState.SetBool(ActiveKey, true);
            SessionState.SetBool(CompleteKey, false);
            SessionState.SetInt(ExitKey, 1);
            SessionState.SetString(DiagnosticsKey, JsonUtility.ToJson(new Diagnostics()));
            SessionState.EraseString(TeardownErrorKey);
            runtimeError = null;
            EditorSceneManager.OpenScene(Entry, OpenSceneMode.Single);
            EditorApplication.EnterPlaymode();
        }

        private static void OnPlayMode(PlayModeStateChange change)
        {
            if (!SessionState.GetBool(ActiveKey, false)) return;
            if (change == PlayModeStateChange.EnteredPlayMode)
            {
                phase = 0;
                spawnNumber = 0;
                Passed.Clear();
                Observations.Clear();
                started = EditorApplication.timeSinceStartup;
            }
            if (change == PlayModeStateChange.EnteredEditMode && SessionState.GetBool(CompleteKey, false))
            {
                // Input System restores its own settings later in this same event dispatch.
                // Verify teardown on the following editor turn before publishing PASS/exit 0.
                EditorApplication.delayCall += FinalizeAfterEditMode;
            }
        }

        private static void OnLog(string message, string trace, LogType type)
        {
            if (!SessionState.GetBool(ActiveKey, false)) return;
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            if (IsEditorSearchStartupDiagnostic(message, trace))
            {
                var diagnostics = JsonUtility.FromJson<Diagnostics>(SessionState.GetString(DiagnosticsKey, "{}")) ?? new Diagnostics();
                diagnostics.entries.Add(message + "\n" + trace);
                SessionState.SetString(DiagnosticsKey, JsonUtility.ToJson(diagnostics));
                return;
            }
            if (SessionState.GetBool(CompleteKey, false))
            {
                string previous = SessionState.GetString(TeardownErrorKey, "");
                SessionState.SetString(TeardownErrorKey, previous + message + "\n" + trace + "\n");
                return;
            }
            if (runtimeError == null) runtimeError = message + "\n" + trace;
        }

        private static bool IsEditorSearchStartupDiagnostic(string message, string trace)
        {
            // Only this observed Unity 6000.5 Search startup failure is classified separately.
            // Any gameplay/project frame or other Editor error continues to fail the run.
            if (!message.StartsWith("ArgumentOutOfRangeException:", StringComparison.Ordinal)
                || !trace.Contains("UnityEditor.Search.SearchDatabase")
                || !trace.Contains("GetDefaultSearchDatabase")
                || !trace.Contains("UnityEditor.Search.SearchInit.IndexationOnStartup")) return false;
            foreach (string raw in trace.Split('\n'))
            {
                string frame = raw.Trim();
                if (frame.Length == 0) continue;
                if (frame.StartsWith("at ", StringComparison.Ordinal)) frame = frame.Substring(3);
                if (!frame.StartsWith("System.", StringComparison.Ordinal)
                    && !frame.StartsWith("UnityEditor.Search.", StringComparison.Ordinal)
                    && !frame.StartsWith("UnityEditor.EditorApplication.", StringComparison.Ordinal)) return false;
            }
            return true;
        }

        private static void Update()
        {
            if (!SessionState.GetBool(ActiveKey, false) || SessionState.GetBool(CompleteKey, false) || !EditorApplication.isPlaying) return;
            try
            {
                if (runtimeError != null) throw new InvalidOperationException("Runtime error during checks: " + runtimeError);
                if (EditorApplication.timeSinceStartup - started > 120) throw new TimeoutException("Kitchen scenes did not finish the checks in 120 seconds.");
                switch (phase)
                {
                    case 0:
                        session = KitchenSession.Instance;
                        var fridge = Object.FindFirstObjectByType<KitchenFridge>();
                        if (!session || !session.mouthAnchor || !session.inventory || !fridge || fridge.CountStock() == 0) return;
                        originalEnergy = session.Energy;
                        Assert(session.energyResource != null, "scene/shared-energy-resource");
                        foreach (var oven in Object.FindObjectsByType<KitchenOven>(FindObjectsSortMode.None)) oven.enabled = false;
                        foreach (var hob in Object.FindObjectsByType<KitchenHob>(FindObjectsSortMode.None)) hob.enabled = false;
                        Assert(Object.FindObjectsByType<PlayerInventory>(FindObjectsSortMode.None).Length == 1, "scene/one-shared-inventory");
                        Assert(session.inventory.gameObject.scene.name == "MasterScene", "scene/shared-inventory-owned-by-master");
                        phase++;
                        break;
                    case 1: CheckAtomicInventory(); phase++; break;
                    case 2: CheckFoodStates(); phase++; break;
                    case 3: CheckSalad(true); phase++; break;
                    case 4: CheckSalad(false); phase++; break;
                    case 5: CheckFridge(); phase++; break;
                    case 6: CheckOven(); phase++; break;
                    case 7: CheckHob(); phase++; break;
                    case 8: BeginGravityPanCheck(); phase++; break;
                    case 9:
                        if (Time.time - gravityStarted < 2f) return;
                        FinishGravityPanCheck(); phase++; break;
                    case 10: CheckGrid(); phase++; break;
                    case 11: BeginTrashCheck(); phase++; break;
                    case 12:
                        if (EditorApplication.timeSinceStartup - trashStarted < 0.5) return;
                        Assert(!trashFood, "trash/physical-trigger-destroys-food");
                        Assert(session.inventory.GetAmount(trashResource) == trashInventoryBefore, "trash/single-inventory-debit");
                        phase++;
                        break;
                    case 13: BeginDesktopInputCheck(); phase++; break;
                    case 14: if (AdvanceDesktopInputCheck()) phase++; break;
                    case 15:
                        retainedInventory = session.inventory;
                        retainedEnergyResource = session.energyResource;
                        retainedFood = Spawn("Steak");
                        retainedResource = retainedFood.CurrentResource;
                        retainedBefore = retainedInventory.GetAmount(retainedResource);
                        Assert(retainedFood.Claim(), "inventory/claim-before-session-destroy");
                        Object.Destroy(session);
                        phase++;
                        break;
                    case 16:
                        if (session) return;
                        Assert(retainedInventory && retainedFood && retainedInventory.GetAmount(retainedResource) == retainedBefore + 1, "inventory/ledger-survives-kitchen-session-destroy");
                        Object.Destroy(retainedFood.gameObject);
                        phase++;
                        break;
                    case 17:
                        if (retainedFood) return;
                        Assert(retainedInventory.GetAmount(retainedResource) == retainedBefore, "inventory/food-destruction-debits-retained-ledger-without-session");
                        retainedInventory.SetAmount(retainedEnergyResource, originalEnergy);
                        Finish(null);
                        break;
                }
            }
            catch (Exception exception) { Finish(exception.ToString()); }
        }

        private static void CheckAtomicInventory()
        {
            var a = Prefab("Chicken").GetComponent<KitchenFood>().definition.rawResource;
            var b = Prefab("Steak").GetComponent<KitchenFood>().definition.rawResource;
            var reward = Prefab("Tomato").GetComponent<KitchenFood>().definition.rawResource;
            int beforeA = session.inventory.GetAmount(a), beforeB = session.inventory.GetAmount(b), beforeReward = session.inventory.GetAmount(reward);
            session.inventory.SetAmount(a, 2);
            session.inventory.SetAmount(b, 0);
            bool exchanged = session.inventory.TryExchange(new Dictionary<ResourceData, int> { { a, 1 }, { b, 1 } }, new Dictionary<ResourceData, int> { { reward, 1 } });
            Assert(!exchanged && session.inventory.GetAmount(a) == 2 && session.inventory.GetAmount(b) == 0 && session.inventory.GetAmount(reward) == beforeReward, "inventory/insufficient-batch-is-atomic");
            Assert(session.inventory.TryExchange(new Dictionary<ResourceData, int> { { a, 1 } }, new Dictionary<ResourceData, int> { { reward, 1 } }), "inventory/valid-batch-succeeds");
            Assert(session.inventory.GetAmount(a) == 1 && session.inventory.GetAmount(reward) == beforeReward + 1, "inventory/valid-batch-exact-amounts");
            session.inventory.SetAmount(a, beforeA);
            session.inventory.SetAmount(b, beforeB);
            session.inventory.SetAmount(reward, beforeReward);
        }

        private static void CheckFoodStates()
        {
            string[] kinds = { "Chicken", "Steak" };
            int[,] energyChanges = { { -15, 30, -20 }, { 5, 25, -15 } };
            for (int kind = 0; kind < kinds.Length; kind++)
            for (int state = 0; state < 3; state++)
            {
                KitchenFood food = Spawn(kinds[kind]);
                if (state == 1) food.ApplyHeat(food.definition.goodAtSeconds);
                if (state == 2) food.ApplyHeat(food.definition.burntAtSeconds);
                Assert(food.State == (FoodState)state, "food/" + kinds[kind] + "/state-" + state);
                ResourceData resource = food.CurrentResource;
                int before = session.inventory.GetAmount(resource);
                Assert(food.Claim() && session.inventory.GetAmount(resource) == before + 1, "food/" + kinds[kind] + "/claim-" + state);
                SetEnergy(50);
                Assert(food.TryConsume() && session.Energy == 50 + energyChanges[kind, state], "food/" + kinds[kind] + "/energy-" + state);
                Assert(!food.TryConsume() && session.inventory.GetAmount(resource) == before && session.Energy == 50 + energyChanges[kind, state], "food/" + kinds[kind] + "/consume-once-" + state);
            }
        }

        private static void CheckSalad(bool clean)
        {
            KitchenSaladBowl bowl = Object.FindFirstObjectByType<KitchenSaladBowl>();
            Assert(bowl && bowl.ingredientCount == 0, "salad/" + clean + "/empty-bowl");
            KitchenFood tomato = Spawn("Tomato");
            int tomatoRawBefore = session.inventory.GetAmount(tomato.definition.rawResource);
            int tomatoPreparedBefore = session.inventory.GetAmount(tomato.definition.preparedResource);
            Assert(!bowl.TryAddIngredient(tomato), "salad/" + clean + "/reject-unchopped");
            Assert(tomato.Claim(), "salad/" + clean + "/claim-tomato");
            if (clean) tomato.Wash(3f);
            tomato = ChopAndFind(tomato);
            Assert(tomato.IsDirty != clean, "salad/" + clean + "/washing-survives-chop");
            Assert(bowl.TryAddIngredient(tomato) && !bowl.TryAddIngredient(tomato), "salad/" + clean + "/ingredient-once");
            Assert(!bowl.Mix(), "salad/" + clean + "/requires-two-kinds");
            KitchenFood lettuce = Spawn("Lettuce");
            Assert(lettuce.Claim(), "salad/" + clean + "/claim-lettuce");
            lettuce.Wash(3f);
            lettuce = ChopAndFind(lettuce);
            Assert(bowl.TryAddIngredient(lettuce), "salad/" + clean + "/add-second-kind");
            ResourceData saladResource = bowl.saladPrefab.GetComponent<KitchenFood>().CurrentResource;
            int beforeSalad = session.inventory.GetAmount(saladResource);
            Assert(bowl.Mix(), "salad/" + clean + "/mix-succeeds");
            KitchenFood salad = Object.FindObjectsByType<KitchenFood>(FindObjectsSortMode.None).FirstOrDefault(food => !food.IsConsumed && food.definition.kind == FoodKind.Salad);
            Assert(salad && salad.IsDirty != clean && bowl.ingredientCount == 0 && bowl.DifferentKinds == 0, "salad/" + clean + "/output-and-reset");
            Assert(session.inventory.GetAmount(tomato.definition.rawResource) == tomatoRawBefore && session.inventory.GetAmount(tomato.definition.preparedResource) == tomatoPreparedBefore && session.inventory.GetAmount(saladResource) == beforeSalad + 1, "salad/" + clean + "/resource-transfer");
            SetEnergy(50);
            Assert(salad.TryConsume() && session.Energy == (clean ? 70 : 35), "salad/" + clean + "/energy");
        }

        private static KitchenFood ChopAndFind(KitchenFood food)
        {
            Vector3 at = food.transform.position;
            FoodKind kind = food.definition.kind;
            Assert(food.Chop(), "produce/" + kind + "/chop-spawns-pieces");
            KitchenFood replacement = Object.FindObjectsByType<KitchenFood>(FindObjectsSortMode.None).FirstOrDefault(candidate => !candidate.IsConsumed && candidate.IsChopped && candidate.definition.kind == kind && Vector3.Distance(candidate.transform.position, at) < 0.01f);
            Assert(replacement != null, "produce/" + kind + "/replacement-preserves-location");
            Freeze(replacement);
            return replacement;
        }

        private static void CheckFridge()
        {
            KitchenFridge fridge = Object.FindFirstObjectByType<KitchenFridge>();
            int stock = fridge.CountStock();
            Assert(stock == fridge.slots.Length, "fridge/all-configured-slots-stocked");
            int alive = Object.FindObjectsByType<KitchenFood>(FindObjectsSortMode.None).Count(food => !food.IsConsumed);
            fridge.Refill();
            fridge.Refill();
            Assert(fridge.CountStock() == stock && Object.FindObjectsByType<KitchenFood>(FindObjectsSortMode.None).Count(food => !food.IsConsumed) == alive, "fridge/refill-occupied-slots-is-idempotent");
            KitchenFood taken = fridge.slots.Select(slot => slot.GetComponentInChildren<KitchenFood>()).First(food => food != null);
            taken.SetDesktopHeld(true);
            taken.transform.position = new Vector3(25, 4, 25);
            Physics.SyncTransforms();
            fridge.door.SetOpenImmediately(!fridge.door.IsOpen);
            Assert(taken && !taken.IsConsumed && taken.IsHeld && fridge.CountStock() == stock, "fridge/transition-refills-without-deleting-held-food");
            fridge.door.SetOpenImmediately(!fridge.door.IsOpen);
            fridge.Refill();
            Assert(fridge.CountStock() == stock && Object.FindObjectsByType<KitchenFood>(FindObjectsSortMode.None).Count(food => !food.IsConsumed) == alive + 1, "fridge/repeated-transition-does-not-duplicate-stock");
            taken.SetDesktopHeld(false);
            taken.Discard();

            KitchenFood chicken = fridge.slots.Select(slot => slot.GetComponentInChildren<KitchenFood>()).First(food => food && food.definition.kind == FoodKind.Chicken);
            Transform chickenSlot = chicken.transform.parent;
            chicken.SetDesktopHeld(true);
            chicken.transform.position = new Vector3(25, 4, 26);
            KitchenFood blocker = Spawn("Chicken");
            blocker.transform.SetPositionAndRotation(chickenSlot.position + chickenSlot.right * 0.31f, chickenSlot.rotation);
            blocker.SetDesktopHeld(true);
            Physics.SyncTransforms();
            fridge.Refill();
            Assert(fridge.CountStock() == stock - 1 && blocker && blocker.IsHeld, "fridge/full-prefab-volume-blocks-offset-held-food");
            blocker.SetDesktopHeld(false);
            blocker.Discard();
            fridge.Refill();
            Assert(fridge.CountStock() == stock, "fridge/cleared-volume-refills-safely");
            chicken.SetDesktopHeld(false);
            chicken.Discard();
        }

        private static void CheckOven()
        {
            KitchenOven oven = Object.FindFirstObjectByType<KitchenOven>();
            Assert(oven && oven.chamber && oven.door && oven.timerKnob, "oven/configured");
            KitchenFood chicken = Spawn("Chicken");
            KitchenFood steak = Spawn("Steak");
            chicken.transform.position = oven.chamber.position;
            steak.transform.position = oven.chamber.position + oven.chamber.right * 0.1f;
            Physics.SyncTransforms();
            oven.door.SetOpenImmediately(false);
            oven.timerKnob.SetValue(20);
            chicken.SetDesktopHeld(true);
            oven.Tick(2);
            Assert(Near(chicken.CookSeconds, 0) && Near(oven.RemainingSeconds, 18), "oven/held-food-not-heated");
            chicken.SetDesktopHeld(false);
            oven.Tick(3);
            Assert(Near(chicken.CookSeconds, 3) && Near(steak.CookSeconds, 0) && Near(oven.RemainingSeconds, 15), "oven/only-chicken-in-chamber-heats");
            oven.door.SetOpenImmediately(true);
            oven.Tick(5);
            Assert(Near(chicken.CookSeconds, 3) && Near(oven.RemainingSeconds, 15) && !oven.IsHeating, "oven/open-door-pauses-heat-and-timer");
            oven.timerKnob.SetValue(30);
            Assert(Near(chicken.CookSeconds, 3) && Near(oven.RemainingSeconds, 30), "oven/retiming-preserves-cooking-progress");
            oven.door.SetOpenImmediately(false);
            oven.Tick(50);
            Assert(Near(chicken.CookSeconds, 33) && Near(oven.RemainingSeconds, 0) && Near(oven.timerKnob.Value, 0) && !oven.IsHeating, "oven/expiry-clamps-time-and-stops");
            oven.timerKnob.SetValue(10);
            oven.Tick(10);
            Assert(Near(chicken.CookSeconds, 43) && chicken.State == FoodState.Burnt, "oven/repeated-cycle-accumulates-to-burnt");
            chicken.Discard();
            steak.Discard();
        }

        private static void CheckHob()
        {
            KitchenHob hob = Object.FindFirstObjectByType<KitchenHob>();
            Assert(hob && hob.pan && hob.burner && hob.powerKnob, "hob/configured");
            KitchenPan pan = hob.pan;
            Vector3 oldPosition = pan.transform.position;
            Quaternion oldRotation = pan.transform.rotation;
            Rigidbody body = pan.GetComponent<Rigidbody>();
            bool wasKinematic = body.isKinematic;
            body.isKinematic = true;
            pan.transform.SetPositionAndRotation(hob.burner.position, hob.burner.rotation);
            KitchenFood steak = Spawn("Steak");
            steak.transform.position = pan.cookingSurface.position;
            Physics.SyncTransforms();
            hob.powerKnob.SetValue(1); hob.Tick(4);
            Assert(Near(steak.CookSeconds, 2), "hob/power-one-half-speed");
            hob.powerKnob.SetValue(2); hob.Tick(4);
            Assert(Near(steak.CookSeconds, 6), "hob/power-two-normal-speed");
            hob.powerKnob.SetValue(3); hob.Tick(1);
            Assert(Near(steak.CookSeconds, 8) && steak.State == FoodState.Good, "hob/power-three-double-speed-and-good-steak");
            steak.SetDesktopHeld(true); hob.Tick(10);
            Assert(Near(steak.CookSeconds, 8), "hob/held-steak-not-heated");
            steak.SetDesktopHeld(false);
            KitchenDesktopHeld held = pan.GetComponent<KitchenDesktopHeld>();
            if (!held) held = pan.gameObject.AddComponent<KitchenDesktopHeld>();
            held.SetHeld(true); hob.Tick(10);
            Assert(Near(steak.CookSeconds, 8), "hob/held-pan-not-heated");
            held.SetHeld(false);
            pan.transform.position += Vector3.right * 2f;
            Physics.SyncTransforms(); hob.Tick(10);
            Assert(Near(steak.CookSeconds, 8), "hob/pan-away-from-burner-not-heated");
            pan.transform.position = hob.burner.position;
            Physics.SyncTransforms();
            hob.powerKnob.SetValue(0); hob.Tick(10);
            Assert(Near(steak.CookSeconds, 8), "hob/off-switch-stops-heating");
            hob.powerKnob.SetValue(3); hob.Tick(5);
            Assert(Near(steak.CookSeconds, 18) && steak.State == FoodState.Burnt, "hob/continued-cooking-burns-steak");
            hob.powerKnob.SetValue(0);
            pan.transform.SetPositionAndRotation(oldPosition, oldRotation);
            body.isKinematic = wasKinematic;
            steak.Discard();
        }

        private static void BeginGravityPanCheck()
        {
            gravityHob = Object.FindFirstObjectByType<KitchenHob>();
            KitchenPan pan = gravityHob.pan;
            Rigidbody panBody = pan.GetComponent<Rigidbody>();
            pan.transform.SetPositionAndRotation(gravityHob.burner.position + gravityHob.burner.up * 0.10f, gravityHob.burner.rotation);
            panBody.isKinematic = false;
            panBody.useGravity = true;
            panBody.constraints = RigidbodyConstraints.None;
            panBody.linearVelocity = Vector3.zero;
            panBody.angularVelocity = Vector3.zero;
            gravitySteak = Spawn("Steak");
            gravitySteak.transform.SetPositionAndRotation(pan.cookingSurface.position + pan.transform.up * 0.30f, pan.transform.rotation);
            Rigidbody steakBody = gravitySteak.GetComponent<Rigidbody>();
            steakBody.isKinematic = false;
            steakBody.useGravity = true;
            steakBody.constraints = RigidbodyConstraints.None;
            steakBody.linearVelocity = Vector3.zero;
            steakBody.angularVelocity = Vector3.zero;
            Physics.SyncTransforms();
            gravityHob.powerKnob.SetValue(2);
            gravityHob.enabled = true;
            gravityStarted = Time.time;
        }

        private static void FinishGravityPanCheck()
        {
            KitchenPan pan = gravityHob.pan;
            Assert(gravitySteak != null, "hob/gravity-drop-steak-survives");
            Rigidbody steakBody = gravitySteak.GetComponent<Rigidbody>();
            Rigidbody panBody = pan.GetComponent<Rigidbody>();
            Assert(!steakBody.isKinematic && steakBody.useGravity && !panBody.isKinematic && panBody.useGravity, "hob/gravity-test-uses-dynamic-bodies");
            Assert(pan.GetSteaks().Contains(gravitySteak) && steakBody.linearVelocity.magnitude < 0.5f && panBody.linearVelocity.magnitude < 0.5f, "hob/gravity-drop-settles-inside-physical-pan");
            Assert(gravityHob.IsHeating && gravitySteak.CookSeconds > 0.5f, "hob/gravity-dropped-steak-heats-through-update");
            Observations.Add("Gravity pan check: simulationSeconds=" + (Time.time - gravityStarted).ToString("0.000")
                + ", steakCookSeconds=" + gravitySteak.CookSeconds.ToString("0.000")
                + ", steakVelocity=" + steakBody.linearVelocity.magnitude.ToString("0.000")
                + ", panVelocity=" + panBody.linearVelocity.magnitude.ToString("0.000"));
            gravityHob.powerKnob.SetValue(0);
            gravityHob.enabled = false;
            gravitySteak.Discard();
        }

        private static void BeginDesktopInputCheck()
        {
            desktop = Object.FindFirstObjectByType<KitchenDesktopController>();
            Assert(desktop && desktop.viewCamera && desktop.playerRoot && desktop.IsDesktopActive, "desktop/scene-controller-active");
            oldPlayerPosition = desktop.playerRoot.position;
            oldPlayerRotation = desktop.playerRoot.rotation;
            oldCameraPosition = desktop.viewCamera.transform.position;
            oldCameraRotation = desktop.viewCamera.transform.rotation;
            oldAutoDetectXR = desktop.autoDetectXR;
            desktop.autoDetectXR = false;
            inputMotor = desktop.playerRoot.GetComponent<CharacterController>();
            motorWasEnabled = inputMotor && inputMotor.enabled;
            if (inputMotor) inputMotor.enabled = false;
            previousKeyboard = Keyboard.current;
            previousMouse = Mouse.current;
            // Use only in-process Input System devices. No OS keyboard/mouse input is sent.
            // Keep the existing settings object's identity. InputManager destroys temporary
            // HideAndDontSave settings when replacing InputSystem.settings, so cloning and
            // swapping would invalidate the original reference needed on editor teardown.
            // These runtime setters only apply settings; no asset is saved or marked dirty.
            originalInputSettings = InputSystem.settings;
            originalEditorInputBehavior = originalInputSettings.editorInputBehaviorInPlayMode;
            originalBackgroundBehavior = originalInputSettings.backgroundBehavior;
            originalInputSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            originalInputSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            testKeyboard = InputSystem.AddDevice<Keyboard>("KitchenSmokeKeyboard");
            testMouse = InputSystem.AddDevice<Mouse>("KitchenSmokeMouse");
            testKeyboard.MakeCurrent();
            testMouse.MakeCurrent();
            inputDoor = Object.FindFirstObjectByType<KitchenFridge>().door;
            inputDoor.SetOpenImmediately(false);
            Collider doorPanel = inputDoor.GetComponentsInChildren<Collider>().Where(collider => !collider.isTrigger).OrderByDescending(collider => collider.bounds.size.sqrMagnitude).First();
            AimAt(doorPanel.bounds.center, -inputDoor.transform.forward);
            InputSystem.QueueStateEvent(testMouse, new MouseState());
            InputSystem.QueueStateEvent(testKeyboard, new KeyboardState(Key.E));
            inputPhase = 0;
            MarkInputQueued();
        }

        private static bool AdvanceDesktopInputCheck()
        {
            if (Time.frameCount < inputQueuedFrame + 2 || Time.time - inputQueuedTime < 0.25f) return false;
            if (inputPhase == 0)
            {
                Assert(inputDoor.IsOpen, "desktop/keyboard-E-opens-door-via-update");
                InputSystem.QueueStateEvent(testKeyboard, new KeyboardState());
                inputKnob = Object.FindFirstObjectByType<KitchenOven>().timerKnob;
                inputKnob.SetValue(0);
                Collider dial = inputKnob.GetComponentsInChildren<Collider>().First(collider => !collider.isTrigger);
                AimAt(dial.bounds.center, -inputKnob.transform.forward);
                InputSystem.QueueStateEvent(testMouse, new MouseState { scroll = new Vector2(0, 120f) });
                inputPhase++;
            }
            else if (inputPhase == 1)
            {
                Assert(Near(inputKnob.Value, inputKnob.step), "desktop/mouse-wheel-adjusts-knob-via-update");
                InputSystem.QueueStateEvent(testMouse, new MouseState());
                inputFood = Spawn("Steak");
                inputFood.transform.position = new Vector3(50f, 3f, 50f);
                Physics.SyncTransforms();
                Collider foodCollider = inputFood.GetComponent<Collider>();
                AimAt(foodCollider.bounds.center, Vector3.back);
                InputSystem.QueueStateEvent(testMouse, new MouseState().WithButton(MouseButton.Left));
                inputPhase++;
            }
            else if (inputPhase == 2)
            {
                Assert(inputFood && desktop.HeldObject == inputFood.gameObject && inputFood.IsHeld && inputFood.IsClaimed, "desktop/left-click-grabs-and-claims-via-update");
                SetEnergy(50);
                InputSystem.QueueStateEvent(testMouse, new MouseState());
                InputSystem.QueueStateEvent(testKeyboard, new KeyboardState(Key.F));
                inputPhase++;
            }
            else
            {
                Assert(!inputFood && session.Energy == 55, "desktop/keyboard-F-consumes-held-food-via-update");
                CleanupDesktopInput();
                return true;
            }
            MarkInputQueued();
            return false;
        }

        private static void AimAt(Vector3 target, Vector3 approach)
        {
            Transform eye = desktop.viewCamera.transform;
            Vector3 position = target + approach.normalized * 0.75f;
            desktop.playerRoot.position += position - eye.position;
            eye.rotation = Quaternion.LookRotation(target - eye.position, Vector3.up);
            Physics.SyncTransforms();
        }

        private static void MarkInputQueued()
        {
            inputQueuedFrame = Time.frameCount;
            inputQueuedTime = Time.time;
        }

        private static void CleanupDesktopInput()
        {
            if (desktop)
            {
                desktop.Release();
                desktop.autoDetectXR = oldAutoDetectXR;
                desktop.playerRoot.SetPositionAndRotation(oldPlayerPosition, oldPlayerRotation);
                desktop.viewCamera.transform.SetPositionAndRotation(oldCameraPosition, oldCameraRotation);
                if (inputMotor) inputMotor.enabled = motorWasEnabled;
            }
            if (testKeyboard != null) { InputSystem.RemoveDevice(testKeyboard); testKeyboard = null; }
            if (testMouse != null) { InputSystem.RemoveDevice(testMouse); testMouse = null; }
            if (originalInputSettings)
            {
                originalInputSettings.editorInputBehaviorInPlayMode = originalEditorInputBehavior;
                originalInputSettings.backgroundBehavior = originalBackgroundBehavior;
                originalInputSettings = null;
            }
            previousKeyboard?.MakeCurrent();
            previousMouse?.MakeCurrent();
            desktop = null;
        }

        private static void CheckGrid()
        {
            KitchenGridPlacement grid = Object.FindFirstObjectByType<KitchenGridPlacement>();
            Assert(grid && grid.cells.Length > 0 && grid.modulePrefabs.Length > 1, "grid/configured");
            grid.Select(1); grid.Place(0);
            Assert(!grid.HasFoundation(0) && !grid.HasEquipment(0), "grid/equipment-requires-foundation");
            grid.Select(0); grid.Place(0);
            Assert(grid.HasFoundation(0) && !grid.HasEquipment(0), "grid/foundation-first");
            int children = grid.transform.childCount;
            grid.Place(0);
            Assert(grid.transform.childCount == children, "grid/cannot-duplicate-foundation");
            grid.Select(1); grid.Place(0);
            Assert(grid.HasEquipment(0), "grid/equipment-after-foundation");
            children = grid.transform.childCount;
            grid.Place(0);
            Assert(grid.transform.childCount == children, "grid/cannot-stack-equipment");
            foreach (KitchenHob hob in grid.GetComponentsInChildren<KitchenHob>()) Assert(hob.pan != null, "grid/cloned-hob-has-own-pan");
        }

        private static void BeginTrashCheck()
        {
            KitchenTrash trash = Object.FindFirstObjectByType<KitchenTrash>();
            Assert(trash != null, "trash/configured");
            Collider trigger = trash.GetComponentsInChildren<Collider>().FirstOrDefault(collider => collider.isTrigger);
            Assert(trigger != null, "trash/physical-trigger-configured");
            trashFood = Spawn("Apple");
            trashResource = trashFood.CurrentResource;
            trashInventoryBefore = session.inventory.GetAmount(trashResource);
            Assert(trashFood.Claim(), "trash/claim-test-food");
            trashFood.transform.position = trigger.bounds.center;
            Rigidbody body = trashFood.GetComponent<Rigidbody>();
            body.isKinematic = false;
            body.useGravity = false;
            body.constraints = RigidbodyConstraints.FreezeAll;
            Physics.SyncTransforms();
            trashStarted = EditorApplication.timeSinceStartup;
        }

        private static GameObject Prefab(string kind)
        {
            string path = "Assets/Kitchen/Prefabs/Food/" + kind + ".prefab";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (!prefab) throw new InvalidOperationException("Missing actual kitchen prefab: " + path);
            return prefab;
        }

        private static KitchenFood Spawn(string kind)
        {
            GameObject instance = Object.Instantiate(Prefab(kind), new Vector3(30 + ++spawnNumber, 4, 30), Quaternion.identity);
            KitchenFood food = instance.GetComponent<KitchenFood>();
            Assert(food && food.definition, "prefab/" + kind + "/food-definition");
            Freeze(food);
            return food;
        }

        private static void Freeze(KitchenFood food)
        {
            Rigidbody body = food.GetComponent<Rigidbody>();
            if (body) { body.useGravity = false; body.isKinematic = true; }
        }

        private static void SetEnergy(int amount) => session.ChangeEnergy(amount - session.Energy);
        private static bool Near(float value, float expected) => Mathf.Abs(value - expected) < 0.02f;

        private static void Assert(bool condition, string name)
        {
            if (!condition) throw new InvalidOperationException("Assertion failed: " + name);
            Passed.Add(name);
            Debug.Log("[Kitchen Play Check] PASS " + name);
        }

        private static void Finish(string failure)
        {
            if (SessionState.GetBool(CompleteKey, false)) return;
            SessionState.SetBool(CompleteKey, true);
            SessionState.SetInt(ExitKey, failure == null ? 0 : 1);
            if (testKeyboard != null || testMouse != null || originalInputSettings) CleanupDesktopInput();
            if (session) SetEnergy(originalEnergy);
            var diagnostics = JsonUtility.FromJson<Diagnostics>(SessionState.GetString(DiagnosticsKey, "{}"));
            var report = new Report { passed = failure == null, timestampUtc = DateTime.UtcNow.ToString("O"), assertionCount = Passed.Count, assertions = Passed.ToArray(), failure = failure,
                editorDiagnostics = diagnostics?.entries?.ToArray() ?? Array.Empty<string>(), observations = Observations.ToArray() };
            string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs"));
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "kitchen-play-checks.json"), JsonUtility.ToJson(report, true));
            Debug.Log("KITCHEN_GAMEPLAY_CHECKS_COMPLETE " + Passed.Count + " assertions; awaiting editor teardown verification.");
            EditorApplication.ExitPlaymode();
        }

        private static void FinalizeAfterEditMode()
        {
            int exitCode = 1;
            string finalMessage;
            try
            {
                string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "kitchen-play-checks.json"));
                Report report = JsonUtility.FromJson<Report>(File.ReadAllText(path));
                string teardownError = SessionState.GetString(TeardownErrorKey, "");
                if (!InputSystem.settings) teardownError += "InputSystem.settings lost its native settings object after Play Mode.\n";
                report.editorTeardownVerified = string.IsNullOrEmpty(teardownError);
                if (!report.editorTeardownVerified)
                {
                    report.passed = false;
                    report.failure = (report.failure ?? "") + "\nEditor teardown failed:\n" + teardownError;
                }
                File.WriteAllText(path, JsonUtility.ToJson(report, true));
                exitCode = report.passed ? 0 : 1;
                finalMessage = report.passed
                    ? "KITCHEN_PLAY_CHECKS_PASS " + report.assertionCount + " assertions; editor teardown verified"
                    : "KITCHEN_PLAY_CHECKS_FAIL " + report.failure;
            }
            catch (Exception exception) { finalMessage = "KITCHEN_PLAY_CHECKS_FAIL editor finalization: " + exception; }
            SessionState.SetInt(ExitKey, exitCode);
            SessionState.EraseBool(ActiveKey);
            SessionState.EraseBool(CompleteKey);
            SessionState.EraseString(TeardownErrorKey);
            if (exitCode == 0) Debug.Log(finalMessage);
            else Debug.LogError(finalMessage);
            if (Application.isBatchMode) EditorApplication.Exit(exitCode);
        }
    }
}
