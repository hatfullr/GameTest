using System.Collections.Generic;
using UnityEditor;
using UnityEngine;


namespace UnityTest
{
    /// <summary>
    /// The window that pops up when a settings cog button is clicked
    /// </summary>
    [System.Serializable]
    public class SettingsWindow : EditorWindow
    {
        private Test test;
        private bool visible = false;

        private const float flashInterval = 0.25f;
        private const int nFlashes = 1;
        private int nFlashed = 0;
        private float flashStart = 0f;

        private Editor prefabEditor;
        private SerializedProperty prefab;
        private SerializedObject serializedObject;

        private Dictionary<Component, bool> expanded = new Dictionary<Component, bool>();

        private Vector2 scrollPosition;
        

        public static SettingsWindow Init()
        {
            SettingsWindow window = EditorWindow.GetWindow<SettingsWindow>(true);
            return window;
        }

        void Awake()
        {
            minSize = new Vector2(
                EditorGUIUtility.labelWidth + EditorGUIUtility.fieldWidth,
                minSize.y
            );
        }

        public void SetTest(Test test)
        {
            titleContent = new GUIContent(test.attribute.name + " (Settings)");
            this.test = test;
            prefabEditor = Editor.CreateEditor(this.test.GetDefaultPrefab());
            expanded = new Dictionary<Component, bool>();
            serializedObject = new SerializedObject(this.test);
            prefab = serializedObject.FindProperty(nameof(test.prefab));
        }

        public Test GetTest() => test;

        public void SetVisible(bool value)
        {
            if (visible == value)
            {
                if (visible) Flash();
                return;
            }
            if (value) ShowUtility();
            else Close();
            visible = value;
        }

        public void Flash()
        {
            flashStart = Time.realtimeSinceStartup;
            nFlashed = -1;
            EditorApplication.update += _Flash;
        }

        private void _Flash()
        {
            int i = (int)((Time.realtimeSinceStartup - flashStart) / (0.5f * flashInterval));
            if (i < nFlashes * 2)
            {
                if (i > nFlashed)
                {
                    nFlashed = i;
                }
                else return;

                // We know that the TestManagerUI window is visible, because this method is called when the user clicks the
                // settings cog in the window.
                if (i % 2 == 0) Focus();
                else EditorWindow.GetWindow<TestManagerUI>().Focus();
            }
            else
            {
                EditorApplication.update -= _Flash;
                Focus();
            }
        }


        void OnGUI()
        {
            if (test == null)
            {
                SetVisible(false);
                return;
            }

            if (Event.current.type == EventType.MouseDown && position.Contains(GUIUtility.GUIToScreenPoint(Event.current.mousePosition)))
            {
                GUI.FocusControl(null);
                Repaint();
            }

            if (serializedObject == null || prefabEditor == null || prefab == null) return;

            serializedObject.Update();
            using (new EditorGUILayout.VerticalScope())
            {
                DrawHeader();
                DrawInspector();
                DrawFooter();
            }
            serializedObject.ApplyModifiedProperties();
        }

        private void DrawHeader()
        {
            using (new EditorGUILayout.VerticalScope(Style.Get("Settings/Header")))
            {
                EditorGUILayout.PropertyField(prefab, new GUIContent(prefab.displayName, Style.Tooltips.settingsWindowPrefab));
            }
        }

        private void DrawInspector()
        {
            using (new EditorGUI.DisabledGroupScope(prefab.objectReferenceValue != null))
            {
                prefabEditor.DrawHeader();

                EditorGUILayout.ScrollViewScope scope = new EditorGUILayout.ScrollViewScope(scrollPosition);
                scrollPosition = scope.scrollPosition;
                using (scope)
                {
                    foreach (Component component in (prefabEditor.target as GameObject).GetComponents<Component>())
                    {
                        Editor ed = Editor.CreateEditor(component);

                        if (!expanded.ContainsKey(component)) expanded.Add(component, true);
                        expanded[component] = EditorGUILayout.InspectorTitlebar(expanded[component], ed);

                        if (expanded[component])
                        {
                            using (new EditorGUILayout.VerticalScope(EditorStyles.inspectorDefaultMargins, GUILayout.ExpandWidth(true))) ed.OnInspectorGUI();
                            EditorGUILayout.Space();
                        }
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
                    prefab.objectReferenceValue = null;
                    test.CreateDefaultPrefab();
                    if (TestManager.GetDebugMode().HasFlag(TestManager.DebugMode.Log)) Utilities.Log("Reset " + test.attribute.GetPath());
                }
            }
        }
    }
}
