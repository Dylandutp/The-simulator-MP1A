using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace HouseSim.Gym
{
    /// <summary>
    /// Makes bought equipment grabbable and reports grab/release through XRI events.
    /// GymSim owns the grid and decides whether the released object can be installed.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GymEquipmentPlacement : MonoBehaviour
    {
        GymSim owner;
        GymSimThing thing;
        XRGrabInteractable grabInteractable;
        Rigidbody body;
        bool isHeld;

        public GymSimThing Thing => thing;
        public Rigidbody Body => body;
        public bool IsRubberMat => thing != null && thing.kind == "rubberMat";
        public bool UsesFloorGravity => thing != null && thing.kind == "shaker";

        public void Configure(GymSim gymSim, GymSimThing gymThing, bool startsInstalled)
        {
            owner = gymSim;
            thing = gymThing;

            XRSimpleInteractable simpleInteractable = GetComponent<XRSimpleInteractable>();
            if (simpleInteractable != null)
                DestroyImmediate(simpleInteractable);

            body = GetComponent<Rigidbody>();
            if (body == null) body = gameObject.AddComponent<Rigidbody>();
            body.useGravity = true;
            body.isKinematic = startsInstalled && !UsesFloorGravity;
            body.mass = UsesFloorGravity ? 4f : Mathf.Max(1f, body.mass);
            body.linearDamping = UsesFloorGravity ? 2f : body.linearDamping;
            body.angularDamping = UsesFloorGravity ? 4f : body.angularDamping;
            body.constraints = thing != null && thing.kind == "wrench"
                ? RigidbodyConstraints.None
                : RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

            foreach (MeshCollider meshCollider in GetComponentsInChildren<MeshCollider>())
                meshCollider.convex = true;

            grabInteractable = GetComponent<XRGrabInteractable>();
            if (grabInteractable == null) grabInteractable = gameObject.AddComponent<XRGrabInteractable>();
            grabInteractable.useDynamicAttach = true;
            grabInteractable.throwOnDetach = false;
            grabInteractable.movementType = XRBaseInteractable.MovementType.Kinematic;
            grabInteractable.trackRotation = true;
            grabInteractable.selectEntered.RemoveListener(OnGrabbed);
            grabInteractable.selectExited.RemoveListener(OnReleased);
            grabInteractable.selectEntered.AddListener(OnGrabbed);
            grabInteractable.selectExited.AddListener(OnReleased);

            HoverOutline outline = GetComponent<HoverOutline>();
            if (outline != null) outline.Bind(grabInteractable);
        }

        public void SetInstalled(bool installed)
        {
            if (thing != null) thing.placed = installed;
            if (body == null) return;

            body.isKinematic = installed && !UsesFloorGravity;
            if (!installed) return;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }

        public void PlaceBottomAt(float surfaceY)
        {
            if (!TryGetBounds(out Bounds bounds)) return;
            transform.position += Vector3.up * (surfaceY - bounds.min.y + .005f);
        }

        public void SnapTo(Vector3 surfacePosition, Quaternion gridRotation)
        {
            float yaw = Mathf.Round(gridRotation.eulerAngles.y / 90f) * 90f;
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            transform.position = surfacePosition;
            PlaceBottomAt(surfacePosition.y);
        }

        public void SnapUpright(float floorY)
        {
            transform.rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
            PlaceBottomAt(floorY);
        }

        public void ReleaseWithGravity()
        {
            transform.rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
            if (body == null) return;
            body.isKinematic = false;
            body.useGravity = true;
            body.WakeUp();
        }

        bool TryGetBounds(out Bounds bounds)
        {
            bounds = default;
            bool found = false;
            foreach (Renderer candidate in GetComponentsInChildren<Renderer>())
            {
                if (candidate == null || !candidate.enabled || candidate is ParticleSystemRenderer || candidate.GetComponentInParent<GymSimGauge>() != null) continue;
                if (!found) { bounds = candidate.bounds; found = true; }
                else bounds.Encapsulate(candidate.bounds);
            }

            if (found) return true;
            foreach (Collider candidate in GetComponentsInChildren<Collider>())
            {
                if (candidate == null || candidate.isTrigger || !candidate.enabled) continue;
                if (!found) { bounds = candidate.bounds; found = true; }
                else bounds.Encapsulate(candidate.bounds);
            }
            return found;
        }

        void OnGrabbed(SelectEnterEventArgs _)
        {
            isHeld = true;
            owner?.BeginMovingEquipment(this);
        }

        void OnReleased(SelectExitEventArgs _)
        {
            if (thing == null || thing.kind != "wrench")
                transform.rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
            isHeld = false;
            owner?.FinishMovingEquipment(this);
        }

        void LateUpdate()
        {
            if (!isHeld || (thing != null && thing.kind == "wrench")) return;
            transform.rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
        }

        void OnDestroy()
        {
            if (grabInteractable == null) return;
            grabInteractable.selectEntered.RemoveListener(OnGrabbed);
            grabInteractable.selectExited.RemoveListener(OnReleased);
        }
    }
}
