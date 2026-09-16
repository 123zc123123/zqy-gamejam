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
            PulseOutline(hudRoot, true);
            while (!done && !Failed(match, localId)) yield return null;
            float shown = 0f;
            while (!Failed(match, localId) && shown < TutorialDirector.BoundShowSeconds)
            {
                shown += Time.unscaledDeltaTime;
                yield return null;
            }
            PulseOutline(hudRoot, false);
        }

        static IEnumerator LessonPickup(MatchController match, int localId, string kind, string dialogueId)
        {
            Vector3 at = match.PointInward(localId, 6.5f);
            match.SpawnTutorialPickup(kind, at);
            yield return null;
            yield return null;
            if (!PickupAlive(match, kind))
            {
                at = match.PointInward(localId, 9.5f);
                match.SpawnTutorialPickup(kind, at);
                yield return null;
            }
            ShowMarker(at);
            bool done = false;
            DialogueBoxView.Play(dialogueId, () => done = true);
            while (!done && !Failed(match, localId)) yield return null;
            while (!Failed(match, localId) && PickupAlive(match, kind)) yield return null;
            HideMarker();
        }

        static IEnumerator LessonNest(MatchController match, int localId)
        {
            Vector3 at = match.PointInward(localId, 7.5f);
            match.SpawnTutorialNest(at, TutorialDirector.NestHp);
            ShowMarker(at);
            bool done = false;
            DialogueBoxView.Play(TutorialDirector.IdBattleNest, () => done = true);
            while (!done && !Failed(match, localId)) yield return null;
            while (!Failed(match, localId) && match.Nest != null && match.Nest.alive) yield return null;
            HideMarker();
        }

        static IEnumerator LessonKill(MatchController match, int localId)
        {
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

        static Color outlineHome = Color.white;
        static bool outlineHomeCaptured;
        static Image outlineImage;

        static void PulseOutline(Transform hudRoot, bool on)
        {
            if (outlineImage == null && hudRoot != null)
            {
                Transform named = FindNamed(hudRoot, "tableOutline");
                if (named != null) outlineImage = named.GetComponent<Image>();
            }
            if (outlineImage == null) return;
            if (!outlineHomeCaptured)
            {
                outlineHome = outlineImage.color;
                outlineHomeCaptured = true;
            }
            outlineImage.color = on ? new Color(1f, 0.35f, 0.2f, 1f) : outlineHome;
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
