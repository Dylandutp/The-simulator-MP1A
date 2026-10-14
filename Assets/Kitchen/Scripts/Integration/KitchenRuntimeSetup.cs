using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.XR.CoreUtils;

namespace HouseSim.Kitchen
{
    public class KitchenRuntimeSetup : MonoBehaviour
    {
        public Transform spawn;
        public KitchenSession session;
        IEnumerator Start()
        {
            bool createdPlayer = FindFirstObjectByType<XROrigin>() == null;
            if (createdPlayer)
            {
                if (!SceneManager.GetSceneByName("MasterScene").isLoaded)
                    yield return SceneManager.LoadSceneAsync("MasterScene", LoadSceneMode.Additive);
            }
            var origin = FindFirstObjectByType<XROrigin>();
            if (origin == null) { Debug.LogError("Kitchen requires the shared MasterScene XR Origin."); yield break; }
            session.AttachInventoryToMasterScene();
            // The standalone entry starts in this room. Loading Kitchen into an
            // existing house must not pull the player away from another room.
            if (spawn != null && createdPlayer)
                origin.transform.SetPositionAndRotation(spawn.position, spawn.rotation);
            Camera cam = origin.Camera != null ? origin.Camera : Camera.main;
            if (cam == null) yield break;
            session.mouthAnchor = cam.transform;
            var controllerControls = gameObject.GetComponent<KitchenControllerControls>();
            if (!controllerControls) controllerControls = gameObject.AddComponent<KitchenControllerControls>();
            // The room owns its lighting; avoid doubling the original template sun.
            foreach (var light in origin.gameObject.scene.GetRootGameObjects())
                if (light.TryGetComponent<Light>(out var sun) && sun.type == LightType.Directional) sun.enabled = false;
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
            var scope = gameObject.GetComponent<KitchenRoomScope>();
            if (!scope) scope = gameObject.AddComponent<KitchenRoomScope>();
            BoxCollider roomFloor = null;
            foreach (var collider in GetComponentsInChildren<BoxCollider>(true))
                if (collider.name == "Kitchen floor") { roomFloor = collider; break; }
            scope.Configure(origin, roomFloor, controllerControls,
                Application.platform != RuntimePlatform.Android);
            Application.targetFrameRate = 72;
            Application.runInBackground = true;
        }
    }
}
