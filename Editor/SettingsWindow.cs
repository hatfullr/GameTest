using System.Collections.Generic;
using UnityEditor;
using UnityEngine;


namespace GameTest
{
    /// <summary>
    /// The window that pops up when a settings cog button is clicked
    /// </summary>
    [System.Serializable]
    public class SettingsWindow : EditorWindow
    {
        private Test test;
        private static Dictionary<Component, bool> expanded = new Dictionary<Component, bool>();
        private static Vector2 scrollPosition;

        public void Init(Test test)
        {
            minSize = new Vector2(
                EditorGUIUtility.labelWidth + EditorGUIUtility.fieldWidth,
                minSize.y
            );
            titleContent = new GUIContent(test.attribute.name + " (Settings)");
            this.test = test;
            Repaint();
        }

        public Test GetTest() => test;

        void OnGUI()
        {
            if (test == null)
            {
                Close();
                return;
            }

            DrawHeader();
            DrawInspector();
            DrawFooter();
        }

        private void DrawHeader()
        {
            GUIContent label = new GUIContent("Prefab Override", Style.Tooltips.settingsWindowPrefab);
            using (new EditorGUILayout.VerticalScope(Style.Get("Settings/Header")))
            {
                EditorGUILayout.ObjectField(label, test.prefab, typeof(GameObject), false);
            }
        }

        private void DrawInspector()
        {
            //prefabEditor.serializedObject.Update();
            using (new EditorGUI.DisabledGroupScope(test.prefab != null))
            {
                Editor prefabEditor = Editor.CreateEditor(test.defaultPrefab);
                prefabEditor.DrawHeader();
                DestroyImmediate(prefabEditor); // This is critically important. Otherwise, wierd errors appear in the Console after
                                                // first recompile after a Test was destroyed
                
                EditorGUILayout.ScrollViewScope scope = new EditorGUILayout.ScrollViewScope(scrollPosition);
                scrollPosition = scope.scrollPosition;
                using (scope)
                { 
                    foreach (Component component in test.defaultPrefab.GetComponents<Component>())
                    {
                        Editor ed = Editor.CreateEditor(component);
                        if (!expanded.ContainsKey(component)) expanded.Add(component, true);
                        expanded[component] = EditorGUILayout.InspectorTitlebar(expanded[component], ed);

                        if (expanded[component])
                        {
                            using (new EditorGUILayout.VerticalScope(EditorStyles.inspectorDefaultMargins, GUILayout.ExpandWidth(true))) ed.OnInspectorGUI();
                            EditorGUILayout.Space();
                        }
                        DestroyImmediate(ed);
                    }
                }
            }
        }

        private void DrawFooter()
        {
            using (new EditorGUILayout.HorizontalScope(Style.Get("Settings/Footer")))
            {
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Reset"))
                {
                    if (!EditorUtility.DisplayDialog("Reset this test?", "This action cannot be undone.",
                        "Yes", "No"
                    )) return;
                    test.DestroyDefaultPrefab();
                    Init(test);
                    if (Logger.debug.HasFlag(Logger.DebugMode.Log)) Logger.Log("Reset " + test.attribute.GetPath());
                    GUIUtility.ExitGUI();
                }
            }
        }
    }
}
