using System;
using System.Collections;
using UnityEngine;
using Unity.XR.CoreUtils;
using Object = UnityEngine.Object;

namespace HouseSim.Kitchen.Editor
{
    public static class KitchenPlayerHeightChecks
    {
        public static IEnumerator Run(Action<bool, string> check)
        {
            foreach (float initialHeight in new[] { .85f, 1.85f })
            {
                var root = new GameObject("Height check rig");
                root.SetActive(false);
                root.transform.position = new Vector3(90, 0, 90);
                var floor = new GameObject("Tracking offset");
                floor.transform.SetParent(root.transform, false);
                var eye = new GameObject("Tracked eye").AddComponent<Camera>();
                eye.enabled = false;
                eye.transform.SetParent(floor.transform, false);
                eye.transform.localPosition = new Vector3(.1f, initialHeight, .2f);
                var hand = new GameObject("Tracked hand").transform;
                hand.SetParent(floor.transform, false);
                hand.localPosition = new Vector3(.3f, initialHeight - .3f, .5f);
                var origin = root.AddComponent<XROrigin>();
                origin.Camera = eye;
                origin.CameraFloorOffsetObject = floor;
                root.SetActive(true);
                var owner = new GameObject("Height check owner");
                var height = owner.AddComponent<KitchenPlayerHeight>();
                Vector3 eyePose = eye.transform.localPosition, handPose = hand.localPosition;
                Vector3 relation = hand.position - eye.transform.position;
                float originalDeviceHeight = origin.CameraYOffset;
                var originalMode = origin.RequestedTrackingOriginMode;
                height.Configure(origin);
                height.CalibrateFromCurrentPose();
                yield return null;
                check(Mathf.Abs(origin.CameraInOriginSpaceHeight - Mathf.Max(1.7f, initialHeight)) < .001f,
                    "height/initial-eye-" + initialHeight);
                check(Vector3.Distance(relation, hand.position - eye.transform.position) < .001f
                    && eye.transform.localPosition == eyePose && hand.localPosition == handPose,
                    "height/head-and-hand-tracked-poses-preserved-" + initialHeight);
                eye.transform.localPosition -= Vector3.up * .25f;
                yield return null;
                check(Mathf.Abs(origin.CameraInOriginSpaceHeight - (Mathf.Max(1.7f, initialHeight) - .25f)) < .001f,
                    "height/natural-crouch-remains-" + initialHeight);
                floor.transform.localPosition = Vector3.zero;
                check(Mathf.Abs(origin.CameraInOriginSpaceHeight - (Mathf.Max(1.7f, initialHeight) - .25f)) < .001f,
                    "height/tracking-offset-reset-does-not-remove-lift-" + initialHeight);
                height.Deactivate();
                check(eye.transform.parent == floor.transform && hand.parent == floor.transform
                    && !height.IsCalibrated && height.AddedHeight == 0
                    && Mathf.Approximately(origin.CameraYOffset, originalDeviceHeight)
                    && origin.RequestedTrackingOriginMode == originalMode,
                    "height/room-exit-restores-height-and-hierarchy-" + initialHeight);
                height.Configure(origin);
                height.CalibrateFromCurrentPose();
                check(eye.transform.parent != floor.transform && eye.transform.parent.parent == floor.transform
                    && floor.transform.childCount == 1 && hand.parent == eye.transform.parent,
                    "height/immediate-reentry-has-no-stale-offset-" + initialHeight);
                height.enabled = false;
                check(eye.transform.parent == floor.transform && hand.parent == floor.transform
                    && !height.IsCalibrated && Mathf.Approximately(origin.CameraYOffset, originalDeviceHeight),
                    "height/disabling-restores-shared-rig-" + initialHeight);
                height.enabled = true;
                origin.CameraYOffset = 1.42f;
                origin.RequestedTrackingOriginMode = XROrigin.TrackingOriginMode.Device;
                height.Configure(origin);
                height.CalibrateFromCurrentPose();
                Object.Destroy(owner);
                yield return null;
                check(eye.transform.parent == floor.transform && hand.parent == floor.transform,
                    "height/unload-restores-shared-hierarchy-" + initialHeight);
                check(Mathf.Approximately(origin.CameraYOffset, 1.42f)
                    && origin.RequestedTrackingOriginMode == XROrigin.TrackingOriginMode.Device,
                    "height/reentry-captures-latest-room-settings-" + initialHeight);

                owner = new GameObject("Height competing owner check");
                height = owner.AddComponent<KitchenPlayerHeight>();
                height.Configure(origin);
                origin.CameraYOffset = 1.52f;
                origin.RequestedTrackingOriginMode = XROrigin.TrackingOriginMode.NotSpecified;
                var newHandParent = new GameObject("Other room hand parent").transform;
                newHandParent.SetParent(floor.transform, false);
                hand.SetParent(newHandParent, false);
                Object.Destroy(owner);
                yield return null;
                check(Mathf.Approximately(origin.CameraYOffset, 1.52f)
                    && origin.RequestedTrackingOriginMode == XROrigin.TrackingOriginMode.NotSpecified
                    && hand.parent == newHandParent && eye.transform.parent == floor.transform,
                    "height/cleanup-preserves-other-room-changes-" + initialHeight);
                Object.Destroy(root);
                yield return null;
            }
        }
    }
}
