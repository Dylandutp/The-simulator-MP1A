using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using Unity.XR.CoreUtils;

namespace HouseSim.Kitchen
{
    // Face buttons adjust an existing grab; thumbsticks remain reserved for locomotion.
    public sealed class KitchenHeldControllerInput : MonoBehaviour
    {
        public float reachSpeed = .8f;
        public float minimumReach = .06f;
        public float maximumReach = 3f;
        XRBaseInteractor[] interactors;
        InputAction pull, push, eat;

        public void Configure(XROrigin origin)
        {
            Clear();
            interactors = origin.GetComponentsInChildren<XRBaseInteractor>(true);
            pull = new InputAction("Kitchen pull held item", InputActionType.Button, "<XRController>{RightHand}/{PrimaryButton}");
            push = new InputAction("Kitchen push held item", InputActionType.Button, "<XRController>{RightHand}/{SecondaryButton}");
            eat = new InputAction("Kitchen eat held food", InputActionType.Button, "<XRController>{LeftHand}/{PrimaryButton}");
            if (isActiveAndEnabled) EnableActions();
        }

        void OnEnable() => EnableActions();
        void EnableActions() { pull?.Enable(); push?.Enable(); eat?.Enable(); }
        void OnDisable() { pull?.Disable(); push?.Disable(); eat?.Disable(); }
        void OnDestroy() => Clear();

        public void Clear()
        {
            pull?.Dispose(); push?.Dispose(); eat?.Dispose();
            pull = push = eat = null;
            interactors = null;
        }

        void Update()
        {
            if (interactors == null || eat == null) return;
            if (eat.WasPressedThisFrame()) EatHeldFood();
            float direction = (push.IsPressed() ? 1 : 0) - (pull.IsPressed() ? 1 : 0);
            if (direction != 0) AdjustHeldReach(direction * reachSpeed * Time.deltaTime);
        }

        void EatHeldFood()
        {
            // Match A/B hand priority, without imposing a head/hand distance limit.
            var food = FindHeldFood(InteractorHandedness.Right) ?? FindHeldFood(InteractorHandedness.Left);
            if (food) food.TryConsume();
        }

        KitchenFood FindHeldFood(InteractorHandedness hand)
        {
            foreach (var interactor in interactors)
            {
                if (!interactor || !interactor.isActiveAndEnabled || interactor.handedness != hand || !interactor.hasSelection) continue;
                foreach (var selected in interactor.interactablesSelected)
                {
                    var food = selected.transform.GetComponent<KitchenFood>();
                    if (food && !food.IsConsumed && food.IsHeld) return food;
                }
            }
            return null;
        }

        void AdjustHeldReach(float amount)
        {
            // The right-hand item takes priority; A/B can also position a left-hand item
            // when the right hand is free. Doors and knobs are never translated.
            var selected = FindHeldGrab(InteractorHandedness.Right) ?? FindHeldGrab(InteractorHandedness.Left);
            if (!selected) return;
            var grab = (XRGrabInteractable)selected.firstInteractableSelected;
            var anchor = selected.GetAttachTransform(grab);
            Transform hand = selected.transform;
            if (selected is NearFarInteractor nearFar)
                hand = nearFar.interactionAttachController.transformToFollow;
            else if (selected is XRRayInteractor ray && ray.rayOriginTransform)
                hand = ray.rayOriginTransform;
            if (!hand || !anchor) return;
            Vector3 offset = anchor.position - hand.position;
            float distance = offset.magnitude;
            Vector3 direction = distance > .001f ? offset / distance : hand.forward;
            float next = Mathf.Clamp(distance + amount, minimumReach, Mathf.Max(maximumReach, distance));
            Vector3 target = hand.position + direction * next;
            if (selected is NearFarInteractor near)
                near.interactionAttachController.MoveTo(target);
            else
                anchor.position = target;
        }

        XRBaseInteractor FindHeldGrab(InteractorHandedness hand)
        {
            foreach (var interactor in interactors)
                if (interactor && interactor.isActiveAndEnabled && interactor.handedness == hand &&
                    interactor.hasSelection && interactor.firstInteractableSelected is XRGrabInteractable &&
                    (interactor is NearFarInteractor || interactor is XRRayInteractor)) return interactor;
            return null;
        }
    }
}
