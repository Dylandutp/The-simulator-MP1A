using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace HouseSim.Kitchen
{
    [RequireComponent(typeof(XRSimpleInteractable))]
    public sealed class KitchenToggle : MonoBehaviour
    {
        public bool Value;
        public Transform pivot;
        public Vector3 localAxis = Vector3.right;
        public float closedAngle;
        public float openAngle = 65f;
        public TextMesh label;
        public string labelPrefix = "WATER";
        public GameObject activeVisual;
        public UnityEvent<bool> changed = new UnityEvent<bool>();
        public float OpenAmount => Mathf.InverseLerp(closedAngle, openAngle, currentAngle);
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
        private bool initialized;

        private void Awake()
        {
            if (!pivot) pivot = transform;
            baseRotation = pivot.localRotation;
            currentAngle = Value ? openAngle : closedAngle;
            initialized = true;
            interactable = GetComponent<XRSimpleInteractable>();
            interactable.selectEntered.AddListener(OnGrab);
            interactable.selectExited.AddListener(OnRelease);
            Refresh();
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
            if (hand != null && IsDragging) DragTo(hand.transform.position);
        }

        public void BeginDrag(Vector3 worldPosition)
        {
            IsDragging = true;
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
            bool next = Value ? OpenAmount > 0.035f : OpenAmount >= 0.08f;
            bool didChange = next != Value;
            Value = next;
            Refresh();
            if (didChange) changed.Invoke(Value);
        }

        public void EndDrag()
        {
            hand = null;
            IsDragging = false;
        }

        public void Toggle() => SetValue(!Value);

        public void SetValue(bool value)
        {
            bool didChange = value != Value;
            Value = value;
            currentAngle = value ? openAngle : closedAngle;
            if (hand != null) lastHandVector = Vector3.ProjectOnPlane(hand.transform.position - Pivot.position, WorldAxis);
            Refresh();
            if (didChange) changed.Invoke(Value);
        }

        private void Refresh()
        {
            if (initialized && pivot) pivot.localRotation = baseRotation * Quaternion.AngleAxis(currentAngle, localAxis.normalized);
            if (activeVisual) activeVisual.SetActive(Value);
            if (label) label.text = labelPrefix + "\n" + (Value ? "ON" : "OFF");
        }
    }
}
