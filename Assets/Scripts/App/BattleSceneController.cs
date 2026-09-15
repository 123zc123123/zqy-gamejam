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
        private GameObject eliminationPanel;
        private EliminationPage eliminationPage;
        private TouchInput touchInput;
        private MatchKind matchKind;
        private bool resultShown;
        private bool eliminationShown;
        private bool awardedThisMatch;

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
            MatchKnobs runtime = matchKind == MatchKind.Training
                ? TrainingCamp.WithDuration(match != null ? match.Knobs : null)
                : CompetitiveMatch.WithDuration(match != null ? match.Knobs : null);
            bool lanBattle = network != null && network.IsRunning
                && (network.IsMatchReady || network.IsHost || network.LocalPlayerId >= 0);
            if (lanBattle)
            {
                if (matchKind == MatchKind.None) matchKind = MatchKind.Friend;
                if (match != null)
                    match.Configure(
                        network.IsHost ? MatchRunMode.Host : MatchRunMode.Client,
                        MatchController.MaxPlayers,
                        runtime);
                if (!network.IsMatchReady) network.PrepareBattle();
                network.BindMatchController(match);
                CricketPick[] lanPicks = AppServices.TakePendingLocalPicks();
                int localId = network.LocalPlayerId >= 0 ? network.LocalPlayerId : 0;
                if (lanPicks != null && match != null) match.SetRoster(localId, lanPicks);
            }
            else if (match != null)
            {
                match.Configure(MatchRunMode.Offline, MatchController.MaxPlayers, runtime);
                ApplyPendingRosters(match, matchKind);
                match.ResetMatch(MatchController.MaxPlayers, System.Environment.TickCount);
                if (matchKind == MatchKind.Training)
                {
                    for (int i = 0; i < TrainingCamp.BotCount; i++)
                        match.SetPlayerIdle(i + 1, true);
                }
            }

            touchInput = GetComponent<TouchInput>();
            if (touchInput != null)
            {
                int localPlayerId = LocalPlayerId();
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
            bool art = TryBuildArtResult();
            TryBuildEliminationUi();
            if (art) return;

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
            UiFactory.CreateButton(panel, "ReturnButton", "返回", ReturnToBattleEntrance,
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

            BindSettlementButtons(overlay);
            resultPanel.SetActive(false);
            return true;
        }

        private bool TryBuildEliminationUi()
        {
            GameObject prefab = Resources.Load<GameObject>("Settlement/Prefabs/Chuju");
            if (prefab == null) return false;

            RectTransform overlay = UiFactory.CreateOverlay("BattleEliminatedCanvas", 250);
            GameObject page = Instantiate(prefab, overlay, false);
            page.name = "Chuju";
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

            eliminationPanel = overlay.gameObject;
            eliminationPage = page.GetComponent<EliminationPage>();
            if (eliminationPage == null) eliminationPage = page.AddComponent<EliminationPage>();
            BindNamedButton(overlay, "退出", ReturnToBattleEntrance);
            BindNamedButton(overlay, "观战", SpectateBattlefield);
            UnityEngine.UI.Image overlayImage = overlay.GetComponent<UnityEngine.UI.Image>();
            if (overlayImage != null)
            {
                overlayImage.color = Color.clear;
                overlayImage.raycastTarget = false;
            }
            eliminationPanel.SetActive(false);
            return true;
        }

        private void OnStateChanged(MatchState state)
        {
            if (state == null) return;
            if (state.over)
            {
                HideElimination();
                ShowResult(state);
                return;
            }
            ShowEliminationIfNeeded();
        }

        private void ShowResult(MatchState state)
        {
            if (resultShown || resultPanel == null) return;
            resultShown = true;
            int localPlayerId = LocalPlayerId();
            if (settlementPage != null)
            {
                settlementPage.Bind(match, matchKind, localPlayerId);
                awardedThisMatch = true;
            }
            else if (resultText != null)
            {
                if (state.winnerId < 0)
                    resultText.text = "对局结束\n本局没有存活玩家";
                else
                    resultText.text = state.winnerId == localPlayerId
                        ? "胜利！\n你是本局赢家"
                        : "对局结束\n获胜者：玩家 " + (state.winnerId + 1);
            }
            resultPanel.SetActive(true);
            DisableLocalInput();
        }

        private void ShowEliminationIfNeeded()
        {
            if (eliminationShown || eliminationPanel == null || match == null) return;
            if (matchKind != MatchKind.Random && matchKind != MatchKind.Friend && matchKind != MatchKind.Training)
                return;
            int localPlayerId = LocalPlayerId();
            if (match.PlayerStillIn(localPlayerId)) return;
            if (match.Place(localPlayerId) <= 0) return;
            if (eliminationPage != null) eliminationPage.Bind(match, localPlayerId);
            eliminationPanel.SetActive(true);
            eliminationShown = true;
            DisableLocalInput();
        }

        private void BindSettlementButtons(RectTransform overlay)
        {
            if (BindNamedButton(overlay, "退出", ReturnToBattleEntrance)
                | BindNamedButton(overlay, "返回", ReturnToBattleEntrance)
                | BindNamedButton(overlay, "ReturnButton", ReturnToBattleEntrance))
            {
                BindNamedButton(overlay, "观战", SpectateBattlefield);
                return;
            }

            UiFactory.CreateButton(overlay, "ReturnButton", "退出", ReturnToBattleEntrance,
                new Vector2(0.22f, 0.04f), new Vector2(0.78f, 0.12f), Vector2.zero, Vector2.zero);
        }

        private static bool BindNamedButton(Transform root, string objectName, UnityEngine.Events.UnityAction clicked)
        {
            Transform found = FindNamed(root, objectName);
            if (found == null) return false;
            UnityEngine.UI.Button button = found.GetComponent<UnityEngine.UI.Button>();
            if (button == null) button = found.gameObject.AddComponent<UnityEngine.UI.Button>();
            UnityEngine.UI.Image image = found.GetComponent<UnityEngine.UI.Image>();
            if (image != null)
            {
                image.raycastTarget = true;
                button.targetGraphic = image;
            }
            // Figma 的 btn-ready 把 TMP 字放在子节点且 raycastTarget=1。
            // 字体套上后文字矩形会盖住父 Button，点击就进不了 onClick。
            UnityEngine.UI.Graphic[] graphics = found.GetComponentsInChildren<UnityEngine.UI.Graphic>(true);
            for (int i = 0; i < graphics.Length; i++)
            {
                if (graphics[i] == null || graphics[i].gameObject == found.gameObject) continue;
                graphics[i].raycastTarget = false;
            }
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(clicked);
            return true;
        }

        private static Transform FindNamed(Transform root, string objectName)
        {
            if (root == null) return null;
            if (root.name == objectName) return root;
            Transform direct = root.Find(objectName);
            if (direct != null) return direct;
            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                if (transforms[i].name == objectName) return transforms[i];
            }
            return null;
        }

        private void SpectateBattlefield()
        {
            HideElimination();
            if (settlementPage != null) settlementPage.HideForSpectate();
            DisableLocalInput();
        }

        private void HideElimination()
        {
            if (eliminationPanel != null) eliminationPanel.SetActive(false);
        }

        private void DisableLocalInput()
        {
            if (touchInput != null) touchInput.enabled = false;
        }

        private int LocalPlayerId()
        {
            return network != null && network.LocalPlayerId >= 0 ? network.LocalPlayerId : 0;
        }

        private void AwardIfLeavingEarly()
        {
            if (awardedThisMatch || match == null) return;
            if (matchKind != MatchKind.Random && matchKind != MatchKind.Friend) return;
            int place = match.Place(LocalPlayerId());
            if (place <= 0) return;
            PlayerDataService.AwardPlaceRewards(place);
            awardedThisMatch = true;
        }

        private void ReturnToBattleEntrance()
        {
            AwardIfLeavingEarly();
            if (network != null) network.Stop();
            Lobby.Show(Lobby.Page.BattleEnter);
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
