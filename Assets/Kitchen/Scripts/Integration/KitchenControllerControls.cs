using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Inputs;
using UnityEngine.XR.Interaction.Toolkit.Samples.StarterAssets;
using Unity.XR.CoreUtils;

namespace HouseSim.Kitchen
{
    // Scope binding overrides to the loaded kitchen, without changing the shared rig asset.
    public sealed class KitchenControllerControls : MonoBehaviour
    {
        readonly List<(InputAction action, int index, InputBinding binding)> bindings = new();
        readonly List<(ControllerInputActionManager manager, bool motion)> managers = new();

        public void Configure(XROrigin origin)
        {
            Restore();
            foreach (var manager in origin.GetComponentsInChildren<ControllerInputActionManager>(true))
            {
                managers.Add((manager, manager.smoothMotionEnabled));
                manager.smoothMotionEnabled = manager.name.Contains("Left");
            }
            var visited = new HashSet<InputActionAsset>();
            foreach (var input in origin.GetComponentsInChildren<InputActionManager>(true))
                foreach (var asset in input.actionAssets)
                {
                    if (!asset || !visited.Add(asset)) continue;
                    foreach (string hand in new[] { "Left", "Right" })
                    {
                        var interaction = asset.FindActionMap("XRI " + hand + " Interaction");
                        if (interaction != null)
                        {
                            Remap(interaction.FindAction("Select"), "TriggerButton", hand);
                            Remap(interaction.FindAction("Select Value"), "Trigger", hand);
                            Remap(interaction.FindAction("Activate"), "SecondaryButton", hand);
                            Remap(interaction.FindAction("Activate Value"), "SecondaryButton", hand);
                        }
                        var locomotion = asset.FindActionMap("XRI " + hand + " Locomotion");
                        if (locomotion == null) continue;
                        DisableBindings(locomotion.FindAction("Teleport Mode"));
                        DisableBindings(locomotion.FindAction("Teleport Mode Cancel"));
                    }
                }
        }

        void Remap(InputAction action, string control, string hand)
        {
            if (action == null) return;
            for (int i = 0; i < action.bindings.Count; ++i)
                if (action.bindings[i].path.StartsWith("<XRController>"))
                    Override(action, i, "<XRController>{" + hand + "Hand}/{" + control + "}");
        }

        void DisableBindings(InputAction action)
        {
            if (action == null) return;
            for (int i = 0; i < action.bindings.Count; ++i) Override(action, i, "");
        }

        void Override(InputAction action, int index, string path)
        {
            bindings.Add((action, index, action.bindings[index]));
            action.ApplyBindingOverride(index, path);
        }

        void OnDestroy() => Restore();

        void Restore()
        {
            foreach (var entry in bindings)
            {
                entry.action.RemoveBindingOverride(entry.index);
                if (entry.binding.hasOverrides) entry.action.ApplyBindingOverride(entry.index, entry.binding);
            }
            bindings.Clear();
            foreach (var entry in managers)
                if (entry.manager) entry.manager.smoothMotionEnabled = entry.motion;
            managers.Clear();
        }
    }
}
