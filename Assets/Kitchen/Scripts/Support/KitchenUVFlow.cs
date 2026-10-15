using UnityEngine;

namespace HouseSim.Kitchen
{
    /// <summary>Scrolls a repeated stripe texture on an existing mesh without cloning its material.</summary>
    public sealed class KitchenUVFlow : MonoBehaviour
    {
        public Renderer target;
        public float speed = 0.35f;
        public Vector2 direction = Vector2.right;
        public KitchenToggle source;
        public bool generateStripes = true;
        public Color stripeColor = new Color(1f, 0.45f, 0.08f);
        public Color backgroundColor = new Color(0.12f, 0.025f, 0.01f);
        MaterialPropertyBlock properties;
        Texture2D stripeTexture;
        Vector2 offset;
        Vector2 tiling = Vector2.one;
        static readonly int BaseMap = Shader.PropertyToID("_BaseMap");
        static readonly int MainTex = Shader.PropertyToID("_MainTex");
        static readonly int BaseMapST = Shader.PropertyToID("_BaseMap_ST");
        static readonly int MainTexST = Shader.PropertyToID("_MainTex_ST");

        void Start()
        {
            if (target == null) target = GetComponent<Renderer>();
            if (target == null) { enabled = false; return; }
            properties = new MaterialPropertyBlock();
            Material material = target.sharedMaterial;
            if (material != null)
            {
                string property = material.HasProperty(BaseMap) ? "_BaseMap" : "_MainTex";
                if (material.HasProperty(property))
                {
                    tiling = material.GetTextureScale(property);
                    offset = material.GetTextureOffset(property);
                }
            }
            if (generateStripes)
            {
                stripeTexture = new Texture2D(64, 8, TextureFormat.RGBA32, false)
                {
                    name = "Kitchen runtime heat stripes",
                    wrapMode = TextureWrapMode.Repeat,
                    filterMode = FilterMode.Bilinear,
                    hideFlags = HideFlags.DontSave
                };
                var pixels = new Color[64 * 8];
                for (int y = 0; y < 8; ++y)
                    for (int x = 0; x < 64; ++x)
                        pixels[y * 64 + x] = (x + y * 2) % 32 < 12 ? stripeColor : backgroundColor;
                stripeTexture.SetPixels(pixels);
                stripeTexture.Apply(false, true);
            }
            Apply();
        }

        void Update()
        {
            if (source != null && !source.Value) return;
            offset += direction * (speed * Time.deltaTime);
            offset.x = Mathf.Repeat(offset.x, 1f);
            offset.y = Mathf.Repeat(offset.y, 1f);
            Apply();
        }

        void Apply()
        {
            if (target == null || properties == null) return;
            target.GetPropertyBlock(properties);
            if (stripeTexture != null)
            {
                properties.SetTexture(BaseMap, stripeTexture);
                properties.SetTexture(MainTex, stripeTexture);
            }
            Vector4 value = new Vector4(tiling.x, tiling.y, offset.x, offset.y);
            properties.SetVector(BaseMapST, value);
            properties.SetVector(MainTexST, value);
            target.SetPropertyBlock(properties);
        }

        void OnDestroy()
        {
            if (stripeTexture != null) Destroy(stripeTexture);
        }
    }
}
