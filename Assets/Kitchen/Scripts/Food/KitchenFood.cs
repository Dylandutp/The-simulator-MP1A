using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace HouseSim.Kitchen
{
    [RequireComponent(typeof(XRGrabInteractable))]
    public class KitchenFood : MonoBehaviour
    {
        public KitchenFoodDefinition definition;
        public Renderer[] visualRenderers;
        [Min(0.05f)] public float mouthDistance = 0.3f;
        public float CookSeconds { get; private set; }
        public float WashProgress { get; private set; }
        public float VisibleWashProgress => displayedWash;
        public int CutStage { get; private set; }
        public bool IsChopped => CutStage > 0;
        public static event System.Action<KitchenFood> Released;
        public float ServingsRemaining { get; private set; } = 1f;
        public bool IsConsumed => ServingsRemaining <= 0f;
        public bool IsClaimed { get; private set; }
        public float UnclaimedPortions => IsClaimed ? 0f : ServingsRemaining;
        public bool IsProduce => definition != null && definition.IsProduce;
        public bool IsDirty => contaminatedFood || (IsProduce ? WashProgress < 1f : definition != null && definition.kind == FoodKind.Salad && contaminatedSalad);
        public bool IsHeld => desktopHeld || (grab != null && grab.isSelected);
        public FoodState State => GetState(CookSeconds);
        public ResourceData CurrentResource => GetResource(CookSeconds, IsChopped);

        private XRGrabInteractable grab;
        private MaterialPropertyBlock colors;
        private ResourceData claimedResource;
        private PlayerInventory claimedInventory;
        private bool desktopHeld;
        private bool contaminatedSalad;
        private bool contaminatedFood;
        private float displayedWash;
        private bool quitting;
        private KitchenFoodModelVisual modelVisual;

        private void Awake()
        {
            grab = GetComponent<XRGrabInteractable>();
            colors = new MaterialPropertyBlock();
            if (!GetComponent<KitchenContactStabilizer>()) gameObject.AddComponent<KitchenContactStabilizer>();
            if (visualRenderers == null || visualRenderers.Length == 0)
                visualRenderers = GetComponentsInChildren<Renderer>();
            modelVisual = GetComponent<KitchenFoodModelVisual>();
            if (modelVisual) modelVisual.Apply(this, contaminatedFood);
        }

        private void OnEnable()
        {
            if (grab == null) grab = GetComponent<XRGrabInteractable>();
            grab.selectEntered.AddListener(OnSelected);
            grab.selectExited.AddListener(OnReleased);
            grab.activated.AddListener(OnActivated);
        }

        private void Start() { UpdateAppearance(); }

        private void OnDisable()
        {
            if (grab == null) return;
            grab.selectEntered.RemoveListener(OnSelected);
            grab.selectExited.RemoveListener(OnReleased);
            grab.activated.RemoveListener(OnActivated);
        }

        private void OnApplicationQuit() { quitting = true; }

        private void OnDestroy()
        {
            if (!quitting && IsClaimed && claimedInventory != null)
                ExchangeClaimedResource(null);
            IsClaimed = false;
            claimedInventory = null;
        }

        private void OnSelected(SelectEnterEventArgs args) { Claim(); }
        private void OnReleased(SelectExitEventArgs args) { if (!IsHeld) Released?.Invoke(this); }

        private void OnActivated(ActivateEventArgs args)
        {
            var session = KitchenSession.Instance;
            if (!IsHeld || session == null || session.mouthAnchor == null) return;
            if (Vector3.Distance(transform.position, session.mouthAnchor.position) <= mouthDistance) TryConsume();
            else session.Notify("Bring the food to your mouth, then press trigger to eat.");
        }

        public void SetDesktopHeld(bool held)
        {
            bool wasHeld = desktopHeld;
            desktopHeld = held;
            if (held) Claim();
            else if (wasHeld && !IsHeld) Released?.Invoke(this);
        }

        // A short rinse completes the rule; the visible cleaning still fades smoothly.
        private void Update()
        {
            if (!IsProduce || Mathf.Approximately(displayedWash, WashProgress)) return;
            displayedWash = Mathf.MoveTowards(displayedWash, WashProgress, Time.deltaTime / 0.55f);
            UpdateAppearance();
        }

        public void MarkUnclean()
        {
            if (IsConsumed || contaminatedFood) return;
            contaminatedFood = true;
            UpdateAppearance();
            KitchenSession.Instance?.Notify(DisplayName + " is unclean. Eating it costs energy, even when cooked.");
        }

        public bool Claim()
        {
            if (IsConsumed) return false;
            if (IsClaimed) return true;
            var session = KitchenSession.Instance;
            var resource = CurrentResource;
            if (session == null || resource == null || !session.ExchangeOne(null, resource)) return false;
            IsClaimed = true;
            claimedResource = resource;
            claimedInventory = session.inventory;
            session.Notify(DisplayName + " collected: " + ServingsRemaining.ToString("0") + " portion in inventory.");
            session.Tutorial("take-food", "Take food from the fridge.\nHeld food enters your inventory.", transform.position);
            return true;
        }

        public void ApplyHeat(float seconds)
        {
            if (IsConsumed || definition == null || !IsFinitePositive(seconds)) return;
            if (definition.kind != FoodKind.Chicken && definition.kind != FoodKind.Steak) return;
            float next = Mathf.Min(CookSeconds + seconds, 86400f);
            var nextResource = GetResource(next, IsChopped);
            if (!MigrateClaimedResource(nextResource)) return;
            CookSeconds = next;
            UpdateAppearance();
        }

        public void Wash(float seconds)
        {
            if (IsConsumed || !IsProduce || !IsFinitePositive(seconds) || !IsDirty) return;
            WashProgress = KitchenFoodRules.AdvanceWash(WashProgress, seconds);
            UpdateAppearance();
            if (!IsDirty) KitchenSession.Instance?.Notify(DisplayName + " is clean.");
        }

        public bool Chop()
        {
            if (IsConsumed || !IsProduce || CutStage >= 2) return false;
            if (CutStage == 1)
            {
                CutStage = 2;
                GetComponent<KitchenCutVisual>()?.SetStage(CutStage);
                UpdateAppearance();
                KitchenSession.Instance?.Notify(DisplayName + " cut into smaller pieces. Pick up the whole portion together.");
                return true;
            }
            if (definition.choppedPrefab == null)
            {
                KitchenSession.Instance?.Notify("This food has no chopped prefab configured.");
                return false;
            }
            var replacementObject = Instantiate(definition.choppedPrefab, transform.position, transform.rotation);
            replacementObject.transform.localScale = transform.lossyScale;
            var oldRecovery = GetComponent<KitchenRespawnTool>();
            var newRecovery = replacementObject.GetComponent<KitchenRespawnTool>();
            if (oldRecovery && newRecovery)
            {
                newRecovery.enabled = oldRecovery.enabled;
                newRecovery.recoveryAnchor = oldRecovery.recoveryAnchor;
                newRecovery.minimumY = oldRecovery.minimumY;
                newRecovery.maximumDistance = oldRecovery.maximumDistance;
            }
            var replacement = replacementObject.GetComponent<KitchenFood>();
            if (replacement == null)
            {
                Destroy(replacementObject);
                return false;
            }
            replacement.definition = definition;
            replacement.CutStage = 1;
            replacement.ServingsRemaining = ServingsRemaining;
            replacement.WashProgress = WashProgress;
            replacement.CookSeconds = CookSeconds;
            replacement.displayedWash = displayedWash;
            replacement.contaminatedFood = contaminatedFood;
            var nextResource = replacement.CurrentResource;
            if (!MigrateClaimedResource(nextResource))
            {
                Destroy(replacementObject);
                return false;
            }
            replacement.IsClaimed = IsClaimed;
            replacement.claimedResource = claimedResource;
            replacement.claimedInventory = claimedInventory;
            IsClaimed = false;
            claimedResource = null;
            claimedInventory = null;
            var originalBody = GetComponent<Rigidbody>();
            var replacementBody = replacement.GetComponent<Rigidbody>();
            if (originalBody != null && replacementBody != null && !replacementBody.isKinematic)
            {
                replacementBody.linearVelocity = originalBody.linearVelocity;
                replacementBody.angularVelocity = originalBody.angularVelocity;
            }
            var cutVisual = replacement.GetComponent<KitchenCutVisual>();
            if (!cutVisual) cutVisual = replacement.gameObject.AddComponent<KitchenCutVisual>();
            cutVisual.Configure(this);
            cutVisual.SetStage(1);
            replacement.UpdateAppearance();
            // The replacement shares the old location for this frame, never its contacts.
            foreach (var collider in GetComponentsInChildren<Collider>()) collider.enabled = false;
            ServingsRemaining = 0f; // The existing portion moved into the chopped replacement.
            KitchenSession.Instance?.Notify(DisplayName + " chopped. Lift the knife and cut again for smaller pieces. Pick up the whole portion together.");
            Destroy(gameObject);
            return true;
        }

        public bool TryConsume()
        {
            if (IsConsumed || definition == null) return false;
            var session = KitchenSession.Instance;
            if (session == null) return false;
            if (IsProduce)
            {
                session.Notify("Wash and chop produce, then mix at least two different kinds into a salad.");
                return false;
            }
            if (!Claim() || !RemoveClaimedResource()) return false;
            KitchenFoodRules.TryGetEnergyChange(definition.kind, State, IsDirty, out int energy);
            ConsumeServing();
            session.ChangeEnergy(energy);
            session.Notify(DisplayName + ": " + (energy >= 0 ? "+" : "") + energy + " energy. Energy " + session.Energy + "/100.");
            // The consumed food is destroyed this frame. Keep each bite and its
            // following reaction on the player's mouth, using the food's benefit
            // before the shared energy meter clamps it at zero or full.
            KitchenAudioEmitter.Get(session.mouthAnchor ? session.mouthAnchor : session.transform)
                .PlayEatingSequence(energy);
            return true;
        }

        public void Discard()
        {
            if (IsConsumed || !RemoveClaimedResource()) return;
            ConsumeServing();
            KitchenSession.Instance?.Notify(DisplayName + " discarded.");
        }

        public bool TryTransferToBowl()
        {
            if (IsConsumed || !IsProduce || !IsChopped || !Claim() || !RemoveClaimedResource()) return false;
            ConsumeServing();
            return true;
        }

        private void ConsumeServing()
        {
            ServingsRemaining = Mathf.Max(0f, ServingsRemaining - 1f);
            if (ServingsRemaining <= 0f) Destroy(gameObject);
        }

        public void InitializeSalad(bool dirty)
        {
            contaminatedSalad = dirty;
            WashProgress = dirty ? 0f : 1f;
            UpdateAppearance();
        }

        private bool RemoveClaimedResource()
        {
            if (!IsClaimed) return true;
            if (!ExchangeClaimedResource(null)) return false;
            IsClaimed = false;
            claimedResource = null;
            claimedInventory = null;
            return true;
        }

        private bool MigrateClaimedResource(ResourceData nextResource)
        {
            if (!IsClaimed || nextResource == claimedResource) return true;
            if (nextResource == null || !ExchangeClaimedResource(nextResource)) return false;
            claimedResource = nextResource;
            return true;
        }

        private bool ExchangeClaimedResource(ResourceData to)
        {
            if (claimedInventory == null || claimedResource == null) return false;
            var costs = new System.Collections.Generic.Dictionary<ResourceData, int> { { claimedResource, 1 } };
            var rewards = new System.Collections.Generic.Dictionary<ResourceData, int>();
            if (to != null) rewards.Add(to, 1);
            bool success = claimedInventory.TryExchange(costs, rewards);
            if (success && KitchenSession.Instance != null && KitchenSession.Instance.inventory == claimedInventory)
                KitchenSession.Instance.RefreshDisplays();
            return success;
        }

        internal void RebindClaimInventory(PlayerInventory previous, PlayerInventory replacement)
        {
            if (IsClaimed && claimedInventory == previous) claimedInventory = replacement;
        }

        private FoodState GetState(float seconds)
        {
            if (definition == null) return FoodState.Raw;
            return KitchenFoodRules.GetState(definition.kind, seconds, definition.goodAtSeconds, definition.burntAtSeconds);
        }

        private ResourceData GetResource(float seconds, bool chopped)
        {
            if (definition == null) return null;
            if (GetState(seconds) != FoodState.Raw && definition.cookedResource != null) return definition.cookedResource;
            if (chopped && definition.preparedResource != null) return definition.preparedResource;
            return definition.rawResource;
        }

        private string DisplayName => definition == null ? name : string.IsNullOrEmpty(definition.displayName) ? definition.name : definition.displayName;
        private static bool IsFinitePositive(float value) => value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);

        private void UpdateAppearance()
        {
            if (definition == null) return;
            if (!modelVisual) modelVisual = GetComponent<KitchenFoodModelVisual>();
            if (modelVisual && modelVisual.Apply(this, contaminatedFood)) return;
            if (visualRenderers == null) return;
            if (colors == null) colors = new MaterialPropertyBlock();
            Color color = State == FoodState.Raw ? definition.rawColor : State == FoodState.Good ? definition.cookedColor : definition.burntColor;
            if (IsProduce) color = Color.Lerp(definition.rawColor * 0.45f, definition.rawColor, displayedWash);
            if (definition.kind == FoodKind.Salad && IsDirty) color = Color.Lerp(definition.cookedColor, new Color(0.3f, 0.19f, 0.06f), 0.5f);
            if (contaminatedFood) color = Color.Lerp(color, new Color(0.24f, 0.18f, 0.10f), 0.45f);
            color.a = 1f;
            foreach (var renderer in visualRenderers)
            {
                if (renderer == null) continue;
                renderer.GetPropertyBlock(colors);
                colors.SetColor("_BaseColor", color);
                colors.SetColor("_Color", color);
                renderer.SetPropertyBlock(colors);
            }
        }
    }
}
