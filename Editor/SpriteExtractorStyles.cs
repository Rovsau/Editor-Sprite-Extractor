using UnityEditor;
using UnityEngine;

namespace MyNamespace.EditorSpriteExtractor.Window
{
    public static class SpriteExtractorStyles
    {
        public static GUIStyle HorizontalSeparator { get; private set; } = new GUIStyle(GUI.skin.horizontalSlider);
        public static GUIStyle Title { get; private set; } = new GUIStyle(GUI.skin.label)
        {
            fontSize = 14,
            fontStyle = FontStyle.Bold,
        };
        public static GUIStyle Label { get; private set; } = new GUIStyle(GUI.skin.label);
        public static GUIStyle RadioButton { get; private set; } = new GUIStyle(EditorStyles.radioButton)
        {
            fontSize = 10
        };
        public static GUIStyle LabelBold { get; private set; } = new GUIStyle(GUI.skin.label)
        {
            fontStyle = FontStyle.Bold
        };
        public static GUIStyle Button { get; private set; } = new GUIStyle(GUI.skin.button)
        {
            padding = new RectOffset(6, 6, 6, 6)
        };
        public static GUIStyle MainWrapper { get; private set; } = new GUIStyle()
        {
            padding = new RectOffset(10, 10, 10, 10)
        };
        public static GUIStyle Area_Config { get; private set; } = new GUIStyle()
        {
            fixedWidth = 350f
        };
        public static GUIStyle Area_Config_Left { get; private set; } = new GUIStyle()
        {
            fixedWidth = 128f + 24
        };
        public static GUIStyle HelpBox { get; private set; } = new GUIStyle(EditorStyles.helpBox)
        {
            alignment = TextAnchor.UpperRight,
            padding = new RectOffset(12, 12, 12, 12),
        };
    }
}