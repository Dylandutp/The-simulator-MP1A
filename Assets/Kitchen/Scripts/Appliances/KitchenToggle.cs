using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace HouseSim.Kitchen
{
    [RequireComponent(typeof(XRSimpleInteractable))]
    public sealed class KitchenToggle : MonoBehaviour
    {
        public bool Value;
        public TextMesh label;
        public string labelPrefix = "WATER";
        public GameObject activeVisual;
        public UnityEvent<bool> changed = new UnityEvent<bool>();
        private XRSimpleInteractable interactable;

        private void Awake()
        {
            interactable = GetComponent<XRSimpleInteractable>();
            interactable.selectEntered.AddListener(OnPressed);
            Refresh();
        }

        private void OnDestroy()
        {
            if (interactable) interactable.selectEntered.RemoveListener(OnPressed);
        }

        private void OnPressed(SelectEnterEventArgs args) => Toggle();
        public void Toggle() => SetValue(!Value);

        public void SetValue(bool value)
        {
            if (value == Value) { Refresh(); return; }
            Value = value;
            Refresh();
            changed.Invoke(Value);
        }

        private void Refresh()
        {
            if (activeVisual) activeVisual.SetActive(Value);
            if (label) label.text = labelPrefix + "\n" + (Value ? "ON" : "OFF");
        }
    }
}
