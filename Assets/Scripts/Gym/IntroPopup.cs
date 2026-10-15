using System.Collections;
using UnityEngine;

namespace HouseSim.Gym
{
    public sealed class IntroPopup : MonoBehaviour
    {
        [SerializeField] PopupPanel popupPanel;
        [SerializeField, Min(0f)] float openDelay = 0.5f;

        bool hasPresented;

        public bool ReadyForGameplay => hasPresented && popupPanel != null && !popupPanel.IsOpen;

        IEnumerator Start()
        {
            if (openDelay > 0f) yield return new WaitForSeconds(openDelay);
            PositionInFrontOfPlayer();
            hasPresented = true;
            popupPanel?.Open();
        }

        public void CloseIntro() => popupPanel?.Close();

        void PositionInFrontOfPlayer()
        {
            Camera camera = Camera.main;
            if (camera == null) return;
            Vector3 forward = Vector3.ProjectOnPlane(camera.transform.forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < .01f) forward = camera.transform.forward;
            transform.position = camera.transform.position + forward * 1.35f;
            transform.rotation = Quaternion.LookRotation(transform.position - camera.transform.position, Vector3.up);
        }
    }
}
