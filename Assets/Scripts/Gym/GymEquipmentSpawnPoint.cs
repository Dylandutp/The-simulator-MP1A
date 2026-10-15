using UnityEngine;

namespace HouseSim.Gym
{
    /// <summary>Marks where loose shop purchases appear inside GymRoomScene.</summary>
    public sealed class GymEquipmentSpawnPoint : MonoBehaviour
    {
        [SerializeField, Min(.25f)] float spacing = 1.5f;
        [SerializeField, Min(1)] int positionsPerRow = 3;

        public Vector3 GetSpawnPosition(int purchaseIndex)
        {
            int column = purchaseIndex % positionsPerRow;
            int row = purchaseIndex / positionsPerRow;
            float centeredColumn = column - (positionsPerRow - 1) * .5f;
            return transform.position + transform.right * (centeredColumn * spacing) + transform.forward * (row * spacing);
        }
    }
}
