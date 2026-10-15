using UnityEngine;

namespace HouseSim.Kitchen
{
    public sealed class KitchenPendantLights : MonoBehaviour
    {
        public Light[] lights;
        public Renderer[] diffusers;
        public Transform rocker;
        [SerializeField] bool isOn = true;
        public bool IsOn => isOn;
        MaterialPropertyBlock block;
        KitchenAudioEmitter audioEmitter;
        void Awake()
        {
            audioEmitter = KitchenAudioEmitter.Get(this);
            Apply();
        }
        public void Toggle()
        {
            isOn = !isOn;
            Apply();
            audioEmitter.PlayCue(KitchenSound.LightSwitch);
        }
        public void Apply()
        {
            if (block == null) block = new MaterialPropertyBlock();
            if (lights != null) foreach (var light in lights) if (light) light.enabled = isOn;
            if (diffusers != null) foreach (var diffuser in diffusers)
            {
                if (!diffuser) continue;
                diffuser.GetPropertyBlock(block);
                block.SetColor("_EmissionColor", isOn ? new Color(1,.76f,.42f)*1.3f : Color.black);
                diffuser.SetPropertyBlock(block);
            }
            if (rocker) rocker.localRotation = Quaternion.Euler(isOn ? -12 : 12,0,0);
        }
    }
}
