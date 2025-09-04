using UnityEditor.SceneManagement;
using UnityEditor;
using UnityEngine;

namespace GameTest
{
    public class TestManagerToolbar
    {
        public static void Draw(TestManagerUI ui)
        {
            using (new EditorGUILayout.HorizontalScope(Style.Get("TestManagerUI/Toolbar")))
            {
                // Left
                DrawPlayButton(ui.manager);
                DrawPauseButton(ui.manager);
                DrawSkipButton(ui.manager);
                DrawGoToEmptySceneButton();
                DrawClearButton(ui);

                // Left
                GUILayout.FlexibleSpace();
                // Right

                DrawSearchBar(ui);
                DrawWelcomeButton(ui.manager);
                DrawTestVisibilityButton(ui);
                DrawDebugButton(ui.manager);
                DrawRefreshButton(ui);
                // Right
            }
        }

        public static void DrawPlayButton(TestManager manager)
        {
            GUIContent content = Style.GetIcon("TestManagerUI/Toolbar/Play/Off");
            if (manager.running) content = Style.GetIcon("TestManagerUI/Toolbar/Play/On");

            bool current;
            using (new EditorGUI.DisabledScope(manager.queue.Count == 0 && Test.current == null))
            {
                current = GUILayout.Toggle(manager.running, content, Style.Get("TestManagerUI/Toolbar/Play"));
            }

            if (manager.running != current) // The user clicked on the button
            {
                if (manager.running) manager.Stop();
                else manager.RequestStart();
            }
        }

        public static void DrawPauseButton(TestManager manager)
        {
            GUIContent content = Style.GetIcon("TestManagerUI/Toolbar/Pause/Off");
            if (manager.paused) content = Style.GetIcon("TestManagerUI/Toolbar/Pause/On");

            bool wasPaused = manager.paused;
            manager.paused = GUILayout.Toggle(manager.paused, content, Style.Get("TestManagerUI/Toolbar/Pause"));
            if (wasPaused && !manager.paused && manager.running)
            {
                manager.RunNext();
            }
        }

        public static void DrawSkipButton(TestManager manager)
        {
            GUIContent content = Style.GetIcon("TestManagerUI/Toolbar/Skip");
            using (new EditorGUI.DisabledScope(!manager.running))
            {
                if (GUILayout.Button(content, Style.Get("TestManagerUI/Toolbar/Skip"))) manager.Skip();
            }
        }

        public static void DrawGoToEmptySceneButton()
        {
            using (new EditorGUI.DisabledScope(Utilities.IsSceneEmpty() || EditorApplication.isPlaying))
            {
                if (GUILayout.Button(Style.GetIcon("TestManagerUI/Toolbar/GoToEmptyScene"), Style.Get("TestManagerUI/Toolbar/GoToEmptyScene")))
                {
                    EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
                    Logger.Log("Entered an empty scene");
                    GUIUtility.ExitGUI();
                }
            }
        }

        public static void DrawDebugButton(TestManager manager)
        {
            System.Array values = System.Enum.GetValues(typeof(Logger.DebugMode));

            bool hasNothing = true;
            bool hasEverything = true;
            bool hasAnything = false;

            foreach (Logger.DebugMode mode in values)
            {
                if (manager.debug.HasFlag(mode))
                {
                    hasNothing = false;
                    hasAnything = true;
                }
                else hasEverything = false;
            }

            GUIContent debugContent = Style.GetIcon("TestManagerUI/Toolbar/Debug/Off");
            if (hasAnything) debugContent = Style.GetIcon("TestManagerUI/Toolbar/Debug/On");

            void ClearFlags()
            {
                foreach (Logger.DebugMode mode in values) manager.debug &= ~mode;
                Logger.debug = manager.debug;
            }
            void SetAllFlags()
            {
                foreach (Logger.DebugMode mode in values) manager.debug |= mode;
                Logger.debug = manager.debug;
            }

            Rect rect = Style.GetRect("TestManagerUI/Toolbar/Debug", debugContent);
            if (EditorGUI.DropdownButton(rect, debugContent, FocusType.Passive, Style.Get("TestManagerUI/Toolbar/Clear")))
            {
                GenericMenu toolsMenu = new GenericMenu();

                if (hasNothing) toolsMenu.AddDisabledItem(new GUIContent("Nothing"), true);
                else toolsMenu.AddItem(new GUIContent("Nothing"), hasNothing, ClearFlags);

                if (hasEverything) toolsMenu.AddDisabledItem(new GUIContent("Everything"), true);
                else toolsMenu.AddItem(new GUIContent("Everything"), hasEverything, SetAllFlags);

                foreach (Logger.DebugMode mode in System.Enum.GetValues(typeof(Logger.DebugMode)))
                {
                    toolsMenu.AddItem(new GUIContent(mode.ToString()), manager.debug.HasFlag(mode), () =>
                    {
                        if (manager.debug.HasFlag(mode))
                        {
                            manager.debug &= ~mode;
                            Logger.debug = manager.debug;
                        }
                        else
                        {
                            manager.debug |= mode;
                            Logger.debug = manager.debug;
                        }
                    });
                }

                toolsMenu.DropDown(rect);
            }
        }

        public static void DrawRefreshButton(TestManagerUI ui)
        {
            if (GUILayout.Button(Style.GetIcon("TestManagerUI/Toolbar/Refresh"), Style.Get("TestManagerUI/Toolbar/Refresh")))
                ui.Refresh();
        }

        public static void DrawWelcomeButton(TestManager manager)
        {
            manager.showWelcome = GUILayout.Toggle(manager.showWelcome, Style.GetIcon("TestManagerUI/Toolbar/Welcome"), Style.Get("TestManagerUI/Toolbar/Welcome"));
        }

        public static void DrawClearButton(TestManagerUI ui)
        {
            bool selectedHaveResults = false;
            bool anyResults = false;
            foreach (Test test in ui.manager.GetTests())
            {
                if (test.result == Test.Result.None) continue;
                anyResults = true;
                if (test.selected)
                {
                    selectedHaveResults = true;
                    break;
                }
            }

            using (new EditorGUI.DisabledScope(!anyResults))
            {
                GUIContent clear = Style.GetIcon("TestManagerUI/Toolbar/Clear");
                Rect clearRect = Style.GetRect("TestManagerUI/Toolbar/Clear", clear);
                if (EditorGUI.DropdownButton(clearRect, clear, FocusType.Passive, Style.Get("TestManagerUI/Toolbar/Clear")))
                {
                    GenericMenu toolsMenu = new GenericMenu();
                    if (selectedHaveResults) toolsMenu.AddItem(new GUIContent("Reset Selected"), false, ui.ResetSelected);
                    else toolsMenu.AddDisabledItem(new GUIContent("Reset Selected"));

                    if (anyResults) toolsMenu.AddItem(new GUIContent("Reset All"), false, ui.ResetAll);
                    else toolsMenu.AddDisabledItem(new GUIContent("Reset All"));

                    toolsMenu.DropDown(clearRect);
                }
            }
        }


        /// <summary>
        /// Draw the search bar, using the min and max widths defined in Utilities.searchBarMinWidth and Utilities.searchBarMaxWidth.
        /// </summary>
        public static void DrawSearchBar(TestManagerUI ui)
        {
            if (ui.searchField == null) return;
            string newSearch = ui.searchField.OnToolbarGUI(ui.manager.search, GUILayout.MinWidth(Utilities.searchBarMinWidth), GUILayout.MaxWidth(Utilities.searchBarMaxWidth));

            Rect rect = GUILayoutUtility.GetLastRect();
            if (Utilities.IsMouseButtonReleased() && !(Utilities.IsMouseOverRect(rect) && GUI.enabled)) EditorGUI.FocusTextInControl(null);

            if (ui.manager.search != newSearch) ui.manager.UpdateSearchMatches(ui, newSearch);
        }
    
        public static void DrawTestVisibilityButton(TestManagerUI ui)
        {
            GUIContent content = Style.GetIcon("TestManagerUI/Toolbar/TestVisibility/Off");
            if (ui.manager.testsVisible) content = Style.GetIcon("TestManagerUI/Toolbar/TestVisibility/On");
            
            if (ui.manager.testsVisible != GUILayout.Toggle(ui.manager.testsVisible, content, Style.Get("TestManagerUI/Toolbar/TestVisibility")))
                ui.manager.ToggleTestVisibility();
        }
    }
}