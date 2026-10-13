using UnityEngine;

namespace OfficeSim
{
    public class WorkPad : MonoBehaviour
    {
        public Workstation station;

        void OnTriggerStay(Collider other)
        {
            var tool = other.GetComponentInParent<OfficeTool>();
            if (tool)
            {
                station.ManualContact(tool);
            }
        }

        void OnTriggerExit(Collider other)
        {
            var tool = other.GetComponentInParent<OfficeTool>();
            if (tool)
            {
                station.ExitContact(tool);
            }
        }
    }
}
