using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

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
        private GameObject nestView;
        private readonly Dictionary<int, LineRenderer> chargeArrows = new Dictionary<int, LineRenderer>();
        private Material arrowMaterial;

        private static readonly Color[] PlayerColors =
        {
            new Color(0.18f, 0.75f, 1f),
            new Color(1f, 0.32f, 0.32f),
            new Color(0.35f, 1f, 0.42f),
            new Color(1f, 0.78f, 0.18f)
        };

        private void Awake()
        {
            propertyBlock = new MaterialPropertyBlock();
            if (match == null) match = GetComponent<DouQuquMatchController>();
            if (match == null) match = FindObjectOfType<DouQuquMatchController>();
            EnsureBattleCamera();
            EnsureRoots();
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

        /// <summary>创建运行时容器，保证表现对象不会散落在场景根节点。</summary>
        private void EnsureRoots()
        {
            bugsRoot = CreateRoot("Bugs");
            babiesRoot = CreateRoot("Babies");
            eggsRoot = CreateRoot("Eggs");
            pickupsRoot = CreateRoot("Pickups");
            nestRoot = CreateRoot("Nest");
            arrowsRoot = CreateRoot("ChargeArrows");
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
        }

        private void RefreshBugs(MatchState state)
        {
            seenIds.Clear();
            if (state.bugs == null) return;
            for (int i = 0; i < state.bugs.Length; i++)
            {
                BugState bug = state.bugs[i];
                if (bug == null) continue;
                seenIds.Add(bug.id);
                GameObject view = GetOrCreate(bugViews, bug.id, PrefabForBug(bug.id), bugsRoot, "Bug_" + bug.id);
                if (view == null) continue;
                view.SetActive(bug.alive);
                if (!bug.alive) continue;
                view.transform.position = bug.position + Vector3.up * (groundOffset + bug.height);
                view.transform.localScale = Vector3.one * VisualScale(view, bug.radius, state.knobs.bugR);
                FaceXz(view, bug.charging ? Vector3.zero : bug.velocity, bug.chargeDirection);
                if (tintPlayers)
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
            seenIds.Clear();
            MatchKnobs knobs = state.knobs;
            if (state.bugs != null)
            {
                for (int i = 0; i < state.bugs.Length; i++)
                {
                    BugState bug = state.bugs[i];
                    if (bug == null || !bug.alive) continue;
                    float cap = DouQuquRules.EffectiveChargeTime(knobs, bug);
                    float speed = DouQuquRules.EffectiveChargeSpeed(knobs, bug) * bug.chargeTime;
                    float fill = cap > 0.0001f ? Mathf.Clamp01(bug.chargeTime / cap) : 0f;
                    PlaceChargeArrow(bug.id, bug.charging, speed, fill, knobs, bug.chargeDirection, bug.position, bug.radius);
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
                PlaceChargeArrow(baby.id, baby.charging, speed, fill, knobs, baby.chargeDirection, baby.position, baby.radius);
                if (baby.charging) seenIds.Add(baby.id);
            }
            foreach (KeyValuePair<int, LineRenderer> pair in chargeArrows)
                if (!seenIds.Contains(pair.Key) && pair.Value != null) pair.Value.enabled = false;
        }

        private void PlaceChargeArrow(int id, bool charging, float speed, float fill, MatchKnobs knobs, Vector2 direction, Vector3 position, float radius)
        {
            LineRenderer line = GetChargeArrow(id);
            float dist = DouQuquRules.JumpRange(knobs, speed);
            if (!charging || dist < 0.35f || direction.sqrMagnitude < 0.0001f)
            {
                line.enabled = false;
                return;
            }
            Vector2 dir = direction.normalized;
            Vector2 perp = new Vector2(-dir.y, dir.x);
            float head = Mathf.Min(1.35f, dist * 0.2f);
            float width = Mathf.Max(0.28f, radius * 0.55f);
            Vector3 origin = position + Vector3.up * 0.08f;
            Vector3 tip = origin + new Vector3(dir.x, 0f, dir.y) * dist;
            Vector3 neck = tip - new Vector3(dir.x, 0f, dir.y) * head;
            Vector3 left = neck + new Vector3(perp.x, 0f, perp.y) * width;
            Vector3 right = neck - new Vector3(perp.x, 0f, perp.y) * width;
            line.enabled = true;
            line.positionCount = 5;
            line.SetPosition(0, origin);
            line.SetPosition(1, tip);
            line.SetPosition(2, left);
            line.SetPosition(3, tip);
            line.SetPosition(4, right);
            float shaft = Mathf.Lerp(0.12f, 0.28f, fill);
            line.startWidth = shaft;
            line.endWidth = shaft;
            Color color = fill >= 0.98f
                ? new Color(0.98f, 0.86f, 0.45f, 0.95f)
                : Color.Lerp(new Color(0.77f, 0.65f, 0.45f, 0.45f), new Color(0.94f, 0.90f, 0.72f, 0.88f), fill);
            line.startColor = color;
            line.endColor = color;
        }

        private LineRenderer GetChargeArrow(int id)
        {
            LineRenderer line;
            if (chargeArrows.TryGetValue(id, out line) && line != null) return line;
            GameObject go = new GameObject("ChargeArrow_" + id);
            go.transform.SetParent(arrowsRoot, false);
            line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 5;
            line.numCapVertices = 3;
            line.numCornerVertices = 2;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.alignment = LineAlignment.View;
            if (arrowMaterial == null)
            {
                Shader shader = Shader.Find("Sprites/Default");
                if (shader == null) shader = Shader.Find("Unlit/Color");
                arrowMaterial = new Material(shader);
            }
            line.sharedMaterial = arrowMaterial;
            chargeArrows[id] = line;
            return line;
        }

        private static void FaceXz(GameObject view, Vector3 velocity, Vector2 chargeDirection)
        {
            Vector2 face = new Vector2(velocity.x, velocity.z);
            if (face.sqrMagnitude < 0.04f) face = chargeDirection;
            if (face.sqrMagnitude < 0.0001f) face = Vector2.up;
            float yaw = Mathf.Atan2(face.x, face.y) * Mathf.Rad2Deg + 180f;
            view.transform.rotation = Quaternion.Euler(90f, yaw, 0f);
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
