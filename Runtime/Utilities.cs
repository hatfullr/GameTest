using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;


namespace GameTest
{
    public static class Utilities
    {
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
        /// Location of the folder where data gets stored by default. The user can change the data path in the UI, so TestManagerUI.GetDataPath
        /// should likely be used instead.
        /// </summary>
        public static string defaultDataPath = Path.Join(assetsPath, "GameTest");

        /// <summary>
        /// True if the editor is using the theme called "DarkSkin". Otherwise, false.
        /// </summary>
        public static bool isDarkTheme = true;

        /// <summary>
        /// HTML color green. Adapts to DarkSkin and LightSkin.
        /// </summary>
        public static string green { get { if (isDarkTheme) return "#50C878"; return "#00802b"; } }
        /// <summary>
        /// HTML color red. Adapts to DarkSkin and LightSkin.
        /// </summary>
        public static string red { get { if (isDarkTheme) return "red"; return "red"; } }
        /// <summary>
        /// HTML color yellow. Adapts to DarkSkin and LightSkin.
        /// </summary>
        public static string yellow { get { if (isDarkTheme) return "yellow"; return "#806600"; } }

        public static float searchBarMinWidth = 80f;
        public static float searchBarMaxWidth = 300f;

        #region UI Helpers
#if UNITY_EDITOR
        public enum  RectAlignment
        {
            LowerLeft,
            MiddleLeft,
            UpperLeft,
            UpperCenter,
            UpperRight,
            MiddleRight,
            LowerRight,
            LowerCenter,
            MiddleCenter,
        }

        /// <summary>
        /// Returns a new Rect the same size as toAlign, positioned relative to relativeTo according to alignment.
        /// </summary>
        public static Rect AlignRect(Rect toAlign, Rect relativeTo, RectAlignment alignment, RectOffset padding = null)
        {
            Rect rect = new Rect(toAlign);
            if (padding == null) padding = new RectOffset(0, 0, 0, 0);

            if (alignment == RectAlignment.LowerLeft || alignment == RectAlignment.MiddleLeft || alignment == RectAlignment.UpperLeft)
            {
                rect.x = relativeTo.x + padding.left;
            }
            else if (alignment == RectAlignment.LowerCenter || alignment == RectAlignment.MiddleCenter || alignment == RectAlignment.UpperCenter)
            {
                rect.x = relativeTo.center.x - 0.5f * rect.width;
            }
            else if (alignment == RectAlignment.LowerRight || alignment == RectAlignment.MiddleRight || alignment == RectAlignment.UpperRight)
            {
                rect.x = relativeTo.xMax - rect.width - padding.right;
            }


            if (alignment == RectAlignment.LowerLeft || alignment == RectAlignment.LowerCenter || alignment == RectAlignment.LowerRight)
            {
                rect.y = relativeTo.yMax - rect.height - padding.bottom;
            }
            else if (alignment == RectAlignment.MiddleLeft || alignment == RectAlignment.MiddleCenter || alignment == RectAlignment.MiddleRight)
            {
                rect.y = relativeTo.center.y - 0.5f * rect.height;
            }
            else if (alignment == RectAlignment.UpperLeft || alignment == RectAlignment.UpperCenter || alignment == RectAlignment.UpperRight)
            {
                rect.y = relativeTo.y + padding.top;
            }

            return rect;
        }

        /// <summary>
        /// Align many Rects. Each given Rect is "stacked" such that its edge or corner touches the next Rect in the array at the given stacking order.
        /// If a padding is given, each Rect will be positioned in a way that respects the padding.
        /// </summary>
        public static Rect[] AlignRects(
            Rect[] toAlign, Rect relativeTo, RectAlignment alignment, RectAlignment stacking,
            RectOffset[] padding = null
        )
        {
            Rect[] ret = new Rect[toAlign.Length];
            Rect rel = new Rect(relativeTo);
            if (padding == null)
            {
                padding = new RectOffset[toAlign.Length];
                for (int i = 0; i < toAlign.Length; i++) padding[i] = null;
            }

            for (int i = 0; i < ret.Length; i++)
            {
                ret[i] = AlignRect(toAlign[i], rel, alignment, padding[i]);
                if (stacking == RectAlignment.LowerLeft || stacking == RectAlignment.MiddleLeft || stacking == RectAlignment.UpperLeft)
                {
                    rel.x -= ret[i].width;
                }
                else if (stacking == RectAlignment.LowerRight || stacking == RectAlignment.MiddleRight || stacking == RectAlignment.UpperRight)
                {
                    rel.x += ret[i].width;
                }
                if (stacking == RectAlignment.LowerLeft || stacking == RectAlignment.LowerCenter || stacking == RectAlignment.LowerRight)
                {
                    rel.y -= ret[i].height;
                }
                else if (stacking == RectAlignment.UpperLeft || stacking == RectAlignment.UpperCenter || stacking == RectAlignment.UpperRight)
                {
                    rel.y += ret[i].height;
                }
            }
            return ret;
        }

        public static Rect GetPaddedRect(Rect rect, RectOffset padding)
        {
            return new Rect(
                rect.x + padding.left,
                rect.y + padding.top,
                rect.width - padding.horizontal,
                rect.height - padding.vertical
            );
        }

        public static void DrawDebugOutline(Rect rect, Color color)
        {
            Rect left = new Rect(rect.xMin, rect.y, 1f, rect.height);
            Rect right = new Rect(rect.xMax - 1f, rect.y, 1f, rect.height);
            Rect top = new Rect(rect.x, rect.yMin, rect.width, 1f);
            Rect bottom = new Rect(rect.x, rect.yMax - 1f, rect.width, 1f);
            foreach (Rect r in new Rect[] { left, right, top, bottom })
            {
                EditorGUI.DrawRect(r, color);
            }
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
        /// Returns true if the mouse is currently hovering over the given Rect, and false otherwise.
        /// </summary>
        public static bool IsMouseOverRect(Rect rect)
        {
            if (Event.current != null) return rect.Contains(Event.current.mousePosition);
            return false;
        }

        /// <summary>
        /// When the mouse cursor is hovering over the given rect, the mouse cursor will change to the type specified.
        /// </summary>
        public static void SetCursorInRect(Rect rect, MouseCursor cursor) => EditorGUIUtility.AddCursorRect(rect, cursor);

        public static bool IsMouseButtonPressed() { if (Event.current == null) return false; return Event.current.rawType == EventType.MouseDown; }
        public static bool IsMouseButtonReleased() { if (Event.current == null) return false; return Event.current.rawType == EventType.MouseUp; }
        public static bool IsMouseDragging() { if (Event.current == null) return false; return Event.current.rawType == EventType.MouseDrag; }
#endif
        #endregion

        #region Filesystem Helpers
#if UNITY_EDITOR
        /// <summary>
        /// Loop through all *.asset files in Assets/GameTest/Data to find the first asset that meets the given criteria function. The
        /// input parameter to the criteria function is any Object. The result of the criteria function must be a bool.
        /// </summary>
        public static T SearchForAsset<T>(System.Func<T, bool> criteria, string searchDirectory, bool errorOnMissing = true)
        {
            foreach (string path in Directory.GetFiles(searchDirectory, "*", SearchOption.TopDirectoryOnly))
            {
                T asset = LoadAssetAtPath<T>(GetUnityPath(path));
                if (asset == null) continue; // No asset of the given type exists at the given path
                if (criteria(asset)) return asset;
            }
            if (errorOnMissing) throw new AssetNotFound("type = '" + typeof(T) + "', criteria = '" + criteria + "'");
            return (T)(object)null;
        }

        /// <summary>
        /// Loop through all *.asset files in Assets/GameTest/Data to find the first asset that meets the given criteria function. The
        /// input parameter to the criteria function is any Object. The result of the criteria function must be a bool.
        /// </summary>
        public static T LoadAssetAtPath<T>(string assetPath) => (T)(object)AssetDatabase.LoadAssetAtPath(GetUnityPath(assetPath), typeof(T));
        

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
                if (!AssetDatabase.IsValidFolder(dir)) AssetDatabase.CreateFolder(parent, newFolderName);
            }

            // Create the final directory at the destination path
            parent = Path.GetDirectoryName(directory);
            newFolderName = Path.GetFileName(directory);
            if (!AssetDatabase.IsValidFolder(directory)) AssetDatabase.CreateFolder(parent, newFolderName);

            return directory;
        }

        /// <summary>
        /// Get the file path to Assets/GameTest/Data/[name].asset.
        /// </summary>
        public static string GetAssetPath(string name, string directory = null)
        {
            if (directory == null) directory = defaultDataPath;
            string path = Path.Join(directory, name);
            if (Path.GetExtension(path) != ".asset") path = Path.ChangeExtension(path, ".asset");
            return GetUnityPath(path);
        }
#endif

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

            string basename = Path.GetFileName(path);
            if (basename == "Assets" || basename == "Packages")
            {
                if (Path.GetFullPath(Path.GetDirectoryName(path)) == Path.GetFullPath(projectPath)) return basename;
            }

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
#endregion

        #region Exceptions
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
        #endregion
    }
}