using UnityEngine;

namespace HouseSim.Kitchen
{
    public enum FoodKind { Chicken, Steak, Tomato, Lettuce, Apple, Salad }
    public enum FoodState { Raw, Good, Burnt }

    [CreateAssetMenu(fileName = "KitchenFood", menuName = "Kitchen/Food Definition")]
    public class KitchenFoodDefinition : ScriptableObject
    {
        public string displayName;
        public FoodKind kind;
        public ResourceData rawResource;
        public ResourceData preparedResource;
        public ResourceData cookedResource;
        public GameObject prefab;
        public GameObject choppedPrefab;
        public Color rawColor = Color.white;
        public Color cookedColor = new Color(0.72f, 0.36f, 0.12f);
        public Color burntColor = new Color(0.12f, 0.07f, 0.04f);
        [Min(0.01f)] public float goodAtSeconds = 20f;
        [Min(0.02f)] public float burntAtSeconds = 40f;

        public bool IsProduce => kind == FoodKind.Tomato || kind == FoodKind.Lettuce || kind == FoodKind.Apple;

        private void OnValidate()
        {
            goodAtSeconds = Mathf.Max(0.01f, goodAtSeconds);
            burntAtSeconds = Mathf.Max(goodAtSeconds + 0.01f, burntAtSeconds);
        }
    }
}
