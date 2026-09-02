using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UIElements;

namespace DouQuqu
{
    /// <summary>连接匹配会话与战斗场景，并在对局结束后显示结算和负责退出清理。</summary>
    [DefaultExecutionOrder(-500)]
    public sealed class DouQuquBattleSceneController : MonoBehaviour
    {
        private DouQuquMatchController match;
        private DouQuquLanSession network;
        private GameObject resultPanel;
        private TMP_Text resultText;
        private bool resultShown;

        private void Awake()
        {
            match = GetComponent<DouQuquMatchController>();
            if (match == null) match = FindObjectOfType<DouQuquMatchController>();
            DouQuquDemoView view = GetComponent<DouQuquDemoView>();
            if (view != null)
            {
                view.SetAutoStart(false);
                view.BindMatch(match);
            }
            DouQuquMergeBoardView mergeView = GetComponent<DouQuquMergeBoardView>();
            if (mergeView != null) mergeView.enabled = false;

            network = DouQuquAppServices.Instance.Network;
            if (network != null && network.IsMatchReady)
            {
                network.BindMatchController(match);
            }
            else if (match != null)
            {
                // 直接从编辑器运行战斗场景时保留单机四人 AI 测试能力。
                match.Configure(MatchRunMode.Offline, DouQuquMatchController.MaxPlayers);
                match.ResetMatch(DouQuquMatchController.MaxPlayers, System.Environment.TickCount);
                match.StartMatch();
            }

            DouQuquTouchInput touchInput = GetComponent<DouQuquTouchInput>();
            if (touchInput != null)
            {
                int localPlayerId = network != null && network.LocalPlayerId >= 0 ? network.LocalPlayerId : 0;
                touchInput.BindRuntime(match, network, localPlayerId);
            }
        }

        private void OnEnable()
        {
            if (match != null) match.StateChanged += OnStateChanged;
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
            RectTransform root = DouQuquUiFactory.CreateScreen("BattleResultCanvas");
            root.GetComponent<Canvas>().sortingOrder = 100;
            // 结算界面平时必须是透明覆盖层，否则隐藏的 ResultPanel 仍会因为
            // CreateScreen 创建的全屏背景 Image 把战斗场景完全遮住。
            UnityEngine.UI.Image overlay = root.GetComponent<UnityEngine.UI.Image>();
            if (overlay != null)
            {
                overlay.color = Color.clear;
                overlay.raycastTarget = false;
            }
            RectTransform panel = DouQuquUiFactory.CreatePanel(root, "ResultPanel",
                new Vector2(0.30f, 0.25f), new Vector2(0.70f, 0.75f), Vector2.zero, Vector2.zero);
            resultPanel = panel.gameObject;
            resultText = DouQuquUiFactory.CreateText(panel, "ResultTMP", "对局结束", 52f,
                new Vector2(0.08f, 0.48f), new Vector2(0.92f, 0.84f), Vector2.zero, Vector2.zero);
            DouQuquUiFactory.CreateButton(panel, "ReturnButton", "返回匹配界面", ReturnToMatchmaking,
                new Vector2(0.20f, 0.16f), new Vector2(0.80f, 0.36f), Vector2.zero, Vector2.zero);
            resultPanel.SetActive(false);
        }

        private void OnStateChanged(MatchState state)
        {
            if (state == null || !state.over || resultShown) return;
            resultShown = true;
            int localPlayerId = network != null && network.LocalPlayerId >= 0 ? network.LocalPlayerId : 0;
            if (state.winnerId < 0)
                resultText.text = "对局结束\n本局没有存活玩家";
            else
                resultText.text = state.winnerId == localPlayerId
                    ? "胜利！\n你是本局赢家"
                    : "对局结束\n获胜者：玩家 " + (state.winnerId + 1);
            resultPanel.SetActive(true);
        }

        private void ReturnToMatchmaking()
        {
            if (network != null) network.Stop();
            DouQuquSceneNames.Load(DouQuquSceneNames.Matchmaking);
        }
    }
}
