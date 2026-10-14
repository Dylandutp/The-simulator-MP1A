using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR;
using Unity.XR.CoreUtils;
using UnityEngine.XR.Interaction.Toolkit.Samples.StarterAssets;

namespace HouseSim.Kitchen
{
    public class KitchenRuntimeSetup : MonoBehaviour
    {
        public Transform spawn;
        public KitchenSession session;
        IEnumerator Start()
        {
            if (FindFirstObjectByType<XROrigin>() == null)
            {
                if (!SceneManager.GetSceneByName("MasterScene").isLoaded)
                    yield return SceneManager.LoadSceneAsync("MasterScene", LoadSceneMode.Additive);
            }
            var origin = FindFirstObjectByType<XROrigin>();
            if (origin == null) { Debug.LogError("Kitchen requires the shared MasterScene XR Origin."); yield break; }
            session.AttachInventoryToMasterScene();
            if (spawn != null) origin.transform.SetPositionAndRotation(spawn.position, spawn.rotation);
            Camera cam = origin.Camera != null ? origin.Camera : Camera.main;
            if (cam == null) yield break;
            session.mouthAnchor = cam.transform;
            var controllerControls = gameObject.GetComponent<KitchenControllerControls>();
            if (!controllerControls) controllerControls = gameObject.AddComponent<KitchenControllerControls>();
            controllerControls.Configure(origin);
            // The room owns its lighting; avoid doubling the original template sun.
            foreach (var light in origin.gameObject.scene.GetRootGameObjects())
                if (light.TryGetComponent<Light>(out var sun) && sun.type == LightType.Directional) sun.enabled = false;
            if (!XRSettings.isDeviceActive && Application.platform != RuntimePlatform.Android)
            {
                foreach (var behaviour in origin.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    string type = behaviour.GetType().Name;
                    if (type == "TrackedPoseDriver" || type.Contains("MoveProvider") || type.Contains("TurnProvider")) behaviour.enabled = false;
                }
                if (cam.transform.parent != null) cam.transform.parent.localPosition = Vector3.zero;
                cam.transform.localPosition = new Vector3(0, KitchenPlayerHeight.InitialEyeHeight, 0);
                cam.transform.localRotation = Quaternion.identity;
                var desktop = origin.gameObject.GetComponent<KitchenDesktopController>();
                if (desktop == null) desktop = origin.gameObject.AddComponent<KitchenDesktopController>();
                desktop.viewCamera = cam;
                desktop.playerRoot = origin.transform;
                desktop.showHelp = false;
            }
            else
            {
                var height = gameObject.GetComponent<KitchenPlayerHeight>();
                if (!height) height = gameObject.AddComponent<KitchenPlayerHeight>();
                height.Configure(origin);
            }
            var hud = gameObject.GetComponent<KitchenEnergyHUD>();
            if (!hud) hud = gameObject.AddComponent<KitchenEnergyHUD>();
            hud.session = session;
            hud.viewCamera = cam;
            var context = gameObject.GetComponent<KitchenContextHUD>();
            if (!context) context = gameObject.AddComponent<KitchenContextHUD>();
            context.viewCamera = cam;
            context.playerRoot = origin.transform;
            context.desktop = origin.GetComponent<KitchenDesktopController>();
            var recovery = gameObject.GetComponent<KitchenPlayerRecovery>();
            if (!recovery) recovery = gameObject.AddComponent<KitchenPlayerRecovery>();
            recovery.origin = origin;
            recovery.spawn = spawn;
            Application.targetFrameRate = 72;
            Application.runInBackground = true;
        }
    }
}
