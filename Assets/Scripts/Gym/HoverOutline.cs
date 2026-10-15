using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace HouseSim.Gym
{
    /// <summary>Adds the shared outline material while the controller ray points at this item.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(XRBaseInteractable))]
    public sealed class HoverOutline : MonoBehaviour
    {
        [SerializeField] Renderer[] targetRenderers = Array.Empty<Renderer>();
        [SerializeField] Material outlineMaterial;

        XRBaseInteractable interactable;
        Material[][] originalMaterials;
        bool xriHovered;
        bool manuallyHighlighted;
        bool outlineApplied;

        public void Bind(XRBaseInteractable target)
        {
            if (interactable == target) return;
            Unsubscribe();
            interactable = target;
            Subscribe();
        }

        public void SetManualHighlight(bool highlighted)
        {
            manuallyHighlighted = highlighted;
            ApplyState();
        }

        void Awake()
        {
            CacheOriginalMaterials();
            Bind(GetComponent<XRBaseInteractable>());
        }

        void OnEnable()
        {
            if (interactable == null) interactable = GetComponent<XRBaseInteractable>();
            Subscribe();
        }

        void OnDisable()
        {
            Unsubscribe();
            xriHovered = false;
            manuallyHighlighted = false;
            RestoreOriginalMaterials();
        }

        void Subscribe()
        {
            if (!isActiveAndEnabled || interactable == null) return;
            interactable.hoverEntered.RemoveListener(OnHoverEntered);
            interactable.hoverExited.RemoveListener(OnHoverExited);
            interactable.hoverEntered.AddListener(OnHoverEntered);
            interactable.hoverExited.AddListener(OnHoverExited);
        }

        void Unsubscribe()
        {
            if (interactable == null) return;
            interactable.hoverEntered.RemoveListener(OnHoverEntered);
            interactable.hoverExited.RemoveListener(OnHoverExited);
        }

        void OnHoverEntered(HoverEnterEventArgs _)
        {
            xriHovered = true;
            ApplyState();
        }

        void OnHoverExited(HoverExitEventArgs _)
        {
            xriHovered = false;
            ApplyState();
        }

        void ApplyState()
        {
            if (xriHovered || manuallyHighlighted) ApplyOutline();
            else RestoreOriginalMaterials();
        }

        void ApplyOutline()
        {
            if (outlineApplied || outlineMaterial == null) return;
            if (originalMaterials == null || originalMaterials.Length != targetRenderers.Length) CacheOriginalMaterials();

            for (int i = 0; i < targetRenderers.Length; i++)
            {
                Renderer target = targetRenderers[i];
                if (target == null) continue;
                Material[] originals = originalMaterials[i];
                var outlined = new Material[originals.Length + 1];
                Array.Copy(originals, outlined, originals.Length);
                outlined[outlined.Length - 1] = outlineMaterial;
                target.sharedMaterials = outlined;
            }
            outlineApplied = true;
        }

        void CacheOriginalMaterials()
        {
            if (outlineApplied) RestoreOriginalMaterials();
            if (targetRenderers == null || targetRenderers.Length == 0)
            {
                var renderers = new List<Renderer>();
                foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true))
                {
                    if (renderer == null || renderer is ParticleSystemRenderer || renderer.transform.name == "Gauge") continue;
                    renderers.Add(renderer);
                }
                targetRenderers = renderers.ToArray();
            }

            originalMaterials = new Material[targetRenderers.Length][];
            for (int i = 0; i < targetRenderers.Length; i++)
                originalMaterials[i] = targetRenderers[i] != null ? targetRenderers[i].sharedMaterials : Array.Empty<Material>();
        }

        void RestoreOriginalMaterials()
        {
            if (!outlineApplied || originalMaterials == null) return;
            for (int i = 0; i < targetRenderers.Length; i++)
                if (targetRenderers[i] != null) targetRenderers[i].sharedMaterials = originalMaterials[i];
            outlineApplied = false;
        }
    }
}
