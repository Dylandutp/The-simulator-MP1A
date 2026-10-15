using UnityEngine;
using UnityEngine.UI;

namespace HouseSim.Gym
{
    /// <summary>Shared open, close, and sound behavior for every gym popup.</summary>
    public sealed class PopupPanel : MonoBehaviour
    {
        [SerializeField] GameObject panelRoot;
        [SerializeField] Button closeButton;
        [SerializeField] AudioSource openSoundSource;
        [SerializeField] AudioSource closeSoundSource;
        [SerializeField] bool startsOpen;
        [SerializeField] bool oneTimeOnly;

        bool hasOpened;

        public bool IsOpen { get; private set; }

        void Awake()
        {
            if (panelRoot == null) panelRoot = gameObject;
            IsOpen = startsOpen;
            hasOpened = startsOpen;
            panelRoot.SetActive(startsOpen);
        }

        void OnEnable()
        {
            if (closeButton == null) return;
            closeButton.onClick.RemoveListener(Close);
            closeButton.onClick.AddListener(Close);
        }

        void OnDisable()
        {
            if (closeButton != null) closeButton.onClick.RemoveListener(Close);
        }

        public void Open() => OpenInternal(true);

        public void OpenSilently() => OpenInternal(false);

        void OpenInternal(bool playSound)
        {
            if (oneTimeOnly && hasOpened) return;

            if (!gameObject.activeSelf) gameObject.SetActive(true);
            PositionInFrontOfPlayer();
            panelRoot.SetActive(true);
            IsOpen = true;
            hasOpened = true;
            if (playSound) Play(openSoundSource);
        }

        public void Close()
        {
            if (!IsOpen) return;

            Play(closeSoundSource);
            IsOpen = false;
            panelRoot.SetActive(false);
        }

        public void Toggle()
        {
            if (IsOpen) Close();
            else Open();
        }

        static void Play(AudioSource source)
        {
            if (source != null && source.clip != null) source.Play();
        }

        void PositionInFrontOfPlayer()
        {
            Camera camera = Camera.main;
            if (camera == null) return;

            Vector3 forward = Vector3.ProjectOnPlane(camera.transform.forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < .01f) forward = camera.transform.forward.normalized;
            transform.position = camera.transform.position + forward * 1.25f;
            transform.rotation = Quaternion.LookRotation(transform.position - camera.transform.position, Vector3.up);

            Canvas canvas = GetComponent<Canvas>();
            if (canvas == null) return;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 100;
        }
    }
}
