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

        private GameObject pageRoot;
        private GameObject actionsRoot;
        private GameObject playersRoot;
        private GameObject leaveRoot;
        private GameObject readyRoot;
        private bool bound;
        private bool friendRoom;

        public bool InRoom { get; private set; }

        public void BindPage(GameObject root)
        {
            pageRoot = root;
            bound = true;
            CacheRoots();
            EnsureTeamRoomUi();
            EnsureReadyButton();
            RelabelMatchButton();
            BindButtons();
            ApplyVisual();
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
            AppServices.PendingMatchKind = MatchKind.Random;
            InRoom = true;
            friendRoom = false;
            ApplyVisual();
            Lobby.Show(Lobby.Page.HeroSelection);
        }

        public void EnterFriendRoom()
        {
            AppServices.PendingMatchKind = MatchKind.Friend;
            InRoom = true;
            friendRoom = true;
            ApplyVisual();
        }

        public void GoHeroSelection()
        {
            if (!InRoom) return;
            Lobby.Show(Lobby.Page.HeroSelection);
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

        private void BindButtons()
        {
            if (pageRoot == null) return;
            Transform root = pageRoot.transform;

            GameObject match = FindGo(root, "StartMatchButton");
            if (match == null) match = FindGo(root, "随机匹配");
            if (match == null) match = FindGo(root, "开始匹配");
            if (match != null) BindButton(match, EnterRandomMatch);
            else Debug.LogWarning("[DouQuqu] 进战页没有随机匹配按钮");

            Transform team = FindNamed(root, "好友组队");
            if (team != null)
            {
                BindButton(team.gameObject, EnterFriendRoom);
                Transform confirm = FindNamed(team, "确认");
                if (confirm != null) BindButton(confirm.gameObject, EnterFriendRoom);
            }

            if (readyRoot != null) BindButton(readyRoot, GoHeroSelection);
            if (leaveRoot != null) BindButton(leaveRoot, LeaveRoom);

            GameObject rules = FindGo(root, "SideButton_活动介绍");
            if (rules == null) rules = FindGo(root, "玩法规则");
            if (rules != null) BindButton(rules, ActivityPopup.ShowRules);

            GameObject training = FindGo(root, "训练营");
            if (training != null) BindButton(training, OpenTrainingCamp);
        }

        private static void OpenTrainingCamp()
        {
            AppServices.PendingMatchKind = MatchKind.Training;
            Lobby.Show(Lobby.Page.HeroSelection);
        }

        private void RelabelMatchButton()
        {
            if (pageRoot == null) return;
            GameObject match = FindGo(pageRoot.transform, "StartMatchButton");
            if (match == null) match = FindGo(pageRoot.transform, "开始匹配");
            if (match == null) return;
            TMP_Text[] labels = match.GetComponentsInChildren<TMP_Text>(true);
            for (int i = 0; i < labels.Length; i++)
            {
                if (labels[i].text == "开始匹配"
                    || labels[i].name == "开始匹配"
                    || labels[i].text == "随机匹配"
                    || labels[i].name == "随机匹配")
                {
                    labels[i].text = "随机\n匹配";
                    labels[i].enableWordWrapping = true;
                    labels[i].alignment = TextAlignmentOptions.Center;
                }
            }
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

            if (room != null) EnsureRoomInput(room as RectTransform);
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
            if (room.GetComponent<TMP_InputField>() != null) return;

            Image image = room.GetComponent<Image>();
            if (image == null) image = room.gameObject.AddComponent<Image>();
            image.raycastTarget = true;

            TMP_Text placeholder = room.GetComponentInChildren<TMP_Text>(true);
            if (placeholder != null)
            {
                placeholder.text = "房间号：";
                placeholder.raycastTarget = false;
            }

            GameObject textGo = new GameObject("RoomCodeText", typeof(RectTransform), typeof(TextMeshProUGUI));
            RectTransform textRect = textGo.GetComponent<RectTransform>();
            textRect.SetParent(room, false);
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(16f, 8f);
            textRect.offsetMax = new Vector2(-16f, -8f);
            TextMeshProUGUI inputText = textGo.GetComponent<TextMeshProUGUI>();
            inputText.font = UiFactory.Font;
            inputText.fontSize = placeholder != null ? placeholder.fontSize : 36f;
            inputText.color = placeholder != null ? placeholder.color : new Color(0.31f, 0.35f, 0.28f, 1f);
            inputText.alignment = TextAlignmentOptions.Center;
            inputText.raycastTarget = true;

            TMP_InputField field = room.gameObject.AddComponent<TMP_InputField>();
            field.textViewport = room;
            field.textComponent = inputText;
            field.placeholder = placeholder;
            field.characterLimit = 8;
            field.lineType = TMP_InputField.LineType.SingleLine;
            field.caretWidth = 2;
        }

        private void ApplyVisual()
        {
            if (actionsRoot != null) actionsRoot.SetActive(!InRoom);
            if (playersRoot != null) playersRoot.SetActive(InRoom);
            if (leaveRoot != null) leaveRoot.SetActive(InRoom);
            if (readyRoot != null) readyRoot.SetActive(InRoom && friendRoom);
            if (Lobby.Instance == null) return;
            Lobby.Instance.RefreshNavVisibility();
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
