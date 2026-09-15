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
        private GameObject trainingExitRoot;
        private GameObject spectateLeaveRoot;
        private TouchInput touchInput;
        private MatchKind matchKind;
        private bool resultShown;
        private bool eliminationShown;
        private bool awardedThisMatch;
        private bool goldGrantedThisMatch;
        private bool leavingAfterReward;

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
            BuildTrainingExitUi();
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
            eliminationPanel.SetActive(false);
            eliminationPage = page.GetComponent<EliminationPage>();
            if (eliminationPage == null) eliminationPage = page.AddComponent<EliminationPage>();
            eliminationPage.WatchClicked -= SpectateBattlefield;
            eliminationPage.WatchClicked += SpectateBattlefield;
            eliminationPage.ExitClicked -= ReturnToBattleEntrance;
            eliminationPage.ExitClicked += ReturnToBattleEntrance;
            BindNamedButton(overlay, "退出", ReturnToBattleEntrance);
            BindNamedButton(overlay, "观战", SpectateBattlefield);
            UnityEngine.UI.Image overlayImage = overlay.GetComponent<UnityEngine.UI.Image>();
            if (overlayImage != null)
            {
                overlayImage.color = Color.clear;
                overlayImage.raycastTarget = false;
            }
            if (eliminationPanel != null) eliminationPanel.SetActive(false);
            return true;
        }

        private void OnStateChanged(MatchState state)
        {
            if (state == null) return;
            if (state.over)
            {
                HideElimination();
                if (spectateLeaveRoot != null) spectateLeaveRoot.SetActive(false);
                if (state.elapsed > 0.2f) ShowResult(state);
                return;
            }
            if (!state.started) return;
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
            eliminationShown = true;
            eliminationPanel.SetActive(true);
            if (eliminationPage != null) eliminationPage.Bind(match, localPlayerId);
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

        private void BuildTrainingExitUi()
        {
            if (matchKind != MatchKind.Training || trainingExitRoot != null) return;
            RectTransform overlay = UiFactory.CreateOverlay("TrainingExitCanvas", 240);
            GameObject buttonRoot = CreateTrainingExitFromRoomButton(overlay);
            if (buttonRoot == null)
            {
                UnityEngine.UI.Button button = UiFactory.CreateButton(overlay, "TrainingExitButton", "退出训练",
                    ReturnToBattleEntrance, new Vector2(0f, 1f), new Vector2(0f, 1f),
                    new Vector2(42f, -108f), new Vector2(260f, -32f));
                buttonRoot = button.gameObject;
            }

            trainingExitRoot = overlay.gameObject;
            BindNamedButton(buttonRoot.transform, "退出训练", ReturnToBattleEntrance);
            BindNamedButton(buttonRoot.transform, "离开房间", ReturnToBattleEntrance);
            BindNamedButton(buttonRoot.transform, buttonRoot.name, ReturnToBattleEntrance);
        }

        private static GameObject CreateTrainingExitFromRoomButton(RectTransform overlay)
        {
            GameObject battleEntrance = Resources.Load<GameObject>("BattleEntrance/Prefabs/BattleEntrance");
            Transform source = battleEntrance != null ? FindNamed(battleEntrance.transform, "Group 11") : null;
            if (source == null) source = battleEntrance != null ? FindNamed(battleEntrance.transform, "离开房间") : null;
            if (source == null) return null;

            GameObject clone = UnityEngine.Object.Instantiate(source.gameObject, overlay, false);
            clone.name = "退出训练";
            clone.SetActive(true);
            RectTransform rect = clone.transform as RectTransform;
            if (rect != null)
            {
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = new Vector2(0f, 1f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = new Vector2(129.5f, -54.5f);
                rect.sizeDelta = new Vector2(183f, 81f);
                rect.localScale = Vector3.one;
            }

            TMP_Text[] labels = clone.GetComponentsInChildren<TMP_Text>(true);
            for (int i = 0; i < labels.Length; i++)
            {
                if (labels[i] == null) continue;
                labels[i].text = "退出训练";
                labels[i].enableWordWrapping = false;
            }

            return clone;
        }

        private static bool BindNamedButton(Transform root, string objectName, UnityEngine.Events.UnityAction clicked)
        {
            Transform found = FindNamed(root, objectName);
            if (found == null) return false;
            UnityEngine.UI.Button button = found.GetComponent<UnityEngine.UI.Button>();
            if (button == null) button = found.gameObject.AddComponent<UnityEngine.UI.Button>();
            UnityEngine.UI.Image image = found.GetComponent<UnityEngine.UI.Image>();
            bool addedImage = image == null;
            if (addedImage) image = found.gameObject.AddComponent<UnityEngine.UI.Image>();
            if (image != null)
            {
                image.raycastTarget = true;
                if (addedImage)
                    image.color = new Color(1f, 1f, 1f, 0.01f);
                else if (image.color.a <= 0.01f && image.sprite == null)
                    image.color = new Color(1f, 1f, 1f, 0.01f);
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
            HideStick();
            if (trainingExitRoot != null) trainingExitRoot.SetActive(false);
            BuildSpectateLeaveUi();
            BattleCamera cam = FindObjectOfType<BattleCamera>();
            if (cam != null) cam.EnterGodView();
        }

        private void HideStick()
        {
            HudStick stick = FindObjectOfType<HudStick>();
            if (stick != null) stick.gameObject.SetActive(false);
            UIDocument document = FindObjectOfType<UIDocument>();
            if (document != null) document.enabled = false;
        }

        private void BuildSpectateLeaveUi()
        {
            if (spectateLeaveRoot != null)
            {
                spectateLeaveRoot.SetActive(true);
                return;
            }

            RectTransform overlay = UiFactory.CreateOverlay("SpectateLeaveCanvas", 245);
            UnityEngine.UI.Image overlayImage = overlay.GetComponent<UnityEngine.UI.Image>();
            if (overlayImage != null)
            {
                overlayImage.color = Color.clear;
                overlayImage.raycastTarget = false;
            }

            GameObject battleEntrance = Resources.Load<GameObject>("BattleEntrance/Prefabs/BattleEntrance");
            Transform source = battleEntrance != null ? FindNamed(battleEntrance.transform, "Group 11") : null;
            if (source == null) source = battleEntrance != null ? FindNamed(battleEntrance.transform, "离开房间") : null;
            GameObject buttonRoot;
            if (source != null)
            {
                buttonRoot = UnityEngine.Object.Instantiate(source.gameObject, overlay, false);
                buttonRoot.name = "离开房间";
                buttonRoot.SetActive(true);
                RectTransform rect = buttonRoot.transform as RectTransform;
                if (rect != null)
                {
                    rect.anchorMin = new Vector2(1f, 1f);
                    rect.anchorMax = new Vector2(1f, 1f);
                    rect.pivot = new Vector2(0.5f, 0.5f);
                    rect.anchoredPosition = new Vector2(-129.5f, -54.5f);
                    rect.sizeDelta = new Vector2(183f, 81f);
                    rect.localScale = Vector3.one;
                }
            }
            else
            {
                UnityEngine.UI.Button button = UiFactory.CreateButton(overlay, "离开房间", "离开房间",
                    ReturnToBattleEntrance, new Vector2(1f, 1f), new Vector2(1f, 1f),
                    new Vector2(-260f, -108f), new Vector2(-42f, -32f));
                buttonRoot = button.gameObject;
            }

            spectateLeaveRoot = overlay.gameObject;
            BindNamedButton(buttonRoot.transform, "离开房间", ReturnToBattleEntrance);
            BindNamedButton(buttonRoot.transform, buttonRoot.name, ReturnToBattleEntrance);
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
            PlayerDataService.AwardMatchRewards(place, match.MatchScore(LocalPlayerId()));
            awardedThisMatch = true;
            goldGrantedThisMatch = true;
        }

        private void ReturnToBattleEntrance()
        {
            if (leavingAfterReward)
            {
                FinishLeaveBattle();
                return;
            }
            AwardIfLeavingEarly();
            int gold = LeaveRewardGold();
            if (!goldGrantedThisMatch)
            {
                goldGrantedThisMatch = true;
                if (gold > 0) PlayerDataService.AddGold(gold);
            }
            leavingAfterReward = true;
            ActivityPopup.ShowMessage("对局奖励", "积分 +" + gold + "\n金币 +" + gold, FinishLeaveBattle);
        }

        private int LeaveRewardGold()
        {
            if (match == null) return 0;
            int place = match.Place(LocalPlayerId());
            int kill = match.MatchScore(LocalPlayerId());
            return PlayerDataService.MatchRewardGold(place, kill);
        }

        private void FinishLeaveBattle()
        {
            leavingAfterReward = false;
            bool keepHostAuthority = network != null && network.DetachMatchControllerForSceneTransition();
            if (network != null && !keepHostAuthority) network.Stop();
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
