using UnityEngine;

namespace OfficeSim
{
    // Only purchased automatic prefabs contain this clock. Manual desks have no timer.
    public sealed class AutomaticWorkstation : MonoBehaviour
    {
        private Workstation station;

        void Awake()
        {
            station = GetComponent<Workstation>();
        }

        void Update()
        {
            station.TickAutomatic(Time.deltaTime);
        }
    }
}
