using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace GameTest
{
    public class PreferencesWindow : EditorWindow
    {
        private TestManager manager;

        [SerializeField] private Vector2 scrollPosition;

        [SerializeField] private State currentState;
        [SerializeField] State previousState;

        [System.Serializable]
        private class State
        {
            public string dataPath;
            public TestManagerUI.TestSortOrder testSortOrder;

            public State(string dataPath, TestManagerUI.TestSortOrder testSortOrder)
            {
                this.dataPath = dataPath;
                this.testSortOrder = testSortOrder;
            }

            public State(TestManager manager)
            {
                dataPath = manager.GetDataPath();
                testSortOrder = manager.testSortOrder;
            }

            public State(State other)
            {
                dataPath = other.dataPath;
                testSortOrder = other.testSortOrder;
            }

            public override bool Equals(object other)
            {
                if (other == null) return false;
                if (other.GetType() != typeof(State)) return false;
                State o = other as State;
                return 
                    dataPath.Equals(o.dataPath) && 
                    testSortOrder.Equals(o.testSortOrder)
                ;
            }

            public override int GetHashCode() => System.HashCode.Combine(dataPath);
        }

        #region Unity events
        void OnEnable()
        {
            Load();
        } 
        #endregion

        #region Persistence
        private void Load()
        {
            manager = TestManager.Get();
            if (manager == null) throw new System.NullReferenceException("Failed to find a TestManager. The preferences window cannot be opened if the TestManagerUI is not opened");

            currentState = new State(manager);
            previousState = new State(manager);
        }

        private void Save()
        {
            if (manager == null) throw new System.NullReferenceException("manager is null. This is likely because the Load method was not called before calling Save.");
            
            try
            {
                manager.SetDataPath(currentState.dataPath);
                manager.testSortOrder = currentState.testSortOrder;
            }
            catch (System.Exception e)
            {
                Logger.LogError("Failed to save. " + e.Message);
                return;
            }

            previousState = new State(currentState);

            if (EditorWindow.HasOpenInstances<TestManagerUI>())
            {
                EditorWindow.GetWindow<TestManagerUI>().Repaint();
            }

            Logger.Log("Preferences saved");
        }

        public override void SaveChanges()
        {
            base.SaveChanges();
            Save();
        }

        private void Reset()
        {
            currentState = new State(
                Utilities.GetUnityPath(Utilities.defaultDataPath),
                TestManagerUI.TestSortOrder.Name
            );
        }
        #endregion

        #region UI
        public static PreferencesWindow ShowWindow()
        {
            PreferencesWindow window = EditorWindow.GetWindow<PreferencesWindow>(true, nameof(GameTest) + " Preferences", true);
            return window;
        }

        /// <summary>
        /// Close all Preferences windows
        /// </summary>
        public static void CloseAll(bool save = false)
        {
            PreferencesWindow window = null;
            if (HasOpenInstances<PreferencesWindow>()) window = GetWindow<PreferencesWindow>();
            while (window != null)
            {
                if (save) window.Save();
                window.Close();
                window = null;
                if (HasOpenInstances<PreferencesWindow>()) window = GetWindow<PreferencesWindow>();
            }
        }

        void OnGUI()
        {
            using (new EditorGUILayout.VerticalScope())
            {
                DrawPanel();
                DrawControlButtons();
            }
            hasUnsavedChanges = !currentState.Equals(previousState);
        }

        private void DrawPanel()
        {
            EditorGUILayout.ScrollViewScope scope = new EditorGUILayout.ScrollViewScope(
                scrollPosition
            );
            using (scope)
            {
                currentState.dataPath = EditorGUILayout.TextField(
                    new GUIContent("Data Path", "Where information about your tests and the UI get stored, including test default prefabs."),
                    currentState.dataPath
                );
                currentState.testSortOrder = (TestManagerUI.TestSortOrder)EditorGUILayout.EnumPopup(
                    new GUIContent("Test Sort Order", "If a script has multiple tests, this is the order they will be sorted in the UI."), 
                    currentState.testSortOrder
                );
            }
            scrollPosition = scope.scrollPosition;
        }

        private void DrawControlButtons()
        {
            GUIStyle style = new GUIStyle(GUI.skin.box);
            style.padding.left = 0;
            style.padding.right = 0;
            style.margin.left = 0;
            style.margin.right = 0;
            style.border.left = 0;
            style.border.right = 0;
            style.margin.bottom = 0;
            style.border.bottom = 0;
            using (new EditorGUILayout.HorizontalScope(style))
            {
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Reset")) Reset();
                if (GUILayout.Button("Save")) Save();
            }
        }
        #endregion
    }
}