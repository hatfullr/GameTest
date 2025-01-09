using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.Reflection;
using System.IO;
using System.Text.RegularExpressions;
using System.Linq;


namespace GameTest
{
    /// <summary>
    /// Holds the assemblies, Test methods, and queues. Executes the tests when instructed.
    /// </summary>
    [System.Serializable]
    public class TestManager : ScriptableObject
    {
        // If you change default values here, you must also change them in PreferencesWindow and some other places.
        public bool showWelcome = true;
        public Logger.DebugMode debug = Logger.DebugMode.Log | Logger.DebugMode.LogWarning | Logger.DebugMode.LogError;
        public string search = null;
        public bool paused = false;
        public bool running = false;
        /// <summary>
        /// Set to true when user presses the Start toolbar button (calls RequestStart). Set to false when Stop() is called.
        /// </summary>
        public bool startRequested = false;
        public TestManagerUI.TestSortOrder testSortOrder = TestManagerUI.TestSortOrder.Name;

        [SerializeField] private string dataPath = null;
        [SerializeField] private bool uiRefreshing = false;

        public Vector2 scrollPosition;

        public List<Foldout> foldouts = new List<Foldout>();

        public List<Test> queue = new List<Test>();
        public List<Test> finished = new List<Test>();

        /// <summary>
        /// When the tests begin, the queue is saved into this variable so that the orders of the tests can be maintained.
        /// When testing stops, the queue is repopulated with this list, and then this list is cleared. 
        /// </summary>
        [SerializeField] private List<Test> originalQueue = new List<Test>();

        [SerializeField] private GUIQueue _guiQueue;
        public GUIQueue guiQueue
        {
            get
            {
                if (_guiQueue == null) _guiQueue = new GUIQueue();
                return _guiQueue;
            }
        }

        public bool loadingWheelVisible = false;
        public string loadingWheelText = null;

        public List<Test> searchMatches = new List<Test>();

        private uint previousFrameNumber = 0;

        public bool stopping = false;

        public string testToReveal;
        public PingData pingData { get; private set; } = new PingData();

        /// <summary>
        /// Invoked when all Tests in the queue have been run and the editor has exited Play mode.
        /// </summary>
        public System.Action onStop;

        private List<System.Tuple<TestAttribute, MethodInfo>> attributesAndMethods = new List<System.Tuple<TestAttribute, MethodInfo>>();

        private System.Threading.Tasks.Task task;

        /// <summary>
        /// Don't set this. Should only be set in the AssetModificationProcessor.
        /// </summary>
        public static string filePath;

        public class PingData
        {
            private Test _test;
            public Test test
            {
                get => _test;
                set
                {
                    timeSincePingStart = -1f;
                    _test = value;
                }
            }
            public float waitTime = Style.TestManagerUI.pingWaitTime;
            public float fadeInTime = Style.TestManagerUI.pingFadeInTime;
            public float fadeOutTime = Style.TestManagerUI.pingFadeOutTime;
            public Color color = Style.TestManagerUI.pingColor;
            public Rect rect;

            private float timeSincePingStart = -1f;

            /// <summary>
            /// Needs to be called in OnGUI
            /// </summary>
            public void HandlePing(EditorWindow window)
            {
                if (test == null) return;
                if (timeSincePingStart < 0) timeSincePingStart = Time.realtimeSinceStartup;
                float totalTime = fadeInTime + waitTime + fadeOutTime;
                float t = Time.realtimeSinceStartup - timeSincePingStart;
                if (!(t >= 0.0f && t < totalTime))
                {
                    timeSincePingStart = -1f;
                    test = null;
                    return;
                }
                float alpha;
                if (t < fadeInTime) alpha = Mathf.Lerp(0f, color.a, t / fadeInTime);
                else if (t < waitTime) alpha = color.a;
                else alpha = Mathf.Lerp(color.a, 0f, 1f - (totalTime - t) / fadeOutTime);
                EditorGUI.DrawRect(rect, new Color(color.r, color.g, color.b, alpha));
                window.Repaint();
            }
        }


        #region Persistence Methods
        /// <summary>
        /// Returns the currently set data path. The user can change the data path via the UI.
        /// </summary>
        public string GetDataPath()
        {
            if (string.IsNullOrEmpty(dataPath)) dataPath = Utilities.defaultDataPath;
            return Utilities.EnsureDirectoryExists(dataPath);
        }

        public void SetDataPath(string path)
        {
            path = Utilities.GetUnityPath(path);
            if (Path.GetFullPath(path) == Path.GetFullPath(dataPath)) return;

            // Move the contents of the current dataPath into the new dataPath
            if (!string.IsNullOrEmpty(dataPath))
            {
                Utilities.EnsureDirectoryExists(Path.GetDirectoryName(path));
                FileUtil.MoveFileOrDirectory(dataPath, path);
                FileUtil.MoveFileOrDirectory(Path.ChangeExtension(dataPath, ".meta"), Path.ChangeExtension(path, ".meta"));
                AssetDatabase.Refresh();
            }

            dataPath = path;
        }

        /// <summary>
        /// Checks if a TestManagerUI window is open. If so, the manager property of the window is returned. Otherwise, returns null.
        /// </summary>
        public static TestManager Get()
        {
            if (EditorWindow.HasOpenInstances<TestManagerUI>()) return EditorWindow.GetWindow<TestManagerUI>().manager;
            return null;
        }

        /// <summary>
        /// Locate the TestManager ScriptableObject asset in this project, load it, then return it. If no asset was found, a new one is created at Utilities.dataPath. If more than one is found,
        /// a Utilities.LogError is printed and null is returned.
        /// </summary>
        public static TestManager Load()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:" + nameof(TestManager)))
            {
                filePath = AssetDatabase.GUIDToAssetPath(guid);
                return AssetDatabase.LoadAssetAtPath<TestManager>(filePath);
            }

            // Didn't find an existing TestManager, so create one
            TestManager result = ScriptableObject.CreateInstance(typeof(TestManager)) as TestManager;
            filePath = Path.Join(Utilities.defaultDataPath, nameof(GameTest) + ".asset");
            AssetDatabase.CreateAsset(result, filePath);
            return result;
        }

        /// <summary>
        /// Save the TestManager asset as well as all assets linked to it.
        /// </summary>
        public void Save()
        {
            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssetIfDirty(this);
        }
        #endregion


        #region Unity Events 
        // Some of these (EditorWindow events) need to be called by the TestManagerUI

        void Awake()
        {
            Logger.debug = debug;
        }

        [HideInCallstack]
        public void Update()
        {
            if (Time.frameCount > previousFrameNumber && Test.current != null)
            {
                guiQueue.IncrementTimer(Time.deltaTime);
            }
            previousFrameNumber = (uint)Time.frameCount;
        }

        /// <summary>
        /// "factory reset" the TestManager, but don't clear out the asset data. If you want to clear the asset data, use TestManager.ClearData().
        /// </summary>
        public void Reset()
        {
            foldouts.Clear();
            scrollPosition = default;
            showWelcome = true;
            loadingWheelVisible = false;
            loadingWheelText = null;
            search = default;
            searchMatches.Clear();
            originalQueue.Clear();
            running = default;
            startRequested = default;
            stopping = default;
            uiRefreshing = default;

            pingData = new PingData();
            testToReveal = default;

            debug = Logger.DebugMode.Log | Logger.DebugMode.LogWarning | Logger.DebugMode.LogError;
            Logger.debug = debug;
            testSortOrder = TestManagerUI.TestSortOrder.Name;
            previousFrameNumber = 0;

            guiQueue.Reset();
        }

        public void OnPlayStateChanged(PlayModeStateChange change)
        {
            if (running && change == PlayModeStateChange.ExitingPlayMode) Stop();
            if (change == PlayModeStateChange.EnteredPlayMode && startRequested && !uiRefreshing) Start();
        }

        public void OnBeforeTestManagerUIRefresh()
        {
            uiRefreshing = true;
        }
        public void OnAfterTestManagerUIRefresh()
        {
            uiRefreshing = false;
            // isPlaying is true on EnteredPlayMode and ExitingEditMode
            if (EditorApplication.isPlaying && startRequested) Start();
        }
        #endregion

        #region Test Execution
        /// <summary>
        /// Submit a request to start the tests. The manager will then enter Play mode if the editor is not in Play mode already.
        /// This can cause the domain to be reloaded, so the manager waits until the domain has been reloaded. Then the tests 
        /// actually start.
        /// </summary>
        public void RequestStart()
        {
            if (running) return;
            if (startRequested) return;

            startRequested = true;

            finished.Clear();
            originalQueue.Clear();
            originalQueue.AddRange(queue); // Save a copy of the original queue to restore queue order in Stop()

            if (!EditorApplication.isPlaying)
            {
                EditorApplication.EnterPlaymode();
                // Unity does the following:
                //   1. ExitingPlayMode
                //   2. OnBeforeAssemblyReload
                //   3. OnAfterAssemblyReload
                //   4. EnteredPlayMode
                // Setps 2 and 3 don't happen if EditorSettings.enterPlayModeOptions.HasFlag(EnterPlayModeOptions.DisableDomainReload)
            }
            else Start();
        }

        /// <summary>
        /// Begin working through the queue, running each Test.
        /// </summary>
        private void Start()
        {
            Logger.Log("Starting");
            running = true;
            RunNext();
        }

        /// <summary>
        /// Stop running tests and clear the queues
        /// </summary>
        public void Stop()
        {
            if (stopping) return;

            stopping = true;
            SkipRemainingTests();

            // Restore test ordering
            queue.Clear();
            queue.AddRange(originalQueue);
            originalQueue.Clear();

            running = false;
            startRequested = false;
            paused = false;

            if (EditorApplication.isPlaying) EditorApplication.ExitPlaymode();
            if (onStop != null) onStop.Invoke();
            Logger.Log("Finished", null, null);
            stopping = false;
        }

        [HideInCallstack]
        public void RunNext()
        {
            guiQueue.ResetTimer();

            // This is a redundancy fallback
            if (queue.Count == 0)
            {
                Stop();
                return;
            }

            Test test = queue[0];
            queue.RemoveAt(0);

            test.onFinished -= OnRunFinished;
            test.onFinished += OnRunFinished;

            test.Run();
        }

        private void OnRunFinished(Test test)
        {
            test.onFinished -= OnRunFinished;
            AddToFinishedQueue(test);
            guiQueue.ResetTimer();

            // Ensure that the finished test's results are shown in the UI
            string path = test.attribute.GetPath();
            bool found = false;
            foreach (Foldout foldout in foldouts)
            {
                foreach (Test t in foldout.tests)
                {
                    if (path == t.attribute.GetPath())
                    {
                        t.result = test.result;
                        found = true;
                        break;
                    }
                }
                if (!found) continue;
                break;
            }

            foreach (Foldout foldout in foldouts) foldout.UpdateState(this);

            if (queue.Count == 0) Stop(); // no more tests left to run
            else if (running && !paused) RunNext();
        }

        /// <summary>
        /// Stop running the current test and start running the next in the queue.
        /// </summary>
        [HideInCallstack]
        public void Skip()
        {
            if (Test.current != null)
            {
                Test.current.result = Test.Result.Skipped;
                Test.current.OnRunComplete();
            }
            else
            {
                if (queue.Count == 0) Stop(); // this shouldn't happen, but is a fallback in case it does somehow
                else RunNext();
            }
        }

        private void SkipRemainingTests()
        {
            running = false;

            // If there is a Test running currently, skip it
            if (Test.current != null)
            {
                Test.current.result = Test.Result.Skipped;
                Test.current.OnRunComplete();
            }

            foreach (Test test in queue)
            {
                test.result = Test.Result.Skipped;
                test.PrintResult();
                AddToFinishedQueue(test);
            }
        }
        #endregion

        #region Assembly and Test Management
        public IEnumerable<Test> GetTests()
        {
            foreach (Foldout foldout in foldouts)
            {
                foreach (Test test in foldout.tests)
                {
                    yield return test;
                }
            }
        }

        public void UpdateSearchMatches(TestManagerUI ui, string newSearch)
        {
            searchMatches.Clear();
            search = newSearch;
            if (string.IsNullOrEmpty(search)) return;
            Regex re = new Regex(search, RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.IgnorePatternWhitespace);

            string path;
            MatchCollection matches;
            foreach (Test test in GetTests().OrderBy(x => x.attribute.GetPath()))
            {
                path = test.attribute.GetPath();
                matches = re.Matches(path);
                if (matches.Count == 0) continue;

                searchMatches.Add(test);
            }
        }
        /// <summary>
        /// Remove the Test from the queue if it is already there. Then insert the Test into the queue at the top.
        /// </summary>
        public void AddToQueue(Test test)
        {
            RemoveFromQueue(test);
            queue.Add(test);
        }
        /// <summary>
        /// Remove all Tests in the queue whose paths match the path of the given Test.
        /// </summary>
        public void RemoveFromQueue(Test test)
        {
            string path = test.attribute.GetPath();
            for (int i = queue.Count - 1; i >= 0; i--)
            {
                if (queue[i].attribute.GetPath() != path) continue;
                queue.RemoveAt(i);
            }
        }
        private void AddToFinishedQueue(Test test)
        {
            finished.Add(test); // This makes a reverse order
        }

        /// <summary>
        /// Remove all Tests in the finished queue whose paths match the path of the given Test.
        /// </summary>
        private void RemoveFromFinishedQueue(Test test)
        {
            string path = test.attribute.GetPath();
            for (int i = finished.Count - 1; i >= 0; i--)
            {
                if (finished[i].attribute.GetPath() != path) continue;
                finished.RemoveAt(i);
            }
        }

        public bool IsMethodIgnored(MethodInfo method) => method.GetCustomAttribute<IgnoreAttribute>(false) != null;

        /// <summary>
        /// Collect all the assemblies that Unity has compiled.
        /// </summary>
        public HashSet<System.Reflection.Assembly> GetAssemblies()
        {
            List<string> added = new List<string>();
            HashSet<System.Reflection.Assembly> assemblies = new HashSet<System.Reflection.Assembly>();
            string path;
            foreach (UnityEditor.Compilation.AssembliesType type in (UnityEditor.Compilation.AssembliesType[])System.Enum.GetValues(typeof(UnityEditor.Compilation.AssembliesType))) // Hit all assembly types
            {
                foreach (UnityEditor.Compilation.Assembly assembly in UnityEditor.Compilation.CompilationPipeline.GetAssemblies(type))
                {
                    path = Path.Join(Utilities.projectPath, assembly.outputPath);
                    if (added.Contains(path)) continue;
                    assemblies.Add(System.Reflection.Assembly.LoadFile(path));
                    added.Add(path);
                }
            }
            return assemblies;
        }

        
        /// <summary>
        /// Create a new Test with the given attribute and method. If a Foldout does not exist yet for the new Test,
        /// Foldouts are created for the entire path of the new Test.
        /// </summary>
        private void CreateTest(TestAttribute attribute, MethodInfo method)
        {
            // Locate the Foldout that this new Test would be a member of
            Foldout foundFoldout = null;
            Test foundTest = null;
            string parent = Path.GetDirectoryName(attribute.GetPath());
            foreach (Foldout foldout in foldouts)
            {
                if (foldout.path == parent)
                {
                    foundFoldout = foldout;
                    foreach (Test test in foundFoldout.tests)
                    {
                        if (test.attribute == attribute)
                        {
                            foundTest = test;
                            break;
                        }
                    }
                    break;
                }
            }

            if (foundTest != null) // Found existing matching Test, so update its method
            {
                foundTest.method = method;
            }
            else // Did not find any existing Test, so make a new one
            {
                if (foundFoldout == null) // No Foldout found for the Test, so we also need to make a new one
                {
                    // We might need to make several Foldouts so that there is a Foldout for each step on the path
                    Foldout f;
                    foreach (string path in Utilities.IterateDirectories(attribute.GetPath()))
                    {
                        f = null;
                        foreach (Foldout foldout in foldouts)
                        {
                            if (foldout.path == path)
                            {
                                f = foldout;
                                if (foundFoldout == null) foundFoldout = foldout;
                                break;
                            }
                        }
                        if (f == null)
                        {
                            foldouts.Add(new Foldout(path));
                            if (foundFoldout == null) foundFoldout = foldouts[foldouts.Count - 1];
                        }
                    }
                }

                foundFoldout.tests.Add(new Test(attribute, method));
            }
        }

        private void UpdateTestAttributesAndMethods(HashSet<System.Reflection.Assembly> assemblies)
        {
            List<System.Tuple<TestAttribute, MethodInfo>> result = new List<System.Tuple<TestAttribute, MethodInfo>>();
            object[] classAttributes, methodAttributes;
            foreach (System.Reflection.Assembly assembly in assemblies)
            {
                foreach (System.Type type in assembly.GetTypes())
                {
                    if (!type.IsClass) continue; // only work with classes

#pragma warning disable CS0618 // "obsolete" markers
                    classAttributes = type.GetCustomAttributes(typeof(SuiteAttribute), false);
#pragma warning restore CS0618
                    if (classAttributes.Length > 0)
                    {
                        // Locate the SuiteAttribute if there is one. It has to be done this way for some reason I can't understand.
                        foreach (object attr in classAttributes)
                        {
#pragma warning disable CS0618 // "obsolete" markers
                            if (attr.GetType() != typeof(SuiteAttribute)) continue;
                            SuiteAttribute suiteAttribute = attr as SuiteAttribute;
#pragma warning restore CS0618

                            // Get the Suite's SetUp and TearDown methods. These are null if it doesn't have them
                            MethodInfo[] methods = type.GetMethods(Utilities.bindingFlags);

                            MethodInfo setUp = null;
                            MethodInfo tearDown = null;
                            foreach (MethodInfo method in methods)
                            {
                                if (IsMethodIgnored(method)) continue;
                                if (method.Name == "SetUp")
                                {
                                    setUp = method;
                                    if (tearDown != null) break;
                                }
                                else if (method.Name == "TearDown")
                                {
                                    tearDown = method;
                                    if (setUp != null) break;
                                }
                            }

                            foreach (MethodInfo method in methods)
                            {
                                if (IsMethodIgnored(method)) continue;
                                if (setUp != null)
                                    if (method.Name == setUp.Name) continue;
                                if (tearDown != null)
                                    if (method.Name == tearDown.Name) continue;

                                TestAttribute testAttribute;

                                if (setUp != null && tearDown != null)
                                    testAttribute = new TestAttribute(setUp.Name, tearDown.Name, suiteAttribute.pauseOnFail, method.Name, suiteAttribute.sourceFile);
                                else if (setUp != null && tearDown == null)
                                    testAttribute = new TestAttribute(setUp.Name, suiteAttribute.pauseOnFail, method.Name, suiteAttribute.sourceFile);
                                else
                                    testAttribute = new TestAttribute(suiteAttribute.pauseOnFail, method.Name, suiteAttribute.sourceFile);

                                result.Add(new System.Tuple<TestAttribute, MethodInfo>(testAttribute, method));
                            }
                            break;
                        }
                        continue;
                    }


                    // If the class was not a Suite, check its methods for TestAttributes.
                    foreach (MethodInfo method in type.GetMethods(Utilities.bindingFlags))
                    {
                        if (IsMethodIgnored(method)) continue;

                        // It has to be done this way for some reason I can't understand.
                        methodAttributes = method.GetCustomAttributes(typeof(TestAttribute), false);
                        if (methodAttributes.Length == 0) continue;
                        foreach (object attribute in methodAttributes)
                        {
                            if (attribute.GetType() != typeof(TestAttribute)) continue;
                            result.Add(new System.Tuple<TestAttribute, MethodInfo>(attribute as TestAttribute, method));
                            break;
                        }
                    }
                }
            }
            attributesAndMethods = result;
        }

        private async void UpdateTestsAsync(HashSet<System.Reflection.Assembly> assemblies, System.Action onFinished = null)
        {
            if (task != null) return; // the task is already running, so do nothing and wait for it to finish

            task = System.Threading.Tasks.Task.Run(() => UpdateTestAttributesAndMethods(assemblies));
            await task;
            task = null;

            List<TestAttribute> foundAttributes = new List<TestAttribute>();
            foreach (System.Tuple<TestAttribute, MethodInfo> obj in attributesAndMethods)
            {
                TestAttribute attribute = obj.Item1;
                MethodInfo method = obj.Item2;

                foundAttributes.Add(attribute);
                CreateTest(attribute, method);
            }

            // Remove any old Tests that have now been removed from the user's code
            foreach (Foldout foldout in foldouts.ToArray())
            {
                foreach (Test test in foldout.tests.ToArray())
                {
                    if (foundAttributes.Contains(test.attribute)) continue;
                    RemoveTest(test, foldout: foldout);
                }
            }

            // Check the GUIQueue for any Tests whose methods need to be updated now
            foreach (List<Test> q in new List<List<Test>> { queue, finished })
            {
                foreach (Test test in q)
                {
                    foreach (System.Tuple<TestAttribute, MethodInfo> obj in attributesAndMethods)
                    {
                        if (obj.Item1 != test.attribute) continue;
                        test.method = obj.Item2;
                        break;
                    }
                }
            }

            // DEBUGGING: Pretend that the task took a long time
            //await Task.Delay(5000);

            if (onFinished != null) onFinished();
        }

        /// <summary>
        /// Removes the given Test from the given Foldout (if given), and from both the queue and finished queue. If the Foldout now has no Tests, the Foldout is removed, 
        /// along with any parent Foldouts that now have no Tests in any of their children.
        /// </summary>
        public void RemoveTest(Test test, Foldout foldout = null)
        {
            if (foldout != null)
            {
                foldout.tests.Remove(test);
                if (foldout.tests.Count == 0) RemoveFoldout(foldout);
            }
            RemoveFromQueue(test);
            RemoveFromFinishedQueue(test);
            // Clean up the default prefab if there is one
            test.DeleteDefaultPrefab();

            void CloseRelevantSettingsWindows()
            {
                if (EditorWindow.HasOpenInstances<SettingsWindow>())
                {
                    SettingsWindow settings = EditorWindow.GetWindow<SettingsWindow>();
                    if (settings.GetTest().attribute.GetPath() == test.attribute.GetPath())
                    {
                        settings.Close();
                        CloseRelevantSettingsWindows(); // Go again to try and close all open settings windows that are open on this Test
                    }
                }
            }
            CloseRelevantSettingsWindows();
        }

        /// <summary>
        /// Remove the given Foldout. Then remove the parents of this Foldout that no longer lead to any tests.
        /// </summary>
        public void RemoveFoldout(Foldout foldout)
        {
            if (foldouts.Contains(foldout)) foldouts.Remove(foldout);

            foreach (Foldout f in foldouts.ToArray())
            {
                if (!f.IsParentOf(foldout)) continue;
                bool hasTests = false;
                foreach (Test test in f.GetTests(this, includeSubdirectories: true))
                {
                    hasTests = true;
                    break;
                }
                if (!hasTests)
                {
                    RemoveFoldout(f);
                }
            }
        }

        /// <summary>
        /// Collect the assemblies that Unity has compiled and search them for all the class methods with TestAttribute and classes with SuiteAttribute.
        /// This is the slowest function in the package, as the reflection methods for checking for custom attributes is a bit slow.
        /// </summary>
        public void UpdateTests(System.Action onFinished = null)
        {
            UpdateTestsAsync(GetAssemblies(), onFinished);
            //Utilities.Log("Tests updated");
        }
        #endregion
    }
}