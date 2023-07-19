using TriInspector;
using TriInspector.Drawers;
using UnityEditor;
using UnityEngine;

[assembly: RegisterTriValueDrawer(typeof(ObjectReferenceDrawer), TriDrawerOrder.Fallback)]

namespace TriInspector.Drawers
{
    public class ObjectReferenceDrawer : TriValueDrawer<Object>
    {
        public override TriElement CreateElement(TriValue<Object> value, TriElement next)
        {
            if (value.Property.IsRootProperty || value.Property.TryGetSerializedProperty(out _))
            {
                return next;
            }

            return new ObjectReferenceDrawerElement(value);
        }

        private class ObjectReferenceDrawerElement : TriElement
        {
            private TriValue<Object> _propertyValue;
            private readonly bool _allowSceneObjects;

            public ObjectReferenceDrawerElement(TriValue<Object> propertyValue)
            {
                _propertyValue = propertyValue;
                _allowSceneObjects = propertyValue.Property.PropertyTree.TargetIsPersistent == false;
            }

            public override float GetHeight(float width)
            {
                return EditorGUIUtility.singleLineHeight;
            }

            public override void OnGUI(Rect position)
            {
                var value = _propertyValue.SmartValue;

                EditorGUI.BeginChangeCheck();

                value = EditorGUI.ObjectField(position, _propertyValue.Property.DisplayNameContent, value,
                    _propertyValue.Property.FieldType, _allowSceneObjects);

                if (EditorGUI.EndChangeCheck())
                {
                    _propertyValue.SmartValue = value;
                }
            }
        }
    }
}