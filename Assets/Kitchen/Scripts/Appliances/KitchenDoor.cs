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
        private float currentAngle;
        private float targetAngle;
        private bool movingToTarget;

        private void Awake()
        {
            if (!pivot) pivot = transform;
            baseRotation = pivot.localRotation;
            currentAngle = targetAngle = closedAngle;
            interactable = GetComponent<XRSimpleInteractable>();
            interactable.selectEntered.AddListener(OnGrab);
            interactable.selectExited.AddListener(OnRelease);
            ApplyAngle();
        }

        private void OnDestroy()
        {
            if (!interactable) return;
            interactable.selectEntered.RemoveListener(OnGrab);
            interactable.selectExited.RemoveListener(OnRelease);
        }

        private void OnDisable() => EndDrag();

        private void OnGrab(SelectEnterEventArgs args)
        {
            hand = args.interactorObject;
            BeginDrag(hand.transform.position);
        }

        private void OnRelease(SelectExitEventArgs args)
        {
            if (args.interactorObject == hand) EndDrag();
        }

        private void Update()
        {
            if (hand != null && IsDragging && !movingToTarget) DragTo(hand.transform.position);
            else if (movingToTarget)
            {
                currentAngle = Mathf.MoveTowards(currentAngle, targetAngle, 150f * Time.deltaTime);
                if (Mathf.Approximately(currentAngle, targetAngle) && movingToTarget)
                {
                    movingToTarget = false;
                    if (hand != null) lastHandVector = Vector3.ProjectOnPlane(hand.transform.position - Pivot.position, WorldAxis);
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
            if (hand != null) lastHandVector = Vector3.ProjectOnPlane(hand.transform.position - Pivot.position, WorldAxis);
            ApplyAngle();
        }

        private void ApplyAngle()
        {
            pivot.localRotation = baseRotation * Quaternion.AngleAxis(currentAngle, localAxis.normalized);
            bool nextOpen = IsOpen ? OpenAmount > 0.035f : OpenAmount >= 0.08f;
            if (nextOpen != IsOpen)
            {
                IsOpen = nextOpen;
                stateChanged.Invoke(IsOpen);
            }
            if (label) label.text = labelPrefix + "\n" + (IsOpen ? "OPEN" : "CLOSED");
        }
    }
}
