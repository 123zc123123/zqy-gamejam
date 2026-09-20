using System.Collections.Generic;
using UnityEngine;

namespace DouQuqu
{
    /// <summary>
    /// Demo 表现层：把权威 MatchState 映射到简单几何体预制体。
    /// 该组件不参与物理和规则计算，只负责生成、复用、隐藏和着色场景对象。
    /// </summary>
    public sealed class DemoView : MonoBehaviour
    {
        [Header("对局")]
        [SerializeField] private MatchController match;
        [SerializeField] private bool autoStart = true;
        [SerializeField, Range(1, MatchController.MaxPlayers)] private int playerCount = 4;
        [SerializeField] private int randomSeed = 20260828;

        [Header("预制体")]
        [SerializeField] private GameObject bugPrefab;
        [SerializeField] private GameObject qingTouPrefab;
        [SerializeField] private GameObject youHuluPrefab;
        [SerializeField] private GameObject babyPrefab;
        [SerializeField] private GameObject eggPrefab;
        [SerializeField] private GameObject nestPrefab;
        [SerializeField] private GameObject heartPrefab;
        [SerializeField] private GameObject sizePrefab;
        [SerializeField] private GameObject shieldPrefab;
        [SerializeField] private GameObject chargePrefab;
        [Header("覆盖层预制体")]
        [SerializeField] private GameObject staminaRingPrefab;
        [SerializeField] private GameObject staminaBarPrefab;
        [SerializeField] private GameObject chargeArrowPrefab;
        [SerializeField] private GameObject groundMarkerPrefab;
        [SerializeField] private GameObject cricketUnitPrefab;

        [Header("显示")]
        [SerializeField] private float groundOffset = 0.35f;
        [SerializeField] private bool tintPlayers = false;
        [SerializeField] private bool fitVisualToCollision = true;
        [Header("网络表现")]
        [SerializeField, Range(5f, 50f)] private float clientPositionSmoothing = 24f;
        [SerializeField, Range(0f, 0.15f)] private float clientExtrapolationLimit = 0.08f;
        [SerializeField, Min(0.5f)] private float clientSnapDistance = 2.5f;

        private readonly Dictionary<int, GameObject> bugViews = new Dictionary<int, GameObject>();
        private readonly Dictionary<int, GameObject> babyViews = new Dictionary<int, GameObject>();
        private readonly Dictionary<int, GameObject> pickupViews = new Dictionary<int, GameObject>();
        private readonly Dictionary<int, string> pickupKinds = new Dictionary<int, string>();
        private readonly List<GameObject> eggViews = new List<GameObject>();
        private readonly HashSet<int> seenIds = new HashSet<int>();
        // Unity 对象不能在 MonoBehaviour 构造阶段创建，属性块在 Awake 中初始化。
        private MaterialPropertyBlock propertyBlock;

        private Transform bugsRoot;
        private Transform babiesRoot;
        private Transform eggsRoot;
        private Transform pickupsRoot;
        private Transform nestRoot;
        private Transform arrowsRoot;
        private Transform ringsRoot;
        private Transform barsRoot;
        private Transform markersRoot;
        private GameObject nestView;
        private readonly Dictionary<int, ChargeArrow> chargeArrows = new Dictionary<int, ChargeArrow>();
        private readonly Dictionary<int, StaminaRing> staminaRings = new Dictionary<int, StaminaRing>();
        private readonly Dictionary<int, StaminaBar> staminaBars = new Dictionary<int, StaminaBar>();
        private readonly Dictionary<int, GroundMarker> groundMarkers = new Dictionary<int, GroundMarker>();
        private readonly Dictionary<int, SkillBar> skillBars = new Dictionary<int, SkillBar>();
        private readonly Dictionary<int, int> assignedBugProfiles = new Dictionary<int, int>();
        private readonly Dictionary<int, string> assignedSkinLabels = new Dictionary<int, string>();
        private readonly HashSet<int> initializedBugPositions = new HashSet<int>();
        private readonly HashSet<int> initializedBabyPositions = new HashSet<int>();
        private readonly HashSet<int> initializedPickupPositions = new HashSet<int>();
        private readonly HashSet<int> initializedEggPositions = new HashSet<int>();
        private Sprite[] premiumBugSprites;
        private MatchState assignedProfileState;
        private int assignedProfileSeed = int.MinValue;
        private bool warnedMissingOverlays;
        private int observedClientTick = -1;
        private float clientSnapshotAge;
        private ArenaZoneView zoneView;
        private int lastNestHits = int.MinValue;



        private void Awake()
        {
            propertyBlock = new MaterialPropertyBlock();
            if (match == null) match = GetComponent<MatchController>();
            if (match == null) match = FindObjectOfType<MatchController>();
            LoadPremiumBugSprites();
            EnsureBattleCamera();
            EnsureRoots();
            if (GetComponent<BattleFx>() == null) gameObject.AddComponent<BattleFx>();
        }

        /// <summary>
        /// 加载育虫盘已经使用的 4×4 精品虫立绘。资源放在 Resources 下，后续替换动画时
        /// 只需在战斗虫根节点下增加动画组件即可，不需要改战斗状态或碰撞逻辑。
        /// </summary>
        private void LoadPremiumBugSprites()
        {
            premiumBugSprites = new Sprite[16];
            for (int quality = 1; quality <= 4; quality++)
            {
                for (int temperament = 1; temperament <= 4; temperament++)
                {
                    int index = (quality - 1) * 4 + (temperament - 1);
                    premiumBugSprites[index] = Resources.Load<Sprite>(
                        "Merge/MergeQualities/quality-" + quality + "-" + temperament);
                }
            }
        }

        private static void EnsureBattleCamera()
        {
            Camera main = Camera.main;
            if (main == null) return;
            if (main.GetComponent<BattleCamera>() == null)
                main.gameObject.AddComponent<BattleCamera>();
        }

        private void OnEnable()
        {
            BattleFx fx = GetComponent<BattleFx>();
            if (fx != null) fx.Bind(match);
        }

        private void Start()
        {
            if (match == null) return;
            if (autoStart && !match.IsStarted)
            {
                match.Configure(MatchRunMode.Offline, Mathf.Clamp(playerCount, 1, MatchController.MaxPlayers));
                match.ResetMatch(playerCount, randomSeed);
                match.StartMatch();
            }
            if (FindObjectOfType<BattleHudBinder>() == null)
            {
                Camera main = Camera.main;
                BattleCamera cam = main != null ? main.GetComponent<BattleCamera>() : null;
                if (cam != null) cam.FollowLocalPlayer(match, 0);
            }
            RefreshView();
        }

        private void Update()
        {
            // 客户端快照和非 Unity 驱动的控制器也能通过每帧刷新及时更新表现。
            if (match != null && match.State != null)
            {
                if (match.RunMode == MatchRunMode.Client)
                    clientSnapshotAge += Time.unscaledDeltaTime;
                RefreshView();
            }
        }

        /// <summary>由战斗场景流程关闭旧的自动开局，防止客户端等待快照时误启动单机局。</summary>
        public void SetAutoStart(bool enabled)
        {
            autoStart = enabled;
        }

        /// <summary>允许场景流程在运行时替换为当前场景的权威状态组件。</summary>
        public void BindMatch(MatchController controller)
        {
            if (match == controller) return;
            match = controller;
            initializedBugPositions.Clear();
            initializedBabyPositions.Clear();
            initializedPickupPositions.Clear();
            initializedEggPositions.Clear();
            BattleFx fx = GetComponent<BattleFx>();
            if (fx != null) fx.Bind(match);
        }

        /// <summary>创建运行时容器，保证表现对象不会散落在场景根节点。</summary>
        private void EnsureRoots()
        {
            bugsRoot = CreateRoot("Bugs");
            babiesRoot = CreateRoot("Babies");
            eggsRoot = CreateRoot("Eggs");
            pickupsRoot = CreateRoot("Pickups");
            nestRoot = CreateRoot("Nest");
            arrowsRoot = CreateRoot("ChargeArrows");
            ringsRoot = CreateRoot("StaminaRings");
            barsRoot = CreateRoot("StaminaBars");
            markersRoot = CreateRoot("GroundMarkers");
            if (ringsRoot != null) ringsRoot.gameObject.SetActive(false);
            _ = staminaRingPrefab;
            _ = groundMarkerPrefab;
        }

        private Transform CreateRoot(string rootName)
        {
            Transform root = transform.Find(rootName);
            if (root != null) return root;
            GameObject child = new GameObject(rootName);
            child.transform.SetParent(transform, false);
            return child.transform;
        }

        /// <summary>按当前状态刷新五类实体；对象只创建一次，离场后先隐藏以便复用。</summary>
        private void RefreshView()
        {
            MatchState state = match == null ? null : match.State;
            if (state == null) return;
            if (match.RunMode == MatchRunMode.Client && state.tick != observedClientTick)
            {
                observedClientTick = state.tick;
                clientSnapshotAge = 0f;
            }
            RefreshBugs(state);
            RefreshBabies(state);
            RefreshEggs(state);
            RefreshPickups(state);
            RefreshNest(state);
            RefreshChargeArrows(state);
            RefreshGroundMarkers(state);
            RefreshStaminaOverlays(state);
            RefreshSkillBars(state);
            RefreshZoneView(state);
        }

        private void RefreshZoneView(MatchState state)
        {
            if (zoneView == null) zoneView = ArenaZoneView.Ensure();
            if (zoneView == null || state == null) return;
            bool schedule = state.playerCount > 1 && state.knobs != null && state.knobs.zoneSchedule;
            zoneView.Refresh(state.knobs, state.elapsed, schedule);
        }

        private void RefreshBugs(MatchState state)
        {
            if (!ReferenceEquals(assignedProfileState, state) || assignedProfileSeed != state.randomSeed)
            {
                assignedBugProfiles.Clear();
                assignedSkinLabels.Clear();
                initializedBugPositions.Clear();
                initializedBabyPositions.Clear();
                initializedPickupPositions.Clear();
                initializedEggPositions.Clear();
                assignedProfileState = state;
                assignedProfileSeed = state.randomSeed;
            }
            seenIds.Clear();
            if (state.bugs == null) return;
            for (int i = 0; i < state.bugs.Length; i++)
            {
                BugState bug = state.bugs[i];
                if (bug == null) continue;
                seenIds.Add(bug.id);
                int profile = VisualProfileForBug(state, bug);
                int assignedProfile;
                if (assignedBugProfiles.TryGetValue(bug.id, out assignedProfile) && assignedProfile != profile)
                {
                    GameObject old;
                    if (bugViews.TryGetValue(bug.id, out old) && old != null) Destroy(old);
                    bugViews.Remove(bug.id);
                }
                GameObject prefab = cricketUnitPrefab != null ? cricketUnitPrefab : PrefabForBug(bug.id);
                GameObject view = GetOrCreate(bugViews, bug.id, prefab, bugsRoot, "Bug_" + bug.id);
                if (view == null) continue;
                CricketUnit unit = view.GetComponent<CricketUnit>();
                GameObject body = BodyOf(view);
                if (body == null) continue;
                CricketVisual skeletal = body.GetComponent<CricketVisual>();
                int quality = profile / 4 + 1;
                int temperament = profile % 4 + 1;
                string skinLabel = CricketVisual.SkinLabel(quality, temperament, bug.guanYuGhost);
                string assignedSkin;
                bool profileChanged = !assignedBugProfiles.TryGetValue(bug.id, out assignedProfile) || assignedProfile != profile;
                bool skinChanged = !assignedSkinLabels.TryGetValue(bug.id, out assignedSkin) || assignedSkin != skinLabel;
                if (profileChanged || skinChanged)
                {
                    if (skeletal != null)
                        skeletal.ApplySkin(skinLabel);
                    else if (profileChanged)
                        ApplyPremiumBugSprite(body, profile);
                    assignedBugProfiles[bug.id] = profile;
                    assignedSkinLabels[bug.id] = skinLabel;
                }
                view.SetActive(bug.alive);
                if (!bug.alive)
                {
                    initializedBugPositions.Remove(bug.id);
                    continue;
                }
                Vector3 predictedPosition = bug.position;
                if (match.RunMode == MatchRunMode.Client)
                {
                    float predictionTime = Mathf.Min(clientSnapshotAge, clientExtrapolationLimit);
                    predictedPosition += new Vector3(bug.velocity.x, 0f, bug.velocity.z) * predictionTime;
                }
                if (unit != null)
                {
                    Vector3 target = new Vector3(predictedPosition.x, 0f, predictedPosition.z);
                    view.transform.position = SmoothClientWorldPosition(initializedBugPositions, bug.id, view.transform.position, target);
                    view.transform.rotation = Quaternion.identity;
                    view.transform.localScale = Vector3.one;
                    float grow = bug.radius / Mathf.Max(0.01f, state.knobs.bugR);
                    unit.ApplyMotion(bug.height, grow);
                }
                else
                {
                    float visualScale = VisualScale(body, bug.radius, state.knobs.bugR);
                    Vector3 target = predictedPosition + Vector3.up * (groundOffset + bug.height);
                    view.transform.position = SmoothClientWorldPosition(initializedBugPositions, bug.id, view.transform.position, target);
                    view.transform.localScale = Vector3.one * visualScale;
                }
                // 蓄力中跟摇杆（图片上部=头）；飞行中跟速度。空中不改朝向。
                CricketVisual cricket = body.GetComponent<CricketVisual>();
                FaceXz(body, bug.charging ? Vector3.zero : bug.velocity, bug.chargeDirection);
                if (unit != null) unit.AlignMarkerToBody();
                if (cricket != null)
                {
                    cricket.ApplyTeam(bug.id == 0, bug.charging, Rules.ChargeLocked(bug), bug.guanYuGhost);
                    cricket.ApplyCrown(bug.id == LocalPlayerId() && PlayerDataService.CrownEquipped);
                    CricketAnim anim = body.GetComponent<CricketAnim>();
                    if (anim == null) anim = body.GetComponentInChildren<CricketAnim>(true);
                    if (anim == null) anim = body.AddComponent<CricketAnim>();
                    anim.Apply(bug);
                }
                else if (tintPlayers)
                {
                    Color tint = GroundMarker.ColorForPlayer(bug.id);
                    if (bug.charging) tint = Color.Lerp(tint, Color.white, 0.35f);
                    Tint(body, tint);
                }
                else
                {
                    Tint(body, bug.charging ? new Color(1f, 0.96f, 0.88f, 1f) : Color.white);
                }
            }
            HideUnseen(bugViews, seenIds);
        }

        /// <summary>镜头跟随平滑后的本机 Transform；没有表现对象时返回 false，由镜头退回权威坐标。</summary>
        public bool TryGetVisualFollowPosition(int playerId, out Vector3 position)
        {
            position = Vector3.zero;
            if (match == null || match.State == null || match.State.bugs == null) return false;
            if (playerId < 0 || playerId >= match.State.bugs.Length) return false;
            BugState bug = match.State.bugs[playerId];
            if (bug == null || !bug.alive) return false;
            GameObject view;
            if (!bugViews.TryGetValue(bug.id, out view) || view == null || !view.activeSelf) return false;
            position = view.transform.position;
            return true;
        }

        /// <summary>
        /// 客户端只平滑表现 Transform，不改权威 MatchState。出生、复活或大幅纠正时直接对齐，
        /// 普通移动则用与帧率无关的指数插值消除低频快照造成的跳格。
        /// </summary>
        private Vector3 SmoothClientWorldPosition(HashSet<int> initialized, int id, Vector3 current, Vector3 target)
        {
            return ClientVisualSmoothing.Step(
                current,
                target,
                initialized,
                id,
                clientSnapDistance,
                clientPositionSmoothing,
                Time.unscaledDeltaTime,
                match != null && match.RunMode == MatchRunMode.Client);
        }

        private int VisualProfileForBug(MatchState state, BugState bug)
        {
            int quality = bug != null ? bug.quality : 0;
            int temperament = bug != null ? bug.temperament : 0;
            int slot = state.cricketIndex != null && bug.id >= 0 && bug.id < state.cricketIndex.Length
                ? state.cricketIndex[bug.id] : 0;
            if (quality < 1 || temperament < 1)
            {
                if (state.roster != null && bug.id >= 0 && bug.id < state.roster.Length)
                {
                    CricketPick[] picks = state.roster[bug.id];
                    CricketPick pick = picks != null && slot >= 0 && slot < picks.Length ? picks[slot] : null;
                    if (pick != null && pick.catalogId != 0)
                    {
                        quality = Mathf.Clamp(pick.quality, 1, 4);
                        temperament = Mathf.Clamp(pick.temperament, 1, 4);
                    }
                }
            }

            if (quality < 1 || temperament < 1)
            {
                quality = PositiveModulo(state.randomSeed + bug.id + slot * 17, 4) + 1;
                temperament = PositiveModulo((state.randomSeed / 7) + bug.id * 3 + slot * 11, 4) + 1;
            }

            return (quality - 1) * 4 + (temperament - 1);
        }

        private static int PositiveModulo(int value, int modulus)
        {
            int result = value % modulus;
            return result < 0 ? result + modulus : result;
        }

        private void ApplyPremiumBugSprite(GameObject view, int profile)
        {
            if (premiumBugSprites == null || premiumBugSprites.Length == 0) return;
            profile = Mathf.Clamp(profile, 0, premiumBugSprites.Length - 1);
            Sprite sprite = premiumBugSprites[profile];
            if (sprite == null) return;
            SpriteRenderer renderer = view.GetComponentInChildren<SpriteRenderer>();
            if (renderer != null && renderer.sprite != sprite) renderer.sprite = sprite;
        }

        private void RefreshBabies(MatchState state)
        {
            seenIds.Clear();
            for (int i = 0; i < state.babies.Count; i++)
            {
                BabyState baby = state.babies[i];
                if (baby == null) continue;
                seenIds.Add(baby.id);
                GameObject view = GetOrCreate(babyViews, baby.id, babyPrefab, babiesRoot, "Baby_" + baby.id);
                if (view == null) continue;
                view.SetActive(baby.alive);
                if (!baby.alive)
                {
                    initializedBabyPositions.Remove(baby.id);
                    continue;
                }
                Vector3 babyTarget = baby.position;
                if (match.RunMode == MatchRunMode.Client)
                {
                    float predictionTime = Mathf.Min(clientSnapshotAge, clientExtrapolationLimit);
                    babyTarget += new Vector3(baby.velocity.x, 0f, baby.velocity.z) * predictionTime;
                }
                babyTarget += Vector3.up * (groundOffset + baby.height);
                view.transform.position = SmoothClientWorldPosition(
                    initializedBabyPositions, baby.id, view.transform.position, babyTarget);
                float babyRef = Mathf.Max(0.01f, state.knobs.bugR * Mathf.Max(0.01f, state.knobs.babyRScale));
                view.transform.localScale = Vector3.one * VisualScale(view, baby.radius, babyRef);
                FaceXz(view, baby.velocity, baby.chargeDirection);
                Tint(view, Color.white);
            }
            HideUnseen(babyViews, seenIds);
        }

        private void RefreshEggs(MatchState state)
        {
            for (int i = 0; i < state.eggs.Count; i++)
            {
                EggState egg = state.eggs[i];
                if (egg == null) continue;
                while (eggViews.Count <= i)
                    eggViews.Add(CreateView(eggPrefab, eggsRoot, "Egg_" + eggViews.Count));
                GameObject view = eggViews[i];
                if (view == null) continue;
                view.SetActive(egg.alive);
                if (!egg.alive)
                {
                    initializedEggPositions.Remove(i);
                    continue;
                }
                Vector3 eggTarget = egg.position + Vector3.up * groundOffset;
                if (match.RunMode == MatchRunMode.Client)
                {
                    float predictionTime = Mathf.Min(clientSnapshotAge, clientExtrapolationLimit);
                    eggTarget += new Vector3(egg.velocity.x, 0f, egg.velocity.z) * predictionTime;
                }
                view.transform.position = SmoothClientWorldPosition(
                    initializedEggPositions, i, view.transform.position, eggTarget);
                view.transform.localScale = Vector3.one;
                Tint(view, new Color(0.95f, 0.95f, 0.72f));
            }
            for (int i = state.eggs.Count; i < eggViews.Count; i++)
                if (eggViews[i] != null) eggViews[i].SetActive(false);
        }

        private void RefreshPickups(MatchState state)
        {
            seenIds.Clear();
            for (int i = 0; i < state.pickups.Count; i++)
            {
                PickupState pickup = state.pickups[i];
                if (pickup == null) continue;
                seenIds.Add(pickup.id);
                GameObject prefab = PrefabForPickup(pickup.kind);
                GameObject view;
                if (!pickupViews.TryGetValue(pickup.id, out view) || pickupKinds[pickup.id] != pickup.kind)
                {
                    if (view != null) Destroy(view);
                    view = CreateView(prefab, pickupsRoot, "Pickup_" + pickup.id);
                    pickupViews[pickup.id] = view;
                    pickupKinds[pickup.id] = pickup.kind;
                }
                if (view == null) continue;
                view.SetActive(pickup.alive);
                if (!pickup.alive)
                {
                    initializedPickupPositions.Remove(pickup.id);
                    continue;
                }
                Vector3 pickupTarget = pickup.position + Vector3.up * groundOffset;
                view.transform.position = SmoothClientWorldPosition(
                    initializedPickupPositions, pickup.id, view.transform.position, pickupTarget);
                bool shield = pickup.kind == "shield" || pickup.kind == "charge";
                view.transform.localScale = Vector3.one * (shield ? 3.4f : 2f);
            }
            HideUnseen(pickupViews, seenIds);
        }

        private void RefreshNest(MatchState state)
        {
            if (state.nest == null || !state.nest.alive)
            {
                if (nestView != null) nestView.SetActive(false);
                return;
            }
            if (nestView == null)
            {
                nestView = CreateView(nestPrefab, nestRoot, "NestView");
                lastNestHits = int.MinValue;
            }
            if (nestView == null) return;
            nestView.SetActive(true);
            nestView.transform.position = state.nest.position + Vector3.up * 0.15f;
            nestView.transform.localScale = Vector3.one * VisualScale(nestView, state.knobs.nestR, state.knobs.nestR) * 1.45f;
            Tint(nestView, Color.white);
            RefreshNestHits(nestView, Mathf.Max(0, Mathf.CeilToInt(state.nest.hp)));
        }

        private void RefreshNestHits(GameObject nestView, int hits)
        {
            if (hits == lastNestHits) return;
            lastNestHits = hits;
            Transform badge = nestView.transform.Find("HitsBadge");
            if (badge == null)
            {
                GameObject go = new GameObject("HitsBadge");
                go.transform.SetParent(nestView.transform, false);
                badge = go.transform;
            }

            SpriteRenderer leftover = badge.GetComponent<SpriteRenderer>();
            if (leftover != null) leftover.enabled = false;

            badge.localRotation = Quaternion.identity;
            badge.localScale = Vector3.one;
            badge.localPosition = NestHitLabelLocal(nestView);

            TextMesh hitsText = null;
            TextMesh[] labels = badge.GetComponentsInChildren<TextMesh>();
            for (int i = 0; i < labels.Length; i++)
            {
                if (labels[i] != null && labels[i].name == "Hits")
                {
                    hitsText = labels[i];
                    break;
                }
            }
            if (hitsText == null)
                hitsText = CreateNestHitGlyph(badge, "Hits", 18);
            if (badge.Find("HitsOutline0") == null)
            {
                float outline = 0.018f;
                CreateNestHitGlyph(badge, "HitsOutline0", 17).transform.localPosition = new Vector3(-outline, 0f, 0.01f);
                CreateNestHitGlyph(badge, "HitsOutline1", 17).transform.localPosition = new Vector3(outline, 0f, 0.01f);
                CreateNestHitGlyph(badge, "HitsOutline2", 17).transform.localPosition = new Vector3(0f, -outline, 0.01f);
                CreateNestHitGlyph(badge, "HitsOutline3", 17).transform.localPosition = new Vector3(0f, outline, 0.01f);
            }
            string value = hits.ToString();
            Color fill = new Color(0.06f, 0.05f, 0.04f, 1f);
            Color outlineColor = new Color(1f, 0.97f, 0.90f, 1f);
            labels = badge.GetComponentsInChildren<TextMesh>();
            for (int i = 0; i < labels.Length; i++)
            {
                TextMesh label = labels[i];
                if (label == null) continue;
                label.text = value;
                label.color = label.name == "Hits" ? fill : outlineColor;
            }
            badge.gameObject.SetActive(hits > 0);
        }

        static TextMesh CreateNestHitGlyph(Transform parent, string objectName, int sortingOrder)
        {
            GameObject label = new GameObject(objectName);
            label.transform.SetParent(parent, false);
            label.transform.localPosition = Vector3.zero;
            label.transform.localRotation = Quaternion.identity;
            label.transform.localScale = Vector3.one;
            TextMesh text = label.AddComponent<TextMesh>();
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.characterSize = 0.11f;
            text.fontSize = 80;
            text.fontStyle = FontStyle.Bold;
            MeshRenderer mesh = label.GetComponent<MeshRenderer>();
            if (mesh != null) mesh.sortingOrder = sortingOrder;
            return text;
        }

        /// <summary>次数写在巢图右下角自带圆圈里，不再另叠一层 NestHits。</summary>
        static Vector3 NestHitLabelLocal(GameObject nestView)
        {
            SpriteRenderer spriteRenderer = nestView.GetComponent<SpriteRenderer>();
            if (spriteRenderer == null || spriteRenderer.sprite == null)
                return new Vector3(0.52f, -0.48f, -0.02f);
            Bounds bounds = spriteRenderer.sprite.bounds;
            return new Vector3(
                bounds.min.x + bounds.size.x * 0.775f,
                bounds.min.y + bounds.size.y * 0.235f,
                -0.02f);
        }

        private GameObject PrefabForBug(int id)
        {
            if (bugPrefab != null) return bugPrefab;
            if (id == 0 && qingTouPrefab != null) return qingTouPrefab;
            if (id == 1 && youHuluPrefab != null) return youHuluPrefab;
            if (id % 2 == 0 && qingTouPrefab != null) return qingTouPrefab;
            return youHuluPrefab;
        }

        /// <summary>
        /// 把预制体视觉外接圆对齐玩法半径。scale=1 时 Sprite 大约 1 单位，
        /// 而 bugR=1.8 的碰撞直径是 3.6，不拟合就会「手感比画面大一圈」。
        /// </summary>
        private float VisualScale(GameObject view, float radius, float referenceRadius)
        {
            if (!fitVisualToCollision) return Mathf.Max(0.05f, radius / Mathf.Max(0.01f, referenceRadius));
            float visual = SpriteVisualSize(view);
            return Mathf.Max(0.05f, (2f * radius) / Mathf.Max(0.05f, visual));
        }

        private static float SpriteVisualSize(GameObject view)
        {
            CricketVisual cricket = view.GetComponent<CricketVisual>();
            if (cricket != null) return cricket.VisualSize;

            SpriteRenderer spriteRenderer = view.GetComponentInChildren<SpriteRenderer>();
            if (spriteRenderer != null && spriteRenderer.sprite != null)
            {
                Vector3 size = spriteRenderer.sprite.bounds.size;
                return Mathf.Max(size.x, size.y);
            }
            return 1f;
        }

        private void RefreshChargeArrows(MatchState state)
        {
            WarnIfOverlaysMissing();
            seenIds.Clear();
            MatchKnobs knobs = state.knobs;
            if (state.bugs != null)
            {
                for (int i = 0; i < state.bugs.Length; i++)
                {
                    BugState bug = state.bugs[i];
                    if (bug == null || !bug.alive) continue;
                    float cap = Rules.EffectiveChargeTime(knobs, bug);
                    float dist = Rules.JumpDistance(knobs, bug, bug.chargeTime);
                    float fill = cap > 0.0001f ? Mathf.Clamp01(bug.chargeTime / cap) : 0f;
                    PlaceChargeArrow(bug.id, bug.charging, dist, fill, knobs, bug.chargeDirection, bug.position, bug.radius, GroundMarker.ColorForPlayer(bug.id));
                    if (bug.charging) seenIds.Add(bug.id);
                }
            }
            for (int i = 0; i < state.babies.Count; i++)
            {
                BabyState baby = state.babies[i];
                if (baby == null || !baby.alive) continue;
                float cap = Rules.BabyChargeTime(knobs);
                float speed = Rules.BabyChargeSpeed(knobs, baby);
                float dist = Rules.JumpRange(knobs, speed);
                float fill = cap > 0.0001f ? Mathf.Clamp01(baby.chargeTime / cap) : 0f;
                PlaceChargeArrow(baby.id, baby.charging, dist, fill, knobs, baby.chargeDirection, baby.position, baby.radius, GroundMarker.ColorForPlayer(baby.ownerId));
                if (baby.charging) seenIds.Add(baby.id);
            }
            foreach (KeyValuePair<int, ChargeArrow> pair in chargeArrows)
                if (!seenIds.Contains(pair.Key) && pair.Value != null) pair.Value.Hide();
        }

        private void PlaceChargeArrow(int id, bool charging, float dist, float fill, MatchKnobs knobs, Vector2 direction, Vector3 position, float radius, Color playerColor)
        {
            if (!charging)
            {
                ChargeArrow existing;
                if (chargeArrows.TryGetValue(id, out existing) && existing != null) existing.Hide();
                return;
            }
            ChargeArrow arrow = GetChargeArrow(id);
            if (arrow == null) return;
            float ratio = knobs != null ? knobs.chargeBarRatio : 3f;
            float alphaMin = knobs != null ? knobs.chargeBarAlphaMin : 0.4f;
            float alphaMax = knobs != null ? knobs.chargeBarAlphaMax : 1f;
            arrow.Apply(true, dist, fill, direction, position + Vector3.up * 0.08f, radius, playerColor, ratio, alphaMin, alphaMax);
        }

        private void RefreshGroundMarkers(MatchState state)
        {
            seenIds.Clear();
            if (state.bugs != null)
            {
                for (int i = 0; i < state.bugs.Length; i++)
                {
                    BugState bug = state.bugs[i];
                    if (bug == null || !bug.alive) continue;
                    seenIds.Add(bug.id);
                    PlaceGroundMarker(bug.id, bug.position, bug.radius, bug.height, bug.charging, GroundMarker.ColorForPlayer(bug.id));
                }
            }
            for (int i = 0; i < state.babies.Count; i++)
            {
                BabyState baby = state.babies[i];
                if (baby == null || !baby.alive) continue;
                int markerId = 1000 + baby.id;
                seenIds.Add(markerId);
                PlaceGroundMarker(markerId, baby.position, baby.radius, baby.height, baby.charging, GroundMarker.ColorForPlayer(baby.ownerId));
            }
            foreach (KeyValuePair<int, GroundMarker> pair in groundMarkers)
                if (!seenIds.Contains(pair.Key) && pair.Value != null) pair.Value.Hide();
        }

        private void PlaceGroundMarker(int id, Vector3 position, float radius, float height, bool charging, Color playerColor)
        {
            GroundMarker marker = GetGroundMarker(id);
            if (marker == null) return;
            if (MarkerOfUnit(id) != null) marker.Paint(playerColor, charging);
            else marker.Apply(position, radius, playerColor, height, charging);
        }

        private void RefreshStaminaOverlays(MatchState state)
        {
            WarnIfOverlaysMissing();
            seenIds.Clear();
            MatchKnobs knobs = state.knobs;
            if (state.bugs != null && knobs != null)
            {
                for (int i = 0; i < state.bugs.Length; i++)
                {
                    BugState bug = state.bugs[i];
                    if (bug == null || !bug.alive) continue;
                    seenIds.Add(bug.id);
                    PlaceStaminaBar(bug, knobs);
                }
            }
            foreach (KeyValuePair<int, StaminaRing> pair in staminaRings)
                if (pair.Value != null) pair.Value.Hide();
            foreach (KeyValuePair<int, StaminaBar> pair in staminaBars)
                if (!seenIds.Contains(pair.Key) && pair.Value != null) pair.Value.Hide();
        }

        private void RefreshSkillBars(MatchState state)
        {
            seenIds.Clear();
            if (state.bugs == null || state.knobs == null) return;
            float max = Mathf.Max(0.01f, state.knobs.luBuArmorT);
            for (int i = 0; i < state.bugs.Length; i++)
            {
                BugState bug = state.bugs[i];
                if (bug == null || !bug.alive || !Rules.ChargeLocked(bug)) continue;
                seenIds.Add(bug.id);
                SkillBar bar = GetSkillBar(bug.id);
                if (bar == null) continue;
                bar.Apply(bug.luBuArmorT / max, bug.position, bug.radius, bug.height);
            }
            foreach (KeyValuePair<int, SkillBar> pair in skillBars)
                if (!seenIds.Contains(pair.Key) && pair.Value != null) pair.Value.Hide();
        }

        private void PlaceStaminaBar(BugState bug, MatchKnobs knobs)
        {
            StaminaBar bar = GetStaminaBar(bug.id);
            if (bar == null) return;
            float max = Mathf.Max(1f, Rules.StaminaMaxOf(knobs, bug));
            int slots = Mathf.Clamp(knobs.staminaSlots, 3, StaminaBar.MaxSlots);
            float current = Mathf.Max(0f, bug.stamina);
            float pending = bug.charging ? Rules.JumpStaminaCost(knobs, bug) : 0f;
            float ratio = current / max;
            float pendingRatio = pending / max;
            float hotGate = Rules.IsLuBu(bug) ? Mathf.Clamp01(knobs.luBuArmorStamina) : 0f;
            float grow = bug.radius / Mathf.Max(0.01f, knobs.bugR);
            bar.SetGrow(grow);
            if (BarOfUnit(bug.id) != null) bar.ApplyFill(ratio, slots, pendingRatio, hotGate);
            else bar.Apply(ratio, slots, bug.position + Vector3.up * bug.height, bug.radius, pendingRatio, hotGate);
        }

        private static GameObject BodyOf(GameObject view)
        {
            if (view == null) return null;
            CricketUnit unit = view.GetComponent<CricketUnit>();
            if (unit != null && unit.BodyObject != null) return unit.BodyObject;
            return view;
        }

        private GroundMarker MarkerOfUnit(int id)
        {
            GameObject view;
            if (!bugViews.TryGetValue(id, out view) || view == null) return null;
            CricketUnit unit = view.GetComponent<CricketUnit>();
            return unit != null ? unit.Marker : null;
        }

        private StaminaBar BarOfUnit(int id)
        {
            GameObject view;
            if (!bugViews.TryGetValue(id, out view) || view == null) return null;
            CricketUnit unit = view.GetComponent<CricketUnit>();
            return unit != null ? unit.Bar : null;
        }

        private GroundMarker GetGroundMarker(int id)
        {
            GroundMarker fromUnit = MarkerOfUnit(id);
            if (fromUnit != null) return fromUnit;
            GroundMarker marker;
            if (groundMarkers.TryGetValue(id, out marker) && marker != null) return marker;
            Transform parent = markersRoot != null ? markersRoot : transform;
            marker = InstantiateOverlay<GroundMarker>(groundMarkerPrefab, parent, "GroundMarker_" + id);
            if (marker == null)
            {
                GameObject view = new GameObject("GroundMarker_" + id);
                view.transform.SetParent(parent, false);
                marker = view.AddComponent<GroundMarker>();
            }
            groundMarkers[id] = marker;
            return marker;
        }

        private SkillBar GetSkillBar(int id)
        {
            SkillBar bar;
            if (skillBars.TryGetValue(id, out bar) && bar != null) return bar;
            Transform parent = barsRoot != null ? barsRoot : transform;
            GameObject view = new GameObject("SkillBar_" + id);
            view.transform.SetParent(parent, false);
            bar = view.AddComponent<SkillBar>();
            skillBars[id] = bar;
            return bar;
        }

        private StaminaBar GetStaminaBar(int id)
        {
            StaminaBar fromUnit = BarOfUnit(id);
            if (fromUnit != null) return fromUnit;
            StaminaBar bar;
            if (staminaBars.TryGetValue(id, out bar) && bar != null) return bar;
            bar = InstantiateOverlay<StaminaBar>(staminaBarPrefab, barsRoot, "StaminaBar_" + id);
            if (bar != null) staminaBars[id] = bar;
            return bar;
        }

        private ChargeArrow GetChargeArrow(int id)
        {
            ChargeArrow arrow;
            if (chargeArrows.TryGetValue(id, out arrow) && arrow != null) return arrow;
            arrow = InstantiateOverlay<ChargeArrow>(chargeArrowPrefab, arrowsRoot, "ChargeArrow_" + id);
            if (arrow != null) chargeArrows[id] = arrow;
            return arrow;
        }

        private static T InstantiateOverlay<T>(GameObject prefab, Transform parent, string objectName) where T : Component
        {
            if (prefab == null) return null;
            GameObject view = Instantiate(prefab, parent);
            view.name = objectName;
            return view.GetComponent<T>();
        }

        private void WarnIfOverlaysMissing()
        {
            if (warnedMissingOverlays) return;
            bool unitReady = cricketUnitPrefab != null;
            bool overlaysReady = staminaBarPrefab != null && groundMarkerPrefab != null;
            if (chargeArrowPrefab != null && (unitReady || overlaysReady)) return;
            warnedMissingOverlays = true;
            Debug.LogWarning("[DouQuqu] 缺少成虫单位或覆盖层预制体，请在菜单运行 DouQuqu/Rebuild Overlay Prefabs。");
        }

        /// <summary>
        /// 顶视朝向。贴图正面朝上对着顶视相机。不用 Euler(90, yaw, 0)，避免 X=90 万向节锁把偏航吃掉。
        /// 贴图头在图上部：本地 +Y 对准蓄力/飞行方向。
        /// </summary>
        private static void FaceXz(GameObject view, Vector3 velocity, Vector2 chargeDirection)
        {
            Vector2 face = new Vector2(velocity.x, velocity.z);
            if (face.sqrMagnitude < 0.04f) face = chargeDirection;
            if (face.sqrMagnitude < 0.0001f) face = Vector2.up;
            Vector3 head = new Vector3(face.x, 0f, face.y);
            view.transform.rotation = Quaternion.LookRotation(Vector3.up, head);
        }

        int LocalPlayerId()
        {
            if (match == null) return 0;
            LanSession lan = match.GetComponent<LanSession>();
            if (lan == null) lan = match.GetComponentInParent<LanSession>();
            if (lan == null) lan = match.GetComponentInChildren<LanSession>(true);
            if (lan != null && lan.LocalPlayerId >= 0) return lan.LocalPlayerId;
            return 0;
        }

        private GameObject PrefabForPickup(string kind)
        {
            if (kind == "size") return sizePrefab;
            if (kind == "shield" || kind == "charge") return shieldPrefab;
            return heartPrefab;
        }

        private static Color PickupColor(string kind)
        {
            if (kind == "size") return new Color(1f, 0.38f, 0.9f);
            if (kind == "shield" || kind == "charge") return new Color(0.25f, 0.85f, 1f);
            return new Color(1f, 0.22f, 0.32f);
        }

        private GameObject GetOrCreate(Dictionary<int, GameObject> views, int id, GameObject prefab, Transform parent, string objectName)
        {
            GameObject view;
            if (views.TryGetValue(id, out view) && view != null) return view;
            view = CreateView(prefab, parent, objectName);
            views[id] = view;
            return view;
        }

        private GameObject CreateView(GameObject prefab, Transform parent, string objectName)
        {
            if (prefab == null) return null;
            GameObject view = Instantiate(prefab, parent);
            view.name = objectName;
            return view;
        }

        private static void HideUnseen(Dictionary<int, GameObject> views, HashSet<int> ids)
        {
            foreach (KeyValuePair<int, GameObject> pair in views)
                if (!ids.Contains(pair.Key) && pair.Value != null) pair.Value.SetActive(false);
        }

        /// <summary>用属性块着色，不复制材质资产，运行时不会污染预制体。</summary>
        private void Tint(GameObject view, Color color)
        {
            Renderer[] renderers = view.GetComponentsInChildren<Renderer>();
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                // SpriteRenderer 直接改颜色最稳定；部分 Unity 版本对
                // SpriteRenderer.GetPropertyBlock 的目标对象校验更严格，
                // 这里不再走材质属性块，避免 2D 贴图刷新时抛异常。
                SpriteRenderer spriteRenderer = renderer as SpriteRenderer;
                if (spriteRenderer != null)
                {
                    spriteRenderer.color = color;
                    continue;
                }
                if (renderer.GetComponent<TextMesh>() != null) continue;
                if (propertyBlock == null) propertyBlock = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(propertyBlock);
                propertyBlock.SetColor("_BaseColor", color);
                propertyBlock.SetColor("_Color", color);
                renderer.SetPropertyBlock(propertyBlock);
            }
        }
    }
}
