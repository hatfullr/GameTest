using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
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
        [SerializeField] public string path;
        [SerializeField] private bool selected;
        [SerializeField] private bool expanded;
        [SerializeField] private bool locked;

        [SerializeField] private bool _isSuite = false;
        /// <summary>
        /// true if this Foldout is a Suite, and false otherwise. This is set only after Add() is called with an input test that originates
        /// from a Suite class.
        /// </summary>
        public bool isSuite { get => _isSuite; private set => _isSuite = value; }

        public List<Test> tests = new List<Test>();

        private static TestManagerUI _ui;
        private static TestManagerUI ui
        {
            get
            {
                if (_ui == null) _ui = EditorWindow.GetWindow<TestManagerUI>();
                return _ui;
            }
        }

        #region UI Methods
        public void Draw()
        {
            // If this is the root foldout
            if (string.IsNullOrEmpty(path)) throw new System.Exception("Cannot call Draw() on the rootFoldout. This should never happen.");

            bool wasExpanded = expanded;
            bool wasSelected = selected;
            bool wasMixed = IsMixed();
            bool wasLocked = locked;

            selected |= IsAllSelected(); // Set to the proper state ahead of time if needed
            ui.DrawListItem(this, ref expanded, ref locked, ref selected, true, true, true, true);

            // Process events
            // Check if the user just expanded the Foldout while holding down the Alt key
            if (Event.current.alt && expanded != wasExpanded) ExpandAll(expanded);
            
            if (wasSelected != selected)
            {
                // mixed is the same as the toggle not being selected
                if (wasMixed)
                {
                    if (selected) Select();
                }
                else
                {
                    if (wasSelected) Deselect();
                    else Select();
                }
            }
            
            if (locked != wasLocked)
            {
                if (locked && !wasLocked) Lock();
                else if (!locked && wasLocked) Unlock();
            }
            else locked = IsAllLocked();

            if (expanded)
            {
                ui.indentLevel++;
                DrawChildren();
                ui.indentLevel--;
            }
        }
        private void DrawChildren()
        {
            float indent = ui.indentLevel * ui.indentWidth;
            foreach (Test test in tests.OrderBy(x => x.attribute.name))
            {
                //Rect rect = EditorGUILayout.GetControlRect(false);
                //rect = TestManagerUI.PaintResultFeatures(rect, test);
                //rect.x += indent;
                //rect.width -= indent;
                ui.DrawTest(test, true, !test.isInSuite, true);
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

        #region Tree Methods
        /// <summary>
        /// Locate all the Foldout objects that are children of this Foldout.
        /// </summary>
        public IEnumerable<Foldout> GetChildren(bool includeSubdirectories = true)
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
    }
}
