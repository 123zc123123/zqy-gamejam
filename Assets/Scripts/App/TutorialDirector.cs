using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DouQuqu
{
    /// <summary>
    /// 首登新手：选虫前对白发虫 → 一槽开战 → 结算点退出回进战页 → 老头说话 → 返回村子点小铺。
    /// 步骤写在 PlayerProfile.tutorialStep；对白正文在 Resources/Dialogue。
    /// </summary>
    public static class TutorialDirector
    {
        public const int StepOff = 0;
        public const int StepHeroSelectTalk = 1;
        public const int StepBattle = 2;
        public const int StepClickExit = 3;
        public const int StepBattleEnterTalk = 4;
        public const int StepClickBackIcon = 5;
        public const int StepOpenShop = 6;
        public const int StepShopTalk = 7;
        public const int StepBuyEgg = 8;
        public const int StepBoughtTalk = 9;
        public const int StepDone = 10;
        public const int StepSwipeNav = 11;
        public const int StepClickBreed = 12;
        public const int StepPlaceEgg1 = 13;
        public const int StepPlaceEgg2 = 14;
        public const int StepMergeLarva = 15;

        public const string IdHeroSelect = "dlg.tutorial.hero_select";
        public const string IdSettlement = "dlg.tutorial.settlement";
        public const string IdShop = "dlg.tutorial.shop";
        public const string IdShopBought = "dlg.tutorial.shop_bought";
        public const string IdBattleJump = "dlg.tutorial.battle_jump";
        public const string IdBattleBound = "dlg.tutorial.battle_bound";
        public const string IdBattleHeart = "dlg.tutorial.battle_heart";
        public const string IdBattleShield = "dlg.tutorial.battle_shield";
        public const string IdBattleNest = "dlg.tutorial.battle_nest";
        public const string IdBattleKill = "dlg.tutorial.battle_kill";
        public const string IdBreedDone = "dlg.tutorial.breed_done";
        public const string IdBreedMerge = "dlg.tutorial.breed_merge";

        public const float BoundShowSeconds = 2.5f;
        public const float NestHp = 1f;

        public static bool NeedsBattleLesson => Step == StepBattle;

        public static bool IsActive
        {
            get
            {
                int step = Step;
                if (step >= StepSwipeNav && step <= StepMergeLarva) return true;
                return step > StepOff && step < StepDone;
            }
        }

        public static bool OneSlotStart => Step == StepBattle;
        public static bool OneLifeBattle => Step == StepBattle;
        public static bool BlocksHeroReady => Step == StepHeroSelectTalk || DialogueBoxView.IsPlaying;
        public static bool BlocksStarterGrant(PlayerProfile player)
        {
            if (player == null) return false;
            int step = player.tutorialStep;
            if (step >= StepSwipeNav && step <= StepMergeLarva) return true;
            return step > StepOff && step < StepDone;
        }

        public static int Step => PlayerDataService.TutorialStep;

        public static void BeginForNewPlayer()
        {
            PlayerDataService.SetTutorialStep(StepHeroSelectTalk);
        }

        public static void RouteAfterLogin()
        {
            int step = Step;
            if (step <= StepOff) return;
            if (step == StepSwipeNav || step == StepClickBreed)
            {
                Lobby.SetPending(Lobby.Page.Shop);
                return;
            }
            if (step >= StepPlaceEgg1 && step <= StepMergeLarva)
            {
                Lobby.SetPending(Lobby.Page.Merge);
                return;
            }
            if (step >= StepDone) return;
            if (step <= StepBattle)
            {
                AppServices.PendingMatchKind = MatchKind.Training;
                Lobby.SetPending(Lobby.Page.HeroSelection);
                return;
            }

            if (step <= StepClickBackIcon)
            {
                Lobby.SetPending(Lobby.Page.BattleEnter);
                return;
            }

            Lobby.SetPending(Lobby.Page.Home);
        }

        public static void OnHeroSelectOpened(HeroSelectionController selection)
        {
            if (selection == null) return;
            if (Step != StepHeroSelectTalk) return;
            DialogueBoxView.Play(IdHeroSelect, () =>
            {
                if (PlayerDataService.BackpackCount() <= 0)
                    PlayerDataService.AddFinestToBackpack(1, 1);
                PlayerDataService.SetTutorialStep(StepBattle);
                selection.NotifyBackpackChanged();
            });
        }

        public static void OnSettlementShown(GameObject resultRoot)
        {
            SpotlightBattleExit(resultRoot);
        }

        public static void OnEliminationShown(GameObject resultRoot)
        {
            SpotlightBattleExit(resultRoot);
        }

        public static bool TryHandleReturn()
        {
            if (Step != StepBattle && Step != StepClickExit) return false;
            TutorialSpotlight.Hide();
            PlayerDataService.SetTutorialStep(StepBattleEnterTalk);
            Lobby.Show(Lobby.Page.BattleEnter);
            return true;
        }

        public static void OnBattleEnterReady()
        {
            if (Step == StepClickExit || Step == StepBattleEnterTalk)
            {
                TutorialSpotlight.Hide();
                PlayerDataService.SetTutorialStep(StepBattleEnterTalk);
                DialogueBoxView.Play(IdSettlement, () =>
                {
                    PlayerDataService.SetTutorialStep(StepClickBackIcon);
                    SpotlightBackIcon();
                });
                return;
            }

            if (Step == StepClickBackIcon) SpotlightBackIcon();
        }

        public static void OnHomeReady()
        {
            if (Step == StepClickBackIcon)
            {
                TutorialSpotlight.Hide();
                PlayerDataService.SetTutorialStep(StepOpenShop);
            }
            if (Step != StepOpenShop) return;
            DelaySpotlightShop();
        }

        public static void OnShopOpened(ShopController shop)
        {
            if (shop == null) return;
            if (Step == StepOpenShop || Step == StepShopTalk)
            {
                TutorialSpotlight.Hide();
                PlayerDataService.SetTutorialStep(StepShopTalk);
                DialogueBoxView.Play(IdShop, () =>
                {
                    PlayerDataService.SetTutorialStep(StepBuyEgg);
                    TutorialSpotlight.Show(shop.EggOfferButton != null ? shop.EggOfferButton.gameObject : null, "买一只幼虫");
                });
                return;
            }

            if (Step == StepBuyEgg)
                TutorialSpotlight.Show(shop.EggOfferButton != null ? shop.EggOfferButton.gameObject : null, "买一只幼虫");
            if (Step == StepSwipeNav || Step == StepClickBreed)
                TutorialShopNavHint.Begin();
        }

        public static void OnBoughtEggs()
        {
            if (Step != StepBuyEgg) return;
            TutorialSpotlight.Hide();
            PlayerDataService.SetTutorialStep(StepBoughtTalk);
            DialogueBoxView.Play(IdShopBought, () =>
            {
                PlayerDataService.SetTutorialStep(StepSwipeNav);
                TutorialShopNavHint.Begin();
            });
        }

        public static void OnBreedingOpened()
        {
            if (Step < StepSwipeNav || Step > StepMergeLarva) return;
            TutorialFingerHint.Hide();
            TutorialSpotlight.Hide();
            TutorialShopNavHint.Stop();
            if (Step == StepSwipeNav || Step == StepClickBreed)
                PlayerDataService.SetTutorialStep(StepPlaceEgg1);
            TutorialBreedHint.Begin();
        }

        static void DelaySpotlightShop()
        {
            if (Step != StepOpenShop) return;
            GameObject shop = FindActive("MenuButtonShop");
            TutorialSpotlight.Show(shop, "点击小铺");
        }

        static void SpotlightBattleExit(GameObject root)
        {
            if (Step != StepBattle && Step != StepClickExit) return;
            PlayerDataService.SetTutorialStep(StepClickExit);
            TutorialSpotlight.Show(FindExitButton(root), "点击退出");
        }

        static void SpotlightBackIcon()
        {
            GameObject back = FindActive("返回icon");
            if (back == null) back = FindActive("BackIcon");
            TutorialSpotlight.Show(back, "点击返回");
        }

        static GameObject FindActive(string objectName)
        {
            if (string.IsNullOrEmpty(objectName)) return null;
            GameObject found = GameObject.Find(objectName);
            if (found != null && found.activeInHierarchy) return found;
            Transform nested = FindNamed(null, objectName);
            return nested != null && nested.gameObject.activeInHierarchy ? nested.gameObject : null;
        }

        static GameObject FindExitButton(GameObject root)
        {
            if (root == null) return null;
            Button[] buttons = root.GetComponentsInChildren<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
            {
                Button button = buttons[i];
                if (button == null) continue;
                if (button.gameObject.name == "退出") return button.gameObject;
                TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
                if (label != null && label.text == "退出") return button.gameObject;
            }
            Transform named = FindNamed(root.transform, "退出");
            return named != null ? named.gameObject : null;
        }

        static Transform FindNamed(Transform root, string objectName)
        {
            if (string.IsNullOrEmpty(objectName)) return null;
            if (root == null)
            {
                GameObject[] all = Resources.FindObjectsOfTypeAll<GameObject>();
                for (int i = 0; i < all.Length; i++)
                {
                    GameObject go = all[i];
                    if (go == null || go.name != objectName) continue;
                    if (!go.scene.IsValid() || !go.activeInHierarchy) continue;
                    return go.transform;
                }
                return null;
            }

            if (root.name == objectName) return root;
            Transform direct = root.Find(objectName);
            if (direct != null) return direct;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform nested = FindNamed(root.GetChild(i), objectName);
                if (nested != null) return nested;
            }
            return null;
        }
    }
}
