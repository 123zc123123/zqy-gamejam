using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace DouQuqu
{
    /// <summary>第一场训练营六课：跳、出圈、吃饲料、吃护盾、撞房子、撞人得分。</summary>
    public static class TutorialBattleDirector
    {
        const int DummyId = 1;
        const float MarkerPulse = 1.6f;

        public static IEnumerator Play(MatchController match, int localId, Transform hudRoot)
        {
            if (match == null || !TutorialDirector.NeedsBattleLesson) yield break;

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
        }

        static bool Failed(MatchController match, int localId)
        {
            return match == null || match.IsOver || !match.PlayerStillIn(localId);
        }

        static IEnumerator LessonJump(MatchController match, int localId, Transform hudRoot)
        {
            bool done = false;
            DialogueBoxView.Play(TutorialDirector.IdBattleJump, () => done = true);
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
            float shown = 0f;
            while (!Failed(match, localId) && shown < TutorialDirector.BoundShowSeconds)
            {
                PulseOutline(hudRoot, true);
                shown += Time.unscaledDeltaTime;
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
            yield return WaitUntilLanded(match, localId);
        }

        static IEnumerator LessonKill(MatchController match, int localId)
        {
            yield return WaitUntilLanded(match, localId);
            int before = match.MatchScore(localId);
            Vector3 at = match.PointOutward(localId, 5.5f);
            match.MovePlayerTo(DummyId, at);
            match.SetPlayerIdle(DummyId, true);
            ShowMarker(at);
            bool done = false;
            DialogueBoxView.Play(TutorialDirector.IdBattleKill, () => done = true);
            while (!done && !Failed(match, localId)) yield return null;
            while (!Failed(match, localId) && match.MatchScore(localId) <= before && match.PlayerStillIn(DummyId))
                yield return null;
            HideMarker();
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
            RestoreTableTint(hudRoot);
            ArenaZoneView zone = ArenaZoneView.Ensure();
            if (zone != null) zone.SetTutorialBoundPulse(on);
        }

        static void RestoreTableTint(Transform hudRoot)
        {
            if (outlineImage == null)
                outlineImage = FindOutlineImage(hudRoot);
            if (outlineImage != null)
                outlineImage.color = Color.white;
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
