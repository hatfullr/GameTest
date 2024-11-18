using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Text.RegularExpressions;
using Codice.CM.Common.Tree;
using UnityEditor.VersionControl;

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

        private SettingsWindow settingsWindow;

        public Foldout rootFoldout => manager.rootFoldout;
        public List<Foldout> foldouts => manager.foldouts;

        // Defer these parameters so that they are saved in the TestManager object (EditorWindows can't be saved as ScriptableObjects :( )
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

        void OnInspectorUpdate()
        {
            if (settingsWindow != null) settingsWindow.Repaint();
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
            Utilities.debug = manager.debug;

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
            }, message: "Resetting");
        }

        private void Refresh(System.Action onFinished = null, string message = "Refreshing")
        {
            StartLoadingWheel(message);
            Repaint();

            Test previousSettingsTest = null;
            if (settingsWindow != null) previousSettingsTest = settingsWindow.GetTest();

            manager.UpdateTests(() =>
            {
                Test newSettingsTest = null;
                foreach (Test test in manager.tests)
                {
                    if (previousSettingsTest != null)
                    {
                        if (test.attribute == previousSettingsTest.attribute) newSettingsTest = test;
                    }
                    
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
            //bool selectAll = false;
            //bool deselectAll = false;

            EditorGUILayout.VerticalScope mainScope = new EditorGUILayout.VerticalScope(GUILayout.ExpandHeight(true), GUILayout.ExpandWidth(true));

            using (mainScope)
            {
                using (new EditorGUI.DisabledScope(loadingWheelVisible))
                {
                    // The main window
                    using (new EditorGUILayout.VerticalScope(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true)))
                    {
                        // Toolbar controls
                        using (new EditorGUILayout.HorizontalScope(Style.Get("TestManagerUI/Toolbar")))
                        {
                            // Left
                            DrawPlayButton(state);
                            DrawPauseButton(state);
                            DrawSkipButton();
                            DrawGoToEmptySceneButton();

                            //using (new EditorGUI.DisabledScope(foldouts.Count == 0 || manager.tests.Count == 0))
                            //{
                            //    bool[] result = DrawUniversalToggle(state);
                            //    selectAll = result[0];
                            //    deselectAll = result[1];
                            //}

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

                        // The box that shows the Foldouts and Tests
                        GUIStyle style = Style.Get("TestManagerUI/TestView");
                        EditorGUILayout.VerticalScope scrollScope = new EditorGUILayout.VerticalScope(style, GUILayout.ExpandHeight(true), GUILayout.ExpandWidth(true));
                        using (scrollScope)
                        {
                            indentLevel = 0;

                            viewRect.x = 0f;
                            viewRect.y = 0f;
                            viewRect.width = Mathf.Max(minWidth, scrollScope.rect.width);
                            viewRect.height = GetListHeight();

                            if (showWelcome) viewRect.height += GetWelcomeHeight();

                            if (viewRect.height > scrollScope.rect.height) // This means the vertical scrollbar is visible
                            {
                                viewRect.width -= GUI.skin.verticalScrollbar.CalcSize(GUIContent.none).x;
                            }

                            GUI.ScrollViewScope scrollViewScope = new GUI.ScrollViewScope(
                                scrollScope.rect,
                                scrollPosition,
                                viewRect,
                                false,
                                false,
                                GUI.skin.horizontalScrollbar,
                                GUI.skin.verticalScrollbar
                            );
                            using (scrollViewScope)
                            {
                                scrollPosition = scrollViewScope.scrollPosition;

                                itemRect = new Rect(viewRect.x, viewRect.y, viewRect.width, Style.lineHeight);

                                if (showWelcome)
                                {
                                    Rect welcomeRect = DrawWelcome(); // Welcome message
                                    itemRect.y = welcomeRect.yMax;
                                }

                                // Apply padding
                                itemRect.x += style.padding.left;
                                itemRect.y += style.padding.top;
                                itemRect.width -= style.padding.horizontal;

                                

                                if (string.IsNullOrEmpty(search)) DrawNormalMode();
                                else DrawSearchMode();

                                //Utilities.DrawDebugOutline(viewRect, Color.red);
                            }
                        }
                    }

                    if (!manager.running) // Otherwise stuff will keep being added into the queue during testing time
                    {
                        foreach (Test test in manager.tests)
                        {
                            if (test.selected && !manager.queue.Contains(test)) manager.AddToQueue(test);
                            else if (manager.queue.Contains(test) && !test.selected) manager.queue.Remove(test);
                        }
                    }
                }

                manager.guiQueue.Draw();
            }

            if (loadingWheelVisible) DrawLoadingWheel(mainScope.rect);

            if (!loadingWheelVisible)
            {
                // Doing things this way avoids annoying GUI errors complaining about groups not being ended properly.
                if (refresh) Refresh();
            }
        }

        private float GetWelcomeHeight()
        {
            GUIStyle messageStyle = Style.Get("TestManagerUI/Welcome/Message");
            GUIContent message = new GUIContent(Style.welcomeMessage);
            GUIStyle donateStyle = Style.Get("TestManagerUI/Donate");
            GUIStyle docStyle = Style.Get("TestManagerUI/Documentation");
            GUIContent donate = Style.GetIcon("TestManagerUI/Donate");
            GUIContent doc = Style.GetIcon("TestManagerUI/Documentation");

            const int nLinks = 2;
            string[] links = new string[nLinks] { Style.donationLink, Style.documentationLink };

            GUIStyle[] linkStyles = new GUIStyle[nLinks] { donateStyle, docStyle };
            GUIContent[] linkContent = new GUIContent[nLinks] { donate, doc };

            Rect[] linkRects = new Rect[nLinks];
            for (int i = 0; i < nLinks; i++) linkRects[i] = new Rect(Vector2.zero, linkStyles[i].CalcSize(linkContent[i]));

            Rect titleRect = new Rect(viewRect.x, viewRect.y, viewRect.width, 0f);
            titleRect.height = 0;
            for (int i = 0; i < nLinks; i++) titleRect.height = Mathf.Max(titleRect.height, linkRects[i].height);

            Rect body = new Rect(
                viewRect.x,
            titleRect.yMax,
            viewRect.width,
                messageStyle.CalcHeight(message, titleRect.width) + messageStyle.padding.vertical
            );

            return titleRect.height + body.height;
        }

        private Rect DrawWelcome()
        {
            // Setup styles and content
            GUIContent icon = Style.GetIcon("TestManagerUI/Welcome");
            GUIContent title = new GUIContent(Style.welcomeTitle, icon.image);
            GUIContent message = new GUIContent(Style.welcomeMessage);
            GUIContent donate = Style.GetIcon("TestManagerUI/Donate");
            GUIContent doc = Style.GetIcon("TestManagerUI/Documentation");

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

            Color bg = Color.black * 0.2f;

            // Drawing
            GUI.Box(bgRect, GUIContent.none, welcomeStyle);
            EditorGUI.DrawRect(titleRect, bg);
            EditorGUI.DrawRect(new Rect(titleRect.x, titleRect.yMax, titleRect.width, dy), bg + new Color(0f, 0f, 0f, 0.1f));

            using (new EditorGUIUtility.IconSizeScope(new Vector2(titleRect.height - welcomeStyle.padding.vertical, titleRect.height - welcomeStyle.padding.vertical)))
            {
                EditorGUI.LabelField(Utilities.GetPaddedRect(titleRect, titleStyle.padding), title, titleStyle);
            }

            for (int i = 0; i < nLinks; i++)
            {
                if (EditorGUI.LinkButton(Utilities.GetPaddedRect(linkRects[i], EditorStyles.linkLabel.padding), linkContent[i])) Application.OpenURL(links[i]);
            }

            EditorGUI.LabelField(Utilities.GetPaddedRect(body, messageStyle.padding), message, messageStyle);

            Rect ret = new Rect(bgRect);

            //viewRect.y = bgRect.yMax;
            //viewRect.height -= bgRect.height;

            // DEBUGGING
            foreach (System.Tuple<Rect, Color> kvp in new System.Tuple<Rect, Color>[]
            {
                //new System.Tuple<Rect,Color>(bgRect,      Color.green),
                //new System.Tuple<Rect,Color>(titleRect, Color.red),
                //new System.Tuple<Rect,Color>(body,      Color.red)
                //new System.Tuple<Rect,Color>(viewRect,      Color.red)
                //new System.Tuple<Rect,Color>(ret,      Color.red)
            }) Utilities.DrawDebugOutline(kvp.Item1, kvp.Item2);

            return ret;
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
            foreach (Test match in new List<Test>(manager.searchMatches))
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

                DrawListItem(itemRect, match, ref match.expanded, ref match.locked, ref match.selected,
                    showFoldout: false,
                    showScript: true,
                    showLock: true,
                    showToggle: true,
                    showResultBackground: true,
                    showClearResult: true,
                    showResult: true,
                    showGoTo: true,
                    showSettings: false,
                    name: final
                );
                itemRect.y += itemRect.height;
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
            System.Array values = System.Enum.GetValues(typeof(Utilities.DebugMode));

            bool hasNothing = true;
            bool hasEverything = true;
            bool hasAnything = false;
            
            foreach (Utilities.DebugMode mode in values)
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
                foreach (Utilities.DebugMode mode in values) manager.debug &= ~mode;
            }
            void SetAllFlags()
            {
                foreach (Utilities.DebugMode mode in values) manager.debug |= mode;
            }

            Rect rect = Style.GetRect("TestManagerUI/Toolbar/Debug", debugContent);
            if (EditorGUI.DropdownButton(rect, debugContent, FocusType.Passive, Style.Get("TestManagerUI/Toolbar/Clear")))
            {
                GenericMenu toolsMenu = new GenericMenu();

                if (hasNothing) toolsMenu.AddDisabledItem(new GUIContent("Nothing"), true);
                else toolsMenu.AddItem(new GUIContent("Nothing"), hasNothing, ClearFlags);

                if (hasEverything) toolsMenu.AddDisabledItem(new GUIContent("Everything"), true);
                else toolsMenu.AddItem(new GUIContent("Everything"), hasEverything, SetAllFlags);

                foreach (Utilities.DebugMode mode in System.Enum.GetValues(typeof(Utilities.DebugMode)))
                {
                    toolsMenu.AddItem(new GUIContent(mode.ToString()), manager.debug.HasFlag(mode), () =>
                    {
                        if (manager.debug.HasFlag(mode)) manager.debug &= ~mode;
                        else manager.debug |= mode;
                    });
                }

                toolsMenu.DropDown(rect);
            }
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
            if (!EditorUtility.DisplayDialog("Reset UnityTest Manager?", "This will clear all saved information about tests, GameObjects, etc. " +
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
            Rect itemRect,
            Object item,
            ref bool expanded, ref bool locked, ref bool selected,
            bool showFoldout = true,
            bool showScript = false,
            bool showLock = true,
            bool showToggle = true,
            bool showResultBackground = true,
            bool showClearResult = true,
            bool showResult = true,
            bool showGoTo = false,
            bool showSettings = true,
            bool changeItemRectWidthOnTextOverflow = false,
            bool showTooltips = true,
            string tooltipOverride = null,
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
                GUIStyle goToStyle = Style.Get("GoToSearch");
                GUIStyle settingsStyle = Style.Get("Settings");

                GUIContent lockIcon;
                if (locked) lockIcon = Style.GetIcon("LockOn");
                else lockIcon = Style.GetIcon("LockOff");

                GUIContent scriptIcon = Style.GetIcon("Script");
                GUIContent clearIcon = Style.GetIcon("ClearResult");
                GUIContent goToIcon = Style.GetIcon("GoToSearch");
                GUIContent settingsIcon = Style.GetIcon("Settings");



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
                if (showTooltips)
                {
                    if (tooltipOverride != null) toggleContent.tooltip = tooltipOverride;
                    else if (result == Test.Result.Fail) toggleContent.tooltip = Style.Tooltips.testFailed;
                    else if (result == Test.Result.Pass) toggleContent.tooltip = Style.Tooltips.testPassed;
                }


                float clearWidth = Style.GetWidth(clearStyle, clearIcon);
                float resultWidth = Style.GetWidth(resultStyle, resultIcon);
                float settingsWidth = Style.GetWidth(settingsStyle, settingsIcon);
                
                if (!showClearResult) clearWidth = 0f;
                if (!showResult) resultWidth = 0f;
                if (!showSettings) settingsWidth = 0f;



                // Setup Rects for drawing
                Rect indentedRect = EditorGUI.IndentedRect(itemRect);

                float leftOffset = 0f;
                Rect foldoutRect = new Rect(itemRect);
                foldoutRect.width = Style.GetWidth(foldoutStyle);
                if (!showFoldout) foldoutRect.width = 0f;
                leftOffset += foldoutRect.width;

                Rect goToRect = new Rect(itemRect);
                goToRect.x += leftOffset;
                goToRect.width = Style.GetWidth(goToStyle, goToIcon);
                if (!showGoTo) goToRect.width = 0f;
                leftOffset += goToRect.width;

                Rect scriptRect = new Rect(indentedRect); // use itemRect when using EditorGUI, like LabelField, and indentedRect when using GUI, like Label.
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

                Rect settingsRect = new Rect(itemRect);
                settingsRect.x = settingsRect.xMax - (rightOffset + settingsWidth) - settingsStyle.margin.right;
                settingsRect.width = settingsWidth;
                rightOffset += settingsRect.width;
                if (showSettings) rightOffset += settingsStyle.margin.left;

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
                    else if (result == Test.Result.Skipped) resultColor = Style.skippedColor;

                    if (result != Test.Result.None) EditorGUI.DrawRect(resultBGRect, resultColor);
                }
                if (showFoldout)
                {
                    expanded = EditorGUI.Foldout(foldoutRect, expanded, GUIContent.none, foldoutStyle);
                }

                if (showGoTo)
                {
                    if (GUI.Button(goToRect, goToIcon, goToStyle))
                    {
                        foreach (Foldout foldout in foldouts)
                        {
                            List<Test> tests = new List<Test>(foldout.GetTests(manager));
                            if (tests.Contains((item as Test))) foldout.expanded = true;
                        }
                        manager.UpdateSearchMatches(this, null);
                    }
                }

                if (result != Test.Result.None && showResultBackground) GUI.backgroundColor = new Color(resultColor.r, resultColor.g, resultColor.b, 1f);
                if (showScript)
                {
                    if (script == null) throw new System.Exception("Failed to find script for list item '" + item + "'");

                    GUI.Label(scriptRect, scriptIcon, scriptStyle);

                    // We actually detect both single click and double click when a double click is issued, but doing both single and double
                    // click behaviors at the same time isn't really a deal breaker here.
                    if (Utilities.IsMouseOverRect(scriptRect) && Event.current != null)
                    {
                        if (Event.current.rawType == EventType.MouseUp && Event.current.clickCount == 1)
                        {
                            //Debug.Log("Single click");
                            EditorGUIUtility.PingObject(script); // 1 click, show the script in the Project folder
                            Event.current.Use();
                        }
                        else if (Event.current.rawType == EventType.MouseDown && Event.current.clickCount > 1)
                        {
                            //Debug.Log("Double+ click");
                            AssetDatabase.OpenAsset(script, lineNumber); // 2+ clicks, open the script
                            GUIUtility.ExitGUI();
                            Event.current.Use();
                        }
                    }
                }
                if (showSettings)
                {
                    if (GUI.Button(settingsRect, settingsIcon, settingsStyle))
                    {
                        if (settingsWindow == null) settingsWindow = EditorWindow.GetWindow<SettingsWindow>(true);
                        settingsWindow.Init(item as Test);
                        settingsWindow.ShowUtility();
                    }
                }
                if (showLock)
                {
                    // Desperately trying to save my light skin users
                    Color contentColor = GUI.contentColor;
                    if (!Utilities.isDarkTheme) GUI.contentColor = Color.black * 0.5f;
                    if (GUI.Button(lockedRect, lockIcon, lockedStyle)) locked = !locked;
                    GUI.contentColor = contentColor;
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
                //scriptRect.x += indent;
                toggleRect.x += indent;

                // Process right-mouse clicks on the foldout button for our left-handed friends
                if (Utilities.IsMouseOverRect(foldoutRect))
                {

                    if (Event.current != null)
                    {
                        if (Event.current.button == 1 && Event.current.type == EventType.MouseUp)
                        {
                            if (expanded) expanded = false;
                            else expanded = true;
                            Repaint();
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
                    //new System.Tuple < Rect, Color >(itemRect,        Color.red),
                    //new System.Tuple < Rect, Color >(goToRect,        Color.red),
                    //new System.Tuple < Rect, Color >(settingsRect,     Color.red),
                }) Utilities.DrawDebugOutline(kvp.Item1, kvp.Item2);
            }
        }

        /*
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
        */
        #endregion Tests

        #endregion UI
    }
}