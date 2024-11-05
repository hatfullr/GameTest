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
            bool wasMixed = IsMixed(ui.manager);
            bool wasLocked = locked;

            Rect rect = EditorGUILayout.BeginVertical();
            {
                Test.Result result = GetTotalResult(ui.manager);

                // This creates a nice visual grouping for the tests to hang out in
                if (expanded && tests.Count > 0)
                {
                    int previousIndentLevel = EditorGUI.indentLevel;
                    EditorGUI.indentLevel = ui.indentLevel + 1;
                    rect = EditorGUI.IndentedRect(ui.itemRect);
                    rect.height = Style.lineHeight * (tests.Count + 1);
                    EditorGUI.indentLevel = previousIndentLevel;

                    Rect header = new Rect(rect);
                    header.height = Style.lineHeight;

                    GUIStyle style = Style.Get("TestRect");

                    GUI.Box(rect, GUIContent.none, style);
                    GUI.Box(header, GUIContent.none, style);
                }

                selected |= IsAllSelected(ui.manager); // Set to the proper state ahead of time if needed
                if (!IsAnySelected(ui.manager)) selected = false;

                ui.DrawListItem(this, ref expanded, ref locked, ref selected,
                    showFoldout: true,
                    showScript: false,
                    showLock: true,
                    showToggle: true,
                    showResultBackground: true,
                    showClearResult: tests.Count > 0,
                    showResult: tests.Count > 0,
                    changeItemRectWidthOnTextOverflow: true
                );

                if (expanded)
                {
                    // Originally order the child Foldouts by their names
                    List<Foldout> children = new List<Foldout>(GetChildren(ui.manager, false).OrderBy(x => x.GetName()));

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
                                showScript: true,
                                showLock: true,
                                showToggle: true,
                                showResultBackground: true,
                                showClearResult: true,
                                showResult: true,
                                changeItemRectWidthOnTextOverflow: true
                            );
                        }
                        ui.itemRect.y += Style.TestManagerUI.foldoutMargin;
                    }
                    ui.indentLevel--;
                }
            }
            EditorGUILayout.EndVertical();


            // Process events
            // Check if the user just expanded the Foldout while holding down the Alt key
            if (Event.current.alt && expanded != wasExpanded) ExpandAll(ui.manager, expanded);

            if (wasSelected != selected)
            {
                // mixed is the same as the toggle not being selected
                if (wasMixed)
                {
                    if (selected) Select(ui.manager);
                }
                else
                {
                    if (wasSelected) Deselect(ui.manager);
                    else Select(ui.manager);
                }
            }

            if (locked != wasLocked)
            {
                if (locked && !wasLocked) Lock(ui.manager);
                else if (!locked && wasLocked) Unlock(ui.manager);
            }
            else locked = IsAllLocked(ui.manager);
        }
        #endregion

        #region Controls
        /// <summary>
        /// Toggle on the Foldout, and thereby all its children. Does not affect locked items.
        /// </summary>
        public void Select(TestManager manager)
        {
            selected = true;
            foreach (Test test in tests)
                if (!test.locked) test.selected = selected;
            foreach (Foldout foldout in GetChildren(manager)) foldout.Select(manager);
        }
        /// <summary>
        /// Toggle off the Foldout, and thereby all its children.
        /// </summary>
        public void Deselect(TestManager manager)
        {
            selected = false;
            foreach (Test test in tests)
                if (!test.locked) test.selected = selected;
            foreach (Foldout foldout in GetChildren(manager)) foldout.Deselect(manager);
        }
        /// <summary>
        /// Lock the Foldout, and thereby all its children.
        /// </summary>
        public void Lock(TestManager manager)
        {
            locked = true;
            foreach (Test test in tests) test.locked = locked;
            foreach (Foldout foldout in GetChildren(manager)) foldout.Lock(manager);
        }
        /// <summary>
        /// Unlock the Foldout, and thereby all its children.
        /// </summary>
        public void Unlock(TestManager manager)
        {
            locked = false;
            foreach (Test test in tests) test.locked = locked;
            foreach (Foldout foldout in GetChildren(manager)) foldout.Unlock(manager);
        }
        /// <summary>
        /// When the user is holding Alt and clicks on the foldout arrow, expand also all child foldouts and Tests
        /// </summary>
        public void ExpandAll(TestManager manager, bool value)
        {
            expanded = value;
            foreach (Test test in GetTests(manager)) test.expanded = value;
            foreach (Foldout child in GetChildren(manager)) child.ExpandAll(manager, value);
        }
        #endregion

        #region Tree Methods
        /// <summary>
        /// Locate all the Foldout objects that are children of this Foldout.
        /// </summary>
        public IEnumerable<Foldout> GetChildren(TestManager manager, bool includeSubdirectories = true)
        {
            if (includeSubdirectories)
            {
                foreach (Foldout foldout in manager.foldouts)
                    if (IsParentOf(foldout)) yield return foldout;
            }
            else
            {
                bool isRoot = string.IsNullOrEmpty(path);
                foreach (Foldout foldout in manager.foldouts)
                {
                    if (string.IsNullOrEmpty(foldout.path)) // This is the root foldout
                    {
                        continue; // The rootFoldout is never a child of any other foldout
                    }
                    
                    string dirname = Path.GetDirectoryName(foldout.path);
                    if (isRoot && string.IsNullOrEmpty(dirname)) yield return foldout;
                    else if (dirname == path) yield return foldout;
                }
            }
        }

        /// <summary>
        /// Find all visible Foldouts. A Foldout is visible if all its parent Foldouts are expanded.
        /// </summary>
        public static IEnumerable<Foldout> GetVisible(TestManagerUI ui)
        {
            foreach (Foldout foldout in ui.foldouts)
            {
                if (string.IsNullOrEmpty(foldout.path)) // This is the root foldout
                {
                    continue; // The rootFoldout is never visible
                }

                string dirname = Path.GetDirectoryName(foldout.path);
                if (string.IsNullOrEmpty(dirname)) yield return foldout; // Direct children of the rootFoldout are always visible
                else
                {
                    // Check if all parent foldouts are expanded. If so, this Foldout is visible
                    bool allParentsVisible = true;
                    foreach (string parentPath in Utilities.IterateDirectories(foldout.path))
                    {
                        if (parentPath == foldout.path) continue;
                        foreach (Foldout f in ui.foldouts)
                        {
                            if (f.path != parentPath) continue;
                            if (!f.expanded) allParentsVisible = false;
                            break;
                        }
                        if (!allParentsVisible) break;
                    }

                    if (allParentsVisible) yield return foldout;
                }
            }
        }

        /// <summary>
        /// Locate all the Test objects included in this Foldout.
        /// </summary>
        public IEnumerable<Test> GetTests(TestManager manager, bool includeSubdirectories = true)
        {
            if (tests != null)
            {
                foreach (Test test in tests) yield return test;
                foreach (Foldout child in GetChildren(manager, includeSubdirectories))
                    foreach (Test test in child.tests) yield return test;
            }
        }

        /// <summary>
        /// Return the cumulative testing result from all child tests
        /// </summary>
        public Test.Result GetTotalResult(TestManager manager)
        {
            bool anyPassed = false;
            foreach (Test test in GetTests(manager))
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
        public bool IsMixed(TestManager manager)
        {
            int nSelected = 0;
            List<Test> tests = new List<Test>(GetTests(manager));
            foreach (Test test in tests)
            {
                if (test.selected) nSelected++;
            }
            return nSelected != 0 && nSelected != tests.Count;
        }
        /// <summary>
        /// Returns true if all tests and child Foldouts are selected, in every subdirectory.
        /// </summary>
        public bool IsAllSelected(TestManager manager)
        {
            foreach (Test test in tests)
                if (!test.selected) return false;
            foreach (Foldout child in GetChildren(manager))
                if (!child.selected) return false;
            return true;
        }
        public bool IsAnySelected(TestManager manager)
        {
            foreach (Test test in tests)
                if (test.selected) return true;
            foreach (Foldout child in GetChildren(manager))
                if (child.selected) return true;
            return false;
        }
        /// <summary>
        /// Returns true if all tests and child Foldouts are locked, in every subdirectory.
        /// </summary>
        public bool IsAllLocked(TestManager manager)
        {
            foreach (Test test in tests)
                if (!test.locked) return false;
            foreach (Foldout child in GetChildren(manager))
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
