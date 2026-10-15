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
    /// <summary>Runs real desktop pickup and XR manager selection without requiring a connected headset.</summary>
    public static class KitchenKnifeGripChecks
    {
        static readonly Vector3 Origin = new Vector3(180f, 4f, 180f);

        public static IEnumerator Run(Action<bool, string> check)
        {
            yield return Desktop(check);
            yield return Controller(check, "left", new Vector3(26f, -35f, 41f));
            yield return Controller(check, "right", new Vector3(-23f, 55f, -37f));
        }

        static KitchenKnife MakeKnife()
        {
            var station = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Kitchen/Prefabs/Stations/PrepStation.prefab");
            var knife = Object.Instantiate(station.GetComponentInChildren<KitchenKnife>().gameObject,
                Origin, Quaternion.Euler(70f, 130f, 115f)).GetComponent<KitchenKnife>();
            foreach (var recovery in knife.GetComponentsInChildren<KitchenRespawnTool>()) recovery.enabled = false;
            var body = knife.GetComponent<Rigidbody>();
            body.useGravity = false;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            body.isKinematic = true;
            knife.GetComponent<XRGrabInteractable>().throwOnDetach = false;
            return knife;
        }

        static IEnumerator Desktop(Action<bool, string> check)
        {
            var eyeObject = new GameObject("Knife desktop grip check");
            var anchorObject = new GameObject("Knife grip check anchor");
            KitchenKnife knife = null;
            KitchenDesktopController desktop = null;
            CursorLockMode oldLock = Cursor.lockState;
            bool oldVisible = Cursor.visible;
            try
            {
                eyeObject.transform.SetPositionAndRotation(Origin - Vector3.forward, Quaternion.Euler(35f, 25f, 0f));
                var camera = eyeObject.AddComponent<Camera>(); camera.enabled = false;
                anchorObject.transform.position = Origin;
                desktop = eyeObject.AddComponent<KitchenDesktopController>();
                desktop.viewCamera = camera; desktop.playerRoot = eyeObject.transform;
                desktop.holdAnchor = anchorObject.transform;
                desktop.autoDetectXR = false; desktop.createCharacterController = false;
                desktop.moveSpeed = 0f; desktop.lookSensitivity = 0f; desktop.interactionDistance = 0f; desktop.showHelp = false;
                yield return null;
                knife = MakeKnife();
                Physics.SyncTransforms();
                typeof(KitchenDesktopController).GetMethod("TryGrab", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(desktop, new object[] { knife.GetComponentInChildren<Collider>() });
                check(desktop.HeldObject == knife.gameObject && Vector3.Dot(knife.BladeDownDirection, Vector3.down) > .999f,
                    "knife-grip/desktop-tilted-pickup-immediately-edge-down");
                check(Vector3.Dot(knife.transform.right, Vector3.ProjectOnPlane(camera.transform.forward, Vector3.up).normalized) > .999f,
                    "knife-grip/desktop-blade-points-away-from-player");
                camera.transform.rotation = Quaternion.Euler(-50f, 75f, 0f);
                yield return new WaitForSeconds(.4f);
                check(Vector3.Dot(knife.BladeDownDirection, Vector3.down) > .999f,
                    "knife-grip/desktop-looking-up-or-down-keeps-cutting-pose");
                check(KitchenAudioEmitter.Get(knife).CueCount(KitchenSound.Cut) == 0,
                    "knife-grip/desktop-orienting-does-not-trigger-cut-sound");
                desktop.Release(false);
                // The last held FixedUpdate may already have queued MoveRotation for this
                // physics step. Drain that target before checking a newly assigned loose pose.
                yield return new WaitForFixedUpdate();
                yield return new WaitForFixedUpdate();
                Quaternion loose = Quaternion.Euler(35f, 80f, 100f);
                var body = knife.GetComponent<Rigidbody>();
                body.rotation = loose;
                knife.transform.rotation = loose;
                Physics.SyncTransforms();
                yield return new WaitForFixedUpdate();
                yield return new WaitForFixedUpdate();
                yield return null;
                float bodyError = Quaternion.Angle(body.rotation, loose);
                float visualError = Quaternion.Angle(knife.transform.rotation, loose);
                Debug.Log("KITCHEN_KNIFE_RELEASE: bodyDegrees=" + bodyError.ToString("0.000") + " visualDegrees=" + visualError.ToString("0.000"));
                check(desktop.HeldObject == null && !KitchenDesktopHeld.IsHeldByDesktop(knife.gameObject)
                    && bodyError < .1f && visualError < .1f, "knife-grip/released-knife-is-not-world-locked");
            }
            finally
            {
                if (desktop) { desktop.Release(false); desktop.enabled = false; }
                if (knife) Object.Destroy(knife.gameObject);
                Object.Destroy(anchorObject); Object.Destroy(eyeObject);
                Cursor.lockState = oldLock; Cursor.visible = oldVisible;
            }
            yield return null;
        }

        static IEnumerator Controller(Action<bool, string> check, string side, Vector3 handAngles)
        {
            KitchenKnife knife = null;
            var handObject = new GameObject("Knife " + side + " controller grip check");
            XRInteractionManager manager = Object.FindFirstObjectByType<XRInteractionManager>();
            KitchenKnifeGripInteractor hand = null;
            XRGrabInteractable grab = null;
            try
            {
                handObject.transform.SetPositionAndRotation(Origin + new Vector3(.3f, .3f, .1f), Quaternion.Euler(handAngles));
                hand = handObject.AddComponent<KitchenKnifeGripInteractor>();
                hand.interactionManager = manager;
                hand.keepSelectedTargetValid = true;
                var attach = new GameObject("Controller attach").transform;
                attach.SetParent(hand.transform, false);
                attach.localPosition = new Vector3(.02f, -.015f, .035f);
                attach.localRotation = Quaternion.Euler(-12f, 5f, 7f);
                hand.attachTransform = attach;
                knife = MakeKnife();
                grab = knife.GetComponent<XRGrabInteractable>();
                grab.interactionManager = manager;
                yield return null; // Register the transformer and interactor with the real XR manager.
                check(grab.singleGrabTransformersCount == 1 && ReferenceEquals(grab.GetSingleGrabTransformerAt(0), knife),
                    "knife-grip/" + side + "/saved-prefab-registers-knife-transformer");
                manager.SelectEnter((IXRSelectInteractor)hand, (IXRSelectInteractable)grab);
                yield return new WaitForSeconds(.35f);
                Pose target = grab.GetTargetPose();
                Debug.Log("KITCHEN_KNIFE_XR_INITIAL: side=" + side + " selected=" + grab.isSelected
                    + " movement=" + grab.movementType + " maxAngular=" + knife.GetComponent<Rigidbody>().maxAngularVelocity
                    + " targetDown=" + Vector3.Dot(target.rotation * Vector3.down, Vector3.down).ToString("0.000")
                    + " bodyDown=" + Vector3.Dot(knife.GetComponent<Rigidbody>().rotation * Vector3.down, Vector3.down).ToString("0.000")
                    + " visualDown=" + Vector3.Dot(knife.BladeDownDirection, Vector3.down).ToString("0.000")
                    + " gripDistance=" + Vector3.Distance(knife.HandleCenter, attach.position).ToString("0.000"));
                check(grab.isSelected && Vector3.Dot(target.rotation * Vector3.down, Vector3.down) > .995f,
                    "knife-grip/" + side + "/transformer-computes-edge-down-target");
                // The authored grab uses VelocityTracking with an 8 rad/s body cap.
                // A knife starting nearly upside down needs actual fixed steps to reach
                // its correct target; don't mistake the eased physical turn for a lock failure.
                yield return SettleGrip(knife, attach);
                check(grab.isSelected && Vector3.Dot(knife.BladeDownDirection, Vector3.down) > .995f,
                    "knife-grip/" + side + "/xr-selection-aligns-edge-down");
                check(Vector3.Distance(knife.HandleCenter, attach.position) < .015f,
                    "knife-grip/" + side + "/controller-holds-handle-not-blade");
                check(Vector3.Dot(knife.transform.right, Vector3.ProjectOnPlane(attach.forward, Vector3.up).normalized) > .99f,
                    "knife-grip/" + side + "/blade-follows-actual-attach-forward");

                Quaternion before = knife.transform.rotation;
                Quaternion wristRoll = Quaternion.AngleAxis(37f, attach.forward);
                hand.transform.rotation = wristRoll * hand.transform.rotation;
                yield return new WaitForSeconds(.2f);
                check(Quaternion.Angle(knife.transform.rotation, wristRoll * before) < 1f,
                    "knife-grip/" + side + "/wrist-roll-remains-natural-after-pickup");
                check(Vector3.Dot(knife.BladeDownDirection, Vector3.down) < .95f,
                    "knife-grip/" + side + "/no-continuous-world-upright-lock");
                hand.transform.position += new Vector3(.08f, -.12f, .04f);
                yield return new WaitForSeconds(.2f);
                check(Vector3.Distance(knife.HandleCenter, attach.position) < .015f,
                    "knife-grip/" + side + "/single-hand-downward-motion-keeps-grip");
                check(KitchenAudioEmitter.Get(knife).CueCount(KitchenSound.Cut) == 0,
                    "knife-grip/" + side + "/empty-air-motion-does-not-cut");

                manager.SelectExit((IXRSelectInteractor)hand, (IXRSelectInteractable)grab);
                yield return null;
                check(!grab.isSelected, "knife-grip/" + side + "/releases-through-xr-manager");
                knife.transform.rotation = Quaternion.Euler(155f, -20f, 72f);
                knife.GetComponent<Rigidbody>().rotation = knife.transform.rotation;
                manager.SelectEnter((IXRSelectInteractor)hand, (IXRSelectInteractable)grab);
                yield return new WaitForSeconds(.3f);
                yield return SettleGrip(knife, attach);
                check(Vector3.Dot(knife.BladeDownDirection, Vector3.down) > .995f,
                    "knife-grip/" + side + "/new-pickup-realigns-instead-of-preserving-tipped-pose");
            }
            finally
            {
                if (manager && hand && grab && grab.isSelected) manager.SelectExit((IXRSelectInteractor)hand, (IXRSelectInteractable)grab);
                if (hand) hand.enabled = false;
                if (knife) Object.Destroy(knife.gameObject);
                Object.Destroy(handObject);
            }
            yield return null;
        }

        static IEnumerator SettleGrip(KitchenKnife knife, Transform attach)
        {
            float deadline = Time.time + .6f;
            while (Time.time < deadline && (Vector3.Dot(knife.BladeDownDirection, Vector3.down) <= .995f
                || Vector3.Distance(knife.HandleCenter, attach.position) >= .015f))
                yield return new WaitForFixedUpdate();
            yield return null;
            Debug.Log("KITCHEN_KNIFE_XR_SETTLED: visualDown=" + Vector3.Dot(knife.BladeDownDirection, Vector3.down).ToString("0.000")
                + " gripDistance=" + Vector3.Distance(knife.HandleCenter, attach.position).ToString("0.000"));
        }
    }

    // Base interactor keeps selection active, but deliberately supplies no hover targets or real controller input.
    public sealed class KitchenKnifeGripInteractor : XRBaseInteractor
    {
        public override void GetValidTargets(List<IXRInteractable> targets) => targets.Clear();
    }
}
