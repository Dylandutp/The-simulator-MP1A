using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Inputs;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation;
using UnityEngine.XR.Interaction.Toolkit.Samples.StarterAssets;
using Unity.XR.CoreUtils;

namespace HouseSim.Kitchen.Editor
{
    public static class KitchenControllerChecks
    {
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
                        check(select.IsPressed() && activate.IsPressed(), "controller/" + hand + "/face-button-eats-while-held");
                        InputSystem.QueueStateEvent(device, new XRSimulatedControllerState { primary2DAxis = hand == "Left" ? Vector2.up : Vector2.right });
                        yield return new WaitForSeconds(.1f);
                        var move = asset.FindAction("XRI " + hand + " Locomotion/Move", true);
                        var turn = asset.FindAction("XRI " + hand + " Locomotion/Snap Turn", true);
                        check(hand == "Left" ? move.ReadValue<Vector2>().magnitude > .5f && !turn.enabled
                            : !move.enabled && turn.ReadValue<Vector2>().magnitude > .5f, "controller/" + hand + "/stick-route");
                        var teleport = asset.FindAction("XRI " + hand + " Locomotion/Teleport Mode", true);
                        check(teleport.controls.Count == 0, "controller/" + hand + "/no-teleport-input");
                        check(!select.IsPressed(), "controller/" + hand + "/release-ends-grab");
                    }
                    finally { InputSystem.RemoveDevice(device); }
                }
            }
            finally
            {
                settings.backgroundBehavior = focus;
                settings.editorInputBehaviorInPlayMode = routing;
            }
        }
    }
}
