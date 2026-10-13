using System.Collections.Generic;
using UnityEngine;

namespace HouseSim.Kitchen
{
    [RequireComponent(typeof(AudioSource))]
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
        private AudioSource bell;
        private AudioClip bellClip;
        private readonly HashSet<KitchenFood> heated = new HashSet<KitchenFood>();
        private bool resettingKnob;
        private float nextLabelRefresh;

        private void Start()
        {
            if (timerKnob)
            {
                timerKnob.valueChanged.AddListener(OnTimerChanged);
                RemainingSeconds = Mathf.Clamp(timerKnob.Value, 0f, 60f);
            }
            bell = GetComponent<AudioSource>();
            bell.playOnAwake = false;
            bell.spatialBlend = 1f;
            bell.minDistance = 1f;
            bell.maxDistance = 8f;
            bellClip = MakeBell();
        }

        private void OnDestroy()
        {
            if (timerKnob) timerKnob.valueChanged.RemoveListener(OnTimerChanged);
            if (bellClip) Destroy(bellClip);
        }

        private void OnTimerChanged(float seconds)
        {
            if (!resettingKnob) RemainingSeconds = Mathf.Clamp(seconds, 0f, 60f);
        }

        private void Update() => Tick(Time.deltaTime);

        public void Tick(float deltaTime)
        {
            if (float.IsNaN(deltaTime) || float.IsInfinity(deltaTime) || deltaTime < 0f) return;
            IsHeating = door && !door.IsOpen && door.OpenAmount <= 0.035f && RemainingSeconds > 0f;
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
                    if (bell && bellClip) bell.PlayOneShot(bellClip, 0.6f);
                }
            }
            RefreshLabel();
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

        private static AudioClip MakeBell()
        {
            const int rate = 22050;
            var samples = new float[rate];
            for (int i = 0; i < samples.Length; i++)
            {
                float t = i / (float)rate;
                samples[i] = (Mathf.Sin(2f * Mathf.PI * 1100f * t) + 0.3f * Mathf.Sin(2f * Mathf.PI * 1650f * t)) * Mathf.Exp(-5.5f * t) * 0.3f;
            }
            AudioClip clip = AudioClip.Create("Kitchen oven bell", samples.Length, 1, rate, false);
            clip.SetData(samples, 0);
            return clip;
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
