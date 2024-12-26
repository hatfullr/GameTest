using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace GameTest
{
    /// <summary>
    /// Handles all the GUI styling.
    /// </summary>
    public static class Style
    {
        private static Dictionary<string, GUIStyle> styles = new Dictionary<string, GUIStyle>();
        private static Dictionary<string, GUIContent> icons = new Dictionary<string, GUIContent>();

        public const string donationLink = "https://ko-fi.com/rogerhatfull";
        public const string documentationLink = "https://github.com/hatfullr/GameTest";
        public static string welcomeMessage = string.Join('\n',
            "<b>If this tool has helped you, please consider donating to help me create more cool stuff.</b> Thank you for using GameTest.",
            "",
            "<size=14>Getting Started:</size>",
            "<b>1.</b> In any MonoBehaviour, write \"using GameTest;\" at the top, write a method with a \"[Test]\" attribute, and include \"Assert.\" statement(s). Then recompile.",
            "<b>2.</b> After recompiling, find and select your test below to add it to the queue.",
            "<b>3.</b> Click the Play button above (in the toolbar). A test fails if it throws an AssertionException, and passes otherwise. Check the Console for detailed results.",
            "Click the GitHub button or see README.md for additional information.",
            "",
            "<size=14>Tips:</size>",
            "• Click script icons to open test code",
            "• Check tooltips, and Edit > Preferences > Enable PlayMode Tooltips",
            "• Hide GameTest from stack trace with \"Strip logging callstack\" in the triple-dot menu (top right of Console window)",
            "",
            "<i><size=10>To hide this message, press the speech bubble in the toolbar.</size></i>"
        );
        public const string welcomeTitle = "Welcome";

        public static float lineHeight = EditorGUIUtility.singleLineHeight;

        public static class TestManagerUI
        {
            public const string windowTitle = nameof(GameTest);
            public const float minHeight = 300f;
            public const float minWidth = 350f;
            public const float spinRate = 0.05f;
            /// <summary>
            /// The small margin that appears after a Foldout's Tests have been drawn.
            /// </summary>
            public const float foldoutMargin = 4f;
            public const float minTextWidth = 50f;
            public static Color pingColor = new Color(Color.yellow.r, Color.yellow.g, Color.yellow.b, 0.25f);
            public const float pingWaitTime = 2.5f;
            public const float pingFadeInTime = 0.2f;
            public const float pingFadeOutTime = 1.5f;
        }
        public static class GUIQueue
        {
            public const float minHeight = 150f;
            public static Color dragFromColor
            {
                get
                {
                    Color ret = Color.black;
                    if (Utilities.isDarkTheme) ret = Color.white;
                    ret.a = 0.25f;
                    return ret;
                }
            }
            public static Color dragToColor
            {
                get
                {
                    Color ret = Color.cyan;
                    if (Utilities.isDarkTheme) ret.r = 0.5f;
                    else ret *= 0.5f;
                    ret.a = 0.5f;
                    return ret;
                }
            }
            public static Color dragHoverColor
            {
                get
                {
                    Color ret = dragFromColor;
                    ret.a *= 0.5f;
                    return ret;
                }
            }
            public const float dragBarHeight = 1f;
        }

        public static class Tooltips
        {
            public const string clearSelected = "Clear selected test results";
            public const string playOff = "Run selected tests";
            public const string playOn = "Stop testing";
            public const string pauseOff = "Pause testing";
            public const string pauseOn = "Resume testing";
            public const string skip = "Stop the current test and skip to the next";
            public const string goToEmptyScene = "Go to empty scene";
            public const string debugOff = "Enable/disable debug messages";
            public const string debugOn = "Enable/disable debug messages";
            public const string refresh = "Refresh test methods and classes by searching all assemblies. This should never be necessary, but could " +
                        "be helpful if GameTest is having trouble detecting your tests.";
            public const string welcome = "Show/hide the welcome message";
            public const string testPassed = "Passed";
            public const string testFailed = "Failed";
            public const string testSkipped = "Skipped";
            public const string clearTest = "Clear test result";
            public const string goToSearch = "Go to foldout";
            public const string lockButton = "Keep item selected/deselected";
            public const string settings = "Modify Test settings";
            public const string toolbarToggle = "Select/deselect all unlocked tests";
            public const string donate = donationLink + "\nAny amount is greatly appreciated. It keeps me fed :)";
            public const string documentation = documentationLink;
            public const string settingsWindowPrefab = "When this test runs, a copy of this GameObject is instantiated as the testing environment. " +
                "If no GameObject is given, a new GameObject is instantiated with the MonoBehaviour that contains the test method, and " +
                "the properties of the MonoBehaviour are updated to match the properties below.";
        }

        public static Color failColor = new Color(1f, 0f, 0f, 0.1f);
        public static Color passColor = new Color(0, 1f, 0f, 0.1f);
        public static Color skippedColor = new Color(1f, 1f, 0f, 0.1f);

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

        /// <summary>
        /// For a given Rect, return a rect that has been adjusted by the padding of the given GUIStyle.
        /// </summary>
        public static Rect GetPaddedRect(Rect rect, GUIStyle style) => GetHorizontallyPaddedRect(GetVerticallyPaddedRect(rect, style), style);

        public static Rect GetVerticallyPaddedRect(Rect rect, GUIStyle style)
        {
            Rect result = new Rect(rect);
            result.height -= style.padding.vertical;
            result.y += style.padding.top;
            return result;
        }

        public static Rect GetHorizontallyPaddedRect(Rect rect, GUIStyle style)
        {
            Rect result = new Rect(rect);
            result.width -= style.padding.horizontal;
            result.x += style.padding.left;
            return result;
        }

        public static Rect ApplyMargins(Rect rect, GUIStyle style)
        {
            rect.x += style.margin.left;
            rect.y += style.margin.top;
            rect.width -= style.margin.horizontal;
            rect.height -= style.margin.vertical;
            return rect;
        }

        /// <summary>
        /// Reposition the inner rect relative to the outer rect
        /// </summary>
        public static Rect AlignRect(Rect inner, Rect outer, TextAnchor alignment = TextAnchor.MiddleCenter)
        {
            switch (alignment)
            {
                case TextAnchor.LowerLeft:
                    inner.position = new Vector2(outer.x, outer.yMax - inner.height);
                    break;
                case TextAnchor.LowerCenter:
                    inner.center = new Vector2(outer.center.x, outer.yMax - inner.height);
                    break;
                case TextAnchor.LowerRight:
                    inner.position = new Vector2(outer.xMax - inner.width, outer.yMax - inner.height);
                    break;

                case TextAnchor.MiddleLeft:
                    inner.position = new Vector2(outer.x, outer.center.y - 0.5f * inner.height);
                    break;
                case TextAnchor.MiddleCenter:
                    inner.center = outer.center;
                    break;
                case TextAnchor.MiddleRight:
                    inner.position = new Vector2(outer.xMax - inner.width, outer.center.y - 0.5f * inner.height);
                    break;

                case TextAnchor.UpperLeft:
                    inner.position = outer.position;
                    break;
                case TextAnchor.UpperCenter:
                    inner.position = new Vector2(outer.center.x - 0.5f * inner.width, outer.y);
                    break;
                case TextAnchor.UpperRight:
                    inner.position = new Vector2(outer.xMax - inner.width, outer.y);
                    break;

                default:
                    throw new System.NotImplementedException("Unrecognized TextAnchor value: " + alignment);
            }

            return inner;
        }


        /// <summary>
        /// For the given style, calculate the width that the given text would be. If that width exceeds the width of the given rect,
        /// then the text would overflow if placed inside that rect. By default the text will then be cut off on the right-hand side.
        /// That default can be changed using the given alignment. If the text fits inside the rect without problems, nothing is changed.
        /// </summary>
        public static GUIStyle GetTextOverflowAlignmentStyle(Rect rect, GUIStyle style, string text, TextAnchor alignment)
        {
            if (GetWidth(style, text) >= rect.width) // If the text is overflowing, cut it off on the left side instead of the right side. Makes it easier to read.
            {
                style = new GUIStyle(style);
                style.alignment = alignment;
            }
            return style;
        }

        private static void InitializeStyle(string style)
        {
            GUIStyle s;
            switch (style)
            {
                #region TestManagerUI
                case "TestManagerUI/TestView":
                    s = new GUIStyle(EditorStyles.inspectorFullWidthMargins);
                    s.padding.right = 0;
                    s.padding.top = 2;
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
                case "TestManagerUI/Toolbar/Skip":
                    s = new GUIStyle(EditorStyles.toolbarButton);
                    break;
                case "TestManagerUI/Toolbar/GoToEmptyScene":
                    s = new GUIStyle(EditorStyles.toolbarButton);
                    break;
                case "TestManagerUI/Toolbar/Debug":
                    s = new GUIStyle(EditorStyles.toolbarDropDown);
                    break;
                case "TestManagerUI/Toolbar/Refresh":
                    s = new GUIStyle(EditorStyles.toolbarButton);
                    break;
                case "TestManagerUI/Toolbar/Welcome":
                    s = new GUIStyle(EditorStyles.toolbarButton);
                    break;

                case "TestManagerUI/Welcome":
                    s = new GUIStyle(EditorStyles.helpBox);
                    break;
                case "TestManagerUI/Welcome/Title":
                    s = new GUIStyle(EditorStyles.largeLabel);
                    s.richText = true;
                    s.imagePosition = ImagePosition.ImageLeft;
                    s.fontSize = 18;
                    s.fontStyle = FontStyle.Bold;
                    s.alignment = TextAnchor.MiddleLeft;

                    s.padding.left = 0;
                    s.padding.right = 0;
                    s.padding.top = 0;
                    s.padding.bottom = 0;

                    s.margin.bottom = 1;

                    s.contentOffset = new Vector2(Get("TestManagerUI/Welcome").padding.left, 0f);
                    break;
                case "TestManagerUI/Welcome/Message":
                    s = new GUIStyle(EditorStyles.wordWrappedLabel);
                    s.richText = true;
                    s.fontSize = 12;
                    s.margin.top = 1;
                    //s.margin.bottom = 10;
                    break;
                case "TestManagerUI/Donate":
                    s = new GUIStyle(EditorStyles.linkLabel);
                    s.fixedHeight = new GUIStyle("LargeButton").fixedHeight;
                    s.alignment = TextAnchor.LowerCenter;
                    Vector2 textSizeDonate = s.CalcSize(new GUIContent(GetIcon("TestManagerUI/Donate").text));
                    // Afford enough space for the regular text, plus the icon, limiting the icon's height to fit inside the button.
                    Font fontDonate = s.font;
                    if (fontDonate == null) fontDonate = GUI.skin.font;
                    s.fixedWidth = textSizeDonate.x + Mathf.Min(fontDonate.lineHeight, textSizeDonate.y);
                    //s.contentOffset = new Vector2(0f, 10f);
                    break;
                case "TestManagerUI/Documentation":
                    s = new GUIStyle(Get("TestManagerUI/Donate"));
                    Vector2 textSizeDoc = s.CalcSize(new GUIContent(GetIcon("TestManagerUI/Documentation").text));
                    // Afford enough space for the regular text, plus the icon, limiting the icon's height to fit inside the button.
                    Font fontDoc = s.font;
                    if (fontDoc == null) fontDoc = GUI.skin.font;
                    s.fixedWidth = textSizeDoc.x + Mathf.Min(fontDoc.lineHeight, textSizeDoc.y);
                    break;
                #endregion Toolbar

                #endregion TestManagerUI

                #region List items
                // Most other styles rely on the Toggle style to set their height etc.
                case "Toggle":
                    s = new GUIStyle(EditorStyles.label);
                    s.fixedHeight = lineHeight;
                    s.richText = true;
                    s.alignment = TextAnchor.MiddleLeft;
                    break;
                case "ToggleHeader":
                    s = new GUIStyle(Get("Toggle"));
                    s.fontStyle = FontStyle.Bold;
                    break;
                case "GoToSearch":
                    s = new GUIStyle(EditorStyles.iconButton);
                    s.padding = new RectOffset(0, 0, 0, 0);
                    s.alignment = TextAnchor.MiddleCenter;
                    break;
                case "Script":
                    s = new GUIStyle(EditorStyles.objectField);
                    s.margin = EditorStyles.iconButton.margin;
                    s.padding = EditorStyles.iconButton.padding;
                    s.contentOffset = EditorStyles.iconButton.contentOffset;
                    s.fixedWidth = EditorStyles.iconButton.fixedWidth;
                    s.fixedHeight = Get("Toggle").fixedHeight;
                    s.alignment = TextAnchor.MiddleCenter;
                    break;
                case "Foldout":
                    s = new GUIStyle(EditorStyles.foldout);
                    s.contentOffset = Vector2.zero;
                    s.alignment = TextAnchor.MiddleCenter;
                    s.fixedHeight = Get("Toggle").fixedHeight;
                    break;
                case "TestRect":
                    s = new GUIStyle(GUI.skin.box);
                    s.margin = new RectOffset(0, 0, 0, 0);
                    s.padding = new RectOffset(0, 0, 0, 0);
                    break;
                case "Lock":
                    s = new GUIStyle(EditorStyles.iconButton);
                    s.fixedHeight = Get("Toggle").fixedHeight; // fill to the same height as the toggle
                    s.alignment = TextAnchor.MiddleCenter;
                    break;
                case "Result":
                    s = new GUIStyle(EditorStyles.label);
                    s.imagePosition = ImagePosition.ImageOnly;
                    s.fixedHeight = Get("Toggle").fixedHeight;
                    s.fixedWidth = Get("ClearResult").fixedWidth;
                    s.stretchHeight = false;
                    s.padding = new RectOffset(0, 0, 0, 0);
                    s.margin = new RectOffset(0, 2, 0, 0);
                    s.contentOffset = new Vector2(-1f, -1f);
                    s.alignment = TextAnchor.MiddleCenter;
                    break;
                case "ClearResult":
                    s = new GUIStyle(EditorStyles.iconButton);
                    s.fixedHeight = Get("Toggle").fixedHeight;
                    s.stretchHeight = false;
                    s.margin = new RectOffset(0, 0, 0, 0);
                    s.padding = new RectOffset(0, 0, 0, 0);
                    //s.contentOffset = new Vector2(0f, 1f);
                    s.imagePosition = ImagePosition.ImageOnly;
                    s.alignment = TextAnchor.MiddleCenter;
                    break;
                case "Settings":
                    s = new GUIStyle(EditorStyles.iconButton);
                    s.contentOffset = new Vector2(0, 1); // centers the image
                    s.alignment = TextAnchor.MiddleCenter;
                    break;
                #endregion List items

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
                    break;
                case "GUIQueue/Test":
                    s = new GUIStyle(EditorStyles.label);
                    s.alignment = TextAnchor.MiddleLeft;
                    s.clipping = TextClipping.Clip;
                    break;
                case "GUIQueue/DragHandle":
                    s = "RL DragHandle"; // https://github.com/Unity-Technologies/UnityCsReference/blob/9cecb4a6817863f0134896edafa84753ae2be96f/Editor/Mono/GUI/ReorderableList.cs#L162
                    s.contentOffset = new Vector2(0f, 7f);
                    s.alignment = TextAnchor.MiddleCenter;
                    s.margin.left = 5;
                    s.margin.right = 5;
                    break;
                case "GUIQueue/Toolbar/BoldLabel":
                    s = new GUIStyle(EditorStyles.boldLabel);
                    break;
                case "GUIQueue/Toolbar/Button":
                    s = new GUIStyle(EditorStyles.toolbarButton);
                    break;
                case "GUIQueue/Queue":
                    s = new GUIStyle(EditorStyles.helpBox);
                    s.margin.top = 1;
                    s.padding.left = 1;
                    s.padding.right = 1;
                    break;
                case "GUIQueue/Queue/Title":
                    s = new GUIStyle(EditorStyles.boldLabel);
                    s.alignment = TextAnchor.LowerLeft;
                    break;
                case "GUIQueue/Queue/Clear":
                    s = new GUIStyle(GUI.skin.button);
                    break;
                case "GUIQueue/FrameCounter":
                    s = new GUIStyle(EditorStyles.label);
                    s.alignment = TextAnchor.LowerRight;
                    break;
                #endregion GUIQueue


                #region Settings Window
                case "Settings/Header":
                    s = new GUIStyle(EditorStyles.helpBox);
                    break;
                case "Settings/Inspector":
                    s = new GUIStyle(EditorStyles.helpBox);
                    break;
                case "Settings/Footer":
                    s = new GUIStyle(EditorStyles.helpBox);
                    s.margin = new RectOffset(-1, -1, s.margin.top, 0);
                    break;
                #endregion Settings Window

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
                    c.tooltip = Tooltips.toolbarToggle;
                    break;
                case "TestManagerUI/Toolbar/Toggle/On/Hover":
                    c = new GUIContent(EditorGUIUtility.IconContent("toggle_on_hover"));
                    c.tooltip = Tooltips.toolbarToggle;
                    break;
                case "TestManagerUI/Toolbar/Toggle/Off":
                    c = new GUIContent(EditorGUIUtility.IconContent("toggle_bg"));
                    c.tooltip = Tooltips.toolbarToggle;
                    break;
                case "TestManagerUI/Toolbar/Toggle/Off/Hover":
                    c = new GUIContent(EditorGUIUtility.IconContent("toggle_bg_hover"));
                    c.tooltip = Tooltips.toolbarToggle;
                    break;
                case "TestManagerUI/Toolbar/Toggle/Mixed":
                    c = new GUIContent(EditorGUIUtility.IconContent("toggle_mixed_bg"));
                    c.tooltip = Tooltips.toolbarToggle;
                    break;
                case "TestManagerUI/Toolbar/Toggle/Mixed/Hover":
                    c = new GUIContent(EditorGUIUtility.IconContent("toggle_mixed_bg_hover"));
                    c.tooltip = Tooltips.toolbarToggle;
                    break;

                case "TestManagerUI/Toolbar/Clear":
                    c = new GUIContent(EditorGUIUtility.IconContent("clear"));
                    c.tooltip = Tooltips.clearSelected;
                    break;

                case "TestManagerUI/Toolbar/Play/Off":
                    c = new GUIContent(EditorGUIUtility.IconContent("PlayButton"));
                    c.tooltip = Tooltips.playOff;
                    break;
                case "TestManagerUI/Toolbar/Play/On":
                    c = new GUIContent(EditorGUIUtility.IconContent("PlayButton On"));
                    c.tooltip = Tooltips.playOn;
                    break;

                case "TestManagerUI/Toolbar/Pause/Off":
                    c = new GUIContent(EditorGUIUtility.IconContent("PauseButton"));
                    c.tooltip = Tooltips.pauseOff;
                    break;
                case "TestManagerUI/Toolbar/Pause/On":
                    c = new GUIContent(EditorGUIUtility.IconContent("PauseButton On"));
                    c.tooltip = Tooltips.pauseOn;
                    break;

                case "TestManagerUI/Toolbar/Skip":
                    c = new GUIContent(EditorGUIUtility.IconContent("StepButton"));
                    c.tooltip = Tooltips.skip;
                    break;

                case "TestManagerUI/Toolbar/GoToEmptyScene":
                    c = new GUIContent(EditorGUIUtility.IconContent("SceneLoadIn"));
                    c.tooltip = Tooltips.goToEmptyScene;
                    break;

                case "TestManagerUI/Toolbar/Debug/Off":
                    c = new GUIContent(EditorGUIUtility.IconContent("DebuggerDisabled"));
                    c.tooltip = Tooltips.debugOff;
                    break;
                case "TestManagerUI/Toolbar/Debug/On":
                    c = new GUIContent(EditorGUIUtility.IconContent("DebuggerAttached"));
                    c.tooltip = Tooltips.debugOn;
                    break;

                case "TestManagerUI/Toolbar/Refresh":
                    c = new GUIContent(EditorGUIUtility.IconContent("Refresh"));
                    c.tooltip = Tooltips.refresh;
                    break;

                case "TestManagerUI/Toolbar/Welcome": // the welcome button
                    c = new GUIContent(EditorGUIUtility.IconContent("console.infoicon.sml"));
                    c.tooltip = Tooltips.welcome;
                    break;
                #endregion Toolbar

                #region Welcome
                case "TestManagerUI/Welcome": // the speech bubble icon in the welcome message
                    c = new GUIContent(EditorGUIUtility.IconContent("console.infoicon"));
                    break;
                case "TestManagerUI/Donate":
                    c = new GUIContent(EditorGUIUtility.IconContent("RightHandZoomSilhouette"));
                    c.text = "Donate";
                    c.tooltip = Tooltips.donate;
                    break;
                case "TestManagerUI/Documentation":
                    c = new GUIContent("GitHub");
                    c.image = EditorGUIUtility.IconContent("TextAsset Icon").image;
                    c.tooltip = Tooltips.documentation;
                    break;
                #endregion

                #region List items
                case "Settings":
                    c = new GUIContent(EditorGUIUtility.IconContent("Settings"));
                    c.tooltip = Tooltips.settings;
                    break;
                case "GoToSearch":
                    c = new GUIContent(EditorGUIUtility.IconContent("back"));
                    c.tooltip = Tooltips.goToSearch;
                    break;
                case "Script":
                    c = new GUIContent(EditorGUIUtility.IconContent("cs Script Icon"));
                    break;
                case "LockOn":
                    c = new GUIContent(EditorGUIUtility.IconContent("IN LockButton on"));
                    c.tooltip = Tooltips.lockButton;
                    break;
                case "LockOff":
                    c = new GUIContent(EditorGUIUtility.IconContent("IN LockButton"));
                    c.tooltip = Tooltips.lockButton;
                    break;
                case "Result/None":
                    c = new GUIContent(EditorGUIUtility.IconContent("TestNormal"));
                    break;
                case "Result/Pass":
                    c = new GUIContent(EditorGUIUtility.IconContent("TestPassed"));
                    c.tooltip = Tooltips.testPassed;
                    break;
                case "Result/Fail":
                    c = new GUIContent(EditorGUIUtility.IconContent("TestFailed"));
                    c.tooltip = Tooltips.testFailed;
                    break;
                case "Result/Skipped":
                    c = new GUIContent(EditorGUIUtility.IconContent("TestInconclusive"));
                    c.tooltip = Tooltips.testSkipped;
                    break;
                case "ClearResult":
                    c = new GUIContent(EditorGUIUtility.IconContent("clear"));
                    c.tooltip = Tooltips.clearTest;
                    break;
                case "Suite/SettingsButton":
                    c = new GUIContent(EditorGUIUtility.IconContent("_Popup"));
                    break;
                #endregion List items
                #endregion TestManagerUI

                #region GUIQueue
                case "GUIQueue/Toolbar/Options":
                    c = new GUIContent(EditorGUIUtility.IconContent("_Menu"));// pane options"));
                    break;
                #endregion
                
                default:
                    throw new System.NotImplementedException("Unrecognized icon name '" + icon + "'");
            }
            icons[icon] = c;
        }
    }
}