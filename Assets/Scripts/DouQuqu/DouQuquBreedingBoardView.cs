using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DouQuqu
{
    /// <summary>
    /// 用美术育虫盘 Canvas 驱动 DouQuquMergeBoard。
    /// 规则仍在棋盘模型里，这里只负责 20 格显示、拖合成和导航按钮。
    /// </summary>
    public sealed class DouQuquBreedingBoardView : MonoBehaviour
    {
        public const int CellCount = 20;

        [SerializeField] private DouQuquMergeBoard board;
        [SerializeField] private GameObject canvasPrefab;
        [SerializeField] private bool autoReset = true;
        [SerializeField] private int randomSeed = 20260902;
        [SerializeField] private Sprite[] levelSprites;

        private readonly RectTransform[] cells = new RectTransform[CellCount];
        private readonly Image[] pieceImages = new Image[CellCount];
        private readonly Text[] pieceLabels = new Text[CellCount];
        private GameObject canvasInstance;
        private Image dragGhost;
        private Text goldText;
        private int draggingPieceId = -1;
        private int sourceCell = -1;

        private static readonly Color[] LevelColors =
        {
            new Color(0.76f, 0.62f, 0.38f, 1f),
            new Color(0.48f, 0.78f, 0.42f, 1f),
            new Color(0.82f, 0.48f, 0.22f, 1f)
        };

        private void Awake()
        {
            if (board == null) board = GetComponent<DouQuquMergeBoard>();
            EnsureEventSystem();
        }

        private void OnEnable()
        {
            if (board != null) board.BoardChanged += RefreshBoard;
        }

        private void Start()
        {
            SpawnCanvas();
            BindCells();
            BindHud();
            if (board == null) return;
            board.SetSize(4, 5);
            if (autoReset) board.ResetBoard(randomSeed);
            else RefreshBoard();
        }

        private void OnDisable()
        {
            if (board != null) board.BoardChanged -= RefreshBoard;
        }

        /// <summary>棋盘模型按美术 4×5 对齐。</summary>
        public void BeginDrag(int cellIndex, PointerEventData eventData)
        {
            if (board == null) return;
            MergePiece piece = FindPieceAt(cellIndex);
            if (piece == null) return;
            draggingPieceId = piece.id;
            sourceCell = cellIndex;
            ShowGhost(piece, eventData.position);
        }

        public void Drag(PointerEventData eventData)
        {
            if (dragGhost == null) return;
            dragGhost.rectTransform.position = eventData.position;
        }

        public void EndDrag(PointerEventData eventData)
        {
            if (draggingPieceId < 0)
            {
                HideGhost();
                return;
            }
            int target = HitCell(eventData.position);
            if (target >= 0 && board != null)
                board.TryMove(draggingPieceId, target);
            draggingPieceId = -1;
            sourceCell = -1;
            HideGhost();
        }

        private void SpawnCanvas()
        {
            if (canvasInstance != null) return;
            GameObject placed = GameObject.Find("BreedingBoard");
            if (placed == null) placed = GameObject.Find("BreedingBoardCanvas");
            if (placed != null)
            {
                canvasInstance = placed;
                return;
            }
            Canvas existing = FindObjectOfType<Canvas>();
            if (existing != null)
            {
                canvasInstance = existing.gameObject;
                return;
            }
            if (canvasPrefab == null) return;
            canvasInstance = Instantiate(canvasPrefab);
            canvasInstance.name = "BreedingBoardCanvas";
        }

        private void BindCells()
        {
            if (canvasInstance == null) return;
            for (int i = 0; i < CellCount; i++)
            {
                Transform found = FindNamed(canvasInstance.transform, "BreedingBoard_Cell" + (i + 1));
                if (found == null) found = FindNamed(canvasInstance.transform, "Cell " + (i + 1));
                if (found == null) continue;
                RectTransform rect = found as RectTransform;
                if (rect == null) rect = found.GetComponent<RectTransform>();
                cells[i] = rect;
                DouQuquBreedingCell hook = found.GetComponent<DouQuquBreedingCell>();
                if (hook == null) hook = found.gameObject.AddComponent<DouQuquBreedingCell>();
                hook.Index = i;
                hook.View = this;
                pieceImages[i] = EnsureChildImage(rect, "PieceIcon");
                pieceLabels[i] = EnsureChildText(rect, "PieceLabel");
            }
        }

        private void BindHud()
        {
            if (canvasInstance == null) return;
            Button back = FindButtonByChildName(canvasInstance.transform, "返回icon");
            if (back == null) back = FindButtonByLabel("返回");
            if (back != null) back.onClick.AddListener(() => DouQuquSceneNames.Load(DouQuquSceneNames.MainMenu));

            Button fight = FindButtonByLabel("斗蛐蛐");
            if (fight != null) fight.onClick.AddListener(() => DouQuquSceneNames.Load(DouQuquSceneNames.Matchmaking));

            Button registry = FindButtonByLabel("蛐蛐谱");
            if (registry != null) registry.onClick.AddListener(() => DouQuquSceneNames.Load(DouQuquSceneNames.Collection));

            Button backpack = FindButtonByLabel("背包");
            if (backpack != null) backpack.onClick.AddListener(SpawnOne);

            goldText = FindTextByName(canvasInstance.transform, "18,450");
            if (goldText == null)
            {
                Text[] texts = canvasInstance.GetComponentsInChildren<Text>(true);
                for (int i = 0; i < texts.Length; i++)
                    if (texts[i] != null && texts[i].text.IndexOf(',') >= 0) { goldText = texts[i]; break; }
            }
        }

        private void SpawnOne()
        {
            if (board == null) return;
            for (int i = 0; i < board.Width * board.Height; i++)
            {
                if (FindPieceAt(i) != null) continue;
                board.TrySpawn(i, 1);
                return;
            }
        }

        private void RefreshBoard()
        {
            for (int i = 0; i < CellCount; i++)
            {
                MergePiece piece = FindPieceAt(i);
                if (pieceImages[i] == null) continue;
                if (piece == null)
                {
                    pieceImages[i].enabled = false;
                    if (pieceLabels[i] != null) pieceLabels[i].text = "";
                    continue;
                }
                pieceImages[i].enabled = true;
                int colorIndex = Mathf.Clamp(piece.level - 1, 0, LevelColors.Length - 1);
                if (levelSprites != null && colorIndex < levelSprites.Length && levelSprites[colorIndex] != null)
                {
                    pieceImages[i].sprite = levelSprites[colorIndex];
                    pieceImages[i].color = Color.white;
                }
                else
                {
                    pieceImages[i].sprite = null;
                    pieceImages[i].color = LevelColors[colorIndex];
                }
                if (pieceLabels[i] != null)
                    pieceLabels[i].text = piece.level.ToString();
            }
        }

        private MergePiece FindPieceAt(int cell)
        {
            if (board == null || board.Pieces == null) return null;
            IReadOnlyList<MergePiece> pieces = board.Pieces;
            for (int i = 0; i < pieces.Count; i++)
                if (pieces[i] != null && pieces[i].cell == cell) return pieces[i];
            return null;
        }

        private int HitCell(Vector2 screenPosition)
        {
            for (int i = 0; i < CellCount; i++)
            {
                if (cells[i] == null) continue;
                if (RectTransformUtility.RectangleContainsScreenPoint(cells[i], screenPosition, null))
                    return i;
            }
            return -1;
        }

        private void ShowGhost(MergePiece piece, Vector2 screenPosition)
        {
            if (dragGhost == null)
            {
                GameObject go = new GameObject("MergeDragGhost", typeof(RectTransform), typeof(Canvas), typeof(Image));
                Canvas canvas = go.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 500;
                dragGhost = go.GetComponent<Image>();
                dragGhost.raycastTarget = false;
                RectTransform rect = dragGhost.rectTransform;
                rect.sizeDelta = new Vector2(160f, 160f);
            }
            dragGhost.gameObject.SetActive(true);
            int colorIndex = Mathf.Clamp(piece.level - 1, 0, LevelColors.Length - 1);
            dragGhost.color = LevelColors[colorIndex];
            dragGhost.rectTransform.position = screenPosition;
        }

        private void HideGhost()
        {
            if (dragGhost != null) dragGhost.gameObject.SetActive(false);
        }

        private static void EnsureEventSystem()
        {
            if (FindObjectOfType<EventSystem>() != null) return;
            GameObject go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<StandaloneInputModule>();
        }

        private static Image EnsureChildImage(RectTransform parent, string childName)
        {
            Transform existing = parent.Find(childName);
            Image image = existing != null ? existing.GetComponent<Image>() : null;
            if (image != null) return image;
            GameObject go = new GameObject(childName, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.12f, 0.12f);
            rect.anchorMax = new Vector2(0.88f, 0.88f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            image = go.GetComponent<Image>();
            image.raycastTarget = false;
            return image;
        }

        private static Text EnsureChildText(RectTransform parent, string childName)
        {
            Transform existing = parent.Find(childName);
            Text text = existing != null ? existing.GetComponent<Text>() : null;
            if (text != null) return text;
            GameObject go = new GameObject(childName, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            text = go.GetComponent<Text>();
            text.alignment = TextAnchor.MiddleCenter;
            text.fontSize = 48;
            text.color = Color.white;
            text.raycastTarget = false;
            text.font = Font.CreateDynamicFontFromOSFont("Arial", 48);
            return text;
        }

        private static Transform FindNamed(Transform root, string name)
        {
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform hit = FindNamed(root.GetChild(i), name);
                if (hit != null) return hit;
            }
            return null;
        }

        private static Button FindButtonByLabel(string label)
        {
            Text[] texts = FindObjectsOfType<Text>();
            for (int i = 0; i < texts.Length; i++)
            {
                if (texts[i] == null || texts[i].text != label) continue;
                Button button = texts[i].GetComponentInParent<Button>();
                if (button != null) return button;
            }
            return null;
        }

        private static Button FindButtonByChildName(Transform root, string name)
        {
            Transform found = FindNamed(root, name);
            if (found == null) return null;
            return found.GetComponent<Button>() ?? found.GetComponentInParent<Button>();
        }

        private static Text FindTextByName(Transform root, string name)
        {
            Transform found = FindNamed(root, name);
            return found != null ? found.GetComponent<Text>() : null;
        }
    }
}
