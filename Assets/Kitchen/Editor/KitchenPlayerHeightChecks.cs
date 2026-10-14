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
                Object.Destroy(owner);
                yield return null;
                check(eye.transform.parent == floor.transform && hand.parent == floor.transform,
                    "height/unload-restores-shared-hierarchy-" + initialHeight);
                Object.Destroy(root);
                yield return null;
            }
        }
    }
}
