using System.Collections.Generic;
using UnityEngine;

namespace DouQuqu
{
    /// <summary>
    /// 局内卡通特效。一次性事件走 Resources/Battle/Fx，护盾循环挂在虫身上。
    /// 缺预制体时静默跳过，不挡战斗。
    /// </summary>
    public sealed class BattleFx : MonoBehaviour
    {
        const string ResourceFolder = "Battle/Fx/";
        const float FxHeight = 0.55f;

        [SerializeField] private MatchController match;
        [SerializeField] private float hitScale = 3.2f;
        [SerializeField] private float burstScale = 3.6f;
        [SerializeField] private float loopScale = 2.4f;

        private Transform fxRoot;
        private readonly Dictionary<string, GameObject> cache = new Dictionary<string, GameObject>();
        private readonly Dictionary<int, GameObject> shieldLoops = new Dictionary<int, GameObject>();
        private float lastHitAt = -1f;

        public void Bind(MatchController controller)
        {
            if (match == controller) return;
            if (isActiveAndEnabled && match != null) match.GameplayEvent -= OnGameplayEvent;
            match = controller;
            if (isActiveAndEnabled && match != null) match.GameplayEvent += OnGameplayEvent;
        }

        private void Awake()
        {
            if (match == null) match = GetComponent<MatchController>();
            if (match == null) match = FindObjectOfType<MatchController>();
            EnsureRoot();
        }

        private void OnEnable()
        {
            if (match != null) match.GameplayEvent += OnGameplayEvent;
        }

        private void OnDisable()
        {
            if (match != null) match.GameplayEvent -= OnGameplayEvent;
            ClearLoops();
        }

        private void LateUpdate()
        {
            SyncShieldLoops();
        }

        private void OnGameplayEvent(string kind, Vector3 position)
        {
            if (string.IsNullOrEmpty(kind)) return;
            Vector3 at = position + Vector3.up * FxHeight;
            switch (kind)
            {
                case "hit":
                case "baby-hit":
                    if (Time.unscaledTime - lastHitAt < 0.05f) return;
                    lastHitAt = Time.unscaledTime;
                    Play("hit", at, kind == "baby-hit" ? hitScale * 0.7f : hitScale, 2f);
                    break;
                case "out":
                case "kill":
                case "player-out":
                case "baby-out":
                case "baby-dead":
                    Play("dust", at, burstScale, 2.4f);
                    break;
                case "shield-save":
                    Play("shieldBurst", at, burstScale, 2.4f);
                    break;
                case "pickup:heart":
                    Play("heart", at, burstScale * 0.85f, 2.2f);
                    break;
                case "pickup:shield":
                case "item-spawn:shield":
                    Play("sparkle", at, burstScale * 0.8f, 2.2f);
                    break;
                case "grow":
                    Play("buff", at, burstScale, 2.4f);
                    break;
                case "nest-hit":
                    Play("nestHit", at, hitScale, 2f);
                    break;
                case "nest-break":
                    Play("nestBreak", at, burstScale * 1.2f, 2.8f);
                    break;
                case "egg-hatch":
                case "cricket-in":
                    Play("hatch", at, hitScale, 2.2f);
                    break;
                case "rage-start":
                    Play("rage", Vector3.up * FxHeight, burstScale * 1.4f, 3.2f);
                    break;
                case "revive":
                    Play("reviveBurst", at, burstScale * 1.3f, 3.4f);
                    Play("hatch", at, hitScale * 1.1f, 2.8f);
                    Play("revive", at, burstScale * 1.2f, 3.6f);
                    break;
                case "match-over":
                    Play("confetti", Vector3.up * 1.2f, burstScale * 1.2f, 4f);
                    break;
                default:
                    HandleStealPopup(kind, position);
                    break;
            }
        }

        private void HandleStealPopup(string kind, Vector3 world)
        {
            if (!string.IsNullOrEmpty(kind) && kind.StartsWith("steal-gain:"))
            {
                Play("heart", world + Vector3.up * FxHeight, burstScale * 0.7f, 1.2f);
                return;
            }
            if (!string.IsNullOrEmpty(kind) && kind.StartsWith("steal-loss:"))
                return;
            if (kind == "steal")
                Play("heart", world + Vector3.up * FxHeight, burstScale * 0.7f, 1.2f);
        }

        private void SpawnDelta(Vector3 position, string text, Color color)
        {
            EnsureRoot();
            Vector3 at = new Vector3(position.x, 0.35f, position.z);
            GameObject go = new GameObject("StaminaDelta");
            go.transform.SetParent(fxRoot, false);
            go.transform.position = at;
            go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            Font font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            MakeDeltaMesh(go.transform, text, color, 0f, font);
            MakeDeltaMesh(go.transform, text, new Color(0f, 0f, 0f, 0.85f), 0.04f, font);
            go.AddComponent<StaminaDeltaDrift>().Begin(1.35f);
        }

        static void MakeDeltaMesh(Transform parent, string text, Color color, float zBias, Font font)
        {
            GameObject go = new GameObject(zBias > 0f ? "Outline" : "Fill");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, 0f, zBias);
            go.transform.localRotation = Quaternion.identity;
            TextMesh mesh = go.AddComponent<TextMesh>();
            mesh.text = text;
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.characterSize = 0.14f;
            mesh.fontSize = 96;
            mesh.fontStyle = FontStyle.Bold;
            mesh.color = color;
            if (font != null) mesh.font = font;
            MeshRenderer renderer = go.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.sortingOrder = zBias > 0f ? 240 : 241;
                if (font != null && font.material != null) renderer.sharedMaterial = font.material;
            }
        }

        private void SyncShieldLoops()
        {
            MatchState state = match != null ? match.State : null;
            HashSet<int> live = null;
            if (state != null && state.bugs != null)
            {
                live = new HashSet<int>();
                for (int i = 0; i < state.bugs.Length; i++)
                {
                    BugState bug = state.bugs[i];
                    if (bug == null || !bug.alive || !Rules.ShieldActive(bug)) continue;
                    live.Add(bug.id);
                    GameObject loop = GetOrCreateLoop(bug.id);
                    if (loop == null) continue;
                    loop.SetActive(true);
                    loop.transform.position = bug.position + Vector3.up * FxHeight;
                    loop.transform.rotation = Quaternion.identity;
                }
            }

            List<int> stale = null;
            foreach (KeyValuePair<int, GameObject> pair in shieldLoops)
            {
                if (live != null && live.Contains(pair.Key)) continue;
                if (stale == null) stale = new List<int>();
                stale.Add(pair.Key);
            }
            if (stale == null) return;
            for (int i = 0; i < stale.Count; i++)
            {
                GameObject loop;
                if (!shieldLoops.TryGetValue(stale[i], out loop)) continue;
                if (loop != null) Destroy(loop);
                shieldLoops.Remove(stale[i]);
            }
        }

        private GameObject GetOrCreateLoop(int bugId)
        {
            GameObject existing;
            if (shieldLoops.TryGetValue(bugId, out existing) && existing != null) return existing;
            GameObject prefab = Load("shieldLoop");
            if (prefab == null) return null;
            GameObject loop = Instantiate(prefab, fxRoot);
            loop.name = "ShieldLoop_" + bugId;
            loop.transform.rotation = Quaternion.identity;
            loop.transform.localScale = Vector3.one * loopScale;
            shieldLoops[bugId] = loop;
            return loop;
        }

        static Material fallbackParticle;

        private void Play(string key, Vector3 position, float scale, float life)
        {
            GameObject prefab = Load(key);
            if (prefab == null)
            {
                Debug.LogWarning("[DouQuqu] 缺战斗特效 " + ResourceFolder + key);
                return;
            }
            EnsureRoot();
            GameObject instance = Instantiate(prefab, fxRoot);
            instance.name = key;
            instance.transform.position = position;
            instance.transform.localScale = Vector3.one * Mathf.Max(0.2f, scale);
            ParticleSystemRenderer[] renderers = instance.GetComponentsInChildren<ParticleSystemRenderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                renderers[i].sortingOrder = 420;
                renderers[i].allowOcclusionWhenDynamic = false;
                if (renderers[i].sharedMaterial == null || renderers[i].sharedMaterial.shader == null)
                    renderers[i].sharedMaterial = FallbackParticle();
            }
            ParticleSystem[] systems = instance.GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < systems.Length; i++)
            {
                ParticleSystem.MainModule main = systems[i].main;
                main.simulationSpeed = 6f;
                main.scalingMode = ParticleSystemScalingMode.Hierarchy;
                systems[i].Play(true);
            }
            Destroy(instance, Mathf.Max(0.12f, life / 6f));
        }

        static Material FallbackParticle()
        {
            if (fallbackParticle != null) return fallbackParticle;
            Shader shader = Shader.Find("Particles/Standard Unlit");
            if (shader == null) shader = Shader.Find("Legacy Shaders/Particles/Additive");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            fallbackParticle = shader != null ? new Material(shader) : null;
            if (fallbackParticle != null) fallbackParticle.color = new Color(1f, 0.85f, 0.2f, 1f);
            return fallbackParticle;
        }

        private GameObject Load(string key)
        {
            GameObject prefab;
            if (cache.TryGetValue(key, out prefab)) return prefab;
            prefab = Resources.Load<GameObject>(ResourceFolder + key);
            cache[key] = prefab;
            return prefab;
        }

        private void EnsureRoot()
        {
            if (fxRoot != null) return;
            Transform existing = transform.Find("BattleFx");
            if (existing != null)
            {
                fxRoot = existing;
                return;
            }
            GameObject root = new GameObject("BattleFx");
            root.transform.SetParent(transform, false);
            fxRoot = root.transform;
        }

        private void ClearLoops()
        {
            foreach (KeyValuePair<int, GameObject> pair in shieldLoops)
                if (pair.Value != null) Destroy(pair.Value);
            shieldLoops.Clear();
        }
    }

    sealed class StaminaDeltaDrift : MonoBehaviour
    {
        float life = 1f;
        float age;
        Vector3 start;

        public void Begin(float duration)
        {
            life = Mathf.Max(0.1f, duration);
            start = transform.position;
        }

        private void Update()
        {
            age += Time.deltaTime;
            float t = Mathf.Clamp01(age / life);
            Vector3 drift = Vector3.forward;
            Camera cam = Camera.main;
            if (cam != null)
            {
                drift = Vector3.ProjectOnPlane(cam.transform.up, Vector3.up);
                if (drift.sqrMagnitude < 0.01f) drift = Vector3.forward;
                else drift.Normalize();
            }
            transform.position = start + drift * (2.6f * t);
            transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            TextMesh[] meshes = GetComponentsInChildren<TextMesh>();
            for (int i = 0; i < meshes.Length; i++)
            {
                Color color = meshes[i].color;
                color.a = (1f - t) * (meshes[i].name == "Outline" ? 0.85f : 1f);
                meshes[i].color = color;
            }
            if (t >= 1f) Destroy(gameObject);
        }
    }
}
