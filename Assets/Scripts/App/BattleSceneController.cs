using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UIElements;

namespace DouQuqu
{
    /// <summary>连接匹配会话与战斗场景，并在对局结束后显示结算和负责退出清理。</summary>
    [DefaultExecutionOrder(-500)]
    public sealed class BattleSceneController : MonoBehaviour
    {
        private MatchController match;
        private LanSession network;
        private GameObject resultPanel;
        private TMP_Text resultText;
        private SettlementPage settlementPage;
        private UnityEngine.UI.Button returnButton;
        private MatchKind matchKind;
        private bool resultShown;
        private bool localEliminated;

        private void Awake()
        {
            match = GetComponent<MatchController>();
            if (match == null) match = FindObjectOfType<MatchController>();
            DemoView view = GetComponent<DemoView>();
            if (view != null)
            {
                view.SetAutoStart(false);
                view.BindMatch(match);
            }
            MergeBoardView mergeView = GetComponent<MergeBoardView>();
            if (mergeView != null) mergeView.enabled = false;

            network = AppServices.Instance.Network;
            matchKind = AppServices.TakePendingMatchKind();
            bool lanBattle = network != null && network.IsRunning
                && (network.IsMatchReady || network.IsHost || network.LocalPlayerId >= 0);
            if (lanBattle)
            {
                if (!network.IsMatchReady) network.PrepareBattle();
                network.BindMatchController(match);
                if (matchKind == MatchKind.None) matchKind = MatchKind.Friend;
                CricketPick[] lanPicks = AppServices.TakePendingLocalPicks();
                int localId = network.LocalPlayerId >= 0 ? network.LocalPlayerId : 0;
                if (lanPicks != null && match != null) match.SetRoster(localId, lanPicks);
            }
            else if (match != null)
            {
                MatchKnobs runtime = matchKind == MatchKind.Training
                    ? TrainingCamp.WithDuration(match.Knobs)
                    : null;
                match.Configure(MatchRunMode.Offline, MatchController.MaxPlayers, runtime);
                ApplyPendingRosters(match, matchKind);
                match.ResetMatch(MatchController.MaxPlayers, System.Environment.TickCount);
                if (matchKind == MatchKind.Training)
                {
                    for (int i = 0; i < TrainingCamp.BotCount; i++)
                        match.SetPlayerIdle(i + 1, true);
                }
            }

            TouchInput touchInput = GetComponent<TouchInput>();
            if (touchInput != null)
            {
                int localPlayerId = network != null && network.LocalPlayerId >= 0 ? network.LocalPlayerId : 0;
                touchInput.BindRuntime(match, network, localPlayerId);
            }
        }

        private void OnEnable()
        {
            if (match != null) match.StateChanged += OnStateChanged;
            if (match != null) match.PlayerEliminated += OnPlayerEliminated;
        }

        private void Start()
        {
            HideMergeUi();
            BuildResultUi();
            if (match != null && match.State != null) OnStateChanged(match.State);
        }

        private void OnDisable()
        {
            if (match != null) match.StateChanged -= OnStateChanged;
            if (match != null) match.PlayerEliminated -= OnPlayerEliminated;
        }

        private void HideMergeUi()
        {
            UIDocument document = GetComponent<UIDocument>();
            if (document == null || document.rootVisualElement == null) return;
            VisualElement mergeRoot = document.rootVisualElement.Q<VisualElement>("merge-root");
            if (mergeRoot != null) mergeRoot.style.display = DisplayStyle.None;
        }

        private void BuildResultUi()
        {
            if (TryBuildArtResult()) return;

            RectTransform root = UiFactory.CreateScreen("BattleResultCanvas");
            root.GetComponent<Canvas>().sortingOrder = 100;
            // 结算界面平时必须是透明覆盖层，否则隐藏的 ResultPanel 仍会因为
            // CreateScreen 创建的全屏背景 Image 把战斗场景完全遮住。
            UnityEngine.UI.Image overlay = root.GetComponent<UnityEngine.UI.Image>();
            if (overlay != null)
            {
                overlay.color = Color.clear;
                overlay.raycastTarget = false;
            }
            RectTransform panel = UiFactory.CreatePanel(root, "ResultPanel",
                new Vector2(0.30f, 0.25f), new Vector2(0.70f, 0.75f), Vector2.zero, Vector2.zero);
            resultPanel = panel.gameObject;
            resultText = UiFactory.CreateText(panel, "ResultTMP", "对局结束", 52f,
                new Vector2(0.08f, 0.48f), new Vector2(0.92f, 0.84f), Vector2.zero, Vector2.zero);
            returnButton = UiFactory.CreateButton(panel, "ReturnButton", "返回", ReturnToBattleEntrance,
                new Vector2(0.20f, 0.16f), new Vector2(0.80f, 0.36f), Vector2.zero, Vector2.zero);
            resultPanel.SetActive(false);
        }

        private bool TryBuildArtResult()
        {
            GameObject prefab = Resources.Load<GameObject>("Settlement/Prefabs/Jiesuan");
            if (prefab == null) return false;

            RectTransform overlay = UiFactory.CreateOverlay("BattleResultCanvas", 300);
            GameObject page = Instantiate(prefab, overlay, false);
            page.name = "Jiesuan";
            RectTransform pageRect = page.GetComponent<RectTransform>();
            if (pageRect != null)
            {
                pageRect.anchorMin = new Vector2(0.5f, 0.5f);
                pageRect.anchorMax = new Vector2(0.5f, 0.5f);
                pageRect.pivot = new Vector2(0.5f, 0.5f);
                pageRect.anchoredPosition = Vector2.zero;
                pageRect.sizeDelta = new Vector2(1080f, 1920f);
                pageRect.localScale = Vector3.one;
            }

            resultPanel = overlay.gameObject;
            settlementPage = page.GetComponent<SettlementPage>();
            if (settlementPage == null) settlementPage = page.AddComponent<SettlementPage>();
            Transform title = page.transform.Find("Title");
            if (title == null) title = page.transform.Find("Banner/Title");
            resultText = title != null ? title.GetComponent<TMP_Text>() : overlay.GetComponentInChildren<TMP_Text>(true);

            BindOrCreateReturnButton(overlay);
            resultPanel.SetActive(false);
            return true;
        }

        private void OnStateChanged(MatchState state)
        {
            if (state == null) return;
            bool eliminated = IsLocalEliminated(state);
            if (!resultShown && eliminated)
            {
                ShowResult(state, true);
                return;
            }

            if (!state.over) return;
            if (!resultShown)
            {
                ShowResult(state, false);
                return;
            }

            // 本地淘汰后已经进入即时结算；最终帧到达时只补全最终名次。
            if (localEliminated && resultText != null) resultText.text = "结算完成\n你已淘汰";
            SetReturnInteractable(true);
        }

        private void OnPlayerEliminated(int playerId)
        {
            if (playerId != LocalPlayerId() || match == null || match.State == null || resultShown) return;
            ShowResult(match.State, true);
        }

        private void ShowResult(MatchState state, bool eliminated)
        {
            resultShown = true;
            localEliminated |= eliminated;
            int localPlayerId = LocalPlayerId();
            if (settlementPage != null)
            {
                settlementPage.Bind(match, matchKind, localPlayerId);
            }
            if (resultText != null)
            {
                if (eliminated)
                    resultText.text = "结算完成\n你已淘汰";
                else if (state.winnerId < 0)
                    resultText.text = "对局结束\n本局没有存活玩家";
                else
                    resultText.text = state.winnerId == localPlayerId
                        ? "胜利！\n你是本局赢家"
                        : "对局结束\n获胜者：玩家 " + (state.winnerId + 1);
            }
            resultPanel.SetActive(true);
            SetInputLocked();
            // 输掉的玩家立即拥有返回权限；普通房主会在返回前把权威模拟交给持久对象。
            SetReturnInteractable(true);
        }

        private int LocalPlayerId()
        {
            return network != null && network.LocalPlayerId >= 0 ? network.LocalPlayerId : 0;
        }

        private bool IsLocalEliminated(MatchState state)
        {
            int id = LocalPlayerId();
            return state.playerIn != null && id >= 0 && id < state.playerIn.Length && !state.playerIn[id];
        }

        private void SetReturnInteractable(bool interactable)
        {
            if (returnButton != null) returnButton.interactable = interactable;
        }

        private void SetInputLocked()
        {
            TouchInput[] touchInputs = FindObjectsOfType<TouchInput>(true);
            for (int i = 0; i < touchInputs.Length; i++)
                if (touchInputs[i] != null) touchInputs[i].enabled = false;

            HudStick[] sticks = FindObjectsOfType<HudStick>(true);
            for (int i = 0; i < sticks.Length; i++)
                if (sticks[i] != null) sticks[i].enabled = false;

            KeyboardInput[] keyboards = FindObjectsOfType<KeyboardInput>(true);
            for (int i = 0; i < keyboards.Length; i++)
                if (keyboards[i] != null) keyboards[i].enabled = false;
        }

        private void BindOrCreateReturnButton(RectTransform overlay)
        {
            UnityEngine.UI.Button existing = FindReturnButton(overlay);
            if (existing != null)
            {
                returnButton = existing;
                existing.onClick.RemoveAllListeners();
                existing.onClick.AddListener(ReturnToBattleEntrance);
                return;
            }

            returnButton = UiFactory.CreateButton(overlay, "ReturnButton", "返回", ReturnToBattleEntrance,
                new Vector2(0.22f, 0.04f), new Vector2(0.78f, 0.12f), Vector2.zero, Vector2.zero);
        }

        private static UnityEngine.UI.Button FindReturnButton(Transform root)
        {
            if (root == null) return null;
            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform t = transforms[i];
                if (t.name != "ReturnButton" && t.name != "返回" && t.name != "返回按钮") continue;
                UnityEngine.UI.Button button = t.GetComponent<UnityEngine.UI.Button>();
                if (button != null) return button;
            }
            return null;
        }

        private void ReturnToBattleEntrance()
        {
            bool matchOver = match != null && match.IsOver;
            bool ordinaryHostStillSimulating = network != null && network.IsHost
                && !network.IsDedicatedServer && !matchOver;
            if (ordinaryHostStillSimulating)
            {
                // 房主退出表现层前保留一份无界面的权威模拟，其他设备仍能收到后续快照。
                if (!network.DetachMatchControllerForSceneTransition())
                {
                    Debug.LogWarning("[DouQuqu] 无法保留房主权威模拟，结束当前局域网会话。");
                    network.Stop();
                }
            }
            else if (network != null)
            {
                network.Stop();
            }
            SceneNames.Load(SceneNames.BattleEntrance);
        }

        private static void ApplyPendingRosters(MatchController match, MatchKind kind)
        {
            if (match == null) return;
            CricketPick[] localPicks = AppServices.TakePendingLocalPicks();
            if (localPicks != null) match.SetRoster(0, localPicks);
            if (kind != MatchKind.Training) return;
            for (int i = 0; i < TrainingCamp.BotCount; i++)
                match.SetRoster(i + 1, TrainingCamp.PicksForBot(i));
        }
    }
}
