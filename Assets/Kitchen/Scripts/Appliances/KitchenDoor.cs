using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace HouseSim.Kitchen
{
    [RequireComponent(typeof(XRSimpleInteractable))]
    public sealed class KitchenDoor : MonoBehaviour
    {
        public Transform pivot;
        public Vector3 localAxis = Vector3.up;
        public float closedAngle;
        public float openAngle = 105f;
        public TextMesh label;
        public string labelPrefix = "DOOR";
        public UnityEvent<bool> stateChanged = new UnityEvent<bool>();
        public float OpenAmount => Mathf.InverseLerp(closedAngle, openAngle, currentAngle);
        public bool IsOpen { get; private set; }
        public float Angle => currentAngle;
        public Transform Pivot => pivot ? pivot : transform;
        public Vector3 WorldAxis => Pivot.parent
            ? Pivot.parent.TransformDirection(baseRotation * localAxis.normalized)
            : baseRotation * localAxis.normalized;
        public bool IsDragging { get; private set; }

        private XRSimpleInteractable interactable;
        private IXRSelectInteractor hand;
        private Quaternion baseRotation;
        private Vector3 lastHandVector;
        private Transform controllerPose;
        private Vector3 lastControllerPosition;
        private Quaternion lastControllerRotation;
        private Vector3 controllerPullAxis;
        private Vector3 localGrabPoint;
        private bool useRayDrag;
        private float currentAngle;
        private float targetAngle;
        private bool movingToTarget;
        private bool audioReady;
        private KitchenAudioEmitter audioEmitter;

        private void Awake()
        {
            if (!pivot) pivot = transform;
            baseRotation = pivot.localRotation;
            currentAngle = targetAngle = closedAngle;
            interactable = GetComponent<XRSimpleInteractable>();
            interactable.selectEntered.AddListener(OnGrab);
            interactable.selectExited.AddListener(OnRelease);
            ApplyAngle();
            audioReady = true;
        }

        private void OnDestroy()
        {
            if (!interactable) return;
            interactable.selectEntered.RemoveListener(OnGrab);
            interactable.selectExited.RemoveListener(OnRelease);
        }

        private void OnDisable()
        {
            EndDrag();
            if (audioEmitter) audioEmitter.StopAll();
        }

        private void OnGrab(SelectEnterEventArgs args)
        {
            hand = args.interactorObject;
            BeginDrag(hand.transform.position);
            Vector3 handlePoint = ClosestHandlePoint(hand.transform.position);
            if (hand is IXRRayProvider ray)
            {
                Transform rayOrigin = ray.GetOrCreateRayOrigin();
                if (rayOrigin && Vector3.Distance(handlePoint, rayOrigin.position) > .35f)
                {
                    controllerPose = rayOrigin;
                    useRayDrag = true;
                    if (ray.rayEndTransform && ray.rayEndTransform.IsChildOf(Pivot))
                        handlePoint = ClosestHandlePoint(ray.rayEndPoint);
                }
            }
            if (!controllerPose) controllerPose = hand.transform;
            localGrabPoint = Pivot.InverseTransformPoint(handlePoint);
            ReanchorControllerDrag();
        }

        private void OnRelease(SelectExitEventArgs args)
        {
            if (args.interactorObject == hand) EndDrag();
        }

        private void Update()
        {
            if (hand != null && IsDragging && !movingToTarget) UpdateControllerDrag();
            else if (movingToTarget)
            {
                currentAngle = Mathf.MoveTowards(currentAngle, targetAngle, 150f * Time.deltaTime);
                if (Mathf.Approximately(currentAngle, targetAngle) && movingToTarget)
                {
                    movingToTarget = false;
                    if (hand != null) ReanchorControllerDrag();
                }
            }
            ApplyAngle();
        }

        public void BeginDrag(Vector3 worldPosition)
        {
            IsDragging = true;
            movingToTarget = false;
            targetAngle = currentAngle;
            lastHandVector = Vector3.ProjectOnPlane(worldPosition - Pivot.position, WorldAxis);
        }

        public void DragTo(Vector3 worldPosition)
        {
            if (!IsDragging) return;
            Vector3 next = Vector3.ProjectOnPlane(worldPosition - Pivot.position, WorldAxis);
            if (lastHandVector.sqrMagnitude > 0.0001f && next.sqrMagnitude > 0.0001f)
                DragByDegrees(Vector3.SignedAngle(lastHandVector, next, WorldAxis));
            lastHandVector = next;
        }

        public void DragByDegrees(float signedDegrees)
        {
            if (float.IsNaN(signedDegrees) || float.IsInfinity(signedDegrees)) return;
            currentAngle = Mathf.Clamp(currentAngle + signedDegrees, Mathf.Min(closedAngle, openAngle), Mathf.Max(closedAngle, openAngle));
            targetAngle = currentAngle;
            movingToTarget = false;
            ApplyAngle();
        }

        public void EndDrag()
        {
            hand = null;
            controllerPose = null;
            useRayDrag = false;
            IsDragging = false;
            // Leave the door exactly where it was released; neither grip nor release toggles it.
        }

        public void Toggle() => SetOpen(!IsOpen);

        public void SetOpen(bool open)
        {
            targetAngle = open ? openAngle : closedAngle;
            movingToTarget = true;
        }

        public void SetOpenImmediately(bool open)
        {
            currentAngle = targetAngle = open ? openAngle : closedAngle;
            movingToTarget = false;
            ApplyAngle();
            if (hand != null) ReanchorControllerDrag();
        }

        private Vector3 ClosestHandlePoint(Vector3 from)
        {
            Vector3 closest = Pivot.position;
            float distance = float.PositiveInfinity;
            foreach (Collider handle in interactable.colliders)
            {
                if (!handle || !handle.enabled || !handle.gameObject.activeInHierarchy) continue;
                Vector3 point = handle.ClosestPoint(from);
                float next = (point - from).sqrMagnitude;
                if (next < distance) { closest = point; distance = next; }
            }
            return closest;
        }

        private void ReanchorControllerDrag()
        {
            if (!controllerPose) return;
            lastControllerPosition = controllerPose.position;
            lastControllerRotation = controllerPose.rotation;
            Vector3 radius = Vector3.ProjectOnPlane(Pivot.TransformPoint(localGrabPoint) - Pivot.position, WorldAxis);
            controllerPullAxis = Vector3.Cross(WorldAxis, radius).normalized;
        }

        private void UpdateControllerDrag()
        {
            if (!controllerPose) return;
            Vector3 position = controllerPose.position;
            Quaternion rotation = controllerPose.rotation;
            Vector3 travel = position - lastControllerPosition;
            Quaternion rotationDelta = rotation * Quaternion.Inverse(lastControllerRotation);
            // A lost tracking pose must not slam the door. Rebase the next sample instead.
            if (travel.sqrMagnitude <= .25f && Quaternion.Angle(rotation, lastControllerRotation) <= 60f)
            {
                // Use the handle's opening direction, not the distant controller-to-hinge
                // angle. One comfortable 40 cm pull can traverse either door's full range.
                float pullDelta = Vector3.Dot(travel, controllerPullAxis) * Mathf.Abs(openAngle - closedAngle) / .4f;
                float rayDelta = 0f;
                if (useRayDrag && (travel.sqrMagnitude > .00000001f || Quaternion.Angle(rotation, lastControllerRotation) > .005f))
                {
                    Vector3 handlePoint = Pivot.TransformPoint(localGrabPoint);
                    Vector3 targetDirection = rotationDelta * (handlePoint - lastControllerPosition).normalized;
                    rayDelta = FindRayHingeDelta(position, targetDirection, handlePoint - Pivot.position);
                }
                // Ray pointing and pulling can describe the same gesture. Do not add both.
                DragByDegrees(Mathf.Abs(rayDelta) > Mathf.Abs(pullDelta) ? rayDelta : pullDelta);
            }
            lastControllerPosition = position;
            lastControllerRotation = rotation;
        }

        private float FindRayHingeDelta(Vector3 origin, Vector3 direction, Vector3 radius)
        {
            float low = Mathf.Max(Mathf.Min(closedAngle, openAngle) - currentAngle, -25f);
            float high = Mathf.Min(Mathf.Max(closedAngle, openAngle) - currentAngle, 25f);
            float best = 0f;
            float error = RayHingeError(0f, origin, direction, radius);
            float spacing = (high - low) / 24f;
            if (spacing < .00001f) return 0f;
            for (int i = 0; i <= 24; i++)
            {
                float candidate = low + spacing * i;
                float next = RayHingeError(candidate, origin, direction, radius);
                if (next < error) { error = next; best = candidate; }
            }
            float a = Mathf.Max(low, best - spacing);
            float b = Mathf.Min(high, best + spacing);
            for (int i = 0; i < 8; i++)
            {
                float left = Mathf.Lerp(a, b, 1f / 3f);
                float right = Mathf.Lerp(a, b, 2f / 3f);
                if (RayHingeError(left, origin, direction, radius) < RayHingeError(right, origin, direction, radius)) b = right;
                else a = left;
            }
            float refined = (a + b) * .5f;
            return RayHingeError(refined, origin, direction, radius) < error ? refined : best;
        }

        private float RayHingeError(float degrees, Vector3 origin, Vector3 direction, Vector3 radius)
        {
            Vector3 candidate = Pivot.position + Quaternion.AngleAxis(degrees, WorldAxis) * radius - origin;
            if (Vector3.Dot(candidate, direction) <= 0f) return float.PositiveInfinity;
            return (candidate.normalized - direction).sqrMagnitude + degrees * degrees * .000000001f;
        }

        private void ApplyAngle()
        {
            pivot.localRotation = baseRotation * Quaternion.AngleAxis(currentAngle, localAxis.normalized);
            bool nextOpen = IsOpen ? OpenAmount > 0.035f : OpenAmount >= 0.08f;
            if (nextOpen != IsOpen)
            {
                IsOpen = nextOpen;
                // Hysteresis requires a deliberate opening and an almost fully closed
                // door. Holding or wiggling an already open door does not repeat cues.
                if (audioReady && isActiveAndEnabled)
                {
                    if (!audioEmitter) audioEmitter = KitchenAudioEmitter.Get(this);
                    audioEmitter.PlayCue(IsOpen ? KitchenSound.DoorOpen : KitchenSound.DoorClose);
                }
                stateChanged.Invoke(IsOpen);
            }
            if (label) label.text = labelPrefix + "\n" + (IsOpen ? "OPEN" : "CLOSED");
        }
    }
}
