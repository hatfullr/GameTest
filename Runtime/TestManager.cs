using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.Compilation;
using System.Reflection;
using System.IO;

namespace UnityTest
{
    /// <summary>
    /// Holds the assemblies, Test methods, and queues. Executes the tests when instructed.
    /// </summary>
    public static class TestManager
    {
        public const string editorPref = "UnityTestManager";
        //public const string delimiter = "\n===|TestManager|===\n"; // Some unique value
        //public const string testDelimiter = "\n!!!@@@Test@@@!!!\n"; // Some unique value

        private static List<System.Reflection.Assembly> assemblies = new List<System.Reflection.Assembly>();

        /// <summary>
        /// The code methods which have a TestAttribute attached to them.
        /// </summary>
        public static SortedDictionary<TestAttribute, MethodInfo> methods = new SortedDictionary<TestAttribute, MethodInfo>();

        /// <summary>
        /// The Test objects associated with the methods that have a TestAttribute attached to them.
        /// </summary>
        public static SortedDictionary<TestAttribute, Test> tests = new SortedDictionary<TestAttribute, Test>();

        public static Queue queue = new Queue();
        public static Queue finishedTests = new Queue();

        public static float timer;
        public static uint nframes;

        /// <summary>
        /// When true, debug messages are printed to Console.
        /// </summary>
        public static bool debug = true;

        public static bool paused = false;
        public static bool running = false;

        private static uint previousFrameNumber = 0;

        [HideInCallstack]
        public static void OnEnable()
        {
            Load();
            AssemblyReloadEvents.beforeAssemblyReload += Save;
            AssemblyReloadEvents.afterAssemblyReload += Load;
            EditorApplication.playModeStateChanged += OnPlayStateChanged;
        }

        public static void OnDisable()
        {
            AssemblyReloadEvents.beforeAssemblyReload -= Save;
            AssemblyReloadEvents.afterAssemblyReload -= AfterAssemblyReload;
            EditorApplication.playModeStateChanged -= OnPlayStateChanged;
            Save();
        }

        private static void AfterAssemblyReload()
        {
            Load();
            if (running && queue.Count == 0) Start();
        }

        [HideInCallstack]
        public static void Update()
        {
            if (Time.frameCount > previousFrameNumber && !paused) OnUpdate();
            previousFrameNumber = (uint)Time.frameCount;
        }

        [HideInCallstack]
        private static void OnUpdate()
        {
            if (running)
            {
                if (EditorApplication.isPaused) return;

                if (Test.current == null) // No test is currently running
                {
                    if (queue.Count > 0) // Start the next test if there is one
                    {
                        RunNext();
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

        public static void Save()
        {
            EditorPrefs.SetString(editorPref, GetString());
        }

        public static void Load()
        {
            FromString(EditorPrefs.GetString(editorPref));
        }

        private static string GetString()
        {
            return string.Join('\n',
                timer,
                nframes,
                debug,
                paused,
                running,
                previousFrameNumber
            );
        }

        private static void FromString(string data)
        {
            if (string.IsNullOrEmpty(data)) return;
            string[] c = data.Split('\n');
            try { timer = float.Parse(c[0]); }
            catch (System.Exception e) { if (!(e is System.FormatException || e is System.IndexOutOfRangeException)) throw e; }
            try { nframes = uint.Parse(c[1]); }
            catch (System.Exception e) { if (!(e is System.FormatException || e is System.IndexOutOfRangeException)) throw e; }
            try { debug = bool.Parse(c[2]); }
            catch (System.Exception e) { if (!(e is System.FormatException || e is System.IndexOutOfRangeException)) throw e; }
            try { paused = bool.Parse(c[3]); }
            catch (System.Exception e) { if (!(e is System.FormatException || e is System.IndexOutOfRangeException)) throw e; }
            try { running = bool.Parse(c[4]); }
            catch (System.Exception e) { if (!(e is System.FormatException || e is System.IndexOutOfRangeException)) throw e; }
            try { previousFrameNumber = uint.Parse(c[5]); }
            catch (System.Exception e) { if (!(e is System.FormatException || e is System.IndexOutOfRangeException)) throw e; }
        }

        [HideInCallstack]
        private static void RunNext()
        {
            timer = 0f;
            nframes = 0;
            Test test = queue.Dequeue();

            [HideInCallstack]
            void OnFinished()
            {
                test.onFinished -= OnFinished;
                test.PrintResult();
                finishedTests.Enqueue(test);
                
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
        public static void Skip()
        {
            if (Test.current == null) throw new System.Exception("Cannot skip because there is no current Test");
            Test.current.skipped = true;

            Test.current.CancelCoroutines();
            Test.current.OnRunComplete();

            Test.current = null;
        }

        public static void Reset()
        {
            debug = true;
            previousFrameNumber = 0;
            timer = 0f;
            nframes = 0;
            queue = new Queue();
            finishedTests = new Queue();
            tests = new SortedDictionary<TestAttribute, Test>();
            methods = new SortedDictionary<TestAttribute, MethodInfo>();
            UpdateAssemblies();
            UpdateMethods();
            CreateTests();
        }


        public static void OnPlayStateChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.ExitingPlayMode)
            {
                queue.Clear();
                timer = 0f;
                nframes = 0;
                Test.current = null;
            }
            if (change == PlayModeStateChange.EnteredEditMode) running = false;
        }


        #region Test Execution
        /// <summary>
        /// Begin working through the queue, running each Test.
        /// </summary>
        public static void Start()
        {
            finishedTests.Clear();

            running = true;

            foreach (Test test in tests.Values)
                if (test.selected) queue.Enqueue(test);

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
        public static void Stop()
        {
            if (debug) Utilities.Log("Finished", null, null);
            queue.Clear();
            paused = false;
            running = false;
            if (EditorApplication.isPlaying) EditorApplication.ExitPlaymode();
        }
        #endregion




        #region Assembly and Test Management
        private static TestAttribute GetAttribute(MethodInfo method) => method.GetCustomAttribute(typeof(TestAttribute), false) as TestAttribute;

        public static bool IsMethodIgnored(MethodInfo method) => method.GetCustomAttribute(typeof(IgnoreAttribute), false) != null;

        /// <summary>
        /// Locate the assemblies that Unity has most recently compile, and load them into memory. This can only be called from the main thread.
        /// </summary>
        public static void UpdateAssemblies()
        {
            assemblies.Clear();
            foreach (AssembliesType type in (AssembliesType[])System.Enum.GetValues(typeof(AssembliesType))) // Hit all assembly types
            {
                foreach (UnityEditor.Compilation.Assembly assembly in CompilationPipeline.GetAssemblies(type))
                {
                    assemblies.Add(System.Reflection.Assembly.LoadFile(Path.Join(Utilities.projectPath, assembly.outputPath)));
                }
            }
        }

        /// <summary>
        /// Using the assemblies located by UpdateAssemblies(), find all the test methods and test suite classes, which are stored in 
        /// "methods" and "classes" respectively.
        /// </summary>
        public static void UpdateMethods()
        {
            List<MethodInfo> _methods = new List<MethodInfo>();
            List<System.Type> classes = new List<System.Type>();
            foreach (System.Reflection.Assembly assembly in assemblies)
            {
                foreach (System.Type type in assembly.GetTypes())
                {
                    if (!type.IsClass) continue; // only work with classes

                    // Fetch the test methods
                    foreach (MethodInfo method in type.GetMethods(Utilities.bindingFlags))
                    {
                        // Make sure the method has the TestAttribute decorator
                        object[] attributes = method.GetCustomAttributes(typeof(TestAttribute), false);
                        if (attributes.Length == 0) continue;
                        bool hasAttribute = false;
                        foreach (object attribute in attributes)
                        {
                            if (attribute.GetType() != typeof(TestAttribute)) continue;
                            hasAttribute = true;
                            break;
                        }
                        if (!hasAttribute) continue;

                        // It has the TestAttribute decorator
                        _methods.Add(method);
                    }

                    // Fetch the test suites
                    foreach (object attribute in type.GetCustomAttributes(typeof(SuiteAttribute), false))
                    {
                        if (attribute.GetType() != typeof(SuiteAttribute)) continue;
                        // Yes, this type has our SuiteAttribute
                        classes.Add(type);
                        break;
                    }
                }
            }

            List<TestAttribute> found = new List<TestAttribute>();
            foreach (MethodInfo method in _methods)
            {
                if (IsMethodIgnored(method)) continue;

                TestAttribute attribute = GetAttribute(method);
                found.Add(attribute);
                if (methods.ContainsKey(attribute)) methods[attribute] = method; // Update the method with the current one
                else methods.Add(attribute, method); // Add a new method
            }
            

            foreach (System.Type cls in classes)
            {
                SuiteAttribute attribute = cls.GetCustomAttribute(typeof(SuiteAttribute), false) as SuiteAttribute;
                bool hasSetUp = false;
                bool hasTearDown = false;
                foreach (MethodInfo method in cls.GetMethods(Utilities.bindingFlags))
                {
                    if (IsMethodIgnored(method)) continue;
                    if (method.Name == "SetUp") hasSetUp = true;
                    else if (method.Name == "TearDown") hasTearDown = true;
                }

                foreach (MethodInfo method in cls.GetMethods(Utilities.bindingFlags))
                {
                    if (IsMethodIgnored(method)) continue;
                    if (method.Name == "SetUp" || method.Name == "TearDown") continue;
                    TestAttribute attr;

                    if (hasSetUp && hasTearDown)
                        attr = new TestAttribute("SetUp", "TearDown", attribute.pauseOnFail, Path.Join(attribute.GetPath(), method.Name), attribute.sourceFile);
                    else if (hasSetUp && !hasTearDown)
                        attr = new TestAttribute("SetUp", attribute.pauseOnFail, Path.Join(attribute.GetPath(), method.Name), attribute.sourceFile);
                    else
                        attr = new TestAttribute(attribute.pauseOnFail, Path.Join(attribute.GetPath(), method.Name), attribute.sourceFile);
                    found.Add(attr);
                    if (methods.ContainsKey(attr)) methods[attr] = method;
                    else methods.Add(attr, method);
                }
            }

            // Ensure that methods no longer contains old, invalid items
            foreach (TestAttribute attribute in new List<TestAttribute>(methods.Keys))
            {
                if (!found.Contains(attribute)) methods.Remove(attribute);
            }
        }


        /// <summary>
        /// Populate the "tests" attribute with Test objects that are correctly linked up to their associated methods.
        /// </summary>
        public static void CreateTests()
        {
            Test newTest;
            foreach (TestAttribute attribute in methods.Keys)
            {
                newTest = Test.Get(attribute);
                newTest.method = methods[attribute];
                tests.Add(attribute, newTest);
            }
        }
        #endregion
    }
}