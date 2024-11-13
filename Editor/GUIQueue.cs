using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

namespace UnityTest
{
    /// <summary>
    /// This is where the queued and finished tests in the TestManagerUI are drawn
    /// </summary>
    [System.Serializable]
    public class GUIQueue : ScriptableObject
    {
        public const string fileName = nameof(GUIQueue);

        [SerializeField] private Vector2 queueScrollPosition;
        [SerializeField] private Vector2 finishedScrollPosition;
        [SerializeField] private bool hideMain = false;

        private float height = Style.GUIQueue.minHeight;

        private Rect splitterRect, mainRect;
        private bool dragging;
        private Vector2 dragPos;

        private static TestManagerUI _ui;
        private static TestManagerUI ui
        {
            get
            {
                if (_ui == null) _ui = EditorWindow.GetWindow<TestManagerUI>();
                return _ui;
            }
        }

        public void Reset()
        {
            splitterRect = default;
            mainRect = default;
            queueScrollPosition = default;
            finishedScrollPosition = default;
            height = Style.GUIQueue.minHeight;
            hideMain = false;

            Utilities.DeleteAsset(fileName, Utilities.dataPath);
            Utilities.SaveAsset(this);
        }

        public void Draw()
        {
            DrawSplitter();

            if (hideMain)
            {
                ProcessEvents();
                return;
            }

            // The queue window
            using (new EditorGUILayout.VerticalScope(GUILayout.ExpandWidth(true), GUILayout.Height(height)))
            {
                DrawQueueRunning();

                EditorGUILayout.Space();

                // "Queue" space
                using (new EditorGUILayout.HorizontalScope(GUILayout.ExpandHeight(true), GUILayout.ExpandWidth(true)))
                {
                    //Rect left = new Rect(rect.x, rect.y, 0.5f * rect.width, rect.height);
                    //Rect right = new Rect(rect.x + 0.5f * rect.width, rect.y, 0.5f * rect.width, rect.height);
                    queueScrollPosition = DrawQueue("Selected", ui.manager.queue, null, queueScrollPosition, 
                        showResult: false
                    );
                    finishedScrollPosition = DrawQueue("Finished", ui.manager.finishedTests, ui.manager.finishedResults, finishedScrollPosition, 
                        showResult: true,
                        showSettings : false,
                        reversed: true
                    );
                }
            }

            mainRect = GUILayoutUtility.GetLastRect();

            ProcessEvents();
        }

        private Vector2 DrawQueue(string title, List<Test> queue, List<Test.Result> results, Vector2 scrollPosition, 
            bool showResult = false,
            bool showSettings = true,
            bool reversed = false
        )
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.ExpandWidth(true)))
            {
                // header labels for the queue
                using (new EditorGUILayout.HorizontalScope(GUILayout.ExpandWidth(true)))
                {
                    GUIStyle style = Style.Get("GUIQueue/Queue/Title");
                    Rect r = EditorGUILayout.GetControlRect(false);
                    r.width = Style.GetWidth(style, title);
                    GUI.Label(r, title, style);

                    GUILayout.FlexibleSpace();

                    bool disabled = false;
                    if (queue != null) disabled = queue.Count == 0;

                    using (new EditorGUI.DisabledScope(disabled))
                    {
                        if (GUILayout.Button("Clear") && !disabled)
                        {
                            if (queue == ui.manager.queue)
                                foreach (Test test in queue)
                                    test.selected = false;
                            queue.Clear();
                        }
                    }
                }



                // Queue area
                GUIStyle queueStyle = Style.Get("GUIQueue/Queue");
                EditorGUILayout.VerticalScope s = new EditorGUILayout.VerticalScope(queueStyle, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
                Rect queueRect = s.rect;
                using (s)
                {
                    // Respect proper padding
                    queueRect.x += queueStyle.padding.left;
                    queueRect.y += queueStyle.padding.top;
                    queueRect.width -= queueStyle.padding.horizontal;
                    queueRect.height -= queueStyle.padding.vertical;
                    
                    Rect viewRect = new Rect(queueRect);

                    List<Test> tests = new List<Test>();

                    if (queue != null)
                    {
                        tests = new List<Test>(queue);
                        if (reversed) tests.Reverse();

                        ui.indentLevel = 0;
                        ui.itemRect = queueRect;
                        ui.itemRect.height = EditorGUIUtility.singleLineHeight;
                        viewRect.height = EditorGUIUtility.singleLineHeight * tests.Count;
                    }
                    
                    scrollPosition = GUI.BeginScrollView(
                        queueRect,
                        scrollPosition,
                        viewRect,
                        false,
                        false,
                        GUIStyle.none,
                        GUI.skin.verticalScrollbar
                    );
                    {
                        foreach (Test test in tests)
                        {
                            DrawQueueTest(test, queue, 
                                showResult: showResult,
                                showSettings: showSettings
                            );
                        }
                    }
                    GUI.EndScrollView();
                }

                GUILayout.Space(Style.Get("GUIQueue/Queue").margin.bottom);
            }

            return scrollPosition;
        }

        private void DrawQueueRunning()
        {
            using (new GUILayout.VerticalScope())
            {
                using (new GUILayout.HorizontalScope())
                {
                    float previousLabelWidth = EditorGUIUtility.labelWidth;
                    //EditorGUIUtility.labelWidth = 0f;
                    GUILayout.Label("Running", Style.Get("GUIQueue/Queue/Title"), GUILayout.Width(Style.GetWidth("GUIQueue/Queue/Title", "Running")));

                    GUILayout.FlexibleSpace();

                    GUILayout.Label("frame " + string.Format("{0,8}", ui.manager.nframes) + "    " + ui.manager.timer.ToString("0.0000 s"));

                    EditorGUIUtility.labelWidth = previousLabelWidth;
                }

                GUIStyle box = Style.Get("GUIQueue/Queue");
                Rect rect = GUILayoutUtility.GetRect(GUIContent.none, box);
                rect.height += box.padding.vertical;
                GUI.Box(rect, GUIContent.none, box);
                rect.x += box.padding.left;
                rect.width -= box.padding.horizontal;
                rect.y += box.padding.top;
                rect.height -= box.padding.vertical;
                if (Test.current != null) DrawQueueTest(rect, Test.current);
            }
        }

        private void DrawQueueTest(Test test, List<Test> queue,
            bool showResult = false,
            bool showSettings = true
        )
        {
            GUIStyle clearStyle = Style.Get("ClearResult");
            GUIContent clearIcon = Style.GetIcon("ClearResult", "Remove test from queue");

            Rect last = new Rect(ui.itemRect);
            last.width = Style.GetWidth(clearStyle, clearIcon);
            float previousItemRectX = ui.itemRect.x;
            ui.itemRect.x += last.width;
            ui.itemRect.width -= last.width;

            if (GUI.Button(last, clearIcon, clearStyle)) queue.Remove(test);

            bool dummy = false;
            //Utilities.DrawDebugOutline(ui.itemRect, Color.red);
            ui.DrawListItem(test, ref dummy, ref dummy, ref dummy, 
                    showFoldout: false,
                    showScript: true,
                    showLock: false,
                    showToggle: false,
                    showResultBackground: showResult,
                    showClearResult: false,
                    showResult: showResult,
                    showSettings: showSettings,
                    name: test.attribute.GetPath()
                );
            ui.itemRect.x = previousItemRectX;
            ui.itemRect.width += last.width;
        }


        private void DrawQueueTest(Rect rect, Test test)
        {
            GUI.Label(rect, test.attribute.GetPath(), Style.Get("GUIQueue/Test"));
        }


        private void DrawSplitter()
        {
            using (new GUILayout.HorizontalScope(Style.Get("GUIQueue/Toolbar")))
            {
                GUIContent label = new GUIContent("Tests");
                GUIStyle labelStyle = Style.Get("GUIQueue/Toolbar/BoldLabel");
                GUILayout.Label(label, labelStyle, GUILayout.Width(Style.GetWidth(labelStyle, label)));

                using (new GUILayout.HorizontalScope(Style.Get("GUIQueue/Toolbar/Label"), GUILayout.ExpandWidth(true)))
                {
                    GUILayout.Space(GUI.skin.box.padding.left);
                    using (new GUILayout.VerticalScope())
                    {
                        GUILayout.FlexibleSpace();
                        GUILayout.Box(GUIContent.none, Style.Get("GUIQueue/Toolbar/Splitter"), GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
                        GUILayout.FlexibleSpace();
                    }
                    GUILayout.Space(GUI.skin.box.padding.right);
                }

                splitterRect = GUILayoutUtility.GetLastRect();

                Rect cursorRect = new Rect(splitterRect);
                if (dragging && Event.current != null) // This mimics Unity's preview window behavior
                {
                    cursorRect.yMin = 0f;
                    cursorRect.yMax = Screen.currentResolution.height;
                    cursorRect.xMin = 0f;
                }

                Utilities.SetCursorInRect(cursorRect, MouseCursor.SplitResizeUpDown);
                //EditorGUI.DrawRect(cursorRect, Color.red); // for debugging

                // "triple dot" menu
                GUIContent options = Style.GetIcon("GUIQueue/Toolbar/Options");

                GUIStyle optionsStyle = Style.Get("GUIQueue/Toolbar/Button");
                Rect optionsRect = GUILayoutUtility.GetRect(options, optionsStyle, GUILayout.Width(Style.GetWidth(optionsStyle, options)));
                if (EditorGUI.DropdownButton(optionsRect, options, FocusType.Passive, optionsStyle))
                {
                    GenericMenu menu = new GenericMenu();
                    menu.AddItem(new GUIContent("Reset"), false, Reset);
                    menu.DropDown(optionsRect);
                }
            }
        }


        private void ProcessEvents()
        {
            if (Event.current == null) return;

            if (Utilities.IsMouseButtonPressed() && Utilities.IsMouseOverRect(splitterRect) && GUI.enabled)
            {
                dragging = true;
                dragPos = Event.current.mousePosition;
            }
            else if (Utilities.IsMouseButtonReleased())
            {
                dragging = false;
                EditorWindow.GetWindow<TestManagerUI>().Repaint();
            }


            if (dragging && Utilities.IsMouseDragging())
            {
                float delta = (Event.current.mousePosition.y + 0.5f * splitterRect.height) - dragPos.y;

                hideMain = Event.current.mousePosition.y > mainRect.yMax - 0.5f * Style.GUIQueue.minHeight;

                if (!hideMain)
                {
                    float newHeight = Mathf.Max(mainRect.yMax - (dragPos.y + delta), Style.GUIQueue.minHeight);

                    // Make sure that the main window maintains a minimum height
                    if (mainRect.yMax - (newHeight + splitterRect.height) > Style.TestManagerUI.minHeight)
                        height = newHeight;
                }

                EditorWindow.GetWindow<TestManagerUI>().Repaint();
            }
        }
    }
}