[![ko-fi](https://ko-fi.com/img/githubbutton_sm.svg)](https://ko-fi.com/K3K415S8MR)

# GameTest
Are you tired of finding new bugs in old code? Use `GameTest` to run automated tests that ensure code is working as intended. Unlike the [Unity Test Framework](https://docs.unity3d.com/Packages/com.unity.test-framework@1.4/manual/index.html) (UTF), `GameTest` does not require any special assembly definition files (`*.asmdef`) or any other prerequisites. `GameTest` works "out of the box".

## Features
1. User interface with granular control of testing parameters
2. Write test methods in any `MonoBehaviour`
3. Create complex testing environment prefabs

----------------------------

## Installation
1. Go to `Window > Package Manager` in the Unity application menu bar.
2. Click "+" in the top left and select "Add package from git URL...".
3. Paste in the GitHub repo link: https://github.com/hatfullr/GameTest.git
4. Press Enter, or click "Add".
5. Open GameTest at `Window > GameTest`.

If you run into issues, check [Installation Troubleshooting](#installation-troubleshooting).

### The GameTest Folder
Upon opening the GameTest window, a folder called "GameTest" will be created in the "Assets" folder. This is where `GameTest` will store data about the tests in your project. If you ever need to manually reset `GameTest`, delete this folder and re-open GameTest. It is recommended that you do not rename or move this folder, and it's probably also a bad idea to store anything you care about there.

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
When a test fails, it will throw an exception that is visible in the console log. Unfortunately, double-clicking the message will not deliver you to the line of code where the test failed. This is a limitation of the Unity editor API. To find the line where a test failed, follow these steps:
1. In the console, click the "triple dot" button in the top-right.
2. Turn on "Strip logging callstack". This removes any clutter from the stack trace in the console that might have come from `GameTest`.
3. Click the message in the console.
4. Find the link to your script in the stack trace and click on it.

Unfortunately, this is the best solution currently possible in Unity, at least until Unity adds an API for the console window.

## Installation Troubleshooting

###  `No 'git' executable was found. Please install Git on your system then restart Unity and Unity Hub`
This means Unity can't find your Git installation. From the [Unity manual](https://docs.unity3d.com/Manual/upm-git.html):

> To use Git dependencies in a project, make sure you installed the [Git client](https://git-scm.com/) (minimum version 2.14.0) on your computer and that you have added the Git executable path to the PATH system environment variable.
>
> **Warning:** Unity tested the Package Manager to work with Git 2.14.0 and above. Unity can’t guarantee the results if you use Git versions below 2.14.0.

After installing Git, make sure you close the Unity editor as well as Unity Hub (make sure the process is killed; Unity Hub likes to hide). If installing Git doesn't work, you may need to add Git to your PATH variable. [This post](https://discussions.unity.com/t/no-git-executable-was-found-please-install-git-on-your-system-and-restart-unity/755063/6) might help.
