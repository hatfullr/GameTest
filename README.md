# UnityTest
A testing framework for the Unity editor that helps you maintain and debug your code without requiring "assembly definition" files, unlike the [Unity Test Framework](https://docs.unity3d.com/Packages/com.unity.test-framework@1.4/manual/index.html) (UTF).

##Features
1. No `*.asmdef` files required.
   - UTF requires assembly definition (`*.asmdef`) files in your project. This can create significant overhead for projects which do not already contain `*.asmdef` files, as all existing code must be organized into separate assemblies by hand. This can cause unsolvable dependency issues, rendering UTF unusable `UnityTest` requires no such prerequisite work.
2. Run your tests directly from a Unity editor window.
   - Select which tests to run.
   - See the results directly in the editor window or in the console.
3. Write test methods directly within your classes.
   - Simply add an attribute above any method in any class to make it show in the UnityTest Manager window.
4. Setup and teardown functions can be created for each test for finer control.
   - By default, a test created as in (3) will instantiate a `GameObject` with the `MonoBehaviour` class attached to it. This is then passed as input to your test method in (3). You can instead programmatically create your own `GameObject` in a setup method, and cleanup your test in a teardown method.
5. Create your own test suite classes for finer control.
   - Each suite behaves similar to a `MonoBehaviour`, in that an inspector is available for linking object references and setting variable values. Each method in the suite works like the test methods in (3), with the benefit of residing in a separate class. This can be helpful for larger tests.
6. Create complex testing environment prefabs.
   - Most games require a specific code to function under specific conditions, such as an enemy being in a certain location or the player providing a series of inputs. You can create such environments by hand in the editor and save it as a prefab. Place that prefab in the "Default Prefab" field to instantiate it at the start of the test.

----------------------------

##Getting Started
1. 




##Test Methods



##Test Suites