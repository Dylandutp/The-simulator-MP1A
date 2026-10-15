using UnityEngine;

namespace HouseSim.Kitchen
{
    public class KitchenStationHint : MonoBehaviour
    {
        public string id;
        [TextArea] public string message;
        public float distance = 1.35f;
        // Legacy authoring metadata only. Pointed-item HUD replaces proximity popups.
    }
}
