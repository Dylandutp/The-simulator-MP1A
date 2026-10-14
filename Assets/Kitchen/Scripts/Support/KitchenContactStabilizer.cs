using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace HouseSim.Kitchen
{
    /// <summary>Protect loose ingredients from the effectively infinite mass of tracked hands.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(Rigidbody))]
    public sealed class KitchenContactStabilizer : MonoBehaviour
    {
        static readonly List<KitchenContactStabilizer> Active = new List<KitchenContactStabilizer>();
        static long nextOrder;
        long order;
        readonly Dictionary<KitchenContactStabilizer, float> ignored = new Dictionary<KitchenContactStabilizer, float>();
        static PhysicsMaterial softMaterial;
        Rigidbody body;
        KitchenFood food;
        KitchenPan pan;
        KitchenKnife knife;
        XRGrabInteractable grab;
        Collider[] colliders;
        bool Held => food ? food.IsHeld : (grab && grab.isSelected) || KitchenDesktopHeld.IsHeldByDesktop(gameObject);

        void Awake()
        {
            order = ++nextOrder;
            body = GetComponent<Rigidbody>(); food = GetComponent<KitchenFood>();
            pan = GetComponent<KitchenPan>(); knife = GetComponent<KitchenKnife>();
            grab = GetComponent<XRGrabInteractable>();
            body.maxDepenetrationVelocity = .8f;
            body.maxAngularVelocity = 8f;
            body.solverIterations = 10;
            body.solverVelocityIterations = 4;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = body.isKinematic ? CollisionDetectionMode.ContinuousSpeculative : CollisionDetectionMode.ContinuousDynamic;
            if (food) { body.linearDamping = .25f; body.angularDamping = 1.5f; }
            if (!softMaterial) softMaterial = new PhysicsMaterial("Kitchen gentle contact")
            {
                bounciness = 0f, dynamicFriction = .45f, staticFriction = .6f,
                bounceCombine = PhysicsMaterialCombine.Minimum, frictionCombine = PhysicsMaterialCombine.Average
            };
            RefreshColliders();
        }

        public void RefreshColliders()
        {
            colliders = GetComponentsInChildren<Collider>();
            foreach (var collider in colliders)
                if (collider.attachedRigidbody == body && !collider.isTrigger) collider.sharedMaterial = softMaterial;
        }

        void OnEnable() { Active.Add(this); }
        void OnDisable()
        {
            foreach (var other in Active)
            {
                if (!other || other == this) continue;
                SetPair(other, false);
                other.ignored.Remove(this);
            }
            ignored.Clear(); Active.Remove(this);
        }

        void FixedUpdate()
        {
            if (food && !body.isKinematic && !Held)
            {
                body.linearVelocity = Vector3.ClampMagnitude(body.linearVelocity, 4f);
                body.angularVelocity = Vector3.ClampMagnitude(body.angularVelocity, 8f);
            }
            foreach (var other in Active)
            {
                if (!other || other == this || order > other.order) continue;
                // Pan assistance owns this pair; resting steak must retain physical pan support.
                if ((pan && other.food && other.food.definition && other.food.definition.kind == FoodKind.Steak)
                    || (other.pan && food && food.definition && food.definition.kind == FoodKind.Steak)) continue;
                if (!food && !other.food) continue;
                bool shouldIgnore = (knife && other.food) || (other.knife && food) || Held || other.Held;
                if (shouldIgnore)
                {
                    if (!ignored.ContainsKey(other)) SetPair(other, true);
                    ignored[other] = Time.fixedTime;
                }
                else if (ignored.TryGetValue(other, out float lastHeld) && Time.fixedTime - lastHeld >= .08f)
                {
                    // A short release grace plus capped separation velocity avoids an overlap explosion.
                    SetPair(other, false);
                    ignored.Remove(other);
                }
            }
        }

        void SetPair(KitchenContactStabilizer other, bool value)
        {
            if (colliders == null || other.colliders == null) return;
            foreach (var a in colliders)
                foreach (var b in other.colliders)
                    if (a && b && a.enabled && b.enabled && !a.isTrigger && !b.isTrigger
                        && a.attachedRigidbody == body && b.attachedRigidbody == other.body)
                        Physics.IgnoreCollision(a, b, value);
        }
    }
}
