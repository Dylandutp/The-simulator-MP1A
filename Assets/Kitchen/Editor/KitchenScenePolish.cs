using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace HouseSim.Kitchen.Editor
{
    /// <summary>Targeted migration of the user's saved scene. Never calls the scene generator.</summary>
    public static class KitchenScenePolish
    {
        const string ScenePath="Assets/Kitchen/Scenes/Kitchen.unity";
        static readonly HashSet<string> RemovedNames=new HashSet<string>{"Stock display panel","Oven status panel","Hob status panel","Wash progress panel","Bowl state panel","Last action panel","Prep instructions","Board label","Bin label","Tap label"};
        public static void ApplyAndCheck() { Apply(); KitchenFoodChecks.Run(); KitchenPlayChecks.Run(); }
        [Serializable] class Pose { public string id,path; public Vector3 position,scale; public Quaternion rotation; }
        [Serializable] class Audit { public int preservedTransforms; public string[] unexpectedChanges; }

        [MenuItem("Kitchen/Apply Pointed HUD and Lighting to Saved Scene")]
        public static void Apply()
        {
            if(EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode before editing the saved scene.");
            var scene=EditorSceneManager.OpenScene(ScenePath);
            var room=scene.GetRootGameObjects().Single(go=>go.name=="Kitchen Room");
            var before=room.GetComponentsInChildren<Transform>(true).Where(t=>!Intended(t)).Select(t=>new Pose{
                id=GlobalObjectId.GetGlobalObjectIdSlow(t).ToString(),path=PathOf(t),position=t.localPosition,rotation=t.localRotation,scale=t.localScale}).ToArray();
            Directory.CreateDirectory("Logs/UserLayoutBackup");
            string backup="Logs/UserLayoutBackup/Kitchen.pre-polish."+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")+".unity";
            File.Copy(ScenePath,backup,false);
            foreach(string path in AssetDatabase.FindAssets("t:Prefab",new[]{"Assets/Kitchen/Prefabs"}).Select(AssetDatabase.GUIDToAssetPath))
            {
                var root=PrefabUtility.LoadPrefabContents(path);
                try { RemoveSigns(root); Describe(root); PrefabUtility.SaveAsPrefabAsset(root,path); }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            RemoveSigns(room);
            foreach(var fridge in room.GetComponentsInChildren<KitchenFridge>(true)) Describe(fridge.gameObject);
            foreach(var oven in room.GetComponentsInChildren<KitchenOven>(true)) Describe(oven.gameObject);
            foreach(var hob in room.GetComponentsInChildren<KitchenHob>(true)) Describe(hob.gameObject);
            foreach(var zone in room.GetComponentsInChildren<KitchenWashZone>(true)) Describe(zone.transform.parent.gameObject);
            foreach(var bowl in room.GetComponentsInChildren<KitchenSaladBowl>(true)) Describe(bowl.transform.parent.gameObject);
            var session=room.GetComponentInChildren<KitchenSession>();
            RepairInventory(room,session);
            var grid=room.GetComponentInChildren<KitchenGridPlacement>();
            grid.equipmentYawDegrees=new float[grid.cells.Length];
            grid.equipmentYawDegrees[0]=180; grid.equipmentYawDegrees[1]=180;
            AddInfo(grid.gameObject,"Kitchen expansion","Choose BASE, then a BUILD tile. Add one appliance to that same tile.");
            AddLighting(room);
            var after=room.GetComponentsInChildren<Transform>(true).GroupBy(t=>GlobalObjectId.GetGlobalObjectIdSlow(t).ToString()).ToDictionary(g=>g.Key,g=>g.First());
            var changed=new List<string>();
            foreach(var pose in before)
            {
                if(!after.TryGetValue(pose.id,out var t)) { changed.Add("Missing: "+pose.path); continue; }
                if(Vector3.Distance(t.localPosition,pose.position)>.0001f || Vector3.Distance(t.localScale,pose.scale)>.0001f || Quaternion.Angle(t.localRotation,pose.rotation)>.01f) changed.Add("Moved: "+pose.path);
            }
            File.WriteAllText("Logs/scene-layout-preservation.json",JsonUtility.ToJson(new Audit{preservedTransforms=before.Length-changed.Count,unexpectedChanges=changed.ToArray()},true));
            if(changed.Count>0) throw new InvalidOperationException("Unexpected layout change; scene not saved: "+string.Join(", ",changed));
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("KITCHEN_POLISH_PASS: preserved "+before.Length+" unrelated transforms; inventory, hover HUD, grid orientation and lighting applied.");
            Preview(room,session);
        }
        static bool Intended(Transform t)
        {
            for(var p=t;p;p=p.parent)
                if(p.name=="Kitchen inventory board" || p.name=="Pendant light switch" || p.name.StartsWith("Pendant soft light ") || RemovedNames.Contains(p.name) || p.GetComponent<KitchenStationHint>()) return true;
            return false;
        }
        static string PathOf(Transform t)=>t.parent?PathOf(t.parent)+"/"+t.name:t.name;
        static void RemoveSigns(GameObject root)
        {
            foreach(var t in root.GetComponentsInChildren<Transform>(true).Reverse())
                if(t && (RemovedNames.Contains(t.name) || t.GetComponent<KitchenStationHint>())) Object.DestroyImmediate(t.gameObject);
        }
        static void AddInfo(GameObject root,string title,string message)
        {
            var info=root.GetComponent<KitchenItemInfo>(); if(!info) info=root.AddComponent<KitchenItemInfo>();
            info.title=title; info.description=message;
            if(PrefabUtility.IsPartOfPrefabInstance(info)) PrefabUtility.RecordPrefabInstancePropertyModifications(info);
        }
        static void Describe(GameObject root)
        {
            foreach(var pan in root.GetComponentsInChildren<KitchenPan>()) AddInfo(pan.gameObject,"Frying pan","Rest upright on the induction ring, then add steak. Lift the pan to stop heating.");
            foreach(var knife in root.GetComponentsInChildren<KitchenKnife>()) AddInfo(knife.gameObject,"Chef's knife","Put washed produce on the board, then move the held blade through it to chop.");
            foreach(var bowl in root.GetComponentsInChildren<KitchenSaladBowl>()) AddInfo(bowl.gameObject,"Salad bowl","Add two different chopped ingredients, then press MIX. Every ingredient must be washed.");
            if(root.GetComponent<KitchenFridge>()) AddInfo(root,"Fridge","Hold and pull the handle. Take ingredients; opening or closing restocks empty spaces.");
            else if(root.GetComponent<KitchenOven>()) AddInfo(root,"Oven","Place chicken inside and close the door. Turn the dial: 25-30 s for golden chicken.");
            else if(root.GetComponent<KitchenHob>()) AddInfo(root,"Induction hob","Rest the pan on the ring. Turn the heat dial; steak takes 8-17 s at 50% heat.");
            else if(root.GetComponentInChildren<KitchenWashZone>()) AddInfo(root,"Sink","Hold and lift the tap lever. Rinse fruit and vegetables in the stream for 3 seconds.");
            else if(root.GetComponentInChildren<KitchenSaladBowl>()) AddInfo(root,"Salad station","Wash and chop two different ingredients. Drop them into the bowl, then press MIX.");
            else if(root.GetComponentInChildren<KitchenTrash>()) AddInfo(root,"Food bin","Release unwanted food inside to discard it. Pans, knives and bowls are kept.");
            else if(root.GetComponent<KitchenFood>()) AddInfo(root,"Food","Prepare before eating. Bring a meal to your mouth + X, or hold it and press F on PC.");
        }
        static void RepairInventory(GameObject room,KitchenSession session)
        {
            var all=room.GetComponentsInChildren<Transform>(true);
            var wall=all.Single(t=>t.name=="Right wall").GetComponent<Renderer>();
            var board=all.Single(t=>t.name=="Kitchen inventory board");
            board.SetPositionAndRotation(new Vector3(wall.bounds.min.x-.07f,1.75f,wall.bounds.center.z-.8f),Quaternion.Euler(0,90,0));
            board.localScale=Vector3.one;
            var back=board.Find("Inventory back"); back.localPosition=Vector3.zero; back.localRotation=Quaternion.identity; back.localScale=new Vector3(1.9f,2f,.06f);
            var text=board.GetComponentInChildren<TextMesh>();
            text.transform.localPosition=new Vector3(-.80f,.86f,-.055f); text.transform.localRotation=Quaternion.identity; text.transform.localScale=Vector3.one;
            text.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); text.fontSize=64; text.characterSize=.022f;
            text.anchor=TextAnchor.UpperLeft; text.alignment=TextAlignment.Left; text.color=new Color(.94f,.90f,.76f);
            text.GetComponent<Renderer>().sharedMaterial=text.font.material;
            text.GetComponent<Renderer>().shadowCastingMode=ShadowCastingMode.Off;
            text.text="PLAYER INVENTORY\n"+string.Join("\n",session.visibleResources.Where(r=>r && r!=session.energyResource).Select(r=>r.DisplayName+": 0"));
            var fit=text.GetComponent<KitchenTextFit>(); fit.maxWidth=1.6f; fit.maxHeight=1.72f; fit.preferredCharacterSize=.022f;
            session.resourceText=text; session.statusText=null;
        }
        static void AddLighting(GameObject room)
        {
            var all=room.GetComponentsInChildren<Transform>(true);
            var wall=all.Single(t=>t.name=="Right wall").GetComponent<Renderer>();
            var switchRoot=all.FirstOrDefault(t=>t.name=="Pendant light switch");
            if(!switchRoot) {switchRoot=new GameObject("Pendant light switch").transform; switchRoot.SetParent(room.transform,false);}
            switchRoot.SetPositionAndRotation(new Vector3(wall.bounds.min.x-.07f,1.35f,wall.bounds.center.z-2.2f),Quaternion.Euler(0,90,0));
            var lighting=switchRoot.GetComponent<KitchenPendantLights>(); if(!lighting) lighting=switchRoot.gameObject.AddComponent<KitchenPendantLights>();
            var shades=all.Where(t=>t.name=="Pendant shade").OrderBy(t=>t.position.x).ToArray();
            var lights=new List<Light>();
            for(int i=0;i<shades.Length;i++)
            {
                string name="Pendant soft light "+(i+1);
                var light=all.FirstOrDefault(t=>t.name==name)?.GetComponent<Light>();
                if(!light){light=new GameObject(name).AddComponent<Light>(); light.transform.SetParent(room.transform,false);}
                light.transform.position=shades[i].position+Vector3.down*.18f;
                light.transform.rotation=Quaternion.Euler(90,0,0);
                light.type=LightType.Spot; light.spotAngle=120; light.innerSpotAngle=70; light.range=5.5f; light.intensity=2f;
                light.color=new Color(1,.83f,.61f); light.shadows=LightShadows.None; light.renderMode=LightRenderMode.ForcePixel;
                lights.Add(light);
            }
            lighting.lights=lights.ToArray(); lighting.diffusers=all.Where(t=>t.name=="Pendant diffuser").Select(t=>t.GetComponent<Renderer>()).ToArray();
            var face=switchRoot.Find("Switch plate");
            if(!face)
            {
                var obj=GameObject.CreatePrimitive(PrimitiveType.Cube); face=obj.transform; obj.name="Switch plate"; face.SetParent(switchRoot,false);
                face.localScale=new Vector3(.19f,.26f,.045f);
                obj.GetComponent<Renderer>().sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/Kitchen/Materials/Porcelain.mat");
                var rocker=GameObject.CreatePrimitive(PrimitiveType.Cube); rocker.name="Light rocker"; rocker.transform.SetParent(switchRoot,false);
                rocker.transform.localPosition=new Vector3(0,0,-.04f); rocker.transform.localScale=new Vector3(.11f,.16f,.035f);
                rocker.GetComponent<Renderer>().sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/Kitchen/Materials/Brass.mat");
                var action=rocker.AddComponent<KitchenAction>(); action.label="Pendant lights";
                UnityEventTools.AddPersistentListener(action.action,lighting.Toggle); lighting.rocker=rocker.transform;
            }
            AddInfo(switchRoot.gameObject,"Pendant lights","Click the wall switch, or point and squeeze Grip, to toggle all three warm lights.");
            lighting.Apply();
        }
        static void Preview(GameObject room,KitchenSession session)
        {
            var camera=new GameObject("Polish preview camera").AddComponent<Camera>(); camera.fieldOfView=60; camera.nearClipPlane=.03f;
            var board=session.resourceText.transform.parent;
            camera.transform.position=board.position-board.forward*2.8f; camera.transform.rotation=board.rotation;
            KitchenBuildTools.Capture(camera,"Builds/Kitchen-Inventory.png",1200,1100);
            camera.transform.position=new Vector3(4.9f,2.8f,-4.35f); camera.transform.LookAt(new Vector3(-.3f,1.2f,1.8f));
            KitchenBuildTools.Capture(camera,"Builds/Kitchen-Preview.png",1600,1100);
            Object.DestroyImmediate(camera.gameObject);
        }
    }
}
