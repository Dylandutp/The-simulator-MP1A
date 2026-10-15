using UnityEngine;

namespace HouseSim.Gym
{
    /// <summary>
    /// Editable prefab anchor for a runtime stat pad. Move or rotate this child in
    /// Prefab Mode to adjust the pad without changing simulation code.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GymStatPadAnchor : MonoBehaviour
    {
        [SerializeField] bool liesFlat;
        public bool LiesFlat => liesFlat;

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(.75f, .02f, .18f, .9f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(.64f, .34f, .015f));
        }
    }
}
