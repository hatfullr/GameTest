using System.Collections;
using UnityEngine;

namespace GameTest
{
    public class IEnumeratorTests : MonoBehaviour
    {
        [Test]
        private IEnumerator WaitForSeconds(GameObject go)
        {
            float start = Time.realtimeSinceStartup;
            yield return new WaitForSecondsRealtime(1f);
            float duration = Time.realtimeSinceStartup - start;
            Assert.AreApproximatelyEqual(duration, 1f, 0.01f);
        }

        [Test]
        private IEnumerator CountFrames(GameObject go)
        {
            int start = Time.frameCount;
            yield return null; // go to next frame
            Assert.AreEqual(start + 1, Time.frameCount);
            yield return new WaitForEndOfFrame(); // Unity hasn't gone to the next frame yet
            Assert.AreEqual(start + 1, Time.frameCount);
        }
    }
}