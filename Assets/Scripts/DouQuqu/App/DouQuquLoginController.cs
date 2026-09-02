using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DouQuqu
{
    /// <summary>登录场景：只收集玩家名并从数据服务加载或创建玩家。</summary>
    public sealed class DouQuquLoginController : MonoBehaviour
    {
        private TMP_InputField nameInput;
        private TMP_Text statusText;
        private Button loginButton;

        private void Start()
        {
            BuildUi();
            nameInput.ActivateInputField();
        }

        private void BuildUi()
        {
            RectTransform root = DouQuquUiFactory.CreateScreen("LoginCanvas");
            RectTransform panel = DouQuquUiFactory.CreatePanel(root, "LoginPanel",
                new Vector2(0.28f, 0.18f), new Vector2(0.72f, 0.82f), Vector2.zero, Vector2.zero);
            DouQuquUiFactory.CreateText(panel, "Title", "斗蟋蟀 Demo", 58f,
                new Vector2(0.08f, 0.72f), new Vector2(0.92f, 0.94f), Vector2.zero, Vector2.zero);
            DouQuquUiFactory.CreateText(panel, "Hint", "输入名称后登录", 28f,
                new Vector2(0.08f, 0.62f), new Vector2(0.92f, 0.73f), Vector2.zero, Vector2.zero);
            nameInput = DouQuquUiFactory.CreateInput(panel, "PlayerNameInput", "玩家名称",
                new Vector2(0.14f, 0.43f), new Vector2(0.86f, 0.57f), Vector2.zero, Vector2.zero);
            loginButton = DouQuquUiFactory.CreateButton(panel, "LoginButton", "登录", Login,
                new Vector2(0.24f, 0.23f), new Vector2(0.76f, 0.36f), Vector2.zero, Vector2.zero);
            statusText = DouQuquUiFactory.CreateText(panel, "Status", string.Empty, 24f,
                new Vector2(0.08f, 0.08f), new Vector2(0.92f, 0.19f), Vector2.zero, Vector2.zero);
            nameInput.onSubmit.AddListener(_ => Login());
        }

        private void Login()
        {
            loginButton.interactable = false;
            string error;
            if (!DouQuquPlayerDataService.LoginOrCreate(nameInput.text, out error))
            {
                statusText.text = error;
                loginButton.interactable = true;
                return;
            }
            statusText.text = "登录成功";
            DouQuquSceneNames.Load(DouQuquSceneNames.MainMenu);
        }
    }
}
