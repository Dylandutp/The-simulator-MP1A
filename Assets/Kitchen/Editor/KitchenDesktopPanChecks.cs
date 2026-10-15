using System;
using System.Collections;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HouseSim.Kitchen.Editor
{
    /// <summary>Runs the real desktop hold casts with a separate supported food root in their path.</summary>
    public static class KitchenDesktopPanChecks
    {
        public static IEnumerator Run(Action<bool, string> check)
        {
            Vector3 origin = new Vector3(140f, 4f, 140f);
            CursorLockMode cursorLock = Cursor.lockState;
            bool cursorVisible = Cursor.visible;
            var cameraObject = new GameObject("Desktop pan test camera");
            var anchorObject = new GameObject("Desktop pan test hold anchor");
            KitchenDesktopController desktop = null;
            KitchenPan pan = null;
            KitchenFood steak = null;
            try
            {
                // The eye-to-anchor sphere cast passes through the steak; a missing assembly
                // exclusion pulls the pan toward the eye instead of maintaining this anchor.
                cameraObject.transform.position = origin + new Vector3(0f, .075f, -1.05f);
                cameraObject.transform.rotation = Quaternion.identity;
                var camera = cameraObject.AddComponent<Camera>();
                camera.enabled = false;
                anchorObject.transform.position = origin;
                desktop = cameraObject.AddComponent<KitchenDesktopController>();
                desktop.viewCamera = camera; desktop.playerRoot = cameraObject.transform;
                desktop.holdAnchor = anchorObject.transform;
                desktop.autoDetectXR = false; desktop.createCharacterController = false;
                desktop.moveSpeed = 0f; desktop.lookSensitivity = 0f;
                desktop.interactionDistance = 0f; desktop.showHelp = false;
                yield return null; // Initializes the actual desktop mode without synthesizing input.
                check(desktop.IsDesktopActive, "desktop-pan/isolated-desktop-controller-active");

                var station = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Kitchen/Prefabs/Stations/InductionHob.prefab");
                pan = Object.Instantiate(station.GetComponentInChildren<KitchenPan>().gameObject, origin, Quaternion.identity).GetComponent<KitchenPan>();
                var panRecovery = pan.GetComponent<KitchenRespawnTool>();
                if (panRecovery) panRecovery.enabled = false;
                var panBody = pan.GetComponent<Rigidbody>();
                panBody.useGravity = true; panBody.isKinematic = false;
                Vector3 seat = pan.cookingSurface.TransformPoint(new Vector3(0f, pan.supportedLocalHeight, 0f));
                var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Kitchen/Prefabs/Food/Steak.prefab");
                steak = Object.Instantiate(source, seat, Quaternion.identity).GetComponent<KitchenFood>();
                var recovery = steak.GetComponent<KitchenRespawnTool>();
                if (recovery) recovery.enabled = false;
                var steakBody = steak.GetComponent<Rigidbody>();
                steakBody.useGravity = true; steakBody.isKinematic = false;
                Physics.SyncTransforms();
                typeof(KitchenDesktopController).GetMethod("TryGrab", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(desktop, new object[] { pan.GetComponent<Collider>() });
                check(desktop.HeldObject == pan.gameObject && pan.IsHeld && pan.SupportedSteakCount == 1,
                    "desktop-pan/real-pickup-captures-steak-before-first-hold-cast");

                float largestDrift = 0f;
                float until = Time.time + .7f;
                while (Time.time < until)
                {
                    yield return new WaitForFixedUpdate();
                    largestDrift = Mathf.Max(largestDrift, Vector3.Distance(panBody.position, origin));
                }
                check(largestDrift < .035f && pan.SupportedSteakCount == 1 && !steak.IsDirty,
                    "desktop-pan/supported-steak-does-not-obstruct-own-hold-cast");
                check(Vector3.Distance(steakBody.position, pan.cookingSurface.TransformPoint(new Vector3(0f, pan.supportedLocalHeight, 0f))) < .035f,
                    "desktop-pan/steak-remains-with-held-pan-at-anchor");

                float baseY = steakBody.position.y;
                int previousCatches = pan.CatchCount;
                check(pan.TryToss(.5f) && pan.IsTossing, "desktop-pan/toss-starts-during-real-desktop-hold");
                float highest = baseY;
                float airbornePanDrift = 0f;
                until = Time.time + .9f;
                while (Time.time < until)
                {
                    yield return new WaitForFixedUpdate();
                    highest = Mathf.Max(highest, steakBody.position.y);
                    airbornePanDrift = Mathf.Max(airbornePanDrift, Vector3.Distance(panBody.position, origin));
                }
                check(airbornePanDrift < .035f, "desktop-pan/airborne-steak-does-not-push-pan-toward-eye");
                check(highest - baseY > .025f && highest - baseY < .25f && pan.CatchCount == previousCatches + 1
                    && pan.SupportedSteakCount == 1 && !steak.IsDirty,
                    "desktop-pan/real-desktop-hold-catches-bounded-clean-toss");

                desktop.Release(false);
                check(desktop.HeldObject == null && !pan.IsHeld && !panBody.isKinematic && panBody.useGravity
                    && !steakBody.isKinematic && steakBody.useGravity,
                    "desktop-pan/release-restores-original-physics-modes");
                Debug.Log("KITCHEN_DESKTOP_PAN: carryDrift=" + largestDrift.ToString("0.000")
                    + " tossDrift=" + airbornePanDrift.ToString("0.000") + " peak=" + (highest - baseY).ToString("0.000"));
            }
            finally
            {
                if (desktop) { desktop.Release(false); desktop.enabled = false; }
                if (steak) Object.Destroy(steak.gameObject);
                if (pan) Object.Destroy(pan.gameObject);
                Object.Destroy(cameraObject); Object.Destroy(anchorObject);
                Cursor.lockState = cursorLock;
                Cursor.visible = cursorVisible;
            }
            yield return null;
        }
    }
}
