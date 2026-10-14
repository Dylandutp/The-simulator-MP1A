using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace HouseSim.Kitchen.Editor
{
    public static class KitchenRestaurantAssets
    {
        const string Root = "Assets/Kitchen/Art/RestaurantBits";
        static Material palette;
        public static void Validate()
        {
            RenderApplied();
            KitchenMechanicsChecks.RunAll();
        }
        public static void ApplyAndValidate() { Apply(); Validate(); }

        public static void RenderApplied()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camera = new GameObject("Model preview camera").AddComponent<Camera>();
            camera.transform.position = new Vector3(0f,18f,-21f); camera.transform.LookAt(Vector3.zero);
            camera.orthographic = true; camera.orthographicSize = 7.2f;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.16f,.19f,.22f);
            camera.nearClipPlane = .01f; camera.farClipPlane = 50f;
            var sun = new GameObject("Model preview light").AddComponent<Light>();
            sun.type=LightType.Directional;sun.intensity=1.65f;sun.transform.rotation=Quaternion.Euler(45,-30,0);
            RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.65f,.65f,.65f);
            var entries = new List<(GameObject visual,string label)>();
            foreach(string name in new[]{"Chicken","Steak","Tomato","Lettuce"})
            {
                var food = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Kitchen/Prefabs/Food/"+name+".prefab");
                var models = food.GetComponent<KitchenFoodModelVisual>();
                string title=name=="Chicken"?"Ham":name;
                entries.Add((models.rawVisual,title+" | Whole / Raw"));
                entries.Add((models.cookedVisual?models.cookedVisual:models.firstCutVisual,title+(models.cookedVisual?" | Cooked":" | First cut")));
                entries.Add((models.burntVisual?models.burntVisual:models.secondCutVisual,title+(models.burntVisual?" | Burnt":" | Second cut")));
            }
            var station=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Kitchen/Prefabs/Stations/PrepStation.prefab");
            entries.Add((station.GetComponentInChildren<KitchenKnife>(true).transform.Find("Restaurant Chef Knife").gameObject,"Chef Knife"));
            entries.Add((station.GetComponentInChildren<KitchenSaladBowl>(true).transform.Find("Restaurant mixing bowl").gameObject,"Mixing Bowl"));
            for(int i=0;i<entries.Count;i++)
            {
                var model=Object.Instantiate(entries[i].visual);model.SetActive(true);
                Bounds bounds=LocalBounds(model.transform);
                Bounds baseline=i<12?LocalBounds(entries[i/3*3].visual.transform):bounds;
                float scale=1.4f/Mathf.Max(baseline.size.x,baseline.size.y,baseline.size.z);
                model.transform.localScale=Vector3.one*scale;
                Vector3 cell=new Vector3((i%3-1)*3f,0,(2-i/3)*3.3f);
                model.transform.position=cell-new Vector3(bounds.center.x,bounds.min.y,bounds.center.z)*scale;
                var label=new GameObject("Preview label").AddComponent<TextMesh>();label.text=entries[i].label;
                label.fontSize=40;label.characterSize=.039f;label.anchor=TextAnchor.MiddleCenter;label.color=Color.white;
                label.transform.SetPositionAndRotation(cell+new Vector3(0,-.1f,-.9f),camera.transform.rotation);
            }
            KitchenBuildTools.Capture(camera,"Builds/Kitchen-RestaurantModels.png",1600,1750);
            Debug.Log("RESTAURANT_APPLIED_GALLERY_PASS");
        }
        public static void Apply()
        {
            // Target existing prefab contents only; never regenerate or save the user's room.
            palette = Palette();
            Directory.CreateDirectory(Root + "/Meshes");
            AuthorFood("Chicken", FoodKind.Chicken);
            AuthorFood("Steak", FoodKind.Steak);
            AuthorFood("Tomato", FoodKind.Tomato);
            AuthorFood("ChoppedTomato", FoodKind.Tomato);
            AuthorFood("Lettuce", FoodKind.Lettuce);
            AuthorFood("ChoppedLettuce", FoodKind.Lettuce);
            AuthorTools();
            ConfigurePan();
            AssetDatabase.SaveAssets();
            Debug.Log("RESTAURANT_PREFABS_APPLIED");
        }
        public static void ConfigurePan()
        {
            string materialPath="Assets/Kitchen/Materials/Pan Smoke.mat";
            Material smoke=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if(!smoke)
            {
                smoke=new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
                AssetDatabase.CreateAsset(smoke,materialPath);
            }
            smoke.SetFloat("_Surface",1f);smoke.SetFloat("_Blend",0f);smoke.SetFloat("_SrcBlend",5f);smoke.SetFloat("_DstBlend",10f);smoke.SetFloat("_ZWrite",0f);
            smoke.SetColor("_BaseColor",Color.white);smoke.SetOverrideTag("RenderType","Transparent");
            smoke.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");smoke.renderQueue=3000;EditorUtility.SetDirty(smoke);
            string path="Assets/Kitchen/Prefabs/Stations/InductionHob.prefab";
            GameObject root=PrefabUtility.LoadPrefabContents(path);
            try
            {
                KitchenPan pan=root.GetComponentInChildren<KitchenPan>(true);
                var grip=pan.GetComponent<KitchenPanGrip>();if(!grip)grip=pan.gameObject.AddComponent<KitchenPanGrip>();
                if(!pan.GetComponent<KitchenPanHeat>())pan.gameObject.AddComponent<KitchenPanHeat>();
                var visual=pan.GetComponent<KitchenPanHeatVisual>();if(!visual)visual=pan.gameObject.AddComponent<KitchenPanHeatVisual>();
                visual.smokeMaterialTemplate=smoke;
                var grab=new SerializedObject(pan.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>());
                var transformers=grab.FindProperty("m_StartingSingleGrabTransformers");
                if(transformers==null)throw new InvalidOperationException("Pan grab transformer list missing.");
                transformers.arraySize=1;transformers.GetArrayElementAtIndex(0).objectReferenceValue=grip;
                grab.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root,path);
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
            AssetDatabase.SaveAssets();
            Debug.Log("KITCHEN_HOT_PAN_AUTHORED");
        }

        static void AuthorFood(string name, FoodKind kind)
        {
            string path = "Assets/Kitchen/Prefabs/Food/" + name + ".prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var food = root.GetComponent<KitchenFood>();
                var box = root.GetComponent<BoxCollider>();
                if (name.StartsWith("Chopped"))
                {
                    var original = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Kitchen/Prefabs/Food/" + kind + ".prefab").GetComponent<BoxCollider>();
                    box.center = original.center; box.size = original.size;
                }
                Bounds target = new Bounds(box.center, box.size - Vector3.one * .004f);
                foreach (Transform child in root.transform.Cast<Transform>().ToArray()) Object.DestroyImmediate(child.gameObject);
                var model = root.GetComponent<KitchenFoodModelVisual>();
                if (!model) model = root.AddComponent<KitchenFoodModelVisual>();
                model.rawVisual = Group(root.transform, "Raw model");
                if (kind == FoodKind.Chicken)
                {
                    AddModel("food_ingredient_ham", model.rawVisual.transform);
                    model.cookedVisual = Group(root.transform, "Cooked model");
                    AddModel("food_ingredient_ham_cooked", model.cookedVisual.transform);
                    model.burntVisual = Group(root.transform, "Burnt model");
                    AddModel("food_ingredient_ham_trash", model.burntVisual.transform);
                }
                else if (kind == FoodKind.Steak)
                {
                    AddModel("food_ingredient_steak", model.rawVisual.transform);
                    model.cookedVisual = Group(root.transform, "Cooked model");
                    MeshNode("Seared steak", model.cookedVisual.transform, SearedSteak(), TintMaterial("Seared steak", new Color(.95f,.90f,.84f)));
                    model.burntVisual = Group(root.transform, "Burnt model");
                    AddModel("food_ingredient_steak", model.burntVisual.transform, TintMaterial("Burnt steak", new Color(.075f,.065f,.055f)));
                }
                else
                {
                    bool tomato = kind == FoodKind.Tomato;
                    AddModel(tomato ? "food_ingredient_tomato" : "food_ingredient_lettuce", model.rawVisual.transform);
                    model.firstCutVisual = Group(root.transform, "First cut model");
                    model.secondCutVisual = Group(root.transform, "Second cut model");
                    model.firstCutPieceCount = 2; model.secondCutPieceCount = 3;
                    if (tomato)
                    {
                        Mesh whole = MeshOf("food_ingredient_tomato");
                        float plane = whole.bounds.max.x - whole.bounds.size.x / 3f;
                        Mesh cut = TrimTomato(whole, plane);
                        cut = SaveMesh(cut, "Tomato two-thirds");
                        MeshNode("Tomato two-thirds", model.firstCutVisual.transform, cut, palette);
                        Transform slice = AddModel("food_ingredient_tomato_slice", model.firstCutVisual.transform);
                        slice.name = "Tomato slice";
                        slice.localRotation = Quaternion.identity;
                        slice.localPosition = new Vector3(plane + MeshOf("food_ingredient_tomato_slice").bounds.extents.x + .04f, 0f, 0f);
                        AddModel("food_ingredient_tomato_slices", model.secondCutVisual.transform);
                    }
                    else
                    {
                        AddModel("food_ingredient_lettuce_chopped", model.firstCutVisual.transform);
                        for (int i=0;i<3;i++)
                        {
                            Transform slice = AddModel("food_ingredient_lettuce_slice",model.secondCutVisual.transform);
                            slice.name = "Lettuce slice " + (i+1);
                            slice.localPosition = new Vector3((i-1)*.18f,i*.055f,(i==1?.12f:-.08f));
                            slice.localRotation = Quaternion.Euler(0f,(i-1)*32f,0f);
                        }
                    }
                }
                Fit(model.rawVisual.transform,target);
                foreach (GameObject group in new[] { model.cookedVisual, model.burntVisual, model.firstCutVisual, model.secondCutVisual })
                    if (group)
                    {
                        if (food.IsProduce) FitAtScale(group.transform,target,model.rawVisual.transform.localScale.x);
                        else Fit(group.transform,target);
                        group.SetActive(false);
                    }
                model.rawVisual.SetActive(true);
                if (food.IsProduce)
                {
                    model.useCutColliderBounds = true;
                    model.firstCutColliderBounds = GroupBoundsInParent(model.firstCutVisual.transform);
                    model.secondCutColliderBounds = GroupBoundsInParent(model.secondCutVisual.transform);
                }
                food.visualRenderers = model.rawVisual.GetComponentsInChildren<Renderer>();
                PrefabUtility.SaveAsPrefabAsset(root,path);
                Debug.Log("RESTAURANT_FOOD_AUTHORED " + name);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        static void AuthorTools()
        {
            string path = "Assets/Kitchen/Prefabs/Stations/PrepStation.prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                KitchenKnife knife = root.GetComponentInChildren<KitchenKnife>(true);
                foreach (Renderer r in knife.GetComponentsInChildren<Renderer>(true)) Object.DestroyImmediate(r);
                foreach (MeshFilter f in knife.GetComponentsInChildren<MeshFilter>(true)) Object.DestroyImmediate(f);
                Transform old = knife.transform.Find("Restaurant Chef Knife");
                if (old) Object.DestroyImmediate(old.gameObject);
                Mesh source = MeshOf("knife");
                Matrix4x4 matrix = Matrix4x4.TRS(new Vector3(-.17f,.025f,0f),Quaternion.Euler(0,0,-90),Vector3.one*.32f);
                Mesh baked = Object.Instantiate(source); baked.name = "Chef Knife aligned";
                baked.vertices = source.vertices.Select(matrix.MultiplyPoint3x4).ToArray();
                baked.normals = source.normals.Select(n => matrix.MultiplyVector(n).normalized).ToArray(); baked.RecalculateBounds();
                baked = SaveMesh(baked,"Chef Knife aligned");
                MeshNode("Restaurant Chef Knife",knife.transform,baked,palette);
                Bounds blade = PointBounds(source.vertices.Where(p=>p.y>=.145f).Select(matrix.MultiplyPoint3x4));
                Bounds handle = PointBounds(source.vertices.Where(p=>p.y<.145f).Select(matrix.MultiplyPoint3x4));
                knife.useExplicitBladeRegion = true; knife.bladeReference = knife.transform;
                knife.bladeLocalBounds = blade; knife.handleLocalCenter = handle.center;
                SetKnifeCollider(knife.transform.Find("Knife blade"),blade);
                Bounds handleCollision=handle;handleCollision.Encapsulate(new Vector3(blade.min.x,handle.center.y,handle.center.z));
                SetKnifeCollider(knife.transform.Find("Knife handle"),handleCollision);
                KitchenSaladBowl bowl = root.GetComponentInChildren<KitchenSaladBowl>(true);
                foreach (Transform child in bowl.transform.Cast<Transform>().ToArray())
                    if (child.GetComponent<MeshFilter>() && !child.GetComponent<Collider>()) Object.DestroyImmediate(child.gameObject);
                Transform visual = AddModel("bowl",bowl.transform);
                visual.name = "Restaurant mixing bowl";
                Fit(visual, new Bounds(new Vector3(0,.078f,0),new Vector3(.48f,.152f,.48f)));
                PrefabUtility.SaveAsPrefabAsset(root,path);
                Debug.Log("RESTAURANT_TOOLS_AUTHORED blade="+blade+" handle="+handle+" bowl="+LocalBounds(visual));
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        static void SetKnifeCollider(Transform shape, Bounds bounds)
        {
            shape.localPosition = Vector3.zero; shape.localRotation = Quaternion.identity; shape.localScale = Vector3.one;
            var collider = shape.GetComponent<BoxCollider>(); collider.center = bounds.center; collider.size = bounds.size;
        }
        static Bounds PointBounds(IEnumerable<Vector3> points)
        {
            var values = points.ToArray(); Bounds b = new Bounds(values[0],Vector3.zero);
            foreach (Vector3 p in values) b.Encapsulate(p); return b;
        }
        static GameObject Group(Transform parent,string name)
        { var group = new GameObject(name); group.transform.SetParent(parent,false); return group; }
        static Mesh MeshOf(string name) => AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/"+name+".fbx").GetComponentInChildren<MeshFilter>().sharedMesh;
        static Transform AddModel(string name, Transform parent, Material material = null) => MeshNode(name,parent,MeshOf(name),material ? material : palette);
        static Transform MeshNode(string name,Transform parent,Mesh mesh,Material material)
        {
            var node = Group(parent,name); node.AddComponent<MeshFilter>().sharedMesh=mesh;
            node.AddComponent<MeshRenderer>().sharedMaterial=material; return node.transform;
        }
        static void Fit(Transform visual,Bounds target)
        {
            Bounds source = LocalBounds(visual);
            float scale = Mathf.Min(target.size.x/source.size.x,target.size.y/source.size.y,target.size.z/source.size.z);
            visual.localScale = Vector3.one*scale;
            visual.localPosition = new Vector3(target.center.x, target.min.y, target.center.z)
                - new Vector3(source.center.x,source.min.y,source.center.z)*scale;
        }
        static void FitAtScale(Transform visual,Bounds target,float scale)
        {
            Bounds source=LocalBounds(visual);visual.localScale=Vector3.one*scale;
            visual.localPosition=new Vector3(target.center.x,target.min.y,target.center.z)
                -new Vector3(source.center.x,source.min.y,source.center.z)*scale;
        }
        static Bounds GroupBoundsInParent(Transform group)
        {
            Bounds b=LocalBounds(group);
            return new Bounds(group.localPosition+Vector3.Scale(b.center,group.localScale),Vector3.Scale(b.size,group.localScale)+Vector3.one*.004f);
        }
        static Mesh SearedSteak()
        {
            Mesh source=MeshOf("food_ingredient_steak");Mesh result=Object.Instantiate(source);result.name="Seared steak palette";
            var pixels=new Texture2D(2,2);pixels.LoadImage(File.ReadAllBytes(Root+"/restaurantbits_texture.png"));
            Vector2[] uv=source.uv;
            for(int i=0;i<uv.Length;i++)
            {
                Color color=pixels.GetPixelBilinear(uv[i].x,uv[i].y);
                // Move only the red meat into the atlas's roast-brown ramp. Bone/fat
                // keep their original UVs; the supplied model geometry is unchanged.
                if(color.r>color.g*1.25f && color.r>color.b*1.15f)
                    uv[i]=new Vector2(.85f,Mathf.Lerp(.79f,.96f,Mathf.InverseLerp(.25f,.85f,color.grayscale)));
            }
            Object.DestroyImmediate(pixels);result.uv=uv;return SaveMesh(result,"Seared steak palette");
        }
        static Material TintMaterial(string name,Color color)
        {
            string path=Root+"/Materials/"+name+".mat";
            Material material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material) { material=new Material(palette);AssetDatabase.CreateAsset(material,path); }
            material.CopyPropertiesFromMaterial(palette); material.SetColor("_BaseColor",color);EditorUtility.SetDirty(material);return material;
        }
        static Mesh SaveMesh(Mesh mesh,string name)
        {
            string path=Root+"/Meshes/"+name+".asset";
            Mesh existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(existing) { EditorUtility.CopySerialized(mesh,existing);Object.DestroyImmediate(mesh);return existing; }
            else AssetDatabase.CreateAsset(mesh,path);
            return mesh;
        }

        struct V
        {
            public Vector3 p,n; public Vector2 uv;
            public static V Lerp(V a,V b,float t)=>new V{p=Vector3.Lerp(a.p,b.p,t),n=Vector3.Lerp(a.n,b.n,t).normalized,uv=Vector2.Lerp(a.uv,b.uv,t)};
        }
        static Mesh TrimTomato(Mesh mesh,float plane)
        {
            Vector3[] p=mesh.vertices,n=mesh.normals;Vector2[] uv=mesh.uv;int[] triangles=mesh.triangles;
            var vertices=new List<Vector3>();var normals=new List<Vector3>();var uvs=new List<Vector2>();var indices=new List<int>();var edge=new List<Vector3>();
            void Add(V v){indices.Add(vertices.Count);vertices.Add(v.p);normals.Add(v.n);uvs.Add(v.uv);}
            for(int i=0;i<triangles.Length;i+=3)
            {
                var source=new V[3];for(int j=0;j<3;j++){int k=triangles[i+j];source[j]=new V{p=p[k],n=n[k],uv=uv[k]};}
                var clipped=new List<V>();
                for(int j=0;j<3;j++)
                {
                    V a=source[j],b=source[(j+1)%3];float da=plane-a.p.x,db=plane-b.p.x;bool ia=da>=0,ib=db>=0;
                    if(ia)clipped.Add(a);
                    if(ia!=ib){V cross=V.Lerp(a,b,da/(da-db));clipped.Add(cross);if(!edge.Any(q=>(q-cross.p).sqrMagnitude<.00000001f))edge.Add(cross.p);}
                }
                for(int j=1;j+1<clipped.Count;j++){Add(clipped[0]);Add(clipped[j]);Add(clipped[j+1]);}
            }
            Vector3 center=Vector3.zero;foreach(Vector3 point in edge)center+=point;center/=edge.Count;
            edge.Sort((a,b)=>Mathf.Atan2(a.z-center.z,a.y-center.y).CompareTo(Mathf.Atan2(b.z-center.z,b.y-center.y)));
            V Cap(Vector3 point)=>new V{p=point,n=Vector3.right,uv=new Vector2(.805f,.40f)};
            for(int i=0;i<edge.Count;i++){Vector3 a=edge[i],b=edge[(i+1)%edge.Count];if(Vector3.Cross(a-center,b-center).x<0){Vector3 t=a;a=b;b=t;}Add(Cap(center));Add(Cap(a));Add(Cap(b));}
            var result=new Mesh{name="Tomato two-thirds"};result.SetVertices(vertices);result.SetNormals(normals);result.SetUVs(0,uvs);result.SetTriangles(indices,0);result.RecalculateBounds();return result;
        }
        public static void InspectGallery()
        {
            Inspect();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            palette = Palette();
            var camera = new GameObject("Asset inspection camera").AddComponent<Camera>();
            camera.transform.SetPositionAndRotation(new Vector3(0f, 8.8f, -11f), Quaternion.Euler(37f, 0f, 0f));
            camera.orthographic = true; camera.orthographicSize = 3.9f;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.16f,.19f,.22f);
            camera.nearClipPlane = .01f; camera.farClipPlane = 50f;
            var sun = new GameObject("Preview light").AddComponent<Light>();
            sun.type = LightType.Directional; sun.intensity = 1.8f; sun.transform.rotation = Quaternion.Euler(45f,-30f,0f);
            RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = new Color(.65f,.65f,.65f);
            string[] paths = Directory.GetFiles(Root, "*.fbx").OrderBy(p => p).ToArray();
            for (int i = 0; i < paths.Length; i++)
            {
                var model = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(paths[i]));
                foreach (Renderer r in model.GetComponentsInChildren<Renderer>()) r.sharedMaterial = palette;
                Bounds bounds = LocalBounds(model.transform);
                float scale = 1.3f / Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
                model.transform.localScale = Vector3.one * scale;
                Vector3 cell = new Vector3((i % 4 - 1.5f) * 2.1f, 0f, (i / 4 - 1f) * 2.25f);
                model.transform.position = cell - new Vector3(bounds.center.x, bounds.min.y, bounds.center.z) * scale;
                var label = new GameObject("Asset label").AddComponent<TextMesh>();
                label.text = Path.GetFileNameWithoutExtension(paths[i]).Replace("food_ingredient_", "");
                label.fontSize = 36; label.characterSize = .046f; label.anchor = TextAnchor.MiddleCenter; label.color = Color.white;
                label.transform.SetPositionAndRotation(cell + new Vector3(0f, -.14f, -.85f), camera.transform.rotation);
                if (paths[i].EndsWith("knife.fbx"))
                {
                    var mesh = model.GetComponentInChildren<MeshFilter>().sharedMesh;
                    File.WriteAllLines("Logs/RestaurantKnifeVertices.csv", mesh.vertices.Select((p,k)=>p.x+","+p.y+","+p.z+","+mesh.uv[k].x+","+mesh.uv[k].y));
                }
            }
            KitchenBuildTools.Capture(camera,"Builds/Restaurant-SourceModels.png",1800,1300);
            var station = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Kitchen/Prefabs/Stations/PrepStation.prefab");
            foreach (var collider in station.GetComponentsInChildren<Collider>())
                Debug.Log("RESTAURANT_STATION_COLLIDER " + collider.name + " " + collider.GetType().Name + " pos=" + collider.transform.localPosition + " scale=" + collider.transform.localScale);
            Debug.Log("RESTAURANT_GALLERY_PASS");
        }

        static Material Palette()
        {
            Directory.CreateDirectory(Root + "/Materials");
            string path = Root + "/Materials/Restaurant Palette.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material) { material = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(material,path); }
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/restaurantbits_texture.png"));
            material.SetColor("_BaseColor", Color.white); material.SetFloat("_Smoothness",.22f); material.SetFloat("_Metallic",0f);
            EditorUtility.SetDirty(material); AssetDatabase.SaveAssets(); return material;
        }

        static Bounds LocalBounds(Transform root)
        {
            bool first = true; Bounds result = default;
            foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                Bounds b = filter.sharedMesh.bounds; Matrix4x4 matrix = root.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                for (int i=0;i<8;i++)
                {
                    Vector3 p = matrix.MultiplyPoint3x4(b.center + Vector3.Scale(b.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1)));
                    if (first) {result = new Bounds(p,Vector3.zero);first=false;} else result.Encapsulate(p);
                }
            }
            return result;
        }
        public static void Inspect()
        {
            foreach (string path in Directory.GetFiles(Root, "*.fbx"))
            {
                var importer = (ModelImporter)AssetImporter.GetAtPath(path);
                importer.isReadable = true;
                importer.importAnimation = false;
                importer.SaveAndReimport();
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var instance = Object.Instantiate(model);
                Debug.Log("RESTAURANT_MODEL " + Path.GetFileName(path));
                foreach (MeshFilter filter in instance.GetComponentsInChildren<MeshFilter>())
                {
                    Mesh mesh = filter.sharedMesh;
                    Debug.Log("RESTAURANT_MESH " + filter.name + " verts=" + mesh.vertexCount + " submeshes=" + mesh.subMeshCount
                        + " colors=" + mesh.colors.Length + " localBounds=" + mesh.bounds + " worldBounds=" + filter.GetComponent<Renderer>().bounds
                        + " rotation=" + filter.transform.rotation.eulerAngles + " scale=" + filter.transform.lossyScale);
                    foreach (Material material in filter.GetComponent<Renderer>().sharedMaterials)
                        Debug.Log("RESTAURANT_MATERIAL " + material.name + " shader=" + material.shader.name + " color=" + material.color
                            + " texture=" + material.mainTexture);
                }
                Object.DestroyImmediate(instance);
            }
            Debug.Log("RESTAURANT_INSPECTION_PASS");
        }
    }
}
