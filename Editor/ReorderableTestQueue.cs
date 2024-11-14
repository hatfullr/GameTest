using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityTest;

/// <summary>
/// I didn't like Unity's implementation of ReorderableList / it was too confusing to implement. This is my own
/// style of ReorderableList.
/// </summary>
public static class ReorderableTestQueue
{
    private static Test clickedOn;
    private static Rect clickedOnRect;
    private static Test mouseOver;
    private static Rect mouseOverRect;
    private static Test dragging;

    public struct QueueFieldResult
    {
        public List<Test> queue;
        public Vector2 scrollPosition;
    }

    public static QueueFieldResult QueueField(
        GUIContent title,
        List<Test> queue,
        Vector2 scrollPosition,
        System.Action<Rect, Test> testDrawer,
        bool reversed = false,
        bool deselectOnClear = true
    )
    {
        using (new EditorGUILayout.VerticalScope(GUILayout.ExpandWidth(true)))
        {
            List<Test> tests = new List<Test>();

            if (queue != null)
            {
                tests = new List<Test>(queue);
                if (reversed) tests.Reverse();
            }

            // Collect styles
            GUIStyle clearStyle = Style.Get("ClearResult");
            GUIContent clearIcon = Style.GetIcon("ClearResult", "Remove test from queue");
            GUIStyle titleStyle = Style.Get("GUIQueue/Queue/Title");
            GUIStyle queueStyle = Style.Get("GUIQueue/Queue");

            using (new EditorGUILayout.VerticalScope())
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(title, titleStyle, GUILayout.MaxWidth(Style.GetWidth(titleStyle, title)));
                    GUILayout.FlexibleSpace();
                    bool disabled = false;
                    if (queue != null) disabled = queue.Count == 0;

                    using (new EditorGUI.DisabledScope(disabled))
                    {
                        if (GUILayout.Button("Clear") && !disabled)
                        {
                            if (deselectOnClear)
                                foreach (Test test in queue)
                                    test.selected = false;
                            queue.Clear();
                        }
                    }
                }

                EditorGUILayout.ScrollViewScope scrollViewScope = new EditorGUILayout.ScrollViewScope(
                    scrollPosition,
                    false,
                    false,
                    GUIStyle.none,
                    GUI.skin.verticalScrollbar,
                    queueStyle,
                    GUILayout.ExpandHeight(true)
                );
                using (scrollViewScope)
                {
                    using (new EditorGUILayout.VerticalScope(GUILayout.ExpandWidth(true)))
                    {
                        if (Event.current != null)
                        {
                            mouseOver = null;
                            mouseOverRect = new Rect(0, 0, 0, 0);
                            if (Event.current.rawType != EventType.MouseDrag) dragging = null;
                        }
                        
                        foreach (Test test in tests)
                        {
                            Rect rect = EditorGUILayout.GetControlRect(false);
                            testDrawer.Invoke(rect, test);

                            if (Utilities.IsMouseOverRect(rect))
                            {
                                mouseOver = test;
                                mouseOverRect = rect;
                            }

                            if (clickedOn == test)
                            {
                                //EditorGUI.DrawRect(rect, Color.red);
                                if (Event.current != null)
                                {
                                    if (Event.current.rawType == EventType.MouseDrag)
                                    {
                                        dragging = test;
                                    }
                                }
                            }

                            if (dragging != null)
                            {
                                Utilities.DrawDebugOutline(rect, Color.red);
                                
                            }
                            Debug.Log(dragging);
                        }
                    }
                    scrollPosition = scrollViewScope.scrollPosition;

                    //Color color = Color.clear;

                   

                    //Utilities.DrawDebugOutline(rect, color);
                }
            }

            // Process events
            if (Event.current != null)
            {
                if (mouseOver != null && clickedOn != null && Event.current.rawType == EventType.MouseDrag)
                {
                    //EditorGUI.DrawRect(mouseOverRect, Color.green);
                    //EditorGUI.DrawRect(clickedOnRect, Color.red);
                    //Debug.Log(mouseOverRect + " " + clickedOnRect);
                }
                else if (mouseOver != null && clickedOn == null && Event.current.rawType == EventType.MouseDown)
                {
                    clickedOn = mouseOver;
                    clickedOnRect = mouseOverRect;
                }
                else if (clickedOn != null && Event.current.rawType == EventType.MouseUp)
                {
                    clickedOn = null;
                    clickedOnRect = new Rect(0, 0, 0, 0);
                }
            }
        }

        QueueFieldResult result = new QueueFieldResult();
        result.queue = queue;
        result.scrollPosition = scrollPosition;
        return result;
    }
}
