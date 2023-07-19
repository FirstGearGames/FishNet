using UnityEditor;
using UnityEngine;

namespace TriInspector.Drawers
{
    public abstract class BuiltinDrawerBase<T> : TriValueDrawer<T>
    {
        public sealed override TriElement CreateElement(TriValue<T> propertyValue, TriElement next)
        {
            if (propertyValue.Property.TryGetSerializedProperty(out _))
            {
                return next;
            }

            return base.CreateElement(propertyValue, next);
        }

        public virtual int CompactModeLines => 1;
        public virtual int WideModeLines => 1;

        public sealed override float GetHeight(float width, TriValue<T> propertyValue, TriElement next)
        {
            var lineHeight = EditorGUIUtility.singleLineHeight;
            var spacing = EditorGUIUtility.standardVerticalSpacing;
            var lines = EditorGUIUtility.wideMode ? WideModeLines : CompactModeLines;
            return lineHeight * lines + spacing * (lines - 1);
        }

        public sealed override void OnGUI(Rect position, TriValue<T> propertyValue, TriElement next)
        {
            var value = propertyValue.SmartValue;

            EditorGUI.BeginChangeCheck();

            value = OnValueGUI(position, propertyValue.Property.DisplayNameContent, value);

            if (EditorGUI.EndChangeCheck())
            {
                propertyValue.SmartValue = value;
            }
        }

        protected abstract T OnValueGUI(Rect position, GUIContent label, T value);
    }
}