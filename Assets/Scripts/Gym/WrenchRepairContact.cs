using UnityEngine;

namespace HouseSim.Gym
{
    /// <summary>Tracks the repairable object physically touching the wrench.</summary>
    public sealed class WrenchRepairContact : MonoBehaviour
    {
        GymSimThing touching;
        float lastContactTime;

        public bool TryGetRepairTarget(out GymSimThing target)
        {
            PollOverlaps();
            target = Time.time - lastContactTime <= .15f ? touching : null;
            return target != null;
        }

        public bool IsTouching(GymSimThing target)
        {
            PollOverlaps();
            return target != null && Time.time - lastContactTime <= .15f && touching == target;
        }

        void OnCollisionStay(Collision collision) => TryRecord(collision.collider);
        void OnTriggerStay(Collider other) => TryRecord(other);

        // Installed equipment and a grabbed wrench can both be kinematic, so Unity may
        // not emit collision callbacks. This still uses the real collider bounds and
        // makes contact repair reliable for that common XRI setup.
        void PollOverlaps()
        {
            foreach (Collider wrenchCollider in GetComponentsInChildren<Collider>())
            {
                if (wrenchCollider == null || !wrenchCollider.enabled || wrenchCollider.isTrigger) continue;
                Bounds bounds = wrenchCollider.bounds;
                bounds.Expand(.02f);
                Collider[] overlaps = Physics.OverlapBox(bounds.center, bounds.extents, Quaternion.identity,
                    ~0, QueryTriggerInteraction.Collide);
                foreach (Collider overlap in overlaps)
                {
                    if (overlap == null || overlap.transform.IsChildOf(transform)) continue;
                    if (!bounds.Intersects(overlap.bounds)) continue;
                    if (TryRecord(overlap)) return;
                }
            }
        }

        bool TryRecord(Collider other)
        {
            if (other == null || other.transform.IsChildOf(transform)) return false;
            GymSimThing candidate = other.GetComponentInParent<GymSimThing>();
            if (candidate == null || candidate.kind == "wrench") return false;

            bool repairable = candidate.kind == "rubberMat" ||
                              candidate.stats.ContainsKey(candidate.kind + "Durability");
            if (!repairable) return false;

            touching = candidate;
            lastContactTime = Time.time;
            return true;
        }
    }
}
