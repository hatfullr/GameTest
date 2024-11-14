using UnityEditor;
using UnityEngine;

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
                    ReorderableTestQueue.QueueFieldResult result = ReorderableTestQueue.QueueField(
                        new GUIContent("Selected"),
                        ui.manager.queue,
                        queueScrollPosition,
                        (Rect rect, Test test) =>
                        {
                            bool dummy = false;
                            //Utilities.DrawDebugOutline(ui.itemRect, Color.red);
                            ui.DrawListItem(rect, test, ref dummy, ref dummy, ref dummy,
                                showFoldout: false,
                                showScript: true,
                                showLock: false,
                                showToggle: false,
                                showResultBackground: false,
                                showClearResult: false,
                                showResult: false,
                                showSettings: true,
                                name: test.attribute.GetPath()
                            );
                        },
                        reversed: false,
                        deselectOnClear: true
                    );
                    ui.manager.queue = result.queue;
                    queueScrollPosition = result.scrollPosition;

                    result = ReorderableTestQueue.QueueField(
                        new GUIContent("Finished"),
                        ui.manager.finishedTests,
                        finishedScrollPosition,
                        (Rect rect, Test test) =>
                        {
                            bool dummy = false;
                            //Utilities.DrawDebugOutline(ui.itemRect, Color.red);
                            ui.DrawListItem(rect, test, ref dummy, ref dummy, ref dummy,
                                showFoldout: false,
                                showScript: true,
                                showLock: false,
                                showToggle: false,
                                showResultBackground: true,
                                showClearResult: false,
                                showResult: true,
                                showSettings: false,
                                name: test.attribute.GetPath()
                            );
                        },
                        reversed: true,
                        deselectOnClear: false
                    );
                    ui.manager.finishedTests = result.queue;
                    finishedScrollPosition = result.scrollPosition;
                }
            }

            mainRect = GUILayoutUtility.GetLastRect();

            ProcessEvents();
        }

        private void DrawQueueRunning()
        {
            using (new GUILayout.VerticalScope())
            {
                using (new GUILayout.HorizontalScope())
                {
                    GUILayout.Label("Running", Style.Get("GUIQueue/Queue/Title"), GUILayout.Width(Style.GetWidth("GUIQueue/Queue/Title", "Running")));
                    GUILayout.FlexibleSpace();
                    GUILayout.Label("frame " + string.Format("{0,8}", ui.manager.nframes) + "    " + ui.manager.timer.ToString("0.0000 s"));
                }

                GUIStyle box = Style.Get("GUIQueue/Queue");
                Rect rect = GUILayoutUtility.GetRect(GUIContent.none, box);
                rect.height += box.padding.vertical;
                GUI.Box(rect, GUIContent.none, box);
                rect.x += box.padding.left;
                rect.width -= box.padding.horizontal;
                rect.y += box.padding.top;
                rect.height -= box.padding.vertical;
                if (Test.current != null) GUI.Label(rect, Test.current.attribute.GetPath(), Style.Get("GUIQueue/Test"));
            }
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