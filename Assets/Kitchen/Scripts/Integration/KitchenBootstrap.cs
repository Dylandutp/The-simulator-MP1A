using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

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
            SceneManager.SetActiveScene(SceneManager.GetSceneByName(kitchenScene));
        }
    }
}
