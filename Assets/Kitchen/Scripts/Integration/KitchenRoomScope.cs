using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using Unity.XR.CoreUtils;

namespace HouseSim.Kitchen
{
    // Additive scenes can remain loaded after the player walks into another room.
    // The authored floor, rather than scene loading, owns the kitchen input lifetime.
    [DefaultExecutionOrder(-100)]
    public sealed class KitchenRoomScope : MonoBehaviour
    {
        public bool IsInside { get; private set; }
        public BoxCollider RoomFloor { get; private set; }
        XROrigin origin;
        KitchenControllerControls controls;
        KitchenPlayerHeight height;
        KitchenDesktopController desktop;
        bool useDesktopPreview, ownsDesktop, previousDesktopEnabled;
        Camera previousDesktopCamera;
        Transform previousDesktopRoot;
        bool previousDesktopHelp;
        readonly List<(Behaviour component, bool enabled)> desktopOverrides = new();
        Transform cameraParent;
        Vector3 previousParentPosition, previousEyePosition, appliedEyePosition;
        Quaternion previousEyeRotation;
        bool desktopApplied;

        public void Configure(XROrigin player, BoxCollider roomFloor, KitchenControllerControls controllerControls, bool useDesktopPreview)
        {
            LeaveRoom();
            origin = player;
            RoomFloor = roomFloor;
            controls = controllerControls;
            this.useDesktopPreview = useDesktopPreview;
            if (!RoomFloor) Debug.LogWarning("Kitchen controls require the room floor collider; shared controls remain unchanged.", this);
            RefreshPresence();
        }

        void Update() => RefreshPresence();

        public void RefreshPresence()
        {
            bool inside = isActiveAndEnabled && origin && origin.Camera && RoomFloor && ContainsHead(origin.Camera.transform.position);
            if (inside == IsInside)
            {
                // A headset can connect after the editor entered Play mode.
                if (inside && desktopApplied != ShouldUseDesktop()) ApplyInputMode();
                return;
            }
            if (inside) EnterRoom();
            else LeaveRoom();
        }

        bool ContainsHead(Vector3 point)
        {
            var floorTransform = RoomFloor.transform;
            Vector3 local = floorTransform.InverseTransformPoint(point) - RoomFloor.center;
            Vector3 half = RoomFloor.size * .5f;
            Vector3 scale = floorTransform.lossyScale;
            // Small exit-only tolerance prevents tracked-head jitter at a doorway.
            float margin = IsInside ? .08f : 0f;
            float xScale = Mathf.Max(.001f, Mathf.Abs(scale.x));
            float yScale = Mathf.Max(.001f, Mathf.Abs(scale.y));
            float zScale = Mathf.Max(.001f, Mathf.Abs(scale.z));
            float heightAboveFloor = (local.y - half.y) * yScale;
            return Mathf.Abs(local.x) <= half.x + margin / xScale
                && Mathf.Abs(local.z) <= half.z + margin / zScale
                // Keep recovery alive during a fall from this room, even if one
                // physics step carries the player more than four metres below it.
                && (heightAboveFloor >= -4f || IsInside) && heightAboveFloor <= 4f;
        }

        void EnterRoom()
        {
            IsInside = true;
            if (controls && controls.isActiveAndEnabled) controls.Configure(origin);
            ApplyInputMode();
            SetRoomPresentation(true);
        }

        bool ShouldUseDesktop()
        {
            if (!useDesktopPreview || XRSettings.isDeviceActive) return false;
            return !XRSettings.enabled || string.IsNullOrEmpty(XRSettings.loadedDeviceName)
                || !InputDevices.GetDeviceAtXRNode(XRNode.Head).isValid;
        }

        void ApplyInputMode()
        {
            if (ShouldUseDesktop())
            {
                if (height) height.Deactivate();
                EnableDesktop();
            }
            else
            {
                DisableDesktop();
                height = GetComponent<KitchenPlayerHeight>();
                if (!height) height = gameObject.AddComponent<KitchenPlayerHeight>();
                height.Configure(origin);
            }
        }

        void LeaveRoom()
        {
            IsInside = false;
            if (controls) controls.Deactivate();
            if (height) height.Deactivate();
            DisableDesktop();
            SetRoomPresentation(false);
        }

        void SetRoomPresentation(bool active)
        {
            var recovery = GetComponent<KitchenPlayerRecovery>();
            if (recovery) recovery.enabled = active;
            var energy = GetComponent<KitchenEnergyHUD>();
            if (energy) energy.enabled = active;
            var context = GetComponent<KitchenContextHUD>();
            if (context) context.enabled = active;
        }

        void EnableDesktop()
        {
            var cam = origin.Camera;
            desktop = origin.GetComponent<KitchenDesktopController>();
            if (!desktop)
            {
                desktop = origin.gameObject.AddComponent<KitchenDesktopController>();
                desktop.enabled = false;
                ownsDesktop = true;
            }
            previousDesktopEnabled = desktop.enabled;
            previousDesktopCamera = desktop.viewCamera;
            previousDesktopRoot = desktop.playerRoot;
            previousDesktopHelp = desktop.showHelp;
            foreach (var behaviour in origin.GetComponentsInChildren<MonoBehaviour>(true))
            {
                string type = behaviour.GetType().Name;
                if (type != "TrackedPoseDriver" && !type.Contains("MoveProvider") && !type.Contains("TurnProvider")) continue;
                desktopOverrides.Add((behaviour, behaviour.enabled));
                behaviour.enabled = false;
            }
            cameraParent = cam.transform.parent;
            if (cameraParent)
            {
                previousParentPosition = cameraParent.localPosition;
                cameraParent.localPosition = Vector3.zero;
            }
            previousEyePosition = cam.transform.localPosition;
            previousEyeRotation = cam.transform.localRotation;
            appliedEyePosition = new Vector3(0, KitchenPlayerHeight.InitialEyeHeight, 0);
            cam.transform.localPosition = appliedEyePosition;
            cam.transform.localRotation = Quaternion.identity;
            desktop.viewCamera = cam;
            desktop.playerRoot = origin.transform;
            desktop.showHelp = false;
            desktop.enabled = true;
            desktop.ResetMotion();
            desktopApplied = true;
            var context = GetComponent<KitchenContextHUD>();
            if (context) context.desktop = desktop;
        }

        void DisableDesktop()
        {
            if (!desktopApplied) return;
            desktopApplied = false;
            if (desktop)
            {
                desktop.enabled = false;
                if (desktop.viewCamera == (origin ? origin.Camera : null)) desktop.viewCamera = previousDesktopCamera;
                if (desktop.playerRoot == (origin ? origin.transform : null)) desktop.playerRoot = previousDesktopRoot;
                if (!desktop.showHelp) desktop.showHelp = previousDesktopHelp;
                if (!ownsDesktop) desktop.enabled = previousDesktopEnabled;
            }
            foreach (var state in desktopOverrides)
                if (state.component && !state.component.enabled) state.component.enabled = state.enabled;
            desktopOverrides.Clear();
            if (cameraParent && cameraParent.localPosition == Vector3.zero) cameraParent.localPosition = previousParentPosition;
            if (origin && origin.Camera)
            {
                var eye = origin.Camera.transform;
                if (eye.localPosition == appliedEyePosition) eye.localPosition = previousEyePosition;
                if (eye.localRotation == Quaternion.identity) eye.localRotation = previousEyeRotation;
            }
        }

        void OnEnable() => RefreshPresence();
        void OnDisable() => LeaveRoom();
        void OnDestroy()
        {
            LeaveRoom();
            if (ownsDesktop && desktop) Destroy(desktop);
        }
    }
}
