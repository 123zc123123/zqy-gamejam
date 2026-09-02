using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DouQuqu
{
    /// <summary>匹配界面：展示头像、TMP 计时、四个槽位，并驱动一键局域网匹配。</summary>
    public sealed class DouQuquMatchmakingController : MonoBehaviour
    {
        private DouQuquLanSession network;
        private TMP_Text timerText;
        private TMP_Text statusText;
        private TMP_Text slotsText;
        private Button matchButton;
        private TMP_Text matchButtonText;
        private bool loadingBattle;

        private void Awake()
        {
            network = DouQuquAppServices.Instance.Network;
        }

        private void OnEnable()
        {
            if (network == null) return;
            network.LobbyChanged += OnLobbyChanged;
            network.MatchReady += OnMatchReady;
            network.NetworkError += OnNetworkError;
        }

        private void Start()
        {
            if (!DouQuquPlayerDataService.RequireLogin()) return;
            BuildUi();
            RefreshLobby();
        }

        private void Update()
        {
            if (network == null || timerText == null) return;
            float elapsed = network.MatchmakingElapsed;
            timerText.text = string.Format("匹配计时  {0:00}:{1:00.0}", Mathf.FloorToInt(elapsed / 60f), elapsed % 60f);
            if (network.IsAutomaticMatchmaking)
                statusText.text = network.IsHost
                    ? "正在等待玩家，剩余 " + network.MatchmakingTimeRemaining.ToString("0.0") + " 秒"
                    : "正在搜索局域网中的玩家……";
        }

        private void OnDisable()
        {
            if (network == null) return;
            network.LobbyChanged -= OnLobbyChanged;
            network.MatchReady -= OnMatchReady;
            network.NetworkError -= OnNetworkError;
        }

        private void BuildUi()
        {
            RectTransform root = DouQuquUiFactory.CreateScreen("MatchmakingCanvas");
            RectTransform panel = DouQuquUiFactory.CreatePanel(root, "MatchmakingPanel",
                new Vector2(0.20f, 0.07f), new Vector2(0.80f, 0.93f), Vector2.zero, Vector2.zero);
            DouQuquUiFactory.CreateText(panel, "Title", "局域网匹配", 58f,
                new Vector2(0.08f, 0.83f), new Vector2(0.92f, 0.96f), Vector2.zero, Vector2.zero);
            DouQuquUiFactory.CreateAvatar(panel,
                new Vector2(0.12f, 0.54f), new Vector2(0.30f, 0.78f), Vector2.zero, Vector2.zero);
            DouQuquUiFactory.CreateText(panel, "PlayerName", DouQuquPlayerDataService.CurrentPlayerName, 30f,
                new Vector2(0.09f, 0.45f), new Vector2(0.33f, 0.53f), Vector2.zero, Vector2.zero);
            timerText = DouQuquUiFactory.CreateText(panel, "MatchTimerTMP", "匹配计时  00:00.0", 40f,
                new Vector2(0.36f, 0.68f), new Vector2(0.91f, 0.79f), Vector2.zero, Vector2.zero);
            statusText = DouQuquUiFactory.CreateText(panel, "MatchStatus", "点击按钮开始匹配", 27f,
                new Vector2(0.36f, 0.58f), new Vector2(0.91f, 0.68f), Vector2.zero, Vector2.zero);
            slotsText = DouQuquUiFactory.CreateText(panel, "PlayerSlots", string.Empty, 28f,
                new Vector2(0.36f, 0.28f), new Vector2(0.91f, 0.57f), Vector2.zero, Vector2.zero,
                TextAlignmentOptions.TopLeft);
            matchButton = DouQuquUiFactory.CreateButton(panel, "MatchButton", "开始匹配", ToggleMatch,
                new Vector2(0.32f, 0.11f), new Vector2(0.68f, 0.23f), Vector2.zero, Vector2.zero);
            matchButtonText = matchButton.GetComponentInChildren<TMP_Text>();
            DouQuquUiFactory.CreateButton(panel, "BackButton", "返回", Back,
                new Vector2(0.06f, 0.05f), new Vector2(0.22f, 0.13f), Vector2.zero, Vector2.zero);
        }

        private void ToggleMatch()
        {
            if (network.IsAutomaticMatchmaking)
            {
                network.CancelAutomaticMatchmaking();
                statusText.text = "已取消匹配";
                matchButtonText.text = "开始匹配";
                RefreshLobby();
                return;
            }
            network.StartAutomaticMatchmaking(DouQuquPlayerDataService.CurrentPlayerName, 10f);
            if (network.IsRunning)
            {
                statusText.text = "正在搜索局域网中的玩家……";
                matchButtonText.text = "取消匹配";
            }
        }

        private void OnLobbyChanged(LanLobbySnapshot lobby)
        {
            RefreshLobby();
        }

        private void RefreshLobby()
        {
            if (slotsText == null || network == null) return;
            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < DouQuquMatchController.MaxPlayers; i++)
            {
                LanPlayerSlot slot = i < network.Slots.Count ? network.Slots[i] : null;
                string value = slot == null || !slot.connected
                    ? "等待玩家"
                    : slot.playerName + (slot.isBot ? "（机器人）" : string.Empty);
                builder.Append("槽位 ").Append(i + 1).Append("　").Append(value);
                if (i < DouQuquMatchController.MaxPlayers - 1) builder.AppendLine();
            }
            slotsText.text = builder.ToString();
        }

        private void OnNetworkError(string message)
        {
            if (statusText != null) statusText.text = message;
        }

        private void OnMatchReady()
        {
            if (loadingBattle) return;
            loadingBattle = true;
            if (statusText != null) statusText.text = "匹配成功，正在进入游戏……";
            DouQuquSceneNames.Load(DouQuquSceneNames.Battle);
        }

        private void Back()
        {
            network.CancelAutomaticMatchmaking();
            DouQuquSceneNames.Load(DouQuquSceneNames.MainMenu);
        }
    }
}
