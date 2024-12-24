using System.IO;

namespace GameTest
{
    /// <summary>
    /// This method will be included in Window > Unit Test Manager. It must return void and accept a GameObject, which has this method's
    /// Component attached. The GameObject is created by the SetUp function. Use UnityEngine.Assert to process tests.
    /// </summary>
    [System.AttributeUsage(System.AttributeTargets.Method), System.Serializable]
    public class TestAttribute : System.Attribute, System.IComparable<TestAttribute>
    {
        /// <summary>
        /// The test method name which appears in the test manager. The default is the name of the method. Names must be unique per-file.
        /// </summary>
        public string name;
        /// <summary>
        /// Name of a static method which returns a GameObject and accepts no parameters.
        /// </summary>
        public string setUp;
        /// <summary>
        /// Name of a static method which returns void and accepts the GameObject returned by SetUp.
        /// </summary>
        public string tearDown;
        /// <summary>
        /// Pause the editor when this test fails. No other subsequent tests will run. default = false.
        /// </summary>
        public bool pauseOnFail;
        /// <summary>
        /// DO NOT MODIFY. This is the path to the source file that this attribute was used in. It is set by a reflection technique.
        /// </summary>
        public string sourceFile;

        /// <summary>
        /// DO NOT MODIFY. This is the line number where this attribute was used in.</param>
        /// </summary>
        public int lineNumber;

        /// <summary>
        /// This method will be added to Window > GameTest Manager.
        /// </summary>
        /// <param name="setUp">Name of a static method which returns a GameObject and accepts no parameters.</param>
        /// <param name="tearDown">Name of a static method which returns void and accepts the GameObject returned by SetUp.</param>
        /// <param name="pauseOnFail">Pause the editor when this test fails. No other subsequent tests will run. default = false.</param>
        /// <param name="name">The test method name which appears in the test manager. The default is the name of the method. Names must be unique per-file.</param>
        /// <param name="sourceFile">DO NOT USE. It is used by reflection techniques to locate the source file that this attribute was used in.</param>
        /// <param name="lineNumber">DO NOT USE. It is used by reflection techniques to locate the line number where this attribute was used in.</param>
        public TestAttribute(
            string setUp,
            string tearDown,
            bool pauseOnFail = false,
            [System.Runtime.CompilerServices.CallerMemberName] string name = default,
            [System.Runtime.CompilerServices.CallerFilePath] string sourceFile = default,
            [System.Runtime.CompilerServices.CallerLineNumber] int lineNumber = default)
        {
            this.pauseOnFail = pauseOnFail;
            this.setUp = setUp;
            this.tearDown = tearDown;
            this.name = name;
            this.sourceFile = Path.GetFullPath(sourceFile);
            this.lineNumber = lineNumber;
        }

        /// <summary>
        /// This method will be added to Window > GameTest Manager.
        /// </summary>
        /// <param name="pauseOnFail">Pause testing when this test fails. No other subsequent tests will run. default = false.</param>
        /// <param name="name">The test method name which appears in the test manager. The default is the name of the method. Names must be unique per-file.</param>
        /// <param name="sourceFile">DO NOT USE. It is used by reflection techniques to locate the source file that this attribute was used in.</param>
        /// <param name="lineNumber">DO NOT USE. It is used by reflection techniques to locate the line number where this attribute was used in.</param>
        public TestAttribute(
            bool pauseOnFail = false,
            [System.Runtime.CompilerServices.CallerMemberName] string name = default,
            [System.Runtime.CompilerServices.CallerFilePath] string sourceFile = default,
            [System.Runtime.CompilerServices.CallerLineNumber] int lineNumber = default)
        {
            setUp = "";
            tearDown = "";
            this.pauseOnFail = pauseOnFail;
            this.name = name;
            this.sourceFile = Path.GetFullPath(sourceFile);
            this.lineNumber = lineNumber;
        }

        /// <summary>
        /// This method will be added to Window > GameTest Manager.
        /// </summary>
        /// <param name="setUp">Name of a static method which returns a GameObject and accepts no parameters.</param>
        /// <param name="pauseOnFail">Pause testing when this test fails. No other subsequent tests will run. default = false.</param>
        /// <param name="name">The test method name which appears in the test manager. The default is the name of the method. Names must be unique per-file.</param>
        /// <param name="sourceFile">DO NOT USE. It is used by reflection techniques to locate the source file that this attribute was used in.</param>
        /// <param name="lineNumber">DO NOT USE. It is used by reflection techniques to locate the line number where this attribute was used in.</param>
        public TestAttribute(
            string setUp,
            bool pauseOnFail = false,
            [System.Runtime.CompilerServices.CallerMemberName] string name = default,
            [System.Runtime.CompilerServices.CallerFilePath] string sourceFile = default,
            [System.Runtime.CompilerServices.CallerLineNumber] int lineNumber = default)
        {
            this.setUp = setUp;
            tearDown = "";
            this.pauseOnFail = pauseOnFail;
            this.name = name;
            this.sourceFile = Path.GetFullPath(sourceFile);
            this.lineNumber = lineNumber;
        }

        public int CompareTo(TestAttribute other)
        {
#if UNITY_EDITOR
            return GetPath().CompareTo(other.GetPath());
#else
            return 0;
#endif
        }

#if UNITY_EDITOR
        /// <summary>
        /// Get the unique identifier for this test.
        /// </summary>
        public string GetPath()
        {
            string path = Utilities.GetUnityPath(sourceFile);
            string directory = Path.Join(Path.GetDirectoryName(path), Path.GetFileNameWithoutExtension(path));
            return Path.Join(directory, name);
        }


        #region Operators
        public override string ToString() => "TestAttribute(" + GetPath() + ")";

        public override bool Equals(object other)
        {
            if (other is null) return false;
            if (GetType() != other.GetType()) return false;
            return this == (other as TestAttribute);
        }

        public override int GetHashCode() => (sourceFile + name + setUp + tearDown + pauseOnFail).GetHashCode();

        public static bool operator ==(TestAttribute left, TestAttribute right) => left.sourceFile == right.sourceFile && left.name == right.name && left.setUp == right.setUp && left.tearDown == right.tearDown && left.pauseOnFail == right.pauseOnFail;
        public static bool operator !=(TestAttribute left, TestAttribute right) => !(left == right);
#endregion
#endif
        }
}