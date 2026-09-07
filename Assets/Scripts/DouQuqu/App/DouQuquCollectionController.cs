using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

namespace DouQuqu
{
    /// <summary>图鉴页：绑 cricket-collection Prefab，把收集数写到「收集总数」。</summary>
    public sealed class DouQuquCollectionController : MonoBehaviour
    {
        private const int CatalogSize = 16;

        private TMP_Text collectionText;
        private TMP_Text countText;
        private bool bound;

        public void BindPage(GameObject pageRoot)
        {
            if (pageRoot == null) return;
            bound = true;
            DouQuquBottomNavBar.SuppressEmbedded(pageRoot.transform);
            countText = FindCountLabel(pageRoot.transform);
            collectionText = FindListLabel(pageRoot.transform);
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
            collectionText = DouQuquUiFactory.CreateText(panel, "CollectionList", string.Empty, 32f,
                new Vector2(0.10f, 0.08f), new Vector2(0.90f, 0.82f), Vector2.zero, Vector2.zero,
                TextAlignmentOptions.TopLeft);
            DouQuquBottomNavBar.EnsureOn(root);
        }

        private static TMP_Text FindListLabel(Transform root)
        {
            Transform found = FindNamed(root, "CollectionList");
            if (found == null) found = FindNamed(root, "CatalogList");
            if (found == null) return null;
            return found.GetComponent<TMP_Text>();
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
            if (collectionText == null) return;
            if (entries.Count == 0)
            {
                collectionText.text = "还没有蟋蟀。\n把两只成虫合成精品虫即可收入图鉴。";
                return;
            }
            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < entries.Count; i++)
            {
                CricketCollectionEntry entry = entries[i];
                builder.Append(DouQuquCricketCatalog.FullName(entry.drawA, entry.drawB))
                    .Append("　× ").Append(entry.count);
                if (i < entries.Count - 1) builder.AppendLine();
            }
            collectionText.text = builder.ToString();
        }
    }
}
