[![ko-fi](https://ko-fi.com/img/githubbutton_sm.svg)](https://ko-fi.com/K3K415S8MR)

_Special thanks to Ganesh for help with testing._

# GameTest
GameTest ensures that the implementation of your game's design is done correctly across all version and builds. Similar to the [Unity Test Framework](https://docs.unity3d.com/Packages/com.unity.test-framework@1.4/manual/index.html) (UTF), GameTest provides a way to write automated tests so that it is easy to detect and debug problems as your code grows to meet your design goals. GameTest can also be used instead of UTF for existing projects that cannot transition to rigid "assembly definition" files (`.asmdef`) because of code dependency issues. GameTest does not require `.asmdef` files.

- [Installation](#installation)
- [Quick Start](#quick-start)
- [Debugging](#debugging)
- [Tests](#tests)

----------------------------

## Installation
1. Go to `Window > Package Manager` in the Unity application menu bar.
2. Click "+" in the top left and select "Add package from git URL...".
3. Paste in the GitHub repo link: https://github.com/hatfullr/GameTest.git
4. Press Enter, or click "Add".
5. Open GameTest at `Window > GameTest`.

If you run into issues, check [Installation Troubleshooting](#installation-troubleshooting).

### The GameTest Folder
Upon opening the GameTest window, a folder called "GameTest" will be created in the "Assets" folder. This is where GameTest will store data about the tests in your project. If you ever need to manually reset GameTest, delete this folder and re-open GameTest. You can move this folder in the preferences window in the triple-dot menu at the top right of the UI. It's probably a bad idea to store anything you care about in the GameTest folder.

### Installation Troubleshooting

####  `No 'git' executable was found. Please install Git on your system then restart Unity and Unity Hub`
This means Unity can't find your Git installation. From the [Unity manual](https://docs.unity3d.com/Manual/upm-git.html):

> To use Git dependencies in a project, make sure you installed the [Git client](https://git-scm.com/) (minimum version 2.14.0) on your computer and that you have added the Git executable path to the PATH system environment variable.
>
> **Warning:** Unity tested the Package Manager to work with Git 2.14.0 and above. Unity can’t guarantee the results if you use Git versions below 2.14.0.

After installing Git, make sure you close the Unity editor as well as Unity Hub (make sure the process is killed; Unity Hub likes to hide). If installing Git doesn't work, you may need to add Git to your PATH variable. [This post](https://discussions.unity.com/t/no-git-executable-was-found-please-install-git-on-your-system-and-restart-unity/755063/6) might help.


## Quick Start
Unit testing is intended to make sure code runs properly throughout version changes. This is particularly helpful when there are many complicated and inter-dependent systems. The following example keeps things simple. Suppose you have some Unity class called `Example`:
```C#
using UnityEngine;

public class Example : MonoBehaviour
{
    public int number;

    private void Method()
    {
        number++;
    }
}
```
Are we sure that `Method` actually increments `number` by one as we expect? Let's make a test:
```C#
using UnityEngine;
using GameTest;

public class Example : MonoBehaviour
{
    public int number;

    private void Method()
    {
        number++;
    }

    [Test] // Create a unit test with the name "TestMethod"
    private void TestMethod(GameObject gameObject)
    {
        Example script = gameObject.GetComponent<Example>();
        int previous = script.number; // Store the previous value of "number"
        script.Method(); // Increment "number"
        Assert.AreEqual(previous + 1, script.number); // Check if the increment worked
    }
}
```
Open GameTest at `Window > GameTest` and type "Method" in the search bar. Check the box to enable the test and then press the play button in the top left of the window (not the editor's play button). The editor will then enter Play mode and run the test. First, a `GameObject` is instantiated with an attached `Example` component. Then, that component's `TestMethod` is called. The results of the test are shown in the console.

## Debugging
When a test fails, it will throw an exception that is visible in the console log. Unfortunately, double-clicking the message will not deliver you to the line of code where the test failed. Instead, follow these steps:
1. In the console, click the "triple dot" button in the top-right.
2. Turn on "Strip logging callstack". This removes any clutter from the stack trace in the console that might have come from GameTest.
3. Click the message in the console.
4. Find the link to your script in the stack trace and click on it.

Unfortunately, this is the best solution currently possible in Unity, at least until Unity adds an API for the console window.

## Tests
Each test will execute in the queued order. You can reorder the queue by clicking and dragging individual tests.

The two kinds of tests are standard tests and coroutine tests. Standard tests return `void` and coroutine tests return `IEnumerator`:

```C#
public class Example : MonoBehaviour
{
    [Test] // Standard test
    private void StandardTest(GameObject gameObject)
    {
        Assert.IsTrue(true); // passes
    }
	
	[Test] // Coroutine test
	private IEnumerator CoroutineTest(GameObject gameObject)
	{
		int start = Time.frameCount;
		yield return null; // go to next frame
		Assert.AreEqual(start + 1, Time.frameCount);
		yield return new WaitForEndOfFrame(); // Unity hasn't gone to the next frame yet
		Assert.AreEqual(start + 1, Time.frameCount);
		
		float startTime = Time.realtimeSinceStartup;
		yield return new WaitForSecondsRealtime(1f);
		float duration = Time.realtimeSinceStartup - startTime;
		Assert.AreApproximatelyEqual(duration, 1f, 0.01f);
	}
}
```

A test cannot have a return type other than `void` or `IEnumerator`.

Note that the Pause button in the GameTest toolbar only prevents queued tests from being started. The Pause button in the Unity editor only stops coroutine tests from advancing frames and has no effect on standard tests.

If a test has `pauseOnFail = true` and it fails, then both the Unity editor and GameTest will be paused.