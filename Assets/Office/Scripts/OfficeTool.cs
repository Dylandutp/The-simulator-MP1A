using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace OfficeSim
{
    public class OfficeTool : MonoBehaviour
    {
        public ToolKind kind;
        public Transform tip;
        public Workstation owner;
        public Transform homeAnchor;
        public bool retired;
        private XRGrabInteractable grab;
        public bool Held => !retired && grab.isSelected;

        void Awake()
        {
            grab = GetComponent<XRGrabInteractable>();
            grab.firstSelectEntered.AddListener(
                _ => OfficeGame.Instance.PlayGrab());
        }

        public void Retire()
        {
            retired = true;
            if (grab.isSelected)
            {
                grab.interactionManager.CancelInteractableSelection((IXRSelectInteractable)grab);
            }

            grab.enabled = false;
            foreach (var collider in GetComponentsInChildren<Collider>())
            {
                collider.enabled = false;
            }

            gameObject.SetActive(false);
            Destroy(gameObject);
        }

        void Update()
        {
            if (transform.position.y > -2)
            {
                return;
            }

            transform.position = homeAnchor.position;
            var rb = GetComponent<Rigidbody>();
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
    }
}
