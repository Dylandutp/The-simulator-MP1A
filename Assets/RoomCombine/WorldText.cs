using UnityEngine;
namespace HouseSim.RoomCombine
{
    [RequireComponent(typeof(TextMesh), typeof(MeshRenderer))]
    public sealed class WorldText : MonoBehaviour
    {
        TextMesh text;
        Material original, depthMaterial;
        MeshRenderer target;
        void Awake()
        {
            if(depthMaterial) return;
            text=GetComponent<TextMesh>(); target=GetComponent<MeshRenderer>();
            original=target.sharedMaterial;
            var shader=Resources.Load<Shader>("RoomWorldText");
            if(!shader) { Debug.LogError("World text shader missing",this); return; }
            depthMaterial=new Material(shader) { name="World text (depth tested)" };
            RefreshAtlas(); target.sharedMaterial=depthMaterial;
        }
        void OnEnable() { Font.textureRebuilt+=FontRebuilt; RefreshAtlas(); }
        void OnDisable() { Font.textureRebuilt-=FontRebuilt; }
        void FontRebuilt(Font font) { if(!text.font || text.font==font) RefreshAtlas(); }
        void LateUpdate() { RefreshAtlas(); }
        void RefreshAtlas()
        {
            if(!depthMaterial || !text)return;
            var source=text.font ? text.font.material : original;
            if(source && depthMaterial.mainTexture!=source.mainTexture) depthMaterial.mainTexture=source.mainTexture;
        }
        void OnDestroy()
        {
            if(target && target.sharedMaterial==depthMaterial) target.sharedMaterial=original;
            if(depthMaterial) { if(Application.isPlaying) Destroy(depthMaterial); else DestroyImmediate(depthMaterial); }
        }
        public static void Apply(GameObject root)
        {
            foreach(var text in root.GetComponentsInChildren<TextMesh>(true))
                if(!text.GetComponent<WorldText>()) text.gameObject.AddComponent<WorldText>();
        }
    }
}
