using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace HouseSim.Kitchen
{
    // Preserves refrigerator stock without changing the XR grab interaction's movement mode.
    // A released item remains dynamic after XRGrab restores its original rigidbody settings.
    public sealed class KitchenStockItem : MonoBehaviour
    {
        public KitchenFood Food { get; private set; }
        public bool IsReleased { get; private set; }
        private Rigidbody body;
        private XRGrabInteractable grab;

        private void Awake()
        {
            grab = GetComponent<XRGrabInteractable>();
            if (!grab) return;
            grab.selectEntered.AddListener(OnTaken);
            grab.selectExited.AddListener(OnDropped);
        }

        private void OnDestroy()
        {
            if (!grab) return;
            grab.selectEntered.RemoveListener(OnTaken);
            grab.selectExited.RemoveListener(OnDropped);
        }

        private void OnTaken(SelectEnterEventArgs args) => Release();

        private void OnDropped(SelectExitEventArgs args)
        {
            if (IsReleased && body) body.isKinematic = false;
        }

        public void Stock(KitchenFood food)
        {
            Food = food;
            body = GetComponent<Rigidbody>();
            IsReleased = false;
            if (body)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.isKinematic = true;
            }
        }

        public void Release()
        {
            if (IsReleased) return;
            IsReleased = true;
            transform.SetParent(null, true);
            if (body && (!Food || !Food.IsHeld)) body.isKinematic = false;
        }

        private void LateUpdate()
        {
            if (Food && Food.IsHeld) Release();
            if (IsReleased && body && (!Food || !Food.IsHeld)) body.isKinematic = false;
        }
    }
}
