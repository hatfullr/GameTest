using UnityEditor;
using UnityEngine;


namespace UnityTest
{
    /// <summary>
    /// The window that pops up when a settings cog button is clicked for a Suite
    /// </summary>
    public class Settings : EditorWindow
    {
        private Test test;
        private Suite currentSuite;
        private Editor suiteEditor;
        private bool visible = false;

        private const float flashInterval = 0.25f;
        private const int nFlashes = 1;
        private int nFlashed = 0;
        private float flashStart = 0f;

        void Awake()
        {
            minSize = new Vector2(
                EditorGUIUtility.labelWidth + EditorGUIUtility.fieldWidth,
                minSize.y
            );
        }

        public void SetTest(Test test)
        {
            if (test == this.test) return;

            // Search the Data for this test's class
            currentSuite = Suite.Get(test.method.DeclaringType);
            suiteEditor = Editor.CreateEditor(currentSuite);

            this.test = test;
            titleContent = new GUIContent(test.method.DeclaringType.Name + " (Settings)");
        }

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

        //public void Toggle() => SetVisible(!visible);

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

            suiteEditor.OnInspectorGUI();

        }
    }
}
