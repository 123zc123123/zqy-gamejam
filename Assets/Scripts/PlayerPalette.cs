using UnityEngine;
using UnityEngine.UI;

namespace DouQuqu
{
    /// <summary>
    /// P1–P4 身份色。调色只改这份资源：Resources/Common/PlayerPalette。
    /// 描边用在进房/选虫/战斗/结算头像框；背景和圆圈只用在局内。
    /// </summary>
    [CreateAssetMenu(menuName = "DouQuqu/Player Palette", fileName = "PlayerPalette")]
    public sealed class PlayerPalette : ScriptableObject
    {
        public const string ResourcePath = "Common/PlayerPalette";
        public const string AvatarResource = "Common/Textures/PlayerAvatar";
        public const int PlayerCount = 4;

        [System.Serializable]
        public sealed class Slot
        {
            [InspectorCn("描边", "头像框 outline，比局内色更亮")]
            public Color outline;
            [InspectorCn("局内头像框背景", "战斗 HUD 玩家卡底色")]
            public Color hudBackground;
            [InspectorCn("局内蛐蛐圆圈", "脚下定位圆")]
            public Color circle;
        }

        [InspectorCn("P1 棕金")]
        public Slot p1 = DefaultP1();
        [InspectorCn("P2 红")]
        public Slot p2 = DefaultP2();
        [InspectorCn("P3 绿")]
        public Slot p3 = DefaultP3();
        [InspectorCn("P4 蓝")]
        public Slot p4 = DefaultP4();

        static PlayerPalette cached;
        static readonly Slot[] Fallback = { DefaultP1(), DefaultP2(), DefaultP3(), DefaultP4() };

        public static PlayerPalette Current
        {
            get
            {
                if (cached == null) cached = Resources.Load<PlayerPalette>(ResourcePath);
                return cached;
            }
        }

        public static Color OutlineColor(int playerId)
        {
            return Resolve(playerId).outline;
        }

        public static Color HudBackground(int playerId)
        {
            return Resolve(playerId).hudBackground;
        }

        public static Color Circle(int playerId)
        {
            return Resolve(playerId).circle;
        }

        /// <summary>PlayerFrame 里的头像贴图是玩家形象，不是上场虫。</summary>
        public static void BindAvatar(Transform root, bool visible = true)
        {
            if (root == null) return;
            Transform portrait = FindNamed(root, "头像贴图") ?? FindNamed(root, "Avatar");
            if (portrait == null)
            {
                Transform avatarRoot = FindNamed(root, "avatar");
                if (avatarRoot != null)
                {
                    Transform nested = avatarRoot.Find("avatar");
                    portrait = nested != null ? nested : avatarRoot;
                }
            }

            if (portrait == null) return;
            Image image = portrait.GetComponent<Image>();
            if (image == null) image = portrait.GetComponentInChildren<Image>(true);
            if (image == null) return;
            if (!visible)
            {
                image.enabled = false;
                return;
            }

            image.enabled = true;
            image.preserveAspect = true;
            Sprite sprite = Resources.Load<Sprite>(AvatarResource);
            if (sprite != null) image.sprite = sprite;
        }

        public static void PaintOutline(Transform root, int playerId)
        {
            if (root == null) return;
            Color color = OutlineColor(playerId);
            Transform stroke = FindNamed(root, "外边描线");
            if (stroke != null)
            {
                Image image = stroke.GetComponent<Image>();
                if (image != null) image.color = color;
            }

            Transform border = FindNamed(root, "avatar-border");
            if (border != null)
            {
                Outline outline = border.GetComponent<Outline>();
                if (outline != null) outline.effectColor = color;
            }
        }

        public static void PaintHudCard(Transform card, int playerId)
        {
            if (card == null) return;
            PaintOutline(card, playerId);
            Transform bg = FindNamed(card, "Background");
            if (bg == null) bg = FindNamed(card, "背景");
            if (bg == null) return;
            Image image = bg.GetComponent<Image>();
            if (image != null) image.color = HudBackground(playerId);
        }

        public static void PaintBattleHud(Transform hudRoot)
        {
            if (hudRoot == null) return;
            for (int i = 0; i < PlayerCount; i++)
            {
                Transform card = FindNamed(hudRoot, "Player" + (i + 1));
                if (card == null) card = FindNamed(hudRoot, "FigmaPlayer" + (i + 1));
                PaintHudCard(card, i);
            }
        }

        void Reset()
        {
            p1 = DefaultP1();
            p2 = DefaultP2();
            p3 = DefaultP3();
            p4 = DefaultP4();
        }

        Slot SlotAt(int index)
        {
            if (index == 0) return p1;
            if (index == 1) return p2;
            if (index == 2) return p3;
            return p4;
        }

        static Slot Resolve(int playerId)
        {
            int index = Mathf.Abs(playerId) % PlayerCount;
            PlayerPalette palette = Current;
            Slot slot = palette != null ? palette.SlotAt(index) : null;
            return slot ?? Fallback[index];
        }

        static Slot DefaultP1()
        {
            return new Slot
            {
                outline = new Color(1.00f, 0.88f, 0.32f, 1f),
                hudBackground = new Color(0.90f, 0.68f, 0.08f, 0.61f),
                circle = new Color(0.90f, 0.68f, 0.08f, 1f)
            };
        }

        static Slot DefaultP2()
        {
            return new Slot
            {
                outline = new Color(0.98f, 0.42f, 0.40f, 1f),
                hudBackground = new Color(0.62f, 0.17f, 0.17f, 0.61f),
                circle = new Color(0.62f, 0.17f, 0.17f, 1f)
            };
        }

        static Slot DefaultP3()
        {
            return new Slot
            {
                outline = new Color(0.45f, 0.92f, 0.40f, 1f),
                hudBackground = new Color(0.18f, 0.35f, 0.15f, 0.61f),
                circle = new Color(0.18f, 0.35f, 0.15f, 1f)
            };
        }

        static Slot DefaultP4()
        {
            return new Slot
            {
                outline = new Color(0.62f, 0.76f, 1.00f, 1f),
                hudBackground = new Color(0.43f, 0.53f, 0.87f, 0.61f),
                circle = new Color(0.43f, 0.53f, 0.87f, 1f)
            };
        }

        static Transform FindNamed(Transform root, string objectName)
        {
            if (root == null) return null;
            if (root.name == objectName) return root;
            Transform direct = root.Find(objectName);
            if (direct != null) return direct;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform nested = FindNamed(root.GetChild(i), objectName);
                if (nested != null) return nested;
            }
            return null;
        }
    }
}
