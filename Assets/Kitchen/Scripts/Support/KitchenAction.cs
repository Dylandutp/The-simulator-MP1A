using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace HouseSim.Kitchen
{
    /// <summary>A physical button shared by XR selection and the desktop E key.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(XRSimpleInteractable))]
    public sealed class KitchenAction : MonoBehaviour
    {
        public string label = "Use";
        public UnityEvent action = new UnityEvent();
        [Min(0f)] public float debounceSeconds = 0.15f;
        XRSimpleInteractable interactable;
        float lastInvocation = -100f;

        void Awake()
        {
            interactable = GetComponent<XRSimpleInteractable>();
            if (interactable != null)
                interactable.selectEntered.AddListener(OnSelected);
        }

        void OnDestroy()
        {
            if (interactable != null)
                interactable.selectEntered.RemoveListener(OnSelected);
        }

        void OnSelected(SelectEnterEventArgs args) => Invoke();

        public void Invoke()
        {
            if (!isActiveAndEnabled || Time.unscaledTime - lastInvocation < debounceSeconds)
                return;
            lastInvocation = Time.unscaledTime;
            action.Invoke();
        }
    }
}
