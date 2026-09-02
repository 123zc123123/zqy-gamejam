using System;
using UnityEngine;
using UnityEngine.UI;

namespace ZqyGameJam.UI.Home
{
    [DisallowMultipleComponent]
    public sealed class CricketHomepageView : MonoBehaviour
    {
        public enum Destination { Festival, Cage, Gift, Shop, Rank, Battle, Share, Arena, Collection, Training, Enter }

        [Header("Content")]
        [SerializeField] private Text playerName;
        [SerializeField] private Text playerPower;
        [SerializeField] private Text coins;
        [SerializeField] private Text attendanceTitle;
        [SerializeField] private Text attendanceTime;

        [Header("Navigation")]
        [SerializeField] private Button festivalButton;
        [SerializeField] private Button cageButton;
        [SerializeField] private Button giftButton;
        [SerializeField] private Button shopButton;
        [SerializeField] private Button rankButton;
        [SerializeField] private Button battleButton;
        [SerializeField] private Button shareButton;
        [SerializeField] private Button arenaButton;
        [SerializeField] private Button collectionButton;
        [SerializeField] private Button trainingButton;
        [SerializeField] private Button enterButton;

        public event Action<Destination> NavigationRequested;

        private void Awake()
        {
            Wire(festivalButton, Destination.Festival);
            Wire(cageButton, Destination.Cage);
            Wire(giftButton, Destination.Gift);
            Wire(shopButton, Destination.Shop);
            Wire(rankButton, Destination.Rank);
            Wire(battleButton, Destination.Battle);
            Wire(shareButton, Destination.Share);
            Wire(arenaButton, Destination.Arena);
            Wire(collectionButton, Destination.Collection);
            Wire(trainingButton, Destination.Training);
            Wire(enterButton, Destination.Enter);
            ApplyDefaultFont();
        }

        private void OnDestroy()
        {
            Unwire(festivalButton); Unwire(cageButton); Unwire(giftButton); Unwire(shopButton);
            Unwire(rankButton); Unwire(battleButton); Unwire(shareButton); Unwire(arenaButton);
            Unwire(collectionButton); Unwire(trainingButton); Unwire(enterButton);
        }

        private void Wire(Button button, Destination destination)
        {
            if (button != null) button.onClick.AddListener(() => NavigationRequested?.Invoke(destination));
        }

        private void Unwire(Button button)
        {
            if (button != null) button.onClick.RemoveAllListeners();
        }

        private void ApplyDefaultFont()
        {
            Font font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "SimHei", "Arial" }, 32);
            if (font == null) return;
            Text[] labels = GetComponentsInChildren<Text>(true);
            foreach (Text label in labels) label.font = font;
        }

        public void SetPlayer(string value, int power)
        {
            if (playerName != null) playerName.text = value;
            if (playerPower != null) playerPower.text = "最强战力 " + power;
        }

        public void SetCoins(int value)
        {
            if (coins != null) coins.text = value.ToString("N0");
        }

        public void SetAttendance(string title, string remainingTime)
        {
            if (attendanceTitle != null) attendanceTitle.text = title;
            if (attendanceTime != null) attendanceTime.text = remainingTime;
        }
    }
}