using UnityEngine;
using UnityEngine.Assertions;

/// <summary>
/// This is just for showing helpful (hopefully) Unit Test examples
/// </summary>
namespace UnityTest
{
    public class ExampleTests : MonoBehaviour
    {
        /// <summary>
        /// SetUp functions take no arguments and they return a GameObject to which this script is attached.
        /// The Unit Tests are performed after OnEnable, which means Start won't have been run before the test runs.
        /// You can call Start before the test runs yourself by creating a custom SetUp function.
        /// </summary>
        private static GameObject SetUp()
        {
            // Create the GameObject that will be returned and attach an ExampleTests Component to it
            GameObject testObject = new GameObject("ExampleTests (SetUpExample)", typeof(ExampleTests));

            // Uncomment these lines if you want to run the Start function on the attached ExampleTests object
            // before the Unit Test executes.
            //ExampleTests exampleTests = testObject.GetComponent<ExampleTests>();
            //exampleTests.Start();

            // For the SetUpTearDown example we create a parent GameObject and let testObject be its child
            GameObject testParent = new GameObject("Test Parent");
            testObject.transform.SetParent(testParent.transform);

            // We will now lose reference to testParent because we must return testObject. See TearDown for more.
            return testObject;
        }


        /// <summary>
        /// Sometimes you have to build the scene hierarchy yourself in your SetUp script (i.e. using SetParent). By default
        /// the Unit Test will only destroy its own GameObject after succeeding its test, but if you need to destroy other
        /// GameObjects that you created in SetUp, you can specify your own TearDown method. It must accept a GameObject which
        /// is the same one that was returned by SetUp.
        /// </summary>
        private static void TearDown(GameObject testObject)
        {
            ExampleTests script = testObject.GetComponent<ExampleTests>();
            // This will destroy both the parent we created in SetUp and the GameObject this Component is attached to.
            // Make sure to use DestroyImmediate so that future Unit Tests won't be confused.
            DestroyImmediate(testObject.transform.parent.gameObject);
        }


        [UnityTest.Test("Examples/SimpleFail")]
        private void SimpleFail(GameObject testObject)
        {
            Assert.IsTrue(false, "You can include a message describing this test.");
        }

        [UnityTest.Test("Examples/SimplePass")]
        private void SimplePass(GameObject testObject)
        {
            Assert.IsTrue(true); // or no message
        }

        /// <summary>
        /// You specify the exact SetUp and TearDown functions for each Unit Test, so you can make SetUp and TearDown
        /// functions for each one of them if you like.
        /// </summary>
        [UnityTest.Test("Examples/SetUpTearDown", nameof(SetUp), nameof(TearDown), true)]
        private void SetUpTearDown(GameObject testObject)
        {
            // Let the test fail so that it can be seen in the scene hierarchy that there is a GameObject with name "Test Parent"
            // and it has a child "ExampleTests (SetUpExample)" as defined in the SetUp script.
            Assert.IsTrue(false, "SetUpTearDown is meant to fail");
        }
    }

    /// <summary>
    /// An example of a UnitTestSuite. Defining the SetUp and TearDown methods is not necessary, but can be done to have more control.
    /// Each method acts like a UnitTest. The SetUp method must have the name "SetUp", and the same is true for "TearDown". SetUp is 
    /// invoked before each method, and TearDown is invoked after each method.
    /// </summary>
    [UnityTest.Suite("Examples/TestSuite")]
    public static class ExampleTestSuite
    {

    }
}