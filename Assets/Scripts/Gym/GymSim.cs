using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;

namespace HouseSim.Gym
{
    /// <summary>
    /// Self-contained action driven gym loop. It deliberately has no
    /// keyboard/mouse fallback: all gameplay enters through XR InputActions and the right ray.
    /// </summary>
    public sealed partial class GymSim : MonoBehaviour
    {
        const float Cell = 1f;
        readonly Dictionary<string, Stat> resources = new Dictionary<string, Stat>();
        readonly Dictionary<Vector2Int, GymSimThing> occupancy = new Dictionary<Vector2Int, GymSimThing>();
        readonly Dictionary<Vector2Int, GymSimThing> mats = new Dictionary<Vector2Int, GymSimThing>();
        readonly List<GymSimThing> things = new List<GymSimThing>();
        readonly Dictionary<string, int> inventory = new Dictionary<string, int>();
        readonly Dictionary<string, int> questProgress = new Dictionary<string, int>();

        GymSimCatalog catalog;

        InputAction use, cycle, resourcesToggle, cancel, move, turn;
        Transform rightHand, leftHand, headset, rigRoot;
        GymSimThing hover, activeMachine, activeShaker;
        TextMesh wristText, fullPanel, tooltip;
        GameObject ghost, board, shakerMenu, heldPreview;
        int slot;
        bool showResources, placing, introShown, matTipShown, boardTipShown, wrenchEquipped;
        float hapticTimer, messageUntil, milestone, nextTurnTime;

        static readonly string[] Slots = { "benchPress", "waterDispenser", "wrench", "rubberMat", "shaker" };
        static readonly Dictionary<string, string> MachineGain = new Dictionary<string, string> {
            { "armCurl", "armGain" }, { "tricepsCable", "armGain" }, { "rustyDumbbell", "armGain" },
            { "benchPress", "chestGain" }, { "chestFly", "chestGain" }, { "latPulldown", "backGain" },
            { "rowingMachine", "backGain" }, { "legPress", "legGain" }, { "treadmill", "legGain" }
        };
        static readonly Dictionary<string, int> Prices = new Dictionary<string, int> {
            { "shaker", 15 }, { "rubberMat", 5 }, { "armCurl", 20 }, { "tricepsCable", 20 }, { "rustyDumbbell", 10 },
            { "chestFly", 25 }, { "latPulldown", 30 }, { "rowingMachine", 30 }, { "legPress", 40 }, { "treadmill", 50 }
        };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void CreateRuntime()
        {
            bool gymSceneLoaded = false;
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
            {
                var scene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
                if (scene.isLoaded && scene.name == "GymRoomScene") { gymSceneLoaded = true; break; }
            }
            if (!gymSceneLoaded || FindFirstObjectByType<GymSim>() != null) return;
            new GameObject("Gym Sim").AddComponent<GymSim>();
        }

        void Awake()
        {
            catalog = Resources.Load<GymSimCatalog>("GymSimCatalog");
            BuildStats();
            questProgress["sessions"] = questProgress["repair"] = questProgress["craft"] = questProgress["drink"] = questProgress["allGains"] = 0;
            inventory["benchPress"] = inventory["waterDispenser"] = inventory["wrench"] = inventory["rubberMat"] = 1;
            foreach (string item in Slots) if (!inventory.ContainsKey(item)) inventory[item] = 0;
            FindRig(); BuildActions(); RegisterPlacedPrefabs(); BuildWrist(); ShowIntro();
        }

        void OnEnable() { use.Enable(); cycle.Enable(); resourcesToggle.Enable(); cancel.Enable(); move.Enable(); turn.Enable(); }
        void OnDisable() { use.Disable(); cycle.Disable(); resourcesToggle.Disable(); cancel.Disable(); move.Disable(); turn.Disable(); }
        void OnDestroy() { use.Dispose(); cycle.Dispose(); resourcesToggle.Dispose(); cancel.Dispose(); move.Dispose(); turn.Dispose(); }

        void BuildStats()
        {
            if (catalog != null && catalog.playerResources != null && catalog.playerResources.Length == 15)
            {
                foreach (GymSimResourceDefinition definition in catalog.playerResources)
                    Add(definition.name, definition.start, definition.min, definition.max);
                return;
            }
            Add("gymCoins", 0, 0, 999); Add("energy", 100, 0, 100); Add("hydration", 80, 0, 100); Add("health", 100, 0, 100); Add("maxHealth", 100, 100, 1000); Add("stress", 50, 15, 100);
            Add("armGain", 0, 0, 100); Add("backGain", 0, 0, 100); Add("chestGain", 0, 0, 100); Add("legGain", 0, 0, 100);
            Add("water", 0, 0, 99); Add("powder", 0, 0, 99); Add("energyStick", 0, 0, 99); Add("milk", 0, 0, 99); Add("fruit", 0, 0, 99);
        }
        void Add(string n, float v, float lo, float hi) { var stat = new Stat(n, v, lo, hi); stat.Changed += _ => RefreshPanels(); resources[n] = stat; }
        void AddThingStats(GymSimThing t)
        {
            if (MachineGain.ContainsKey(t.kind)) { t.AddStat(t.kind + "Duration", 100, 0, 100); t.AddStat(t.kind + "Load", 0, 0, 100); t.AddStat(t.kind + "Durability", 100, 0, 100); t.sessionParticles = t.gameObject.AddComponent<ParticleSystem>(); t.sessionParticles.Stop(); t.sessionAudio = t.gameObject.AddComponent<AudioSource>(); t.sessionAudio.loop = true; }
            if (t.kind == "treadmill") t.AddStat("treadmillMilesRun", 0, 0, 9999);
            if (t.kind == "waterDispenser") { t.AddStat("waterReservoir", 100, 0, 100); t.sessionParticles = t.gameObject.AddComponent<ParticleSystem>(); t.sessionParticles.Stop(); t.sessionAudio = t.gameObject.AddComponent<AudioSource>(); }
            if (t.kind == "rubberMat") t.AddStat("matIntegrity", 100, 0, 100);
            if (t.kind == "shaker") { t.sessionParticles = t.gameObject.AddComponent<ParticleSystem>(); t.sessionParticles.Stop(); t.sessionAudio = t.gameObject.AddComponent<AudioSource>(); }
        }

        void FindRig()
        {
            Camera camera = Camera.main; headset = camera != null ? camera.transform : transform; rigRoot = headset.parent != null ? headset.parent : headset;
            foreach (Transform t in FindObjectsByType<Transform>(FindObjectsSortMode.None)) {
                string n = t.name.ToLowerInvariant();
                if (n.Contains("right") && n.Contains("controller")) rightHand = t;
                if (n.Contains("left") && n.Contains("controller")) leftHand = t;
            }
            if (rightHand == null) rightHand = headset;
            if (leftHand == null) leftHand = headset;
        }
        void BuildActions()
        {
            use = new InputAction("RightTrigger", InputActionType.Button, "<XRController>{RightHand}/trigger");
            cycle = new InputAction("CycleInventory", InputActionType.Button, "<XRController>{LeftHand}/primaryButton");
            resourcesToggle = new InputAction("ResourcePanel", InputActionType.Button, "<XRController>{LeftHand}/secondaryButton");
            cancel = new InputAction("CancelPlacement", InputActionType.Button, "<XRController>{RightHand}/secondaryButton");
            move = new InputAction("Move", InputActionType.Value, "<XRController>{LeftHand}/primary2DAxis");
            turn = new InputAction("SnapTurn", InputActionType.Value, "<XRController>{RightHand}/primary2DAxis");
            cycle.performed += _ => { slot = (slot + 1) % Slots.Length; wrenchEquipped = Slots[slot] == "wrench"; UpdateHeldPreview(); Toast("Selected " + Slots[slot]); };
            resourcesToggle.performed += _ => { showResources = !showResources; if (fullPanel != null) fullPanel.gameObject.SetActive(showResources); };
            cancel.performed += _ => CancelPlacement();
        }

        // Room authors place these marker-bearing prefabs in GymRoomScene. No starter
        // equipment, board, mat, or wild item is spawned by code.
        void RegisterPlacedPrefabs()
        {
            foreach (GymSimPrefab marker in FindObjectsByType<GymSimPrefab>(FindObjectsSortMode.None))
            {
                if (string.IsNullOrEmpty(marker.kind)) continue;
                if (marker.kind == "wallBoard")
                {
                    board = marker.gameObject;
                    GymSimThing boardThing = marker.GetComponent<GymSimThing>();
                    if (boardThing == null) boardThing = marker.gameObject.AddComponent<GymSimThing>();
                    boardThing.Setup(this, "wallBoard", false, Vector2Int.zero);
                    things.Add(boardThing);
                    continue;
                }
                GymSimThing thing = marker.GetComponent<GymSimThing>();
                if (thing == null) thing = marker.gameObject.AddComponent<GymSimThing>();
                Vector2Int cell = CellOf(marker.transform.position);
                bool placed = marker.kind != "wrench";
                thing.Setup(this, marker.kind, placed, cell);
                AddThingStats(thing);
                things.Add(thing);
                if (marker.kind == "rubberMat") mats[cell] = thing;
                else if (placed) occupancy[cell] = thing;
                CreateText("Gauge", marker.transform, new Vector3(-.38f, .43f, -.42f), .055f, marker.kind, Color.white).gameObject.AddComponent<GymSimGauge>().thing = thing;
            }
        }
        GymSimThing CreateThing(string kind, Vector3 p, bool placed)
        {
            Color c = kind == "rubberMat" ? new Color(.05f,.05f,.05f) : kind.StartsWith("wild") ? Color.cyan : new Color(.25f,.38f,.5f);
            GameObject template = catalog != null ? catalog.FindPrefab(kind) : null;
            var go = template != null
                ? Instantiate(template, p, Quaternion.identity)
                : Cube(kind, p, kind == "rubberMat" ? new Vector3(.96f,.08f,.96f) : new Vector3(.8f,.65f,.8f), c);
            go.name = kind;
            var marker = go.GetComponent<GymSimPrefab>();
            if (marker != null) marker.kind = kind;
            var t = go.GetComponent<GymSimThing>();
            if (t == null) t = go.AddComponent<GymSimThing>();
            var cell = CellOf(p); t.Setup(this, kind, placed, cell); AddThingStats(t); things.Add(t); if (placed) { if (kind == "rubberMat") mats[cell] = t; else occupancy[cell] = t; }
            CreateText("Gauge", go.transform, new Vector3(-.38f,.43f,-.42f), .055f, kind, Color.white).gameObject.AddComponent<GymSimGauge>().thing = t;
            return t;
        }
        GameObject Cube(string n, Vector3 p, Vector3 s, Color c) { var g=GameObject.CreatePrimitive(PrimitiveType.Cube); g.name=n; g.transform.position=p; g.transform.localScale=s; g.GetComponent<Renderer>().material.color=c; return g; }
        TextMesh CreateText(string n, Transform parent, Vector3 local, float size, string text, Color color) { var g=new GameObject(n); g.transform.SetParent(parent); g.transform.localPosition=local; g.transform.localRotation=Quaternion.identity; var tm=g.AddComponent<TextMesh>(); tm.text=text; tm.characterSize=size; tm.fontSize=64; tm.color=color; tm.anchor=TextAnchor.UpperLeft; return tm; }
        void BuildWrist() { wristText=CreateText("Wrist Panel", leftHand, new Vector3(.05f,.08f,.12f), .018f, "", Color.white); fullPanel=CreateText("Full Resources", leftHand, new Vector3(.05f,.22f,.12f), .012f, "", Color.white); fullPanel.gameObject.SetActive(false); tooltip=CreateText("Hover Tooltip", transform, Vector3.zero,.045f,"",Color.yellow); tooltip.gameObject.SetActive(false); heldPreview=Cube("Held Item",rightHand.position,Vector3.one*.12f,Color.white);heldPreview.transform.SetParent(rightHand);heldPreview.transform.localPosition=new Vector3(0f,-.05f,.12f);heldPreview.GetComponent<Collider>().enabled=false;UpdateHeldPreview();RefreshPanels(); }

        void Update()
        {
            float dt = Time.deltaTime; UpdateLocomotion(dt); UpdateRay(); HandleUse(); Passive(dt); UpdateIdleMachines(dt); UpdatePlacement(); CheckMilestones(); if (board != null && !boardTipShown && Vector3.Distance(headset.position, board.transform.position) <= 2.5f) { boardTipShown = true; ShowPanel("RESOURCE BOARD\n" + ResourceText() + "\nLeft Y opens your resources anytime.\n\nClose", board.transform.position + Vector3.left * 2f); } if (messageUntil > 0f && Time.time >= messageUntil) { messageUntil = 0f; RefreshPanels(); }
        }

        void BeginPlacement(string kind) { if(kind=="wrench"){wrenchEquipped=true;Toast("Wrench equipped");return;} if(inventory.ContainsKey(kind)&&inventory[kind]<=0){Toast("Not in inventory");return;} placing=true; if(ghost==null){ghost=Cube("Placement Ghost",Vector3.zero,Vector3.one,new Color(0,1,0,.4f)); var col=ghost.GetComponent<Collider>(); col.enabled=false;} ghost.SetActive(true); }
        void UpdatePlacement(){ if(!placing)return; RaycastHit hit; if(Physics.Raycast(new Ray(rightHand.position,rightHand.forward),out hit,15f)){var p=Snap(hit.point);ghost.transform.position=p; var cell=CellOf(p); bool valid=ValidPlacement(Slots[slot],cell); ghost.GetComponent<Renderer>().material.color=valid?new Color(0,1,0,.4f):new Color(1,0,0,.4f); } }
        bool ValidPlacement(string kind, Vector2Int c){ bool hasMat=mats.ContainsKey(c); return kind=="rubberMat" ? !hasMat&&!occupancy.ContainsKey(c) : hasMat&&!occupancy.ContainsKey(c); }
        void Place(){ Vector2Int c=CellOf(ghost.transform.position); string kind=Slots[slot]; if(!ValidPlacement(kind,c)){Toast("Invalid placement");return;} if(kind=="wrench"){wrenchEquipped=true;Toast("Wrench equipped");CancelPlacement();return;} CreateThing(kind,ghost.transform.position,true); inventory[kind]--; CancelPlacement(); }
        void CancelPlacement(){placing=false;if(ghost!=null)ghost.SetActive(false);}
        Vector3 Snap(Vector3 p) => new Vector3(Mathf.Floor(p.x)+.5f,.15f,Mathf.Floor(p.z)+.5f);
        Vector2Int CellOf(Vector3 p) => new Vector2Int(Mathf.FloorToInt(p.x),Mathf.FloorToInt(p.z));
        void OpenBoard()
        {
            Toast("Board: quests / buy items");
            if (!boardTipShown)
            {
                boardTipShown = true;
                ShowPanel("RESOURCE BOARD\n" + ResourceText() + "\nLeft Y opens resources anytime.\nClose with trigger", board.transform.position + Vector3.left * 2f);
            }

            foreach (var pair in Prices)
            {
                bool exists = false;
                foreach (var existingThing in things)
                    if (existingThing.kind == "buy" + pair.Key) exists = true;
                if (exists) continue;

                var button = Cube("Buy " + pair.Key,
                    board.transform.position + new Vector3(3f, .9f - (things.Count % 8) * .28f, -.2f),
                    new Vector3(1.3f, .2f, .08f), new Color(.15f, .35f, .2f));
                var buyThing = button.AddComponent<GymSimThing>();
                buyThing.Setup(this, "buy" + pair.Key, false, Vector2Int.zero);
                buyThing.label = "Buy " + pair.Key + " (" + pair.Value + ")";
                things.Add(buyThing);
                CreateText("Text", button.transform, new Vector3(-.55f, 0f, -.05f), .03f, buyThing.label, Color.white);
            }
        }
        void Buy(string item){int price;if(!Prices.TryGetValue(item,out price)||resources["gymCoins"].value<price){Toast("Not enough coins");return;}resources["gymCoins"].Change(-price);if(!inventory.ContainsKey(item))inventory[item]=0;inventory[item]++;Toast("Bought "+item);}
        void OpenShaker(GymSimThing s){ activeShaker=s; if(shakerMenu!=null)Destroy(shakerMenu); shakerMenu=new GameObject("Shaker Recipes"); shakerMenu.transform.position=s.transform.position+Vector3.right; string[] r={"Protein Shake: water + powder => energy +20","Smoothie: milk + fruit => energy +10, hydration +10","Energy Drink: water + energy stick => energy +30","Protein MilkShake: milk + powder => health +15","Fruit Water: water + fruit => hydration +20","Mega Shake: powder + milk + fruit => energy +25, health +10","Power Latte: milk + energy stick => energy +20","Recovery Shake: water + fruit + powder => health +20, hydration +10, stress -10"}; for(int i=0;i<r.Length;i++){bool available=CanCraft(i);var b=Cube("Recipe "+i,shakerMenu.transform.position+Vector3.up*(i*.3f),new Vector3(2,.22f,.08f),available?new Color(.16f,.42f,.24f):Color.gray);var t=b.AddComponent<GymSimThing>();t.Setup(this,"recipe"+i,false,Vector2Int.zero);t.label=r[i];things.Add(t);CreateText("Text",b.transform,new Vector3(-.95f,0,-.05f),.03f,r[i],Color.white);} }
        void ShowIntro(){introShown=true; ShowPanel("Left X cycles items. Right trigger uses and places. Hold trigger on a machine to exercise. Left Y opens resources. Quests and buying are on the wall board.\n\nGot it",headset.position+headset.forward*2); }
        void ShowPanel(string s,Vector3 p){var g=Cube("Popup",p,new Vector3(3,1.8f,.08f),new Color(.08f,.1f,.15f));var pop=g.AddComponent<GymSimThing>();pop.Setup(this,"popup",false,Vector2Int.zero);pop.label="Dismiss";things.Add(pop);CreateText("Popup text",g.transform,new Vector3(-1.35f,.7f,-.05f),.04f,s,Color.white);}
        string ResourceText(){var b=new StringBuilder();foreach(var r in resources)b.AppendLine(r.Key+": "+r.Value.value.ToString("0")+(r.Key=="health"?"/"+resources["maxHealth"].value.ToString("0"):""));return b.ToString();}
        void RefreshPanels(){if(wristText==null)return; wristText.text=$"ENERGY {resources["energy"].value:0}\nHYDRATION {resources["hydration"].value:0}\nHEALTH {resources["health"].value:0}/{resources["maxHealth"].value:0}\nSTRESS {resources["stress"].value:0}";fullPanel.text=ResourceText();}
        void Toast(string s){wristText.text+="\n"+s;messageUntil=Time.time+1.5f;}
        void Haptic(float duration,float amplitude){var d=InputDevices.GetDeviceAtXRNode(XRNode.RightHand); if(d.TryGetHapticCapabilities(out var c)&&c.supportsImpulse)d.SendHapticImpulse(0,amplitude,duration);}
        public void Craft(int n)
        {
            string[][] input = { new[]{"water","powder"},new[]{"milk","fruit"},new[]{"water","energyStick"},new[]{"milk","powder"},new[]{"water","fruit"},new[]{"powder","milk","fruit"},new[]{"milk","energyStick"},new[]{"water","fruit","powder"} };
            if (n < 0 || n >= input.Length) return;
            foreach (string item in input[n]) if (resources[item].value < 1) { Toast("Missing " + item); return; }
            foreach (string item in input[n]) resources[item].Change(-1);
            if(n==0) resources["energy"].Change(20); else if(n==1){resources["energy"].Change(10);resources["hydration"].Change(10);} else if(n==2)resources["energy"].Change(30); else if(n==3)resources["health"].Change(15); else if(n==4)resources["hydration"].Change(20); else if(n==5){resources["energy"].Change(25);resources["health"].Change(10);} else if(n==6)resources["energy"].Change(20); else {resources["health"].Change(20);resources["hydration"].Change(10);resources["stress"].Change(-10);}
            questProgress["craft"]++; if(activeShaker!=null){activeShaker.sessionParticles.Play();activeShaker.sessionAudio.Play();Invoke(nameof(StopShakerEffects),1f);} Haptic(.15f,.2f); Toast("Recipe mixed");
        }
        void StopShakerEffects(){if(activeShaker!=null){activeShaker.sessionParticles.Stop();activeShaker.sessionAudio.Stop();}}
        bool CanCraft(int n)
        {
            string[][] input = { new[]{"water","powder"},new[]{"milk","fruit"},new[]{"water","energyStick"},new[]{"milk","powder"},new[]{"water","fruit"},new[]{"powder","milk","fruit"},new[]{"milk","energyStick"},new[]{"water","fruit","powder"} };
            if (n < 0 || n >= input.Length) return false;
            foreach (string item in input[n]) if (resources[item].value < 1) return false;
            return true;
        }
    }

    public sealed class GymSimThing : MonoBehaviour
    {
        public string kind, label; public bool placed; public Vector2Int cell; public readonly Dictionary<string,Stat> stats=new Dictionary<string,Stat>(); public ParticleSystem sessionParticles; public AudioSource sessionAudio; GymSim game; Material material; Color baseColor;
        public void Setup(GymSim owner,string type,bool isPlaced,Vector2Int gridCell){game=owner;kind=type;placed=isPlaced;cell=gridCell;label=type;var renderer=GetComponentInChildren<Renderer>();if(renderer!=null){material=renderer.material;baseColor=material.color;}}
        public void AddStat(string n,float v,float lo,float hi){var s=new Stat(n,v,lo,hi);stats[n]=s;}
        public Stat Get(string n)=>stats[n]; public void Highlight(bool on){if(material!=null)material.EnableKeyword("_EMISSION");if(material!=null)material.SetColor("_EmissionColor",on?Color.yellow:Color.black);}
        public string Tooltip(){if(stats.Count==0)return "Trigger: "+label;var b=new StringBuilder("Hold Trigger: Use "+kind);foreach(var s in stats)b.Append("\n").Append(s.Key).Append(" ").Append(s.Value.value.ToString("0"));return b.ToString();}
    }
    public sealed class GymSimGauge : MonoBehaviour { public GymSimThing thing; TextMesh text; void Awake(){text=GetComponent<TextMesh>();} void Update(){if(thing==null||text==null)return;var b=new StringBuilder(thing.kind);foreach(var s in thing.stats)b.Append("\n").Append(s.Key.Replace(thing.kind,"" )).Append(" ").Append(s.Value.value.ToString("0"));text.text=b.ToString();} }
}
