using UnityEngine;

namespace OfficeSim
{
    public sealed class WritingWorkstation : Workstation
    {
        public float writingDistance = 1.8f;

        private Vector3 previousTip;
        private bool touchingPaper;
        protected override MachineKind Kind => MachineKind.Writer;

        void Update()
        {
            string progressName = IsAutomatic ? "Writer Cycle" : "Writing Progress";
            string supplyName = IsAutomatic ? "Writer Paper" : "Paper";
            readout.text = $"{state.Status}   |   {progressName}: {state.progress:P0}\n" +
                $"{supplyName}: {state.input}/5    OUT: {state.outputCount}/5\n" +
                $"Pen Ink: {state.ink}/20";
        }

        protected override void UseTool(OfficeTool tool)
        {
            Vector3 position = tool.tip.position;
            if (!touchingPaper)
            {
                previousTip = position;
                touchingPaper = true;
                return;
            }

            float distance = Mathf.Min(Vector3.Distance(position, previousTip), .04f);
            previousTip = position;
            if (state.Advance(distance / writingDistance))
            {
                Produce();
                toolCooldown = Time.time + .6f;
            }
        }

        protected override void ResetContact()
        {
            touchingPaper = false;
        }

        public override void ExitContact(OfficeTool tool)
        {
            if (tool == currentTool)
            {
                touchingPaper = false;
            }
        }
    }
}
