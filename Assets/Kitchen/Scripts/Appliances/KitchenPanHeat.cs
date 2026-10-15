using UnityEngine;

namespace HouseSim.Kitchen
{
    /// <summary>The pan owns its temperature, residual heat, food cooking and sizzle.</summary>
    [DisallowMultipleComponent]
    public sealed class KitchenPanHeat : MonoBehaviour
    {
        public const float FullPowerPreheatSeconds = 5f;
        public const float RetainedHeatSeconds = 15f;
        public float ThermalLevel { get; private set; }
        public float HeatProgress => ThermalLevel;
        public bool IsHot { get; private set; }
        public bool Heating { get; private set; }
        public bool IsHeating => Heating;
        public bool IsCooking { get; private set; }
        public float ResidualSeconds { get; private set; }
        public float CookingRate { get; private set; }
        public float ActivePower { get; private set; }

        private KitchenPan pan;
        private KitchenAudioEmitter audioEmitter;
        private int markedFrame = -1;
        private int simulatedFrame = -1;
        private float markedPower;
        private float markedRate;

        private void Awake()
        {
            pan = GetComponent<KitchenPan>();
            audioEmitter = KitchenAudioEmitter.Get(this);
            if (!GetComponent<KitchenPanHeatVisual>()) gameObject.AddComponent<KitchenPanHeatVisual>();
        }

        /// <summary>Burners report input; even overlapping burners heat this pan only once per frame.</summary>
        public void MarkHeatingFrame(float power, float cookingMultiplier)
        {
            if (!FinitePositive(power) || !FinitePositive(cookingMultiplier)) return;
            power = Mathf.Clamp01(power);
            if (markedFrame != Time.frameCount)
            {
                markedFrame = Time.frameCount;
                markedPower = markedRate = 0f;
            }
            if (power < markedPower) return;
            markedPower = power;
            markedRate = cookingMultiplier;
        }

        private void LateUpdate()
        {
            // Explicit deterministic ticks used by editor checks must not also run automatically.
            if (simulatedFrame != Time.frameCount) Tick(Time.deltaTime);
        }

        public void Tick(float deltaTime)
        {
            if (float.IsNaN(deltaTime) || float.IsInfinity(deltaTime) || deltaTime < 0f) return;
            simulatedFrame = Time.frameCount;
            ActivePower = markedFrame == Time.frameCount ? markedPower : 0f;
            float inputRate = markedRate;
            markedFrame = -1;
            markedPower = markedRate = 0f;
            Heating = ActivePower > 0f;
            float cookingTime = 0f;
            if (Heating)
            {
                CookingRate = inputRate;
                if (IsHot)
                {
                    ThermalLevel = 1f;
                    ResidualSeconds = RetainedHeatSeconds;
                    cookingTime = deltaTime;
                }
                else
                {
                    float untilHot = Mathf.Max(0f, 1f - ThermalLevel) * FullPowerPreheatSeconds / ActivePower;
                    ThermalLevel = Mathf.Clamp01(ThermalLevel + deltaTime * ActivePower / FullPowerPreheatSeconds);
                    if (deltaTime + .000001f >= untilHot)
                    {
                        IsHot = true;
                        ThermalLevel = 1f;
                        ResidualSeconds = RetainedHeatSeconds;
                        // A frame that crosses the threshold cooks only for its hot remainder.
                        cookingTime = Mathf.Max(0f, deltaTime - untilHot);
                    }
                }
            }
            else if (IsHot)
            {
                cookingTime = Mathf.Min(deltaTime, ResidualSeconds);
                ResidualSeconds = Mathf.Max(0f, ResidualSeconds - deltaTime);
                ThermalLevel = ResidualSeconds / RetainedHeatSeconds;
                IsHot = ResidualSeconds > 0f;
            }
            else
            {
                ThermalLevel = Mathf.Max(0f, ThermalLevel - deltaTime / RetainedHeatSeconds);
                ResidualSeconds = 0f;
            }

            bool hasFood = false;
            if (pan && (IsHot || cookingTime > 0f))
            {
                foreach (KitchenFood food in pan.GetSteaks())
                {
                    if (!pan.CanHeatFood(food)) continue;
                    if (cookingTime > 0f) food.ApplyHeat(cookingTime * CookingRate);
                    hasFood = true;
                }
            }
            IsCooking = IsHot && hasFood;
            audioEmitter.SetLoop(KitchenSound.Cooking, IsCooking);
        }

        private void OnDisable()
        {
            markedFrame = -1;
            markedPower = markedRate = 0f;
            Heating = IsCooking = false;
            ActivePower = 0f;
            if (audioEmitter) audioEmitter.SetLoop(KitchenSound.Cooking, false);
        }

        private static bool FinitePositive(float value) => value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
