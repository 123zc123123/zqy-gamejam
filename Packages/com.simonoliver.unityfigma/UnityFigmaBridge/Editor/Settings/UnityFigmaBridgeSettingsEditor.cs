using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityFigmaBridge.Editor.FigmaApi;

namespace UnityFigmaBridge.Editor.Settings
{
    [CustomEditor(typeof(UnityFigmaBridgeSettings))]
    public sealed class UnityFigmaBridgeSettingsEditor : UnityEditor.Editor
    {
        private static Vector2 s_PageScrollPos;
        private static Vector2 s_FrameScrollPos;
        private static string s_FrameSearch = string.Empty;

        public override void OnInspectorGUI()
        {
            var targetSettingsObject = target as UnityFigmaBridgeSettings;
            var onlyImportPages = targetSettingsObject.OnlyImportSelectedPages;
            var onlyImportFrames = targetSettingsObject.OnlyImportSelectedFrames;
            var preEditUrl = targetSettingsObject.DocumentUrl;
            DrawDefaultInspector();

            if (targetSettingsObject.DocumentUrl != preEditUrl)
            {
                if (targetSettingsObject.OnlyImportSelectedPages)
                {
                    targetSettingsObject.OnlyImportSelectedPages = false;
                    targetSettingsObject.PageDataList.Clear();
                }

                targetSettingsObject.FrameDataList.Clear();
            }
            else
            {
                if (targetSettingsObject.OnlyImportSelectedPages != onlyImportPages &&
                    targetSettingsObject.OnlyImportSelectedPages)
                {
                    RefreshSelectionLists(targetSettingsObject);
                }

                if (targetSettingsObject.OnlyImportSelectedFrames != onlyImportFrames &&
                    targetSettingsObject.OnlyImportSelectedFrames)
                {
                    RefreshSelectionLists(targetSettingsObject);
                }
            }

            DrawSelectionLists(targetSettingsObject);
        }

        public static void DrawSettingsSelectionLists(UnityFigmaBridgeSettings settings)
        {
            if (settings == null) return;
            DrawSelectionLists(settings);
        }

        public static void DrawSelectionLists(UnityFigmaBridgeSettings targetSettingsObject)
        {
            if (targetSettingsObject.OnlyImportSelectedPages)
            {
                GUILayout.Space(20);
                var changed = ListPages("Select Pages to import", targetSettingsObject.PageDataList,
                    ref s_PageScrollPos);
                if (changed)
                {
                    EditorUtility.SetDirty(targetSettingsObject);
                    AssetDatabase.SaveAssetIfDirty(targetSettingsObject);
                }
            }

            if (!targetSettingsObject.OnlyImportSelectedFrames) return;

            GUILayout.Space(20);
            var frameChanged = ListFrames(targetSettingsObject, ref s_FrameScrollPos, ref s_FrameSearch);
            if (frameChanged)
            {
                EditorUtility.SetDirty(targetSettingsObject);
                AssetDatabase.SaveAssetIfDirty(targetSettingsObject);
            }
        }

        /// <summary>
        /// Download the document and refresh the page list
        /// </summary>
        /// <param name="settings"></param>
        private static async void RefreshSelectionLists(UnityFigmaBridgeSettings settings)
        {
            // Only refresh pages if we have a valid file
            var requirementsMet = UnityFigmaBridgeImporter.CheckRequirements();
            if (!requirementsMet) return;

            // Retrieve the Figma document
            var figmaFile = await UnityFigmaBridgeImporter.DownloadFigmaDocument(settings.FileId);
            if (figmaFile == null) return;
            
            settings.RefreshForUpdatedPages(figmaFile);
            settings.RefreshForUpdatedFrames(figmaFile);

            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssetIfDirty(settings);
        }

        /// <summary>
        /// List all pages in the settings file
        /// </summary>
        /// <param name="listTitle"></param>
        /// <param name="dataList"></param>
        /// <param name="scrollPos"></param>
        /// <returns></returns>
        private static bool ListPages(string listTitle, IReadOnlyList<FigmaPageData> dataList, ref Vector2 scrollPos)
        {
            var applyChanges = false;
            using (new EditorGUILayout.VerticalScope()) {
                GUILayout.Label(listTitle, EditorStyles.boldLabel);
                GUILayout.Space(5);
                using (new EditorGUILayout.HorizontalScope()) {
                    if (GUILayout.Button("Select all", GUILayout.Width(80))) {
                        applyChanges = true;
                        foreach (var data in dataList) {
                            data.Selected = true;
                        }
                    }

                    if (GUILayout.Button("Deselect all", GUILayout.Width(80))) {
                        applyChanges = true;
                        foreach (var data in dataList) {
                            data.Selected = false;
                        }
                    }
                }
                GUILayout.Space(5);

                using (var scrollViewScope = new EditorGUILayout.ScrollViewScope(scrollPos, GUILayout.Height(160)))
                {
                    foreach (var data in dataList) {
                        var isChecked = data.Selected;
                        data.Selected = EditorGUILayout.ToggleLeft(data.Name, data.Selected);
                        if (isChecked != data.Selected) {
                            applyChanges = true;
                        }
                        
                    }
                    scrollPos = scrollViewScope.scrollPosition;
                }

                return applyChanges;

            }
        }

        private static bool ListFrames(UnityFigmaBridgeSettings settings, ref Vector2 scrollPos, ref string search)
        {
            var applyChanges = false;
            using (new EditorGUILayout.VerticalScope())
            {
                GUILayout.Label("Select Frames / Components to import", EditorStyles.boldLabel);
                GUILayout.Label("只生成勾选项。被实例用到的 Component 仍会生成 Prefab。", EditorStyles.wordWrappedMiniLabel);
                GUILayout.Space(5);
                search = EditorGUILayout.TextField("Filter", search);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Select all", GUILayout.Width(80)))
                    {
                        applyChanges = true;
                        foreach (var data in FilteredFrames(settings, search)) data.Selected = true;
                    }

                    if (GUILayout.Button("Deselect all", GUILayout.Width(80)))
                    {
                        applyChanges = true;
                        foreach (var data in FilteredFrames(settings, search)) data.Selected = false;
                    }

                    if (GUILayout.Button("Refresh list", GUILayout.Width(90)))
                    {
                        RefreshSelectionLists(settings);
                    }
                }

                GUILayout.Space(5);
                var selectedCount = settings.FrameDataList.Count(f => f.Selected);
                GUILayout.Label($"Selected {selectedCount} / {settings.FrameDataList.Count}", EditorStyles.miniLabel);

                using (var scrollViewScope = new EditorGUILayout.ScrollViewScope(scrollPos, GUILayout.Height(240)))
                {
                    foreach (var data in FilteredFrames(settings, search))
                    {
                        var isChecked = data.Selected;
                        var label = $"{data.Name}  [{data.PageName}]  {data.NodeType}  {data.NodeId}";
                        data.Selected = EditorGUILayout.ToggleLeft(label, data.Selected);
                        if (isChecked != data.Selected) applyChanges = true;
                    }

                    scrollPos = scrollViewScope.scrollPosition;
                }

                return applyChanges;
            }
        }

        private static IEnumerable<FigmaFrameData> FilteredFrames(UnityFigmaBridgeSettings settings, string search)
        {
            if (string.IsNullOrEmpty(search)) return settings.FrameDataList;
            return settings.FrameDataList.Where(data =>
                (!string.IsNullOrEmpty(data.Name) && data.Name.IndexOf(search, System.StringComparison.OrdinalIgnoreCase) >= 0) ||
                (!string.IsNullOrEmpty(data.NodeId) && data.NodeId.IndexOf(search, System.StringComparison.OrdinalIgnoreCase) >= 0) ||
                (!string.IsNullOrEmpty(data.PageName) && data.PageName.IndexOf(search, System.StringComparison.OrdinalIgnoreCase) >= 0));
        }
    }
}
