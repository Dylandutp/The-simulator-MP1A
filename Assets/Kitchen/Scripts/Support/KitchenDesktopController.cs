using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace HouseSim.Kitchen
{
    /// <summary>Mouse/keyboard preview fallback. Real XR controls take priority when a headset is active.</summary>
    public sealed class KitchenDesktopController : MonoBehaviour
    {
        public Camera viewCamera;
        public Transform playerRoot;
        public float moveSpeed = 2.3f;
        public bool autoDetectXR = true;
        public bool desktopEnabled = true;
        public float lookSensitivity = 0.12f;
        public float interactionDistance = 3f;
        public float holdDistance = 1.05f;
        public Transform holdAnchor;
        public LayerMask interactionMask = ~0;
        public bool showHelp = true;
        public bool createCharacterController = true;

        readonly Dictionary<Behaviour, bool> poseDrivers = new Dictionary<Behaviour, bool>();
        CharacterController motor;
        Rigidbody heldBody;
        XRGrabInteractable heldGrab;
        KitchenFood heldFood;
        KitchenDesktopHeld heldMarker;
        Quaternion heldViewRotation;
        bool keepHeldUpright;
        bool heldWasKinematic;
        bool heldUsedGravity;
        CollisionDetectionMode heldCollisionMode;
        Vector3 heldVelocity;
        Vector3 lastHeldPosition;
        float heldRadius;
        float yaw;
        float pitch;
        float fallSpeed;
        bool activeDesktop;
        bool initialized;
        string focusedLabel = "";
        GUIStyle helpStyle;
        GUIStyle crosshairStyle;

        public bool IsDesktopActive => activeDesktop;
        public GameObject HeldObject => heldGrab == null ? null : heldGrab.gameObject;

        void Start()
        {
            if (viewCamera == null) viewCamera = Camera.main;
            if (viewCamera == null) { enabled = false; return; }
            if (playerRoot == null)
                playerRoot = viewCamera.transform.parent != null ? viewCamera.transform.parent : viewCamera.transform;
            motor = playerRoot.GetComponent<CharacterController>();
            yaw = playerRoot.eulerAngles.y;
            pitch = NormalizeAngle(viewCamera.transform.localEulerAngles.x);
            initialized = true;
            SetDesktopMode(ShouldUseDesktop());
        }

        bool ShouldUseDesktop()
        {
            if (!desktopEnabled) return false;
            if (!autoDetectXR) return true;
            bool activeHeadset = XRSettings.isDeviceActive;
            // A loaded provider alone does not imply that an HMD is connected.
            if (!activeHeadset && XRSettings.enabled && !string.IsNullOrEmpty(XRSettings.loadedDeviceName))
                activeHeadset = UnityEngine.XR.InputDevices.GetDeviceAtXRNode(XRNode.Head).isValid;
            return !activeHeadset;
        }

        void SetDesktopMode(bool useDesktop)
        {
            if (activeDesktop == useDesktop) return;
            activeDesktop = useDesktop;
            if (!useDesktop)
            {
                Release(false);
                foreach (var pair in poseDrivers)
                    if (pair.Key != null) pair.Key.enabled = pair.Value;
                poseDrivers.Clear();
                UnlockCursor();
                return;
            }

            // Input System and legacy tracked-pose drivers both have this type name.
            // Only drivers attached to the desktop camera are altered and restored on XR activation.
            foreach (var component in viewCamera.GetComponents<Behaviour>())
            {
                if (component.GetType().Name != "TrackedPoseDriver") continue;
                poseDrivers[component] = component.enabled;
                component.enabled = false;
            }
            if (motor == null && createCharacterController)
            {
                motor = playerRoot.gameObject.AddComponent<CharacterController>();
                motor.height = 1.7f;
                motor.radius = 0.22f;
                motor.center = new Vector3(0f, 0.85f, 0f);
                motor.stepOffset = 0.2f;
                motor.skinWidth = 0.025f;
            }
            yaw = playerRoot.eulerAngles.y;
            pitch = NormalizeAngle(viewCamera.transform.localEulerAngles.x);
        }

        void Update()
        {
            if (!initialized || viewCamera == null) return;
            SetDesktopMode(ShouldUseDesktop());
            if (!activeDesktop) return;

            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;
            if (keyboard == null || mouse == null) return;
            if (keyboard.escapeKey.wasPressedThisFrame) UnlockCursor();
            UpdateLook(mouse);
            UpdateMovement(keyboard);

            RaycastHit hit;
            bool hasTarget = TryAim(out hit);
            focusedLabel = hasTarget ? Describe(hit.collider) : "";
            if (mouse.leftButton.wasPressedThisFrame)
            {
                if (heldGrab != null) Release(true);
                else if (hasTarget) TryGrab(hit.collider);
            }
            if (keyboard.eKey.wasPressedThisFrame && hasTarget) Interact(hit.collider);
            if (keyboard.fKey.wasPressedThisFrame && heldFood != null) heldFood.TryConsume();
            if (keyboard.cKey.wasPressedThisFrame && heldFood != null) heldFood.Chop();
            if (keyboard.rKey.wasPressedThisFrame && heldBody != null) LevelHeldObject();

            float wheel = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(wheel) > 0.01f)
            {
                KitchenKnob knob = hasTarget ? hit.collider.GetComponentInParent<KitchenKnob>() : null;
                if (knob != null) knob.Adjust(Mathf.Sign(wheel) * KnobStep(knob));
                else if (heldGrab != null) holdDistance = Mathf.Clamp(holdDistance + Mathf.Sign(wheel) * 0.1f, 0.45f, 1.8f);
            }
        }

        void UpdateLook(Mouse mouse)
        {
            if (!mouse.rightButton.isPressed)
            {
                if (mouse.rightButton.wasReleasedThisFrame) UnlockCursor();
                return;
            }
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            Vector2 delta = mouse.delta.ReadValue();
            yaw += delta.x * lookSensitivity;
            pitch = Mathf.Clamp(pitch - delta.y * lookSensitivity, -75f, 75f);
            if (playerRoot == viewCamera.transform)
                playerRoot.rotation = Quaternion.Euler(pitch, yaw, 0f);
            else
            {
                playerRoot.rotation = Quaternion.Euler(0f, yaw, 0f);
                viewCamera.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
            }
        }

        void UpdateMovement(Keyboard keyboard)
        {
            float x = (keyboard.dKey.isPressed ? 1f : 0f) - (keyboard.aKey.isPressed ? 1f : 0f);
            float z = (keyboard.wKey.isPressed ? 1f : 0f) - (keyboard.sKey.isPressed ? 1f : 0f);
            Vector3 forward = Vector3.ProjectOnPlane(viewCamera.transform.forward, Vector3.up).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            Vector3 movement = Vector3.ClampMagnitude(forward * z + right * x, 1f) * moveSpeed;
            if (motor != null && motor.enabled)
            {
                fallSpeed = motor.isGrounded ? -1f : Mathf.Max(fallSpeed - 9.81f * Time.deltaTime, -20f);
                movement.y = fallSpeed;
                motor.Move(movement * Time.deltaTime);
            }
            else playerRoot.position += movement * Time.deltaTime;
        }

        bool TryAim(out RaycastHit nearest)
        {
            nearest = default;
            Ray ray = viewCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f));
            RaycastHit[] hits = Physics.RaycastAll(ray, interactionDistance, interactionMask, QueryTriggerInteraction.Ignore);
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (RaycastHit hit in hits)
            {
                if (IsOwnCollider(hit.collider)) continue;
                nearest = hit;
                return true;
            }
            return false;
        }

        bool IsOwnCollider(Collider collider)
        {
            if (collider.transform.IsChildOf(playerRoot)) return true;
            return heldGrab != null && collider.transform.IsChildOf(heldGrab.transform);
        }

        void Interact(Collider target)
        {
            KitchenAction action = target.GetComponentInParent<KitchenAction>();
            if (action != null) { action.Invoke(); return; }
            KitchenDoor door = target.GetComponentInParent<KitchenDoor>();
            if (door != null) { door.Toggle(); return; }
            KitchenToggle toggle = target.GetComponentInParent<KitchenToggle>();
            if (toggle != null) { toggle.Toggle(); return; }
            KitchenKnob knob = target.GetComponentInParent<KitchenKnob>();
            if (knob != null) knob.Adjust(KnobStep(knob));
        }

        string Describe(Collider target)
        {
            KitchenAction action = target.GetComponentInParent<KitchenAction>();
            if (action != null) return "E: " + action.label;
            if (target.GetComponentInParent<KitchenDoor>() != null) return "E: Open / close door";
            if (target.GetComponentInParent<KitchenKnob>() != null) return "Mouse wheel: turn knob   |   E: increase";
            if (target.GetComponentInParent<KitchenToggle>() != null) return "E: Switch on / off";
            if (target.GetComponentInParent<XRGrabInteractable>() != null) return "Left click: pick up";
            return "";
        }

        void TryGrab(Collider target)
        {
            XRGrabInteractable grab = target.GetComponentInParent<XRGrabInteractable>();
            if (grab == null || !grab.enabled || grab.isSelected) return;
            Rigidbody body = grab.GetComponent<Rigidbody>();
            if (body == null) return;
            heldGrab = grab;
            heldBody = body;
            heldFood = grab.GetComponent<KitchenFood>();
            heldMarker = grab.GetComponent<KitchenDesktopHeld>();
            if (heldMarker == null) heldMarker = grab.gameObject.AddComponent<KitchenDesktopHeld>();
            heldMarker.SetHeld(true);
            heldFood?.SetDesktopHeld(true);
            heldWasKinematic = body.isKinematic;
            heldUsedGravity = body.useGravity;
            heldCollisionMode = body.collisionDetectionMode;
            if (!body.isKinematic)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }
            body.isKinematic = true;
            body.useGravity = false;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            heldViewRotation = Quaternion.Inverse(viewCamera.transform.rotation) * body.rotation;
            keepHeldUpright = false;
            heldRadius = Mathf.Clamp(target.bounds.extents.magnitude * 0.45f, 0.055f, 0.2f);
            lastHeldPosition = body.position;
            heldVelocity = Vector3.zero;
            KitchenSession.Instance?.Tutorial("desktop_grab", "Desktop: left click releases. F eats. C cuts. R levels a tipped tool.\nScroll changes reach unless aiming at a knob.", grab.transform.position + Vector3.up * 0.2f);
        }

        void FixedUpdate()
        {
            if (!activeDesktop || heldBody == null || heldGrab == null) return;
            Transform eye = viewCamera.transform;
            Vector3 goal = holdAnchor != null ? holdAnchor.position : eye.position + eye.forward * holdDistance - eye.up * 0.16f;
            Vector3 displacement = goal - eye.position;
            float length = displacement.magnitude;
            if (length > 0.01f)
            {
                RaycastHit[] hits = Physics.SphereCastAll(eye.position, heldRadius, displacement / length, length, interactionMask, QueryTriggerInteraction.Ignore);
                float allowed = length;
                foreach (RaycastHit hit in hits)
                {
                    if (!IsOwnCollider(hit.collider)) allowed = Mathf.Min(allowed, Mathf.Max(0.15f, hit.distance - 0.025f));
                }
                goal = eye.position + displacement / length * allowed;
            }
            Vector3 nextPosition = Vector3.MoveTowards(heldBody.position, goal, 8f * Time.fixedDeltaTime);
            Vector3 travel = nextPosition - heldBody.position;
            if (travel.sqrMagnitude > 0.000001f)
            {
                float allowedTravel = travel.magnitude;
                foreach (RaycastHit hit in Physics.SphereCastAll(heldBody.position, heldRadius, travel.normalized, allowedTravel, interactionMask, QueryTriggerInteraction.Ignore))
                {
                    // Ignore zero-distance starting overlaps so a stocked item can leave its shelf.
                    if (IsOwnCollider(hit.collider) || hit.distance <= 0.001f) continue;
                    allowedTravel = Mathf.Min(allowedTravel, Mathf.Max(0f, hit.distance - 0.005f));
                }
                nextPosition = heldBody.position + travel.normalized * allowedTravel;
            }
            heldVelocity = (nextPosition - lastHeldPosition) / Time.fixedDeltaTime;
            lastHeldPosition = nextPosition;
            heldBody.MovePosition(nextPosition);
            Quaternion viewRotation = keepHeldUpright ? Quaternion.Euler(0f, eye.eulerAngles.y, 0f) : eye.rotation;
            heldBody.MoveRotation(Quaternion.Slerp(heldBody.rotation, viewRotation * heldViewRotation, 14f * Time.fixedDeltaTime));
        }

        /// <summary>Recovers a tipped pan or plate using one desktop input, then keeps it level while aiming.</summary>
        public void LevelHeldObject()
        {
            if (heldBody == null || viewCamera == null) return;
            Quaternion cameraYaw = Quaternion.Euler(0f, viewCamera.transform.eulerAngles.y, 0f);
            heldViewRotation = Quaternion.Inverse(cameraYaw) * Quaternion.Euler(0f, heldBody.rotation.eulerAngles.y, 0f);
            keepHeldUpright = true;
        }

        public void Release(bool toss = false)
        {
            if (heldFood != null) heldFood.SetDesktopHeld(false);
            if (heldMarker != null) heldMarker.SetHeld(false);
            if (heldBody != null)
            {
                heldBody.isKinematic = heldWasKinematic;
                heldBody.useGravity = heldUsedGravity;
                heldBody.collisionDetectionMode = heldCollisionMode;
                if (!heldBody.isKinematic)
                {
                    heldBody.linearVelocity = toss && viewCamera != null
                        ? Vector3.ClampMagnitude(heldVelocity * 0.2f + viewCamera.transform.forward * 0.35f, 1.4f)
                        : Vector3.zero;
                    heldBody.angularVelocity = Vector3.zero;
                }
            }
            heldGrab = null;
            heldBody = null;
            heldFood = null;
            heldMarker = null;
            keepHeldUpright = false;
        }

        void OnDisable()
        {
            if (activeDesktop) SetDesktopMode(false);
            else UnlockCursor();
        }

        void OnApplicationFocus(bool focus)
        {
            if (!focus) UnlockCursor();
        }

        static float NormalizeAngle(float angle) => angle > 180f ? angle - 360f : angle;
        static float KnobStep(KitchenKnob knob) => knob.step > 0f ? knob.step : Mathf.Max((knob.maxValue - knob.minValue) * 0.05f, 0.01f);
        static void UnlockCursor()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        void OnGUI()
        {
            if (!activeDesktop || !showHelp) return;
            if (helpStyle == null)
            {
                helpStyle = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.MiddleLeft, fontSize = 15, padding = new RectOffset(12, 12, 8, 8) };
                helpStyle.normal.textColor = Color.white;
                crosshairStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 23 };
                crosshairStyle.normal.textColor = Color.white;
            }
            float width = Mathf.Min(Screen.width - 24, 850f);
            GUI.Box(new Rect(12, Screen.height - 91, width, 79), "PC CONTROLS  |  WASD move  |  Hold right mouse to look\nLeft click grab / drop  |  E use  |  Wheel knob / reach  |  F eat  |  C cut  |  R level\n" + (heldGrab != null ? "Holding: " + heldGrab.name + "   " : "") + focusedLabel, helpStyle);
            GUI.Label(new Rect(Screen.width * 0.5f - 12f, Screen.height * 0.5f - 15f, 24f, 30f), "+", crosshairStyle);
        }
    }
}
