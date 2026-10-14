using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.XR.CoreUtils;

namespace HouseSim.Kitchen
{
    /// <summary>The individual APK loads the team's master plus this additive room.</summary>
    public class KitchenBootstrap : MonoBehaviour
    {
        public string masterScene = "MasterScene";
        public string kitchenScene = "Kitchen";
        IEnumerator Start()
        {
            if (!SceneManager.GetSceneByName(masterScene).isLoaded)
                yield return SceneManager.LoadSceneAsync(masterScene, LoadSceneMode.Additive);
            if (!SceneManager.GetSceneByName(kitchenScene).isLoaded)
                yield return SceneManager.LoadSceneAsync(kitchenScene, LoadSceneMode.Additive);
            var room = SceneManager.GetSceneByName(kitchenScene);
            SceneManager.SetActiveScene(room);
            // Only the standalone entry owns this initial teleport. A house loading
            // the room additively leaves its existing player's position unchanged.
            var origin = FindFirstObjectByType<XROrigin>();
            foreach (var root in room.GetRootGameObjects())
            {
                var setup = root.GetComponentInChildren<KitchenRuntimeSetup>();
                if (!setup || !setup.spawn || !origin) continue;
                origin.transform.SetPositionAndRotation(setup.spawn.position, setup.spawn.rotation);
                setup.GetComponent<KitchenRoomScope>()?.RefreshPresence();
                break;
            }
        }
    }
}
