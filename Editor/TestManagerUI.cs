using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.IO;
using System.Threading.Tasks;

namespace UnityTest
{
    public class TestManagerUI : EditorWindow, IHasCustomMenu
    {
        private TestManager _manager;
        private TestManager manager
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

        public static HashSet<Foldout> foldouts { get; private set; } = new HashSet<Foldout>();

        private Vector2 scrollPosition;

        private bool refreshing = false;

        public const float minHeight = 300f;

        private const string headerDelimiter = "\n===|TestManagerUIHeader|===\n";  // Some unique value
        private const string delimiter = "\n===|TestManagerUI|===\n"; // Some unique value
        private const string splitDelimiter = "\n===|TestSplit|===\n"; // Some unique value
        private const string foldoutDelimiter = "\n===|TestManagerUIFoldout|===\n"; // Some unique value

        public static float indentWidth;
        public static int indentLevel;

        private static bool debug = true;
        private static bool showWelcome = true;

        private static float spinStartTime = 0f;
        private const float spinRate = 0.05f;
        private static int spinIndex = 0;
        private static bool loadingWheelVisible = false;

        private static Settings _settings;
        public static Settings settings
        {
            get
            {
                if (_settings == null) _settings = EditorWindow.GetWindow<Settings>(true, null, true);
                return _settings;
            }
        }

        private static bool startCalled;
        private static bool assemblyReloading = false;

        private void DoReset()
        {
            EditorPrefs.SetString(Utilities.editorPrefs, null);

            manager.Reset();
            OnDisable();
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
            assemblyReloading = false;
            
            OnEnable();
        }

        public void AddItemsToMenu(GenericMenu menu)
        {
            menu.AddItem(new GUIContent("Reset"), false, ShowResetConfirmation);
        }

        [MenuItem("Window/UnityTest Manager")]
        public static void ShowWindow()
        {
            EditorWindow window = EditorWindow.GetWindow(typeof(TestManagerUI));
            window.titleContent = new GUIContent("UnityTest Manager");
        }


        void OnEnable()
        {
            AssemblyReloadEvents.beforeAssemblyReload += BeforeAssemblyReload;
            AssemblyReloadEvents.afterAssemblyReload += AfterAssemblyReload;
            
            EditorApplication.playModeStateChanged += OnPlayStateChanged;
            manager.onStartUpdatingMethods += StartLoadingWheel;
            manager.onFinishUpdatingMethods += OnMethodsUpdated;

            if (!assemblyReloading)
            {
                manager.StartUpdatingMethods();
                //Load();
                //manager.CreateTests();
                //CreateFoldouts();
                //StopLoadingWheel();
                //Repaint();
            }
        }
        void OnDisable()
        {
            AssemblyReloadEvents.beforeAssemblyReload -= BeforeAssemblyReload;
            AssemblyReloadEvents.afterAssemblyReload -= AfterAssemblyReload;

            EditorApplication.playModeStateChanged -= OnPlayStateChanged;
            manager.onStartUpdatingMethods -= StartLoadingWheel;
            manager.onFinishUpdatingMethods -= OnMethodsUpdated;

            Save();
        }

        private void BeforeAssemblyReload()
        {
            Save();
            assemblyReloading = true;
        }

        private void AfterAssemblyReload()
        {
            assemblyReloading = false;
            manager.StartUpdatingMethods(); // We do everything else in OnMethodsUpdated, after checking the assemblies for tests
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


        private void CreateFoldout(TestAttribute attribute)
        {
            string path = attribute.GetPath();

            Test newTest;
            //if (manager.cachedTests.ContainsKey(path)) newTest = manager.cachedTests[path];
            if (manager.tests.ContainsKey(attribute)) newTest = manager.tests[attribute];
            else newTest = manager.tests[attribute];

            Foldout directory = rootFoldout;
            foreach (string p in Utilities.IterateDirectories(Path.GetDirectoryName(path), true))
            {
                Foldout newFoldout;
                if (Foldout.ExistsAtPath(p)) newFoldout = Foldout.GetAtPath(p);
                else
                {
                    newFoldout = new Foldout(p);
                    foldouts.Add(newFoldout);
                }
                directory = newFoldout;
            }
            // After traversing the directories, we end in the correct directory needed for this test
            directory.Add(newTest);
        }

        private void CreateFoldouts()
        {
            if (foldouts == null) foldouts = new HashSet<Foldout>();

            // For all the attributes, create Foldouts and Tests
            foreach (TestAttribute attribute in manager.methods.Keys)
            {
                if (Foldout.ExistsAtPath(attribute.GetPath())) continue;
                CreateFoldout(attribute);
            }
        }

        private async void Refresh()
        {
            refreshing = true;

            var oldMethods = manager.methods;
            spinStartTime = Time.realtimeSinceStartup;
            await Task.Run(manager.UpdateMethods);
            Repaint();

            bool needUpdate = false;
            foreach (TestAttribute attribute in manager.methods.Keys)
            {
                if (!manager.methods.ContainsKey(attribute))
                {
                    needUpdate = true;
                    break;
                }
            }
            if (!needUpdate)
            {
                foreach (TestAttribute attribute in oldMethods.Keys)
                {
                    if (!manager.methods.ContainsKey(attribute))
                    {
                        needUpdate = true;
                        break;
                    }
                }
            }

            if (needUpdate)
            {
                // Hard refresh of everything
                OnEnable();
            }

            refreshing = false;
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





        /// <summary>
        /// Implementing my own Start function because we need it
        /// </summary>
        private void Start()
        {
            startCalled = true;

            if (EditorApplication.isPlaying)
            {
                if (manager.runTestsOnPlayMode) RunSelected();
                manager.runTestsOnPlayMode = false;
            }
        }

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
            if (!startCalled) Start();
            if (!EditorApplication.isPlaying) return;
            manager.Update();
            if (manager.running && !manager.paused) Repaint(); // keeps the frame counter and timer up-to-date
        }

        

        /// <summary>
        /// Called after the manager is finished checking assemblies for tests.
        /// </summary>
        private void OnMethodsUpdated()
        {
            Load();
            manager.CreateTests();
            CreateFoldouts();
            StopLoadingWheel();
            Repaint();
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
            if (debug) debugContent.image = EditorGUIUtility.IconContent("d_DebuggerAttached").image;
            debugContent.tooltip = "Enable/disable debug messages";
            debug = GUILayout.Toggle(debug, debugContent, EditorStyles.toolbarButton);
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
            if (EditorPrefs.GetString(Utilities.editorPrefs) == GetString(manager.GetTests())) return; // no data to lose
            if (!EditorUtility.DisplayDialog("Reset UnityTest Manager?", "Are you sure? This will clear all saved information about tests, GameObjects, etc. " +
                "If you have encountered a bug, first try closing the UnityTest Manager and opening it again.",
                "Yes", "No"
            //DialogOptOutDecisionType.ForThisMachine, "reset UnityTest Manager"
            )) return;
            // User clicked "OK"
            DoReset();
        }
        #endregion



        #region Persistence Methods
        /// <summary>
        /// Construct a string to save in the EditorPrefs
        /// </summary>
        private string GetString(SortedDictionary<TestAttribute, Test> tests)
        {
            string GetHeader()
            {
                return string.Join(headerDelimiter,
                    showWelcome,
                    manager.GetString(),
                    guiQueue.GetString()
                );
            }

            string GetTestsString()
            {
                if (tests.Count == 0) return null;
                List<Test> ts = new List<Test>(tests.Values);
                string[] strings = new string[ts.Count];
                for (int i = 0; i < ts.Count; i++) strings[i] = ts[i].GetString();
                return string.Join(delimiter, strings);
            }

            string GetFoldoutString()
            {
                if (foldouts == null) return null;
                List<Foldout> _foldouts = new List<Foldout>(foldouts);
                string[] strings = new string[_foldouts.Count];
                for (int i = 0; i < _foldouts.Count; i++) strings[i] = _foldouts[i].GetString();
                return string.Join(foldoutDelimiter, strings);
            }

            string header = GetHeader();
            string testData = GetTestsString();
            string foldoutsData = GetFoldoutString();

            string data = "";
            if (!string.IsNullOrEmpty(header)) data += header;
            data += splitDelimiter;

            if (!string.IsNullOrEmpty(testData)) data += testData;
            data += splitDelimiter;

            if (!string.IsNullOrEmpty(foldoutsData)) data += foldoutsData;

            return data;
        }


        private void FromString(string data)
        {
            void ParseHeader(string s)
            {
                string[] c = s.Split(headerDelimiter);
                showWelcome = bool.Parse(c[0]);
                manager.FromString(c[1]);
                guiQueue.FromString(c[2]);
            }

            void ParseTests(string s)
            {
                manager.tests.Clear();
                foreach (string item in s.Split(delimiter))
                {
                    Test newTest = Test.FromString(item);
                    if (newTest == null) continue;
                    manager.tests.Add(newTest.attribute, newTest);
                }
            }

            void ParseFoldouts(string s)
            {
                foldouts.Clear();

                List<string> added = new List<string>();
                foreach (string foldoutData in s.Split(foldoutDelimiter))
                {
                    if (Foldout.Exists(foldoutData)) continue;
                    foldouts.Add(Foldout.CreateFromData(foldoutData));
                }
            }

            // The string always has 3 splitDelimiters
            string[] sections = data.Split(splitDelimiter);
            if (sections.Length != 3) return; // expected length

            try { ParseHeader(sections[0]); }
            catch (System.FormatException) { }

            try { ParseTests(sections[1]); }
            catch (System.FormatException) { }

            try { ParseFoldouts(sections[2]); }
            catch (System.FormatException) { }
        }



        /// <summary>
        /// Save the object to EditorPrefs.
        /// </summary>
        private void Save()
        {
            EditorPrefs.SetString(Utilities.editorPrefs, GetString(manager.tests));
        }

        /// <summary>
        /// Parse the string saved by Save() in the EditorPrefs
        /// </summary>
        private void Load()
        {
            string data = EditorPrefs.GetString(Utilities.editorPrefs, GetString(manager.GetTests()));
            // Overwrite the created tests with their appropriate saved values
            FromString(data);


            // If a test was removed since last load, we need to update our Foldouts accordingly
            HashSet<string> expected = new HashSet<string>(); // HashSet = unique values only
            foreach (Test test in manager.tests.Values) //manager.cachedTests.Values)
            {
                expected.Add(Path.GetDirectoryName(test.attribute.GetPath()));
            }

            
            foreach (Foldout foldout in new List<Foldout>(foldouts))
            {
                if (expected.Contains(foldout.path)) continue;
                foldouts.Remove(foldout);
            }
        }
        #endregion
    }
}