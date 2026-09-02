using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

namespace DouQuqu
{
    /// <summary>图鉴场景：按 A、B 两个参数汇总玩家已经合成出的蟋蟀数量。</summary>
    public sealed class DouQuquCollectionController : MonoBehaviour
    {
        private TMP_Text collectionText;

        private void Start()
        {
            if (!DouQuquPlayerDataService.RequireLogin()) return;
            BuildUi();
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
                new Vector2(0.16f, 0.08f), new Vector2(0.84f, 0.92f), Vector2.zero, Vector2.zero);
            DouQuquUiFactory.CreateText(panel, "Title", "蟋蟀图鉴", 58f,
                new Vector2(0.08f, 0.84f), new Vector2(0.92f, 0.96f), Vector2.zero, Vector2.zero);
            collectionText = DouQuquUiFactory.CreateText(panel, "CollectionList", string.Empty, 32f,
                new Vector2(0.10f, 0.19f), new Vector2(0.90f, 0.82f), Vector2.zero, Vector2.zero,
                TextAlignmentOptions.TopLeft);
            DouQuquUiFactory.CreateButton(panel, "BackButton", "返回", Back,
                new Vector2(0.36f, 0.05f), new Vector2(0.64f, 0.14f), Vector2.zero, Vector2.zero);
        }

        private void RefreshCollection()
        {
            if (collectionText == null) return;
            List<CricketCollectionEntry> entries = DouQuquPlayerDataService.GetCollectionSnapshot();
            if (entries.Count == 0)
            {
                collectionText.text = "还没有蟋蟀。\n前往合成界面，将两只二级棋子合成到三级即可获得。";
                return;
            }
            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < entries.Count; i++)
            {
                CricketCollectionEntry entry = entries[i];
                builder.Append('蟋').Append('蟀').Append(' ')
                    .Append(entry.drawA).Append(',').Append(entry.drawB)
                    .Append("　× ").Append(entry.count);
                if (i < entries.Count - 1) builder.AppendLine();
            }
            collectionText.text = builder.ToString();
        }

        private void Back()
        {
            DouQuquSceneNames.Load(DouQuquSceneNames.MainMenu);
        }
    }
}
