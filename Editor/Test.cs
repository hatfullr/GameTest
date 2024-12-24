using System.Reflection;
using UnityEngine;
using System.IO;
using UnityEditor;


namespace GameTest
{
    /// <summary>
    /// A unit test that will appear in GameTest as a toggleable test. Each Test has an executable method and an attribute.
    /// </summary>
    [System.Serializable]
    public class Test
    {
        /// <summary>
        /// The user-set prefab that is instantiated when running this test. If null, then "defaultPrefab" is used instead.
        /// </summary>
        public GameObject prefab;

        public Result result;
        /// <summary>
        /// The test method to be executed.
        /// </summary>
        public MethodInfo method;
        /// <summary>
        /// The attribute on the method to be executed.
        /// </summary>
        public TestAttribute attribute;
        public bool selected, locked;
        [HideInInspector] public bool isInSuite;
        public System.Action<Test> onFinished;

        private GameObject gameObject;
        private Object script;
        private GameObject instantiatedDefaultGO;

        private Coroutine coroutine;
        
        public static Test current { get; private set; }
        private static GameObject coroutineGO;
        private static bool sceneWarningPrinted = false;

        [SerializeField] private GameObject _defaultPrefab;
        public GameObject defaultPrefab
        {
            get
            {
                string name = attribute.GetPath();
                if (_defaultPrefab == null) _defaultPrefab = Utilities.SearchForAsset((GameObject g) => g.name == name, Utilities.dataPath, false);
                if (_defaultPrefab == null)
                {
                    string rawPath = Utilities.GetAssetPath(name);
                    Utilities.EnsureDirectoryExists(Path.GetDirectoryName(rawPath));
                    string path = Utilities.GetUnityPath(rawPath);
                    path = Path.ChangeExtension(path, ".prefab");
                    GameObject gameObject = new GameObject(name, method.DeclaringType);
                    _defaultPrefab = PrefabUtility.SaveAsPrefabAsset(gameObject, path, out bool success);
                    Object.DestroyImmediate(gameObject);
                    if (!success) throw new System.Exception("Failed to create prefab: " + gameObject.name);
                }
                return _defaultPrefab;
            }
        }

        [System.Serializable]
        public enum Result
        {
            None,
            Pass,
            Fail,
            Skipped,
        }

        public Test(TestAttribute attribute, MethodInfo method)
        {
            this.attribute = attribute;
            this.method = method;
#pragma warning disable CS0618 // "obsolete" markers
            isInSuite = method.DeclaringType.GetCustomAttribute(typeof(SuiteAttribute)) != null;
#pragma warning restore CS0618
        }
        
        public Test(Test original)
        {
            attribute = original.attribute;
            method = original.method;
            isInSuite = original.isInSuite;

            selected = original.selected;
            locked = original.locked;
            result = original.result;
            script = original.script;
            prefab = original.prefab;
        }

        private class CoroutineMonoBehaviour : MonoBehaviour { }

        public override string ToString() => "Test(" + attribute.GetPath() + ")";

        public static void SetCurrentTest(Test test)
        {
            if (test == null)
            {
                Assert.currentTestSource = null;
                Assert.currentTestScript = null;
            }
            else
            {
                Assert.currentTestSource = test.attribute.sourceFile;
                Assert.currentTestScript = test.GetScript();
            }
            current = test;
        }

        /// <summary>
        /// Destroy the default prefab, which is stored in this project.
        /// </summary>
        public bool DestroyDefaultPrefab()
        {
            bool ret = AssetDatabase.DeleteAsset(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(defaultPrefab));
            _defaultPrefab = null;
            return ret;
        }

        public GameObject DefaultSetUp()
        {
            if (prefab != null)
            {
                instantiatedDefaultGO = null;
                instantiatedDefaultGO = Object.Instantiate(prefab);
                return instantiatedDefaultGO;
            }
            // Checking if the method is a part of a Unit Test Suite
#pragma warning disable CS0618 // "obsolete" markers
            if (method.DeclaringType.GetCustomAttribute(typeof(SuiteAttribute), false) != null) return null;
#pragma warning restore CS0618 // "obsolete" markers
            return new GameObject(attribute.name + " (" + method.DeclaringType + ")", method.DeclaringType);
        }

        public void DefaultTearDown()
        {
            if (gameObject != null) Object.DestroyImmediate(gameObject);
            if (instantiatedDefaultGO != null) Object.DestroyImmediate(instantiatedDefaultGO);
            gameObject = null;
            instantiatedDefaultGO = null;
        }

        private void SetUp()
        {
            if (!string.IsNullOrEmpty(attribute.setUp))
            {
                // Custom method
                MethodInfo setUp = method.DeclaringType.GetMethod(attribute.setUp, Utilities.bindingFlags);
                object result = setUp.Invoke(method.DeclaringType, null);

                if (isInSuite)
                {
                    if (result != null) throw new System.Exception("Return type of SetUp in Suite must be void: " + method.DeclaringType);
                }
                else
                {
                    if (result.GetType() != typeof(GameObject)) throw new System.Exception("The SetUp method must return a GameObject, which is destroyed in TearDown. Received '" + result.GetType() + "' instead");

                    try
                    {
                        gameObject = result as GameObject;
                    }
                    catch (System.Exception e)
                    {
                        throw new System.Exception("Failed to convert the result of the SetUp function to a GameObject. The SetUp function must always return a GameObject which is destroyed in TearDown.\n" + e.Message);
                    }
                }
            }
            else
            {
                // Do the default setup
                gameObject = DefaultSetUp();
            }
        }

        private void TearDown()
        {
            if (!string.IsNullOrEmpty(attribute.tearDown))
            {
                MethodInfo tearDown = method.DeclaringType.GetMethod(attribute.tearDown, Utilities.bindingFlags);
                if (isInSuite) tearDown.Invoke(null, null);
                else tearDown.Invoke(gameObject.GetComponent(method.DeclaringType), new object[] { gameObject });
            }
            else DefaultTearDown();
        }

        [HideInCallstack]
        public void Run()
        {
            if (method == null) throw new System.Exception("Missing method reference on Test " + attribute.GetPath());

            if (!EditorApplication.isPlaying)
            {
                Logger.LogError("Cannot run a Test while not in Play mode");
                return;
            }

            Application.logMessageReceived -= HandleLog; // In case it's already added, remove the event (works even if not added)
            Application.logMessageReceived += HandleLog;

            SetCurrentTest(this);

            // Check if this scene is empty
            GameObject[] gameObjects = Object.FindObjectsOfType<GameObject>();
            if (gameObjects.Length > 0)
            {
                bool ignore = sceneWarningPrinted;

                // When UnityEngine.Rendering.DebugManager.instance.enableRuntimeUI is true, it's because the user is both using the URP pipeline,
                // and their settings are setup such that a GameObject called "[Debug Updater]" is created in empty scenes while in play mode.
                // Couldn't find any helpful info online about it. However, compiling this code requires URP in order to avoid a "type or namespace" 
                // error. To allow cross-pipeline support, the following #if flag has been issued to check if URP is installed.
                if (gameObjects.Length == 1)
                {
#if UNITY_PIPELINE_URP
                    ignore = UnityEngine.Rendering.DebugManager.instance.enableRuntimeUI && gameObjects[0].name == "[Debug Updater]";
#endif
                }
                if (!ignore)
                {
                    Logger.LogWarning("You are not in an empty scene. Test results might be misleading. Perhaps your previous TearDown function " +
                        "didn't correctly remove all the GameObjects, or you used Destroy instead of DestroyImmediate. Otherwise this might be intended behavior for " +
                        "the custom tests you wrote, in which case you can ignore this error.");
                    sceneWarningPrinted = true;
                }
            }

            result = Result.None;
            SetUp();

            // check the game object
            if (gameObject == null) throw new System.NullReferenceException("GameObject == null. Check your SetUp method for " + attribute.name + " in " + Utilities.GetUnityPath(attribute.sourceFile));

            Component component = gameObject.GetComponent(method.DeclaringType);

            if (component == null && instantiatedDefaultGO == null)
                throw new System.NullReferenceException("Component of type " + method.DeclaringType + " not found in the GameObject returned by the SetUp method.");

            // invoke the method
            if (method.ReturnType == typeof(System.Collections.IEnumerator))
            { // An IEnumerable is intended to be run over many frames as a coroutine, using the yield statement to separate frames.
                if (coroutineGO != null) Object.DestroyImmediate(coroutineGO);
                coroutineGO = new GameObject("Coroutine helper", typeof(CoroutineMonoBehaviour));
                coroutineGO.hideFlags = HideFlags.HideAndDontSave;
                
                try { StartCoroutine(method.Invoke(component, new object[] { gameObject }) as System.Collections.IEnumerator); }
                catch (TargetException) { Logger.LogError("TargetException was thrown (1). Please submit a bug report."); }
            }
            else
            {
                try { method.Invoke(component, new object[] { gameObject }); } // probably of type void
                catch (TargetException) { Logger.LogError("TargetException was thrown (2). Please submit a bug report."); }
                
                OnRunComplete();
            }
        }

        private void StartCoroutine(System.Collections.IEnumerator coroutineMethod)
        {
            CoroutineMonoBehaviour mono = coroutineGO.GetComponent<CoroutineMonoBehaviour>();
            coroutine = mono.StartCoroutine(DoCoroutine(coroutineMethod));
        }

        private System.Collections.IEnumerator DoCoroutine(System.Collections.IEnumerator coroutineMethod)
        {
            // Run the coroutine
            yield return coroutineMethod;

            // Clean up
            OnRunComplete();
        }

        /// <summary>
        /// Called when the test is finished, regardless of the results. Calls the TearDown function.
        /// </summary>
        [HideInCallstack]
        public void OnRunComplete()
        {
            CancelCoroutine();
            if (coroutineGO != null) Object.DestroyImmediate(coroutineGO);
            coroutineGO = null;

            TearDown();
            if (result == Result.None && result != Result.Skipped) result = Result.Pass;

            Application.logMessageReceived -= HandleLog;

            PrintResult();

            if (attribute.pauseOnFail && result == Result.Fail) EditorApplication.isPaused = true;

            SetCurrentTest(null);

            if (onFinished != null) onFinished(this);
        }

        /// <summary>
        /// Write to the Utilities.Log the result of this test.
        /// </summary>
        [HideInCallstack]
        public void PrintResult()
        {
            string message = attribute.GetPath();
            if (result == Result.Pass) message = string.Join(' ', Logger.ColorString("(Passed)", Utilities.green), message);
            else if (result == Result.Fail) message = string.Join(' ', Logger.ColorString("(Failed)", Utilities.red), message);
            else if (result == Result.Skipped) message = string.Join(' ', Logger.ColorString("(Skipped)", Utilities.yellow), message);
            else if (result == Result.None) message = string.Join(' ', "(Finished)", message);
            else throw new System.NotImplementedException(result.ToString());
            
            Logger.Log(message, GetScript());
        }

        public void CancelCoroutine()
        {
            if (coroutineGO != null)
            {
                CoroutineMonoBehaviour mono = coroutineGO.GetComponent<CoroutineMonoBehaviour>();
                if (mono != null && coroutine != null)
                {
                    mono.StopCoroutine(coroutine);
                }
            }
        }

        private void HandleLog(string logString, string stackTrace, LogType type)
        {
            if (type == LogType.Exception || type == LogType.Assert)
            {
                CancelCoroutine();
                result = Result.Fail;
                EditorApplication.isPaused = !EditorApplication.isPaused && // If false (already paused), stay paused
                    attribute.pauseOnFail; // If not paused (playing) and we should pause, then pause
            }
        }

        public void Reset()
        {
            CancelCoroutine();
            result = Result.None;
        }

        /// <summary>
        /// Locate the .cs script in the project where this Test is defined
        /// </summary>
        public Object GetScript()
        {
            if (script != null) return script;

            string pathToSearch = Path.GetDirectoryName(Path.Join(
                Path.GetFileName(Application.dataPath),     // Usually it's "Assets", unless Unity ever changes that
                Path.GetRelativePath(Application.dataPath, attribute.sourceFile))
            );

            string basename = Path.GetFileName(attribute.sourceFile);

            // It's ridiculous, but we fail to find scripts directly because Unity hasn't updated the cache yet,
            // or something like that. This method searches the directory that we know the script is in to
            // retrieve the GUIDs.
            string[] guids = AssetDatabase.FindAssets("t:Script", new string[] { pathToSearch });
            string matched = null;
            foreach (string guid in guids)
            {
                // Get the path of this asset by its GUID
                string path = AssetDatabase.GUIDToAssetPath(guid);
                // Simply check the basenames. There cannot be files of the same name in the same location.
                if (Path.GetFileName(path) == basename)
                {
                    matched = guid;
                    break;
                }
            }

            if (matched == null) Logger.LogError("Failed to find script '" + method.DeclaringType.FullName + "' in '" + pathToSearch + "'");
            else
            {
                script = AssetDatabase.LoadAssetAtPath(AssetDatabase.GUIDToAssetPath(matched), typeof(MonoScript));
            }
            return script;
        }

        /// <summary>
        /// This never destroys the user's set prefab for this object.
        /// </summary>
        public void DeleteDefaultPrefab()
        {
            if (_defaultPrefab == null) return;
            
            Object.DestroyImmediate(_defaultPrefab, true);

            string path = Utilities.GetAssetPath(attribute.GetPath());
            string dir = Path.GetDirectoryName(path);

            // Find and remove empty directories
            foreach (string directory in Utilities.IterateDirectories(dir, true))
            {
                string basename = Path.GetFileName(directory);
                if (basename == nameof(GameTest)) return;
                string[] files = Directory.GetFiles(directory, "*", SearchOption.TopDirectoryOnly);
                if (files == null) return;
                if (files.Length > 0) return;
                FileUtil.DeleteFileOrDirectory(directory);
            }
        }
    }
}