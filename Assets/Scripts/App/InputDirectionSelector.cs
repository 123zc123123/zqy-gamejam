using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DouQuqu
{
    public static class InputDirectionSettings
    {
        private const string Key = "DouQuqu.InputDirection.Reverse";
        public static bool ReverseDrag { get; private set; } = true;
        public static bool HasChoice => PlayerPrefs.HasKey(Key);
        public static void Load() { ReverseDrag = PlayerPrefs.GetInt(Key, 1) != 0; }
        public static void Set(bool reverse) { ReverseDrag = reverse; PlayerPrefs.SetInt(Key, reverse ? 1 : 0); PlayerPrefs.Save(); }
    }

    public sealed class InputDirectionSelector : MonoBehaviour
    {
        public static void Show(Action confirmed)
        {
            InputDirectionSettings.Load();
            RectTransform root = UiFactory.CreateOverlay("InputDirectionSelector", 500);
            Image dim = root.GetComponent<Image>();
            if (dim != null) dim.color = new Color(0.04f, 0.025f, 0.02f, 0.72f);
            GameObject panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            RectTransform panelRect = panel.GetComponent<RectTransform>(); panelRect.SetParent(root, false);
            panelRect.anchorMin = new Vector2(0.08f, 0.28f);
            panelRect.anchorMax = new Vector2(0.92f, 0.72f);
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;
            panel.GetComponent<Image>().color = new Color(0.96f, 0.86f, 0.62f, 1f);
            TMP_Text title = UiFactory.CreateText(panelRect, "Title", "选择操作方向", 48f, new Vector2(0.05f, 0.76f), new Vector2(0.95f, 0.95f), Vector2.zero, Vector2.zero); title.color = Color.white; ClearTextEffects(title);
            TMP_Text hint = UiFactory.CreateText(panelRect, "Hint", "拖动方向决定蛐蛐的跳跃方向", 28f, new Vector2(0.05f, 0.61f), new Vector2(0.95f, 0.75f), Vector2.zero, Vector2.zero); hint.color = Color.white; ClearTextEffects(hint);
            bool reverse = InputDirectionSettings.ReverseDrag;
            Button reverseButton = UiFactory.CreateButton(panelRect, "Reverse", "相反方向\n往后拉，向前跳", null, new Vector2(0.08f, 0.30f), new Vector2(0.48f, 0.56f), Vector2.zero, Vector2.zero);
            Button sameButton = UiFactory.CreateButton(panelRect, "Same", "相同方向\n往哪拖，往哪跳", null, new Vector2(0.52f, 0.30f), new Vector2(0.92f, 0.56f), Vector2.zero, Vector2.zero);
            Button confirm = UiFactory.CreateButton(panelRect, "Confirm", "确认", null, new Vector2(0.28f, 0.07f), new Vector2(0.72f, 0.23f), Vector2.zero, Vector2.zero);
            TMP_Text reverseLabel = reverseButton.GetComponentInChildren<TMP_Text>(true);
            TMP_Text sameLabel = sameButton.GetComponentInChildren<TMP_Text>(true);
            if (reverseLabel != null) { reverseLabel.gameObject.SetActive(true); reverseLabel.text = "相反方向\n往后拖，向前跳"; reverseLabel.fontSize = 32f; reverseLabel.fontStyle = FontStyles.Normal; reverseLabel.outlineWidth = 0f; }
            if (sameLabel != null) { sameLabel.gameObject.SetActive(true); sameLabel.text = "相同方向\n往前拖，向前跳"; sameLabel.fontSize = 32f; sameLabel.fontStyle = FontStyles.Normal; sameLabel.outlineWidth = 0f; }
            StyleChoiceText(reverseButton);
            StyleChoiceText(sameButton);
            StyleChoiceText(confirm);
            Image reverseImage = reverseButton.GetComponent<Image>(); Image sameImage = sameButton.GetComponent<Image>();
            Action refresh = () => { reverseImage.color = reverse ? new Color(1f, 0.62f, 0.16f, 1f) : new Color(0.70f, 0.53f, 0.32f, 1f); sameImage.color = reverse ? new Color(0.70f, 0.53f, 0.32f, 1f) : new Color(1f, 0.62f, 0.16f, 1f); };
            reverseButton.onClick.AddListener(() => { reverse = true; refresh(); }); sameButton.onClick.AddListener(() => { reverse = false; refresh(); });
            confirm.onClick.AddListener(() => { InputDirectionSettings.Set(reverse); UnityEngine.Object.Destroy(root.gameObject); confirmed?.Invoke(); });
            refresh();
        }

        private static void StyleChoiceText(Button button)
        {
            if (button == null) return;
            TMP_Text text = button.GetComponentInChildren<TMP_Text>(true);
            if (text == null) return;
            text.fontSize = 40f;
            text.enableWordWrapping = false;
            text.alignment = TextAlignmentOptions.Center;
            text.fontStyle = FontStyles.Normal;
            text.outlineWidth = 0f;
            text.color = Color.white;
            ClearTextEffects(text);
        }

        private static void ClearTextEffects(TMP_Text text)
        {
            if (text == null) return;
            text.fontStyle = FontStyles.Normal;
            Material battleMaterial = Resources.Load<Material>("Fonts/Chinese SDF battlematchbtn");
            if (battleMaterial != null)
            {
                text.fontMaterial = battleMaterial;
                return;
            }
            Material source = text.fontMaterial;
            if (source == null) return;
            Material clean = new Material(source);
            if (clean.HasProperty("_OutlineWidth")) clean.SetFloat("_OutlineWidth", 0f);
            if (clean.HasProperty("_OutlineSoftness")) clean.SetFloat("_OutlineSoftness", 0f);
            if (clean.HasProperty("_UnderlayDilate")) clean.SetFloat("_UnderlayDilate", 0f);
            if (clean.HasProperty("_UnderlaySoftness")) clean.SetFloat("_UnderlaySoftness", 0f);
            text.fontMaterial = clean;
        }

    }
}
