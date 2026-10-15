using UnityEngine;

namespace HouseSim.Gym
{
    public sealed class MachineFirstUsePopup : MonoBehaviour
    {
        [SerializeField] PopupPanel popupPanel;

        bool hasTriggered;

        public bool IsOpen => popupPanel != null && popupPanel.IsOpen;

        public void OnMachineApproached(Transform machine)
        {
            if (hasTriggered) return;
            hasTriggered = true;
            PositionInFrontOfPlayer(machine);
            popupPanel?.Open();
        }

        void PositionInFrontOfPlayer(Transform machine)
        {
            Camera camera = Camera.main;
            if (camera == null) return;

            Vector3 forward = Vector3.ProjectOnPlane(camera.transform.forward, Vector3.up).normalized;
            if (machine != null)
            {
                Vector3 towardMachine = Vector3.ProjectOnPlane(machine.position - camera.transform.position, Vector3.up).normalized;
                if (towardMachine.sqrMagnitude > .01f) forward = towardMachine;
            }
            if (forward.sqrMagnitude < .01f) forward = Vector3.forward;
            transform.position = camera.transform.position + forward * 1.25f;
            transform.rotation = Quaternion.LookRotation(transform.position - camera.transform.position, Vector3.up);
        }
    }
}
