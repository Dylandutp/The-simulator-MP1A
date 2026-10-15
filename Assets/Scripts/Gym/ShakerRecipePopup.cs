using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.UI;

namespace HouseSim.Gym
{
    [Serializable]
    public sealed class RecipeResourceAmount
    {
        public string resource;
        public float amount = 1f;

        public RecipeResourceAmount(string resourceName, float resourceAmount)
        {
            resource = resourceName;
            amount = resourceAmount;
        }
    }

    [Serializable]
    public sealed class ShakerRecipe
    {
        public string recipeName;
        public List<RecipeResourceAmount> inputs = new List<RecipeResourceAmount>();
        public List<RecipeResourceAmount> outputs = new List<RecipeResourceAmount>();

        public ShakerRecipe(string name, RecipeResourceAmount[] recipeInputs, RecipeResourceAmount[] recipeOutputs)
        {
            recipeName = name;
            inputs.AddRange(recipeInputs);
            outputs.AddRange(recipeOutputs);
        }
    }

    public sealed class ShakerRecipePopup : MonoBehaviour
    {
        [SerializeField] PopupPanel popupPanel;
        [SerializeField] PlayerResources playerResources;
        [SerializeField] QuestProgress questProgress;
        [SerializeField] MachineEffects shakerEffects;
        [SerializeField] AudioSource audioSource;
        [SerializeField] AudioClip craftSound;
        [SerializeField] AudioClip errorSound;
        [SerializeField] List<ShakerRecipe> recipes = new List<ShakerRecipe>();
        [SerializeField] List<Button> recipeButtons = new List<Button>();

        MachineEffects activeShakerEffects;
        public bool IsOpen => popupPanel != null && popupPanel.IsOpen;

        void Awake()
        {
            ResolveReferences();
            KeepSixRecipes();
            RefreshRecipeLabels();
        }

        void ResolveReferences()
        {
            if (popupPanel == null) popupPanel = GetComponentInChildren<PopupPanel>(true);
            if (popupPanel == null) popupPanel = GetComponentInParent<PopupPanel>(true);
            if (playerResources == null) playerResources = FindAnyObjectByType<PlayerResources>();
            if (questProgress == null) questProgress = FindAnyObjectByType<QuestProgress>();
        }

        void Reset() => ResetRecipesToDefaults();

        void Update()
        {
            if (popupPanel == null || !popupPanel.IsOpen) return;

            int count = Mathf.Min(recipes.Count, recipeButtons.Count);
            for (int i = 0; i < count; i++)
                if (recipeButtons[i] != null) recipeButtons[i].interactable = CanCraft(i);
        }

        public void OpenShakerMenu(MachineEffects sourceEffects)
        {
            ResolveReferences();
            activeShakerEffects = sourceEffects != null ? sourceEffects : shakerEffects;
            if (popupPanel == null)
            {
                Debug.LogError("The shaker recipe popup has no PopupPanel assigned.", this);
                return;
            }
            popupPanel.OpenSilently();
        }

        public void CloseShakerMenu() => popupPanel?.Close();

        public bool CanCraft(int recipeIndex)
        {
            if (playerResources == null) playerResources = FindAnyObjectByType<PlayerResources>();
            if (playerResources == null || recipeIndex < 0 || recipeIndex >= recipes.Count) return false;

            foreach (RecipeResourceAmount input in recipes[recipeIndex].inputs)
            {
                if (input == null || !playerResources.Has(input.resource, input.amount)) return false;
            }
            return true;
        }

        public void Craft(int recipeIndex)
        {
            if (!CanCraft(recipeIndex))
            {
                Play(errorSound);
                return;
            }

            ShakerRecipe recipe = recipes[recipeIndex];
            foreach (RecipeResourceAmount input in recipe.inputs)
                playerResources.TrySpend(input.resource, input.amount);
            foreach (RecipeResourceAmount output in recipe.outputs)
                playerResources.Add(output.resource, output.amount);

            questProgress?.Increment("craft", 1);
            questProgress?.MarkRecipeCrafted(recipe.recipeName);
            if (activeShakerEffects != null) activeShakerEffects.PlayTimedEffect(craftSound, 5f);
            else Play(craftSound);
            SendMixHaptic();
        }

        void ResetRecipesToDefaults()
        {
            recipes = CreateDefaultRecipes();
            RefreshRecipeLabels();
        }

        void KeepSixRecipes()
        {
            List<ShakerRecipe> defaults = CreateDefaultRecipes();
            if (recipes == null || recipes.Count == 0) recipes = defaults;

            ShakerRecipe recovery = recipes.Find(recipe => recipe != null && string.Equals(recipe.recipeName, "Recovery Shake", StringComparison.OrdinalIgnoreCase));
            if (recovery == null) recovery = defaults.Find(recipe => recipe.recipeName == "Recovery Shake");
            recipes.RemoveAll(recipe => recipe != null && string.Equals(recipe.recipeName, "Recovery Shake", StringComparison.OrdinalIgnoreCase));
            if (recipes.Count >= 6) recipes.RemoveRange(5, recipes.Count - 5);
            recipes.Add(recovery);
            if (recipes.Count > 6) recipes.RemoveRange(6, recipes.Count - 6);
            for (int i = 6; i < recipeButtons.Count; i++)
                if (recipeButtons[i] != null) recipeButtons[i].gameObject.SetActive(false);
        }

        public void RefreshRecipeLabels()
        {
            int count = Mathf.Min(recipes.Count, recipeButtons.Count);
            for (int i = 0; i < count; i++)
            {
                Button button = recipeButtons[i];
                if (button == null) continue;

                ShakerRecipe recipe = recipes[i];
                button.gameObject.name = recipe.recipeName;
                TMP_Text[] labels = button.GetComponentsInChildren<TMP_Text>(true);
                foreach (TMP_Text label in labels)
                {
                    if (label.gameObject.name == "Name") label.text = recipe.recipeName.ToUpperInvariant();
                    else if (label.gameObject.name == "Details") label.text = BuildDetails(recipe);
                }
            }
        }

        void Play(AudioClip clip)
        {
            if (audioSource != null && clip != null) audioSource.PlayOneShot(clip);
        }

        static void SendMixHaptic()
        {
            InputDevice controller = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
            if (controller.TryGetHapticCapabilities(out HapticCapabilities capabilities) && capabilities.supportsImpulse)
                controller.SendHapticImpulse(0, .2f, .15f);
        }

        static string BuildDetails(ShakerRecipe recipe)
        {
            return JoinAmounts(recipe.inputs, false) + "\n" + JoinAmounts(recipe.outputs, true);
        }

        static string JoinAmounts(List<RecipeResourceAmount> amounts, bool showSign)
        {
            var text = new StringBuilder();
            for (int i = 0; i < amounts.Count; i++)
            {
                if (i > 0) text.Append("  ·  ");
                RecipeResourceAmount amount = amounts[i];
                text.Append(DisplayResource(amount.resource));
                if (showSign) text.Append(amount.amount >= 0f ? " +" : " ");
                else text.Append(" ×");
                text.Append(amount.amount.ToString("0.#"));
            }
            return text.ToString();
        }

        static string DisplayResource(string resource)
        {
            if (resource == "energyStick") return "Energy Stick";
            if (string.IsNullOrEmpty(resource)) return string.Empty;
            return char.ToUpperInvariant(resource[0]) + resource.Substring(1);
        }

        static List<ShakerRecipe> CreateDefaultRecipes()
        {
            return new List<ShakerRecipe>
            {
                Recipe("Protein Shake", In("water", 1f), In("powder", 1f), Out("energy", 20f)),
                Recipe("Smoothie", In("milk", 1f), In("fruit", 1f), Out("energy", 10f), Out("hydration", 10f)),
                Recipe("Energy Drink", In("water", 1f), In("energyStick", 1f), Out("energy", 30f)),
                Recipe("Protein Milkshake", In("milk", 1f), In("powder", 1f), Out("health", 15f)),
                Recipe("Fruit Water", In("water", 1f), In("fruit", 1f), Out("hydration", 20f)),
                Recipe("Recovery Shake", In("water", 1f), In("fruit", 1f), In("powder", 1f), Out("health", 20f), Out("hydration", 10f), Out("stress", -10f)),
            };
        }

        static ShakerRecipe Recipe(string name, params TaggedAmount[] amounts)
        {
            var inputs = new List<RecipeResourceAmount>();
            var outputs = new List<RecipeResourceAmount>();
            foreach (TaggedAmount amount in amounts)
            {
                var entry = new RecipeResourceAmount(amount.resource, amount.amount);
                if (amount.isInput) inputs.Add(entry);
                else outputs.Add(entry);
            }
            return new ShakerRecipe(name, inputs.ToArray(), outputs.ToArray());
        }

        static TaggedAmount In(string resource, float amount) => new TaggedAmount(resource, amount, true);
        static TaggedAmount Out(string resource, float amount) => new TaggedAmount(resource, amount, false);

        readonly struct TaggedAmount
        {
            public readonly string resource;
            public readonly float amount;
            public readonly bool isInput;

            public TaggedAmount(string resourceName, float resourceAmount, bool input)
            {
                resource = resourceName;
                amount = resourceAmount;
                isInput = input;
            }
        }
    }
}
