using UnityEngine;
using System.Collections;

namespace HouseSim.Gym
{
    public class TutorialPopup : MonoBehaviour
    {
        public GameObject textCanvas;
        private bool hasTriggered;

        private void Start()
        {
            if (textCanvas == null)
            {
                Debug.LogError("Tutorial popup is missing its text canvas.", this);
                return;
            }

            if (textCanvas != gameObject) textCanvas.SetActive(false);
        }

        private void OnTriggerEnter(Collider other)
        {
            bool isPlayer = other.CompareTag("Player") || other.CompareTag("MainCamera") ||
                            other.GetComponentInParent<Camera>() != null;
            if (!hasTriggered && isPlayer)
            {
                if (textCanvas == null)
                {
                    Debug.LogError("Tutorial popup is missing its text canvas.", this);
                    return;
                }

                hasTriggered = true;
                textCanvas.SetActive(true);
                StartCoroutine(HidePopupAndDestroy());
            }
        }

        private IEnumerator HidePopupAndDestroy()
        {
            yield return new WaitForSeconds(10f);
            if (textCanvas != gameObject) textCanvas.SetActive(false);
            Destroy(gameObject);
        }
    }
}