using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Linq;

namespace UnityTest
{
    public class TestManagerUI : EditorWindow, IHasCustomMenu
    {
        public const string windowTitle = "UnityTest Manager";
        public const float minHeight = 300f;
        private const string delimiter = "\n===|TestManagerUIHeader|===\n";  // Some unique value
        private const string foldoutDelimiter = "\n===|TestManagerUIFoldout|===\n"; // Some unique value
        private const float spinRate = 0.05f;
        public const float scriptWidth = 150f;

        private static TestManagerUI _Instance;
        public static TestManagerUI Instance
        {
            get
            {
                if (_Instance == null)
                {
                    _Instance = EditorWindow.GetWindow(typeof(TestManagerUI)) as TestManagerUI;
                    _Instance.titleContent = new GUIContent(windowTitle);
                }
                return _Instance;
            }
        }

        private TestManager _manager;
        public TestManager manager
        {
            get
            {
                if (_manager == null) _manager = new TestManager();
                return _manager;
            }
        }

        private GUIQueue _guiQueue;
        private GUIQueue guiQueue
        {
            get
            {
                if (_guiQueue == null) _guiQueue = new GUIQueue(manager);
                return _guiQueue;
            }
        }

        private Foldout _rootFoldout;
        public Foldout rootFoldout
        {
            get
            {
                if (_rootFoldout == null) _rootFoldout = new Foldout(null);
                return _rootFoldout;
            }
        }

        public HashSet<Foldout> foldouts { get; private set; } = new HashSet<Foldout>();

        private Vector2 scrollPosition;

        private bool refreshing = false;

        public float indentWidth;
        public int indentLevel;

        private bool showWelcome = true;

        private float spinStartTime = 0f;
        private int spinIndex = 0;
        private bool loadingWheelVisible = false;

        private Settings _settings;
        public Settings settings
        {
            get
            {
                if (_settings == null) _settings = EditorWindow.GetWindow<Settings>(true, null, true);
                return _settings;
            }
        }

        private static GUIStyle toggleStyle;
        private static GUIStyle foldoutStyle;

        private void DoReset()
        {
            EditorPrefs.SetString(Utilities.editorPrefs, null);

            manager.Reset();
            guiQueue.Reset();

            _settings = null;
            showWelcome = true;
            indentLevel = 0;
            indentWidth = 0f;
            scrollPosition = Vector2.zero;
            _rootFoldout = null;
            refreshing = false;
            loadingWheelVisible = false;
            foldouts = new HashSet<Foldout>();

            Load();
            Debug.Log("Reset " + windowTitle);
        }

        public void AddItemsToMenu(GenericMenu menu)
        {
            menu.AddItem(new GUIContent("Reset"), false, ShowResetConfirmation);
        }

        [MenuItem("Window/UnityTest Manager")]
        public static void ShowWindow()
        {
            Instance.titleContent = new GUIContent("UnityTest Manager");
        }

        /// <summary>
        /// Called after ShowWindow but before OnEnable, and only when the window is opened.
        /// </summary>
        void Awake()
        {
            Load();
        }

        /// <summary>
        /// Called before AssemblyReloadEvents.afterAssemblyReload, and whenever the user opens the window.
        /// </summary>
        void OnEnable()
        {
            AssemblyReloadEvents.beforeAssemblyReload += Save;
            AssemblyReloadEvents.afterAssemblyReload += Load;
            EditorApplication.playModeStateChanged += OnPlayStateChanged;

            if (EditorApplication.isPlaying)
            {
                if (manager.runTestsOnPlayMode) RunSelected();
                manager.runTestsOnPlayMode = false;
            }
        }

        /// <summary>
        /// Called when the window is closed, before OnDestroy(), as well as right before assembly reload.
        /// </summary>
        void OnDisable()
        {
            AssemblyReloadEvents.beforeAssemblyReload -= Save;
            AssemblyReloadEvents.afterAssemblyReload -= Load;
            EditorApplication.playModeStateChanged -= OnPlayStateChanged;
        }

        /// <summary>
        /// Called only when the window is closed.
        /// </summary>
        void OnDestroy()
        {
            Save();
        }

        private void Refresh()
        {
            refreshing = true;
            Repaint();
            Save();
            Load();
            Repaint();
            refreshing = false;
        }



        #region Persistence Methods
        /// <summary>
        /// Save the object to EditorPrefs.
        /// </summary>
        private void Save()
        {
            EditorPrefs.SetString(Utilities.editorPrefs, GetString());
        }

        /// <summary>
        /// Parse the string saved by Save() in the EditorPrefs
        /// </summary>
        private void Load()
        {
            manager.Reset();
            CreateFoldouts();

            // Load relevant information that isn't related to the tests
            if (EditorPrefs.HasKey(Utilities.editorPrefs)) FromString(EditorPrefs.GetString(Utilities.editorPrefs));
        }


        /// <summary>
        /// Construct a string to save in the EditorPrefs. Tests themselves cannot be saved because we lack their MethodInfo objects (not serializable).
        /// Foldout.expanded needs to be saved so we can remember which Foldouts are expanded.
        /// </summary>
        private string GetString()
        {
            return string.Join(delimiter,
                showWelcome,
                manager.debug,
                guiQueue.GetString(),
                string.Join(foldoutDelimiter, foldouts.Select(x => x.GetString()))
            );
        }

        /// <summary>
        /// Set values from a data string that was saved in EditorPrefs.
        /// </summary>
        private void FromString(string data)
        {
            string[] c = data.Split(delimiter);
            try { showWelcome = bool.Parse(c[0]); }
            catch (System.Exception e) { if (!(e is System.FormatException || e is System.IndexOutOfRangeException)) throw e; }
            try { manager.debug = bool.Parse(c[1]); }
            catch (System.Exception e) { if (!(e is System.FormatException || e is System.IndexOutOfRangeException)) throw e; }
            try { guiQueue.FromString(c[2]); }
            catch (System.Exception e) { if (!(e is System.FormatException || e is System.IndexOutOfRangeException)) throw e; }
            try
            {
                Foldout foldout = null;
                foreach (string dat in c[3].Split(foldoutDelimiter))
                {
                    foldout = null;
                    try { foldout = Foldout.FromString(dat); }
                    catch(System.Exception e) { if (!(e is System.FormatException || e is System.IndexOutOfRangeException)) throw e; }
                    if (foldout == null) continue;

                    bool contains = false;
                    foreach (Foldout f in foldouts)
                    {
                        if (f.path == foldout.path)
                        {
                            f.CopyFrom(foldout);
                            contains = true;
                            break;
                        }
                    }
                    if (!contains) foldouts.Add(foldout);
                }
            }
            catch (System.Exception e) { if (!(e is System.FormatException || e is System.IndexOutOfRangeException)) throw e; }
        }




        #endregion







        private void StartLoadingWheel()
        {
            spinStartTime = Time.realtimeSinceStartup;
            loadingWheelVisible = true;
        }

        private void StopLoadingWheel()
        {
            loadingWheelVisible = false;
        }

        private void CreateFoldouts()
        {
            if (foldouts == null) foldouts = new HashSet<Foldout>();

            // Tests have already been created.
            foreach (Test test in manager.tests.Values)
            {
                // Ensure that Foldouts are created at each level to support this Test
                Foldout final = null;
                foreach (string p in Utilities.IterateDirectories(test.attribute.GetPath(), true))
                {
                    if (Foldout.ExistsAtPath(p)) final = Foldout.GetAtPath(p);
                    else
                    {
                        final = new Foldout(p);
                        foldouts.Add(final);
                    }
                }
                final.tests.Add(test);
            }
        }



        private void GoToEmptyScene()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
            Debug.Log("Created an empty scene");
        }

        private void RunSelected()
        {
            foreach (Test test in rootFoldout.GetTests())
                if (test.selected) manager.queue.Enqueue(test);// test.Run();
        }

        private void ResetSelected()
        {
            foreach (Test test in rootFoldout.GetTests())
                if (test.selected) test.Reset();
        }
        private void ResetAll()
        {
            foreach (Test test in manager.tests.Values) test.Reset();
        }

        private void SelectAll() => rootFoldout.Select(); // Simulate a press

        private void DeselectAll() => rootFoldout.Deselect(); // Simulate a press

        private void OnPlayStateChanged(PlayModeStateChange change)
        {
            manager.OnPlayStateChanged(change);
            if (change == PlayModeStateChange.EnteredPlayMode && Utilities.IsSceneEmpty()) Focus();
            //if (change == PlayModeStateChange.EnteredEditMode) Repaint();
            //if (change == PlayModeStateChange.ExitingEditMode) Save();
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
            if (manager.running && !manager.paused) Repaint(); // keeps the frame counter and timer up-to-date
        }







        #region Drawing Methods
        void OnGUI()
        {
            State state = new State(rootFoldout);

            bool refresh = false;
            bool selectAll = false;
            bool deselectAll = false;

            bool wasEnabled = GUI.enabled;
            // The main window
            EditorGUILayout.BeginVertical();
            {
                GUI.enabled &= !loadingWheelVisible;
                // Toolbar controls
                EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
                {
                    // Left
                    DrawPlayButton(state);
                    DrawPauseButton();
                    DrawGoToEmptySceneButton();

                    bool[] result = DrawUniversalToggle(state);
                    selectAll = result[0];
                    deselectAll = result[1];

                    DrawClearButton(state);


                    // Left
                    GUILayout.FlexibleSpace();
                    // Right


                    DrawWelcomeButton();
                    DrawDebugButton();
                    refresh = DrawRefreshButton();
                    // Right
                }
                EditorGUILayout.EndHorizontal();

                if (showWelcome) // Welcome message
                {
                    EditorGUILayout.HelpBox(
                        "Welcome to UnityTest! To get started, select a test below and click the Play button in the toolbar. " +
                        "Press the X button in the toolbar to clear test results. You can open the code for each test by double-clicking its script object. " +
                        "Create your tests in any C# class in the Assets folder by simply writing a method with a UnityTest.Test attribute. " +
                        "See the included README for additional information. Happy testing!" + "\n\n" +
                        "If you would like to support this project, please donate at _____________________. Any amount is greatly appreciated; it keeps me fed :)" + "\n\n" +
                        "To hide this message, press the speech bubble in the toolbar above."
                    , MessageType.Info);
                }
                GUI.enabled = wasEnabled;

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
                    // The box that shows the Tests
                    scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, false, false, GUI.skin.horizontalScrollbar, GUI.skin.verticalScrollbar, EditorStyles.inspectorFullWidthMargins);
                    if (rootFoldout != null)
                    {
                        indentLevel = 0;
                        foreach (Foldout child in rootFoldout.GetChildren(false)) child.Draw();
                    }
                    EditorGUILayout.EndScrollView();
                }
            }
            EditorGUILayout.EndVertical();

            if (guiQueue != null)
            {
                GUI.enabled = wasEnabled && !loadingWheelVisible;
                guiQueue.Draw();
                GUI.enabled = wasEnabled;
            }

            if (!loadingWheelVisible)
            {
                // Doing things this way avoids annoying GUI errors complaining about groups not being ended properly.
                if (refresh) Refresh();
                else if (selectAll) SelectAll();
                else if (deselectAll) DeselectAll();
            }
        }

        /// <summary>
        /// Draw the loading wheel shown during a refresh and whenever the assemblies are being checked for tests.
        /// </summary>
        private void DrawLoadingWheel(Rect rect, string text)
        {
            float time = Time.realtimeSinceStartup;
            if (time - spinStartTime >= spinRate)
            {
                spinIndex++;
                if (spinIndex > 11) spinIndex = 0;
                spinStartTime = time;
            }
            GUIContent content = EditorGUIUtility.IconContent("d_WaitSpin" + spinIndex.ToString("00"));
            content.text = text;

            GUIStyle style = new GUIStyle(EditorStyles.largeLabel);
            style.alignment = TextAnchor.MiddleCenter;
            style.imagePosition = ImagePosition.ImageAbove;
            GUI.Label(rect, content, style);
        }

        private bool[] DrawUniversalToggle(State state)
        {
            // This is all just to draw the toggle button in the toolbar
            GUIContent toggle = new GUIContent(EditorGUIUtility.IconContent("d_toggle_on")); // guess at initial value
            GUIStyle toggleStyle = EditorStyles.toolbarButton;

            Rect rect = GUILayoutUtility.GetRect(toggle, toggleStyle);

            bool hover = false;
            if (Event.current != null) hover = rect.Contains(Event.current.mousePosition) && GUI.enabled;

            // Change the style of the toggle button according to the current state
            if (state.anySelected && !state.allSelected)
            {
                if (hover) toggle.image = EditorGUIUtility.IconContent("d_toggle_mixed_bg_hover").image;
                else toggle.image = EditorGUIUtility.IconContent("d_toggle_mixed_bg").image;
            }
            else
            {
                if (state.allSelected)
                {
                    if (hover) toggle.image = EditorGUIUtility.IconContent("d_toggle_on_hover").image;
                }
                else if (!state.anySelected)
                {
                    if (hover) toggle.image = EditorGUIUtility.IconContent("d_toggle_bg_hover").image;
                    else toggle.image = EditorGUIUtility.IconContent("d_toggle_bg").image;
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
            bool wasEnabled = GUI.enabled;
            GUIContent clear = new GUIContent(EditorGUIUtility.IconContent("d_clear"));
            clear.tooltip = "Clear selected Test results";
            GUI.enabled &= state.selectedHaveResults;

            Rect clearRect = GUILayoutUtility.GetRect(clear, EditorStyles.toolbarDropDown);
            if (EditorGUI.DropdownButton(clearRect, clear, FocusType.Passive, EditorStyles.toolbarDropDown))
            {
                GenericMenu toolsMenu = new GenericMenu();
                if (state.anySelected) toolsMenu.AddItem(new GUIContent("Reset Selected"), false, ResetSelected);
                else toolsMenu.AddDisabledItem(new GUIContent("Reset Selected"));

                if (state.anyResults) toolsMenu.AddItem(new GUIContent("Reset All"), false, ResetAll);
                else toolsMenu.AddDisabledItem(new GUIContent("Reset All"));

                toolsMenu.DropDown(clearRect);
            }
            GUI.enabled = wasEnabled;
        }



        private void DrawPlayButton(State state)
        {
            bool wasEnabled = GUI.enabled;

            string image = "PlayButton";
            string tooltip = "Run selected tests";
            if (manager.running)
            {
                image += " On";
                tooltip = "Stop testing";
            }

            GUIContent content = new GUIContent(EditorGUIUtility.IconContent(image));
            content.tooltip = tooltip;

            GUI.enabled &= state.anySelected;
            bool current = GUILayout.Toggle(manager.running, content, EditorStyles.toolbarButton);
            GUI.enabled = wasEnabled;

            if (manager.running != current) // The user clicked on the button
            {
                if (manager.running)
                {
                    manager.Stop();
                }
                else
                {
                    if (EditorApplication.isPlaying) RunSelected();
                    else
                    {
                        manager.runTestsOnPlayMode = true;
                        //Save();
                        EditorApplication.EnterPlaymode(); // can cause recompile
                    }
                }
            }
        }

        private void DrawPauseButton()
        {
            bool wasEnabled = GUI.enabled;

            string image = "PauseButton";
            string tooltip = "Pause testing";
            if (manager.running)
            {
                image += " On";
                tooltip = "Resume testing";
            }

            GUIContent content = new GUIContent(EditorGUIUtility.IconContent(image));
            content.tooltip = tooltip;

            GUI.enabled &= manager.running;
            manager.paused = GUILayout.Toggle(manager.paused, content, EditorStyles.toolbarButton);
            GUI.enabled = wasEnabled;
        }

        private void DrawGoToEmptySceneButton()
        {
            bool wasEnabled = GUI.enabled;
            GUIContent emptyScene = new GUIContent(EditorGUIUtility.IconContent("SceneLoadIn"));
            emptyScene.tooltip = "Go to empty scene";
            GUI.enabled &= !Utilities.IsSceneEmpty() && !EditorApplication.isPlaying;
            if (GUILayout.Button(emptyScene, EditorStyles.toolbarButton))
            {
                GoToEmptyScene();
                GUIUtility.ExitGUI();
            }
            GUI.enabled = wasEnabled;
        }

        private void DrawDebugButton()
        {
            GUIContent debugContent = new GUIContent(EditorGUIUtility.IconContent("d_DebuggerDisabled"));
            if (manager.debug) debugContent.image = EditorGUIUtility.IconContent("d_DebuggerAttached").image;
            debugContent.tooltip = "Enable/disable debug messages";
            manager.debug = GUILayout.Toggle(manager.debug, debugContent, EditorStyles.toolbarButton);
        }

        private bool DrawRefreshButton()
        {
            GUIContent refreshContent = new GUIContent(EditorGUIUtility.IconContent("Refresh"));
            refreshContent.tooltip = "Refresh Test methods and classes by searching all assemblies";
            return GUILayout.Button(refreshContent, EditorStyles.toolbarButton);
        }

        private void DrawWelcomeButton()
        {
            GUIContent image = new GUIContent(EditorGUIUtility.IconContent("console.infoicon.sml"));
            image.tooltip = "Show/hide the welcome message";
            showWelcome = GUILayout.Toggle(showWelcome, image, EditorStyles.toolbarButton);
        }

        /// <summary>
        /// Say "are you sure?"
        /// </summary>
        private void ShowResetConfirmation()
        {
            //if (EditorPrefs.GetString(Utilities.editorPrefs) == GetString(manager.GetTests())) return; // no data to lose
            if (!EditorUtility.DisplayDialog("Reset UnityTest Manager?", "Are you sure? This will clear all saved information about tests, GameObjects, etc. " +
                "If you have encountered a bug, first try closing the UnityTest Manager and opening it again.",
                "Yes", "No"
            //DialogOptOutDecisionType.ForThisMachine, "reset UnityTest Manager"
            )) return;
            // User clicked "OK"
            DoReset();
        }

        #endregion


        #region Test drawing
        public static GUIStyle GetToggleStyle()
        {
            if (toggleStyle == null)
            {
                toggleStyle = new GUIStyle(EditorStyles.iconButton);
                toggleStyle.alignment = EditorStyles.toggle.alignment;
                toggleStyle.fixedWidth = EditorStyles.toggle.fixedWidth;
                toggleStyle.fixedHeight = EditorStyles.toggle.fixedHeight;
                toggleStyle.font = EditorStyles.toggle.font;
                toggleStyle.fontStyle = EditorStyles.toggle.fontStyle;
                toggleStyle.fontSize = EditorStyles.toggle.fontSize;
                toggleStyle.clipping = EditorStyles.toggle.clipping;
                toggleStyle.border = EditorStyles.toggle.border;
                toggleStyle.contentOffset = EditorStyles.toggle.contentOffset;
                toggleStyle.imagePosition = EditorStyles.toggle.imagePosition;
                toggleStyle.margin = EditorStyles.toggle.margin;
                toggleStyle.overflow = EditorStyles.toggle.overflow;
                toggleStyle.padding = EditorStyles.toggle.padding;
                toggleStyle.richText = EditorStyles.toggle.richText;
                toggleStyle.stretchHeight = EditorStyles.toggle.stretchHeight;
                toggleStyle.stretchWidth = EditorStyles.toggle.stretchWidth;
                toggleStyle.wordWrap = EditorStyles.toggle.wordWrap;

                toggleStyle.padding.left = 0;
            }
            return toggleStyle;
        }

        public static GUIStyle GetFoldoutStyle()
        {
            if (foldoutStyle == null)
            {
                foldoutStyle = new GUIStyle(EditorStyles.foldout);
                foldoutStyle.padding = new RectOffset(0, 0, 0, 0);
                foldoutStyle.overflow = new RectOffset(0, 0, 0, 0);
                foldoutStyle.contentOffset = Vector2.zero;
                foldoutStyle.margin = new RectOffset(0, 0, 0, 0);

            }
            return foldoutStyle;
        }

        public static void PaintResultFeatures(Rect rect, Test.Result result)
        {
            if (result == Test.Result.Fail)
            {
                EditorGUI.DrawRect(rect, new Color(1f, 0f, 0f, 0.1f));
            }
        }

        public static List<bool> DrawToggle(Rect rect, string name, bool selected, bool locked, bool showLock = true, bool isMixed = false)
        {
            bool wasMixed = EditorGUI.showMixedValue;
            EditorGUI.showMixedValue = isMixed;

            // Draw the light highlight when the toggle is selected
            if (selected)
            {
                Rect r = new Rect(rect);
                float w = EditorStyles.toggle.padding.left;
                r.x += w;
                r.width -= w;
                EditorGUI.DrawRect(r, new Color(1f, 1f, 1f, 0.05f));
            }

            // Draw the lock button
            if (showLock)
            {
                Rect lockRect = new Rect(rect);
                lockRect.width = EditorStyles.toggle.CalcSize(GUIContent.none).x;
                locked = GUI.Toggle(lockRect, locked, GUIContent.none, "IN LockButton");
                rect.x += lockRect.width;
                rect.width -= lockRect.width;
            }

            // Draw the toggle
            bool wasEnabled = GUI.enabled;
            GUI.enabled &= !locked;
            selected = EditorGUI.ToggleLeft(rect, name, selected, GetToggleStyle());

            GUI.enabled = wasEnabled;

            EditorGUI.showMixedValue = wasMixed;

            return new List<bool> { selected, locked };
        }

        public static void DrawTest(Rect rect, Test test, bool showLock = true, bool showFoldout = true, bool allowExpand = true)
        {
            bool wasEnabled = GUI.enabled;

            float toggleWidth = EditorStyles.toggle.CalcSize(GUIContent.none).x;

            // Draw the expanded box first so it appears behind everything else
            if (test.expanded && allowExpand && !test.IsInSuite())
            {
                float h = GUI.skin.label.CalcHeight(GUIContent.none, rect.width) + GUI.skin.label.margin.vertical;

                GUIStyle boxStyle = new GUIStyle("GroupBox");
                boxStyle.padding = GUI.skin.label.padding;
                boxStyle.margin = new RectOffset((int)rect.x, (int)(0.5f * boxStyle.border.right), 0, 0);
                boxStyle.padding.left = (int)(toggleWidth * 2);
                boxStyle.padding.right -= boxStyle.margin.right;

                rect.y += 0.5f * boxStyle.padding.top;

                GUILayout.Space(-h); // Move the box closer to the Test foldout above
                GUILayout.BeginHorizontal(boxStyle); // This is so we can shift the GroupBox drawing to the right
                {
                    GUILayout.BeginVertical();
                    {
                        GUILayout.Space(h);

                        test.defaultGameObject = EditorGUI.ObjectField(
                            EditorGUILayout.GetControlRect(true),
                            new GUIContent("Default Prefab", "Provide a prefab from the Project folder. If the " +
                                "default SetUp method is used in this test then it will receive an instantiated copy of this prefab."),
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
                foldoutRect.width = toggleWidth;
                test.expanded = allowExpand && GUI.Toggle(foldoutRect, test.expanded && allowExpand, GUIContent.none, GetFoldoutStyle());

                Rect scriptRect = new Rect(rect);
                scriptRect.x = rect.xMax - scriptWidth;
                scriptRect.width = scriptWidth;
                GUI.enabled = false;
                float previousLabelWidth = EditorGUIUtility.labelWidth;
                EditorGUIUtility.labelWidth = 0f;
                EditorGUI.ObjectField(scriptRect, GUIContent.none, test.GetScript(), test.method.DeclaringType, false);
                EditorGUIUtility.labelWidth = previousLabelWidth;
                GUI.enabled = wasEnabled;
            }

            Rect toggleRect = new Rect(rect);
            toggleRect.x += toggleWidth;
            toggleRect.width -= toggleWidth;
            if (!test.IsInSuite()) toggleRect.width -= scriptWidth;

            List<bool> res = DrawToggle(toggleRect, test.attribute.name, test.selected, test.locked, showLock, false);
            test.selected = res[0];
            test.locked = res[1];

            GUI.enabled = wasEnabled;
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


                //Debug.Log(property.propertyType);

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
        #endregion
    }
}