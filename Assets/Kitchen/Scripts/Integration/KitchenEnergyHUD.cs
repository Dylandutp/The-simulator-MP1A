using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;

namespace HouseSim.Kitchen
{
    /// <summary>Shared inventory energy, pinned to the upper-left of the player's view.</summary>
    public sealed class KitchenEnergyHUD : MonoBehaviour
    {
        public KitchenSession session;
        public Camera viewCamera;
        public Canvas DisplayCanvas { get; private set; }
        public float DisplayedFraction { get; private set; }
        private RectTransform fill;
        private Image fillImage;
        private Text valueText;
        private int previousEnergy = -1;

        private void Start()
        {
            if (!viewCamera) viewCamera = Camera.main;
            var canvasObject = new GameObject("Energy HUD", typeof(RectTransform), typeof(Canvas));
            canvasObject.transform.SetParent(transform, false);
            DisplayCanvas = canvasObject.GetComponent<Canvas>();
            DisplayCanvas.sortingOrder = 100;
            bool immersive = XRSettings.isDeviceActive || Application.platform == RuntimePlatform.Android;
            DisplayCanvas.renderMode = immersive ? RenderMode.WorldSpace : RenderMode.ScreenSpaceOverlay;
            DisplayCanvas.worldCamera = viewCamera;
            var canvasRect = (RectTransform)canvasObject.transform;
            if (immersive && viewCamera)
            {
                canvasRect.SetParent(viewCamera.transform, false);
                canvasRect.sizeDelta = new Vector2(270, 74);
                canvasRect.localPosition = new Vector3(-.25f, .22f, .75f);
                canvasRect.localRotation = Quaternion.identity;
                canvasRect.localScale = Vector3.one * .001f;
            }
            var panel = Rect("Energy panel", canvasRect, new Vector2(270, 74));
            panel.anchorMin = panel.anchorMax = immersive ? new Vector2(.5f,.5f) : new Vector2(0,1);
            panel.pivot = immersive ? new Vector2(.5f,.5f) : new Vector2(0,1);
            panel.anchoredPosition = immersive ? Vector2.zero : new Vector2(24,-24);
            var background = panel.gameObject.AddComponent<Image>();
            background.color = new Color(.24f,.29f,.27f,.65f);
            background.raycastTarget = false;
            var label = Rect("Energy value", panel, new Vector2(238,32));
            label.anchorMin = label.anchorMax = new Vector2(0,1);
            label.pivot = new Vector2(0,1);
            label.anchoredPosition = new Vector2(16,-6);
            valueText = label.gameObject.AddComponent<Text>();
            valueText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            valueText.fontSize = 18;
            valueText.alignment = TextAnchor.MiddleLeft;
            valueText.color = new Color(.96f,.94f,.84f);
            valueText.raycastTarget = false;
            var track = Rect("Energy track",panel,new Vector2(238,14));
            track.anchorMin = track.anchorMax = new Vector2(0,0);
            track.pivot = Vector2.zero;
            track.anchoredPosition = new Vector2(16,15);
            var trackImage = track.gameObject.AddComponent<Image>();
            trackImage.color = new Color(.26f,.31f,.28f);
            trackImage.raycastTarget = false;
            fill = Rect("Energy fill",track,Vector2.zero);
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = Vector2.one;
            fill.offsetMin = fill.offsetMax = Vector2.zero;
            fillImage = fill.gameObject.AddComponent<Image>();
            fillImage.raycastTarget = false;
            Refresh();
        }

        private static RectTransform Rect(string name, Transform parent, Vector2 size)
        {
            var rect = (RectTransform)new GameObject(name,typeof(RectTransform)).transform;
            rect.SetParent(parent,false);
            rect.sizeDelta = size;
            return rect;
        }

        private void LateUpdate() => Refresh();

        private void Refresh()
        {
            if (!session || !fill || previousEnergy == session.Energy) return;
            previousEnergy = session.Energy;
            DisplayedFraction = Mathf.Clamp01(previousEnergy / 100f);
            valueText.text = "ENERGY   " + previousEnergy + " / 100";
            fill.anchorMax = new Vector2(DisplayedFraction,1);
            fillImage.color = Color.Lerp(new Color(.9f,.24f,.17f),new Color(.35f,.79f,.49f),DisplayedFraction);
        }

        private void OnDestroy()
        {
            // The VR canvas is parented to MasterScene's camera, so explicitly remove it on room unload.
            if (DisplayCanvas) Destroy(DisplayCanvas.gameObject);
        }
    }
}
