using UnityEngine;
using Unity.XR.CoreUtils;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Gravity;

namespace HouseSim.Kitchen
{
    /// <summary>Recovers the complete player rig without altering the tracked head's local pose.</summary>
    public sealed class KitchenPlayerRecovery : MonoBehaviour
    {
        public XROrigin origin;
        public Transform spawn;
        public float belowFloor = -3f;
        public int RecoveryCount { get; private set; }

        private void LateUpdate()
        {
            if (origin && spawn && (origin.transform.position.y < belowFloor ||
                (origin.Camera && origin.Camera.transform.position.y < belowFloor))) RecoverNow();
        }

        public void RecoverNow()
        {
            if (!origin || !spawn) return;
            // End grabs before moving the rig: otherwise the next tracked-hand sample
            // would rotate a held handle by the teleport displacement.
            foreach (var interactor in origin.GetComponentsInChildren<XRBaseInteractor>(true))
                if (interactor.interactionManager && interactor is IXRSelectInteractor selector)
                    interactor.interactionManager.CancelInteractorSelection(selector);
            foreach (var gravity in origin.GetComponentsInChildren<GravityProvider>(true))
                gravity.ResetFallForce();
            var motor = origin.GetComponent<CharacterController>();
            bool restoreMotor = motor && motor.enabled;
            if (restoreMotor) motor.enabled = false;
            var desktop = origin.GetComponent<KitchenDesktopController>();
            origin.transform.rotation = spawn.rotation;
            // Account for room-scale X/Z displacement so the head, not only the rig origin,
            // arrives safely over the pad. Preserve the user's standing or seated height.
            Vector3 trackedOffset = origin.Camera ? origin.Camera.transform.position - origin.transform.position : Vector3.zero;
            origin.transform.position = spawn.position - new Vector3(trackedOffset.x,0,trackedOffset.z);
            if (desktop) desktop.ResetMotion();
            foreach (var body in origin.GetComponentsInChildren<Rigidbody>())
                if (!body.isKinematic) { body.linearVelocity = Vector3.zero; body.angularVelocity = Vector3.zero; }
            Physics.SyncTransforms();
            if (restoreMotor) motor.enabled = true;
            RecoveryCount++;
        }
    }
}
