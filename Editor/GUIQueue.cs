using UnityEditor;
using UnityEngine;

namespace UnityTest
{
    /// <summary>
    /// This is where the queued and finished tests in the TestManagerUI are drawn
    /// </summary>
    [System.Serializable]
    public class GUIQueue
    {
        [SerializeField] private bool hideMain = false;
        [SerializeField] private float height = Style.GUIQueue.minHeight;
        public float timer;
        public uint nframes;

        private ReorderableTestQueue queue;
        private ReorderableTestQueue finishedQueue;

        private Rect splitterRect, mainRect;
        private bool dragging;
        private Vector2 dragPos;

        private TestManagerUI _ui;
        public TestManagerUI ui
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
            height = Style.GUIQueue.minHeight;
            hideMain = false;
            queue.Clear();
            finishedQueue.Clear();
            ResetTimer();
        }

        public void OnPlayStateChanged(PlayModeStateChange change)
        {
            ResetTimer();
        }

        public void IncrementTimer(float singleFrameDeltaTime)
        {
            timer += singleFrameDeltaTime;
            nframes += 1;
        }

        public void ResetTimer()
        {
            timer = 0f;
            nframes = 0;
        }

        public void Draw()
        {
            if (queue == null)
            {
                Debug.Log("Creating new queue");
                queue = new ReorderableTestQueue(
                ref ui.manager.queue,
                new GUIContent("Selected"),
                testDrawer: (Rect rect, Test test) =>
                {
                    bool dummy = false;
                    ui.DrawListItem(rect, test, ref dummy, ref dummy, ref dummy,
                        showFoldout: false,
                        showScript: true,
                        showLock: false,
                        showToggle: false,
                        showResultBackground: false,
                        showClearResult: false,
                        showResult: false,
                        showSettings: true,
                        showTooltips: true,
                        tooltipOverride: "Click+drag to reorder",
                        name: test.attribute.GetPath()
                    );
                },
                onDrag: ui.Repaint,
                reversed: false,
                deselectOnClear: true
            );
            }
            if (finishedQueue == null) finishedQueue = new ReorderableTestQueue(
                ref ui.manager.finishedTests,
                new GUIContent("Finished"),
                testDrawer: (Rect rect, Test test) =>
                {
                    bool dummy = false;
                    ui.DrawListItem(rect, test, ref dummy, ref dummy, ref dummy,
                        showFoldout: false,
                        showScript: true,
                        showLock: false,
                        showToggle: false,
                        showResultBackground: true,
                        showClearResult: false,
                        showResult: true,
                        showSettings: false,
                        showTooltips: false,
                        name: test.attribute.GetPath()
                    );
                },
                onDrag: ui.Repaint,
                reversed: true,
                deselectOnClear: false,
                allowReorder: false
            );

            DrawSplitter();

            if (hideMain)
            {
                ProcessEvents();
                return;
            }

            // The queue window
            GUIStyle queueStyle = Style.Get("GUIQueue/Queue");

            EditorGUILayout.VerticalScope scope = new EditorGUILayout.VerticalScope(GUILayout.Height(height));
            using (scope)
            {
                DrawQueueRunning();

                EditorGUILayout.Space();

                Rect rect = EditorGUILayout.GetControlRect(false, GUILayout.ExpandHeight(true));

                Rect left = new Rect(rect);
                Rect right = new Rect(rect);
                left.width *= 0.5f;
                right.width *= 0.5f;
                right.x = left.xMax;

                // Apply margins
                left.width -= 0.5f * queueStyle.margin.right;
                right.x += 0.5f * queueStyle.margin.left;
                right.width -= 0.5f * queueStyle.margin.left;

                queue.Draw(left);
                finishedQueue.Draw(right);
            }

            mainRect = scope.rect;

            ProcessEvents();
        }

        private void DrawQueueRunning()
        {
            GUIStyle titleStyle = Style.Get("GUIQueue/Queue/Title");
            GUIStyle frameStyle = Style.Get("GUIQueue/FrameCounter");
            GUIStyle boxStyle = Style.Get("GUIQueue/Queue");
            GUIStyle testStyle = Style.Get("GUIQueue/Test");

            GUIContent title = new GUIContent("Running");
            GUIContent frames = new GUIContent("frame " + string.Format("{0,8}", nframes) + "    " + timer.ToString("0.0000 s"));

            float titleWidth = Style.GetWidth(titleStyle, title);
            float frameWidth = Style.GetWidth(frameStyle, frames);

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(title, titleStyle, GUILayout.Width(titleWidth));
                GUILayout.FlexibleSpace();
                EditorGUILayout.LabelField(frames, frameStyle, GUILayout.Width(frameWidth));
            }
            
            Rect rect = GUILayoutUtility.GetRect(GUIContent.none, boxStyle);
            rect.height += boxStyle.padding.vertical;
            GUI.Box(rect, GUIContent.none, boxStyle);
            rect.x += boxStyle.padding.left;
            rect.width -= boxStyle.padding.horizontal;
            rect.y += boxStyle.padding.top;
            rect.height -= boxStyle.padding.vertical;
            if (Test.current != null) GUI.Label(rect, Test.current.attribute.GetPath(), testStyle);
        }


        private void DrawSplitter()
        {
            GUIStyle boldLabelStyle = Style.Get("GUIQueue/Toolbar/BoldLabel");
            GUIStyle toolbarStyle = Style.Get("GUIQueue/Toolbar");
            GUIStyle labelStyle = Style.Get("GUIQueue/Toolbar/Label");
            GUIStyle splitterStyle = Style.Get("GUIQueue/Toolbar/Splitter");
            GUIStyle optionsStyle = Style.Get("GUIQueue/Toolbar/Button");

            GUIContent boldLabel = new GUIContent("Tests");
            GUIContent options = Style.GetIcon("GUIQueue/Toolbar/Options");

            float boldLabelWidth = Style.GetWidth(boldLabelStyle, boldLabel);

            using (new GUILayout.HorizontalScope(toolbarStyle))
            {
                GUILayout.Label(boldLabel, boldLabelStyle, GUILayout.Width(boldLabelWidth));

                using (new GUILayout.HorizontalScope(labelStyle, GUILayout.ExpandWidth(true)))
                {
                    GUILayout.Space(GUI.skin.box.padding.left);
                    using (new GUILayout.VerticalScope())
                    {
                        GUILayout.FlexibleSpace();
                        GUILayout.Box(GUIContent.none, splitterStyle, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
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
                ui.Repaint();
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

                ui.Repaint();
            }
        }
    }
}