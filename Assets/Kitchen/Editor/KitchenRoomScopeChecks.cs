using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Inputs;
using UnityEngine.XR.Interaction.Toolkit.Samples.StarterAssets;
using Unity.XR.CoreUtils;
using Object = UnityEngine.Object;

namespace HouseSim.Kitchen.Editor
{
    public static class KitchenRoomScopeChecks
    {
        public static IEnumerator Run(Action<bool, string> check)
        {
            var scope = Object.FindFirstObjectByType<KitchenRoomScope>();
            var origin = Object.FindFirstObjectByType<XROrigin>();
            check(scope && origin && scope.RoomFloor && scope.IsInside,
                "room-scope/saved-kitchen-starts-with-local-controls");
            if (!scope || !origin || !scope.RoomFloor) yield break;

            var floor = scope.RoomFloor;
            var asset = origin.GetComponent<InputActionManager>().actionAssets.First(a => a.FindActionMap("XRI Left Interaction") != null);
            var select = asset.FindAction("XRI Left Interaction/Select", true);
            int bindingIndex = Enumerable.Range(0, select.bindings.Count)
                .First(i => select.bindings[i].path.StartsWith("<XRController>"));
            var managers = origin.GetComponentsInChildren<ControllerInputActionManager>(true);
            var left = managers.First(manager => manager.name.Contains("Left"));
            var right = managers.First(manager => manager.name.Contains("Right"));
            string authoredActions = asset.ToJson();
            Vector3 position = origin.transform.position;
            Quaternion rotation = origin.transform.rotation;
            Vector3 cameraPosition = origin.Camera.transform.localPosition;
            Quaternion cameraRotation = origin.Camera.transform.localRotation;
            Vector3 floorScale = floor.transform.localScale;
            Quaternion floorRotation = floor.transform.localRotation;
            bool enabled = scope.enabled;
            InputBinding original = default;
            bool originalCaptured = false, leftMotion = false, rightMotion = false;
            try
            {
                PutHeadAt(origin, Point(floor, floor.size.x * .5f + 1, 0));
                yield return null;
                check(!scope.IsInside && KitchenActions() == 0,
                    "room-scope/walk-out-with-scene-loaded-disables-A-B-X");
                check(scope.gameObject.scene.isLoaded,
                    "room-scope/exit-did-not-require-scene-unload");
                var recovery = scope.GetComponent<KitchenPlayerRecovery>();
                check(!recovery.enabled && !scope.GetComponent<KitchenEnergyHUD>().enabled
                    && !scope.GetComponent<KitchenContextHUD>().enabled
                    && !origin.GetComponent<KitchenDesktopController>().enabled,
                    "room-scope/exit-stops-kitchen-recovery-HUD-and-desktop-input");
                var sharedDrivers = origin.GetComponentsInChildren<MonoBehaviour>(true).Where(component =>
                    component.GetType().Name == "TrackedPoseDriver" || component.GetType().Name.Contains("MoveProvider")
                    || component.GetType().Name.Contains("TurnProvider")).ToArray();
                var sharedDriverStates = sharedDrivers.Select(driver => driver.enabled).ToArray();
                PutHeadAt(origin, Point(floor, 0, 0) - Vector3.up * 10f);
                scope.RefreshPresence();
                check(!scope.IsInside, "room-scope/room-below-kitchen-does-not-activate-profile");
                original = select.bindings[bindingIndex];
                leftMotion = left.smoothMotionEnabled;
                rightMotion = right.smoothMotionEnabled;
                originalCaptured = true;

                // Simulate the next room's existing controls before crossing into Kitchen.
                const string otherRoomSelect = "<XRController>{LeftHand}/{SecondaryButton}";
                select.ApplyBindingOverride(bindingIndex, otherRoomSelect);
                left.smoothMotionEnabled = false;
                right.smoothMotionEnabled = true;
                PutHeadAt(origin, Point(floor, 0, 0));
                scope.RefreshPresence();
                check(scope.IsInside && UsesTrigger(select, bindingIndex) && KitchenActions() == 3,
                    "room-scope/entry-enables-trigger-and-three-face-button-actions");
                check(left.smoothMotionEnabled && !right.smoothMotionEnabled,
                    "room-scope/entry-applies-kitchen-stick-routing");
                int recoveries = recovery.RecoveryCount;
                PutHeadAt(origin, Point(floor, 0, 0) - Vector3.up * 10f);
                scope.RefreshPresence();
                check(scope.IsInside && recovery.enabled, "room-scope/deep-fall-retains-recovery");
                yield return null;
                check(recovery.RecoveryCount == recoveries + 1 && scope.IsInside,
                    "room-scope/deep-fall-returns-to-kitchen");

                PutHeadAt(origin, Point(floor, 0, floor.size.z * .5f + 1));
                scope.RefreshPresence();
                check(!scope.IsInside && select.bindings[bindingIndex].overridePath == otherRoomSelect
                    && !left.smoothMotionEnabled && right.smoothMotionEnabled && KitchenActions() == 0,
                    "room-scope/exit-restores-other-room-binding-and-stick-routing");

                const string updatedSelect = "<XRController>{LeftHand}/{Primary2DAxisClick}";
                select.ApplyBindingOverride(bindingIndex, updatedSelect);
                PutHeadAt(origin, Point(floor, 0, 0));
                scope.RefreshPresence();
                check(scope.IsInside && UsesTrigger(select, bindingIndex),
                    "room-scope/reentry-reapplies-kitchen-controls");
                scope.enabled = false;
                check(select.bindings[bindingIndex].overridePath == updatedSelect && KitchenActions() == 0,
                    "room-scope/disable-restores-most-recent-baseline");
                scope.enabled = true;
                scope.RefreshPresence();
                check(scope.IsInside && UsesTrigger(select, bindingIndex) && KitchenActions() == 3,
                    "room-scope/reenable-inside-restores-kitchen-profile");

                // A later room manager may claim an input before Kitchen sees the exit.
                const string replacementSelect = "<XRController>{LeftHand}/{MenuButton}";
                select.ApplyBindingOverride(bindingIndex, replacementSelect);
                left.smoothMotionEnabled = false;
                PutHeadAt(origin, Point(floor, -floor.size.x * .5f - 1, 0));
                scope.RefreshPresence();
                check(select.bindings[bindingIndex].overridePath == replacementSelect && !left.smoothMotionEnabled,
                    "room-scope/exit-preserves-newer-room-overrides");

                // The authored floor remains the source of truth after layout edits.
                floor.transform.localRotation = floorRotation * Quaternion.Euler(0, 37, 0);
                floor.transform.localScale = new Vector3(floorScale.x * .75f, floorScale.y, floorScale.z * .6f);
                PutHeadAt(origin, Point(floor, 0, 0));
                scope.RefreshPresence();
                check(scope.IsInside && UsesTrigger(select, bindingIndex),
                    "room-scope/rotated-resized-floor-still-detects-entry");
                PutHeadAt(origin, Point(floor, floor.size.x * .5f + .15f, 0));
                scope.RefreshPresence();
                check(!scope.IsInside && KitchenActions() == 0,
                    "room-scope/rotated-resized-floor-detects-exit-without-world-AABB-leak");
                PutHeadAt(origin, Point(floor, 0, 0));
                scope.Configure(origin, floor, scope.GetComponent<KitchenControllerControls>(), false);
                check(scope.IsInside && !origin.GetComponent<KitchenDesktopController>().enabled
                    && sharedDrivers.Select(driver => driver.enabled).SequenceEqual(sharedDriverStates),
                    "room-scope/VR-mode-restores-shared-pose-and-locomotion-drivers");
                scope.Configure(origin, floor, scope.GetComponent<KitchenControllerControls>(), true);
                check(scope.IsInside && origin.GetComponent<KitchenDesktopController>().enabled
                    && sharedDrivers.All(driver => !driver.enabled)
                    && !scope.GetComponent<KitchenPlayerHeight>().IsCalibrated,
                    "room-scope/desktop-mode-releases-VR-height-and-owns-drivers-only-inside");
                check(asset.ToJson() == authoredActions,
                    "room-scope/shared-input-asset-authored-bindings-unchanged");
            }
            finally
            {
                scope.enabled = false;
                if (originalCaptured)
                {
                    select.RemoveBindingOverride(bindingIndex);
                    if (original.hasOverrides) select.ApplyBindingOverride(bindingIndex, original);
                    left.smoothMotionEnabled = leftMotion;
                    right.smoothMotionEnabled = rightMotion;
                }
                floor.transform.localRotation = floorRotation;
                floor.transform.localScale = floorScale;
                origin.transform.SetPositionAndRotation(position, rotation);
                origin.Camera.transform.SetLocalPositionAndRotation(cameraPosition, cameraRotation);
                scope.enabled = enabled;
                if (enabled) scope.RefreshPresence();
                Physics.SyncTransforms();
            }
            yield return null;
        }

        static bool UsesTrigger(InputAction action, int index) =>
            action.bindings[index].effectivePath == "<XRController>{LeftHand}/{TriggerButton}";

        static int KitchenActions() => InputSystem.ListEnabledActions().Count(action =>
            action.name == "Kitchen pull held item" || action.name == "Kitchen push held item" || action.name == "Kitchen eat held food");

        static Vector3 Point(BoxCollider floor, float x, float z) =>
            floor.transform.TransformPoint(floor.center + new Vector3(x, floor.size.y * .5f, z)) + floor.transform.up * 1.7f;

        static void PutHeadAt(XROrigin origin, Vector3 position) =>
            origin.transform.position += position - origin.Camera.transform.position;
    }
}
