using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace DouQuqu
{
    /// <summary>
    /// DeadZone narrative 对白 markdown 解析。
    /// 其他 AI 改对白只改 Resources/Dialogue 下的 txt，不要改本文件。
    /// </summary>
    public static class DialogueMarkdown
    {
        public static DialogueGroup Parse(string source, string fallbackId)
        {
            if (string.IsNullOrWhiteSpace(source)) return null;
            string text = source.Replace("\r\n", "\n").Replace('\r', '\n').Trim();
            if (text.Length == 0) return null;
            if (text[0] == '{') return ParseJson(text);

            DialogueGroup group = new DialogueGroup { id = fallbackId };
            int bodyStart = 0;
            if (text.StartsWith("---", StringComparison.Ordinal))
            {
                int end = text.IndexOf("\n---", 3, StringComparison.Ordinal);
                if (end >= 0)
                {
                    ParseFrontMatter(text.Substring(3, end - 3), group);
                    bodyStart = end + 4;
                    if (bodyStart < text.Length && text[bodyStart] == '\n') bodyStart++;
                }
            }

            ParseBeats(text.Substring(bodyStart), group);
            if (string.IsNullOrEmpty(group.id)) group.id = fallbackId;
            return group.lines.Count == 0 ? null : group;
        }

        static DialogueGroup ParseJson(string text)
        {
            try
            {
                DialogueGroup group = JsonUtility.FromJson<DialogueGroup>(text);
                if (group == null || group.lines == null || group.lines.Count == 0) return null;
                return group;
            }
            catch (Exception)
            {
                return null;
            }
        }

        static void ParseFrontMatter(string block, DialogueGroup group)
        {
            string[] rows = block.Split('\n');
            for (int i = 0; i < rows.Length; i++)
            {
                string row = rows[i].Trim();
                if (row.Length == 0 || row.StartsWith("#", StringComparison.Ordinal)) continue;
                int colon = IndexOfYamlColon(row);
                if (colon <= 0) continue;
                string key = row.Substring(0, colon).Trim();
                string value = Unquote(row.Substring(colon + 1).Trim());
                if (key == "id") group.id = value;
                else if (key == "title") group.title = value;
            }
        }

        static void ParseBeats(string body, DialogueGroup group)
        {
            string[] rows = body.Split('\n');
            BeatDraft current = null;
            for (int i = 0; i < rows.Length; i++)
            {
                string raw = rows[i];
                string trimmed = raw.Trim();
                if (trimmed.StartsWith("##", StringComparison.Ordinal))
                {
                    FlushBeat(group, current);
                    current = new BeatDraft();
                    current.id = ParseBeatId(trimmed);
                    continue;
                }

                if (current == null) continue;
                if (trimmed.StartsWith(">", StringComparison.Ordinal))
                {
                    ParseMetaLine(trimmed.Substring(1).Trim(), current);
                    continue;
                }

                if (TryBareMetaLine(trimmed, current)) continue;

                if (trimmed.Length == 0)
                {
                    if (current.body.Length > 0) current.body.Append('\n');
                    continue;
                }

                current.body.AppendLine(trimmed);
            }

            FlushBeat(group, current);
        }

        static readonly string[] MetaKeys = { "mood", "auto_skip", "autoskip", "cps", "speaker", "speaker_name", "text", "avatar" };

        static bool TryBareMetaLine(string line, BeatDraft beat)
        {
            int colon = IndexOfYamlColon(line);
            if (colon <= 0) return false;
            string key = line.Substring(0, colon).Trim().ToLowerInvariant();
            if (!IsMetaKey(key)) return false;
            ParseMetaLine(line, beat);
            return true;
        }

        static bool IsMetaKey(string key)
        {
            for (int i = 0; i < MetaKeys.Length; i++)
                if (MetaKeys[i] == key) return true;
            return false;
        }

        static void ParseMetaLine(string line, BeatDraft beat)
        {
            int colon = IndexOfYamlColon(line);
            if (colon <= 0) return;
            string key = line.Substring(0, colon).Trim().ToLowerInvariant();
            string value = Unquote(line.Substring(colon + 1).Trim());
            if (key == "mood") beat.mood = value;
            else if (key == "auto_skip" || key == "autoskip")
            {
                float seconds;
                if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out seconds))
                    beat.autoSkip = seconds;
            }
            else if (key == "cps")
            {
                float cps;
                if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out cps))
                    beat.cps = cps;
            }
            else if (key == "speaker" || key == "speaker_name") beat.speaker = value;
            else if (key == "avatar") beat.avatar = value;
            else if (key == "text") beat.body.AppendLine(value);
        }

        static void FlushBeat(DialogueGroup group, BeatDraft beat)
        {
            if (beat == null) return;
            string body = beat.body.ToString().Trim();
            if (body.Length == 0) return;
            string speaker = beat.speaker;
            string text = body;
            SplitSpeaker(body, ref speaker, ref text);
            if (string.IsNullOrWhiteSpace(text)) return;
            group.lines.Add(new DialogueLine
            {
                id = string.IsNullOrEmpty(beat.id) ? (group.lines.Count + 1).ToString() : beat.id,
                speaker = speaker ?? string.Empty,
                text = text.Trim(),
                mood = string.IsNullOrEmpty(beat.mood) ? "neutral" : beat.mood,
                avatar = beat.avatar ?? string.Empty,
                autoSkip = beat.autoSkip,
                cps = beat.cps
            });
        }

        static void SplitSpeaker(string body, ref string speaker, ref string text)
        {
            string[] rows = body.Split('\n');
            StringBuilder content = new StringBuilder();
            for (int i = 0; i < rows.Length; i++)
            {
                string row = rows[i].Trim();
                if (row.Length == 0) continue;
                int colon = IndexOfSpeakerColon(row);
                if (colon > 0 && colon <= 12)
                {
                    string maybe = row.Substring(0, colon).Trim();
                    if (LooksLikeSpeaker(maybe))
                    {
                        if (string.IsNullOrEmpty(speaker)) speaker = maybe;
                        row = row.Substring(colon + 1).Trim();
                    }
                }
                if (content.Length > 0) content.Append('\n');
                content.Append(row);
            }

            text = content.ToString();
        }

        static bool LooksLikeSpeaker(string value)
        {
            if (string.IsNullOrEmpty(value)) return false;
            if (value.IndexOf(' ') >= 0) return false;
            if (IsMetaKey(value.ToLowerInvariant())) return false;
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (char.IsLetterOrDigit(c) || c == '_' || c > 127) continue;
                return false;
            }
            return true;
        }

        static string ParseBeatId(string heading)
        {
            string title = heading.TrimStart('#').Trim();
            int dot = title.IndexOf('.');
            if (dot > 0)
            {
                string num = title.Substring(0, dot).Trim();
                int parsed;
                if (int.TryParse(num, out parsed)) return parsed.ToString();
            }
            return title;
        }

        static int IndexOfYamlColon(string row)
        {
            int ascii = row.IndexOf(':');
            int wide = row.IndexOf('：');
            if (ascii < 0) return wide;
            if (wide < 0) return ascii;
            return Math.Min(ascii, wide);
        }

        static int IndexOfSpeakerColon(string row)
        {
            int wide = row.IndexOf('：');
            if (wide > 0) return wide;
            return row.IndexOf(':');
        }

        static string Unquote(string value)
        {
            if (value.Length >= 2)
            {
                char first = value[0];
                char last = value[value.Length - 1];
                if ((first == '"' && last == '"') || (first == '\'' && last == '\''))
                    return value.Substring(1, value.Length - 2);
            }
            return value;
        }

        sealed class BeatDraft
        {
            public string id;
            public string speaker;
            public string mood;
            public string avatar;
            public float autoSkip;
            public float cps;
            public readonly StringBuilder body = new StringBuilder();
        }
    }
}
