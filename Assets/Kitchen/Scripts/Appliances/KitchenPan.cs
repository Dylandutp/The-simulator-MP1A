using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace HouseSim.Kitchen
{
    [RequireComponent(typeof(Rigidbody), typeof(XRGrabInteractable))]
    public sealed class KitchenPan : MonoBehaviour
    {
        public Transform cookingSurface;
        [Tooltip("Half extents of the food area, in cooking-surface local units.")]
        public Vector3 bounds = new Vector3(0.21f, 0.10f, 0.21f);
        [Tooltip("Height of the steak root above the cooking surface's local origin.")]
        public float supportedLocalHeight = -0.043f;
        [Tooltip("Small tosses rise at most this many metres above their launch point.")]
        public float gentleTossHeight = 0.14f;
        public bool IsHeld => (grab && grab.isSelected) || KitchenDesktopHeld.IsHeldByDesktop(gameObject);
        public int SupportedSteakCount => supported.Count;
        public int TossCount { get; private set; }
        public int CatchCount { get; private set; }
        public int EscapedSteakCount { get; private set; }
        public int PlacementCount { get; private set; }
        public bool ContainsSupportedFood(KitchenFood food) => food && supported.Exists(item => item.food == food);
        public bool CanHeatFood(KitchenFood food)
        {
            if (!food || !food.gameObject.activeInHierarchy || food.IsHeld || food.IsConsumed || !food.definition
                || food.definition.kind != FoodKind.Steak || deliberatelySpilled.Contains(food)
                || escaped.Exists(item => item.food == food)) return false;
            Support carried = supported.Find(item => item.food == food);
            if (carried != null) return !carried.airborne;
            Transform area = cookingSurface ? cookingSurface : transform;
            float localHeight = area.InverseTransformPoint(food.transform.position).y;
            return localHeight >= supportedLocalHeight - .025f && localHeight <= supportedLocalHeight + .035f;
        }
        public bool IsTossing
        {
            get { foreach (Support item in supported) if (item.airborne) return true; return false; }
        }
        private XRGrabInteractable grab;
        private Collider[] panColliders;
        private Rigidbody panBody;
        private readonly List<Support> supported = new List<Support>();
        private readonly List<Support> escaped = new List<Support>();
        private readonly HashSet<KitchenFood> deliberatelySpilled = new HashSet<KitchenFood>();
        private readonly List<PendingPlacement> pendingPlacements = new List<PendingPlacement>();
        private readonly Collider[] placementHits = new Collider[32];
        private Vector3 previousPosition;
        private Quaternion previousRotation;
        private Vector3 surfaceVelocity;
        private float previousUpSpeed;
        private float gestureReadyAt;
        private float nextTossAt;
        private bool sampled;
        private bool wasHeld;
        private float violentMotionSeconds;
        private float tippedSeconds;

        private struct PendingPlacement
        {
            public KitchenFood food;
            public int releaseFrame;
            public float expiresAt;
        }

        private sealed class Support
        {
            public KitchenFood food;
            public Rigidbody body;
            public Vector3 localPosition;
            public Quaternion localRotation;
            public readonly List<CollisionPair> pairs = new List<CollisionPair>();
            public bool airborne;
            public float launchedAt;
            public float launchHeight;
            public float duration;
            public float releasedAt;
            public bool protectHeld;
        }

        private struct CollisionPair
        {
            public Collider pan;
            public Collider food;
            public bool wasIgnored;
        }

        private void Awake()
        {
            grab = GetComponent<XRGrabInteractable>();
            if (!cookingSurface) cookingSurface = transform;
            panBody = GetComponent<Rigidbody>();
            panBody.maxAngularVelocity = 8f;
            panBody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            panBody.interpolation = RigidbodyInterpolation.Interpolate;
            panColliders = GetComponentsInChildren<Collider>();
            if (!GetComponent<KitchenContactStabilizer>()) gameObject.AddComponent<KitchenContactStabilizer>();
            if (!GetComponent<KitchenPanHeat>()) gameObject.AddComponent<KitchenPanHeat>();
        }

        private void OnEnable()
        {
            if (grab) grab.selectEntered.AddListener(OnGrabbed);
            KitchenFood.Released += OnFoodReleased;
        }

        private void OnFoodReleased(KitchenFood food)
        {
            if (!CanPlace(food)) return;
            pendingPlacements.Add(new PendingPlacement
            {
                food = food, releaseFrame = Time.frameCount, expiresAt = Time.unscaledTime + .3f
            });
        }

        private bool CanPlace(KitchenFood food) => food && !food.IsConsumed && !food.IsHeld
            && food.definition && food.definition.kind == FoodKind.Steak && IsInsideSnapRange(food);

        /// <summary>A shallow space over the real pan opening, not a magnet around the utensil.</summary>
        public bool IsInsideSnapRange(KitchenFood food)
        {
            if (!food) return false;
            Transform area = cookingSurface ? cookingSurface : transform;
            if (Vector3.Dot(area.up, Vector3.up) < .72f) return false;
            BoxCollider shape = food.GetComponent<BoxCollider>();
            if (!shape || !shape.enabled || shape.isTrigger) return false;
            Matrix4x4 intoArea = area.worldToLocalMatrix * shape.transform.localToWorldMatrix;
            Bounds foodBounds = new Bounds(intoArea.MultiplyPoint3x4(shape.center), Vector3.zero);
            for (int corner = 0; corner < 8; corner++)
            {
                Vector3 sign = new Vector3((corner & 1) == 0 ? -1f : 1f,
                    (corner & 2) == 0 ? -1f : 1f, (corner & 4) == 0 ? -1f : 1f);
                foodBounds.Encapsulate(intoArea.MultiplyPoint3x4(shape.center + Vector3.Scale(shape.size * .5f, sign)));
            }
            float x = foodBounds.center.x / Mathf.Max(.01f, bounds.x + .05f);
            float z = foodBounds.center.z / Mathf.Max(.01f, bounds.z + .05f);
            // Even a large collider cannot reach through the bottom or draw a remote item in.
            if (x * x + z * z > 1f || foodBounds.min.y < supportedLocalHeight - .018f
                || foodBounds.min.y > .15f || foodBounds.max.y < supportedLocalHeight) return false;
            Bounds volume = new Bounds();
            volume.SetMinMax(new Vector3(-bounds.x, supportedLocalHeight - .012f, -bounds.z),
                new Vector3(bounds.x, .15f, bounds.z));
            if (!foodBounds.Intersects(volume)) return false;
            Physics.SyncTransforms();
            int count = Physics.OverlapBoxNonAlloc(area.TransformPoint(volume.center),
                Vector3.Scale(volume.extents, Abs(area.lossyScale)), placementHits, area.rotation, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++) if (placementHits[i] == shape) return true;
            return false;
        }

        private void ProcessPendingPlacements()
        {
            for (int i = pendingPlacements.Count - 1; i >= 0; i--)
            {
                PendingPlacement pending = pendingPlacements[i];
                if (!CanPlace(pending.food) || Time.unscaledTime > pending.expiresAt)
                { pendingPlacements.RemoveAt(i); continue; }
                Rigidbody body = pending.food.GetComponent<Rigidbody>();
                if (!body) { pendingPlacements.RemoveAt(i); continue; }
                if (body.isKinematic || Time.frameCount <= pending.releaseFrame) continue;
                pendingPlacements.RemoveAt(i);
                PlaceReleasedSteak(pending.food, body);
            }
        }

        private void PlaceReleasedSteak(KitchenFood food, Rigidbody body)
        {
            Transform area = cookingSurface ? cookingSurface : transform;
            BoxCollider shape = food.GetComponent<BoxCollider>();
            Vector3 scale = Abs(food.transform.lossyScale);
            Vector3 localPosition = new Vector3(0f, supportedLocalHeight, 0f);
            Vector3 target = area.TransformPoint(localPosition);
            Quaternion rotation = area.rotation;
            Vector3 center = target + rotation * Vector3.Scale(shape.center, scale);
            Vector3 extents = Vector3.Scale(shape.size * .5f, scale);
            Physics.SyncTransforms();
            int count = Physics.OverlapBoxNonAlloc(center, extents + Vector3.one * .001f,
                placementHits, rotation, ~0, QueryTriggerInteraction.Ignore);
            if (count == placementHits.Length) return;
            for (int i = 0; i < count; i++)
            {
                Collider hit = placementHits[i];
                if (!hit || hit.transform.IsChildOf(food.transform) || hit.attachedRigidbody == panBody) continue;
                return; // Respect another steak, a tool, a counter or any other solid blocker.
            }
            // Held-food contact protection and carriage must have one owner per pair.
            for (int i = escaped.Count - 1; i >= 0; i--)
                if (escaped[i].food == food) { RestorePairs(escaped[i]); escaped.RemoveAt(i); }
            for (int i = supported.Count - 1; i >= 0; i--)
                if (supported[i].food == food) { RestorePairs(supported[i]); supported.RemoveAt(i); }
            body.position = target;
            body.rotation = rotation;
            food.transform.SetPositionAndRotation(target, rotation);
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.Sleep();
            deliberatelySpilled.Remove(food); // An explicit grab/release is a deliberate return to the pan.
            if (IsHeld)
            {
                var item = new Support { food = food, body = body, localPosition = localPosition, localRotation = Quaternion.identity };
                IgnorePanContacts(item);
                supported.Add(item);
            }
            ++PlacementCount;
        }
        private void OnGrabbed(SelectEnterEventArgs args) => BeginCarry();
        public void BeginCarry()
        {
            if (!IsHeld) return;
            CaptureSteaks();
            sampled = false;
            gestureReadyAt = Mathf.Max(gestureReadyAt, Time.time + .35f);
            violentMotionSeconds = tippedSeconds = 0f;
        }

        internal void DeferGripAlignmentGestures(float seconds)
        {
            // Leveling a freshly selected pan is generated motion, not a deliberate spill.
            gestureReadyAt = Mathf.Max(gestureReadyAt, Time.time + seconds);
        }

        private void FixedUpdate()
        {
            Transform area = cookingSurface ? cookingSurface : transform;
            bool held = IsHeld;
            float dt = Time.fixedDeltaTime;
            Vector3 displacement = area.position - previousPosition;
            float turned = Quaternion.Angle(previousRotation, area.rotation);
            // Pickup, teleport, tracking recovery and tool respawn are not cooking gestures.
            bool discontinuity = !sampled || held != wasHeld || displacement.magnitude > .40f || turned > 65f;
            surfaceVelocity = !discontinuity && dt > 0f ? displacement / dt : Vector3.zero;
            float upSpeed = Vector3.Dot(surfaceVelocity, Vector3.up);
            if (discontinuity)
            {
                gestureReadyAt = Mathf.Max(gestureReadyAt, Time.time + .35f);
                previousUpSpeed = 0f;
                violentMotionSeconds = tippedSeconds = 0f;
                if (sampled && held && wasHeld && (displacement.magnitude > .40f || turned > 65f))
                    foreach (Support item in supported) if (item.food && !item.food.IsHeld) Seat(item);
            }
            previousPosition = area.position;
            previousRotation = area.rotation;
            sampled = true;
            wasHeld = held;
            ProtectHeldSteaks();
            RestoreEscapedCollisions();
            ProcessPendingPlacements();
            deliberatelySpilled.RemoveWhere(food => !food || food.IsConsumed);

            if (!held)
            {
                ReleaseSupport();
                previousUpSpeed = 0f;
                violentMotionSeconds = tippedSeconds = 0f;
                return;
            }

            CaptureSteaks();
            float upright = Vector3.Dot(area.up, Vector3.up);
            if (Time.time >= gestureReadyAt && supported.Count > 0)
            {
                float angularSpeed = dt > 0f ? turned * Mathf.Deg2Rad / dt : 0f;
                Vector3 planarVelocity = Vector3.ProjectOnPlane(surfaceVelocity, Vector3.up);
                bool upwardFlick = upSpeed >= .65f && upSpeed - previousUpSpeed > .08f;
                // A single tracking/hand spike should not ruin a meal. Spilling requires
                // a sustained strong motion, or deliberately holding the pan nearly sideways.
                bool violent = upSpeed >= 3.6f || planarVelocity.magnitude >= 4.2f
                    || (angularSpeed >= 10f && upSpeed >= .7f);
                violentMotionSeconds = violent ? violentMotionSeconds + dt : Mathf.Max(0f, violentMotionSeconds - dt * 3f);
                tippedSeconds = upright < .3f ? tippedSeconds + dt : 0f;
                bool excessive = violentMotionSeconds >= .075f || tippedSeconds >= .18f;
                if (excessive) TryToss(1.4f);
                else if (upwardFlick && upright > .65f && Time.time >= nextTossAt)
                    TryToss(Mathf.InverseLerp(.65f, 3.6f, upSpeed));
            }
            previousUpSpeed = upSpeed;

            for (int i = supported.Count - 1; i >= 0; --i)
            {
                Support item = supported[i];
                if (item.food && item.food.IsHeld)
                {
                    ProtectPickedUpSteak(item);
                    supported.RemoveAt(i);
                    continue;
                }
                if (!item.food || item.food.IsConsumed || !item.body || item.body.isKinematic)
                {
                    RestorePairs(item);
                    supported.RemoveAt(i);
                    continue;
                }
                if (item.airborne) AdvanceFlight(item, area);
                else Seat(item);
            }
        }

        /// <summary>Same small toss for a controller flick or PC input; a strength above one deliberately spills.</summary>
        public bool TryToss(float strength = .5f)
        {
            bool spill = strength > 1f;
            if (!IsHeld || (!spill && Time.time < nextTossAt) || float.IsNaN(strength) || float.IsInfinity(strength)) return false;
            CaptureSteaks();
            bool launched = false;
            foreach (Support item in supported.ToArray())
            {
                if (!item.food || item.food.IsHeld || !item.body || item.body.isKinematic || (item.airborne && !spill)) continue;
                if (spill)
                {
                    Escape(item, Mathf.Clamp(strength, 1f, 2f));
                    launched = true;
                    continue;
                }

                Seat(item);
                item.airborne = true;
                item.launchedAt = Time.time;
                item.launchHeight = item.body.position.y;
                float height = Mathf.Lerp(.065f, Mathf.Clamp(gentleTossHeight, .08f, .18f), Mathf.Clamp01(strength));
                float gravity = Mathf.Max(.1f, -Physics.gravity.y);
                float speed = Mathf.Sqrt(2f * gravity * height);
                item.duration = 2f * speed / gravity;
                item.body.linearVelocity = Vector3.ClampMagnitude(Vector3.ProjectOnPlane(surfaceVelocity, Vector3.up), .45f) + Vector3.up * speed;
                item.body.angularVelocity = Vector3.zero;
                item.body.WakeUp();
                launched = true;
            }
            if (launched)
            {
                ++TossCount;
                nextTossAt = Time.time + .48f;
            }
            return launched;
        }

        private void CaptureSteaks()
        {
            Transform area = cookingSurface ? cookingSurface : transform;
            if (Vector3.Dot(area.up, Vector3.up) < .45f) return;
            foreach (KitchenFood food in GetSteaks())
            {
                if (supported.Exists(item => item.food == food) || escaped.Exists(item => item.food == food)
                    || deliberatelySpilled.Contains(food)) continue;
                Rigidbody body = food.GetComponent<Rigidbody>();
                if (!body || body.isKinematic) continue;
                Vector3 point = area.InverseTransformPoint(body.position);
                // The existing pan volume is the entire capture range: no food on a counter or in a hand is pulled in.
                if (point.y > .075f || body.linearVelocity.y > .75f) continue;
                var item = new Support
                {
                    food = food,
                    body = body,
                    localPosition = new Vector3(Mathf.Clamp(point.x, -bounds.x * .2f, bounds.x * .2f), supportedLocalHeight,
                        Mathf.Clamp(point.z, -bounds.z * .2f, bounds.z * .2f)),
                    localRotation = Quaternion.Euler(0f, (Quaternion.Inverse(area.rotation) * body.rotation).eulerAngles.y, 0f)
                };
                IgnorePanContacts(item);
                supported.Add(item);
            }
        }

        private void IgnorePanContacts(Support item)
        {
            Collider[] foodColliders = item.food.GetComponentsInChildren<Collider>();
            foreach (Collider a in panColliders)
            {
                if (!a || a.isTrigger || a.attachedRigidbody != panBody) continue;
                foreach (Collider b in foodColliders)
                {
                    if (!b || b.isTrigger || b.attachedRigidbody != item.body) continue;
                    item.pairs.Add(new CollisionPair { pan = a, food = b, wasIgnored = Physics.GetIgnoreCollision(a, b) });
                    Physics.IgnoreCollision(a, b, true);
                }
            }
        }

        private void ProtectHeldSteaks()
        {
            // The general contact stabilizer leaves pan/steak ownership here. A tracked steak must not
            // hammer a free pan, and picking one out must not immediately restore an overlapping pair.
            Transform area = cookingSurface ? cookingSurface : transform;
            Vector3 half = Vector3.Scale(bounds + new Vector3(.2f, .2f, .2f), Abs(area.lossyScale));
            foreach (Collider hit in Physics.OverlapBox(area.position, half, area.rotation, ~0, QueryTriggerInteraction.Ignore))
            {
                KitchenFood food = hit.GetComponentInParent<KitchenFood>();
                if (!food || !food.IsHeld || !food.definition || food.definition.kind != FoodKind.Steak
                    || supported.Exists(item => item.food == food) || escaped.Exists(item => item.food == food)) continue;
                Rigidbody body = food.GetComponent<Rigidbody>();
                if (!body) continue;
                var protection = new Support { food = food, body = body, releasedAt = Time.time, protectHeld = true };
                IgnorePanContacts(protection);
                escaped.Add(protection);
            }
        }

        private void ProtectPickedUpSteak(Support item)
        {
            item.protectHeld = true;
            item.releasedAt = Time.time;
            escaped.Add(item);
        }

        private void Seat(Support item)
        {
            if (!item.body || item.body.isKinematic || !item.food || item.food.IsHeld) return;
            Transform area = cookingSurface ? cookingSurface : transform;
            item.airborne = false;
            // Keep the original Rigidbody mode/gravity flags: XR and desktop grabbing remember them.
            // Only pan/steak contact is assisted, so a moving tracked pan cannot inject a huge contact impulse.
            item.body.position = area.TransformPoint(item.localPosition);
            item.body.rotation = area.rotation * item.localRotation;
            item.body.linearVelocity = Vector3.zero;
            item.body.angularVelocity = Vector3.zero;
            if (item.body.useGravity) item.body.AddForce(-Physics.gravity, ForceMode.Acceleration);
        }

        private void AdvanceFlight(Support item, Transform area)
        {
            Vector3 seat = area.TransformPoint(item.localPosition);
            Vector3 offset = item.body.position - seat;
            float elapsed = Time.time - item.launchedAt;
            float horizontal = Vector3.ProjectOnPlane(offset, Vector3.up).magnitude;
            // Gestures above decide whether a deliberate spill occurred. Ordinary drift
            // during a small toss gets a forgiving catch instead of instant contamination.
            if (horizontal > .3f)
            {
                Vector3 limited = Vector3.ClampMagnitude(Vector3.ProjectOnPlane(offset, Vector3.up), .24f);
                item.body.position = new Vector3(seat.x + limited.x, item.body.position.y, seat.z + limited.z);
                offset = item.body.position - seat;
                horizontal = limited.magnitude;
            }
            if (elapsed > .07f && offset.y <= .02f && item.body.linearVelocity.y <= Mathf.Max(.1f, surfaceVelocity.y))
            {
                ++CatchCount;
                Seat(item);
                return;
            }
            if (elapsed > .75f)
            {
                ++CatchCount;
                Seat(item);
                return;
            }

            Vector3 correction = Vector3.ProjectOnPlane(-offset * 14f + surfaceVelocity, Vector3.up);
            correction = Vector3.ClampMagnitude(correction, 3.4f);
            Vector3 velocity = item.body.linearVelocity;
            // Vertical motion is real gravity. The mild horizontal assistance keeps an ordinary flick over the pan.
            velocity.x = correction.x;
            velocity.z = correction.z;
            if (item.body.position.y > Mathf.Max(item.launchHeight, seat.y) + Mathf.Clamp(gentleTossHeight, .08f, .18f) + .015f)
                velocity.y = Mathf.Min(velocity.y, 0f);
            item.body.linearVelocity = velocity;
            float rock = Mathf.Sin(Mathf.Clamp01(elapsed / item.duration) * Mathf.PI) * 22f;
            item.body.MoveRotation(area.rotation * item.localRotation * Quaternion.Euler(rock, 0f, 0f));
        }

        private void Escape(Support item, float strength)
        {
            if (!item.food || !item.body) return;
            Transform area = cookingSurface ? cookingSurface : transform;
            Vector3 direction = Vector3.ProjectOnPlane(surfaceVelocity, Vector3.up);
            if (direction.sqrMagnitude < .1f) direction = Vector3.ProjectOnPlane(area.forward, Vector3.up);
            if (direction.sqrMagnitude < .01f) direction = Vector3.forward;
            direction.Normalize();
            item.body.linearVelocity = Vector3.ClampMagnitude(direction * (1.7f + strength * .65f) + Vector3.up * (1.1f + strength * .5f), 3.8f);
            item.body.angularVelocity = Vector3.Cross(Vector3.up, direction) * 5f;
            item.body.WakeUp();
            item.food.MarkUnclean();
            deliberatelySpilled.Add(item.food);
            item.releasedAt = Time.time;
            item.airborne = false;
            supported.Remove(item);
            escaped.Add(item);
            ++EscapedSteakCount;
            KitchenSession.Instance?.Notify("Steak spilled out of the pan: unclean. Eating it now loses energy.");
        }

        private void RestoreEscapedCollisions()
        {
            for (int i = escaped.Count - 1; i >= 0; --i)
            {
                Support item = escaped[i];
                Transform area = cookingSurface ? cookingSurface : transform;
                if (item.food && item.food.IsHeld && new Bounds(Vector3.zero, (bounds + Vector3.one * .25f) * 2f)
                    .Contains(area.InverseTransformPoint(item.food.transform.position)))
                {
                    item.releasedAt = Time.time;
                    continue;
                }
                if (item.protectHeld)
                {
                    if (!item.food || Time.time - item.releasedAt >= .08f)
                    {
                        RestorePairs(item);
                        escaped.RemoveAt(i);
                    }
                    continue;
                }
                // A short grace lets the steak clear the rim before contacts return; it cannot be immediately recaptured.
                if (!item.food || Time.time - item.releasedAt >= .18f) RestorePairs(item);
                if (!item.food || Time.time - item.releasedAt >= 1f) escaped.RemoveAt(i);
            }
        }

        private static void RestorePairs(Support item)
        {
            foreach (CollisionPair pair in item.pairs)
                if (pair.pan && pair.food) Physics.IgnoreCollision(pair.pan, pair.food, pair.wasIgnored);
            item.pairs.Clear();
        }

        private void ReleaseSupport()
        {
            foreach (Support item in supported)
            {
                if (item.food && item.food.IsHeld)
                {
                    ProtectPickedUpSteak(item);
                    continue;
                }
                if (item.body && item.food && !item.food.IsHeld && !item.body.isKinematic && !item.airborne)
                    item.body.linearVelocity = Vector3.ClampMagnitude(surfaceVelocity, .7f);
                RestorePairs(item);
            }
            supported.Clear();
        }

        private void OnDisable()
        {
            if (grab) grab.selectEntered.RemoveListener(OnGrabbed);
            KitchenFood.Released -= OnFoodReleased;
            pendingPlacements.Clear();
            ReleaseSupport();
            foreach (Support item in escaped) RestorePairs(item);
            escaped.Clear();
            sampled = false;
            deliberatelySpilled.Clear();
            violentMotionSeconds = tippedSeconds = 0f;
        }

        public IEnumerable<KitchenFood> GetSteaks()
        {
            Transform area = cookingSurface ? cookingSurface : transform;
            Vector3 halfExtents = Vector3.Scale(bounds, Abs(area.lossyScale));
            var unique = new HashSet<KitchenFood>();
            foreach (Collider hit in Physics.OverlapBox(area.position, halfExtents, area.rotation, ~0, QueryTriggerInteraction.Ignore))
            {
                KitchenFood food = hit.GetComponentInParent<KitchenFood>();
                if (food && !food.IsConsumed && !food.IsHeld && food.definition && food.definition.kind == FoodKind.Steak
                    && new Bounds(Vector3.zero, bounds * 2f).Contains(area.InverseTransformPoint(food.transform.position)) && unique.Add(food))
                    yield return food;
            }
        }

        private static Vector3 Abs(Vector3 value) => new Vector3(Mathf.Abs(value.x), Mathf.Abs(value.y), Mathf.Abs(value.z));

        private void OnDrawGizmosSelected()
        {
            Transform area = cookingSurface ? cookingSurface : transform;
            Gizmos.matrix = area.localToWorldMatrix;
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.4f);
            Gizmos.DrawWireCube(Vector3.zero, bounds * 2f);
        }
    }
}
