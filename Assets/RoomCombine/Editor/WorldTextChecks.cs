using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using HouseSim.RoomCombine;
using Object=UnityEngine.Object;

public static class WorldTextChecks
{
    public static void RunAll() { Run(); HouseSim.Kitchen.Editor.KitchenPlayChecks.Run(); }
    [MenuItem("Tools/Room Combine/Check Text Occlusion")]
    public static void Run()
    {
        var scene=EditorSceneManager.NewPreviewScene();
        var previous=RenderTexture.active;
        RenderTexture rt=null; Texture2D pixels=null; Material black=null;
        try
        {
            var go=new GameObject("Occlusion test camera");SceneManager.MoveGameObjectToScene(go,scene);
            var camera=go.AddComponent<Camera>();camera.scene=scene;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;camera.orthographic=true;camera.orthographicSize=1;
            rt=new RenderTexture(256,256,24);camera.targetTexture=rt;pixels=new Texture2D(256,256,TextureFormat.RGB24,false);
            var label=new GameObject("Test text");SceneManager.MoveGameObjectToScene(label,scene);label.transform.position=new Vector3(0,0,2);
            var text=label.AddComponent<TextMesh>();text.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");text.text="VISIBLE";text.anchor=TextAnchor.MiddleCenter;text.characterSize=.08f;text.fontSize=64;text.color=Color.white;
            text.font.RequestCharactersInTexture(text.text,text.fontSize);label.GetComponent<Renderer>().sharedMaterial=text.font.material;
            var world=label.AddComponent<WorldText>();world.SendMessage("Awake");
            int Capture(string name)
            {
                camera.Render();RenderTexture.active=rt;pixels.ReadPixels(new Rect(0,0,256,256),0,0);pixels.Apply();
                File.WriteAllBytes("Logs/WorldText-"+name+".png",pixels.EncodeToPNG());
                int count=0;foreach(var p in pixels.GetPixels32())if(p.r>80 && p.g>80 && p.b>80)count++;return count;
            }
            int visible=Capture("visible");
            var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);SceneManager.MoveGameObjectToScene(wall,scene);wall.transform.position=new Vector3(0,0,1);wall.transform.localScale=new Vector3(3,3,.1f);
            black=new Material(Shader.Find("Universal Render Pipeline/Unlit"));black.color=Color.black;wall.GetComponent<Renderer>().sharedMaterial=black;
            int occluded=Capture("occluded");
            wall.SetActive(false);label.transform.rotation=Quaternion.Euler(0,180,0);
            int back=Capture("back");
            string result=$"visible={visible}; behind-wall={occluded}; back-face={back}";
            if(visible<100 || occluded>5 || back>5)throw new Exception("WORLD_TEXT_CHECK_FAIL "+result);
            File.WriteAllText("Logs/WorldTextCheck.result","WORLD_TEXT_CHECK_PASS "+result);
            Debug.Log("WORLD_TEXT_CHECK_PASS "+result);
        }
        finally
        {
            RenderTexture.active=previous;EditorSceneManager.ClosePreviewScene(scene);
            if(rt){rt.Release();Object.DestroyImmediate(rt);}if(pixels)Object.DestroyImmediate(pixels);if(black)Object.DestroyImmediate(black);
        }
    }
}
