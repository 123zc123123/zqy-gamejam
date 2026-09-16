using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DouQuqu
{
    /// <summary>
    /// 底部对话框：说话人 + 打字机正文。立绘可选，markdown 写 avatar。
    /// 点击：打字中揭完全句；打完进入下一句。
    /// 播放：DialogueBoxView.Play("dlg.xxx")。
    /// </summary>
    public sealed class DialogueBoxView : MonoBehaviour
    {
        public const int SortingOrder = 320;
        const int DefaultCps = 32;
        const int ShortLineInstantChars = 10;

        static readonly Color SolidBg = new Color(4f / 255f, 6f / 255f, 10f / 255f, 230f / 255f);
        static readonly Color DimColor = new Color(4f / 255f, 6f / 255f, 10f / 255f, 0.35f);
        static readonly Color SpeakerColor = new Color(0.93f, 0.78f, 0.38f, 1f);
        static readonly Color BodyColor = new Color(0.95f, 0.96f, 0.97f, 1f);

        static readonly Dictionary<char, float> PunctDelayMs = new Dictionary<char, float>
        {
            { '，', 120f }, { '、', 120f }, { '；', 120f }, { '：', 120f },
            { ',', 100f }, { ';', 100f }, { ':', 100f },
            { '。', 260f }, { '！', 260f }, { '？', 260f },
            { '.', 220f }, { '!', 220f }, { '?', 220f },
            { '…', 200f }, { '—', 300f }, { '\n', 200f }
        };

        static readonly Dictionary<string, MoodPreset> Moods = new Dictionary<string, MoodPreset>
        {
            { "neutral", new MoodPreset(1f, 1f) },
            { "calm", new MoodPreset(0.85f, 1.15f) },
            { "warm", new MoodPreset(0.9f, 1.1f) },
            { "tense", new MoodPreset(1.15f, 0.85f) },
            { "urgent", new MoodPreset(1.35f, 0.7f) },
            { "angry", new MoodPreset(1.25f, 0.8f) },
            { "sad", new MoodPreset(0.7f, 1.35f) },
            { "fear", new MoodPreset(0.8f, 1.2f) },
            { "radio", new MoodPreset(1.2f, 0.75f) }
        };

        static DialogueBoxView instance;

        TMP_Text speakerLabel;
        TMP_Text contentLabel;
        TMP_Text continueHint;
        Image portraitImage;
        DialogueGroup group;
        int lineIndex = -1;
        bool typingDone;
        bool revealing;
        float revealTimer;
        float nextDelay;
        int visibleChars;
        string currentText = string.Empty;
        Action onComplete;
        float autoSkipRemaining = -1f;

        public static bool IsPlaying => instance != null && instance.gameObject.activeSelf && instance.group != null;
        public static bool IsTyping => IsPlaying && !instance.typingDone;

        public static event Action PlayingChanged;

        public static void Play(string groupId, Action completed = null)
        {
            DialogueGroup group = DialogueCatalog.Get(groupId);
            if (group == null)
            {
                Debug.LogWarning("[DouQuqu] 找不到对白组 " + groupId + "。把 markdown 放到 Resources/Dialogue/。");
                if (completed != null) completed();
                return;
            }

            Play(group, completed);
        }

        public static void Play(DialogueGroup group, Action completed = null)
        {
            if (group == null || group.lines == null || group.lines.Count == 0)
            {
                if (completed != null) completed();
                return;
            }

            if (instance == null) instance = Create();
            instance.onComplete = completed;
            instance.group = group;
            instance.lineIndex = -1;
            instance.gameObject.SetActive(true);
            NotifyPlaying();
            instance.ShowNextLine();
        }

        public static void Hide()
        {
            if (instance == null) return;
            instance.StopPlayback(false);
        }

        public static bool HandleClick()
        {
            if (!IsPlaying) return false;
            instance.OnClicked();
            return true;
        }

        static void NotifyPlaying()
        {
            if (PlayingChanged != null) PlayingChanged();
        }

        static DialogueBoxView Create()
        {
            RectTransform root = UiFactory.CreateOverlay("DialogueBoxCanvas", SortingOrder);
            DialogueBoxView view = root.gameObject.AddComponent<DialogueBoxView>();

            Image dim = root.gameObject.AddComponent<Image>();
            dim.color = DimColor;
            dim.raycastTarget = true;
            Button click = root.gameObject.AddComponent<Button>();
            click.transition = Selectable.Transition.None;
            click.onClick.AddListener(view.OnClicked);

            Image bar = MakeImage(root, "Bar", SolidBg);
            RectTransform barRect = bar.rectTransform;
            barRect.anchorMin = new Vector2(0f, 0f);
            barRect.anchorMax = new Vector2(1f, 0f);
            barRect.pivot = new Vector2(0.5f, 0f);
            barRect.anchoredPosition = Vector2.zero;
            barRect.sizeDelta = new Vector2(0f, 420f);
            bar.raycastTarget = false;

            Image portrait = MakeImage(root, "Portrait", Color.white);
            RectTransform portraitRect = portrait.rectTransform;
            portraitRect.anchorMin = new Vector2(0f, 0f);
            portraitRect.anchorMax = new Vector2(0f, 0f);
            portraitRect.pivot = new Vector2(0f, 0f);
            portraitRect.anchoredPosition = new Vector2(-72f, 108f);
            portraitRect.sizeDelta = new Vector2(420f, 560f);
            portrait.preserveAspect = true;
            portrait.raycastTarget = false;
            portrait.enabled = false;
            view.portraitImage = portrait;

            view.speakerLabel = UiFactory.CreateText(barRect, "Speaker", string.Empty, 36f,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(72f, -88f), new Vector2(-72f, -36f),
                TextAlignmentOptions.BottomLeft);
            view.speakerLabel.color = SpeakerColor;
            view.speakerLabel.fontStyle = FontStyles.Bold;
            view.speakerLabel.enableWordWrapping = false;

            view.contentLabel = UiFactory.CreateText(barRect, "Content", string.Empty, 40f,
                new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(72f, 56f), new Vector2(-72f, -108f),
                TextAlignmentOptions.TopLeft);
            view.contentLabel.color = BodyColor;
            view.contentLabel.lineSpacing = 12f;
            view.contentLabel.overflowMode = TextOverflowModes.Overflow;

            view.continueHint = UiFactory.CreateText(barRect, "Continue", "▼", 28f,
                new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-96f, 18f), new Vector2(-48f, 52f),
                TextAlignmentOptions.Center);
            view.continueHint.color = SpeakerColor;
            view.continueHint.gameObject.SetActive(false);

            UiFonts.ApplyTree(root);
            return view;
        }

        static Image MakeImage(Transform parent, string name, Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            Image image = go.GetComponent<Image>();
            image.color = color;
            return image;
        }

        void Update()
        {
            if (group == null) return;
            if (revealing) TickReveal();
            if (typingDone && autoSkipRemaining >= 0f)
            {
                autoSkipRemaining -= Time.unscaledDeltaTime;
                if (autoSkipRemaining <= 0f)
                {
                    autoSkipRemaining = -1f;
                    ShowNextLine();
                }
            }
        }

        void OnClicked()
        {
            if (group == null) return;
            if (!typingDone)
            {
                RevealAll();
                return;
            }

            ShowNextLine();
        }

        void ShowNextLine()
        {
            lineIndex++;
            if (group == null || lineIndex >= group.lines.Count)
            {
                StopPlayback(true);
                return;
            }

            DialogueLine line = group.lines[lineIndex];
            currentText = line == null || line.text == null ? string.Empty : line.text;
            speakerLabel.text = line == null || string.IsNullOrEmpty(line.speaker) ? string.Empty : line.speaker;
            speakerLabel.gameObject.SetActive(!string.IsNullOrEmpty(speakerLabel.text));
            ApplyPortrait(line);
            contentLabel.text = currentText;
            contentLabel.maxVisibleCharacters = 0;
            continueHint.gameObject.SetActive(false);
            typingDone = false;
            autoSkipRemaining = -1f;
            visibleChars = 0;
            revealTimer = 0f;
            nextDelay = 0f;

            if (ShouldShowInstant(currentText))
            {
                RevealAll();
                ArmAutoSkip(line);
                return;
            }

            revealing = true;
        }

        void TickReveal()
        {
            revealTimer += Time.unscaledDeltaTime;
            while (revealing && revealTimer >= nextDelay)
            {
                revealTimer -= nextDelay;
                if (visibleChars >= currentText.Length)
                {
                    FinishTyping();
                    return;
                }

                char ch = currentText[visibleChars];
                visibleChars++;
                contentLabel.maxVisibleCharacters = visibleChars;
                nextDelay = CharDelay(ch, CurrentLine());
            }
        }

        void RevealAll()
        {
            revealing = false;
            visibleChars = currentText.Length;
            contentLabel.maxVisibleCharacters = int.MaxValue;
            FinishTyping();
        }

        void FinishTyping()
        {
            revealing = false;
            typingDone = true;
            contentLabel.maxVisibleCharacters = int.MaxValue;
            continueHint.gameObject.SetActive(true);
            ArmAutoSkip(CurrentLine());
        }

        void ArmAutoSkip(DialogueLine line)
        {
            autoSkipRemaining = line != null && line.autoSkip > 0f ? line.autoSkip : -1f;
        }

        void ApplyPortrait(DialogueLine line)
        {
            if (portraitImage == null) return;
            string avatar = line == null ? string.Empty : line.avatar;
            Sprite sprite = string.IsNullOrEmpty(avatar)
                ? null
                : Resources.Load<Sprite>("Dialogue/Portraits/" + avatar);
            portraitImage.sprite = sprite;
            portraitImage.enabled = sprite != null;
            float left = sprite != null ? 400f : 72f;
            SetOffsets(speakerLabel.rectTransform, new Vector2(left, -88f), new Vector2(-72f, -36f));
            SetOffsets(contentLabel.rectTransform, new Vector2(left, 56f), new Vector2(-72f, -108f));
        }

        static void SetOffsets(RectTransform rect, Vector2 min, Vector2 max)
        {
            if (rect == null) return;
            rect.offsetMin = min;
            rect.offsetMax = max;
        }

        DialogueLine CurrentLine()
        {
            if (group == null || lineIndex < 0 || lineIndex >= group.lines.Count) return null;
            return group.lines[lineIndex];
        }

        void StopPlayback(bool completed)
        {
            revealing = false;
            typingDone = true;
            group = null;
            lineIndex = -1;
            autoSkipRemaining = -1f;
            Action done = onComplete;
            onComplete = null;
            gameObject.SetActive(false);
            NotifyPlaying();
            if (completed && done != null) done();
        }

        static bool ShouldShowInstant(string text)
        {
            if (string.IsNullOrEmpty(text)) return true;
            return text.Length <= ShortLineInstantChars;
        }

        static float CharDelay(char ch, DialogueLine line)
        {
            MoodPreset mood = ResolveMood(line);
            float cps = line != null && line.cps > 0f ? line.cps : DefaultCps * mood.cpsMul;
            float baseDelay = 1f / Mathf.Max(1f, cps);
            float punct;
            if (!PunctDelayMs.TryGetValue(ch, out punct)) return baseDelay;
            return baseDelay + punct * 0.001f * mood.pauseMul;
        }

        static MoodPreset ResolveMood(DialogueLine line)
        {
            string key = line == null || string.IsNullOrEmpty(line.mood) ? "neutral" : line.mood.Trim().ToLowerInvariant();
            MoodPreset preset;
            if (Moods.TryGetValue(key, out preset)) return preset;
            return Moods["neutral"];
        }

        struct MoodPreset
        {
            public readonly float cpsMul;
            public readonly float pauseMul;

            public MoodPreset(float cpsMul, float pauseMul)
            {
                this.cpsMul = cpsMul;
                this.pauseMul = pauseMul;
            }
        }
    }
}
