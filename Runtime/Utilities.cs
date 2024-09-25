using System.Collections.Generic;
using System.IO;
using UnityEngine;


namespace UnityTest
{
    public static class Utilities
    {
        public const string editorPrefs = "UnityTest";

        /// <summary>
        /// Location of the "Assets" folder
        /// </summary>
        public static string assetsPath { get; } = Path.GetFullPath(Application.dataPath);
        /// <summary>
        /// Location of the "Packages" folder
        /// </summary>
        public static string packagesPath { get; } = Path.GetFullPath(Path.Join(assetsPath, "..", "Packages"));
        /// <summary>
        /// Location of the "Packages/UnityTest/Runtime" folder
        /// </summary>
        public static string runtimeDir { get; } = Path.GetFullPath(Path.Join(packagesPath, "UnityTest", "Runtime"));
        /// <summary>
        /// The file located at "Packages/UnityTest/Runtime/ExampleTests.cs"
        /// </summary>
        public static string exampleTestsFile { get; } = Path.GetFullPath(Path.Join(runtimeDir, "ExampleTests.cs"));

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
            else if (IsPathChild(packagesPath, path)) // it's in the "Packages" folder
            {
                return Path.GetRelativePath(Path.GetDirectoryName(packagesPath), path);
            }

            throw new InvalidUnityPath(path);
        }



        /// <summary>
        /// Returns true if the given child path is located in any subdirectory of parent, or if it is located in parent itself.
        /// Returns false if the parent and child paths are the same, or if the child is not located within the parent.
        /// </summary>
        public static bool IsPathChild(string parent, string child)
        {
            // NullOrEmpty == root directory
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