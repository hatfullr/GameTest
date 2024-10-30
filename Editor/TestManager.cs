using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.Compilation;
using System.Reflection;
using System.IO;
using System.Threading.Tasks;
using System.Threading;

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
        public Tests tests;

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

        private SettingsWindow _settingsWindow;
        public SettingsWindow settingsWindow
        {
            get
            {
                if (_settingsWindow == null) _settingsWindow = EditorWindow.GetWindow<SettingsWindow>();
                return _settingsWindow;
            }
        }

        public float timer;
        public uint nframes;

        /// <summary>
        /// When true, debug messages are printed to Console.
        /// </summary>
        public bool debug = true;

        public bool paused = false;
        public bool running = false;

        private uint previousFrameNumber = 0;

        /// <summary>
        /// Invoked when all Tests in the queue have been run and the editor has exited Play mode.
        /// </summary>
        public System.Action onStop;

        private List<AttributeAndMethod> attributesAndMethods = new List<AttributeAndMethod>();

        private Task task;

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
            }
            EditorUtility.ClearProgressBar();
        }

        

        public void OnPlayStateChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.ExitingPlayMode)
            {
                timer = 0f;
                nframes = 0;
                Test.current = null;
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
            Test.current.skipped = true;

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

            debug = true;
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
                if (debug) Utilities.Log("Starting");
            }
        }

        /// <summary>
        /// Stop running tests and clear the queues
        /// </summary>
        public void Stop()
        {
            if (debug) Utilities.Log("Finished", null, null);
            queue.Clear();
            paused = false;
            running = false;
            if (EditorApplication.isPlaying) EditorApplication.ExitPlaymode();
            Test.current = null;
            onStop();
        }
        #endregion




        #region Assembly and Test Management
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
        private HashSet<System.Reflection.Assembly> GetAssemblies()
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

        private Test CreateTest(TestAttribute attribute, MethodInfo method)
        {
            // Try to locate an existing Test asset that matches the given TestAttribute.
            // If none are found, create a new Test object and saves the asset to disk.

            // It isn't efficient, but let's try just searching through all the existing Test objects for one that has a matching TestAttribute
            Test test = Utilities.SearchForAsset<Test>((Test t) => t.attribute == attribute, Utilities.testDataPath, false);
            if (test == null) // Didn't find it
            {
                test = Utilities.CreateAsset<Test>(System.Guid.NewGuid().ToString(), Utilities.testDataPath, (Test t) =>
                {
                    t.method = method;
                    t.isInSuite = method.DeclaringType.GetCustomAttribute(typeof(SuiteAttribute)) != null;
                    t.attribute = attribute;
                });
            }
            else
            {
                test.method = method;
                test.isInSuite = method.DeclaringType.GetCustomAttribute(typeof(SuiteAttribute)) != null;
            }
            return test;
        }
        
        private void UpdateTestAttributesAndMethods(HashSet<System.Reflection.Assembly> assemblies)
        {
            List<AttributeAndMethod> result = new List<AttributeAndMethod>();
            object[] classAttributes, methodAttributes;
            foreach (System.Reflection.Assembly assembly in assemblies)
            {
                foreach (System.Type type in assembly.GetTypes())
                {
                    if (!type.IsClass) continue; // only work with classes

                    classAttributes = type.GetCustomAttributes(typeof(SuiteAttribute), false);
                    if (classAttributes.Length > 0)
                    {
                        // Locate the SuiteAttribute if there is one. It has to be done this way for some reason I can't understand.
                        foreach (object attr in classAttributes)
                        {
                            if (attr.GetType() != typeof(SuiteAttribute)) continue;
                            SuiteAttribute suiteAttribute = attr as SuiteAttribute;

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

                                result.Add(new AttributeAndMethod(testAttribute, method));
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
                            result.Add(new AttributeAndMethod(attribute as TestAttribute, method));
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

            task = Task.Run(() => UpdateTestAttributesAndMethods(assemblies));
            await task;
            task = null;

            List<TestAttribute> foundAttributes = new List<TestAttribute>();
            foreach (AttributeAndMethod obj in attributesAndMethods)
            {
                foundAttributes.Add(obj.attribute);
                if (tests.ContainsAttribute(obj.attribute)) tests.UpdateMethod(obj.attribute, obj.method);
                else tests.Add(CreateTest(obj.attribute, obj.method)); // Also creates a Test asset in the database
            }

            // Ensure that "tests" no longer contains any old Test objects that weren't found during this update
            foreach (TestAttribute attribute in tests.GetAttributes())
            {
                if (!foundAttributes.Contains(attribute)) tests.RemoveAtAttribute(attribute); // Also deletes the Test asset from the database
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
            if (debug) Utilities.Log("Tests updated");
        }

        #endregion


        [System.Serializable]
        public class Tests : List<Test>
        {
            /// <summary>
            /// Remove the Test from this list and also delete its asset in the asset database
            /// </summary>
            public new void Remove(Test test)
            {
                Utilities.DeleteAsset(test);
                base.Remove(test);
            }
            public void RemoveAtAttribute(TestAttribute attribute)
            {
                foreach (Test test in new List<Test>(this))
                {
                    if (test.attribute == attribute)
                    {
                        Remove(test);
                        return;
                    }
                }
            }
            public bool ContainsAttribute(TestAttribute attribute)
            {
                foreach (TestAttribute a in GetAttributes())
                    if (a == attribute) return true;
                return false;
            }
            public void UpdateMethod(TestAttribute attribute, MethodInfo method)
            {
                foreach (Test test in this)
                {
                    if (test.attribute == attribute)
                    {
                        test.method = method;
                        return;
                    }
                }
            }
            public IEnumerable<TestAttribute> GetAttributes()
            {
                foreach (Test test in new List<Test>(this))
                    yield return test.attribute;
            }
        }

        private struct AttributeAndMethod
        {
            public TestAttribute attribute;
            public MethodInfo method;

            public AttributeAndMethod(TestAttribute attribute, MethodInfo method)
            {
                this.attribute = attribute;
                this.method = method;
            }
        }
    }
}