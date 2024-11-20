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
        public bool showWelcome = true;
        public Utilities.DebugMode debug;
        public string search = null;
        public bool paused = false;
        public bool running = false;

        public Vector2 scrollPosition;

        public List<Foldout> foldouts = new List<Foldout>();

        public List<Test> queue = new List<Test>();
        public List<Test> finishedTests = new List<Test>();

        public GUIQueue guiQueue;
        
        public bool loadingWheelVisible = false;
        public string loadingWheelText = null;

        public List<Test> searchMatches = new List<Test>();

        
        

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
            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssetIfDirty(this);
        }

        public void OnPlayStateChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.ExitingPlayMode)
            {
                SkipRemainingTests();
            }
            if (change == PlayModeStateChange.EnteredEditMode) running = false;

            guiQueue.OnPlayStateChanged(change);
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
                    guiQueue.IncrementTimer(Time.deltaTime);
                }
            }
        }

        [HideInCallstack]
        private void RunNext()
        {
            guiQueue.ResetTimer();

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
            foldouts.Clear();
            scrollPosition = default;
            showWelcome = true;
            loadingWheelVisible = false;
            loadingWheelText = null;
            search = default;
            searchMatches.Clear();

            debug = Utilities.DebugMode.Log | Utilities.DebugMode.LogWarning | Utilities.DebugMode.LogError;
            Utilities.debug = debug;
            previousFrameNumber = 0;

            guiQueue.Reset();
        }


        #region Test Execution
        /// <summary>
        /// Begin working through the queue, running each Test.
        /// </summary>
        public void Start()
        {
            finishedTests.Clear();

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
        public void AddToQueue(Test test)
        {
            foreach (Test t in queue.ToArray())
            {
                if (t.attribute.GetPath() == test.attribute.GetPath())
                {
                    queue.Remove(t);
                    break;
                }
            }
            queue.Insert(0, test);
        }
        private void AddToFinishedQueue(Test test)
        {
            finishedTests.Insert(0, new Test(test));
        }
        private Test PopFromQueue()
        {
            Test test = queue[0];
            queue.RemoveAt(0);
            return test;
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
                Debug.Log("Set Test method " + foundTest + " " + method);
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
                    foldout.tests.Remove(test);
                    if (foldout.tests.Count == 0)
                    {
                        foldouts.Remove(foldout);
                        break;
                    }
                }
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