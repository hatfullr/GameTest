using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Linq;
using System.Text.RegularExpressions;
using static UnityTest.TestManager;

namespace UnityTest
{
    public class TestManagerUI : EditorWindow, IHasCustomMenu
    {
        [SerializeField] private GUIQueue _guiQueue;
        [SerializeField] private Foldout _rootFoldout;
        [SerializeField] public List<Foldout> foldouts = new List<Foldout>();
        [SerializeField] private Vector2 scrollPosition;
        [SerializeField] private bool refreshing = false;
        [SerializeField] private bool showWelcome = true;
        [SerializeField] private bool loadingWheelVisible = false;
        [SerializeField] private string search = null;

        public float indentWidth;
        public int indentLevel;
        private float spinStartTime = 0f;
        private int spinIndex = 0;
        private UnityEditor.IMGUI.Controls.SearchField searchField;

        public System.Action onLostFocus, onFocus;

        private TestManager _manager;
        public TestManager manager
        {
            get
            {
                if (_manager == null) _manager = Utilities.CreateAsset<TestManager>(TestManager.fileName, Utilities.dataPath);
                return _manager;
            }
        }

        private GUIQueue guiQueue
        {
            get
            {
                if (_guiQueue == null) _guiQueue = Utilities.CreateAsset<GUIQueue>(GUIQueue.fileName, Utilities.dataPath);
                return _guiQueue;
            }
        }

        private Foldout rootFoldout
        {
            get
            {
                if (_rootFoldout == null) _rootFoldout = Utilities.CreateAsset<Foldout>("rootFoldout", Utilities.foldoutDataPath);
                return _rootFoldout;
            }
        }

        private Settings _settings;
        public Settings settings
        {
            get
            {
                if (_settings == null) _settings = GetWindow<Settings>(true, null, true);
                return _settings;
            }
        }

        #region Unity UI
        public void AddItemsToMenu(GenericMenu menu)
        {
            menu.AddItem(new GUIContent("Reset"), false, ShowResetConfirmation);
        }

        [MenuItem("Window/UnityTest Manager")]
        public static void ShowWindow()
        {
            TestManagerUI window = GetWindow<TestManagerUI>(Style.TestManagerUI.windowTitle);
            window.minSize = new Vector2(Style.TestManagerUI.minWidth, Style.TestManagerUI.minHeight);
        }
        #endregion Unity UI


        #region Events
        /// <summary>
        /// Called after ShowWindow but before OnEnable, and only when the window is opened.
        /// </summary>
        void Awake()
        {
            // Only do a refresh if we think this is the first time the window has been opened.
            if (foldouts.Count == 0) Refresh();
        }

        /// <summary>
        /// Called before AssemblyReloadEvents.afterAssemblyReload, and whenever the user opens the window.
        /// </summary>
        void OnEnable()
        {
            searchField = new UnityEditor.IMGUI.Controls.SearchField(); // Unity demands we do this in OnEnable and nowhere else

            // Clear these events out if they are already added
            AssemblyReloadEvents.afterAssemblyReload -= OnAfterAssemblyReload;
            EditorApplication.playModeStateChanged -= OnPlayStateChanged;

            AssemblyReloadEvents.afterAssemblyReload += OnAfterAssemblyReload;
            EditorApplication.playModeStateChanged += OnPlayStateChanged;

            manager.onStop -= OnTestManagerFinished;
            manager.onStop += OnTestManagerFinished;
        }

        /// <summary>
        /// Called when the window is closed, before OnDestroy(), as well as right before assembly reload.
        /// </summary>
        void OnDisable()
        {
            AssemblyReloadEvents.afterAssemblyReload -= OnAfterAssemblyReload;
            EditorApplication.playModeStateChanged -= OnPlayStateChanged;

            manager.onStop -= OnTestManagerFinished;
        }

        private void OnAfterAssemblyReload()
        {
            Refresh();

            if (manager.running && manager.queue.Count == 0) manager.Start();
        }

        private void OnPlayStateChanged(PlayModeStateChange change)
        {
            manager.OnPlayStateChanged(change);

            if (change == PlayModeStateChange.EnteredPlayMode && Utilities.IsSceneEmpty()) Focus();

            // If we don't Repaint() here, then the toolbar buttons can appear incorrect.
            if (change == PlayModeStateChange.EnteredEditMode) Repaint();
        }

        /// <summary>
        /// Called when the TestManager has finished all queued tests and exited Play mode. In this method, we repopulate the queue with the
        /// currently selected Tests.
        /// </summary>
        private void OnTestManagerFinished()
        {
            foreach (Test test in manager.tests)
            {
                if (test.selected) manager.AddToQueue(test);  // manager.queue.Enqueue(test);
            }
        }

        /// <summary>
        /// Called only when the window is closed.
        /// </summary>
        void OnDestroy()
        {
            //Save();
        }

        void OnLostFocus()
        {
            if (onLostFocus != null) onLostFocus();
        }

        void OnFocus()
        {
            if (onFocus != null) onFocus();
        }

        [HideInCallstack]
        void Update()
        {
            if (loadingWheelVisible)
            {
                Repaint();
                return;
            }
            if (!EditorApplication.isPlaying) return;
            manager.Update();
            if (manager.running) Repaint(); // keeps the frame counter and timer up-to-date
        }
        #endregion Events


        #region Methods
        private void DoReset()
        {
            // Delete all data associated with UnityTest
            Utilities.DeleteFolder(Utilities.dataPath);

            manager.Reset();

            foldouts = new List<Foldout>();
            scrollPosition = default;
            refreshing = false;
            showWelcome = true;
            loadingWheelVisible = false;
            search = default;

            _settings = default;
            indentLevel = default;
            indentWidth = default;
            spinStartTime = default;
            spinIndex = default;

            Refresh();

            Utilities.Log("Reset " + Style.TestManagerUI.windowTitle);
        }

        private void Refresh()
        {
            refreshing = true;
            Repaint();

            manager.UpdateTests();

            foreach (Test test in manager.tests)
            {
                Foldout final = null;
                foreach (string p in Utilities.IterateDirectories(test.attribute.GetPath(), true))
                {
                    Foldout found = null;
                    foreach (Foldout foldout in foldouts)
                    {
                        if (foldout.path == p)
                        {
                            found = foldout;
                            break;
                        }
                    }

                    if (found == null)
                    {
                        found = Utilities.SearchForAsset<Foldout>((Foldout f) => f.path == p, Utilities.foldoutDataPath, false);
                        if (found == null) // Failed to find a matching asset, so create a new one now
                        {
                            found = Utilities.CreateAsset<Foldout>(System.Guid.NewGuid().ToString(), Utilities.foldoutDataPath, (Foldout newFoldout) =>
                            {
                                newFoldout.path = p;
                            });
                        }
                        foldouts.Add(found);
                    }
                    final = found;
                }
                if (final.tests.Contains(test)) continue;
                final.tests.Add(test);
            }
            Repaint();
            refreshing = false;
        }

        private void ResetSelected()
        {
            foreach (Test test in rootFoldout.GetTests())
                if (test.selected) test.Reset();
        }
        private void ResetAll()
        {
            foreach (Test test in manager.tests) test.Reset();
        }
        #endregion Methods


        #region Persistence
        /// <summary>
        /// Save current data to Assets/UnityTest/Data
        /// </summary>
        private void Save()
        {
            Utilities.MarkAssetsForSave(guiQueue, manager, rootFoldout, this);
            //Utilities.SaveDirtyAssets(guiQueue, manager, rootFoldout, this);
            //if (foldouts != null)
            //    foreach (Foldout foldout in foldouts)
            //        Utilities.SaveAsset(foldout);
        }
        #endregion Persistence


        #region UI
        void OnGUI()
        {
            Utilities.isDarkTheme = GUI.skin.name == "DarkSkin";
            State state = new State(rootFoldout);

            bool refresh = false;
            bool selectAll = false;
            bool deselectAll = false;

            // The main window
            EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            {
                using (new EditorGUI.DisabledScope(loadingWheelVisible))
                {
                    // Toolbar controls
                    EditorGUILayout.BeginHorizontal(Style.Get("TestManagerUI/Toolbar"));
                    {
                        // Left
                        DrawPlayButton(state);
                        DrawPauseButton(state);
                        DrawSkipButton();
                        DrawGoToEmptySceneButton();

                        using (new EditorGUI.DisabledScope(foldouts.Count == 0 || manager.tests.Count == 0))
                        {
                            bool[] result = DrawUniversalToggle(state);
                            selectAll = result[0];
                            deselectAll = result[1];
                        }

                        DrawClearButton(state);

                        // Left
                        GUILayout.FlexibleSpace();
                        // Right

                        DrawSearchBar();
                        DrawWelcomeButton();
                        DrawDebugButton();
                        refresh = DrawRefreshButton();
                        // Right
                    }
                    EditorGUILayout.EndHorizontal();
                }

                if (loadingWheelVisible)
                {
                    Rect rect = EditorGUILayout.BeginVertical(GUILayout.ExpandHeight(true), GUILayout.ExpandWidth(true));
                    {
                        string text = "Finding tests";
                        if (refreshing) text = "Refreshing";
                        DrawLoadingWheel(rect, text);
                    }
                    EditorGUILayout.EndVertical();
                }
                else
                {
                    // The box that shows the Foldouts and Tests
                    scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, false, false, GUI.skin.horizontalScrollbar, GUI.skin.verticalScrollbar, Style.Get("TestManagerUI/TestView"));
                    {
                        if (showWelcome) // Welcome message
                        {
                            GUIContent content = Style.GetIcon("TestManagerUI/Welcome");
                            GUIContent donate = Style.GetIcon("TestManagerUI/Donate");
                            GUIStyle donateStyle = Style.Get("TestManagerUI/Donate");

                            content.text = Style.welcomeMessage + "\n\n";

                            GUILayout.Box(content, Style.Get("TestManagerUI/Welcome"));
                            Rect rect = GUILayoutUtility.GetLastRect();
                            float height = donateStyle.CalcSize(donate).y;
                            float width = Style.GetWidth(donateStyle, donate);
                            rect.y += rect.height - height - donateStyle.margin.bottom;
                            rect.height = height;
                            rect.x += rect.width - width - donateStyle.margin.right;
                            rect.width = width;
                            if (GUI.Button(rect, donate)) Application.OpenURL(Style.donationLink);

                        }

                        if (string.IsNullOrEmpty(search)) DrawNormalMode();
                        else DrawSearchMode();
                    }
                    EditorGUILayout.EndScrollView();
                }
            }
            EditorGUILayout.EndVertical();

            if (!manager.running) // Otherwise stuff will keep being added into the queue during testing time
            {
                foreach (Test test in manager.tests)
                {
                    if (test.selected && !manager.queue.Contains(test)) manager.AddToQueue(test);
                    else if (manager.queue.Contains(test) && !test.selected) manager.queue.Remove(test);
                }
            }

            guiQueue.Draw();

            if (!loadingWheelVisible)
            {
                // Doing things this way avoids annoying GUI errors complaining about groups not being ended properly.
                if (refresh) Refresh();
                else if (selectAll) rootFoldout.Select(); // Simulate a press
                else if (deselectAll) rootFoldout.Deselect(); // Simulate a press
            }
        }

        /// <summary>
        /// Draw the tests as nested foldouts in a hierarchy according to their individual paths.
        /// </summary>
        private void DrawNormalMode()
        {
            indentLevel = 0;
            foreach (Foldout child in rootFoldout.GetChildren(false)) child.Draw();
        }

        /// <summary>
        /// Shows the tests as their full paths when text is present in the search bar. Only shows the tests matching the search regex.
        /// </summary>
        private void DrawSearchMode()
        {
            indentLevel = 0;

            Regex re = new Regex(search, RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.IgnorePatternWhitespace);

            string path, final;
            MatchCollection matches;
            Rect rect;
            foreach (Test test in rootFoldout.GetTests().OrderBy(x => x.attribute.GetPath()))
            {
                path = test.attribute.GetPath();
                matches = re.Matches(path);
                if (matches.Count == 0) continue;

                // Modify the color or something of the regex matches to show where the matches happened
                final = "";
                for (int i = 0; i < matches.Count; i++)
                {
                    if (i == 0) final += path[..matches[i].Index];
                    else final += path[(matches[i - 1].Index + matches[i - 1].Length)..matches[i].Index];
                    final += "<b>" + path[matches[i].Index..(matches[i].Index + matches[i].Length)] + "</b>";
                }
                final += path[(matches[matches.Count - 1].Index + matches[matches.Count - 1].Length)..];
                rect = EditorGUILayout.GetControlRect(false);
                rect = PaintResultFeatures(rect, test);
                DrawTest(rect, test, true, !test.IsInSuite(), true, final);
            }
        }

        /// <summary>
        /// Draw the search bar, using the min and max widths defined in Utilities.searchBarMinWidth and Utilities.searchBarMaxWidth.
        /// </summary>
        private void DrawSearchBar()
        {
            if (searchField == null) return;
            string newSearch = searchField.OnToolbarGUI(search, GUILayout.MinWidth(Utilities.searchBarMinWidth), GUILayout.MaxWidth(Utilities.searchBarMaxWidth));

            Rect rect = GUILayoutUtility.GetLastRect();
            if (Utilities.IsMouseButtonReleased() && !Utilities.IsMouseOverRect(rect)) EditorGUI.FocusTextInControl(null);

            search = newSearch;
        }

        /// <summary>
        /// Draw the loading wheel shown during a refresh and whenever the assemblies are being checked for tests.
        /// </summary>
        private void DrawLoadingWheel(Rect rect, string text)
        {
            float time = Time.realtimeSinceStartup;
            if (time - spinStartTime >= Style.TestManagerUI.spinRate)
            {
                spinIndex++;
                spinStartTime = time;
            }

            GUIContent content;
            try { content = Style.GetIcon("TestManagerUI/LoadingWheel/" + spinIndex, text); }
            catch (System.NotImplementedException)
            {
                spinIndex = 0;
                content = Style.GetIcon("TestManagerUI/LoadingWheel/" + spinIndex, text);
            }
            GUI.Label(rect, content, Style.Get("TestManagerUI/LoadingWheel"));
        }

        private bool[] DrawUniversalToggle(State state)
        {
            // This is all just to draw the toggle button in the toolbar
            GUIContent toggle = Style.GetIcon("TestManagerUI/Toolbar/Toggle/On"); // guess at initial value
            GUIStyle toggleStyle = Style.Get("TestManagerUI/Toolbar/Toggle/On");

            Rect rect = GUILayoutUtility.GetRect(toggle, toggleStyle);

            bool hover = Utilities.IsMouseOverRect(rect);

            // Change the style of the toggle button according to the current state
            if (state.anySelected && !state.allSelected)
            {
                if (hover) toggle = Style.GetIcon("TestManagerUI/Toolbar/Toggle/Mixed/Hover");
                else toggle = Style.GetIcon("TestManagerUI/Toolbar/Toggle/Mixed");
            }
            else
            {
                if (state.allSelected)
                {
                    if (hover) toggle = Style.GetIcon("TestManagerUI/Toolbar/Toggle/On/Hover");
                }
                else if (!state.anySelected)
                {
                    if (hover) toggle = Style.GetIcon("TestManagerUI/Toolbar/Toggle/Off/Hover");
                    else toggle = Style.GetIcon("TestManagerUI/Toolbar/Toggle/Off");
                }
            }

            toggle.tooltip = "Select/deselect all unlocked tests";

            bool wasMixed = EditorGUI.showMixedValue;
            EditorGUI.showMixedValue = state.anySelected && !state.allSelected;
            bool choice = GUI.Button(rect, toggle, toggleStyle);
            EditorGUI.showMixedValue = wasMixed;

            if (choice) return new bool[2] { !state.allSelected, state.anySelected };
            return new bool[2] { false, false };
        }

        private void DrawClearButton(State state)
        {
            using (new EditorGUI.DisabledScope(!state.selectedHaveResults))
            {
                GUIContent clear = Style.GetIcon("TestManagerUI/Toolbar/Clear");
                Rect clearRect = Style.GetRect("TestManagerUI/Toolbar/Clear", clear);
                if (EditorGUI.DropdownButton(clearRect, clear, FocusType.Passive, Style.Get("TestManagerUI/Toolbar/Clear")))
                {
                    GenericMenu toolsMenu = new GenericMenu();
                    if (state.anySelected) toolsMenu.AddItem(new GUIContent("Reset Selected"), false, ResetSelected);
                    else toolsMenu.AddDisabledItem(new GUIContent("Reset Selected"));

                    if (state.anyResults) toolsMenu.AddItem(new GUIContent("Reset All"), false, ResetAll);
                    else toolsMenu.AddDisabledItem(new GUIContent("Reset All"));

                    toolsMenu.DropDown(clearRect);
                }
            }
        }



        private void DrawPlayButton(State state)
        {
            GUIContent content = Style.GetIcon("TestManagerUI/Toolbar/Play/Off");
            if (manager.running) content = Style.GetIcon("TestManagerUI/Toolbar/Play/On");

            bool current;
            using (new EditorGUI.DisabledScope(!state.anySelected))
            {
                current = GUILayout.Toggle(manager.running, content, Style.Get("TestManagerUI/Toolbar/Play"));
            }

            if (manager.running != current) // The user clicked on the button
            {
                if (manager.running)
                {
                    manager.Stop();
                }
                else
                {
                    manager.Start();
                }
            }
        }

        private void DrawPauseButton(State state)
        {
            GUIContent content = Style.GetIcon("TestManagerUI/Toolbar/Pause/Off");
            if (manager.paused) content = Style.GetIcon("TestManagerUI/Toolbar/Pause/On");

            using (new EditorGUI.DisabledScope(!manager.running && !state.anySelected))
            {
                manager.paused = GUILayout.Toggle(manager.paused, content, Style.Get("TestManagerUI/Toolbar/Pause"));
            }
        }

        private void DrawSkipButton()
        {
            GUIContent content = Style.GetIcon("TestManagerUI/Toolbar/Skip");
            using (new EditorGUI.DisabledScope(!manager.running))
            {
                if (GUILayout.Button(content, Style.Get("TestManagerUI/Toolbar/Skip"))) manager.Skip();
            }
        }

        private void DrawGoToEmptySceneButton()
        {
            using (new EditorGUI.DisabledScope(Utilities.IsSceneEmpty() || EditorApplication.isPlaying))
            {
                if (GUILayout.Button(Style.GetIcon("TestManagerUI/Toolbar/GoToEmptyScene"), Style.Get("TestManagerUI/Toolbar/GoToEmptyScene")))
                {
                    EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
                    Utilities.Log("Entered an empty scene");
                    GUIUtility.ExitGUI();
                }
            }
        }

        private void DrawDebugButton()
        {
            GUIContent debugContent = Style.GetIcon("TestManagerUI/Toolbar/Debug/Off");
            if (manager.debug) debugContent = Style.GetIcon("TestManagerUI/Toolbar/Debug/On");
            manager.debug = GUILayout.Toggle(manager.debug, debugContent, Style.Get("TestManagerUI/Toolbar/Debug"));
        }

        private bool DrawRefreshButton()
        {
            return GUILayout.Button(Style.GetIcon("TestManagerUI/Toolbar/Refresh"), Style.Get("TestManagerUI/Toolbar/Refresh"));
        }

        private void DrawWelcomeButton()
        {
            showWelcome = GUILayout.Toggle(showWelcome, Style.GetIcon("TestManagerUI/Toolbar/Welcome"), Style.Get("TestManagerUI/Toolbar/Welcome"));
        }

        /// <summary>
        /// Say "are you sure?"
        /// </summary>
        private void ShowResetConfirmation()
        {
            if (!EditorUtility.DisplayDialog("Reset UnityTest Manager?", "Are you sure? This will clear all saved information about tests, GameObjects, etc. " +
                "If you have encountered a bug, first try closing the UnityTest Manager and opening it again.",
                "Yes", "No"
            )) return;
            // User clicked "OK"
            DoReset();
        }

        private void StartLoadingWheel()
        {
            spinStartTime = Time.realtimeSinceStartup;
            loadingWheelVisible = true;
        }

        private void StopLoadingWheel()
        {
            loadingWheelVisible = false;
        }




        #region Tests

        /// <summary>
        /// Show only the background color change and the result status icon on the right. The clear result button is not drawn.
        /// </summary>
        public static Rect PaintResultFeatures(Rect rect, Test.Result result)
        {
            GUIContent image = Style.GetIcon("Test/Result/" + result.ToString());

            Color color = Color.clear;
            if (result == Test.Result.Fail) color = new Color(1f, 0f, 0f, 0.1f);
            else if (result == Test.Result.Pass) color = new Color(0f, 1f, 0f, 0.1f);

            EditorGUI.DrawRect(rect, color);

            GUIStyle resultStyle = Style.Get("Test/Result");
            Rect resultRect = new Rect(Vector2.zero, resultStyle.CalcSize(image));
            resultRect = Style.AlignRect(resultRect, rect, TextAnchor.MiddleRight);
            rect.width -= resultRect.width;
            EditorGUI.LabelField(resultRect, image, resultStyle);

            return rect;
        }

        /// <summary>
        /// Show the background color change, the result status icon on the right, and the clear result button on the right.
        /// </summary>
        public static Rect PaintResultFeatures(Rect rect, Test test)
        {
            GUIContent image = Style.GetIcon("Test/Result/" + test.result.ToString());
            GUIContent clearImage = Style.GetIcon("Test/ClearResult");

            Color color = Color.clear;
            if (test.result == Test.Result.Fail) color = new Color(1f, 0f, 0f, 0.1f);
            else if (test.result == Test.Result.Pass) color = new Color(0f, 1f, 0f, 0.1f);

            EditorGUI.DrawRect(rect, color);

            // Reserve space for the two objects
            Rect newRect = new Rect(rect);
            newRect.width = Style.GetWidth("Test/Result", image) + Style.GetWidth("Test/ClearResult", clearImage);
            newRect = Style.AlignRect(newRect, rect, TextAnchor.MiddleRight);
            rect.width -= newRect.width;

            Rect r1 = new Rect(newRect);
            r1.width *= 0.5f;
            Rect r2 = new Rect(r1);
            r2.x += r1.width;
            r2.width = newRect.width - r1.width;

            GUIStyle style = new GUIStyle(Style.Get("Test/Result"));
            style.padding = new RectOffset(0, 0, 0, 0);
            style.margin = new RectOffset(style.padding.left, style.padding.right, 0, 0);

            using (new EditorGUI.DisabledScope(test.result == Test.Result.None))
            {
                if (GUI.Button(r1, clearImage, Style.Get("Test/ClearResult"))) // The X button to clear the result
                    test.Reset();
            }

            GUI.Label(r2, image, style); // The result indicator

            return rect;
        }

        public static List<bool> DrawToggle(Rect rect, string name, bool selected, bool locked, bool showLock = true, bool isMixed = false)
        {
            bool wasMixed = EditorGUI.showMixedValue;
            EditorGUI.showMixedValue = isMixed;

            // Draw the lock button
            if (showLock)
            {
                Rect lockRect = new Rect(rect);
                lockRect.width = Style.GetWidth("Test/Lock");
                //EditorGUI.DrawRect(lockRect, Color.red);
                locked = GUI.Toggle(lockRect, locked, GUIContent.none, Style.Get("Test/Lock"));
                rect.x += lockRect.width;
                rect.width -= lockRect.width;
            }

            // Draw the toggle
            using (new EditorGUI.DisabledScope(locked))
            {
                //EditorGUI.DrawRect(rect, Color.green);
                selected = EditorGUI.ToggleLeft(rect, name, selected, Style.GetTextOverflowAlignmentStyle(rect, Style.Get("Test/Toggle"), name, TextAnchor.MiddleRight));
            }

            EditorGUI.showMixedValue = wasMixed;

            return new List<bool> { selected, locked };
        }

        public void DrawTest(Rect rect, Test test, bool showLock = true, bool showFoldout = true, bool allowExpand = true, string name = null)
        {
            // Draw the expanded box first so it appears behind everything else
            if (test.expanded && allowExpand && !test.IsInSuite())
            {
                float h = GUI.skin.label.CalcHeight(GUIContent.none, rect.width) + GUI.skin.label.margin.vertical;

                GUIStyle boxStyle = new GUIStyle(Style.Get("Test/Expanded"));
                boxStyle.margin = new RectOffset((int)rect.x, boxStyle.margin.right, boxStyle.margin.top, boxStyle.margin.bottom);

                rect.y += 0.5f * boxStyle.padding.top;

                GUILayout.Space(-h); // Move the box closer to the Test foldout above
                GUILayout.BeginHorizontal(boxStyle); // This is so we can shift the GroupBox drawing to the right
                {
                    GUILayout.BeginVertical();
                    {
                        GUILayout.Space(h);

                        test.defaultGameObject = EditorGUI.ObjectField(
                            EditorGUILayout.GetControlRect(true),
                            new GUIContent("Default Prefab", "This test will receive a copy of this GameObject if it does not use a 'SetUp' method and if " +
                            "a GameObject has been given here. Otherwise, this test will receive the GameObject from its 'SetUp' method as usual."),
                            test.defaultGameObject,
                            typeof(GameObject),
                            false
                        ) as GameObject;
                    }
                    GUILayout.EndVertical();
                }
                GUILayout.EndHorizontal();
            }

            if (showFoldout)
            {
                // This prevents the foldout from grabbing focus on mouse clicks on the toggle buttons
                Rect foldoutRect = new Rect(rect);
                foldoutRect.width = Style.GetWidth("Test/Foldout");
                rect.x += foldoutRect.width;
                rect.width -= foldoutRect.width;
                test.expanded = allowExpand && GUI.Toggle(foldoutRect, test.expanded && allowExpand, GUIContent.none, Style.Get("Test/Foldout"));

                Rect scriptRect = new Rect(rect);
                scriptRect.x = rect.xMax - Style.TestManagerUI.scriptWidth;
                scriptRect.width = Style.TestManagerUI.scriptWidth;
                float previousLabelWidth = EditorGUIUtility.labelWidth;
                EditorGUIUtility.labelWidth = 0f;
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUI.ObjectField(scriptRect, GUIContent.none, test.GetScript(), test.method.DeclaringType, false);
                }
                EditorGUIUtility.labelWidth = previousLabelWidth;
            }

            float toggleWidth = Style.GetWidth("Test/Lock");
            Rect toggleRect = new Rect(rect);
            if (!test.IsInSuite()) toggleRect.width -= Style.TestManagerUI.scriptWidth;

            if (string.IsNullOrEmpty(name)) name = test.attribute.name;
            //EditorGUI.DrawRect(toggleRect, Color.red);
            List<bool> res = DrawToggle(toggleRect, name, test.selected, test.locked, showLock, false);

            test.selected = res[0];
            test.locked = res[1];
        }


        [CustomPropertyDrawer(typeof(Test.TestPrefab))]
        public class TestPrefabPropertyDrawer : PropertyDrawer
        {
            private const float xpadding = 2f;
            private float lineHeight = EditorGUIUtility.singleLineHeight;

            private GUIContent[] methodNames;

            private void Initialize(SerializedProperty property)
            {
                System.Type type = property.serializedObject.targetObject.GetType();
                string[] names = type.GetMethods(Utilities.bindingFlags)
                              .Where(m => m.GetCustomAttributes(typeof(TestAttribute), true).Length > 0)
                              .Select(m => m.Name).ToArray();
                methodNames = new GUIContent[names.Length];
                for (int i = 0; i < names.Length; i++)
                    methodNames[i] = new GUIContent(names[i]);
            }

            public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
            {
                return lineHeight;
            }

            public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
            {
                if (methodNames == null) Initialize(property);

                SerializedProperty _gameObject = property.FindPropertyRelative(nameof(_gameObject));
                SerializedProperty _methodName = property.FindPropertyRelative(nameof(_methodName));

                int index = 0;
                bool labelInOptions = false;
                for (int i = 0; i < methodNames.Length; i++)
                {
                    if (methodNames[i].text == _methodName.stringValue)
                    {
                        index = i;
                    }
                    if (methodNames[i].text == label.text) labelInOptions = true;
                }

                // Create the rects to draw as though we had no prefix label (as inside ReorderableLists)
                Rect rect1 = new Rect(position);
                rect1.width = EditorGUIUtility.labelWidth - xpadding;

                Rect rect2 = new Rect(position);
                rect2.xMin = rect1.xMax + xpadding;
                rect2.width = position.xMax - rect1.xMax;

                if (labelInOptions && property.displayName == label.text)
                { // If we are in a ReorderableList, then don't draw the prefix label.
                    label = GUIContent.none;
                    // For some reason the height is not right if we don't do this...
                    rect2.height = lineHeight;
                }
                else
                { // Otherwise, draw a prefix label
                    Rect rect = new Rect(position);
                    rect.width = EditorGUIUtility.labelWidth - xpadding;
                    rect1.xMin = rect.xMax + xpadding;
                    rect1.width = (position.xMax - rect.xMax) * 0.5f - 2 * xpadding;
                    rect2.xMin = rect1.xMax + xpadding;
                    rect2.width = position.xMax - rect2.xMin;

                    EditorGUI.LabelField(rect, label);
                    label = GUIContent.none;
                }
                int result = EditorGUI.Popup(rect1, label, index, methodNames);
                if (result < methodNames.Length) _methodName.stringValue = methodNames[result].text;
                EditorGUI.ObjectField(rect2, _gameObject, GUIContent.none);
            }
        }
        #endregion Tests

        #endregion UI
    }
}