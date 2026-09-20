using TMPro;
using UnityEngine;
using UnityEngine.UI;
using ZqyGameJam.UI.QuquXiangqing;

namespace DouQuqu
{
    /// <summary>
    /// 开宝箱自选一只极品：预制体半透明黑底、「任选一只」、四只 PackCricket、底部确认。
    /// 每只下面有详情放大镜，点开只读蛐蛐详情。
    /// </summary>
    public sealed class QuestChestPickPopup : MonoBehaviour
    {
        public const string PrefabPath = "Common/Prefabs/QuestChestPick";
        public const int Quality = 4;

        System.Action<int> confirmed;
        int selectedTemperament;
        Button confirmButton;
        Image confirmImage;
        readonly GameObject[] packs = new GameObject[4];
        QuquXiangqingView detailView;

        public static QuestChestPickPopup Show(System.Action<int> onConfirmed)
        {
            GameObject prefab = Resources.Load<GameObject>(PrefabPath);
            if (prefab == null)
            {
                Debug.LogWarning("[DouQuqu] 找不到宝箱领奖 " + PrefabPath);
                return null;
            }

            GameObject go = Instantiate(prefab);
            go.name = "QuestChestPick";
            QuestChestPickPopup popup = go.GetComponent<QuestChestPickPopup>();
            if (popup == null) popup = go.AddComponent<QuestChestPickPopup>();
            popup.confirmed = onConfirmed;
            popup.Bind();
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

        void Bind()
        {
            Transform crickets = FindNamed(transform, "crickets");
            for (int t = 1; t <= 4; t++)
            {
                Transform slot = FindSlot(crickets, t);
                Transform pack = FindPack(slot);
                if (pack == null) pack = FindNamed(transform, "PackCricket_4_" + t);
                if (pack == null) continue;
                PaintPack(pack.gameObject, t);
                packs[t - 1] = pack.gameObject;

                int captured = t;
                if (slot != null && slot != pack)
                    BindClick(slot.gameObject, () => Select(captured));

                Transform inspect = FindInspect(slot, t);
                if (inspect == null) inspect = FindNamed(transform, "Inspect_" + t);
                if (inspect != null)
                    BindClick(inspect.gameObject, () => OpenDetail(captured));
            }

            Transform confirm = FindNamed(transform, "Confirm");
            if (confirm != null)
            {
                confirmImage = confirm.GetComponent<Image>();
                confirmButton = confirm.GetComponent<Button>();
                if (confirmButton == null) confirmButton = confirm.gameObject.AddComponent<Button>();
                confirmButton.transition = Selectable.Transition.None;
                if (confirmImage != null) confirmButton.targetGraphic = confirmImage;
                confirmButton.onClick.RemoveAllListeners();
                confirmButton.onClick.AddListener(Confirm);
            }

            selectedTemperament = 0;
            RefreshSelection();
            UiFonts.ApplyTree(transform);
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
                descColor,
                Quality);
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
            if (badge != null)
            {
                badge.gameObject.SetActive(true);
                CricketCatalog.ApplyQualityLabel(badge.GetComponent<Image>(), Quality);
            }

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
            int captured = temperament;
            BindClick(root, () => Select(captured));
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

        static void BindClick(GameObject go, UnityEngine.Events.UnityAction clicked)
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

        static Transform FindSlot(Transform crickets, int temperament)
        {
            string slotName = temperament.ToString();
            if (crickets != null)
            {
                Transform direct = crickets.Find(slotName);
                if (direct != null) return direct;
                Transform named = FindNamed(crickets, slotName);
                if (named != null) return named;
            }
            return null;
        }

        static Transform FindPack(Transform slot)
        {
            if (slot == null) return null;
            for (int i = 0; i < slot.childCount; i++)
            {
                Transform child = slot.GetChild(i);
                if (child.name.StartsWith("Inspect")) continue;
                if (child.name.StartsWith("PackCricket")) return child;
                if (child.Find("头像") != null || child.Find("背景") != null) return child;
            }
            return FindNamed(slot, "PackCricket");
        }

        static Transform FindInspect(Transform slot, int temperament)
        {
            Transform from = slot != null ? slot : null;
            if (from == null) return null;
            Transform named = from.Find("Inspect_" + temperament);
            if (named != null) return named;
            named = from.Find("Inspect");
            if (named != null) return named;
            for (int i = 0; i < from.childCount; i++)
            {
                Transform child = from.GetChild(i);
                if (child.name.StartsWith("Inspect")) return child;
            }
            return FindNamed(from, "Inspect_" + temperament);
        }

        static Transform FindNamed(Transform root, string objectName)
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
