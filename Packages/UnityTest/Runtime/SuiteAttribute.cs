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
    [System.AttributeUsage(System.AttributeTargets.Class)]
    public class SuiteAttribute : System.Attribute, System.IEquatable<SuiteAttribute>, System.IComparable<SuiteAttribute>
    {
        public string path { get; private set; }

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
        /// <param name="path">A unique identifier for this test. Each '/' determines the depth in Window > Unit Test Manager.</param>
        /// <param name="pauseOnFail">Pause the editor when this test fails. No other subsequent tests will run. default = false.</param>
        /// <param name="sourceFile">DO NOT USE. It is used by reflection techniques to locate the source file that this attribute was used in.</param>
        public SuiteAttribute(
            string path,
            bool pauseOnFail = false,
            [System.Runtime.CompilerServices.CallerFilePath] string sourceFile = default)
        {
            this.path = path;
            this.pauseOnFail = pauseOnFail;
            this.sourceFile = sourceFile;
        }

        public string GetString()
        {
            string s = path;
            s += TestAttribute.delimiter;
            s += pauseOnFail.ToString();
            s += TestAttribute.delimiter;
            s += sourceFile;
            return s;
        }

        public static SuiteAttribute FromString(string s)
        {
            string[] contents = s.Split(TestAttribute.delimiter);
            string path = contents[0];
            bool pauseOnFail = bool.Parse(contents[1]);
            string sourceFile = contents[2];
            return new SuiteAttribute(path, pauseOnFail, sourceFile);
        }

        /// <summary>
        /// Copy the properties of other into this attribute, overwriting values.
        /// </summary>
        public void UpdateFrom(SuiteAttribute other)
        {
            path = other.path;
            pauseOnFail = other.pauseOnFail;
            sourceFile = other.sourceFile;
        }

        #region Operators
        public override bool Equals(object obj)
        {
            if (obj.GetType() == typeof(SuiteAttribute)) return Equals(obj as SuiteAttribute);
            return base.Equals(obj);
        }
        public bool Equals(SuiteAttribute other) => GetString() == other.GetString();
        public int CompareTo(SuiteAttribute other) => path.CompareTo(other.path);
        public override int GetHashCode() => System.HashCode.Combine(GetString());

        public static bool operator ==(SuiteAttribute left, SuiteAttribute right) => Equals(left, right);
        public static bool operator !=(SuiteAttribute left, SuiteAttribute right) => !(left == right);
        #endregion
    }
}