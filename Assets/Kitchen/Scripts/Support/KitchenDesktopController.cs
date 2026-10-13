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
        KitchenKnob draggedKnob;
        KitchenDoor draggedDoor;
        KitchenToggle draggedTap;
        Transform draggedPivot;
        Vector3 controlLocalHit;
        Vector2 previousPointer;
        bool knobRadialReady;

        public bool IsDesktopActive => activeDesktop;
        public GameObject HeldObject => heldGrab == null ? null : heldGrab.gameObject;
        public bool IsDraggingControl => draggedKnob != null || draggedDoor != null || draggedTap != null;

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
                EndControlDrag();
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
            UnlockCursor();
        }

        void Update()
        {
            if (!initialized || viewCamera == null) return;
            SetDesktopMode(ShouldUseDesktop());
            if (!activeDesktop) return;

            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;
            if (keyboard == null || mouse == null) return;
            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                EndControlDrag();
                UnlockCursor();
            }
            if (IsDraggingControl)
            {
                UnlockCursor();
                if (!mouse.leftButton.isPressed) EndControlDrag();
                else UpdateControlDrag(mouse.position.ReadValue());
                return;
            }
            UpdateLook(mouse);
            UpdateMovement(keyboard);

            RaycastHit hit;
            bool hasTarget = TryAim(out hit);
            focusedLabel = hasTarget ? Describe(hit.collider) : "";
            if (mouse.leftButton.wasPressedThisFrame)
            {
                if (heldGrab != null) Release(true);
                else if (hasTarget && !TryBeginControlDrag(hit, PointerPosition(mouse)))
                {
                    if (!Interact(hit.collider)) TryGrab(hit.collider);
                }
            }
            if (keyboard.eKey.wasPressedThisFrame && hasTarget) Interact(hit.collider);
            if (keyboard.fKey.wasPressedThisFrame && heldFood != null) heldFood.TryConsume();
            if (keyboard.cKey.wasPressedThisFrame && heldFood != null) heldFood.Chop();
            if (keyboard.rKey.wasPressedThisFrame && heldBody != null) LevelHeldObject();

            float wheel = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(wheel) > 0.01f && heldGrab != null)
                holdDistance = Mathf.Clamp(holdDistance + Mathf.Sign(wheel) * 0.1f, 0.45f, 1.8f);
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
            Ray ray = viewCamera.ScreenPointToRay(PointerPosition(Mouse.current));
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

        static Vector2 PointerPosition(Mouse mouse)
        {
            if (Cursor.lockState == CursorLockMode.Locked || mouse == null)
                return new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            return mouse.position.ReadValue();
        }

        // Match the XR collider list: a door panel must not act as its handle.
        static bool IsControlCollider(Component control, Collider target)
        {
            if (control == null) return false;
            XRSimpleInteractable interactable = control.GetComponent<XRSimpleInteractable>();
            return interactable != null && interactable.isActiveAndEnabled && !interactable.isSelected
                && interactable.colliders.Contains(target);
        }

        bool Interact(Collider target)
        {
            KitchenAction action = target.GetComponentInParent<KitchenAction>();
            if (!IsControlCollider(action, target)) return false;
            action.Invoke();
            return true;
        }

        string Describe(Collider target)
        {
            KitchenAction action = target.GetComponentInParent<KitchenAction>();
            if (IsControlCollider(action, target)) return "Click / E: " + action.label;
            if (IsControlCollider(target.GetComponentInParent<KitchenDoor>(), target)) return "Hold left mouse on handle and pull";
            if (IsControlCollider(target.GetComponentInParent<KitchenKnob>(), target)) return "Hold left mouse and circle clockwise / counterclockwise";
            if (IsControlCollider(target.GetComponentInParent<KitchenToggle>(), target)) return "Hold left mouse on tap lever and drag";
            if (target.GetComponentInParent<XRGrabInteractable>() != null) return "Left click: pick up";
            return "";
        }

        bool TryBeginControlDrag(RaycastHit hit, Vector2 pointer)
        {
            KitchenKnob knob = hit.collider.GetComponentInParent<KitchenKnob>();
            KitchenDoor door = hit.collider.GetComponentInParent<KitchenDoor>();
            KitchenToggle tap = hit.collider.GetComponentInParent<KitchenToggle>();
            if (IsControlCollider(knob, hit.collider))
            {
                draggedKnob = knob;
                draggedPivot = knob.Pivot;
                knob.BeginDrag(hit.point);
                focusedLabel = "Turning knob: circle clockwise to increase; release to stop";
            }
            else if (IsControlCollider(door, hit.collider))
            {
                draggedDoor = door;
                draggedPivot = door.Pivot;
                door.BeginDrag(hit.point);
                focusedLabel = "Pull the handle along its hinge arc; release to stop";
            }
            else if (IsControlCollider(tap, hit.collider))
            {
                draggedTap = tap;
                draggedPivot = tap.Pivot;
                tap.BeginDrag(hit.point);
                focusedLabel = "Drag the tap lever along its hinge arc; release to stop";
            }
            else return false;

            if (draggedPivot == null) { EndControlDrag(); return false; }
            controlLocalHit = draggedPivot.InverseTransformPoint(hit.point);
            previousPointer = pointer;
            Vector3 center = viewCamera.WorldToScreenPoint(draggedPivot.position);
            knobRadialReady = (pointer - (Vector2)center).sqrMagnitude >= 16f;
            bool wasLocked = Cursor.lockState == CursorLockMode.Locked;
            UnlockCursor();
            if (wasLocked) Mouse.current?.WarpCursorPosition(pointer);
            return true;
        }

        void UpdateControlDrag(Vector2 pointer)
        {
            if (draggedPivot == null) { EndControlDrag(); return; }
            if ((pointer - previousPointer).sqrMagnitude < 0.0001f) return;
            if (draggedKnob != null)
            {
                Vector3 center3 = viewCamera.WorldToScreenPoint(draggedPivot.position);
                Vector2 center = center3;
                Vector2 before = previousPointer - center;
                Vector2 after = pointer - center;
                // Screen-space circles stay well conditioned even when the knob face is oblique.
                // Crossing the center only reanchors the gesture; it never jumps half a turn.
                if (center3.z > 0f && knobRadialReady && before.sqrMagnitude >= 16f && after.sqrMagnitude >= 16f
                    && DistanceToSegmentSquared(center, previousPointer, pointer) >= 9f)
                    draggedKnob.DragByDegrees(Mathf.Clamp(-Vector2.SignedAngle(before, after), -45f, 45f));
                knobRadialReady = after.sqrMagnitude >= 16f;
            }
            else
            {
                Vector3 axis = draggedDoor != null ? draggedDoor.WorldAxis : draggedTap.WorldAxis;
                float angle = draggedDoor != null ? draggedDoor.Angle : draggedTap.Angle;
                float closed = draggedDoor != null ? draggedDoor.closedAngle : draggedTap.closedAngle;
                float open = draggedDoor != null ? draggedDoor.openAngle : draggedTap.openAngle;
                float delta = FindHingeDelta(pointer - previousPointer, axis, angle, closed, open);
                if (draggedDoor != null) draggedDoor.DragByDegrees(delta);
                else draggedTap.DragByDegrees(delta);
            }
            previousPointer = pointer;
        }

        float FindHingeDelta(Vector2 pointerDelta, Vector3 axis, float currentAngle, float closedAngle, float openAngle)
        {
            Vector3 pivot = draggedPivot.position;
            Vector3 radius = draggedPivot.TransformPoint(controlLocalHit) - pivot;
            Vector3 screen = viewCamera.WorldToScreenPoint(pivot + radius);
            if (screen.z <= 0f || Vector3.ProjectOnPlane(radius, axis).sqrMagnitude < 0.000001f) return 0f;
            Vector2 target = (Vector2)screen + pointerDelta;
            // Fit the actual projected handle arc instead of dividing by its screen tangent.
            // This also works when looking nearly along the hinge plane, where ray/plane
            // intersections and tangent division become unstable. Local search prevents
            // jumping to the other branch of a projected ellipse or to a remote angle.
            float low = Mathf.Max(Mathf.Min(closedAngle, openAngle) - currentAngle, -25f);
            float high = Mathf.Min(Mathf.Max(closedAngle, openAngle) - currentAngle, 25f);
            float best = 0f;
            float bestError = HingeScreenError(0f, pivot, radius, axis, target);
            const int samples = 24;
            float spacing = (high - low) / samples;
            if (spacing <= 0.00001f) return 0f;
            for (int i = 0; i <= samples; i++)
            {
                float candidate = low + spacing * i;
                float error = HingeScreenError(candidate, pivot, radius, axis, target);
                if (error < bestError) { bestError = error; best = candidate; }
            }
            float a = Mathf.Max(low, best - spacing);
            float b = Mathf.Min(high, best + spacing);
            for (int i = 0; i < 8; i++)
            {
                float left = Mathf.Lerp(a, b, 1f / 3f);
                float right = Mathf.Lerp(a, b, 2f / 3f);
                if (HingeScreenError(left, pivot, radius, axis, target) < HingeScreenError(right, pivot, radius, axis, target)) b = right;
                else a = left;
            }
            float refined = (a + b) * 0.5f;
            return HingeScreenError(refined, pivot, radius, axis, target) < bestError ? refined : best;
        }

        float HingeScreenError(float degrees, Vector3 pivot, Vector3 radius, Vector3 axis, Vector2 target)
        {
            Vector3 point = viewCamera.WorldToScreenPoint(pivot + Quaternion.AngleAxis(degrees, axis) * radius);
            if (point.z <= 0f) return float.PositiveInfinity;
            return ((Vector2)point - target).sqrMagnitude + degrees * degrees * 0.0001f;
        }

        static float DistanceToSegmentSquared(Vector2 point, Vector2 a, Vector2 b)
        {
            Vector2 segment = b - a;
            float fraction = segment.sqrMagnitude > 0f ? Mathf.Clamp01(Vector2.Dot(point - a, segment) / segment.sqrMagnitude) : 0f;
            return (point - (a + segment * fraction)).sqrMagnitude;
        }

        void EndControlDrag()
        {
            if (draggedKnob != null) draggedKnob.EndDrag();
            if (draggedDoor != null) draggedDoor.EndDrag();
            if (draggedTap != null) draggedTap.EndDrag();
            draggedKnob = null;
            draggedDoor = null;
            draggedTap = null;
            draggedPivot = null;
            knobRadialReady = false;
            focusedLabel = "";
        }

        /// <summary>Clears falling and interaction state before/after the player is recovered.</summary>
        public void ResetMotion()
        {
            EndControlDrag();
            Release(false);
            fallSpeed = 0f;
            heldVelocity = Vector3.zero;
            if (playerRoot != null) yaw = playerRoot.eulerAngles.y;
            if (viewCamera != null) pitch = NormalizeAngle(viewCamera.transform.localEulerAngles.x);
            UnlockCursor();
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
            KitchenSession.Instance?.Tutorial("desktop_grab", "Desktop: left click releases. F eats. C cuts. R levels a tipped tool.\nScroll changes held reach. Set objects down before dragging handles or knobs.", grab.transform.position + Vector3.up * 0.2f);
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
            EndControlDrag();
            if (activeDesktop) SetDesktopMode(false);
            else UnlockCursor();
        }

        void OnApplicationFocus(bool focus)
        {
            if (!focus) { EndControlDrag(); UnlockCursor(); }
        }

        static float NormalizeAngle(float angle) => angle > 180f ? angle - 360f : angle;
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
            float width = Mathf.Min(Screen.width - 24, 980f);
            GUI.Box(new Rect(12, Screen.height - 91, width, 79), "PC  |  WASD move  |  Hold RMB to look  |  Point with cursor; hold LMB to drag controls\nClick grab / drop / button  |  E button  |  Wheel held reach  |  F eat  |  C cut  |  R level\n" + (heldGrab != null ? "Holding: " + heldGrab.name + "   " : "") + focusedLabel, helpStyle);
            if (Cursor.lockState == CursorLockMode.Locked)
                GUI.Label(new Rect(Screen.width * 0.5f - 12f, Screen.height * 0.5f - 15f, 24f, 30f), "+", crosshairStyle);
        }
    }
}
