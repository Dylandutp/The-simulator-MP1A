using UnityEngine;

namespace HouseSim.Kitchen
{
    public class KitchenEnergyBar : MonoBehaviour
    {
        public Transform fill;
        public Renderer fillRenderer;
        public KitchenSession session;
        public float fullWidth = 1.3f;
        MaterialPropertyBlock block;
        void Awake() { block = new MaterialPropertyBlock(); }
        void Update()
        {
            if (session == null || session.inventory == null || session.energyResource == null || fill == null) return;
            float t = Mathf.Clamp01(session.inventory.GetAmount(session.energyResource) / 100f);
            Vector3 scale = fill.localScale; scale.x = fullWidth * t; fill.localScale = scale;
            Vector3 pos = fill.localPosition; pos.x = -.5f * fullWidth + .5f * fullWidth * t; fill.localPosition = pos;
            if (fillRenderer != null)
            {
                if (block == null) block = new MaterialPropertyBlock();
                block.SetColor("_BaseColor", Color.Lerp(new Color(.85f,.18f,.12f), new Color(.23f,.69f,.4f), t));
                fillRenderer.SetPropertyBlock(block);
            }
        }
    }
}
