using UnityEngine;

namespace HouseSim.Gym
{
    /// <summary>
    /// One grid socket on a rubber mat. A socket can hold exactly one piece of equipment.
    /// </summary>
    public sealed class GymMatSocket : MonoBehaviour
    {
        GymSim owner;
        GymSimThing mat;
        GymEquipmentPlacement occupant;

        public Vector2Int Cell => mat != null ? mat.cell : Vector2Int.zero;
        public bool IsFree => occupant == null;
        public GymEquipmentPlacement Occupant => occupant;

        public Vector3 SurfacePosition
        {
            get
            {
                if (mat == null) return transform.position;

                float surfaceY = mat.transform.position.y;
                foreach (Collider candidate in mat.GetComponentsInChildren<Collider>())
                {
                    if (candidate == null || candidate.isTrigger || !candidate.enabled) continue;
                    surfaceY = Mathf.Max(surfaceY, candidate.bounds.max.y);
                }

                return new Vector3(mat.transform.position.x, surfaceY, mat.transform.position.z);
            }
        }

        public void Configure(GymSim gymSim, GymSimThing matThing)
        {
            owner = gymSim;
            mat = matThing;
            transform.position = SurfacePosition;
            transform.rotation = matThing.transform.rotation;
        }

        public void SetOccupant(GymEquipmentPlacement placement)
        {
            occupant = placement;
        }

        public void ClearOccupant(GymEquipmentPlacement placement)
        {
            if (occupant == placement) occupant = null;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = occupant == null ? new Color(.72f, .02f, .22f, .8f) : new Color(.35f, .35f, .4f, .8f);
            Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
            Gizmos.DrawWireCube(new Vector3(0f, .2f, 0f), new Vector3(.9f, .4f, .9f));
        }
    }
}
