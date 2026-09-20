using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DouQuqu
{
    /// <summary>第一场训练营七课：跳、出圈、吃饲料、吃护盾、撞房子、落点完美击、撞人得分。</summary>
    public static class TutorialBattleDirector
    {
        const int DummyId = 1;
        const float MarkerPulse = 1.6f;
        public const float CloseupHatchSeconds = 2.4f;
        public static bool LessonsActive { get; private set; }

        public static IEnumerator Play(MatchController match, int localId, Transform hudRoot)
        {
            if (match == null || !TutorialDirector.NeedsBattleLesson) yield break;

            LessonsActive = true;
            DialogueCatalog.Reload();
            match.SetTutorialFreeze(true);
            match.ClearPickups();
            for (int i = 0; i < MatchController.MaxPlayers; i++)
            {
                if (i == localId) continue;
                match.SetPlayerIdle(i, true);
            }

            yield return LessonJump(match, localId, hudRoot);
            if (!Failed(match, localId)) yield return LessonBound(match, localId, hudRoot);
            if (!Failed(match, localId)) yield return LessonPickup(match, localId, "heart", TutorialDirector.IdBattleHeart);
            if (!Failed(match, localId)) yield return LessonPickup(match, localId, "shield", TutorialDirector.IdBattleShield);
            if (!Failed(match, localId)) yield return LessonNest(match, localId);
            if (!Failed(match, localId)) yield return LessonSweet(match, localId);
            if (!Failed(match, localId)) yield return LessonKill(match, localId);

            TutorialFingerHint.Hide();
            HideMarker();
            PulseOutline(hudRoot, false);
            match.SetTutorialFreeze(false);
            for (int i = 0; i < MatchController.MaxPlayers; i++)
            {
                if (i == localId) continue;
                match.SetPlayerIdle(i, false);
            }
            LessonsActive = false;
        }

        static bool Failed(MatchController match, int localId)
        {
            return match == null || match.IsOver || !match.PlayerStillIn(localId);
        }

        static IEnumerator LessonJump(MatchController match, int localId, Transform hudRoot)
        {
            InputDirectionSettings.Load();
            string id = InputDirectionSettings.ReverseDrag
                ? TutorialDirector.IdBattleJump
                : TutorialDirector.IdBattleJumpSame;
            bool done = false;
            DialogueBoxView.Play(id, () => done = true);
            while (!done && !Failed(match, localId)) yield return null;
            TutorialFingerHint.Show(hudRoot);
            while (!Failed(match, localId) && !PlayerJumped(match, localId)) yield return null;
            TutorialFingerHint.Hide();
        }

        static IEnumerator LessonBound(MatchController match, int localId, Transform hudRoot)
        {
            bool done = false;
            DialogueBoxView.Play(TutorialDirector.IdBattleBound, () => done = true);
            while (!done && !Failed(match, localId))
            {
                PulseOutline(hudRoot, true);
                yield return null;
            }
            PulseOutline(hudRoot, false);
        }

        static IEnumerator LessonPickup(MatchController match, int localId, string kind, string dialogueId)
        {
            yield return WaitUntilLanded(match, localId);
            match.ClearPickups();
            Vector3 at = PlaceAway(match, localId, 8.5f);
            match.SpawnTutorialPickup(kind, at);
            ShowMarker(at);
            yield return null;
            if (!PickupAlive(match, kind))
            {
                at = PlaceAway(match, localId, 10.5f);
                match.SpawnTutorialPickup(kind, at);
                ShowMarker(at);
            }
            bool done = false;
            DialogueBoxView.Play(dialogueId, () => done = true);
            while (!done && !Failed(match, localId)) yield return null;
            while (!Failed(match, localId) && PickupAlive(match, kind)) yield return null;
            HideMarker();
            yield return WaitUntilLanded(match, localId);
        }

        static IEnumerator LessonNest(MatchController match, int localId)
        {
            yield return WaitUntilLanded(match, localId);
            match.ClearPickups();
            Vector3 at = PlaceAway(match, localId, 9f);
            match.SpawnTutorialNest(at, TutorialDirector.NestHp);
            ShowMarker(at);
            bool done = false;
            DialogueBoxView.Play(TutorialDirector.IdBattleNest, () => done = true);
            while (!done && !Failed(match, localId)) yield return null;
            while (!Failed(match, localId) && match.Nest != null && match.Nest.alive) yield return null;
            HideMarker();
            yield return WatchHatch(match, localId, at);
            yield return WaitUntilLanded(match, localId);
        }

        static IEnumerator WatchHatch(MatchController match, int localId, Vector3 nestAt)
        {
            float waitEggs = 0f;
            while (!Failed(match, localId) && waitEggs < 1.2f && !HasLiveEgg(match) && !HasLiveBaby(match))
            {
                waitEggs += Time.unscaledDeltaTime;
                yield return null;
            }

            BattleCamera cam = Object.FindObjectOfType<BattleCamera>();
            Vector3 look = WatchPoint(match.Eggs, match.Babies, nestAt);
            if (cam != null) cam.LookAtPoint(look, TutorialDirector.NestLookSeconds);
            float lookT = 0f;
            while (!Failed(match, localId) && lookT < TutorialDirector.NestLookSeconds)
            {
                look = WatchPoint(match.Eggs, match.Babies, look);
                lookT += Time.unscaledDeltaTime;
                yield return null;
            }

            EggState focusEgg = PickNearestEgg(match.Eggs, look);
            if (focusEgg != null)
            {
                focusEgg.velocity = Vector3.zero;
                look = focusEgg.position;
                look.y = 0f;
                DriveCloseupHatch(focusEgg, match.Elapsed, 0f, CloseupHatchSeconds);
            }
            match.SetTutorialHoldClock(false);
            float waited = 0f;
            while (!Failed(match, localId) && waited < TutorialDirector.NestHatchWaitSeconds
                && focusEgg != null && focusEgg.alive)
            {
                look = focusEgg.position;
                look.y = 0f;
                if (cam != null) cam.LookAtPoint(look, 0f);
                DriveCloseupHatch(focusEgg, match.Elapsed, waited, CloseupHatchSeconds);
                if (CloseupHatchDue(waited, CloseupHatchSeconds))
                    match.ForceHatchEgg(focusEgg);
                waited += Time.unscaledDeltaTime;
                yield return null;
            }

            look = WatchPoint(null, match.Babies, look);
            if (cam != null) cam.LookAtPoint(look, 0f);
            match.SetTutorialHoldClock(true);

            bool talkDone = false;
            DialogueBoxView.Play(TutorialDirector.IdBattleNestHatch, () => talkDone = true);
            while (!talkDone && !Failed(match, localId))
            {
                look = WatchPoint(null, match.Babies, look);
                if (cam != null) cam.LookAtPoint(look, 0f);
                yield return null;
            }

            match.SetTutorialHoldClock(true);
            if (cam != null) cam.FollowLocalPlayer(match, localId);
            float back = 0f;
            while (!Failed(match, localId) && back < TutorialDirector.NestLookBackSeconds)
            {
                back += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        static IEnumerator LessonSweet(MatchController match, int localId)
        {
            yield return WaitUntilLanded(match, localId);
            Vector3 at = PlaceAway(match, localId, 8.5f);
            match.RestoreTutorialDummy(DummyId, at);
            ShowMarker(at);
            bool sweet = false;
            System.Action<string, Vector3> onEvent = (kind, _) =>
            {
                if (HitPairInvolves(kind, "perfect-ids:", localId, DummyId)) sweet = true;
            };
            match.GameplayEvent += onEvent;
            try
            {
                bool done = false;
                DialogueBoxView.Play(TutorialDirector.IdBattleSweet, () => done = true);
                while (!done && !Failed(match, localId)) yield return null;
                while (!Failed(match, localId) && !sweet)
                {
                    BugState dummy = Bug(match, DummyId);
                    if (dummy != null && dummy.alive)
                    {
                        Vector3 mark = dummy.position;
                        mark.y = 0f;
                        ShowMarker(mark);
                    }
                    else if (!match.PlayerStillIn(DummyId) && PlayerGrounded(match, localId))
                    {
                        bool hintDone = false;
                        DialogueBoxView.Play(TutorialDirector.IdBattleSweetMiss, () => hintDone = true);
                        while (!hintDone && !Failed(match, localId) && !sweet) yield return null;
                        if (Failed(match, localId) || sweet) continue;
                        match.RestoreTutorialDummy(DummyId, at);
                        ShowMarker(at);
                    }
                    yield return null;
                }
            }
            finally
            {
                match.GameplayEvent -= onEvent;
            }
            HideMarker();
            yield return WaitUntilLanded(match, localId);
        }

        static IEnumerator LessonKill(MatchController match, int localId)
        {
            yield return WaitUntilLanded(match, localId);
            int before = match.MatchScore(localId);
            Vector3 at = match.PointOutward(localId, 5.5f);
            match.RestoreTutorialDummy(DummyId, at);
            ShowMarker(at);
            bool done = false;
            DialogueBoxView.Play(TutorialDirector.IdBattleKill, () => done = true);
            while (!done && !Failed(match, localId)) yield return null;
            while (!Failed(match, localId) && match.MatchScore(localId) <= before && match.PlayerStillIn(DummyId))
                yield return null;
            HideMarker();
        }

        public static bool HitPairInvolves(string kind, string prefix, int a, int b)
        {
            if (string.IsNullOrEmpty(kind) || string.IsNullOrEmpty(prefix) || !kind.StartsWith(prefix))
                return false;
            string rest = kind.Substring(prefix.Length);
            int split = rest.IndexOf(':');
            if (split <= 0) return false;
            int x;
            int y;
            if (!int.TryParse(rest.Substring(0, split), out x)) return false;
            if (!int.TryParse(rest.Substring(split + 1), out y)) return false;
            return (x == a && y == b) || (x == b && y == a);
        }

        static bool PlayerJumped(MatchController match, int localId)
        {
            BugState bug = Bug(match, localId);
            return bug != null && bug.alive && bug.airborne && bug.height > 0.15f;
        }

        static bool PlayerGrounded(MatchController match, int localId)
        {
            BugState bug = Bug(match, localId);
            return bug != null && bug.alive && !bug.airborne && bug.height <= 0.08f;
        }

        static IEnumerator WaitUntilLanded(MatchController match, int localId)
        {
            while (!Failed(match, localId) && !PlayerGrounded(match, localId))
                yield return null;
            float settled = 0f;
            while (!Failed(match, localId) && settled < 0.28f)
            {
                if (!PlayerGrounded(match, localId))
                {
                    settled = 0f;
                    yield return null;
                    continue;
                }
                settled += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        static Vector3 PlaceAway(MatchController match, int localId, float distance)
        {
            BugState bug = Bug(match, localId);
            if (bug == null) return match.PointInward(localId, distance);
            Vector3 pos = bug.position;
            Vector3 inward = new Vector3(-pos.x, 0f, -pos.z);
            if (inward.sqrMagnitude < 0.01f) inward = Vector3.forward;
            inward.Normalize();
            Vector3 side = Vector3.Cross(Vector3.up, inward);
            float minGap = bug.radius + 5.5f;
            Vector3[] tries =
            {
                pos + side * distance,
                pos - side * distance,
                pos + inward * distance,
                pos + side * (distance + 2.5f),
                pos - side * (distance + 2.5f),
                pos + inward * (distance + 3f)
            };
            for (int i = 0; i < tries.Length; i++)
            {
                Vector3 at = Rules.ClampInsideArena(tries[i], 2.4f);
                at.y = 0f;
                float gap = Vector2.Distance(new Vector2(at.x, at.z), new Vector2(pos.x, pos.z));
                if (gap >= minGap) return at;
            }
            return match.PointInward(localId, distance);
        }

        /// <summary>特写进度条跟真实秒走，不跟被 cap 的比赛时钟。left=0 时条空。</summary>
        public static void DriveCloseupHatch(EggState egg, float elapsed, float waited, float closeupHatch)
        {
            if (egg == null || !egg.alive) return;
            float duration = Mathf.Max(0.01f, closeupHatch);
            float left = Mathf.Max(0f, duration - Mathf.Max(0f, waited));
            egg.hatchDuration = duration;
            egg.hatchAt = elapsed + left;
        }

        public static bool CloseupHatchDue(float waited, float closeupHatch)
        {
            return waited + 1e-4f >= Mathf.Max(0.01f, closeupHatch);
        }

        public static EggState PickNearestEgg(IReadOnlyList<EggState> eggs, Vector3 anchor)
        {
            EggState best = null;
            float bestDist = float.PositiveInfinity;
            if (eggs == null) return null;
            for (int i = 0; i < eggs.Count; i++)
            {
                EggState egg = eggs[i];
                if (egg == null || !egg.alive) continue;
                float dist = (egg.position - anchor).sqrMagnitude;
                if (best != null && dist >= bestDist) continue;
                best = egg;
                bestDist = dist;
            }
            return best;
        }

        /// <summary>选一颗还活着的卵（没有卵就选宝宝），对准镜头中心。优先离锚点最近的那颗。</summary>
        public static Vector3 WatchPoint(IReadOnlyList<EggState> eggs, IReadOnlyList<BabyState> babies, Vector3 fallback)
        {
            Vector3 best = fallback;
            float bestDist = float.PositiveInfinity;
            bool found = false;
            if (eggs != null)
            {
                for (int i = 0; i < eggs.Count; i++)
                {
                    if (!TryCloser(eggs[i] != null && eggs[i].alive, eggs[i] != null ? eggs[i].position : Vector3.zero,
                            fallback, ref found, ref best, ref bestDist))
                        continue;
                }
            }
            if (found)
            {
                best.y = 0f;
                return best;
            }
            if (babies != null)
            {
                for (int i = 0; i < babies.Count; i++)
                {
                    if (!TryCloser(babies[i] != null && babies[i].alive, babies[i] != null ? babies[i].position : Vector3.zero,
                            fallback, ref found, ref best, ref bestDist))
                        continue;
                }
            }
            best.y = 0f;
            return best;
        }

        static bool TryCloser(bool alive, Vector3 position, Vector3 anchor, ref bool found, ref Vector3 best, ref float bestDist)
        {
            if (!alive) return false;
            float dist = (position - anchor).sqrMagnitude;
            if (found && dist >= bestDist) return false;
            found = true;
            best = position;
            bestDist = dist;
            return true;
        }

        public static bool HasLiveEgg(MatchController match)
        {
            if (match == null || match.Eggs == null) return false;
            for (int i = 0; i < match.Eggs.Count; i++)
            {
                EggState egg = match.Eggs[i];
                if (egg != null && egg.alive) return true;
            }
            return false;
        }

        public static bool HasLiveBaby(MatchController match)
        {
            if (match == null || match.Babies == null) return false;
            for (int i = 0; i < match.Babies.Count; i++)
            {
                BabyState baby = match.Babies[i];
                if (baby != null && baby.alive) return true;
            }
            return false;
        }

        static bool PickupAlive(MatchController match, string kind)
        {
            if (match == null || match.Pickups == null) return false;
            for (int i = 0; i < match.Pickups.Count; i++)
            {
                PickupState pickup = match.Pickups[i];
                if (pickup != null && pickup.alive && pickup.kind == kind) return true;
            }
            return false;
        }

        static BugState Bug(MatchController match, int id)
        {
            if (match == null || match.Bugs == null || id < 0 || id >= match.Bugs.Length) return null;
            return match.Bugs[id];
        }

        static GameObject marker;

        static void ShowMarker(Vector3 world)
        {
            if (marker == null)
            {
                marker = new GameObject("TutorialWorldMarker");
                SpriteRenderer renderer = marker.AddComponent<SpriteRenderer>();
                renderer.sprite = Resources.Load<Sprite>("Battle/Entities/Textures/Circle");
                renderer.color = new Color(1f, 0.86f, 0.2f, 0.72f);
                renderer.sortingOrder = 40;
                marker.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            }
            marker.SetActive(true);
            marker.transform.position = world + Vector3.up * 0.08f;
            marker.transform.localScale = Vector3.one * MarkerPulse;
        }

        static void HideMarker()
        {
            if (marker != null) marker.SetActive(false);
        }

        static Image outlineImage;

        static void PulseOutline(Transform hudRoot, bool on)
        {
            if (outlineImage == null)
                outlineImage = FindOutlineImage(hudRoot);
            if (outlineImage != null)
            {
                if (!on) outlineImage.color = Color.white;
                else
                {
                    float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * Mathf.PI * 3.4f);
                    outlineImage.color = Color.Lerp(Color.white, new Color(1f, 0.25f, 0.08f, 1f), 0.55f + 0.45f * pulse);
                }
            }
            ArenaZoneView zone = ArenaZoneView.Ensure();
            if (zone != null) zone.SetTutorialBoundPulse(on);
        }

        static Image FindOutlineImage(Transform hudRoot)
        {
            Transform named = hudRoot != null ? FindNamed(hudRoot, "tableOutline") : null;
            if (named == null)
            {
                Transform world = GameObject.Find("BattleWorld") != null
                    ? GameObject.Find("BattleWorld").transform
                    : null;
                if (world != null) named = FindNamed(world, "tableOutline");
            }
            return named != null ? named.GetComponent<Image>() : null;
        }

        static Transform FindNamed(Transform root, string objectName)
        {
            if (root == null) return null;
            if (root.name == objectName) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform hit = FindNamed(root.GetChild(i), objectName);
                if (hit != null) return hit;
            }
            return null;
        }
    }
}
