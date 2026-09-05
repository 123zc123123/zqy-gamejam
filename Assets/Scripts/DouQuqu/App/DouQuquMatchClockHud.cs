using TMPro;
using UnityEngine;

namespace DouQuqu
{
    /// <summary>
    /// 把局内 Countdown/Value 接到对局剩余时间。开赛前显示正赛时长，开赛后按秒倒数。
    /// </summary>
    public sealed class DouQuquMatchClockHud : MonoBehaviour
    {
        private static readonly Color NormalColor = Color.white;
        private static readonly Color RageColor = new Color(0.941f, 0.627f, 0.439f, 1f);

        private DouQuquMatchController match;
        private TMP_Text valueText;
        private TMP_Text labelText;
        private string lastShown;

        public void Bind(DouQuquMatchController matchController)
        {
            match = matchController;
            CacheLabels();
            Refresh(true);
        }

        private void LateUpdate()
        {
            Refresh(false);
        }

        private void CacheLabels()
        {
            if (valueText != null) return;
            Transform value = FindNamed(transform, "Value");
            if (value != null) valueText = value.GetComponent<TMP_Text>();
            Transform label = FindNamed(transform, "Label");
            if (label != null) labelText = label.GetComponent<TMP_Text>();
            if (valueText == null)
            {
                TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);
                for (int i = 0; i < texts.Length; i++)
                {
                    if (texts[i] == null) continue;
                    if (labelText == null && texts[i].text != null && texts[i].text.IndexOf(':') < 0)
                    {
                        labelText = texts[i];
                        continue;
                    }
                    if (valueText == null) valueText = texts[i];
                }
            }
        }

        private void Refresh(bool force)
        {
            CacheLabels();
            if (valueText == null) return;
            if (match == null) match = Object.FindObjectOfType<DouQuquMatchController>();

            MatchKnobs knobs = match != null && match.Knobs != null
                ? match.Knobs
                : DouQuquRules.DefaultKnobs();
            bool started = match != null && match.IsStarted;
            float elapsed = match != null ? match.Elapsed : 0f;
            MatchPhase phase = DouQuquRules.Phase(knobs, elapsed);
            bool rage = started && DouQuquRules.IsRage(knobs, elapsed);
            string clock = DouQuquRules.FormatClock(DouQuquRules.RemainingClock(knobs, elapsed, started));
            if (!force && clock == lastShown && !rage) return;

            lastShown = clock;
            valueText.text = clock;
            valueText.color = rage ? RageColor : NormalColor;
            if (labelText != null)
            {
                labelText.text = rage ? "狂暴" : "剩余时间";
                labelText.color = rage ? RageColor : NormalColor;
            }

            if (phase == MatchPhase.Over && started)
                valueText.text = DouQuquRules.FormatClock(0f);
        }

        private static Transform FindNamed(Transform root, string objectName)
        {
            if (root.name == objectName) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform hit = FindNamed(root.GetChild(i), objectName);
                if (hit != null) return hit;
            }

            return null;
        }
    }
}
