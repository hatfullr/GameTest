using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace UnityTest
{
    [System.Serializable]
    public class Foldout : ScriptableObject
    {
        /// <summary>
        /// All Test objects in this Foldout will have paths that start with this path.
        /// </summary>
        public string path;
        [SerializeField] private bool selected;
        public bool expanded;
        [SerializeField] private bool locked;

        public List<Test> tests = new List<Test>();

        /// <summary>
        /// Returns true if any tests in this Foldout come from a Suite, and false otherwise.
        /// </summary>
        public bool IsSuite()
        {
            foreach (Test test in tests)
                if (test.isInSuite) return true;
            return false;
        }

        #region UI Methods
        public void Draw(TestManagerUI ui)
        {
            // If this is the root foldout
            if (string.IsNullOrEmpty(path)) throw new System.Exception("Cannot call Draw() on the rootFoldout. This should never happen.");

            bool wasExpanded = expanded;
            bool wasSelected = selected;
            bool wasMixed = IsMixed(ui);
            bool wasLocked = locked;

            Rect rect = EditorGUILayout.BeginVertical();
            {
                Test.Result result = GetTotalResult(ui);

                // This creates a nice visual grouping for the tests to hang out in
                if (expanded && tests.Count > 0)
                {
                    int previousIndentLevel = EditorGUI.indentLevel;
                    EditorGUI.indentLevel = ui.indentLevel + 1;
                    rect = EditorGUI.IndentedRect(rect);
                    EditorGUI.indentLevel = previousIndentLevel;

                    Rect header = new Rect(rect);
                    header.height = EditorGUIUtility.singleLineHeight;

                    GUIStyle style = Style.Get("TestRect");

                    GUI.Box(rect, GUIContent.none, style);
                    GUI.Box(header, GUIContent.none, style);
                }

                selected |= IsAllSelected(ui); // Set to the proper state ahead of time if needed
                ui.DrawListItem(this, ref expanded, ref locked, ref selected, 
                    showFoldout: true, 
                    showLock: true, 
                    showToggle: true, 
                    showResultBackground: true, 
                    showScript: tests.Count > 0, 
                    showClearResult: tests.Count > 0, 
                    showResult: tests.Count > 0
                );

                // Process events
                // Check if the user just expanded the Foldout while holding down the Alt key
                if (Event.current.alt && expanded != wasExpanded) ExpandAll(ui, expanded);

                if (wasSelected != selected)
                {
                    // mixed is the same as the toggle not being selected
                    if (wasMixed)
                    {
                        if (selected) Select(ui);
                    }
                    else
                    {
                        if (wasSelected) Deselect(ui);
                        else Select(ui);
                    }
                }

                if (locked != wasLocked)
                {
                    if (locked && !wasLocked) Lock(ui);
                    else if (!locked && wasLocked) Unlock(ui);
                }
                else locked = IsAllLocked(ui);

                if (expanded)
                {
                    // Originally order the child Foldouts by their names
                    List<Foldout> children = new List<Foldout>(GetChildren(ui, false).OrderBy(x => x.GetName()));

                    ui.indentLevel++;
                    if (children.Count > 0)
                    {
                        foreach (Foldout child in children) child.Draw(ui);
                    }
                    else
                    {
                        foreach (Test test in tests.OrderBy(x => x.attribute.name))
                        {
                            ui.DrawListItem(test, ref test.expanded, ref test.locked, ref test.selected,
                                showFoldout: false,
                                showLock: true,
                                showToggle: true,
                                showResultBackground: true,
                                showScript: false,
                                showClearResult: true,
                                showResult: true
                            );
                        }
                    }
                    ui.indentLevel--;
                }
            }
            EditorGUILayout.EndVertical();
        }
        #endregion

        #region Controls
        /// <summary>
        /// Toggle on the Foldout, and thereby all its children. Does not affect locked items.
        /// </summary>
        public void Select(TestManagerUI ui)
        {
            selected = true;
            foreach (Test test in tests)
                if (!test.locked) test.selected = selected;
            foreach (Foldout foldout in GetChildren(ui)) foldout.Select(ui);
        }
        /// <summary>
        /// Toggle off the Foldout, and thereby all its children.
        /// </summary>
        public void Deselect(TestManagerUI ui)
        {
            selected = false;
            foreach (Test test in tests)
                if (!test.locked) test.selected = selected;
            foreach (Foldout foldout in GetChildren(ui)) foldout.Deselect(ui);
        }
        /// <summary>
        /// Lock the Foldout, and thereby all its children.
        /// </summary>
        public void Lock(TestManagerUI ui)
        {
            locked = true;
            foreach (Test test in tests) test.locked = locked;
            foreach (Foldout foldout in GetChildren(ui)) foldout.Lock(ui);
        }
        /// <summary>
        /// Unlock the Foldout, and thereby all its children.
        /// </summary>
        public void Unlock(TestManagerUI ui)
        {
            locked = false;
            foreach (Test test in tests) test.locked = locked;
            foreach (Foldout foldout in GetChildren(ui)) foldout.Unlock(ui);
        }
        /// <summary>
        /// When the user is holding Alt and clicks on the foldout arrow, expand also all child foldouts and Tests
        /// </summary>
        public void ExpandAll(TestManagerUI ui, bool value)
        {
            expanded = value;
            foreach (Test test in GetTests(ui)) test.expanded = value;
            foreach (Foldout child in GetChildren(ui)) child.ExpandAll(ui, value);
        }
        #endregion

        #region Tree Methods
        /// <summary>
        /// Locate all the Foldout objects that are children of this Foldout.
        /// </summary>
        public IEnumerable<Foldout> GetChildren(TestManagerUI ui, bool includeSubdirectories = true)
        {
            if (includeSubdirectories)
            {
                foreach (Foldout foldout in ui.foldouts)
                    if (IsParentOf(foldout)) yield return foldout;
            }
            else
            {
                bool isRoot = string.IsNullOrEmpty(path);
                foreach (Foldout foldout in ui.foldouts)
                {
                    if (string.IsNullOrEmpty(foldout.path)) // This is the root foldout
                    {
                        continue; // The rootFoldout is never a child of any other foldout
                    }
                    else
                    {
                        string dirname = Path.GetDirectoryName(foldout.path);
                        if (isRoot && string.IsNullOrEmpty(dirname)) yield return foldout;
                        else if (dirname == path) yield return foldout;
                    }
                }
            }
        }

        /// <summary>
        /// Locate all the Test objects included in this Foldout.
        /// </summary>
        public IEnumerable<Test> GetTests(TestManagerUI ui, bool includeSubdirectories = true)
        {
            if (tests != null)
            {
                foreach (Test test in tests) yield return test;
                foreach (Foldout child in GetChildren(ui, includeSubdirectories))
                    foreach (Test test in child.tests) yield return test;
            }
        }

        /// <summary>
        /// Return the cumulative testing result from all child tests
        /// </summary>
        public Test.Result GetTotalResult(TestManagerUI ui)
        {
            bool anyPassed = false;
            foreach (Test test in GetTests(ui))
            {
                // If any children are Fail, we are Fail
                if (test.result == Test.Result.Fail) return Test.Result.Fail;
                anyPassed |= test.result == Test.Result.Pass;
            }
            if (anyPassed) return Test.Result.Pass;
            return Test.Result.None; // inconclusive
        }
        #endregion

        #region Operators
        public override string ToString() => "Foldout(" + path + ")";
        #endregion

        #region Properties Methods
        /// <summary>
        /// Returns true if the other Foldout contains this Foldout in any of its subdirectories, and false otherwise.
        /// </summary>
        public bool IsChildOf(Foldout other) => Utilities.IsPathChild(other.path, path);

        /// <summary>
        /// Returns true if this Foldout contains the other Foldout in any subdirectory, and false otherwise.
        /// </summary>
        public bool IsParentOf(Foldout other) => other.IsChildOf(this);

        /// <summary>
        /// Returns true if more than one of this Foldout's tests is selected, but not all of them. The tests that are checked are
        /// all of those in every subdirectory.
        /// </summary>
        public bool IsMixed(TestManagerUI ui)
        {
            int nSelected = 0;
            List<Test> tests = new List<Test>(GetTests(ui));
            foreach (Test test in tests)
            {
                if (test.selected) nSelected++;
            }
            return nSelected != 0 && nSelected != tests.Count;
        }
        /// <summary>
        /// Returns true if all tests and child Foldouts are selected, in every subdirectory.
        /// </summary>
        public bool IsAllSelected(TestManagerUI ui)
        {
            foreach (Test test in tests)
                if (!test.selected) return false;
            foreach (Foldout child in GetChildren(ui))
                if (!child.selected) return false;
            return true;
        }
        /// <summary>
        /// Returns true if all tests and child Foldouts are locked, in every subdirectory.
        /// </summary>
        public bool IsAllLocked(TestManagerUI ui)
        {
            foreach (Test test in tests)
                if (!test.locked) return false;
            foreach (Foldout child in GetChildren(ui))
                if (!child.locked) return false;
            return true;
        }
        /// <summary>
        /// Return the last element in this Foldout's path (the "file name").
        /// </summary>
        public string GetName() => Path.GetFileName(path);
        #endregion
    }
}
