using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

namespace HouseSim.Kitchen.Editor
{
    /// <summary>Authors actual editable prefabs, materials, data and additive scenes. Runtime needs no editor code.</summary>
    public static class KitchenSceneBuilder
    {
        const string Root = "Assets/Kitchen";
        static readonly Dictionary<string, Material> mats = new Dictionary<string, Material>();
        static readonly Dictionary<FoodKind, KitchenFoodDefinition> foods = new Dictionary<FoodKind, KitchenFoodDefinition>();
        static readonly List<ResourceData> resources = new List<ResourceData>();
        static Font font;
        static Material cream, green, dark, wood, brass, white, metal, glass, red, blue;

        [MenuItem("Kitchen/Build or Rebuild Kitchen Scene")]
        public static void Build()
        {
            foreach (string path in new[] { "Scenes", "Prefabs/Food", "Prefabs/Stations", "Data/Resources", "Data/Food", "Materials", "Meshes" })
                Directory.CreateDirectory(Root + "/" + path);
            AssetDatabase.Refresh();
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            mats.Clear(); resources.Clear(); foods.Clear();
            cream = Mat("Warm plaster", new Color(.91f,.86f,.73f));
            green = Mat("Sage cabinetry", new Color(.23f,.39f,.33f));
            dark = Mat("Graphite", new Color(.06f,.095f,.09f));
            wood = Mat("Honey oak", new Color(.61f,.37f,.19f));
            brass = Mat("Brass", new Color(.79f,.58f,.22f), .7f);
            white = Mat("Porcelain", new Color(.96f,.95f,.87f));
            metal = Mat("Brushed steel", new Color(.48f,.55f,.56f), .8f);
            glass = Mat("Oven window", new Color(.095f,.13f,.14f), .3f);
            red = Mat("Tomato red", new Color(.8f,.13f,.065f));
            blue = Mat("Water blue", new Color(.25f,.67f,.84f));
            CreateFoodAssets();
            var fridgePrefab = SavePrefab(CreateFridge(), "Stations/Fridge");
            var ovenPrefab = SavePrefab(CreateOven(), "Stations/Oven");
            var hobPrefab = SavePrefab(CreateHob(), "Stations/InductionHob");
            var sinkPrefab = SavePrefab(CreateSink(), "Stations/WashStation");
            var prepPrefab = SavePrefab(CreatePrep(), "Stations/PrepStation");
            var binPrefab = SavePrefab(CreateBin(), "Stations/CompostBin");
            var foundation = new GameObject("CounterFoundation");
            Box("Oak plinth",foundation.transform,new Vector3(0,.04f,0),new Vector3(1.1f,.08f,1.05f),wood);
            Text("Foundation label","FOUNDATION",foundation.transform,new Vector3(0,.085f,-.3f),.015f).transform.localRotation=Quaternion.Euler(90,0,0);
            var foundationPrefab=SavePrefab(foundation,"Stations/CounterFoundation");

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var room = new GameObject("Kitchen Room");
            CreateRoom(room.transform);
            Instance(fridgePrefab,room.transform,new Vector3(-3.7f,0,3.8f));
            Instance(sinkPrefab,room.transform,new Vector3(-1.8f,0,3.8f));
            Instance(hobPrefab,room.transform,new Vector3(.25f,0,3.8f));
            Instance(ovenPrefab,room.transform,new Vector3(2.25f,0,3.8f));
            Instance(prepPrefab,room.transform,new Vector3(.1f,0,.3f));
            Instance(binPrefab,room.transform,new Vector3(2.4f,0,.6f));
            var sessionObject = new GameObject("Kitchen Shared Services");
            sessionObject.transform.SetParent(room.transform);
            var session=sessionObject.AddComponent<KitchenSession>();
            session.energyResource=Resource("Energy","Energy");
            session.visibleResources=resources.FindAll(resource => resource != session.energyResource).ToArray();
            session.initialEnergy=70;session.recoveryAnchor=Empty("Lost food recovery",room.transform,new Vector3(-.55f,1.4f,.3f));
            CreateBoards(room.transform,session);
            var runtime=room.AddComponent<KitchenRuntimeSetup>();
            runtime.session=session;
            runtime.spawn=Empty("Player arrival",room.transform,new Vector3(.25f,0,-2.4f));
            CreateGrid(room.transform,foundationPrefab,fridgePrefab,hobPrefab,ovenPrefab);
            RenderSettings.ambientMode=AmbientMode.Flat;
            RenderSettings.ambientLight=new Color(.68f,.72f,.75f);
            RenderSettings.fog=false;
            var sun=new GameObject("Kitchen soft daylight").AddComponent<Light>();
            sun.transform.SetParent(room.transform); sun.type=LightType.Directional; sun.intensity=1.5f;
            sun.transform.rotation=Quaternion.Euler(45,-35,0); sun.shadows=LightShadows.Soft;
            // Retain editable room geometry; the only XR rig comes from the shared MasterScene.
            EditorSceneManager.SaveScene(scene,Root+"/Scenes/Kitchen.unity");
            Scene entry=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            new GameObject("Load Shared Master + Kitchen").AddComponent<KitchenBootstrap>();
            EditorSceneManager.SaveScene(entry,Root+"/Scenes/KitchenEntry.unity");
            EditorBuildSettings.scenes=new[] {
                new EditorBuildSettingsScene(Root+"/Scenes/KitchenEntry.unity",true),
                new EditorBuildSettingsScene("Assets/Scenes/MasterScene.unity",true),
                new EditorBuildSettingsScene(Root+"/Scenes/Kitchen.unity",true)
            };
            PlayerSettings.companyName="CS417"; PlayerSettings.productName="Kitchen - House Sim";
            PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android,"edu.illinois.cs417.kitchen");
            PlayerSettings.Android.minSdkVersion=AndroidSdkVersions.AndroidApiLevel29;
            PlayerSettings.Android.targetArchitectures=AndroidArchitecture.ARM64;
            PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Android,ScriptingImplementation.IL2CPP);
            PlayerSettings.runInBackground=true;
            PlayerSettings.colorSpace=ColorSpace.Linear;
            PlayerSettings.defaultInterfaceOrientation=UIOrientation.LandscapeLeft;
            AssetDatabase.SaveAssets();
            Debug.Log("KITCHEN_SCENE_BUILD_PASS: KitchenEntry + additive Kitchen; six stations, food prefabs and shared resources authored.");
        }

        static void CreateFoodAssets()
        {
            foreach(FoodKind kind in Enum.GetValues(typeof(FoodKind)))
            {
                string path=Root+"/Data/Food/"+kind+".asset";
                var def=AssetDatabase.LoadAssetAtPath<KitchenFoodDefinition>(path);
                if(!def) {def=ScriptableObject.CreateInstance<KitchenFoodDefinition>(); AssetDatabase.CreateAsset(def,path);}
                def.kind=kind; def.displayName=kind==FoodKind.Chicken?"Roast chicken":kind==FoodKind.Steak?"Pan-seared steak":kind.ToString();
                def.rawResource=Resource(kind.ToString(),kind.ToString());
                def.preparedResource=def.IsProduce?Resource("Chopped"+kind,"Chopped "+kind.ToString().ToLowerInvariant()):def.rawResource;
                def.cookedResource=kind==FoodKind.Chicken||kind==FoodKind.Steak?Resource("Cooked"+kind,"Cooked "+kind.ToString().ToLowerInvariant()):def.rawResource;
                def.goodAtSeconds=kind==FoodKind.Steak?8:20; def.burntAtSeconds=kind==FoodKind.Steak?18:40;
                def.rawColor=kind==FoodKind.Chicken?new Color(.94f,.77f,.63f):kind==FoodKind.Steak?new Color(.62f,.12f,.11f):kind==FoodKind.Lettuce?new Color(.29f,.64f,.16f):kind==FoodKind.Salad?new Color(.35f,.7f,.18f):new Color(.83f,.17f,.075f);
                def.cookedColor=kind==FoodKind.Steak?new Color(.39f,.19f,.075f):new Color(.72f,.35f,.09f);
                def.burntColor=new Color(.055f,.04f,.03f); if(def.kind==FoodKind.Salad)def.cookedColor=def.rawColor;
                foods.Add(kind,def);
                def.prefab=SavePrefab(FoodModel(def,false),"Food/"+kind);
                if(def.IsProduce) def.choppedPrefab=SavePrefab(FoodModel(def,true),"Food/Chopped"+kind);
                EditorUtility.SetDirty(def);
            }
        }
        static GameObject FoodModel(KitchenFoodDefinition def,bool chopped)
        {
            var root=new GameObject((chopped?"Chopped ":"")+def.kind);
            var surface=Mat(def.kind+" food",def.rawColor);
            var renderers=new List<Renderer>();
            if(chopped)
            {
                for(int i=0;i<3;i++)
                {
                    var slice=Primitive("Slice "+(i+1),PrimitiveType.Cylinder,root.transform,new Vector3((i-1)*.065f,.035f,0),new Vector3(.11f,.025f,.11f),surface,false);
                    renderers.Add(slice.GetComponent<Renderer>());
                }
            }
            else if(def.kind==FoodKind.Chicken)
            {
                renderers.Add(Sphere("Chicken breast",root.transform,new Vector3(0,.09f,0),new Vector3(.32f,.21f,.43f),surface).GetComponent<Renderer>());
                for(int s=-1;s<=1;s+=2)
                {
                    var leg=Sphere("Drumstick",root.transform,new Vector3(s*.17f,.07f,-.12f),new Vector3(.105f,.12f,.22f),surface);
                    leg.transform.localRotation=Quaternion.Euler(0,s*-20,0);renderers.Add(leg.GetComponent<Renderer>());
                    renderers.Add(Sphere("Wing",root.transform,new Vector3(s*.155f,.09f,.08f),new Vector3(.09f,.09f,.18f),surface).GetComponent<Renderer>());
                }
            }
            else if(def.kind==FoodKind.Steak)
            {
                Sphere("Fat edge",root.transform,new Vector3(0,.031f,0),new Vector3(.285f,.065f,.21f),Mat("Steak fat",new Color(.88f,.73f,.54f)));
                renderers.Add(Sphere("Steak",root.transform,new Vector3(0,.043f,0),new Vector3(.263f,.063f,.186f),surface).GetComponent<Renderer>());
                for(int i=0;i<4;i++) { var score=Box("Sear line",root.transform,new Vector3((i-1.5f)*.046f,.074f,0),new Vector3(.012f,.002f,.105f),surface,false);renderers.Add(score.GetComponent<Renderer>());}
            }
            else if(def.kind==FoodKind.Salad)
            {
                Primitive("Salad plate",PrimitiveType.Cylinder,root.transform,Vector3.zero,new Vector3(.34f,.016f,.34f),white,false);
                for(int i=0;i<9;i++)
                {
                    float a=i*2.39996f;
                    Sphere("Salad leaf",root.transform,new Vector3(Mathf.Cos(a)*.085f,.045f,Mathf.Sin(a)*.085f),new Vector3(.11f,.028f,.065f),surface);
                }
                for(int i=0;i<4;i++) Sphere("Tomato piece",root.transform,new Vector3((i%2-.5f)*.12f,.063f,(i/2-.5f)*.12f),Vector3.one*.055f,red);
                // Only leaves change when the salad is contaminated.
                foreach(var renderer in root.GetComponentsInChildren<Renderer>()) if(renderer.name=="Salad leaf")renderers.Add(renderer);
            }
            else
            {
                if(def.kind==FoodKind.Lettuce)
                {
                    for(int i=0;i<5;i++) {var leaf=Sphere("Leaf",root.transform,new Vector3(Mathf.Sin(i*1.256f)*.04f,.065f,Mathf.Cos(i*1.256f)*.04f),new Vector3(.115f,.13f,.11f),surface);renderers.Add(leaf.GetComponent<Renderer>());}
                }
                else
                {
                    renderers.Add(Sphere("Fruit",root.transform,new Vector3(0,.075f,0),new Vector3(.16f,.145f,.16f),surface).GetComponent<Renderer>());
                    Box("Stem",root.transform,new Vector3(0,.159f,0),new Vector3(.012f,.035f,.012f),wood,false);
                    Sphere("Leaf",root.transform,new Vector3(.028f,.154f,0),new Vector3(.065f,.012f,.035f),green);
                }
            }
            var collider=root.AddComponent<BoxCollider>();
            collider.center=def.kind==FoodKind.Chicken?new Vector3(0,.1f,0):new Vector3(0,.065f,0);
            collider.size=def.kind==FoodKind.Chicken?new Vector3(.41f,.21f,.42f):def.kind==FoodKind.Steak?new Vector3(.28f,.085f,.21f):def.kind==FoodKind.Salad?new Vector3(.34f,.12f,.34f):chopped?new Vector3(.23f,.065f,.12f):new Vector3(.17f,.17f,.17f);
            var bounds=new Bounds(root.transform.position,Vector3.zero); bool hasBounds=false; foreach(var visual in root.GetComponentsInChildren<Renderer>()){if(!hasBounds){bounds=visual.bounds;hasBounds=true;}else bounds.Encapsulate(visual.bounds);} collider.center=bounds.center-root.transform.position; collider.size=bounds.size; Grab(root,.25f);
            var food=root.AddComponent<KitchenFood>(); food.definition=def; food.visualRenderers=renderers.ToArray();
            var recovery=root.AddComponent<KitchenRespawnTool>();
            return root;
        }

        static GameObject CreateFridge()
        {
            var root=new GameObject("Fridge");
            Box("Back",root.transform,new Vector3(0,1.05f,.43f),new Vector3(1.1f,2.1f,.1f),cream);
            Box("Left",root.transform,new Vector3(-.525f,1.05f,0),new Vector3(.07f,2.1f,.88f),cream);
            Box("Right",root.transform,new Vector3(.525f,1.05f,0),new Vector3(.07f,2.1f,.88f),cream);
            Box("Top",root.transform,new Vector3(0,2.075f,0),new Vector3(1.1f,.05f,.9f),cream);
            for(int i=0;i<4;i++)Box("Shelf "+i,root.transform,new Vector3(0,.12f+i*.52f,0),new Vector3(1.02f,.045f,.86f),white);
            var pivot=Empty("Fridge hinge",root.transform,new Vector3(-.535f,0,-.47f));
            Box("Fridge door",pivot,new Vector3(.54f,1.06f,0),new Vector3(1.09f,2.07f,.085f),green);
            var handle=Box("Door brass handle",pivot,new Vector3(.94f,1.13f,-.09f),new Vector3(.055f,.43f,.085f),brass);
            var door=pivot.gameObject.AddComponent<KitchenDoor>();door.pivot=pivot;door.openAngle=105;
            door.GetComponent<XRSimpleInteractable>().colliders.Clear();
            door.GetComponent<XRSimpleInteractable>().colliders.Add(handle.GetComponent<Collider>());
            Text("Fridge name","F R E S H",pivot,new Vector3(.52f,1.76f,-.05f),.034f);
            var fridge=root.AddComponent<KitchenFridge>();fridge.door=door;
            var slots=new List<Transform>();var prefabs=new List<GameObject>();
            FoodKind[] order={FoodKind.Chicken,FoodKind.Steak,FoodKind.Tomato,FoodKind.Lettuce,FoodKind.Apple,FoodKind.Tomato};
            for(int i=0;i<6;i++) {slots.Add(Empty("Stock slot "+i,root.transform,new Vector3(i%2==0?-.22f:.22f,.69f+i/2*.52f,-.1f)));prefabs.Add(foods[order[i]].prefab);}
            fridge.slots=slots.ToArray();fridge.foodPrefabs=prefabs.ToArray();
            fridge.label=Screen("Stock display",root.transform,new Vector3(0,2.3f,-.35f),1.1f,.30f);
            Hint(root.transform,"fridge","Pull the handle to open. Take an ingredient.\nOpening or closing restocks empty spaces.",new Vector3(0,1.65f,-.64f));
            return root;
        }
        static GameObject CreateOven()
        {
            var root=new GameObject("Oven");
            Box("Oven rear",root.transform,new Vector3(0,.78f,.44f),new Vector3(1.22f,1.55f,.1f),green);
            Box("Oven left",root.transform,new Vector3(-.57f,.78f,0),new Vector3(.08f,1.55f,.92f),green);
            Box("Oven right",root.transform,new Vector3(.57f,.78f,0),new Vector3(.08f,1.55f,.92f),green);
            Box("Oven top",root.transform,new Vector3(0,1.51f,0),new Vector3(1.22f,.09f,.93f),wood);
            Box("Oven bottom",root.transform,new Vector3(0,.28f,0),new Vector3(1.14f,.52f,.9f),green);
            Box("Rack",root.transform,new Vector3(0,.65f,0),new Vector3(1.05f,.035f,.78f),metal);
            for(int i=0;i<10;i++)Box("Rack bar",root.transform,new Vector3((i-4.5f)*.10f,.678f,0),new Vector3(.014f,.01f,.73f),brass,false);
            Box("Control fascia",root.transform,new Vector3(0,1.35f,-.44f),new Vector3(1.13f,.29f,.085f),dark);
            var pivot=Empty("Oven hinge",root.transform,new Vector3(0,.55f,-.5f));
            Box("Door frame",pivot,new Vector3(0,.32f,0),new Vector3(1.11f,.65f,.065f),metal);
            Box("Door glass",pivot,new Vector3(0,.30f,-.04f),new Vector3(.88f,.46f,.02f),glass);
            var handle=Box("Oven handle",pivot,new Vector3(0,.56f,-.105f),new Vector3(.76f,.055f,.075f),brass);
            var door=pivot.gameObject.AddComponent<KitchenDoor>();door.pivot=pivot;door.localAxis=Vector3.right;door.openAngle=-90;
            door.GetComponent<XRSimpleInteractable>().colliders.Clear();
            door.GetComponent<XRSimpleInteractable>().colliders.Add(handle.GetComponent<Collider>());
            var knob=Knob(root.transform,new Vector3(.32f,1.35f,-.53f),"Oven",60,0,"s");
            var oven=root.AddComponent<KitchenOven>();oven.door=door;oven.timerKnob=knob;
            oven.chamber=Empty("Cooking volume",root.transform,new Vector3(0,.88f,0));oven.chamberHalfExtents=new Vector3(.48f,.21f,.34f);
            var lamp=new GameObject("Oven lamp").AddComponent<Light>();lamp.transform.SetParent(root.transform,false);lamp.transform.localPosition=new Vector3(0,1.06f,.15f);lamp.type=LightType.Point;lamp.range=1.3f;lamp.intensity=2;lamp.color=new Color(1,.47f,.12f);oven.heatLight=lamp;
            Text("Chicken recipe","ROAST\nCHICKEN",root.transform,new Vector3(-.24f,1.42f,-.495f),.016f);
            Hint(root.transform,"oven","Place chicken on the rack. Close the door.\nTurn the timer. Opening pauses cooking.",new Vector3(0,1.5f,-.65f));
            Hint(root.transform,"chicken","Pale chicken: -15 energy. Golden: +30.\nBlackened: -20. Check the colour before eating.",new Vector3(.4f,1.7f,-.5f));
            return root;
        }
        static GameObject CreateHob()
        {
            var root=new GameObject("InductionHob");Counter(root.transform,1.3f);
            Box("Black glass hob",root.transform,new Vector3(0,.98f,0),new Vector3(1.12f,.045f,.78f),dark);
            var rings=new List<Renderer>();
            for(int i=0;i<3;i++)rings.Add(Torus("Induction coil "+i,root.transform,new Vector3(-.13f,1.006f,.04f),.18f+i*.038f,.008f,Mat("Heat ring",new Color(.14f,.035f,.02f),.3f,true)).GetComponent<Renderer>());
            var knob=Knob(root.transform,new Vector3(.36f,.85f,-.51f),"Heat",100,0,"%");
            var hob=root.AddComponent<KitchenHob>();hob.powerKnob=knob;hob.ringRenderers=rings.ToArray();hob.burner=Empty("Burner",root.transform,new Vector3(-.13f,1.025f,.04f));
            var pan=CreatePan();pan.transform.SetParent(root.transform,false);pan.transform.localPosition=new Vector3(-.13f,1.027f,.04f);hob.pan=pan.GetComponent<KitchenPan>();
            hob.label=Screen("Hob status",root.transform,new Vector3(0,1.53f,.35f),1.22f,.34f);

            Hint(root.transform,"hob","Hold and turn: 0% is off, 100% is maximum.\nA brighter red ring cooks steak faster.",new Vector3(.28f,1.2f,-.6f));
            Hint(root.transform,"pan","Rest the pan on the ring, then drop in steak.\nYou can cook with one hand; no need to hold the pan.",new Vector3(-.2f,1.36f,-.3f));
            return root;
        }
        static GameObject CreatePan()
        {
            var root=new GameObject("FryingPan");
            Primitive("Pan base",PrimitiveType.Cylinder,root.transform,new Vector3(0,.012f,0),new Vector3(.5f,.018f,.5f),dark,false);
            var baseCollider=root.AddComponent<BoxCollider>();baseCollider.center=new Vector3(0,.015f,0);baseCollider.size=new Vector3(.44f,.035f,.44f);
            Torus("Pan rim",root.transform,new Vector3(0,.057f,0),.237f,.018f,metal);
            for(int i=0;i<12;i++)
            {
                float a=i*30f*Mathf.Deg2Rad;
                var side=new GameObject("Pan wall collider");side.transform.SetParent(root.transform,false);side.transform.localPosition=new Vector3(Mathf.Sin(a)*.234f,.052f,Mathf.Cos(a)*.234f);side.transform.localRotation=Quaternion.Euler(0,i*30f,0);
                var c=side.AddComponent<BoxCollider>();c.size=new Vector3(.13f,.08f,.025f);
            }
            Box("Pan handle",root.transform,new Vector3(0,.025f,-.4f),new Vector3(.09f,.065f,.35f),wood);
            Grab(root,1.1f);
            var pan=root.AddComponent<KitchenPan>();pan.cookingSurface=Empty("Cooking surface",root.transform,new Vector3(0,.08f,0));pan.bounds=new Vector3(.205f,.10f,.205f);
            root.AddComponent<KitchenRespawnTool>();return root;
        }
        static GameObject CreateSink()
        {
            var root=new GameObject("WashStation");Counter(root.transform,1.3f,false);
            Box("Sink floor",root.transform,new Vector3(0,.75f,0),new Vector3(1.12f,.06f,.79f),metal);
            Box("Left rim",root.transform,new Vector3(-.55f,.9f,0),new Vector3(.18f,.31f,.92f),white);
            Box("Right rim",root.transform,new Vector3(.55f,.9f,0),new Vector3(.18f,.31f,.92f),white);
            Box("Front rim",root.transform,new Vector3(0,.91f,-.41f),new Vector3(1.03f,.29f,.13f),white);
            Box("Back rim",root.transform,new Vector3(0,.91f,.40f),new Vector3(1.03f,.29f,.13f),white);
            Box("Faucet upright",root.transform,new Vector3(.12f,1.24f,.3f),new Vector3(.045f,.48f,.045f),metal);
            Box("Faucet neck",root.transform,new Vector3(.12f,1.47f,.12f),new Vector3(.045f,.045f,.39f),metal);
            var water=Primitive("Running water",PrimitiveType.Cylinder,root.transform,new Vector3(.12f,1.11f,-.05f),new Vector3(.032f,.35f,.032f),FlowMat(),false);
            Box("Tap base",root.transform,new Vector3(.4f,1.03f,.25f),new Vector3(.13f,.12f,.13f),metal);
            var tapPivot=Empty("Tap hinge",root.transform,new Vector3(.4f,1.095f,.25f));
            var handle=Box("Tap lever",tapPivot,new Vector3(0,0,-.15f),new Vector3(.065f,.055f,.30f),brass);
            var toggle=tapPivot.gameObject.AddComponent<KitchenToggle>();toggle.activeVisual=water;toggle.labelPrefix="TAP";
            toggle.pivot=tapPivot;toggle.localAxis=Vector3.right;toggle.openAngle=65;
            toggle.GetComponent<XRSimpleInteractable>().colliders.Clear();
            toggle.GetComponent<XRSimpleInteractable>().colliders.Add(handle.GetComponent<Collider>());
            toggle.label=Text("Tap label","TAP",root.transform,new Vector3(.4f,1.34f,.3f),.013f);
            var flow=water.AddComponent<KitchenUVFlow>();flow.target=water.GetComponent<Renderer>();flow.source=toggle;flow.speed=.8f;flow.generateStripes=false;flow.direction=Vector2.down;
            var zoneObj=new GameObject("Water washing area");zoneObj.transform.SetParent(root.transform,false);zoneObj.transform.localPosition=new Vector3(0,1.05f,-.03f);
            var zone=zoneObj.AddComponent<BoxCollider>();zone.isTrigger=true;zone.size=new Vector3(.89f,.55f,.59f);
            var wash=zoneObj.AddComponent<KitchenWashZone>();wash.faucet=toggle;wash.label=Screen("Wash progress",root.transform,new Vector3(0,1.7f,.32f),1.18f,.3f);
            Hint(root.transform,"washing","Hold and lift the lever. Wash produce for 3 s.\nLower the lever to stop the water.",new Vector3(0,1.3f,-.65f));
            return root;
        }
        static GameObject CreatePrep()
        {
            var root=new GameObject("PrepStation");Counter(root.transform,2.6f);
            Box("Cutting board",root.transform,new Vector3(-.67f,1.012f,0),new Vector3(.88f,.055f,.63f),wood);
            Text("Board label","WASH  >  CHOP",root.transform,new Vector3(-.67f,1.02f,-.35f),.016f).transform.localRotation=Quaternion.Euler(65,0,0);
            var knife=new GameObject("ChefKnife");knife.transform.SetParent(root.transform,false);knife.transform.localPosition=new Vector3(-.67f,1.085f,-.02f);
            Box("Knife blade",knife.transform,new Vector3(.025f,0,0),new Vector3(.22f,.065f,.009f),metal);
            Box("Knife handle",knife.transform,new Vector3(-.16f,0,0),new Vector3(.16f,.045f,.035f),dark);
            Grab(knife,.25f);knife.AddComponent<KitchenKnife>();knife.AddComponent<KitchenRespawnTool>();
            var bowl=new GameObject("MixingBowl");bowl.transform.SetParent(root.transform,false);bowl.transform.localPosition=new Vector3(.62f,1.03f,.08f);
            Primitive("Bowl base",PrimitiveType.Cylinder,bowl.transform,new Vector3(0,.01f,0),new Vector3(.42f,.018f,.42f),white,false);
            Torus("Bowl lip",bowl.transform,new Vector3(0,.12f,0),.22f,.032f,white);
            var bowlFloor=bowl.AddComponent<BoxCollider>();bowlFloor.center=new Vector3(0,.015f,0);bowlFloor.size=new Vector3(.37f,.03f,.37f);
            var inner=new GameObject("Bowl ingredient trigger");inner.transform.SetParent(bowl.transform,false);inner.transform.localPosition=new Vector3(0,.1f,0);
            var trigger=inner.AddComponent<BoxCollider>();trigger.isTrigger=true;trigger.size=new Vector3(.4f,.19f,.4f);
            Grab(bowl,.7f);bowl.GetComponent<Rigidbody>().isKinematic=true;
            var salad=bowl.AddComponent<KitchenSaladBowl>();salad.saladPrefab=foods[FoodKind.Salad].prefab;
            salad.outputPoint=Empty("Salad output",root.transform,new Vector3(.63f,1.3f,-.1f));
            salad.label=Screen("Bowl state",root.transform,new Vector3(.65f,1.29f,.37f),.96f,.24f);
            Button("Mix salad",root.transform,new Vector3(.65f,1.03f,-.32f),new Vector3(.32f,.06f,.15f),"MIX",salad.MixAction);
            Text("Prep instructions","FRESH SALAD\nWash fruit + vegetables\nChop two kinds, drop into bowl, MIX",root.transform,new Vector3(0,.68f,-.482f),.018f);
            Hint(root.transform,"chop","Leave produce on the board, then grab the knife.\nMove the blade through it. Pick up the chopped pieces.",new Vector3(-.65f,1.38f,-.4f));
            Hint(root.transform,"salad","Drop two different chopped ingredients into the bowl.\nPress MIX. One dirty ingredient contaminates the salad.",new Vector3(.65f,1.4f,-.4f));
            return root;
        }
        static GameObject CreateBin()
        {
            var root=new GameObject("CompostBin");
            Primitive("Bin base",PrimitiveType.Cylinder,root.transform,new Vector3(0,.055f,0),new Vector3(.53f,.055f,.53f),dark,false);
            for(int i=0;i<12;i++)
            {
                float a=i*Mathf.PI/6;var side=Box("Bin wall",root.transform,new Vector3(Mathf.Sin(a)*.26f,.36f,Mathf.Cos(a)*.26f),new Vector3(.14f,.62f,.035f),green);side.transform.localRotation=Quaternion.Euler(0,i*30,0);
            }
            Torus("Brass rim",root.transform,new Vector3(0,.68f,0),.26f,.018f,brass);
            var zone=new GameObject("Discard food here");zone.transform.SetParent(root.transform,false);zone.transform.localPosition=new Vector3(0,.55f,0);
            var trigger=zone.AddComponent<BoxCollider>();trigger.isTrigger=true;trigger.size=new Vector3(.45f,.3f,.45f);
            var trash=zone.AddComponent<KitchenTrash>();trash.label=Text("Bin label","COMPOST",root.transform,new Vector3(0,.9f,0),.018f);
            Hint(root.transform,"trash","Release unwanted food into the bin.\nCookware and tools are kept.",new Vector3(0,1.2f,-.25f));return root;
        }
        static void CreateRoom(Transform parent)
        {
            var floor=Box("Kitchen floor",parent,new Vector3(0,-.1f,0),new Vector3(12,.2f,10),Mat("Floor grout",new Color(.47f,.44f,.36f)));
            var teleport=floor.AddComponent<TeleportationArea>();teleport.interactionLayers=1 << 31;
            for(int x=0;x<12;x++) for(int z=0;z<10;z++)
                Box("Floor tile",parent,new Vector3(x-5.5f,.003f,z-4.5f),new Vector3(.975f,.012f,.975f),(x+z)%2==0?Mat("Cream tile",new Color(.80f,.77f,.65f)):Mat("Sand tile",new Color(.72f,.68f,.55f)),false);
            Box("Back wall",parent,new Vector3(0,1.8f,4.95f),new Vector3(12,3.6f,.18f),cream);
            Box("Ceiling",parent,new Vector3(0,3.65f,0),new Vector3(12,.1f,10),cream).GetComponent<Renderer>().shadowCastingMode=ShadowCastingMode.Off;
            Box("Left wall",parent,new Vector3(-6,1.8f,0),new Vector3(.18f,3.6f,10),cream);
            Box("Right wall",parent,new Vector3(6,1.8f,0),new Vector3(.18f,3.6f,10),cream);
            Box("Back splash",parent,new Vector3(-.8f,1.2f,4.84f),new Vector3(9.8f,.8f,.04f),Mat("Sage tile",new Color(.49f,.63f,.53f)));
            for(int x=0;x<20;x++)Box("Tile grout",parent,new Vector3(-5.55f+x*.5f,1.2f,4.81f),new Vector3(.012f,.8f,.008f),white,false);
            for(int y=0;y<3;y++)Box("Tile grout",parent,new Vector3(-.8f,.95f+y*.25f,4.81f),new Vector3(9.8f,.012f,.008f),white,false);
            Box("Back skirting",parent,new Vector3(0,.10f,4.83f),new Vector3(11.8f,.2f,.05f),green);
            Box("Right skirting",parent,new Vector3(5.88f,.10f,0),new Vector3(.05f,.2f,9.9f),green);
            Box("Left skirting",parent,new Vector3(-5.88f,.10f,0),new Vector3(.05f,.2f,9.9f),green);
            // A framed stylized view is inexpensive on standalone headsets.
            var window=Empty("Window",parent,new Vector3(-5.885f,2.2f,3.25f));window.localRotation=Quaternion.Euler(0,-90,0);
            Box("Window sky",window,Vector3.zero,new Vector3(2.05f,1.4f,.025f),Mat("Sky",new Color(.49f,.74f,.83f)),false);
            Sphere("Distant hill",window,new Vector3(0,-.45f,-.03f),new Vector3(2.0f,.75f,.03f),Mat("Hill",new Color(.43f,.59f,.39f)));
            Box("Window top",window,new Vector3(0,.73f,-.055f),new Vector3(2.16f,.08f,.12f),wood);
            Box("Window bottom",window,new Vector3(0,-.73f,-.055f),new Vector3(2.16f,.08f,.12f),wood);
            for(int s=-1;s<=1;s+=2)Box("Window frame",window,new Vector3(s*1.04f,0,-.055f),new Vector3(.08f,1.5f,.12f),wood);
            Box("Window mullion",window,new Vector3(0,0,-.07f),new Vector3(.05f,1.45f,.06f),white);
            for(int i=0;i<3;i++)
            {
                var light=Primitive("Pendant shade",PrimitiveType.Cylinder,parent,new Vector3(-1.45f+i*1.45f,3.0f,.3f),new Vector3(.46f,.11f,.46f),green,false);
                Box("Pendant cord",parent,new Vector3(-1.45f+i*1.45f,3.33f,.3f),new Vector3(.018f,.6f,.018f),dark,false);
                Primitive("Pendant diffuser",PrimitiveType.Cylinder,parent,new Vector3(-1.45f+i*1.45f,2.88f,.3f),new Vector3(.39f,.014f,.39f),Mat("Lamp glow",new Color(1,.84f,.47f),0,true),false);
            }
            Text("Kitchen title","THE DAILY KITCHEN",parent,new Vector3(-.65f,3.32f,4.8f),.058f).color=new Color(.12f,.25f,.20f);
            Text("Kitchen subtitle","FRESH INGREDIENTS  /  GOOD FOOD  /  MORE ENERGY",parent,new Vector3(-.65f,2.95f,4.8f),.021f).color=new Color(.2f,.32f,.26f);
        }
        static void CreateBoards(Transform parent,KitchenSession session)
        {
            var recipes=Empty("Recipe wall",parent,new Vector3(4.5f,1.98f,4.78f));
            Box("Recipe frame",recipes,Vector3.zero,new Vector3(2.55f,1.5f,.055f),wood);
            Box("Recipe board",recipes,new Vector3(0,0,-.035f),new Vector3(2.43f,1.38f,.025f),dark,false);
            Text("Recipe title","Recipes:",recipes,new Vector3(-1.08f,.58f,-.055f),.034f,TextAnchor.UpperLeft);
            RecipeRow("chicken","Roast Chicken","25s-30s",recipes,.16f);
            RecipeRow("steak","Pan-Seared Steak","8s-17s",recipes,-.13f);
            RecipeRow("salad","Salad","cleaned fruit & Veg",recipes,-.42f);
            var board=Empty("Kitchen inventory board",parent,new Vector3(-5.85f,1.6f,.5f));board.localRotation=Quaternion.Euler(0,-90,0);
            Box("Inventory back",board,Vector3.zero,new Vector3(1.9f,2.0f,.06f),dark);
            session.resourceText=Text("Shared inventory","INVENTORY",board,new Vector3(-.82f,.85f,-.055f),.022f,TextAnchor.UpperLeft);
            session.statusText=Screen("Last action",parent,new Vector3(.1f,.9f,-.19f),2.5f,.19f);
            Hint(parent,"energy","Hold a meal and press X to eat.\nYour energy is shown at the upper-left of your view.",new Vector3(2.4f,1.65f,.3f));
        }
        static void RecipeRow(string id,string dish,string note,Transform parent,float y)
        {
            var name=Text("Recipe "+id,dish,parent,new Vector3(-1.08f,y,-.055f),.019f,TextAnchor.UpperLeft);
            var nameFit=name.gameObject.AddComponent<KitchenTextFit>();nameFit.maxWidth=1.27f;nameFit.maxHeight=.18f;
            var detail=Text("Recipe "+id+" detail",note,parent,new Vector3(1.1f,y,-.055f),.018f,TextAnchor.UpperRight);
            detail.alignment=TextAlignment.Right;
            var detailFit=detail.gameObject.AddComponent<KitchenTextFit>();detailFit.maxWidth=.88f;detailFit.maxHeight=.20f;
            detail.color=new Color(.73f,.85f,.68f);
        }
        static void CreateGrid(Transform parent,GameObject foundation,GameObject fridge,GameObject hob,GameObject oven)
        {
            var root=new GameObject("Kitchen expansion grid");root.transform.SetParent(parent,false);
            var grid=root.AddComponent<KitchenGridPlacement>();grid.modulePrefabs=new[]{foundation,fridge,hob,oven};
            var cells=new List<Transform>();var indicators=new List<Renderer>();
            for(int i=0;i<4;i++)
            {
                var cell=Empty("Grid cell "+i,root.transform,new Vector3(-4.7f+(i%2)*1.6f,0,-1.3f-(i/2)*1.8f));cells.Add(cell);
                var marker=Box("Placement pad "+i,cell,new Vector3(0,.015f,0),new Vector3(1.13f,.025f,1.06f),Mat("Grid glow",new Color(.22f,.51f,.39f)),false);indicators.Add(marker.GetComponent<Renderer>());
                var select=Box("Build here "+(i+1),cell,new Vector3(0,.05f,-.60f),new Vector3(.54f,.06f,.18f),brass);
                var gridCell=select.AddComponent<KitchenGridCell>();gridCell.grid=grid;gridCell.index=i;
                var action=select.AddComponent<KitchenAction>();action.label="Build in cell "+(i+1);UnityEventTools.AddPersistentListener(action.action,gridCell.Place);
                Text("Cell label","BUILD "+(i+1),cell,new Vector3(0,.085f,-.60f),.018f).transform.localRotation=Quaternion.Euler(90,0,0);
            }
            grid.cells=cells.ToArray();grid.cellIndicators=indicators.ToArray();
            var toolbar=Empty("Build menu",root.transform,new Vector3(-5.8f,1.65f,-2.2f));toolbar.localRotation=Quaternion.Euler(0,-90,0);
            Box("Build panel",toolbar,Vector3.zero,new Vector3(2.45f,1.45f,.04f),dark);
            Text("Build title","KITCHEN EXPANSION",toolbar,new Vector3(0,.61f,-.04f),.026f);
            grid.label=Text("Build choice","SELECT A MODULE",toolbar,new Vector3(0,.23f,-.055f),.020f);
            string[] labels={"BASE","FRIDGE","HOB","OVEN"};
            for(int i=0;i<4;i++)
            {
                var button=Box(labels[i],toolbar,new Vector3(-.8f+i*.54f,-.12f,-.07f),new Vector3(.48f,.2f,.08f),green);
                var action=button.AddComponent<KitchenAction>();action.label=labels[i];UnityEventTools.AddIntPersistentListener(action.action,grid.Select,i);
                Text(labels[i]+" label",labels[i],toolbar,new Vector3(-.8f+i*.54f,-.12f,-.12f),.019f);
            }
            var instructions=Text("Build instructions","Choose a module, then a floor BUILD pad.\nPlace BASE first. One appliance per base.",toolbar,new Vector3(0,-.38f,-.05f),.016f);
            var fit=instructions.gameObject.AddComponent<KitchenTextFit>();fit.maxWidth=2.20f;fit.maxHeight=.28f;
            Hint(root.transform,"building","Pick BASE on the wall, then a floor BUILD pad.\nNext select an appliance and the same grid cell.",new Vector3(-3.6f,1.25f,-2.1f));
        }
        static TextMesh Screen(string name,Transform parent,Vector3 position,float width,float height)
        {
            var panel=Empty(name+" panel",parent,position);
            Box("Brass border",panel,Vector3.zero,new Vector3(width+.035f,height+.035f,.032f),brass,false);
            Box("Display back",panel,new Vector3(0,0,-.021f),new Vector3(width,height,.018f),dark,false);
            var text=Text(name,name.ToUpperInvariant(),panel,new Vector3(0,height*.5f-.025f,-.033f),.018f);
            var fit=text.gameObject.AddComponent<KitchenTextFit>();fit.maxWidth=width-.06f;fit.maxHeight=height-.05f;
            return text;
        }
        static void Counter(Transform parent,float width,bool top=true)
        {
            Box("Cabinet body",parent,new Vector3(0,.45f,0),new Vector3(width,.9f,.9f),green);
            if(top)Box("Countertop",parent,new Vector3(0,.95f,0),new Vector3(width+.06f,.07f,.97f),white);
            for(int s=-1;s<=1;s+=2)
            {
                Box("Cabinet inset",parent,new Vector3(s*width*.245f,.47f,-.461f),new Vector3(width*.44f,.70f,.02f),Mat("Sage inset",new Color(.28f,.44f,.37f)),false);
                Box("Cabinet handle",parent,new Vector3(s*width*.075f,.66f,-.49f),new Vector3(.025f,.16f,.04f),brass,false);
            }
            Box("Toe kick",parent,new Vector3(0,.06f,-.43f),new Vector3(width,.12f,.06f),dark,false);
        }
        static KitchenKnob Knob(Transform parent,Vector3 position,string label,float max,float step,string unit)
        {
            var root=new GameObject(label+" rotary knob");root.transform.SetParent(parent,false);root.transform.localPosition=position;
            var visual=Empty("Dial",root.transform,Vector3.zero);
            visual.localRotation=Quaternion.AngleAxis(-135,Vector3.back);
            var disk=Primitive("Dial grip",PrimitiveType.Cylinder,visual,Vector3.zero,new Vector3(.17f,.045f,.17f),brass,true);disk.transform.localRotation=Quaternion.Euler(90,0,0);
            Box("Dial index",visual,new Vector3(0,.05f,-.048f),new Vector3(.012f,.050f,.008f),dark,false);
            var knob=root.AddComponent<KitchenKnob>();knob.knobVisual=visual;knob.minValue=0;knob.maxValue=max;knob.step=step;knob.unit=unit;knob.labelPrefix=label;knob.localAxis=Vector3.back;
            knob.GetComponent<XRSimpleInteractable>().colliders.Clear();
            knob.GetComponent<XRSimpleInteractable>().colliders.Add(disk.GetComponent<Collider>());
            int intervals=label=="Oven"?6:4;
            for(int i=0;i<=intervals;i++)
            {
                float degrees=-135+i*270f/intervals;
                Vector3 direction=Quaternion.AngleAxis(degrees,Vector3.back)*Vector3.up;
                var tick=Box("Dial tick "+i,root.transform,direction*.108f+Vector3.back*.008f,new Vector3(.007f,.021f,.006f),white,false);
                tick.transform.localRotation=Quaternion.AngleAxis(degrees,Vector3.back);
                string value=(max*i/intervals).ToString("0")+(i==intervals?unit:"");
                Text("Dial scale "+i,value,root.transform,direction*.145f+Vector3.back*.012f,.008f,TextAnchor.MiddleCenter);
            }
            return knob;
        }
        static void Button(string name,Transform parent,Vector3 position,Vector3 size,string label,UnityAction onClick)
        {
            var b=Box(name,parent,position,size,brass);var action=b.AddComponent<KitchenAction>();action.label=label;UnityEventTools.AddPersistentListener(action.action,onClick);
            Text(name+" label",label,parent,position+new Vector3(0,size.y*.55f,0),.017f).transform.localRotation=Quaternion.Euler(90,0,0);
        }
        static void Hint(Transform parent,string id,string message,Vector3 position)
        {
            var hint=Empty(id+" tutorial",parent,position).gameObject.AddComponent<KitchenStationHint>();hint.id=id;hint.message=message;
        }
        static ResourceData Resource(string id,string display)
        {
            string path=Root+"/Data/Resources/"+id+".asset";var asset=AssetDatabase.LoadAssetAtPath<ResourceData>(path);
            if(!asset){asset=ScriptableObject.CreateInstance<ResourceData>();AssetDatabase.CreateAsset(asset,path);}
            asset.Configure(display,"Shared House Sim kitchen resource");EditorUtility.SetDirty(asset);if(!resources.Contains(asset))resources.Add(asset);return asset;
        }
        static Material Mat(string name,Color color,float metallic=0,bool emission=false)
        {
            if(mats.TryGetValue(name,out var cached))return cached;
            string path=Root+"/Materials/"+name+".mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!mat){mat=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(mat,path);}
            mat.SetColor("_BaseColor",color);mat.SetFloat("_Metallic",metallic);mat.SetFloat("_Smoothness",metallic>0?.55f:.25f);
            if(emission){mat.EnableKeyword("_EMISSION");mat.SetColor("_EmissionColor",color*.5f);}
            EditorUtility.SetDirty(mat);mats[name]=mat;return mat;
        }
        static Material FlowMat()
        {
            var mat=Mat("Flowing water",new Color(.2f,.65f,.82f),.15f);
            string path=Root+"/Materials/WaterFlow.asset";var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if(!texture)
            {
                texture=new Texture2D(16,64,TextureFormat.RGBA32,false);texture.name="WaterFlow";texture.wrapMode=TextureWrapMode.Repeat;
                for(int y=0;y<64;y++)for(int x=0;x<16;x++)texture.SetPixel(x,y,y%16<5?new Color(.75f,.96f,1):new Color(.19f,.59f,.83f));
                texture.Apply();AssetDatabase.CreateAsset(texture,path);
            }
            mat.SetTexture("_BaseMap",texture);EditorUtility.SetDirty(mat);return mat;
        }
        static GameObject Primitive(string name,PrimitiveType type,Transform parent,Vector3 pos,Vector3 scale,Material material,bool collider)
        {
            var go=GameObject.CreatePrimitive(type);go.name=name;go.transform.SetParent(parent,false);go.transform.localPosition=pos;go.transform.localScale=scale;go.GetComponent<Renderer>().sharedMaterial=material;
            if(!collider)UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());return go;
        }
        static GameObject Box(string name,Transform parent,Vector3 pos,Vector3 size,Material material,bool collider=true)=>Primitive(name,PrimitiveType.Cube,parent,pos,size,material,collider);
        static GameObject Sphere(string name,Transform parent,Vector3 pos,Vector3 size,Material material)=>Primitive(name,PrimitiveType.Sphere,parent,pos,size,material,false);
        static Transform Empty(string name,Transform parent,Vector3 pos)
        {var go=new GameObject(name);go.transform.SetParent(parent,false);go.transform.localPosition=pos;return go.transform;}
        static TextMesh Text(string name,string content,Transform parent,Vector3 pos,float size,TextAnchor anchor=TextAnchor.UpperCenter)
        {
            var go=new GameObject(name);go.transform.SetParent(parent,false);go.transform.localPosition=pos;
            var text=go.AddComponent<TextMesh>();text.text=content;text.font=font;text.fontSize=64;text.characterSize=size;text.anchor=anchor;text.alignment=anchor==TextAnchor.UpperLeft?TextAlignment.Left:TextAlignment.Center;text.color=new Color(.94f,.9f,.76f);
            go.GetComponent<MeshRenderer>().sharedMaterial=font.material;
            float fitWidth=0,fitHeight=0;
            if(name=="Recipes"){fitWidth=2.3f;fitHeight=1.1f;}
            else if(name=="Controls instructions"){fitWidth=2.62f;fitHeight=1.24f;}
            else if(name=="Shared inventory"){fitWidth=1.87f;fitHeight=1.43f;}
            else if(name=="Build choice"){fitWidth=2.05f;fitHeight=.22f;}
            if(fitWidth>0){var fit=go.AddComponent<KitchenTextFit>();fit.maxWidth=fitWidth;fit.maxHeight=fitHeight;}
            return text;
        }
        static void Grab(GameObject go,float mass)
        {
            var rb=go.GetComponent<Rigidbody>();if(!rb)rb=go.AddComponent<Rigidbody>();rb.mass=mass;rb.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;rb.interpolation=RigidbodyInterpolation.Interpolate;
            var grab=go.GetComponent<XRGrabInteractable>();if(!grab)grab=go.AddComponent<XRGrabInteractable>();grab.movementType=XRBaseInteractable.MovementType.VelocityTracking;grab.useDynamicAttach=true;grab.throwOnDetach=true;
        }
        static GameObject SavePrefab(GameObject root,string relative)
        {
            var prefab=PrefabUtility.SaveAsPrefabAsset(root,Root+"/Prefabs/"+relative+".prefab");UnityEngine.Object.DestroyImmediate(root);return prefab;
        }
        static GameObject Instance(GameObject prefab,Transform parent,Vector3 pos)
        {var go=(GameObject)PrefabUtility.InstantiatePrefab(prefab,parent);go.transform.localPosition=pos;return go;}
        static GameObject Torus(string name,Transform parent,Vector3 pos,float major,float minor,Material material)
        {
            const int around=48,tube=8;var vertices=new Vector3[around*tube];var normals=new Vector3[vertices.Length];var uv=new Vector2[vertices.Length];var triangles=new int[around*tube*6];
            for(int i=0;i<around;i++)for(int j=0;j<tube;j++)
            {
                float a=i*Mathf.PI*2/around,b=j*Mathf.PI*2/tube;int k=i*tube+j;
                var n=new Vector3(Mathf.Cos(a)*Mathf.Cos(b),Mathf.Sin(b),Mathf.Sin(a)*Mathf.Cos(b));
                vertices[k]=new Vector3(Mathf.Cos(a)*major,0,Mathf.Sin(a)*major)+n*minor;normals[k]=n;uv[k]=new Vector2(i/(float)around,j/(float)tube);
                int q=k*6;triangles[q]=k;triangles[q+1]=i*tube+(j+1)%tube;triangles[q+2]=((i+1)%around)*tube+j;triangles[q+3]=triangles[q+1];triangles[q+4]=((i+1)%around)*tube+(j+1)%tube;triangles[q+5]=triangles[q+2];
            }
            string path=Root+"/Meshes/Ring_"+major.ToString("F3",System.Globalization.CultureInfo.InvariantCulture)+"_"+minor.ToString("F3",System.Globalization.CultureInfo.InvariantCulture)+".asset";
            var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(!mesh){mesh=new Mesh();mesh.name=name;AssetDatabase.CreateAsset(mesh,path);}
            mesh.Clear();mesh.vertices=vertices;mesh.normals=normals;mesh.uv=uv;mesh.triangles=triangles;mesh.RecalculateBounds();EditorUtility.SetDirty(mesh);
            var go=new GameObject(name);go.transform.SetParent(parent,false);go.transform.localPosition=pos;go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=material;return go;
        }
    }
}
