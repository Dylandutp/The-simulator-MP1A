using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Transformers;

namespace HouseSim.Kitchen
{
    /// <summary>Blade-only cutting: one deliberate down stroke, followed by a lift to rearm.</summary>
    [DefaultExecutionOrder(150)]
    [RequireComponent(typeof(XRGrabInteractable))]
    public class KitchenKnife : XRBaseGrabTransformer
    {
        [Min(0.01f)] public float minimumSpeed = 0.18f;
        [Min(0.05f)] public float chopCooldown = 0.35f;
        [Min(0.02f)] public float minimumDownwardTravel = 0.04f;
        [Min(0.02f)] public float rearmLift = 0.05f;
        [Tooltip("Use a defined blade-only cutting region for a mesh that contains both blade and handle.")]
        public bool useExplicitBladeRegion;
        [Tooltip("Local space of bladeLocalBounds. Keep its axes aligned with the knife root: length +X, edge -Y. Null uses the root.")]
        public Transform bladeReference;
        [Tooltip("Cutting region in bladeReference local units; exclude the handle and decorative geometry.")]
        public Bounds bladeLocalBounds = new Bounds(new Vector3(.025f, 0f, 0f), new Vector3(.22f, .065f, .009f));
        [Tooltip("Center of the grip in knife-root local units, independent of the visual mesh transform.")]
        public Vector3 handleLocalCenter = new Vector3(-.16f, 0f, 0f);
        private XRGrabInteractable grab;
        private KitchenAudioEmitter audioEmitter;
        private Rigidbody body;
        private Transform blade;
        private Bounds bladeBounds;
        private Vector3 previousBladeCenter;
        private Vector3 previousEdge;
        private float strokeTopY;
        private float cutHeight;
        private float nextChopAt;
        private bool wasHeld;
        private bool armed = true;
        private readonly Collider[] overlaps = new Collider[48];
        private readonly RaycastHit[] sweeps = new RaycastHit[48];
        private float desktopStartedAt = -1f;
        private Vector3 desktopStartPosition;
        private Quaternion desktopStartRotation;
        private Vector3 desktopRaisedPosition;
        private Vector3 desktopCutPosition;
        private Quaternion desktopCutRotation;
        private IXRSelectInteractor gripInteractor;
        private Quaternion gripRotationOffset;
        private Vector3 resolvedHandleCenter = new Vector3(-.16f, 0f, 0f);
        private float cutResumeAt;

        public bool IsDesktopCutting => desktopStartedAt >= 0f;
        public bool StrokeArmed => armed;
        public Vector3 BladeCenter => blade.TransformPoint(bladeBounds.center);
        public Vector3 BladeEdge => BladeCenter - Vector3.up * BladeHalfHeight;
        public Vector3 HandleCenter => transform.TransformPoint(resolvedHandleCenter);
        public Vector3 BladeDownDirection => -blade.up;
        private float BladeHalfHeight => Mathf.Abs(blade.up.y) * HalfExtents.y + Mathf.Abs(blade.right.y) * HalfExtents.x + Mathf.Abs(blade.forward.y) * HalfExtents.z;
        private Vector3 HalfExtents => Vector3.Scale(bladeBounds.extents, Abs(blade.lossyScale));
        private bool IsHeld => (grab != null && grab.isSelected) || KitchenDesktopHeld.IsHeldByDesktop(gameObject);

        private void Awake()
        {
            grab = GetComponent<XRGrabInteractable>();
            audioEmitter = KitchenAudioEmitter.Get(this);
            body = GetComponent<Rigidbody>();
            if (!GetComponent<KitchenContactStabilizer>()) gameObject.AddComponent<KitchenContactStabilizer>();
            ResolveCuttingGeometry();
            ResetStroke();
        }

        private void ResolveCuttingGeometry()
        {
            if (useExplicitBladeRegion)
            {
                blade = bladeReference ? bladeReference : transform;
                bladeBounds = bladeLocalBounds;
                resolvedHandleCenter = handleLocalCenter;
                return;
            }

            // Existing primitive knives keep their original name-based bounds and grip.
            var meshes = GetComponentsInChildren<MeshFilter>();
            foreach (var mesh in meshes)
                if (mesh.name.ToLowerInvariant().Contains("handle") && mesh.sharedMesh)
                {
                    resolvedHandleCenter = transform.InverseTransformPoint(mesh.transform.TransformPoint(mesh.sharedMesh.bounds.center));
                    break;
                }
            foreach (var mesh in meshes)
            {
                if (!mesh.name.ToLowerInvariant().Contains("blade") || mesh.sharedMesh == null) continue;
                blade = mesh.transform;
                bladeBounds = mesh.sharedMesh.bounds;
                break;
            }
            if (blade == null)
            {
                blade = transform;
                bladeBounds = new Bounds(new Vector3(0.025f, 0f, 0f), new Vector3(0.22f, 0.065f, 0.009f));
            }
        }

        /// <summary>At pickup the handle points into the palm, the blade points ahead, and its edge faces down.</summary>
        public Quaternion UprightGripRotation(Vector3 forward)
        {
            Vector3 levelForward = Vector3.ProjectOnPlane(forward, Vector3.up);
            if (levelForward.sqrMagnitude < .001f && Camera.main)
                levelForward = Vector3.ProjectOnPlane(Camera.main.transform.forward, Vector3.up);
            if (levelForward.sqrMagnitude < .001f) levelForward = Vector3.forward;
            // This knife's length is local +X, its cutting edge local -Y.
            return Quaternion.LookRotation(levelForward.normalized, Vector3.up) * Quaternion.Euler(0f, -90f, 0f);
        }

        public void PrepareDesktopGrip(Transform view)
        {
            Quaternion rotation = UprightGripRotation(view ? view.forward : Vector3.forward);
            transform.rotation = rotation;
            if (body) body.rotation = rotation;
            BeginGrip();
        }

        public override void OnGrab(XRGrabInteractable grabInteractable)
        {
            base.OnGrab(grabInteractable);
            gripInteractor = grabInteractable.interactorsSelecting[0];
            Transform hand = gripInteractor.GetAttachTransform(grabInteractable);
            gripRotationOffset = Quaternion.Inverse(hand.rotation) * UprightGripRotation(hand.forward);
            BeginGrip();
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
            // The offset is captured once. Wrist roll/pitch remains available for real cutting motions.
            targetPose.rotation = hand.rotation * gripRotationOffset;
            targetPose.position = hand.position - targetPose.rotation * Vector3.Scale(resolvedHandleCenter, localScale);
        }

        private void BeginGrip()
        {
            desktopStartedAt = -1f;
            // Moving from the worktop into a correctly oriented grip is not a cutting stroke.
            cutResumeAt = Time.time + Mathf.Max(.15f, grab.attachEaseInTime + .05f);
            ResetStroke();
        }

        private void ResetStroke()
        {
            previousBladeCenter = BladeCenter;
            previousEdge = BladeEdge;
            strokeTopY = previousEdge.y;
            armed = true;
        }

        private void LateUpdate()
        {
            if (!IsHeld)
            {
                desktopStartedAt = -1f;
                wasHeld = false;
                ResetStroke();
                return;
            }
            if (!wasHeld) { ResetStroke(); wasHeld = true; }
            if (Time.time < cutResumeAt) { ResetStroke(); return; }
            AnimateDesktopStroke();
            Vector3 center = BladeCenter;
            Vector3 edge = BladeEdge;
            Vector3 movement = edge - previousEdge;
            strokeTopY = Mathf.Max(strokeTopY, edge.y);
            if (!armed && edge.y - cutHeight >= rearmLift)
            {
                armed = true;
                strokeTopY = edge.y;
            }
            float downwardSpeed = -movement.y / Mathf.Max(Time.deltaTime, 0.001f);
            float sideways = new Vector2(movement.x, movement.z).magnitude;
            if (armed && Time.time >= nextChopAt && downwardSpeed >= minimumSpeed &&
                strokeTopY - edge.y >= minimumDownwardTravel && -movement.y >= sideways * 0.4f)
            {
                // Queries keep cutting responsive when knife/food physical collisions are suppressed.
                Vector3 extents = HalfExtents + Vector3.one * 0.002f;
                Vector3 travel = center - previousBladeCenter;
                int count = travel.sqrMagnitude > 0.000001f
                    ? Physics.BoxCastNonAlloc(previousBladeCenter, extents, travel.normalized, sweeps,
                        blade.rotation, travel.magnitude, ~0, QueryTriggerInteraction.Ignore) : 0;
                for (int i = 0; i < count && armed; i++) TryCut(sweeps[i].collider, edge);
                if (armed)
                {
                    count = Physics.OverlapBoxNonAlloc(center, extents, overlaps, blade.rotation, ~0, QueryTriggerInteraction.Ignore);
                    for (int i = 0; i < count && armed; i++) TryCut(overlaps[i], edge);
                }
            }
            previousBladeCenter = center;
            previousEdge = edge;
        }

        private void TryCut(Collider other, Vector3 edge)
        {
            if (other == null) return;
            var food = other.GetComponentInParent<KitchenFood>();
            if (food == null || food.IsConsumed || !food.IsProduce || food.CutStage >= 2) return;
            if (strokeTopY < other.bounds.max.y + 0.01f) return;
            if (!food.Chop()) return;
            armed = false;
            cutHeight = edge.y;
            nextChopAt = Time.time + chopCooldown;
            audioEmitter.PlayCue(KitchenSound.Cut);
        }

        /// <summary>PC C-key plays the same measured down-stroke used by a tracked hand.</summary>
        public bool TryDesktopChop(KitchenFood target)
        {
            if (IsDesktopCutting || !KitchenDesktopHeld.IsHeldByDesktop(gameObject) || target == null ||
                target.IsHeld || target.IsConsumed || !target.IsProduce || target.CutStage >= 2 ||
                Time.time < nextChopAt || Vector3.Distance(BladeCenter, target.transform.position) > 0.8f) return false;
            var targetCollider = target.GetComponent<Collider>();
            if (targetCollider == null) return false;
            desktopStartPosition = transform.position;
            desktopStartRotation = transform.rotation;
            desktopCutRotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
            Vector3 localCenterOffset = transform.InverseTransformPoint(BladeCenter);
            Quaternion leveledBlade = desktopCutRotation * Quaternion.Inverse(transform.rotation) * blade.rotation;
            float halfHeight = Mathf.Abs((leveledBlade * Vector3.up).y) * HalfExtents.y +
                Mathf.Abs((leveledBlade * Vector3.right).y) * HalfExtents.x + Mathf.Abs((leveledBlade * Vector3.forward).y) * HalfExtents.z;
            Vector3 rotatedEdgeOffset = desktopCutRotation * Vector3.Scale(localCenterOffset, transform.lossyScale) - Vector3.up * halfHeight;
            Vector3 surface = targetCollider.bounds.center;
            surface.y = targetCollider.bounds.max.y;
            desktopRaisedPosition = surface + Vector3.up * 0.13f - rotatedEdgeOffset;
            desktopCutPosition = surface + Vector3.down * Mathf.Min(0.07f, targetCollider.bounds.size.y * 0.55f) - rotatedEdgeOffset;
            desktopStartedAt = Time.time;
            return true;
        }

        private void AnimateDesktopStroke()
        {
            if (!IsDesktopCutting) return;
            float elapsed = Time.time - desktopStartedAt;
            if (elapsed < 0.18f)
            {
                float t = Mathf.SmoothStep(0f, 1f, elapsed / 0.18f);
                transform.SetPositionAndRotation(Vector3.Lerp(desktopStartPosition, desktopRaisedPosition, t),
                    Quaternion.Slerp(desktopStartRotation, desktopCutRotation, t));
            }
            else if (elapsed < 0.4f)
                transform.SetPositionAndRotation(Vector3.Lerp(desktopRaisedPosition, desktopCutPosition, (elapsed - 0.18f) / 0.22f), desktopCutRotation);
            else if (elapsed < 0.62f)
            {
                float t = Mathf.SmoothStep(0f, 1f, (elapsed - 0.4f) / 0.22f);
                transform.SetPositionAndRotation(Vector3.Lerp(desktopCutPosition, desktopStartPosition, t),
                    Quaternion.Slerp(desktopCutRotation, desktopStartRotation, t));
            }
            else { transform.SetPositionAndRotation(desktopStartPosition, desktopStartRotation); desktopStartedAt = -1f; }
            if (body != null && !body.isKinematic) { body.linearVelocity = Vector3.zero; body.angularVelocity = Vector3.zero; }
        }

        private static Vector3 Abs(Vector3 value) => new Vector3(Mathf.Abs(value.x), Mathf.Abs(value.y), Mathf.Abs(value.z));
    }
}
