using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;


namespace UnityTest
{
    public static class Utilities
    {
        public const string debugTag = "[UnityTest]";

        public const string editorPrefs = "UnityTest";
        public const string guidPrefs = "UnityTest/GUIDs";
        public const char guidDelimiter = '\n';
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
        /// The directory in "Packages/UnityTest".
        /// </summary>
        public static string rootPath { get; } = Path.Join(packagesPath, "UnityTest");

        /// <summary>
        /// Location of the "Packages/UnityTest/Runtime" folder.
        /// </summary>
        public static string runtimePath { get; } = Path.Join(rootPath, "Runtime");

        /// <summary>
        /// Location of the "Packages/UnityTest/Runtime/Data" folder.
        /// </summary>
        public static string dataPath { get; } = EnsureDirectoryExists(Path.Join(runtimePath, "Data"));

        /// <summary>
        /// The file located at "Packages/UnityTest/Runtime/ExampleTests.cs"
        /// </summary>
        public static string exampleTestsFile { get; } = Path.Join(runtimePath, "ExampleTests.cs");

        /// <summary>
        /// Location of the "Library/PackageCache" folder, which is sometimes used by Unity for packages.
        /// </summary>
        public static string packageCachePath { get; } = Path.Join(projectPath, "Library", "PackageCache");

        /// <summary>
        /// The location of the UnityTest package. If UnityTest was installed using the Package Manager, then this path will
        /// be in Library/PackageCache/.../unitytest. Otherwise, it is likely to be Packages/UnityTest
        /// </summary>
        public static string packageRootPath
        {
            get
            {
                string Get([System.Runtime.CompilerServices.CallerFilePath] string path = null) => path;
                string runtimePath = Path.GetDirectoryName(Get());
                return Path.GetDirectoryName(runtimePath);
            }
        }

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
        /// Create directories so that the given directory path exists. Returns the given directory path.
        /// </summary>
        private static string EnsureDirectoryExists(string directory)
        {
            Debug.Log("Ensuring directory exists: " + directory);
            if (Directory.Exists(directory)) return directory;
            Debug.Log("Directory didn't exist");

            if (IsPathChild(assetsPath, directory) || IsPathChild(packagesPath, directory))
            {
                Debug.Log("path is child of assetsPath or packagesPath");
                directory = GetUnityPath(directory);
                AssetDatabase.CreateFolder(Path.GetDirectoryName(directory), Path.GetFileName(directory));
                return directory;
            }

            Debug.Log("Doing default bad directory creation");
            Directory.CreateDirectory(directory);
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
            if (IsPathChild(packageCachePath, path))
            {
                // Return an equivalent path, but in Packages/ instead of Library/PackageCache
                if (IsPathChild(packageRootPath, path))
                {
                    return Path.Join(
                        Path.GetFileName(packagesPath),
                        Path.GetRelativePath(packageRootPath, path)
                    );
                }
                // The path was not in Library/PackageCache/.../unitytest, so we cannot make an equivalent path.
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
        public static void SetCursorInRect(Rect rect, MouseCursor cursor) => EditorGUIUtility.AddCursorRect(rect, cursor);

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
            return string.Join(' ', tag, message);
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
        /// Print an exception to the console. The color cannot be changed.
        /// </summary>
        [HideInCallstack] public static void LogException(System.Exception exception, Object context) => Debug.LogException(exception, context);
        [HideInCallstack] public static void LogException(System.Exception exception) => Debug.LogException(exception);
        


        /// <summary>
        /// Signifies that a path is not located in either the "Assets" or "Packages" folder of a project.
        /// </summary>
        public class InvalidUnityPath : System.Exception
        {
            public InvalidUnityPath() { }
            public InvalidUnityPath(string message) : base(message) { }
            public InvalidUnityPath(string message, System.Exception inner) : base(message, inner) { }
        }
    }
}