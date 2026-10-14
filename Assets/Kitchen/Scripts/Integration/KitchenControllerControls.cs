using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Inputs;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;
using UnityEngine.XR.Interaction.Toolkit.Attachment;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Samples.StarterAssets;
using Unity.XR.CoreUtils;

namespace HouseSim.Kitchen
{
    // The room-presence owner applies this profile only while the player is in the kitchen.
    public sealed class KitchenControllerControls : MonoBehaviour
    {
        readonly List<(InputAction action, InputBinding before, InputBinding applied)> bindings = new();
        readonly List<ManagerState> managers = new();
        readonly List<(InteractionAttachController controller, bool useInput, XRInputValueReader.InputSourceMode source)> attachments = new();
        readonly List<(XRRayInteractor ray, bool manipulate)> rays = new();
        KitchenHeldControllerInput heldControls;

        sealed class ManagerState
        {
            public ControllerInputActionManager manager;
            public bool motion, appliedMotion, turn;
            public readonly List<(InputAction action, bool before, bool applied)> actions = new();
        }

        public bool IsApplied { get; private set; }

        public void Configure(XROrigin origin)
        {
            Deactivate();
            if (!origin || !isActiveAndEnabled) return;
            var assets = new HashSet<InputActionAsset>();
            foreach (var input in origin.GetComponentsInChildren<InputActionManager>(true))
                if (input.actionAssets != null)
                    foreach (var asset in input.actionAssets)
                        if (asset) assets.Add(asset);

            // Stick input belongs to walking/turning even during a far grab. The sample
            // manager otherwise disables locomotion while its attachment uses the stick.
            foreach (var attachment in origin.GetComponentsInChildren<InteractionAttachController>(true))
            {
                attachments.Add((attachment, attachment.useManipulationInput, attachment.manipulationInput.inputSourceMode));
                attachment.useManipulationInput = false;
                attachment.manipulationInput.inputSourceMode = XRInputValueReader.InputSourceMode.Unused;
            }
            foreach (var ray in origin.GetComponentsInChildren<XRRayInteractor>(true))
            {
                rays.Add((ray, ray.manipulateAttachTransform));
                ray.manipulateAttachTransform = false;
            }
            foreach (var manager in origin.GetComponentsInChildren<ControllerInputActionManager>(true))
            {
                bool isLeft = manager.name.Contains("Left");
                var state = new ManagerState
                {
                    manager = manager,
                    motion = manager.smoothMotionEnabled,
                    appliedMotion = isLeft,
                    turn = manager.smoothTurnEnabled
                };
                foreach (var asset in assets)
                {
                    var map = asset.FindActionMap("XRI " + (isLeft ? "Left" : "Right") + " Locomotion");
                    if (map == null) continue;
                    foreach (string name in new[] { "Move", "Turn", "Snap Turn", "Teleport Mode", "Teleport Mode Cancel" })
                    {
                        var action = map.FindAction(name);
                        if (action != null) state.actions.Add((action, action.enabled, false));
                    }
                }
                manager.smoothMotionEnabled = state.appliedMotion;
                for (int i = 0; i < state.actions.Count; ++i)
                {
                    var action = state.actions[i];
                    state.actions[i] = (action.action, action.before, action.action.enabled);
                }
                managers.Add(state);
            }
            foreach (var asset in assets)
            {
                foreach (string hand in new[] { "Left", "Right" })
                {
                    var interaction = asset.FindActionMap("XRI " + hand + " Interaction");
                    if (interaction != null)
                    {
                        Remap(interaction.FindAction("Select"), "TriggerButton", hand);
                        Remap(interaction.FindAction("Select Value"), "Trigger", hand);
                        // Eating is handled once across both hands by X below.
                        DisableBindings(interaction.FindAction("Activate"));
                        DisableBindings(interaction.FindAction("Activate Value"));
                    }
                    var locomotion = asset.FindActionMap("XRI " + hand + " Locomotion");
                    if (locomotion == null) continue;
                    DisableBindings(locomotion.FindAction("Teleport Mode"));
                    DisableBindings(locomotion.FindAction("Teleport Mode Cancel"));
                    DisableBindings(locomotion.FindAction("Jump"));
                }
            }
            heldControls = GetComponent<KitchenHeldControllerInput>();
            if (!heldControls) heldControls = gameObject.AddComponent<KitchenHeldControllerInput>();
            heldControls.Configure(origin);
            IsApplied = true;
        }

        void Remap(InputAction action, string control, string hand)
        {
            if (action == null) return;
            for (int i = 0; i < action.bindings.Count; ++i)
                if (action.bindings[i].path != null && action.bindings[i].path.StartsWith("<XRController>"))
                    Override(action, i, "<XRController>{" + hand + "Hand}/{" + control + "}");
        }

        void DisableBindings(InputAction action)
        {
            if (action == null) return;
            for (int i = 0; i < action.bindings.Count; ++i) Override(action, i, "");
        }

        void Override(InputAction action, int index, string path)
        {
            var before = action.bindings[index];
            action.ApplyBindingOverride(index, path);
            bindings.Add((action, before, action.bindings[index]));
        }

        void OnDisable() => Deactivate();
        void OnDestroy() => Deactivate();

        public void Deactivate()
        {
            IsApplied = false;
            if (heldControls) heldControls.Clear();
            foreach (var entry in bindings)
            {
                // Binding indices can change while another room is configuring its controls.
                int index = -1;
                for (int i = 0; i < entry.action.bindings.Count; ++i)
                    if (entry.action.bindings[i].id == entry.before.id) { index = i; break; }
                if (index < 0) continue;
                var current = entry.action.bindings[index];
                var restored = current;
                if (current.overridePath == entry.applied.overridePath)
                    restored.overridePath = entry.before.overridePath;
                if (current.overrideProcessors == entry.applied.overrideProcessors)
                    restored.overrideProcessors = entry.before.overrideProcessors;
                if (current.overrideInteractions == entry.applied.overrideInteractions)
                    restored.overrideInteractions = entry.before.overrideInteractions;
                if (current.overridePath == restored.overridePath &&
                    current.overrideProcessors == restored.overrideProcessors &&
                    current.overrideInteractions == restored.overrideInteractions) continue;
                entry.action.RemoveBindingOverride(index);
                if (restored.hasOverrides) entry.action.ApplyBindingOverride(index, restored);
            }
            bindings.Clear();
            foreach (var entry in attachments)
                if (entry.controller)
                {
                    if (!entry.controller.useManipulationInput)
                        entry.controller.useManipulationInput = entry.useInput;
                    if (entry.controller.manipulationInput.inputSourceMode == XRInputValueReader.InputSourceMode.Unused)
                        entry.controller.manipulationInput.inputSourceMode = entry.source;
                }
            attachments.Clear();
            foreach (var entry in rays)
                if (entry.ray && !entry.ray.manipulateAttachTransform)
                    entry.ray.manipulateAttachTransform = entry.manipulate;
            rays.Clear();
            foreach (var entry in managers)
            {
                if (!entry.manager || entry.manager.smoothMotionEnabled != entry.appliedMotion ||
                    entry.manager.smoothTurnEnabled != entry.turn) continue;
                // Restoring the manager refreshes several actions. Preserve newer action-state
                // changes, and restore the previous enabled state for actions still owned here.
                var enabledStates = new bool[entry.actions.Count];
                for (int i = 0; i < entry.actions.Count; ++i)
                {
                    var action = entry.actions[i];
                    enabledStates[i] = action.action.enabled == action.applied ? action.before : action.action.enabled;
                }
                entry.manager.smoothMotionEnabled = entry.motion;
                for (int i = 0; i < entry.actions.Count; ++i)
                {
                    var action = entry.actions[i].action;
                    if (enabledStates[i]) action.Enable();
                    else action.Disable();
                }
            }
            managers.Clear();
        }
    }
}
