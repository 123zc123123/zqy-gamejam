using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DouQuqu
{
    /// <summary>进战页 BattleEntrance：随机匹配进选虫页；好友组队先等「准备」。</summary>
    public sealed class BattleEnterController : MonoBehaviour
    {
        private static readonly Color ConfirmFill = new Color(0.5192f, 0.0899f, 0.0936f, 1f);
        private static readonly Color TitleColor = new Color(0.9608f, 0.9255f, 0.8235f, 1f);
        private static readonly Color RoomInputText = new Color(0.31f, 0.22f, 0.16f, 1f);

        private GameObject pageRoot;
        private GameObject actionsRoot;
        private GameObject playersRoot;
        private GameObject leaveRoot;
        private GameObject readyRoot;
        private GameObject matchmakingStatusRoot;
        private TMP_Text matchmakingTimerText;
        private readonly Dictionary<Button, bool> lockedButtonStates = new Dictionary<Button, bool>();
        private bool bound;
        private bool friendRoom;
        private bool matching;

        private const float RandomMatchTimeout = 10f;

        public bool InRoom { get; private set; }

        public void BindPage(GameObject root)
        {
            pageRoot = root;
            bound = true;
            CacheRoots();
            EnsureTeamRoomUi();
            EnsureReadyButton();
            EnsureMatchmakingUi();
            BindButtons();
            HookLobby();
            ApplyVisual();
        }

        private void OnEnable()
        {
            HookLobby();
        }

        private void OnDisable()
        {
            if (AppServices.Instance == null || AppServices.Instance.Network == null) return;
            AppServices.Instance.Network.LobbyChanged -= OnLobbyChanged;
            AppServices.Instance.Network.MatchReady -= OnMatchReady;
        }

        private void HookLobby()
        {
            if (AppServices.Instance == null || AppServices.Instance.Network == null) return;
            AppServices.Instance.Network.LobbyChanged -= OnLobbyChanged;
            AppServices.Instance.Network.LobbyChanged += OnLobbyChanged;
            AppServices.Instance.Network.MatchReady -= OnMatchReady;
            AppServices.Instance.Network.MatchReady += OnMatchReady;
        }

        private void OnLobbyChanged(LanLobbySnapshot snapshot)
        {
            RefreshLobbyNames();
            RefreshFriendRoomAction();
            RefreshMatchmakingTimer();
        }

        private void Update()
        {
            if (!matching) return;
            RefreshMatchmakingTimer();

            LanSession network = AppServices.Instance != null ? AppServices.Instance.Network : null;
            if (network == null || !network.IsRunning)
            {
                // Socket 意外停止时解除界面锁，避免玩家被卡在不可点击的匹配页。
                matching = false;
                ApplyVisual();
            }
        }

        private void Start()
        {
            if (bound) return;
            if (!PlayerDataService.RequireLogin()) return;
            GameObject canvas = GameObject.Find("Canvas");
            if (canvas != null) BindPage(canvas);
        }

        public void EnterRandomMatch()
        {
            if (matching) return;

            LanSession network = AppServices.Instance != null ? AppServices.Instance.Network : null;
            if (network == null)
            {
                Debug.LogWarning("[DouQuqu] 没有局域网会话，无法开始随机匹配");
                return;
            }

            string playerName = PlayerDataService.IsLoggedIn ? PlayerDataService.CurrentPlayerName : "玩家";
            AppServices.PendingMatchKind = MatchKind.Random;
            InRoom = true;
            friendRoom = false;
            matching = true;
            network.StartAutomaticMatchmaking(playerName, RandomMatchTimeout);
            if (!network.IsRunning)
            {
                matching = false;
                InRoom = false;
                ApplyVisual();
                return;
            }

            ApplyVisual();
            RefreshLobbyNames();
            RefreshMatchmakingTimer();
        }

        /// <summary>局域网匹配完成后再进入选虫页，确保战斗场景能拿到同一局房间状态。</summary>
        private void OnMatchReady()
        {
            if (!matching && (!friendRoom || !InRoom)) return;
            matching = false;
            ApplyVisual();
            if (Lobby.Instance != null && Lobby.Instance.CurrentPage == Lobby.Page.BattleEnter)
                Lobby.Show(Lobby.Page.HeroSelection);
        }

        public void EnterFriendRoom()
        {
            if (matching) return;

            string code = ReadRoomCode();
            if (string.IsNullOrEmpty(code))
            {
                Debug.LogWarning("[DouQuqu] 请输入房间号");
                return;
            }
            LanSession network = AppServices.Instance != null ? AppServices.Instance.Network : null;
            if (network == null)
            {
                Debug.LogWarning("[DouQuqu] 没有局域网会话");
                return;
            }
            string playerName = PlayerDataService.IsLoggedIn ? PlayerDataService.CurrentPlayerName : "玩家";
            if (!network.JoinOrCreateRoom(code, playerName)) return;
            AppServices.PendingMatchKind = MatchKind.Friend;
            InRoom = true;
            friendRoom = true;
            ApplyVisual();
            RefreshLobbyNames();
            RefreshFriendRoomAction();
        }

        /// <summary>好友房由房主在人齐后统一开始；非房主只等待房主操作。</summary>
        public void OnFriendRoomAction()
        {
            if (!InRoom || !friendRoom || matching) return;
            LanSession network = AppServices.Instance != null ? AppServices.Instance.Network : null;
            if (network == null || network.IsMatchReady) return;

            if (network.IsHost)
            {
                if (!network.CanStart)
                {
                    ActivityPopup.ShowMessage("人数未齐", "四名玩家到齐后，房主才能开始。");
                    return;
                }
                network.StartMatchAsHost();
            }

            RefreshFriendRoomAction();
        }

        public void LeaveRoom()
        {
            LeaveRoomSilent();
            Lobby.Show(Lobby.Page.BattleEnter);
        }

        public void LeaveRoomSilent()
        {
            InRoom = false;
            friendRoom = false;
            matching = false;
            if (AppServices.Instance != null && AppServices.Instance.Network != null)
                AppServices.Instance.Network.LeaveRoom();
            ApplyVisual();
        }

        public void RefreshVisual()
        {
            ApplyVisual();
        }

        private void CacheRoots()
        {
            if (pageRoot == null) return;
            Transform root = pageRoot.transform;
            actionsRoot = FindGo(root, "Group 5");
            playersRoot = FindGo(root, "Group 19");
            leaveRoot = FindGo(root, "Group 11");
            if (leaveRoot == null) leaveRoot = FindGo(root, "离开房间");
            readyRoot = FindGo(root, "准备");
        }

        /// <summary>匹配页没有专用预制体时，运行时补一个适配竖屏的倒计时文本。</summary>
        private void EnsureMatchmakingUi()
        {
            if (pageRoot == null) return;

            matchmakingTimerText = FindTmp(pageRoot.transform, "MatchmakingTimerTMP");
            if (matchmakingTimerText == null)
                matchmakingTimerText = FindTmp(pageRoot.transform, "MatchTimerTMP");

            if (matchmakingTimerText == null)
            {
                GameObject go = new GameObject("MatchmakingTimerTMP", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                go.transform.SetParent(pageRoot.transform, false);
                RectTransform rect = go.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(620f, 82f);
                rect.anchoredPosition = new Vector2(0f, -250f);

                TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();
                text.font = UiFactory.Font;
                text.fontSize = 38f;
                text.color = TitleColor;
                text.alignment = TextAlignmentOptions.Center;
                text.enableWordWrapping = false;
                text.raycastTarget = false;
                matchmakingTimerText = text;
            }

            matchmakingStatusRoot = matchmakingTimerText != null ? matchmakingTimerText.gameObject : null;
            if (matchmakingStatusRoot != null) matchmakingStatusRoot.SetActive(false);
        }

        /// <summary>刷新“已匹配时间 / 10 秒”的显示；匹配完成后隐藏。</summary>
        private void RefreshMatchmakingTimer()
        {
            if (matchmakingTimerText == null) return;
            LanSession network = AppServices.Instance != null ? AppServices.Instance.Network : null;
            if (!matching || network == null)
            {
                if (matchmakingStatusRoot != null) matchmakingStatusRoot.SetActive(false);
                return;
            }

            if (matchmakingStatusRoot != null) matchmakingStatusRoot.SetActive(true);
            int elapsed = Mathf.Clamp(Mathf.FloorToInt(network.MatchmakingElapsed), 0, Mathf.CeilToInt(RandomMatchTimeout));
            matchmakingTimerText.text = string.Format("匹配中  {0:00} / {1:00} 秒", elapsed, Mathf.CeilToInt(RandomMatchTimeout));
        }

        private void BindButtons()
        {
            if (pageRoot == null) return;
            Transform root = pageRoot.transform;

            GameObject match = FindMatchCard(root);
            if (match != null) BindCard(match, EnterRandomMatch);
            else Debug.LogWarning("[DouQuqu] 进战页没有随机匹配按钮");

            Transform team = FindNamed(root, "好友组队");
            if (team != null)
            {
                Transform confirm = FindDirect(team, "确认");
                if (confirm != null) BindButton(confirm.gameObject, EnterFriendRoom);
            }

            if (readyRoot != null) BindButton(readyRoot, OnFriendRoomAction);
            if (leaveRoot != null) BindButton(leaveRoot, LeaveRoom);

            GameObject rules = FindGo(root, "SideButton_玩法说明");
            if (rules == null) rules = FindGo(root, "SideButton_活动介绍");
            if (rules == null) rules = FindGo(root, "玩法说明");
            if (rules == null) rules = FindGo(root, "玩法规则");
            if (rules != null) BindButton(rules, ActivityPopup.ShowRules);

            GameObject training = FindGo(root, "训练营");
            if (training != null) BindCard(training, OpenTrainingCamp);
        }

        /// <summary>
        /// 进战卡改版后根节点叫 MatchBtn-random，真正挡点击的是底部「确认」。
        /// 两处都要绑，否则点卡片或点确认条都会落到空 Button 上。
        /// </summary>
        private static GameObject FindMatchCard(Transform root)
        {
            GameObject match = FindGo(root, "MatchBtn-random");
            if (match == null) match = FindGo(root, "StartMatchButton");
            if (match == null) match = FindGo(root, "随机匹配");
            if (match != null) return match;

            Transform startLabel = FindNamed(root, "开始匹配");
            if (startLabel == null) return null;
            Transform parent = startLabel.parent;
            if (parent != null && parent.name == "确认" && parent.parent != null)
                return parent.parent.gameObject;
            return parent != null ? parent.gameObject : startLabel.gameObject;
        }

        private static void BindCard(GameObject card, UnityEngine.Events.UnityAction clicked)
        {
            if (card == null) return;
            BindButton(card, clicked);
            Transform confirm = FindDirect(card.transform, "确认");
            if (confirm != null) BindButton(confirm.gameObject, clicked);
        }

        private static void OpenTrainingCamp()
        {
            AppServices.PendingMatchKind = MatchKind.Training;
            Lobby.Show(Lobby.Page.HeroSelection);
        }

        private void EnsureTeamRoomUi()
        {
            if (pageRoot == null) return;
            Transform team = FindNamed(pageRoot.transform, "好友组队");
            if (team == null) return;

            Transform room = FindNamed(team, "Rectangle 8");
            RectTransform teamRect = team as RectTransform;
            if (teamRect == null) return;

            Transform confirm = FindNamed(team, "确认");
            if (confirm == null)
            {
                RectTransform roomRect = room as RectTransform;
                GameObject go = new GameObject("确认", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
                RectTransform rect = go.GetComponent<RectTransform>();
                rect.SetParent(team, false);
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                if (roomRect != null)
                {
                    float confirmW = 100f;
                    float gap = 10f;
                    float oldW = roomRect.sizeDelta.x;
                    roomRect.sizeDelta = new Vector2(Mathf.Max(140f, oldW - confirmW - gap), roomRect.sizeDelta.y);
                    float shift = (oldW - roomRect.sizeDelta.x) * 0.5f;
                    roomRect.anchoredPosition += new Vector2(-shift, 0f);
                    rect.sizeDelta = new Vector2(confirmW, roomRect.sizeDelta.y);
                    rect.anchoredPosition = roomRect.anchoredPosition
                        + new Vector2(roomRect.sizeDelta.x * 0.5f + gap + confirmW * 0.5f, 0f);
                }
                else
                {
                    rect.sizeDelta = new Vector2(100f, 72f);
                    rect.anchoredPosition = new Vector2(100f, -70f);
                }

                Image image = go.GetComponent<Image>();
                image.color = ConfirmFill;
                image.raycastTarget = true;
                Outline outline = go.AddComponent<Outline>();
                outline.effectColor = new Color(0.7725f, 0.6275f, 0.3490f, 1f);
                outline.effectDistance = new Vector2(2f, -2f);

                GameObject label = new GameObject("确认", typeof(RectTransform), typeof(TextMeshProUGUI));
                RectTransform labelRect = label.GetComponent<RectTransform>();
                labelRect.SetParent(rect, false);
                labelRect.anchorMin = Vector2.zero;
                labelRect.anchorMax = Vector2.one;
                labelRect.offsetMin = Vector2.zero;
                labelRect.offsetMax = Vector2.zero;
                TextMeshProUGUI text = label.GetComponent<TextMeshProUGUI>();
                text.font = UiFactory.Font;
                text.text = "确认";
                text.fontSize = 36f;
                text.color = TitleColor;
                text.alignment = TextAlignmentOptions.Center;
                text.raycastTarget = false;
                confirm = go.transform;
            }

            PassClicksToRoomAndConfirm(team, room, confirm);
            if (room != null) EnsureRoomInput(room as RectTransform);
        }

        /// <summary>描边/底图/图标盖在白框上面时，点房间号会点到整张卡而不是输入框。</summary>
        private static void PassClicksToRoomAndConfirm(Transform team, Transform room, Transform confirm)
        {
            if (team == null) return;
            Image[] images = team.GetComponentsInChildren<Image>(true);
            for (int i = 0; i < images.Length; i++)
            {
                Image image = images[i];
                if (image == null) continue;
                Transform t = image.transform;
                bool onRoom = room != null && (t == room || t.IsChildOf(room));
                bool onConfirm = confirm != null && (t == confirm || t.IsChildOf(confirm));
                if (onRoom || onConfirm) continue;
                image.raycastTarget = false;
            }
        }

        private void EnsureReadyButton()
        {
            if (pageRoot == null) return;
            if (readyRoot != null) return;

            Transform parent = actionsRoot != null ? actionsRoot.transform.parent : pageRoot.transform;
            GameObject match = FindGo(pageRoot.transform, "StartMatchButton");
            RectTransform readyRect;
            if (match != null)
            {
                readyRoot = Object.Instantiate(match, parent, false);
                readyRoot.name = "准备";
                readyRect = readyRoot.GetComponent<RectTransform>();
                Button copied = readyRoot.GetComponent<Button>();
                if (copied != null) copied.onClick.RemoveAllListeners();
            }
            else
            {
                readyRoot = new GameObject("准备", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
                readyRoot.transform.SetParent(parent, false);
                readyRect = readyRoot.GetComponent<RectTransform>();
                readyRect.anchorMin = new Vector2(0.5f, 0.5f);
                readyRect.anchorMax = new Vector2(0.5f, 0.5f);
                readyRect.pivot = new Vector2(0.5f, 0.5f);
                readyRect.sizeDelta = new Vector2(340f, 160f);
                Image image = readyRoot.GetComponent<Image>();
                image.color = ConfirmFill;
                image.raycastTarget = true;
                Outline outline = readyRoot.AddComponent<Outline>();
                outline.effectColor = new Color(0.7725f, 0.6275f, 0.3490f, 1f);
                outline.effectDistance = new Vector2(3f, -3f);
                GameObject label = new GameObject("准备", typeof(RectTransform), typeof(TextMeshProUGUI));
                RectTransform labelRect = label.GetComponent<RectTransform>();
                labelRect.SetParent(readyRect, false);
                labelRect.anchorMin = Vector2.zero;
                labelRect.anchorMax = Vector2.one;
                labelRect.offsetMin = Vector2.zero;
                labelRect.offsetMax = Vector2.zero;
                TextMeshProUGUI text = label.GetComponent<TextMeshProUGUI>();
                text.font = UiFactory.Font;
                text.text = "准备";
                text.fontSize = 48f;
                text.color = TitleColor;
                text.alignment = TextAlignmentOptions.Center;
                text.raycastTarget = false;
            }

            if (actionsRoot != null)
            {
                RectTransform group = actionsRoot.GetComponent<RectTransform>();
                readyRect.anchorMin = group.anchorMin;
                readyRect.anchorMax = group.anchorMax;
                readyRect.pivot = new Vector2(0.5f, 0.5f);
                readyRect.anchoredPosition = group.anchoredPosition;
                if (match != null)
                    readyRect.sizeDelta = match.GetComponent<RectTransform>().sizeDelta;
            }

            TMP_Text[] labels = readyRoot.GetComponentsInChildren<TMP_Text>(true);
            for (int i = 0; i < labels.Length; i++)
                labels[i].text = "准备";
            readyRoot.SetActive(false);
        }

        private static void EnsureRoomInput(RectTransform room)
        {
            if (room == null) return;

            Image image = room.GetComponent<Image>();
            if (image == null) image = room.gameObject.AddComponent<Image>();
            image.raycastTarget = true;

            Transform placeholderTf = FindNamed(room, "房间号：");
            TMP_Text placeholder = placeholderTf != null ? placeholderTf.GetComponent<TMP_Text>() : null;
            if (placeholder != null) placeholder.raycastTarget = false;

            Transform textTf = FindNamed(room, "RoomCodeText");
            TextMeshProUGUI inputText = textTf != null ? textTf.GetComponent<TextMeshProUGUI>() : null;
            if (inputText == null)
            {
                GameObject textGo = new GameObject("RoomCodeText", typeof(RectTransform), typeof(TextMeshProUGUI));
                RectTransform textRect = textGo.GetComponent<RectTransform>();
                textRect.SetParent(room, false);
                textRect.anchorMin = Vector2.zero;
                textRect.anchorMax = Vector2.one;
                textRect.offsetMin = new Vector2(16f, 8f);
                textRect.offsetMax = new Vector2(-16f, -8f);
                inputText = textGo.GetComponent<TextMeshProUGUI>();
                inputText.font = UiFactory.Font;
                inputText.fontSize = placeholder != null ? placeholder.fontSize : 36f;
                inputText.color = RoomInputText;
                inputText.alignment = TextAlignmentOptions.Center;
                inputText.enableWordWrapping = false;
                inputText.overflowMode = TextOverflowModes.Overflow;
            }
            inputText.raycastTarget = false;

            TMP_InputField field = room.GetComponent<TMP_InputField>();
            if (field == null)
            {
                field = room.gameObject.AddComponent<TMP_InputField>();
                field.targetGraphic = image;
                field.textViewport = room;
                field.textComponent = inputText;
                field.characterLimit = 6;
                field.lineType = TMP_InputField.LineType.SingleLine;
                field.caretWidth = 3;
                field.caretBlinkRate = 0.85f;
                field.customCaretColor = true;
                field.caretColor = RoomInputText;
                if (placeholder != null) field.placeholder = placeholder;
            }
            else
            {
                if (field.textComponent == null) field.textComponent = inputText;
                if (field.textViewport == null) field.textViewport = room;
                if (field.targetGraphic == null) field.targetGraphic = image;
                if (field.placeholder == null && placeholder != null) field.placeholder = placeholder;
            }

            if (placeholder != null && field.placeholder == null)
            {
                field.onSelect.AddListener(_ => placeholder.enabled = false);
                field.onEndEdit.AddListener(value =>
                {
                    placeholder.enabled = string.IsNullOrEmpty(value);
                });
            }
        }

        private string ReadRoomCode()
        {
            if (pageRoot == null) return string.Empty;
            TMP_InputField field = pageRoot.GetComponentInChildren<TMP_InputField>(true);
            if (field != null) return LanSession.NormalizeRoomCode(field.text);
            Transform room = FindNamed(pageRoot.transform, "Rectangle 8");
            if (room == null) return string.Empty;
            TMP_Text label = room.GetComponentInChildren<TMP_Text>(true);
            if (label == null) return string.Empty;
            string text = label.text ?? string.Empty;
            if (text.IndexOf("房间", System.StringComparison.Ordinal) >= 0) return string.Empty;
            return LanSession.NormalizeRoomCode(text);
        }

        private void RefreshLobbyNames()
        {
            if (playersRoot == null || AppServices.Instance == null || AppServices.Instance.Network == null) return;
            IReadOnlyList<LanPlayerSlot> slots = AppServices.Instance.Network.Slots;
            TMP_Text[] labels = playersRoot.GetComponentsInChildren<TMP_Text>(true);
            int slot = 0;
            for (int i = 0; i < labels.Length && slot < 4; i++)
            {
                TMP_Text label = labels[i];
                if (label == null) continue;
                string sample = label.text ?? string.Empty;
                if (sample.IndexOf("离开", System.StringComparison.Ordinal) >= 0) continue;
                if (sample.IndexOf("准备", System.StringComparison.Ordinal) >= 0) continue;
                string name = "空位";
                if (slots != null && slot < slots.Count && slots[slot] != null && slots[slot].connected)
                {
                    name = string.IsNullOrEmpty(slots[slot].playerName) ? "玩家" : slots[slot].playerName;
                    if (slot == 0) name += "（房主）";
                }
                label.text = name;
                slot++;
            }

            PaintLobbyFrames();
        }

        private void PaintLobbyFrames()
        {
            if (playersRoot == null) return;
            Transform root = playersRoot.transform;
            int count = Mathf.Min(4, root.childCount);
            for (int i = 0; i < count; i++)
            {
                Transform card = root.GetChild(i);
                if (card == null) continue;
                PlayerPalette.PaintOutline(card, i);
            }
        }

        private void ApplyVisual()
        {
            if (actionsRoot != null) actionsRoot.SetActive(!InRoom);
            if (playersRoot != null) playersRoot.SetActive(InRoom);
            if (leaveRoot != null) leaveRoot.SetActive(InRoom);
            if (readyRoot != null) readyRoot.SetActive(InRoom && friendRoom);
            SetPageButtonsLocked(matching);
            RefreshFriendRoomAction();
            RefreshMatchmakingTimer();
            if (InRoom) PaintLobbyFrames();
            if (Lobby.Instance == null) return;
            Lobby.Instance.RefreshNavVisibility();
        }

        /// <summary>根据本机身份刷新好友房按钮：房主可开始，其他玩家等待房主。</summary>
        private void RefreshFriendRoomAction()
        {
            if (readyRoot == null || !InRoom || !friendRoom) return;
            LanSession network = AppServices.Instance != null ? AppServices.Instance.Network : null;
            Button button = readyRoot.GetComponent<Button>();
            TMP_Text[] labels = readyRoot.GetComponentsInChildren<TMP_Text>(true);
            string label;
            bool interactable;

            if (network == null || !network.IsRunning || network.IsMatchReady)
            {
                label = "连接中";
                interactable = false;
            }
            else if (network.IsHost)
            {
                label = "开始";
                interactable = true;
            }
            else if (network.LocalPlayerId < 0)
            {
                label = "连接中";
                interactable = false;
            }
            else
            {
                label = "等待房主";
                interactable = false;
            }

            for (int i = 0; i < labels.Length; i++) labels[i].text = label;
            if (button != null) button.interactable = interactable;
        }

        /// <summary>匹配期间锁住其它入口，保留“离开房间”按钮用于取消匹配。</summary>
        private void SetPageButtonsLocked(bool locked)
        {
            if (pageRoot == null) return;
            if (locked)
            {
                Button[] buttons = pageRoot.GetComponentsInChildren<Button>(true);
                for (int i = 0; i < buttons.Length; i++)
                {
                    Button button = buttons[i];
                    if (button == null) continue;
                    if (leaveRoot != null && (button.gameObject == leaveRoot || button.transform.IsChildOf(leaveRoot.transform)))
                        continue;
                    if (!lockedButtonStates.ContainsKey(button))
                        lockedButtonStates.Add(button, button.interactable);
                    button.interactable = false;
                }
                return;
            }

            foreach (KeyValuePair<Button, bool> pair in lockedButtonStates)
            {
                if (pair.Key != null) pair.Key.interactable = pair.Value;
            }
            lockedButtonStates.Clear();
        }

        private static void BindButton(GameObject go, UnityEngine.Events.UnityAction clicked)
        {
            if (go == null) return;
            Image image = go.GetComponent<Image>();
            if (image == null) image = go.AddComponent<Image>();
            if (image != null)
            {
                image.raycastTarget = true;
                if (image.color.a <= 0.01f && image.sprite == null)
                    image.color = new Color(1f, 1f, 1f, 0.01f);
            }

            Button button = go.GetComponent<Button>();
            if (button == null) button = go.AddComponent<Button>();
            if (button == null) return;
            button.transition = Selectable.Transition.None;
            if (image != null) button.targetGraphic = image;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(clicked);
        }

        private static GameObject FindGo(Transform root, string objectName)
        {
            Transform found = FindNamed(root, objectName);
            return found != null ? found.gameObject : null;
        }

        private static TMP_Text FindTmp(Transform root, string objectName)
        {
            Transform found = FindNamed(root, objectName);
            return found != null ? found.GetComponent<TMP_Text>() : null;
        }

        private static Transform FindDirect(Transform root, string name)
        {
            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                if (child.name == name) return child;
            }

            return null;
        }

        private static Transform FindNamed(Transform root, string objectName)
        {
            if (root == null) return null;
            if (root.name == objectName) return root;
            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                if (transforms[i] != root && transforms[i].name == objectName) return transforms[i];
            }

            return null;
        }
    }
}
