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

            GUILayout.BeginHorizontal();
            {
                Rect controlRect = EditorGUILayout.GetControlRect(false);

                // Draw the test result indicators
                if (!expanded) controlRect = TestManagerUI.PaintResultFeatures(controlRect, GetTotalResult());

                // Setup the indented area
                Rect indentedRect;
                if (ui.indentWidth == 0f)
                {
                    EditorGUI.indentLevel++;
                    indentedRect = EditorGUI.IndentedRect(controlRect);
                    EditorGUI.indentLevel--;
                    ui.indentWidth = controlRect.width - indentedRect.width;
                }
                else
                {
                    indentedRect = new Rect(controlRect);
                    indentedRect.x += ui.indentWidth * ui.indentLevel;
                    indentedRect.width -= ui.indentWidth * ui.indentLevel;
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
                        ui.settings.SetTest(tests[0]);
                        ui.settings.SetVisible(true);
                    }

                    indentedRect.x += indentedRect.width;
                    indentedRect.width = Style.TestManagerUI.scriptWidth;
                    using (new EditorGUI.DisabledScope(true))
                    {
                        EditorGUI.ObjectField(indentedRect, GUIContent.none, tests[0].GetScript(), tests[0].method.DeclaringType, false);
                    }
                }
            }
            GUILayout.EndHorizontal();

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
                Rect rect = EditorGUILayout.GetControlRect(false);
                rect = TestManagerUI.PaintResultFeatures(rect, test);
                rect.x += indent;
                rect.width -= indent;
                ui.DrawTest(rect, test, true, !test.IsInSuite(), true);
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
