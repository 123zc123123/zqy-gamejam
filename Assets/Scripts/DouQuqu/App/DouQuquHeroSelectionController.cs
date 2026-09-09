using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DouQuqu
{
    /// <summary>选虫页己方三槽：背包、绿框、点卡入槽、准备锁定。匹配在进战页。</summary>
    public sealed class DouQuquHeroSelectionController : MonoBehaviour
    {
        public const int SelectSeconds = 59;
        private const int SlotCount = 3;
        private static readonly Color GreenFill = new Color(0.28f, 0.92f, 0.34f, 0.22f);
        private static readonly Color GreenLine = new Color(0.22f, 0.86f, 0.30f, 1f);
        private static readonly Color TabOn = new Color(0.96f, 0.90f, 0.62f, 1f);
        private static readonly Color TabOff = new Color(0.78f, 0.74f, 0.62f, 0.72f);
        private static readonly string[] FilterLabels = { "全部", "沉稳", "强攻", "灵巧", "智谋" };

        private GameObject pageRoot;
        private TMP_Text timerText;
        private Button matchButton;
        private TMP_Text matchButtonText;
        private TMP_Text expandLabel;
        private Transform ownZone;
        private Transform backpackRoot;
        private Transform cardGrid;
        private Transform[] otherZones;
        private Transform readyBadge;
        private RectTransform greenBox;
        private Sprite[] qualitySprites;

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
            DouQuquBottomNavBar.SuppressEmbedded(root.transform);
            if (!TryBindArt(root.transform))
                BuildOverlay(root.transform);
            CaptureHomes();
            LoadQualitySprites();
        }

        public void BeginSession()
        {
            if (!bound) return;
            sessionActive = true;
            ready = false;
            expanded = false;
            selectedSlot = 0;
            filterTemperament = 0;
            for (int i = 0; i < SlotCount; i++) slotEntries[i] = null;
            deadlineUnscaled = Time.unscaledTime + SelectSeconds;
            ApplyCollapsed();
            RefreshAll();
        }

        public void CancelSelection()
        {
            sessionActive = false;
            ApplyCollapsed();
        }

        private void Update()
        {
            if (!sessionActive || timerText == null) return;
            int remain = Mathf.Max(0, Mathf.CeilToInt(deadlineUnscaled - Time.unscaledTime));
            timerText.text = remain.ToString();
            if (remain > 0 || ready) return;
            AutoFillAndReady();
        }

        private bool TryBindArt(Transform root)
        {
            timerText = FindTmp(root, "59") ?? FindTmp(root, "MatchTimerTMP");
            matchButton = FindButton(root, "MatchButton") ?? FindButton(root, "btn-ready");
            if (matchButton != null)
            {
                BindClick(matchButton.gameObject, OnReadyClicked);
                matchButtonText = matchButton.GetComponentInChildren<TMP_Text>(true);
                if (matchButtonText != null) matchButtonText.text = "准备就绪";
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
            if (readyBadge != null) readyBadge.gameObject.SetActive(false);

            Transform name = FindNamed(zone, "玩家二") ?? FindNamed(zone, "PlayerName");
            if (name != null)
            {
                TMP_Text label = name.GetComponent<TMP_Text>();
                if (label != null && DouQuquPlayerDataService.IsLoggedIn)
                    label.text = DouQuquPlayerDataService.CurrentPlayerName;
            }

            Transform frame = FindNamed(zone, "Frame 6");
            if (frame == null) return;
            int index = 0;
            for (int i = 0; i < frame.childCount && index < SlotCount; i++)
            {
                Transform child = frame.GetChild(i);
                slots[index] = ReadSlot(child);
                int captured = index;
                BindClick(child.gameObject, () => OnSlotClicked(captured));
                index++;
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
                if (child.name.StartsWith("Frame")) frames.Add(child);
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
            RectTransform panel = DouQuquUiFactory.CreatePanel(parent, "HeroSelectionPanel",
                new Vector2(0.08f, 0.08f), new Vector2(0.92f, 0.52f), Vector2.zero, Vector2.zero);
            timerText = DouQuquUiFactory.CreateText(panel, "MatchTimerTMP", SelectSeconds.ToString(), 32f,
                new Vector2(0.08f, 0.70f), new Vector2(0.92f, 0.82f), Vector2.zero, Vector2.zero);
            DouQuquUiFactory.CreateText(panel, "Hint", "选虫页美术未绑上，无法选虫。", 26f,
                new Vector2(0.08f, 0.40f), new Vector2(0.92f, 0.68f), Vector2.zero, Vector2.zero);
            matchButton = DouQuquUiFactory.CreateButton(panel, "MatchButton", "准备就绪", OnReadyClicked,
                new Vector2(0.10f, 0.14f), new Vector2(0.48f, 0.26f), Vector2.zero, Vector2.zero);
            matchButtonText = matchButton.GetComponentInChildren<TMP_Text>();
            DouQuquUiFactory.CreateButton(panel, "BackButton", "返回", Back,
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
            if (cardGrid != null) cardGrid.gameObject.SetActive(false);
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
            if (!sessionActive || ready) return;
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
            RefreshAll();
            DouQuquAppServices.PendingLocalPicks = CopyPicks();
            DouQuquSceneNames.Load(DouQuquSceneNames.Battle);
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
            CricketBackpackEntry entry = DouQuquPlayerDataService.FindBackpack(instanceId);
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
            if (readyBadge != null) readyBadge.gameObject.SetActive(ready);
            if (matchButton != null) matchButton.interactable = sessionActive && !ready;
            if (ownZone != null)
            {
                Transform name = FindNamed(ownZone, "玩家二");
                TMP_Text label = name != null ? name.GetComponent<TMP_Text>() : null;
                if (label != null && DouQuquPlayerDataService.IsLoggedIn)
                    label.text = DouQuquPlayerDataService.CurrentPlayerName;
            }
        }

        private void RefreshSlots()
        {
            for (int i = 0; i < SlotCount; i++)
            {
                SlotView slot = slots[i];
                if (slot == null) continue;
                CricketBackpackEntry entry = slotEntries[i];
                bool filled = entry != null;
                if (slot.badgeRoot != null) slot.badgeRoot.SetActive(filled);
                if (slot.quality != null)
                    slot.quality.text = filled ? DouQuquCricketCatalog.QualityName(entry.quality) : string.Empty;
                if (slot.name != null)
                    slot.name.text = filled ? DouQuquCricketCatalog.CricketName(entry.quality, entry.temperament) : string.Empty;
                if (slot.portrait != null)
                {
                    slot.portrait.enabled = true;
                    slot.portrait.preserveAspect = true;
                    if (filled)
                    {
                        Sprite sprite = SpriteFor(entry.quality, entry.temperament);
                        slot.portrait.sprite = sprite;
                        slot.portrait.color = Color.white;
                    }
                    else
                    {
                        slot.portrait.sprite = null;
                        slot.portrait.color = new Color(0.55f, 0.55f, 0.48f, 0.45f);
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
                if (card.quality != null) card.quality.text = DouQuquCricketCatalog.QualityName(entry.quality);
                if (card.name != null) card.name.text = DouQuquCricketCatalog.CricketName(entry.quality, entry.temperament);
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
            List<CricketBackpackEntry> all = DouQuquPlayerDataService.GetBackpackSnapshot();
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
            List<CricketBackpackEntry> all = DouQuquPlayerDataService.GetBackpackSnapshot();
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

        private void EnsureGreenBox()
        {
            if (greenBox != null) return;
            GameObject go = new GameObject("GreenBox", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            greenBox = go.GetComponent<RectTransform>();
            Image image = go.GetComponent<Image>();
            image.sprite = WhiteSprite();
            image.color = GreenFill;
            image.raycastTarget = false;
            Outline outline = go.AddComponent<Outline>();
            outline.effectColor = GreenLine;
            outline.effectDistance = new Vector2(5f, -5f);
            go.SetActive(false);
        }

        private void PlaceGreenBox(Transform slot)
        {
            greenBox.SetParent(slot, false);
            greenBox.anchorMin = Vector2.zero;
            greenBox.anchorMax = Vector2.one;
            greenBox.offsetMin = new Vector2(-6f, -6f);
            greenBox.offsetMax = new Vector2(6f, 6f);
            greenBox.localScale = Vector3.one;
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
            Transform badge = FindNamed(root, "Frame");
            slot.badgeRoot = badge != null ? badge.gameObject : null;
            Transform quality = FindNamed(root, "四品");
            slot.quality = quality != null ? quality.GetComponent<TMP_Text>() : null;
            Transform name = FindNamed(root, "青牙王");
            slot.name = name != null ? name.GetComponent<TMP_Text>() : root.GetComponentInChildren<TMP_Text>(true);
            Transform portrait = FindNamed(root, "Rectangle");
            slot.portrait = portrait != null ? portrait.GetComponent<Image>() : root.GetComponent<Image>();
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
            card.group = root.GetComponent<CanvasGroup>();
            if (card.group == null) card.group = root.AddComponent<CanvasGroup>();
            return card;
        }

        private static void PlaceCard(RectTransform rect, int index)
        {
            if (rect == null) return;
            const float originX = 117.63f;
            const float originY = -127.5f;
            const float stepX = 250.25f;
            const float stepY = -270f;
            int col = index % 4;
            int row = index / 4;
            rect.anchoredPosition = new Vector2(originX + col * stepX, originY + row * stepY);
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

        private static Button FindButton(Transform root, string objectName)
        {
            Transform found = FindNamed(root, objectName);
            return found != null ? found.GetComponent<Button>() : null;
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
            DouQuquLobby.Show(DouQuquLobby.Page.BattleEnter);
        }

        private sealed class SlotView
        {
            public Transform root;
            public Image portrait;
            public TMP_Text quality;
            public TMP_Text name;
            public GameObject badgeRoot;
        }

        private sealed class CardView
        {
            public GameObject root;
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
            public int temperament;
        }
    }
}
