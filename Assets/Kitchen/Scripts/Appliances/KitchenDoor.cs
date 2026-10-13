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
            interactable.activated.AddListener(OnActivated);
            ApplyAngle();
        }

        private void OnDestroy()
        {
            if (!interactable) return;
            interactable.selectEntered.RemoveListener(OnGrab);
            interactable.selectExited.RemoveListener(OnRelease);
            interactable.activated.RemoveListener(OnActivated);
        }

        private Vector3 WorldAxis => pivot.parent
            ? pivot.parent.TransformDirection(baseRotation * localAxis.normalized)
            : baseRotation * localAxis.normalized;

        private Vector3 HandVector()
        {
            return Vector3.ProjectOnPlane(hand.transform.position - pivot.position, WorldAxis);
        }

        private void OnGrab(SelectEnterEventArgs args)
        {
            hand = args.interactorObject;
            movingToTarget = false;
            targetAngle = currentAngle;
            lastHandVector = HandVector();
        }

        private void OnRelease(SelectExitEventArgs args)
        {
            hand = null;
            if (movingToTarget) return;
            targetAngle = currentAngle;
            // The final few degrees latch closed, without pulling an open door shut.
            if (OpenAmount < 0.07f) targetAngle = closedAngle;
        }

        private void Update()
        {
            if (hand != null && !movingToTarget)
            {
                Vector3 next = HandVector();
                if (lastHandVector.sqrMagnitude > 0.0001f && next.sqrMagnitude > 0.0001f)
                    currentAngle += Vector3.SignedAngle(lastHandVector, next, WorldAxis);
                currentAngle = Mathf.Clamp(currentAngle, Mathf.Min(closedAngle, openAngle), Mathf.Max(closedAngle, openAngle));
                targetAngle = currentAngle;
                lastHandVector = next;
            }
            else
            {
                currentAngle = Mathf.MoveTowards(currentAngle, targetAngle, 150f * Time.deltaTime);
                if (Mathf.Approximately(currentAngle, targetAngle) && movingToTarget)
                {
                    movingToTarget = false;
                    if (hand != null) lastHandVector = HandVector();
                }
            }
            ApplyAngle();
        }

        public void Toggle() => SetOpen(!IsOpen);
        private void OnActivated(ActivateEventArgs args) => Toggle();

        public void SetOpen(bool open)
        {
            targetAngle = open ? openAngle : closedAngle;
            movingToTarget = true;
        }

        public void SetOpenImmediately(bool open)
        {
            currentAngle = targetAngle = open ? openAngle : closedAngle;
            movingToTarget = false;
            if (hand != null) lastHandVector = HandVector();
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
