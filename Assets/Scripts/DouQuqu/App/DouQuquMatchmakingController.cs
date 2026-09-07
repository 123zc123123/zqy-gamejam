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
        private TMP_Text[] playerNameTexts;
        private Button matchButton;
        private TMP_Text matchButtonText;
        private bool loadingBattle;
        private bool bound;
        private bool compactTimer;

        public void BindPage(GameObject pageRoot)
        {
            if (pageRoot == null) return;
            bound = true;
            DouQuquBottomNavBar.SuppressEmbedded(pageRoot.transform);
            if (!TryBindArt(pageRoot.transform))
                BuildOverlay(pageRoot.transform);
            RefreshLobby();
        }

        public void CancelMatchmaking()
        {
            if (network != null) network.CancelAutomaticMatchmaking();
            if (matchButtonText != null) matchButtonText.text = compactTimer ? "确认选择" : "开始匹配";
            if (statusText != null) statusText.text = "点击按钮开始匹配";
        }

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
            if (!bound) BuildUi();
            RefreshLobby();
        }

        private void Update()
        {
            if (network == null || timerText == null) return;
            float elapsed = network.MatchmakingElapsed;
            if (compactTimer)
                timerText.text = Mathf.Max(0, 59 - Mathf.FloorToInt(elapsed)).ToString();
            else
                timerText.text = string.Format("匹配计时  {0:00}:{1:00.0}", Mathf.FloorToInt(elapsed / 60f), elapsed % 60f);
            if (network.IsAutomaticMatchmaking && statusText != null)
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
                new Vector2(0.32f, 0.22f), new Vector2(0.68f, 0.33f), Vector2.zero, Vector2.zero);
            matchButtonText = matchButton.GetComponentInChildren<TMP_Text>();
            DouQuquUiFactory.CreateButton(panel, "AiBattleButton", "AI 对战", StartAiBattle,
                new Vector2(0.32f, 0.10f), new Vector2(0.68f, 0.20f), Vector2.zero, Vector2.zero);
            DouQuquUiFactory.CreateButton(panel, "BackButton", "返回", Back,
                new Vector2(0.06f, 0.05f), new Vector2(0.22f, 0.13f), Vector2.zero, Vector2.zero);
        }

        private bool TryBindArt(Transform root)
        {
            timerText = FindTmp(root, "MatchTimerTMP") ?? FindTmp(root, "59");
            compactTimer = timerText != null && (timerText.name == "59" || timerText.name == "MatchTimerTMP");
            statusText = FindTmp(root, "MatchStatus");
            slotsText = FindTmp(root, "PlayerSlots");
            playerNameTexts = FindPlayerNameTexts(root);
            matchButton = FindButton(root, "MatchButton") ?? FindButton(root, "btn-ready");
            if (matchButton != null)
            {
                matchButton.onClick.RemoveAllListeners();
                matchButton.onClick.AddListener(ToggleMatch);
                matchButtonText = matchButton.GetComponentInChildren<TMP_Text>();
            }
            Button ai = FindButton(root, "AiBattleButton");
            if (ai != null)
            {
                ai.onClick.RemoveAllListeners();
                ai.onClick.AddListener(StartAiBattle);
            }
            BindAllNamed(root, "BackButton", Back);
            BindAllNamed(root, "back-button", Back);
            return timerText != null && matchButton != null;
        }

        private void BuildOverlay(Transform pageRoot)
        {
            Canvas canvas = pageRoot.GetComponent<Canvas>();
            if (canvas == null) canvas = pageRoot.GetComponentInChildren<Canvas>(true);
            Transform parent = canvas != null ? canvas.transform : pageRoot;
            RectTransform panel = DouQuquUiFactory.CreatePanel(parent, "MatchmakingPanel",
                new Vector2(0.08f, 0.08f), new Vector2(0.92f, 0.52f), Vector2.zero, Vector2.zero);
            Image image = panel.GetComponent<Image>();
            if (image != null) image.color = new Color(0.07f, 0.14f, 0.22f, 0.82f);
            DouQuquUiFactory.CreateText(panel, "Title", "局域网匹配", 44f,
                new Vector2(0.08f, 0.84f), new Vector2(0.92f, 0.96f), Vector2.zero, Vector2.zero);
            timerText = DouQuquUiFactory.CreateText(panel, "MatchTimerTMP", "匹配计时  00:00.0", 32f,
                new Vector2(0.08f, 0.70f), new Vector2(0.92f, 0.82f), Vector2.zero, Vector2.zero);
            statusText = DouQuquUiFactory.CreateText(panel, "MatchStatus", "点击按钮开始匹配", 26f,
                new Vector2(0.08f, 0.58f), new Vector2(0.92f, 0.70f), Vector2.zero, Vector2.zero);
            slotsText = DouQuquUiFactory.CreateText(panel, "PlayerSlots", string.Empty, 24f,
                new Vector2(0.08f, 0.28f), new Vector2(0.92f, 0.58f), Vector2.zero, Vector2.zero,
                TextAlignmentOptions.TopLeft);
            matchButton = DouQuquUiFactory.CreateButton(panel, "MatchButton", "开始匹配", ToggleMatch,
                new Vector2(0.10f, 0.14f), new Vector2(0.48f, 0.26f), Vector2.zero, Vector2.zero);
            matchButtonText = matchButton.GetComponentInChildren<TMP_Text>();
            DouQuquUiFactory.CreateButton(panel, "AiBattleButton", "AI 对战", StartAiBattle,
                new Vector2(0.52f, 0.14f), new Vector2(0.90f, 0.26f), Vector2.zero, Vector2.zero);
            DouQuquUiFactory.CreateButton(panel, "BackButton", "返回", Back,
                new Vector2(0.10f, 0.02f), new Vector2(0.36f, 0.12f), Vector2.zero, Vector2.zero);
        }

        private static TMP_Text FindTmp(Transform root, string objectName)
        {
            Transform found = FindNamed(root, objectName);
            return found != null ? found.GetComponent<TMP_Text>() : null;
        }

        private static Button FindButton(Transform root, string objectName)
        {
            Transform found = FindNamed(root, objectName);
            return found != null ? found.GetComponent<Button>() : null;
        }

        private void BindAllNamed(Transform root, string objectName, UnityEngine.Events.UnityAction clicked)
        {
            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                if (transforms[i].name != objectName) continue;
                Button button = transforms[i].GetComponent<Button>();
                if (button == null) button = transforms[i].gameObject.AddComponent<Button>();
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(clicked);
            }
        }

        private static Transform FindNamed(Transform root, string objectName)
        {
            if (root == null) return null;
            if (root.name == objectName) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform hit = FindNamed(root.GetChild(i), objectName);
                if (hit != null) return hit;
            }
            return null;
        }

        private static TMP_Text[] FindPlayerNameTexts(Transform root)
        {
            var texts = new TMP_Text[DouQuquMatchController.MaxPlayers];
            for (int i = 0; i < texts.Length; i++)
            {
                Transform zone = FindNamed(root, "PlayerZone" + (i + 1)) ?? FindNth(root, "player-2-zone", i);
                if (zone == null) continue;
                Transform name = FindNamed(zone, "玩家二") ?? FindNamed(zone, "玩家一") ?? FindNamed(zone, "PlayerName");
                if (name != null) texts[i] = name.GetComponent<TMP_Text>();
            }
            return texts;
        }

        private static Transform FindNth(Transform root, string objectName, int index)
        {
            int current = 0;
            return FindNthRecursive(root, objectName, ref current, index);
        }

        private static Transform FindNthRecursive(Transform root, string objectName, ref int current, int index)
        {
            if (root.name == objectName)
            {
                if (current == index) return root;
                current++;
            }
            for (int i = 0; i < root.childCount; i++)
            {
                Transform hit = FindNthRecursive(root.GetChild(i), objectName, ref current, index);
                if (hit != null) return hit;
            }
            return null;
        }

        private void ToggleMatch()
        {
            if (network.IsAutomaticMatchmaking)
            {
                network.CancelAutomaticMatchmaking();
                if (statusText != null) statusText.text = "已取消匹配";
                if (matchButtonText != null) matchButtonText.text = "确认选择";
                RefreshLobby();
                return;
            }
            network.StartAutomaticMatchmaking(DouQuquPlayerDataService.CurrentPlayerName, 10f);
            if (network.IsRunning)
            {
                if (statusText != null) statusText.text = "正在搜索局域网中的玩家……";
                if (matchButtonText != null) matchButtonText.text = "取消匹配";
            }
        }

        private void OnLobbyChanged(LanLobbySnapshot lobby)
        {
            RefreshLobby();
        }

        private void RefreshLobby()
        {
            if (network == null) return;
            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < DouQuquMatchController.MaxPlayers; i++)
            {
                LanPlayerSlot slot = i < network.Slots.Count ? network.Slots[i] : null;
                string value = slot == null || !slot.connected
                    ? "等待玩家"
                    : slot.playerName + (slot.isBot ? "（机器人）" : string.Empty);
                if (playerNameTexts != null && i < playerNameTexts.Length && playerNameTexts[i] != null)
                    playerNameTexts[i].text = value;
                builder.Append("槽位 ").Append(i + 1).Append("　").Append(value);
                if (i < DouQuquMatchController.MaxPlayers - 1) builder.AppendLine();
            }
            if (slotsText != null) slotsText.text = builder.ToString();
        }

        private void OnNetworkError(string message)
        {
            if (statusText != null) statusText.text = message;
        }

        private void StartAiBattle()
        {
            if (loadingBattle) return;
            loadingBattle = true;
            if (network != null)
            {
                network.CancelAutomaticMatchmaking();
                network.Stop();
            }
            if (statusText != null) statusText.text = "正在进入 AI 对战……";
            DouQuquSceneNames.Load(DouQuquSceneNames.Battle);
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
            if (network != null) network.CancelAutomaticMatchmaking();
            DouQuquLobby.Show(DouQuquLobby.Page.BattleEnter);
        }
    }
}
