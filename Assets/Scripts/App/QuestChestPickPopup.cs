using TMPro;
using UnityEngine;
using UnityEngine.UI;
using ZqyGameJam.UI.QuquXiangqing;

namespace DouQuqu
{
    /// <summary>
    /// 开宝箱自选一只极品：任务说明底图、「任选一只」、四只 PackCricket、底部确认。
    /// 每只下面有详情放大镜，点开只读蛐蛐详情。
    /// </summary>
    public sealed class QuestChestPickPopup : MonoBehaviour
    {
        public const string PackPrefabPath = "Common/Prefabs/PackCricket";
        public const string HelpPrefabPath = "BattleEntrance/Prefabs/Parts/QuestHelp";
        public const string ReadyPrefabPath = "Common/Prefabs/btn-ready";
        public const string InspectIconPath = "Collection/Textures/StatHelpIcon";
        public const int Quality = 4;

        System.Action<int> confirmed;
        int selectedTemperament;
        Button confirmButton;
        Image confirmImage;
        readonly GameObject[] packs = new GameObject[4];
        QuquXiangqingView detailView;

        public static QuestChestPickPopup Show(System.Action<int> onConfirmed)
        {
            RectTransform overlay = UiFactory.CreateOverlay("QuestChestPickCanvas", 280);
            Image dim = overlay.GetComponent<Image>();
            if (dim == null) dim = overlay.gameObject.AddComponent<Image>();
            dim.color = new Color(0.05f, 0.03f, 0.02f, 0.62f);
            dim.raycastTarget = true;

            QuestChestPickPopup popup = overlay.gameObject.AddComponent<QuestChestPickPopup>();
            popup.confirmed = onConfirmed;
            popup.Build(overlay);
            return popup;
        }

        public void Close()
        {
            if (detailView != null)
            {
                Destroy(detailView.gameObject);
                detailView = null;
            }
            if (gameObject != null) Destroy(gameObject);
        }

        void Build(RectTransform overlay)
        {
            RectTransform panel = CreatePanel(overlay);
            CreateTitle(panel);
            CreatePacks(panel);
            CreateConfirm(overlay);
            RefreshSelection();
            UiFonts.ApplyTree(transform);
        }

        RectTransform CreatePanel(RectTransform overlay)
        {
            GameObject prefab = Resources.Load<GameObject>(HelpPrefabPath);
            GameObject panelGo;
            if (prefab != null)
            {
                panelGo = Instantiate(prefab, overlay, false);
                panelGo.name = "QuestHelp";
            }
            else
            {
                panelGo = new GameObject("QuestHelp", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                panelGo.transform.SetParent(overlay, false);
            }

            RectTransform panel = panelGo.GetComponent<RectTransform>();
            panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0.5f, 0.5f);
            panel.anchoredPosition = new Vector2(0f, 40f);
            panel.sizeDelta = new Vector2(960f, 620f);
            panel.localScale = Vector3.one;
            Image graphic = panel.GetComponent<Image>();
            if (graphic != null)
            {
                graphic.preserveAspect = true;
                graphic.raycastTarget = true;
            }

            Transform body = panel.Find("Body");
            if (body != null) body.gameObject.SetActive(false);
            return panel;
        }

        void CreateTitle(RectTransform panel)
        {
            GameObject go = new GameObject("Title", typeof(RectTransform), typeof(TextMeshProUGUI));
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(panel, false);
            rect.anchorMin = new Vector2(0.08f, 0.82f);
            rect.anchorMax = new Vector2(0.92f, 0.96f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();
            text.font = UiFonts.Font;
            text.text = "任选一只";
            text.fontSize = 42f;
            text.color = new Color(0.28f, 0.16f, 0.10f, 1f);
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
        }

        void CreatePacks(RectTransform panel)
        {
            GameObject prefab = Resources.Load<GameObject>(PackPrefabPath);
            if (prefab == null) return;

            float scale = 0.55f;
            float spacing = 200f;
            float startX = -1.5f * spacing;
            for (int t = 1; t <= 4; t++)
            {
                GameObject inst = Instantiate(prefab, panel, false);
                inst.name = "PackCricket_4_" + t;
                RectTransform rt = inst.GetComponent<RectTransform>();
                rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = new Vector2(startX + (t - 1) * spacing, -20f);
                rt.sizeDelta = new Vector2(288f, 288f);
                rt.localScale = new Vector3(scale, scale, 1f);
                PaintPack(inst, t);
                CreateInspectButton(panel, t, rt.anchoredPosition);
                packs[t - 1] = inst;
            }
        }

        void CreateInspectButton(RectTransform panel, int temperament, Vector2 packPos)
        {
            GameObject go = new GameObject("Inspect_" + temperament, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(panel, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(68f, 68f);
            rect.anchoredPosition = new Vector2(packPos.x, packPos.y - 124f);
            rect.localScale = Vector3.one;

            Image image = go.GetComponent<Image>();
            Sprite icon = Resources.Load<Sprite>(InspectIconPath);
            if (icon == null)
            {
                Debug.LogWarning("[DouQuqu] 找不到详情放大镜：" + InspectIconPath);
            }
            image.sprite = icon;
            image.preserveAspect = true;
            image.raycastTarget = true;
            image.color = Color.white;

            Button button = go.GetComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.targetGraphic = image;
            int captured = temperament;
            button.onClick.AddListener(() => OpenDetail(captured));
        }

        void OpenDetail(int temperament)
        {
            if (!EnsureDetailView()) return;
            detailView.SetCatalogMode();
            Color? descColor = null;
            Color skillColor;
            if (CricketCatalog.TrySkillBlurbColor(Quality, temperament, out skillColor))
                descColor = skillColor;
            detailView.Show(
                CricketCatalog.RankLabel(Quality, temperament),
                CricketCatalog.CricketName(Quality, temperament),
                CricketCatalog.Blurb(Quality, temperament),
                CricketCatalog.Portrait(Quality, temperament),
                CricketCatalog.TemperamentName(temperament),
                CricketCatalog.PanelStatDisplays(Quality, temperament),
                CricketCatalog.PanelStatStrongFlags(temperament),
                descColor);
        }

        bool EnsureDetailView()
        {
            if (detailView != null) return true;
            GameObject prefab = Resources.Load<GameObject>(QuquXiangqingView.PrefabResourcePath);
            detailView = QuquXiangqingView.InstantiateOverlay(prefab);
            return detailView != null;
        }

        void PaintPack(GameObject root, int temperament)
        {
            Transform mask = root.transform.Find("选中的蛐蛐遮罩");
            if (mask != null) mask.gameObject.SetActive(false);
            Transform badge = root.transform.Find("品级");
            if (badge != null) badge.gameObject.SetActive(false);

            Transform bg = root.transform.Find("背景");
            Image bgImage = bg != null ? bg.GetComponent<Image>() : null;
            if (bgImage != null)
            {
                bgImage.sprite = CricketCatalog.PackBackground(Quality, temperament);
                bgImage.enabled = bgImage.sprite != null;
                bgImage.preserveAspect = true;
                bgImage.color = Color.white;
                bgImage.raycastTarget = false;
            }

            Transform portrait = root.transform.Find("头像");
            Image portraitImage = portrait != null ? portrait.GetComponent<Image>() : null;
            if (portraitImage != null)
            {
                portraitImage.sprite = CricketCatalog.Portrait(Quality, temperament);
                portraitImage.enabled = portraitImage.sprite != null;
                portraitImage.raycastTarget = false;
                CricketCatalog.FitPackPortrait(portraitImage);
            }

            Transform nameNode = root.transform.Find("白头狮");
            TMP_Text nameText = nameNode != null ? nameNode.GetComponent<TMP_Text>() : null;
            if (nameText != null)
            {
                nameText.text = CricketCatalog.CricketName(Quality, temperament);
                nameText.raycastTarget = false;
            }

            Image hit = root.GetComponent<Image>();
            if (hit == null) hit = root.AddComponent<Image>();
            hit.color = new Color(1f, 1f, 1f, 0.01f);
            hit.raycastTarget = true;
            Button button = root.GetComponent<Button>();
            if (button == null) button = root.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.targetGraphic = hit;
            int captured = temperament;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => Select(captured));
        }

        void Select(int temperament)
        {
            selectedTemperament = temperament;
            RefreshSelection();
        }

        void RefreshSelection()
        {
            for (int i = 0; i < packs.Length; i++)
            {
                GameObject pack = packs[i];
                if (pack == null) continue;
                Transform mask = pack.transform.Find("选中的蛐蛐遮罩");
                if (mask != null) mask.gameObject.SetActive(i + 1 == selectedTemperament);
            }

            bool ready = selectedTemperament >= 1 && selectedTemperament <= 4;
            if (confirmButton != null) confirmButton.interactable = ready;
            if (confirmImage != null)
                confirmImage.color = ready ? Color.white : new Color(1f, 1f, 1f, 0.45f);
        }

        void CreateConfirm(RectTransform overlay)
        {
            GameObject prefab = Resources.Load<GameObject>(ReadyPrefabPath);
            Sprite blue = Resources.Load<Sprite>("Common/Textures/蓝色bg");
            GameObject go;
            if (prefab != null)
            {
                go = Instantiate(prefab, overlay, false);
            }
            else
            {
                go = new GameObject("Confirm", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
                go.transform.SetParent(overlay, false);
            }

            go.name = "Confirm";
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(321f, 141f);
            rect.anchoredPosition = new Vector2(0f, -430f);
            rect.localScale = Vector3.one;

            confirmImage = go.GetComponent<Image>();
            if (confirmImage != null)
            {
                if (blue != null) confirmImage.sprite = blue;
                confirmImage.color = Color.white;
                confirmImage.preserveAspect = true;
                confirmImage.raycastTarget = true;
            }

            TMP_Text tmp = go.GetComponentInChildren<TMP_Text>(true);
            if (tmp != null)
            {
                tmp.text = "确认";
                tmp.color = Color.white;
                tmp.raycastTarget = false;
            }

            confirmButton = go.GetComponent<Button>();
            if (confirmButton == null) confirmButton = go.AddComponent<Button>();
            confirmButton.transition = Selectable.Transition.None;
            if (confirmImage != null) confirmButton.targetGraphic = confirmImage;
            confirmButton.onClick.RemoveAllListeners();
            confirmButton.onClick.AddListener(Confirm);
        }

        void Confirm()
        {
            if (selectedTemperament < 1 || selectedTemperament > 4) return;
            if (confirmButton != null) confirmButton.interactable = false;
            System.Action<int> callback = confirmed;
            confirmed = null;
            int temperament = selectedTemperament;
            RectTransform cell = packs[temperament - 1] != null
                ? packs[temperament - 1].GetComponent<RectTransform>()
                : null;
            string nameLine = CricketCatalog.CricketName(Quality, temperament);
            string idiom = CricketCatalog.Idiom(temperament);
            string detail = string.IsNullOrEmpty(idiom) ? nameLine : nameLine + "  ·  " + idiom;
            FinestRevealFx.Play(
                cell,
                "极 品",
                nameLine,
                detail,
                CricketCatalog.Portrait(Quality, temperament),
                true,
                CricketCatalog.QualityColors[Quality],
                () =>
                {
                    Close();
                    if (callback != null) callback.Invoke(temperament);
                });
        }
    }
}
