using UnityEngine;

namespace OfficeSim
{
    public sealed class FilingWorkstation : Workstation
    {
        public float filingDuration = 1.5f;

        protected override MachineKind Kind => MachineKind.Filer;

        void Update()
        {
            string progressName = IsAutomatic ? "Filer Cycle" : "Binding Progress";
            string folderState = state.lidClosed ? "Closed" : "Open";
            readout.text = $"{state.Status}   |   {progressName}: {state.progress:P0}\n" +
                $"Input: {state.input}/5    OUT: {state.outputCount}/5\n" +
                $"Folders: {state.folders}/20   Folder: {folderState}";
        }

        public void CloseFolder()
        {
            if (state.HasSupplies || state.active)
            {
                state.lidClosed = true;
            }
        }

        protected override void UseTool(OfficeTool tool)
        {
            if (!state.lidClosed)
            {
                return;
            }

            if (state.Advance(Time.fixedDeltaTime / filingDuration))
            {
                Produce();
                toolCooldown = Time.time + 1;
            }
        }
    }
}
