using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DouQuqu
{
    /// <summary>登录场景：绑 Login 预制体上的名字输入和登录按钮。</summary>
    public sealed class LoginController : MonoBehaviour
    {
        public const string PrefabResourcePath = "Login/Prefabs/Login";

        private TMP_InputField nameInput;
        private TMP_Text statusText;
        private Button loginButton;

        private void Awake()
        {
            Screen.orientation = ScreenOrientation.Portrait;
            Screen.autorotateToPortrait = true;
            Screen.autorotateToPortraitUpsideDown = false;
            Screen.autorotateToLandscapeLeft = false;
            Screen.autorotateToLandscapeRight = false;
        }

        private void Start()
        {
            VenueClient.Ensure();
            EnsureEventSystem();
            GameObject page = FindScenePage() ?? MountPrefab();
            if (page == null)
            {
                Debug.LogError("[DouQuqu] 找不到登录页 " + PrefabResourcePath);
                return;
            }

            BindPage(page);
            if (nameInput != null) nameInput.ActivateInputField();
        }

        private void BindPage(GameObject root)
        {
            UiFonts.ApplyTree(root.transform);
            nameInput = FindInput(root.transform);
            loginButton = FindButton(root.transform);
            statusText = FindText(root.transform, "Status");

            if (nameInput == null || loginButton == null)
            {
                Debug.LogError("[DouQuqu] 登录页缺少 PlayerNameInput 或 LoginButton");
                return;
            }

            nameInput.characterLimit = 20;
            nameInput.onSubmit.RemoveAllListeners();
            nameInput.onSubmit.AddListener(_ => Login());
            loginButton.onClick.RemoveAllListeners();
            loginButton.onClick.AddListener(Login);
        }

        private static GameObject FindScenePage()
        {
            Transform[] transforms = FindObjectsOfType<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform t = transforms[i];
                if ((t.name == "Login" || t.name == "LoginCanvas") && t.gameObject.scene.IsValid())
                    return t.gameObject;
            }

            return null;
        }

        private static GameObject MountPrefab()
        {
            GameObject prefab = Resources.Load<GameObject>(PrefabResourcePath);
            if (prefab == null) return null;

            GameObject instance = Instantiate(prefab);
            instance.name = "Login";
            Canvas canvas = instance.GetComponent<Canvas>();
            if (canvas == null) canvas = instance.GetComponentInChildren<Canvas>(true);
            if (canvas != null)
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

            return instance;
        }

        private static void EnsureEventSystem()
        {
            if (FindObjectOfType<EventSystem>() != null) return;
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }

        private static TMP_InputField FindInput(Transform root)
        {
            Transform named = FindNamed(root, "PlayerNameInput");
            if (named != null)
            {
                TMP_InputField field = named.GetComponent<TMP_InputField>();
                if (field != null) return field;
            }

            return root.GetComponentInChildren<TMP_InputField>(true);
        }

        private static Button FindButton(Transform root)
        {
            Transform named = FindNamed(root, "LoginButton");
            if (named == null) return null;
            Button button = named.GetComponent<Button>();
            return button != null ? button : named.gameObject.AddComponent<Button>();
        }

        private static TMP_Text FindText(Transform root, string objectName)
        {
            Transform named = FindNamed(root, objectName);
            return named != null ? named.GetComponent<TMP_Text>() : null;
        }

        private static Transform FindNamed(Transform root, string objectName)
        {
            if (root.name == objectName) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform hit = FindNamed(root.GetChild(i), objectName);
                if (hit != null) return hit;
            }

            return null;
        }

        private void Login()
        {
            if (loginButton == null || nameInput == null || !loginButton.interactable) return;
            loginButton.interactable = false;
            StartCoroutine(LoginRoutine());
        }

        private void SetStatus(string text)
        {
            if (statusText != null) statusText.text = text;
        }

        private System.Collections.IEnumerator LoginRoutine()
        {
            VenueClient venue = VenueClient.Ensure();
            if (venue != null && venue.HasServer)
            {
                SetStatus("正在连展会账本…");
                bool done = false;
                PlayerProfile remote = null;
                string remoteError = null;
                yield return venue.Login(nameInput.text, (player, error) =>
                {
                    remote = player;
                    remoteError = error;
                    done = true;
                });
                while (!done) yield return null;
                if (remote != null)
                {
                    PlayerDataService.AdoptRemote(remote);
                    SetStatus("登录成功（展会账本）");
                    SceneNames.Load(SceneNames.MainMenu);
                    yield break;
                }
                if (!string.IsNullOrEmpty(remoteError)) SetStatus(remoteError + "，改用本机");
            }

            string errorLocal;
            if (!PlayerDataService.LoginOrCreate(nameInput.text, out errorLocal))
            {
                SetStatus(errorLocal);
                loginButton.interactable = true;
                yield break;
            }
            SetStatus("登录成功");
            SceneNames.Load(SceneNames.MainMenu);
        }
    }
}
