using UnityEngine;

namespace HouseSim.Kitchen
{
    public sealed class KitchenGridCell : MonoBehaviour
    {
        public KitchenGridPlacement grid;
        public int index;
        public void Place()
        {
            if (grid != null) grid.Place(index);
        }
    }
}
