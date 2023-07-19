using UnityEditor;
using UnityEngine;

namespace TriInspector.Elements
{
    public class TriMultiEditNotSupportedElement : TriElement
    {
        private readonly TriProperty _property;
        private readonly GUIContent _message;

        public TriMultiEditNotSupportedElement(TriProperty property)
        {
            _property = property;
            _message = new GUIContent("Multi edit not supported");
        }

        public override float GetHeight(float width)
        {
            return EditorGUIUtility.singleLineHeight;
        }

        public override void OnGUI(Rect position)
        {
            EditorGUI.LabelField(position, _property.DisplayNameContent, _message);
        }
    }
}