using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace HouseSim.Kitchen
{
    /// <summary>Pointer-only inspection card anchored above the item's initial world position.</summary>
    public sealed class KitchenContextHUD : MonoBehaviour
    {
        public Camera viewCamera;
        public Transform playerRoot;
        public KitchenDesktopController desktop;
        public KitchenItemInfo Current { get; private set; }
        public Canvas DisplayCanvas { get; private set; }
        public string DisplayedTitle => titleText ? titleText.text : "";
        XRBaseInteractor[] interactors;
        readonly RaycastHit[] hits = new RaycastHit[32];
        CanvasGroup group;
        RectTransform panel;
        Text titleText, bodyText;
        float nextScan;

        void Start()
        {
            interactors = playerRoot.GetComponentsInChildren<XRBaseInteractor>(true);
            var go = new GameObject("Pointed item HUD",typeof(RectTransform),typeof(Canvas),typeof(CanvasGroup));
            go.transform.SetParent(transform,false);
            DisplayCanvas = go.GetComponent<Canvas>();
            DisplayCanvas.renderMode = RenderMode.WorldSpace;
            DisplayCanvas.worldCamera = viewCamera;
            DisplayCanvas.sortingOrder = 90;
            group = go.GetComponent<CanvasGroup>(); group.alpha = 0; group.blocksRaycasts = false; group.interactable = false;
            var canvasRect = (RectTransform)go.transform;
            canvasRect.sizeDelta = new Vector2(560,120);
            canvasRect.pivot = new Vector2(.5f,0);
            canvasRect.localScale = Vector3.one*.0012f;
            panel = Rect("Inspect card",canvasRect);
            panel.anchorMin = panel.anchorMax = new Vector2(.5f,0);
            panel.pivot = new Vector2(.5f,0);
            panel.sizeDelta = new Vector2(560,120);
            panel.anchoredPosition = Vector2.zero;
            var background = panel.gameObject.AddComponent<Image>();
            background.color = new Color(.025f,.05f,.045f,.82f); background.raycastTarget = false;
            titleText = Label("Item title",panel,20,new Color(.94f,.90f,.75f),new Vector2(16,-8),new Vector2(-16,-34));
            bodyText = Label("Item description",panel,17,new Color(.87f,.89f,.85f),new Vector2(16,-36),new Vector2(-16,-112));
        }
        static RectTransform Rect(string name,Transform parent)
        {
            var rect = (RectTransform)new GameObject(name,typeof(RectTransform)).transform;
            rect.SetParent(parent,false); return rect;
        }
        static Text Label(string name,Transform parent,int fontSize,Color color,Vector2 topLeft,Vector2 bottomRight)
        {
            var rect=Rect(name,parent); rect.anchorMin=new Vector2(0,1); rect.anchorMax=new Vector2(1,1);
            rect.offsetMin=new Vector2(topLeft.x,bottomRight.y); rect.offsetMax=new Vector2(bottomRight.x,topLeft.y);
            var text=rect.gameObject.AddComponent<Text>(); text.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize=fontSize; text.alignment=TextAnchor.UpperLeft; text.color=color; text.raycastTarget=false;
            return text;
        }
        void LateUpdate()
        {
            if (!group) return;
            if (Time.unscaledTime >= nextScan)
            {
                nextScan=Time.unscaledTime+.08f;
                var previous = Current;
                Current = FindPointedItem();
                if (Current && Current != previous) PositionAtItem(Current);
                if (Current) { titleText.text=Current.ReadTitle(); bodyText.text=Current.ReadDescription(); }
            }
            group.alpha=Mathf.MoveTowards(group.alpha,Current ? 1 : 0,Time.unscaledDeltaTime*7);
        }
        void PositionAtItem(KitchenItemInfo item)
        {
            item.CaptureDescriptionPosition();
            var card = DisplayCanvas.transform;
            card.position = item.InitialDescriptionPosition;
            // Face the viewer once on appearance; neither position nor rotation follows the head.
            if (viewCamera)
            {
                Vector3 forward = card.position - viewCamera.transform.position;
                if (forward.sqrMagnitude > .0001f) card.rotation = Quaternion.LookRotation(forward, Vector3.up);
            }
        }
        KitchenItemInfo FindPointedItem()
        {
            if (desktop && desktop.IsDesktopActive)
                return desktop.TryGetPointedCollider(out var collider) ? collider.GetComponentInParent<KitchenItemInfo>() : null;
            KitchenItemInfo candidate=null;
            float closest=float.PositiveInfinity;
            foreach (var interactor in interactors)
            {
                if (!interactor || !interactor.isActiveAndEnabled || !(interactor is NearFarInteractor) || !(interactor is IXRRayProvider ray)) continue;
                Transform from=ray.GetOrCreateRayOrigin();
                var info=InspectRay(new Ray(from.position,from.forward),out float distance);
                // Retain the currently pointed item when both hands target different devices.
                if (info && info==Current) return info;
                if (info && distance<closest) { closest=distance; candidate=info; }
            }
            return candidate;
        }
        public KitchenItemInfo InspectRay(Ray ray,out float distance)
        {
            distance=float.PositiveInfinity; Collider nearest=null;
            int count=Physics.RaycastNonAlloc(ray,hits,4f,~0,QueryTriggerInteraction.Ignore);
            for(int i=0;i<count;i++)
            {
                if (playerRoot && hits[i].collider.transform.IsChildOf(playerRoot)) continue;
                if(hits[i].distance<distance){distance=hits[i].distance; nearest=hits[i].collider;}
            }
            return nearest ? nearest.GetComponentInParent<KitchenItemInfo>() : null;
        }
        void OnDestroy() { if(DisplayCanvas) Destroy(DisplayCanvas.gameObject); }
    }
}
