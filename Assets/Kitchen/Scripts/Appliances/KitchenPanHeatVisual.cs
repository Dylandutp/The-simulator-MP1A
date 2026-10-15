using UnityEngine;
using UnityEngine.Rendering;

namespace HouseSim.Kitchen
{
    /// <summary>Only the cooking base warms in color; a bounded particle system shows hot-pan wisps.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(KitchenPan))]
    public sealed class KitchenPanHeatVisual : MonoBehaviour
    {
        [Tooltip("Referenced particle material keeps the URP shader available in Quest builds. A runtime copy is used.")]
        public Material smokeMaterialTemplate;
        public Renderer BottomRenderer => bottomRenderer;
        public ParticleSystem Smoke => smoke;
        public float DisplayedThermalLevel { get; private set; }
        public bool SmokeActive { get; private set; }

        private KitchenPan pan;
        private KitchenPanHeat heat;
        private Renderer bottomRenderer;
        private MaterialPropertyBlock originalProperties;
        private MaterialPropertyBlock warmProperties;
        private Color originalColor;
        private float appliedLevel = -1f;
        private ParticleSystem smoke;
        private Material ownedSmokeMaterial;
        private Texture2D ownedSmokeTexture;
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorProperty = Shader.PropertyToID("_Color");
        private static readonly Color HotColor = new Color(1f, .235f, .035f, 1f);

        private void Awake()
        {
            pan = GetComponent<KitchenPan>();
            heat = GetComponent<KitchenPanHeat>();
            Transform bottom = transform.Find("Pan base");
            if (bottom) bottomRenderer = bottom.GetComponent<Renderer>();
            if (bottomRenderer)
            {
                originalProperties = new MaterialPropertyBlock();
                warmProperties = new MaterialPropertyBlock();
                bottomRenderer.GetPropertyBlock(originalProperties);
                Material material = bottomRenderer.sharedMaterial;
                originalColor = originalProperties.HasProperty(BaseColor) ? originalProperties.GetColor(BaseColor)
                    : originalProperties.HasProperty(ColorProperty) ? originalProperties.GetColor(ColorProperty)
                    : material && material.HasProperty(BaseColor) ? material.GetColor(BaseColor)
                    : material && material.HasProperty(ColorProperty) ? material.GetColor(ColorProperty) : Color.gray;
            }
            CreateSmoke();
        }

        private void OnEnable() => Refresh();
        private void LateUpdate() => Refresh();

        /// <summary>Can be called after a deterministic heat tick; also refreshed each rendered frame.</summary>
        public void Refresh()
        {
            if (!isActiveAndEnabled) return;
            if (!heat) heat = GetComponent<KitchenPanHeat>();
            float level = heat ? Mathf.Clamp01(heat.ThermalLevel) : 0f;
            DisplayedThermalLevel = level;
            if (bottomRenderer && !Mathf.Approximately(appliedLevel, level))
            {
                if (level <= 0f) bottomRenderer.SetPropertyBlock(originalProperties);
                else
                {
                    Color color = Color.Lerp(originalColor, HotColor, level);
                    color.a = originalColor.a;
                    bottomRenderer.GetPropertyBlock(warmProperties);
                    warmProperties.SetColor(BaseColor, color);
                    warmProperties.SetColor(ColorProperty, color);
                    bottomRenderer.SetPropertyBlock(warmProperties);
                }
                appliedLevel = level;
            }
            bool emit = heat && heat.IsHot && level > .0001f;
            SmokeActive = emit;
            if (!smoke) return;
            var emission = smoke.emission;
            emission.rateOverTime = emit ? 6f * level : 0f;
            emission.enabled = emit;
            if (emit)
            {
                if (!smoke.isPlaying) smoke.Play(false);
            }
            else if (smoke.isPlaying)
            {
                // Existing wisps fade out over at most 1.5 seconds after cooling ends.
                smoke.Stop(false, ParticleSystemStopBehavior.StopEmitting);
            }
        }

        private void CreateSmoke()
        {
            var smokeObject = new GameObject("Pan heat wisps");
            smokeObject.transform.SetParent(transform, false);
            Transform surface = pan && pan.cookingSurface ? pan.cookingSurface : transform;
            float bottomOffset = pan ? pan.supportedLocalHeight + .02f : .02f;
            smokeObject.transform.localPosition = transform.InverseTransformPoint(surface.TransformPoint(Vector3.up * bottomOffset));
            smoke = smokeObject.AddComponent<ParticleSystem>();
            smoke.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = smoke.main;
            main.playOnAwake = false;
            main.loop = true;
            main.duration = 2f;
            main.maxParticles = 24;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1f, 1.5f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(.035f, .065f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = Color.white;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Shape;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            var emission = smoke.emission;
            emission.enabled = false;
            emission.rateOverTime = 0f;
            var shape = smoke.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = pan ? Mathf.Clamp(Mathf.Min(pan.bounds.x, pan.bounds.z) * .72f, .03f, .18f) : .14f;
            shape.radiusThickness = 1f;
            shape.rotation = new Vector3(90f, 0f, 0f);
            var velocity = smoke.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(-.018f, .018f);
            velocity.y = new ParticleSystem.MinMaxCurve(.08f, .16f);
            velocity.z = new ParticleSystem.MinMaxCurve(-.018f, .018f);
            var size = smoke.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, .6f, 1f, 1.9f));
            var color = smoke.colorOverLifetime;
            color.enabled = true;
            var fade = new Gradient();
            fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(.32f, .15f),
                    new GradientAlphaKey(.2f, .55f), new GradientAlphaKey(0f, 1f) });
            color.color = fade;
            var renderer = smoke.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.minParticleSize = 0f;
            renderer.maxParticleSize = .15f;
            ownedSmokeTexture = CreateSoftWispTexture();
            if (smokeMaterialTemplate) ownedSmokeMaterial = new Material(smokeMaterialTemplate);
            else
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
                if (!shader && bottomRenderer && bottomRenderer.sharedMaterial) shader = bottomRenderer.sharedMaterial.shader;
                if (shader) ownedSmokeMaterial = new Material(shader);
            }
            if (ownedSmokeMaterial)
            {
                ownedSmokeMaterial.name = "Pan heat wisps (runtime)";
                ownedSmokeMaterial.SetOverrideTag("RenderType", "Transparent");
                ownedSmokeMaterial.SetFloat("_Surface", 1f);
                ownedSmokeMaterial.SetFloat("_Blend", 0f);
                ownedSmokeMaterial.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                ownedSmokeMaterial.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                ownedSmokeMaterial.SetFloat("_ZWrite", 0f);
                ownedSmokeMaterial.SetFloat("_Cull", (float)CullMode.Off);
                ownedSmokeMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                ownedSmokeMaterial.DisableKeyword("_ALPHATEST_ON");
                ownedSmokeMaterial.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                ownedSmokeMaterial.SetColor(BaseColor, Color.white);
                ownedSmokeMaterial.SetColor(ColorProperty, Color.white);
                ownedSmokeMaterial.SetTexture("_BaseMap", ownedSmokeTexture);
                ownedSmokeMaterial.SetTexture("_MainTex", ownedSmokeTexture);
                ownedSmokeMaterial.renderQueue = (int)RenderQueue.Transparent;
                renderer.sharedMaterial = ownedSmokeMaterial;
            }
        }

        private static Texture2D CreateSoftWispTexture()
        {
            const int resolution = 64;
            var texture = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false, true)
            {
                name = "Pan wisp soft alpha (runtime)", wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            var pixels = new Color[resolution * resolution];
            for (int y = 0; y < resolution; y++)
                for (int x = 0; x < resolution; x++)
                {
                    float dx = ((x + .5f) / resolution - .5f) * 2f;
                    float dy = ((y + .5f) / resolution - .5f) * 2f;
                    float radius = Mathf.Sqrt(dx * dx + dy * dy);
                    float alpha = Mathf.Exp(-radius * radius * 3.5f)
                        * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.6f, 1f, radius)));
                    pixels[y * resolution + x] = new Color(1f, 1f, 1f, alpha);
                }
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            return texture;
        }

        private void OnDisable()
        {
            SmokeActive = false;
            DisplayedThermalLevel = 0f;
            appliedLevel = -1f;
            if (bottomRenderer && originalProperties != null) bottomRenderer.SetPropertyBlock(originalProperties);
            if (smoke) smoke.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        private void OnDestroy()
        {
            if (bottomRenderer && originalProperties != null) bottomRenderer.SetPropertyBlock(originalProperties);
            if (smoke) Destroy(smoke.gameObject);
            if (ownedSmokeMaterial) Destroy(ownedSmokeMaterial);
            if (ownedSmokeTexture) Destroy(ownedSmokeTexture);
        }
    }
}
