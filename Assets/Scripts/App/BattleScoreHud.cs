using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DouQuqu
{
    /// <summary>
    /// 战斗 HUD 玩家卡：头像=当前上场虫，三槽=roster 立绘，出圈变灰打叉。
    /// 积分在名字旁，连杀在头像右上。
    /// </summary>
    public sealed class BattleScoreHud : MonoBehaviour
    {
        const int PlayerCount = 4;
        const int SlotCount = 3;
        const float BadgeSize = 44f;
        static readonly Color AliveColor = Color.white;
        static readonly Color WaitingColor = new Color(1f, 1f, 1f, 0.92f);
        static readonly Color DeadColor = new Color(0.32f, 0.32f, 0.32f, 0.82f);
        static readonly Color DeadMarkColor = new Color(0.92f, 0.22f, 0.18f, 0.92f);
        static readonly Color CurrentOutline = new Color(1f, 0.86f, 0.28f, 0.95f);

        MatchController match;
        readonly CardView[] cards = new CardView[PlayerCount];
        readonly int[] lastScore = { int.MinValue, int.MinValue, int.MinValue, int.MinValue };
        readonly int[] lastStreak = { int.MinValue, int.MinValue, int.MinValue, int.MinValue };
        readonly int[] lastAvatarKey = { int.MinValue, int.MinValue, int.MinValue, int.MinValue };
        readonly int[] lastSlotKey = new int[PlayerCount * SlotCount];
        readonly string[] lastNames = new string[PlayerCount];
        bool namedOnce;

        public void Bind(MatchController matchController)
        {
            match = matchController;
            CacheCards();
            namedOnce = false;
            for (int i = 0; i < lastSlotKey.Length; i++) lastSlotKey[i] = int.MinValue;
            for (int i = 0; i < PlayerCount; i++)
            {
                lastScore[i] = int.MinValue;
                lastStreak[i] = int.MinValue;
                lastAvatarKey[i] = int.MinValue;
                lastNames[i] = null;
            }
            Refresh(true);
        }

        void LateUpdate()
        {
            Refresh(false);
        }

        void CacheCards()
        {
            for (int i = 0; i < PlayerCount; i++)
            {
                if (cards[i] != null && cards[i].root != null) continue;
                Transform card = FindPlayerCard(transform, i + 1);
                if (card == null) continue;
                cards[i] = ReadCard(card, i);
            }
        }

        void Refresh(bool force)
        {
            CacheCards();
            if (match == null) match = Object.FindObjectOfType<MatchController>();
            if (!namedOnce || force)
            {
                BindNames();
                namedOnce = true;
            }

            for (int i = 0; i < PlayerCount; i++)
            {
                CardView card = cards[i];
                if (card == null) continue;

                int score = match != null ? match.MatchScore(i) : 0;
                int streak = match != null ? match.KillStreak(i) : 0;
                if (card.score != null && (force || score != lastScore[i]))
                {
                    card.score.text = score.ToString();
                    lastScore[i] = score;
                }

                if (card.streak != null && (force || streak != lastStreak[i]))
                {
                    lastStreak[i] = streak;
                    card.streak.text = streak > 0 ? streak.ToString() : "";
                    card.streak.gameObject.SetActive(streak > 0);
                }

                bool started = match != null && match.IsStarted;
                bool inMatch = !started || match.PlayerStillIn(i);
                int current = match != null ? match.CricketIndex(i) : 0;
                bool currentAlive = !started || CurrentAlive(i);
                PaintAvatar(card, i, current, inMatch, currentAlive, force);
                PaintSlots(card, i, current, inMatch, currentAlive, force);
                if (card.group != null) card.group.alpha = inMatch ? 1f : 0.55f;
            }
        }

        void BindNames()
        {
            for (int i = 0; i < PlayerCount; i++)
            {
                CardView card = cards[i];
                if (card == null || card.playerName == null) continue;
                string name = DisplayName(i);
                if (name == lastNames[i]) continue;
                lastNames[i] = name;
                card.playerName.text = name;
            }
        }

        void PaintAvatar(CardView card, int playerId, int current, bool inMatch, bool currentAlive, bool force)
        {
            if (card.avatar == null) return;
            CricketPick pick = match != null ? match.RosterPick(playerId, current) : null;
            int key = AvatarKey(pick, inMatch, currentAlive);
            if (!force && key == lastAvatarKey[playerId]) return;
            lastAvatarKey[playerId] = key;

            Sprite portrait = pick != null ? CricketCatalog.Portrait(pick.quality, pick.temperament) : null;
            if (portrait != null) card.avatar.sprite = portrait;
            card.avatar.preserveAspect = true;
            bool dead = !inMatch || !currentAlive;
            card.avatar.color = dead ? DeadColor : AliveColor;
        }

        void PaintSlots(CardView card, int playerId, int current, bool inMatch, bool currentAlive, bool force)
        {
            if (card.slots == null) return;
            for (int s = 0; s < SlotCount; s++)
            {
                SlotView slot = card.slots[s];
                if (slot == null) continue;
                CricketPick pick = match != null ? match.RosterPick(playerId, s) : null;
                SlotLife life = Classify(s, current, inMatch, currentAlive);
                int key = SlotKey(pick, life);
                int index = playerId * SlotCount + s;
                if (!force && key == lastSlotKey[index]) continue;
                lastSlotKey[index] = key;

                Sprite portrait = pick != null ? CricketCatalog.Portrait(pick.quality, pick.temperament) : null;
                if (slot.portrait != null)
                {
                    slot.portrait.enabled = true;
                    slot.portrait.preserveAspect = true;
                    if (portrait != null) slot.portrait.sprite = portrait;
                    slot.portrait.color = ColorFor(life);
                }

                if (slot.frame != null)
                    slot.frame.color = life == SlotLife.Dead ? new Color(0.55f, 0.55f, 0.55f, 1f) : Color.white;

                if (slot.deadMark != null)
                {
                    bool dead = life == SlotLife.Dead;
                    slot.deadMark.gameObject.SetActive(dead);
                    if (dead) slot.deadMark.text = "×";
                }

                if (slot.outline != null)
                {
                    bool currentSlot = life == SlotLife.Current;
                    slot.outline.enabled = currentSlot;
                    if (currentSlot) slot.outline.effectColor = CurrentOutline;
                }
            }
        }

        SlotLife Classify(int slot, int current, bool inMatch, bool currentAlive)
        {
            if (!inMatch || slot < current) return SlotLife.Dead;
            if (slot > current) return SlotLife.Waiting;
            return currentAlive ? SlotLife.Current : SlotLife.Dead;
        }

        static Color ColorFor(SlotLife life)
        {
            if (life == SlotLife.Dead) return DeadColor;
            if (life == SlotLife.Waiting) return WaitingColor;
            return AliveColor;
        }

        bool CurrentAlive(int playerId)
        {
            if (match == null || match.Bugs == null || playerId < 0 || playerId >= match.Bugs.Length) return false;
            BugState bug = match.Bugs[playerId];
            return bug != null && bug.alive;
        }

        static int AvatarKey(CricketPick pick, bool inMatch, bool currentAlive)
        {
            int q = pick == null ? 0 : pick.quality;
            int t = pick == null ? 0 : pick.temperament;
            int id = pick == null ? 0 : pick.catalogId;
            int flags = (inMatch ? 1 : 0) | (currentAlive ? 2 : 0);
            return (id << 16) ^ (q << 8) ^ (t << 4) ^ flags;
        }

        static int SlotKey(CricketPick pick, SlotLife life)
        {
            int q = pick == null ? 0 : pick.quality;
            int t = pick == null ? 0 : pick.temperament;
            int id = pick == null ? 0 : pick.catalogId;
            return (id << 16) ^ (q << 8) ^ (t << 4) ^ (int)life;
        }

        string DisplayName(int playerId)
        {
            LanSession net = AppServices.Instance != null ? AppServices.Instance.Network : null;
            int localId = net != null && net.LocalPlayerId >= 0 ? net.LocalPlayerId : 0;
            if (net != null && net.Slots != null)
            {
                for (int i = 0; i < net.Slots.Count; i++)
                {
                    LanPlayerSlot slot = net.Slots[i];
                    if (slot != null && slot.playerId == playerId && !string.IsNullOrEmpty(slot.playerName))
                        return slot.playerName;
                }
            }

            if (playerId == localId)
            {
                if (PlayerDataService.IsLoggedIn && !string.IsNullOrEmpty(PlayerDataService.CurrentPlayerName))
                    return PlayerDataService.CurrentPlayerName;
                return "玩家" + (playerId + 1);
            }

            int bot = playerId > localId ? playerId - 1 : playerId;
            return TrainingCamp.BotName(bot);
        }

        CardView ReadCard(Transform card, int playerIndex)
        {
            CardView view = new CardView { root = card };
            CanvasGroup group = card.GetComponent<CanvasGroup>();
            if (group == null) group = card.gameObject.AddComponent<CanvasGroup>();
            view.group = group;

            Transform scoreNode = FindNamed(card, "Score");
            view.score = scoreNode != null ? scoreNode.GetComponent<TMP_Text>() : null;
            Transform nameNode = FindNamed(card, "PlayerName");
            view.playerName = nameNode != null ? nameNode.GetComponent<TMP_Text>() : null;

            Transform avatar = FindNamed(card, "Avatar");
            view.avatar = avatar != null ? avatar.GetComponent<Image>() : null;
            Transform avatarFrame = FindNamed(card, "AvatarFrame");
            view.streak = EnsureBadge(avatarFrame as RectTransform, view.score);

            view.slots = new SlotView[SlotCount];
            for (int s = 0; s < SlotCount; s++)
            {
                Transform cricket = FindNamed(card, "Cricket" + (s + 1));
                view.slots[s] = EnsureSlot(cricket, view.score);
            }

            return view;
        }

        static SlotView EnsureSlot(Transform cricket, TMP_Text sample)
        {
            if (cricket == null) return null;
            Image frame = cricket.GetComponent<Image>();
            Mask mask = cricket.GetComponent<Mask>();
            if (mask == null) mask = cricket.gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = true;

            Transform portraitNode = cricket.Find("Portrait");
            RectTransform portraitRect;
            if (portraitNode == null)
            {
                GameObject go = new GameObject("Portrait", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                portraitRect = go.GetComponent<RectTransform>();
                portraitRect.SetParent(cricket, false);
            }
            else
            {
                portraitRect = portraitNode as RectTransform;
            }

            portraitRect.anchorMin = Vector2.zero;
            portraitRect.anchorMax = Vector2.one;
            portraitRect.offsetMin = new Vector2(6f, 6f);
            portraitRect.offsetMax = new Vector2(-6f, -6f);
            portraitRect.localScale = Vector3.one;
            portraitRect.SetAsFirstSibling();
            Image portrait = portraitRect.GetComponent<Image>();
            if (portrait == null) portrait = portraitRect.gameObject.AddComponent<Image>();
            portrait.raycastTarget = false;
            portrait.preserveAspect = true;

            Transform markNode = cricket.Find("DeadMark");
            RectTransform markRect;
            if (markNode == null)
            {
                GameObject go = new GameObject("DeadMark", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                markRect = go.GetComponent<RectTransform>();
                markRect.SetParent(cricket, false);
            }
            else
            {
                markRect = markNode as RectTransform;
            }

            markRect.anchorMin = Vector2.zero;
            markRect.anchorMax = Vector2.one;
            markRect.offsetMin = Vector2.zero;
            markRect.offsetMax = Vector2.zero;
            markRect.localScale = Vector3.one;
            markRect.SetAsLastSibling();
            TMP_Text deadMark = markRect.GetComponent<TMP_Text>();
            deadMark.alignment = TextAlignmentOptions.Midline;
            deadMark.fontSize = 48f;
            deadMark.fontStyle = FontStyles.Bold;
            deadMark.color = DeadMarkColor;
            deadMark.raycastTarget = false;
            deadMark.enableWordWrapping = false;
            deadMark.overflowMode = TextOverflowModes.Overflow;
            if (sample != null && sample.font != null)
            {
                deadMark.font = sample.font;
                deadMark.fontSharedMaterial = sample.fontSharedMaterial;
            }

            Outline outline = cricket.GetComponent<Outline>();
            if (outline == null) outline = cricket.gameObject.AddComponent<Outline>();
            outline.effectDistance = new Vector2(2.2f, -2.2f);
            outline.enabled = false;

            return new SlotView
            {
                frame = frame,
                portrait = portrait,
                deadMark = deadMark,
                outline = outline
            };
        }

        static TMP_Text EnsureBadge(RectTransform avatar, TMP_Text scoreSample)
        {
            if (avatar == null) return null;
            Transform existing = avatar.Find("KillStreak");
            RectTransform rect;
            if (existing != null)
            {
                rect = existing as RectTransform;
            }
            else
            {
                GameObject go = new GameObject("KillStreak", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                rect = go.GetComponent<RectTransform>();
                rect.SetParent(avatar, false);
            }

            rect.anchorMin = rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(6f, 6f);
            rect.sizeDelta = new Vector2(BadgeSize, BadgeSize);
            rect.localScale = Vector3.one;
            rect.SetAsLastSibling();

            TMP_Text text = rect.GetComponent<TMP_Text>();
            text.alignment = TextAlignmentOptions.Midline;
            text.fontSize = 28f;
            text.fontStyle = FontStyles.Bold;
            text.color = new Color(1f, 0.86f, 0.28f, 1f);
            text.raycastTarget = false;
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Overflow;
            if (scoreSample != null && scoreSample.font != null)
            {
                text.font = scoreSample.font;
                text.fontSharedMaterial = scoreSample.fontSharedMaterial;
            }

            Outline outline = rect.GetComponent<Outline>();
            if (outline == null) outline = rect.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.15f, 0.08f, 0f, 0.9f);
            outline.effectDistance = new Vector2(1.2f, -1.2f);
            return text;
        }

        static Transform FindPlayerCard(Transform root, int playerNumber)
        {
            Transform card = FindNamed(root, "Player" + playerNumber);
            if (card == null) card = FindNamed(root, "FigmaPlayer" + playerNumber);
            return card;
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

        enum SlotLife
        {
            Waiting = 0,
            Current = 1,
            Dead = 2
        }

        sealed class CardView
        {
            public Transform root;
            public CanvasGroup group;
            public TMP_Text playerName;
            public TMP_Text score;
            public TMP_Text streak;
            public Image avatar;
            public SlotView[] slots;
        }

        sealed class SlotView
        {
            public Image frame;
            public Image portrait;
            public TMP_Text deadMark;
            public Outline outline;
        }
    }
}
