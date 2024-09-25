using System.Diagnostics;
using System.IO;

namespace UnityTest
{
    /// <summary>
    /// This method will be included in Window > Unit Test Manager. It must return void and accept a GameObject, which has this method's
    /// Component attached. The GameObject is created by the SetUp function. Use UnityEngine.Assert to process tests.
    /// </summary>
    [System.AttributeUsage(System.AttributeTargets.Method)]
    public class TestAttribute : System.Attribute, System.IEquatable<TestAttribute>, System.IComparable<TestAttribute>
    {
        public const string delimiter = "\n**..-- TestAttributeDelimiter --..**\n";

        /// <summary>
        /// The test method name which appears in the test manager. The default is the name of the method. Names must be unique per-file.
        /// </summary>
        public string name { get; private set; }

        /// <summary>
        /// Name of a static method which returns a GameObject and accepts no parameters.
        /// </summary>
        public string setUp { get; private set; }

        /// <summary>
        /// Name of a static method which returns void and accepts the GameObject returned by SetUp.
        /// </summary>
        public string tearDown { get; private set; }

        /// <summary>
        /// Pause the editor when this test fails. No other subsequent tests will run. default = false.
        /// </summary>
        public bool pauseOnFail { get; private set; }

        /// <summary>
        /// DO NOT MODIFY. This is the path to the source file that this attribute was used in. It is set by a reflection technique.
        /// </summary>
        public string sourceFile { get; private set; }

        /// <summary>
        /// This method will be added to Window > Unit Test Manager based on its path. Use '/' to create nested toggles.
        /// </summary>
        /// <param name="setUp">Name of a static method which returns a GameObject and accepts no parameters.</param>
        /// <param name="tearDown">Name of a static method which returns void and accepts the GameObject returned by SetUp.</param>
        /// <param name="pauseOnFail">Pause the editor when this test fails. No other subsequent tests will run. default = false.</param>
        /// <param name="name">The test method name which appears in the test manager. The default is the name of the method. Names must be unique per-file.</param>
        /// <param name="sourceFile">DO NOT USE. It is used by reflection techniques to locate the source file that this attribute was used in.</param>
        public TestAttribute(
            string setUp,
            string tearDown,
            bool pauseOnFail = false,
            [System.Runtime.CompilerServices.CallerMemberName] string name = default,
            [System.Runtime.CompilerServices.CallerFilePath] string sourceFile = default)
        {
            this.pauseOnFail = pauseOnFail;
            this.setUp = setUp;
            this.tearDown = tearDown;
            this.name = name;
            this.sourceFile = Path.GetFullPath(sourceFile);
        }

        /// <summary>
        /// This method will be added to Window > Unit Test Manager based on its path. Use '/' to create nested toggles.
        /// </summary>
        /// <param name="path">A unique identifier for this test. Each '/' determines the depth in Window > Unit Test Manager.</param>
        /// <param name="name">The test method name which appears in the test manager. The default is the name of the method. Names must be unique per-file.</param>
        /// <param name="sourceFile">DO NOT USE. It is used by reflection techniques to locate the source file that this attribute was used in.</param>
        public TestAttribute(
            bool pauseOnFail = false,
            [System.Runtime.CompilerServices.CallerMemberName] string name = default,
            [System.Runtime.CompilerServices.CallerFilePath] string sourceFile = default)
        {
            setUp = "";
            tearDown = "";
            this.pauseOnFail = pauseOnFail;
            this.name = name;
            this.sourceFile = Path.GetFullPath(sourceFile);
        }

        /// <summary>
        /// This method will be added to Window > Unit Test Manager based on its path. Use '/' to create nested toggles.
        /// </summary>
        /// <param name="path"><summary>A unique identifier for this test. Each '/' determines the depth in Window > Unit Test Manager.</summary></param>
        /// <param name="setUp">Name of a static method which returns a GameObject and accepts no parameters.</param>
        /// <param name="pauseOnFail">Pause the editor when this test fails. No other subsequent tests will run. default = false.</param>
        /// <param name="name">The test method name which appears in the test manager. The default is the name of the method. Names must be unique per-file.</param>
        /// <param name="sourceFile">DO NOT USE. It is used by reflection techniques to locate the source file that this attribute was used in.</param>
        public TestAttribute(
            string setUp,
            bool pauseOnFail = false,
            [System.Runtime.CompilerServices.CallerMemberName] string name = default,
            [System.Runtime.CompilerServices.CallerFilePath] string sourceFile = default)
        {
            this.setUp = setUp;
            tearDown = "";
            this.pauseOnFail = pauseOnFail;
            this.name = name;
            this.sourceFile = Path.GetFullPath(sourceFile);
        }

        /// <summary>
        /// Get the unique identifier for this test.
        /// </summary>
        public string GetPath()
        {
            string path = Utilities.GetUnityPath(sourceFile);
            string directory = Path.Join(Path.GetDirectoryName(path), Path.GetFileNameWithoutExtension(path));
            return Path.Join(directory, name);
        }


        public string GetString()
        {
            return string.Join(delimiter,
                setUp,
                tearDown,
                pauseOnFail.ToString(),
                name,
                sourceFile
            );
        }

        public static TestAttribute FromString(string s)
        {
            string[] contents = s.Split(delimiter);
            return new TestAttribute(
                contents[0], // setUp
                contents[1], // tearDown
                bool.Parse(contents[2]), // pauseOnFail
                contents[3], // name
                contents[4] // sourceFile
            );
        }

        /// <summary>
        /// Copy the properties of other into this attribute, overwriting values.
        /// </summary>
        public void UpdateFrom(TestAttribute other)
        {
            setUp = other.setUp;
            tearDown = other.tearDown;
            pauseOnFail = other.pauseOnFail;
            name = other.name;
            sourceFile = other.sourceFile;
        }


        #region Operators
        public override bool Equals(object obj)
        {
            if (obj.GetType() == typeof(TestAttribute)) return Equals(obj as TestAttribute);
            return base.Equals(obj);
        }
        public bool Equals(TestAttribute other) => GetString() == other.GetString();
        public int CompareTo(TestAttribute other) => GetPath().CompareTo(other.GetPath());
        public override int GetHashCode() => System.HashCode.Combine(GetString());
        
        public static bool operator ==(TestAttribute left, TestAttribute right) => Equals(left, right);
        public static bool operator !=(TestAttribute left, TestAttribute right) => !(left == right);
        #endregion
    }
}