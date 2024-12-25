using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace GameTest
{
    [System.Serializable]
    public class Foldout
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
        /// This is updated in method UpdateIsMixed, which is only called at certain times.
        /// </summary>
        [SerializeField] private bool isMixed;

        public Test.Result result { get; private set; }

        public Foldout(string path, bool selected = false, bool expanded = false, bool locked = false)
        {
            this.path = path;
            this.selected = selected;
            this.expanded = expanded;
            this.locked = locked;
        }

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
            UnityEngine.Profiling.Profiler.BeginSample(nameof(GameTest) + ".Foldout");

            //bool wasExpanded = expanded;
            //bool wasSelected = selected;
            //bool wasMixed = isMixed; //IsMixed(ui.manager);
            //bool wasLocked = locked;

            EditorGUILayout.VerticalScope scope = new EditorGUILayout.VerticalScope();
            Rect rect = scope.rect;
            using (scope)
            {
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

                //selected |= IsAllSelected(ui.manager); // Set to the proper state ahead of time if needed
                //if (!IsAnySelected(ui.manager)) selected = false;

                ui.DrawListItem(ui.itemRect, this, ref expanded, ref locked, ref selected,
                    showFoldout: true,
                    showScript: false,
                    showLock: true,
                    showToggle: true,
                    showResultBackground: true,
                    showClearResult: tests.Count > 0,
                    showResult: tests.Count > 0,
                    showSettings: false,
                    changeItemRectWidthOnTextOverflow: true
                );
                ui.itemRect.y += ui.itemRect.height;

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
                        IEnumerable<Test> order;
                        if (ui.manager.testSortOrder == TestManagerUI.TestSortOrder.Name) order = tests.OrderBy(x => x.attribute.name);
                        else if (ui.manager.testSortOrder == TestManagerUI.TestSortOrder.LineNumber) order = tests.OrderBy(x => x.attribute.lineNumber);
                        else throw new System.NotImplementedException();

                        bool dummy = false;
                        foreach (Test test in order)
                        {
                            ui.DrawListItem(ui.itemRect, test, ref dummy, ref test.locked, ref test.selected,
                                showFoldout: false,
                                showScript: true,
                                showLock: true,
                                showToggle: true,
                                showResultBackground: true,
                                showClearResult: true,
                                showResult: true,
                                changeItemRectWidthOnTextOverflow: true
                            );
                            ui.itemRect.y += ui.itemRect.height;
                        }
                        ui.itemRect.y += Style.TestManagerUI.foldoutMargin;
                    }
                    ui.indentLevel--;
                }
            }


            // Process events
            // Check if the user just expanded the Foldout while holding down the Alt key
            //if (Event.current.alt && expanded != wasExpanded) ExpandAll(ui.manager, expanded);

            /*
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
            */

            UnityEngine.Profiling.Profiler.EndSample();
        }
        #endregion

        #region Controls
        public bool IsSelected() => selected;
        public bool IsLocked() => locked;
        public bool IsExpanded() => expanded;

        /// <summary>
        /// Toggle on the Foldout, and thereby all its children. Does not affect locked items.
        /// </summary>
        public void Select(TestManager manager)
        {
            isMixed = false;
            selected = true;
            foreach (Test test in tests)
            {
                if (test.locked) continue;
                test.selected = selected;
                manager.AddToQueue(test);
            }
            foreach (Foldout foldout in GetChildren(manager)) foldout.Select(manager);
        }
        /// <summary>
        /// Toggle off the Foldout, and thereby all its children.
        /// </summary>
        public void Deselect(TestManager manager)
        {
            isMixed = false;
            selected = false;
            foreach (Test test in tests)
            {
                if (test.locked) continue;
                test.selected = selected;
                manager.RemoveFromQueue(test);
            }
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

        public IEnumerable<Foldout> GetParents(TestManager manager, bool reverse = false)
        {
            Foldout parent;
            foreach (string parentPath in Utilities.IterateDirectories(path, reverse: reverse))
            {
                // Locate the parent Foldout
                parent = null;
                foreach (Foldout foldout in manager.foldouts)
                {
                    if (foldout.path != parentPath) continue;
                    parent = foldout;
                    break;
                }
                if (parent == null) throw new System.Exception("Failed to find Foldout with path " + parentPath);
                yield return parent;
            }
        }

        /// <summary>
        /// Find all visible Foldouts. A Foldout is visible if all its parent Foldouts are expanded.
        /// </summary>
        public static IEnumerable<Foldout> GetVisible(TestManagerUI ui)
        {
            foreach (Foldout foldout in ui.manager.foldouts)
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
                        foreach (Foldout f in ui.manager.foldouts)
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
        /// Update the value of isMixed based on the current state. This is expensive and should only be called when a Test or Foldout has been selected.
        /// Returns true if the Foldout's state changed, and false otherwise.
        /// </summary>
        public bool UpdateState(TestManager manager)
        {
            bool wasMixed = isMixed;
            bool wasLocked = locked;
            bool wasSelected = selected;
            Test.Result previousResult = result;

            result = Test.Result.None;
            int nSelected = 0;
            int nPassed = 0;
            int nFailed = 0;
            int nSkipped = 0;
            int nLocked = 0;
            int total = 0;
            foreach (Test test in GetTests(manager, false))
            {
                if (test.selected) nSelected++;
                if (test.locked) nLocked++;

                if (test.result == Test.Result.Pass) nPassed++;
                else if (test.result == Test.Result.Fail) nFailed++;
                else if (test.result == Test.Result.Skipped) nSkipped++;

                total++;
            }
            int nMixed = 0;
            foreach (Foldout child in GetChildren(manager, false))
            {
                if (child.selected) nSelected++;
                if (child.isMixed) nMixed++;
                if (child.locked) nLocked++;

                if (child.result == Test.Result.Pass) nPassed++;
                else if (child.result == Test.Result.Fail) nFailed++;
                else if (child.result == Test.Result.Skipped) nSkipped++;

                total++;
            }

            isMixed = (nSelected > 0 && nSelected != total) || nMixed > 0;

            if (!isMixed)
            {
                if (nSelected > 0 && nSelected == total) selected = true;
                else if (nSelected == 0 && total > 0) selected = false;
            }

            if (total > 0) locked = nLocked == total;

            int totalRan = nPassed + nFailed + nSkipped;
            if (totalRan > 0)
            {
                if (nPassed + nSkipped == totalRan) result = Test.Result.Pass;
                else if (nFailed + nSkipped == totalRan) result = Test.Result.Fail;
                else if (nSkipped == totalRan) result = Test.Result.Skipped;
            }

            return isMixed != wasMixed || result != previousResult || selected != wasSelected || locked != wasLocked;
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
        /// Return true if this Foldout does not have any parent.
        /// </summary>
        public bool IsRoot()
        {
            if (string.IsNullOrEmpty(path)) return true;
            return string.IsNullOrEmpty(Path.GetDirectoryName(path));
        }

        /// <summary>
        /// Returns true if more than one of this Foldout's tests is selected, but not all of them.
        /// </summary>
        public bool IsMixed() => isMixed;
        
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
