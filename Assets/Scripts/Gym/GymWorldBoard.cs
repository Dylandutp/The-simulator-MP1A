using System.Text;
using UnityEngine;

namespace HouseSim.Gym
{
    public class GymWorldBoard : MonoBehaviour
    {
        public enum BoardType
        {
            Chalkboard,
            LockerClipboard
        }

        public BoardType boardType;
        public TextMesh boardText;
        public PlayerResourceWallet wallet;
        public GymQuestTracker questTracker;

        private void OnEnable()
        {
            if (wallet != null) wallet.ResourcesChanged += Refresh;
        }

        private void Start()
        {
            Refresh();
        }

        private void OnDisable()
        {
            if (wallet != null) wallet.ResourcesChanged -= Refresh;
        }

        public void BindWallet(PlayerResourceWallet playerWallet)
        {
            if (wallet != null) wallet.ResourcesChanged -= Refresh;
            wallet = playerWallet;
            if (isActiveAndEnabled && wallet != null) wallet.ResourcesChanged += Refresh;
            Refresh();
        }

        public void BindQuestTracker(GymQuestTracker tracker)
        {
            questTracker = tracker;
            Refresh();
        }

        public void Refresh()
        {
            if (boardText == null) return;
            if (boardType == BoardType.LockerClipboard)
            {
                boardText.text = questTracker != null && questTracker.armGainQuestCompleted
                    ? "TODAY'S QUESTS\n\nArm Gain goal complete!\n100 GymCoins awarded.\n\nBuild a Rubber Mat\nbefore placing a machine."
                    : "TODAY'S QUESTS\n\nReach 10 Arm Gain\nReward: 100 GymCoins\n\nBuild a Rubber Mat\nbefore placing a machine.";
                return;
            }

            if (wallet == null)
            {
                boardText.text = "GYM STATUS\nLoading player resources...";
                return;
            }

            StringBuilder text = new StringBuilder();
            text.AppendLine("GYM STATUS");
            if (wallet.IsDead)
            {
                text.AppendLine("DEATH: Health depleted");
                text.AppendLine();
            }
            text.AppendLine($"GymCoins  {wallet.GetValue("GymCoins"):0}");
            text.AppendLine($"Health    {wallet.GetValue("Health"):0}/{wallet.GetValue("MaxHealth"):0}");
            text.AppendLine($"Energy    {wallet.GetValue("Energy"):0}");
            text.AppendLine($"Hydration {wallet.GetValue("Hydration"):0}");
            text.AppendLine();
            text.AppendLine($"Arm Gain   {wallet.GetValue("ArmGain"):0}");
            text.AppendLine($"Chest Gain {wallet.GetValue("ChestGain"):0}");
            text.AppendLine($"Leg Gain   {wallet.GetValue("LegGain"):0}");
            text.AppendLine($"Back Gain  {wallet.GetValue("BackGain"):0}");
            text.AppendLine();
            text.AppendLine("Select a mat to build your");
            text.AppendLine("chosen machine. Use the");
            text.AppendLine("Wrench to repair or salvage.");
            boardText.text = text.ToString();
        }
    }
}
