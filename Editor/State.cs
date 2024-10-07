using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityTest;

namespace UnityTest
{
    /// <summary>
    /// Hold information about the current state of the manager window, such as if tests are selected, if tests have results, etc.
    /// This is used for drawing certain features which depend on the state.
    /// </summary>
    public class State
    {
        public bool anySelected { get; private set; }
        public bool allSelected { get; private set; }
        public bool selectedHaveResults { get; private set; }
        public bool anyResults { get; private set; }
        public bool anyFailed { get; private set; }

        public State(Foldout rootFoldout)
        {
            if (rootFoldout == null) return;
            allSelected = true;
            anyResults = false;
            anyFailed = false;
            foreach (Test test in rootFoldout.GetTests())
            {
                if (test.result != Test.Result.None) anyResults = true;
                if (test.result == Test.Result.Fail) anyFailed = true;

                if (test.locked)
                {
                    if (test.selected) selectedHaveResults |= test.result != Test.Result.None;
                }
                else
                {
                    if (test.selected)
                    {
                        anySelected = true;
                        selectedHaveResults |= test.result != Test.Result.None;
                    }
                    else allSelected = false;
                }
            }
        }
    }
}