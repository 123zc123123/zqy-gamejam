using UnityEngine;
using UnityEngine.UIElements;

namespace DouQuqu
{
    /// <summary>合成场景流程：隐藏对战方向盘、写入图鉴，并提供返回主界面的入口。</summary>
    [DefaultExecutionOrder(-100)]
    public sealed class DouQuquMergeSceneController : MonoBehaviour
    {
        [SerializeField] private DouQuquMergeBoard board;
        [SerializeField] private UIDocument uiDocument;

        private Button backButton;

        private void Awake()
        {
            if (board == null) board = GetComponent<DouQuquMergeBoard>();
            if (board == null) board = FindObjectOfType<DouQuquMergeBoard>();
            if (uiDocument == null) uiDocument = GetComponent<UIDocument>();
            if (uiDocument == null) uiDocument = FindObjectOfType<UIDocument>();
        }

        private void OnEnable()
        {
            if (board != null) board.DrawCompleted += RecordCricket;
        }

        private void Start()
        {
            if (!DouQuquPlayerDataService.RequireLogin()) return;
            if (uiDocument != null && uiDocument.enabled) BindUi();
        }

        private void OnDisable()
        {
            if (board != null) board.DrawCompleted -= RecordCricket;
            if (backButton != null) backButton.clicked -= Back;
        }

        private void BindUi()
        {
            if (uiDocument == null || uiDocument.rootVisualElement == null) return;
            VisualElement root = uiDocument.rootVisualElement;
            VisualElement directionPad = root.Q<VisualElement>("direction-pad");
            VisualElement touchHint = root.Q<VisualElement>("touch-hint");
            if (directionPad != null) directionPad.style.display = DisplayStyle.None;
            if (touchHint != null) touchHint.style.display = DisplayStyle.None;

            // 合成从对战 HUD 中独立成场景后，将棋盘面板放到屏幕中央。
            VisualElement mergeRoot = root.Q<VisualElement>("merge-root");
            if (mergeRoot != null)
            {
                mergeRoot.style.top = 28f;
                mergeRoot.style.right = StyleKeyword.Auto;
                mergeRoot.style.left = new Length(50f, LengthUnit.Percent);
                mergeRoot.style.translate = new Translate(new Length(-50f, LengthUnit.Percent), 0f);
            }

            backButton = root.Q<Button>("merge-back-button");
            if (backButton == null && mergeRoot != null)
            {
                backButton = new Button { name = "merge-back-button", text = "返回主界面" };
                backButton.AddToClassList("merge-back-button");
                mergeRoot.Add(backButton);
            }
            if (backButton != null) backButton.clicked += Back;
        }

        private void RecordCricket(MergeDrawResult result)
        {
            DouQuquPlayerDataService.RecordCricket(result.weightedValue, result.uniformValue);
        }

        private void Back()
        {
            DouQuquSceneNames.Load(DouQuquSceneNames.MainMenu);
        }
    }
}
