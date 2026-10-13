using System;
using System.Collections.Generic;
using UnityEngine;

namespace HouseSim.Gym
{
    public class GymGridBuilder : MonoBehaviour
    {
        private const int MatCost = 10;
        private const float TileSize = 1.5f;

        public GymContentCatalog catalog;
        public ResourceData gymCoinsResource;
        public Vector3 gridOrigin;
        public Func<GymMachineDefinition, Vector3, GameObject> machineFactory;

        private readonly Dictionary<Vector2Int, RubberMatFoundation> mats =
            new Dictionary<Vector2Int, RubberMatFoundation>();
        private readonly HashSet<Vector2Int> occupiedByMachine = new HashSet<Vector2Int>();
        private GymMachineDefinition selectedMachine;

        public Vector2Int GetCell(Vector3 worldPosition)
        {
            Vector3 local = worldPosition - gridOrigin;
            return new Vector2Int(
                Mathf.RoundToInt(local.x / TileSize),
                Mathf.RoundToInt(local.z / TileSize));
        }

        public void SelectMachine(GymMachineDefinition definition)
        {
            if (definition == null)
            {
                Debug.LogError("Cannot select an unconfigured machine for construction.", this);
                return;
            }

            selectedMachine = definition;
        }

        public bool TryBuildMat(Vector3 targetPosition)
        {
            Vector2Int cell = GetCell(targetPosition);
            if (mats.ContainsKey(cell))
            {
                Debug.Log("A rubber mat already occupies that grid tile.", this);
                return false;
            }

            if (!SpendCoins(MatCost)) return false;

            GameObject matPrefab = catalog.utilityPrefabs != null && catalog.utilityPrefabs.Length > 0
                ? catalog.utilityPrefabs[0]
                : null;
            GameObject matObject = matPrefab == null
                ? GameObject.CreatePrimitive(PrimitiveType.Plane)
                : Instantiate(matPrefab);
            matObject.name = "Rubber Mat";
            matObject.transform.position = CellPosition(cell, 0.01f);
            matObject.transform.localScale =
                new Vector3((TileSize - 0.04f) / 10f, 1f, (TileSize - 0.04f) / 10f);
            Renderer matRenderer = matObject.GetComponent<Renderer>();
            if (matRenderer == null)
            {
                Debug.LogError("The Rubber Mat prefab must have a visible plane renderer.", matObject);
                HouseEvents.ReportResourceGained(gymCoinsResource, MatCost);
                Destroy(matObject);
                return false;
            }
            matRenderer.material.color = new Color(0.12f, 0.13f, 0.14f);

            RubberMatFoundation foundation = matObject.GetComponent<RubberMatFoundation>();
            if (foundation == null) foundation = matObject.AddComponent<RubberMatFoundation>();
            foundation.gridCell = cell;
            foundation.builder = this;
            MachineStats matStats = matObject.GetComponent<MachineStats>();
            if (matStats == null) matStats = matObject.AddComponent<MachineStats>();
            matStats.Configure("Rubber Mat", catalog.GetStat("MatIntegrity"), null);
            mats.Add(cell, foundation);
            GymActionInteractable matInteraction = matObject.GetComponent<GymActionInteractable>();
            if (matInteraction == null) matInteraction = matObject.AddComponent<GymActionInteractable>();
            ConfigureMatInteraction(matInteraction, matObject);
            return true;
        }

        public bool TryBuildSelectedMachine(Vector3 targetPosition)
        {
            if (selectedMachine == null)
            {
                Debug.Log("Choose a machine from the chalkboard before placing it.", this);
                return false;
            }

            Vector2Int cell = GetCell(targetPosition);
            if (!mats.ContainsKey(cell))
            {
                Debug.Log("Machines can only be built on a Rubber Mat.", this);
                return false;
            }

            if (occupiedByMachine.Contains(cell))
            {
                Debug.Log("That rubber mat already has a machine.", this);
                return false;
            }

            if (machineFactory == null)
            {
                Debug.LogError("The gym machine factory has not been configured.", this);
                return false;
            }

            if (!SpendCoins(selectedMachine.buildCost)) return false;

            Vector3 placement = CellPosition(cell, selectedMachine.dimensions.y * 0.5f + 0.12f);
            GameObject machine = machineFactory(selectedMachine, placement);
            if (machine == null)
            {
                HouseEvents.ReportResourceGained(gymCoinsResource, selectedMachine.buildCost);
                Debug.LogError($"Failed to build '{selectedMachine.machineName}'.", this);
                return false;
            }

            occupiedByMachine.Add(cell);
            GridPlacement marker = machine.AddComponent<GridPlacement>();
            marker.builder = this;
            marker.gridCell = cell;
            return true;
        }

        public bool TryBuildMatAtView()
        {
            return TryBuildMat(GetAimPosition());
        }

        private void ConfigureMatInteraction(GymActionInteractable interactable, GameObject matObject)
        {
            interactable.Configure(() =>
            {
                if (selectedMachine != null)
                    TryBuildSelectedMachine(matObject.transform.position);
            }, allowGrab: false);
        }

        private bool SpendCoins(int amount)
        {
            if (PlayerResourceWallet.Instance == null || gymCoinsResource == null)
            {
                Debug.LogError("Building requires an initialized wallet and GymCoins resource.", this);
                return false;
            }
            if (PlayerResourceWallet.Instance.IsDead) return false;

            if (!PlayerResourceWallet.Instance.TrySpend(gymCoinsResource, amount))
            {
                Debug.Log("Not enough GymCoins to build that object.", this);
                return false;
            }

            return true;
        }

        private Vector3 CellPosition(Vector2Int cell, float height)
        {
            return gridOrigin + new Vector3(cell.x * TileSize, height, cell.y * TileSize);
        }

        private Vector3 GetAimPosition()
        {
            Camera playerCamera = Camera.main;
            if (playerCamera != null)
            {
                Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
                RaycastHit[] hits = Physics.RaycastAll(ray, 20f);
                Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));
                foreach (RaycastHit hit in hits)
                {
                    if (hit.collider.gameObject.name == "floor") return hit.point;
                }

                Vector3 projected = playerCamera.transform.position + playerCamera.transform.forward * 3f;
                projected.y = gridOrigin.y;
                return projected;
            }

            return gridOrigin + new Vector3(0f, 0f, TileSize);
        }

        public void ReleaseMat(Vector2Int cell)
        {
            mats.Remove(cell);
            occupiedByMachine.Remove(cell);
        }

        public void ReleaseMachine(Vector2Int cell)
        {
            occupiedByMachine.Remove(cell);
        }
    }
}
