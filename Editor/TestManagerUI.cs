using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace GameTest
{
    public class TestManagerUI : EditorWindow, IHasCustomMenu
    {
        public int indentLevel;
        private float spinStartTime = 0f;
        private int spinIndex = 0;
        public UnityEditor.IMGUI.Controls.SearchField searchField { get; private set; }

        public System.Action onLostFocus, onFocus;

        public TestManager manager;

        public SettingsWindow settingsWindow;

        public Rect viewRect = new Rect(0f, 0f, -1f, -1f);
        public Rect itemRect;
        public Rect scrollRect;

        public float minWidth = 0f;

        private bool reloadingDomain = false;

        public Change change;

        public bool drawingMainView = false;
        private Dictionary<string, Rect> testRects = new Dictionary<string, Rect>();

        public class Change
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

        public enum UIEvent
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


        #region Window
        public void AddItemsToMenu(GenericMenu menu)
        {
            menu.AddItem(new GUIContent("Preferences..."), false, () => PreferencesWindow.ShowWindow());
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
        #endregion


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
            PreferencesWindow.CloseAll();

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
            PreferencesWindow.CloseAll();
        }

        private void OnAfterAssemblyReload()
        {
            manager = TestManager.Load();
            reloadingDomain = false;
            Refresh();
        }

        private void OnPlayStateChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.EnteredPlayMode && Utilities.IsSceneEmpty()) Focus();

            if (manager != null) manager.OnPlayStateChanged(change);

            // If we don't Repaint() here, then the toolbar buttons can appear incorrect. This should always happen as the very last thing.
            Repaint();
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


        #region Reset and Refresh
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

        public void Refresh(System.Action onFinished = null, string message = "Refreshing")
        {
            if (manager != null) manager.OnBeforeTestManagerUIRefresh();
            StartLoadingWheel(message);
            Repaint();

            Test previousSettingsTest = null;
            if (settingsWindow != null) previousSettingsTest = settingsWindow.GetTest();

            if (manager != null)
                manager.UpdateTests(() =>
                {
                    TestManagerTestView.UpdateFoldoutStates(manager);
                    StopLoadingWheel();
                    Repaint();

                    if (onFinished != null) onFinished();

                    manager.OnAfterTestManagerUIRefresh();
                });
        }

        public void ResetSelected()
        {
            if (manager == null) return;
            foreach (Test test in manager.GetTests())
                if (test.selected) test.Reset();
            TestManagerTestView.UpdateFoldoutStates(manager);
        }
        public void ResetAll()
        {
            if (manager == null) return;
            foreach (Test test in manager.GetTests()) test.Reset();
            TestManagerTestView.UpdateFoldoutStates(manager);
        }
        #endregion


        #region UI
        void OnGUI()
        {
            if (reloadingDomain) return;

            testRects = new Dictionary<string, Rect>();

            if (manager == null) manager = TestManager.Load();
            if (settingsWindow != null && !manager.testsVisible) settingsWindow.Close();

            Utilities.isDarkTheme = GUI.skin.name == "DarkSkin";

            UnityEngine.Profiling.Profiler.BeginSample(nameof(GameTest), this);

            using (EditorGUILayout.VerticalScope mainScope = new EditorGUILayout.VerticalScope(GUILayout.ExpandHeight(true), GUILayout.ExpandWidth(true)))
            {
                using (new EditorGUI.DisabledScope(manager.loadingWheelVisible))
                {
                    // The main window
                    using (new EditorGUILayout.VerticalScope(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true)))
                    {
                        // Toolbar controls
                        TestManagerToolbar.Draw(this);

                        // The box that shows the Foldouts and Tests
                        TestManagerTestView.Draw(this);
                    }

                    manager.guiQueue.Draw();
                }

                if (manager.loadingWheelVisible) DrawLoadingWheel(mainScope.rect);
            }
            
            if (change != null) ProcessChange();

            UnityEngine.Profiling.Profiler.EndSample();
        }

        public Mode GetMode()
        {
            if (manager == null) return Mode.Normal;
            if (!string.IsNullOrEmpty(manager.search)) return Mode.Search;
            return Mode.Normal;
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

        /// <summary>
        /// Say "are you sure?" If the answer is yes, call DoReset(), which resets the TestManager, resets the UI, and then calls Refresh() to
        /// locate Tests in the user's project.
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
                                        wasSelected,
                                        toggleStyle
                                    );
                                    EditorGUI.showMixedValue = wasMixed;

                                    // We have to limit the change check here to just what the user has clicked
                                    // Otherwise, toggles in the UI think they have been clicked on when they really haven't
                                    // It has to do with cascading changes as a result of a user click
                                    if (wasSelected != selected && change == null && Utilities.IsMouseOverRect(tempRect))
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
                            using (new EditorGUI.DisabledScope(!manager.testsVisible))
                            {
                                if (GUI.Button(settingsRect, settingsIcon, settingsStyle))
                                {
                                    if (settingsWindow == null) settingsWindow = EditorWindow.GetWindow<SettingsWindow>(true);
                                    settingsWindow.Init(item as Test);
                                    settingsWindow.ShowUtility();
                                }
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
            Repaint();
        }
        #endregion
    }
}