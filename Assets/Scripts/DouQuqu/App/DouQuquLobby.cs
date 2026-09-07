using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using ZqyGameJam.UI.Home;

namespace DouQuqu
{
    /// <summary>
    /// 大厅壳：主页 / 育虫 / 图鉴 / 入口 / 匹配 / 商店都是 Prefab 页，只在进出对局时切 Scene。
    /// </summary>
    [DefaultExecutionOrder(-200)]
    public sealed class DouQuquLobby : MonoBehaviour
    {
        public enum Page
        {
            Home = 0,
            Merge = 1,
            Collection = 2,
            BattleEnter = 3,
            Matchmaking = 4,
            Shop = 5
        }

        public const string HomePrefab = "MainMenu/Home/Prefabs/CricketHomepage";
        public const string MergePrefab = "Merge/Prefabs/Canvas";
        public const string CollectionPrefab = "Collection/Prefabs/FigmaImport_cricket-collection_10_6";
        public const string BattleEnterPrefab = "BattleEntrance/Prefabs/BattleEntrance";
        public const string MatchmakingPrefab = "Matchmaking/Prefabs/FigmaImport_cricket-battle-royale_55_4";
        public const string ShopPrefab = "Shop/Prefabs/Shop";

        public static DouQuquLobby Instance { get; private set; }
        public static Page PendingPage { get; private set; }
        public static bool HasPendingPage { get; private set; }

        public Page CurrentPage { get; private set; }

        private readonly GameObject[] pages = new GameObject[6];
        private DouQuquBreedingBoardView breedingView;
        private DouQuquCollectionController collection;
        private DouQuquBattleEnterController battleEnter;
        private DouQuquMatchmakingController matchmaking;
        private DouQuquShopController shop;
        private DouQuquBottomNavBar nav;

        public static void SetPending(Page page)
        {
            PendingPage = page;
            HasPendingPage = true;
        }

        public static bool TryShow(string sceneName)
        {
            Page page;
            if (!TryMapScene(sceneName, out page)) return false;
            if (Instance != null)
            {
                Instance.ShowPage(page);
                return true;
            }

            SetPending(page);
            if (SceneManager.GetActiveScene().name == DouQuquSceneNames.MainMenu)
                return false;

            SceneManager.LoadScene(DouQuquSceneNames.MainMenu, LoadSceneMode.Single);
            return true;
        }

        public static void Show(Page page)
        {
            if (Instance == null)
                Instance = FindObjectOfType<DouQuquLobby>();
            if (Instance != null)
            {
                Instance.ShowPage(page);
                return;
            }

            SetPending(page);
            if (SceneManager.GetActiveScene().name != DouQuquSceneNames.MainMenu)
                SceneManager.LoadScene(DouQuquSceneNames.MainMenu, LoadSceneMode.Single);
        }

        public static bool TryMapScene(string sceneName, out Page page)
        {
            page = Page.Home;
            if (string.IsNullOrEmpty(sceneName)) return false;
            if (sceneName == DouQuquSceneNames.MainMenu) { page = Page.Home; return true; }
            if (sceneName == DouQuquSceneNames.Merge) { page = Page.Merge; return true; }
            if (sceneName == DouQuquSceneNames.Collection) { page = Page.Collection; return true; }
            if (sceneName == DouQuquSceneNames.BattleEnter) { page = Page.BattleEnter; return true; }
            if (sceneName == DouQuquSceneNames.Matchmaking) { page = Page.Matchmaking; return true; }
            if (sceneName == DouQuquSceneNames.Shop) { page = Page.Shop; return true; }
            return false;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;
            EnsureEventSystem();
            CaptureOrSpawnHome();
            pages[(int)Page.Merge] = Mount(MergePrefab, "LobbyPage_Merge");
            pages[(int)Page.Collection] = Mount(CollectionPrefab, "LobbyPage_Collection");
            pages[(int)Page.BattleEnter] = Mount(BattleEnterPrefab, "LobbyPage_BattleEnter");
            pages[(int)Page.Matchmaking] = Mount(MatchmakingPrefab, "LobbyPage_Matchmaking");
            pages[(int)Page.Shop] = Mount(ShopPrefab, "LobbyPage_Shop");
            EnsureRuntime();
            EnsureNav();
            Page start = HasPendingPage ? PendingPage : Page.Home;
            HasPendingPage = false;
            ShowPage(start);
        }

        private void OnEnable()
        {
            if (Instance == null) Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void ShowPage(Page page)
        {
            if (pages[(int)page] == null && page != Page.Home)
            {
                Debug.LogWarning("[DouQuqu] 大厅缺少页面 " + page);
                page = Page.Home;
            }

            if (CurrentPage == Page.Matchmaking && page != Page.Matchmaking && matchmaking != null)
                matchmaking.CancelMatchmaking();

            if (page != Page.BattleEnter && page != Page.Matchmaking && battleEnter != null && battleEnter.InRoom)
                battleEnter.LeaveRoomSilent();

            CurrentPage = page;
            for (int i = 0; i < pages.Length; i++)
            {
                if (pages[i] != null) pages[i].SetActive(i == (int)page);
            }

            if (page == Page.BattleEnter && battleEnter != null)
                battleEnter.RefreshVisual();
            SetNavVisible(ShouldShowNav(page));
        }

        public void SetNavVisible(bool visible)
        {
            if (nav != null) nav.transform.root.gameObject.SetActive(visible);
        }

        private bool ShouldShowNav(Page page)
        {
            if (page == Page.Matchmaking) return false;
            if (page == Page.BattleEnter && battleEnter != null && battleEnter.InRoom) return false;
            return true;
        }

        private void CaptureOrSpawnHome()
        {
            GameObject home = FindNamed("DouQuquMainMenu");
            if (home != null)
            {
                Canvas canvas = home.GetComponentInParent<Canvas>();
                pages[(int)Page.Home] = canvas != null ? canvas.gameObject : home;
                return;
            }

            CricketHomepageView view = FindObjectOfType<CricketHomepageView>(true);
            if (view != null)
            {
                pages[(int)Page.Home] = view.gameObject;
                return;
            }

            GameObject existingCanvas = GameObject.Find("Canvas");
            if (existingCanvas != null)
            {
                pages[(int)Page.Home] = existingCanvas;
                return;
            }

            pages[(int)Page.Home] = Mount(HomePrefab, "LobbyPage_Home");
        }

        private void EnsureRuntime()
        {
            DouQuquMergeBoard board = GetComponent<DouQuquMergeBoard>();
            if (board == null) board = gameObject.AddComponent<DouQuquMergeBoard>();

            breedingView = GetComponent<DouQuquBreedingBoardView>();
            if (breedingView == null) breedingView = gameObject.AddComponent<DouQuquBreedingBoardView>();
            if (pages[(int)Page.Merge] != null) breedingView.AttachCanvas(pages[(int)Page.Merge]);

            if (GetComponent<DouQuquMergeSceneController>() == null)
                gameObject.AddComponent<DouQuquMergeSceneController>();

            collection = GetComponent<DouQuquCollectionController>();
            if (collection == null) collection = gameObject.AddComponent<DouQuquCollectionController>();
            if (pages[(int)Page.Collection] != null)
                TryBind("图鉴", () => collection.BindPage(pages[(int)Page.Collection]));

            battleEnter = GetComponent<DouQuquBattleEnterController>();
            if (battleEnter == null) battleEnter = gameObject.AddComponent<DouQuquBattleEnterController>();
            if (pages[(int)Page.BattleEnter] != null)
                TryBind("进战", () => battleEnter.BindPage(pages[(int)Page.BattleEnter]));

            matchmaking = GetComponent<DouQuquMatchmakingController>();
            if (matchmaking == null) matchmaking = gameObject.AddComponent<DouQuquMatchmakingController>();
            if (pages[(int)Page.Matchmaking] != null)
                TryBind("匹配", () => matchmaking.BindPage(pages[(int)Page.Matchmaking]));

            shop = GetComponent<DouQuquShopController>();
            if (shop == null) shop = gameObject.AddComponent<DouQuquShopController>();
            if (pages[(int)Page.Shop] != null)
                TryBind("商店", () => shop.BindPage(pages[(int)Page.Shop]));
        }

        private static void TryBind(string pageName, System.Action bind)
        {
            try
            {
                bind();
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[DouQuqu] 大厅页绑定失败 " + pageName + "\n" + e);
            }
        }

        private void EnsureNav()
        {
            GameObject navCanvas = GameObject.Find("LobbyNavCanvas");
            if (navCanvas == null)
            {
                RectTransform root = DouQuquUiFactory.CreateOverlay("LobbyNavCanvas", 200);
                navCanvas = root.gameObject;
            }

            nav = DouQuquBottomNavBar.EnsureOn(navCanvas.transform);
        }

        private static GameObject Mount(string resourcesPath, string objectName)
        {
            GameObject prefab = Resources.Load<GameObject>(resourcesPath);
            if (prefab == null)
            {
                Debug.LogWarning("[DouQuqu] 找不到大厅页 " + resourcesPath);
                return null;
            }

            GameObject instance = Instantiate(prefab);
            instance.name = objectName;
            Canvas canvas = instance.GetComponent<Canvas>();
            if (canvas == null) canvas = instance.GetComponentInChildren<Canvas>(true);
            if (canvas == null)
            {
                RectTransform overlay = DouQuquUiFactory.CreateOverlay(objectName, 10);
                instance.transform.SetParent(overlay, false);
                Stretch(instance.transform as RectTransform);
                instance = overlay.gameObject;
                instance.name = objectName;
            }
            else
            {
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                if (canvas.sortingOrder < 1) canvas.sortingOrder = 10;
                CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
                if (scaler != null)
                {
                    scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                    scaler.referenceResolution = new Vector2(1080f, 1920f);
                    scaler.matchWidthOrHeight = 1f;
                }
            }

            DouQuquBottomNavBar.SuppressEmbedded(instance.transform);
            instance.SetActive(false);
            return instance;
        }

        private static void Stretch(RectTransform rect)
        {
            if (rect == null) return;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
        }

        private static void EnsureEventSystem()
        {
            if (FindObjectOfType<UnityEngine.EventSystems.EventSystem>() != null) return;
            new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem),
                typeof(UnityEngine.EventSystems.StandaloneInputModule));
        }

        private static GameObject FindNamed(string objectName)
        {
            Transform[] transforms = FindObjectsOfType<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform t = transforms[i];
                if (t.name == objectName && t.gameObject.scene.IsValid())
                    return t.gameObject;
            }
            return null;
        }
    }
}
