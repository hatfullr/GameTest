using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Linq;
using System.Text.RegularExpressions;

namespace UnityTest
{
    public class TestManagerUI : EditorWindow, IHasCustomMenu
    {
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

        public Foldout rootFoldout => manager.rootFoldout;
        public List<Foldout> foldouts => manager.foldouts;
        public GUIQueue guiQueue => manager.guiQueue;
        public SettingsWindow settingsWindow => manager.settingsWindow;

        public Vector2 scrollPosition { get => manager.scrollPosition; set => manager.scrollPosition = value; }
        public bool showWelcome { get => manager.showWelcome; set => manager.showWelcome = value; }
        public bool loadingWheelVisible { get => manager.loadingWheelVisible; set => manager.loadingWheelVisible = value; }
        public string search { get => manager.search; set => manager.search = value; }
        public string loadingWheelText { get => manager.loadingWheelText; set => manager.loadingWheelText = value; }

        public Rect viewRect = new Rect(0f, 0f, -1f, -1f);
        public Rect itemRect;

        private float minWidth = 0f;


        public enum Mode
        {
            Normal,
            Search,
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
            Refresh();
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

            // Before the editor application quits, save the assets for next time
            EditorApplication.quitting -= manager.Save;
            EditorApplication.quitting += manager.Save;
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

        void OnDestroy()
        {
            if (manager.running) manager.Stop();

            // Save all the loaded assets
            manager.Save();
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
                if (test.selected) manager.AddToQueue(test);
            }
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

            indentLevel = default;
            spinStartTime = default;
            spinIndex = default;

            Refresh(() => 
            {
                Utilities.Log("Reset");
            });
        }

        private void Refresh(System.Action onFinished = null, string message = "Refreshing")
        {
            StartLoadingWheel(message);
            Repaint();

            manager.UpdateTests(() =>
            {
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

                StopLoadingWheel();
                Repaint();

                if (onFinished != null) onFinished();
            });
        }

        private void ResetSelected()
        {
            foreach (Test test in rootFoldout.GetTests(manager))
                if (test.selected) test.Reset();
        }
        private void ResetAll()
        {
            foreach (Test test in manager.tests) test.Reset();
        }
        #endregion Methods


        #region UI
        void OnGUI()
        {
            Utilities.isDarkTheme = GUI.skin.name == "DarkSkin";
            State state = new State(this, rootFoldout);

            bool refresh = false;
            bool selectAll = false;
            bool deselectAll = false;

            Rect fullRect = EditorGUILayout.BeginVertical(GUILayout.ExpandHeight(true), GUILayout.ExpandWidth(true));
            {
                using (new EditorGUI.DisabledScope(loadingWheelVisible))
                {
                    // The main window
                    EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
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

                        // The box that shows the Foldouts and Tests
                        GUIStyle style = Style.Get("TestManagerUI/TestView");
                        Rect scrollRect = EditorGUILayout.BeginVertical(style, GUILayout.ExpandHeight(true), GUILayout.ExpandWidth(true));
                        {
                            indentLevel = 0;

                            viewRect.x = 0f;
                            viewRect.y = 0f;
                            viewRect.width = Mathf.Max(minWidth, scrollRect.width);
                            viewRect.height = GetListHeight();

                            //Utilities.DrawDebugOutline(new Rect(scrollRect.x + viewRect.x, scrollRect.y + viewRect.y, viewRect.width, viewRect.height), Color.red);

                            scrollPosition = GUI.BeginScrollView(
                                scrollRect,
                                scrollPosition,
                                viewRect,
                                false,
                                false,
                                GUI.skin.horizontalScrollbar,
                                GUI.skin.verticalScrollbar
                            );
                            {
                                if (showWelcome) DrawWelcome(); // Welcome message

                                itemRect = new Rect(viewRect.x, viewRect.y, viewRect.width, Style.lineHeight);
                                // Apply padding
                                itemRect.x += style.padding.left;
                                itemRect.y += style.padding.top;
                                itemRect.width -= style.padding.horizontal;

                                if (string.IsNullOrEmpty(search)) DrawNormalMode();
                                else DrawSearchMode();
                            }
                            GUI.EndScrollView();
                        }
                        EditorGUILayout.EndVertical();
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
                }
            }
            EditorGUILayout.EndVertical();

            if (loadingWheelVisible) DrawLoadingWheel(fullRect);

            if (!loadingWheelVisible)
            {
                // Doing things this way avoids annoying GUI errors complaining about groups not being ended properly.
                if (refresh) Refresh();
                else if (selectAll) rootFoldout.Select(manager); // Simulate a press
                else if (deselectAll) rootFoldout.Deselect(manager); // Simulate a press
            }
        }

        private void DrawWelcome()
        {
            // Setup styles and content
            //GUIContent content = Style.GetIcon("TestManagerUI/Welcome");
            GUIContent icon = Style.GetIcon("TestManagerUI/Welcome");
            GUIContent title = new GUIContent(Style.welcomeTitle, icon.image);
            GUIContent message = new GUIContent(Style.welcomeMessage);
            GUIContent donate = Style.GetIcon("TestManagerUI/Donate");
            GUIContent doc = Style.GetIcon("TestManagerUI/Documentation");

            //GUIStyle mainStyle = Style.Get("TestManagerUI/TestView");
            GUIStyle welcomeStyle = Style.Get("TestManagerUI/Welcome");
            GUIStyle titleStyle = Style.Get("TestManagerUI/Welcome/Title");
            GUIStyle messageStyle = Style.Get("TestManagerUI/Welcome/Message");
            GUIStyle donateStyle = Style.Get("TestManagerUI/Donate");
            GUIStyle docStyle = Style.Get("TestManagerUI/Documentation");

            // Setup stuff relating to the link buttons
            const int nLinks = 2;
            string[] links = new string[nLinks] { Style.donationLink, Style.documentationLink };
            GUIStyle[] linkStyles = new GUIStyle[nLinks] { donateStyle, docStyle };
            GUIContent[] linkContent = new GUIContent[nLinks] { donate, doc };
            RectOffset[] padding = new RectOffset[nLinks];
            for (int i = 0; i < nLinks; i++) padding[i] = linkStyles[i].margin;

            Rect[] linkRects = new Rect[nLinks];
            for (int i = 0; i < nLinks; i++) linkRects[i] = new Rect(Vector2.zero, linkStyles[i].CalcSize(linkContent[i]));

            // Setup Rects
            Rect titleRect = new Rect(viewRect.x, viewRect.y, viewRect.width, 0f);
            titleRect.height = 0;
            for (int i = 0; i < nLinks; i++) titleRect.height = Mathf.Max(titleRect.height, linkRects[i].height);

            Rect body = new Rect(
                viewRect.x,
                titleRect.yMax,
                viewRect.width,
                messageStyle.CalcHeight(message, titleRect.width) + messageStyle.padding.vertical
            );

            Rect bgRect = new Rect(viewRect.x, viewRect.y, viewRect.width, titleRect.height + body.height);
            bgRect.y -= welcomeStyle.padding.top; // This hides the top part of the background, making it look kinda like a tab in the UI
            bgRect.height += welcomeStyle.padding.top;

            // Apply margins
            float dy = titleStyle.margin.bottom + messageStyle.margin.top;
            body.y += dy;
            bgRect.height += dy;

            // Alignment
            linkRects = Utilities.AlignRects(
                linkRects,
                titleRect,
                Utilities.RectAlignment.LowerRight,
                Utilities.RectAlignment.MiddleLeft,
                padding: padding
            );

            Color bg;
            if (Utilities.isDarkTheme) bg = new Color(0f, 0f, 0f, 0.2f);
            else bg = new Color(1f, 1f, 1f, 0.2f);

            // Drawing
            GUI.Box(bgRect, GUIContent.none, welcomeStyle);
            EditorGUI.DrawRect(titleRect, bg);
            EditorGUI.DrawRect(new Rect(titleRect.x, titleRect.yMax, titleRect.width, dy), bg + new Color(0f, 0f, 0f, 0.1f));

            using (new EditorGUIUtility.IconSizeScope(new Vector2(titleRect.height - welcomeStyle.padding.vertical, titleRect.height - welcomeStyle.padding.vertical)))
            {
                EditorGUI.DropShadowLabel(Utilities.GetPaddedRect(titleRect, titleStyle.padding), title, titleStyle);
            }
            //GUI.Box(header, title, titleStyle);

            for (int i = 0; i < nLinks; i++)
            {
                if (EditorGUI.LinkButton(Utilities.GetPaddedRect(linkRects[i], EditorStyles.linkLabel.padding), linkContent[i])) Application.OpenURL(links[i]);
            }

            //EditorGUI.LabelField(titleRect, title, titleStyle);

            EditorGUI.LabelField(Utilities.GetPaddedRect(body, messageStyle.padding), message, messageStyle);

            // DEBUGGING
            foreach (System.Tuple<Rect, Color> kvp in new System.Tuple<Rect, Color>[]
            {
                //new System.Tuple<Rect,Color>(rect,      Color.green),
                //new System.Tuple<Rect,Color>(titleRect, Color.red),
                //new System.Tuple<Rect,Color>(body,      Color.red)
            }) Utilities.DrawDebugOutline(kvp.Item1, kvp.Item2);

            viewRect.y = bgRect.yMax;
            viewRect.height -= bgRect.height;
        }

        private Mode GetMode()
        {
            if (!string.IsNullOrEmpty(search)) return Mode.Search;
            return Mode.Normal;
        }

        private float GetListHeight()
        {
            Mode mode = GetMode();
            float height = 0f;

            if (mode == Mode.Normal)
            {
                foreach (Foldout foldout in Foldout.GetVisible(this))
                {
                    height += Style.lineHeight;
                    if (foldout.expanded)
                    {
                        height += Style.lineHeight * foldout.tests.Count;
                        if (foldout.tests.Count > 0) height += Style.TestManagerUI.foldoutMargin;
                    }
                }
            }
            else if (mode == Mode.Search)
            {
                foreach (Test test in manager.searchMatches)
                {
                    height += Style.lineHeight;
                }
            }
            else throw new System.NotImplementedException("Unrecognized Mode " + mode);

            return height;
        }

        /// <summary>
        /// Draw the tests as nested foldouts in a hierarchy according to their individual paths.
        /// </summary>
        private void DrawNormalMode()
        {
            foreach (Foldout child in rootFoldout.GetChildren(manager, false)) child.Draw(this);
        }

        /// <summary>
        /// Shows the tests as their full paths when text is present in the search bar. Only shows the tests matching the search regex.
        /// </summary>
        private void DrawSearchMode()
        {
            Regex re = new Regex(search, RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.IgnorePatternWhitespace);
            MatchCollection matches;
            string path, final;
            foreach (Test match in manager.searchMatches)
            {
                path = match.attribute.GetPath();
                matches = re.Matches(path);

                // Modify the color or something of the regex matches to show where the matches happened
                final = "";
                for (int i = 0; i < matches.Count; i++)
                {
                    if (i == 0) final += path[..matches[i].Index];
                    else final += path[(matches[i - 1].Index + matches[i - 1].Length)..matches[i].Index];
                    final += "<b>" + path[matches[i].Index..(matches[i].Index + matches[i].Length)] + "</b>";
                }
                final += path[(matches[matches.Count - 1].Index + matches[matches.Count - 1].Length)..];
                DrawListItem(match, ref match.expanded, ref match.locked, ref match.selected,
                    showFoldout: false,
                    showScript: true,
                    showLock: true,
                    showToggle: true,
                    showResultBackground: true,
                    showClearResult: true,
                    showResult: true,
                    name: final
                );
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
            if (Utilities.IsMouseButtonReleased() && !(Utilities.IsMouseOverRect(rect) && GUI.enabled)) EditorGUI.FocusTextInControl(null);

            if (search != newSearch) manager.UpdateSearchMatches(this, newSearch);
        }

        /// <summary>
        /// Draw the loading wheel shown during a refresh and whenever the assemblies are being checked for tests.
        /// </summary>
        private void DrawLoadingWheel(Rect rect)
        {
            float time = Time.realtimeSinceStartup;
            if (time - spinStartTime >= Style.TestManagerUI.spinRate)
            {
                spinIndex++;
                spinStartTime = time;
            }

            GUIContent content;
            try { content = Style.GetIcon("TestManagerUI/LoadingWheel/" + spinIndex); }
            catch (System.NotImplementedException)
            {
                spinIndex = 0;
                content = Style.GetIcon("TestManagerUI/LoadingWheel/" + spinIndex);
            }
            content.text = loadingWheelText;
            GUI.Label(rect, content, Style.Get("TestManagerUI/LoadingWheel"));
        }

        private bool[] DrawUniversalToggle(State state)
        {
            // This is all just to draw the toggle button in the toolbar
            GUIContent toggle = Style.GetIcon("TestManagerUI/Toolbar/Toggle/On"); // guess at initial value
            GUIStyle toggleStyle = Style.Get("TestManagerUI/Toolbar/Toggle/On");

            Rect rect = GUILayoutUtility.GetRect(toggle, toggleStyle);

            bool hover = Utilities.IsMouseOverRect(rect) && GUI.enabled;

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
            if (manager.debug != TestManager.DebugMode.Nothing) debugContent = Style.GetIcon("TestManagerUI/Toolbar/Debug/On");

            Rect clearRect = Style.GetRect("TestManagerUI/Toolbar/Debug", debugContent);
            if (EditorGUI.DropdownButton(clearRect, debugContent, FocusType.Passive, Style.Get("TestManagerUI/Toolbar/Clear")))
            {
                GenericMenu toolsMenu = new GenericMenu();
                foreach (TestManager.DebugMode mode in System.Enum.GetValues(typeof(TestManager.DebugMode)))
                {
                    toolsMenu.AddItem(new GUIContent(mode.ToString()), manager.debug.HasFlag(mode), () => {

                        if (mode == TestManager.DebugMode.Nothing)
                        {
                            manager.debug = mode;
                        }
                        else if (mode == TestManager.DebugMode.Everything)
                        {
                            manager.debug = mode;
                        }
                        else
                        {
                            manager.debug &= ~TestManager.DebugMode.Nothing;
                            if (manager.debug.HasFlag(mode))
                            {
                                manager.debug &= ~mode;
                                manager.debug &= ~TestManager.DebugMode.Everything;
                            }
                            else manager.debug |= mode;

                            bool anyLeft = false;
                            foreach (TestManager.DebugMode m in System.Enum.GetValues(typeof(TestManager.DebugMode)))
                            {
                                if (m == TestManager.DebugMode.Nothing || m == TestManager.DebugMode.Everything) continue;
                                if (manager.debug.HasFlag(m))
                                {
                                    anyLeft = true;
                                    break;
                                }
                            }
                            if (!anyLeft) manager.debug = TestManager.DebugMode.Nothing;
                        }
                    });
                }
                

                //if (state.anySelected) toolsMenu.AddItem(new GUIContent("Reset Selected"), false, ResetSelected);
                //else toolsMenu.AddDisabledItem(new GUIContent("Reset Selected"));

                //if (state.anyResults) toolsMenu.AddItem(new GUIContent("Reset All"), false, ResetAll);
                //else toolsMenu.AddDisabledItem(new GUIContent("Reset All"));

                toolsMenu.DropDown(clearRect);
            }

            //manager.debug = GUILayout.Toggle(manager.debug, debugContent, Style.Get("TestManagerUI/Toolbar/Debug"));
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

        private void StartLoadingWheel(string text = null)
        {
            loadingWheelText = text;
            spinStartTime = Time.realtimeSinceStartup;
            loadingWheelVisible = true;
        }

        private void StopLoadingWheel()
        {
            loadingWheelVisible = false;
            loadingWheelText = null;
        }




        #region Tests
        /// <summary>
        /// Draw an item in the manager's list, which will be either a Foldout or a Test. This method draws only the following controls:
        /// the foldout button, the lock button, the toggle button, the label, the suite settings cog (if the item is a Suite), the script 
        /// object reference, the "clear results" button, and the test results. It does not draw the contents of the foldout for a Test object.
        /// </summary>
        public void DrawListItem(
            Object item,
            ref bool expanded, ref bool locked, ref bool selected,
            bool showFoldout = true,
            bool showScript = false,
            bool showLock = true,
            bool showToggle = true,
            bool showResultBackground = true,
            bool showClearResult = true,
            bool showResult = true,
            bool changeItemRectWidthOnTextOverflow = false,
            string name = null
        )
        {
            using (new EditorGUI.IndentLevelScope(indentLevel))
            {
                // Save initial state information
                float previousLabelWidth = EditorGUIUtility.labelWidth;
                bool wasMixed = EditorGUI.showMixedValue;
                Color previousBackgroundColor = GUI.backgroundColor;

                // Setup styles
                GUIStyle toggleStyle = Style.Get("Toggle");
                GUIStyle lockedStyle = Style.Get("Lock");
                GUIStyle foldoutStyle = Style.Get("Foldout");
                GUIStyle scriptStyle = Style.Get("Script");
                GUIStyle clearStyle = Style.Get("ClearResult");
                GUIStyle resultStyle = Style.Get("Result");

                GUIContent lockIcon;
                if (locked) lockIcon = Style.GetIcon("LockOn");
                else lockIcon = Style.GetIcon("LockOff");

                GUIContent scriptIcon = Style.GetIcon("Script");
                GUIContent clearIcon = Style.GetIcon("ClearResult");



                Object script = null;
                int lineNumber = 0;
                bool isMixed;
                Test.Result result;
                System.Action onClearPressed = () => { };
                if (item.GetType() == typeof(Foldout))
                {
                    Foldout foldout = item as Foldout;
                    if (string.IsNullOrEmpty(name)) name = foldout.GetName();
                    isMixed = foldout.IsMixed(manager);
                    if (foldout.tests.Count > 0 && foldout.expanded) toggleStyle = Style.Get("ToggleHeader");
                    result = foldout.GetTotalResult(manager);
                    onClearPressed += () =>
                    {
                        foreach (Test test in foldout.GetTests(manager)) test.Reset();
                    };
                }
                else if (item.GetType() == typeof(Test))
                {
                    Test test = item as Test;
                    if (string.IsNullOrEmpty(name)) name = test.attribute.name;
                    isMixed = false;
                    script = test.GetScript();
                    lineNumber = test.attribute.lineNumber;
                    result = test.result;
                    onClearPressed += test.Reset;
                    scriptIcon.tooltip = System.IO.Path.GetFileName(test.attribute.sourceFile) + " (L" + lineNumber + ")\n" +
                        "<size=10>double-click to open</size>";
                }
                else throw new System.NotImplementedException("Unimplemented type " + item);

                GUIContent resultIcon = Style.GetIcon("Result/" + result.ToString());
                GUIContent toggleContent = new GUIContent(name);
                if (result == Test.Result.Fail) toggleContent.tooltip = Style.Tooltips.testFailed;
                else if (result == Test.Result.Pass) toggleContent.tooltip = Style.Tooltips.testPassed;


                float clearWidth = Style.GetWidth(clearStyle, clearIcon);
                float resultWidth = Style.GetWidth(resultStyle, resultIcon);
                
                if (!showClearResult) clearWidth = 0f;
                if (!showResult) resultWidth = 0f;




                // Setup Rects for drawing
                Rect indentedRect = EditorGUI.IndentedRect(itemRect);

                float leftOffset = 0f;
                Rect foldoutRect = new Rect(itemRect);
                foldoutRect.width = Style.GetWidth(foldoutStyle);
                if (!showFoldout) foldoutRect.width = 0f;
                leftOffset += foldoutRect.width;

                Rect scriptRect = new Rect(itemRect);
                scriptRect.x += leftOffset;
                scriptRect.width = Style.GetWidth(scriptStyle, scriptIcon);
                if (!showScript) scriptRect.width = 0f;
                leftOffset += scriptRect.width;

                Rect lockedRect = new Rect(indentedRect);
                lockedRect.x += leftOffset;
                lockedRect.width = Style.GetWidth(lockedStyle);
                if (!showLock) lockedRect.width = 0f;
                leftOffset += lockedRect.width;

                float rightOffset = 0f;
                Rect resultRect = new Rect(itemRect);
                resultRect.x = resultRect.xMax - resultWidth - (itemRect.width - indentedRect.width) - resultStyle.margin.right;
                resultRect.width = resultWidth;
                rightOffset += resultRect.width;
                if (showResult) rightOffset += resultStyle.margin.left;

                Rect clearRect = new Rect(itemRect);
                clearRect.x = clearRect.xMax - (rightOffset + clearWidth) - clearStyle.margin.right;
                clearRect.width = clearWidth;
                rightOffset += clearRect.width;
                if (showClearResult) rightOffset += clearStyle.margin.left;

                Rect toggleRect = new Rect(itemRect);
                toggleRect.x += leftOffset;
                toggleRect.width -= leftOffset + rightOffset;

                // textRect is for fitting the text within the window (change to right-aligned when cutting off text, so text is cutoff on the left instead of the right)
                Rect textRect = new Rect(indentedRect);
                if (showToggle) leftOffset += EditorStyles.toggle.padding.left;
                textRect.x += leftOffset;
                textRect.width -= leftOffset + rightOffset;

                if (textRect.width < Style.TestManagerUI.minTextWidth && changeItemRectWidthOnTextOverflow && indentedRect.width > 0f)
                {
                    minWidth = itemRect.width + (Style.TestManagerUI.minTextWidth - textRect.width);
                }

                toggleStyle = new GUIStyle(Style.GetTextOverflowAlignmentStyle(textRect, toggleStyle, toggleContent.text, TextAnchor.MiddleRight));

                // Drawing
                Color resultColor = Color.clear;
                if (showResultBackground)
                {
                    Rect resultBGRect = new Rect(indentedRect);
                    if (showFoldout)
                    {
                        resultBGRect.x += foldoutRect.width;
                        resultBGRect.width -= foldoutRect.width;
                    }
                    if (result == Test.Result.Fail) resultColor = Style.failColor; 
                    else if (result == Test.Result.Pass) resultColor = Style.passColor;

                    if (result != Test.Result.None) EditorGUI.DrawRect(resultBGRect, resultColor);
                }
                if (showFoldout)
                {
                    expanded = EditorGUI.Foldout(foldoutRect, expanded, GUIContent.none, foldoutStyle);
                }

                if (result != Test.Result.None && showResultBackground) GUI.backgroundColor = new Color(resultColor.r, resultColor.g, resultColor.b, 1f);
                if (showScript)
                {
                    if (script == null) throw new System.Exception("Failed to find script for list item '" + item + "'");
                    EditorGUI.LabelField(scriptRect, scriptIcon, scriptStyle);
                }
                if (showLock)
                {
                    if (GUI.Button(lockedRect, lockIcon, lockedStyle)) locked = !locked;
                }
                if (showToggle)
                {
                    EditorGUI.showMixedValue = isMixed;
                    using (new EditorGUI.DisabledScope(locked)) selected = EditorGUI.ToggleLeft(toggleRect, toggleContent, selected, toggleStyle);
                    EditorGUI.showMixedValue = wasMixed;
                }
                else
                {
                    EditorGUI.LabelField(toggleRect, toggleContent, toggleStyle);
                }

                if (showClearResult)
                {
                    using (new EditorGUI.DisabledScope(result == Test.Result.None))
                    {
                        if (GUI.Button(clearRect, clearIcon, clearStyle)) onClearPressed(); // The X button to clear the result
                    }
                }

                if (showResult) EditorGUI.LabelField(resultRect, resultIcon, resultStyle);

                // Reset GUI to previous state
                EditorGUIUtility.labelWidth = previousLabelWidth;
                GUI.backgroundColor = previousBackgroundColor;


                // For processing events and everything that doesn't involve drawing, we need to position the Rects above in the proper
                // places, depending on the current indentation level. The reason is that EditorGUI controls automatically handle indentation,
                // so we needed to remove the indentation from some of the Rects above. Below, we expect the indentations to be there, so
                // we need to add them back in to the Rects here.
                float indent = itemRect.width - indentedRect.width;
                foldoutRect.x += indent;
                scriptRect.x += indent;
                toggleRect.x += indent;
                //resultRect.x += indent;
                //clearRect.x += indent;


                // Process clicks on the script
                if (Utilities.IsMouseButtonReleased())
                {
                    if (Utilities.IsMouseOverRect(scriptRect) && GUI.enabled)
                    {
                        if (Event.current.clickCount == 1)
                        {
                            EditorGUIUtility.PingObject(script);
                            Event.current.Use();
                        }
                        else if (Event.current.clickCount == 2)
                        {
                            AssetDatabase.OpenAsset(script, lineNumber);
                            GUIUtility.ExitGUI();
                        }
                    }
                }


                // DEBUGGING: Uncomment lines below to see the Rects.
                foreach (System.Tuple<Rect, Color> kvp in new List<System.Tuple<Rect, Color>>()
                {
                    //new System.Tuple < Rect, Color >(foldoutRect,     Color.red),
                    //new System.Tuple < Rect, Color >(scriptRect,      Color.red),
                    //new System.Tuple < Rect, Color >(lockedRect,      Color.red),
                    //new System.Tuple < Rect, Color >(toggleRect,      Color.red),
                    //new System.Tuple < Rect, Color >(textRect,        Color.green),
                    //new System.Tuple < Rect, Color >(indentedRect,    Color.red),
                    //new System.Tuple < Rect, Color >(rect,            Color.red),
                    //new System.Tuple < Rect, Color >(resultRect,      Color.red),
                    //new System.Tuple < Rect, Color >(clearRect,       Color.red),
                    //new System.Tuple < Rect, Color >(itemRect,       Color.red),
                })
                {
                    Utilities.DrawDebugOutline(kvp.Item1, kvp.Item2);
                }
            }

            itemRect.y += itemRect.height;
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