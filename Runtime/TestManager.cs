using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.Reflection;
using System.IO;
using System.Threading.Tasks;

namespace UnityTest
{
    public class TestManager
    {
        private const string delimiter = "\n===|TestManager|===\n"; // Some unique value

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

        public System.Action onStartUpdatingMethods;
        public System.Action onFinishUpdatingMethods;

        /// <summary>
        /// When true, debug messages are printed to Console.
        /// </summary>
        public bool debug = true;

        public bool paused = false;
        public bool running = false;

        public uint previousFrameNumber = 0;

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

            if (Test.isTesting)
            {
                if (!EditorApplication.isPaused && Test.current.result == Test.Result.Fail) return;

                timer += Time.deltaTime;
                nframes++;
            }

            if (queue.Count == 0) return;
            running = true;
            if (Test.isTesting) return;
            // Start the next Test in the queue
            timer = 0f;
            nframes = 0;
            Test test = queue.Dequeue();

            [HideInCallstack]
            void OnFinished()
            {
                if (debug) test.PrintResult();
                finishedTests.Enqueue(test);
                if (queue.Count == 0)
                {
                    running = false;
                    if (debug) Debug.Log("[UnityTest] Finished");
                }
                test.onFinished -= OnFinished;
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


        




        #region Mechanisms
        private TestAttribute GetAttribute(MethodInfo method) => method.GetCustomAttribute(typeof(TestAttribute), false) as TestAttribute;

        public bool IsMethodIgnored(MethodInfo method) => method.GetCustomAttribute(typeof(IgnoreAttribute), false) != null;

        public async void StartUpdatingMethods()
        {
            onStartUpdatingMethods.Invoke();
            await Task.Run(UpdateMethods);
            onFinishUpdatingMethods.Invoke();
        }

        public void UpdateMethods()
        {
            // This hits all assemblies
            Assembly[] assemblies = System.AppDomain.CurrentDomain.GetAssemblies();

            List<MethodInfo> _methods = new List<MethodInfo>();
            List<System.Type> classes = new List<System.Type>();
            foreach (Assembly assembly in assemblies)
            {
                foreach (System.Type type in assembly.GetTypes())
                {
                    if (!type.IsClass) continue; // only work with classes

                    // Fetch the test methods
                    foreach (MethodInfo method in type.GetMethods(Test.bindingFlags))
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


            //IEnumerable<System.Type> types = assemblies // Returns all currenlty loaded assemblies
            //    .SelectMany(x => x.GetTypes()) // returns all types defined in these assemblies
            //    .Where(x => x.IsClass); // only yields classes
            //List<MethodInfo> _methods = types
            //    .SelectMany(x => x.GetMethods(Test.bindingFlags))
            //    .Where(x => x.GetCustomAttributes(typeof(TestAttribute), false).FirstOrDefault() != null).ToList();
            //List<System.Type> classes = types.Where(x => x.GetCustomAttributes(typeof(SuiteAttribute), false).FirstOrDefault() != null).ToList();

            methods.Clear();
            foreach (MethodInfo method in _methods)
            {
                if (IsMethodIgnored(method)) continue;
                try { methods.Add(GetAttribute(method), method); }
                catch (System.ArgumentException) // tried to add duplicate key (multiple tests have the same path)
                {
                    Debug.LogError("Test ignored because it has the same path as another test: '" + GetAttribute(method).GetPath() + "'");
                }
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
                        methods.Add(new TestAttribute("SetUp", "TearDown", attribute.pauseOnFail, Path.Join(attribute.GetPath(), method.Name), attribute.sourceFile), method);
                    else if (hasSetUp && !hasTearDown)
                        methods.Add(new TestAttribute("SetUp", attribute.pauseOnFail, Path.Join(attribute.GetPath(), method.Name), attribute.sourceFile), method);
                    else
                        methods.Add(new TestAttribute(attribute.pauseOnFail, Path.Join(attribute.GetPath(), method.Name), attribute.sourceFile), method);
                }
            }
        }

        public SortedDictionary<TestAttribute, Test> GetTests()
        {
            SortedDictionary<TestAttribute, Test> result = new SortedDictionary<TestAttribute, Test>();
            Test newTest;
            foreach (TestAttribute attribute in methods.Keys)
            {
                string path = attribute.GetPath();
                if (tests.ContainsKey(attribute))
                {
                    newTest = tests[attribute];
                    newTest.method = methods[attribute];
                    newTest.selected = tests[attribute].selected;
                }
                else newTest = new Test(attribute, methods[attribute]);
                result.Add(attribute, newTest);
            }
            return result;
        }

        public void CreateTests()
        {
            tests.Clear();
            tests = GetTests();
        }

        /// <summary>
        /// Stop running tests and clear the queues
        /// </summary>
        public void Stop()
        {
            queue.Clear();
            paused = false;
            running = false;
            EditorApplication.ExitPlaymode();
        }
        #endregion


        #region Persistence Methods
        /// <summary>
        /// Return the data as a string
        /// </summary>
        public string GetString()
        {
            return string.Join(delimiter,
                debug,
                previousFrameNumber,
                timer,
                nframes,
                runTestsOnPlayMode
            );
        }
        public void FromString(string data)
        {
            if (string.IsNullOrEmpty(data)) return;

            string[] c = data.Split(delimiter);

            try { debug = bool.Parse(c[0]); }
            catch (System.Exception e) { if (!(e is System.FormatException || e is System.IndexOutOfRangeException)) throw e; }

            try { previousFrameNumber = uint.Parse(c[1]); }
            catch (System.Exception e) { if (!(e is System.FormatException || e is System.IndexOutOfRangeException)) throw e; }

            try { timer = float.Parse(c[2]); }
            catch (System.Exception e) { if (!(e is System.FormatException || e is System.IndexOutOfRangeException)) throw e; }

            try { nframes = int.Parse(c[3]); }
            catch (System.Exception e) { if (!(e is System.FormatException || e is System.IndexOutOfRangeException)) throw e; }

            try { runTestsOnPlayMode = bool.Parse(c[4]); }
            catch (System.Exception e) { if (!(e is System.FormatException || e is System.IndexOutOfRangeException)) throw e; }
        }
        #endregion
    }
}