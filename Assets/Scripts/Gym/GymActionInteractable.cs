using System;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace HouseSim.Gym
{
    [RequireComponent(typeof(Collider))]
    public class GymActionInteractable : MonoBehaviour
    {
        private Action action;
        private Action releaseAction;
        private XRBaseInteractable xrInteractable;

        public void Configure(Action onInteract, Action onRelease = null, bool allowGrab = true)
        {
            action = onInteract;
            releaseAction = onRelease;
            xrInteractable = allowGrab
                ? GetComponent<XRGrabInteractable>()
                : GetComponent<XRSimpleInteractable>();

            if (xrInteractable == null)
            {
                xrInteractable = allowGrab
                    ? gameObject.AddComponent<XRGrabInteractable>()
                    : gameObject.AddComponent<XRSimpleInteractable>();
            }

            xrInteractable.selectEntered.RemoveListener(HandleSelectEntered);
            xrInteractable.selectExited.RemoveListener(HandleSelectExited);
            xrInteractable.selectEntered.AddListener(HandleSelectEntered);
            xrInteractable.selectExited.AddListener(HandleSelectExited);
        }

        private void HandleSelectEntered(SelectEnterEventArgs args) => action?.Invoke();
        private void HandleSelectExited(SelectExitEventArgs args) => releaseAction?.Invoke();

        private void OnDestroy()
        {
            if (xrInteractable != null)
            {
                xrInteractable.selectEntered.RemoveListener(HandleSelectEntered);
                xrInteractable.selectExited.RemoveListener(HandleSelectExited);
            }
        }

        private void OnMouseDown() => action?.Invoke();
    }
}
