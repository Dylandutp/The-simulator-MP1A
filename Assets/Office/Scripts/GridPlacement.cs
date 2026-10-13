using UnityEngine;
using UnityEngine.InputSystem;

namespace OfficeSim
{
    public class GridPlacement : MonoBehaviour
    {
        public Transform rightController;
        public GameObject gridVisual;
        public LayerMask obstacleMask;
        public Collider floor;
        public GameObject previewPrefab;
        public bool Active { get; private set; }

        private MachineKind selected;
        private Workstation moving;
        private ConveyorBelt movingBelt;
        private bool buildingConveyor;
        private ConveyorKind selectedConveyor;
        private GameObject ghost;
        private Renderer ghostRenderer;
        private MaterialPropertyBlock previewColor;

        public InputActionReference confirmInput;
        public InputActionReference cancelInput;
        public InputActionReference rotateInput;

        private Vector2Int candidate;
        private int turns;
        private bool pointed;

        void Awake()
        {
            previewColor = new MaterialPropertyBlock();
        }

        void OnEnable()
        {
            confirmInput.action.Enable();
            cancelInput.action.Enable();
            rotateInput.action.Enable();
        }

        void OnDisable()
        {
            confirmInput.action.Disable();
            cancelInput.action.Disable();
            rotateInput.action.Disable();
            Cancel();
        }

        public void BeginBuild(MachineKind kind)
        {
            Cancel();
            selected = kind;
            if (OfficeGame.Instance.inventory[Resource.Cash] < OfficeRules.Price(kind))
            {
                OfficeGame.Instance.Message("Not enough Cash to build this equipment.");
                return;
            }

            Begin();
        }

        public void BeginMove(Workstation station)
        {
            Cancel();
            moving = station;
            selected = station.state.kind;
            station.state.moving = true;
            turns = Mathf.RoundToInt(station.transform.eulerAngles.y / 90);
            Begin();
        }

        public void BeginConveyor(ConveyorKind kind)
        {
            Cancel();
            if (OfficeGame.Instance.inventory[Resource.Cash] < 10)
            {
                OfficeGame.Instance.Message("Not enough Cash. Conveyors cost $10.");
                return;
            }

            buildingConveyor = true;
            selectedConveyor = kind;
            Begin();
        }

        public void BeginMove(ConveyorBelt belt)
        {
            Cancel();
            movingBelt = belt;
            buildingConveyor = true;
            selectedConveyor = belt.kind;
            belt.moving = true;
            turns = Mathf.RoundToInt(belt.transform.eulerAngles.y / 90);
            Begin();
        }

        void Begin()
        {
            Active = true;
            gridVisual.SetActive(true);
            ghost = Instantiate(previewPrefab);
            ghostRenderer = ghost.GetComponent<Renderer>();
            if (buildingConveyor && selectedConveyor != ConveyorKind.Straight)
            {
                float angle = selectedConveyor == ConveyorKind.Left ? -90 : 90;
                ghost.transform.GetChild(0).localRotation = Quaternion.Euler(0, angle, 0);
            }
            ghost.transform.rotation = Quaternion.Euler(0, turns * 90, 0);
        }

        void Update()
        {
            if (!Active)
            {
                return;
            }

            if (Physics.Raycast(rightController.position, rightController.forward, out RaycastHit hit,
                20, ~(1 << 2), QueryTriggerInteraction.Ignore) && hit.collider == floor)
            {
                AimAtFloor(hit.point);
            }

            bool valid = pointed && CanPlace(candidate, moving);
            ghost.transform.position = OfficeRules.Position(candidate) + Vector3.up * .46f;
            ghost.transform.rotation = Quaternion.Euler(0, turns * 90, 0);
            Color color = valid ? new Color(.15f, .85f, .6f, .4f) : new Color(.95f, .2f, .15f, .4f);
            previewColor.SetColor("_BaseColor", color);
            ghostRenderer.SetPropertyBlock(previewColor);

            if (cancelInput.action.WasPressedThisFrame())
            {
                Cancel();
                return;
            }

            if (rotateInput.action.WasPressedThisFrame())
            {
                Rotate();
            }

            if (confirmInput.action.WasPressedThisFrame())
            {
                Confirm();
            }
        }

        public bool CanPlace(Vector2Int cell, Workstation ignored = null)
        {
            if (!OfficeRules.Inside(cell))
            {
                return false;
            }

            foreach (var station in OfficeGame.Instance.stations)
            {
                if (station != ignored && station.cell == cell)
                {
                    return false;
                }
            }

            foreach (var belt in OfficeGame.Instance.conveyors)
            {
                if (belt != movingBelt && belt.cell == cell)
                {
                    return false;
                }
            }

            if (OfficeRules.Cell(OfficeGame.Instance.delivery.transform.position) == cell)
            {
                return false;
            }

            var colliders = Physics.OverlapBox(
                OfficeRules.Position(cell) + new Vector3(0, 0.8f, 0),
                new Vector3(0.53f, 0.78f, 0.53f),
                Quaternion.identity,
                obstacleMask,
                QueryTriggerInteraction.Ignore);
            foreach (var collider in colliders)
            {
                bool belongsToMovingStation = ignored && collider.transform.IsChildOf(ignored.transform);
                bool belongsToMovingBelt = movingBelt && collider.transform.IsChildOf(movingBelt.transform);
                if (!belongsToMovingStation && !belongsToMovingBelt)
                {
                    return false;
                }
            }

            return true;
        }

        public void Rotate()
        {
            turns = (turns + 1) % 4;
            ghost.transform.rotation = Quaternion.Euler(0, turns * 90, 0);
        }

        public void AimAtFloor(Vector3 position)
        {
            candidate = OfficeRules.Cell(position);
            pointed = true;
        }

        public void Confirm()
        {
            if (!Active || !pointed || !CanPlace(candidate, moving))
            {
                return;
            }

            if (movingBelt)
            {
                movingBelt.transform.SetPositionAndRotation(OfficeRules.Position(candidate), Quaternion.Euler(0, turns * 90, 0));
                movingBelt.cell = candidate;
                movingBelt.moving = false;
                movingBelt = null;
            }
            else if (moving)
            {

                var items = FindObjectsByType<OfficeItem>();
                foreach (var item in items)
                {
                    if (item.outputOwner == moving)
                    {
                        item.transform.SetParent(moving.transform, true);
                    }
                }

                moving.transform.SetPositionAndRotation(OfficeRules.Position(candidate), Quaternion.Euler(0, turns * 90, 0));
                moving.cell = candidate;
                foreach (var item in items)
                {
                    if (item.outputOwner == moving)
                    {
                        item.transform.SetParent(null, true);
                    }
                }

                moving.state.moving = false;
                moving = null;
            }
            else if (buildingConveyor)
            {
                if (!OfficeGame.Instance.inventory.Spend(Resource.Cash, 10))
                {
                    return;
                }

                var belt = Instantiate(OfficeGame.Instance.conveyorPrefabs[(int)selectedConveyor],
                    OfficeRules.Position(candidate), Quaternion.Euler(0, turns * 90, 0));
                belt.cell = candidate;
            }
            else
            {
                if (!OfficeGame.Instance.inventory.Spend(Resource.Cash, OfficeRules.Price(selected)))
                {
                    return;
                }

                var station = Instantiate(
                    OfficeGame.Instance.machinePrefabs[(int)selected],
                    OfficeRules.Position(candidate),
                    Quaternion.Euler(0, turns * 90, 0));
                station.cell = candidate;
            }

            OfficeGame.Instance.Ping();
            Cancel();
        }

        public void Cancel()
        {
            if (!Active)
            {
                return;
            }

            if (movingBelt)
            {
                movingBelt.moving = false;
            }

            movingBelt = null;
            buildingConveyor = false;
            if (moving)
            {
                moving.state.moving = false;
            }

            moving = null;
            Active = false;
            pointed = false;
            turns = 0;
            Destroy(ghost);
            gridVisual.SetActive(false);
        }
    }
}
