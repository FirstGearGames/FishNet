using UnityEditor;
using UnityEngine;

namespace TriInspector.Elements
{
    public class TriNoDrawerElement : TriElement
    {
        private readonly GUIContent _message;
        private readonly TriProperty _property;

        public TriNoDrawerElement(TriProperty property)
        {
            _property = property;
            _message = new GUIContent($"No drawer for {property.FieldType}");
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