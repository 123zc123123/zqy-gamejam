using System.Collections.Generic;
using UnityEngine;

namespace DouQuqu
{
    /// <summary>
    /// Demo 表现层：把权威 MatchState 映射到简单几何体预制体。
    /// 该组件不参与物理和规则计算，只负责生成、复用、隐藏和着色场景对象。
    /// </summary>
    public sealed class DouQuquDemoView : MonoBehaviour
    {
        [Header("对局")]
        [SerializeField] private DouQuquMatchController match;
        [SerializeField] private bool autoStart = true;
        [SerializeField, Range(1, DouQuquMatchController.MaxPlayers)] private int playerCount = 4;
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

        [Header("显示")]
        [SerializeField] private float groundOffset = 0.35f;
        [SerializeField] private bool tintPlayers = false;
        [SerializeField] private bool fitVisualToCollision = true;

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
        private readonly Dictionary<int, DouQuquChargeArrow> chargeArrows = new Dictionary<int, DouQuquChargeArrow>();
        private readonly Dictionary<int, DouQuquStaminaRing> staminaRings = new Dictionary<int, DouQuquStaminaRing>();
        private readonly Dictionary<int, DouQuquStaminaBar> staminaBars = new Dictionary<int, DouQuquStaminaBar>();
        private readonly Dictionary<int, DouQuquGroundMarker> groundMarkers = new Dictionary<int, DouQuquGroundMarker>();
        private readonly Dictionary<int, int> assignedBugProfiles = new Dictionary<int, int>();
        private Sprite[] premiumBugSprites;
        private MatchState assignedProfileState;
        private int assignedProfileSeed = int.MinValue;
        private bool warnedMissingOverlays;

        private static readonly Color[] PlayerColors = DouQuquGroundMarker.PlayerColors;

        private void Awake()
        {
            propertyBlock = new MaterialPropertyBlock();
            if (match == null) match = GetComponent<DouQuquMatchController>();
            if (match == null) match = FindObjectOfType<DouQuquMatchController>();
            LoadPremiumBugSprites();
            EnsureBattleCamera();
            EnsureRoots();
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
            if (main.GetComponent<DouQuquBattleCamera>() == null)
                main.gameObject.AddComponent<DouQuquBattleCamera>();
        }

        private void OnEnable()
        {
            if (match != null) match.StateChanged += OnStateChanged;
        }

        private void Start()
        {
            if (match == null) return;
            if (autoStart && !match.IsStarted)
            {
                match.Configure(MatchRunMode.Offline, Mathf.Clamp(playerCount, 1, DouQuquMatchController.MaxPlayers));
                match.ResetMatch(playerCount, randomSeed);
                match.StartMatch();
            }
            RefreshView();
        }

        private void Update()
        {
            // 客户端快照和非 Unity 驱动的控制器也能通过每帧刷新及时更新表现。
            if (match != null && match.State != null) RefreshView();
        }

        private void OnDisable()
        {
            if (match != null) match.StateChanged -= OnStateChanged;
        }

        private void OnStateChanged(MatchState state)
        {
            RefreshView();
        }

        /// <summary>由战斗场景流程关闭旧的自动开局，防止客户端等待快照时误启动单机局。</summary>
        public void SetAutoStart(bool enabled)
        {
            autoStart = enabled;
        }

        /// <summary>允许场景流程在运行时替换为当前场景的权威状态组件。</summary>
        public void BindMatch(DouQuquMatchController controller)
        {
            if (match == controller) return;
            if (isActiveAndEnabled && match != null) match.StateChanged -= OnStateChanged;
            match = controller;
            if (isActiveAndEnabled && match != null) match.StateChanged += OnStateChanged;
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
            RefreshBugs(state);
            RefreshBabies(state);
            RefreshEggs(state);
            RefreshPickups(state);
            RefreshNest(state);
            RefreshChargeArrows(state);
            RefreshGroundMarkers(state);
            RefreshStaminaOverlays(state);
        }

        private void RefreshBugs(MatchState state)
        {
            if (!ReferenceEquals(assignedProfileState, state) || assignedProfileSeed != state.randomSeed)
            {
                assignedBugProfiles.Clear();
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
                GameObject view = GetOrCreate(bugViews, bug.id, PrefabForBug(bug.id), bugsRoot, "Bug_" + bug.id);
                if (view == null) continue;
                int profile = VisualProfileForBug(state, bug);
                int assignedProfile;
                if (!assignedBugProfiles.TryGetValue(bug.id, out assignedProfile) || assignedProfile != profile)
                {
                    // 骨骼蛐蛐走 Sprite Library，不能把身体 Sprite 换成精品立绘。
                    if (view.GetComponent<DouQuquCricketVisual>() == null)
                        ApplyPremiumBugSprite(view, profile);
                    assignedBugProfiles[bug.id] = profile;
                }
                view.SetActive(bug.alive);
                if (!bug.alive) continue;
                view.transform.position = bug.position + Vector3.up * (groundOffset + bug.height);
                view.transform.localScale = Vector3.one * VisualScale(view, bug.radius, state.knobs.bugR);
                // 蓄力中跟摇杆（图片上部=头）；飞行中跟速度。空中不改朝向。
                FaceXz(view, bug.charging ? Vector3.zero : bug.velocity, bug.chargeDirection);
                DouQuquCricketVisual cricket = view.GetComponent<DouQuquCricketVisual>();
                if (cricket != null)
                {
                    cricket.ApplyTeam(bug.id == 0, bug.charging);
                }
                else if (tintPlayers)
                {
                    Color tint = PlayerColors[Mathf.Abs(bug.id) % PlayerColors.Length];
                    if (bug.charging) tint = Color.Lerp(tint, Color.white, 0.35f);
                    Tint(view, tint);
                }
                else
                {
                    Tint(view, bug.charging ? new Color(1f, 0.96f, 0.88f, 1f) : Color.white);
                }
            }
            HideUnseen(bugViews, seenIds);
        }

        private int VisualProfileForBug(MatchState state, BugState bug)
        {
            // 已接入选虫数据时沿用真实品质/性格；占位选虫则按对局种子分散到不同品质。
            int quality = 0;
            int temperament = 0;
            int slot = state.cricketIndex != null && bug.id >= 0 && bug.id < state.cricketIndex.Length
                ? state.cricketIndex[bug.id] : 0;
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

            if (quality == 0)
            {
                // 轮换品质保证一局里能看到不同档位，同时仍由 seed 决定，回放/联机不会漂移。
                // slot 参与计算，虫子换代时会得到新的外观。
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
                if (!baby.alive) continue;
                view.transform.position = baby.position + Vector3.up * (groundOffset + baby.height);
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
                if (!egg.alive) continue;
                view.transform.position = egg.position + Vector3.up * groundOffset;
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
                if (!pickup.alive) continue;
                view.transform.position = pickup.position + Vector3.up * groundOffset;
                view.transform.localScale = Vector3.one;
                Tint(view, PickupColor(pickup.kind));
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
            if (nestView == null) nestView = CreateView(nestPrefab, nestRoot, "NestView");
            if (nestView == null) return;
            nestView.SetActive(true);
            nestView.transform.position = state.nest.position + Vector3.up * 0.15f;
            nestView.transform.localScale = Vector3.one;
            float ratio = Mathf.Clamp01(state.nest.hp / Mathf.Max(1f, state.knobs.nestHP));
            Tint(nestView, Color.Lerp(new Color(0.9f, 0.18f, 0.12f), new Color(0.75f, 0.42f, 0.18f), ratio));
        }

        private GameObject PrefabForBug(int id)
        {
            if (id == 0 && qingTouPrefab != null) return qingTouPrefab;
            if (id == 1 && youHuluPrefab != null) return youHuluPrefab;
            if (id % 2 == 0 && qingTouPrefab != null) return qingTouPrefab;
            if (youHuluPrefab != null) return youHuluPrefab;
            return bugPrefab;
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
            DouQuquCricketVisual cricket = view.GetComponent<DouQuquCricketVisual>();
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
                    float cap = DouQuquRules.EffectiveChargeTime(knobs, bug);
                    float speed = DouQuquRules.JumpDeltaV(knobs, bug);
                    float fill = cap > 0.0001f ? Mathf.Clamp01(bug.chargeTime / cap) : 0f;
                    PlaceChargeArrow(bug.id, bug.charging, speed, fill, knobs, bug.chargeDirection, bug.position, bug.radius, bug.id == 0);
                    if (bug.charging) seenIds.Add(bug.id);
                }
            }
            for (int i = 0; i < state.babies.Count; i++)
            {
                BabyState baby = state.babies[i];
                if (baby == null || !baby.alive) continue;
                float cap = DouQuquRules.BabyChargeTime(knobs);
                float speed = DouQuquRules.BabyChargeSpeed(knobs, baby);
                float fill = cap > 0.0001f ? Mathf.Clamp01(baby.chargeTime / cap) : 0f;
                PlaceChargeArrow(baby.id, baby.charging, speed, fill, knobs, baby.chargeDirection, baby.position, baby.radius, baby.ownerId == 0);
                if (baby.charging) seenIds.Add(baby.id);
            }
            foreach (KeyValuePair<int, DouQuquChargeArrow> pair in chargeArrows)
                if (!seenIds.Contains(pair.Key) && pair.Value != null) pair.Value.Hide();
        }

        private void PlaceChargeArrow(int id, bool charging, float speed, float fill, MatchKnobs knobs, Vector2 direction, Vector3 position, float radius, bool ally)
        {
            if (!charging)
            {
                DouQuquChargeArrow existing;
                if (chargeArrows.TryGetValue(id, out existing) && existing != null) existing.Hide();
                return;
            }
            DouQuquChargeArrow arrow = GetChargeArrow(id);
            if (arrow == null) return;
            float dist = DouQuquRules.JumpRange(knobs, speed);
            arrow.Apply(true, dist, fill, direction, position + Vector3.up * 0.08f, radius, ally);
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
                    PlaceGroundMarker(bug.id, bug.position, bug.radius, bug.height, bug.charging, DouQuquGroundMarker.ColorForPlayer(bug.id));
                }
            }
            for (int i = 0; i < state.babies.Count; i++)
            {
                BabyState baby = state.babies[i];
                if (baby == null || !baby.alive) continue;
                int markerId = 1000 + baby.id;
                seenIds.Add(markerId);
                PlaceGroundMarker(markerId, baby.position, baby.radius, baby.height, baby.charging, DouQuquGroundMarker.ColorForPlayer(baby.ownerId));
            }
            foreach (KeyValuePair<int, DouQuquGroundMarker> pair in groundMarkers)
                if (!seenIds.Contains(pair.Key) && pair.Value != null) pair.Value.Hide();
        }

        private void PlaceGroundMarker(int id, Vector3 position, float radius, float height, bool charging, Color playerColor)
        {
            DouQuquGroundMarker marker = GetGroundMarker(id);
            if (marker == null) return;
            marker.Apply(position, radius, playerColor, height, charging);
        }

        private void RefreshStaminaOverlays(MatchState state)
        {
            WarnIfOverlaysMissing();
            foreach (KeyValuePair<int, DouQuquStaminaRing> pair in staminaRings)
                if (pair.Value != null) pair.Value.Hide();
            foreach (KeyValuePair<int, DouQuquStaminaBar> pair in staminaBars)
                if (pair.Value != null) pair.Value.Hide();
        }

        private void PlaceStaminaBar(BugState bug, MatchKnobs knobs)
        {
            DouQuquStaminaBar bar = GetStaminaBar(bug.id);
            if (bar == null) return;
            float max = Mathf.Max(1f, knobs.staminaMax);
            int slots = Mathf.Clamp(knobs.staminaSlots, 3, DouQuquStaminaBar.MaxSlots);
            float current = Mathf.Max(0f, bug.stamina);
            float pending = bug.charging ? DouQuquRules.JumpStaminaCost(knobs, bug) : 0f;
            bar.Apply(current / max, slots, bug.position + Vector3.up * bug.height, bug.radius, pending / max);
        }

        private DouQuquGroundMarker GetGroundMarker(int id)
        {
            DouQuquGroundMarker marker;
            if (groundMarkers.TryGetValue(id, out marker) && marker != null) return marker;
            Transform parent = markersRoot != null ? markersRoot : transform;
            GameObject view = new GameObject("GroundMarker_" + id);
            view.transform.SetParent(parent, false);
            marker = view.AddComponent<DouQuquGroundMarker>();
            groundMarkers[id] = marker;
            return marker;
        }

        private DouQuquStaminaBar GetStaminaBar(int id)
        {
            DouQuquStaminaBar bar;
            if (staminaBars.TryGetValue(id, out bar) && bar != null) return bar;
            bar = InstantiateOverlay<DouQuquStaminaBar>(staminaBarPrefab, barsRoot, "StaminaBar_" + id);
            if (bar != null) staminaBars[id] = bar;
            return bar;
        }

        private DouQuquChargeArrow GetChargeArrow(int id)
        {
            DouQuquChargeArrow arrow;
            if (chargeArrows.TryGetValue(id, out arrow) && arrow != null) return arrow;
            arrow = InstantiateOverlay<DouQuquChargeArrow>(chargeArrowPrefab, arrowsRoot, "ChargeArrow_" + id);
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
            if (staminaBarPrefab != null && chargeArrowPrefab != null) return;
            warnedMissingOverlays = true;
            Debug.LogWarning("[DouQuqu] 缺少耐力条或蓄力箭头预制体，请在菜单运行 DouQuqu/Rebuild Overlay Prefabs。");
        }

        /// <summary>
        /// 顶视朝向。精品立绘的图片上部是头：Sprite 本地 +Y 对准蓄力/飞行方向，
        /// 贴图正面朝上对着顶视相机。不用 Euler(90, yaw, 0)，避免 X=90 万向节锁把偏航吃掉。
        /// </summary>
        private static void FaceXz(GameObject view, Vector3 velocity, Vector2 chargeDirection)
        {
            Vector2 face = new Vector2(velocity.x, velocity.z);
            if (face.sqrMagnitude < 0.04f) face = chargeDirection;
            if (face.sqrMagnitude < 0.0001f) face = Vector2.up;
            Vector3 head = new Vector3(face.x, 0f, face.y);
            view.transform.rotation = Quaternion.LookRotation(Vector3.up, head);
        }

        private GameObject PrefabForPickup(string kind)
        {
            if (kind == "size") return sizePrefab;
            if (kind == "shield") return shieldPrefab;
            if (kind == "charge") return chargePrefab;
            return heartPrefab;
        }

        private static Color PickupColor(string kind)
        {
            if (kind == "size") return new Color(1f, 0.38f, 0.9f);
            if (kind == "shield") return new Color(0.25f, 0.85f, 1f);
            if (kind == "charge") return new Color(1f, 0.58f, 0.12f);
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
                if (propertyBlock == null) propertyBlock = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(propertyBlock);
                propertyBlock.SetColor("_BaseColor", color);
                propertyBlock.SetColor("_Color", color);
                renderer.SetPropertyBlock(propertyBlock);
            }
        }
    }
}
