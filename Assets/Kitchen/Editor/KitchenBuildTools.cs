using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HouseSim.Kitchen.Editor
{
    public static class KitchenBuildTools
    {
        static readonly string[] Scenes={"Assets/Kitchen/Scenes/KitchenEntry.unity","Assets/Scenes/MasterScene.unity","Assets/Kitchen/Scenes/Kitchen.unity"};
        public static void ValidateEditor()
        {
            // A saved scene may contain intentional manual layout edits.
            if (!File.Exists(Scenes[2])) KitchenSceneBuilder.Build();
            KitchenFoodChecks.Run();
            Preview();
        }
        public static void ValidateAndPlay()
        {
            ValidateEditor();
            KitchenPlayChecks.Run();
        }
        [MenuItem("Kitchen/Build Windows Preview")]
        public static void BuildWindows()
        {
            Directory.CreateDirectory("Builds/Windows");
            BuildReport report=BuildPipeline.BuildPlayer(new BuildPlayerOptions {scenes=Scenes,locationPathName="Builds/Windows/Kitchen.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.None});
            Finish(report,"WINDOWS");
        }
        [MenuItem("Kitchen/Build Quest APK")]
        public static void BuildAndroid()
        {
            Directory.CreateDirectory("Builds/Quest");
            ConfigureQuestRendering();
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android,ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures=AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion=AndroidSdkVersions.AndroidApiLevel29;
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android,new[]{GraphicsDeviceType.Vulkan});
            PlayerSettings.Android.optimizedFramePacing=true;
            PlayerSettings.defaultInterfaceOrientation=UIOrientation.LandscapeLeft;
            EditorUserBuildSettings.buildAppBundle=false;
            BuildReport report=BuildPipeline.BuildPlayer(new BuildPlayerOptions {scenes=Scenes,locationPathName="Builds/Quest/Kitchen.apk",target=BuildTarget.Android,options=BuildOptions.None});
            Finish(report,"QUEST");
        }
        static void ConfigureQuestRendering()
        {
            var pipeline=AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/Mobile_RPAsset.asset");
            if(pipeline==null)throw new Exception("Mobile rendering asset is missing.");
            pipeline.msaaSampleCount=4;
            pipeline.renderScale=1f;
            pipeline.supportsHDR=false;
            pipeline.shadowDistance=12f;
            pipeline.supportsCameraDepthTexture=false;
            pipeline.supportsCameraOpaqueTexture=false;
            EditorUtility.SetDirty(pipeline);AssetDatabase.SaveAssets();
        }
        static void Finish(BuildReport report,string target)
        {
            Debug.Log("KITCHEN_"+target+"_BUILD_"+report.summary.result+" | errors="+report.summary.totalErrors+" | "+report.summary.totalSize+" bytes");
            if(report.summary.result!=BuildResult.Succeeded) throw new Exception(target+" build failed");
        }
        [MenuItem("Kitchen/Render Scene Preview")]
        public static void Preview()
        {
            EditorSceneManager.OpenScene("Assets/Kitchen/Scenes/Kitchen.unity");
            var go=new GameObject("Preview camera");var cam=go.AddComponent<Camera>();
            cam.transform.position=new Vector3(5.15f,3.15f,-4.35f);cam.transform.LookAt(new Vector3(-.3f,1.2f,1.8f));
            cam.fieldOfView=65;cam.nearClipPlane=.03f;cam.farClipPlane=50;cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.15f,.18f,.17f);
            Capture(cam,"Builds/Kitchen-Preview.png",1600,1100);
            cam.transform.position=new Vector3(.25f,1.65f,-2.4f);cam.transform.LookAt(new Vector3(0,1.15f,3.8f));
            Capture(cam,"Builds/Kitchen-PlayerView.png",1600,1000);
            cam.transform.position=new Vector3(4.5f,1.98f,2.35f);cam.transform.LookAt(new Vector3(4.5f,1.98f,4.78f));
            Capture(cam,"Builds/Kitchen-Recipes.png",1400,900);
            UnityEngine.Object.DestroyImmediate(go);
            Debug.Log("KITCHEN_PREVIEW_PASS");
        }
        public static void Capture(Camera camera,string path,int width,int height)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var rt=new RenderTexture(width,height,24,RenderTextureFormat.ARGB32);var previous=camera.targetTexture;var active=RenderTexture.active;
            camera.targetTexture=rt;
            for(int pass=0;pass<3;pass++)
            {
                camera.Render();
                foreach(var fit in UnityEngine.Object.FindObjectsByType<KitchenTextFit>(FindObjectsSortMode.None))fit.FitNow();
            }
            camera.Render();RenderTexture.active=rt;
            var image=new Texture2D(width,height,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,width,height),0,0);image.Apply();
            File.WriteAllBytes(path,image.EncodeToPNG());camera.targetTexture=previous;RenderTexture.active=active;
            UnityEngine.Object.DestroyImmediate(image);UnityEngine.Object.DestroyImmediate(rt);
        }
    }
}
