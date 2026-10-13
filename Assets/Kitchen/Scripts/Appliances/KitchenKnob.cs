using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace HouseSim.Kitchen
{
    [RequireComponent(typeof(XRSimpleInteractable))]
    public sealed class KitchenKnob : MonoBehaviour
    {
        public Transform knobVisual;
        public Vector3 localAxis = Vector3.forward;
        public float minValue;
        public float maxValue = 60f;
        public float step = 5f;
        public float angleRange = 270f;
        public TextMesh label;
        public string labelPrefix = "TIMER";
        public string unit = " s";
        public UnityEvent<float> valueChanged = new UnityEvent<float>();
        public float Value => currentValue;
        [SerializeField] private float currentValue;

        private XRSimpleInteractable interactable;
        private IXRSelectInteractor hand;
        private Quaternion baseRotation;
        private Vector3 previousVector;
        private bool positionGesture;
        private bool initialized;
        private float rawAngle;

        private void Awake()
        {
            if (!knobVisual) knobVisual = transform;
            baseRotation = knobVisual.localRotation;
            initialized = true;
            interactable = GetComponent<XRSimpleInteractable>();
            interactable.selectEntered.AddListener(OnGrab);
            interactable.selectExited.AddListener(OnRelease);
            interactable.activated.AddListener(OnActivated);
            currentValue = Mathf.Clamp(currentValue, minValue, maxValue);
            Refresh();
        }

        private void OnDestroy()
        {
            if (!interactable) return;
            interactable.selectEntered.RemoveListener(OnGrab);
            interactable.selectExited.RemoveListener(OnRelease);
            interactable.activated.RemoveListener(OnActivated);
        }

        private Vector3 WorldAxis => knobVisual.parent
            ? knobVisual.parent.TransformDirection(baseRotation * localAxis.normalized)
            : baseRotation * localAxis.normalized;

        private Vector3 GestureVector()
        {
            return Vector3.ProjectOnPlane(positionGesture
                ? hand.transform.position - knobVisual.position
                : hand.transform.up, WorldAxis);
        }

        private void OnGrab(SelectEnterEventArgs args)
        {
            hand = args.interactorObject;
            positionGesture = Vector3.ProjectOnPlane(hand.transform.position - knobVisual.position, WorldAxis).sqrMagnitude > 0.0009f;
            previousVector = GestureVector();
            rawAngle = Mathf.InverseLerp(minValue, maxValue, currentValue) * angleRange;
        }

        private void OnRelease(SelectExitEventArgs args) => hand = null;

        private void OnActivated(ActivateEventArgs args)
        {
            float next = currentValue + (step > 0f ? step : 1f);
            SetValue(next > maxValue + 0.001f ? minValue : next);
            rawAngle = Mathf.InverseLerp(minValue, maxValue, currentValue) * angleRange;
            if (hand != null) previousVector = GestureVector();
        }

        private void Update()
        {
            if (hand == null) return;
            Vector3 next = GestureVector();
            if (next.sqrMagnitude > 0.0001f && previousVector.sqrMagnitude > 0.0001f)
            {
                rawAngle = Mathf.Clamp(rawAngle + Vector3.SignedAngle(previousVector, next, WorldAxis), 0f, Mathf.Max(1f, angleRange));
                SetValue(Mathf.Lerp(minValue, maxValue, rawAngle / Mathf.Max(1f, angleRange)));
            }
            previousVector = next;
        }

        public void Adjust(float delta) => SetValue(currentValue + delta);

        public void SetValue(float value)
        {
            float next = Mathf.Clamp(value, minValue, maxValue);
            if (step > 0f) next = Mathf.Clamp(minValue + Mathf.Round((next - minValue) / step) * step, minValue, maxValue);
            if (Mathf.Approximately(next, currentValue)) { Refresh(); return; }
            currentValue = next;
            Refresh();
            valueChanged.Invoke(currentValue);
        }

        private void Refresh()
        {
            if (initialized && knobVisual) knobVisual.localRotation = baseRotation * Quaternion.AngleAxis(Mathf.InverseLerp(minValue, maxValue, currentValue) * angleRange, localAxis.normalized);
            if (label) label.text = labelPrefix + "\n" + currentValue.ToString("0.#") + unit;
        }
    }
}
