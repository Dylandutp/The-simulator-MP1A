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
        [Tooltip("Axis pointing out of the dial toward its user. Positive rotation increases the value clockwise.")]
        public Vector3 localAxis = Vector3.back;
        public float minValue;
        public float maxValue = 60f;
        [Tooltip("Legacy authoring field. The physical dial is continuous and does not snap to steps.")]
        public float step;
        public float angleRange = 270f;
        public TextMesh label;
        public string labelPrefix = "TIMER";
        public string unit = " s";
        public UnityEvent<float> valueChanged = new UnityEvent<float>();
        public float Value => currentValue;
        public float NormalizedValue => Mathf.InverseLerp(minValue, maxValue, currentValue);
        public float Angle => NormalizedValue * angleRange;
        public Transform Pivot => knobVisual ? knobVisual : transform;
        public Vector3 WorldAxis => Pivot.parent
            ? Pivot.parent.TransformDirection(baseRotation * localAxis.normalized)
            : baseRotation * localAxis.normalized;
        public bool IsDragging { get; private set; }
        public event System.Action<bool> DragStateChanged;
        [SerializeField] private float currentValue;

        private XRSimpleInteractable interactable;
        private IXRSelectInteractor hand;
        private Quaternion baseRotation;
        private Vector3 previousVector;
        private Quaternion previousHandRotation;
        private bool initialized;
        private KitchenAudioEmitter audioEmitter;

        private void Awake()
        {
            if (!knobVisual) knobVisual = transform;
            baseRotation = knobVisual.localRotation;
            initialized = true;
            interactable = GetComponent<XRSimpleInteractable>();
            interactable.selectEntered.AddListener(OnGrab);
            interactable.selectExited.AddListener(OnRelease);
            currentValue = Mathf.Clamp(currentValue, minValue, maxValue);
            Refresh();
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
            previousHandRotation = hand.transform.rotation;
        }

        private void OnRelease(SelectExitEventArgs args)
        {
            if (args.interactorObject == hand) EndDrag();
        }

        private void Update()
        {
            if (hand == null || !IsDragging) return;
            Vector3 axis = WorldAxis.normalized;
            Vector3 nextVector = Vector3.ProjectOnPlane(hand.transform.position - Pivot.position, axis);
            Quaternion nextRotation = hand.transform.rotation;
            float positionDelta = 0f;
            if (previousVector.sqrMagnitude > 0.0009f && nextVector.sqrMagnitude > 0.0009f)
                positionDelta = SmallGestureDelta(Vector3.SignedAngle(previousVector, nextVector, axis));

            // Extract the incremental wrist twist about the dial axis. Unlike projected
            // controller-up vectors, this stays stable when a controller points along the axis.
            Quaternion rotationDelta = nextRotation * Quaternion.Inverse(previousHandRotation);
            float twistComponent = Vector3.Dot(new Vector3(rotationDelta.x, rotationDelta.y, rotationDelta.z), axis);
            float wristDelta = 0f;
            if (twistComponent * twistComponent + rotationDelta.w * rotationDelta.w > 0.000001f)
                wristDelta = SmallGestureDelta(Mathf.DeltaAngle(0f,
                    2f * Mathf.Atan2(twistComponent, rotationDelta.w) * Mathf.Rad2Deg));

            // Either a wrist twist or an arc around the dial works from any grip offset.
            // Taking one gesture avoids counting the same movement twice.
            DragByDegrees(Mathf.Abs(wristDelta) >= Mathf.Abs(positionDelta) ? wristDelta : positionDelta);
            previousVector = nextVector;
            previousHandRotation = nextRotation;
        }

        private static float SmallGestureDelta(float degrees)
        {
            // Discard tracking discontinuities, but always advance the sampled pose above.
            return Mathf.Abs(degrees) <= 60f ? degrees : 0f;
        }

        public void BeginDrag(Vector3 worldPosition)
        {
            if (!isActiveAndEnabled || IsDragging) return;
            IsDragging = true;
            previousVector = Vector3.ProjectOnPlane(worldPosition - Pivot.position, WorldAxis);
            // Pause the timer before starting the winding loop, so the two recordings
            // never overlap even for a stationary press or an immediate release.
            DragStateChanged?.Invoke(true);
            if (!audioEmitter) audioEmitter = KitchenAudioEmitter.Get(this);
            if (GetComponentInParent<KitchenOven>()) audioEmitter.SetLoop(KitchenSound.OvenSpin, true);
            else if (GetComponentInParent<KitchenHob>()) audioEmitter.PlayCue(KitchenSound.StoveTurn);
        }

        public void DragTo(Vector3 worldPosition)
        {
            if (IsDragging) DragVector(Vector3.ProjectOnPlane(worldPosition - Pivot.position, WorldAxis));
        }

        private void DragVector(Vector3 next)
        {
            if (next.sqrMagnitude > 0.0001f && previousVector.sqrMagnitude > 0.0001f)
                DragByDegrees(Vector3.SignedAngle(previousVector, next, WorldAxis));
            previousVector = next;
        }

        public void DragByDegrees(float clockwiseDegrees)
        {
            if (Mathf.Abs(angleRange) < 0.001f) return;
            // Audio belongs to the press/release gesture, not to individual motion
            // samples. Automatic SetValue updates also remain silent.
            SetValue(currentValue + clockwiseDegrees / angleRange * (maxValue - minValue));
        }

        public void EndDrag()
        {
            hand = null;
            bool wasDragging = IsDragging;
            IsDragging = false;
            if (audioEmitter) audioEmitter.SetLoop(KitchenSound.OvenSpin, false);
            if (wasDragging) DragStateChanged?.Invoke(false);
        }

        public void Adjust(float delta) => SetValue(currentValue + delta);

        public void SetValue(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) return;
            float next = Mathf.Clamp(value, minValue, maxValue);
            if (next == currentValue) { Refresh(); return; }
            currentValue = next;
            Refresh();
            valueChanged.Invoke(currentValue);
        }

        private void Refresh()
        {
            if (initialized && knobVisual) knobVisual.localRotation = baseRotation * Quaternion.AngleAxis(Angle, localAxis.normalized);
            if (label) label.text = labelPrefix + "\n" + currentValue.ToString("0.#") + unit;
        }
    }
}
