using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Transformers;

namespace HouseSim.Kitchen
{
    /// <summary>Seat the handle in one hand with the pan open upward, then allow natural wrist movement.</summary>
    [RequireComponent(typeof(KitchenPan), typeof(XRGrabInteractable))]
    public sealed class KitchenPanGrip : XRBaseGrabTransformer
    {
        [Tooltip("Fallback grip center in pan-root local units; the Pan handle mesh overrides it when present.")]
        public Vector3 handleLocalCenter = new Vector3(0f, .025f, -.4f);
        private Vector3 resolvedHandleCenter;
        private IXRSelectInteractor gripInteractor;
        private Quaternion gripRotationOffset;
        private Rigidbody body;
        public Vector3 HandleCenter => transform.TransformPoint(resolvedHandleCenter);

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            resolvedHandleCenter = handleLocalCenter;
            Transform handle = transform.Find("Pan handle");
            var mesh = handle ? handle.GetComponent<MeshFilter>() : null;
            if (mesh && mesh.sharedMesh)
                resolvedHandleCenter = transform.InverseTransformPoint(handle.TransformPoint(mesh.sharedMesh.bounds.center));
        }

        public Quaternion UprightGripRotation(Vector3 forward)
        {
            Vector3 level = Vector3.ProjectOnPlane(forward, Vector3.up);
            if (level.sqrMagnitude < .001f && Camera.main)
                level = Vector3.ProjectOnPlane(Camera.main.transform.forward, Vector3.up);
            if (level.sqrMagnitude < .001f) level = Vector3.forward;
            // Pan opening is local +Y; its handle extends toward local -Z (the player).
            return Quaternion.LookRotation(level.normalized, Vector3.up);
        }

        public void PrepareDesktopGrip(Transform view)
        {
            Quaternion rotation = UprightGripRotation(view ? view.forward : Vector3.forward);
            transform.rotation = rotation;
            if (body) body.rotation = rotation;
        }

        public override void OnGrab(XRGrabInteractable grabInteractable)
        {
            base.OnGrab(grabInteractable);
            gripInteractor = grabInteractable.interactorsSelecting[0];
            Transform hand = gripInteractor.GetAttachTransform(grabInteractable);
            gripRotationOffset = Quaternion.Inverse(hand.rotation) * UprightGripRotation(hand.forward);
            GetComponent<KitchenPan>().DeferGripAlignmentGestures(Mathf.Max(.35f, grabInteractable.attachEaseInTime + .65f));
        }

        public override void OnGrabCountChanged(XRGrabInteractable grabInteractable, Pose targetPose, Vector3 localScale)
        {
            if (grabInteractable.interactorsSelecting.Count > 0 && gripInteractor != grabInteractable.interactorsSelecting[0])
                OnGrab(grabInteractable);
        }

        public override void Process(XRGrabInteractable grabInteractable, XRInteractionUpdateOrder.UpdatePhase updatePhase,
            ref Pose targetPose, ref Vector3 localScale)
        {
            if (gripInteractor == null || grabInteractable.interactorsSelecting.Count == 0) return;
            Transform hand = gripInteractor.GetAttachTransform(grabInteractable);
            // Only pickup levels the pan. Later wrist roll/pitch is preserved for tossing and tipping.
            targetPose.rotation = hand.rotation * gripRotationOffset;
            targetPose.position = hand.position - targetPose.rotation * Vector3.Scale(resolvedHandleCenter, localScale);
        }
    }
}
