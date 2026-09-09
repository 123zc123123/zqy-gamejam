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
        private readonly Dictionary<int, int> assignedBugProfiles = new Dictionary<int, int>();
        private Sprite[] premiumBugSprites;
        private MatchState assignedProfileState;
        private int assignedProfileSeed = int.MinValue;
        private bool warnedMissingOverlays;

        private static readonly Color[] PlayerColors = GroundMarker.PlayerColors;

        private void Awake()
        {
            propertyBlock = new MaterialPropertyBlock();
            if (match == null) match = GetComponent<MatchController>();
            if (match == null) match = FindObjectOfType<MatchController>();
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
            if (main.GetComponent<BattleCamera>() == null)
                main.gameObject.AddComponent<BattleCamera>();
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
                match.Configure(MatchRunMode.Offline, Mathf.Clamp(playerCount, 1, MatchController.MaxPlayers));
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
        public void BindMatch(MatchController controller)
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
                if (unit != null) unit.Bind();
                GameObject body = BodyOf(view);
                if (body == null) continue;
                if (!assignedBugProfiles.TryGetValue(bug.id, out assignedProfile) || assignedProfile != profile)
                {
                    CricketVisual skeletal = body.GetComponent<CricketVisual>();
                    if (skeletal != null)
                    {
                        int quality = profile / 4 + 1;
                        int temperament = profile % 4 + 1;
                        skeletal.ApplySkin(CricketVisual.SkinLabel(quality, temperament));
                    }
                    else
                    {
                        ApplyPremiumBugSprite(body, profile);
                    }
                    assignedBugProfiles[bug.id] = profile;
                }
                view.SetActive(bug.alive);
                if (!bug.alive) continue;
                if (unit != null)
                {
                    view.transform.position = new Vector3(bug.position.x, 0f, bug.position.z);
                    view.transform.rotation = Quaternion.identity;
                    view.transform.localScale = Vector3.one;
                    float grow = bug.radius / Mathf.Max(0.01f, state.knobs.bugR);
                    unit.ApplyMotion(bug.height, grow);
                }
                else
                {
                    float visualScale = VisualScale(body, bug.radius, state.knobs.bugR);
                    view.transform.position = bug.position + Vector3.up * (groundOffset + bug.height);
                    view.transform.localScale = Vector3.one * visualScale;
                }
                // 蓄力中跟摇杆（图片上部=头）；飞行中跟速度。空中不改朝向。
                CricketVisual cricket = body.GetComponent<CricketVisual>();
                FaceXz(body, bug.charging ? Vector3.zero : bug.velocity, bug.chargeDirection);
                if (unit != null) unit.AlignMarkerToBody();
                if (cricket != null)
                {
                    cricket.ApplyTeam(bug.id == 0, bug.charging);
                    CricketAnim anim = body.GetComponent<CricketAnim>();
                    if (anim == null) anim = body.GetComponentInChildren<CricketAnim>(true);
                    if (anim == null) anim = body.AddComponent<CricketAnim>();
                    anim.Apply(bug);
                }
                else if (tintPlayers)
                {
                    Color tint = PlayerColors[Mathf.Abs(bug.id) % PlayerColors.Length];
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
                view.transform.localScale = Vector3.one * 2f;
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
            nestView.transform.localScale = Vector3.one * 2f;
            Tint(nestView, Color.white);
            RefreshNestHits(nestView, Mathf.Max(0, Mathf.CeilToInt(state.nest.hp)));
        }

        private void RefreshNestHits(GameObject nestView, int hits)
        {
            Transform badge = nestView.transform.Find("HitsBadge");
            if (badge == null)
            {
                GameObject go = new GameObject("HitsBadge");
                go.transform.SetParent(nestView.transform, false);
                go.transform.localPosition = new Vector3(0.42f, -0.38f, -0.02f);
                go.transform.localRotation = Quaternion.identity;
                go.transform.localScale = Vector3.one * 0.45f;
                SpriteRenderer icon = go.AddComponent<SpriteRenderer>();
                icon.sprite = Resources.Load<Sprite>("Battle/Entities/Textures/NestHits");
                icon.sortingOrder = 16;
                icon.color = Color.white;
                GameObject label = new GameObject("Hits");
                label.transform.SetParent(go.transform, false);
                label.transform.localPosition = Vector3.zero;
                label.transform.localRotation = Quaternion.identity;
                label.transform.localScale = Vector3.one;
                TextMesh text = label.AddComponent<TextMesh>();
                text.anchor = TextAnchor.MiddleCenter;
                text.alignment = TextAlignment.Center;
                text.characterSize = 0.18f;
                text.fontSize = 64;
                text.color = Color.white;
                text.fontStyle = FontStyle.Bold;
                MeshRenderer mesh = label.GetComponent<MeshRenderer>();
                if (mesh != null) mesh.sortingOrder = 17;
                badge = go.transform;
            }
            TextMesh hitsText = badge.GetComponentInChildren<TextMesh>();
            if (hitsText != null) hitsText.text = hits.ToString();
            badge.gameObject.SetActive(hits > 0);
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
                    float speed = Rules.JumpDeltaV(knobs, bug);
                    float fill = cap > 0.0001f ? Mathf.Clamp01(bug.chargeTime / cap) : 0f;
                    PlaceChargeArrow(bug.id, bug.charging, speed, fill, knobs, bug.chargeDirection, bug.position, bug.radius, GroundMarker.ColorForPlayer(bug.id));
                    if (bug.charging) seenIds.Add(bug.id);
                }
            }
            for (int i = 0; i < state.babies.Count; i++)
            {
                BabyState baby = state.babies[i];
                if (baby == null || !baby.alive) continue;
                float cap = Rules.BabyChargeTime(knobs);
                float speed = Rules.BabyChargeSpeed(knobs, baby);
                float fill = cap > 0.0001f ? Mathf.Clamp01(baby.chargeTime / cap) : 0f;
                PlaceChargeArrow(baby.id, baby.charging, speed, fill, knobs, baby.chargeDirection, baby.position, baby.radius, GroundMarker.ColorForPlayer(baby.ownerId));
                if (baby.charging) seenIds.Add(baby.id);
            }
            foreach (KeyValuePair<int, ChargeArrow> pair in chargeArrows)
                if (!seenIds.Contains(pair.Key) && pair.Value != null) pair.Value.Hide();
        }

        private void PlaceChargeArrow(int id, bool charging, float speed, float fill, MatchKnobs knobs, Vector2 direction, Vector3 position, float radius, Color playerColor)
        {
            if (!charging)
            {
                ChargeArrow existing;
                if (chargeArrows.TryGetValue(id, out existing) && existing != null) existing.Hide();
                return;
            }
            ChargeArrow arrow = GetChargeArrow(id);
            if (arrow == null) return;
            float dist = Rules.JumpRange(knobs, speed);
            arrow.Apply(true, dist, fill, direction, position + Vector3.up * 0.08f, radius, playerColor);
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
            if (BarOfUnit(bug.id) != null) bar.ApplyFill(ratio, slots, pendingRatio);
            else bar.Apply(ratio, slots, bug.position + Vector3.up * bug.height, bug.radius, pendingRatio);
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
