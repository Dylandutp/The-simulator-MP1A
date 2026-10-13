using UnityEngine;

namespace OfficeSim
{
    public sealed class WorkstationVisuals : MonoBehaviour
    {
        public Workstation station;
        public GameObject[] inputSheets;
        public GameObject[] folderStack;
        public GameObject inkBottle;
        public GameObject stampPad;
        public GameObject readyDocument;
        public GameObject[] writingLines;
        public GameObject approvalMark;

        void LateUpdate()
        {
            Refresh();
        }

        public void Refresh()
        {
            var state = station.state;
            SetCount(inputSheets, state.input);
            if (state.kind == MachineKind.Filer)
            {
                SetCount(folderStack, state.folders);
            }
            else if (state.kind == MachineKind.Stamper)
            {
                stampPad.SetActive(state.stampPads > 0 || state.active);
            }
            else
            {
                inkBottle.SetActive(state.ink > 0 || state.active);
            }

            if (station.IsAutomatic)
            {
                station.workArea.gameObject.SetActive(false);
                return;
            }

            bool ready = !state.moving && state.outputCount < OfficeRules.Capacity && (state.HasSupplies || state.active);
            readyDocument.SetActive(ready);
            station.workArea.gameObject.SetActive(ready);

            if (state.kind == MachineKind.Writer)
            {
                SetCount(writingLines, Mathf.FloorToInt(state.progress * writingLines.Length));
            }

            if (state.kind == MachineKind.Stamper)
            {
                approvalMark.SetActive(state.progress >= .5f);
            }
        }

        static void SetCount(GameObject[] objects, int count)
        {
            for (int i = 0; i < objects.Length; i++)
            {
                objects[i].SetActive(i < count);
            }
        }
    }
}
