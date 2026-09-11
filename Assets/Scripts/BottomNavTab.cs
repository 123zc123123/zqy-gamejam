using UnityEngine;
using UnityEngine.UI;

namespace DouQuqu
{
    /// <summary>
    /// 底栏功能入口共用一份预制体。实例只改模块、显示名和 icon 路径。
    /// </summary>
    [ExecuteAlways]
    public sealed class BottomNavTab : MonoBehaviour
    {
        public enum NavModule
        {
            Battle = 0,
            Breeding = 1,
            Registry = 2,
            Ranking = 3,
            Shop = 4,
            Academy = 5
        }

        private static readonly Color IdleBg = Color.white;
        private static readonly Color IdleText = new Color(0.898f, 0.768f, 0.561f, 1f);
        private static readonly Color SelectedText = new Color(0.22f, 0.13f, 0.07f, 1f);

        [SerializeField] private NavModule module;
        [SerializeField] private string displayName;
        [SerializeField] private string iconPath;
        [SerializeField] private Image icon;
        [SerializeField] private Text label;
        [SerializeField] private Image selectedBackground;

        private Image background;
        private bool selected;

        public NavModule ModuleId => module;
        public string DisplayName => displayName;
        public string IconPath => iconPath;
        public Button Button => GetComponent<Button>();
        public bool IsSelected => selected;

        private void Awake()
        {
            ApplyVisuals();
            ApplySelectedColors();
        }

        private void OnValidate()
        {
            ApplyVisuals();
            ApplySelectedColors();
        }

        public void Configure(NavModule moduleId, string name, string path)
        {
            module = moduleId;
            displayName = name;
            iconPath = path ?? "";
            ApplyVisuals();
            ApplySelectedColors();
        }

        public void SetSelected(bool on)
        {
            selected = on;
            CacheParts();
            ApplySelectedColors();
        }

        public void ApplyVisuals()
        {
            CacheParts();

            if (label != null && !string.IsNullOrEmpty(displayName))
                label.text = displayName;

            ApplyIcon();
        }

        private void CacheParts()
        {
            if (label == null)
            {
                Transform found = transform.Find("Label");
                if (found == null) found = transform.Find("text");
                if (found != null) label = found.GetComponent<Text>();
                if (label == null) label = GetComponentInChildren<Text>(true);
            }

            if (icon == null)
            {
                Transform found = transform.Find("Icon");
                if (found != null) icon = found.GetComponent<Image>();
            }

            if (background == null) background = GetComponent<Image>();
            if (selectedBackground == null)
            {
                Transform found = transform.Find("Selected");
                if (found != null) selectedBackground = found.GetComponent<Image>();
            }
        }

        private void ApplySelectedColors()
        {
            CacheParts();
            if (!Application.isPlaying)
            {
                if (label != null) label.color = IdleText;
                return;
            }

            if (background != null)
            {
                background.color = selected ? new Color(1f, 1f, 1f, 0f) : IdleBg;
                background.raycastTarget = true;
            }

            if (selectedBackground != null)
            {
                selectedBackground.gameObject.SetActive(selected);
                selectedBackground.raycastTarget = false;
            }

            if (label != null)
                label.color = selected ? SelectedText : IdleText;
        }

        private void ApplyIcon()
        {
            if (icon == null) return;
            Sprite sprite = LoadIcon();
            if (sprite == null) sprite = icon.sprite;
            icon.sprite = sprite;
            icon.enabled = sprite != null;
            icon.preserveAspect = true;
            icon.raycastTarget = false;
        }

        private Sprite LoadIcon()
        {
            if (string.IsNullOrEmpty(iconPath)) return null;

            Sprite sprite = Resources.Load<Sprite>(iconPath);
            if (sprite != null) return sprite;

            int resourcesIndex = iconPath.Replace('\\', '/').IndexOf("/Resources/", System.StringComparison.OrdinalIgnoreCase);
            if (resourcesIndex >= 0)
            {
                string relative = iconPath.Substring(resourcesIndex + "/Resources/".Length);
                int dot = relative.LastIndexOf('.');
                if (dot >= 0) relative = relative.Substring(0, dot);
                sprite = Resources.Load<Sprite>(relative);
                if (sprite != null) return sprite;
            }

#if UNITY_EDITOR
            sprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(iconPath);
            if (sprite != null) return sprite;
            Texture2D texture = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(iconPath);
            if (texture != null)
                return Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
#endif
            return null;
        }
    }
}
