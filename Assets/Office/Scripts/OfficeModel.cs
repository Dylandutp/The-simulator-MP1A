using System;
using UnityEngine;

namespace OfficeSim
{
    public enum Resource
    {
        Cash,
        Paper,
        Ink,
        Folders,
        Drafts,
        ApprovedDocuments,
        CompletedFiles,
        StampPads
    }

    public enum MachineKind
    {
        Writer,
        Stamper,
        Filer
    }

    public enum ToolKind
    {
        Pen,
        Stamp,
        Stapler
    }

    public static class OfficeRules
    {
        public const float CellSize = 1.2f;
        public const int Capacity = 5;
        public const int Radius = 4;

        public static string Label(Resource resource)
        {
            switch (resource)
            {
                case Resource.ApprovedDocuments:
                    return "Approved Documents";
                case Resource.CompletedFiles:
                    return "Completed Files";
                case Resource.StampPads:
                    return "Stamp Pads";
                default:
                    return resource.ToString();
            }
        }

        public static Resource Input(MachineKind kind)
        {
            switch (kind)
            {
                case MachineKind.Writer:
                    return Resource.Paper;
                case MachineKind.Stamper:
                    return Resource.Drafts;
                default:
                    return Resource.ApprovedDocuments;
            }
        }

        public static Resource Output(MachineKind kind)
        {
            switch (kind)
            {
                case MachineKind.Writer:
                    return Resource.Drafts;
                case MachineKind.Stamper:
                    return Resource.ApprovedDocuments;
                default:
                    return Resource.CompletedFiles;
            }
        }

        public static int Price(MachineKind kind)
        {
            switch (kind)
            {
                case MachineKind.Writer:
                    return 50;
                case MachineKind.Stamper:
                    return 70;
                default:
                    return 90;
            }
        }

        public static Vector3 Position(Vector2Int cell)
        {
            return new Vector3(cell.x * CellSize, 0, cell.y * CellSize);
        }

        public static Vector2Int Cell(Vector3 position)
        {
            return new Vector2Int(Mathf.RoundToInt(position.x / CellSize), Mathf.RoundToInt(position.z / CellSize));
        }

        public static bool Inside(Vector2Int cell)
        {
            return Mathf.Abs(cell.x) <= Radius && Mathf.Abs(cell.y) <= Radius;
        }
    }

    [Serializable]
    public class ResourceInventory
    {
        [SerializeField]
        private int[] values = new int[8];
        public int this[Resource resource] => values[(int)resource];

        public void Add(Resource resource, int amount)
        {
            values[(int)resource] += amount;
        }

        public bool Spend(Resource resource, int amount)
        {
            if (this[resource] < amount)
            {
                return false;
            }

            values[(int)resource] -= amount;
            return true;
        }

        public void Initialize()
        {
            Array.Clear(values, 0, values.Length);
            Add(Resource.Paper, 10);
            Add(Resource.Ink, 20);
            Add(Resource.Folders, 10);
            Add(Resource.StampPads, 10);
        }
    }

    // Inputs belong to the machine once deposited. Outputs become owned physical items
    // only on completion. Moving a station keeps its current progress and inputs.
    [Serializable]
    public class MachineState
    {
        public MachineKind kind;
        public bool automatic;
        public bool moving;
        public int input;
        public int ink;
        public int stampPads;
        public int folders;
        public int outputCount;
        [Range(0, 1)]
        public float progress;
        public bool active;
        public bool lidClosed;
        public bool HasSupplies
        {
            get
            {
                if (input == 0)
                {
                    return false;
                }

                switch (kind)
                {
                    case MachineKind.Writer:
                        return ink > 0;
                    case MachineKind.Stamper:
                        return stampPads > 0;
                    default:
                        return folders > 0;
                }
            }
        }

        public string Status
        {
            get
            {
                if (moving)
                {
                    return "Moving";
                }
                if (outputCount >= OfficeRules.Capacity)
                {
                    return "Output Full";
                }
                if (active)
                {
                    return "Processing";
                }
                if (input == 0)
                {
                    return kind == MachineKind.Writer ? "Needs Paper" : "Waiting for Input";
                }
                if (kind == MachineKind.Writer && ink == 0)
                {
                    return "Needs Ink";
                }
                if (kind == MachineKind.Stamper && stampPads == 0)
                {
                    return "Needs Stamp Pads";
                }
                if (kind == MachineKind.Filer && folders == 0)
                {
                    return "Needs Folders";
                }

                return "Ready";
            }
        }

        public bool CanAccept(Resource resource)
        {
            if (resource == OfficeRules.Input(kind))
            {
                return input < OfficeRules.Capacity;
            }

            switch (resource)
            {
                case Resource.Ink:
                    return kind == MachineKind.Writer && ink < 20;
                case Resource.StampPads:
                    return kind == MachineKind.Stamper && stampPads < 20;
                case Resource.Folders:
                    return kind == MachineKind.Filer && folders < 20;
                default:
                    return false;
            }
        }

        public void Deposit(Resource resource)
        {
            if (resource == OfficeRules.Input(kind))
            {
                input++;
            }
            else if (resource == Resource.Ink)
            {
                ink++;
            }
            else if (resource == Resource.StampPads)
            {
                stampPads++;
            }
            else
            {
                folders++;
            }
        }

        public bool Advance(float amount)
        {
            if (moving || outputCount >= OfficeRules.Capacity || amount <= 0)
            {
                return false;
            }

            if (!active)
            {
                if (!HasSupplies)
                {
                    return false;
                }

                input--;
                if (kind == MachineKind.Filer)
                {
                    folders--;
                }
                else if (kind == MachineKind.Stamper)
                {
                    stampPads--;
                }
                else
                {
                    ink--;
                }

                active = true;
            }

            progress = Mathf.Min(1, progress + amount);
            if (progress < 1)
            {
                return false;
            }

            active = false;
            progress = 0;
            outputCount++;
            lidClosed = false;
            return true;
        }
    }
}
