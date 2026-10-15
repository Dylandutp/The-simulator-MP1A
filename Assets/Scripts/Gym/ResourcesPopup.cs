using UnityEngine;
using UnityEngine.InputSystem;

namespace HouseSim.Gym
{
    public sealed class ResourcesPopup : MonoBehaviour
    {
        [SerializeField] PopupPanel popupPanel;
        [SerializeField] InputActionReference toggleAction;

        InputAction yToggle;

        void OnEnable()
        {
            if (popupPanel == null) popupPanel = GetComponent<PopupPanel>();
            yToggle = new InputAction("ToggleResourcesY", InputActionType.Button, "<XRController>{LeftHand}/secondaryButton");
            yToggle.performed += OnTogglePerformed;
            yToggle.Enable();
        }

        void OnDisable()
        {
            if (yToggle == null) return;
            yToggle.performed -= OnTogglePerformed;
            yToggle.Disable();
            yToggle.Dispose();
            yToggle = null;
        }

        void OnTogglePerformed(InputAction.CallbackContext context) => popupPanel?.Toggle();
    }
}
