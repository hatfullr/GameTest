using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;


namespace UnityTest
{
    public static class Utilities
    {
        public static string debugTag { get => "[" + GetPackageInfo().displayName + "]"; }
        public const BindingFlags bindingFlags = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly | BindingFlags.Static;

        /// <summary>
        /// Location of the "Assets" folder.
        /// </summary>
        public static string assetsPath { get; } = Application.dataPath;

        /// <summary>
        /// Location of the Unity project.
        /// </summary>
        public static string projectPath { get; } = Path.GetDirectoryName(assetsPath);

        /// <summary>
        /// Location of the "Packages" folder, found as two directories up from the "Runtime" folder.
        /// </summary>
        public static string packagesPath { get; } = Path.Join(projectPath, "Packages");

        /// <summary>
        /// Location where data assets are stored.
        /// </summary>
        public static string dataPath { get => EnsureDirectoryExists(Path.Join(assetsPath, "UnityTest", "Data")); }

        /// <summary>
        /// Location where Foldout assets are stored.
        /// </summary>
        public static string foldoutDataPath { get => EnsureDirectoryExists(Path.Join(dataPath, "Foldouts")); }

        /// <summary>
        /// Location where Test assets are stored.
        /// </summary>
        public static string testDataPath { get => EnsureDirectoryExists(Path.Join(dataPath, "Tests")); }

        /// <summary>
        /// True if the editor is using the theme called "DarkSkin". Otherwise, false.
        /// </summary>
        public static bool isDarkTheme = true;

        /// <summary>
        /// HTML color green. Adapts to DarkSkin and LightSkin.
        /// </summary>
        public static string green { get { if (isDarkTheme) return "#50C878"; return "#164f00"; } }
        /// <summary>
        /// HTML color red. Adapts to DarkSkin and LightSkin.
        /// </summary>
        public static string red { get { if (isDarkTheme) return "red"; return "red"; } }

        public static float searchBarMinWidth = 80f;
        public static float searchBarMaxWidth = 300f;

        /// <summary>
        /// Query the package.json file to determine exactly what the current name of this package is.
        /// </summary>
        public static UnityEditor.PackageManager.PackageInfo GetPackageInfo() => UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(Utilities).Assembly);

        private static HashSet<string> loggedExceptions = new HashSet<string>();

        public static void DrawDebugOutline(Rect rect, Color color)
        {
            Rect left = new Rect(rect.xMin, rect.y, 1f, rect.height);
            Rect right = new Rect(rect.xMax, rect.y, 1f, rect.height);
            Rect top = new Rect(rect.x, rect.yMin, rect.width, 1f);
            Rect bottom = new Rect(rect.x, rect.yMax, rect.width, 1f);
            foreach (Rect r in new Rect[] { left, right, top, bottom })
            {
                EditorGUI.DrawRect(r, color);
            }
        }

        /// <summary>
        /// Given the path to a file, returns true if the path is located in the Samples folder, and false otherwise.
        /// </summary>
        public static bool IsSample(string path)
        {
            foreach (UnityEditor.PackageManager.UI.Sample sample in GetSamples())
            {
                if (sample.importPath == path) return true;
            }
            return false;
        }

        public static IEnumerable<UnityEditor.PackageManager.UI.Sample> GetSamples()
        {
            UnityEditor.PackageManager.PackageInfo info = GetPackageInfo();
            return UnityEditor.PackageManager.UI.Sample.FindByPackage(info.name, info.version);
        }

        /// <summary>
        /// Get the file path to Assets/UnityPath/Data/[name].asset.
        /// </summary>
        public static string GetAssetPath(string name, string directory)
        {
            if (directory == null) directory = dataPath;
            string path = Path.Join(directory, name);
            if (Path.GetExtension(path) != ".asset") path = Path.ChangeExtension(path, ".asset");
            return GetUnityPath(path);
        }

        public static string GetAssetPath(Object asset) => GetUnityPath(AssetDatabase.GetAssetPath(asset));

        /// <summary>
        /// Check Assets/UnityTest/Data/[name].asset to see if it exists.
        /// </summary>
        public static bool AssetExists(string name, string directory) => File.Exists(GetAssetPath(name, directory));

        /// <summary>
        /// Loop through all *.asset files in Assets/UnityTest/Data to find the first asset that meets the given criteria function. The
        /// input parameter to the criteria function is any Object. The result of the criteria function must be a bool.
        /// </summary>
        public static T SearchForAsset<T>(System.Func<T, bool> criteria, string searchDirectory, bool errorOnMissing = true)
        {
            foreach (string path in Directory.GetFiles(searchDirectory, "*.asset", SearchOption.TopDirectoryOnly))
            {
                T asset = LoadAssetAtPath<T>(GetUnityPath(path));
                if (asset == null) continue; // No asset of the given type exists at the given path
                if (criteria(asset)) return asset;
            }
            if (errorOnMissing) throw new AssetNotFound("type = '" + typeof(T) + "', criteria = '" + criteria + "'");
            return (T)(object)null;
        }

        /// <summary>
        /// Loop through all *.asset files in Assets/UnityTest/Data to find the first asset that meets the given criteria function. The
        /// input parameter to the criteria function is any Object. The result of the criteria function must be a bool.
        /// </summary>
        public static T LoadAssetAtPath<T>(string assetPath) => (T)(object)AssetDatabase.LoadAssetAtPath(GetUnityPath(assetPath), typeof(T));

        /// <summary>
        /// Create a new asset file at Assets/UnityTest/Data/[name].asset. If overwrite is true then if an asset with the same name already
        /// exists, it is destroyed and a new one is created in its place. Otherwise, if overwrite is false, the asset is loaded and returned.
        /// The type must inherit from ScriptableObject.
        /// </summary>
        public static T CreateAsset<T>(string name, string directory, System.Action<T> initializer = null, bool overwrite = false)
        {
            if (directory == null) directory = dataPath;

            // First check if this asset already exists
            string path = GetUnityPath(GetAssetPath(name, directory));
            if (AssetExists(name, directory))
            {
                if (overwrite)
                {
                    if (!AssetDatabase.DeleteAsset(path)) throw new System.Exception("Failed to overwrite asset at path '" + path + "'");
                }
                else return (T)(object)AssetDatabase.LoadAssetAtPath(path, typeof(T));
            }

            ScriptableObject result = ScriptableObject.CreateInstance(typeof(T));
            if (initializer != null) initializer((T)(object)result);
            AssetDatabase.CreateAsset(result, path);
            return (T)(object)result;
        }

        public static void SaveAssets(IEnumerable<Object> assets)
        {
            MarkAssetsForSave(assets);
            SaveDirtyAssets(assets);
        }
        public static void SaveAsset(Object asset) => SaveAssets(new List<Object> { asset });

        public static void SaveDirtyAsset(Object asset) => AssetDatabase.SaveAssetIfDirty(asset);
        public static void SaveDirtyAssets(IEnumerable<Object> assets)
        {
            foreach (Object asset in assets)
            {
                if (asset == null) continue;
                SaveDirtyAsset(asset);
            }
        }

        public static void MarkAssetForSave(Object asset) => EditorUtility.SetDirty(asset);
        public static void MarkAssetsForSave(IEnumerable<Object> assets)
        {
            foreach (Object asset in assets)
            {
                if (asset == null) continue;
                MarkAssetForSave(asset);
            }
        }

        public static void DeleteAsset(string name, string directory) => AssetDatabase.DeleteAsset(GetAssetPath(name, directory));
        public static void DeleteAsset(Object asset)
        {
            string path = GetAssetPath(asset);
            DeleteAsset(Path.GetFileName(path), GetUnityPath(Path.GetDirectoryName(path)));
        }

        public static void DeleteFolder(string path) => AssetDatabase.DeleteAsset(GetUnityPath(path));

        /// <summary>
        /// Create directories so that the given directory path exists. Returns the given directory path.
        /// </summary>
        public static string EnsureDirectoryExists(string directory)
        {
            directory = GetUnityPath(directory);

            string parent, newFolderName;

            // reverse order begins with top-most directory
            foreach (string dir in IterateDirectories(directory, true))
            {
                parent = Path.GetDirectoryName(dir);
                newFolderName = Path.GetFileName(dir);
                if (!AssetDatabase.IsValidFolder(dir))
                {
                    AssetDatabase.CreateFolder(parent, newFolderName);
                }
            }

            // Create the final directory at the destination path
            parent = Path.GetDirectoryName(directory);
            newFolderName = Path.GetFileName(directory);
            if (!AssetDatabase.IsValidFolder(directory))
            {
                AssetDatabase.CreateFolder(parent, newFolderName);
            }

            return directory;
        }

        public static bool IsSceneEmpty()
        {
            GameObject[] objects = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
            if (objects == null) return true;
            if (objects.Length == 0) return true;
            if (objects.Length != 2) return false;
            return objects[0].name == "MainCamera" && objects[1].name == "Directional Light";
        }

        /// <summary>
        /// Return each successive directory, starting at the directory of the given path and moving outward toward the root path, 
        /// until the root directory is reached.
        /// </summary>
        /// <param name="path">The path to iterate over</param>
        /// <param name="reverse">If true, the iterator starts at the root path and works out to the directory of the given path.</param>
        public static IEnumerable<string> IterateDirectories(string path, bool reverse = false)
        {
            if (reverse)
            {
                List<string> enumerator = new List<string>(IterateDirectories(path, false));
                enumerator.Reverse();
                foreach (string item in enumerator)
                {
                    yield return item;
                }
            }
            else
            {
                string directory = Path.GetDirectoryName(path);
                while (!string.IsNullOrEmpty(directory))
                {
                    yield return directory;
                    directory = Path.GetDirectoryName(directory);
                }
            }
        }


        /// <summary>
        /// For a given full file path, return a new path that starts either with "Assets" or "Packages", in the way that
        /// Unity expects for function AssetDatabase.LoadAssetAtPath().
        /// </summary>
        public static string GetUnityPath(string path)
        {
            // If the path begins with either "Assets" or "Packages", then it's already a Unity path.
            if (path.StartsWith("Assets") || path.StartsWith("Packages")) return path;

            path = Path.GetFullPath(path); // normalize the path

            if (IsPathChild(assetsPath, path)) // it's in the "Assets" folder
            {
                return Path.Join(
                    Path.GetFileName(assetsPath),
                    Path.GetRelativePath(assetsPath, path)
                );
            }
            if (IsPathChild(packagesPath, path)) // it's in the "Packages" folder somewhere
            {
                return Path.Join(
                    Path.GetFileName(packagesPath),
                    Path.GetRelativePath(packagesPath, path)
                );
            }
            throw new InvalidUnityPath(path);
        }

        /// <summary>
        /// Returns true if the given child path is located in any subdirectory of parent, or if it is located in parent itself.
        /// Returns false if the parent and child paths are the same, or if the child is not located within the parent.
        /// </summary>
        public static bool IsPathChild(string parent, string child)
        {
            if ((parent == child) || (string.IsNullOrEmpty(parent) && string.IsNullOrEmpty(child))) return false;

            if (string.IsNullOrEmpty(parent)) return true;
            if (string.IsNullOrEmpty(child)) return false;

            // Although this method does not require realistic file paths, we must treat them as realistic paths for
            // the purposes of comparison.
            parent = Path.GetFullPath(parent);
            child = Path.GetFullPath(child);
            
            // First check if the two are even on the same disk
            if (parent.Contains(Path.VolumeSeparatorChar) && child.Contains(Path.VolumeSeparatorChar))
            {
                string parentVolume = parent[..(parent.IndexOf(Path.VolumeSeparatorChar) + 1)];
                string childVolume = child[..(child.IndexOf(Path.VolumeSeparatorChar) + 1)];
                if (parentVolume != childVolume) return false;
            }

            //Debug.Log(parent + " " + child);
            foreach (string path in IterateDirectories(child))
            {
                //Debug.Log(path);
                if (parent == path) return true;
            }
            return false;
        }

        /// <summary>
        /// Returns true if the mouse is currently hovering over the given Rect, and false otherwise.
        /// </summary>
        public static bool IsMouseOverRect(Rect rect)
        {
            if (Event.current != null) return rect.Contains(Event.current.mousePosition) && GUI.enabled;
            return false;
        }

        /// <summary>
        /// When the mouse cursor is hovering over the given rect, the mouse cursor will change to the type specified.
        /// </summary>
        public static void SetCursorInRect(Rect rect, UnityEditor.MouseCursor cursor) => UnityEditor.EditorGUIUtility.AddCursorRect(rect, cursor);

        public static bool IsMouseButtonPressed() { if (Event.current == null) return false; return Event.current.rawType == EventType.MouseDown; }
        public static bool IsMouseButtonReleased() { if (Event.current == null) return false; return Event.current.rawType == EventType.MouseUp; }
        public static bool IsMouseDragging() { if (Event.current == null) return false; return Event.current.rawType == EventType.MouseDrag; }

        public static string ColorString(string text, string color)
        {
            if (string.IsNullOrEmpty(text)) return null;
            return "<color=" + color + ">" + text + "</color>";
        }

        [HideInCallstack]
        private static string GetLogString(string message, string color = null)
        {
            string tag = "<size=10>" + debugTag + "</size>";
            if (!string.IsNullOrEmpty(color)) tag = ColorString(tag, color);
            return string.Join(' ', tag, message) + "\n<size=10>(Disable these messages with the debug toolbar button)</size>";
        }


        /// <summary>
        /// Print a log message to the console, intended for debug messages.
        /// </summary>
        [HideInCallstack] public static void Log(string message, Object context, string color) => Debug.LogFormat(LogType.Log, LogOption.NoStacktrace, context, "{0}", GetLogString(message, color));
        [HideInCallstack] public static void Log(string message, Object context) => Log(message, context, null);
        [HideInCallstack] public static void Log(string message, string color) => Log(message, null, color);
        [HideInCallstack] public static void Log(string message) => Log(message, null, null);

        /// <summary>
        /// Print a warning message to the console.
        /// </summary>
        [HideInCallstack] public static void LogWarning(string message, Object context, string color) => Debug.LogWarning(GetLogString(message, color), context);
        [HideInCallstack] public static void LogWarning(string message, Object context) => LogWarning(message, context, null);
        [HideInCallstack] public static void LogWarning(string message, string color) => LogWarning(message, null, color);
        [HideInCallstack] public static void LogWarning(string message) => LogWarning(message, null, null);


        /// <summary>
        /// Print a warning message to the console.
        /// </summary>
        [HideInCallstack] public static void LogError(string message, Object context, string color) => Debug.LogError(GetLogString(message, color), context);
        [HideInCallstack] public static void LogError(string message, Object context) => LogError(message, context, null);
        [HideInCallstack] public static void LogError(string message, string color) => LogError(message, null, color);
        [HideInCallstack] public static void LogError(string message) => LogError(message, null, null);

        /// <summary>
        /// Print an exception to the console. The color cannot be changed. To ensure that an exception is logged only a single time
        /// per assembly reload, set "once" to true. Only the exception's message is checked when "once" is true.
        /// </summary>
        [HideInCallstack] public static void LogException(System.Exception exception, Object context, bool once = false)
        {
            if (once && loggedExceptions.Contains(exception.Message)) return;
            Debug.LogException(exception, context);
            loggedExceptions.Add(exception.Message);
        }
        [HideInCallstack] public static void LogException(System.Exception exception, bool once = false)
        {
            if (once && loggedExceptions.Contains(exception.Message)) return;
            Debug.LogException(exception);
            loggedExceptions.Add(exception.Message);
        }
        


        /// <summary>
        /// Signifies that a path is not located in either the "Assets" or "Packages" folder of a project.
        /// </summary>
        public class InvalidUnityPath : System.Exception
        {
            public InvalidUnityPath() { }
            public InvalidUnityPath(string message) : base(message) { }
            public InvalidUnityPath(string message, System.Exception inner) : base(message, inner) { }
        }

        /// <summary>
        /// Raised when SearchForAsset fails to locate an asset given its criteria function.
        /// </summary>
        public class AssetNotFound : System.Exception
        {
            public AssetNotFound() { }
            public AssetNotFound(string message) : base(message) { }
            public AssetNotFound(string message, System.Exception inner) : base(message, inner) { }
        }
    }
}