using UnityEngine;
using UnityEngine.UI;

namespace OfficeSim
{
    public sealed class OfficeButtonAudio : MonoBehaviour
    {
        void Awake()
        {
            GetComponent<Button>().onClick.AddListener(PlayClick);
        }

        void PlayClick()
        {
            OfficeGame.Instance.PlayButtonClick();
        }
    }
}
