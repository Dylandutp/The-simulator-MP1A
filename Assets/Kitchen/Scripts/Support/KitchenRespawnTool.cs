using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace HouseSim.Kitchen
{
    /// <summary>Returns dropped utensils instead of letting a lost tool break the cooking loop.</summary>
    public sealed class KitchenRespawnTool : MonoBehaviour
    {
        public Transform recoveryAnchor;
        public float minimumY = -2.5f;
        [Min(0f)] public float maximumDistance = 25f;
        Vector3 initialPosition;
        Quaternion initialRotation;
        Rigidbody body;
        XRGrabInteractable grab;

        void Start()
        {
            initialPosition = transform.position;
            initialRotation = transform.rotation;
            body = GetComponent<Rigidbody>();
            grab = GetComponent<XRGrabInteractable>();
            if (recoveryAnchor == null && GetComponent<KitchenFood>() != null && KitchenSession.Instance != null)
                recoveryAnchor = KitchenSession.Instance.recoveryAnchor;
        }

        void Update()
        {
            if ((grab != null && grab.isSelected) || KitchenDesktopHeld.IsHeldByDesktop(gameObject)) return;
            Vector3 home = recoveryAnchor != null ? recoveryAnchor.position : initialPosition;
            if (transform.position.y < minimumY || (maximumDistance > 0f && (transform.position - home).sqrMagnitude > maximumDistance * maximumDistance))
                ReturnHome();
        }

        public void ReturnHome()
        {
            if ((grab != null && grab.isSelected) || KitchenDesktopHeld.IsHeldByDesktop(gameObject)) return;
            Vector3 position = recoveryAnchor != null ? recoveryAnchor.position : initialPosition;
            Quaternion rotation = recoveryAnchor != null ? recoveryAnchor.rotation : initialRotation;
            if (body != null)
            {
                if (!body.isKinematic)
                {
                    body.linearVelocity = Vector3.zero;
                    body.angularVelocity = Vector3.zero;
                }
                body.position = position;
                body.rotation = rotation;
            }
            else transform.SetPositionAndRotation(position, rotation);
            KitchenSession.Instance?.Notify(name + " returned to its worktop.");
        }
    }
}
