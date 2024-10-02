using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace UnityTest
{
    /// <summary>
    /// Handles all the GUI styling.
    /// </summary>
    public static class Style
    {
        private static Dictionary<string, GUIStyle> styles = new Dictionary<string, GUIStyle>();
        private static Dictionary<string, GUIContent> icons = new Dictionary<string, GUIContent>();

        public static class TestManagerUI
        {
            public const string windowTitle = "UnityTest Manager";
            public const float minHeight = 300f;
            public const float minWidth = 500f;
            public const float spinRate = 0.05f;
            public const float scriptWidth = 150f;
        }
        public static class GUIQueue
        {
            public const float minHeight = 200f;
            //public const float minWidth = 300f;
        }

        public static GUIStyle Get(string style)
        {
            if (!styles.ContainsKey(style)) InitializeStyle(style);
            return styles[style];
        }

        public static GUIContent GetIcon(string iconName, string tooltip = null)
        {
            if (!icons.ContainsKey(iconName)) InitializeIcon(iconName);
            if (tooltip != null)
            {
                GUIContent icon = new GUIContent(icons[iconName]);
                if (Utilities.isDarkTheme && !icon.image.name.StartsWith("d_"))
                {
                    icon.image = EditorGUIUtility.IconContent("d_" + icon.image.name).image;
                }
                if (icon.tooltip != null) icon.tooltip = tooltip;
                return icon;
            }
            return icons[iconName];
        }

        /// <summary>
        /// Get the pixel width of the given GUIContent as used with the given GUIStyle.
        /// </summary>
        public static float GetWidth(GUIStyle style) => style.CalcSize(GUIContent.none).x;
        public static float GetWidth(GUIStyle style, GUIContent content) => style.CalcSize(content).x;
        public static float GetWidth(GUIStyle style, string content) => GetWidth(style, new GUIContent(content));
        public static float GetWidth(string style) => GetWidth(Get(style));
        public static float GetWidth(string style, GUIContent content) => GetWidth(Get(style), content);
        public static float GetWidth(string style, string content) => GetWidth(Get(style), new GUIContent(content));

        public static Rect GetRect(GUIStyle style) => GUILayoutUtility.GetRect(GUIContent.none, style);
        public static Rect GetRect(GUIStyle style, GUIContent content) => GUILayoutUtility.GetRect(content, style);
        public static Rect GetRect(string style, GUIContent content) => GetRect(Get(style), content);
        public static Rect GetRect(GUIStyle style, string content) => GetRect(style, new GUIContent(content));
        public static Rect GetRect(string style, string content) => GetRect(Get(style), new GUIContent(content));


        private static void InitializeStyle(string style)
        {
            GUIStyle s;
            switch (style)
            {
                #region TestManagerUI
                case "TestManagerUI/TestView":
                    s = new GUIStyle(EditorStyles.inspectorFullWidthMargins);
                    break;

                case "TestManagerUI/LoadingWheel":
                    s = new GUIStyle(EditorStyles.largeLabel);
                    s.alignment = TextAnchor.MiddleCenter;
                    s.imagePosition = ImagePosition.ImageAbove;
                    break;

                #region Toolbar
                case "TestManagerUI/Toolbar":
                    s = new GUIStyle(EditorStyles.toolbar);
                    break;

                case "TestManagerUI/Toolbar/Toggle/On":
                    s = new GUIStyle(EditorStyles.toolbarButton);
                    break;
                case "TestManagerUI/Toolbar/Toggle/Off":
                    s = new GUIStyle(Get("TestManagerUI/Toolbar/Toggle/On"));
                    break;
                case "TestManagerUI/Toolbar/Toggle/Mixed":
                    s = new GUIStyle(Get("TestManagerUI/Toolbar/Toggle/On"));
                    break;
                case "TestManagerUI/Toolbar/Clear":
                    s = new GUIStyle(EditorStyles.toolbarDropDown);
                    break;
                case "TestManagerUI/Toolbar/Play":
                    s = new GUIStyle(EditorStyles.toolbarButton);
                    break;
                case "TestManagerUI/Toolbar/Pause":
                    s = new GUIStyle(EditorStyles.toolbarButton);
                    break;
                case "TestManagerUI/Toolbar/GoToEmptyScene":
                    s = new GUIStyle(EditorStyles.toolbarButton);
                    break;
                case "TestManagerUI/Toolbar/Debug":
                    s = new GUIStyle(EditorStyles.toolbarButton);
                    break;
                case "TestManagerUI/Toolbar/Refresh":
                    s = new GUIStyle(EditorStyles.toolbarButton);
                    break;
                case "TestManagerUI/Toolbar/Welcome":
                    s = new GUIStyle(EditorStyles.toolbarButton);
                    break;
                #endregion Toolbar

                #endregion TestManagerUI

                #region Test
                case "Test/Foldout":
                    s = new GUIStyle(EditorStyles.foldout);
                    s.contentOffset = Vector2.zero;
                    break;
                case "Test/Lock":
                    s = new GUIStyle("IN LockButton");
                    s.fixedHeight = Get("Test/Toggle").fixedHeight; // fill to the same height as the toggle
                    break;
                case "Test/Toggle":
                    s = new GUIStyle(EditorStyles.iconButton);
                    GUIStyle s2 = new GUIStyle(EditorStyles.toggle);
                    s.alignment = s2.alignment;
                    s.fixedWidth = s2.fixedWidth;
                    s.fixedHeight = s2.fixedHeight;
                    s.font = s2.font;
                    s.fontStyle = s2.fontStyle;
                    s.fontSize = s2.fontSize;
                    s.clipping = s2.clipping;
                    s.border = s2.border;
                    s.contentOffset = s2.contentOffset;
                    s.imagePosition = s2.imagePosition;
                    s.margin = s2.margin;
                    s.overflow = s2.overflow;
                    s.padding = s2.padding;
                    s.richText = s2.richText;
                    s.stretchHeight = s2.stretchHeight;
                    s.stretchWidth = s2.stretchWidth;
                    s.wordWrap = s2.wordWrap;
                    s.padding = new RectOffset(s.padding.right, s.padding.right, s.padding.top, s.padding.bottom);
                    s.margin = new RectOffset((int)GetWidth(EditorStyles.toggle), s.margin.right, s.margin.top, s.margin.bottom);
                    break;
                case "Test/Expanded":
                    s = new GUIStyle("GroupBox");
                    s.padding = GUI.skin.label.padding;
                    s.margin = new RectOffset(0, (int)(0.5f * s.border.right), 0, 0);
                    s.padding.left = (int)(GetWidth("Test/Toggle") * 2);
                    s.padding.right -= s.margin.right;
                    break;
                case "Test/Result":
                    s = new GUIStyle(GUIStyle.none);
                    break;
                case "Test/ClearResult":
                    s = new GUIStyle(EditorStyles.iconButton);
                    s.fixedHeight = Get("Test/Toggle").fixedHeight;
                    break;
                case "Test/Suite/SettingsButton":
                    s = new GUIStyle(EditorGUIUtility.GetBuiltinSkin(EditorSkin.Inspector).FindStyle("IconButton"));
                    break;
                #endregion Test


                #region Foldout
                case "Foldout":
                    s = new GUIStyle(EditorStyles.foldout);
                    break;

                #endregion Foldout

                #region GUIQueue
                case "GUIQueue/Toolbar":
                    s = new GUIStyle("PreToolbar");
                    break;
                case "GUIQueue/Toolbar/Splitter":
                    s = new GUIStyle("WindowBottomResize");
                    break;
                case "GUIQueue/Toolbar/Label":
                    s = new GUIStyle(EditorStyles.label);
                    s.alignment = TextAnchor.MiddleLeft;
                    s.padding = new RectOffset(0, 0, 0, 0);
                    //s.wordWrap = true;
                    break;
                case "GUIQueue/Test":
                    s = new GUIStyle(EditorStyles.label);
                    s.alignment = TextAnchor.MiddleLeft;
                    s.padding = new RectOffset(0, 0, 0, 0);
                    s.clipping = TextClipping.Clip;
                    break;
                case "GUIQueue/Toolbar/BoldLabel":
                    s = new GUIStyle(EditorStyles.boldLabel);
                    break;
                case "GUIQueue/Toolbar/Button":
                    s = new GUIStyle(EditorStyles.toolbarButton);
                    break;
                case "GUIQueue/Queue":
                    s = new GUIStyle("HelpBox");
                    break;
                case "GUIQueue/Queue/Title":
                    s = new GUIStyle(EditorStyles.boldLabel);
                    break;
                case "GUIQueue/Test/Remove/Button":
                    s = new GUIStyle(EditorStyles.iconButton);
                    break;
                #endregion GUIQueue

                default: // Accesses the built-in GUIStyles by their string names
                    throw new System.NotImplementedException("Unrecognized style '" + style + "'");
            }
            styles[style] = s;
        }

        private static void InitializeIcon(string icon)
        {
            GUIContent c = new GUIContent(GUIContent.none);
            switch (icon)
            {
                #region TestManagerUI
                #region Loading Wheel
                case "TestManagerUI/LoadingWheel/0":
                    c = new GUIContent(EditorGUIUtility.IconContent("WaitSpin00"));
                    break;
                case "TestManagerUI/LoadingWheel/1":
                    c = new GUIContent(EditorGUIUtility.IconContent("WaitSpin01"));
                    break;
                case "TestManagerUI/LoadingWheel/2":
                    c = new GUIContent(EditorGUIUtility.IconContent("WaitSpin02"));
                    break;
                case "TestManagerUI/LoadingWheel/3":
                    c = new GUIContent(EditorGUIUtility.IconContent("WaitSpin03"));
                    break;
                case "TestManagerUI/LoadingWheel/4":
                    c = new GUIContent(EditorGUIUtility.IconContent("WaitSpin04"));
                    break;
                case "TestManagerUI/LoadingWheel/5":
                    c = new GUIContent(EditorGUIUtility.IconContent("WaitSpin05"));
                    break;
                case "TestManagerUI/LoadingWheel/6":
                    c = new GUIContent(EditorGUIUtility.IconContent("WaitSpin06"));
                    break;
                case "TestManagerUI/LoadingWheel/7":
                    c = new GUIContent(EditorGUIUtility.IconContent("WaitSpin07"));
                    break;
                case "TestManagerUI/LoadingWheel/8":
                    c = new GUIContent(EditorGUIUtility.IconContent("WaitSpin08"));
                    break;
                case "TestManagerUI/LoadingWheel/9":
                    c = new GUIContent(EditorGUIUtility.IconContent("WaitSpin09"));
                    break;
                case "TestManagerUI/LoadingWheel/10":
                    c = new GUIContent(EditorGUIUtility.IconContent("WaitSpin10"));
                    break;
                case "TestManagerUI/LoadingWheel/11":
                    c = new GUIContent(EditorGUIUtility.IconContent("WaitSpin11"));
                    break;
                #endregion Loading Wheel

                #region Toolbar
                case "TestManagerUI/Toolbar/Toggle/On":
                    c = new GUIContent(EditorGUIUtility.IconContent("toggle_on"));
                    break;
                case "TestManagerUI/Toolbar/Toggle/On/Hover":
                    c = new GUIContent(EditorGUIUtility.IconContent("toggle_on_hover"));
                    break;
                case "TestManagerUI/Toolbar/Toggle/Off":
                    c = new GUIContent(EditorGUIUtility.IconContent("toggle_bg"));
                    break;
                case "TestManagerUI/Toolbar/Toggle/Off/Hover":
                    c = new GUIContent(EditorGUIUtility.IconContent("toggle_bg_hover"));
                    break;
                case "TestManagerUI/Toolbar/Toggle/Mixed":
                    c = new GUIContent(EditorGUIUtility.IconContent("toggle_mixed_bg"));
                    break;
                case "TestManagerUI/Toolbar/Toggle/Mixed/Hover":
                    c = new GUIContent(EditorGUIUtility.IconContent("toggle_mixed_bg_hover"));
                    break;

                case "TestManagerUI/Toolbar/Clear":
                    c = new GUIContent(EditorGUIUtility.IconContent("clear"));
                    c.tooltip = "Clear selected Test results";
                    break;

                case "TestManagerUI/Toolbar/Play/Off":
                    c = new GUIContent(EditorGUIUtility.IconContent("PlayButton"));
                    c.tooltip = "Run selected tests";
                    break;
                case "TestManagerUI/Toolbar/Play/On":
                    c = new GUIContent(EditorGUIUtility.IconContent("PlayButton On"));
                    c.tooltip = "Stop testing";
                    break;

                case "TestManagerUI/Toolbar/Pause/Off":
                    c = new GUIContent(EditorGUIUtility.IconContent("PauseButton"));
                    c.tooltip = "Pause testing";
                    break;
                case "TestManagerUI/Toolbar/Pause/On":
                    c = new GUIContent(EditorGUIUtility.IconContent("PauseButton On"));
                    c.tooltip = "Resume testing";
                    break;

                case "TestManagerUI/Toolbar/GoToEmptyScene":
                    c = new GUIContent(EditorGUIUtility.IconContent("SceneLoadIn"));
                    c.tooltip = "Go to empty scene";
                    break;

                case "TestManagerUI/Toolbar/Debug/Off":
                    c = new GUIContent(EditorGUIUtility.IconContent("DebuggerDisabled"));
                    c.tooltip = "Enable/disable debug messages";
                    break;
                case "TestManagerUI/Toolbar/Debug/On":
                    c = new GUIContent(EditorGUIUtility.IconContent("DebuggerAttached"));
                    c.tooltip = "Enable/disable debug messages";
                    break;

                case "TestManagerUI/Toolbar/Refresh":
                    c = new GUIContent(EditorGUIUtility.IconContent("Refresh"));
                    c.tooltip = "Refresh Test methods and classes by searching all assemblies";
                    break;

                case "TestManagerUI/Toolbar/Welcome":
                    c = new GUIContent(EditorGUIUtility.IconContent("console.infoicon.sml"));
                    c.tooltip = "Show/hide the welcome message";
                    break;
                #endregion Toolbar

                #endregion TestManagerUI

                #region Test
                case "Test/Result/None":
                    c = new GUIContent(EditorGUIUtility.IconContent("TestNormal"));
                    break;
                case "Test/Result/Pass":
                    c = new GUIContent(EditorGUIUtility.IconContent("TestPassed"));
                    c.tooltip = "Passed";
                    break;
                case "Test/Result/Fail":
                    c = new GUIContent(EditorGUIUtility.IconContent("TestFailed"));
                    c.tooltip = "Failed";
                    break;
                case "Test/ClearResult":
                    c = new GUIContent(EditorGUIUtility.IconContent("clear"));
                    c.tooltip = "Clear test result";
                    break;
                case "Test/Suite/SettingsButton":
                    c = new GUIContent(EditorGUIUtility.IconContent("_Popup"));
                    break;
                #endregion Test

                #region GUIQueue
                case "GUIQueue/Toolbar/Options":
                    c = new GUIContent(EditorGUIUtility.IconContent("pane options"));
                    break;
                case "GUIQueue/Test/Remove/Button":
                    c = new GUIContent(EditorGUIUtility.IconContent("clear"));
                    break;
                #endregion
                default:
                    throw new System.NotImplementedException("Unrecognized icon name '" + icon + "'");
            }
            icons[icon] = c;
        }
    }
}