using System.IO;

namespace UnityTest
{
    /// <summary>
    /// A suite of unit tests that can be used as an attribute for individual classes to define a series of tests. Each method
    /// in such a class behaves as though they had the Test attribute attached to them, except for the special method names
    /// "SetUp" and "TearDown". The names of each method will appear as a toggle in the Unit Test Manager.
    /// 
    /// If SetUp is present, it is called before each method is executed. If TearDown is present (must have SetUp present), it
    /// is called after each method is executed.
    /// </summary>
    [System.AttributeUsage(System.AttributeTargets.Class), System.Serializable, System.Obsolete("Testing suites are not yet supported")]
    public class SuiteAttribute : System.Attribute, System.IComparable<SuiteAttribute>
    {
        /// <summary>
        /// The test suite name which appears in the test manager. The default is the name of the suite class. Names must be unique per-file.
        /// </summary>
        public string name { get; private set; }

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
        /// <param name="pauseOnFail">Pause the editor when this test fails. No other subsequent tests will run. default = false.</param>
        /// <param name="name">The test suite name which appears in the test manager. The default is the name of the suite class. Names must be unique per-file.</param>
        /// <param name="sourceFile">DO NOT USE. It is used by reflection techniques to locate the source file that this attribute was used in.</param>
        public SuiteAttribute(
            bool pauseOnFail = false,
            [System.Runtime.CompilerServices.CallerMemberName] string name = default,
            [System.Runtime.CompilerServices.CallerFilePath] string sourceFile = default)
        {
            this.pauseOnFail = pauseOnFail;
            this.name = name;
            this.sourceFile = sourceFile;
        }

        /// <summary>
        /// Get the unique identifier for this test suite.
        /// </summary>
        public string GetPath()
        {
            string path = Utilities.GetUnityPath(sourceFile);
            string fileName = Path.GetFileNameWithoutExtension(path);
            return Path.Join(Path.GetDirectoryName(path), fileName, name);
        }

        #region Operators
        public int CompareTo(SuiteAttribute other) => GetPath().CompareTo(other.GetPath());

        public override bool Equals(object other)
        {
            if (other is null) return false;
            if (GetType() != other.GetType()) return false;
            return this == (other as SuiteAttribute);
        }
        public override int GetHashCode() => (sourceFile + name + pauseOnFail).GetHashCode();

        public static bool operator ==(SuiteAttribute left, SuiteAttribute right) => left.sourceFile == right.sourceFile && left.name == right.name && left.pauseOnFail == right.pauseOnFail;
        public static bool operator !=(SuiteAttribute left, SuiteAttribute right) => !(left == right);
        #endregion
    }
}