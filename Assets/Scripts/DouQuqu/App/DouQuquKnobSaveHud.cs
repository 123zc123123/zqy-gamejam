using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DouQuqu
{
    /// <summary>PlayerPrefs 键：局内保存旋钮，退出 Play 后覆写 Demo 场景。</summary>
    public static class DouQuquKnobSaveKeys
    {
        public const string Json = "DouQuqu.PendingMatchKnobsJson";
        public const string Dirty = "DouQuqu.PendingMatchKnobsDirty";
    }

    /// <summary>
    /// 编辑器局内「保存配置」按钮。Play 里改的 Inspector 退出会丢，
    /// 所以这里先记下当前旋钮，退出 Play 后再写回 Demo 场景。
    /// </summary>
    public sealed class DouQuquKnobSaveHud : MonoBehaviour
    {
        private DouQuquMatchController match;
        private TMP_Text label;
        private float resetLabelAt;

        public static void Ensure(DouQuquMatchController match)
        {
            if (!Application.isEditor || !Application.isPlaying || match == null) return;
            if (FindObjectOfType<DouQuquKnobSaveHud>() != null) return;

            RectTransform root = DouQuquUiFactory.CreateOverlay("KnobSaveCanvas", 250);
            Button button = DouQuquUiFactory.CreateButton(root, "SaveKnobsButton", "保存配置", null,
                new Vector2(0.72f, 0.91f), new Vector2(0.98f, 0.98f), Vector2.zero, Vector2.zero);
            DouQuquKnobSaveHud hud = root.gameObject.AddComponent<DouQuquKnobSaveHud>();
            hud.match = match;
            hud.label = button.GetComponentInChildren<TMP_Text>(true);
            button.onClick.AddListener(hud.Save);
        }

        private void Update()
        {
            if (resetLabelAt <= 0f || Time.unscaledTime < resetLabelAt) return;
            resetLabelAt = 0f;
            if (label != null) label.text = "保存配置";
        }

        private void Save()
        {
            if (match == null) match = FindObjectOfType<DouQuquMatchController>();
            if (match == null || !match.TrySaveKnobsToScene())
            {
                SetLabel("保存失败");
                Debug.LogWarning("[DouQuqu] 局内保存失败：找不到 MatchController。");
                return;
            }

            MatchKnobs knobs = match.Knobs;
            SetLabel("已记下");
            Debug.Log(string.Format(
                "[DouQuqu] 已记下当前旋钮。退出 Play 后会覆写 Demo Inspector。蓄满时间={0} 起跳力气={1} 蓄力速度={2}",
                knobs != null ? knobs.tChargeMax : 0f,
                knobs != null ? knobs.tFloor : 0f,
                knobs != null ? knobs.vRate : 0f));
        }

        private void SetLabel(string text)
        {
            if (label != null) label.text = text;
            resetLabelAt = Time.unscaledTime + 1.6f;
        }
    }
}
