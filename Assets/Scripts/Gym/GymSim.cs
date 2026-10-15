using System;
using System.Collections.Generic;
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
        readonly Dictionary<Vector2Int, GymSimThing> occupancy = new Dictionary<Vector2Int, GymSimThing>();
        readonly Dictionary<Vector2Int, GymSimThing> mats = new Dictionary<Vector2Int, GymSimThing>();
        readonly Dictionary<Vector2Int, GymMatSocket> matSockets = new Dictionary<Vector2Int, GymMatSocket>();
        readonly List<GymSimThing> things = new List<GymSimThing>();

        GymSimCatalog catalog;
        PlayerResources resources;
        QuestProgress questProgress;
        MachineFirstUsePopup machineFirstUsePopup;
        IntroPopup introPopup;

        InputAction use, cancel;
        Transform rightHand, headset;
        GymSimThing hover, activeMachine;
        bool wrenchEquipped;
        MachineEffects wrenchEffects;
        WrenchRepairContact wrenchContact;
        float hapticTimer, milestone;
        GymSimThing repairTarget, activeDispenser;
        float repairElapsed, repairStartValue, dispenserElapsed, dispensedWater;

        static readonly string[] InventorySlots =
        {
            "benchPress", "waterDispenser", "wrench", "rubberMat", "shaker",
            "armCurl", "tricepsCable", "rustyDumbbell", "chestFly",
            "latPulldown", "rowingMachine", "legPress", "treadmill"
        };
        static readonly Dictionary<string, string> MachineGain = new Dictionary<string, string> {
            { "armCurl", "armGain" }, { "tricepsCable", "armGain" }, { "rustyDumbbell", "armGain" },
            { "benchPress", "chestGain" }, { "chestFly", "chestGain" }, { "latPulldown", "backGain" },
            { "rowingMachine", "backGain" }, { "legPress", "legGain" }, { "treadmill", "legGain" }
        };

        public bool SupportsInventoryItem(string itemKind)
        {
            return Array.IndexOf(InventorySlots, itemKind) >= 0;
        }

        bool IsSocketEquipment(string itemKind)
        {
            return MachineGain.ContainsKey(itemKind);
        }

        bool IsGrabbableEquipment(string itemKind)
        {
            return IsSocketEquipment(itemKind) || itemKind == "shaker" || itemKind == "wrench" || itemKind == "rubberMat";
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void CreateRuntime()
        {
            bool gymSceneLoaded = false;
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
            {
                var scene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
                if (scene.isLoaded && scene.name == "GymRoomScene") { gymSceneLoaded = true; break; }
            }
            if (!gymSceneLoaded || FindAnyObjectByType<GymSim>() != null) return;
            new GameObject("Gym Sim").AddComponent<GymSim>();
        }

        void Awake()
        {
            catalog = Resources.Load<GymSimCatalog>("GymSimCatalog");
            resources = FindAnyObjectByType<PlayerResources>();
            if (resources == null) resources = gameObject.AddComponent<PlayerResources>();
            questProgress = FindAnyObjectByType<QuestProgress>();
            machineFirstUsePopup = FindAnyObjectByType<MachineFirstUsePopup>();
            introPopup = FindAnyObjectByType<IntroPopup>();
            FindRig(); BuildActions(); RegisterPlacedPrefabs();
        }

        void OnEnable() { use.Enable(); cancel.Enable(); }
        void OnDisable() { use.Disable(); cancel.Disable(); }
        void OnDestroy()
        {
            use.Dispose();
            cancel.Dispose();
        }

        void AddThingStats(GymSimThing t)
        {
            if (MachineGain.ContainsKey(t.kind))
            {
                float startingDurability = t.kind == "rustyDumbbell" ? 0f : 100f;
                t.AddStat(t.kind + "Load", 0, 0, 100);
                t.AddStat(t.kind + "Durability", startingDurability, 0, 100);
            }
            if (t.kind == "treadmill") t.AddStat("treadmillMilesRun", 0, 0, 9999);
            if (t.kind == "waterDispenser") t.AddStat("waterReservoir", 100, 0, 100);
            if (t.kind == "rubberMat") t.AddStat("matIntegrity", 100, 0, 100);
        }

        void FindRig()
        {
            Camera camera = Camera.main;
            headset = camera != null ? camera.transform : transform;

            Transform rightController = null;
            Transform rightRay = null;
            foreach (Transform candidate in FindObjectsByType<Transform>(FindObjectsInactive.Exclude))
            {
                if (string.Equals(candidate.name, "Right Controller", StringComparison.OrdinalIgnoreCase))
                    rightController = candidate;
                if (!string.Equals(candidate.name, "Ray Interactor", StringComparison.OrdinalIgnoreCase)) continue;
                for (Transform parent = candidate.parent; parent != null; parent = parent.parent)
                {
                    if (!string.Equals(parent.name, "Right Controller", StringComparison.OrdinalIgnoreCase)) continue;
                    rightRay = candidate;
                    break;
                }
            }
            rightHand = rightRay != null ? rightRay : rightController != null ? rightController : headset;
        }
        void BuildActions()
        {
            use = new InputAction("ContextActionB", InputActionType.Button);
            use.AddBinding("<XRController>{RightHand}/{secondaryButton}");
            use.AddBinding("<OculusTouchController>{RightHand}/secondaryButton");

            cancel = new InputAction("StopActionTrigger", InputActionType.Button);
            cancel.AddBinding("<XRController>{RightHand}/{triggerButton}");
            cancel.AddBinding("<XRController>{RightHand}/trigger").WithInteraction("press");
        }

        // Room authors place these marker-bearing prefabs in GymRoomScene. No starter
        // equipment, board, mat, or wild item is spawned by code.
        void RegisterPlacedPrefabs()
        {
            GymSimPrefab[] markers = FindObjectsByType<GymSimPrefab>(FindObjectsInactive.Include);

            // Register mats first so equipment in the authored room can claim its socket.
            foreach (GymSimPrefab marker in markers)
            {
                if (marker != null && marker.kind == "rubberMat") RegisterMarker(marker, true);
            }

            foreach (GymSimPrefab marker in markers)
            {
                if (marker == null || string.IsNullOrEmpty(marker.kind) || marker.kind == "rubberMat") continue;
                if (marker.kind == "wallBoard") { RegisterMarker(marker, false); continue; }

                Vector2Int cell = CellOf(marker.transform.position);
                bool placed = marker.kind != "wrench";
                if (IsSocketEquipment(marker.kind))
                    placed = mats.ContainsKey(cell) && !occupancy.ContainsKey(cell);
                RegisterMarker(marker, placed);
            }
        }

        GymSimThing RegisterMarker(GymSimPrefab marker, bool placed)
        {
            GymSimThing thing = marker.GetComponent<GymSimThing>();
            if (thing == null) thing = marker.gameObject.AddComponent<GymSimThing>();

            Vector2Int cell = CellOf(marker.transform.position);
            thing.Setup(this, marker.kind, placed, cell);
            if (marker.kind == "wrench") ConfigureWrench(thing);
            AddThingStats(thing);
            things.Add(thing);

            if (marker.kind == "rubberMat")
            {
                mats[cell] = thing;
                EnsureMatSocket(thing);
            }
            else if (placed && marker.kind != "wallBoard" && marker.kind != "shaker" && marker.kind != "wrench")
            {
                occupancy[cell] = thing;
            }

            if (IsGrabbableEquipment(marker.kind))
                ConfigurePlacement(thing, placed);

            if (IsSocketEquipment(marker.kind) || marker.kind == "waterDispenser" || marker.kind == "rubberMat") EnsureGauge(thing);
            return thing;
        }

        GymSimThing CreateThing(string kind, Vector3 p, bool placed)
        {
            Color c = kind == "rubberMat" ? new Color(.05f,.05f,.05f) : kind.StartsWith("wild") ? Color.cyan : new Color(.25f,.38f,.5f);
            GameObject template = catalog != null ? catalog.FindPrefab(kind) : null;
            var go = template != null
                ? Instantiate(template, p, Quaternion.identity)
                : Cube(kind, p, kind == "rubberMat" ? new Vector3(.96f,.08f,.96f) : new Vector3(.8f,.65f,.8f), c);
            MoveToGymRoom(go);
            go.name = kind;
            var marker = go.GetComponent<GymSimPrefab>();
            if (marker != null) marker.kind = kind;
            var t = go.GetComponent<GymSimThing>();
            if (t == null) t = go.AddComponent<GymSimThing>();
            var cell = CellOf(p);
            t.Setup(this, kind, placed, cell);
            if (kind == "wrench") ConfigureWrench(t);
            AddThingStats(t);
            things.Add(t);
            if (placed)
            {
                if (kind == "rubberMat") { mats[cell] = t; EnsureMatSocket(t); }
                else if (kind != "shaker" && kind != "wrench") occupancy[cell] = t;
            }
            if (IsGrabbableEquipment(kind)) ConfigurePlacement(t, placed);
            if (IsSocketEquipment(kind) || kind == "waterDispenser" || kind == "rubberMat") EnsureGauge(t);
            return t;
        }

        void MoveToGymRoom(GameObject target)
        {
            UnityEngine.SceneManagement.Scene gymScene = UnityEngine.SceneManagement.SceneManager.GetSceneByName("GymRoomScene");
            if (target != null && gymScene.IsValid() && gymScene.isLoaded)
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(target, gymScene);
        }

        void EnsureGauge(GymSimThing thing)
        {
            if (thing == null || (!IsSocketEquipment(thing.kind) && thing.kind != "waterDispenser" && thing.kind != "rubberMat")) return;
            GymSimGauge existingGauge = thing.GetComponentInChildren<GymSimGauge>(true);
            if (existingGauge != null)
            {
                existingGauge.thing = thing;
                return;
            }
            GameObject panel = new GameObject("Stat Pad");
            GymStatPadAnchor anchor = thing.GetComponentInChildren<GymStatPadAnchor>(true);
            panel.transform.SetParent(anchor != null ? anchor.transform : thing.transform, false);
            GymSimGauge gauge = panel.AddComponent<GymSimGauge>();
            gauge.thing = thing;
            gauge.BuildAndAttach();
        }

        void ConfigureWrench(GymSimThing thing)
        {
            wrenchEffects = thing.effects;
            wrenchContact = thing.GetComponent<WrenchRepairContact>();
            if (wrenchContact == null) wrenchContact = thing.gameObject.AddComponent<WrenchRepairContact>();
        }

        GymEquipmentPlacement ConfigurePlacement(GymSimThing thing, bool installed)
        {
            GymEquipmentPlacement placement = thing.GetComponent<GymEquipmentPlacement>();
            if (placement == null) placement = thing.gameObject.AddComponent<GymEquipmentPlacement>();
            placement.Configure(this, thing, installed);

            if (installed && IsSocketEquipment(thing.kind) && matSockets.TryGetValue(thing.cell, out GymMatSocket socket))
                socket.SetOccupant(placement);
            return placement;
        }

        GymMatSocket EnsureMatSocket(GymSimThing mat)
        {
            Transform socketTransform = mat.transform.Find("Equipment Socket");
            if (socketTransform == null)
            {
                GameObject socketObject = new GameObject("Equipment Socket");
                socketTransform = socketObject.transform;
                socketTransform.SetParent(mat.transform, false);

                Vector3 parentScale = mat.transform.lossyScale;
                socketTransform.localScale = new Vector3(
                    Mathf.Approximately(parentScale.x, 0f) ? 1f : 1f / Mathf.Abs(parentScale.x),
                    Mathf.Approximately(parentScale.y, 0f) ? 1f : 1f / Mathf.Abs(parentScale.y),
                    Mathf.Approximately(parentScale.z, 0f) ? 1f : 1f / Mathf.Abs(parentScale.z));
            }

            socketTransform.gameObject.SetActive(true);
            GymMatSocket socket = socketTransform.GetComponent<GymMatSocket>();
            if (socket == null) socket = socketTransform.gameObject.AddComponent<GymMatSocket>();
            socket.Configure(this, mat);

            BoxCollider trigger = socketTransform.GetComponent<BoxCollider>();
            if (trigger == null) trigger = socketTransform.gameObject.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.center = new Vector3(0f, .2f, 0f);
            trigger.size = new Vector3(.9f, .4f, .9f);

            matSockets[mat.cell] = socket;
            return socket;
        }

        internal void BeginMovingEquipment(GymEquipmentPlacement placement)
        {
            if (placement == null || placement.Thing == null) return;
            GymSimThing thing = placement.Thing;

            if (thing.kind == "rubberMat")
            {
                if (matSockets.TryGetValue(thing.cell, out GymMatSocket socket))
                {
                    GymEquipmentPlacement mountedEquipment = socket.Occupant;
                    if (mountedEquipment != null)
                    {
                        ReleaseFromSocket(mountedEquipment);
                        mountedEquipment.SetInstalled(false);
                    }
                    socket.gameObject.SetActive(false);
                }
                matSockets.Remove(thing.cell);
                mats.Remove(thing.cell);
            }
            else if (IsSocketEquipment(thing.kind))
            {
                ReleaseFromSocket(placement);
            }

            if (thing.kind == "wrench") wrenchEquipped = true;

            thing.placed = false;
            placement.SetInstalled(false);
        }

        internal void FinishMovingEquipment(GymEquipmentPlacement placement)
        {
            if (placement == null || placement.Thing == null) return;
            if (placement.IsRubberMat) TryInstallMat(placement);
            else if (IsSocketEquipment(placement.Thing.kind)) TryInstallOnMat(placement);
            else PlaceFreely(placement);
        }

        void TryInstallMat(GymEquipmentPlacement placement)
        {
            Vector2Int cell = CellOf(placement.transform.position);
            if (mats.ContainsKey(cell) || occupancy.ContainsKey(cell))
            {
                placement.SetInstalled(false);
                Toast("That grid space is occupied");
                return;
            }

            Vector3 gridCenter = new Vector3(cell.x + .5f, .004f, cell.y + .5f);
            placement.SnapTo(gridCenter, placement.transform.rotation);
            GymSimThing mat = placement.Thing;
            mat.cell = cell;
            mat.placed = true;
            mats[cell] = mat;
            EnsureMatSocket(mat);
            placement.SetInstalled(true);
            Toast("Rubber mat placed on grid");
        }

        void TryInstallOnMat(GymEquipmentPlacement placement)
        {
            GymMatSocket closest = null;
            float closestDistance = 1f;
            Vector3 position = placement.transform.position;
            foreach (GymMatSocket socket in matSockets.Values)
            {
                if (socket == null || !socket.isActiveAndEnabled || !socket.IsFree || occupancy.ContainsKey(socket.Cell)) continue;
                Vector3 delta = socket.SurfacePosition - position;
                delta.y = 0f;
                float distance = delta.magnitude;
                if (distance >= closestDistance) continue;
                closest = socket;
                closestDistance = distance;
            }

            if (closest == null)
            {
                placement.SetInstalled(false);
                Toast("Release equipment over a free rubber mat");
                return;
            }

            placement.SnapTo(closest.SurfacePosition, placement.transform.rotation);
            GymSimThing thing = placement.Thing;
            thing.cell = closest.Cell;
            thing.placed = true;
            occupancy[thing.cell] = thing;
            closest.SetOccupant(placement);
            placement.SetInstalled(true);
            Toast(DisplayName(thing.kind) + " installed");
        }

        void PlaceFreely(GymEquipmentPlacement placement)
        {
            if (placement.UsesFloorGravity)
            {
                placement.Thing.placed = true;
                placement.SetInstalled(true);
                placement.ReleaseWithGravity();
                Toast(DisplayName(placement.Thing.kind) + " placed");
                return;
            }
            float floorY = FindFloorBelow(placement);
            placement.SnapUpright(floorY);
            placement.Thing.placed = true;
            placement.SetInstalled(true);
            if (placement.Thing.kind == "wrench") wrenchEquipped = false;
            Toast(DisplayName(placement.Thing.kind) + " placed");
        }

        float FindFloorBelow(GymEquipmentPlacement placement)
        {
            RaycastHit[] hits = Physics.RaycastAll(placement.transform.position + Vector3.up * 1.5f, Vector3.down, 8f);
            float best = 0f;
            float bestDistance = float.MaxValue;
            foreach (RaycastHit hit in hits)
            {
                if (hit.collider == null || hit.collider.isTrigger || hit.collider.transform.IsChildOf(placement.transform)) continue;
                if (hit.distance >= bestDistance) continue;
                bestDistance = hit.distance;
                best = hit.point.y;
            }
            return best;
        }

        void ReleaseFromSocket(GymEquipmentPlacement placement)
        {
            if (placement == null || placement.Thing == null) return;
            GymSimThing thing = placement.Thing;
            if (occupancy.TryGetValue(thing.cell, out GymSimThing occupant) && occupant == thing)
                occupancy.Remove(thing.cell);
            if (matSockets.TryGetValue(thing.cell, out GymMatSocket socket))
                socket.ClearOccupant(placement);
            thing.placed = false;
        }
        GameObject Cube(string n, Vector3 p, Vector3 s, Color c) { var g=GameObject.CreatePrimitive(PrimitiveType.Cube); g.name=n; g.transform.position=p; g.transform.localScale=s; g.GetComponent<Renderer>().material.color=c; return g; }
        void Update()
        {
            PruneDestroyedThings();
            float dt = Time.deltaTime; UpdateRay(); CheckFirstMachineApproach(); HandleUse(); UpdateRepair(dt); UpdateWaterDispense(dt); Passive(dt); UpdateIdleMachines(dt); CheckMilestones();
        }

        void PruneDestroyedThings()
        {
            for (int i = things.Count - 1; i >= 0; i--)
                if (things[i] == null) things.RemoveAt(i);

            if (hover == null) hover = null;
            if (activeMachine == null) activeMachine = null;
            if (activeDispenser == null) activeDispenser = null;
            if (repairTarget == null)
            {
                repairTarget = null;
                wrenchEffects?.StopSession();
            }

            RemoveDestroyedEntries(occupancy);
            RemoveDestroyedEntries(mats);
            var deadSocketCells = new List<Vector2Int>();
            foreach (KeyValuePair<Vector2Int, GymMatSocket> entry in matSockets)
                if (entry.Value == null) deadSocketCells.Add(entry.Key);
            foreach (Vector2Int cell in deadSocketCells) matSockets.Remove(cell);
        }

        static void RemoveDestroyedEntries(Dictionary<Vector2Int, GymSimThing> entries)
        {
            var deadCells = new List<Vector2Int>();
            foreach (KeyValuePair<Vector2Int, GymSimThing> entry in entries)
                if (entry.Value == null) deadCells.Add(entry.Key);
            foreach (Vector2Int cell in deadCells) entries.Remove(cell);
        }

        internal void UnregisterThing(GymSimThing thing)
        {
            if (ReferenceEquals(thing, null)) return;

            if (ReferenceEquals(hover, thing)) hover = null;
            if (ReferenceEquals(activeMachine, thing)) activeMachine = null;
            if (ReferenceEquals(activeDispenser, thing)) activeDispenser = null;
            if (ReferenceEquals(repairTarget, thing))
            {
                repairTarget = null;
                repairElapsed = 0f;
                repairStartValue = 0f;
                wrenchEffects?.StopSession();
            }

            for (int i = things.Count - 1; i >= 0; i--)
                if (ReferenceEquals(things[i], thing)) things.RemoveAt(i);

            if (occupancy.TryGetValue(thing.cell, out GymSimThing occupied) && ReferenceEquals(occupied, thing))
                occupancy.Remove(thing.cell);
            if (mats.TryGetValue(thing.cell, out GymSimThing mat) && ReferenceEquals(mat, thing))
            {
                mats.Remove(thing.cell);
                matSockets.Remove(thing.cell);
            }

            GymEquipmentPlacement placement = thing.GetComponent<GymEquipmentPlacement>();
            if (placement != null)
            {
                foreach (GymMatSocket socket in matSockets.Values)
                    if (socket != null && ReferenceEquals(socket.Occupant, placement)) socket.ClearOccupant(placement);
            }

            if (thing.kind == "wrench")
            {
                wrenchEquipped = false;
                wrenchContact = null;
                wrenchEffects = null;
            }
        }

        Vector2Int CellOf(Vector3 p) => new Vector2Int(Mathf.FloorToInt(p.x),Mathf.FloorToInt(p.z));
        void Toast(string message) => Debug.Log("[Gym Sim] " + message);
        void Haptic(float duration,float amplitude){var d=InputDevices.GetDeviceAtXRNode(XRNode.RightHand); if(d.TryGetHapticCapabilities(out var c)&&c.supportsImpulse)d.SendHapticImpulse(0,amplitude,duration);}

        internal static string DisplayName(string kind)
        {
            switch (kind)
            {
                case "benchPress": return "Bench Press";
                case "waterDispenser": return "Water Dispenser";
                case "rubberMat": return "Rubber Mat";
                case "armCurl": return "Arm Curl";
                case "tricepsCable": return "Triceps Cable";
                case "rustyDumbbell": return "Rusty Dumbbell";
                case "chestFly": return "Chest Fly";
                case "latPulldown": return "Lat Pulldown";
                case "rowingMachine": return "Rowing Machine";
                case "legPress": return "Leg Press";
                case "armGain": return "Arms Progress";
                case "backGain": return "Back Progress";
                case "chestGain": return "Chest Progress";
                case "legGain": return "Legs Progress";
                case "cardioLevel": return "Cardio Level";
                default: return char.ToUpperInvariant(kind[0]) + kind.Substring(1);
            }
        }

        internal static string MuscleResourceFor(string kind)
        {
            return MachineGain.TryGetValue(kind, out string resourceName) ? resourceName : null;
        }

        internal float ResourceValue(string resourceName)
        {
            return resources != null ? resources.Get(resourceName) : 0f;
        }
    }

    public sealed class GymSimThing : MonoBehaviour
    {
        public string kind; public bool placed; public Vector2Int cell; public readonly Dictionary<string,Stat> stats=new Dictionary<string,Stat>(); public MachineEffects effects; GymSim game;
        public GymSim Owner => game;
        public void Setup(GymSim owner,string type,bool isPlaced,Vector2Int gridCell){game=owner;kind=type;placed=isPlaced;cell=gridCell;effects=GetComponent<MachineEffects>();}
        public void AddStat(string n,float v,float lo,float hi){var s=new Stat(n,v,lo,hi);stats[n]=s;}
        public Stat Get(string n)=>stats[n];
        void OnDestroy(){if(game!=null)game.UnregisterThing(this);}
    }
    public sealed class GymSimGauge : MonoBehaviour
    {
        public GymSimThing thing;
        readonly List<Material> createdMaterials = new List<Material>();
        readonly TextMesh[] valueTexts = new TextMesh[4];
        readonly Transform[] fillBars = new Transform[4];
        const float LabelLeft = -.4f;
        const float BarLeft = -.12f;
        const float BarWidth = .35f;
        const float ValueRight = .4f;

        public void BuildAndAttach()
        {
            if (thing == null) return;
            bool isMat = thing.kind == "rubberMat";
            GymStatPadAnchor authoredAnchor = thing.GetComponentInChildren<GymStatPadAnchor>(true);
            Transform anchor = authoredAnchor != null ? authoredAnchor.transform : thing.transform;
            if (transform.parent != anchor) transform.SetParent(anchor, false);

            Bounds bounds = default;
            bool found = false;
            foreach (Renderer renderer in thing.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null || renderer.transform.IsChildOf(transform)) continue;
                if (!found) { bounds = renderer.bounds; found = true; }
                else bounds.Encapsulate(renderer.bounds);
            }
            Vector3 worldPosition = found
                ? new Vector3(bounds.center.x, bounds.max.y + .22f, bounds.center.z)
                : thing.transform.position + Vector3.up;
            if (isMat && found) worldPosition = new Vector3(bounds.center.x, bounds.max.y + .012f, bounds.center.z);

            if (authoredAnchor != null)
            {
                transform.localPosition = Vector3.zero;
                transform.localRotation = Quaternion.identity;
            }
            else
            {
                transform.position = worldPosition;
                transform.rotation = isMat
                    ? Quaternion.Euler(90f, thing.transform.eulerAngles.y, 0f)
                    : Quaternion.Euler(0f, thing.transform.eulerAngles.y, 0f);
            }
            Vector3 scale = transform.lossyScale;
            transform.localScale = new Vector3(SafeInverse(scale.x), SafeInverse(scale.y), SafeInverse(scale.z)) * .68f;

            CreatePanelPart("Border", new Color(.36f, .015f, .075f, 1f), new Vector3(0f, 0f, .018f), new Vector3(.92f, .5f, 1f));
            CreatePanelPart("Background", new Color(.028f, .034f, .045f, .98f), new Vector3(0f, 0f, .012f), new Vector3(.9f, .48f, 1f));
            CreatePanelPart("Header", new Color(.055f, .064f, .08f, 1f), new Vector3(0f, .198f, .008f), new Vector3(.9f, .075f, 1f));
            CreatePanelPart("Accent", new Color(.72f, .015f, .14f, 1f), new Vector3(0f, .237f, .005f), new Vector3(.9f, .01f, 1f));

            CreateLabel("Pad Label", new Vector3(-.4f, .214f, 0f), .0055f,
                GymSim.DisplayName(thing.kind).ToUpperInvariant(), new Color(.7f, .72f, .77f), TextAnchor.UpperLeft);

            if (thing.kind == "waterDispenser")
            {
                BuildRow(0, "WATER", .035f);
            }
            else if (isMat)
            {
                BuildRow(0, "INTEGRITY", .035f);
            }
            else
            {
                BuildRow(0, "LOAD", .12f);
                BuildRow(1, "CONDITION", .03f);
                BuildRow(2, "TRAINING", -.06f);
                if (thing.kind == "treadmill") BuildRow(3, "MILES", -.15f);
            }
        }

        void LateUpdate()
        {
            if (thing == null || valueTexts[0] == null) return;

            if (thing.kind == "waterDispenser")
            {
                Stat reservoir = thing.Get("waterReservoir");
                UpdateRow(0, reservoir.value, reservoir.max);
                return;
            }
            if (thing.kind == "rubberMat")
            {
                Stat integrity = thing.Get("matIntegrity");
                UpdateRow(0, integrity.value, integrity.max);
                return;
            }

            string muscle = GymSim.MuscleResourceFor(thing.kind);
            Stat load = thing.Get(thing.kind + "Load");
            Stat condition = thing.Get(thing.kind + "Durability");
            float training = !string.IsNullOrEmpty(muscle) && thing.Owner != null ? thing.Owner.ResourceValue(muscle) : 0f;
            UpdateRow(0, load.value, load.max);
            UpdateRow(1, condition.value, condition.max);
            UpdateRow(2, training, 100f);
            if (thing.kind == "treadmill") UpdateMilesRow(3, thing.Get("treadmillMilesRun").value);
        }

        void BuildRow(int index, string label, float y)
        {
            CreateLabel(label + " Label", new Vector3(LabelLeft, y, 0f), .0055f,
                label, new Color(.69f, .72f, .77f), TextAnchor.UpperLeft);
            valueTexts[index] = CreateLabel(label + " Value", new Vector3(ValueRight, y, 0f), .0055f,
                "0%", new Color(.94f, .95f, .97f), TextAnchor.UpperRight);
            CreatePanelPart(label + " Track", new Color(.13f, .15f, .19f, 1f),
                new Vector3(BarLeft + BarWidth * .5f, y - .02f, .006f), new Vector3(BarWidth, .011f, 1f));
            fillBars[index] = CreatePanelPart(label + " Fill", new Color(.72f, .015f, .14f, 1f),
                new Vector3(BarLeft, y - .02f, .003f), new Vector3(.001f, .011f, 1f));
        }

        TextMesh CreateLabel(string name, Vector3 position, float size, string value, Color color, TextAnchor anchor)
        {
            GameObject label = new GameObject(name);
            label.transform.SetParent(transform, false);
            label.transform.localPosition = position;
            TextMesh text = label.AddComponent<TextMesh>();
            text.text = value;
            text.anchor = anchor;
            text.alignment = anchor == TextAnchor.UpperRight ? TextAlignment.Right : TextAlignment.Left;
            text.characterSize = size;
            text.fontSize = 72;
            text.color = color;
            return text;
        }

        Transform CreatePanelPart(string name, Color color, Vector3 position, Vector3 scale)
        {
            GameObject part = GameObject.CreatePrimitive(PrimitiveType.Quad);
            part.name = name;
            part.transform.SetParent(transform, false);
            part.transform.localPosition = position;
            part.transform.localRotation = Quaternion.identity;
            part.transform.localScale = scale;
            Collider collider = part.GetComponent<Collider>();
            if (collider != null) Destroy(collider);
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            Material material = new Material(shader);
            material.color = color;
            part.GetComponent<Renderer>().material = material;
            createdMaterials.Add(material);
            return part.transform;
        }

        static float SafeInverse(float value) => Mathf.Abs(value) < .0001f ? 1f : 1f / Mathf.Abs(value);
        void UpdateRow(int index, float value, float maximum)
        {
            float percent = maximum <= 0f ? 0f : Mathf.Clamp01(value / maximum);
            valueTexts[index].text = Mathf.RoundToInt(percent * 100f) + "%";
            Transform fill = fillBars[index];
            Vector3 scale = fill.localScale;
            scale.x = Mathf.Max(.001f, BarWidth * percent);
            fill.localScale = scale;
            Vector3 position = fill.localPosition;
            position.x = BarLeft + scale.x * .5f;
            fill.localPosition = position;
        }

        void UpdateMilesRow(int index, float miles)
        {
            valueTexts[index].text = miles.ToString("0.00") + " MI";
            float percent = Mathf.Repeat(miles, 10f) / 10f;
            Transform fill = fillBars[index];
            Vector3 scale = fill.localScale;
            scale.x = Mathf.Max(.001f, BarWidth * percent);
            fill.localScale = scale;
            Vector3 position = fill.localPosition;
            position.x = BarLeft + scale.x * .5f;
            fill.localPosition = position;
        }

        void OnDestroy()
        {
            foreach (Material material in createdMaterials)
                if (material != null) Destroy(material);
        }
    }
}
