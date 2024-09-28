using System.Linq;
using UnityEditor;
#if UNITY_EDITOR
using UnityEngine;
#endif

namespace UnityTest
{
    public class Suite : ScriptableObject
    {
        public static string GetAssetPath(System.Type type) => string.Join("/", Utilities.dataPath, type.Name + ".asset");

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
    }

    public class SuiteClassException : System.Exception
    {
        public SuiteClassException() { }
        public SuiteClassException(string message) : base(message) { }
        public SuiteClassException(string message, System.Exception inner) : base(message, inner) { }
    }
}