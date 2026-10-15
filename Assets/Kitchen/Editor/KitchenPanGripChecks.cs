using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using Object = UnityEngine.Object;

namespace HouseSim.Kitchen.Editor
{
    public static class KitchenPanGripChecks
    {
        static readonly Vector3 Origin = new Vector3(220f, 4f, 220f);

        public static IEnumerator Run(Action<bool, string> check)
        {
            yield return Desktop(check);
            yield return Controller(check, "left", Quaternion.Euler(24f, -35f, 33f));
            yield return Controller(check, "right", Quaternion.Euler(-20f, 55f, -42f));
        }

        static KitchenPan MakePan()
        {
            var station = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Kitchen/Prefabs/Stations/InductionHob.prefab");
            var pan = Object.Instantiate(station.GetComponentInChildren<KitchenPan>().gameObject,
                Origin, Quaternion.Euler(65f, 110f, 72f)).GetComponent<KitchenPan>();
            foreach (var recovery in pan.GetComponentsInChildren<KitchenRespawnTool>()) recovery.enabled = false;
            var body = pan.GetComponent<Rigidbody>();
            body.useGravity = false; body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative; body.isKinematic = true;
            pan.GetComponent<XRGrabInteractable>().throwOnDetach = false;
            return pan;
        }

        static IEnumerator Desktop(Action<bool, string> check)
        {
            var cameraObject = new GameObject("Pan desktop grip check");
            var anchorObject = new GameObject("Pan grip anchor");
            KitchenDesktopController desktop = null;
            KitchenPan pan = null;
            CursorLockMode oldLock = Cursor.lockState;
            bool oldVisible = Cursor.visible;
            try
            {
                cameraObject.transform.SetPositionAndRotation(Origin - Vector3.forward, Quaternion.Euler(35f, 30f, 0f));
                var camera = cameraObject.AddComponent<Camera>(); camera.enabled = false;
                anchorObject.transform.position = Origin;
                desktop = cameraObject.AddComponent<KitchenDesktopController>();
                desktop.viewCamera = camera; desktop.playerRoot = cameraObject.transform;
                desktop.holdAnchor = anchorObject.transform; desktop.autoDetectXR = false;
                desktop.createCharacterController = false; desktop.moveSpeed = 0f; desktop.lookSensitivity = 0f;
                desktop.interactionDistance = 0f; desktop.showHelp = false;
                yield return null;
                pan = MakePan();
                check(pan.GetComponent<KitchenPanGrip>(), "pan-grip/saved-prefab-has-grip-component");
                Physics.SyncTransforms();
                typeof(KitchenDesktopController).GetMethod("TryGrab", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(desktop, new object[] { pan.GetComponentInChildren<Collider>() });
                check(desktop.HeldObject == pan.gameObject && Vector3.Dot(pan.transform.up, Vector3.up) > .999f,
                    "pan-grip/desktop-tipped-pickup-immediately-opening-up");
                check(Vector3.Dot(pan.transform.forward, Vector3.ProjectOnPlane(camera.transform.forward, Vector3.up).normalized) > .999f,
                    "pan-grip/desktop-handle-faces-player");
                camera.transform.rotation = Quaternion.Euler(-50f, 80f, 0f);
                yield return new WaitForSeconds(.4f);
                check(Vector3.Dot(pan.transform.up, Vector3.up) > .999f,
                    "pan-grip/desktop-camera-pitch-does-not-tip-pan");
                check(pan.TossCount == 0 && pan.EscapedSteakCount == 0,
                    "pan-grip/pickup-alignment-is-not-a-toss");
                desktop.Release(false);
                yield return new WaitForFixedUpdate(); yield return new WaitForFixedUpdate();
                Quaternion loose = Quaternion.Euler(35f, 80f, 100f);
                var body = pan.GetComponent<Rigidbody>(); body.rotation = loose; pan.transform.rotation = loose;
                Physics.SyncTransforms();
                yield return new WaitForFixedUpdate(); yield return new WaitForFixedUpdate(); yield return null;
                check(!pan.IsHeld && Quaternion.Angle(body.rotation, loose) < .1f && Quaternion.Angle(pan.transform.rotation, loose) < .1f,
                    "pan-grip/released-pan-remains-free-to-tip");
            }
            finally
            {
                if (desktop) { desktop.Release(false); desktop.enabled = false; }
                if (pan) Object.Destroy(pan.gameObject);
                Object.Destroy(cameraObject); Object.Destroy(anchorObject);
                Cursor.lockState = oldLock; Cursor.visible = oldVisible;
            }
            yield return null;
        }

        static IEnumerator Controller(Action<bool, string> check, string side, Quaternion rotation)
        {
            var handObject = new GameObject("Pan " + side + " controller check");
            XRInteractionManager manager = Object.FindFirstObjectByType<XRInteractionManager>();
            KitchenPanGripInteractor hand = null;
            KitchenPan pan = null;
            KitchenFood steak = null;
            XRGrabInteractable grab = null;
            try
            {
                handObject.transform.SetPositionAndRotation(Origin + new Vector3(.1f, .4f, -.3f), rotation);
                hand = handObject.AddComponent<KitchenPanGripInteractor>();
                hand.interactionManager = manager; hand.keepSelectedTargetValid = true;
                var attach = new GameObject("Pan test controller attach").transform;
                attach.SetParent(hand.transform, false);
                attach.localPosition = new Vector3(.015f, -.02f, .03f);
                attach.localRotation = Quaternion.Euler(-9f, 5f, 7f);
                hand.attachTransform = attach;
                pan = MakePan();
                var grip = pan.GetComponent<KitchenPanGrip>();
                grab = pan.GetComponent<XRGrabInteractable>(); grab.interactionManager = manager;
                yield return null;
                check(grip && grab.singleGrabTransformersCount == 1 && ReferenceEquals(grab.GetSingleGrabTransformerAt(0), grip),
                    "pan-grip/" + side + "/xr-registers-pan-grip");
                if (side == "left")
                {
                    // A loaded pan may face away from the hand before pickup; its generated
                    // turn into a useful grip must not count as a deliberate toss or spill.
                    pan.transform.rotation = Quaternion.Euler(0f, 170f, 0f);
                    pan.GetComponent<Rigidbody>().rotation = pan.transform.rotation;
                    var foodPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Kitchen/Prefabs/Food/Steak.prefab");
                    Vector3 seat = pan.cookingSurface.TransformPoint(new Vector3(0f, pan.supportedLocalHeight, 0f));
                    steak = Object.Instantiate(foodPrefab, seat, pan.transform.rotation).GetComponent<KitchenFood>();
                    foreach (var recovery in steak.GetComponentsInChildren<KitchenRespawnTool>()) recovery.enabled = false;
                    steak.GetComponent<Rigidbody>().useGravity = false;
                    Physics.SyncTransforms();
                }
                manager.SelectEnter((IXRSelectInteractor)hand, (IXRSelectInteractable)grab);
                yield return new WaitForSeconds(.3f);
                Pose target = grab.GetTargetPose();
                check(grab.isSelected && Vector3.Dot(target.rotation * Vector3.up, Vector3.up) > .995f,
                    "pan-grip/" + side + "/xr-target-opening-up");
                float deadline = Time.time + .65f;
                while (Time.time < deadline && (Vector3.Dot(pan.transform.up, Vector3.up) < .995f
                    || Vector3.Distance(grip.HandleCenter, attach.position) > .015f)) yield return new WaitForFixedUpdate();
                yield return null;
                check(Vector3.Dot(pan.transform.up, Vector3.up) > .995f && Vector3.Distance(grip.HandleCenter, attach.position) < .015f,
                    "pan-grip/" + side + "/opening-up-with-handle-in-hand");
                if (steak)
                {
                    check(pan.ContainsSupportedFood(steak) && !steak.IsDirty && pan.TossCount == 0 && pan.EscapedSteakCount == 0,
                        "pan-grip/loaded-xr-pickup-alignment-keeps-steak-clean-and-seated");
                    steak.Discard();
                }
                Quaternion before = pan.transform.rotation;
                Quaternion wrist = Quaternion.AngleAxis(37f, attach.forward);
                hand.transform.rotation = wrist * hand.transform.rotation;
                yield return new WaitForSeconds(.25f);
                check(Quaternion.Angle(pan.transform.rotation, wrist * before) < 1f && Vector3.Dot(pan.transform.up, Vector3.up) < .95f,
                    "pan-grip/" + side + "/wrist-can-tip-pan-after-pickup");
                hand.transform.position += new Vector3(.06f, .10f, .03f);
                yield return new WaitForSeconds(.2f);
                check(Vector3.Distance(grip.HandleCenter, attach.position) < .015f,
                    "pan-grip/" + side + "/wrist-motion-retains-handle-attachment");
                manager.SelectExit((IXRSelectInteractor)hand, (IXRSelectInteractable)grab);
                yield return new WaitForFixedUpdate(); yield return new WaitForFixedUpdate();
                pan.transform.rotation = Quaternion.Euler(130f, -20f, 90f);
                pan.GetComponent<Rigidbody>().rotation = pan.transform.rotation;
                manager.SelectEnter((IXRSelectInteractor)hand, (IXRSelectInteractable)grab);
                yield return new WaitForSeconds(.9f);
                check(Vector3.Dot(pan.transform.up, Vector3.up) > .995f,
                    "pan-grip/" + side + "/repickup-relevels-tipped-pan");
            }
            finally
            {
                if (manager && hand && grab && grab.isSelected) manager.SelectExit((IXRSelectInteractor)hand, (IXRSelectInteractable)grab);
                if (hand) hand.enabled = false;
                if (steak && !steak.IsConsumed) steak.Discard();
                if (pan) Object.Destroy(pan.gameObject);
                Object.Destroy(handObject);
            }
            yield return null;
        }
    }

    public sealed class KitchenPanGripInteractor : XRBaseInteractor
    {
        public override void GetValidTargets(List<IXRInteractable> targets) => targets.Clear();
    }
}
