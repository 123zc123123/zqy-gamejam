using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using ZqyGameJam.UI.QuquXiangqing;

namespace DouQuqu
{
    /// <summary>图鉴页：绑 collection Prefab，点卡片弹出详情。</summary>
    public sealed class DouQuquCollectionController : MonoBehaviour
    {
        private const int CatalogSize = 16;

        private TMP_Text countText;
        private bool bound;
        private readonly CardSlot[] cards = new CardSlot[CatalogSize];
        private Sprite[] qualitySprites;
        private GameObject xiangqingPrefab;
        private QuquXiangqingView detailView;

        private sealed class CardSlot
        {
            public int quality;
            public int temperament;
            public CanvasGroup group;
            public Button button;
        }

        public void BindPage(GameObject pageRoot)
        {
            if (pageRoot == null) return;
            bound = true;
            DouQuquBottomNavBar.SuppressEmbedded(pageRoot.transform);
            countText = FindCountLabel(pageRoot.transform);
            BindCards(pageRoot.transform);
            LoadQualitySprites();
            if (xiangqingPrefab == null)
                xiangqingPrefab = Resources.Load<GameObject>(QuquXiangqingView.PrefabResourcePath);
            RefreshCollection();
        }

        private void Start()
        {
            if (!DouQuquPlayerDataService.RequireLogin()) return;
            if (!bound) BuildUi();
            RefreshCollection();
        }

        private void OnEnable()
        {
            DouQuquPlayerDataService.PlayerDataChanged += RefreshCollection;
        }

        private void OnDisable()
        {
            DouQuquPlayerDataService.PlayerDataChanged -= RefreshCollection;
        }

        private void BuildUi()
        {
            RectTransform root = DouQuquUiFactory.CreateScreen("CollectionCanvas");
            RectTransform panel = DouQuquUiFactory.CreatePanel(root, "CollectionPanel",
                new Vector2(0.16f, 0.18f), new Vector2(0.84f, 0.92f), Vector2.zero, Vector2.zero);
            DouQuquUiFactory.CreateText(panel, "Title", "蟋蟀图鉴", 58f,
                new Vector2(0.08f, 0.84f), new Vector2(0.92f, 0.96f), Vector2.zero, Vector2.zero);
            DouQuquBottomNavBar.EnsureOn(root);
        }

        private void BindCards(Transform root)
        {
            List<Transform> found = new List<Transform>();
            CollectNamed(root, "CricketCard", found);
            int count = Mathf.Min(CatalogSize, found.Count);
            for (int i = 0; i < count; i++)
            {
                int quality = i / 4 + 1;
                int temperament = i % 4 + 1;
                cards[i] = MakeSlot(found[i].gameObject, quality, temperament);
            }
        }

        private CardSlot MakeSlot(GameObject root, int quality, int temperament)
        {
            CanvasGroup group = root.GetComponent<CanvasGroup>();
            if (group == null) group = root.AddComponent<CanvasGroup>();
            group.blocksRaycasts = true;
            group.interactable = true;

            Image hit = root.GetComponent<Image>();
            if (hit == null) hit = root.AddComponent<Image>();
            hit.color = new Color(1f, 1f, 1f, 0.01f);
            hit.raycastTarget = true;

            Button button = root.GetComponent<Button>();
            if (button == null) button = root.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.targetGraphic = hit;
            button.onClick.RemoveAllListeners();
            int capturedQuality = quality;
            int capturedTemperament = temperament;
            button.onClick.AddListener(() => OpenDetail(capturedQuality, capturedTemperament));

            return new CardSlot
            {
                quality = quality,
                temperament = temperament,
                group = group,
                button = button
            };
        }

        private void OpenDetail(int quality, int temperament)
        {
            if (!EnsureDetailView()) return;
            detailView.SetCatalogMode();
            detailView.Show(
                DouQuquCricketCatalog.QualityName(quality),
                DouQuquCricketCatalog.CricketName(quality, temperament),
                DouQuquCricketCatalog.Blurb(temperament),
                SpriteFor(quality, temperament),
                DouQuquCricketCatalog.TemperamentName(temperament),
                DouQuquCricketCatalog.PanelStatDisplays(quality, temperament),
                DouQuquCricketCatalog.PanelStatStrongFlags(temperament));
        }

        private bool EnsureDetailView()
        {
            if (detailView != null) return true;
            if (xiangqingPrefab == null)
                xiangqingPrefab = Resources.Load<GameObject>(QuquXiangqingView.PrefabResourcePath);
            detailView = QuquXiangqingView.InstantiateOverlay(xiangqingPrefab);
            return detailView != null;
        }

        private void LoadQualitySprites()
        {
            if (qualitySprites != null) return;
            qualitySprites = new Sprite[CatalogSize];
            for (int quality = 1; quality <= 4; quality++)
            {
                for (int temperament = 1; temperament <= 4; temperament++)
                {
                    qualitySprites[(quality - 1) * 4 + (temperament - 1)] = LoadSprite(
                        "Merge/MergeQualities/quality-" + quality + "-" + temperament);
                }
            }
        }

        private Sprite SpriteFor(int quality, int temperament)
        {
            int index = (Mathf.Clamp(quality, 1, 4) - 1) * 4 + (Mathf.Clamp(temperament, 1, 4) - 1);
            if (qualitySprites != null && index >= 0 && index < qualitySprites.Length)
                return qualitySprites[index];
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

        private static void CollectNamed(Transform root, string objectName, List<Transform> into)
        {
            if (root == null) return;
            if (root.name == objectName) into.Add(root);
            for (int i = 0; i < root.childCount; i++)
                CollectNamed(root.GetChild(i), objectName, into);
        }

        private static TMP_Text FindCountLabel(Transform root)
        {
            Transform found = FindNamed(root, "收集总数_ 1 _ 5");
            if (found == null) found = FindNamedContains(root, "收集总数");
            if (found == null) return null;
            return found.GetComponent<TMP_Text>();
        }

        private static Transform FindNamedContains(Transform root, string fragment)
        {
            if (root == null) return null;
            if (root.name.IndexOf(fragment) >= 0) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform hit = FindNamedContains(root.GetChild(i), fragment);
                if (hit != null) return hit;
            }
            return null;
        }

        private static Transform FindNamed(Transform root, string objectName)
        {
            if (root == null) return null;
            if (root.name == objectName) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform hit = FindNamed(root.GetChild(i), objectName);
                if (hit != null) return hit;
            }
            return null;
        }

        private void RefreshCollection()
        {
            List<CricketCollectionEntry> entries = DouQuquPlayerDataService.GetCollectionSnapshot();
            if (countText != null)
                countText.text = "收集总数: " + entries.Count + " / " + CatalogSize;
            for (int i = 0; i < cards.Length; i++)
            {
                CardSlot slot = cards[i];
                if (slot == null || slot.group == null) continue;
                bool owned = HasEntry(entries, slot.quality, slot.temperament);
                slot.group.alpha = owned ? 1f : 0.42f;
            }
        }

        private static bool HasEntry(List<CricketCollectionEntry> entries, int quality, int temperament)
        {
            if (entries == null) return false;
            for (int i = 0; i < entries.Count; i++)
            {
                CricketCollectionEntry entry = entries[i];
                if (entry != null && entry.drawA == quality && entry.drawB == temperament)
                    return true;
            }
            return false;
        }
    }
}
