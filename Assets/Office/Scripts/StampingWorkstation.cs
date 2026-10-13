using UnityEngine;

namespace OfficeSim
{
    public sealed class StampingWorkstation : Workstation
    {
        public float stampingDuration = 1.2f;

        protected override MachineKind Kind => MachineKind.Stamper;

        void Update()
        {
            string progressName = IsAutomatic ? "Stamper Cycle" : "Stamp Progress";
            readout.text = $"{state.Status}   |   {progressName}: {state.progress:P0}\n" +
                $"Input: {state.input}/5    OUT: {state.outputCount}/5\n" +
                $"Stamp Pads: {state.stampPads}/20";
        }

        protected override void UseTool(OfficeTool tool)
        {
            if (state.Advance(Time.fixedDeltaTime / stampingDuration))
            {
                Produce();
                toolCooldown = Time.time + 1;
            }
        }
    }
}
