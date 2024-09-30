using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

namespace UnityTest
{
    /// <summary>
    /// This is where the queued and finished tests in the TestManagerUI are drawn
    /// </summary>
    public class GUIQueue
    {
        private const string delimiter = "-~!@ delim @!~-";

        private Vector2 queueScrollPosition, finishedScrollPosition;

        private float height = Style.GUIQueue.minHeight;

        private Rect splitterRect, mainRect;
        private bool dragging;
        private bool hideMain = false;
        private Vector2 dragPos;

        public GUIQueue(string data = null)
        {
            FromString(data);
        }

        public string GetString()
        {
            return string.Join(delimiter,
                height,
                hideMain,
                TestManager.queue.GetString(),
                TestManager.finishedTests.GetString()
            );
        }

        public void FromString(string data)
        {
            if (string.IsNullOrEmpty(data)) return;

            string[] split = data.Split(delimiter);
            try { height = float.Parse(split[0]); }
            catch (System.Exception e) { if (!(e is System.FormatException || e is System.IndexOutOfRangeException)) throw e; }
            try { hideMain = bool.Parse(split[1]); }
            catch (System.Exception e) { if (!(e is System.FormatException || e is System.IndexOutOfRangeException)) throw e; }
            try { TestManager.queue = Queue.FromString(split[2]); }
            catch (System.Exception e) { if (!(e is System.FormatException || e is System.IndexOutOfRangeException)) throw e; }
            try { TestManager.finishedTests = Queue.FromString(split[3]); }
            catch (System.Exception e) { if (!(e is System.FormatException || e is System.IndexOutOfRangeException)) throw e; }
        }

        public void Reset()
        {
            splitterRect = new Rect();
            mainRect = new Rect();
            queueScrollPosition = Vector2.zero;
            finishedScrollPosition = Vector2.zero;
            height = Style.GUIQueue.minHeight;
            hideMain = false;
        }

        

        public void Draw()
        {
            bool wasEnabled = GUI.enabled;

            DrawSplitter();

            if (hideMain)
            {
                ProcessEvents();
                return;
            }

            // The queue window
            EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true), GUILayout.Height(height));
            {
                DrawQueueCurrent();

                EditorGUILayout.Space();

                // "Queue" space
                EditorGUILayout.BeginHorizontal();
                {
                    DrawQueue("Queued", TestManager.queue, queueScrollPosition);
                    DrawQueue("Finished", TestManager.finishedTests, finishedScrollPosition, true);
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndVertical();

            mainRect = GUILayoutUtility.GetLastRect();

            GUI.enabled = wasEnabled;

            ProcessEvents();
        }

        private void DrawQueue(string title, Queue queue, Vector2 scrollPosition, bool paintResultFeatures = false)
        {
            bool wasEnabled = GUI.enabled;
            EditorGUILayout.BeginVertical();
            {
                // header labels for the queue
                EditorGUILayout.BeginHorizontal();
                {
                    EditorGUILayout.LabelField(title, Style.Get("GUIQueue/Queue/Title"));
                    GUILayout.FlexibleSpace();
                    if (queue != null)
                    {
                        GUI.enabled &= queue.Count > 0;
                        if (GUILayout.Button("Clear")) queue.Clear();
                        GUI.enabled = wasEnabled;
                    }
                    else
                    {
                        GUI.enabled = false;
                        GUILayout.Button("Clear");
                        GUI.enabled = wasEnabled;
                    }
                }
                EditorGUILayout.EndHorizontal();


                // Queue area
                EditorGUILayout.BeginVertical(Style.Get("GUIQueue/Queue"));
                {
                    scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, false, false);
                    {
                        if (queue != null)
                        {
                            foreach (Test test in new List<Test>(queue.tests))
                            {
                                Rect rect = EditorGUILayout.GetControlRect(false);
                                DrawQueueTest(rect, test, queue, paintResultFeatures);
                            }
                        }
                    }
                    EditorGUILayout.EndScrollView();
                }
                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndVertical();
            GUI.enabled = wasEnabled;
        }

        private void DrawQueueCurrent()
        {
            bool wasEnabled = GUI.enabled;
            // "Current" space
            EditorGUILayout.BeginVertical();
            {
                EditorGUILayout.BeginHorizontal();
                {
                    float previousLabelWidth = EditorGUIUtility.labelWidth;
                    EditorGUIUtility.labelWidth = 0f;
                    EditorGUILayout.LabelField("Current", Style.Get("GUIQueue/Queue/Title"), GUILayout.Width(Style.GetWidth("GUIQueue/Queue/Title", "Current")));

                    GUILayout.FlexibleSpace();

                    GUI.enabled = false;
                    GUILayout.Label("frame " + string.Format("{0,8}", TestManager.nframes) + "    " + TestManager.timer.ToString("0.0000 s"));
                    GUI.enabled = wasEnabled;

                    EditorGUIUtility.labelWidth = previousLabelWidth;
                }
                EditorGUILayout.EndHorizontal();

                GUIStyle box = Style.Get("GUIQueue/Queue");
                Rect rect = EditorGUILayout.GetControlRect(false);
                rect.height += box.padding.vertical;
                GUI.Box(rect, GUIContent.none, box);
                rect.x += box.padding.left;
                rect.width -= box.padding.horizontal;
                rect.y += box.padding.top;
                rect.height -= box.padding.vertical;
                if (Test.current != null) DrawQueueTest(rect, Test.current);
            }
            EditorGUILayout.EndVertical();
            GUI.enabled = wasEnabled;
        }



        private void DrawQueueTest(Rect rect, Test test, Queue queue, bool paintResultFeatures = true)
        {
            bool wasEnabled = GUI.enabled;
            
            if (paintResultFeatures) rect = TestManagerUI.PaintResultFeatures(rect, test.result);

            GUIContent content = Style.GetIcon("GUIQueue/Test/Remove/Button", "Remove test from queue");
            GUIStyle style = Style.Get("GUIQueue/Test/Remove/Button");

            Rect controlsRect = new Rect(rect);

            // Center the button
            controlsRect.y = rect.center.y - 0.5f * style.CalcSize(content).y;

            controlsRect.width = Style.GetWidth(style, content);
            if (GUI.Button(controlsRect, content, style)) queue.Remove(test); // X button
            rect.width -= controlsRect.width;
            rect.x += controlsRect.width;

            string s = test.attribute.GetPath();
            GUIStyle testStyle = Style.Get("GUIQueue/Test");
            float width = Style.GetWidth(testStyle, s);

            if (width >= rect.width) // If the text is overflowing, cut it off on the left side instead of the right side. Makes it easier to read.
            {
                testStyle = new GUIStyle(testStyle);
                testStyle.alignment = TextAnchor.MiddleRight;
            }

            GUI.Label(rect, s, testStyle);

            GUI.enabled = wasEnabled;
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

                EditorGUIUtility.AddCursorRect(cursorRect, MouseCursor.SplitResizeUpDown);
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

            if (Event.current.rawType == EventType.MouseDown && splitterRect.Contains(Event.current.mousePosition))
            {
                dragging = true;
                dragPos = Event.current.mousePosition;
            }
            else if (Event.current.rawType == EventType.MouseUp)
            {
                dragging = false;
                EditorWindow.GetWindow<TestManagerUI>().Repaint();
            }


            if (dragging && Event.current.rawType == EventType.MouseDrag)
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