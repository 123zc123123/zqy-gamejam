using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DouQuqu
{
    /// <summary>合成盘上的背包：用选虫页同一套「选择名角背包」，默认展开。</summary>
    public sealed class MergeBackpackPanel : MonoBehaviour
    {
        private const string PrefabPath = "HeroSelection/Prefabs/Parts/选择名角背包";
        private static readonly string[] FilterLabels = { "全部", "沉稳", "强攻", "灵巧", "智谋" };
        private static readonly Color TabOn = new Color(0.96f, 0.90f, 0.62f, 1f);
        private static readonly Color TabOff = new Color(0.78f, 0.74f, 0.62f, 0.72f);

        private GameObject overlay;
        private Transform backpackRoot;
        private Transform cardGrid;
        private GameObject cardTemplate;
        private TMP_Text expandLabel;
        private readonly List<CardView> cards = new List<CardView>();
        private readonly List<FilterTab> filterTabs = new List<FilterTab>();
        private Sprite[] qualitySprites;
        private int filterTemperament;
        private System.Action<CricketBackpackEntry> onCardInspect;

        public bool IsOpen => overlay != null && overlay.activeSelf;

        public void Refresh()
        {
            if (overlay == null || !overlay.activeSelf) return;
            RefreshFilters();
            RefreshCards();
        }

        public void Show(System.Action<CricketBackpackEntry> inspect)
        {
            onCardInspect = inspect;
            EnsureOverlay();
            if (overlay == null) return;
            overlay.SetActive(true);
            filterTemperament = 0;
            if (expandLabel != null) expandLabel.text = "收起";
            if (cardGrid != null) cardGrid.gameObject.SetActive(true);
            RefreshFilters();
            RefreshCards();
        }

        public void Hide()
        {
            if (overlay != null) overlay.SetActive(false);
        }

        private void EnsureOverlay()
        {
            if (overlay != null) return;
            GameObject prefab = Resources.Load<GameObject>(PrefabPath);
            if (prefab == null)
            {
                Debug.LogWarning("[DouQuqu] 找不到选虫背包 " + PrefabPath);
                return;
            }

            overlay = new GameObject("MergeBackpackOverlay", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = overlay.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 360;
            CanvasScaler scaler = overlay.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 1f;
            RectTransform overlayRect = overlay.GetComponent<RectTransform>();
            overlayRect.anchorMin = Vector2.zero;
            overlayRect.anchorMax = Vector2.one;
            overlayRect.offsetMin = Vector2.zero;
            overlayRect.offsetMax = Vector2.zero;

            GameObject dim = new GameObject("Dimmer", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            RectTransform dimRect = dim.GetComponent<RectTransform>();
            dimRect.SetParent(overlayRect, false);
            dimRect.anchorMin = Vector2.zero;
            dimRect.anchorMax = Vector2.one;
            dimRect.offsetMin = Vector2.zero;
            dimRect.offsetMax = Vector2.zero;
            Image dimImage = dim.GetComponent<Image>();
            dimImage.color = new Color(0.05f, 0.03f, 0.02f, 0.55f);
            Button dimButton = dim.GetComponent<Button>();
            dimButton.transition = Selectable.Transition.None;
            dimButton.onClick.AddListener(Hide);

            GameObject bag = Instantiate(prefab, overlayRect, false);
            bag.name = "选择名角背包";
            backpackRoot = bag.transform;
            RectTransform bagRect = bag.GetComponent<RectTransform>();
            if (bagRect != null)
            {
                bagRect.anchorMin = new Vector2(0.5f, 0f);
                bagRect.anchorMax = new Vector2(0.5f, 0f);
                bagRect.pivot = new Vector2(0.5f, 0f);
                bagRect.anchoredPosition = new Vector2(0f, 40f);
                bagRect.localScale = Vector3.one;
            }

            BindBackpack(backpackRoot);
            LoadQualitySprites();
        }

        private void BindBackpack(Transform backpack)
        {
            Transform expand = FindNamed(backpack, "展开背包");
            if (expand != null)
            {
                expandLabel = expand.GetComponent<TMP_Text>();
                BindClick(expand.gameObject, Hide);
            }
            Transform bar = FindNamed(backpack, "Rectangle 12");
            if (bar != null) BindClick(bar.gameObject, Hide);

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
                FilterTab tab = new FilterTab { root = frame, label = label, temperament = i };
                filterTabs.Add(tab);
                int captured = i;
                BindClick(frame.gameObject, () =>
                {
                    filterTemperament = captured;
                    RefreshFilters();
                    RefreshCards();
                });
            }
        }

        private void CollectExistingCards(Transform grid)
        {
            cards.Clear();
            for (int i = 0; i < grid.childCount; i++)
            {
                Transform child = grid.GetChild(i);
                if (child.name.IndexOf("极品") < 0) continue;
                cards.Add(ReadCard(child.gameObject));
            }
        }

        private void RefreshFilters()
        {
            for (int i = 0; i < filterTabs.Count; i++)
            {
                FilterTab tab = filterTabs[i];
                bool on = tab.temperament == filterTemperament;
                if (tab.label == null) continue;
                tab.label.color = on ? TabOn : TabOff;
                tab.label.fontStyle = on ? FontStyles.Bold : FontStyles.Normal;
            }
        }

        private void RefreshCards()
        {
            if (cardGrid == null || cardTemplate == null) return;
            List<CricketBackpackEntry> entries = FilteredBackpack();
            while (cards.Count < entries.Count)
            {
                GameObject clone = Instantiate(cardTemplate, cardGrid, false);
                clone.name = "极品_1";
                clone.SetActive(true);
                cards.Add(ReadCard(clone));
            }
            for (int i = 0; i < cards.Count; i++)
            {
                CardView card = cards[i];
                if (card == null || card.root == null) continue;
                if (i >= entries.Count)
                {
                    card.root.SetActive(false);
                    continue;
                }
                CricketBackpackEntry entry = entries[i];
                card.root.SetActive(true);
                PlaceCard(card.root.transform as RectTransform, i);
                if (card.quality != null) card.quality.text = CricketCatalog.QualityName(entry.quality);
                if (card.name != null) card.name.text = CricketCatalog.CricketName(entry.quality, entry.temperament);
                if (card.portrait != null)
                {
                    card.portrait.sprite = SpriteFor(entry.quality, entry.temperament);
                    card.portrait.enabled = card.portrait.sprite != null;
                    card.portrait.preserveAspect = true;
                    card.portrait.color = Color.white;
                }
                CricketBackpackEntry captured = entry;
                BindClick(card.root, () =>
                {
                    if (onCardInspect != null) onCardInspect(captured);
                });
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
            int index = (Mathf.Clamp(quality, 1, 4) - 1) * 4 + (Mathf.Clamp(temperament, 1, 4) - 1);
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

        private static CardView ReadCard(GameObject root)
        {
            CardView card = new CardView { root = root };
            Transform quality = FindNamed(root.transform, "极品");
            card.quality = quality != null ? quality.GetComponent<TMP_Text>() : null;
            Transform name = FindNamed(root.transform, "白头狮");
            card.name = name != null ? name.GetComponent<TMP_Text>() : null;
            Transform portrait = FindNamed(root.transform, "头像");
            card.portrait = portrait != null ? portrait.GetComponent<Image>() : null;
            return card;
        }

        private static void PlaceCard(RectTransform rect, int index)
        {
            if (rect == null) return;
            int col = index % 4;
            int row = index / 4;
            rect.anchoredPosition = new Vector2(117.63f + col * 250.25f, -127.5f + row * -270f);
        }

        private static void BindClick(GameObject go, UnityEngine.Events.UnityAction clicked)
        {
            if (go == null) return;
            Graphic graphic = go.GetComponent<Graphic>();
            if (graphic == null)
            {
                Image image = go.AddComponent<Image>();
                image.color = new Color(1f, 1f, 1f, 0.01f);
                image.raycastTarget = true;
                graphic = image;
            }
            else graphic.raycastTarget = true;
            Button button = go.GetComponent<Button>();
            if (button == null) button = go.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.targetGraphic = graphic;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(clicked);
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

        private sealed class CardView
        {
            public GameObject root;
            public TMP_Text quality;
            public TMP_Text name;
            public Image portrait;
        }

        private sealed class FilterTab
        {
            public Transform root;
            public TMP_Text label;
            public int temperament;
        }
    }
}
