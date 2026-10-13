using UnityEngine;

namespace HouseSim.Kitchen
{
    /// <summary>Lets cooking appliances distinguish desktop-held utensils from resting ones.</summary>
    [DisallowMultipleComponent]
    public sealed class KitchenDesktopHeld : MonoBehaviour
    {
        public bool IsHeld { get; private set; }
        public void SetHeld(bool value) => IsHeld = value;

        public static bool IsHeldByDesktop(GameObject target)
        {
            if (target == null) return false;
            var held = target.GetComponentInParent<KitchenDesktopHeld>();
            return held != null && held.IsHeld;
        }
    }
}
