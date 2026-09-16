using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace DouQuqu
{
    /// <summary>
    /// 收口后用下一档 field 蒙版裁 tableBg，outline 迅速缩到新圈。
    /// 崩裂纹用 Battle 预制体里的 liewen（默认隐藏，只在塌陷环带上按预制体尺寸铺）。
    /// 不改 Board / 场景 / BattleTable 尺寸。
    /// </summary>
    public sealed class BattleTableShrink : MonoBehaviour
    {
        const float OutlineShrinkT = 0.15f;

        RectTransform table;
        RectTransform tableBg;
        RectTransform tableOutline;
        RectTransform tableClip;
        RectTransform tableFadeClip;
        RectTransform tableOutlineHost;
        RectTransform tableBgFade;
        RectTransform liewen;
        CanvasGroup fadeGroup;
        readonly RectTransform[] fields = new RectTransform[3];
        Vector2 tableSize;
        Vector2 liewenSize;
        Vector2 outlinePadMin;
        Vector2 outlinePadMax;
        int shownTier = -1;
        int warnNextTier = -1;
        Coroutine outlineCo;
        Coroutine fadeCo;

        public static BattleTableShrink Bind(RectTransform hudRoot, RectTransform pit)
        {
            if (hudRoot == null) return null;
            RectTransform table = FindNamed(hudRoot, "BattleTable") as RectTransform;
            if (table == null) return null;
            BattleTableShrink shrink = table.GetComponent<BattleTableShrink>();
            if (shrink == null) shrink = table.gameObject.AddComponent<BattleTableShrink>();
            shrink.table = table;
            shrink.CaptureFields(pit);
            shrink.CaptureLiewen(hudRoot);
            shrink.Ensure();
            shrink.HideLiewen();
            return shrink;
        }

        public void SnapTo(int tier, float fadeT, bool instant)
        {
            Ensure();
            if (tableClip == null || tableOutlineHost == null) return;
            tier = Mathf.Clamp(tier, 0, Rules.LastZoneTier);
            if (tier == shownTier) return;

            Vector2 fromSize = shownTier < 0 ? TableSize() : SizeOnTable(shownTier);
            Vector2 toSize = SizeOnTable(tier);
            int fromTier = shownTier;
            shownTier = tier;

            if (instant || fromTier < 0)
            {
                StopAnims();
                SetCenteredSize(tableClip, toSize);
                SetCenteredSize(tableOutlineHost, toSize);
                ClearWarn();
                HideFade();
                return;
            }

            SetCenteredSize(tableFadeClip, fromSize);
            SetCenteredSize(tableClip, toSize);
            ClearWarn();
            ShowFade();
            ShowLiewenOn(tableFadeClip);
            StopAnims();
            outlineCo = StartCoroutine(ShrinkOutline(fromSize, toSize, OutlineShrinkT));
            fadeCo = StartCoroutine(FadeExcess(Mathf.Max(0f, fadeT)));
        }

        /// <summary>预告期不再铺程序裂纹；崩裂只在收口时用 liewen。</summary>
        public void SetWarn(int nextTier, float progress)
        {
            if (nextTier <= shownTier)
            {
                if (warnNextTier >= 0) ClearWarn();
                return;
            }

            warnNextTier = nextTier;
        }

        public void ClearWarn()
        {
            warnNextTier = -1;
        }

        void CaptureFields(RectTransform pit)
        {
            if (pit == null) return;
            fields[0] = FindNamed(pit, "field-0") as RectTransform;
            fields[1] = FindNamed(pit, "field-1") as RectTransform;
            fields[2] = FindNamed(pit, "field-2") as RectTransform;
        }

        void CaptureLiewen(RectTransform hudRoot)
        {
            liewen = FindNamed(hudRoot, "liewen") as RectTransform;
            if (liewen == null) return;
            if (liewen.sizeDelta.x >= 1f && liewen.sizeDelta.y >= 1f)
                liewenSize = liewen.sizeDelta;
            else
                liewenSize = TableSize();
            Image image = liewen.GetComponent<Image>();
            if (image != null) image.raycastTarget = false;
            liewen.gameObject.SetActive(false);
        }

        void ShowLiewenOn(RectTransform host)
        {
            if (liewen == null || host == null) return;
            Vector2 size = liewenSize.x >= 1f ? liewenSize : TableSize();
            liewen.SetParent(host, false);
            SetCenteredSize(liewen, size);
            liewen.SetAsLastSibling();
            liewen.gameObject.SetActive(true);
        }

        void HideLiewen()
        {
            if (liewen != null) liewen.gameObject.SetActive(false);
        }

        void Ensure()
        {
            if (table == null) return;
            tableBg = FindNamed(table, "tableBg") as RectTransform;
            tableOutline = FindNamed(table, "tableOutline") as RectTransform;
            if (tableBg == null || tableOutline == null) return;

            tableSize = TableSize();
            outlinePadMin = tableOutline.offsetMin;
            outlinePadMax = tableOutline.offsetMax;

            tableFadeClip = EnsureHost(table, "tableFadeClip", true);
            tableClip = EnsureHost(table, "tableClip", true);
            tableOutlineHost = EnsureHost(table, "tableOutlineHost", false);

            fadeGroup = tableFadeClip.GetComponent<CanvasGroup>();
            if (fadeGroup == null) fadeGroup = tableFadeClip.gameObject.AddComponent<CanvasGroup>();
            fadeGroup.blocksRaycasts = false;
            fadeGroup.interactable = false;

            if (tableBg.parent != tableClip)
                tableBg.SetParent(tableClip, false);
            PinFullTable(tableBg);
            Image tableBgImage = tableBg.GetComponent<Image>();
            if (tableBgImage != null) tableBgImage.material = null;

            tableBgFade = EnsureFadeCopy();
            PinFullTable(tableBgFade);

            if (tableOutline.parent != tableOutlineHost)
                tableOutline.SetParent(tableOutlineHost, false);
            StretchWithPad(tableOutline, outlinePadMin, outlinePadMax);

            tableFadeClip.SetAsFirstSibling();
            tableClip.SetSiblingIndex(1);
            tableOutlineHost.SetAsLastSibling();
            if (shownTier < 0) HideFade();
        }

        RectTransform EnsureFadeCopy()
        {
            Transform existing = tableFadeClip.Find("tableBgFade");
            GameObject go = existing != null
                ? existing.gameObject
                : new GameObject("tableBgFade", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.SetParent(tableFadeClip, false);
            Image src = tableBg.GetComponent<Image>();
            Image dst = rt.GetComponent<Image>();
            if (dst == null) dst = go.AddComponent<Image>();
            if (src != null)
            {
                dst.sprite = src.sprite;
                dst.type = src.type;
                dst.fillCenter = src.fillCenter;
                dst.preserveAspect = false;
                dst.color = src.color;
            }
            dst.raycastTarget = false;
            dst.enabled = true;
            dst.material = null;
            return rt;
        }

        static RectTransform EnsureHost(RectTransform parent, string hostName, bool mask)
        {
            Transform existing = parent.Find(hostName);
            GameObject go = existing != null
                ? existing.gameObject
                : new GameObject(hostName, typeof(RectTransform));
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            if (mask)
            {
                RectMask2D clip = go.GetComponent<RectMask2D>();
                if (clip == null) clip = go.AddComponent<RectMask2D>();
                clip.enabled = true;
                clip.padding = Vector4.zero;
                clip.softness = Vector2Int.zero;
            }
            return rt;
        }

        Vector2 TableSize()
        {
            if (table != null)
            {
                Rect rect = table.rect;
                if (Mathf.Abs(rect.width) >= 1f && Mathf.Abs(rect.height) >= 1f)
                    return new Vector2(Mathf.Abs(rect.width), Mathf.Abs(rect.height));
                if (table.sizeDelta.x >= 1f && table.sizeDelta.y >= 1f)
                    return table.sizeDelta;
            }
            return new Vector2(2070f, 3063f);
        }

        Vector2 SizeOnTable(int tier)
        {
            Vector2 full = TableSize();
            RectTransform field0 = fields[0];
            RectTransform field = fields[Mathf.Clamp(tier, 0, fields.Length - 1)];
            if (field0 == null || field == null) return full;
            float w0 = Mathf.Abs(field0.rect.width);
            float h0 = Mathf.Abs(field0.rect.height);
            if (w0 < 1f || h0 < 1f) return full;
            return new Vector2(
                full.x * Mathf.Abs(field.rect.width) / w0,
                full.y * Mathf.Abs(field.rect.height) / h0);
        }

        void PinFullTable(RectTransform rt)
        {
            SetCenteredSize(rt, tableSize.x >= 1f ? tableSize : TableSize());
        }

        static void SetCenteredSize(RectTransform rt, Vector2 size)
        {
            if (rt == null) return;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = size;
            rt.localScale = Vector3.one;
            rt.localRotation = Quaternion.identity;
        }

        static void StretchWithPad(RectTransform rt, Vector2 padMin, Vector2 padMax)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = padMin;
            rt.offsetMax = padMax;
            rt.localScale = Vector3.one;
        }

        IEnumerator ShrinkOutline(Vector2 from, Vector2 to, float duration)
        {
            if (duration <= 0f)
            {
                SetCenteredSize(tableOutlineHost, to);
                yield break;
            }

            float age = 0f;
            while (age < duration)
            {
                age += Time.deltaTime;
                float u = Mathf.Clamp01(age / duration);
                u = 1f - (1f - u) * (1f - u);
                SetCenteredSize(tableOutlineHost, Vector2.LerpUnclamped(from, to, u));
                yield return null;
            }
            SetCenteredSize(tableOutlineHost, to);
            outlineCo = null;
        }

        IEnumerator FadeExcess(float duration)
        {
            if (duration <= 0f)
            {
                HideFade();
                yield break;
            }

            float age = 0f;
            while (age < duration)
            {
                age += Time.deltaTime;
                float u = Mathf.Clamp01(age / duration);
                if (fadeGroup != null) fadeGroup.alpha = 1f - u * u;
                yield return null;
            }
            HideFade();
            fadeCo = null;
        }

        void ShowFade()
        {
            if (tableFadeClip == null) return;
            tableFadeClip.gameObject.SetActive(true);
            if (fadeGroup != null) fadeGroup.alpha = 1f;
        }

        void HideFade()
        {
            HideLiewen();
            if (fadeGroup != null) fadeGroup.alpha = 0f;
            if (tableFadeClip != null) tableFadeClip.gameObject.SetActive(false);
        }

        void StopAnims()
        {
            if (outlineCo != null) StopCoroutine(outlineCo);
            if (fadeCo != null) StopCoroutine(fadeCo);
            outlineCo = null;
            fadeCo = null;
        }

        void OnDisable()
        {
            StopAnims();
        }

        void OnDestroy()
        {
            HideLiewen();
        }

        static Transform FindNamed(Transform root, string objectName)
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
    }
}
