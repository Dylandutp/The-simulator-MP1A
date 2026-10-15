using UnityEngine;

namespace HouseSim.Gym
{
    public sealed partial class GymSim
    {
        int purchasedSpawnIndex;

        /// <summary>Creates a loose, grabbable purchase directly inside GymRoomScene.</summary>
        public bool SpawnPurchasedEquipment(string itemKind)
        {
            if (!SupportsInventoryItem(itemKind) || itemKind == "benchPress" || itemKind == "waterDispenser" || itemKind == "wrench")
                return false;

            if (catalog == null) catalog = Resources.Load<GymSimCatalog>("GymSimCatalog");
            if (catalog == null || catalog.FindPrefab(itemKind) == null) return false;

            GymEquipmentSpawnPoint spawnPoint = FindAnyObjectByType<GymEquipmentSpawnPoint>();
            Vector3 forward = Vector3.ProjectOnPlane(headset.forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < .01f) forward = Vector3.forward;
            Vector3 spawnPosition = spawnPoint != null
                ? spawnPoint.GetSpawnPosition(purchasedSpawnIndex++)
                : headset.position + forward * 2f;

            GymSimThing thing = CreateThing(itemKind, spawnPosition, false);
            GymEquipmentPlacement placement = thing.GetComponent<GymEquipmentPlacement>();
            if (placement == null) return false;

            placement.PlaceBottomAt(spawnPosition.y);
            placement.SetInstalled(false);
            Toast(DisplayName(itemKind) + " spawned in the gym");
            return true;
        }
    }
}
