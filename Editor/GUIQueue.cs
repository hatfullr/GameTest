using UnityEditor;
using UnityEngine;

namespace UnityTest
{
    /// <summary>
    /// This is where the queued and finished tests in the TestManagerUI are drawn
    /// </summary>
    public class GUIQueue
    {
        public const float minHeight = 200f;
        private const string delimiter = "-~!@ delim @!~-";

        private Vector2 queueScrollPosition, finishedScrollPosition;

        private float height = minHeight;

        private Rect splitterRect, mainRect;
        private bool dragging;
        private bool hideMain = false;
        private Vector2 dragPos;

        private TestManager manager;

        public GUIQueue(TestManager manager, string data = null)
        {
            this.manager = manager;
            FromString(data);
        }

        public void FromString(string data)
        {
            if (string.IsNullOrEmpty(data)) return;

            string[] split = data.Split(delimiter);

            try { height = float.Parse(split[0]); }
            catch (System.Exception e)
            {
                if (!(e is System.FormatException || e is System.IndexOutOfRangeException)) throw e;
            }

            try { hideMain = bool.Parse(split[1]); }
            catch (System.Exception e)
            {
                if (!(e is System.FormatException || e is System.IndexOutOfRangeException)) throw e;
            }
        }

        public void Reset()
        {
            splitterRect = new Rect();
            mainRect = new Rect();
            queueScrollPosition = Vector2.zero;
            finishedScrollPosition = Vector2.zero;
            height = minHeight;
            hideMain = false;
        }

        public string GetString()
        {
            return string.Join(delimiter,
                height,
                hideMain
            );
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

                // Get the width here so we can properly size the controls to come
                Rect previous = GUILayoutUtility.GetLastRect();

                // "Queue" space
                EditorGUILayout.BeginHorizontal();
                {
                    DrawQueue("Queued", manager.queue, queueScrollPosition);
                    DrawQueue("Finished", manager.finishedTests, finishedScrollPosition, true);
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
                    EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
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
                EditorGUILayout.BeginVertical("HelpBox", GUILayout.ExpandWidth(false));
                {
                    scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, false, false);
                    {
                        if (queue != null)
                        {
                            foreach (Test test in queue.tests)
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
                    float width = EditorStyles.boldLabel.CalcSize(new GUIContent("Current")).x;

                    EditorGUILayout.LabelField("Current", EditorStyles.boldLabel, GUILayout.Width(width));
                    GUILayout.FlexibleSpace();
                    GUI.enabled = false;
                    GUILayout.Label("frame " + string.Format("{0,8}", manager.nframes) + "    " + manager.timer.ToString("0.0000 s"));
                    GUI.enabled = wasEnabled;

                    EditorGUIUtility.labelWidth = previousLabelWidth;
                }
                EditorGUILayout.EndHorizontal();

                Rect rect = EditorGUILayout.GetControlRect(false);
                rect.height += EditorStyles.helpBox.padding.vertical;
                GUI.Box(rect, GUIContent.none, "HelpBox");
                rect.x += EditorStyles.helpBox.padding.left;
                rect.width -= EditorStyles.helpBox.padding.horizontal;
                rect.y += EditorStyles.helpBox.padding.top;
                rect.height -= EditorStyles.helpBox.padding.vertical;
                if (Test.current != null) DrawQueueTest(rect, Test.current);
            }
            EditorGUILayout.EndVertical();
            GUI.enabled = wasEnabled;
        }

        private void DrawQueueTest(Rect rect, Test test, Queue queue, bool paintResultFeatures = true)
        {
            bool wasEnabled = GUI.enabled;

            GUIStyle labelStyle = new GUIStyle(EditorStyles.label);
            labelStyle.alignment = TextAnchor.MiddleLeft;
            labelStyle.padding = new RectOffset(0, 0, 0, 0);

            if (paintResultFeatures) Test.PaintResultFeatures(rect, test.result);

            GUIContent content = new GUIContent(EditorGUIUtility.IconContent("d_clear"));
            content.tooltip = "Remove test from queue";

            GUIStyle style = new GUIStyle(EditorStyles.iconButton);

            Rect controlsRect = new Rect(rect);

            // Center the button
            controlsRect.y = rect.center.y - 0.5f * style.CalcSize(content).y;

            controlsRect.width = style.CalcSize(content).x;
            if (GUI.Button(controlsRect, content, style)) // X button
            {
                queue.Remove(test);
            }
            rect.width -= controlsRect.width;
            rect.x += controlsRect.width;
            GUI.Label(rect, test.attribute.GetPath(), labelStyle);

            GUI.enabled = wasEnabled;
        }


        private void DrawQueueTest(Rect rect, Test test, bool paintResultFeatures = true)
        {
            bool wasEnabled = false;
            //GUI.enabled = false;

            GUIStyle labelStyle = new GUIStyle(EditorStyles.label);
            labelStyle.alignment = TextAnchor.MiddleLeft;
            labelStyle.padding = new RectOffset(0, 0, 0, 0);

            if (paintResultFeatures) Test.PaintResultFeatures(rect, test.result);
            GUI.Label(rect, test.attribute.GetPath(), labelStyle);

            GUI.enabled = wasEnabled;
        }


        private void DrawSplitter()
        {
            GUIStyle style = new GUIStyle("PreToolbar");
            GUILayout.BeginHorizontal(style);
            {
                GUIContent label = new GUIContent("Tests");
                GUIStyle labelStyle = new GUIStyle("ToolbarBoldLabel");
                GUILayout.Label(label, labelStyle, GUILayout.Width(labelStyle.CalcSize(label).x));

                GUILayout.BeginHorizontal("ToolbarLabel", GUILayout.ExpandWidth(true));
                {
                    GUILayout.Space(GUI.skin.box.padding.left);
                    GUILayout.BeginVertical();
                    {
                        GUILayout.FlexibleSpace();
                        GUILayout.Box(GUIContent.none, "WindowBottomResize", GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
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
                GUIContent options = new GUIContent(EditorGUIUtility.IconContent("pane options"));

                Rect optionsRect = GUILayoutUtility.GetRect(options, EditorStyles.toolbarButton, GUILayout.Width(EditorStyles.toolbarButton.CalcSize(options).x));
                if (EditorGUI.DropdownButton(optionsRect, options, FocusType.Passive, EditorStyles.toolbarButton))
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

                hideMain = Event.current.mousePosition.y > mainRect.yMax - 0.5f * minHeight;

                if (!hideMain)
                {
                    float newHeight = Mathf.Max(mainRect.yMax - (dragPos.y + delta), minHeight);

                    // Make sure that the main window maintains a minimum height
                    if (mainRect.yMax - (newHeight + splitterRect.height) > TestManagerUI.minHeight)
                        height = newHeight;
                }

                EditorWindow.GetWindow<TestManagerUI>().Repaint();
            }
        }
    }
}