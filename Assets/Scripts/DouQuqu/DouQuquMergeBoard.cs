using System;
using System.Collections.Generic;
using UnityEngine;

namespace DouQuqu
{
    [Serializable]
    /// <summary>棋盘上的一个可拖拽棋子。</summary>
    public sealed class MergePiece
    {
        public int id;
        public int cell;
        public int level;
        // 合成到 3 级后会先在这个对象上写入抽卡结果，再将棋子从棋盘移除。
        public bool isDrawResult;
        public int drawA;
        public int drawB;

        public MergePiece() { }

        public MergePiece(int pieceId, int boardCell, int pieceLevel)
        {
            id = pieceId;
            cell = boardCell;
            level = pieceLevel;
            isDrawResult = false;
            drawA = 0;
            drawB = 0;
        }
    }

    [Serializable]
    /// <summary>合成棋盘的可序列化快照。</summary>
    public sealed class MergeBoardSnapshot
    {
        public int width;
        public int height;
        public int score;
        public int nextPieceId;
        public MergePiece[] pieces;
        public int drawCount;
        public int drawsWithoutFour;
        public MergeDrawResult lastDraw;
    }

    [Serializable]
    /// <summary>
    /// 合成到 3 级时产生的一次 4×4 抽卡结果。
    /// 参数 A 使用加权概率，参数 B 使用均匀概率；两个参数相互独立。
    /// </summary>
    public struct MergeDrawResult
    {
        public int drawIndex;
        public int weightedValue;
        public int uniformValue;
        public bool pityTriggered;
        public int drawsWithoutFour;

        public MergeDrawResult(int index, int weighted, int uniform, bool pity, int missCount)
        {
            drawIndex = index;
            weightedValue = weighted;
            uniformValue = uniform;
            pityTriggered = pity;
            drawsWithoutFour = missCount;
        }
    }

    /// <summary>
    /// 菜单或赛前模式使用的小型确定性合成棋盘。
    /// 模型独立于 Unity UI 和预制体，表现层只需把拖拽结果传给 TryMove。
    /// </summary>
    public sealed class DouQuquMergeBoard : MonoBehaviour
    {
        // 育虫盘：幼虫 → 中虫 → 成虫 → 精品虫。精品虫留在棋盘上，不再往上合。
        private const int HighestMergeLevel = 4;
        private const int DrawOptionCount = 4;

        [SerializeField] private int width = 4;
        [SerializeField] private int height = 5;
        [SerializeField] private int initialPieces = 0;

        [Header("3 级合成抽卡")]
        [SerializeField] private int drawPityLimit = 10;
        // 参数 A：1 的概率 40%，2 的概率 30%，3 的概率 25%，4 的概率 5%。
        [SerializeField] private float[] weightedDrawWeights = { 40f, 30f, 25f, 5f };
        // 参数 B：四个结果各 25%。实际抽取时会按总权重归一化。
        [SerializeField] private float[] uniformDrawWeights = { 25f, 25f, 25f, 25f };

        private readonly List<MergePiece> pieces = new List<MergePiece>();
        private int nextPieceId;
        private int score;
        private System.Random random;
        private int drawCount;
        private int drawsWithoutFour;
        private MergeDrawResult lastDraw;

        public int Width => width;
        public int Height => height;

        /// <summary>表现层按美术格数对齐棋盘。应在 ResetBoard 前调用。</summary>
        public void SetSize(int newWidth, int newHeight)
        {
            width = Mathf.Clamp(newWidth, 2, 12);
            height = Mathf.Clamp(newHeight, 2, 12);
        }
        public int Score => score;
        public int MaxLevel => HighestMergeLevel;
        public int DrawCount => drawCount;
        public int DrawsWithoutFour => drawsWithoutFour;
        public int DrawPityLimit => Mathf.Max(1, drawPityLimit);
        public MergeDrawResult LastDraw => lastDraw;
        public IReadOnlyList<MergePiece> Pieces => pieces;

        public event Action<MergePiece, MergePiece> MergeCompleted;
        public event Action<MergePiece> PieceSpawned;
        public event Action<MergePiece> PieceRemoved;
        public event Action<MergeDrawResult> DrawCompleted;
        public event Action BoardChanged;

        // Inspector 参数只在组件载入时归一化；逻辑层不依赖任何 UI 或预制体。
        private void Awake()
        {
            width = Mathf.Clamp(width, 2, 12);
            height = Mathf.Clamp(height, 2, 12);
            drawPityLimit = Mathf.Max(1, drawPityLimit);
            NormalizeDrawWeights();
            random = new System.Random(Environment.TickCount);
        }

        /// <summary>清空棋盘、重置分数和 ID，并生成开局棋子。</summary>
        public void ResetBoard(int seed = 0)
        {
            random = new System.Random(seed == 0 ? Environment.TickCount : seed);
            pieces.Clear();
            nextPieceId = 0;
            score = 0;
            drawCount = 0;
            drawsWithoutFour = 0;
            lastDraw = default(MergeDrawResult);
            NormalizeDrawWeights();
            for (int i = 0; i < Mathf.Clamp(initialPieces, 0, width * height); i++) SpawnRandom(1);
            BoardChanged?.Invoke();
        }

        /// <summary>在指定空格生成一个等级被限制在合法范围内的棋子。</summary>
        public bool TrySpawn(int cell, int level)
        {
            if (cell < 0 || cell >= width * height || FindAtCell(cell) != null) return false;
            MergePiece piece = new MergePiece(nextPieceId++, cell, Mathf.Clamp(level, 1, HighestMergeLevel));
            pieces.Add(piece);
            PieceSpawned?.Invoke(piece);
            BoardChanged?.Invoke();
            return true;
        }

        /// <summary>
        /// 结算一次完成的拖拽。目标为空则移动；目标有棋子时，
        /// 只要源、目标不是同一颗棋子且等级相同，就会合成，不限制两个格子的距离。
        /// </summary>
        public bool TryMove(int pieceId, int targetCell)
        {
            MergePiece piece = FindById(pieceId);
            if (piece == null || targetCell < 0 || targetCell >= width * height) return false;
            MergePiece other = FindAtCell(targetCell);
            if (other == null)
            {
                piece.cell = targetCell;
                BoardChanged?.Invoke();
                return true;
            }
            // 已经按 ID 找到了源棋子，直接使用对象引用合成，避免状态刷新或异常快照导致
            // 再按格子查找到另一颗同格棋子。
            return TryMergePieces(piece, other);
        }

        /// <summary>合并两个明确指定格子中的同等级棋子，不限制两个格子的距离。</summary>
        public bool TryMerge(int sourceCell, int targetCell)
        {
            MergePiece source = FindAtCell(sourceCell);
            MergePiece target = FindAtCell(targetCell);
            return TryMergePieces(source, target);
        }

        /// <summary>按已解析出的棋子对象执行一次原子合成。</summary>
        private bool TryMergePieces(MergePiece source, MergePiece target)
        {
            // 两格都有不同棋子、等级相同且源棋子未达上限时允许合成；
            // 拖拽距离和棋盘位置不属于合成条件。
            if (source == null || target == null || source == target
                || source.level != target.level || source.level >= HighestMergeLevel) return false;
            target.level++;
            pieces.Remove(source);
            score += target.level * 10;
            PieceRemoved?.Invoke(source);
            MergeCompleted?.Invoke(target, source);
            BoardChanged?.Invoke();
            return true;
        }

        /// <summary>复制当前棋盘，供存档或局域网同步使用。</summary>
        public MergeBoardSnapshot CaptureSnapshot()
        {
            MergeBoardSnapshot snapshot = new MergeBoardSnapshot
            {
                width = width,
                height = height,
                score = score,
                nextPieceId = nextPieceId,
                pieces = new MergePiece[pieces.Count],
                drawCount = drawCount,
                drawsWithoutFour = drawsWithoutFour,
                lastDraw = lastDraw
            };
            for (int i = 0; i < pieces.Count; i++)
            {
                MergePiece piece = pieces[i];
                snapshot.pieces[i] = new MergePiece(piece.id, piece.cell, piece.level)
                {
                    isDrawResult = piece.isDrawResult,
                    drawA = piece.drawA,
                    drawB = piece.drawB
                };
            }
            return snapshot;
        }

        /// <summary>应用棋盘快照；空快照不会改变当前状态。</summary>
        public void ApplySnapshot(MergeBoardSnapshot snapshot)
        {
            if (snapshot == null || snapshot.pieces == null) return;
            width = Mathf.Clamp(snapshot.width, 2, 12);
            height = Mathf.Clamp(snapshot.height, 2, 12);
            score = snapshot.score;
            nextPieceId = snapshot.nextPieceId;
            drawCount = Mathf.Max(0, snapshot.drawCount);
            drawsWithoutFour = Mathf.Clamp(snapshot.drawsWithoutFour, 0, DrawPityLimit - 1);
            lastDraw = snapshot.lastDraw;
            pieces.Clear();
            HashSet<int> usedIds = new HashSet<int>();
            int maxPieceId = -1;
            for (int i = 0; i < snapshot.pieces.Length; i++)
            {
                MergePiece piece = snapshot.pieces[i];
                if (piece == null || piece.id < 0 || piece.cell < 0 || piece.cell >= width * height
                    || !usedIds.Add(piece.id) || FindAtCell(piece.cell) != null) continue;
                MergePiece copy = new MergePiece(piece.id, piece.cell, Mathf.Clamp(piece.level, 1, HighestMergeLevel))
                {
                    isDrawResult = piece.isDrawResult && piece.level >= HighestMergeLevel,
                    drawA = Mathf.Clamp(piece.drawA, 1, DrawOptionCount),
                    drawB = Mathf.Clamp(piece.drawB, 1, DrawOptionCount)
                };
                pieces.Add(copy);
                maxPieceId = Mathf.Max(maxPieceId, copy.id);
            }
            // 防止异常或旧快照把 nextPieceId 回退到场上已有的 ID，后续生成棋子时发生 ID 冲突。
            nextPieceId = Mathf.Max(Mathf.Max(0, nextPieceId), maxPieceId + 1);
            BoardChanged?.Invoke();
        }

        // 棋子 ID 在移动过程中保持不变，格子索引只表示当前占用位置。
        private MergePiece FindById(int id)
        {
            for (int i = 0; i < pieces.Count; i++) if (pieces[i].id == id) return pieces[i];
            return null;
        }

        private MergePiece FindAtCell(int cell)
        {
            for (int i = 0; i < pieces.Count; i++) if (pieces[i].cell == cell) return pieces[i];
            return null;
        }

        private void SpawnRandom(int level)
        {
            List<int> empty = new List<int>();
            for (int cell = 0; cell < width * height; cell++) if (FindAtCell(cell) == null) empty.Add(cell);
            if (empty.Count == 0) return;
            TrySpawn(empty[random.Next(empty.Count)], level);
        }

        /// <summary>
        /// 触发一次 4×4 抽卡：参数 A 使用 40/30/25/5，参数 B 使用 25/25/25/25。
        /// 连续 9 次未出现 A=4 时，第 10 次强制 A=4，然后重置保底计数。
        /// </summary>
        private void DrawCard(MergePiece resultPiece)
        {
            if (random == null) random = new System.Random(Environment.TickCount);
            drawCount++;

            bool pityTriggered = drawsWithoutFour >= DrawPityLimit - 1;
            int weightedValue = pityTriggered ? DrawOptionCount : RollWeightedOption(weightedDrawWeights);
            int uniformValue = RollWeightedOption(uniformDrawWeights);

            drawsWithoutFour = weightedValue == DrawOptionCount ? 0 : drawsWithoutFour + 1;
            lastDraw = new MergeDrawResult(drawCount, weightedValue, uniformValue, pityTriggered, drawsWithoutFour);
            if (resultPiece != null)
            {
                resultPiece.isDrawResult = true;
                resultPiece.drawA = weightedValue;
                resultPiece.drawB = uniformValue;
            }
            DrawCompleted?.Invoke(lastDraw);
        }

        /// <summary>按权重抽取 1～4；权重总和不要求必须正好等于 100。</summary>
        private int RollWeightedOption(float[] weights)
        {
            float total = 0f;
            for (int i = 0; i < DrawOptionCount; i++)
                if (weights != null && i < weights.Length) total += Mathf.Max(0f, weights[i]);

            if (total <= 0f) return random.Next(1, DrawOptionCount + 1);

            float roll = (float)random.NextDouble() * total;
            float cumulative = 0f;
            for (int i = 0; i < DrawOptionCount; i++)
            {
                if (weights != null && i < weights.Length) cumulative += Mathf.Max(0f, weights[i]);
                if (roll < cumulative) return i + 1;
            }
            return DrawOptionCount;
        }

        /// <summary>保证 Inspector 中的两个概率数组始终有四个合法选项。</summary>
        private void NormalizeDrawWeights()
        {
            weightedDrawWeights = NormalizeWeights(weightedDrawWeights, new[] { 40f, 30f, 25f, 5f });
            uniformDrawWeights = NormalizeWeights(uniformDrawWeights, new[] { 25f, 25f, 25f, 25f });
        }

        private static float[] NormalizeWeights(float[] source, float[] fallback)
        {
            float[] result = new float[DrawOptionCount];
            for (int i = 0; i < DrawOptionCount; i++)
                result[i] = source != null && i < source.Length ? Mathf.Max(0f, source[i]) : fallback[i];
            return result;
        }
    }
}
