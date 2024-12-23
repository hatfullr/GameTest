using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
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
        public Logger.DebugMode debug;
        public string search = null;
        public bool paused = false;
        public bool running = false;
        public TestManagerUI.TestSortOrder testSortOrder = TestManagerUI.TestSortOrder.Name;

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
            filePath = Path.Join(Utilities.dataPath, nameof(UnityTest) + ".asset");
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

        [HideInCallstack]
        public void Update()
        {
            if (Time.frameCount > previousFrameNumber) OnUpdate();
            previousFrameNumber = (uint)Time.frameCount;
        }

        /// <summary>
        /// This method is only called when the editor has advanced a single frame.
        /// </summary>
        [HideInCallstack]
        private void OnUpdate()
        {
            if (Test.current != null) guiQueue.IncrementTimer(Time.deltaTime);
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

            if (test.result == Test.Result.Skipped)
            {
                AddToFinishedQueue(test);
                test.PrintResult();
                return;
            }

            test.onFinished -= OnRunFinished;
            test.onFinished += OnRunFinished;

            test.Run();
        }

        private void OnRunFinished(Test test)
        {
            test.onFinished -= OnRunFinished;
            AddToFinishedQueue(test);
            guiQueue.ResetTimer();
            if (running && !paused)
            {
                if (queue.Count > 0) RunNext();
                else Stop();
            }
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
        }

        private void SkipRemainingTests()
        {
            // Mark all remaining tests for skipping
            foreach (Test test in queue) test.result = Test.Result.Skipped;

            // If there is a Test running currently, skip it
            Skip();
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

            debug = Logger.DebugMode.Log | Logger.DebugMode.LogWarning | Logger.DebugMode.LogError;
            Logger.debug = debug;
            previousFrameNumber = 0;

            guiQueue.Reset();
        }


        #region Test Execution
        /// <summary>
        /// Begin working through the queue, running each Test.
        /// </summary>
        public void Start()
        {
            finished.Clear();

            originalQueue.Clear();
            originalQueue.AddRange(queue); // Save a copy of the original queue to restore queue order in Stop()

            running = true;

            if (!EditorApplication.isPlaying) EditorApplication.EnterPlaymode(); // can cause recompile
            else Logger.Log("Starting");
        }

        /// <summary>
        /// Stop running tests and clear the queues
        /// </summary>
        public void Stop()
        {
            if (stopping) return;

            stopping = true;
            SkipRemainingTests();

            queue.AddRange(originalQueue); // Restore test ordering
            originalQueue.Clear();

            running = false;
            paused = false;

            if (EditorApplication.isPlaying) EditorApplication.ExitPlaymode();
            onStop();
            Logger.Log("Finished", null, null);
            stopping = false;
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
        /// If the Test is in the queue, remove it. Otherwise, do nothing.
        /// </summary>
        public void RemoveFromQueue(Test test)
        {
            foreach (Test t in queue.ToArray())
            {
                if (t.attribute.GetPath() == test.attribute.GetPath())
                {
                    queue.Remove(t);
                    break;
                }
            }
        }
        private void AddToFinishedQueue(Test test)
        {
            finished.Add(test); // This makes a reverse order
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
            if (queue.Contains(test)) queue.Remove(test);
            if (finished.Contains(test)) finished.Remove(test);
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