using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.Events;

namespace UnityTest
{
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
        public bool allowReorder;

        public Vector2 scrollPosition;

        private Test dragging;
        private Rect dragStartRect, mouseOverRect;
        private Test mouseOver;

        private DragOffset dragOffset;

        private Rect dragBar;

        private enum DragOffset
        {
            Lower,
            Upper,
            TopMost,
            BottomMost,
        }

        public ReorderableTestQueue(
            ref List<Test> queue,
            GUIContent title,
            System.Action<Rect, Test> testDrawer = null,
            System.Action onDrag = null,
            bool reversed = false,
            bool deselectOnClear = false,
            bool allowReorder = true
        )
        {
            this.queue = queue;
            this.title = title;
            this.testDrawer = testDrawer;
            this.onDrag = onDrag;
            this.reversed = reversed;
            this.deselectOnClear = deselectOnClear;
            this.allowReorder = allowReorder;
        }

        private bool IsDragging() => dragging != null && allowReorder;

        public void Clear()
        {
            queue.Clear();
        }

        private List<Test> GetTests()
        {
            List<Test> tests = new List<Test>(queue);
            if (reversed) tests.Reverse();
            return tests;
        }

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
            GUIStyle draggingHandle = Style.Get("GUIQueue/DragHandle");
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
                Rect above = new Rect(itemRect.x, viewRect.y, itemRect.width, itemRect.y - viewRect.y);

                List<Test> tests = GetTests();

                if (IsDragging() && Utilities.IsMouseOverRect(above))
                {
                    mouseOver = tests[0];
                    mouseOverRect = itemRect;
                    dragBar = new Rect(itemRect);
                    dragBar.height = Style.GUIQueue.dragBarHeight;
                    dragBar.y -= 0.5f * dragBar.height;
                    dragOffset = DragOffset.TopMost;
                }

                Rect testRect;
                foreach (Test test in tests)
                {
                    if (allowReorder)
                    {
                        Rect dragHandleRect = new Rect(itemRect);
                        dragHandleRect.width = Style.GetWidth(draggingHandle);
                        dragHandleRect.x += draggingHandle.margin.left;

                        testRect = new Rect(itemRect);
                        testRect.x += dragHandleRect.width + draggingHandle.margin.horizontal;
                        testRect.width -= dragHandleRect.width + draggingHandle.margin.horizontal;

                        // Not sure why we gotta do this
                        dragHandleRect.position += draggingHandle.contentOffset;

                        GUI.Label(dragHandleRect, GUIContent.none, draggingHandle);
                    }
                    else testRect = itemRect;

                    //if (title.text == "Selected") Debug.Log(test + " " + test.method);
                    testDrawer.Invoke(testRect, test);

                    // Capture some events
                    if (Utilities.IsMouseOverRect(itemRect))
                    {
                        mouseOver = test;
                        mouseOverRect = itemRect;

                        if (IsDragging())
                        {
                            Rect upper = new Rect(itemRect.x, itemRect.y, itemRect.width, 0.5f * itemRect.height);
                            Rect lower = new Rect(itemRect.x, itemRect.y + upper.height, itemRect.width, upper.height);

                            dragBar = new Rect(itemRect);
                            dragBar.height = Style.GUIQueue.dragBarHeight;

                            dragOffset = DragOffset.Upper;

                            if (Utilities.IsMouseOverRect(lower))
                            {
                                dragOffset = DragOffset.Lower;
                                dragBar.y = itemRect.yMax;
                            }
                            if (test == tests[tests.Count - 1])
                            {
                                Rect below = new Rect(lower);
                                below.height = Screen.height;
                                if (Utilities.IsMouseOverRect(below))
                                {
                                    dragOffset = DragOffset.BottomMost;
                                    dragBar.y = itemRect.yMax;
                                }
                            }

                            dragBar.y -= 0.5f * dragBar.height;
                        }
                        else if (allowReorder) EditorGUI.DrawRect(itemRect, Style.GUIQueue.dragHoverColor);
                    }

                    itemRect.y += itemRect.height;
                }
                if (tests.Count > 0) itemRect.y -= itemRect.height;


                if (IsDragging()) OnDrag();

                // DEBUGGING
                //foreach (System.Tuple<Rect, Color> kvp in new System.Tuple<Rect, Color>[]
                //{
                    //new System.Tuple<Rect, Color>(above, isMouseAboveTests ? Color.green : Color.red),
                    //new System.Tuple<Rect, Color>(below, isMouseBelowTests ? Color.green : Color.red),
                    //new System.Tuple<Rect, Color>(queueRect, isMouseInQueue ? Color.green : Color.red),
                //}) Utilities.DrawDebugOutline(kvp.Item1, kvp.Item2);

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
            if (Event.current.rawType == EventType.MouseDrag && !IsDragging()) OnDragStarted();
            else if (Event.current.rawType == EventType.MouseUp && IsDragging()) OnDragCompleted();
        }

        /// <summary>
        /// Called on each draw that the user is dragging a Test.
        /// </summary>
        private void OnDrag()
        {
            EditorGUI.DrawRect(dragStartRect, Style.GUIQueue.dragFromColor);
            EditorGUI.DrawRect(dragBar, Style.GUIQueue.dragToColor);
            onDrag.Invoke();
        }

        private void OnDragStarted()
        {
            dragging = mouseOver;
            dragStartRect = mouseOverRect;
        }

        private void OnDragCompleted()
        {
            List<Test> tests = GetTests();


            if (dragOffset == DragOffset.TopMost)
            {
                queue.Remove(dragging);
                if (reversed) queue.Add(dragging);
                else queue.Insert(0, dragging);
            }
            else if (dragOffset == DragOffset.BottomMost)
            {
                queue.Remove(dragging);
                if (reversed) queue.Insert(0, dragging);
                else queue.Add(dragging);
            }
            else
            {
                // Put the dragged Test between the neighbor and the current mouseOver Test
                int newIndex = tests.IndexOf(mouseOver);
                if (dragOffset == DragOffset.Lower) newIndex += 1;

                newIndex = Mathf.Clamp(newIndex, 0, tests.Count - 1);

                // index might change after we have removed the Test
                if (tests.IndexOf(dragging) < newIndex) newIndex--;

                if (reversed) newIndex = tests.Count - newIndex;

                queue.Remove(dragging);
                queue.Insert(newIndex, dragging);
            }

            dragging = null;
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
}