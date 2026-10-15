using UnityEngine;
using UnityEngine.UI;

namespace HouseSim.Gym
{
    public sealed class ButtonClickSound : MonoBehaviour
    {
        [SerializeField] AudioSource audioSource;
        [SerializeField] AudioClip clickSound;

        Button button;

        void Awake() => button = GetComponent<Button>();

        void OnEnable()
        {
            if (button == null) button = GetComponent<Button>();
            if (button != null) button.onClick.AddListener(Play);
        }

        void OnDisable()
        {
            if (button != null) button.onClick.RemoveListener(Play);
        }

        public void Play()
        {
            if (audioSource != null && clickSound != null) audioSource.PlayOneShot(clickSound);
        }
    }
}
