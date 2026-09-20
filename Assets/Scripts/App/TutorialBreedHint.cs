using System.Collections;
using UnityEngine;

namespace DouQuqu
{
    /// <summary>育虫盘：金圈放两只幼虫，再拖合成中虫。</summary>
    public sealed class TutorialBreedHint : MonoBehaviour
    {
        static TutorialBreedHint instance;

        BreedingBoardView view;
        MergeBoard board;
        bool waitingTalk;
        bool mergePrompted;

        public static void Begin()
        {
            if (instance == null)
            {
                GameObject host = new GameObject("TutorialBreedHint");
                instance = host.AddComponent<TutorialBreedHint>();
                DontDestroyOnLoad(host);
            }
            instance.Run();
        }

        public static void Stop()
        {
            if (instance == null) return;
            instance.Unbind();
            instance.enabled = false;
            TutorialSpotlight.Hide();
        }

        void Run()
        {
            enabled = true;
            waitingTalk = false;
            mergePrompted = false;
            StopAllCoroutines();
            StartCoroutine(Boot());
        }

        IEnumerator Boot()
        {
            yield return null;
            Unbind();
            view = FindObjectOfType<BreedingBoardView>();
            board = view != null ? view.Board : FindObjectOfType<MergeBoard>();
            if (board != null)
            {
                board.PieceSpawned += OnSpawned;
                board.MergeCompleted += OnMerged;
                board.BoardChanged += OnBoardChanged;
            }
            DialogueCatalog.Reload();
            Refresh();
        }

        void Unbind()
        {
            if (board == null) return;
            board.PieceSpawned -= OnSpawned;
            board.MergeCompleted -= OnMerged;
            board.BoardChanged -= OnBoardChanged;
            board = null;
            view = null;
        }

        void OnSpawned(MergePiece piece)
        {
            Refresh();
        }

        void OnBoardChanged()
        {
            Refresh();
        }

        void OnMerged(MergePiece result, MergePiece consumed)
        {
            if (result != null && result.level >= 2)
                Finish();
        }

        void Refresh()
        {
            if (waitingTalk) return;
            int step = TutorialDirector.Step;
            if (step < TutorialDirector.StepPlaceEgg1 || step > TutorialDirector.StepMergeLarva)
                return;

            int larva = view != null ? view.CountLevel(1) : 0;
            int grown = 0;
            if (view != null)
            {
                grown += view.CountLevel(2);
                grown += view.CountLevel(3);
                grown += view.CountLevel(4);
            }

            if (grown > 0)
            {
                Finish();
                return;
            }

            if (larva >= 2)
            {
                PlayerDataService.SetTutorialStep(TutorialDirector.StepMergeLarva);
                TutorialSpotlight.Hide();
                if (!mergePrompted)
                {
                    mergePrompted = true;
                    DialogueBoxView.Play(TutorialDirector.IdBreedMerge, null);
                }
                return;
            }

            PlayerDataService.SetTutorialStep(larva <= 0
                ? TutorialDirector.StepPlaceEgg1
                : TutorialDirector.StepPlaceEgg2);
            GameObject circle = view != null ? view.GoldCircle : null;
            TutorialSpotlight.Show(circle, larva <= 0 ? "点金圈放幼虫" : "再放一只幼虫");
        }

        void Finish()
        {
            if (waitingTalk) return;
            waitingTalk = true;
            TutorialSpotlight.Hide();
            PlayerDataService.SetTutorialStep(TutorialDirector.StepMergeLarva);
            DialogueBoxView.Play(TutorialDirector.IdBreedDone, () =>
            {
                StartCoroutine(GiftThenDone());
            });
        }

        IEnumerator GiftThenDone()
        {
            PlayerDataService.AddFinestToBackpack(1, 3);
            PlayerDataService.AddFinestToBackpack(1, 2);
            if (view != null) view.RefreshBackpackIfOpen();
            bool first = false;
            bool second = false;
            StartFlyGift(3, new Vector2(-80f, 160f), () => first = true);
            float wait = 0f;
            while (wait < 0.22f)
            {
                wait += Time.unscaledDeltaTime;
                yield return null;
            }
            StartFlyGift(2, new Vector2(-160f, 200f), () => second = true);
            while (!first || !second) yield return null;
            if (view != null) view.RefreshBackpackIfOpen();
            PlayerDataService.SetTutorialStep(TutorialDirector.StepDone);
            Stop();
        }

        void StartFlyGift(int temperament, Vector2 startOffset, System.Action done)
        {
            Sprite face = CricketCatalog.Portrait(1, temperament);
            RectTransform bag = view != null ? view.BackpackButton : null;
            FinestRevealFx.PlayFlyToBag(face, bag, startOffset, done);
        }

        void OnDestroy()
        {
            if (instance == this) instance = null;
            Unbind();
        }
    }
}
