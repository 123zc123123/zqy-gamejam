using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DouQuqu
{
    /// <summary>选虫页己方三槽：背包、绿框、点卡入槽、确定 / 取消选择。匹配在进战页。</summary>
    public sealed class HeroSelectionController : MonoBehaviour
    {
        public const int SelectSeconds = 59;
        private const int SlotCount = 3;
        private const string StatusSelecting = "选择中";
        private const string StatusConfirmed = "已确定";
        private const string LabelConfirm = "确定";
        private const string LabelCancel = "取消选择";
        private const float OwnRowWidth = 983f;
        private const float OwnRowHeight = 258f;
        private const float PlayerFrameNative = 260f;
        private const float PlayerFrameVisual = 182f;
        private const float PackCricketNative = 288f;
        private const float PackCricketVisual = 208f;
        private const float SelectionFrameWidth = 225f;
        private const float SelectionFrameHeight = 258f;
        private static readonly Vector2 PlayerFramePsd = new Vector2(37f, 31f);
        private static readonly Vector2[] SlotPsd =
        {
            new Vector2(267f, 18f),
            new Vector2(501f, 19f),
            new Vector2(736f, 19f)
        };
        private static readonly string[] OwnRowLegacy =
        {
            "Ellipse", "Rectangle 10", "Rectangle 13", "Frame 6", "Component 4", "玩家二"
        };
        private static readonly Color GreenFill = new Color(0.28f, 0.92f, 0.34f, 0.22f);
        private static readonly Color GreenLine = new Color(0.22f, 0.86f, 0.30f, 1f);
        private static readonly Color TabOn = new Color(0.96f, 0.90f, 0.62f, 1f);
        private static readonly Color TabOff = new Color(0.78f, 0.74f, 0.62f, 0.72f);
        private static readonly string[] FilterLabels = { "全部", "沉稳", "强攻", "灵巧", "智谋" };

        private GameObject pageRoot;
        private TMP_Text timerText;
        private Button matchButton;
        private TMP_Text matchButtonText;
        private Image matchButtonImage;
        private TMP_Text expandLabel;
        private Transform ownZone;
        private Transform backpackRoot;
        private Transform cardGrid;
        private Transform[] otherZones;
        private OtherZoneView[] otherZoneViews;
        private Transform readyBadge;
        private TMP_Text ownStatusText;
        private TMP_Text ownPlayerName;
        private RectTransform greenBox;
        private Sprite[] qualitySprites;
        private Sprite confirmSprite;
        private Sprite cancelSprite;
        private Sprite emptyPackBackground;
        private Sprite selectionFrameSprite;

        private readonly SlotView[] slots = new SlotView[SlotCount];
        private readonly CricketBackpackEntry[] slotEntries = new CricketBackpackEntry[SlotCount];
        private readonly List<FilterTab> filterTabs = new List<FilterTab>();
        private readonly List<CardView> cards = new List<CardView>();

        private Vector2 ownZoneHome;
        private Vector2 backpackHome;
        private Vector2 matchButtonHome;
        private bool homesCaptured;
        private bool bound;
        private bool sessionActive;
        private bool ready;
        private bool selectionClosed;
        private bool expanded;
        private int selectedSlot;
        private int filterTemperament;
        private float deadlineUnscaled;
        private GameObject cardTemplate;

        public void BindPage(GameObject root)
        {
            if (root == null) return;
            pageRoot = root;
            bound = true;
            BottomNavBar.SuppressEmbedded(root.transform);
            if (!TryBindArt(root.transform))
                BuildOverlay(root.transform);
            CaptureHomes();
            LoadQualitySprites();
            LoadButtonSprites();
        }

        public void BeginSession()
        {
            if (!bound) return;
            sessionActive = true;
            ready = false;
            selectionClosed = false;
            expanded = false;
            selectedSlot = 0;
            filterTemperament = 0;
            for (int i = 0; i < SlotCount; i++) slotEntries[i] = null;
            deadlineUnscaled = Time.unscaledTime + SelectSeconds;
            ApplyBattleDefer();
            ApplyCollapsed();
            ApplyOtherZones();
            HookBattleReady();
            HookLobby();
            RefreshAll();
        }

        public void CancelSelection()
        {
            sessionActive = false;
            UnhookBattleReady();
            UnhookLobby();
            ClearBattleDefer();
            ApplyCollapsed();
        }

        private void OnDestroy()
        {
            UnhookBattleReady();
            UnhookLobby();
        }

        private void Update()
        {
            if (!sessionActive || timerText == null) return;
            int remain = Mathf.Max(0, Mathf.CeilToInt(deadlineUnscaled - Time.unscaledTime));
            timerText.text = remain.ToString();
            if (remain > 0 || selectionClosed) return;
            selectionClosed = true;
            if (!ready) AutoFillAndReady();
            TryEnterBattle();
        }

        private bool TryBindArt(Transform root)
        {
            timerText = FindTmp(root, "59") ?? FindTmp(root, "MatchTimerTMP");
            Transform readyGo = FindNamed(root, "MatchButton") ?? FindNamed(root, "btn-ready");
            if (readyGo != null)
            {
                BindClick(readyGo.gameObject, OnReadyClicked);
                matchButton = readyGo.GetComponent<Button>();
                matchButtonText = readyGo.GetComponentInChildren<TMP_Text>(true);
                matchButtonImage = readyGo.GetComponent<Image>();
                if (matchButtonText != null) matchButtonText.text = LabelConfirm;
            }

            BindAllNamed(root, "BackButton", Back);
            BindAllNamed(root, "back-button", Back);

            Transform status = FindNamed(root, "MatchStatus");
            if (status != null) status.gameObject.SetActive(false);

            ownZone = FindNamed(root, "PlayerZone4");
            backpackRoot = FindNamed(root, "选择名角背包");
            otherZones = new[]
            {
                FindNamed(root, "PlayerZone1"),
                FindNamed(root, "PlayerZone2"),
                FindNamed(root, "PlayerZone3")
            };
            CaptureOtherZones();

            if (ownZone != null)
            {
                BindOwnZone(ownZone);
                Transform rect10 = FindNamed(ownZone, "Rectangle 10");
                if (rect10 != null) rect10.gameObject.SetActive(false);
            }

            if (backpackRoot != null) BindBackpack(backpackRoot);

            EnsureGreenBox();
            return timerText != null && matchButton != null;
        }

        private void BindOwnZone(Transform zone)
        {
            readyBadge = FindNamed(zone, "ready-badge");
            ownStatusText = ReadStatusText(zone);
            if (readyBadge != null) readyBadge.gameObject.SetActive(true);

            LayoutOwnRow(zone);

            Transform playerFrame = FindNamed(zone, "PlayerFrame");
            Transform name = playerFrame != null
                ? FindNamed(playerFrame, "玩家二")
                : FindNamed(zone, "玩家二") ?? FindNamed(zone, "PlayerName");
            ownPlayerName = name != null ? name.GetComponent<TMP_Text>() : null;
            if (ownPlayerName != null && PlayerDataService.IsLoggedIn)
                ownPlayerName.text = PlayerDataService.CurrentPlayerName;
        }

        private void LayoutOwnRow(Transform zone)
        {
            SizeOwnRow(zone as RectTransform);
            HideOwnRowLegacy(zone);

            float frameScale = PlayerFrameVisual / PlayerFrameNative;
            float packScale = PackCricketVisual / PackCricketNative;
            Vector2 framePos = PsdToLocal(PlayerFramePsd, PlayerFrameVisual);

            Transform playerFrame = EnsureChildPrefab(zone, "PlayerFrame", "Common/Prefabs/PlayerFrame");
            PlaceScaled(playerFrame as RectTransform, framePos, frameScale);

            GameObject packPrefab = Resources.Load<GameObject>("HeroSelection/Prefabs/Parts/PackCricket");
            for (int i = 0; i < SlotCount; i++)
            {
                string slotName = "PackCricket_" + i;
                Transform slotRoot = zone.Find(slotName);
                if (slotRoot == null && packPrefab != null)
                {
                    GameObject go = Object.Instantiate(packPrefab, zone, false);
                    go.name = slotName;
                    slotRoot = go.transform;
                }
                if (slotRoot == null) continue;
                PlaceScaled(slotRoot as RectTransform, PsdToLocal(SlotPsd[i], PackCricketVisual), packScale);
                slots[i] = ReadSlot(slotRoot);
                int captured = i;
                BindClick(slotRoot.gameObject, () => OnSlotClicked(captured));
            }

            if (readyBadge is RectTransform badge)
                PlaceScaled(badge, new Vector2(framePos.x, framePos.y - 70f), 1f);

            RefreshSlots();
        }

        private static void SizeOwnRow(RectTransform zone)
        {
            if (zone == null) return;
            float oldW = zone.rect.width;
            float oldH = zone.rect.height;
            if (oldW <= 1f) oldW = zone.sizeDelta.x;
            if (oldH <= 1f) oldH = zone.sizeDelta.y;
            Vector2 pos = zone.anchoredPosition;
            pos.x += (OwnRowWidth - oldW) * zone.pivot.x;
            pos.y += (OwnRowHeight - oldH) * zone.pivot.y;
            zone.anchoredPosition = pos;
            zone.sizeDelta = new Vector2(OwnRowWidth, OwnRowHeight);
            LayoutElement[] layouts = zone.GetComponents<LayoutElement>();
            for (int i = 0; i < layouts.Length; i++)
            {
                layouts[i].minWidth = OwnRowWidth;
                layouts[i].minHeight = OwnRowHeight;
                layouts[i].preferredWidth = OwnRowWidth;
                layouts[i].preferredHeight = OwnRowHeight;
            }
        }

        private static void HideOwnRowLegacy(Transform zone)
        {
            for (int i = 0; i < zone.childCount; i++)
            {
                Transform child = zone.GetChild(i);
                for (int j = 0; j < OwnRowLegacy.Length; j++)
                {
                    if (child.name != OwnRowLegacy[j]) continue;
                    child.gameObject.SetActive(false);
                    break;
                }
            }
        }

        private static Transform EnsureChildPrefab(Transform parent, string childName, string resourcePath)
        {
            Transform existing = parent.Find(childName);
            if (existing != null) return existing;
            GameObject prefab = Resources.Load<GameObject>(resourcePath);
            if (prefab == null) return null;
            GameObject go = Object.Instantiate(prefab, parent, false);
            go.name = childName;
            return go.transform;
        }

        private static void PlaceScaled(RectTransform rect, Vector2 pos, float scale)
        {
            if (rect == null) return;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = pos;
            rect.localScale = new Vector3(scale, scale, 1f);
            rect.localRotation = Quaternion.identity;
            LayoutElement layout = rect.GetComponent<LayoutElement>();
            if (layout != null) layout.ignoreLayout = true;
        }

        private static Vector2 PsdToLocal(Vector2 psdTopLeft, float size)
        {
            float cx = psdTopLeft.x + size * 0.5f - OwnRowWidth * 0.5f;
            float cy = OwnRowHeight * 0.5f - (psdTopLeft.y + size * 0.5f);
            return new Vector2(cx, cy);
        }

        private void BindBackpack(Transform backpack)
        {
            Transform expand = FindNamed(backpack, "展开背包");
            if (expand != null)
            {
                expandLabel = expand.GetComponent<TMP_Text>();
                BindClick(expand.gameObject, ToggleExpand);
            }
            Transform bar = FindNamed(backpack, "Rectangle 12");
            if (bar != null) BindClick(bar.gameObject, ToggleExpand);

            Transform sample = FindNamed(backpack, "极品_1");
            if (sample != null)
            {
                cardGrid = sample.parent;
                cardTemplate = sample.gameObject;
                CollectExistingCards(cardGrid);
            }

            Transform group = FindNamed(backpack, "Group 7");
            if (group != null) BindFilterTabs(group);
        }

        private void BindFilterTabs(Transform group)
        {
            filterTabs.Clear();
            List<Transform> frames = new List<Transform>();
            for (int i = 0; i < group.childCount; i++)
            {
                Transform child = group.GetChild(i);
                if (child.name.StartsWith("Frame") || child.name.IndexOf("PackPersonalitySwitchTab") >= 0)
                    frames.Add(child);
            }
            frames.Sort((a, b) =>
            {
                RectTransform ra = a as RectTransform;
                RectTransform rb = b as RectTransform;
                float xa = ra != null ? ra.anchoredPosition.x : 0f;
                float xb = rb != null ? rb.anchoredPosition.x : 0f;
                return xa.CompareTo(xb);
            });
            int count = Mathf.Min(frames.Count, FilterLabels.Length);
            for (int i = 0; i < count; i++)
            {
                Transform frame = frames[i];
                TMP_Text label = frame.GetComponentInChildren<TMP_Text>(true);
                if (label != null) label.text = FilterLabels[i];
                FilterTab tab = new FilterTab
                {
                    root = frame,
                    label = label,
                    line = FindNamed(frame, "选择线"),
                    temperament = i
                };
                filterTabs.Add(tab);
                int captured = i;
                BindClick(frame.gameObject, () => OnFilterClicked(captured));
            }
        }

        private void CollectExistingCards(Transform grid)
        {
            cards.Clear();
            for (int i = 0; i < grid.childCount; i++)
            {
                Transform child = grid.GetChild(i);
                if (child.name != "极品_1" && child.name.IndexOf("极品") < 0) continue;
                cards.Add(ReadCard(child.gameObject));
            }
        }

        private void CaptureHomes()
        {
            if (homesCaptured) return;
            RectTransform ownRect = ownZone as RectTransform;
            RectTransform bagRect = backpackRoot as RectTransform;
            RectTransform readyRect = matchButton != null ? matchButton.transform as RectTransform : null;
            if (ownRect != null) ownZoneHome = ownRect.anchoredPosition;
            if (bagRect != null) backpackHome = bagRect.anchoredPosition;
            if (readyRect != null) matchButtonHome = readyRect.anchoredPosition;
            homesCaptured = ownRect != null;
        }

        private void BuildOverlay(Transform page)
        {
            Canvas canvas = page.GetComponent<Canvas>();
            if (canvas == null) canvas = page.GetComponentInChildren<Canvas>(true);
            Transform parent = canvas != null ? canvas.transform : page;
            RectTransform panel = UiFactory.CreatePanel(parent, "HeroSelectionPanel",
                new Vector2(0.08f, 0.08f), new Vector2(0.92f, 0.52f), Vector2.zero, Vector2.zero);
            timerText = UiFactory.CreateText(panel, "MatchTimerTMP", SelectSeconds.ToString(), 32f,
                new Vector2(0.08f, 0.70f), new Vector2(0.92f, 0.82f), Vector2.zero, Vector2.zero);
            UiFactory.CreateText(panel, "Hint", "选虫页美术未绑上，无法选虫。", 26f,
                new Vector2(0.08f, 0.40f), new Vector2(0.92f, 0.68f), Vector2.zero, Vector2.zero);
            matchButton = UiFactory.CreateButton(panel, "MatchButton", LabelConfirm, OnReadyClicked,
                new Vector2(0.10f, 0.14f), new Vector2(0.48f, 0.26f), Vector2.zero, Vector2.zero);
            matchButtonText = matchButton.GetComponentInChildren<TMP_Text>();
            UiFactory.CreateButton(panel, "BackButton", "返回", Back,
                new Vector2(0.10f, 0.02f), new Vector2(0.36f, 0.12f), Vector2.zero, Vector2.zero);
        }

        private void OnSlotClicked(int index)
        {
            if (!sessionActive || ready) return;
            if (index < 0 || index >= SlotCount) return;
            selectedSlot = index;
            RefreshGreenBox();
        }

        private void ToggleExpand()
        {
            if (!sessionActive || ready) return;
            if (expanded) ApplyCollapsed();
            else ApplyExpanded();
            RefreshChrome();
        }

        private void ApplyExpanded()
        {
            expanded = true;
            filterTemperament = 0;
            SetOthersVisible(false);
            float delta = 0f;
            if (otherZones != null && otherZones.Length > 0 && otherZones[0] is RectTransform top)
                delta = top.anchoredPosition.y - ownZoneHome.y;
            SetAnchored(ownZone, ownZoneHome + new Vector2(0f, delta));
            SetAnchored(backpackRoot, backpackHome + new Vector2(0f, delta));
            if (matchButton != null)
                SetAnchored(matchButton.transform, matchButtonHome + new Vector2(0f, delta));
            if (cardGrid != null) cardGrid.gameObject.SetActive(true);
            RefreshFilters();
            RefreshCards();
        }

        private void ApplyCollapsed()
        {
            expanded = false;
            SetOthersVisible(true);
            SetAnchored(ownZone, ownZoneHome);
            SetAnchored(backpackRoot, backpackHome);
            if (matchButton != null) SetAnchored(matchButton.transform, matchButtonHome);
            if (cardGrid != null) cardGrid.gameObject.SetActive(true);
        }

        private void OnFilterClicked(int temperament)
        {
            if (!sessionActive || ready || !expanded) return;
            filterTemperament = temperament;
            RefreshFilters();
            RefreshCards();
        }

        private void OnReadyClicked()
        {
            if (!sessionActive || selectionClosed) return;
            LanSession network = ActiveLanBattle();
            if (network != null && network.IsBattleStarting) return;
            if (ready)
            {
                UnlockReady();
                return;
            }
            if (!AllSlotsFilled()) return;
            LockReady();
        }

        private void AutoFillAndReady()
        {
            if (ready) return;
            List<CricketBackpackEntry> pool = AvailableEntries();
            Shuffle(pool);
            int p = 0;
            for (int i = 0; i < SlotCount; i++)
            {
                if (slotEntries[i] != null) continue;
                if (p >= pool.Count) break;
                slotEntries[i] = pool[p++];
            }
            LockReady();
        }

        private void LockReady()
        {
            ready = true;
            if (expanded) ApplyCollapsed();
            AppServices.PendingLocalPicks = CopyPicks();
            LanSession network = ActiveLanBattle();
            if (network != null)
            {
                HookBattleReady();
                network.SetSelectionReady(true);
            }
            RefreshAll();
            if (network != null && network.IsBattleStarting)
                OnBattleReady();
        }

        private void UnlockReady()
        {
            if (!ready || selectionClosed) return;
            LanSession network = ActiveLanBattle();
            if (network != null && network.IsBattleStarting) return;
            ready = false;
            if (network != null)
                network.SetSelectionReady(false);
            RefreshAll();
        }

        private static LanSession ActiveLanBattle()
        {
            if (AppServices.PendingMatchKind == MatchKind.Training) return null;
            LanSession network = AppServices.Instance != null ? AppServices.Instance.Network : null;
            if (network == null || !network.IsRunning) return null;
            return network;
        }

        private void TryEnterBattle()
        {
            if (!sessionActive || !ready) return;
            AppServices.PendingLocalPicks = CopyPicks();
            LanSession network = ActiveLanBattle();
            if (network != null)
            {
                HookBattleReady();
                network.SetSelectionReady(true);
                if (network.IsHost) network.StartBattleAfterSelectionTimeout();
                RefreshChrome();
                if (network.IsBattleStarting) OnBattleReady();
                return;
            }
            SceneNames.Load(SceneNames.Battle);
        }

        private void HookBattleReady()
        {
            LanSession network = AppServices.Instance != null ? AppServices.Instance.Network : null;
            if (network == null) return;
            network.BattleReady -= OnBattleReady;
            network.BattleReady += OnBattleReady;
        }

        private void UnhookBattleReady()
        {
            LanSession network = AppServices.Instance != null ? AppServices.Instance.Network : null;
            if (network == null) return;
            network.BattleReady -= OnBattleReady;
        }

        private void HookLobby()
        {
            LanSession network = AppServices.Instance != null ? AppServices.Instance.Network : null;
            if (network == null) return;
            network.LobbyChanged -= OnLobbyChanged;
            network.LobbyChanged += OnLobbyChanged;
        }

        private void UnhookLobby()
        {
            LanSession network = AppServices.Instance != null ? AppServices.Instance.Network : null;
            if (network == null) return;
            network.LobbyChanged -= OnLobbyChanged;
        }

        private void OnLobbyChanged(LanLobbySnapshot snapshot)
        {
            if (!sessionActive) return;
            ApplyOtherZones();
        }

        private void ApplyBattleDefer()
        {
            LanSession network = AppServices.Instance != null ? AppServices.Instance.Network : null;
            if (network == null) return;
            network.DeferBattleUntilSelectionTimeout = AppServices.PendingMatchKind != MatchKind.Friend;
        }

        private void ClearBattleDefer()
        {
            LanSession network = AppServices.Instance != null ? AppServices.Instance.Network : null;
            if (network == null) return;
            network.DeferBattleUntilSelectionTimeout = false;
        }

        private void LoadButtonSprites()
        {
            confirmSprite = LoadSprite("HeroSelection/Textures/绿色准备");
            cancelSprite = LoadSprite("HeroSelection/Textures/红色取消");
        }

        private void OnBattleReady()
        {
            if (!sessionActive || !ready) return;
            UnhookBattleReady();
            SceneNames.Load(SceneNames.Battle);
        }

        private CricketPick[] CopyPicks()
        {
            CricketPick[] picks = new CricketPick[SlotCount];
            for (int i = 0; i < SlotCount; i++)
            {
                CricketBackpackEntry entry = slotEntries[i];
                if (entry == null)
                {
                    picks[i] = new CricketPick { catalogId = 0, quality = 1, temperament = 1 };
                    continue;
                }
                picks[i] = new CricketPick
                {
                    catalogId = (entry.quality - 1) * 4 + entry.temperament,
                    quality = entry.quality,
                    temperament = entry.temperament
                };
            }
            return picks;
        }

        private void OnCardClicked(string instanceId)
        {
            if (!sessionActive || ready) return;
            if (string.IsNullOrEmpty(instanceId)) return;
            if (SlotIndexOf(instanceId) >= 0) return;
            CricketBackpackEntry entry = PlayerDataService.FindBackpack(instanceId);
            if (entry == null) return;
            slotEntries[selectedSlot] = entry;
            RefreshSlots();
            RefreshCards();
            RefreshGreenBox();
        }

        private void RefreshAll()
        {
            RefreshChrome();
            RefreshSlots();
            RefreshGreenBox();
            RefreshFilters();
            RefreshCards();
        }

        private void RefreshChrome()
        {
            if (expandLabel != null) expandLabel.text = expanded ? "收起" : "展开背包";
            if (readyBadge != null) readyBadge.gameObject.SetActive(true);
            if (ownStatusText != null) ownStatusText.text = ready ? StatusConfirmed : StatusSelecting;
            if (matchButtonText != null) matchButtonText.text = ready ? LabelCancel : LabelConfirm;
            if (matchButtonImage != null)
            {
                Sprite sprite = ready ? cancelSprite : confirmSprite;
                if (sprite != null) matchButtonImage.sprite = sprite;
            }
            if (matchButton != null) matchButton.interactable = sessionActive && !selectionClosed;
            if (ownPlayerName != null && PlayerDataService.IsLoggedIn)
                ownPlayerName.text = PlayerDataService.CurrentPlayerName;
        }

        private void RefreshSlots()
        {
            if (emptyPackBackground == null)
                emptyPackBackground = LoadSprite("HeroSelection/Textures/PackCricketBg-unSelected");
            for (int i = 0; i < SlotCount; i++)
            {
                SlotView slot = slots[i];
                if (slot == null) continue;
                CricketBackpackEntry entry = slotEntries[i];
                bool filled = entry != null;
                if (slot.badgeRoot != null) slot.badgeRoot.SetActive(filled);
                if (slot.quality != null)
                    slot.quality.text = filled ? CricketCatalog.QualityName(entry.quality) : string.Empty;
                if (slot.name != null)
                {
                    slot.name.gameObject.SetActive(filled);
                    slot.name.text = filled ? CricketCatalog.CricketName(entry.quality, entry.temperament) : string.Empty;
                }
                if (slot.background != null)
                {
                    slot.background.enabled = true;
                    slot.background.preserveAspect = true;
                    slot.background.color = Color.white;
                    slot.background.sprite = filled
                        ? CricketCatalog.PackBackground(entry.quality, entry.temperament)
                        : emptyPackBackground;
                }
                if (slot.portrait != null)
                {
                    slot.portrait.gameObject.SetActive(filled);
                    if (filled)
                    {
                        slot.portrait.enabled = true;
                        slot.portrait.preserveAspect = true;
                        slot.portrait.sprite = CricketCatalog.Portrait(entry.quality, entry.temperament);
                        slot.portrait.color = Color.white;
                    }
                }
            }
        }

        private void RefreshGreenBox()
        {
            if (greenBox == null) return;
            bool show = sessionActive && !ready && selectedSlot >= 0 && selectedSlot < SlotCount
                && slots[selectedSlot] != null && slots[selectedSlot].root != null;
            greenBox.gameObject.SetActive(show);
            if (!show) return;
            PlaceGreenBox(slots[selectedSlot].root);
        }

        private void RefreshFilters()
        {
            for (int i = 0; i < filterTabs.Count; i++)
            {
                FilterTab tab = filterTabs[i];
                bool on = tab.temperament == filterTemperament;
                if (tab.line != null) tab.line.gameObject.SetActive(on);
                if (tab.label != null)
                {
                    tab.label.color = on ? TabOn : TabOff;
                    tab.label.fontStyle = on ? FontStyles.Bold : FontStyles.Normal;
                }
            }
        }

        private void RefreshCards()
        {
            if (cardGrid == null || cardTemplate == null) return;
            List<CricketBackpackEntry> entries = FilteredBackpack();
            EnsureCardCount(entries.Count);
            for (int i = 0; i < cards.Count; i++)
            {
                CardView card = cards[i];
                if (card == null || card.root == null) continue;
                if (i >= entries.Count)
                {
                    card.root.SetActive(false);
                    card.instanceId = null;
                    continue;
                }
                CricketBackpackEntry entry = entries[i];
                card.root.SetActive(true);
                card.instanceId = entry.instanceId;
                PlaceCard(card.root.transform as RectTransform, i);
                if (card.quality != null) card.quality.text = CricketCatalog.QualityName(entry.quality);
                if (card.name != null) card.name.text = CricketCatalog.CricketName(entry.quality, entry.temperament);
                if (card.background != null)
                {
                    card.background.sprite = CricketCatalog.PackBackground(entry.quality, entry.temperament);
                    card.background.enabled = card.background.sprite != null;
                    card.background.preserveAspect = true;
                    card.background.color = Color.white;
                }
                if (card.portrait != null)
                {
                    card.portrait.sprite = SpriteFor(entry.quality, entry.temperament);
                    card.portrait.enabled = card.portrait.sprite != null;
                    card.portrait.preserveAspect = true;
                    card.portrait.color = Color.white;
                }
                bool picked = SlotIndexOf(entry.instanceId) >= 0;
                if (card.group != null) card.group.alpha = picked ? 0.45f : 1f;
                string id = entry.instanceId;
                BindClick(card.root, () => OnCardClicked(id));
            }
        }

        private void EnsureCardCount(int needed)
        {
            while (cards.Count < needed)
            {
                GameObject clone = Object.Instantiate(cardTemplate, cardGrid, false);
                clone.name = "极品_1";
                clone.SetActive(true);
                cards.Add(ReadCard(clone));
            }
        }

        private List<CricketBackpackEntry> FilteredBackpack()
        {
            List<CricketBackpackEntry> all = PlayerDataService.GetBackpackSnapshot();
            if (filterTemperament <= 0) return all;
            List<CricketBackpackEntry> filtered = new List<CricketBackpackEntry>();
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i] != null && all[i].temperament == filterTemperament)
                    filtered.Add(all[i]);
            }
            return filtered;
        }

        private List<CricketBackpackEntry> AvailableEntries()
        {
            List<CricketBackpackEntry> all = PlayerDataService.GetBackpackSnapshot();
            List<CricketBackpackEntry> available = new List<CricketBackpackEntry>();
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i] == null) continue;
                if (SlotIndexOf(all[i].instanceId) >= 0) continue;
                available.Add(all[i]);
            }
            return available;
        }

        private int SlotIndexOf(string instanceId)
        {
            if (string.IsNullOrEmpty(instanceId)) return -1;
            for (int i = 0; i < SlotCount; i++)
            {
                if (slotEntries[i] != null && slotEntries[i].instanceId == instanceId) return i;
            }
            return -1;
        }

        private bool AllSlotsFilled()
        {
            for (int i = 0; i < SlotCount; i++)
                if (slotEntries[i] == null) return false;
            return true;
        }

        private void SetOthersVisible(bool visible)
        {
            if (otherZones == null) return;
            for (int i = 0; i < otherZones.Length; i++)
            {
                if (otherZones[i] != null) otherZones[i].gameObject.SetActive(visible);
            }
        }

        private void CaptureOtherZones()
        {
            if (otherZones == null) return;
            otherZoneViews = new OtherZoneView[otherZones.Length];
            for (int i = 0; i < otherZones.Length; i++)
                otherZoneViews[i] = ReadOtherZone(otherZones[i]);
        }

        private OtherZoneView ReadOtherZone(Transform zone)
        {
            if (zone == null) return null;
            HideCricketSlots(zone);
            OtherZoneView view = new OtherZoneView { root = zone };
            Transform name = FindNamed(zone, "玩家二") ?? FindNamed(zone, "PlayerName");
            view.playerName = name != null ? name.GetComponent<TMP_Text>() : null;
            view.homeName = view.playerName != null ? view.playerName.text : string.Empty;
            view.status = ReadStatusText(zone);
            return view;
        }

        private void ApplyOtherZones()
        {
            if (otherZoneViews == null) return;
            if (AppServices.PendingMatchKind == MatchKind.Training)
            {
                for (int i = 0; i < otherZoneViews.Length; i++)
                    PaintOtherZone(otherZoneViews[i], TrainingCamp.BotName(i), true);
                return;
            }
            LanSession network = AppServices.Instance != null ? AppServices.Instance.Network : null;
            if (network != null && network.IsRunning)
            {
                PaintLanOtherZones(network);
                return;
            }
            for (int i = 0; i < otherZoneViews.Length; i++)
            {
                OtherZoneView view = otherZoneViews[i];
                PaintOtherZone(view, view != null ? view.homeName : string.Empty, false);
            }
        }

        private void PaintLanOtherZones(LanSession network)
        {
            IReadOnlyList<LanPlayerSlot> slots = network.Slots;
            int localId = network.LocalPlayerId;
            int zone = 0;
            if (slots != null)
            {
                for (int i = 0; i < slots.Count && zone < otherZoneViews.Length; i++)
                {
                    if (i == localId) continue;
                    LanPlayerSlot slot = slots[i];
                    string playerName = slot != null && !string.IsNullOrEmpty(slot.playerName)
                        ? slot.playerName
                        : "玩家" + (i + 1);
                    bool confirmed = slot != null && (slot.selectionReady || slot.isBot);
                    PaintOtherZone(otherZoneViews[zone], playerName, confirmed);
                    zone++;
                }
            }
            for (; zone < otherZoneViews.Length; zone++)
            {
                OtherZoneView view = otherZoneViews[zone];
                PaintOtherZone(view, view != null ? view.homeName : string.Empty, false);
            }
        }

        private static void PaintOtherZone(OtherZoneView zone, string playerName, bool confirmed)
        {
            if (zone == null) return;
            if (zone.playerName != null && !string.IsNullOrEmpty(playerName))
                zone.playerName.text = playerName;
            if (zone.status != null)
                zone.status.text = confirmed ? StatusConfirmed : StatusSelecting;
        }

        private static void HideCricketSlots(Transform zone)
        {
            Transform frame = FindNamed(zone, "Frame 6");
            if (frame != null) frame.gameObject.SetActive(false);
        }

        private static TMP_Text ReadStatusText(Transform zone)
        {
            if (zone == null) return null;
            Transform badge = FindNamed(zone, "ready-badge");
            if (badge == null) return null;
            badge.gameObject.SetActive(true);
            Transform label = FindNamed(badge, "已准备");
            return label != null ? label.GetComponent<TMP_Text>() : badge.GetComponentInChildren<TMP_Text>(true);
        }

        private void EnsureGreenBox()
        {
            if (greenBox != null) return;
            if (selectionFrameSprite == null)
                selectionFrameSprite = LoadSprite("HeroSelection/Textures/选择框");
            GameObject go = new GameObject("GreenBox", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            greenBox = go.GetComponent<RectTransform>();
            Image image = go.GetComponent<Image>();
            image.raycastTarget = false;
            if (selectionFrameSprite != null)
            {
                image.sprite = selectionFrameSprite;
                image.color = Color.white;
                image.preserveAspect = true;
            }
            else
            {
                image.sprite = WhiteSprite();
                image.color = GreenFill;
                Outline outline = go.AddComponent<Outline>();
                outline.effectColor = GreenLine;
                outline.effectDistance = new Vector2(5f, -5f);
            }
            go.SetActive(false);
        }

        private void PlaceGreenBox(Transform slot)
        {
            RectTransform slotRect = slot as RectTransform;
            Transform parent = ownZone != null ? ownZone : slot;
            greenBox.SetParent(parent, false);
            greenBox.anchorMin = new Vector2(0.5f, 0.5f);
            greenBox.anchorMax = new Vector2(0.5f, 0.5f);
            greenBox.pivot = new Vector2(0.5f, 0.5f);
            greenBox.sizeDelta = new Vector2(SelectionFrameWidth, SelectionFrameHeight);
            greenBox.localScale = Vector3.one;
            float x = slotRect != null ? slotRect.anchoredPosition.x : 0f;
            greenBox.anchoredPosition = new Vector2(x, 0f);
            greenBox.SetAsLastSibling();
        }

        private void LoadQualitySprites()
        {
            qualitySprites = new Sprite[16];
            for (int quality = 1; quality <= 4; quality++)
            {
                for (int temperament = 1; temperament <= 4; temperament++)
                {
                    qualitySprites[(quality - 1) * 4 + (temperament - 1)] =
                        LoadSprite("Merge/MergeQualities/quality-" + quality + "-" + temperament);
                }
            }
        }

        private Sprite SpriteFor(int quality, int temperament)
        {
            int q = Mathf.Clamp(quality, 1, 4);
            int t = Mathf.Clamp(temperament, 1, 4);
            int index = (q - 1) * 4 + (t - 1);
            if (qualitySprites != null && index < qualitySprites.Length) return qualitySprites[index];
            return null;
        }

        private static Sprite LoadSprite(string path)
        {
            Sprite sprite = Resources.Load<Sprite>(path);
            if (sprite != null) return sprite;
            Texture2D texture = Resources.Load<Texture2D>(path);
            if (texture == null) return null;
            return Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
        }

        private static SlotView ReadSlot(Transform root)
        {
            SlotView slot = new SlotView { root = root };
            Transform bg = FindNamed(root, "背景");
            slot.background = bg != null ? bg.GetComponent<Image>() : null;
            Transform portrait = FindNamed(root, "头像") ?? FindNamed(root, "Rectangle");
            slot.portrait = portrait != null ? portrait.GetComponent<Image>() : root.GetComponent<Image>();
            Transform name = FindNamed(root, "白头狮") ?? FindNamed(root, "青牙王");
            slot.name = name != null ? name.GetComponent<TMP_Text>() : null;
            Transform badge = FindNamed(root, "品级") ?? FindNamed(root, "Frame");
            slot.badgeRoot = badge != null ? badge.gameObject : null;
            Transform quality = FindNamed(root, "极品") ?? FindNamed(root, "四品");
            slot.quality = quality != null ? quality.GetComponent<TMP_Text>() : null;
            return slot;
        }

        private static CardView ReadCard(GameObject root)
        {
            CardView card = new CardView { root = root };
            Transform quality = FindNamed(root.transform, "极品");
            card.quality = quality != null ? quality.GetComponent<TMP_Text>() : null;
            Transform name = FindNamed(root.transform, "白头狮");
            card.name = name != null ? name.GetComponent<TMP_Text>() : null;
            Transform portrait = FindNamed(root.transform, "头像");
            card.portrait = portrait != null ? portrait.GetComponent<Image>() : null;
            Transform bg = FindNamed(root.transform, "背景") ?? FindNamed(root.transform, "Rectangle 11");
            card.background = bg != null ? bg.GetComponent<Image>() : null;
            card.group = root.GetComponent<CanvasGroup>();
            if (card.group == null) card.group = root.AddComponent<CanvasGroup>();
            return card;
        }

        private static void PlaceCard(RectTransform rect, int index)
        {
            if (rect == null) return;
            const int cols = 3;
            const float card = 288f;
            const float topPad = 66f;
            const float vGap = 21f;
            int col = index % cols;
            int row = index / cols;
            float left = col == 0 ? 67f : col == 1 ? 396f : 705f;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(card, card);
            float x = left + card * 0.5f;
            float y = -(topPad + card * 0.5f + row * (card + vGap));
            rect.anchoredPosition = new Vector2(x, y);
        }

        private static void BindClick(GameObject go, UnityEngine.Events.UnityAction clicked)
        {
            if (go == null) return;
            Button button = go.GetComponent<Button>();
            Graphic graphic = go.GetComponent<Graphic>();
            if (button == null && graphic != null && !(graphic is Image))
            {
                Transform catcher = go.transform.Find("__click");
                GameObject hit = catcher != null ? catcher.gameObject : null;
                if (hit == null)
                {
                    hit = new GameObject("__click", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                    RectTransform rect = hit.GetComponent<RectTransform>();
                    rect.SetParent(go.transform, false);
                    rect.anchorMin = Vector2.zero;
                    rect.anchorMax = Vector2.one;
                    rect.offsetMin = Vector2.zero;
                    rect.offsetMax = Vector2.zero;
                    Image image = hit.GetComponent<Image>();
                    image.sprite = WhiteSprite();
                    image.color = new Color(1f, 1f, 1f, 0.01f);
                    image.raycastTarget = true;
                }
                go = hit;
                button = go.GetComponent<Button>();
                graphic = go.GetComponent<Graphic>();
            }
            if (graphic == null)
            {
                Image image = go.AddComponent<Image>();
                image.sprite = WhiteSprite();
                image.color = new Color(1f, 1f, 1f, 0.01f);
                image.raycastTarget = true;
                graphic = image;
            }
            else graphic.raycastTarget = true;
            if (button == null) button = go.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.targetGraphic = graphic;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(clicked);
        }

        private void BindAllNamed(Transform root, string objectName, UnityEngine.Events.UnityAction clicked)
        {
            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                if (transforms[i].name != objectName) continue;
                BindClick(transforms[i].gameObject, clicked);
            }
        }

        private static void SetAnchored(Transform target, Vector2 position)
        {
            RectTransform rect = target as RectTransform;
            if (rect != null) rect.anchoredPosition = position;
        }

        private static void Shuffle(List<CricketBackpackEntry> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                CricketBackpackEntry tmp = list[i];
                list[i] = list[j];
                list[j] = tmp;
            }
        }

        private static Sprite WhiteSprite()
        {
            Texture2D texture = Texture2D.whiteTexture;
            return Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 4f);
        }

        private static TMP_Text FindTmp(Transform root, string objectName)
        {
            Transform found = FindNamed(root, objectName);
            return found != null ? found.GetComponent<TMP_Text>() : null;
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

        private void Back()
        {
            sessionActive = false;
            Lobby.Show(Lobby.Page.BattleEnter);
        }

        private sealed class SlotView
        {
            public Transform root;
            public Image background;
            public Image portrait;
            public TMP_Text quality;
            public TMP_Text name;
            public GameObject badgeRoot;
        }

        private sealed class OtherZoneView
        {
            public Transform root;
            public TMP_Text playerName;
            public TMP_Text status;
            public string homeName;
        }

        private sealed class CardView
        {
            public GameObject root;
            public Image background;
            public Image portrait;
            public TMP_Text quality;
            public TMP_Text name;
            public CanvasGroup group;
            public string instanceId;
        }

        private sealed class FilterTab
        {
            public Transform root;
            public TMP_Text label;
            public Transform line;
            public int temperament;
        }
    }
}
