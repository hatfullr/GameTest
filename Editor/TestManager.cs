using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.Compilation;
using System.Reflection;
using System.IO;
using System.Text.RegularExpressions;
using System.Linq;


namespace UnityTest
{
    /// <summary>
    /// Holds the assemblies, Test methods, and queues. Executes the tests when instructed.
    /// </summary>
    [System.Serializable]
    public class TestManager : ScriptableObject
    {
        public const string fileName = nameof(TestManager);

        /// <summary>
        /// The Test objects associated with the methods that have a TestAttribute attached to them.
        /// </summary>
        public List<Test> tests = new List<Test>();

        public List<Test> queue = new List<Test>();
        public List<Test> finishedTests = new List<Test>();
        public List<Test.Result> finishedResults = new List<Test.Result>();

        [SerializeField] private GUIQueue _guiQueue;
        [SerializeField] private Foldout _rootFoldout;
        public List<Foldout> foldouts = new List<Foldout>();
        public Vector2 scrollPosition;
        public bool showWelcome = true;
        public bool loadingWheelVisible = false;
        public string search = null;
        public string loadingWheelText = null;
        public List<Test> searchMatches = new List<Test>();

        public Utilities.DebugMode debug;

        public GUIQueue guiQueue
        {
            get
            {
                if (_guiQueue == null) _guiQueue = Utilities.CreateAsset<GUIQueue>(GUIQueue.fileName, Utilities.dataPath);
                return _guiQueue;
            }
        }

        public Foldout rootFoldout
        {
            get
            {
                if (_rootFoldout == null) _rootFoldout = Utilities.CreateAsset<Foldout>("rootFoldout", Utilities.foldoutDataPath);
                return _rootFoldout;
            }
        }

        public float timer;
        public uint nframes;

        public bool paused = false;
        public bool running = false;

        private uint previousFrameNumber = 0;

        /// <summary>
        /// Invoked when all Tests in the queue have been run and the editor has exited Play mode.
        /// </summary>
        public System.Action onStop;

        private List<System.Tuple<TestAttribute, MethodInfo>> attributesAndMethods = new List<System.Tuple<TestAttribute, MethodInfo>>();

        private System.Threading.Tasks.Task task;

        /// <summary>
        /// Save the TestManager asset as well as all assets linked to it.
        /// </summary>
        public void Save()
        {
            List<Object> assets = new List<Object>()
            {
                this,
                guiQueue,
                rootFoldout,
            };
            assets.AddRange(foldouts);
            assets.AddRange(tests);

            for (int i = 0; i < assets.Count; i++)
            {
                string message = AssetDatabase.GetAssetPath(assets[i]);
                if (assets[i].GetType() == typeof(Test)) message = "test " + (assets[i] as Test).attribute.GetPath();
                else if (assets[i].GetType() == typeof(Foldout)) message = "foldout " + (assets[i] as Foldout).path;

                if (EditorUtility.DisplayCancelableProgressBar("UnityTest", "Saving " + message, (float)(i + 1) / assets.Count))
                    break;

                Utilities.SaveAsset(assets[i]);

                //System.Threading.Thread.Sleep(2 * (int)1e3); // for testing the cancelable functionality
            }
            EditorUtility.ClearProgressBar();
        }

        

        public void OnPlayStateChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.ExitingPlayMode)
            {
                timer = 0f;
                nframes = 0;
                SkipRemainingTests();
            }
            if (change == PlayModeStateChange.EnteredEditMode) running = false;
        }

        [HideInCallstack]
        public void Update()
        {
            //Debug.Log(Time.frameCount + " " + previousFrameNumber);
            if (Time.frameCount > previousFrameNumber) OnUpdate();
            previousFrameNumber = (uint)Time.frameCount;
        }

        /// <summary>
        /// This method is only called when the editor has advanced a single frame.
        /// </summary>
        [HideInCallstack]
        private void OnUpdate()
        {
            if (running)
            {
                if (Test.current == null) // No test is currently running
                {
                    if (queue.Count > 0) // Start the next test if there is one
                    {
                        if (!paused) RunNext();
                        return;
                    }
                }
                else
                {
                    timer += Time.deltaTime;
                    nframes++;
                }
            }
        }

        [HideInCallstack]
        private void RunNext()
        {
            timer = 0f;
            nframes = 0;
            Test test = PopFromQueue();

            [HideInCallstack]
            void OnFinished()
            {
                test.onFinished -= OnFinished;
                test.PrintResult();
                AddToFinishedQueue(test);
                
                if (test.attribute.pauseOnFail && test.result == Test.Result.Fail)
                {
                    EditorApplication.isPaused = true;
                }

                if (queue.Count == 0)
                {
                    Stop();
                }
            }
            
            test.onFinished += OnFinished;
            test.Run();
        }

        /// <summary>
        /// Stop running the current test and move to the next.
        /// </summary>
        [HideInCallstack]
        public void Skip()
        {
            if (Test.current == null)
            {
                RunNext();
                return;
            }
            Test.current.result = Test.Result.Skipped;

            Test.current.CancelCoroutines();
            Test.current.OnRunComplete();

            Test.current = null;
        }

        /// <summary>
        /// "factory reset" the TestManager, but don't clear out the asset data. If you want to clear the asset data, use TestManager.ClearData().
        /// </summary>
        public void Reset()
        {
            foldouts = new List<Foldout>();
            scrollPosition = default;
            showWelcome = true;
            loadingWheelVisible = false;
            loadingWheelText = null;
            search = default;
            searchMatches = new List<Test>();

            debug = Utilities.DebugMode.Log | Utilities.DebugMode.LogWarning | Utilities.DebugMode.LogError;
            Utilities.debug = debug;
            previousFrameNumber = 0;
            timer = 0f;
            nframes = 0;
            queue = new List<Test>();
            finishedTests = new List<Test>();
            finishedResults = new List<Test.Result>();
        }


        #region Test Execution
        /// <summary>
        /// Begin working through the queue, running each Test.
        /// </summary>
        public void Start()
        {
            finishedTests.Clear();
            finishedResults.Clear();

            running = true;

            if (!EditorApplication.isPlaying)
            {
                EditorApplication.EnterPlaymode(); // can cause recompile
            }
            else
            {
                Utilities.Log("Starting");
            }
        }

        private void SkipRemainingTests()
        {
            // Mark any remaining tests as skipped
            if (Test.current != null)
            {
                Test.current.result = Test.Result.Skipped;
                Test.current.CancelCoroutines();
                AddToFinishedQueue(Test.current);
                Test.current.PrintResult();
                Test.current = null;
            }
            foreach (Test test in queue)
            {
                test.result = Test.Result.Skipped;
                AddToFinishedQueue(test);
                test.PrintResult();
            }
            queue.Clear();
        }

        /// <summary>
        /// Stop running tests and clear the queues
        /// </summary>
        public void Stop()
        {
            SkipRemainingTests();
            paused = false;
            running = false;
            if (EditorApplication.isPlaying) EditorApplication.ExitPlaymode();
            onStop();
            Utilities.Log("Finished", null, null);
        }
        #endregion




        #region Assembly and Test Management
        public void UpdateSearchMatches(TestManagerUI ui, string newSearch)
        {
            searchMatches.Clear();
            search = newSearch;
            if (string.IsNullOrEmpty(search)) return;
            Regex re = new Regex(search, RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.IgnorePatternWhitespace);

            string path;
            MatchCollection matches;
            foreach (Test test in rootFoldout.GetTests(this).OrderBy(x => x.attribute.GetPath()))
            {
                path = test.attribute.GetPath();
                matches = re.Matches(path);
                if (matches.Count == 0) continue;

                searchMatches.Add(test);
            }
        }
        public void AddToQueue(Test test)
        {
            queue.Insert(0, test);
        }
        private void AddToFinishedQueue(Test test)
        {
            finishedTests.Insert(0, test);
            finishedResults.Insert(0, test.result);
        }
        private Test PopFromQueue()
        {
            Test test = queue[0];
            queue.RemoveAt(0);
            return test;
        }

        private T GetAttribute<T>(MethodInfo method) => (T)(object)method.GetCustomAttribute(typeof(T), false);
        private T GetAttribute<T>(System.Type cls) => (T)(object)cls.GetCustomAttribute(typeof(T), false);

        public bool IsMethodIgnored(MethodInfo method) => GetAttribute<IgnoreAttribute>(method) != null;

        /// <summary>
        /// Collect all the assemblies that Unity has compiled.
        /// </summary>
        public HashSet<System.Reflection.Assembly> GetAssemblies()
        {
            List<string> added = new List<string>();
            HashSet<System.Reflection.Assembly> assemblies = new HashSet<System.Reflection.Assembly>();
            string path;
            foreach (AssembliesType type in (AssembliesType[])System.Enum.GetValues(typeof(AssembliesType))) // Hit all assembly types
            {
                foreach (UnityEditor.Compilation.Assembly assembly in CompilationPipeline.GetAssemblies(type))
                {
                    path = Path.Join(Utilities.projectPath, assembly.outputPath);
                    if (added.Contains(path)) continue;
                    assemblies.Add(System.Reflection.Assembly.LoadFile(path));
                    added.Add(path);
                }
            }
            return assemblies;
        }

        

        private void CreateTest(TestAttribute attribute, MethodInfo method)
        {
            // Try to locate an existing Test asset that matches the given TestAttribute.
            // If none are found, create a new Test object and saves the asset to disk.

            // It isn't efficient, but let's try just searching through all the existing Test objects for one that has a matching TestAttribute
            Test test = Utilities.SearchForAsset<Test>((Test t) => t.attribute == attribute, Utilities.testDataPath, false);
            if (test == null) // Didn't find it
            {
                string name = System.Guid.NewGuid().ToString();
                test = Utilities.CreateAsset<Test>(name, Utilities.testDataPath, (Test t) =>
                {
                    t.method = method;
#pragma warning disable CS0618 // "obsolete" markers
                    t.isInSuite = method.DeclaringType.GetCustomAttribute(typeof(SuiteAttribute)) != null;
#pragma warning restore CS0618
                    t.attribute = attribute;
                });
                 
                // Each Test needs a "Default GameObject" that is stored in the UnityTest Data folder.
                //test.CreateDefaultPrefab();
            }
            else
            {
                test.method = method;
#pragma warning disable CS0618 // "obsolete" markers
                test.isInSuite = method.DeclaringType.GetCustomAttribute(typeof(SuiteAttribute)) != null;
#pragma warning restore CS0618
            }

            tests.Add(test);
        }

        /// <summary>
        /// Remove the Test from the list of tests, delete the Test asset, and remove the Test from all Foldouts.
        /// </summary>
        public void DestroyTest(Test test)
        {
            tests.Remove(test);
            Utilities.DeleteAsset(test);

            foreach (Foldout foldout in foldouts)
            {
                if (foldout.tests.Contains(test)) foldout.tests.Remove(test);
            }
        }

        /// <summary>
        /// Return the first Test that has the same TestAttribute, or null if one isn't found.
        /// </summary>
        public Test FindTest(TestAttribute attribute)
        {
            foreach (Test test in tests)
                if (test.attribute == attribute) return test;
            return null;
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

            //Debug.Log("Running");

            task = System.Threading.Tasks.Task.Run(() => UpdateTestAttributesAndMethods(assemblies));
            await task;
            task = null;

            List<TestAttribute> foundAttributes = new List<TestAttribute>();
            foreach (System.Tuple<TestAttribute, MethodInfo> obj in attributesAndMethods)
            {
                TestAttribute attribute = obj.Item1;
                MethodInfo method = obj.Item2;

                foundAttributes.Add(attribute);
                Test foundTest = FindTest(attribute);
                if (foundTest == null) CreateTest(attribute, method);
                else foundTest.method = method;
            }

            // Ensure that "tests" no longer contains any old Test objects that weren't found during this update
            foreach (Test test in new List<Test>(tests))
            {
                if (!foundAttributes.Contains(test.attribute)) DestroyTest(test);
            }

            // DEBUGGING: Pretend that the task took a long time
            //await Task.Delay(5000);

            if (onFinished != null) onFinished();
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