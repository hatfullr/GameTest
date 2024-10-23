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
            EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true), GUILayout.Height(height));
            {
                DrawQueueRunning();

                EditorGUILayout.Space();
                // "Queue" space
                Rect rect = EditorGUILayout.BeginHorizontal();//GUILayout.ExpandHeight(true));
                {
                    Rect left = new Rect(rect.x, rect.y, 0.5f * rect.width, rect.height);
                    Rect right = new Rect(rect.x + 0.5f * rect.width, rect.y, 0.5f * rect.width, rect.height);
                    queueScrollPosition = DrawQueue(left, "Selected", ui.manager.queue, null, queueScrollPosition);
                    finishedScrollPosition = DrawQueue(right, "Finished", ui.manager.finishedTests, ui.manager.finishedResults, finishedScrollPosition, true, true);
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndVertical();

            mainRect = GUILayoutUtility.GetLastRect();

            ProcessEvents();
        }

        private Vector2 DrawQueue(Rect rect, string title, List<Test> queue, List<Test.Result> results, Vector2 scrollPosition, bool paintResultFeatures = false, bool reversed = false)
        {
            EditorGUILayout.BeginVertical(GUILayout.MaxWidth(rect.width));
            {
                // header labels for the queue
                EditorGUILayout.BeginHorizontal(GUILayout.MaxWidth(rect.width));
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
                EditorGUILayout.EndHorizontal();



                // Queue area
                EditorGUILayout.BeginVertical(Style.Get("GUIQueue/Queue"), GUILayout.MaxWidth(rect.width));
                {
                    scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUIStyle.none, GUI.skin.verticalScrollbar, GUILayout.MaxWidth(rect.width));
                    {
                        if (queue != null)
                        {
                            List<Test> tests = new List<Test>(queue);
                            if (reversed) tests.Reverse();
                            foreach (Test test in tests)
                            {
                                DrawQueueTest(GUILayoutUtility.GetRect(GUIContent.none, Style.Get("GUIQueue/Test")), test, queue, paintResultFeatures);
                            }
                        }
                    }
                    EditorGUILayout.EndScrollView();
                }
                EditorGUILayout.EndVertical();

                GUILayout.Space(Style.Get("GUIQueue/Queue").margin.bottom);
            }
            EditorGUILayout.EndVertical();

            return scrollPosition;
        }

        private void DrawQueueRunning()
        {
            GUILayout.BeginVertical();
            {
                GUILayout.BeginHorizontal();
                {
                    float previousLabelWidth = EditorGUIUtility.labelWidth;
                    //EditorGUIUtility.labelWidth = 0f;
                    GUILayout.Label("Running", Style.Get("GUIQueue/Queue/Title"), GUILayout.Width(Style.GetWidth("GUIQueue/Queue/Title", "Running")));

                    GUILayout.FlexibleSpace();

                    GUILayout.Label("frame " + string.Format("{0,8}", ui.manager.nframes) + "    " + ui.manager.timer.ToString("0.0000 s"));

                    EditorGUIUtility.labelWidth = previousLabelWidth;
                }
                GUILayout.EndHorizontal();

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
            GUILayout.EndVertical();
        }

        private void DrawQueueTest(Rect rect, Test test, List<Test> queue, bool paintResultFeatures = true)
        {
            if (paintResultFeatures)
            {
                rect = TestManagerUI.PaintResultFeatures(rect, test.result);
            }

            GUIContent content = Style.GetIcon("GUIQueue/Test/Remove/Button", "Remove test from queue");
            GUIStyle style = Style.Get("GUIQueue/Test/Remove/Button");

            Rect controlsRect = new Rect(rect);

            controlsRect.width = Style.GetWidth(style, content);
            if (GUI.Button(Style.ApplyMargins(controlsRect, style), content, style)) queue.Remove(test); // X button
            rect.width -= controlsRect.width;
            rect.x += controlsRect.width;

            string s = test.attribute.GetPath();
            GUI.Label(rect, s, Style.GetTextOverflowAlignmentStyle(rect, Style.Get("GUIQueue/Test"), s, TextAnchor.MiddleRight));
        }


        private void DrawQueueTest(Rect rect, Test test, bool paintResultFeatures = true)
        {
            if (paintResultFeatures) rect = TestManagerUI.PaintResultFeatures(rect, test.result);
            GUI.Label(rect, test.attribute.GetPath(), Style.Get("GUIQueue/Test"));
        }


        private void DrawSplitter()
        {
            GUILayout.BeginHorizontal(Style.Get("GUIQueue/Toolbar"));
            {
                GUIContent label = new GUIContent("Tests");
                GUIStyle labelStyle = Style.Get("GUIQueue/Toolbar/BoldLabel");
                GUILayout.Label(label, labelStyle, GUILayout.Width(Style.GetWidth(labelStyle, label)));

                GUILayout.BeginHorizontal(Style.Get("GUIQueue/Toolbar/Label"), GUILayout.ExpandWidth(true));
                {
                    GUILayout.Space(GUI.skin.box.padding.left);
                    GUILayout.BeginVertical();
                    {
                        GUILayout.FlexibleSpace();
                        GUILayout.Box(GUIContent.none, Style.Get("GUIQueue/Toolbar/Splitter"), GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
                        GUILayout.FlexibleSpace();
                    }
                    GUILayout.EndVertical();
                    GUILayout.Space(GUI.skin.box.padding.right);
                }
                GUILayout.EndHorizontal();

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
            GUILayout.EndHorizontal();
        }


        private void ProcessEvents()
        {
            if (Event.current == null) return;

            if (Utilities.IsMouseButtonPressed() && Utilities.IsMouseOverRect(splitterRect))
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