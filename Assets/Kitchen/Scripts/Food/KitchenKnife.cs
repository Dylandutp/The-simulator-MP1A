using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace HouseSim.Kitchen
{
    [RequireComponent(typeof(XRGrabInteractable))]
    public class KitchenKnife : MonoBehaviour
    {
        [Min(0.01f)] public float minimumSpeed = 0.18f;
        [Min(0.05f)] public float chopCooldown = 0.35f;
        private XRGrabInteractable grab;
        private Rigidbody body;
        private Vector3 previousPosition;
        private float movementSpeed;
        private float nextChopAt;

        private void Awake()
        {
            grab = GetComponent<XRGrabInteractable>();
            body = GetComponent<Rigidbody>();
            previousPosition = transform.position;
        }

        private void Update()
        {
            movementSpeed = Vector3.Distance(transform.position, previousPosition) / Mathf.Max(Time.deltaTime, 0.001f);
            previousPosition = transform.position;
        }

        private void OnCollisionEnter(Collision collision) { TryChop(collision.collider, collision.relativeVelocity.magnitude); }
        private void OnCollisionStay(Collision collision) { TryChop(collision.collider, collision.relativeVelocity.magnitude); }
        private void OnTriggerEnter(Collider other) { TryChop(other, body != null ? body.linearVelocity.magnitude : movementSpeed); }
        private void OnTriggerStay(Collider other) { TryChop(other, body != null ? body.linearVelocity.magnitude : movementSpeed); }

        private void TryChop(Collider other, float speed)
        {
            bool held = (grab != null && grab.isSelected) || KitchenDesktopHeld.IsHeldByDesktop(gameObject);
            if (!held || Time.time < nextChopAt || Mathf.Max(speed, movementSpeed) < minimumSpeed) return;
            var food = other.GetComponentInParent<KitchenFood>();
            if (food == null || food.IsConsumed || food.IsChopped || !food.IsProduce) return;
            if (!food.Chop()) return;
            nextChopAt = Time.time + chopCooldown;
            KitchenSession.Instance?.Tutorial("chop", "Hold the knife and cut produce.\nChopped food keeps its cleanliness.", transform.position);
        }
    }
}
