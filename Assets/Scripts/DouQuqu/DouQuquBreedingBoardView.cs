using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using ZqyGameJam.UI.QuquXiangqing;

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
        [SerializeField] private int randomSeed = 0;
        [SerializeField] private Sprite[] levelSprites;
        [SerializeField] private GameObject xiangqingPrefab;

        private readonly RectTransform[] cells = new RectTransform[CellCount];
        private readonly Image[] pieceImages = new Image[CellCount];
        private readonly Text[] pieceLabels = new Text[CellCount];
        private GameObject canvasInstance;
        private Image dragGhost;
        private Text goldText;
        private Text eggText;
        private Sprite[] phaseSprites;
        private Sprite[] qualitySprites;
        private QuquXiangqingView detailView;
        private DouQuquMergeBackpackPanel backpackPanel;
        private int detailPieceId = -1;
        private string detailBackpackId;
        private int draggingPieceId = -1;
        private int sourceCell = -1;
        private Vector2 dragStartPosition;
        private bool dragMoved;
        private int lastDetailFrame = -1;

        private static readonly Color[] LevelColors =
        {
            new Color(0.76f, 0.62f, 0.38f, 1f),
            new Color(0.48f, 0.78f, 0.42f, 1f),
            new Color(0.82f, 0.48f, 0.22f, 1f)
        };

        public void AttachCanvas(GameObject canvas)
        {
            canvasInstance = canvas;
        }

        private void Awake()
        {
            if (board == null) board = GetComponent<DouQuquMergeBoard>();
            if (canvasPrefab == null) canvasPrefab = Resources.Load<GameObject>("Merge/Prefabs/Canvas");
            if (xiangqingPrefab == null) xiangqingPrefab = Resources.Load<GameObject>(QuquXiangqingView.PrefabResourcePath);
            EnsureEventSystem();
        }

        private void OnEnable()
        {
            if (board != null) board.BoardChanged += RefreshBoard;
            DouQuquPlayerDataService.PlayerDataChanged += RefreshEconomyHud;
        }

        private void Start()
        {
            LoadPhaseSprites();
            LoadQualitySprites();
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
            DouQuquPlayerDataService.PlayerDataChanged -= RefreshEconomyHud;
        }

        /// <summary>棋盘模型按美术 4×5 对齐。</summary>
        public void BeginDrag(int cellIndex, PointerEventData eventData)
        {
            if (board == null) return;
            MergePiece piece = FindPieceAt(cellIndex);
            if (piece == null) return;
            draggingPieceId = piece.id;
            sourceCell = cellIndex;
            dragStartPosition = eventData.position;
            dragMoved = false;
        }

        public void Drag(PointerEventData eventData)
        {
            if (draggingPieceId < 0) return;
            if (!dragMoved)
            {
                if (Vector2.Distance(eventData.position, dragStartPosition) < ClickSlop()) return;
                dragMoved = true;
                MergePiece piece = FindPieceAt(sourceCell);
                if (piece == null) piece = FindPieceById(draggingPieceId);
                if (sourceCell >= 0 && sourceCell < CellCount && pieceImages[sourceCell] != null)
                    pieceImages[sourceCell].enabled = false;
                if (piece != null) ShowGhost(piece, eventData.position);
            }
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
            int source = sourceCell;
            bool tap = !dragMoved || Vector2.Distance(eventData.position, dragStartPosition) < ClickSlop();
            if (!tap)
            {
                int target = HitCell(eventData.position);
                if (target >= 0 && board != null && target != source)
                    board.TryMove(draggingPieceId, target);
            }
            HideGhost();
            draggingPieceId = -1;
            sourceCell = -1;
            dragMoved = false;
            RefreshBoard();
            if (tap && source >= 0) OnCellClicked(source);
        }

        public void OnCellClicked(int cellIndex)
        {
            MergePiece piece = FindPieceAt(cellIndex);
            if (piece == null) return;
            OpenDetail(piece);
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
            Transform[] all = canvasInstance.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < CellCount; i++)
            {
                Transform found = FindNamed(canvasInstance.transform, "BreedingBoard_Cell" + (i + 1));
                if (found == null) found = FindNamed(canvasInstance.transform, "Cell" + (i + 1));
                if (found == null) found = FindNamed(canvasInstance.transform, "Cell " + (i + 1));
                if (found == null)
                {
                    string wanted = "Cell " + (i + 1);
                    for (int t = 0; t < all.Length; t++)
                    {
                        if (all[t].name == wanted || all[t].name == "Cell" + (i + 1))
                        {
                            found = all[t];
                            break;
                        }
                    }
                }
                if (found == null) continue;
                RectTransform rect = found as RectTransform;
                if (rect == null) rect = found.GetComponent<RectTransform>();
                cells[i] = rect;
                DouQuquBreedingCell hook = found.GetComponent<DouQuquBreedingCell>();
                if (hook == null) hook = found.gameObject.AddComponent<DouQuquBreedingCell>();
                hook.Index = i;
                hook.View = this;
                Button button = found.GetComponent<Button>();
                if (button != null)
                {
                    int captured = i;
                    button.onClick.RemoveAllListeners();
                    button.onClick.AddListener(() => OnCellClicked(captured));
                }
                pieceImages[i] = EnsureChildImage(rect, "PieceIcon");
                pieceLabels[i] = EnsureChildText(rect, "PieceLabel");
            }
        }

        private void BindHud()
        {
            if (canvasInstance == null) return;
            if (DouQuquLobby.Instance != null)
            {
                DouQuquBottomNavBar.SuppressEmbedded(canvasInstance.transform);
            }
            else
            {
                DouQuquBottomNavBar nav = DouQuquBottomNavBar.EnsureOn(canvasInstance.transform);
                if (nav == null)
                {
                    Button back = FindButtonByChildName(canvasInstance.transform, "返回icon");
                    if (back == null) back = FindButtonByLabel("返回");
                    if (back != null) back.onClick.AddListener(() => DouQuquSceneNames.Load(DouQuquSceneNames.MainMenu));

                    Button fight = FindButtonByLabel("斗蛐蛐");
                    if (fight != null) fight.onClick.AddListener(() => DouQuquSceneNames.Load(DouQuquSceneNames.BattleEnter));

                    Button registry = FindButtonByLabel("蛐蛐谱");
                    if (registry != null) registry.onClick.AddListener(() => DouQuquSceneNames.Load(DouQuquSceneNames.Collection));
                }
            }

            Transform[] all = canvasInstance.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                string n = all[i].name;
                if (n.IndexOf("Arena", System.StringComparison.OrdinalIgnoreCase) >= 0
                    || n.IndexOf("Ellipse", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    BindSpawnButton(all[i]);
            }

            goldText = FindTextByName(canvasInstance.transform, "18,450");
            if (goldText == null)
            {
                Text[] texts = canvasInstance.GetComponentsInChildren<Text>(true);
                for (int i = 0; i < texts.Length; i++)
                    if (texts[i] != null && texts[i].text.IndexOf(',') >= 0) { goldText = texts[i]; break; }
            }

            BindBackpackButton();
            RefreshEconomyHud();
        }

        private void OpenDetail(MergePiece piece)
        {
            if (lastDetailFrame == Time.frameCount) return;
            lastDetailFrame = Time.frameCount;
            if (!EnsureDetailView()) return;
            detailPieceId = piece.id;
            detailBackpackId = null;
            string rank;
            string title;
            string desc;
            string subtitle;
            string[] stats = null;
            bool[] strongStats = null;
            if (piece.level >= 4 && piece.isDrawResult)
            {
                rank = DouQuquCricketCatalog.RankLabel(piece.drawA, piece.drawB);
                title = DouQuquCricketCatalog.CricketName(piece.drawA, piece.drawB);
                subtitle = DouQuquCricketCatalog.TemperamentName(piece.drawB);
                desc = DouQuquCricketCatalog.Blurb(piece.drawB);
                stats = DouQuquCricketCatalog.PanelStatDisplays(piece.drawA, piece.drawB);
                strongStats = DouQuquCricketCatalog.PanelStatStrongFlags(piece.drawB);
            }
            else
            {
                rank = piece.level == 1 ? "幼虫" : (piece.level == 2 ? "中虫" : "成虫");
                title = rank;
                subtitle = rank;
                desc = "继续合成可成长为精品虫。";
            }
            Sprite sprite = piece.level >= 4 ? SpriteForQuality(piece.drawA, piece.drawB) : SpriteForLevel(piece.level);
            if (sprite == null) sprite = SpriteForLevel(piece.level);
            bool finest = piece.level >= 4 && piece.isDrawResult;
            detailView.SetPickMode(false);
            if (detailView.storeButton != null) detailView.storeButton.gameObject.SetActive(finest);
            if (detailView.sellButton != null) detailView.sellButton.gameObject.SetActive(finest);
            if (finest) detailView.SetSellPrice(DouQuquPlayerDataService.SellPrice(piece.drawA));
            detailView.Show(rank, title, desc, sprite, subtitle, stats, strongStats);
        }

        private bool EnsureDetailView()
        {
            if (detailView != null) return true;
            if (xiangqingPrefab == null)
                xiangqingPrefab = Resources.Load<GameObject>(QuquXiangqingView.PrefabResourcePath);
            detailView = QuquXiangqingView.InstantiateOverlay(xiangqingPrefab);
            if (detailView == null) return false;
            detailView.Confirmed -= StoreDetailPiece;
            detailView.Confirmed += StoreDetailPiece;
            detailView.Sold -= SellDetailPiece;
            detailView.Sold += SellDetailPiece;
            return true;
        }

        private void StoreDetailPiece()
        {
            MergePiece piece = FindPieceById(detailPieceId);
            if (piece == null || piece.level < 4 || !piece.isDrawResult) return;
            int quality = piece.drawA;
            int temperament = piece.drawB;
            if (board == null || !board.TryTakeFinest(piece.id)) return;
            DouQuquPlayerDataService.AddFinestToBackpack(quality, temperament);
            if (detailView != null) detailView.Hide();
            RefreshBoard();
        }

        private void SellDetailPiece()
        {
            if (!string.IsNullOrEmpty(detailBackpackId))
            {
                CricketBackpackEntry entry = DouQuquPlayerDataService.FindBackpack(detailBackpackId);
                if (entry == null) return;
                int price = DouQuquPlayerDataService.SellPrice(entry.quality);
                if (!DouQuquPlayerDataService.RemoveFromBackpack(detailBackpackId)) return;
                DouQuquPlayerDataService.AddGold(price);
                detailBackpackId = null;
                if (detailView != null) detailView.Hide();
                if (backpackPanel != null) backpackPanel.Refresh();
                return;
            }

            MergePiece piece = FindPieceById(detailPieceId);
            if (piece == null || piece.level < 4 || !piece.isDrawResult) return;
            int gold = DouQuquPlayerDataService.SellPrice(piece.drawA);
            if (board == null || !board.TryTakeFinest(piece.id)) return;
            DouQuquPlayerDataService.AddGold(gold);
            if (detailView != null) detailView.Hide();
            RefreshBoard();
        }

        private void BindBackpackButton()
        {
            if (canvasInstance == null) return;
            Transform named = FindNamed(canvasInstance.transform, "BackpackButton");
            if (named == null) named = FindNamed(canvasInstance.transform, "背包");
            if (named == null) return;
            HookBackpackClick(named.gameObject);
            if (named.parent != null && named.parent.name.IndexOf("btn-left", System.StringComparison.OrdinalIgnoreCase) >= 0)
                HookBackpackClick(named.parent.gameObject);
        }

        private void HookBackpackClick(GameObject go)
        {
            if (go == null) return;
            Image image = go.GetComponent<Image>();
            if (image == null) image = go.GetComponentInChildren<Image>(true);
            if (image != null)
            {
                image.raycastTarget = true;
                if (image.color.a < 0.02f) image.color = new Color(image.color.r, image.color.g, image.color.b, 0.02f);
            }
            Graphic[] graphics = go.GetComponentsInChildren<Graphic>(true);
            for (int i = 0; i < graphics.Length; i++)
                if (graphics[i] != null) graphics[i].raycastTarget = true;
            Button bag = go.GetComponent<Button>();
            if (bag == null) bag = go.AddComponent<Button>();
            bag.transition = Selectable.Transition.None;
            bag.interactable = true;
            if (image != null) bag.targetGraphic = image;
            bag.onClick.RemoveAllListeners();
            bag.onClick.AddListener(OpenMergeBackpack);
        }

        private void OpenMergeBackpack()
        {
            if (backpackPanel == null) backpackPanel = gameObject.AddComponent<DouQuquMergeBackpackPanel>();
            backpackPanel.Show(OpenBackpackCard);
        }

        private void OpenBackpackCard(CricketBackpackEntry entry)
        {
            if (entry == null || !EnsureDetailView()) return;
            detailPieceId = -1;
            detailBackpackId = entry.instanceId;
            detailView.SetBackpackMode();
            detailView.SetSellPrice(DouQuquPlayerDataService.SellPrice(entry.quality));
            detailView.Show(
                DouQuquCricketCatalog.RankLabel(entry.quality, entry.temperament),
                DouQuquCricketCatalog.CricketName(entry.quality, entry.temperament),
                DouQuquCricketCatalog.Blurb(entry.temperament),
                SpriteForQuality(entry.quality, entry.temperament),
                DouQuquCricketCatalog.TemperamentName(entry.temperament),
                DouQuquCricketCatalog.PanelStatDisplays(entry.quality, entry.temperament),
                DouQuquCricketCatalog.PanelStatStrongFlags(entry.temperament));
        }

        private int lastSpawnFrame = -1;

        private void SpawnOne()
        {
            if (board == null || lastSpawnFrame == Time.frameCount) return;
            lastSpawnFrame = Time.frameCount;
            int empty = -1;
            for (int i = 0; i < board.Width * board.Height; i++)
            {
                if (FindPieceAt(i) != null) continue;
                empty = i;
                break;
            }
            if (empty < 0) return;
            if (!DouQuquPlayerDataService.TrySpendEggs(1)) return;
            if (!board.TrySpawn(empty, 1))
                DouQuquPlayerDataService.AddEggs(1);
        }

        private void RefreshEconomyHud()
        {
            if (canvasInstance == null) return;
            if (goldText == null)
            {
                goldText = FindTextByName(canvasInstance.transform, "18,450");
                if (goldText == null)
                {
                    Text[] texts = canvasInstance.GetComponentsInChildren<Text>(true);
                    for (int i = 0; i < texts.Length; i++)
                        if (texts[i] != null && texts[i].text.IndexOf(',') >= 0) { goldText = texts[i]; break; }
                }
            }
            if (eggText == null)
            {
                Transform eggNode = FindNamed(canvasInstance.transform, "99");
                if (eggNode != null) eggText = eggNode.GetComponent<Text>();
            }
            if (goldText != null) goldText.text = DouQuquPlayerDataService.FormatGold(DouQuquPlayerDataService.Gold);
            if (eggText != null) eggText.text = DouQuquPlayerDataService.Eggs.ToString();
            TMP_Text[] tmps = canvasInstance.GetComponentsInChildren<TMP_Text>(true);
            for (int i = 0; i < tmps.Length; i++)
            {
                if (tmps[i] == null) continue;
                if (tmps[i].text == "18,450" || (goldText == null && tmps[i].name.IndexOf("Gold", System.StringComparison.OrdinalIgnoreCase) >= 0))
                    tmps[i].text = DouQuquPlayerDataService.FormatGold(DouQuquPlayerDataService.Gold);
            }
        }

        private void LoadPhaseSprites()
        {
            if (levelSprites != null && levelSprites.Length > 0) return;
            phaseSprites = new Sprite[4];
            phaseSprites[0] = LoadResourceSprite("Merge/MergePhases/phase-1");
            phaseSprites[1] = LoadResourceSprite("Merge/MergePhases/phase-2");
            phaseSprites[2] = LoadResourceSprite("Merge/MergePhases/phase-3");
            phaseSprites[3] = phaseSprites[2];
        }

        private void LoadQualitySprites()
        {
            qualitySprites = new Sprite[16];
            for (int quality = 1; quality <= 4; quality++)
            {
                for (int temperament = 1; temperament <= 4; temperament++)
                {
                    qualitySprites[(quality - 1) * 4 + (temperament - 1)] = LoadResourceSprite(
                        "Merge/MergeQualities/quality-" + quality + "-" + temperament);
                }
            }
        }

        private void ApplyPieceVisual(Image image, Text label, MergePiece piece)
        {
            if (image == null || piece == null) return;
            Sprite sprite = SpriteForLevel(piece.level);
            image.preserveAspect = true;
            if (piece.level >= 4 && piece.isDrawResult)
            {
                Sprite portrait = SpriteForQuality(piece.drawA, piece.drawB);
                bool hasPortrait = portrait != null;
                image.sprite = hasPortrait ? portrait : sprite;
                image.color = Color.white;
                if (!hasPortrait && sprite != null)
                {
                    image.color = piece.drawA >= 4
                        ? DouQuquCricketCatalog.TemperamentColors[Mathf.Clamp(piece.drawB, 1, 4)]
                        : Color.Lerp(Color.white, DouQuquCricketCatalog.QualityColors[Mathf.Clamp(piece.drawA, 1, 4)], 0.55f);
                }
                if (label != null)
                {
                    label.text = DouQuquCricketCatalog.ShortLabel(piece.drawA, piece.drawB);
                    label.fontSize = 22;
                    label.alignment = TextAnchor.LowerCenter;
                    label.color = Color.white;
                }
                return;
            }
            if (sprite != null)
            {
                image.sprite = sprite;
                image.color = Color.white;
                if (label != null) label.text = "";
                return;
            }
            image.sprite = null;
            image.color = LevelColors[Mathf.Clamp(piece.level - 1, 0, LevelColors.Length - 1)];
            if (label != null) label.text = piece.level.ToString();
        }

        private static Sprite LoadResourceSprite(string path)
        {
            Sprite sprite = Resources.Load<Sprite>(path);
            if (sprite != null) return sprite;
            Texture2D texture = Resources.Load<Texture2D>(path);
            if (texture == null) return null;
            return Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
        }

        private Sprite SpriteForQuality(int quality, int temperament)
        {
            int q = Mathf.Clamp(quality, 1, 4);
            int t = Mathf.Clamp(temperament, 1, 4);
            int index = (q - 1) * 4 + (t - 1);
            if (qualitySprites != null && index < qualitySprites.Length)
                return qualitySprites[index];
            return null;
        }

        private Sprite SpriteForLevel(int level)
        {
            int index = Mathf.Clamp(level - 1, 0, 3);
            if (levelSprites != null && index < levelSprites.Length && levelSprites[index] != null)
                return levelSprites[index];
            if (phaseSprites != null && index < phaseSprites.Length)
                return phaseSprites[index];
            return null;
        }

        private void BindSpawnButton(Transform target)
        {
            if (target == null) return;
            DouQuquSimpleClick extra = target.GetComponent<DouQuquSimpleClick>();
            if (extra != null) extra.Clicked = null;
            Image image = target.GetComponent<Image>();
            if (image != null) image.raycastTarget = true;
            Button button = target.GetComponent<Button>();
            if (button == null) button = target.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.interactable = true;
            if (image != null) button.targetGraphic = image;
            Navigation nav = button.navigation;
            nav.mode = Navigation.Mode.None;
            button.navigation = nav;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(SpawnOne);
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
                ApplyPieceVisual(pieceImages[i], pieceLabels[i], piece);
            }
        }

        private static float ClickSlop()
        {
            EventSystem system = EventSystem.current;
            float threshold = system != null ? system.pixelDragThreshold : 10f;
            return Mathf.Max(48f, threshold * 3f);
        }

        private MergePiece FindPieceById(int pieceId)
        {
            if (board == null || board.Pieces == null) return null;
            IReadOnlyList<MergePiece> pieces = board.Pieces;
            for (int i = 0; i < pieces.Count; i++)
                if (pieces[i] != null && pieces[i].id == pieceId) return pieces[i];
            return null;
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
                Canvas canvas = cells[i].GetComponentInParent<Canvas>();
                Camera camera = null;
                if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                    camera = canvas.worldCamera;
                if (RectTransformUtility.RectangleContainsScreenPoint(cells[i], screenPosition, camera))
                    return i;
            }
            return -1;
        }

        private void ShowGhost(MergePiece piece, Vector2 screenPosition)
        {
            if (dragGhost == null)
            {
                GameObject root = new GameObject("MergeDragGhostCanvas", typeof(RectTransform), typeof(Canvas));
                Canvas canvas = root.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 500;
                GameObject icon = new GameObject("GhostIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                icon.transform.SetParent(root.transform, false);
                dragGhost = icon.GetComponent<Image>();
                dragGhost.raycastTarget = false;
                dragGhost.preserveAspect = true;
                RectTransform rect = dragGhost.rectTransform;
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
            }
            Vector2 ghostSize = new Vector2(140f, 140f);
            if (sourceCell >= 0 && cells[sourceCell] != null)
            {
                Rect cellRect = cells[sourceCell].rect;
                ghostSize = new Vector2(Mathf.Max(80f, cellRect.width * 0.72f), Mathf.Max(80f, cellRect.height * 0.72f));
            }
            dragGhost.rectTransform.sizeDelta = ghostSize;
            dragGhost.gameObject.SetActive(true);
            ApplyPieceVisual(dragGhost, null, piece);
            dragGhost.rectTransform.position = screenPosition;
        }

        private void HideGhost()
        {
            if (dragGhost != null) dragGhost.gameObject.SetActive(false);
            if (sourceCell >= 0 && sourceCell < CellCount && pieceImages[sourceCell] != null)
                pieceImages[sourceCell].enabled = FindPieceAt(sourceCell) != null;
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
            image.preserveAspect = true;
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
            text.font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "SimHei", "Arial" }, 28);
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            Outline outline = go.GetComponent<Outline>();
            if (outline == null) outline = go.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
            outline.effectDistance = new Vector2(1.2f, -1.2f);
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
            TMP_Text[] tmps = FindObjectsOfType<TMP_Text>();
            for (int i = 0; i < tmps.Length; i++)
            {
                if (tmps[i] == null || tmps[i].text != label) continue;
                Button button = tmps[i].GetComponentInParent<Button>();
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
