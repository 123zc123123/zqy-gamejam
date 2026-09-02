using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace DouQuqu
{
    /// <summary>
    /// 合成棋盘的手机端 UI 表现层：点击按钮生成棋子，按住棋子拖到目标格后松手结算。
    /// 具体的等级、占格和合成规则仍由 DouQuquMergeBoard 负责。
    /// </summary>
    public sealed class DouQuquMergeBoardView : MonoBehaviour
    {
        private const int UiCellCount = 16;

        [Header("合成棋盘")]
        [SerializeField] private DouQuquMergeBoard board;
        [SerializeField] private UIDocument uiDocument;
        [SerializeField] private bool autoReset = true;
        [SerializeField] private int randomSeed = 20260828;

        private readonly VisualElement[] cells = new VisualElement[UiCellCount];
        private readonly Label[] cellLabels = new Label[UiCellCount];
        private VisualElement mergeRoot;
        private VisualElement mergeBoard;
        private Label scoreLabel;
        private Label drawLabel;
        private Label statusLabel;
        private Button spawnButton;
        private bool uiBound;
        private int pointerId = -1;
        private int draggingPieceId = -1;
        private int sourceCell = -1;
        private int targetCell = -1;
        // 最近一次仍在棋盘内命中的格子，处理手指抬起落在格子间隙的情况。
        private int lastPointedCell = -1;
        // 由棋盘容器统一捕获指针，避免拖动跨格时源格子丢失 PointerUp。
        private bool boardCallbacksBound;

        private static readonly Color EmptyColor = new Color(0.10f, 0.18f, 0.27f, 0.96f);
        private static readonly Color[] LevelColors =
        {
            new Color(0.35f, 0.75f, 1.00f, 1f),
            new Color(0.48f, 0.95f, 0.66f, 1f),
            new Color(1.00f, 0.74f, 0.31f, 1f),
            new Color(1.00f, 0.46f, 0.62f, 1f),
            new Color(0.78f, 0.53f, 1.00f, 1f),
            new Color(1.00f, 0.88f, 0.37f, 1f)
        };

        private void Awake()
        {
            if (board == null) board = GetComponent<DouQuquMergeBoard>();
            if (uiDocument == null) uiDocument = GetComponent<UIDocument>();
        }

        private void OnEnable()
        {
            if (board != null) board.BoardChanged += RefreshBoard;
            if (board != null) board.DrawCompleted += OnDrawCompleted;
            TryBindUi();
        }

        private void Start()
        {
            TryBindUi();
            if (board == null) return;
            if (autoReset) board.ResetBoard(randomSeed);
            else RefreshBoard();
        }

        private void OnDisable()
        {
            ReleasePointerCapture();
            CancelDrag();
            if (board != null) board.BoardChanged -= RefreshBoard;
            if (board != null) board.DrawCompleted -= OnDrawCompleted;
            UnbindUi();
        }

        /// <summary>从当前 UIDocument 查找棋盘节点，并只注册一次触摸回调。</summary>
        private void TryBindUi()
        {
            if (uiBound || uiDocument == null) return;
            VisualElement root = uiDocument.rootVisualElement;
            if (root == null) return;
            mergeRoot = root.Q<VisualElement>("merge-root");
            if (mergeRoot == null)
            {
                enabled = false;
                return;
            }
            mergeBoard = root.Q<VisualElement>("merge-board");
            scoreLabel = root.Q<Label>("merge-score-label");
            drawLabel = root.Q<Label>("merge-draw-label");
            statusLabel = root.Q<Label>("merge-status-label");
            spawnButton = root.Q<Button>("merge-spawn-button");
            for (int i = 0; i < UiCellCount; i++)
            {
                cells[i] = root.Q<VisualElement>("merge-cell-" + i);
                cellLabels[i] = root.Q<Label>("merge-cell-label-" + i);
                if (cells[i] == null) continue;
                cells[i].userData = i;
                // 数字标签只负责显示，不参与命中，保证点击数字和点击格子边缘得到同一个格子。
                cells[i].pickingMode = PickingMode.Position;
                if (cellLabels[i] != null) cellLabels[i].pickingMode = PickingMode.Ignore;
            }
            // PointerDown 从子格子冒泡到棋盘容器；后续移动和抬起统一由容器接收。
            // 这样拖拽跨越多格或离开原格子时，不依赖某一个子元素继续派发事件。
            if (mergeBoard != null)
            {
                mergeBoard.RegisterCallback<PointerDownEvent>(OnCellPointerDown);
                mergeBoard.RegisterCallback<PointerMoveEvent>(OnCellPointerMove);
                mergeBoard.RegisterCallback<PointerUpEvent>(OnCellPointerUp);
                mergeBoard.RegisterCallback<PointerCancelEvent>(OnCellPointerCancel);
                boardCallbacksBound = true;
            }
            else
            {
                // 没有棋盘容器时保留逐格回调作为兼容兜底。
                for (int i = 0; i < UiCellCount; i++)
                {
                    if (cells[i] == null) continue;
                    cells[i].RegisterCallback<PointerDownEvent>(OnCellPointerDown);
                    cells[i].RegisterCallback<PointerMoveEvent>(OnCellPointerMove);
                    cells[i].RegisterCallback<PointerUpEvent>(OnCellPointerUp);
                    cells[i].RegisterCallback<PointerCancelEvent>(OnCellPointerCancel);
                }
            }
            if (spawnButton != null) spawnButton.clicked += SpawnPiece;
            uiBound = true;
            RefreshBoard();
        }

        private void UnbindUi()
        {
            if (!uiBound) return;
            if (boardCallbacksBound && mergeBoard != null)
            {
                mergeBoard.UnregisterCallback<PointerDownEvent>(OnCellPointerDown);
                mergeBoard.UnregisterCallback<PointerMoveEvent>(OnCellPointerMove);
                mergeBoard.UnregisterCallback<PointerUpEvent>(OnCellPointerUp);
                mergeBoard.UnregisterCallback<PointerCancelEvent>(OnCellPointerCancel);
                boardCallbacksBound = false;
            }
            else
            {
                for (int i = 0; i < UiCellCount; i++)
                {
                    if (cells[i] == null) continue;
                    cells[i].UnregisterCallback<PointerDownEvent>(OnCellPointerDown);
                    cells[i].UnregisterCallback<PointerMoveEvent>(OnCellPointerMove);
                    cells[i].UnregisterCallback<PointerUpEvent>(OnCellPointerUp);
                    cells[i].UnregisterCallback<PointerCancelEvent>(OnCellPointerCancel);
                }
            }
            if (spawnButton != null) spawnButton.clicked -= SpawnPiece;
            uiBound = false;
        }

        /// <summary>点击按钮在第一个空格生成一级棋子，连续点击即可准备一对合成材料。</summary>
        private void SpawnPiece()
        {
            if (board == null) return;
            int cellCount = Mathf.Min(UiCellCount, board.Width * board.Height);
            for (int cell = 0; cell < cellCount; cell++)
            {
                if (FindPieceAtCell(cell) != null) continue;
                if (board.TrySpawn(cell, 1))
                {
                    SetStatus("已获得 1 级道具，拖动它到任意同级棋子即可合成");
                    return;
                }
            }
            SetStatus("棋盘已满，请先拖动棋子到空格或完成合成");
        }

        private void OnCellPointerDown(PointerDownEvent evt)
        {
            if (board == null || pointerId >= 0) return;
            // 回调可能挂在棋盘容器上，不能把 currentTarget 当成具体格子。
            int cellIndex = FindCellAtPosition(evt.position);
            if (cellIndex < 0)
            {
                VisualElement eventTarget = evt.target as VisualElement;
                cellIndex = CellIndex(eventTarget);
            }
            if (cellIndex < 0 || cellIndex >= UiCellCount || cells[cellIndex] == null) return;
            VisualElement cell = cells[cellIndex];
            MergePiece piece = FindPieceAtCell(cellIndex);
            if (piece == null) return;

            pointerId = evt.pointerId;
            draggingPieceId = piece.id;
            sourceCell = cellIndex;
            targetCell = cellIndex;
            lastPointedCell = cellIndex;
            cell.AddToClassList("merge-dragging");
            if (boardCallbacksBound && mergeBoard != null) mergeBoard.CapturePointer(pointerId);
            else cell.CapturePointer(pointerId);
            SetStatus("拖动到空格，或拖到任意同等级棋子上");
            evt.StopPropagation();
        }

        private void OnCellPointerMove(PointerMoveEvent evt)
        {
            if (pointerId < 0 || evt.pointerId != pointerId) return;
            int nextTarget = FindCellAtPosition(evt.position);
            if (nextTarget >= 0) lastPointedCell = nextTarget;
            if (nextTarget == targetCell) return;
            ClearTargetHighlight();
            targetCell = nextTarget;
            if (targetCell >= 0 && targetCell < UiCellCount && cells[targetCell] != null)
                cells[targetCell].AddToClassList("merge-target");
            evt.StopPropagation();
        }

        private void OnCellPointerUp(PointerUpEvent evt)
        {
            if (pointerId < 0 || evt.pointerId != pointerId) return;
            // 松手时先以当前指针位置为准；只有当前点落在棋盘间隙时，才使用最近一次有效命中。
            // 这样既不会复用过期目标，也不会因为 4px 的视觉间隙丢失用户的合成意图。
            int releasedCell = FindCellAtPosition(evt.position);
            // 格子之间存在视觉间隙；若抬起点仍在棋盘范围内，沿用最后一个有效命中格，
            // 避免手指落在 4px 间隙时被误判成“棋盘外”。棋盘外则不会兜底，仍然拒绝操作。
            if (releasedCell < 0 && IsInsideMergeBoard(evt.position) && lastPointedCell >= 0)
                releasedCell = lastPointedCell;
            // 松手位置才是最终目标；先清理拖动过程中的旧高亮，避免沿用旧格子。
            ClearTargetHighlight();
            targetCell = releasedCell;
            int drawCountBefore = board == null ? 0 : board.DrawCount;
            bool moved = targetCell >= 0 && board != null && board.TryMove(draggingPieceId, targetCell);
            if (moved)
            {
                MergePiece piece = FindPieceAtCell(targetCell);
                if (board.DrawCount > drawCountBefore)
                {
                    MergeDrawResult draw = board.LastDraw;
                    SetStatus("合成成功，获得蟋蟀 " + draw.weightedValue + "," + draw.uniformValue);
                }
                else if (piece == null) SetStatus("操作完成");
                else if (piece.isDrawResult) SetStatus("操作完成，抽卡结果 " + piece.drawA + "," + piece.drawB);
                else if (piece.level >= board.MaxLevel) SetStatus("操作完成，最高级棋子");
                else SetStatus("操作完成，当前等级 " + piece.level);
            }
            else if (targetCell < 0)
            {
                SetStatus("请将棋子拖到棋盘格内再松手");
            }
            else if (FindPieceAtCell(targetCell) != null)
            {
                SetStatus("合成必须是同等级的棋子");
            }
            else
            {
                SetStatus("没有移动棋子");
            }
            ReleasePointerCapture();
            evt.StopPropagation();
            CancelDrag();
            RefreshBoard();
        }

        private void OnCellPointerCancel(PointerCancelEvent evt)
        {
            if (pointerId < 0 || evt.pointerId != pointerId) return;
            ReleasePointerCapture();
            CancelDrag();
            SetStatus("已取消拖动");
            evt.StopPropagation();
        }

        private void CancelDrag()
        {
            ClearTargetHighlight();
            if (sourceCell >= 0 && sourceCell < UiCellCount && cells[sourceCell] != null)
                cells[sourceCell].RemoveFromClassList("merge-dragging");
            pointerId = -1;
            draggingPieceId = -1;
            sourceCell = -1;
            targetCell = -1;
            lastPointedCell = -1;
        }

        /// <summary>释放本次拖拽的指针捕获，兼容棋盘容器和逐格回调两种绑定方式。</summary>
        private void ReleasePointerCapture()
        {
            if (pointerId < 0) return;
            if (boardCallbacksBound && mergeBoard != null)
            {
                mergeBoard.ReleasePointer(pointerId);
                return;
            }
            if (sourceCell >= 0 && sourceCell < UiCellCount && cells[sourceCell] != null)
                cells[sourceCell].ReleasePointer(pointerId);
        }

        private void ClearTargetHighlight()
        {
            if (targetCell >= 0 && targetCell < UiCellCount && cells[targetCell] != null)
                cells[targetCell].RemoveFromClassList("merge-target");
        }

        private int FindCellAtPosition(Vector2 panelPosition)
        {
            // PointerEvent.position 是 Panel 坐标，优先用 Panel.Pick 命中真实的棋盘格。
            // 这种方式不依赖格子排序，也不受格子内 Label 或第一行边界影响。
            if (mergeRoot != null && mergeRoot.panel != null)
            {
                VisualElement picked = mergeRoot.panel.Pick(panelPosition);
                while (picked != null)
                {
                    int pickedCell = CellIndex(picked);
                    if (pickedCell >= 0 && pickedCell < UiCellCount) return pickedCell;
                    picked = picked.parent;
                }
            }

            // 编辑器模拟触摸偶尔无法 Pick 时，先用真实 worldBound 做严格兜底。
            for (int i = 0; i < UiCellCount; i++)
            {
                if (cells[i] == null) continue;
                if (cells[i].worldBound.Contains(panelPosition)) return i;
            }

            // 格子之间有 margin 间隙，手指落在间隙时按最近中心归属；棋盘外不做最近格吸附。
            if (!IsInsideMergeBoard(panelPosition)) return -1;
            int nearest = -1;
            float nearestDistance = float.MaxValue;
            for (int i = 0; i < UiCellCount; i++)
            {
                if (cells[i] == null) continue;
                Vector2 delta = panelPosition - cells[i].worldBound.center;
                float distance = delta.sqrMagnitude;
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearest = i;
                }
            }
            return nearest;
        }

        /// <summary>判断指针是否仍在棋盘容器内（包含格子间的视觉间隙）。</summary>
        private bool IsInsideMergeBoard(Vector2 panelPosition)
        {
            return mergeBoard != null && mergeBoard.worldBound.Contains(panelPosition);
        }

        private static int CellIndex(VisualElement cell)
        {
            return cell == null || cell.userData == null ? -1 : (int)cell.userData;
        }

        private MergePiece FindPieceAtCell(int cell)
        {
            if (board == null) return null;
            IReadOnlyList<MergePiece> pieces = board.Pieces;
            for (int i = 0; i < pieces.Count; i++)
                if (pieces[i] != null && pieces[i].cell == cell) return pieces[i];
            return null;
        }

        /// <summary>监听棋盘事件刷新所有格子，保证按钮生成、拖拽移动和合成后显示一致。</summary>
        private void RefreshBoard()
        {
            if (!uiBound || board == null) return;
            if (scoreLabel != null) scoreLabel.text = "分数 " + board.Score;
            RefreshDrawLabel();
            for (int i = 0; i < UiCellCount; i++)
            {
                if (cells[i] == null) continue;
                MergePiece piece = FindPieceAtCell(i);
                if (piece == null)
                {
                    cells[i].style.backgroundColor = new StyleColor(EmptyColor);
                    if (cellLabels[i] != null) cellLabels[i].text = string.Empty;
                    continue;
                }
                int colorIndex = Mathf.Clamp(piece.level - 1, 0, LevelColors.Length - 1);
                cells[i].style.backgroundColor = new StyleColor(LevelColors[colorIndex]);
                if (cellLabels[i] != null)
                {
                    // 三级棋子会立即转为图鉴产出并从棋盘消失；这里保留异常快照的显示兜底。
                    if (piece.isDrawResult)
                        cellLabels[i].text = piece.drawA + "," + piece.drawB;
                    else if (piece.level >= board.MaxLevel)
                        cellLabels[i].text = "?";
                    else
                        cellLabels[i].text = piece.level.ToString();
                }
            }
        }

        private void SetStatus(string message)
        {
            if (statusLabel != null) statusLabel.text = message;
        }

        /// <summary>抽卡完成后刷新结果文字；实际奖励发放可由其他系统监听棋盘事件。</summary>
        private void OnDrawCompleted(MergeDrawResult result)
        {
            RefreshDrawLabel();
            string pityText = result.pityTriggered ? "（保底）" : string.Empty;
            SetStatus("合成完成，抽卡结果 A=" + result.weightedValue + " / B=" + result.uniformValue + pityText);
        }

        private void RefreshDrawLabel()
        {
            if (drawLabel == null || board == null) return;
            if (board.DrawCount <= 0)
            {
                drawLabel.text = "合到三级后生成蟋蟀并从棋盘消失：A 40/30/25/5，B 各 25%";
                return;
            }

            MergeDrawResult result = board.LastDraw;
            string pityText = result.pityTriggered ? " · 本次保底" : string.Empty;
            drawLabel.text = "最近抽卡 A=" + result.weightedValue + " / B=" + result.uniformValue
                + pityText + " · 保底进度 " + board.DrawsWithoutFour + "/" + board.DrawPityLimit;
        }
    }
}
