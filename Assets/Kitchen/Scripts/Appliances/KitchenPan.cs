using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace HouseSim.Kitchen
{
    [RequireComponent(typeof(Rigidbody), typeof(XRGrabInteractable))]
    public sealed class KitchenPan : MonoBehaviour
    {
        public Transform cookingSurface;
        [Tooltip("Half extents of the food area, in cooking-surface local units.")]
        public Vector3 bounds = new Vector3(0.21f, 0.10f, 0.21f);
        public bool IsHeld => (grab && grab.isSelected) || KitchenDesktopHeld.IsHeldByDesktop(gameObject);
        private XRGrabInteractable grab;

        private void Awake()
        {
            grab = GetComponent<XRGrabInteractable>();
            if (!cookingSurface) cookingSurface = transform;
            Rigidbody body = GetComponent<Rigidbody>();
            body.maxAngularVelocity = 12f;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.interpolation = RigidbodyInterpolation.Interpolate;
        }

        public IEnumerable<KitchenFood> GetSteaks()
        {
            Transform area = cookingSurface ? cookingSurface : transform;
            Vector3 halfExtents = Vector3.Scale(bounds, Abs(area.lossyScale));
            var unique = new HashSet<KitchenFood>();
            foreach (Collider hit in Physics.OverlapBox(area.position, halfExtents, area.rotation, ~0, QueryTriggerInteraction.Ignore))
            {
                KitchenFood food = hit.GetComponentInParent<KitchenFood>();
                if (food && !food.IsConsumed && !food.IsHeld && food.definition && food.definition.kind == FoodKind.Steak
                    && new Bounds(Vector3.zero, bounds * 2f).Contains(area.InverseTransformPoint(food.transform.position)) && unique.Add(food))
                    yield return food;
            }
        }

        private static Vector3 Abs(Vector3 value) => new Vector3(Mathf.Abs(value.x), Mathf.Abs(value.y), Mathf.Abs(value.z));

        private void OnDrawGizmosSelected()
        {
            Transform area = cookingSurface ? cookingSurface : transform;
            Gizmos.matrix = area.localToWorldMatrix;
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.4f);
            Gizmos.DrawWireCube(Vector3.zero, bounds * 2f);
        }
    }
}
