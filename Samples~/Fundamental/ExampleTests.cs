using UnityEngine;

/// <summary>
/// This is just for showing helpful (hopefully) Test examples
/// </summary>
namespace GameTest
{
    namespace Examples // Separating by a namespace to avoid domain problems in your code
    {
        public class ExampleTests : MonoBehaviour
        {
            /// <summary>
            /// A test that is meant to fail. The given GameObject has only an ExampleTests component attached to it.
            /// </summary>
            [Test]
            private void Fail(GameObject testObject)
            {
                Assert.IsTrue(false, "You can include a message describing this test.");
            }

            /// <summary>
            /// A test that is meant to succeed. The given GameObject has only an ExampleTests component attached to it.
            /// </summary>
            [Test]
            private void Pass(GameObject testObject)
            {
                Assert.IsTrue(true); // or no message
            }

            /// <summary>
            /// A test that uses a SetUp and a TearDown function. In the SetUp function, "testObject" is set as a child of another GameObject
            /// called "Test Parent".
            /// </summary>
            [Test(nameof(SetUp), nameof(TearDown), false)]
            private void SetUpTearDown(GameObject testObject)
            {
                Assert.AreEqual(testObject.transform.parent.name, "Test Parent");
            }

            /// <summary>
            /// SetUp functions must be static, take no arguments, and return a GameObject to which this script is attached.
            /// </summary>
            private static GameObject SetUp()
            {
                // Create the GameObject that will be returned and attach an ExampleTests Component to it
                GameObject testObject = new GameObject("ExampleTests (SetUpExample)", typeof(ExampleTests));

                // Uncomment these lines if you want to run the Start function on the attached ExampleTests object
                // before the Test executes.
                //ExampleTests exampleTests = testObject.GetComponent<ExampleTests>();
                //exampleTests.Start();

                // For the SetUpTearDown example we create a parent GameObject and let testObject be its child
                GameObject testParent = new GameObject("Test Parent");
                testObject.transform.SetParent(testParent.transform);

                // We will now lose reference to testParent because we must return testObject. See TearDown for more.
                return testObject;
            }


            /// <summary>
            /// TearDown functions must take a GameObject argument. Sometimes it is helpful to build the scene in 
            /// a SetUp function. When a Test finishes, it will by default only destroy the GameObject that was created in 
            /// SetUp. If a TearDown function is also given, then instead only the TearDown function is run.
            /// </summary>
            private void TearDown(GameObject testObject)
            {
                // This will destroy both the parent we created in SetUp and the GameObject this Component is attached to.
                // Make sure to use DestroyImmediate so that future Tests won't be confused.
                DestroyImmediate(testObject.transform.parent.gameObject);
            }
        }

        /// <summary>
        /// An example of a Suite. Defining the SetUp and TearDown methods is not necessary, but can be done to have more control.
        /// Each method acts like a Test. The SetUp method must have the name "SetUp", and the same is true for "TearDown". SetUp is 
        /// invoked before each method, and TearDown is invoked after each method.
        /// </summary>
        [Suite]
        public static class ExampleTestSuite
        {

        }
    }
}