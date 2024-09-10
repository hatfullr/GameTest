using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.Reflection;
using System.Linq;
using UnityEditor.SceneManagement;

namespace UnityTest
{
    public class TestManager : EditorWindow, IHasCustomMenu
    {
        /// <summary>
        /// The code methods which have a TestAttribute attached to them.
        /// </summary>
        private static SortedDictionary<TestAttribute, MethodInfo> methods = new SortedDictionary<TestAttribute, MethodInfo>();

        /// <summary>
        /// The Test objects associated with the methods that have a TestAttribute attached to them.
        /// </summary>
        private static SortedDictionary<TestAttribute, Test> tests = new SortedDictionary<TestAttribute, Test>();

        private static SortedDictionary<string, Foldout> cachedFoldouts = new SortedDictionary<string, Foldout>();
        private static SortedDictionary<string, Test> cachedTests = new SortedDictionary<string, Test>();

        public static Foldout rootFoldout;

        private bool refreshing = false;

        private Vector2 scrollPosition, queueScrollPosition;
        
        private const string delimiter = "\n===| TestManager |===\n"; // Some unique value
        private const string splitDelimiter = "\n===| TestSplit |===\n"; // Some unique value
        private const string foldoutDelimiter = "\n===| TestManagerFoldout |===\n"; // Some unique value

        private Queue<Test> queue = new Queue<Test>();

        private float timer;
        private int nframes;

        private static float indentWidth;
        private static int indentLevel;

        private static bool debug;
        private static bool showWelcome = true;
        private static bool playButtonPressed = false;

        private int previousFrameNumber;

        private Dictionary<string, GUIStyle> styles;

        private static string[] fonts;

        private static Settings _settings;
        private static Settings settings
        {
            get
            {
                if (_settings == null) _settings = EditorWindow.GetWindow<Settings>(true, null, true);
                return _settings;
            }
        }

        private void Reset()
        {
            OnDisable();
            EditorPrefs.SetString(nameof(TestManager), "");

            _settings = null;
            previousFrameNumber = 0;
            showWelcome = true;
            debug = false;
            timer = 0f;
            indentLevel = 0;
            indentWidth = 0f;
            nframes = 0;
            queue = new Queue<Test>();
            cachedTests = new SortedDictionary<string, Test>();
            cachedFoldouts = new SortedDictionary<string, Foldout>();
            tests = new SortedDictionary<TestAttribute, Test>();
            methods = new SortedDictionary<TestAttribute, MethodInfo>();
            rootFoldout = null;
            refreshing = false;
            scrollPosition = Vector2.zero;
            queueScrollPosition = Vector2.zero;
            fonts = null;
            playButtonPressed = false;

            Foldout.all = new Dictionary<string, Foldout>();

            
            OnEnable();
        }

        public void AddItemsToMenu(GenericMenu menu)
        {
            menu.AddItem(new GUIContent("Reset"), false, Reset);
        }

        private void InitializeStyles()
        {
            if (styles == null) styles = new Dictionary<string, GUIStyle>();
            styles.Clear();

            fonts = Font.GetOSInstalledFontNames();

            GUIStyle boxStyle = new GUIStyle("Window");
            GUIStyle g = new GUIStyle("GroupBox");
            boxStyle.padding = new RectOffset(g.padding.left * 2, g.padding.right * 2, g.padding.top * 2, g.padding.bottom * 2);
            boxStyle.fixedHeight = g.fixedHeight;
            boxStyle.fixedWidth = g.fixedWidth;
            boxStyle.stretchWidth = g.stretchWidth;
            boxStyle.stretchHeight = g.stretchHeight;
            boxStyle.clipping = g.clipping;
            boxStyle.contentOffset = g.contentOffset;
            boxStyle.overflow = g.overflow;
            boxStyle.richText = g.richText;
            styles.Add("box", boxStyle);

            GUIStyle boxStyle2 = new GUIStyle(boxStyle);
            boxStyle2.padding = new RectOffset(boxStyle.padding.left / 2, boxStyle.padding.right / 2, boxStyle.padding.top / 2, boxStyle.padding.bottom / 2 + 4);
            boxStyle2.overflow = new RectOffset(0, -1, -2, 0); // Fixes an issue with the toolbar not appearing correctly
            styles.Add("box2", boxStyle2);
        }
        private GUIStyle GetStyle(string name) => styles[name];

        [MenuItem("Window/UnityTest Manager")]
        public static void ShowWindow()
        {
            EditorWindow window = EditorWindow.GetWindow(typeof(TestManager));
            window.titleContent = new GUIContent("UnityTest Manager");
        }

        public static bool IsSceneEmpty()
        {
            GameObject[] objects = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
            if (objects == null) return true;
            if (objects.Length == 0) return true;
            if (objects.Length != 2) return false;
            return objects[0].name == "MainCamera" && objects[1].name == "Directional Light";
        }

        /// <summary>
        /// Construct a string to save in the EditorPrefs
        /// </summary>
        private string GetString(SortedDictionary<TestAttribute, Test> tests)
        {
            string data = "";

            data += debug.ToString();
            data += splitDelimiter;
            data += showWelcome.ToString();
            data += splitDelimiter;
            data += playButtonPressed.ToString();
            data += splitDelimiter;

            if (tests.Count == 0) return data;

            List<TestAttribute> attributes = new List<TestAttribute>();
            foreach (TestAttribute attribute in tests.Keys) attributes.Add(attribute);
            
            foreach (TestAttribute attribute in attributes)
            {
                data += tests[attribute].GetString() + delimiter;
            }

            data += splitDelimiter;

            if (Foldout.all == null) Foldout.all = new Dictionary<string, Foldout>();
            foreach (Foldout foldout in Foldout.all.Values)
            {
                data += foldout.GetString() + foldoutDelimiter;
            }

            //Debug.Log(data);
            return data;
        }

        /// <summary>
        /// Returns true if the number of Tests didn't change. Otherwise, returns false.
        /// </summary>
        private void FromString(string data)
        {
            cachedTests.Clear();
            string[] split = data.Split(splitDelimiter);
            int i = 0;

            if (split.Length > i)
            {
                try { debug = bool.Parse(split[i]); }
                catch (System.FormatException) { return; }
            }
            i++;

            if (split.Length > i)
            {
                try { showWelcome = bool.Parse(split[i]); }
                catch (System.FormatException) { return; }
            }
            i++;

            if (split.Length > i)
            {
                try { playButtonPressed = bool.Parse(split[i]); }
                catch (System.FormatException) { return; }
            }
            i++;

            if (split.Length > i)
            {
                foreach (string s in split[i].Split(delimiter))
                {
                    Test newTest = Test.FromString(s);
                    if (newTest == null) continue;
                    cachedTests.Add(newTest.attribute.path, newTest);
                }
            }
            i++;

            if (split.Length > i)
            {
                List<string> added = new List<string>();
                foreach (string foldoutData in split[i].Split(foldoutDelimiter))
                {
                    Foldout foldout = Foldout.FromString(foldoutData);
                    if (foldout == null) continue;
                    // this part is done for us automatically when we called Foldout.FromString:
                    //if (!cachedFoldouts.ContainsKey(foldout.path)) cachedFoldouts.Add(foldout.path, foldout);
                    //else cachedFoldouts[foldout.path] = foldout;
                    added.Add(foldout.path);
                }
                foreach (string path in Foldout.all.Keys)
                {
                    if (!added.Contains(path))
                    {
                        cachedFoldouts.Remove(path);
                        Foldout.all.Remove(path);
                    }
                }
            }
            i++;
        }

        /// <summary>
        /// The cached tests from FromString() might not have the correct up-to-date information. For example, if the user changes pauseOnFail, then
        /// the test we have cached for it will be loaded in with the wrong value. We update the information here.
        /// </summary>
        private void UpdateCachedTests()
        {
            Dictionary<string, TestAttribute> attributes = new Dictionary<string, TestAttribute>();
            foreach (TestAttribute attr in methods.Keys)
            {
                attributes.Add(attr.path, attr);
            }
            
            foreach (string path in cachedTests.Keys)
            {
                if (attributes.ContainsKey(path))
                {
                    cachedTests[path].attribute.UpdateFrom(attributes[path]);
                }
            }
        }

        /// <summary>
        /// Save the object to EditorPrefs.
        /// </summary>
        private void Save()
        {
            EditorPrefs.SetString(nameof(TestManager), GetString(tests));
        }

        /// <summary>
        /// Parse the string saved by Save() in the EditorPrefs
        /// </summary>
        private void Load()
        {
            string data = EditorPrefs.GetString(nameof(TestManager), GetString(GetTests()));
            // Overwrite the created tests with their appropriate saved values
            FromString(data);
        }

        void OnEnable()
        {
            EditorApplication.playModeStateChanged += OnPlayStateChanged;

            UpdateMethods(); // Always update the methods before doing anything else
            Load();
            UpdateCachedTests();
            CreateTests();
            CreateFoldouts();

            if (playButtonPressed)
            {
                playButtonPressed = false;
                RunSelected();
            }
        }

        private void OnPlayStateChanged(PlayModeStateChange change)
        {
            //Debug.Log(change);
            if (change == PlayModeStateChange.ExitingPlayMode)
            {
                queue.Clear();
                timer = 0f;
                nframes = 0;
                Test.current = null;
            }
            else if (change == PlayModeStateChange.EnteredPlayMode && IsSceneEmpty())
            {
                Focus();
            }
        }

        void OnDisable()
        {
            EditorApplication.playModeStateChanged -= OnPlayStateChanged;
            Save();
        }

        void OnGUI()
        {
            Draw();
        }

        void Update()
        {
            if (!Application.isPlaying) return;
            Repaint();
            if (Time.frameCount > previousFrameNumber) OnUpdate();
            previousFrameNumber = Time.frameCount;
        }

        private void OnUpdate()
        {
            if (!Application.isPlaying)
            {
                // Make sure the queue is cleared in this case
                queue.Clear();
                return;
            }

            if (Test.isTesting)
            {
                if (!EditorApplication.isPaused && Test.current.result == Test.Result.Fail) return;

                timer += Time.deltaTime;
                nframes++;
            }

            if (queue.Count == 0) return;
            if (Test.isTesting) return;
            // Start the next Test in the queue
            timer = 0f;
            nframes = 0;
            Test test = queue.Dequeue();
            if (debug) Debug.Log("Running " + test.attribute.path, test.GetScript());
            test.Run();
        }



        #region Mechanisms
        private TestAttribute GetAttribute(MethodInfo method) => method.GetCustomAttribute(typeof(TestAttribute), false) as TestAttribute;

        public bool IsMethodIgnored(MethodInfo method) => method.GetCustomAttribute(typeof(IgnoreAttribute), false) != null;

        private void UpdateMethods()
        {
            // Inspired by https://stackoverflow.com/a/46360267

            IEnumerable<System.Type> types = typeof(Test).Assembly.GetTypes().Where(x => x.IsClass);
            
            List<MethodInfo> _methods = types
                .SelectMany(x => x.GetMethods(Test.bindingFlags))
                .Where(x => x.GetCustomAttributes(typeof(TestAttribute), false).FirstOrDefault() != null).ToList();

            List<System.Type> classes = types.Where(x => x.GetCustomAttributes(typeof(SuiteAttribute), false).FirstOrDefault() != null).ToList();

            // This hits all assemblies, but it's a lot slower.
            /*
            List<MethodInfo> _methods = new List<MethodInfo>(System.AppDomain.CurrentDomain.GetAssemblies() // Returns all currenlty loaded assemblies
                .SelectMany(x => x.GetTypes()) // returns all types defined in this assemblies
                .Where(x => x.IsClass) // only yields classes
                .SelectMany(x => x.GetMethods(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly | BindingFlags.Static)) // returns all methods defined in those classes
                .Where(x => x.GetCustomAttributes(typeof(TestAttribute), false).FirstOrDefault() != null)); // returns only methods that have the TestAttribute
            */

            methods.Clear();
            foreach (MethodInfo method in _methods)
            {
                if (IsMethodIgnored(method)) continue;
                methods.Add(GetAttribute(method), method);
            }
            foreach (System.Type cls in classes)
            {
                SuiteAttribute attribute = cls.GetCustomAttribute(typeof(SuiteAttribute), false) as SuiteAttribute;
                bool hasSetUp = false;
                bool hasTearDown = false;
                foreach (MethodInfo method in cls.GetMethods(Test.bindingFlags))
                {
                    if (IsMethodIgnored(method)) continue;
                    if (method.Name == "SetUp") hasSetUp = true;
                    else if (method.Name == "TearDown") hasTearDown = true;
                }

                foreach (MethodInfo method in cls.GetMethods(Test.bindingFlags))
                {
                    if (IsMethodIgnored(method)) continue;
                    if (method.Name == "SetUp" || method.Name == "TearDown") continue;
                    if (hasSetUp && hasTearDown)
                        methods.Add(new TestAttribute(attribute.path + "/" + method.Name, "SetUp", "TearDown", attribute.pauseOnFail, attribute.sourceFile), method);
                    else if (hasSetUp && !hasTearDown)
                        methods.Add(new TestAttribute(attribute.path + "/" + method.Name, "SetUp", attribute.pauseOnFail, attribute.sourceFile), method);
                    else
                        methods.Add(new TestAttribute(attribute.path + "/" + method.Name, attribute.pauseOnFail, attribute.sourceFile), method);
                }
            }
        }

        private SortedDictionary<TestAttribute, Test> GetTests()
        {
            SortedDictionary<TestAttribute, Test> result = new SortedDictionary<TestAttribute, Test>();
            Test newTest;
            foreach (TestAttribute attribute in methods.Keys)
            {
                if (cachedTests.ContainsKey(attribute.path))
                {
                    newTest = cachedTests[attribute.path];
                    newTest.method = methods[attribute];
                    newTest.selected = cachedTests[attribute.path].selected;
                }
                else newTest = new Test(attribute, methods[attribute]);
                result.Add(attribute, newTest);
            }
            //result = result.OrderBy(x => x.Key.path[0]).ToDictionary(pair => pair.Key, pair => pair.Value);
            //foreach(TestAttribute attribute in result.Keys)
            //{
            //    Debug.Log(attribute.path);
            //}
            return result;
        }

        private void CreateTests()
        {
            tests.Clear();
            tests = GetTests();
        }

        private void CreateFoldout(Foldout rootFoldout, TestAttribute attribute)
        {
            Test newTest;
            if (cachedTests.ContainsKey(attribute.path)) newTest = cachedTests[attribute.path];
            else newTest = tests[attribute];

            string path = attribute.path;
            Foldout directory = rootFoldout;

            string currentPath = directory.path;
            foreach (string basename in path.Split("/").SkipLast(1))
            {
                // We keep track of the directory path we are currently in
                currentPath += basename;

                if (!Foldout.all.ContainsKey(currentPath))
                {
                    Foldout newFoldout = new Foldout(currentPath);
                    // This is done for us in the Foldout constructor
                    //if (cachedFoldouts.ContainsKey(newFoldout.path)) cachedFoldouts[newFoldout.path] = newFoldout;
                    //else cachedFoldouts.Add(newFoldout.path, newFoldout);
                    //Foldout.all.Add(newFoldout.path, newFoldout);
                }

                directory = Foldout.all[currentPath];

                currentPath += "/";
            }

            // After traversing the directories, we end in the correct directory needed for this test
            directory.Add(newTest);
        }

        private void CreateFoldouts()
        {
            rootFoldout = new Foldout("");

            // For all the attributes, create Foldouts and Tests
            foreach (TestAttribute attribute in methods.Keys)
            {
                if (!Foldout.all.ContainsKey(attribute.path)) CreateFoldout(rootFoldout, attribute);
            }
        }

        private void Refresh()
        {
            refreshing = true;

            var oldMethods = methods;
            UpdateMethods();

            bool needUpdate = false;
            foreach (TestAttribute attribute in methods.Keys)
            {
                if (!methods.ContainsKey(attribute))
                {
                    needUpdate = true;
                    break;
                }
            }
            if (!needUpdate)
            {
                foreach (TestAttribute attribute in oldMethods.Keys)
                {
                    if (!methods.ContainsKey(attribute))
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
            Debug.Log("Refreshed UnityTest Manager");
        }

        private void GoToEmptyScene()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
            Debug.Log("Created an empty scene");
        }

        private void RunSelected()
        {
            foreach (Test test in rootFoldout.GetTests())
                if (test.selected) queue.Enqueue(test);// test.Run();
        }
        private void RunAll()
        {
            foreach (Test test in tests.Values) queue.Enqueue(test);// test.Run();
        }

        private void ResetSelected()
        {
            foreach (Test test in rootFoldout.GetTests())
                if (test.selected) test.Reset();
        }
        private void ResetAll()
        {
            foreach (Test test in tests.Values) test.Reset();
        }

        private void SelectAll()
        {
            rootFoldout.OnSelected(); // Simulate a press
        }

        private void DeselectAll()
        {
            rootFoldout.OnDeselected(); // Simulate a press
        }
        #endregion


        /// <summary>
        /// Hold information about the current state of the manager window, such as if tests are selected, if tests have results, etc.
        /// This is used for drawing certain features which depend on the state.
        /// </summary>
        private class State
        {
            public bool anySelected { get; private set; }
            public bool allSelected { get; private set; }
            public bool selectedHaveResults { get; private set; }
            public bool anyResults { get; private set; }

            public State(Foldout rootFoldout)
            {
                if (rootFoldout == null) return;
                allSelected = true;
                anyResults = false;
                foreach (Test test in rootFoldout.GetTests())
                {
                    if (test.result != Test.Result.None) anyResults = true;

                    if (test.locked)
                    {
                        if (test.selected) selectedHaveResults |= test.result != Test.Result.None;
                    }
                    else
                    {
                        if (test.selected)
                        {
                            anySelected = true;
                            selectedHaveResults |= test.result != Test.Result.None;
                        }
                        else allSelected = false;
                    }
                }
            }
        }

        private void Draw()
        {
            InitializeStyles();

            if (rootFoldout == null && !refreshing)
            {
                GUIUtility.ExitGUI();
                return;
            }

            State state = new State(rootFoldout);

            if (showWelcome) // Welcome message
            {
                EditorGUILayout.HelpBox(
                    "Welcome to UnityTest! To get started, try running each of the Example tests below. Tests can only be run in Play Mode. " +
                    "Press the X button to clear test results. Check out the code for the tests by double-clicking the script object. " +
                    "Create your tests in any class in the Assets folder, by simply writing a method with a UnityTest.Test attribute. " +
                    "See the included README for additional information. Happy testing!" + "\n\n" +
                    "If you would like to support this project, please visit _____________________ and donate. It keeps me fed. Thank you :)" + "\n\n" +
                    "To hide this message, press the speech bubble in the toolbar below."
                , MessageType.Info);
            }
            
            bool runSelected = false;
            bool refresh = false;
            bool selectAll = false;
            bool deselectAll = false;

            bool wasEnabled = GUI.enabled;
            //GUI.enabled = (queue.Count == 0 && !Test.isTesting) || !Application.isPlaying;
            // The main window
            EditorGUILayout.BeginVertical(GetStyle("box2"));
            {
                // Toolbar controls
                EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
                {
                    // Left
                    runSelected = DrawPlayButton(state);
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

                // The box that shows the Tests
                scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, false, false);
                if (rootFoldout != null)
                {
                    indentLevel = 0;
                    rootFoldout.Draw();
                }
                EditorGUILayout.EndScrollView();
            }
            EditorGUILayout.EndVertical();

            GUI.enabled = wasEnabled;

            // Doing things this way avoids annoying GUI errors complaining about groups not being ended properly.
            if (runSelected) RunSelected();
            else if (refresh) Refresh();
            else if (selectAll) SelectAll();
            else if (deselectAll) DeselectAll();

            DrawQueue();

            if (playButtonPressed)
            {
                if (EditorApplication.isPlaying) EditorApplication.ExitPlaymode();
                else EditorApplication.EnterPlaymode();
            }
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
            GUI.enabled = state.selectedHaveResults;

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

        private bool DrawPlayButton(State state)
        {
            bool wasEnabled = GUI.enabled;
            
            GUIContent run = new GUIContent(EditorGUIUtility.IconContent("PlayButton"));
            run.tooltip = "Run selected tests";
            GUI.enabled = state.anySelected;
            bool ret = GUILayout.Toggle(playButtonPressed, run, EditorStyles.toolbarButton);
            GUI.enabled = wasEnabled;
            if (ret) playButtonPressed = true;
            return ret;
        }

        private void DrawGoToEmptySceneButton()
        {
            bool wasEnabled = GUI.enabled;
            GUIContent emptyScene = new GUIContent(EditorGUIUtility.IconContent("SceneLoadIn"));
            emptyScene.tooltip = "Go to empty scene";
            GUI.enabled = !IsSceneEmpty() && !Application.isPlaying;
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





        private void DrawQueueTest(Rect rect, Test test, bool paintResultFeatures = true)
        {
            if (paintResultFeatures) Test.PaintResultFeatures(rect, test.result);
            GUI.Label(rect, test.attribute.path);
        }
        private void DrawQueueTest(Test test, bool paintResultFeatures = true)
        {
            Rect rect = EditorGUILayout.GetControlRect(false);
            if (paintResultFeatures) Test.PaintResultFeatures(rect, test.result);
            GUI.Label(rect, test.attribute.path);
        }

        

        private void DrawQueue()
        {
            bool wasEnabled = GUI.enabled;

            // The queue window
            EditorGUILayout.BeginVertical(GetStyle("box"), GUILayout.ExpandWidth(true), GUILayout.MinHeight(200f));
            {
                // "Current" space
                EditorGUILayout.BeginVertical();
                {
                    GUI.enabled = true;
                    EditorGUILayout.BeginHorizontal();
                    {
                        float previousLabelWidth = EditorGUIUtility.labelWidth;
                        EditorGUIUtility.labelWidth = 0f;
                        float width = EditorStyles.boldLabel.CalcSize(new GUIContent("Current")).x;
                        
                        EditorGUILayout.LabelField("Current", EditorStyles.boldLabel, GUILayout.Width(width));

                        GUILayout.FlexibleSpace();

                        GUILayout.Label("frame " + string.Format("{0,8}", nframes) + "    " + timer.ToString("0.0000 s"));

                        EditorGUIUtility.labelWidth = previousLabelWidth;
                    }
                    EditorGUILayout.EndHorizontal();
                    GUI.enabled = false;

                    Rect rect = EditorGUILayout.GetControlRect(false);
                    rect.height += EditorStyles.helpBox.padding.vertical;
                    GUI.Box(rect, GUIContent.none, "HelpBox");
                    rect.x += EditorStyles.helpBox.padding.left;
                    rect.width -= EditorStyles.helpBox.padding.horizontal;
                    rect.y += EditorStyles.helpBox.padding.top;
                    rect.height -= EditorStyles.helpBox.padding.vertical;
                    Test next = null;
                    if (queue.Count > 0) next = queue.Peek();
                    if (Test.current == null && next != null) DrawQueueTest(rect, next, true);
                    else if (Test.current != null) DrawQueueTest(rect, Test.current, true);
                    GUI.enabled = true;
                }
                EditorGUILayout.EndVertical();

                EditorGUILayout.Space();

                // "Queue" space
                GUI.enabled = true;
                EditorGUILayout.BeginHorizontal();
                {
                    GUI.enabled = true;
                    float width = EditorStyles.boldLabel.CalcSize(new GUIContent("Queue")).x;
                    EditorGUILayout.LabelField("Queue", EditorStyles.boldLabel, GUILayout.Width(width));
                    GUILayout.FlexibleSpace();
                    GUI.enabled = queue.Count > 0;
                    width = GUI.skin.button.CalcSize(new GUIContent("Clear")).x;
                    if (GUILayout.Button("Clear", GUILayout.Width(width))) queue.Clear();
                    GUI.enabled = true;
                }
                EditorGUILayout.EndHorizontal();

                GUI.enabled = false;
                EditorGUILayout.BeginVertical("HelpBox", GUILayout.ExpandHeight(true));
                GUI.enabled = true;
                {
                    queueScrollPosition = EditorGUILayout.BeginScrollView(queueScrollPosition, false, false);
                    GUI.enabled = false;
                    foreach (Test test in queue) DrawQueueTest(test, false);
                    GUI.enabled = true;
                    EditorGUILayout.EndScrollView();
                }
                EditorGUILayout.EndVertical();
                GUI.enabled = wasEnabled;
            }
            EditorGUILayout.EndHorizontal();
        }

        public class Settings : EditorWindow
        {
            private Test test;
            private Suite currentSuite;
            private Editor suiteEditor;
            private bool visible = false;
            
            private const float flashInterval = 0.25f;
            private const int nFlashes = 1;
            private int nFlashed = 0;
            private float flashStart = 0f;

            void Awake()
            {
                minSize = new Vector2(
                    EditorGUIUtility.labelWidth + EditorGUIUtility.fieldWidth,
                    minSize.y
                );
            }

            public void SetTest(Test test)
            {
                if (test == this.test) return;

                // Search the Data for this test's class
                currentSuite = Suite.Get(test.method.DeclaringType);
                suiteEditor = Editor.CreateEditor(currentSuite);

                this.test = test;
                titleContent = new GUIContent(test.method.DeclaringType.Name + " (Settings)");
            }

            public void SetVisible(bool value)
            {
                if (visible == value)
                {
                    if (visible) Flash();
                    return;
                }
                if (value) ShowUtility();
                else Close();
                visible = value;
            }

            public void Flash()
            {
                flashStart = Time.realtimeSinceStartup;
                nFlashed = -1;
                EditorApplication.update += _Flash;
            }

            private void _Flash()
            {
                int i = (int)((Time.realtimeSinceStartup - flashStart) / (0.5f * flashInterval));
                if (i < nFlashes * 2)
                {
                    if (i > nFlashed)
                    {
                        nFlashed = i;
                    }
                    else return;

                    // We know that the TestManager window is visible, because this method is called when the user clicks the
                    // settings cog in the window.
                    if (i % 2 == 0) Focus();
                    else EditorWindow.GetWindow<TestManager>().Focus();
                }
                else
                {
                    EditorApplication.update -= _Flash;
                    Focus();
                }
            }

            //public void Toggle() => SetVisible(!visible);

            void OnGUI()
            {
                if (test == null)
                {
                    SetVisible(false);
                    return;
                }
                
                if (Event.current.type == EventType.MouseDown && position.Contains(GUIUtility.GUIToScreenPoint(Event.current.mousePosition)))
                {
                    GUI.FocusControl(null);
                    Repaint();
                }
                
                suiteEditor.OnInspectorGUI();
                
            }
        }

        public class Foldout
        {
            public string path;
            public string name;
            public bool expanded;

            private bool selected;

            private bool locked;

            private const string delimiter = "|)+( FoldoutDelimiter )+(|";
            
            private List<Test> tests = new List<Test>();

            public static Dictionary<string, Foldout> all;

            private bool isSuite = false;

            private static GUIStyle _style;

            public Foldout(string path)
            {
                this.path = path;
                name = path.Split("/").Last();

                if (Foldout.all == null) Foldout.all = new Dictionary<string, Foldout>();
                if (Foldout.all.ContainsKey(this.path))
                {
                    cachedFoldouts[this.path] = this;
                    Foldout.all[this.path] = this;
                }
                else
                {
                    cachedFoldouts.Add(this.path, this);
                    Foldout.all.Add(this.path, this);
                }
            }

            public GUIStyle GetStyle()
            {
                if (_style == null)
                {
                    _style = new GUIStyle(EditorStyles.foldout);
                }
                return _style;
            }

            public string GetString()
            {
                string data = path;

                data += delimiter;
                data += expanded;

                data += delimiter;
                data += selected;

                data += delimiter;
                data += locked;

                return data;
            }

            public static Foldout GetFromPath(string path)
            {
                if (string.IsNullOrEmpty(path)) return rootFoldout;
                return Foldout.all[path];
            }

            public static Foldout FromString(string data)
            {
                if (string.IsNullOrEmpty(data)) return null;
                string[] contents = data.Split(delimiter);
                Foldout result = new Foldout(contents[0]);
                result.expanded = bool.Parse(contents[1]);
                result.selected = bool.Parse(contents[2]);
                result.locked = bool.Parse(contents[3]);
                return result;
            }

            public void Add(Test test)
            {
                if (tests.Contains(test)) throw new System.Exception("Cannot add test because it has already been added: " + test);
                if (!isSuite && tests.Count == 0 && test.IsInSuite()) isSuite = true;
                else if (isSuite && !test.IsInSuite()) throw new System.Exception("Cannot have a mix of tests that are in a TestSuite and not in a suite for a single Foldout " + test); // Should never happen
                tests.Add(test);
                tests = tests.OrderBy(o => o.name).ToList();
            }

            /// <summary>
            /// Returns true if only some child Tests are selected, and false if all are not selected or all are selected.
            /// </summary>
            /// <returns></returns>
            public bool GetIsMixed()
            {
                int nSelected = 0;
                HashSet<Test> tests = GetTests();
                foreach (Test test in tests)
                {
                    if (test.selected) nSelected++;
                }
                return nSelected != 0 && nSelected != tests.Count;
            }

            /// <summary>
            /// When the user is holding Alt and clicks on the foldout arrow, expand also all child foldouts and Tests
            /// </summary>
            /// <param name="value"></param>
            public void ExpandAll(bool value)
            {
                expanded = value;
                foreach (Test test in GetTests()) test.expanded = value;
                foreach (Foldout child in GetChildren())
                {
                    child.ExpandAll(value);
                }
            }

            /// <summary>
            /// Obtain all the Test objects that are children to this Foldout at any level
            /// </summary>
            /// <returns></returns>
            public HashSet<Test> GetTests()
            {
                HashSet<Test> result = new HashSet<Test>(tests);
                foreach (Foldout child in GetAllChildren())
                    foreach (Test test in child.tests)
                        result.Add(test);
                return result;
            }

            /// <summary>
            /// Returns true if other's directory is this Foldout.
            /// </summary>
            public bool IsChildOf(Foldout other)
            {
                return string.Join("/", path.Split("/").SkipLast(1)) == other.path;
            }

            /// <summary>
            /// Returns true if this Foldout is a parent of other.
            /// </summary>
            public bool IsParentOf(Foldout other) => other.IsChildOf(this);
            
            /// <summary>
            /// Find and return all the Foldouts that are direct children of this Foldout, one level deep.
            /// </summary>
            public HashSet<Foldout> GetChildren()
            {
                HashSet<Foldout> result = new HashSet<Foldout>();
                foreach (Foldout child in Foldout.all.Values)
                {
                    if (path == child.path) continue;
                    if (!child.IsChildOf(this)) continue;
                    result.Add(child);
                }
                return result.OrderBy(o => o.path).ToHashSet();
            }

            /// <summary>
            /// Find and return all the Foldouts that are a child of this Foldout at any level.
            /// </summary>
            public HashSet<Foldout> GetAllChildren()
            {
                HashSet<Foldout> result = new HashSet<Foldout>();
                if (string.IsNullOrEmpty(path)) // For the root foldout, all other Foldouts are children
                {
                    result = new HashSet<Foldout>(Foldout.all.Values);
                    result.Remove(this);
                    return result;
                }
                
                foreach (Foldout foldout in Foldout.all.Values)
                {
                    if (foldout.path == path) continue; // skip self
                    if (string.IsNullOrEmpty(foldout.path)) continue; // skip the root foldout (it's never a child)
                    if (!foldout.path.StartsWith(path)) continue;
                    result.Add(foldout);
                }
                return result;
            }

            /// <summary>
            /// Find and return the parent of this Foldout.
            /// </summary>
            public Foldout GetParent()
            {
                foreach (Foldout foldout in Foldout.all.Values)
                {
                    if (path == foldout.path) continue;
                    if (foldout.IsParentOf(this)) return foldout;
                }
                return null;
            }

            

            

            private Test.Result GetTotalResult()
            {
                bool anyPassed = false;
                foreach (Test test in GetTests())
                {
                    // If any children are Fail, we are Fail
                    if (test.result == Test.Result.Fail) return Test.Result.Fail;
                    anyPassed |= test.result == Test.Result.Pass;
                }

                if (anyPassed) return Test.Result.Pass;
                return Test.Result.None;
            }

            private bool AllSelected()
            {
                foreach (Test test in GetTests())
                    if (!test.selected) return false;
                return true;
            }

            private bool AllLocked()
            {
                foreach (Test test in GetTests())
                    if (!test.locked) return false;
                return true;
            }
            
            /// <summary>
            /// Invoked when the user pressed the toggle button.
            /// </summary>
            public void OnSelected()
            {
                foreach (Test test in GetTests())
                    if (!test.locked) test.selected = true;
                foreach (Foldout child in GetChildren())
                    if (!child.locked) child.selected = true;
                selected = true;
            }

            /// <summary>
            /// Invoked when the user pressed the toggle button.
            /// </summary>
            public void OnDeselected()
            {
                foreach (Test test in GetTests())
                    if (!test.locked) test.selected = false;
                foreach (Foldout child in GetChildren())
                    if (!child.locked) child.selected = false;
                selected = false;
            }

            /// <summary>
            /// Invoked when the user pressed the lock button.
            /// </summary>
            public void OnLocked()
            {
                foreach (Test test in GetTests()) test.locked = true;
                locked = true;
            }

            /// <summary>
            /// Invoked when the user pressed the lock button.
            /// </summary>
            public void OnUnlocked()
            {
                foreach (Test test in GetTests()) test.locked = false;
                locked = false;
            }

            

            private void DrawChildren()
            {
                float indent = indentLevel * indentWidth;
                foreach (Test test in tests)
                {
                    Rect rect = EditorGUILayout.GetControlRect(false);
                    Test.PaintResultFeatures(rect, test.result);
                    rect.x += indent;
                    rect.width -= indent;
                    test.Draw(rect, true, !test.IsInSuite(), true);
                }
                HashSet<Foldout> children = GetChildren();
                if (children.Count > 0 && tests.Count > 0) EditorGUILayout.Space(0.5f * EditorGUIUtility.singleLineHeight);
                Foldout examples = null;
                foreach (Foldout child in children)
                {
                    if (child.path.Split("/").First() == "Examples")
                    {
                        examples = child;
                        continue;
                    }
                    child.Draw();
                }

                if (examples != null)
                {
                    EditorGUILayout.Space(0.5f * EditorGUIUtility.singleLineHeight);
                    examples.Draw();
                }
            }

            public void Draw()
            {
                // If this is the root foldout
                if (path == "") 
                {
                    DrawChildren();
                    return;
                }

                GUILayout.BeginHorizontal();
                {
                    Rect controlRect = EditorGUILayout.GetControlRect(false);
                    if (!expanded) Test.PaintResultFeatures(controlRect, GetTotalResult());

                    Rect indentedRect;
                    if (indentWidth == 0f)
                    {
                        EditorGUI.indentLevel++;
                        indentedRect = EditorGUI.IndentedRect(controlRect);
                        EditorGUI.indentLevel--;
                        indentWidth = controlRect.width - indentedRect.width;
                    }
                    else
                    {
                        indentedRect = new Rect(controlRect);
                        indentedRect.x += indentWidth * indentLevel;
                        indentedRect.width -= indentWidth * indentLevel;
                    }

                    bool wasExpanded = expanded;
                    expanded = EditorGUI.Foldout(indentedRect, expanded, string.Empty, GetStyle());

                    if (Event.current.alt && expanded != wasExpanded) ExpandAll(expanded);
                    
                    float toggleWidth = EditorStyles.toggle.CalcSize(GUIContent.none).x;

                    // scan to the right by the toggle width to give space to the Foldout control
                    indentedRect.x += toggleWidth;
                    indentedRect.width -= toggleWidth;

                    bool drawSuite = isSuite && tests.Count > 0;

                    // If we're going to draw a suite, then we need to draw the settings cog and the object reference on the right side,
                    // so room is made for that here. We finish drawing those controls later, after drawing the toggle.
                    if (drawSuite) indentedRect.width -= Test.scriptWidth + toggleWidth;

                    // We need to separate out the user's actual actions from the button's state
                    // The toggle is only "selected" when it is not mixed. If it is mixed, then selected = false.

                    selected = AllSelected();
                    
                    bool isMixed = GetIsMixed();

                    List<bool> results = Test.DrawToggle(indentedRect, name, selected, locked, true, isMixed);
                    
                    // The logic here is confusing. It is the simplest I could make it with the tools Unity gave me
                    if (results[0] != selected)
                    {
                        // mixed is the same as the toggle not being selected
                        if (selected && isMixed) selected = false; // if the toggle was selected, but now it is mixed, it's because the user deselected a child Test
                        else
                        {
                            // We only get into this logic if the user has clicked on the foldout toggle
                            if (isMixed) OnSelected();
                            else if (!selected) OnSelected();
                            else OnDeselected();
                        }
                    }

                    if (results[1] != locked)
                    {
                        if (results[1] && !locked) OnLocked();
                        else if (!results[1] && locked) OnUnlocked();
                    }
                    else locked = AllLocked();
                    

                    if (drawSuite) // Finish drawing the suite
                    {
                        indentedRect.x += indentedRect.width;
                        indentedRect.width = toggleWidth;

                        GUIStyle iconButtonStyle = EditorGUIUtility.GetBuiltinSkin(EditorSkin.Inspector).FindStyle("IconButton");
                        GUIContent moveGroupButton = EditorGUIUtility.IconContent("_Popup");

                        if (GUI.Button(indentedRect, moveGroupButton, iconButtonStyle))
                        {
                            TestManager.settings.SetTest(tests[0]);
                            TestManager.settings.SetVisible(true);
                        }

                        indentedRect.x += indentedRect.width;
                        indentedRect.width = Test.scriptWidth;
                        //rect.x += rect.width;
                        //rect.width = Test.scriptWidth;
                        bool wasEnabled = GUI.enabled;
                        GUI.enabled = false;
                        EditorGUI.ObjectField(indentedRect, GUIContent.none, tests[0].GetScript(), tests[0].method.DeclaringType, false);
                        GUI.enabled = wasEnabled;
                    }
                }
                GUILayout.EndHorizontal();
                
                if (expanded)
                {
                    indentLevel++;
                    DrawChildren();
                    indentLevel--;
                }
            }
        }
    }
}