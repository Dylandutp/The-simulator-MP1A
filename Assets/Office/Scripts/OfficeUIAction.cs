using UnityEngine;

namespace OfficeSim
{
    public sealed class OfficeUIAction : MonoBehaviour
    {
        public Resource resource;
        public MachineKind machine;
        public ConveyorKind conveyor;

        public void Take()
        {
            OfficeGame.Instance.Dispense(resource);
        }

        public void Buy()
        {
            OfficeGame.Instance.Buy(resource, resource == Resource.Folders ? 2 : 1);
        }

        public void Build()
        {
            OfficeGame.Instance.placement.BeginBuild(machine);
        }

        public void BuildConveyor()
        {
            OfficeGame.Instance.placement.BeginConveyor(conveyor);
        }

    }
}
