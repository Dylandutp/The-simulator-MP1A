using System;
using System.Collections;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Inputs;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation;
using UnityEngine.XR.Interaction.Toolkit.Samples.StarterAssets;
using UnityEngine.XR.Interaction.Toolkit.Attachment;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using Unity.XR.CoreUtils;

namespace HouseSim.Kitchen.Editor
{
    public static class KitchenControllerChecks
    {
        public static void VerifyDesktopXR()
        {
            var settings = UnityEngine.XR.OpenXR.OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Standalone);
            var touch = settings ? settings.GetFeature<UnityEngine.XR.OpenXR.Features.Interactions.OculusTouchControllerProfile>() : null;
            if (!touch || !touch.enabled) throw new InvalidOperationException("Desktop Oculus Touch profile must remain enabled after import.");
            Debug.Log("KITCHEN_DESKTOP_XR_PROFILE_PASS");
        }

        public static IEnumerator Run(Action<bool, string> check)
        {
            var origin = UnityEngine.Object.FindFirstObjectByType<XROrigin>();
            var asset = origin.GetComponent<InputActionManager>().actionAssets.First(a => a.FindActionMap("XRI Left Interaction") != null);
            foreach (var manager in origin.GetComponentsInChildren<ControllerInputActionManager>(true))
                check(manager.smoothMotionEnabled == manager.name.Contains("Left") && !manager.smoothTurnEnabled,
                    "controller/" + manager.name + "/walking-and-snap-turn");
            InputSystem.RegisterLayout<XRSimulatedController>();
            var settings = InputSystem.settings;
            var focus = settings.backgroundBehavior;
            var routing = settings.editorInputBehaviorInPlayMode;
            settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            try
            {
                foreach (string hand in new[] { "Left", "Right" })
                {
                    var device = InputSystem.AddDevice<XRSimulatedController>();
                    InputSystem.SetDeviceUsage(device, hand == "Left" ? CommonUsages.LeftHand : CommonUsages.RightHand);
                    try
                    {
                        var select = asset.FindAction("XRI " + hand + " Interaction/Select", true);
                        var activate = asset.FindAction("XRI " + hand + " Interaction/Activate", true);
                        InputSystem.QueueStateEvent(device, new XRSimulatedControllerState { grip = 1, buttons = (ushort)(1 << (int)ControllerButton.GripButton) });
                        yield return new WaitForSeconds(.1f);
                        check(!select.IsPressed(), "controller/" + hand + "/grip-does-not-select");
                        var state = new XRSimulatedControllerState { trigger = 1, buttons = (ushort)(1 << (int)ControllerButton.TriggerButton) };
                        InputSystem.QueueStateEvent(device, state);
                        yield return new WaitForSeconds(.1f);
                        check(select.IsPressed() && !activate.IsPressed(), "controller/" + hand + "/trigger-grabs-without-eating");
                        state.buttons |= (ushort)(1 << (int)ControllerButton.SecondaryButton);
                        InputSystem.QueueStateEvent(device, state);
                        yield return new WaitForSeconds(.1f);
                        check(select.IsPressed() && !activate.IsPressed(), "controller/" + hand + "/secondary-button-does-not-eat");
                        InputSystem.QueueStateEvent(device, new XRSimulatedControllerState { primary2DAxis = hand == "Left" ? Vector2.up : Vector2.right });
                        yield return new WaitForSeconds(.1f);
                        var move = asset.FindAction("XRI " + hand + " Locomotion/Move", true);
                        var turn = asset.FindAction("XRI " + hand + " Locomotion/Snap Turn", true);
                        check(hand == "Left" ? move.ReadValue<Vector2>().magnitude > .5f && !turn.enabled
                            : !move.enabled && turn.ReadValue<Vector2>().magnitude > .5f, "controller/" + hand + "/stick-route");
                        var teleport = asset.FindAction("XRI " + hand + " Locomotion/Teleport Mode", true);
                        check(teleport.controls.Count == 0, "controller/" + hand + "/no-teleport-input");
                        check(!select.IsPressed(), "controller/" + hand + "/release-ends-grab");
                        var jump = asset.FindAction("XRI " + hand + " Locomotion/Jump");
                        check(jump == null || jump.controls.Count == 0, "controller/" + hand + "/no-jump-input");
                    }
                    finally { InputSystem.RemoveDevice(device); }
                }
                yield return HeldObjectControls(origin, asset, check);
            }
            finally
            {
                settings.backgroundBehavior = focus;
                settings.editorInputBehaviorInPlayMode = routing;
            }
        }

        static IEnumerator HeldObjectControls(XROrigin origin, InputActionAsset asset, Action<bool, string> check)
        {
            var controls = origin.GetComponentsInChildren<ControllerInputActionManager>(true);
            var leftRoot = controls.First(c => c.name.Contains("Left")).gameObject;
            var rightRoot = controls.First(c => c.name.Contains("Right")).gameObject;
            var left = leftRoot.GetComponentInChildren<NearFarInteractor>(true);
            var right = rightRoot.GetComponentInChildren<NearFarInteractor>(true);
            var roots = new[] { leftRoot, rightRoot };
            var active = roots.Select(r => r.activeSelf).ToArray();
            var positions = roots.Select(r => r.transform.localPosition).ToArray();
            var rotations = roots.Select(r => r.transform.localRotation).ToArray();
            var nearActive = new[] { left.gameObject.activeSelf, right.gameObject.activeSelf };
            bool keepRightSelected = right.keepSelectedTargetValid;
            var attachments = new[] { (InteractionAttachController)left.interactionAttachController, (InteractionAttachController)right.interactionAttachController };
            var velocityScaling = attachments.Select(a => a.useDistanceBasedVelocityScaling).ToArray();
            var modality = origin.GetComponentInChildren<XRInputModalityManager>(true);
            bool modalityEnabled = modality && modality.enabled;
            if (modality) modality.enabled = false;
            var session = KitchenSession.Instance;
            Transform oldMouth = session.mouthAnchor;
            float oldEnergy = session.Energy;
            var mouth = new GameObject("Controller check mouth");
            session.mouthAnchor = mouth.transform;
            var leftDevice = InputSystem.AddDevice<XRSimulatedController>();
            var rightDevice = InputSystem.AddDevice<XRSimulatedController>();
            InputSystem.SetDeviceUsage(leftDevice, CommonUsages.LeftHand);
            InputSystem.SetDeviceUsage(rightDevice, CommonUsages.RightHand);
            KitchenFood leftFood = null, rightFood = null;
            try
            {
                for (int i = 0; i < roots.Length; i++)
                {
                    roots[i].SetActive(true);
                    roots[i].transform.SetPositionAndRotation(new Vector3(90 + i, 3, 90), Quaternion.identity);
                    attachments[i].useDistanceBasedVelocityScaling = false;
                }
                left.gameObject.SetActive(true); right.gameObject.SetActive(true);
                yield return null;
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Kitchen/Prefabs/Food/Steak.prefab");
                leftFood = UnityEngine.Object.Instantiate(prefab, left.transform.position + Vector3.forward, Quaternion.identity).GetComponent<KitchenFood>();
                rightFood = UnityEngine.Object.Instantiate(prefab, right.transform.position + Vector3.forward, Quaternion.identity).GetComponent<KitchenFood>();
                foreach (var food in new[] { leftFood, rightFood })
                {
                    var body = food.GetComponent<Rigidbody>(); body.useGravity = false; body.isKinematic = true;
                }
                yield return null;
                left.StartManualInteraction((IXRSelectInteractable)leftFood.GetComponent<XRGrabInteractable>());
                right.StartManualInteraction((IXRSelectInteractable)rightFood.GetComponent<XRGrabInteractable>());
                left.interactionAttachController.MoveTo(left.interactionAttachController.transformToFollow.position + Vector3.forward);
                right.interactionAttachController.MoveTo(right.interactionAttachController.transformToFollow.position + Vector3.forward);
                yield return new WaitForSeconds(.12f);
                check(left.hasSelection && right.hasSelection && left.selectionRegion.Value == NearFarInteractor.Region.Far && right.selectionRegion.Value == NearFarInteractor.Region.Far,
                    "controller/held/both-hands-far-selected");
                var anchor = right.GetAttachTransform(rightFood.GetComponent<XRGrabInteractable>());
                var leftAnchor = left.GetAttachTransform(leftFood.GetComponent<XRGrabInteractable>());
                float before = Reach(right, anchor);
                Quaternion beforeRotation = anchor.localRotation;
                InputSystem.QueueStateEvent(leftDevice, new XRSimulatedControllerState { primary2DAxis = Vector2.up });
                InputSystem.QueueStateEvent(rightDevice, new XRSimulatedControllerState { primary2DAxis = Vector2.right });
                yield return new WaitForSeconds(.15f);
                var move = asset.FindAction("XRI Left Locomotion/Move", true);
                var turn = asset.FindAction("XRI Right Locomotion/Snap Turn", true);
                check(move.enabled && move.ReadValue<Vector2>().y > .5f && turn.enabled && turn.ReadValue<Vector2>().x > .5f,
                    "controller/held/sticks-still-walk-and-turn-during-two-far-grabs");
                check(Mathf.Abs(Reach(right, anchor) - before) < .025f && Quaternion.Angle(anchor.localRotation, beforeRotation) < .1f,
                    "controller/held/sticks-do-not-adjust-held-item");
                InputSystem.QueueStateEvent(leftDevice, new XRSimulatedControllerState());
                float leftBefore = Reach(left, leftAnchor);
                InputSystem.QueueStateEvent(rightDevice, new XRSimulatedControllerState { buttons = Button(ControllerButton.PrimaryButton) });
                yield return new WaitForSeconds(.2f);
                float nearer = Reach(right, anchor);
                check(nearer < before - .05f && Mathf.Abs(Reach(left, leftAnchor) - leftBefore) < .025f,
                    "controller/held/A-pulls-right-item-without-moving-left");
                check(asset.FindAction("XRI Right Locomotion/Jump", true).controls.Count == 0, "controller/held/A-cannot-jump");
                InputSystem.QueueStateEvent(rightDevice, new XRSimulatedControllerState { buttons = Button(ControllerButton.SecondaryButton) });
                yield return new WaitForSeconds(.2f);
                check(Reach(right, anchor) > nearer + .05f && rightFood && !rightFood.IsConsumed,
                    "controller/held/B-pushes-item-without-eating");
                InputSystem.QueueStateEvent(rightDevice, new XRSimulatedControllerState());
                mouth.transform.position = new Vector3(90, 10, 90);
                InputSystem.QueueStateEvent(leftDevice, new XRSimulatedControllerState { buttons = Button(ControllerButton.PrimaryButton) });
                yield return new WaitForSeconds(.1f);
                check(rightFood && !rightFood.IsConsumed && leftFood && !leftFood.IsConsumed, "controller/held/X-cannot-eat-food-far-from-mouth");
                InputSystem.QueueStateEvent(leftDevice, new XRSimulatedControllerState());
                yield return null;
                // End the synthetic grab before the edible target is destroyed. From
                // here keep the actual trigger pressed, just as a player would.
                right.EndManualInteraction();
                right.keepSelectedTargetValid = true;
                InputSystem.QueueStateEvent(rightDevice, new XRSimulatedControllerState { trigger = 1, buttons = Button(ControllerButton.TriggerButton) });
                yield return null;
                var rightGrab = rightFood.GetComponent<XRGrabInteractable>();
                if (!right.IsSelecting(rightGrab))
                    right.interactionManager.SelectEnter((IXRSelectInteractor)right, (IXRSelectInteractable)rightGrab);
                yield return new WaitForSeconds(.1f);
                mouth.transform.position = rightFood.transform.position;
                InputSystem.QueueStateEvent(leftDevice, new XRSimulatedControllerState { buttons = Button(ControllerButton.PrimaryButton) });
                yield return new WaitForSeconds(.1f);
                check(!rightFood && leftFood && !leftFood.IsConsumed, "controller/held/X-eats-right-hand-food-near-mouth-once");
                mouth.transform.position = leftFood.transform.position;
                yield return new WaitForSeconds(.12f);
                check(leftFood && !leftFood.IsConsumed, "controller/held/holding-X-does-not-auto-eat-second-food");
                before = Reach(left, leftAnchor);
                InputSystem.QueueStateEvent(rightDevice, new XRSimulatedControllerState { buttons = Button(ControllerButton.PrimaryButton) });
                yield return new WaitForSeconds(.2f);
                check(Reach(left, leftAnchor) < before - .05f, "controller/held/A-adjusts-left-item-when-right-hand-empty");
            }
            finally
            {
                if (left.isPerformingManualInteraction) left.EndManualInteraction();
                if (right.isPerformingManualInteraction) right.EndManualInteraction();
                right.keepSelectedTargetValid = keepRightSelected;
                if (leftFood) UnityEngine.Object.Destroy(leftFood.gameObject);
                if (rightFood) UnityEngine.Object.Destroy(rightFood.gameObject);
                InputSystem.RemoveDevice(leftDevice); InputSystem.RemoveDevice(rightDevice);
                session.mouthAnchor = oldMouth;
                session.ChangeEnergy(Mathf.RoundToInt(oldEnergy - session.Energy));
                UnityEngine.Object.Destroy(mouth);
                for (int i = 0; i < roots.Length; i++)
                {
                    attachments[i].useDistanceBasedVelocityScaling = velocityScaling[i];
                    roots[i].transform.SetLocalPositionAndRotation(positions[i], rotations[i]);
                    roots[i].SetActive(active[i]);
                }
                left.gameObject.SetActive(nearActive[0]); right.gameObject.SetActive(nearActive[1]);
                if (modality) modality.enabled = modalityEnabled;
            }
            yield return null;
        }

        static ushort Button(ControllerButton button) => (ushort)(1 << (int)button);
        static float Reach(NearFarInteractor interactor, Transform anchor) => Vector3.Distance(anchor.position, interactor.interactionAttachController.transformToFollow.position);
    }
}
