using TriInspector.Utilities;
using TriInspectorUnityInternalBridge;
using UnityEditor;
using UnityEngine;

namespace TriInspector.Elements
{
    public class TriInfoBoxElement : TriElement
    {
        private readonly GUIContent _message;
        private readonly Texture2D _icon;
        private readonly Color _color;

        public TriInfoBoxElement(string message, TriMessageType type = TriMessageType.None, Color? color = null)
        {
            var messageType = GetMessageType(type);
            _icon = EditorGUIUtilityProxy.GetHelpIcon(messageType);
            _message = new GUIContent(message);
            _color = color ?? GetColor(type);
        }

        public override float GetHeight(float width)
        {
            var style = _icon == null ? Styles.InfoBoxContentNone : Styles.InfoBoxContent;
            var height = style.CalcHeight(_message, width);
            return Mathf.Max(26, height);
        }

        public override void OnGUI(Rect position)
        {
            using (TriGuiHelper.PushColor(_color))
            {
                GUI.Label(position, string.Empty, Styles.InfoBoxBg);
            }

            if (_icon != null)
            {
                var iconRect = new Rect(position)
                {
                    xMin = position.xMin + 4,
                    width = 20,
                };

                GUI.Label(position, _message, Styles.InfoBoxContent);
                GUI.DrawTexture(iconRect, _icon, ScaleMode.ScaleToFit);
            }
            else
            {
                GUI.Label(position, _message, Styles.InfoBoxContentNone);
            }
        }

        private static Color GetColor(TriMessageType type)
        {
            switch (type)
            {
                case TriMessageType.Error:
                    return new Color(1f, 0.4f, 0.4f);

                case TriMessageType.Warning:
                    return new Color(1f, 0.8f, 0.2f);

                default:
                    return Color.white;
            }
        }

        private static MessageType GetMessageType(TriMessageType type)
        {
            switch (type)
            {
                case TriMessageType.None: return MessageType.None;
                case TriMessageType.Info: return MessageType.Info;
                case TriMessageType.Warning: return MessageType.Warning;
                case TriMessageType.Error: return MessageType.Error;
                default: return MessageType.None;
            }
        }

        private static class Styles
        {
            public static readonly GUIStyle InfoBoxBg;
            public static readonly GUIStyle InfoBoxContent;
            public static readonly GUIStyle InfoBoxContentNone;

            static Styles()
            {
                InfoBoxBg = new GUIStyle(EditorStyles.helpBox);
                InfoBoxContentNone = new GUIStyle(EditorStyles.label)
                {
                    padding = new RectOffset(4, 4, 4, 4),
                    fontSize = InfoBoxBg.fontSize,
                    alignment = TextAnchor.MiddleLeft,
                    wordWrap = true,
                };
                InfoBoxContent = new GUIStyle(InfoBoxContentNone)
                {
                    padding = new RectOffset(26, 4, 4, 4),
                };
            }
        }
    }
}