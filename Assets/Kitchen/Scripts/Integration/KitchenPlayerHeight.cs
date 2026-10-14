using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using Unity.XR.CoreUtils;

namespace HouseSim.Kitchen
{
    // A separate offset keeps floor tracking, gravity, and the tracked head/hand poses intact.
    public sealed class KitchenPlayerHeight : MonoBehaviour
    {
        public const float InitialEyeHeight = 1.7f;
        public float minimumEyeHeight = InitialEyeHeight;
        public bool IsCalibrated { get; private set; }
        public float AddedHeight { get; private set; }
        XROrigin origin;
        Transform trackingParent, heightOffset;
        readonly List<Transform> trackedChildren = new();
        XROrigin.TrackingOriginMode previousMode;
        float previousDeviceHeight;

        public void Configure(XROrigin player)
        {
            if (origin || !player || !player.Camera || !player.CameraFloorOffsetObject) return;
            origin = player;
            previousMode = origin.RequestedTrackingOriginMode;
            previousDeviceHeight = origin.CameraYOffset;
            origin.CameraYOffset = minimumEyeHeight;
            origin.RequestedTrackingOriginMode = XROrigin.TrackingOriginMode.Floor;
            trackingParent = origin.CameraFloorOffsetObject.transform;
            foreach (Transform child in trackingParent) trackedChildren.Add(child);
            heightOffset = new GameObject("Kitchen standing height").transform;
            heightOffset.SetParent(trackingParent, false);
            foreach (var child in trackedChildren) child.SetParent(heightOffset, false);
            StartCoroutine(CalibrateWhenTracked());
        }

        IEnumerator CalibrateWhenTracked()
        {
            float stableTime = 0;
            while (origin && !IsCalibrated)
            {
                var head = InputDevices.GetDeviceAtXRNode(XRNode.Head);
                bool tracked = head.isValid && head.TryGetFeatureValue(CommonUsages.isTracked, out bool active) && active
                    && head.TryGetFeatureValue(CommonUsages.trackingState, out InputTrackingState tracking)
                    && (tracking & InputTrackingState.Position) != 0
                    && head.TryGetFeatureValue(CommonUsages.devicePosition, out _)
                    && origin.CurrentTrackingOriginMode != TrackingOriginModeFlags.Unknown;
                stableTime = tracked ? stableTime + Time.unscaledDeltaTime : 0;
                if (stableTime >= .4f) CalibrateFromCurrentPose();
                yield return null;
            }
        }

        public void CalibrateFromCurrentPose()
        {
            if (!origin || !heightOffset || IsCalibrated) return;
            AddedHeight = Mathf.Max(0, minimumEyeHeight - origin.CameraInOriginSpaceHeight);
            heightOffset.position += origin.transform.TransformVector(Vector3.up * AddedHeight);
            IsCalibrated = true;
        }

        void OnDestroy()
        {
            if (!trackingParent || !heightOffset) return;
            foreach (var child in trackedChildren)
                if (child && child.parent == heightOffset) child.SetParent(trackingParent, false);
            Destroy(heightOffset.gameObject);
            if (origin)
            {
                origin.CameraYOffset = previousDeviceHeight;
                origin.RequestedTrackingOriginMode = previousMode;
            }
        }
    }
}
