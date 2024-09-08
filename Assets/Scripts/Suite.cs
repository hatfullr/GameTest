using System.Linq;
using UnityEditor;
#if UNITY_EDITOR
using UnityEngine;
#endif

namespace UnityTest
{
    public class Suite : ScriptableObject
    {
        public static string GetDirectory()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:script"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith("/Suite.cs")) return string.Join("/", path.Split("/").SkipLast(1));
            }
            throw new System.Exception("Failed to find directory of the Suite script");
        }

        public static string GetAssetPath(System.Type type) => string.Join("/", new string[] { GetDirectory(), type.Name + ".asset" });

        /// <summary>
        /// Obtain the Suite asset, or create a new one if there isn't one yet. The asset is saved in the directory where the Suite 
        /// script is located.
        /// </summary>
        public static Suite Get(System.Type type)
        {
            if (!type.IsSubclassOf(typeof(Suite)))
                throw new SuiteClassException("Unit test suite of type '" + type + "' must inherit from UnityTest.Suite");
            Object suite = AssetDatabase.LoadAssetAtPath(GetAssetPath(type), type);
            if (suite == null) // Create a new asset
            {
                suite = ScriptableObject.CreateInstance(type);
                AssetDatabase.CreateAsset(suite, GetAssetPath(suite.GetType()));
            }
            return suite as Suite;
        }
        //public static T Get<T>() where T : Suite => Get(typeof(T)) as T;
    }

    public class SuiteClassException : System.Exception
    {
        public SuiteClassException() { }
        public SuiteClassException(string message) : base(message) { }
        public SuiteClassException(string message, System.Exception inner) : base(message, inner) { }
    }
}