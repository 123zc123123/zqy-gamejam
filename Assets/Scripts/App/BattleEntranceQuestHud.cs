using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DouQuqu
{
    /// <summary>
    /// 进战页日勤条。位置大小改 RectTransform。
    /// 1 局可点宝箱1，2 局可点宝箱2；点击后换成开宝箱预制体。
    /// </summary>
    public sealed class BattleEntranceQuestHud : MonoBehaviour
    {
        public const string OpenChestPrefabPath = "BattleEntrance/Prefabs/Parts/QuestChestOpen";
        public const string HelpPrefabPath = "BattleEntrance/Prefabs/Parts/QuestHelp";
        public const int Chest1Battles = 1;
        public const int Chest2Battles = 2;
        public const float ProgressFullWidth = 400f;
        public const float ProgressMinWidth = 8f;

        private float progressLeftX;
        private bool progressLeftPinned;
        private GameObject helpOverlay;
        private RectTransform helpImage;
        private QuestChestPickPopup chestPick;

        [InspectorCn("任务图标", "QuestIcon")]
        public RectTransform questIcon;
        [InspectorCn("任务进度", "QuestProgress")]
        public RectTransform questProgress;
        [InspectorCn("宝箱底图", "QuestChestBg")]
        public RectTransform chestBg;
        [InspectorCn("宝箱1", "1局可领")]
        public RectTransform chest1;
        [InspectorCn("宝箱2", "2局可领")]
        public RectTransform chest2;

        private void OnEnable()
        {
            ResolveSlots();
            BindQuestIcon();
            PlayerDataService.PlayerDataChanged -= RefreshChests;
            PlayerDataService.PlayerDataChanged += RefreshChests;
            RefreshChests();
        }

        void ResolveSlots()
        {
            if (questIcon == null) questIcon = FindChildRect("QuestIcon");
            if (questProgress == null) questProgress = FindChildRect("QuestProgress");
            if (chestBg == null) chestBg = FindChildRect("QuestChestBg");
            if (chest1 == null) chest1 = FindChildRect("QuestChest (1)");
            if (chest2 == null) chest2 = FindChildRect("QuestChest");
        }

        RectTransform FindChildRect(string childName)
        {
            Transform found = transform.Find(childName);
            return found as RectTransform;
        }

        private void OnDisable()
        {
            PlayerDataService.PlayerDataChanged -= RefreshChests;
            HideHelp();
            HideChestPick();
        }

        void BindQuestIcon()
        {
            if (questIcon == null) return;
            Image image = questIcon.GetComponent<Image>();
            if (image != null) image.raycastTarget = true;
            Button button = questIcon.GetComponent<Button>();
            if (button == null) button = questIcon.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            if (image != null) button.targetGraphic = image;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(ToggleHelp);
        }

        void ToggleHelp()
        {
            ShowHelp();
        }

        void ShowHelp()
        {
            if (helpOverlay == null) CreateHelpOverlay();
            if (helpOverlay == null) return;
            helpOverlay.SetActive(true);
            helpOverlay.transform.SetAsLastSibling();
            QuestHelpPopup popup = helpOverlay.GetComponent<QuestHelpPopup>();
            if (popup != null)
            {
                popup.ignoreButton = questIcon != null ? questIcon.GetComponent<Button>() : null;
                popup.SwallowCurrentClick();
            }
            ApplyHelpText();
            PlaceHelp();
        }

        void HideHelp()
        {
            if (helpOverlay != null) helpOverlay.SetActive(false);
        }

        void CreateHelpOverlay()
        {
            RectTransform overlayRect = UiFactory.CreateOverlay("QuestHelpCanvas", 260);
            GameObject overlay = overlayRect.gameObject;
            Image dim = overlay.GetComponent<Image>();
            if (dim == null) dim = overlay.AddComponent<Image>();
            dim.color = Color.clear;
            dim.raycastTarget = true;
            QuestHelpPopup popup = overlay.GetComponent<QuestHelpPopup>();
            if (popup == null) popup = overlay.AddComponent<QuestHelpPopup>();
            popup.ignoreButton = questIcon != null ? questIcon.GetComponent<Button>() : null;

            GameObject prefab = Resources.Load<GameObject>(HelpPrefabPath);
            if (prefab != null)
            {
                GameObject help = Object.Instantiate(prefab, overlay.transform, false);
                help.name = "QuestHelp";
                helpImage = help.GetComponent<RectTransform>();
                Image helpGraphic = help.GetComponent<Image>();
                if (helpGraphic != null) helpGraphic.raycastTarget = false;
                EnsureHelpBody(helpImage);
            }

            helpOverlay = overlay;
        }

        void ApplyHelpText()
        {
            if (helpImage == null) return;
            TMP_Text body = FindHelpBody(helpImage);
            if (body == null) return;
            body.text = ActivityPopup.GetBody(ActivityPopup.QuestId);
            UiFonts.ApplyTree(helpImage);
        }

        static void EnsureHelpBody(RectTransform help)
        {
            if (help == null || FindHelpBody(help) != null) return;
            GameObject go = new GameObject("Body", typeof(RectTransform), typeof(TextMeshProUGUI));
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(help, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(28f, 28f);
            rect.offsetMax = new Vector2(-28f, -28f);
            TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();
            text.font = UiFonts.Font;
            text.fontSize = 26f;
            text.color = new Color(0.28f, 0.16f, 0.10f, 1f);
            text.alignment = TextAlignmentOptions.TopLeft;
            text.enableWordWrapping = true;
            text.raycastTarget = false;
        }

        static TMP_Text FindHelpBody(RectTransform help)
        {
            if (help == null) return null;
            Transform found = help.Find("Body");
            return found != null ? found.GetComponent<TMP_Text>() : help.GetComponentInChildren<TMP_Text>(true);
        }

        private void OnDestroy()
        {
            if (helpOverlay != null) Object.Destroy(helpOverlay);
            HideChestPick();
        }

        void PlaceHelp()
        {
            if (helpImage == null || questIcon == null || helpOverlay == null) return;
            RectTransform overlay = helpOverlay.GetComponent<RectTransform>();
            if (overlay == null) return;
            Canvas.ForceUpdateCanvases();
            float overlayW = overlay.rect.width;
            float overlayH = overlay.rect.height;
            if (overlayW < 1f) overlayW = 1080f;
            if (overlayH < 1f) overlayH = 1920f;
            Vector2 iconLocal = overlay.InverseTransformPoint(questIcon.position);
            float pad = 16f;
            float halfHelpX = helpImage.sizeDelta.x * 0.5f;
            float halfHelpY = helpImage.sizeDelta.y * 0.5f;
            if (halfHelpX < 1f) halfHelpX = 193.5f;
            if (halfHelpY < 1f) halfHelpY = 125f;
            float halfIcon = questIcon.rect.width * 0.5f;
            float xRight = iconLocal.x + halfIcon + pad + halfHelpX;
            float xLeft = iconLocal.x - halfIcon - pad - halfHelpX;
            float minX = -overlayW * 0.5f + halfHelpX + 8f;
            float maxX = overlayW * 0.5f - halfHelpX - 8f;
            float x = xRight <= maxX ? xRight : xLeft;
            x = Mathf.Clamp(x, minX, maxX);
            float minY = -overlayH * 0.5f + halfHelpY + 8f;
            float maxY = overlayH * 0.5f - halfHelpY - 8f;
            float y = Mathf.Clamp(iconLocal.y, minY, maxY);
            helpImage.anchorMin = new Vector2(0.5f, 0.5f);
            helpImage.anchorMax = new Vector2(0.5f, 0.5f);
            helpImage.pivot = new Vector2(0.5f, 0.5f);
            helpImage.anchoredPosition = new Vector2(x, y);
            helpImage.localScale = Vector3.one;
        }

        public static bool HasClaimableChest()
        {
            int battles = PlayerDataService.BattleCount;
            if (!PlayerDataService.QuestChest1Claimed && battles >= Chest1Battles) return true;
            if (!PlayerDataService.QuestChest2Claimed && battles >= Chest2Battles) return true;
            return false;
        }

        public void RefreshChests()
        {
            BindChest(chest1, Chest1Battles, PlayerDataService.QuestChest1Claimed, 1);
            BindChest(chest2, Chest2Battles, PlayerDataService.QuestChest2Claimed, 2);
            RefreshProgress();
        }

        void RefreshProgress()
        {
            if (questProgress == null) return;
            PinProgressLeft();
            float t = Chest2Battles <= 0
                ? 0f
                : Mathf.Clamp01(PlayerDataService.BattleCount / (float)Chest2Battles);
            float width = Mathf.Lerp(ProgressMinWidth, ProgressFullWidth, t);
            questProgress.sizeDelta = new Vector2(width, questProgress.sizeDelta.y);
            Image image = questProgress.GetComponent<Image>();
            if (image == null) return;
            image.preserveAspect = false;
            image.type = Image.Type.Simple;
        }

        void PinProgressLeft()
        {
            if (progressLeftPinned || questProgress == null) return;
            float currentWidth = questProgress.sizeDelta.x;
            if (currentWidth <= 0f) currentWidth = ProgressFullWidth;
            progressLeftX = questProgress.anchoredPosition.x - questProgress.pivot.x * currentWidth;
            questProgress.pivot = new Vector2(0f, questProgress.pivot.y);
            Vector2 pos = questProgress.anchoredPosition;
            pos.x = progressLeftX;
            questProgress.anchoredPosition = pos;
            progressLeftPinned = true;
        }

        void BindChest(RectTransform slot, int need, bool claimed, int chestIndex)
        {
            if (slot == null) return;
            EnsureOpenVisual(slot, claimed);
            Button button = slot.GetComponent<Button>();
            if (button == null) button = slot.gameObject.AddComponent<Button>();
            Image image = slot.GetComponent<Image>();
            if (image != null)
            {
                image.raycastTarget = !claimed;
                image.color = claimed || PlayerDataService.BattleCount >= need
                    ? Color.white
                    : new Color(1f, 1f, 1f, 0.55f);
            }
            bool canClick = !claimed && PlayerDataService.BattleCount >= need;
            UiRedDot.Set(slot, canClick);
            button.interactable = canClick;
            ClaimReadyWobble wobble = slot.GetComponent<ClaimReadyWobble>();
            if (wobble == null && canClick) wobble = slot.gameObject.AddComponent<ClaimReadyWobble>();
            if (wobble != null) wobble.enabled = canClick;
            button.transition = Selectable.Transition.None;
            if (image != null) button.targetGraphic = image;
            button.onClick.RemoveAllListeners();
            if (!canClick) return;
            int captured = chestIndex;
            button.onClick.AddListener(() => ClaimChest(captured));
        }

        void ClaimChest(int chestIndex)
        {
            if (PlayerDataService.QuestChest1Claimed && chestIndex == 1) return;
            if (PlayerDataService.QuestChest2Claimed && chestIndex == 2) return;
            if (chestIndex == 1 && PlayerDataService.BattleCount < Chest1Battles) return;
            if (chestIndex == 2 && PlayerDataService.BattleCount < Chest2Battles) return;
            HideHelp();
            ShowChestPick(chestIndex);
        }

        void ShowChestPick(int chestIndex)
        {
            HideChestPick();
            int captured = chestIndex;
            chestPick = QuestChestPickPopup.Show(temperament => FinishChestPick(captured, temperament));
        }

        void FinishChestPick(int chestIndex, int temperament)
        {
            chestPick = null;
            if (!PlayerDataService.ClaimQuestChest(chestIndex)) return;
            PlayerDataService.AddFinestToBackpack(QuestChestPickPopup.Quality, temperament);
            RectTransform slot = chestIndex == 1 ? chest1 : chest2;
            EnsureOpenVisual(slot, true);
            RefreshChests();
        }

        void HideChestPick()
        {
            if (chestPick == null) return;
            chestPick.Close();
            chestPick = null;
        }

        static void EnsureOpenVisual(RectTransform slot, bool opened)
        {
            if (slot == null) return;
            Transform existing = slot.Find("QuestChestOpen");
            Image closed = slot.GetComponent<Image>();
            if (!opened)
            {
                if (existing != null) existing.gameObject.SetActive(false);
                if (closed != null) closed.enabled = true;
                return;
            }

            if (closed != null) closed.enabled = false;
            if (existing != null)
            {
                existing.gameObject.SetActive(true);
                FitOpenChest(existing as RectTransform, slot);
                return;
            }

            GameObject prefab = Resources.Load<GameObject>(OpenChestPrefabPath);
            if (prefab == null) return;
            GameObject openedGo = Object.Instantiate(prefab, slot, false);
            openedGo.name = "QuestChestOpen";
            FitOpenChest(openedGo.GetComponent<RectTransform>(), slot);
        }

        static void FitOpenChest(RectTransform opened, RectTransform slot)
        {
            if (opened == null || slot == null) return;
            float width = slot.sizeDelta.x;
            if (width <= 0f) width = slot.rect.width;
            Image image = opened.GetComponent<Image>();
            float height = width;
            if (image != null && image.sprite != null && image.sprite.rect.width > 0.01f)
                height = width * (image.sprite.rect.height / image.sprite.rect.width);
            opened.anchorMin = new Vector2(0.5f, 0.5f);
            opened.anchorMax = new Vector2(0.5f, 0.5f);
            opened.pivot = new Vector2(0.5f, 0.5f);
            opened.anchoredPosition = Vector2.zero;
            opened.sizeDelta = new Vector2(width, height);
            opened.localScale = Vector3.one;
            if (image == null) return;
            image.preserveAspect = true;
            image.raycastTarget = false;
        }
    }

    /// <summary>可领奖时轻微左右摇摆，领奖后由刷新逻辑自动停用。</summary>
    sealed class ClaimReadyWobble : MonoBehaviour
    {
        private RectTransform rect;
        private Vector3 baseRotation;
        private float time;

        private void OnEnable()
        {
            rect = transform as RectTransform;
            if (rect != null) baseRotation = rect.localEulerAngles;
            time = 0f;
        }

        private void Update()
        {
            if (rect == null) return;
            time += Time.unscaledDeltaTime;
            float angle = Mathf.Sin(time * 7.2f) * 8f;
            rect.localEulerAngles = baseRotation + new Vector3(0f, 0f, angle);
        }

        private void OnDisable()
        {
            if (rect != null) rect.localEulerAngles = baseRotation;
        }
    }
}
