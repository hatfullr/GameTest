using System.Collections;
using UnityEditor;
using UnityEngine;

namespace GameTest
{
    public class AttributeTests : MonoBehaviour
    {
        [Test(pauseOnFail = true)]
        private void PauseOnFailStandard(GameObject go)
        {
            Assert.Fail();
            if (!EditorApplication.isPaused) throw new System.Exception("Editor should be paused");
        }

        [Test(pauseOnFail = true)]
        private IEnumerator PauseOnFailCoroutine(GameObject go)
        {
            int start = Time.frameCount;
            yield return null;
            Assert.Fail();
            if (!EditorApplication.isPaused) throw new System.Exception("Editor should be paused");
            if (start + 1 != Time.frameCount) throw new System.Exception("PauseOnFailCoroutine should only take 1 frame to complete, but took " + (Time.frameCount - start));
        }

        [Test(name = "NameTest AAA")]
        private void NameTest(GameObject go)
        {
            TestManager manager = TestManager.Get();
            foreach (Test test in manager.GetTests())
            {
                if (!test.attribute.GetPath().EndsWith("NameTest AAA")) continue;
                return;
            }
            Assert.Fail("Failed to find \"NameTest AAA\" in the UI");
        }
    }
}