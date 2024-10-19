// TODO: Move this into the Editor/ folder

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

        /// <summary>
        /// When all Tests in the queue have been run and the editor has exited Play mode.
        /// </summary>
        public static System.Action onStop;

        [HideInCallstack]
        public static void OnEnable()
        {
            Load();
        }

        public static void OnDisable()
        {
            Save();
        }

        public static void OnBeforeAssemblyReload()
        {
            Save();
        }

        public static void OnAfterAssemblyReload()
        {
            UpdateTests();
            Load();
            if (running && queue.Count == 0) Start();
        }

        public static void OnPlayStateChanged(PlayModeStateChange change)
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
        public static void Update()
        {
            if (Time.frameCount > previousFrameNumber && !paused) OnUpdate();
            previousFrameNumber = (uint)Time.frameCount;
        }

        /// <summary>
        /// This method is only called when the editor has advanced a single frame.
        /// </summary>
        [HideInCallstack]
        private static void OnUpdate()
        {
            if (running)
            {
                //if (EditorApplication.isPaused) return;

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
                finishedTests.Enqueue(test, test.result); // Giving the result here saves the result
                
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
        public static void Reset()
        {
            debug = true;
            previousFrameNumber = 0;
            timer = 0f;
            nframes = 0;
            queue = new Queue();
            finishedTests = new Queue();
            Save();
        }

        /// <summary>
        /// Delete all the contents of the UnityTest/Data directory, which is where Test and Suite assets are stored. If any Test objects
        /// exist currently in the "tests" property, assets will be created for those tests.
        /// </summary>
        public static void ClearData()
        {
            AssetDatabase.DeleteAsset(Utilities.GetUnityPath(Utilities.dataPath));
            foreach (TestAttribute attribute in new List<TestAttribute>(tests.Keys))
            {
                tests[attribute] = CreateTest(attribute, tests[attribute].method);
            }
        }


        #region Test Execution
        /// <summary>
        /// Begin working through the queue, running each Test.
        /// </summary>
        public static void Start()
        {
            finishedTests.Clear();

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
        public static void Stop()
        {
            if (debug) Utilities.Log("Finished", null, null);
            queue.Clear();
            paused = false;
            running = false;
            if (EditorApplication.isPlaying) EditorApplication.ExitPlaymode();
            onStop();
        }
        #endregion




        #region Assembly and Test Management
        private static T GetAttribute<T>(MethodInfo method) => (T)(object)method.GetCustomAttribute(typeof(T), false);
        private static T GetAttribute<T>(System.Type cls) => (T)(object)cls.GetCustomAttribute(typeof(T), false);

        public static bool IsMethodIgnored(MethodInfo method) => GetAttribute<IgnoreAttribute>(method) != null;

        /// <summary>
        /// Collect all the assemblies that Unity has compiled.
        /// </summary>
        private static List<System.Reflection.Assembly> GetAssemblies()
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

        private static IEnumerable<MethodInfo> GetTests(List<System.Reflection.Assembly> assemblies)
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

        private static IEnumerable<System.Type> GetSuites(List<System.Reflection.Assembly> assemblies)
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


        private static Test CreateTest(TestAttribute attribute, MethodInfo method)
        {
            // Try to locate an existing Test asset that matches the given TestAttribute.
            // If none are found, create a new Test object and saves the asset to disk.

            // It isn't efficient, but let's try just searching through all the existing Test objects for one that has a matching TestAttribute
            foreach (string path in Directory.GetFiles(Utilities.dataPath, "*.asset", SearchOption.TopDirectoryOnly))
            {
                Test t = AssetDatabase.LoadAssetAtPath(Utilities.GetUnityPath(path), typeof(Test)) as Test;
                if (t.attribute != attribute) continue;
                t.method = method;
                return t;
            }

            // No Test was found amongst the existing Test assets that match the given TestAttribute. So we create a new one here.
            Test test = ScriptableObject.CreateInstance<Test>();
            test.attribute = attribute;
            test.method = method;

            // Save the Test asset with a unique GUID name to avoid conflicts.
            AssetDatabase.CreateAsset(test, Utilities.GetUnityPath(Path.Join(Utilities.dataPath, System.Guid.NewGuid() + ".asset")));

            return test;
        }

        private static void UpdateTests()
        {
            List<System.Reflection.Assembly> assemblies = GetAssemblies();

            List<TestAttribute> found = new List<TestAttribute>(); // Record the Tests we found
            // Later, we will use this list to remove from the "tests" property old Tests that were not found this time around.
            // This can happen when the user deletes a file which had Tests in it, for example.

            foreach (MethodInfo method in GetTests(assemblies))
            {
                TestAttribute attribute = GetAttribute<TestAttribute>(method);
                found.Add(attribute);
                if (tests.ContainsKey(attribute)) tests[attribute].method = method; // Update the Test's method
                else tests.Add(attribute, CreateTest(attribute, method)); // Create a new Test
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
                    if (tests.ContainsKey(testAttribute)) tests[testAttribute].method = method;
                    else tests.Add(testAttribute, CreateTest(testAttribute, method));
                }
            }

            // Ensure that "tests" no longer contains any Test objects that weren't found on this update
            foreach (TestAttribute attribute in new List<TestAttribute>(tests.Keys))
            {
                if (!found.Contains(attribute)) tests.Remove(attribute);
            }
        }

        private static MethodInfo GetSuiteMethod(System.Type suite, string name)
        {
            foreach (MethodInfo method in suite.GetMethods(Utilities.bindingFlags))
            {
                if (IsMethodIgnored(method)) continue;
                if (method.Name == name) return method;
            }
            return null;
        }

        #endregion
    }
}