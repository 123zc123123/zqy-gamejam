using MCPForUnity.Editor.Windows;
using UnityEditor;
using UnityEngine;

namespace DouQuqu.Editor
{
    /// <summary>
    /// 打开工程时自动弹出 MCP 窗口，并打开 HTTP 随编辑器启动。
    /// </summary>
    [InitializeOnLoad]
    public static class McpAutoOpen
    {
        private const string SessionKey = "DouQuqu.McpWindowOpenedThisSession";
        private const string AutoStartPref = "MCPForUnity.AutoStartOnLoad";

        static McpAutoOpen()
        {
            if (Application.isBatchMode)
                return;

            if (!EditorPrefs.GetBool(AutoStartPref, false))
                EditorPrefs.SetBool(AutoStartPref, true);

            EditorApplication.delayCall += OpenOncePerSession;
        }

        private static void OpenOncePerSession()
        {
            if (SessionState.GetBool(SessionKey, false))
                return;

            SessionState.SetBool(SessionKey, true);
            MCPForUnityEditorWindow.ShowWindow();
        }
    }
}
