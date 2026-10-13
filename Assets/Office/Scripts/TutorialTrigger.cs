using UnityEngine;

namespace OfficeSim
{
    public sealed class TutorialTrigger : MonoBehaviour
    {
        public string tutorialId;
        public GameObject panel;
        public float encounterDistance = 3.5f;

        void Update()
        {
            Vector3 offset = Camera.main.transform.position - transform.position;
            offset.y = 0;
            if (offset.sqrMagnitude < encounterDistance * encounterDistance)
            {
                OfficeGame.Instance.ShowTutorial(this);
            }
        }

        public void ShowHelp()
        {
            OfficeGame.Instance.ShowTutorial(this, true);
        }

        public void Dismiss()
        {
            panel.SetActive(false);
        }
    }
}
