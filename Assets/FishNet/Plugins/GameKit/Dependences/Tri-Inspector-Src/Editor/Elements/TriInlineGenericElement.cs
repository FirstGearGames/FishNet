using System;
using TriInspector.Utilities;
using UnityEditor;
using UnityEngine;

namespace TriInspector.Elements
{
    internal class TriInlineGenericElement : TriPropertyCollectionBaseElement
    {
        private readonly Props _props;
        private readonly TriProperty _property;

        [Serializable]
        public struct Props
        {
            public bool drawPrefixLabel;
            public float labelWidth;
        }

        public TriInlineGenericElement(TriProperty property, Props props = default)
        {
            _property = property;
            _props = props;

            DeclareGroups(property.ValueType);

            foreach (var childProperty in property.ChildrenProperties)
            {
                AddProperty(childProperty);
            }
        }

        public override void OnGUI(Rect position)
        {
            if (_props.drawPrefixLabel)
            {
                var controlId = GUIUtility.GetControlID(FocusType.Passive);
                position = EditorGUI.PrefixLabel(position, controlId, _property.DisplayNameContent);
            }

            using (TriGuiHelper.PushLabelWidth(_props.labelWidth))
            {
                base.OnGUI(position);
            }
        }
    }
}