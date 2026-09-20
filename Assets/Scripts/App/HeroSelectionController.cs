using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using ZqyGameJam.UI.QuquXiangqing;

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
        private const float DesignWidth = 1080f;
        private const float DesignHeight = 1920f;
        private static readonly Color GreenFill = new Color(0.28f, 0.92f, 0.34f, 0.22f);
        private static readonly Color GreenLine = new Color(0.22f, 0.86f, 0.30f, 1f);
        private static readonly Color TabOn = new Color(0.96f, 0.90f, 0.62f, 1f);
        private static readonly Color StatusOn = new Color(0.20784315f, 0.6666667f, 0.050980397f, 1f);
        private static readonly Color StatusOff = new Color(0.55f, 0.55f, 0.55f, 1f);
        private static readonly string[] FilterLabels = { "全部", "耐战", "强攻", "灵巧", "智谋" };
        private static readonly string[] DetailNames =
        {
            "cricketDetail-Selection", "cricketDetail-Seletion", "cricketDetail"
        };
        private const string SlotIndexPrefabPath = "HeroSelection/Prefabs/Parts/index";
        private const string SlotIndexSpritePath = "HeroSelection/Textures/indexIcon";

        private GameObject pageRoot;
        private TMP_Text timerText;
        private Button matchButton;
        private TMP_Text matchButtonText;
        private Image matchButtonImage;
        private TMP_Text expandLabel;
        private Transform ownZone;
        private Transform playerMe;
        private Transform otherPlayer;
        private Transform backpackRoot;
        private Transform cardGrid;
        private RectTransform cardViewport;
        private ScrollRect cardScroll;
        private Transform[] otherZones;
        private OtherZoneView[] otherZoneViews;
        private Transform readyBadge;
        private Image readyBadgeIcon;
        private TMP_Text ownStatusText;
        private TMP_Text ownPlayerName;
        private RectTransform greenBox;
        private Sprite[] qualitySprites;
        private Sprite confirmSprite;
        private Sprite cancelSprite;
        private Sprite emptyPackBackground;
        private Sprite selectionFrameSprite;
        private Sprite readyOnSprite;
        private Sprite readyOffSprite;

        private readonly SlotView[] slots = new SlotView[SlotCount];
        private readonly CricketBackpackEntry[] slotEntries = new CricketBackpackEntry[SlotCount];
        private readonly List<FilterTab> filterTabs = new List<FilterTab>();
        private readonly List<CardView> cards = new List<CardView>();

        private Vector2 ownZoneHome;
        private Vector2 matchButtonHome;
        private float backpackFollowGap;
        private bool homesCaptured;
        private bool moveMatchButton;
        private bool bound;
        private float lastPageWidth = -1f;
        private bool sessionActive;
        private bool ready;
        private bool selectionClosed;
        private bool expanded;
        private int selectedSlot;
        private string detailInstanceId;
        private int filterTemperament;
        private float deadlineUnscaled;
        private GameObject cardTemplate;
        private QuquXiangqingView pageDetailView;

        public void BindPage(GameObject root)
        {
            if (root == null) return;
            pageRoot = root;
            bound = true;
            lastPageWidth = -1f;
            LockParentCanvasScale();
            BottomNavBar.SuppressEmbedded(root.transform);
            FlattenNestedOverlayCanvases(root.transform);
            if (!TryBindArt(root.transform))
                BuildOverlay(root.transform);
            CaptureHomes();
            LoadQualitySprites();
            LoadButtonSprites();
            ApplyAspectLayout();
        }

        public void BeginSession()
        {
            if (!bound) return;
            LockParentCanvasScale();
            sessionActive = true;
            ready = false;
            selectionClosed = false;
            expanded = false;
            selectedSlot = 0;
            detailInstanceId = null;
            filterTemperament = 0;
            for (int i = 0; i < SlotCount; i++) slotEntries[i] = null;
            deadlineUnscaled = Time.unscaledTime + SelectSeconds;
            ApplyBattleDefer();
            ApplyCollapsed();
            lastPageWidth = -1f;
            ApplyAspectLayout();
            ApplyOtherZones();
            HookBattleReady();
            HookLobby();
            RefreshAll();
            TutorialSpotlight.Hide();
            TutorialDirector.OnHeroSelectOpened(this);
        }

        public void NotifyBackpackChanged()
        {
            if (sessionActive) RefreshAll();
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
            if (bound && pageRoot != null && pageRoot.activeInHierarchy)
                ApplyAspectLayout();
            if (!sessionActive || timerText == null) return;
            if (TutorialDirector.BlocksHeroReady)
            {
                deadlineUnscaled += Time.unscaledDeltaTime;
                return;
            }
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
            if (TutorialDirector.HidesMatchExit)
            {
                Transform back = FindNamed(root, "BackButton");
                if (back == null) back = FindNamed(root, "back-button");
                if (back != null) back.gameObject.SetActive(false);
            }

            Transform status = FindNamed(root, "MatchStatus");
            if (status != null) status.gameObject.SetActive(false);

            playerMe = FindNamed(root, "playerMe");
            otherPlayer = FindNamed(root, "otherPlayer");
            ownZone = FindNamed(root, "PlayerZone4");
            backpackRoot = FindNamed(root, "选择名角背包");
            otherZones = new[]
            {
                FindNamed(root, "PlayerZone1"),
                FindNamed(root, "PlayerZone2"),
                FindNamed(root, "PlayerZone3")
            };
            CaptureOtherZones();

            if (ownZone != null) BindOwnZone(ownZone);

            if (backpackRoot != null) BindBackpack(backpackRoot);
            EnsureBackgroundDimBelowBackpack(root);
            BindCricketDetail(root);

            EnsureGreenBox();
            return timerText != null && matchButton != null;
        }

        private static void FlattenNestedOverlayCanvases(Transform root)
        {
            if (root == null) return;
            Canvas[] canvases = root.GetComponentsInChildren<Canvas>(true);
            for (int i = 0; i < canvases.Length; i++)
            {
                Canvas nested = canvases[i];
                if (nested == null || nested.transform == root) continue;
                Transform t = nested.transform;
                GraphicRaycaster raycaster = nested.GetComponent<GraphicRaycaster>();
                CanvasScaler scaler = nested.GetComponent<CanvasScaler>();
                if (raycaster != null) Object.Destroy(raycaster);
                if (scaler != null) Object.Destroy(scaler);
                Object.Destroy(nested);
                if (t.localScale.sqrMagnitude < 0.0001f)
                    t.localScale = Vector3.one;
            }
        }

        private void EnsureBackgroundDimBelowBackpack(Transform root)
        {
            Transform layers = backpackRoot != null ? backpackRoot.parent : FindNamed(root, "Layers");
            if (layers == null) return;

            Transform dim = FindNamed(root, "BackgroundDim");
            if (dim == null)
            {
                GameObject go = new GameObject("BackgroundDim", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                dim = go.transform;
                dim.SetParent(layers, false);
                RectTransform rect = dim as RectTransform;
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
                rect.pivot = new Vector2(0.5f, 0.5f);
                Image image = go.GetComponent<Image>();
                image.color = new Color(0f, 0f, 0f, 0.5882353f);
                image.raycastTarget = false;
            }
            else dim.gameObject.SetActive(true);

            Transform scenery = layers.Find("arena-background-scenery");
            int index = scenery != null ? scenery.GetSiblingIndex() + 1 : 0;
            dim.SetSiblingIndex(index);
            if (backpackRoot != null && backpackRoot.GetSiblingIndex() <= dim.GetSiblingIndex())
                backpackRoot.SetSiblingIndex(dim.GetSiblingIndex() + 1);
        }

        private void BindOwnZone(Transform zone)
        {
            readyBadge = FindNamed(zone, "ready-badge");
            ownStatusText = ReadStatusText(zone);
            if (readyBadge != null)
            {
                readyBadge.gameObject.SetActive(true);
                Transform icon = FindNamed(readyBadge, "icon");
                readyBadgeIcon = icon != null ? icon.GetComponent<Image>() : null;
            }

            Transform name = FindNamed(zone, "玩家名称")
                ?? FindNamed(zone, "玩家二")
                ?? FindNamed(zone, "PlayerName");
            ownPlayerName = name != null ? name.GetComponent<TMP_Text>() : null;
            if (ownPlayerName != null && PlayerDataService.IsLoggedIn)
                ownPlayerName.text = PlayerDataService.CurrentPlayerName;

            PlayerPalette.PaintOutline(zone, LocalPlayerId());
            PlayerPalette.BindAvatar(zone, true);
            PlayerPalette.SetMeSign(zone, true);
            BindOwnSlots(zone);
        }

        private void BindOwnSlots(Transform zone)
        {
            GameObject packPrefab = Resources.Load<GameObject>("Common/Prefabs/PackCricket");
            for (int i = 0; i < SlotCount; i++)
            {
                string slotName = "PackCricket_" + i;
                Transform slotRoot = zone.Find(slotName);
                if (slotRoot == null) slotRoot = FindNamed(zone, slotName);
                if (slotRoot == null && packPrefab != null)
                {
                    GameObject go = Object.Instantiate(packPrefab, zone, false);
                    go.name = slotName;
                    slotRoot = go.transform;
                }
                if (slotRoot == null) continue;
                slots[i] = ReadSlot(slotRoot);
                int captured = i;
                BindClick(slotRoot.gameObject, () => OnSlotClicked(captured));
            }
            RefreshSlots();
            RefreshGreenBox();
        }

        private void BindCricketDetail(Transform root)
        {
            Transform detail = null;
            for (int i = 0; i < DetailNames.Length && detail == null; i++)
                detail = FindNamed(root, DetailNames[i]);
            if (detail == null) detail = FindNamedStartsWith(root, "cricketDetail");
            if (detail == null) return;
            pageDetailView = detail.GetComponent<QuquXiangqingView>();
            if (pageDetailView == null)
                pageDetailView = detail.gameObject.AddComponent<QuquXiangqingView>();
            pageDetailView.SetCatalogMode();
            DisableRaycasts(detail);
            pageDetailView.gameObject.SetActive(false);
        }

        private void RefreshCricketDetail()
        {
            if (pageDetailView == null) return;
            CricketBackpackEntry entry = PlayerDataService.FindBackpack(detailInstanceId);
            if (entry == null)
            {
                pageDetailView.gameObject.SetActive(false);
                return;
            }

            pageDetailView.gameObject.SetActive(true);
            Color? descColor = null;
            Color skillColor;
            if (CricketCatalog.TrySkillBlurbColor(entry.quality, entry.temperament, out skillColor))
                descColor = skillColor;
            pageDetailView.SetCatalogMode();
            pageDetailView.Show(
                CricketCatalog.RankLabel(entry.quality, entry.temperament),
                CricketCatalog.CricketName(entry.quality, entry.temperament),
                CricketCatalog.Blurb(entry.quality, entry.temperament),
                CricketCatalog.Portrait(entry.quality, entry.temperament),
                CricketCatalog.TemperamentName(entry.temperament),
                CricketCatalog.PanelStatDisplays(entry.quality, entry.temperament),
                CricketCatalog.PanelStatStrongFlags(entry.temperament),
                descColor,
                entry.quality);
        }

        private static void DisableRaycasts(Transform root)
        {
            if (root == null) return;
            Graphic[] graphics = root.GetComponentsInChildren<Graphic>(true);
            for (int i = 0; i < graphics.Length; i++)
            {
                if (graphics[i] != null) graphics[i].raycastTarget = false;
            }
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
                cardViewport = sample.parent as RectTransform;
                cardTemplate = sample.gameObject;
                EnsureCardScroll();
                CollectExistingCards(cardGrid);
            }

            Transform group = FindNamed(backpack, "tab");
            if (group == null) group = FindNamed(backpack, "Group 7");
            if (group != null) BindFilterTabs(group);
        }

        private void BindFilterTabs(Transform group)
        {
            filterTabs.Clear();
            List<Transform> frames = new List<Transform>();
            for (int i = 0; i < group.childCount; i++)
            {
                Transform child = group.GetChild(i);
                string n = child.name;
                if (n.StartsWith("Frame") || n.StartsWith("tab") || n.IndexOf("PackPersonalitySwitchTab") >= 0)
                    frames.Add(child);
            }
            if (frames.Count == 0)
            {
                for (int i = 0; i < group.childCount; i++)
                    frames.Add(group.GetChild(i));
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
                if (label != null)
                {
                    label.enableWordWrapping = false;
                    label.overflowMode = TextOverflowModes.Overflow;
                    label.text = "<nobr>" + FilterLabels[i] + "</nobr>";
                }
                FilterTab tab = new FilterTab
                {
                    root = frame,
                    label = label,
                    line = FindNamed(frame, "选择线"),
                    temperament = i
                };
                filterTabs.Add(tab);
                int captured = i;
                frame.SetAsLastSibling();
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

        private void ApplyAspectLayout()
        {
            if (!bound) return;
            float pageWidth = ReadPageWidth();
            if (pageWidth <= 1f) return;
            if (Mathf.Abs(pageWidth - lastPageWidth) < 0.5f) return;
            lastPageWidth = pageWidth;
            ApplyBackpackAspect(pageWidth / DesignWidth);
            PlaceBackpackAgainstCluster();
            RefreshCards();
        }

        private void LockParentCanvasScale()
        {
            if (pageRoot == null) return;
            Canvas canvas = pageRoot.GetComponent<Canvas>();
            if (canvas == null) canvas = pageRoot.GetComponentInParent<Canvas>();
            if (canvas == null) return;
            CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler == null) return;
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(DesignWidth, DesignHeight);
            scaler.matchWidthOrHeight = 0f;
        }

        private void ApplyBackpackAspect(float widthRatio)
        {
            RectTransform bag = backpackRoot as RectTransform;
            if (bag == null) return;
            float uniformScale = Mathf.Min(1f, widthRatio);
            bag.localScale = new Vector3(uniformScale, uniformScale, 1f);
            Vector2 size = bag.sizeDelta;
            size.x = DesignWidth * Mathf.Max(1f, widthRatio);
            bag.sizeDelta = size;
            LayoutElement layout = bag.GetComponent<LayoutElement>();
            if (layout != null)
            {
                layout.minWidth = size.x;
                layout.preferredWidth = size.x;
            }
        }

        private float PageWidthRatio()
        {
            float width = ReadPageWidth();
            if (width <= 1f) return 1f;
            return width / DesignWidth;
        }

        private float ReadPageWidth()
        {
            if (pageRoot != null)
            {
                RectTransform rect = pageRoot.transform as RectTransform;
                if (rect != null && rect.rect.width > 1f) return rect.rect.width;
                Canvas canvas = pageRoot.GetComponent<Canvas>();
                if (canvas == null) canvas = pageRoot.GetComponentInParent<Canvas>();
                if (canvas != null && canvas.scaleFactor > 0.01f && canvas.pixelRect.width > 1f)
                    return canvas.pixelRect.width / canvas.scaleFactor;
            }
            if (Screen.height > 0) return DesignHeight * Screen.width / (float)Screen.height;
            return DesignWidth;
        }

        private void CaptureHomes()
        {
            if (homesCaptured) return;
            RectTransform cluster = MoveCluster() as RectTransform;
            RectTransform bagRect = backpackRoot as RectTransform;
            bool buttonInside = matchButton != null && cluster != null
                && matchButton.transform.IsChildOf(cluster);
            RectTransform readyRect = !buttonInside && matchButton != null
                ? matchButton.transform as RectTransform
                : null;
            if (cluster != null) ownZoneHome = cluster.anchoredPosition;
            if (readyRect != null) matchButtonHome = readyRect.anchoredPosition;
            moveMatchButton = readyRect != null;
            backpackFollowGap = DesignEdgeY(bagRect, true) - DesignEdgeY(cluster, false);
            homesCaptured = cluster != null;
        }

        private Transform MoveCluster()
        {
            return playerMe != null ? playerMe : ownZone;
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
            SetOthersVisible(false);
            Vector2 delta = new Vector2(0f, ExpandDelta());
            SetAnchored(MoveCluster(), ownZoneHome + delta);
            if (moveMatchButton && matchButton != null)
                SetAnchored(matchButton.transform, matchButtonHome + delta);
            PlaceBackpackAgainstCluster();
            if (cardGrid != null) cardGrid.gameObject.SetActive(true);
            RefreshFilters();
            RefreshCards();
        }

        private void ApplyCollapsed()
        {
            expanded = false;
            SetOthersVisible(true);
            SetAnchored(MoveCluster(), ownZoneHome);
            if (moveMatchButton && matchButton != null)
                SetAnchored(matchButton.transform, matchButtonHome);
            PlaceBackpackAgainstCluster();
            if (cardGrid != null) cardGrid.gameObject.SetActive(true);
        }

        private float ExpandDelta()
        {
            RectTransform others = otherPlayer as RectTransform;
            RectTransform me = MoveCluster() as RectTransform;
            if (others == null || me == null) return 0f;
            return RectTop(others, others.anchoredPosition) - RectTop(me, ownZoneHome);
        }

        private static float RectTop(RectTransform rt, Vector2 anchored)
        {
            return anchored.y + rt.rect.yMax;
        }

        private void PlaceBackpackAgainstCluster()
        {
            RectTransform bag = backpackRoot as RectTransform;
            RectTransform cluster = MoveCluster() as RectTransform;
            if (bag == null || cluster == null) return;
            RectTransform parent = bag.parent as RectTransform;
            if (parent == null || parent.rect.height < 1f) return;

            float followBottom;
            if (moveMatchButton && matchButton != null)
                followBottom = EdgeYIn(matchButton.transform as RectTransform, parent, false);
            else if (cluster.parent == parent)
                followBottom = SiblingEdgeY(cluster, false);
            else
                followBottom = EdgeYIn(cluster, parent, false);

            float dy = followBottom + backpackFollowGap - SiblingEdgeY(bag, true);
            if (Mathf.Abs(dy) < 0.01f) return;
            bag.anchoredPosition += new Vector2(0f, dy);
        }

        private static float DesignEdgeY(RectTransform rt, bool top)
        {
            if (rt == null) return 0f;
            float parentMin = -DesignHeight * 0.5f;
            float parentMax = DesignHeight * 0.5f;
            float anchor = (rt.anchorMin.y + rt.anchorMax.y) * 0.5f;
            float pivotY = Mathf.Lerp(parentMin, parentMax, anchor) + rt.anchoredPosition.y;
            return top ? pivotY + rt.rect.yMax : pivotY + rt.rect.yMin;
        }

        private static float SiblingEdgeY(RectTransform rt, bool top)
        {
            RectTransform parent = rt.parent as RectTransform;
            if (parent == null) return 0f;
            Rect pr = parent.rect;
            float anchorMin = pr.yMin + pr.height * rt.anchorMin.y;
            float anchorMax = pr.yMin + pr.height * rt.anchorMax.y;
            float pivotY = (anchorMin + anchorMax) * 0.5f + rt.anchoredPosition.y;
            float edge = top ? rt.rect.yMax : rt.rect.yMin;
            return pivotY + edge * rt.localScale.y;
        }

        private static float EdgeYIn(RectTransform rt, Transform space, bool top)
        {
            if (rt == null) return 0f;
            Vector3[] corners = new Vector3[4];
            rt.GetWorldCorners(corners);
            Vector3 world = top
                ? (corners[1] + corners[2]) * 0.5f
                : (corners[0] + corners[3]) * 0.5f;
            if (space == null) return world.y;
            return space.InverseTransformPoint(world).y;
        }

        private void OnFilterClicked(int temperament)
        {
            if (!sessionActive || ready) return;
            filterTemperament = temperament;
            RefreshFilters();
            RefreshCards();
        }

        private void OnReadyClicked()
        {
            if (!sessionActive || selectionClosed) return;
            if (TutorialDirector.BlocksHeroReady) return;
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
            if (TutorialDirector.BlocksHeroReady) return;
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
            CricketPick[] picks = CopyPicks();
            AppServices.PendingLocalPicks = picks;
            LanSession network = ActiveLanBattle();
            if (network != null)
            {
                HookBattleReady();
                network.SetSelectionReady(true, picks);
            }
            RefreshAll();
            if (network != null && network.IsBattleStarting)
                OnBattleReady();
            else if (AppServices.PendingMatchKind == MatchKind.Training)
                TryEnterBattle();
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
                network.SetSelectionReady(true, AppServices.PendingLocalPicks);
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
            // 所有真人和机器人都准备后立即开战，不再强制等待选虫倒计时。
            network.DeferBattleUntilSelectionTimeout = false;
        }

        private void ClearBattleDefer()
        {
            LanSession network = AppServices.Instance != null ? AppServices.Instance.Network : null;
            if (network == null) return;
            network.DeferBattleUntilSelectionTimeout = false;
        }

        private void LoadButtonSprites()
        {
            confirmSprite = LoadSprite("Common/Textures/绿色准备");
            cancelSprite = LoadSprite("Common/Textures/红色取消");
            readyOnSprite = LoadSprite("HeroSelection/Textures/准备icon");
            readyOffSprite = LoadSprite("HeroSelection/Textures/未准备icon");
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
            CricketBackpackEntry entry = PlayerDataService.FindBackpack(instanceId);
            if (entry == null) return;
            detailInstanceId = instanceId;
            if (SlotIndexOf(instanceId) < 0)
            {
                slotEntries[selectedSlot] = entry;
                AdvanceToNextEmptySlot();
                RefreshSlots();
                RefreshGreenBox();
            }
            RefreshCards();
            RefreshCricketDetail();
        }

        private void AdvanceToNextEmptySlot()
        {
            int need = TutorialDirector.OneSlotStart ? 1 : SlotCount;
            for (int step = 1; step <= SlotCount; step++)
            {
                int index = (selectedSlot + step) % SlotCount;
                if (index >= need) continue;
                if (slotEntries[index] == null)
                {
                    selectedSlot = index;
                    return;
                }
            }
        }

        private void RefreshAll()
        {
            RefreshChrome();
            RefreshSlots();
            RefreshGreenBox();
            RefreshFilters();
            RefreshCards();
            RefreshCricketDetail();
        }

        private void RefreshChrome()
        {
            if (expandLabel != null) expandLabel.text = expanded ? "收起" : "展开背包";
            if (readyBadge != null) readyBadge.gameObject.SetActive(true);
            ApplyReadyStatus(ownStatusText, readyBadgeIcon, ready);
            if (matchButtonText != null) matchButtonText.text = ready ? LabelCancel : LabelConfirm;
            if (matchButtonImage != null)
            {
                Sprite sprite = ready ? cancelSprite : confirmSprite;
                if (sprite != null)
                {
                    matchButtonImage.sprite = sprite;
                    matchButtonImage.color = Color.white;
                }
            }
            if (matchButton != null) matchButton.interactable = sessionActive && !selectionClosed;
            if (ownPlayerName != null && PlayerDataService.IsLoggedIn)
                ownPlayerName.text = PlayerDataService.CurrentPlayerName;
        }

        private void RefreshSlots()
        {
            if (emptyPackBackground == null)
                emptyPackBackground = LoadSprite("Common/Textures/PackCricketBg-unSelected");
            for (int i = 0; i < SlotCount; i++)
            {
                SlotView slot = slots[i];
                if (slot == null) continue;
                CricketBackpackEntry entry = slotEntries[i];
                bool filled = entry != null;
                if (slot.badgeRoot != null)
                {
                    slot.badgeRoot.SetActive(filled);
                    if (filled)
                        CricketCatalog.ApplyQualityLabel(slot.badgeRoot.GetComponent<Image>(), entry.quality);
                }
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
                        slot.portrait.sprite = CricketCatalog.Portrait(entry.quality, entry.temperament);
                        CricketCatalog.FitPackPortrait(slot.portrait);
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
                    tab.label.color = TabOn;
                    tab.label.fontStyle = on ? FontStyles.Bold : FontStyles.Normal;
                }
            }
        }

        private void RefreshCards()
        {
            if (cardGrid == null || cardTemplate == null) return;
            List<CricketBackpackEntry> entries = FilteredBackpack();
            EnsureCardCount(entries.Count);
            UpdateCardScrollContent(entries.Count);
            for (int i = 0; i < cards.Count; i++)
            {
                CardView card = cards[i];
                if (card == null || card.root == null) continue;
                if (i >= entries.Count)
                {
                    card.root.SetActive(false);
                    card.instanceId = null;
                    ApplyCardMarks(card, false, -1);
                    continue;
                }
                CricketBackpackEntry entry = entries[i];
                card.root.SetActive(true);
                card.instanceId = entry.instanceId;
                PlaceCard(card.root.transform as RectTransform, i);
                if (card.quality != null) card.quality.text = CricketCatalog.QualityName(entry.quality);
                CricketCatalog.ApplyQualityLabel(card.badge, entry.quality);
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
                    CricketCatalog.FitPackPortrait(card.portrait);
                }
                int slotIndex = SlotIndexOf(entry.instanceId);
                bool current = !ready && entry.instanceId == detailInstanceId;
                ApplyCardMarks(card, current, slotIndex);
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

        private void EnsureCardScroll()
        {
            if (cardViewport == null) return;

            cardScroll = cardViewport.GetComponent<ScrollRect>();
            Transform existingContent = null;
            if (cardScroll != null && cardScroll.content != null)
                existingContent = cardScroll.content;

            if (existingContent == null)
            {
                GameObject contentObject = new GameObject("CardScrollContent", typeof(RectTransform));
                contentObject.transform.SetParent(cardViewport, false);
                RectTransform contentRect = contentObject.transform as RectTransform;
                contentRect.anchorMin = new Vector2(0f, 1f);
                contentRect.anchorMax = new Vector2(0f, 1f);
                contentRect.pivot = new Vector2(0f, 1f);
                contentRect.anchoredPosition = Vector2.zero;
                contentRect.sizeDelta = cardViewport.rect.size;

                List<Transform> children = new List<Transform>();
                for (int i = 0; i < cardViewport.childCount; i++)
                {
                    Transform child = cardViewport.GetChild(i);
                    if (child != contentObject.transform) children.Add(child);
                }
                for (int i = 0; i < children.Count; i++)
                    children[i].SetParent(contentObject.transform, false);
                existingContent = contentObject.transform;
            }

            cardGrid = existingContent;
            cardScroll = cardViewport.GetComponent<ScrollRect>();
            if (cardScroll == null) cardScroll = cardViewport.gameObject.AddComponent<ScrollRect>();
            cardScroll.viewport = cardViewport;
            cardScroll.content = cardGrid as RectTransform;
            cardScroll.horizontal = false;
            cardScroll.vertical = true;
            cardScroll.movementType = ScrollRect.MovementType.Clamped;
            cardScroll.inertia = true;
            cardScroll.scrollSensitivity = 32f;

            Image viewportGraphic = cardViewport.GetComponent<Image>();
            if (viewportGraphic == null) viewportGraphic = cardViewport.gameObject.AddComponent<Image>();
            viewportGraphic.color = new Color(1f, 1f, 1f, 0f);
            viewportGraphic.raycastTarget = true;
            if (cardViewport.GetComponent<RectMask2D>() == null)
                cardViewport.gameObject.AddComponent<RectMask2D>();
        }

        private void UpdateCardScrollContent(int entryCount)
        {
            if (cardGrid == null) return;
            RectTransform content = cardGrid as RectTransform;
            if (content == null) return;
            const float card = 288f;
            const float topPad = 66f;
            const float vGap = 21f;
            const float bottomScrollPad = 180f;
            int rows = Mathf.Max(1, Mathf.CeilToInt(Mathf.Max(1, entryCount) / 3f));
            // Leave room below the last row so it can be dragged fully into view.
            float height = topPad + rows * card + Mathf.Max(0, rows - 1) * vGap + 40f + bottomScrollPad;
            float width = DesignWidth * Mathf.Max(1f, PageWidthRatio());
            content.sizeDelta = new Vector2(width, Mathf.Max(cardViewport != null ? cardViewport.rect.height : 795f, height));
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
            int need = TutorialDirector.OneSlotStart ? 1 : SlotCount;
            for (int i = 0; i < need; i++)
                if (slotEntries[i] == null) return false;
            return true;
        }

        private void SetOthersVisible(bool visible)
        {
            if (otherPlayer != null)
            {
                otherPlayer.gameObject.SetActive(visible);
                return;
            }
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
            Transform name = FindNamed(zone, "玩家二")
                ?? FindNamed(zone, "玩家名称")
                ?? FindNamed(zone, "PlayerName");
            view.playerName = name != null ? name.GetComponent<TMP_Text>() : null;
            view.homeName = view.playerName != null ? view.playerName.text : string.Empty;
            view.status = ReadStatusText(zone);
            Transform badge = FindNamed(zone, "ready-badge");
            Transform icon = badge != null ? FindNamed(badge, "icon") : null;
            view.icon = icon != null ? icon.GetComponent<Image>() : null;
            PlayerPalette.BindAvatar(zone, true);
            return view;
        }

        private void ApplyOtherZones()
        {
            if (otherZoneViews == null) return;
            int localId = LocalPlayerId();
            if (ownZone != null)
            {
                PlayerPalette.PaintOutline(ownZone, localId);
                PlayerPalette.BindAvatar(ownZone, true);
                PlayerPalette.SetMeSign(ownZone, true);
            }

            if (AppServices.PendingMatchKind == MatchKind.Training)
            {
                for (int i = 0; i < otherZoneViews.Length; i++)
                {
                    int playerId = i >= localId ? i + 1 : i;
                    PaintOtherZone(otherZoneViews[i], TrainingCamp.BotName(i), true, playerId);
                }
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
                int playerId = i >= localId ? i + 1 : i;
                PaintOtherZone(view, view != null ? view.homeName : string.Empty, false, playerId);
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
                    PaintOtherZone(otherZoneViews[zone], playerName, confirmed, i);
                    zone++;
                }
            }
            for (; zone < otherZoneViews.Length; zone++)
            {
                OtherZoneView view = otherZoneViews[zone];
                PaintOtherZone(view, view != null ? view.homeName : string.Empty, false, -1);
            }
        }

        private void PaintOtherZone(OtherZoneView zone, string playerName, bool confirmed, int playerId)
        {
            if (zone == null) return;
            if (zone.playerName != null && !string.IsNullOrEmpty(playerName))
                zone.playerName.text = playerName;
            ApplyReadyStatus(zone.status, zone.icon, confirmed);
            if (playerId >= 0 && zone.root != null)
                PlayerPalette.PaintOutline(zone.root, playerId);
        }

        private void ApplyReadyStatus(TMP_Text label, Image icon, bool confirmed)
        {
            if (label != null)
            {
                label.text = confirmed ? StatusConfirmed : StatusSelecting;
                label.color = confirmed ? StatusOn : StatusOff;
            }
            if (icon == null) return;
            Sprite sprite = confirmed ? readyOnSprite : readyOffSprite;
            if (sprite != null) icon.sprite = sprite;
        }

        static int LocalPlayerId()
        {
            LanSession net = AppServices.Instance != null ? AppServices.Instance.Network : null;
            return net != null && net.LocalPlayerId >= 0 ? net.LocalPlayerId : 0;
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
            greenBox.SetParent(slot, false);
            greenBox.anchorMin = Vector2.zero;
            greenBox.anchorMax = Vector2.one;
            greenBox.pivot = new Vector2(0.5f, 0.5f);
            greenBox.offsetMin = new Vector2(-12f, -12f);
            greenBox.offsetMax = new Vector2(12f, 12f);
            greenBox.localScale = Vector3.one;
            greenBox.localRotation = Quaternion.identity;
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
            Transform badge = FindNamed(root.transform, "品级");
            card.badge = badge != null ? badge.GetComponent<Image>() : null;
            Transform name = FindNamed(root.transform, "白头狮");
            card.name = name != null ? name.GetComponent<TMP_Text>() : null;
            Transform portrait = FindNamed(root.transform, "头像");
            card.portrait = portrait != null ? portrait.GetComponent<Image>() : null;
            Transform bg = FindNamed(root.transform, "背景") ?? FindNamed(root.transform, "Rectangle 11");
            card.background = bg != null ? bg.GetComponent<Image>() : null;
            Transform selectedMask = FindNamed(root.transform, "选中的蛐蛐遮罩");
            if (selectedMask != null)
            {
                selectedMask.SetAsFirstSibling();
                selectedMask.gameObject.SetActive(false);
                card.selectedMask = selectedMask.gameObject;
            }
            EnsureSlotIndex(card);
            card.group = root.GetComponent<CanvasGroup>();
            if (card.group == null) card.group = root.AddComponent<CanvasGroup>();
            return card;
        }

        private static void EnsureSlotIndex(CardView card)
        {
            if (card == null || card.root == null) return;
            Transform existing = FindNamed(card.root.transform, "SlotIndex");
            if (existing == null)
            {
                GameObject prefab = Resources.Load<GameObject>(SlotIndexPrefabPath);
                if (prefab != null)
                {
                    GameObject go = Object.Instantiate(prefab, card.root.transform, false);
                    go.name = "SlotIndex";
                    FlattenNestedOverlayCanvases(go.transform);
                    existing = go.transform;
                    PlaceSlotIndex(existing as RectTransform);
                }
            }
            if (existing == null)
            {
                GameObject go = new GameObject("SlotIndex", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                existing = go.transform;
                existing.SetParent(card.root.transform, false);
                PlaceSlotIndex(existing as RectTransform);
                Image plate = go.GetComponent<Image>();
                Sprite circle = LoadSprite(SlotIndexSpritePath);
                plate.sprite = circle != null ? circle : WhiteSprite();
                plate.color = Color.white;
                plate.preserveAspect = true;
                plate.raycastTarget = false;
            }
            card.slotIndexRoot = existing.gameObject;
            card.slotIndexRoot.SetActive(false);

            Transform label = FindNamed(existing, "SlotIndexText") ?? FindNamed(existing, "index");
            if (label == existing) label = null;
            if (label == null)
            {
                TMP_Text nested = existing.GetComponentInChildren<TMP_Text>(true);
                if (nested != null) label = nested.transform;
            }
            bool createdText = false;
            if (label == null)
            {
                GameObject textGo = new GameObject("SlotIndexText", typeof(RectTransform), typeof(TextMeshProUGUI));
                label = textGo.transform;
                label.SetParent(existing, false);
                RectTransform textRect = label as RectTransform;
                textRect.anchorMin = Vector2.zero;
                textRect.anchorMax = Vector2.one;
                textRect.offsetMin = Vector2.zero;
                textRect.offsetMax = Vector2.zero;
                createdText = true;
            }
            card.slotIndexText = label.GetComponent<TMP_Text>();
            if (card.slotIndexText == null) return;
            card.slotIndexText.raycastTarget = false;
            if (!createdText) return;
            UiFonts.Apply(card.slotIndexText);
            card.slotIndexText.fontSize = 36f;
            card.slotIndexText.fontStyle = FontStyles.Bold;
            card.slotIndexText.alignment = TextAlignmentOptions.Center;
            card.slotIndexText.color = Color.white;
            card.slotIndexText.enableWordWrapping = false;
            card.slotIndexText.overflowMode = TextOverflowModes.Overflow;
        }

        private static void PlaceSlotIndex(RectTransform rect)
        {
            if (rect == null) return;
            Vector2 size = rect.sizeDelta;
            if (size.x < 1f || size.y < 1f) size = new Vector2(90f, 90f);
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.sizeDelta = size;
            rect.anchoredPosition = new Vector2(-4f, -4f);
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
        }

        private static void ApplyCardMarks(CardView card, bool current, int slotIndex)
        {
            if (card == null) return;
            if (card.selectedMask != null)
            {
                card.selectedMask.transform.SetAsFirstSibling();
                card.selectedMask.SetActive(current);
                Image maskImage = card.selectedMask.GetComponent<Image>();
                if (maskImage != null)
                {
                    maskImage.color = Color.white;
                    maskImage.raycastTarget = false;
                }
                if (card.group != null) card.group.alpha = 1f;
            }
            else if (card.group != null)
                card.group.alpha = current ? 0.45f : 1f;
            if (card.slotIndexRoot != null)
            {
                bool showIndex = slotIndex >= 0;
                card.slotIndexRoot.SetActive(showIndex);
                if (showIndex) card.slotIndexRoot.transform.SetAsLastSibling();
            }
            if (card.slotIndexText != null)
                card.slotIndexText.text = slotIndex >= 0 ? (slotIndex + 1).ToString() : string.Empty;
        }

        private void PlaceCard(RectTransform rect, int index)
        {
            if (rect == null) return;
            const float card = 288f;
            const float topPad = 66f;
            const float vGap = 21f;
            int col = index % 3;
            int row = index / 3;
            float left = col == 0 ? 67f : col == 1 ? 396f : 705f;
            float spacingScale = Mathf.Max(1f, PageWidthRatio());
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(card, card);
            float x = (left + card * 0.5f) * spacingScale;
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

        private static Transform FindNamedStartsWith(Transform root, string prefix)
        {
            if (root == null || string.IsNullOrEmpty(prefix)) return null;
            if (root.name.StartsWith(prefix, System.StringComparison.Ordinal)) return root;
            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                if (transforms[i] != root && transforms[i].name.StartsWith(prefix, System.StringComparison.Ordinal))
                    return transforms[i];
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
            public Image icon;
            public string homeName;
        }

        private sealed class CardView
        {
            public GameObject root;
            public Image background;
            public Image portrait;
            public TMP_Text quality;
            public TMP_Text name;
            public Image badge;
            public GameObject selectedMask;
            public GameObject slotIndexRoot;
            public TMP_Text slotIndexText;
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
