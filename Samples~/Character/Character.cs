using System.Collections;
using UnityEngine;

namespace UnityTest // Separating the namespaces so that this code doesn't conflict with your project
{
    public class Character : MonoBehaviour
    {
        public float speed = 1f;

        // Update is called once per frame
        void Update()
        {
            if (Input.GetKey(KeyCode.W)) MoveForward(Time.deltaTime);
            if (Input.GetKey(KeyCode.A)) MoveLeft(Time.deltaTime);
            if (Input.GetKey(KeyCode.S)) MoveBackward(Time.deltaTime);
            if (Input.GetKey(KeyCode.D)) MoveRight(Time.deltaTime);
        }

        // Very simple functions for movement
        private void MoveForward(float dt) => transform.position += speed * dt * Vector3.forward;
        private void MoveLeft(float dt) => transform.position += speed * dt * Vector3.left;
        private void MoveBackward(float dt) => transform.position += speed * dt * Vector3.back;
        private void MoveRight(float dt) => transform.position += speed * dt * Vector3.right;

        // It can be helpful to separate your tests visually by using a #region.
        #region Tests
        [Test]
        private void TestMovement(GameObject gameObject) // Appears as "MoveForward" in the UnityTest Manager
        {
            // This test will happen all a single frame

            Character character = gameObject.GetComponent<Character>();

            // Forward
            character.transform.position = Vector3.zero; // Making sure we know where the character is located first.
            character.MoveForward(1f); // Counterintuitively, you can access private methods here. It's OK to mark the methods as "public" instead if you want.
                                       // Be careful with floating point comparisons. Use AreApproximatelyEqual when comparing float values and AreEqual when comparing integer values.
            Assert.AreApproximatelyEqual(character.transform.position.z, character.speed * 1f); // Moved forward by the character's speed in 1 second

            // Backward
            character.transform.position = Vector3.zero;
            character.MoveBackward(1f);
            Assert.AreApproximatelyEqual(character.transform.position.z, -character.speed * 1f);

            // Right
            character.transform.position = Vector3.zero;
            character.MoveRight(1f);
            Assert.AreApproximatelyEqual(character.transform.position.x, character.speed * 1f);

            // Left
            character.transform.position = Vector3.zero;
            character.MoveLeft(1f);
            Assert.AreApproximatelyEqual(character.transform.position.x, -character.speed * 1f);
        }


        /// <summary>
        /// This test will take 2 seconds to finish. Use "yield return null" to skip to the next frame.
        /// We use a setup method to attach a mesh to the Character's GameObject so that it is easier to 
        /// see in the editor during the test.
        /// 
        /// Things to try:
        ///    1) Run this test normally, without pressing WASD. It should succeed.
        ///    2) Run this test while holding down W or S. It succeeds, because we never check the z component.
        ///    3) Run this test while holding down A or D. It fails, because the Character is not where we expected.
        ///    4) Click the Pause button (||) in the UnityTest Manager, then click Play (>). The test will not run yet.
        ///       Press the Step (>|) button in the UnityTest Manager. The test will run. Note that the Pause button stops
        ///       the next test from running immediately after the current test, and the Step button either runs the
        ///       next test if no test is running, or it skips the current test. A skipped test has an inconclusive result.
        ///    5) Click the Pause button (||) in the editor (not the UnityTest Manager), then click the Play button (>) in
        ///       the UnityTest Manager. This will bring you into Play mode. Press the Step button (>|) in the editor
        ///       to begin the test. You should see the GameObject get instantiated. Keep pressing the Step button
        ///       to advance frames. You should see a moving cube. In the bottom panel of the UnityTest Manager (labelled 
        ///       "Tests"), the frame count indicator should increase with each press of the Step button, as well as
        ///       the time indicator. Keep pressing the Step button until the test finishes, which should be at about 2s.
        ///
        /// The editor is paused when this test fails. If pauseOnFail = false, then if this test fails, the next test
        /// in the queue will not be started regardless.
        /// </summary>
        [Test(nameof(SetUpMovementContinuous), pauseOnFail = true)]
        private IEnumerator TestMovementContinuous(GameObject gameObject)
        {
            Character character = gameObject.GetComponent<Character>();

            character.transform.position = Vector3.zero; // Make sure we know where the character is located first.

            // Check if the position is still at (0, 0, 0):
            //    Sanity checks can be a good idea. The more checks you make, the more certain you can be of your code's behavior, but also
            //    the more rigid your code becomes (it might be harder to make big changes later on). For example, if later you write a code
            //    that, on every frame, checks for Characters located at (0, 0, 0) and moves them slightly, then the following tests will fail.
            //    The benefit is that when you run this test, you will be alerted to a change in the way your systems work, even if the code
            //    which moves the Characters at (0, 0, 0) is in some other class somewhere else in your project.
            Assert.AreApproximatelyEqual(character.transform.position.x, 0f);
            Assert.AreApproximatelyEqual(character.transform.position.y, 0f);
            Assert.AreApproximatelyEqual(character.transform.position.z, 0f);

            // For the next 2 seconds, move the character forward and separately track how far we expect it to move.
            Vector3 expected = character.transform.position;
            float time = 0f;
            while (time <= 2f)
            {
                float dt = Time.deltaTime;

                character.MoveRight(dt);
                expected.x += character.speed * dt;

                Assert.AreApproximatelyEqual(expected.x, character.transform.position.x);
                // You could add checks here for the y and z components of the Vector3, which should each be 0f.

                yield return null; // Skip to the next frame.
                time += dt;
            }
        }

        private static GameObject SetUpMovementContinuous()
        {
            GameObject go = new GameObject("Character MoveForward Test", typeof(Character));
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.transform.SetParent(go.transform);
            return go;
        }
        #endregion
    }
}