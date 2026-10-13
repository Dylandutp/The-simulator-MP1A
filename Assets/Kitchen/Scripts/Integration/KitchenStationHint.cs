using UnityEngine;

namespace HouseSim.Kitchen
{
    public class KitchenStationHint : MonoBehaviour
    {
        public string id;
        [TextArea] public string message;
        public float distance = 1.35f;
        bool shown;
        void Update()
        {
            if (shown || KitchenSession.Instance == null || Camera.main == null) return;
            if (Vector3.Distance(Camera.main.transform.position, transform.position) > distance) return;
            shown = true;
            KitchenSession.Instance.Tutorial(id, message, transform.position + Vector3.up * .2f);
        }
    }
}
