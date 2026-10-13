using UnityEngine;

namespace OfficeSim
{
    public sealed class MachineMotion : MonoBehaviour
    {
        public Workstation station;
        public Transform movingPart;
        public GameObject inputDocument;
        private Vector3 restPosition;
        private Quaternion restRotation;

        void Start()
        {
            restPosition = movingPart.localPosition;
            restRotation = movingPart.localRotation;
        }

        void LateUpdate()
        {
            if (station.state.kind == MachineKind.Stamper)
            {
                inputDocument.SetActive(station.state.input > 0 || station.state.active);
            }

            float progress = station.state.active ? station.state.progress : 0;
            movingPart.localPosition = restPosition;
            movingPart.localRotation = restRotation;
            if (station.state.kind == MachineKind.Writer)
            {
                movingPart.gameObject.SetActive(station.state.active);
                movingPart.localPosition += Vector3.back * (progress * .37f);
            }
            else if (station.state.kind == MachineKind.Stamper)
            {
                movingPart.localPosition += Vector3.down * (Mathf.Sin(progress * Mathf.PI) * .11f);
            }
            else
            {
                movingPart.localRotation = restRotation * Quaternion.Euler(0, Mathf.Sin(progress * Mathf.PI * 2) * 25, 0);
            }
        }
    }
}
