using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HouseSim.Gym
{
    public sealed class ClaimRewardButton : MonoBehaviour
    {
        [SerializeField] string questId;
        [SerializeField] QuestProgress questProgress;
        [SerializeField] PlayerResources playerResources;
        [SerializeField] AudioSource audioSource;
        [SerializeField] AudioClip clickSound;
        [SerializeField] AudioClip rewardSound;
        [SerializeField] Button button;
        [SerializeField] TMP_Text questLabel;
        [SerializeField] TMP_Text statusLabel;

        void Awake()
        {
            if (questProgress == null) questProgress = FindAnyObjectByType<QuestProgress>();
            if (playerResources == null) playerResources = FindAnyObjectByType<PlayerResources>();
            if (button == null) button = GetComponent<Button>();
        }

        void OnEnable()
        {
            if (questProgress != null) questProgress.Changed += Refresh;
            Refresh();
        }

        void OnDisable()
        {
            if (questProgress != null) questProgress.Changed -= Refresh;
        }

        public void Claim()
        {
            if (questProgress == null) questProgress = FindAnyObjectByType<QuestProgress>();
            if (playerResources == null) playerResources = FindAnyObjectByType<PlayerResources>();

            if (questProgress != null && playerResources != null && questProgress.IsComplete(questId))
            {
                int currentReward = CurrentReward();
                if (!questProgress.ClaimAndAdvance(questId)) { Play(clickSound); return; }
                playerResources.Add("gymCoins", currentReward);
                Play(rewardSound);
                Refresh();
                return;
            }

            Play(clickSound);
        }

        void Refresh()
        {
            if (questProgress == null) return;

            int progress = questProgress.GetProgress(questId);
            int target = questProgress.GetTarget(questId);
            int currentReward = CurrentReward();
            bool finished = questProgress.IsFinished(questId);
            if (button != null) button.interactable = !finished;
            if (questLabel != null)
            {
                questLabel.text = questProgress.GetQuestTitle(questId) + "  •  " + questProgress.GetDifficultyName(questId);
            }
            if (statusLabel != null)
            {
                if (finished) statusLabel.text = "COMPLETE";
                else if (progress >= target) statusLabel.text = $"CLAIM  {currentReward} COINS";
                else statusLabel.text = $"{progress}/{target}  •  {currentReward} COINS";
            }
        }

        int CurrentReward()
        {
            return questProgress != null ? questProgress.GetRewardCoins(questId) : 0;
        }

        void Play(AudioClip clip)
        {
            if (audioSource != null && clip != null) audioSource.PlayOneShot(clip);
        }
    }
}
