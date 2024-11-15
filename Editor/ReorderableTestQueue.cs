using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityTest;
using static UnityTest.TestManager;

/// <summary>
/// I didn't like Unity's implementation of ReorderableList / it was too confusing to implement. This is my own
/// style of ReorderableList.
/// </summary>
public class ReorderableTestQueue
{
    private List<Test> queue;
    public GUIContent title;
    public System.Action<Rect, Test> testDrawer;
    public System.Action onDrag;
    public bool reversed;
    public bool deselectOnClear;

    public Vector2 scrollPosition;

    private Test dragging;
    private Rect dragStartRect;
    private Test mouseOver;
    private Rect mouseOverRect;

    private int dragOffset = 0;

    public struct QueueFieldResult
    {
        public List<Test> queue;
        public Vector2 scrollPosition;
    }

    public ReorderableTestQueue(
        ref List<Test> queue,
        GUIContent title,
        System.Action<Rect, Test> testDrawer = null,
        System.Action onDrag = null,
        bool reversed = false,
        bool deselectOnClear = false
    )
    {
        this.queue = queue;
        this.title = title;
        this.testDrawer = testDrawer;
        this.onDrag = onDrag;
        this.reversed = reversed;
        this.deselectOnClear = deselectOnClear;
    }

    private bool IsDragging() => dragging != null;

    public float GetQueueHeight()
    {
        return Style.lineHeight * queue.Count;
    }

    private void DrawTitle(Rect rect)
    {
        GUIStyle labelStyle = Style.Get("GUIQueue/Queue/Title");
        GUIStyle clearStyle = Style.Get("GUIQueue/Queue/Clear");

        GUIContent label = new GUIContent(title);
        GUIContent clear = new GUIContent("Clear");

        Rect labelRect = new Rect(rect);
        Rect clearRect = new Rect(rect);

        labelRect.width = Style.GetWidth(labelStyle, label);
        clearRect.width = Style.GetWidth(clearStyle, clear);

        clearRect.x = rect.xMax - clearRect.width;

        GUI.Label(labelRect, label, labelStyle);

        bool disabled = false;
        if (queue != null) disabled = queue.Count == 0;

        using (new EditorGUI.DisabledScope(disabled))
        {
            if (GUI.Button(clearRect, clear, clearStyle) && !disabled)
            {
                if (deselectOnClear)
                    foreach (Test test in queue)
                        test.selected = false;
                queue.Clear();
            }
        }

        //Utilities.DrawDebugOutline(labelRect, Color.red);
        //Utilities.DrawDebugOutline(clearRect, Color.red);
    }

    private void DrawBody(Rect rect)
    {
        GUIStyle clearStyle = Style.Get("ClearResult");
        GUIContent clearIcon = Style.GetIcon("ClearResult", "Remove test from queue");
        GUIStyle queueStyle = Style.Get("GUIQueue/Queue");
        GUIStyle horizontalSlider = GUIStyle.none;
        GUIStyle verticalSlider = GUI.skin.verticalScrollbar;

        Rect viewRect = new Rect(rect);
        viewRect.x = 0f;
        viewRect.y = 0f;
        viewRect.width = rect.width;
        viewRect.height = GetQueueHeight();

        Rect itemRect = new Rect(viewRect);
        itemRect.height = Style.lineHeight;

        // Make space for the sliders
        if (viewRect.height > rect.height)
        {
            itemRect.width -= Style.GetWidth(verticalSlider);
        }

        // Draw the background
        GUI.Box(rect, GUIContent.none, queueStyle);

        // Apply padding
        itemRect.x += queueStyle.padding.left;
        itemRect.width -= queueStyle.padding.horizontal;
        itemRect.y += queueStyle.padding.top;

        rect.width -= 2f;

        GUI.ScrollViewScope scrollViewScope = new GUI.ScrollViewScope(
            rect,
            scrollPosition,
            viewRect,
            false,
            false,
            horizontalSlider,
            verticalSlider
        );
        using (scrollViewScope)
        {
            List<Test> tests = new List<Test>(queue);
            if (reversed) tests.Reverse();

            foreach (Test test in tests)
            {
                testDrawer.Invoke(itemRect, test);

                // Capture some events
                if (Utilities.IsMouseOverRect(itemRect))
                {
                    mouseOver = test;
                    mouseOverRect = itemRect;
                }

                if (IsDragging()) OnDrag();

                itemRect.y += itemRect.height;
            }
            scrollPosition = scrollViewScope.scrollPosition;
        }
    }

    private void ProcessEvents(Rect bodyRect)
    {
        if (Event.current == null) return;

        if (!Utilities.IsMouseOverRect(bodyRect)) mouseOver = null;

        ProcessDragDrop();
    }

    private void ProcessDragDrop()
    {
        if (!mouseOver)
        {
            if (Event.current.rawType == EventType.MouseUp && IsDragging()) OnDragCancelled();
            return;
        }

        if (Event.current.rawType == EventType.MouseDrag && !IsDragging()) OnDragStarted();
        else if (Event.current.rawType == EventType.MouseUp) OnDragCompleted();

        //Debug.Log(dragging);
    }

    /// <summary>
    /// Called on each draw that the user is dragging a Test.
    /// </summary>
    private void OnDrag()
    {
        EditorGUI.DrawRect(dragStartRect, Style.GUIQueue.dragFromColor);

        Rect upper = new Rect(mouseOverRect);
        Rect lower = new Rect(mouseOverRect);
        upper.height *= 0.5f;
        lower.height *= 0.5f;
        lower.y += upper.height;

        Rect dragTo = new Rect(mouseOverRect.x, 0f, mouseOverRect.width, 2f);

        if (Utilities.IsMouseOverRect(upper))
        {
            dragTo.y = upper.y - 0.5f * dragTo.height;
            if (reversed) dragOffset = 1;
            else dragOffset = -1;
        }
        else if (Utilities.IsMouseOverRect(lower))
        {
            dragTo.y = lower.yMax - 0.5f * dragTo.height;
            if (reversed) dragOffset = -1;
            else dragOffset = 1;
        }
        else
        {
            dragOffset = 0;
            return;
        }

        EditorGUI.DrawRect(dragTo, Style.GUIQueue.dragToColor);

        onDrag.Invoke();

        //Utilities.DrawDebugOutline(upper, Color.red);
        //Utilities.DrawDebugOutline(lower, Color.green);
    }

    private void OnDragStarted()
    {
        //Debug.Log("Drag started");
        dragging = mouseOver;
        dragStartRect = mouseOverRect;
    }

    private void OnDragCompleted()
    {
        //Debug.Log("Drag completed");

        // Put the dragged Test between the neighbor and the current mouseOver Test
        int oldIndex = queue.IndexOf(dragging);
        int newIndex = queue.IndexOf(mouseOver) + dragOffset;

        if (oldIndex != newIndex)
        {
            queue.RemoveAt(oldIndex);

            /*
            if (reversed)
            {
                if (newIndex < oldIndex) newIndex++; // the actual index could have shifted due to the removal
            }
            else
            {
                if (newIndex > oldIndex) newIndex--; // the actual index could have shifted due to the removal
            }
            */

            if (newIndex > oldIndex) newIndex--; // the actual index could have shifted due to the removal

            newIndex = Mathf.Clamp(newIndex, 0, queue.Count - 1);

            queue.Insert(newIndex, dragging);
        }

        dragging = null;
        dragOffset = 0;
    }

    private void OnDragCancelled()
    {
        //Debug.Log("Drag cancelled");
        dragging = null;
        dragOffset = 0;
    }

    public void Draw(Rect rect)
    {
        GUIStyle bodyStyle = Style.Get("GUIQueue/Queue");

        Rect titleRect = new Rect(rect);
        titleRect.height = Style.lineHeight;

        Rect bodyRect = new Rect(rect);
        bodyRect.y = titleRect.y + titleRect.height;
        bodyRect.height -= titleRect.height;

        // Apply margin
        bodyRect.y += bodyStyle.margin.top;
        bodyRect.height -= bodyStyle.margin.top;

        DrawTitle(titleRect);
        DrawBody(bodyRect);

        ProcessEvents(bodyRect);
    }
}
