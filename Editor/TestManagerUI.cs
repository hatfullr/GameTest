using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Text.RegularExpressions;
using System.Linq;
using Codice.CM.Common.Tree;

/// TODO ideas:
///    1. Add a preferences window
///        a. Let the user control the sorting order of the tests in the TestManager

namespace GameTest
{
    public class TestManagerUI : EditorWindow, IHasCustomMenu
    {
        public int indentLevel;
        private float spinStartTime = 0f;
        private int spinIndex = 0;
        private UnityEditor.IMGUI.Controls.SearchField searchField;

        public System.Action onLostFocus, onFocus;

        public TestManager manager;

        private SettingsWindow settingsWindow;

        public Rect viewRect = new Rect(0f, 0f, -1f, -1f);
        public Rect itemRect;
        private Rect scrollRect;

        private float minWidth = 0f;

        private bool reloadingDomain = false;

        private Change change;

        private bool drawingMainView = false;
        private Dictionary<string, Rect> testRects = new Dictionary<string, Rect>();

        private class Change
        {
            public object what;
            public UIEvent how;

            public Change(object what, UIEvent how)
            {
                this.what = what;
                this.how = how;
            }
        }


        public enum Mode
        {
            Normal,
            Search,
        }

        public enum TestSortOrder
        {
            Name,
            LineNumber,
        }

        private enum UIEvent
        {
            Selected,
            Deselected,
            Locked,
            Unlocked,
            AllExpanded,
            AllCollapsed,
            Result,
            RevealTest,
        }


        #region Unity UI
        public void AddItemsToMenu(GenericMenu menu)
        {
            menu.AddItem(new GUIContent("Reset"), false, ShowResetConfirmation);
            menu.AddItem(new GUIContent("About"), false, ShowAbout);
        }

        [MenuItem("Window/GameTest")]
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
            manager = TestManager.Load();
            Refresh();
        }

        /// <summary>
        /// Called before AssemblyReloadEvents.afterAssemblyReload, and whenever the user opens the window.
        /// </summary>
        void OnEnable()
        {
            Logger.debug = manager.debug;

            searchField = new UnityEditor.IMGUI.Controls.SearchField(); // Unity demands we do this in OnEnable and nowhere else

            // Clear these events out if they are already added
            AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeAssemblyReload;
            AssemblyReloadEvents.afterAssemblyReload -= OnAfterAssemblyReload;
            EditorApplication.playModeStateChanged -= OnPlayStateChanged;

            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
            AssemblyReloadEvents.afterAssemblyReload += OnAfterAssemblyReload;
            EditorApplication.playModeStateChanged += OnPlayStateChanged;

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
        }

        void OnDestroy()
        {
            if (manager != null)
            {
                if (manager.running) manager.Stop();

                // Save all the loaded assets
                manager.Save();
            }
        }

        private void OnBeforeAssemblyReload()
        {
            reloadingDomain = true;
        }

        private void OnAfterAssemblyReload()
        {
            manager = TestManager.Load();
            reloadingDomain = false;
            Refresh(() =>
            {
                if (manager != null)
                    if (manager.running && !manager.paused) manager.RunNext();
            });
        }

        private void OnPlayStateChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.EnteredPlayMode && Utilities.IsSceneEmpty()) Focus();

            if (change == PlayModeStateChange.EnteredEditMode && manager.running)
            {
                manager.Stop();
            }

            // If we don't Repaint() here, then the toolbar buttons can appear incorrect. This should always happen as the very last thing.
            if (change == PlayModeStateChange.EnteredEditMode) Repaint();
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
            if (manager != null)
            {
                if (manager.loadingWheelVisible)
                {
                    Repaint();
                    return;
                }
                if (!EditorApplication.isPlaying) return;
                manager.Update();
                if (manager.running) Repaint(); // keeps the frame counter and timer up-to-date
            }
        }
        #endregion Events


        #region Methods
        private void DoReset()
        {
            if (manager != null) manager.Reset();
            else manager = TestManager.Load();

            indentLevel = default;
            spinStartTime = default;
            spinIndex = default;
            viewRect = new Rect();
            itemRect = new Rect();
            minWidth = default;
            settingsWindow = null;
            reloadingDomain = false;
            testRects = new Dictionary<string, Rect>();

            Refresh(() => Logger.Log("Reset"), message: "Resetting");
        }

        private void Refresh(System.Action onFinished = null, string message = "Refreshing")
        {
            StartLoadingWheel(message);
            Repaint();

            Test previousSettingsTest = null;
            if (settingsWindow != null) previousSettingsTest = settingsWindow.GetTest();

            if (manager != null)
                manager.UpdateTests(() =>
                {
                    UpdateFoldoutStates();
                    StopLoadingWheel();
                    Repaint();

                    if (onFinished != null) onFinished(); 
                });
        }

        private void ResetSelected()
        {
            if (manager == null) return;
            foreach (Test test in manager.GetTests())
                if (test.selected) test.Reset();
        }
        private void ResetAll()
        {
            if (manager == null) return;
            foreach (Test test in manager.GetTests()) test.Reset();
        }
        #endregion Methods


        #region UI
        void OnGUI()
        {
            if (reloadingDomain) return;

            testRects = new Dictionary<string, Rect>();
            //change = null;

            EditorGUI.BeginChangeCheck();

            if (manager == null) manager = TestManager.Load();

            Utilities.isDarkTheme = GUI.skin.name == "DarkSkin";

            UnityEngine.Profiling.Profiler.BeginSample(nameof(GameTest), this);

            EditorGUILayout.VerticalScope mainScope = new EditorGUILayout.VerticalScope(GUILayout.ExpandHeight(true), GUILayout.ExpandWidth(true));
            using (mainScope)
            {
                using (new EditorGUI.DisabledScope(manager.loadingWheelVisible))
                {
                    // The main window
                    using (new EditorGUILayout.VerticalScope(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true)))
                    {
                        // Toolbar controls
                        using (new EditorGUILayout.HorizontalScope(Style.Get("TestManagerUI/Toolbar")))
                        {
                            // Left
                            DrawPlayButton();
                            DrawPauseButton();
                            DrawSkipButton();
                            DrawGoToEmptySceneButton();
                            DrawClearButton();

                            // Left
                            GUILayout.FlexibleSpace();
                            // Right

                            DrawSearchBar();
                            DrawWelcomeButton();
                            DrawDebugButton();
                            DrawRefreshButton();
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

                            if (manager.showWelcome) viewRect.height += GetWelcomeHeight();

                            if (viewRect.height > scrollScope.rect.height) // This means the vertical scrollbar is visible
                            {
                                viewRect.width -= GUI.skin.verticalScrollbar.CalcSize(GUIContent.none).x;
                            }

                            scrollRect = GUIUtility.GUIToScreenRect(scrollScope.rect);
                            GUI.ScrollViewScope scrollViewScope = new GUI.ScrollViewScope(
                                scrollScope.rect,
                                manager.scrollPosition,
                                viewRect,
                                false,
                                false,
                                GUI.skin.horizontalScrollbar,
                                GUI.skin.verticalScrollbar
                            );
                            using (scrollViewScope)
                            {
                                manager.scrollPosition = scrollViewScope.scrollPosition;

                                itemRect = new Rect(viewRect.x, viewRect.y, viewRect.width, Style.lineHeight);

                                if (manager.showWelcome)
                                {
                                    Rect welcomeRect = DrawWelcome(); // Welcome message
                                    itemRect.y = welcomeRect.yMax;
                                }

                                // Apply padding
                                itemRect.x += style.padding.left;
                                itemRect.y += style.padding.top;
                                itemRect.width -= style.padding.horizontal;

                                using (new EditorGUI.DisabledGroupScope(manager.running))
                                {
                                    drawingMainView = true;
                                    if (string.IsNullOrEmpty(manager.search)) DrawNormalMode();
                                    else DrawSearchMode();
                                    drawingMainView = false;
                                }
                            }
                        }
                    }

                    manager.guiQueue.Draw();
                }

                if (manager.loadingWheelVisible) DrawLoadingWheel(mainScope.rect);
            }

            if (EditorGUI.EndChangeCheck() || change != null) ProcessChange();

            UnityEngine.Profiling.Profiler.EndSample();
        }

        private void GetWelcomeRects(out Rect title, out Rect body, out Rect bg, out Rect[] links)
        {
            // Setup styles and content
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
            GUIStyle[] linkStyles = new GUIStyle[nLinks] { donateStyle, docStyle };
            GUIContent[] linkContent = new GUIContent[nLinks] { donate, doc };
            RectOffset[] padding = new RectOffset[nLinks];
            for (int i = 0; i < nLinks; i++) padding[i] = linkStyles[i].margin;

            links = new Rect[nLinks];
            for (int i = 0; i < nLinks; i++) links[i] = new Rect(Vector2.zero, linkStyles[i].CalcSize(linkContent[i]));

            // Setup Rects
            title = new Rect(viewRect.x, viewRect.y, viewRect.width, 0f);
            title.height = 0;
            for (int i = 0; i < nLinks; i++) title.height = Mathf.Max(title.height, links[i].height);

            body = new Rect(
                viewRect.x,
                title.yMax,
                viewRect.width,
                messageStyle.CalcHeight(message, title.width) + messageStyle.padding.vertical
            );

            bg = new Rect(viewRect.x, viewRect.y, viewRect.width, title.height + body.height);
            bg.y -= welcomeStyle.padding.top; // This hides the top part of the background, making it look kinda like a tab in the UI
            bg.height += welcomeStyle.padding.top;

            // Apply margins
            float dy = titleStyle.margin.bottom + messageStyle.margin.top;
            body.y += dy;
            bg.height += dy;

            // Alignment
            links = Utilities.AlignRects(
                links,
                title,
                Utilities.RectAlignment.LowerRight,
                Utilities.RectAlignment.MiddleLeft,
                padding: padding
            );

            title = Utilities.GetPaddedRect(title, titleStyle.padding);
            for (int i = 0; i < nLinks; i++)
            {
                links[i] = Utilities.GetPaddedRect(links[i], EditorStyles.linkLabel.padding);
            }

            body = Utilities.GetPaddedRect(body, messageStyle.padding);
        }

        private float GetWelcomeHeight()
        {
            GetWelcomeRects(out Rect _, out Rect _, out Rect bgRect, out Rect[] _);
            return bgRect.height;
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

            const int nLinks = 2;
            string[] links = new string[nLinks] { Style.donationLink, Style.documentationLink };
            GUIContent[] linkContent = new GUIContent[nLinks] { donate, doc };

            float dy = titleStyle.margin.bottom + messageStyle.margin.top;

            GetWelcomeRects(out Rect titleRect, out Rect body, out Rect bgRect, out Rect[] linkRects);

            Color bg = Color.black * 0.2f;

            // Drawing
            GUI.Box(bgRect, GUIContent.none, welcomeStyle);
            EditorGUI.DrawRect(titleRect, bg);
            EditorGUI.DrawRect(new Rect(titleRect.x, titleRect.yMax, titleRect.width, dy), bg + new Color(0f, 0f, 0f, 0.1f));

            using (new EditorGUIUtility.IconSizeScope(new Vector2(titleRect.height - welcomeStyle.padding.vertical, titleRect.height - welcomeStyle.padding.vertical)))
            {
                EditorGUI.LabelField(titleRect, title, titleStyle);
            }

            for (int i = 0; i < linkRects.Length; i++)
            {
                if (EditorGUI.LinkButton(linkRects[i], linkContent[i])) Application.OpenURL(links[i]);
            }

            EditorGUI.LabelField(body, message, messageStyle);

            // DEBUGGING
            //foreach (System.Tuple<Rect, Color> kvp in new System.Tuple<Rect, Color>[]
            //{
                //new System.Tuple<Rect,Color>(bgRect,    Color.green),
                //new System.Tuple<Rect,Color>(titleRect, Color.red),
                //new System.Tuple<Rect,Color>(body,      Color.yellow),
                //new System.Tuple<Rect,Color>(viewRect,  Color.cyan)
            //}) Utilities.DrawDebugOutline(kvp.Item1, kvp.Item2);

            return bgRect;
        }

        private Mode GetMode()
        {
            if (manager == null) return Mode.Normal;
            if (!string.IsNullOrEmpty(manager.search)) return Mode.Search;
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
                height += Style.TestManagerUI.foldoutMargin; // a bit of extra space at the bottom looks cleanest
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
            foreach (Foldout foldout in manager.foldouts)
                if (foldout.IsRoot()) foldout.Draw(this);
            manager.pingData.HandlePing(this);
        }
            

        /// <summary>
        /// Shows the tests as their full paths when text is present in the search bar. Only shows the tests matching the search regex.
        /// </summary>
        private void DrawSearchMode()
        {
            Regex re = new Regex(manager.search, RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.IgnorePatternWhitespace);
            MatchCollection matches;
            string path, final;
            bool dummy = false;
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

                DrawListItem(itemRect, match, ref dummy, ref match.locked, ref match.selected,
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
            string newSearch = searchField.OnToolbarGUI(manager.search, GUILayout.MinWidth(Utilities.searchBarMinWidth), GUILayout.MaxWidth(Utilities.searchBarMaxWidth));

            Rect rect = GUILayoutUtility.GetLastRect();
            if (Utilities.IsMouseButtonReleased() && !(Utilities.IsMouseOverRect(rect) && GUI.enabled)) EditorGUI.FocusTextInControl(null);

            if (manager.search != newSearch) manager.UpdateSearchMatches(this, newSearch);
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
            content.text = manager.loadingWheelText;
            GUI.Label(rect, content, Style.Get("TestManagerUI/LoadingWheel"));
        }

        private void DrawClearButton()
        {
            bool selectedHaveResults = false;
            bool anyResults = false;
            foreach (Test test in manager.GetTests()) 
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
                    if (selectedHaveResults) toolsMenu.AddItem(new GUIContent("Reset Selected"), false, ResetSelected);
                    else toolsMenu.AddDisabledItem(new GUIContent("Reset Selected"));

                    if (anyResults) toolsMenu.AddItem(new GUIContent("Reset All"), false, ResetAll);
                    else toolsMenu.AddDisabledItem(new GUIContent("Reset All"));

                    toolsMenu.DropDown(clearRect);
                }
            }
        }



        private void DrawPlayButton()
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
                else manager.Start();
            }
        }

        private void DrawPauseButton()
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
                    Logger.Log("Entered an empty scene");
                    GUIUtility.ExitGUI();
                }
            }
        }

        private void DrawDebugButton()
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
            }
            void SetAllFlags()
            {
                foreach (Logger.DebugMode mode in values) manager.debug |= mode;
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
                        if (manager.debug.HasFlag(mode)) manager.debug &= ~mode;
                        else manager.debug |= mode;
                    });
                }

                toolsMenu.DropDown(rect);
            }
        }

        private void DrawRefreshButton()
        {
            if (GUILayout.Button(Style.GetIcon("TestManagerUI/Toolbar/Refresh"), Style.Get("TestManagerUI/Toolbar/Refresh"))) Refresh();
        }

        private void DrawWelcomeButton()
        {
            manager.showWelcome = GUILayout.Toggle(manager.showWelcome, Style.GetIcon("TestManagerUI/Toolbar/Welcome"), Style.Get("TestManagerUI/Toolbar/Welcome"));
        }

        /// <summary>
        /// Say "are you sure?"
        /// </summary>
        private void ShowResetConfirmation()
        {
            if (!EditorUtility.DisplayDialog("Reset GameTest?", "This will clear all saved information about tests, GameObjects, etc. " +
                "If you have encountered a bug, first try closing the GameTest and opening it again.",
                "Yes", "No"
            )) return;
            
            // User clicked "OK"
            DoReset();
        }

        private void ShowAbout()
        {
            if (!EditorUtility.DisplayDialog(nameof(GameTest) + " (" + UnityEditor.PackageManager.PackageInfo.FindForAssembly(GetType().Assembly).version + ")",
                string.Join("\n",
                    "Created by Roger Hatfull",
                    "Special thanks to Ganesh for help with testing",
                    "",
                    "The author is an independent developer who benefits greatly from donations. Please consider donating if you have found this package useful.",
                    "",
                    "Thank you for using " + nameof(GameTest) + "!"
                ),
                "Donate", "Close"
            )) return;
            Application.OpenURL(Style.donationLink);
        }

        private void StartLoadingWheel(string text = null)
        {
            if (manager == null) return;
            manager.loadingWheelText = text;
            spinStartTime = Time.realtimeSinceStartup;
            manager.loadingWheelVisible = true;
        }

        private void StopLoadingWheel()
        {
            if (manager == null) return;
            manager.loadingWheelVisible = false;
            manager.loadingWheelText = null;
        }

        /// <summary>
        /// Do the same thing that UnityEditor does when a click occurs, e.g. on a console message that has a context attached to it. Creates a yellow highlight box that zooms in, holds, and then fades
        /// to show where in the project the thing is that was just clicked.
        /// </summary>
        public void PingTest(Test test)
        {
            manager.pingData.test = test;
        }

        /// <summary>
        /// Expand foldouts as necessary so that the given Test can be seen. If the Test is out of the scroll view, this will scroll the view to make the Test visible.
        /// Does not ping the test. See the PingTest method.
        /// </summary>
        public void RevealTest(Test test)
        {
            manager.testToReveal = test.attribute.GetPath();
            foreach (Foldout parent in test.GetParentFoldouts(manager))
            {
                parent.expanded = true;
            }
            change = new Change(null, UIEvent.RevealTest);
            Repaint();
        }

        private void DoReveal()
        {
            if (!testRects.ContainsKey(manager.testToReveal)) return;
            Rect rect = testRects[manager.testToReveal];

            if (rect.yMin < scrollRect.yMin) // need to scroll the view upwards
            {
                manager.scrollPosition.y -= scrollRect.yMin - rect.yMin;
            }
            else if (rect.yMax > scrollRect.yMax) // need to scroll the view downwards
            {
                manager.scrollPosition.y += rect.yMax - scrollRect.yMax;
            }

            manager.testToReveal = null;
            change = null;
        }

        #region Tests
        /// <summary>
        /// Draw an item in the manager's list, which will be either a Foldout or a Test. This method draws only the following controls:
        /// the foldout button, the lock button, the toggle button, the label, the suite settings cog (if the item is a Suite), the script 
        /// object reference, the "clear results" button, and the test results. It does not draw the contents of the foldout for a Test object.
        /// </summary>
        public void DrawListItem(
            Rect itemRect,
            object item,
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
                Color previousBackgroundColor = GUI.backgroundColor;

                // Setup styles
                GUIStyle toggleStyle = Style.Get("Toggle");
                GUIStyle lockedStyle = Style.Get("Lock");
                GUIStyle foldoutStyle = Style.Get("Foldout");
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
                    if (string.IsNullOrEmpty(name)) name = (item as Foldout).GetName();
                    isMixed = (item as Foldout).IsMixed();
                    if ((item as Foldout).tests.Count > 0 && (item as Foldout).expanded) toggleStyle = Style.Get("ToggleHeader");
                    result = (item as Foldout).result; //GetTotalResult(manager);
                    onClearPressed += () =>
                    {
                        foreach (Test test in (item as Foldout).GetTests(manager)) test.Reset();
                    };
                }
                else if (item.GetType() == typeof(Test))
                {
                    if (string.IsNullOrEmpty(name)) name = (item as Test).attribute.name;
                    isMixed = false;
                    script = (item as Test).GetScript();
                    lineNumber = (item as Test).attribute.lineNumber;
                    result = (item as Test).result;
                    onClearPressed += (item as Test).Reset;
                    scriptIcon.tooltip = System.IO.Path.GetFileName((item as Test).attribute.sourceFile) + " (L" + lineNumber + ")\n" +
                        "<size=10>double-click to open</size>";
                    string path = (item as Test).attribute.GetPath();

                    if (drawingMainView)
                    {
                        testRects.Add(path, GUIUtility.GUIToScreenRect(itemRect));

                        if (manager.pingData.test != null)
                        {
                            if (manager.pingData.test.attribute.GetPath() == path) manager.pingData.rect = itemRect;
                        }
                    }
                }
                else throw new System.NotImplementedException("Unimplemented type " + item);

                GUIContent resultIcon = Style.GetIcon("Result/" + result.ToString());
                GUIContent toggleContent = new GUIContent(name);
                if (showTooltips)
                {
                    if (tooltipOverride != null) toggleContent.tooltip = tooltipOverride;
                    else
                    {
                        if (result == Test.Result.Fail) toggleContent.tooltip = Style.Tooltips.testFailed;
                        else if (result == Test.Result.Pass) toggleContent.tooltip = Style.Tooltips.testPassed;
                        else if (result == Test.Result.Skipped) toggleContent.tooltip = Style.Tooltips.testSkipped;
                        else if (result == Test.Result.None) { }
                        else throw new System.NotImplementedException("Unrecognized result " + result);
                    }
                }


                Rect indentedRect = EditorGUI.IndentedRect(itemRect);
                using (new GUI.GroupScope(indentedRect))
                {
                    // Draw the background color if needed
                    Color resultColor = Color.clear;
                    if (showResultBackground)
                    {
                        if (result == Test.Result.Fail) resultColor = Style.failColor;
                        else if (result == Test.Result.Pass) resultColor = Style.passColor;
                        else if (result == Test.Result.Skipped) resultColor = Style.skippedColor;
                        else if (result == Test.Result.None) { }
                        else throw new System.NotImplementedException("Unrecognized result " + result);

                        float x = (showFoldout ? Style.GetWidth(foldoutStyle) : 0f) + (showGoTo ? Style.GetWidth(goToStyle) : 0f);
                        EditorGUI.DrawRect(
                            new Rect(
                                x, 0f,
                                indentedRect.width - x, indentedRect.height
                            ),
                            resultColor
                        );
                    }



                    // Setup Rects
                    Rect left = new Rect(Vector2.zero, indentedRect.size);

                    // Figure out the size of the stuff on the right-hand side
                    Rect right = new Rect(indentedRect.width, 0f, 0f, indentedRect.height);
                    Rect settingsRect = new Rect(
                        showSettings ? settingsStyle.margin.left : 0f, 0f,
                        showSettings ? Style.GetWidth(settingsStyle, settingsIcon) : 0f, right.height
                    );
                    Rect clearRect = new Rect(
                        settingsRect.xMax + (showSettings ? settingsStyle.margin.right : 0f) + (showClearResult ? clearStyle.margin.left : 0f), 0f,
                        showClearResult ? Style.GetWidth(clearStyle, clearIcon) : 0f, right.height
                    );
                    Rect resultRect = new Rect(
                        clearRect.xMax + (showClearResult ? clearStyle.margin.right : 0f) + (showResult ? resultStyle.margin.left : 0f), 0f,
                        showResult ? Style.GetWidth(resultStyle, resultIcon) : 0f, right.height
                    );
                    
                    right.width = resultRect.width + settingsRect.width + clearRect.width;
                    right.width += (showResult ? resultStyle.margin.horizontal : 0f) + 
                        (showSettings ? settingsStyle.margin.horizontal : 0f) + 
                        (showClearResult ? clearStyle.margin.horizontal : 0f);

                    // final positioning
                    right.x -= right.width;
                    left.width -= right.width;



                    // Drawing
                    Rect tempRect = new Rect(); // temporary holder
                    using (new GUI.GroupScope(left))
                    {
                        tempRect = left;
                        
                        if (showFoldout)
                        {
                            tempRect.width = Style.GetWidth(foldoutStyle);
                            bool wasExpanded = expanded;
                            expanded = GUI.Toggle(
                                tempRect,
                                expanded,
                                GUIContent.none,
                                foldoutStyle
                            );
                            tempRect.x += tempRect.width;

                            if (change == null && expanded != wasExpanded && Event.current.alt)
                            {
                                UIEvent evt = UIEvent.AllExpanded;
                                if (wasExpanded && !expanded) evt = UIEvent.AllCollapsed;
                                change = new Change(item, evt);
                            }
                        }

                        if (showGoTo)
                        {
                            tempRect.width = Style.GetWidth(goToStyle);
                            if (GUI.Button(
                                tempRect,
                                goToIcon,
                                goToStyle
                            ))
                            {
                                foreach (Foldout foldout in manager.foldouts)
                                {
                                    List<Test> tests = new List<Test>(foldout.GetTests(manager));
                                    if (tests.Contains((item as Test))) foldout.expanded = true;
                                }
                                manager.UpdateSearchMatches(this, null);
                            }
                            tempRect.x += tempRect.width;
                        }

                        if (showResultBackground && result != Test.Result.None) GUI.backgroundColor = new Color(resultColor.r, resultColor.g, resultColor.b, 1f);

                        if (showScript)
                        {
                            if (script == null) throw new System.Exception("Failed to find script for list item '" + item + "'");

                            tempRect.width = Style.GetWidth(Style.Get("Script"), scriptIcon);
                            GUI.Label(tempRect, scriptIcon, Style.Get("Script"));

                            // We actually detect both single click and double click when a double click is issued, but doing both single and double
                            // click behaviors at the same time isn't really a deal breaker here.
                            if (Utilities.IsMouseOverRect(tempRect) && Event.current != null)
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

                            tempRect.x += tempRect.width;
                        }

                        if (showLock)
                        {
                            tempRect.width = Style.GetWidth(lockedStyle, lockIcon);

                            // Desperately trying to save my light skin users
                            Color contentColor = GUI.contentColor;
                            if (!Utilities.isDarkTheme) GUI.contentColor = Color.black * 0.5f;
                            if (GUI.Button(tempRect, lockIcon, lockedStyle))
                            {
                                bool wasLocked = locked;
                                locked = !locked;

                                if (change == null)
                                {
                                    UIEvent evt = UIEvent.Unlocked;
                                    if (!wasLocked && locked) evt = UIEvent.Locked;
                                    change = new Change(item, evt);
                                }
                            }
                            tempRect.x += tempRect.width;
                            GUI.contentColor = contentColor;
                        }


                        tempRect.width = left.xMax - tempRect.x;


                        // textRect is for fitting the text within the window (change to right-aligned when cutting off text, so text is cutoff on the left instead of the right)
                        Rect textRect = new Rect(tempRect);
                        if (showToggle)
                        {
                            textRect.x += EditorStyles.toggle.padding.left;
                            textRect.width -= EditorStyles.toggle.padding.left;
                        }

                        if (textRect.width < Style.TestManagerUI.minTextWidth && changeItemRectWidthOnTextOverflow && indentedRect.width > 0f)
                        {
                            minWidth = itemRect.width + (Style.TestManagerUI.minTextWidth - textRect.width);
                        }
                        toggleStyle = Style.GetTextOverflowAlignmentStyle(textRect, toggleStyle, toggleContent.text, TextAnchor.MiddleRight);
                        

                        if (showToggle)
                        {
                            using (new EditorGUI.IndentLevelScope(-EditorGUI.indentLevel))
                            {
                                using (new EditorGUI.DisabledScope(locked))
                                {
                                    bool wasSelected = selected;
                                    bool wasMixed = EditorGUI.showMixedValue;
                                    EditorGUI.showMixedValue = isMixed;
                                    selected = EditorGUI.ToggleLeft(  // "controlled" flow
                                        tempRect,
                                        toggleContent,
                                        selected,
                                        toggleStyle
                                    );
                                    EditorGUI.showMixedValue = wasMixed;

                                    if (wasSelected != selected && change == null)
                                    {
                                        UIEvent evt = UIEvent.Selected;
                                        if (wasSelected && !selected) evt = UIEvent.Deselected;
                                        change = new Change(item, evt);
                                    }
                                }
                            }
                        }
                        else
                        {
                            GUI.Label(tempRect, toggleContent, toggleStyle);
                        }
                    }

                    // Draw right
                    using (new GUI.GroupScope(right))
                    {
                        if (showSettings)
                        {
                            if (GUI.Button(settingsRect, settingsIcon, settingsStyle))
                            {
                                if (settingsWindow == null) settingsWindow = EditorWindow.GetWindow<SettingsWindow>(true);
                                settingsWindow.Init(item as Test);
                                settingsWindow.ShowUtility();
                            }
                        }

                        if (showClearResult)
                        {
                            using (new EditorGUI.DisabledScope(result == Test.Result.None))
                            {
                                if (GUI.Button(clearRect, clearIcon, clearStyle)) onClearPressed(); // The X button to clear the result
                            }
                        }

                        if (showResult) GUI.Label(resultRect, resultIcon, resultStyle);

                        // DEBUGGING
                        //Utilities.DrawDebugOutline(settingsRect, Color.red);
                        //Utilities.DrawDebugOutline(clearRect, Color.red);
                        //Utilities.DrawDebugOutline(resultRect, Color.red);
                    }
                }

                GUI.backgroundColor = previousBackgroundColor;
            }
        }

        private void UpdateFoldoutStates()
        {
            // First, make a list of all the Tests and their depth in the tree. Sort the list of Tests by depth, in reverse order. Update the foldouts in that order.
            Dictionary<Foldout, int> foldouts = new Dictionary<Foldout, int>();
            char[] separators = new char[3] { System.IO.Path.PathSeparator, System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar };
            int deepest = 0;
            int depth;
            foreach (Foldout foldout in manager.foldouts)
            {
                depth = foldout.path.Count(x => separators.Contains(x));
                deepest = Mathf.Max(deepest, depth);
                foldouts.Add(foldout, depth);
            }
            foldouts = new Dictionary<Foldout, int>(foldouts.OrderBy(x => deepest - x.Value)); // this sorts by deepest first
            foreach (Foldout foldout in foldouts.Keys)
            {
                if (!foldout.UpdateState(manager)) break; // stop updating early if there was no change 
            }
        }

        private void ProcessChange()
        {
            if (change == null) return;

            if (change.how == UIEvent.RevealTest)
            {
                DoReveal();
                return;
            }
            else
            {
                if (change.what.GetType() == typeof(Test))
                {
                    // Update the Foldouts
                    foreach (Foldout parent in (change.what as Test).GetParentFoldouts(manager))
                    {
                        if (!parent.UpdateState(manager)) break; // stop updating early if there was no change 
                    }
                    if (change.how == UIEvent.Selected) manager.AddToQueue(change.what as Test);
                    else if (change.how == UIEvent.Deselected) manager.RemoveFromQueue(change.what as Test);
                }
                else if (change.what.GetType() == typeof(Foldout))
                {
                    // Update the Tests and Foldouts in all children
                    Foldout foldout = change.what as Foldout;
                    if (change.how == UIEvent.Locked) foldout.Lock(manager);
                    else if (change.how == UIEvent.Unlocked) foldout.Unlock(manager);
                    else if (change.how == UIEvent.Selected) foldout.Select(manager);
                    else if (change.how == UIEvent.Deselected) foldout.Deselect(manager);
                    else if (change.how == UIEvent.AllExpanded) foldout.ExpandAll(manager, true);
                    else if (change.how == UIEvent.AllCollapsed) foldout.ExpandAll(manager, false);
                    else throw new System.NotImplementedException("Unrecognized UIEvent \"" + change.how + "\" for change in Foldout");

                    // Update the parents
                    foreach (Foldout parent in foldout.GetParents(manager))
                    {
                        if (!parent.UpdateState(manager)) break; // stop updating early if there was no change 
                    }
                }
                else throw new System.NotImplementedException("Unrecognized UI item of type \"" + change.what.GetType() + "\"");
            }
            change = null;
        }
        #endregion Tests

        #endregion UI
    }
}