using UnityEngine;

namespace HouseSim.Kitchen
{
    /// <summary>Short inspectable descriptions, shared by mouse and controller pointers.</summary>
    public sealed class KitchenItemInfo : MonoBehaviour
    {
        public string title;
        [TextArea] public string description;
        KitchenFridge fridge;
        KitchenOven oven;
        KitchenHob hob;
        KitchenSaladBowl bowl;
        KitchenFood food;
        KitchenPan pan;
        KitchenPendantLights lights;
        public Vector3 InitialDescriptionPosition { get; private set; }
        bool positionCaptured;
        void Start() => CaptureDescriptionPosition();

        public void CaptureDescriptionPosition()
        {
            if (positionCaptured) return;
            var bounds = new Bounds(transform.position, Vector3.zero);
            bool found = false;
            foreach (var renderer in GetComponentsInChildren<Renderer>())
            {
                // Stock inside a fridge has its own description and must not move the fridge's anchor.
                if (!renderer.enabled || renderer.GetComponentInParent<KitchenItemInfo>() != this) continue;
                if (!found) { bounds = renderer.bounds; found = true; }
                else bounds.Encapsulate(renderer.bounds);
            }
            InitialDescriptionPosition = new Vector3(bounds.center.x, bounds.max.y + .12f, bounds.center.z);
            positionCaptured = true;
        }
        void Awake()
        {
            fridge = GetComponent<KitchenFridge>(); oven = GetComponent<KitchenOven>();
            hob = GetComponent<KitchenHob>(); bowl = GetComponentInChildren<KitchenSaladBowl>();
            food = GetComponent<KitchenFood>(); lights = GetComponent<KitchenPendantLights>();
            pan = GetComponent<KitchenPan>();
        }
        public string ReadTitle()
        {
            if (fridge) return title + "  |  Stock " + fridge.CountStock() + "/" + fridge.slots.Length;
            if (oven) return title + "  |  " + oven.RemainingSeconds.ToString("0.0") + " s remaining";
            if (hob) return title + "  |  Heat " + (hob.powerKnob.NormalizedValue * 100).ToString("0") + "%";
            if (pan)
            {
                var heat = pan.GetComponent<KitchenPanHeat>();
                return title + (heat && heat.IsHot ? "  |  HOT" : heat ? "  |  Preheat " + (heat.ThermalLevel*100).ToString("0") + "%" : "");
            }
            if (bowl) return title + "  |  " + bowl.ingredientCount + " ingredients, " + bowl.cleanCount + " clean";
            if (food) return (food.definition ? food.definition.displayName : "Food") + "  |  "
                + (food.IsProduce ? "Washed " + (food.WashProgress*100).ToString("0") + "%" + CutDescription()
                    : food.State + (food.IsDirty ? " / UNCLEAN" : ""));
            if (lights) return title + "  |  " + (lights.IsOn ? "ON" : "OFF");
            return title;
        }

        public string ReadDescription()
        {
            if (fridge) return "Fully close the door, then reopen to refill empty slots. A small wiggle does not restock.";
            if (oven) return oven.timerKnob && oven.timerKnob.IsDragging
                ? "Set 25-30 s, then release the dial to start. The door must be closed."
                : "Release chicken inside the open oven. Close door; set 25-30 s and release the dial.";
            if (pan)
            {
                var heat = pan.GetComponent<KitchenPanHeat>();
                if (heat && heat.IsHot) return heat.Heating
                    ? "Hot: steak cooks and sizzles. Lift to toss; residual heat lasts 15 s."
                    : "Hot for " + heat.ResidualSeconds.ToString("0.0") + " s more. Return to a powered burner to restore heat.";
                return "Preheat on the burner: 5 s at 100%. Steak only cooks once hot. Release it just over the pan to seat it.";
            }
            if (hob) return "Preheat pan: 5 s at 100%, 10 s at 50%. Orange-red and white wisps mean hot; steak can then cook.";
            if (GetComponent<KitchenKnife>()) return "Pickup aligns the blade downward. Lift and cut twice. All cut pieces stay together. PC: aim at nearby produce + C.";
            if (GetComponentInChildren<KitchenWashZone>()) return "Open the tap and rinse produce for 0.1 s. Its color gradually becomes clean.";
            if (food && food.IsDirty && food.definition && food.definition.kind == FoodKind.Steak)
                return "Spilled steak is unclean. Eating it costs 15 energy at any doneness; cooking cannot clean it.";
            return description;
        }
        private string CutDescription()
        {
            if (!food || food.CutStage <= 0) return "";
            var models = food.GetComponent<KitchenFoodModelVisual>();
            int count = models ? models.GetPieceCount(food.CutStage) : food.CutStage == 1 ? 2 : 4;
            return " / " + count + " pieces";
        }
    }
}
