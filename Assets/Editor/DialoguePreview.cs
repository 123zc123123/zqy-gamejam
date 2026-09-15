using UnityEditor;
using UnityEngine;

namespace DouQuqu.Editor
{
    public static class DialoguePreview
    {
        [MenuItem("DouQuqu/Dialogue/Reload Catalog")]
        public static void Reload()
        {
            DialogueCatalog.Reload();
            int count = 0;
            foreach (string id in DialogueCatalog.Ids) count++;
            Debug.Log("[DouQuqu] 已加载对白组 " + count + " 个。");
        }

        [MenuItem("DouQuqu/Dialogue/Play Sample")]
        public static void PlaySample()
        {
            if (!Application.isPlaying)
            {
                EditorApplication.EnterPlaymode();
                EditorApplication.delayCall += () => DialogueBoxView.Play("dlg.sample.box");
                return;
            }

            DialogueCatalog.Reload();
            DialogueBoxView.Play("dlg.sample.box");
        }

        [MenuItem("DouQuqu/Tutorial/Reset Current Player To Hero Select")]
        public static void ResetTutorial()
        {
            if (!PlayerDataService.IsLoggedIn)
            {
                Debug.LogWarning("[DouQuqu] 先登录再重置新手。");
                return;
            }
            PlayerDataService.SetTutorialStep(TutorialDirector.StepHeroSelectTalk);
            AppServices.PendingMatchKind = MatchKind.Training;
            Lobby.Show(Lobby.Page.HeroSelection);
        }
    }
}
