using UnityEngine;
using System.Collections.Generic;

namespace OfficeSim
{
    public enum ConveyorKind
    {
        Straight,
        Left,
        Right
    }

    public sealed class ConveyorBelt : MonoBehaviour
    {
        public ConveyorKind kind;
        public Vector2Int cell;
        [Min(0.1f)] public float transferDuration = 2f;
        public int load;
        public float progress;
        public Resource cargo;
        public bool moving;
        public Renderer beltSurface;
        public GameObject[] documentVisuals;
        private MaterialPropertyBlock surfaceProperties;
        private float offset;
        private readonly HashSet<ConveyorBelt> visited = new HashSet<ConveyorBelt>();

        public Vector2Int InputDirection => Direction(Vector3.forward);
        public Vector2Int OutputDirection
        {
            get
            {
                switch (kind)
                {
                    case ConveyorKind.Left:
                        return Direction(Vector3.right);
                    case ConveyorKind.Right:
                        return Direction(Vector3.left);
                    default:
                        return Direction(Vector3.back);
                }
            }
        }

        Vector2Int Direction(Vector3 localDirection)
        {
            var world = transform.TransformDirection(localDirection);
            return new Vector2Int(Mathf.RoundToInt(world.x), Mathf.RoundToInt(world.z));
        }

        void Awake()
        {
            surfaceProperties = new MaterialPropertyBlock();
        }

        void OnEnable()
        {
            OfficeGame.Instance.conveyors.Add(this);
        }

        void OnDisable()
        {
            if (OfficeGame.Instance)
            {
                OfficeGame.Instance.conveyors.Remove(this);
            }
        }

        void OnTriggerEnter(Collider other)
        {
            AcceptCollision(other);
        }
        void OnTriggerStay(Collider other)
        {
            AcceptCollision(other);
        }

        void AcceptCollision(Collider other)
        {
            var item = other.GetComponentInParent<OfficeItem>();
            if (!item || item.Held || moving || load != 0 || item.consumed)
            {
                return;
            }

            if (item.resource != Resource.Drafts && item.resource != Resource.ApprovedDocuments &&
                item.resource != Resource.CompletedFiles)
            {
                return;
            }

            if (item.outputOwner)
            {
                var station = item.outputOwner;
                if (station.state.moving || station.cell != cell + InputDirection)
                {
                    return;
                }
            }
            else
            {
                // Loose documents must be released at the input arrow.
                var position = transform.InverseTransformPoint(item.transform.position);
                if (Mathf.Abs(position.x) > .4f || position.z < .2f || position.z > .55f)
                {
                    return;
                }
            }

            if (CanSend(item.resource) && item.StoreOnConveyor())
            {
                Receive(item.resource);
            }
        }

        public void Receive(Resource resource)
        {
            cargo = resource;
            load = 1;
            progress = 0;
            RefreshVisuals();
        }

        void Update()
        {
            bool running = load > 0 && !moving && CanSend(cargo);
            if (running)
            {
                offset = Mathf.Repeat(offset + Time.deltaTime * .6f, 1);
                progress = Mathf.Min(1, progress + Time.deltaTime / transferDuration);
                if (progress >= 1 && Transfer())
                {
                    load = 0;
                    progress = 0;
                    RefreshVisuals();
                }
            }

            surfaceProperties.SetFloat("_FlowOffset", offset);
            surfaceProperties.SetColor("_BaseColor", running ? new Color(0.1f, 0.55f, 1f) : new Color(0.65f, 0.15f, 0.12f));
            beltSurface.SetPropertyBlock(surfaceProperties);
        }

        bool CanSend(Resource resource)
        {
            var game = OfficeGame.Instance;
            visited.Clear();
            var current = this;
            while (visited.Add(current))
            {
                var targetCell = current.cell + current.OutputDirection;
                foreach (var station in game.stations)
                {
                    if (station.cell == targetCell)
                    {
                        return !station.state.moving && station.state.CanAccept(resource);
                    }
                }

                if (targetCell == OfficeRules.Cell(game.delivery.transform.position))
                {
                    return resource == Resource.CompletedFiles;
                }

                ConveyorBelt next = null;
                foreach (var belt in game.conveyors)
                {
                    if (belt.cell == targetCell && belt.cell + belt.InputDirection == current.cell)
                    {
                        next = belt;
                        break;
                    }
                }

                if (!next || next.moving || next.load > 0)
                {
                    return false;
                }

                current = next;
            }

            return false;
        }

        bool Transfer()
        {
            var game = OfficeGame.Instance;
            var targetCell = cell + OutputDirection;
            foreach (var belt in game.conveyors)
            {
                if (belt.cell == targetCell && belt.cell + belt.InputDirection == cell && !belt.moving && belt.load == 0)
                {
                    // Neighbor transfer changes both belt loads without creating another item.
                    belt.Receive(cargo);
                    return true;
                }
            }

            foreach (var station in game.stations)
            {
                if (station.cell == targetCell && !station.state.moving && station.state.CanAccept(cargo))
                {
                    if (!game.ConsumePhysical(cargo))
                    {
                        return false;
                    }

                    station.state.Deposit(cargo);
                    station.visuals.Refresh();
                    return true;
                }
            }

            if (targetCell == OfficeRules.Cell(game.delivery.transform.position) && cargo == Resource.CompletedFiles)
            {
                if (!game.ConsumePhysical(cargo))
                {
                    return false;
                }

                game.delivery.Deliver();
                return true;
            }

            return false;
        }

        void RefreshVisuals()
        {
            for (int i = 0; i < documentVisuals.Length; i++)
            {
                documentVisuals[i].SetActive(load > 0 && i == (int)cargo - (int)Resource.Drafts);
            }
        }

        public void Move()
        {
            OfficeGame.Instance.placement.BeginMove(this);
        }

    }
}
