using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace UnityTest
{
    public class Foldout
    {
        /// <summary>
        /// All Test objects in this Foldout will have paths that start with this path.
        /// </summary>
        public string path { get; private set; }

        public bool selected, expanded, locked;

        /// <summary>
        /// true if this Foldout is a Suite, and false otherwise. This is set only after Add() is called with an input test that originates
        /// from a Suite class.
        /// </summary>
        public bool isSuite { get; private set; } = false;

        public List<Test> tests { get; private set; } = new List<Test>();

        /// <summary>
        /// Create a new Foldout located at the given path
        /// </summary>
        /// <param name="path"></param>
        public Foldout(string path)
        {
            this.path = path;
        }



        #region UI Methods
        public void Draw()
        {
            // If this is the root foldout
            if (string.IsNullOrEmpty(path)) throw new System.Exception("Cannot call Draw() on the rootFoldout. This should never happen.");

            GUILayout.BeginHorizontal();
            {
                Rect controlRect = EditorGUILayout.GetControlRect(false);

                // Draw the test result indicators
                if (!expanded) controlRect = TestManagerUI.PaintResultFeatures(controlRect, GetTotalResult());

                // Setup the indented area
                Rect indentedRect;
                if (TestManagerUI.Instance.indentWidth == 0f)
                {
                    EditorGUI.indentLevel++;
                    indentedRect = EditorGUI.IndentedRect(controlRect);
                    EditorGUI.indentLevel--;
                    TestManagerUI.Instance.indentWidth = controlRect.width - indentedRect.width;
                }
                else
                {
                    indentedRect = new Rect(controlRect);
                    indentedRect.x += TestManagerUI.Instance.indentWidth * TestManagerUI.Instance.indentLevel;
                    indentedRect.width -= TestManagerUI.Instance.indentWidth * TestManagerUI.Instance.indentLevel;
                }

                // Draw the Foldout control
                bool wasExpanded = expanded;
                expanded = EditorGUI.Foldout(indentedRect, expanded, string.Empty, Style.Get("Foldout"));

                // Check if the user just expanded the Foldout while holding down the Alt key
                if (Event.current.alt && expanded != wasExpanded) ExpandAll(expanded);

                // scan to the right by the toggle width to give space to the Foldout control
                indentedRect.x += Style.GetWidth("Test/Foldout");
                indentedRect.width -= Style.GetWidth("Test/Foldout");

                bool drawSuite = isSuite && tests.Count > 0;

                // If we're going to draw a suite, then we need to draw the settings cog and the object reference on the right side,
                // so room is made for that here. We finish drawing those controls later, after drawing the toggle.
                if (drawSuite) indentedRect.width -= Style.TestManagerUI.scriptWidth + Style.GetWidth("Test/Suite/SettingsButton");

                // We need to separate out the user's actual actions from the button's state
                // The toggle is only "selected" when it is not mixed. If it is mixed, then selected = false.

                selected = IsAllSelected();

                bool isMixed = IsMixed();

                List<bool> results = TestManagerUI.DrawToggle(indentedRect, GetName(), selected, locked, true, isMixed);

                // The logic here is confusing. It is the simplest I could make it with the tools Unity gave me
                if (results[0] != selected)
                {
                    // mixed is the same as the toggle not being selected
                    if (selected && isMixed) selected = false; // if the toggle was selected, but now it is mixed, it's because the user deselected a child Test
                    else
                    {
                        // We only get into this logic if the user has clicked on the foldout toggle
                        if (isMixed) Select();
                        else if (!selected) Select();
                        else Deselect();
                    }
                }

                if (results[1] != locked)
                {
                    if (results[1] && !locked) Lock();
                    else if (!results[1] && locked) Unlock();
                }
                else locked = IsAllLocked();


                if (drawSuite) // Finish drawing the suite
                {
                    indentedRect.x += indentedRect.width;
                    indentedRect.width = Style.GetWidth("Test/Suite/SettingsButton");

                    if (GUI.Button(indentedRect, Style.GetIcon("Test/Suite/SettingsButton"), Style.Get("Test/Suite/SettingsButton")))
                    {
                        TestManagerUI.Instance.settings.SetTest(tests[0]);
                        TestManagerUI.Instance.settings.SetVisible(true);
                    }

                    indentedRect.x += indentedRect.width;
                    indentedRect.width = Style.TestManagerUI.scriptWidth;
                    bool wasEnabled = GUI.enabled;
                    GUI.enabled = false;
                    EditorGUI.ObjectField(indentedRect, GUIContent.none, tests[0].GetScript(), tests[0].method.DeclaringType, false);
                    GUI.enabled = wasEnabled;
                }
            }
            GUILayout.EndHorizontal();

            if (expanded)
            {
                TestManagerUI.Instance.indentLevel++;
                DrawChildren();
                TestManagerUI.Instance.indentLevel--;
            }
        }
        private void DrawChildren()
        {
            float indent = TestManagerUI.Instance.indentLevel * TestManagerUI.Instance.indentWidth;
            foreach (Test test in tests)
            {
                Rect rect = EditorGUILayout.GetControlRect(false);
                rect = TestManagerUI.PaintResultFeatures(rect, test);
                rect.x += indent;
                rect.width -= indent;
                TestManagerUI.DrawTest(rect, test, true, !test.IsInSuite(), true);
            }
            List<Foldout> children = new List<Foldout>(GetChildren(false).OrderBy(x => x.GetName()));
            if (children.Count > 0 && tests.Count > 0) EditorGUILayout.Space(0.5f * EditorGUIUtility.singleLineHeight);

            // Draw the child foldouts, leaving the Examples foldout for last.
            foreach (Foldout child in children) child.Draw();
        }
        #endregion

        #region Controls
        /// <summary>
        /// Toggle on the Foldout, and thereby all its children. Does not affect locked items.
        /// </summary>
        public void Select()
        {
            selected = true;
            foreach (Test test in tests)
                if (!test.locked) test.selected = selected;
            foreach (Foldout foldout in GetChildren()) foldout.Select();
        }
        /// <summary>
        /// Toggle off the Foldout, and thereby all its children.
        /// </summary>
        public void Deselect()
        {
            selected = false;
            foreach (Test test in tests)
                if (!test.locked) test.selected = selected;
            foreach (Foldout foldout in GetChildren()) foldout.Deselect();
        }
        /// <summary>
        /// Lock the Foldout, and thereby all its children.
        /// </summary>
        public void Lock()
        {
            locked = true;
            foreach (Test test in tests) test.locked = locked;
            foreach (Foldout foldout in GetChildren()) foldout.Lock();
        }
        /// <summary>
        /// Unlock the Foldout, and thereby all its children.
        /// </summary>
        public void Unlock()
        {
            locked = false;
            foreach (Test test in tests) test.locked = locked;
            foreach (Foldout foldout in GetChildren()) foldout.Unlock();
        }
        /// <summary>
        /// When the user is holding Alt and clicks on the foldout arrow, expand also all child foldouts and Tests
        /// </summary>
        public void ExpandAll(bool value)
        {
            expanded = value;
            foreach (Test test in GetTests()) test.expanded = value;
            foreach (Foldout child in GetChildren()) child.ExpandAll(value);
        }
        #endregion

        #region Test Object Handling
        /// <summary>
        /// Returns true if the given Test is located within this Foldout, and false otherwise.
        /// </summary>
        public bool Contains(Test test, bool includeSubdirectories = false)
        {
            foreach (Test other in GetTests(includeSubdirectories))
                if (test.attribute == other.attribute) return true;
            return false;
        }
        #endregion

        #region Tree Methods
        /// <summary>
        /// Locate all the Foldout objects that are children of this Foldout.
        /// </summary>
        public IEnumerable<Foldout> GetChildren(bool includeSubdirectories = true)
        {
            if (includeSubdirectories)
            {
                foreach (Foldout foldout in TestManagerUI.Instance.foldouts)
                    if (IsParentOf(foldout)) yield return foldout;
            }
            else
            {
                bool isRoot = string.IsNullOrEmpty(path);
                foreach (Foldout foldout in TestManagerUI.Instance.foldouts)
                {
                    if (isRoot && string.IsNullOrEmpty(foldout.path)) continue; // Don't return the rootFoldout on itself
                    Debug.Log(foldout.path);
                    string dirname = Path.GetDirectoryName(foldout.path);
                    if (isRoot && string.IsNullOrEmpty(dirname)) yield return foldout;
                    else if (Path.GetDirectoryName(foldout.path) == path) yield return foldout;
                }
            }
        }

        /// <summary>
        /// Locate all the Test objects included in this Foldout.
        /// </summary>
        public IEnumerable<Test> GetTests(bool includeSubdirectories = true)
        {
            if (tests != null)
            {
                foreach (Test test in tests) yield return test;
                foreach (Foldout child in GetChildren(includeSubdirectories))
                    foreach (Test test in child.tests) yield return test;
            }
        }

        /// <summary>
        /// Return the cumulative testing result from all child tests
        /// </summary>
        public Test.Result GetTotalResult()
        {
            bool anyPassed = false;
            foreach (Test test in GetTests())
            {
                // If any children are Fail, we are Fail
                if (test.result == Test.Result.Fail) return Test.Result.Fail;
                anyPassed |= test.result == Test.Result.Pass;
            }
            if (anyPassed) return Test.Result.Pass;
            return Test.Result.None; // inconclusive
        }

        /// <summary>
        /// Return the Foldout that is holding the given Test. Foldouts are like folders, which can contain other Foldouts. This method
        /// returns the "folder" that the given Test is in. Returns null if the given Test wasn't found in any Foldout.
        /// </summary>
        public static Foldout GetFoldoutFromTest(Test test)
        {
            foreach (Foldout foldout in TestManagerUI.Instance.foldouts)
            {
                if (foldout.Contains(test)) return foldout;
            }
            return null;
        }
        #endregion

        #region Operators
        public override string ToString() => "Foldout(" + path + ")";
        #endregion

        #region Properties Methods
        /// <summary>
        /// Returns true if the given Foldout already exists.
        /// </summary>
        public static bool Exists(Foldout foldout)
        {
            foreach (Foldout f in TestManagerUI.Instance.foldouts)
                if (foldout.path == f.path) return true;
            return false;
        }
        /// <summary>
        /// Returns true if the given path corresponds to a currently-existing Foldout
        /// </summary>
        public static bool ExistsAtPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return true; // root foldout always exists
            foreach (Foldout foldout in TestManagerUI.Instance.foldouts)
                if (foldout.path == path) return true;
            return false;
        }
        /// <summary>
        /// Returns true if the other Foldout contains this Foldout in any of its subdirectories, and false otherwise.
        /// </summary>
        public bool IsChildOf(Foldout other) => Utilities.IsPathChild(other.path, path);

        /// <summary>
        /// Returns true if this Foldout contains the other Foldout in any subdirectory, and false otherwise.
        /// </summary>
        public bool IsParentOf(Foldout other) => other.IsChildOf(this);

        /// <summary>
        /// Returns true if this Foldout contains any tests from Packages/UnityTest/Runtime/ExampleTests.cs
        /// </summary>
        public bool IsExamples()
        {
            foreach (Test test in tests)
                if (test.attribute.sourceFile == Utilities.exampleTestsFile) return true;
            return false;
        }
        /// <summary>
        /// Returns true if more than one of this Foldout's tests is selected, but not all of them. The tests that are checked are
        /// all of those in every subdirectory.
        /// </summary>
        public bool IsMixed()
        {
            int nSelected = 0;
            List<Test> tests = new List<Test>(GetTests());
            foreach (Test test in tests)
            {
                if (test.selected) nSelected++;
            }
            return nSelected != 0 && nSelected != tests.Count;
        }
        /// <summary>
        /// Returns true if all tests and child Foldouts are selected, in every subdirectory.
        /// </summary>
        public bool IsAllSelected()
        {
            foreach (Test test in tests)
                if (!test.selected) return false;
            foreach (Foldout child in GetChildren())
                if (!child.selected) return false;
            return true;
        }
        /// <summary>
        /// Returns true if all tests and child Foldouts are locked, in every subdirectory.
        /// </summary>
        public bool IsAllLocked()
        {
            foreach (Test test in tests)
                if (!test.locked) return false;
            foreach (Foldout child in GetChildren())
                if (!child.locked) return false;
            return true;
        }
        /// <summary>
        /// Return the last element in this Foldout's path (the "file name").
        /// </summary>
        public string GetName() => Path.GetFileName(path);
        #endregion


        /// <summary>
        /// Return an existing Foldout located at the given path.
        /// </summary>
        /// <param name="path"></param>
        /// <exception cref="FoldoutNotFoundException"></exception>
        public static Foldout GetAtPath(string path)
        {
            foreach (Foldout foldout in TestManagerUI.Instance.foldouts)
                if (path == foldout.path) return foldout;
            throw new FoldoutNotFoundException(path);
        }

        
        #region Persistence Methods
        public string GetString()
        {
            return string.Join('\n',
                path,
                selected,
                expanded,
                locked
            );
        }

        public static Foldout FromString(string data)
        {
            Foldout result = new Foldout("");

            string[] s = data.Split('\n');
            try { result.path = s[0]; }
            catch (System.IndexOutOfRangeException) { return null; }

            try { result.selected = bool.Parse(s[1]); }
            catch (System.Exception e) { if (!(e is System.FormatException || e is System.IndexOutOfRangeException)) throw e; }

            try { result.expanded = bool.Parse(s[2]); }
            catch (System.Exception e) { if (!(e is System.FormatException || e is System.IndexOutOfRangeException)) throw e; }

            try { result.locked = bool.Parse(s[3]); }
            catch (System.Exception e) { if (!(e is System.FormatException || e is System.IndexOutOfRangeException)) throw e; }

            return result;
        }

        public void CopyFrom(Foldout other)
        {
            path = other.path;
            selected = other.selected;
            expanded = other.expanded;
            locked = other.locked;
        }
        #endregion
        

        public class FoldoutNotFoundException : System.Exception
        {
            public FoldoutNotFoundException() { }
            public FoldoutNotFoundException(string message) : base(message) { }
            public FoldoutNotFoundException(string message, System.Exception inner) : base(message, inner) { }
        }
    }
}
