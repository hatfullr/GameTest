using System.Linq;
using System.Reflection;
using UnityEngine;
using System.IO;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace UnityTest
{
#if UNITY_EDITOR
    /// <summary>
    /// A unit test that will appear in the Unit Test Manager as a toggleable test. Use this class as an attribute on a method.
    /// </summary>
    public class Test : System.IEquatable<Test>
    {
        public const BindingFlags bindingFlags = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly | BindingFlags.Static;
        
        public string name;
        public Result result;
        public MethodInfo method;
        public TestAttribute attribute { get; private set; }

        public bool isExample { get; private set; } = false;

        public GameObject defaultGameObject
        {
            get
            {
                if (string.IsNullOrEmpty(_defaultGameObjectGUID)) return null;
                return AssetDatabase.LoadAssetAtPath(AssetDatabase.GUIDToAssetPath(_defaultGameObjectGUID), typeof(GameObject)) as GameObject;
            }
            set
            {
                if (value == null) _defaultGameObjectGUID = null;
                else _defaultGameObjectGUID = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(value));
            }
        }

        /// <summary>
        /// This is true whenever a Test is being run, uncluding during the SetUp and TearDown methods, and false otherwise.
        /// </summary>
        public static bool isTesting => current != null;

        public bool selected;
        public bool locked;

        private string _defaultGameObjectGUID; 
        private GameObject gameObject;
        private Object script = null;
        public bool expanded;
        public const float scriptWidth = 150f;
        private const string delimiter = "\n===| Test |===\n"; // Some unique delimiter
        private GameObject instantiatedDefaultGO = null;
        private static GameObject coroutineGO = null;
        private static List<System.Collections.IEnumerator> coroutines = new List<System.Collections.IEnumerator>();
        private static List<Coroutine> cos = new List<Coroutine>();
        public static Test current;

        private static GUIStyle toggleStyle;
        private static GUIStyle foldoutStyle;

        private static bool sceneWarningPrinted = false;

        public System.Action onFinished;


        #region Operators

        public override bool Equals(object obj)
        {
            if (obj.GetType() == typeof(Test)) return Equals(obj as Test);
            return base.Equals(obj);
        }
        public bool Equals(Test other) => attribute == other.attribute;
        public override int GetHashCode() => System.HashCode.Combine(base.GetHashCode(), GetString());
        public static bool operator ==(Test left, Test right) => Equals(left, right);
        public static bool operator !=(Test left, Test right) => !(left == right);
        #endregion


        public enum Result
        {
            None,
            Pass,
            Fail,
        }
        
        private Test() { }

        public Test(TestAttribute attribute, MethodInfo method)
        {
            name = attribute.path.Split("/").Last();
            this.attribute = attribute;
            this.method = method;
            result = Result.None;
        }

        private class CoroutineMonoBehaviour : MonoBehaviour { }

        public override string ToString() => "Test(" + attribute.path + ")";

        
        public string GetString()
        {
            string data = name;
            data += delimiter;
            data += attribute.GetString();
            data += delimiter;
            data += ((int)result).ToString();
            data += delimiter;
            data += _defaultGameObjectGUID;
            data += delimiter;
            data += selected;
            data += delimiter;
            data += expanded;
            data += delimiter;
            data += locked;
            return data;
        }
        public static Test FromString(string s)
        {
            if (string.IsNullOrEmpty(s)) return null;
            Test newTest = new Test();
            string[] data = s.Split(delimiter);
            newTest.name = data[0];
            newTest.attribute = TestAttribute.FromString(data[1]);
            newTest.result = (Result)int.Parse(data[2]);
            newTest._defaultGameObjectGUID = data[3];
            newTest.selected = bool.Parse(data[4]);
            newTest.expanded = bool.Parse(data[5]);
            newTest.locked = bool.Parse(data[6]);
            //Debug.Log(newTest.name + " " + newTest.selected + " " + data[4]);
            //if (newTest.selected) Debug.Log(newTest.name + " " + newTest.selected);
            return newTest;
        }

        public bool IsInSuite() => method.DeclaringType.GetCustomAttribute(typeof(SuiteAttribute)) != null;

        public GameObject DefaultSetUp()
        {
            if (defaultGameObject != null)
            {
                instantiatedDefaultGO = null;
                instantiatedDefaultGO = Object.Instantiate(defaultGameObject);
                return instantiatedDefaultGO;
            }
            // Checking if the method is a part of a Unit Test Suite
            if (method.DeclaringType.GetCustomAttribute(typeof(SuiteAttribute), false) != null)
            {
                return null;
            }
            return new GameObject(name + " (" + method.DeclaringType + ")", method.DeclaringType);
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
                MethodInfo setUp = method.DeclaringType.GetMethod(attribute.setUp, bindingFlags);
                object result = setUp.Invoke(null, null);


                if (IsInSuite())
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
            //Debug.Log("TearDown " + this);
            if (!string.IsNullOrEmpty(attribute.tearDown))
            {
                MethodInfo tearDown = method.DeclaringType.GetMethod(attribute.tearDown, bindingFlags);
                if (IsInSuite()) tearDown.Invoke(null, null);
                else tearDown.Invoke(gameObject.GetComponent(method.DeclaringType), new object[] { gameObject });
            }
            else DefaultTearDown();
        }

        [HideInCallstack]
        public void Run()
        {
            if (!EditorApplication.isPlaying)
            {
                Debug.LogWarning("Cannot run Unit Tests outside of Play mode!");
                return;
            }

            Application.logMessageReceived += HandleLog;
            
            current = this;

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
                    Debug.LogWarning("You are not in an empty scene. Unit Test results might be misleading. Perhaps your previous TearDown function " +
                        "didn't correctly remove all the GameObjects, or you used Destroy instead of DestroyImmediate. Otherwise this might be intended behavior for " +
                        "the custom tests you wrote, in which case you can ignore this error.");
                    sceneWarningPrinted = true;
                }
            }

            result = Result.None;
            SetUp();

            // If not a Suite, check the game object
            if (!IsInSuite())
            {
                if (gameObject == null) throw new System.NullReferenceException("GameObject == null. Check your SetUp method for " + attribute.path);
                if (gameObject.GetComponent(method.DeclaringType) == null && instantiatedDefaultGO == null)
                    throw new System.NullReferenceException("Component of type " + method.DeclaringType + " not found in the GameObject returned by the SetUp method.");
            }

            // invoke the method
            if (method.ReturnType == typeof(System.Collections.IEnumerator))
            { // An IEnumerable is intended to be run over many frames as a coroutine, using the yield statement to separate frames.
                if (coroutineGO != null) Object.DestroyImmediate(coroutineGO);
                coroutineGO = new GameObject("Coroutine helper", typeof(CoroutineMonoBehaviour));
                coroutineGO.hideFlags = HideFlags.HideAndDontSave;
                if (IsInSuite())
                {
                    Suite suite = Suite.Get(method.DeclaringType);
                    StartCoroutine(method.Invoke(suite, null) as System.Collections.IEnumerator);
                }
                else
                {
                    try { StartCoroutine(method.Invoke(gameObject.GetComponent(method.DeclaringType), new object[] { gameObject }) as System.Collections.IEnumerator); }
                    catch (TargetException) { Debug.LogError("TargetException was thrown (1). Please submit a bug report."); }
                }
            }
            else
            {
                if (IsInSuite())
                {
                    Suite suite = Suite.Get(method.DeclaringType);
                    method.Invoke(suite, null); // probably of type void
                }
                else
                {
                    try { method.Invoke(gameObject.GetComponent(method.DeclaringType), new object[] { gameObject }); } // probably of type void
                    catch (TargetException) { Debug.LogError("TargetException was thrown (2). Please submit a bug report."); }
                }
                OnRunComplete();
            }
        }

        private void StartCoroutine(System.Collections.IEnumerator coroutineMethod)
        {
            CoroutineMonoBehaviour mono = coroutineGO.GetComponent<CoroutineMonoBehaviour>();
            cos.Add(mono.StartCoroutine(DoCoroutine(coroutineMethod)));
        }

        private System.Collections.IEnumerator DoCoroutine(System.Collections.IEnumerator coroutineMethod)
        {
            coroutines.Add(coroutineMethod);

            // Wait until it's our turn to run
            while (coroutines[0] != coroutineMethod)
                yield return null;

            // Run the coroutine
            yield return coroutineMethod;

            // Clean up
            OnRunComplete();

            coroutines.Remove(coroutineMethod);
        }

        /// <summary>
        /// Called when the test is finished, regardless of the results. Pauses the editor if the test specifies to do so.
        /// </summary>
        private void OnRunComplete()
        {
            TearDown();
            if (result == Result.None) result = Result.Pass;

            Application.logMessageReceived -= HandleLog;

            if (coroutineGO != null) Object.DestroyImmediate(coroutineGO);
            coroutineGO = null;

            current = null;

            onFinished.Invoke();
        }

        /// <summary>
        /// Write to the Debug.Log the result of this test.
        /// </summary>
        public void PrintResult()
        {
            if (result == Result.Pass)
                Debug.Log("[UnityTest] <color=green>" + attribute.path + "</color>", GetScript());
            else if (result == Result.Fail)
                Debug.LogError("[UnityTest] <color=red>" + attribute.path + "</color>", GetScript());
            else if (result == Result.None)
                Debug.LogWarning("[UnityTest] <color=yellow>" + attribute.path + "</color>", GetScript());
            else throw new System.NotImplementedException(result.ToString());
        }

        private void CancelCoroutines()
        {
            if (coroutineGO != null)
            {
                CoroutineMonoBehaviour mono = coroutineGO.GetComponent<CoroutineMonoBehaviour>();
                if (mono != null)
                {
                    foreach (Coroutine coroutine in cos)
                    {
                        mono.StopCoroutine(coroutine);
                    }
                }
            }
            coroutines.Clear();
        }

        private void HandleLog(string logString, string stackTrace, LogType type)
        {
            if (type == LogType.Exception || type == LogType.Assert)
            {
                CancelCoroutines();
                result = Result.Fail;
                EditorApplication.isPaused = !EditorApplication.isPaused && // If false (already paused), stay paused
                    attribute.pauseOnFail; // If not paused (playing) and we should pause, then pause
            }
        }

        public void Reset()
        {
            CancelCoroutines();
            result = Result.None;
        }

        /// <summary>
        /// Locate the .cs script in the project where this Test is defined
        /// </summary>
        public Object GetScript()
        {
            if (script != null) return script;

            isExample = false;

            string pathToSearch;

            // Intercept trying to load the ExampleTests.cs script from the package folder
            string internalDir = Path.Join(Path.Join(Path.Join(".", "Packages"), "UnityTest"), "Runtime");
            if (Path.GetDirectoryName(attribute.sourceFile) == internalDir)
            {
                pathToSearch = Path.Join("Packages", "UnityTest", "Runtime");
                isExample = true;
            }
            else
            {
                pathToSearch = Path.GetDirectoryName(Path.Join(
                    Path.GetFileName(Application.dataPath),     // Usually it's "Assets", unless Unity ever changes that
                    Path.GetRelativePath(Application.dataPath, attribute.sourceFile))
                );
            }

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

            if (matched == null) Debug.LogError("Failed to find script '" + method.DeclaringType.FullName + "' in '" + pathToSearch + "'");
            else
            {
                script = AssetDatabase.LoadAssetAtPath(AssetDatabase.GUIDToAssetPath(matched), typeof(MonoScript));
            }
            return script;
        }

        public static GUIStyle GetToggleStyle()
        {
            if (toggleStyle == null)
            {
                toggleStyle = new GUIStyle(EditorStyles.iconButton);
                toggleStyle.alignment = EditorStyles.toggle.alignment;
                toggleStyle.fixedWidth = EditorStyles.toggle.fixedWidth;
                toggleStyle.fixedHeight = EditorStyles.toggle.fixedHeight;
                toggleStyle.font = EditorStyles.toggle.font;
                toggleStyle.fontStyle = EditorStyles.toggle.fontStyle;
                toggleStyle.fontSize = EditorStyles.toggle.fontSize;
                toggleStyle.clipping = EditorStyles.toggle.clipping;
                toggleStyle.border = EditorStyles.toggle.border;
                toggleStyle.contentOffset = EditorStyles.toggle.contentOffset;
                toggleStyle.imagePosition = EditorStyles.toggle.imagePosition;
                toggleStyle.margin = EditorStyles.toggle.margin;
                toggleStyle.overflow = EditorStyles.toggle.overflow;
                toggleStyle.padding = EditorStyles.toggle.padding;
                toggleStyle.richText = EditorStyles.toggle.richText;
                toggleStyle.stretchHeight = EditorStyles.toggle.stretchHeight;
                toggleStyle.stretchWidth = EditorStyles.toggle.stretchWidth;
                toggleStyle.wordWrap = EditorStyles.toggle.wordWrap;

                toggleStyle.padding.left = 0;
            }
            return toggleStyle;
        }

        public static GUIStyle GetFoldoutStyle()
        {
            if (foldoutStyle == null)
            {
                foldoutStyle = new GUIStyle(EditorStyles.foldout);
                foldoutStyle.padding = new RectOffset(0, 0, 0, 0);
                foldoutStyle.overflow = new RectOffset(0, 0, 0, 0);
                foldoutStyle.contentOffset = Vector2.zero;
                foldoutStyle.margin = new RectOffset(0, 0, 0, 0);

            }
            return foldoutStyle;
        }

        public static void PaintResultFeatures(Rect rect, Test.Result result)
        {
            if (result == Test.Result.Fail)
            {
                EditorGUI.DrawRect(rect, new Color(1f, 0f, 0f, 0.1f));
            }
        }

        public static List<bool> DrawToggle(Rect rect, string name, bool selected, bool locked, bool showLock = true, bool isMixed = false)
        {
            bool wasMixed = EditorGUI.showMixedValue;
            EditorGUI.showMixedValue = isMixed;

            // Draw the light highlight when the toggle is selected
            if (selected)
            {
                Rect r = new Rect(rect);
                float w = EditorStyles.toggle.padding.left;
                r.x += w;
                r.width -= w;
                EditorGUI.DrawRect(r, new Color(1f, 1f, 1f, 0.05f));
            }

            // Draw the lock button
            if (showLock)
            {
                Rect lockRect = new Rect(rect);
                lockRect.width = EditorStyles.toggle.CalcSize(GUIContent.none).x;
                locked = GUI.Toggle(lockRect, locked, GUIContent.none, "IN LockButton");
                rect.x += lockRect.width;
                rect.width -= lockRect.width;
            }

            // Draw the toggle
            bool wasEnabled = GUI.enabled;
            GUI.enabled &= !locked;
            selected = EditorGUI.ToggleLeft(rect, name, selected, GetToggleStyle());

            GUI.enabled = wasEnabled;

            EditorGUI.showMixedValue = wasMixed;

            return new List<bool> { selected, locked };
        }

        public void Draw(Rect rect, bool showLock = true, bool showFoldout = true, bool allowExpand = true)
        {
            bool wasEnabled = GUI.enabled;

            float toggleWidth = EditorStyles.toggle.CalcSize(GUIContent.none).x;

            // Draw the expanded box first so it appears behind everything else
            if (expanded && allowExpand && !IsInSuite())
            {
                float h = GUI.skin.label.CalcHeight(GUIContent.none, rect.width) + GUI.skin.label.margin.vertical;

                GUIStyle boxStyle = new GUIStyle("GroupBox");
                boxStyle.padding = GUI.skin.label.padding;
                boxStyle.margin = new RectOffset((int)rect.x, (int)(0.5f * boxStyle.border.right), 0, 0);
                boxStyle.padding.left = (int)(toggleWidth*2);
                boxStyle.padding.right -= boxStyle.margin.right;

                rect.y += 0.5f * boxStyle.padding.top;
                
                GUILayout.Space(-h); // Move the box closer to the Test foldout above
                GUILayout.BeginHorizontal(boxStyle); // This is so we can shift the GroupBox drawing to the right
                {
                    GUILayout.BeginVertical();
                    {
                        GUILayout.Space(h);

                        defaultGameObject = EditorGUI.ObjectField(
                            EditorGUILayout.GetControlRect(true),
                            new GUIContent("Default Prefab", "Provide a prefab from the Project folder. If the " +
                                "default SetUp method is used in this test then it will receive an instantiated copy of this prefab."),
                            defaultGameObject,
                            typeof(GameObject),
                            false
                        ) as GameObject;
                    }
                    GUILayout.EndVertical();
                }
                GUILayout.EndHorizontal();
            }

            if (showFoldout)
            {
                // This prevents the foldout from grabbing focus on mouse clicks on the toggle buttons
                Rect foldoutRect = new Rect(rect);
                foldoutRect.width = toggleWidth;
                expanded = allowExpand && GUI.Toggle(foldoutRect, expanded && allowExpand, GUIContent.none, GetFoldoutStyle());

                Rect scriptRect = new Rect(rect);
                scriptRect.x = rect.xMax - scriptWidth;
                scriptRect.width = scriptWidth;
                GUI.enabled = false;
                float previousLabelWidth = EditorGUIUtility.labelWidth;
                EditorGUIUtility.labelWidth = 0f;
                EditorGUI.ObjectField(scriptRect, GUIContent.none, GetScript(), method.DeclaringType, false);
                EditorGUIUtility.labelWidth = previousLabelWidth;
                GUI.enabled = wasEnabled;
            }

            Rect toggleRect = new Rect(rect);
            toggleRect.x += toggleWidth;
            toggleRect.width -= toggleWidth;
            if (!IsInSuite()) toggleRect.width -= scriptWidth;

            List<bool> res = DrawToggle(toggleRect, name, selected, locked, showLock, false);
            selected = res[0];
            locked = res[1];

            GUI.enabled = wasEnabled;
        }
    }
#endif

    /// <summary>
    /// This object can be used in the inspector to set a default prefab to be instantiated when running a Test instead
    /// of the default, which is to instantiate a new GameObject with an attached Component.
    /// </summary>
    [System.Serializable]
    public class TestPrefab
    {
        [SerializeField] private string _methodName;
        [SerializeField] private GameObject _gameObject;
        [HideInInspector] public GameObject gameObject { get => _gameObject; }
    }


#if UNITY_EDITOR
    [CustomPropertyDrawer(typeof(TestPrefab))]
    public class TestPrefabPropertyDrawer : PropertyDrawer
    {
        private float xpadding = 2f;
        private float ypadding = EditorGUIUtility.standardVerticalSpacing;
        private float lineHeight = EditorGUIUtility.singleLineHeight;

        private GUIContent[] methodNames;

        private void Initialize(SerializedProperty property)
        {
            System.Type type = property.serializedObject.targetObject.GetType();
            string[] names = type.GetMethods(Test.bindingFlags)
                          .Where(m => m.GetCustomAttributes(typeof(TestAttribute), true).Length > 0)
                          .Select(m => m.Name).ToArray();
            methodNames = new GUIContent[names.Length];
            for (int i = 0; i < names.Length; i++)
                methodNames[i] = new GUIContent(names[i]);
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return lineHeight;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (methodNames == null) Initialize(property);

            SerializedProperty _gameObject = property.FindPropertyRelative(nameof(_gameObject));
            SerializedProperty _methodName = property.FindPropertyRelative(nameof(_methodName));


            //Debug.Log(property.propertyType);

            int index = 0;
            bool labelInOptions = false;
            for (int i = 0; i < methodNames.Length; i++)
            {
                if (methodNames[i].text == _methodName.stringValue)
                {
                    index = i;
                }
                if (methodNames[i].text == label.text) labelInOptions = true;
            }

            // Create the rects to draw as though we had no prefix label (as inside ReorderableLists)
            Rect rect1 = new Rect(position);
            rect1.width = EditorGUIUtility.labelWidth - xpadding;

            Rect rect2 = new Rect(position);
            rect2.xMin = rect1.xMax + xpadding;
            rect2.width = position.xMax - rect1.xMax;

            if (labelInOptions && property.displayName == label.text)
            { // If we are in a ReorderableList, then don't draw the prefix label.
                label = GUIContent.none;
                // For some reason the height is not right if we don't do this...
                rect2.height = lineHeight;
            }
            else
            { // Otherwise, draw a prefix label
                Rect rect = new Rect(position);
                rect.width = EditorGUIUtility.labelWidth - xpadding;
                rect1.xMin = rect.xMax + xpadding;
                rect1.width = (position.xMax - rect.xMax) * 0.5f - 2 * xpadding;
                rect2.xMin = rect1.xMax + xpadding;
                rect2.width = position.xMax - rect2.xMin;

                EditorGUI.LabelField(rect, label);
                label = GUIContent.none;
            }
            int result = EditorGUI.Popup(rect1, label, index, methodNames);
            if (result < methodNames.Length) _methodName.stringValue = methodNames[result].text;
            EditorGUI.ObjectField(rect2, _gameObject, GUIContent.none);
        }
    }
#endif

}