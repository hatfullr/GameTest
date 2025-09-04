using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace GameTest
{
    /// <summary>
    /// The box that shows the Foldouts and Tests
    /// </summary>
    public class TestManagerTestView
    {
        public static void UpdateFoldoutStates(TestManager manager)
        {
            foreach (Foldout foldout in manager.foldouts) foldout.UpdateState(manager);
        }

        #region Welcome message
        private static float GetWelcomeHeight(TestManagerUI ui)
        {
            GetWelcomeRects(ui, out Rect _, out Rect _, out Rect bgRect, out Rect[] _);
            return bgRect.height;
        }

        private static void GetWelcomeRects(TestManagerUI ui, out Rect title, out Rect body, out Rect bg, out Rect[] links)
        {
            // Setup styles and content
            GUIContent message = new GUIContent(Style.welcomeMessage);
            GUIContent donate = Style.GetIcon("TestManagerUI/Donate");
            GUIContent doc = Style.GetIcon("TestManagerUI/Documentation");

            GUIStyle welcomeStyle = Style.Get("TestManagerUI/Welcome");
            GUIStyle titleStyle = Style.Get("TestManagerUI/Welcome/Title");
            GUIStyle messageStyle = Style.Get("TestManagerUI/Welcome/Message");
            GUIStyle donateStyle = Style.Get("TestManagerUI/Donate");
            GUIStyle docStyle = Style.Get("TestManagerUI/Documentation");

            // Setup stuff relating to the link buttons
            const int nLinks = 2;
            GUIStyle[] linkStyles = new GUIStyle[nLinks] { donateStyle, docStyle };
            GUIContent[] linkContent = new GUIContent[nLinks] { donate, doc };
            RectOffset[] padding = new RectOffset[nLinks];
            for (int i = 0; i < nLinks; i++) padding[i] = linkStyles[i].margin;

            links = new Rect[nLinks];
            for (int i = 0; i < nLinks; i++) links[i] = new Rect(Vector2.zero, linkStyles[i].CalcSize(linkContent[i]));

            // Setup Rects
            title = new Rect(ui.viewRect.x, ui.viewRect.y, ui.viewRect.width, 0f);
            title.height = 0;
            for (int i = 0; i < nLinks; i++) title.height = Mathf.Max(title.height, links[i].height);

            body = new Rect(
                ui.viewRect.x,
                title.yMax,
                ui.viewRect.width,
                messageStyle.CalcHeight(message, title.width) + messageStyle.padding.vertical
            );

            bg = new Rect(ui.viewRect.x, ui.viewRect.y, ui.viewRect.width, title.height + body.height);
            bg.y -= welcomeStyle.padding.top; // This hides the top part of the background, making it look kinda like a tab in the UI
            bg.height += welcomeStyle.padding.top;

            // Apply margins
            float dy = titleStyle.margin.bottom + messageStyle.margin.top;
            body.y += dy;
            bg.height += dy;

            // Alignment
            links = Utilities.AlignRects(
                links,
                title,
                Utilities.RectAlignment.LowerRight,
                Utilities.RectAlignment.MiddleLeft,
                padding: padding
            );

            title = Utilities.GetPaddedRect(title, titleStyle.padding);
            for (int i = 0; i < nLinks; i++)
            {
                links[i] = Utilities.GetPaddedRect(links[i], EditorStyles.linkLabel.padding);
            }

            body = Utilities.GetPaddedRect(body, messageStyle.padding);
        }
        #endregion

        private static float GetListHeight(TestManagerUI ui)
        {
            TestManagerUI.Mode mode = ui.GetMode();
            float height = 0f;

            if (mode == TestManagerUI.Mode.Normal)
            {
                foreach (Foldout foldout in Foldout.GetVisible(ui))
                {
                    height += Style.lineHeight;
                    if (foldout.expanded)
                    {
                        height += Style.lineHeight * foldout.tests.Count;
                        if (foldout.tests.Count > 0) height += Style.TestManagerUI.foldoutMargin;
                    }
                }
                height += Style.TestManagerUI.foldoutMargin; // a bit of extra space at the bottom looks cleanest
            }
            else if (mode == TestManagerUI.Mode.Search)
            {
                foreach (Test test in ui.manager.searchMatches)
                {
                    height += Style.lineHeight;
                }
            }
            else throw new System.NotImplementedException("Unrecognized Mode " + mode);

            return height;
        }

        /// <summary>
        /// Expand foldouts as necessary so that the given Test can be seen. If the Test is out of the scroll view, this will scroll the view to make the Test visible.
        /// Does not ping the test. See the PingTest method.
        /// </summary>
        public static void RevealTest(TestManagerUI ui, Test test)
        {
            ui.manager.testToReveal = test.attribute.GetPath();
            foreach (Foldout parent in test.GetParentFoldouts(ui.manager))
            {
                parent.expanded = true;
            }
            ui.change = new TestManagerUI.Change(null, TestManagerUI.UIEvent.RevealTest);
            ui.Repaint();
        }

        #region Drawers
        public static void Draw(TestManagerUI ui)
        {
            GUIStyle style = Style.Get("TestManagerUI/TestView");
            EditorGUILayout.VerticalScope scrollScope = new EditorGUILayout.VerticalScope(style, GUILayout.ExpandHeight(true), GUILayout.ExpandWidth(true));
            using (scrollScope)
            {
                ui.indentLevel = 0;

                ui.viewRect.x = 0f;
                ui.viewRect.y = 0f;
                ui.viewRect.width = Mathf.Max(ui.minWidth, scrollScope.rect.width);
                ui.viewRect.height = GetListHeight(ui);

                if (ui.manager.showWelcome) ui.viewRect.height += GetWelcomeHeight(ui);

                if (ui.viewRect.height > scrollScope.rect.height) // This means the vertical scrollbar is visible
                {
                    ui.viewRect.width -= GUI.skin.verticalScrollbar.CalcSize(GUIContent.none).x;
                }

                ui.scrollRect = GUIUtility.GUIToScreenRect(scrollScope.rect);
                GUI.ScrollViewScope scrollViewScope = new GUI.ScrollViewScope(
                    scrollScope.rect,
                    ui.manager.scrollPosition,
                    ui.viewRect,
                    false,
                    false,
                    GUI.skin.horizontalScrollbar,
                    GUI.skin.verticalScrollbar
                );
                using (scrollViewScope)
                {
                    ui.manager.scrollPosition = scrollViewScope.scrollPosition;

                    ui.itemRect = new Rect(ui.viewRect.x, ui.viewRect.y, ui.viewRect.width, Style.lineHeight);

                    if (ui.manager.showWelcome)
                    {
                        Rect welcomeRect = DrawWelcome(ui); // Welcome message
                        ui.itemRect.y = welcomeRect.yMax;
                    }

                    // Apply padding
                    ui.itemRect.x += style.padding.left;
                    ui.itemRect.y += style.padding.top;
                    ui.itemRect.width -= style.padding.horizontal;

                    using (new EditorGUI.DisabledScope(ui.manager.running))
                    {
                        ui.drawingMainView = true;
                        if (string.IsNullOrEmpty(ui.manager.search)) DrawNormalMode(ui);
                        else DrawSearchMode(ui);
                        ui.drawingMainView = false;
                    }
                }
            }
        }

        private static Rect DrawWelcome(TestManagerUI ui)
        {
            // Setup styles and content
            GUIContent icon = Style.GetIcon("TestManagerUI/Welcome");
            GUIContent title = new GUIContent(Style.welcomeTitle, icon.image);
            GUIContent message = new GUIContent(Style.welcomeMessage);
            GUIContent donate = Style.GetIcon("TestManagerUI/Donate");
            GUIContent doc = Style.GetIcon("TestManagerUI/Documentation");

            GUIStyle welcomeStyle = Style.Get("TestManagerUI/Welcome");
            GUIStyle titleStyle = Style.Get("TestManagerUI/Welcome/Title");
            GUIStyle messageStyle = Style.Get("TestManagerUI/Welcome/Message");

            const int nLinks = 2;
            string[] links = new string[nLinks] { Style.donationLink, Style.documentationLink };
            GUIContent[] linkContent = new GUIContent[nLinks] { donate, doc };

            float dy = titleStyle.margin.bottom + messageStyle.margin.top;

            GetWelcomeRects(ui, out Rect titleRect, out Rect body, out Rect bgRect, out Rect[] linkRects);

            Color bg = Color.black * 0.2f;

            // Drawing
            GUI.Box(bgRect, GUIContent.none, welcomeStyle);
            EditorGUI.DrawRect(titleRect, bg);
            EditorGUI.DrawRect(new Rect(titleRect.x, titleRect.yMax, titleRect.width, dy), bg + new Color(0f, 0f, 0f, 0.1f));

            using (new EditorGUIUtility.IconSizeScope(new Vector2(titleRect.height - welcomeStyle.padding.vertical, titleRect.height - welcomeStyle.padding.vertical)))
            {
                EditorGUI.LabelField(titleRect, title, titleStyle);
            }

            for (int i = 0; i < linkRects.Length; i++)
            {
                if (EditorGUI.LinkButton(linkRects[i], linkContent[i])) Application.OpenURL(links[i]);
            }

            EditorGUI.LabelField(body, message, messageStyle);

            // DEBUGGING
            //foreach (System.Tuple<Rect, Color> kvp in new System.Tuple<Rect, Color>[]
            //{
            //new System.Tuple<Rect,Color>(bgRect,    Color.green),
            //new System.Tuple<Rect,Color>(titleRect, Color.red),
            //new System.Tuple<Rect,Color>(body,      Color.yellow),
            //new System.Tuple<Rect,Color>(viewRect,  Color.cyan)
            //}) Utilities.DrawDebugOutline(kvp.Item1, kvp.Item2);

            return bgRect;
        }

        /// <summary>
        /// Draw the tests as nested foldouts in a hierarchy according to their individual paths.
        /// </summary>
        private static void DrawNormalMode(TestManagerUI ui)
        {
            foreach (Foldout foldout in ui.manager.foldouts)
                if (foldout.IsRoot()) foldout.Draw(ui);
            ui.manager.pingData.HandlePing(ui);
        }

        /// <summary>
        /// Shows the tests as their full paths when text is present in the search bar. Only shows the tests matching the search regex.
        /// </summary>
        private static void DrawSearchMode(TestManagerUI ui)
        {
            Regex re = new Regex(ui.manager.search, RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.IgnorePatternWhitespace);
            MatchCollection matches;
            string path, final;
            bool dummy = false;
            foreach (Test match in new List<Test>(ui.manager.searchMatches))
            {
                path = match.attribute.GetPath();
                matches = re.Matches(path);

                // Modify the color or something of the regex matches to show where the matches happened
                final = "";
                for (int i = 0; i < matches.Count; i++)
                {
                    if (i == 0) final += path[..matches[i].Index];
                    else final += path[(matches[i - 1].Index + matches[i - 1].Length)..matches[i].Index];
                    final += "<b>" + path[matches[i].Index..(matches[i].Index + matches[i].Length)] + "</b>";
                }
                final += path[(matches[matches.Count - 1].Index + matches[matches.Count - 1].Length)..];

                ui.DrawListItem(ui.itemRect, match, ref dummy, ref match.locked, ref match.selected,
                    showFoldout: false,
                    showScript: true,
                    showLock: true,
                    showToggle: true,
                    showResultBackground: true,
                    showClearResult: true,
                    showResult: true,
                    showGoTo: true,
                    showSettings: false,
                    name: final
                );
                ui.itemRect.y += ui.itemRect.height;
            }
        }

        #endregion
    }
}