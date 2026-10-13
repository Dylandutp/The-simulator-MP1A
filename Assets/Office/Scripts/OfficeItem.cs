using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace OfficeSim
{
    public class OfficeItem : MonoBehaviour
    {
        public Resource resource;
        public bool consumed;
        public bool owned;
        public Workstation outputOwner;
        private Vector3 safePosition;
        private XRGrabInteractable grab;
        public bool Held => grab.isSelected;

        void Awake()
        {
            grab = GetComponent<XRGrabInteractable>();
            grab.firstSelectEntered.AddListener(_ =>
            {
                ReleaseOutput();
                OfficeGame.Instance.PlayGrab();
            });
            safePosition = transform.position;
        }

        public void Initialize(Resource kind, bool credit)
        {
            resource = kind;
            owned = true;
            consumed = false;
            safePosition = transform.position;
            OfficeGame.Instance.RegisterPhysical(kind, credit);
        }

        public bool Consume()
        {
            if (consumed || !owned || !OfficeGame.Instance.ConsumePhysical(resource))
            {
                return false;
            }

            Retire();
            return true;
        }

        // The conveyor keeps this resource reserved while displaying its own visual.
        public bool StoreOnConveyor()
        {
            if (consumed || !owned)
            {
                return false;
            }

            Retire();
            return true;
        }

        void Retire()
        {
            consumed = true;
            owned = false;
            ReleaseOutput();
            if (grab.isSelected)
            {
                grab.interactionManager.CancelInteractableSelection((IXRSelectInteractable)grab);
            }

            foreach (var collider in GetComponentsInChildren<Collider>())
            {
                collider.enabled = false;
            }

            Destroy(gameObject);
        }

        void ReleaseOutput()
        {
            if (!outputOwner)
            {
                return;
            }

            outputOwner.state.outputCount--;
            outputOwner = null;
        }

        void Update()
        {
            if (transform.position.y > -2 || consumed)
            {
                return;
            }

            transform.position = safePosition + Vector3.up * 0.15f;
            var rb = GetComponent<Rigidbody>();
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
    }
}
