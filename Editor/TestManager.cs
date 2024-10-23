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
        /// When all Tests in the queue have been run and the editor has exited Play mode.
        /// </summary>
        public System.Action onStop;


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
        private List<System.Reflection.Assembly> GetAssemblies()
        {
            List<System.Reflection.Assembly> assemblies = new List<System.Reflection.Assembly>();
            foreach (AssembliesType type in (AssembliesType[])System.Enum.GetValues(typeof(AssembliesType))) // Hit all assembly types
            {
                foreach (UnityEditor.Compilation.Assembly assembly in CompilationPipeline.GetAssemblies(type))
                {
                    assemblies.Add(System.Reflection.Assembly.LoadFile(Path.Join(Utilities.projectPath, assembly.outputPath)));
                }
            }
            return assemblies;
        }

        private IEnumerable<MethodInfo> GetTests(List<System.Reflection.Assembly> assemblies)
        {
            foreach (System.Reflection.Assembly assembly in assemblies)
            {
                foreach (System.Type type in assembly.GetTypes())
                {
                    if (!type.IsClass) continue; // only work with classes
                    foreach (MethodInfo method in type.GetMethods(Utilities.bindingFlags))
                    {
                        if (IsMethodIgnored(method)) continue;

                        // It has to be done this way for some reason I can't understand.
                        object[] attributes = method.GetCustomAttributes(typeof(TestAttribute), false);
                        if (attributes.Length == 0) continue;
                        foreach (object attribute in attributes)
                        {
                            if (attribute.GetType() != typeof(TestAttribute)) continue;
                            yield return method;
                            break;
                        }
                    }
                }
            }
        }

        private IEnumerable<System.Type> GetSuites(List<System.Reflection.Assembly> assemblies)
        {
            foreach (System.Reflection.Assembly assembly in assemblies)
            {
                foreach (System.Type suite in assembly.GetTypes())
                {
                    if (!suite.IsClass) continue; // only work with classes

                    // It has to be done this way for some reason I can't understand.
                    object[] attributes = suite.GetCustomAttributes(typeof(SuiteAttribute), false);
                    if (attributes.Length == 0) continue;
                    foreach (object attribute in attributes)
                    {
                        if (attribute.GetType() != typeof(SuiteAttribute)) continue;
                        yield return suite;
                        break;
                    }
                }
            }
        }


        private Test CreateTest(TestAttribute attribute, MethodInfo method)
        {
            // Try to locate an existing Test asset that matches the given TestAttribute.
            // If none are found, create a new Test object and saves the asset to disk.

            // It isn't efficient, but let's try just searching through all the existing Test objects for one that has a matching TestAttribute
            Test t = Utilities.SearchForAsset<Test>((Test test) => test.attribute == attribute, Utilities.testDataPath, false);
            if (t != null) // Found it
            {
                t.method = method;
                return t;
            }

            return Utilities.CreateAsset<Test>(System.Guid.NewGuid().ToString(), Utilities.testDataPath, (Test test) => { test.attribute = attribute; test.method = method; });
        }

        public void UpdateTests()
        {
            List<System.Reflection.Assembly> assemblies = GetAssemblies();

            List<TestAttribute> found = new List<TestAttribute>(); // Record the Tests we found
            // Later, we will use this list to remove from the "tests" property old Tests that were not found this time around.
            // This can happen when the user deletes a file which had Tests in it, for example.

            foreach (MethodInfo method in GetTests(assemblies))
            {
                TestAttribute attribute = GetAttribute<TestAttribute>(method);
                found.Add(attribute);
                if (tests.ContainsAttribute(attribute)) tests.UpdateMethod(attribute, method);
                else tests.Add(CreateTest(attribute, method));
            }

            foreach (System.Type suite in GetSuites(assemblies))
            {
                SuiteAttribute attribute = GetAttribute<SuiteAttribute>(suite);

                // Get the Suite's SetUp and TearDown methods. These are null if it doesn't have them
                MethodInfo setUp = GetSuiteMethod(suite, "SetUp");
                MethodInfo tearDown = GetSuiteMethod(suite, "TearDown");

                foreach (MethodInfo method in suite.GetMethods(Utilities.bindingFlags))
                {
                    if (IsMethodIgnored(method)) continue;
                    if (method.Name == setUp.Name || method.Name == tearDown.Name) continue;

                    string path = Path.Join(attribute.GetPath(), method.Name); // The path for the Test Manager UI

                    TestAttribute testAttribute;

                    if (setUp != null && tearDown != null)
                        testAttribute = new TestAttribute(setUp.Name, tearDown.Name, attribute.pauseOnFail, path, attribute.sourceFile);
                    else if (setUp != null && tearDown == null)
                        testAttribute = new TestAttribute(setUp.Name, attribute.pauseOnFail, path, attribute.sourceFile);
                    else
                        testAttribute = new TestAttribute(attribute.pauseOnFail, path, attribute.sourceFile);

                    found.Add(testAttribute);

                    if (tests.ContainsAttribute(testAttribute)) tests.UpdateMethod(testAttribute, method);
                    else tests.Add(CreateTest(testAttribute, method));
                }
            }

            // Ensure that "tests" no longer contains any Test objects that weren't found on this update
            foreach (TestAttribute attribute in tests.GetAttributes())
            {
                if (!found.Contains(attribute)) tests.RemoveAtAttribute(attribute);
            }
        }

        private MethodInfo GetSuiteMethod(System.Type suite, string name)
        {
            foreach (MethodInfo method in suite.GetMethods(Utilities.bindingFlags))
            {
                if (IsMethodIgnored(method)) continue;
                if (method.Name == name) return method;
            }
            return null;
        }

        #endregion


        [System.Serializable]
        public class Tests : List<Test>
        {
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
    }
}