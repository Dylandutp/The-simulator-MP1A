using UnityEngine;

namespace HouseSim.Kitchen
{
    public sealed class KitchenHob : MonoBehaviour
    {
        public KitchenKnob powerKnob;
        public Renderer[] ringRenderers;
        public KitchenPan pan;
        public Transform burner;
        public TextMesh label;
        public float horizontalTolerance = 0.23f;
        public float verticalTolerance = 0.22f;
        public bool IsHeating { get; private set; }
        private MaterialPropertyBlock propertyBlock;
        private float nextLabelRefresh;
        private float displayedPower = -1f;
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorProperty = Shader.PropertyToID("_Color");
        private static readonly int Emission = Shader.PropertyToID("_EmissionColor");

        private void Awake()
        {
            propertyBlock = new MaterialPropertyBlock();
            if (!pan) pan = GetComponentInChildren<KitchenPan>(true);
        }

        private void Update() => Tick(Time.deltaTime);

        public void Tick(float deltaTime)
        {
            if (float.IsNaN(deltaTime) || float.IsInfinity(deltaTime) || deltaTime < 0f) return;
            float power = powerKnob ? powerKnob.NormalizedValue : 0f;
            Color color = Color.Lerp(new Color(0.10f, 0.10f, 0.12f), new Color(1f, 0.12f, 0.015f), power);
            if (ringRenderers != null && power != displayedPower)
            {
                foreach (Renderer ring in ringRenderers)
                {
                    if (!ring) continue;
                    ring.GetPropertyBlock(propertyBlock);
                    propertyBlock.SetColor(BaseColor, color);
                    propertyBlock.SetColor(ColorProperty, color);
                    propertyBlock.SetColor(Emission, new Color(1f, 0.12f, 0.015f) * (power * 1.2f));
                    ring.SetPropertyBlock(propertyBlock);
                }
                displayedPower = power;
            }
            IsHeating = power > 0 && PanIsOnBurner();
            RefreshLabel(power);
            if (!IsHeating) return;
            float rate = 2f * power;
            foreach (KitchenFood food in pan.GetSteaks()) food.ApplyHeat(deltaTime * rate);
        }

        private void RefreshLabel(float power)
        {
            if (!label || Time.unscaledTime < nextLabelRefresh) return;
            nextLabelRefresh = Time.unscaledTime + 0.15f;
            string foodStatus = "";
            if (pan)
            {
                foreach (KitchenFood food in pan.GetSteaks())
                {
                    foodStatus = "\nSTEAK " + food.State.ToString().ToUpperInvariant() + "  " + food.CookSeconds.ToString("0.0") + " s";
                    break;
                }
            }
            label.text = "STOVE " + (power == 0f ? "OFF" : (power * 100f).ToString("0.#") + "%") + "\n"
                + (IsHeating ? "PAN HEATING" : power > 0 ? "PLACE PAN ON RING" : "TURN KNOB TO HEAT") + foodStatus;
        }

        private bool PanIsOnBurner()
        {
            if (!pan || pan.IsHeld || !burner) return false;
            Vector3 delta = pan.transform.position - burner.position;
            float vertical = Mathf.Abs(Vector3.Dot(delta, burner.up));
            float horizontal = Vector3.ProjectOnPlane(delta, burner.up).magnitude;
            return vertical <= verticalTolerance && horizontal <= horizontalTolerance && Vector3.Dot(pan.transform.up, burner.up) > 0.75f;
        }
    }
}
