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

        private float minWidth;


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
            foreach (Test test in rootFoldout.GetTests(this))
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
                        scrollPosition = EditorGUILayout.BeginScrollView(
                            scrollPosition,
                            false, false,
                            GUI.skin.horizontalScrollbar, GUI.skin.verticalScrollbar,
                            Style.Get("TestManagerUI/TestView")
                        );
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
                else if (selectAll) rootFoldout.Select(this); // Simulate a press
                else if (deselectAll) rootFoldout.Deselect(this); // Simulate a press
            }
        }

        /// <summary>
        /// Draw the tests as nested foldouts in a hierarchy according to their individual paths.
        /// </summary>
        private void DrawNormalMode()
        {
            indentLevel = 0;
            foreach (Foldout child in rootFoldout.GetChildren(this, false)) child.Draw(this);
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
            foreach (Test test in rootFoldout.GetTests(this).OrderBy(x => x.attribute.GetPath()))
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
                DrawListItem(test, ref test.expanded, ref test.locked, ref test.selected,
                    showFoldout: false,
                    showLock: true,
                    showToggle: true,
                    showResultBackground: true,
                    showScript: true,
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
            if (Utilities.IsMouseButtonReleased() && !Utilities.IsMouseOverRect(rect)) EditorGUI.FocusTextInControl(null);

            search = newSearch;
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
        private void DrawListItemLeft(
            Object item,
            ref bool expanded, ref bool locked, ref bool selected,
            bool showFoldout = true,
            bool showLock = true,
            bool showToggle = true,
            string name = null
        )
        {
            // Save initial state information
            float previousLabelWidth = EditorGUIUtility.labelWidth;
            bool wasMixed = EditorGUI.showMixedValue;

            // Setup styles
            GUIStyle selectedStyle = Style.Get("Toggle");
            GUIStyle lockedStyle = Style.Get("Lock");
            GUIStyle foldoutStyle = Style.Get("Foldout");

            bool isMixed;
            if (item.GetType() == typeof(Foldout))
            {
                Foldout foldout = item as Foldout;
                if (string.IsNullOrEmpty(name)) name = foldout.GetName();
                isMixed = foldout.IsMixed(this);
                if (foldout.tests.Count > 0 && foldout.expanded) selectedStyle = Style.Get("ToggleHeader");
            }
            else if (item.GetType() == typeof(Test))
            {
                Test test = item as Test;
                if (string.IsNullOrEmpty(name)) name = test.attribute.name;
                isMixed = false;
            }
            else throw new System.NotImplementedException("Unimplemented type " + item);

            // Setup Rects for drawing
            Rect rect = EditorGUILayout.GetControlRect(false);
            Rect indentedRect = EditorGUI.IndentedRect(rect);

            Rect foldoutRect = new Rect(rect);
            foldoutRect.width = Style.GetWidth(foldoutStyle, GUIContent.none);

            Rect lockedRect = new Rect(indentedRect);
            lockedRect.x += foldoutRect.width;
            lockedRect.width = Style.GetWidth(lockedStyle, GUIContent.none);

            Rect selectedRect = new Rect(rect);
            selectedRect.x += foldoutRect.width + lockedRect.width;
            selectedRect.width -= foldoutRect.width + lockedRect.width;

            if (!showFoldout)
            {
                lockedRect.x -= foldoutRect.width;
                selectedRect.x -= foldoutRect.width;
                selectedRect.width += foldoutRect.width;
                foldoutRect.width = 0f;
            }
            if (!showLock)
            {
                selectedRect.x -= lockedRect.width;
                selectedRect.width += lockedRect.width;
                lockedRect.width = 0f;
            }

            Rect textRect = new Rect(indentedRect);
            float w = 0f;
            if (showFoldout) w += foldoutRect.width;
            if (showLock) w += lockedRect.width;
            if (showToggle) w += EditorStyles.toggle.padding.left;
            textRect.x += w;
            textRect.width -= w;

            // Drawing
            if (showFoldout) expanded = EditorGUI.Foldout(foldoutRect, expanded, GUIContent.none, foldoutStyle);
            if (showLock) locked = EditorGUI.Toggle(lockedRect, locked, lockedStyle);
            if (showToggle)
            {
                EditorGUI.showMixedValue = isMixed;
                selected = EditorGUI.ToggleLeft(
                    selectedRect, name, selected,
                    Style.GetTextOverflowAlignmentStyle(textRect, selectedStyle, name, TextAnchor.MiddleRight)
                );
                EditorGUI.showMixedValue = wasMixed;
            }
            else
            {
                EditorGUI.LabelField(
                    selectedRect, name,
                    Style.GetTextOverflowAlignmentStyle(textRect, selectedStyle, name, TextAnchor.MiddleRight)
                );
            }

            // Reset GUI to previous state
            EditorGUIUtility.labelWidth = previousLabelWidth;

            //Utilities.DrawDebugOutline(textRect, Color.red); // DEBUGGING
            //Utilities.DrawDebugOutline(indentedRect, Color.green); // DEBUGGING
        }

        private void DrawListItemRight(Object item, bool showScript = true, bool showClearResult = true, bool showResult = true)
        {
            int previousIndent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0; // We need to do this or the method doesn't work correctly.

            Rect rect = GUILayoutUtility.GetLastRect();

            Test.Result result;
            System.Action onClearPressed = () => { };
            Object script = null;
            if (item.GetType() == typeof(Test))
            {
                Test test = item as Test;
                result = test.result;
                onClearPressed += test.Reset;
                if (!test.isInSuite) script = test.GetScript();
            }
            else if (item.GetType() == typeof(Foldout))
            {
                Foldout foldout = item as Foldout;
                result = foldout.GetTotalResult(this);
                onClearPressed += () =>
                {
                    foreach (Test test in foldout.GetTests(this)) test.Reset();
                };

                if (showScript) script = foldout.tests[0].GetScript();
            }
            else throw new System.NotImplementedException("Unrecognized type " + item.GetType());

            GUIContent clearIcon = Style.GetIcon("ClearResult");
            GUIContent resultIcon = Style.GetIcon("Result/" + result.ToString());
            GUIStyle clearStyle = Style.Get("ClearResult");
            GUIStyle resultStyle = Style.Get("Result");
            GUIStyle scriptStyle = new GUIStyle(EditorStyles.objectField);

            float clearWidth = Style.GetWidth(clearStyle, clearIcon);
            float resultWidth = Style.GetWidth(resultStyle, resultIcon);
            float scriptWidth = 0f;
            float suiteSettingsWidth = 0f;
            if (script != null) scriptWidth = Style.TestManagerUI.scriptWidth;

            if (!showScript) scriptWidth = 0f;
            if (!showClearResult) clearWidth = 0f;
            if (!showResult) resultWidth = 0f;

            

            GUILayout.Space(clearWidth + resultWidth + scriptWidth + suiteSettingsWidth);
            Rect last = GUILayoutUtility.GetLastRect(); // The Rect containing the empty space
            last.height = rect.height;
            last.y += 2; // This is the best possible layout that Unity can provide. (angry.)

            Rect remaining = new Rect(last);
            Rect GetRect(float width)
            {
                Rect result = new Rect(remaining.x, remaining.y, width, remaining.height);
                remaining.x = result.xMax;
                remaining.width -= result.width;
                return result;
            }

            Rect scriptRect = GetRect(scriptWidth);
            Rect clearRect = GetRect(clearWidth);
            Rect resultRect = GetRect(resultWidth);

            // Drawing

            if (showScript)
            {
                using (new EditorGUI.DisabledScope(true)) EditorGUI.ObjectField(scriptRect, script, script.GetType(), false);
            }

            if (showClearResult)
            {
                using (new EditorGUI.DisabledScope(result == Test.Result.None))
                {
                    if (GUI.Button(clearRect, clearIcon, clearStyle)) onClearPressed(); // The X button to clear the result
                }
            }

            if (showResult) EditorGUI.LabelField(resultRect, resultIcon, resultStyle);


            // DEBUGGING:
            //Utilities.DrawDebugOutline(rect, Color.green);
            //Utilities.DrawDebugOutline(last, Color.green);
            //Utilities.DrawDebugOutline(scriptRect, Color.red);
            //Utilities.DrawDebugOutline(clearRect, Color.red);
            //Utilities.DrawDebugOutline(resultRect, Color.red);

            EditorGUI.indentLevel = previousIndent;
        }

        /// <summary>
        /// Draw an item in the manager's list, which will be either a Foldout or a Test. This method draws only the following controls:
        /// the foldout button, the lock button, the toggle button, the label, the suite settings cog (if the item is a Suite), the script 
        /// object reference, the "clear results" button, and the test results. It does not draw the contents of the foldout for a Test object.
        /// </summary>
        public void DrawListItem(
            Object item,
            ref bool expanded, ref bool locked, ref bool selected,
            bool showFoldout = true,
            bool showLock = true,
            bool showToggle = true,
            bool showResultBackground = true,
            bool showScript = true,
            bool showClearResult = true,
            bool showResult = true,
            string name = null
        )
        {
            Test.Result result = Test.Result.None;
            if (showResult)
            {
                if (item.GetType() == typeof(Foldout)) result = (item as Foldout).GetTotalResult(this);
                else if (item.GetType() == typeof(Test)) result = (item as Test).result;
                else throw new System.NotImplementedException("Unrecognized item type " + item.GetType());
            }

            using (new EditorGUI.IndentLevelScope(indentLevel))
            {
                Rect rect = EditorGUILayout.BeginHorizontal();
                {
                    if (showResultBackground)
                    {
                        rect = new Rect(EditorGUI.IndentedRect(rect));
                        if (item.GetType() == typeof(Foldout))
                        {
                            // Skip the arrow dropdown icon
                            float foldoutWidth = Style.GetWidth("Foldout");
                            rect.x += foldoutWidth;
                            rect.width -= foldoutWidth;
                        }
                        DrawTestResult(rect, result);
                    }
                    DrawListItemLeft(item, ref expanded, ref locked, ref selected,
                        showFoldout: showFoldout,
                        showLock: showLock,
                        showToggle: showToggle,
                        name: name
                    );

                    DrawListItemRight(item,
                        showScript: showScript,
                        showClearResult: showClearResult,
                        showResult: showResult
                    );
                }
                EditorGUILayout.EndHorizontal();
            }
        }

        public void DrawTestResult(Rect rect, Test.Result result)
        {
            Color color;
            if (result == Test.Result.Fail) color = Style.failColor;
            else if (result == Test.Result.Pass) color = Style.passColor;
            else return;
            EditorGUI.DrawRect(rect, color);
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