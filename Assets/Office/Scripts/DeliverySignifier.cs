using UnityEngine;

namespace OfficeSim
{
    public sealed class DeliverySignifier : MonoBehaviour
    {
        public Renderer surface;
        public Material idleMaterial;
        public Material animatedMaterial;
        public float scrollSpeed = 0.5f;
        [Range(0, 0.3f)] public float distortionStrength = 0.12f;
        private MaterialPropertyBlock properties;
        private float offset;

        void Awake()
        {
            properties = new MaterialPropertyBlock();
        }

        void Update()
        {
            bool ready = OfficeGame.Instance.HasCompletedFiles;
            surface.sharedMaterial = ready ? animatedMaterial : idleMaterial;
            if (ready)
            {
                offset += scrollSpeed * Time.deltaTime;
                properties.SetVector("_NoiseOffset", new Vector4(offset, offset * 0.7f, 0, 0));
                properties.SetFloat("_InwardOffset", Mathf.Repeat(offset, 1));
                properties.SetFloat("_Distortion", distortionStrength);
                surface.SetPropertyBlock(properties);
            }
            else
            {
                surface.SetPropertyBlock(null);
            }
        }
    }
}
