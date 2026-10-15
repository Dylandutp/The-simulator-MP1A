using UnityEngine;

namespace HouseSim.Kitchen
{
    /// <summary>
    /// Fits a TextMesh inside a rectangle in its own local X/Y axes. Use an unscaled
    /// parent when the limits should correspond to world metres. The font atlas size
    /// stays unchanged; only characterSize changes, preserving crisp 64-point glyphs.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TextMesh))]
    public sealed class KitchenTextFit : MonoBehaviour
    {
        [Min(0f)] public float maxWidth = 1.15f;
        [Min(0f)] public float maxHeight = 0.32f;
        [Tooltip("Largest allowed character size. Zero captures the TextMesh's authored character size.")]
        [Min(0f)] public float preferredCharacterSize;
        [SerializeField, HideInInspector] float authoredCharacterSize;

        TextMesh textMesh;
        Renderer textRenderer;
        Vector2 lastMeasuredSize;
        Vector2 sizeBeforeResize;
        float lastAppliedSize;
        float previousMaxWidth;
        float previousMaxHeight;
        float previousPreferredSize;
        bool hasMeasurement;
        bool waitingForGeometry;
        bool forceFit;

        void OnEnable()
        {
            CacheComponents();
            forceFit = true;
            waitingForGeometry = false;
            hasMeasurement = false;
        }

        void OnValidate()
        {
            maxWidth = Mathf.Max(0f, maxWidth);
            maxHeight = Mathf.Max(0f, maxHeight);
            preferredCharacterSize = Mathf.Max(0f, preferredCharacterSize);
            forceFit = true;
        }

        void CacheComponents()
        {
            if (textMesh == null) textMesh = GetComponent<TextMesh>();
            if (textRenderer == null) textRenderer = GetComponent<Renderer>();
            if (textMesh == null) return;
            if (authoredCharacterSize <= 0f) authoredCharacterSize = Mathf.Max(0.00001f, textMesh.characterSize);
            lastAppliedSize = textMesh.characterSize;
        }

        /// <summary>Requests a refit after changing a label or its layout constraints.</summary>
        public void Refit() => forceFit = true;
        public void FitNow() => LateUpdate();

        void LateUpdate()
        {
            if (textMesh == null || textRenderer == null)
            {
                CacheComponents();
                if (textMesh == null || textRenderer == null) return;
            }

            float characterSize = textMesh.characterSize;
            bool externallyResized = !Mathf.Approximately(characterSize, lastAppliedSize);
            if (externallyResized)
            {
                // An Inspector edit establishes a new preferred size. A size written by
                // this fitter never replaces the authored baseline, so short labels can grow back.
                if (characterSize > 0f) authoredCharacterSize = characterSize;
                lastAppliedSize = characterSize;
                waitingForGeometry = false;
                forceFit = true;
            }

            bool limitsChanged = !Mathf.Approximately(previousMaxWidth, maxWidth)
                || !Mathf.Approximately(previousMaxHeight, maxHeight)
                || !Mathf.Approximately(previousPreferredSize, preferredCharacterSize);

            // Local bounds avoid rotated world-axis bounding boxes. Observing geometry
            // also avoids polling TextMesh.text's native string getter every frame.
            Vector3 boundsSize = textRenderer.localBounds.size;
            Vector2 measured = new Vector2(Mathf.Abs(boundsSize.x), Mathf.Abs(boundsSize.y));
            if (!IsFinite(measured.x) || !IsFinite(measured.y) || characterSize <= 0f) return;
            if (measured.x <= 0.000001f || measured.y <= 0.000001f) return;

            bool geometryChanged = !hasMeasurement || !SameSize(measured, lastMeasuredSize);
            if (waitingForGeometry && SameSize(measured, sizeBeforeResize))
            {
                // TextMesh can rebuild at the end of the frame. Do not multiply a new
                // characterSize by bounds that still describe the preceding font geometry.
                return;
            }
            waitingForGeometry = false;
            if (!forceFit && !limitsChanged && !geometryChanged) return;

            previousMaxWidth = maxWidth;
            previousMaxHeight = maxHeight;
            previousPreferredSize = preferredCharacterSize;
            lastMeasuredSize = measured;
            hasMeasurement = true;
            forceFit = false;

            float wanted = preferredCharacterSize > 0f ? preferredCharacterSize : authoredCharacterSize;
            if (maxWidth > 0f) wanted = Mathf.Min(wanted, characterSize * maxWidth / measured.x);
            if (maxHeight > 0f) wanted = Mathf.Min(wanted, characterSize * maxHeight / measured.y);
            wanted = Mathf.Max(0.000001f, wanted);
            if (Mathf.Abs(wanted - characterSize) <= Mathf.Max(0.000001f, characterSize * 0.0005f)) return;

            sizeBeforeResize = measured;
            waitingForGeometry = true;
            lastAppliedSize = wanted;
            textMesh.characterSize = wanted;
        }

        static bool SameSize(Vector2 left, Vector2 right)
        {
            return Mathf.Abs(left.x - right.x) < 0.000001f
                && Mathf.Abs(left.y - right.y) < 0.000001f;
        }

        static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
