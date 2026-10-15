using System.Collections.Generic;
using UnityEngine;

namespace HouseSim.Kitchen
{
    public sealed class KitchenOven : MonoBehaviour
    {
        public KitchenDoor door;
        public KitchenKnob timerKnob;
        public Transform chamber;
        public Vector3 chamberHalfExtents = new Vector3(0.36f, 0.23f, 0.32f);
        public TextMesh label;
        public Light heatLight;
        public float RemainingSeconds { get; private set; }
        public bool IsHeating { get; private set; }
        private KitchenAudioEmitter audioEmitter;
        private readonly HashSet<KitchenFood> heated = new HashSet<KitchenFood>();
        private bool resettingKnob;
        private float nextLabelRefresh;
        private readonly List<PendingPlacement> pendingPlacements = new List<PendingPlacement>();
        private readonly Collider[] placementHits = new Collider[32];
        private BoxCollider rack;
        private Collider[] ovenColliders;

        private struct PendingPlacement
        {
            public KitchenFood food;
            public int releaseFrame;
            public float expiresAt;
        }

        private void Awake()
        {
            Transform rackTransform = transform.Find("Rack");
            if (rackTransform) rack = rackTransform.GetComponent<BoxCollider>();
            ovenColliders = GetComponentsInChildren<Collider>();
        }

        private void OnEnable()
        {
            KitchenFood.Released += OnFoodReleased;
            RefreshClockAudio();
        }

        private void OnDisable()
        {
            KitchenFood.Released -= OnFoodReleased;
            pendingPlacements.Clear();
            if (audioEmitter) audioEmitter.StopAll();
        }

        private void OnFoodReleased(KitchenFood food)
        {
            if (!CanPlace(food)) return;
            // The desktop controller and XR toolkit finish restoring release physics after
            // the selection callback. Waiting until the next physics frame avoids their
            // detach/throw velocity overwriting a successful placement.
            pendingPlacements.Add(new PendingPlacement
            {
                food = food,
                releaseFrame = Time.frameCount,
                expiresAt = Time.unscaledTime + 0.2f
            });
        }

        private void FixedUpdate()
        {
            for (int i = pendingPlacements.Count - 1; i >= 0; i--)
            {
                PendingPlacement pending = pendingPlacements[i];
                if (!CanPlace(pending.food) || Time.unscaledTime > pending.expiresAt)
                {
                    pendingPlacements.RemoveAt(i);
                    continue;
                }
                Rigidbody body = pending.food.GetComponent<Rigidbody>();
                if (!body)
                {
                    pendingPlacements.RemoveAt(i);
                    continue;
                }
                if (Time.frameCount <= pending.releaseFrame || body.isKinematic) continue;
                pendingPlacements.RemoveAt(i);
                PlaceReleasedChicken(pending.food, body);
            }
        }

        private bool CanPlace(KitchenFood food) => chamber && door && door.OpenAmount >= 0.6f
            && food && !food.IsConsumed && !food.IsHeld && food.definition
            && food.definition.kind == FoodKind.Chicken && IsInsideSnapRange(food);

        /// <summary>The actual assist volume starts just inside the closed door plane.</summary>
        public bool IsInsideSnapRange(Vector3 worldPosition)
        {
            return TryGetSnapVolume(out Bounds volume) && volume.Contains(chamber.InverseTransformPoint(worldPosition));
        }

        private bool TryGetSnapVolume(out Bounds volume)
        {
            volume = default;
            if (!chamber || !door) return false;
            float doorPlane = chamber.InverseTransformPoint(door.Pivot.position).z;
            if (doorPlane + 0.01f >= chamberHalfExtents.z) return false;
            volume.SetMinMax(new Vector3(-chamberHalfExtents.x, -chamberHalfExtents.y - 0.03f, doorPlane + 0.01f),
                new Vector3(chamberHalfExtents.x, chamberHalfExtents.y + 0.08f, chamberHalfExtents.z));
            return true;
        }

        /// <summary>
        /// A chicken spanning the doorway qualifies when its real collider enters the
        /// cavity. Its root does not need to be pushed past the rack's front edge.
        /// </summary>
        public bool IsInsideSnapRange(KitchenFood food)
        {
            if (!food || !TryGetSnapVolume(out Bounds volume)) return false;
            BoxCollider shape = food.GetComponent<BoxCollider>();
            if (!shape || !shape.enabled || shape.isTrigger) return false;
            Matrix4x4 intoChamber = chamber.worldToLocalMatrix * shape.transform.localToWorldMatrix;
            Bounds foodBounds = new Bounds(intoChamber.MultiplyPoint3x4(shape.center), Vector3.zero);
            for (int corner = 0; corner < 8; corner++)
            {
                Vector3 sign = new Vector3((corner & 1) == 0 ? -1f : 1f,
                    (corner & 2) == 0 ? -1f : 1f, (corner & 4) == 0 ? -1f : 1f);
                foodBounds.Encapsulate(intoChamber.MultiplyPoint3x4(shape.center + Vector3.Scale(shape.size * 0.5f, sign)));
            }
            // The entry assist must not reach around the sides or through the back.
            // Require an actual part of the food at least 2.5cm into the inner volume,
            // so an entirely outside chicken or a tip grazing the doorway is ignored.
            if (foodBounds.center.x < volume.min.x || foodBounds.center.x > volume.max.x
                || foodBounds.center.z > volume.max.z || foodBounds.max.z <= volume.min.z + 0.025f
                || !foodBounds.Intersects(volume)) return false;

            Physics.SyncTransforms();
            Vector3 scale = chamber.lossyScale;
            scale = new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
            int count = Physics.OverlapBoxNonAlloc(chamber.TransformPoint(volume.center),
                Vector3.Scale(volume.extents, scale), placementHits, chamber.rotation, ~0, QueryTriggerInteraction.Ignore);
            bool intersects = false;
            for (int i = 0; i < count; i++)
                if (placementHits[i] == shape) { intersects = true; break; }
            if (!intersects) return false;

            // Touching or resting on the rack/open door is expected. Penetrating a
            // cabinet wall, fascia or base is not a valid route into the oven.
            foreach (Collider fixture in ovenColliders)
            {
                if (!fixture || !fixture.enabled || fixture.isTrigger || fixture == rack
                    || fixture.transform.IsChildOf(door.Pivot)) continue;
                if (Physics.ComputePenetration(shape, shape.transform.position, shape.transform.rotation,
                    fixture, fixture.transform.position, fixture.transform.rotation, out _, out float depth)
                    && depth > 0.012f) return false;
            }
            return true;
        }

        private void PlaceReleasedChicken(KitchenFood food, Rigidbody body)
        {
            BoxCollider shape = food.GetComponent<BoxCollider>();
            if (!shape || !shape.enabled || shape.isTrigger) return;
            Vector3 scale = food.transform.lossyScale;
            scale = new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
            Vector3 extents = Vector3.Scale(shape.size * 0.5f, scale);
            Vector3 centerOffset = Vector3.Scale(shape.center, scale);
            float chamberScaleY = Mathf.Max(0.001f, Mathf.Abs(chamber.lossyScale.y));
            float rackTop = rack
                ? chamber.InverseTransformPoint(rack.transform.TransformPoint(rack.center + Vector3.up * rack.size.y * 0.5f)).y
                : -chamberHalfExtents.y;
            Vector3 targetLocal = new Vector3(0f,
                Mathf.Max(-chamberHalfExtents.y + 0.004f, rackTop - (centerOffset.y - extents.y) / chamberScaleY + 0.004f), 0f);
            Vector3 target = chamber.TransformPoint(targetLocal);
            Quaternion rotation = chamber.rotation;
            Vector3 center = target + rotation * centerOffset;

            // A full target-volume check prevents two released chickens from being stacked
            // into each other, and also respects a tool or other ingredient on the rack.
            Physics.SyncTransforms();
            int count = Physics.OverlapBoxNonAlloc(center, extents + Vector3.one * 0.001f,
                placementHits, rotation, ~0, QueryTriggerInteraction.Ignore);
            if (count == placementHits.Length) return;
            for (int i = 0; i < count; i++)
            {
                Collider hit = placementHits[i];
                if (!hit || hit.transform.IsChildOf(food.transform)) continue;
                // Only the rack itself is a valid support. Oven walls, another food,
                // a held tool, or any other solid obstacle must leave placement blocked.
                if (hit == rack) continue;
                return;
            }
            body.position = target;
            body.rotation = rotation;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            food.transform.SetPositionAndRotation(target, rotation);
            body.Sleep();
        }

        private void Start()
        {
            if (timerKnob)
            {
                timerKnob.valueChanged.AddListener(OnTimerChanged);
                timerKnob.DragStateChanged += OnTimerDragChanged;
                RemainingSeconds = Mathf.Clamp(timerKnob.Value, 0f, 60f);
            }
            RefreshClockAudio();
        }

        private void OnDestroy()
        {
            if (timerKnob)
            {
                timerKnob.valueChanged.RemoveListener(OnTimerChanged);
                timerKnob.DragStateChanged -= OnTimerDragChanged;
            }
        }

        private void OnTimerChanged(float seconds)
        {
            if (resettingKnob) return;
            RemainingSeconds = Mathf.Clamp(seconds, 0f, 60f);
            RefreshClockAudio();
        }

        private void Update() => Tick(Time.deltaTime);

        private bool CanCountDown => door && !door.IsOpen && door.OpenAmount <= 0.035f
            && RemainingSeconds > 0f && (!timerKnob || !timerKnob.IsDragging);

        private void OnTimerDragChanged(bool dragging)
        {
            IsHeating = isActiveAndEnabled && CanCountDown;
            if (heatLight) heatLight.enabled = IsHeating;
            RefreshClockAudio();
        }

        public void Tick(float deltaTime)
        {
            if (float.IsNaN(deltaTime) || float.IsInfinity(deltaTime) || deltaTime < 0f) return;
            IsHeating = CanCountDown;
            if (heatLight) heatLight.enabled = IsHeating;
            if (IsHeating)
            {
                float heatSeconds = Mathf.Min(deltaTime, RemainingSeconds);
                HeatFood(heatSeconds);
                RemainingSeconds = Mathf.Max(0f, RemainingSeconds - heatSeconds);
                // The physical timer pointer is the remaining-time display. Suppress feedback
                // from this automatic movement while keeping deliberate user turns live.
                resettingKnob = true;
                try
                {
                    if (timerKnob) timerKnob.SetValue(RemainingSeconds);
                }
                finally
                {
                    resettingKnob = false;
                }
                if (RemainingSeconds <= 0f)
                {
                    IsHeating = false;
                    if (heatLight) heatLight.enabled = false;
                    if (!audioEmitter) audioEmitter = KitchenAudioEmitter.Get(this);
                    audioEmitter.PlayCue(KitchenSound.OvenDone);
                }
            }
            RefreshClockAudio();
            RefreshLabel();
        }

        private void RefreshClockAudio()
        {
            if (!isActiveAndEnabled) return;
            if (!audioEmitter) audioEmitter = KitchenAudioEmitter.Get(this);
            // Setting the timer pauses both cooking and ticking until release.
            // Opening the door also pauses them; manual zero cancels without a bell.
            audioEmitter.SetLoop(KitchenSound.ClockTick, CanCountDown);
        }

        private void RefreshLabel()
        {
            if (!label || Time.unscaledTime < nextLabelRefresh) return;
            nextLabelRefresh = Time.unscaledTime + 0.15f;
            string foodStatus = "";
            if (chamber)
            {
                Vector3 scale = chamber.lossyScale;
                Vector3 halfExtents = Vector3.Scale(chamberHalfExtents, new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
                foreach (Collider hit in Physics.OverlapBox(chamber.position, halfExtents, chamber.rotation, ~0, QueryTriggerInteraction.Ignore))
                {
                    KitchenFood food = hit.GetComponentInParent<KitchenFood>();
                    if (!food || food.IsConsumed || !food.definition || food.definition.kind != FoodKind.Chicken
                        || !new Bounds(Vector3.zero, chamberHalfExtents * 2f).Contains(chamber.InverseTransformPoint(food.transform.position))) continue;
                    foodStatus = "\nCHICKEN " + food.State.ToString().ToUpperInvariant() + "  " + food.CookSeconds.ToString("0.0") + " s";
                    break;
                }
            }
            label.text = "OVEN " + Mathf.CeilToInt(RemainingSeconds) + " s\n"
                + (IsHeating ? "HEATING" : RemainingSeconds > 0f ? "CLOSE DOOR TO RESUME" : "SET TIMER TO COOK") + foodStatus;
        }

        private void HeatFood(float seconds)
        {
            if (!chamber) return;
            Vector3 scale = chamber.lossyScale;
            Vector3 halfExtents = Vector3.Scale(chamberHalfExtents, new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
            heated.Clear();
            foreach (Collider hit in Physics.OverlapBox(chamber.position, halfExtents, chamber.rotation, ~0, QueryTriggerInteraction.Ignore))
            {
                KitchenFood food = hit.GetComponentInParent<KitchenFood>();
                if (food && !food.IsConsumed && !food.IsHeld && food.definition && food.definition.kind == FoodKind.Chicken
                    && new Bounds(Vector3.zero, chamberHalfExtents * 2f).Contains(chamber.InverseTransformPoint(food.transform.position)) && heated.Add(food)) food.ApplyHeat(seconds);
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (!chamber) return;
            Gizmos.matrix = chamber.localToWorldMatrix;
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(Vector3.zero, chamberHalfExtents * 2f);
        }
    }
}
