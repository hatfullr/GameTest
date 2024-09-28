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
    public class TestManager
    {
        public const string delimiter = "\n===|TestManager|===\n"; // Some unique value
        public const string testDelimiter = "\n!!!@@@Test@@@!!!\n"; // Some unique value

        private static List<System.Reflection.Assembly> assemblies = new List<System.Reflection.Assembly>();

        /// <summary>
        /// The code methods which have a TestAttribute attached to them.
        /// </summary>
        public SortedDictionary<TestAttribute, MethodInfo> methods = new SortedDictionary<TestAttribute, MethodInfo>();

        /// <summary>
        /// The Test objects associated with the methods that have a TestAttribute attached to them.
        /// </summary>
        public SortedDictionary<TestAttribute, Test> tests = new SortedDictionary<TestAttribute, Test>();

        public Queue queue = new Queue();
        public Queue finishedTests = new Queue();

        public float timer;
        public int nframes;

        /// <summary>
        /// When true, debug messages are printed to Console.
        /// </summary>
        public bool debug = true;

        public bool paused = false;
        public bool running = false;

        private uint previousFrameNumber = 0;

        public bool runTestsOnPlayMode = false;

        [HideInCallstack]
        public void Update()
        {
            if (Time.frameCount > previousFrameNumber && !paused) OnUpdate();
            previousFrameNumber = (uint)Time.frameCount;
        }

        [HideInCallstack]
        private void OnUpdate()
        {
            if (!EditorApplication.isPlaying)
            {
                // Make sure the queue is cleared in this case
                queue.Clear();
                return;
            }

            if (running)
            {
                if (EditorApplication.isPaused) return;
                timer += Time.deltaTime;
                nframes++;
            }
        }

        private void RunNext()
        {
            timer = 0f;
            nframes = 0;
            Test test = queue.Dequeue();

            [HideInCallstack]
            void OnFinished()
            {
                test.onFinished -= OnFinished;
                if (debug) test.PrintResult();
                finishedTests.Enqueue(test);
                if (queue.Count == 0)
                {
                    running = false;
                    if (debug) Debug.Log("[UnityTest] Finished");
                }
                else
                {
                    if (test.attribute.pauseOnFail && test.result == Test.Result.Fail)
                    {
                        EditorApplication.isPaused = true;
                    }
                    else
                    {
                        RunNext(); // Continue on to the next test
                    }
                }
            }
            
            test.onFinished += OnFinished;
            test.Run();
        }

        public void Reset()
        {
            debug = true;
            previousFrameNumber = 0;
            timer = 0f;
            nframes = 0;
            queue = new Queue();
            finishedTests = new Queue();
            tests = new SortedDictionary<TestAttribute, Test>();
            methods = new SortedDictionary<TestAttribute, MethodInfo>();
            runTestsOnPlayMode = false;
            UpdateAssemblies();
            UpdateMethods();
            CreateTests();
        }


        public void OnPlayStateChanged(PlayModeStateChange change)
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
        public void Start()
        {
            running = true;
            RunNext();
        }

        /// <summary>
        /// Stop running tests and clear the queues
        /// </summary>
        public void Stop()
        {
            queue.Clear();
            paused = false;
            running = false;
            if (EditorApplication.isPlaying) EditorApplication.ExitPlaymode();
        }
        #endregion




        #region Assembly and Test Management
        private TestAttribute GetAttribute(MethodInfo method) => method.GetCustomAttribute(typeof(TestAttribute), false) as TestAttribute;

        public bool IsMethodIgnored(MethodInfo method) => method.GetCustomAttribute(typeof(IgnoreAttribute), false) != null;

        /// <summary>
        /// Locate the assemblies that Unity has most recently compile, and load them into memory. This can only be called from the main thread.
        /// </summary>
        public void UpdateAssemblies()
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
        public void UpdateMethods()
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
        public void CreateTests()
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