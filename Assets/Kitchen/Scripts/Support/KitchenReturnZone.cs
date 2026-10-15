using UnityEngine;

namespace HouseSim.Kitchen
{
    [RequireComponent(typeof(Collider))]
    public sealed class KitchenReturnZone : MonoBehaviour
    {
        void Reset() => GetComponent<Collider>().isTrigger = true;
        void OnTriggerEnter(Collider other)
        {
            var tool = other.GetComponentInParent<KitchenRespawnTool>();
            if (tool != null) tool.ReturnHome();
        }
    }
}
