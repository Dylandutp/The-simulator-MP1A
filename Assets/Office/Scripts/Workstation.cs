using UnityEngine;
using UnityEngine.UI;

namespace OfficeSim
{
    public abstract class Workstation : MonoBehaviour
    {
        public float automaticDuration = 5f;
        public MachineState state = new MachineState();
        public Vector2Int cell;
        public Transform output;
        public Transform workArea;
        public Renderer flowStrip;
        public OfficeTool toolPrefab;
        public OfficeTool currentTool;
        public Transform toolHome;
        public WorkstationVisuals visuals;
        public bool IsAutomatic => state.automatic;

        public Text readout;
        protected float toolCooldown;
        protected abstract MachineKind Kind { get; }
        public TutorialTrigger tutorial;
        private MaterialPropertyBlock lightProperties;

        protected void Awake()
        {
            state.kind = Kind;
            state.automatic = GetComponent<AutomaticWorkstation>() != null;
            lightProperties = new MaterialPropertyBlock();
        }

        protected void Start()
        {
            if (!IsAutomatic)
            {
                ConfigureTool(currentTool);
            }

            OfficeGame.Instance.stations.Add(this);
        }

        public void Move()
        {
            OfficeGame.Instance.placement.BeginMove(this);
        }

        protected void OnDestroy()
        {
            if (OfficeGame.Instance)
            {
                OfficeGame.Instance.stations.Remove(this);
            }

            if (currentTool)
            {
                Destroy(currentTool.gameObject);
            }
        }

        void ConfigureTool(OfficeTool tool)
        {
            tool.owner = this;
            tool.homeAnchor = toolHome;
            tool.transform.SetParent(null, true);
        }

        public void ResetTool()
        {
            currentTool.Retire();

            ResetContact();
            toolCooldown = 0;
            currentTool = Instantiate(toolPrefab, toolHome.position, toolHome.rotation);
            ConfigureTool(currentTool);
        }

        public bool Accept(OfficeItem item)
        {
            if (state.moving)
            {
                return false;
            }

            if (!state.CanAccept(item.resource) || !item.Consume())
            {
                return false;
            }

            state.Deposit(item.resource);
            visuals.Refresh();

            return true;
        }

        protected void LateUpdate()
        {
            if (!IsAutomatic)
            {
                return;
            }

            bool processing = state.active && !state.moving && state.outputCount < OfficeRules.Capacity;
            lightProperties.SetColor("_BaseColor", processing ? new Color(0.02f, 0.28f, 1f) : new Color(0.9f, 0.06f, .04f));
            lightProperties.SetTexture("_BaseMap", Texture2D.whiteTexture);
            flowStrip.SetPropertyBlock(lightProperties);
        }

        public void TickAutomatic(float deltaTime)
        {
            if (state.Advance(deltaTime / automaticDuration))
            {
                Produce();
            }
        }

        public void ShowHelp()
        {
            tutorial.ShowHelp();
        }

        public void ManualContact(OfficeTool tool)
        {
            if (IsAutomatic || !tool.Held || tool.owner != this || Time.time < toolCooldown)
            {
                return;
            }

            UseTool(tool);
        }

        protected abstract void UseTool(OfficeTool tool);

        protected virtual void ResetContact()
        {
        }

        public virtual void ExitContact(OfficeTool tool)
        {
        }

        protected void Produce()
        {
            var item = OfficeGame.Instance.SpawnItem(
                OfficeRules.Output(state.kind),
                output.position + Vector3.up * (0.04f * state.outputCount),
                true);
            item.outputOwner = this;
            OfficeGame.Instance.Ping();
            visuals.Refresh();
        }
    }
}
