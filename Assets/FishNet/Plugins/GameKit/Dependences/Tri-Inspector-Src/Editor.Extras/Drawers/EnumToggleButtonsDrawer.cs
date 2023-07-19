using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TriInspector;
using TriInspector.Drawers;
using UnityEditor;
using UnityEngine;

[assembly: RegisterTriAttributeDrawer(typeof(EnumToggleButtonsDrawer), TriDrawerOrder.Drawer,
    ApplyOnArrayElement = true)]

namespace TriInspector.Drawers
{
    public class EnumToggleButtonsDrawer : TriAttributeDrawer<EnumToggleButtonsAttribute>
    {
        public override TriExtensionInitializationResult Initialize(TriPropertyDefinition propertyDefinition)
        {
            if (!propertyDefinition.FieldType.IsEnum)
            {
                return "EnumToggleButtons attribute can be used only on enums";
            }

            return TriExtensionInitializationResult.Ok;
        }

        public override TriElement CreateElement(TriProperty property, TriElement next)
        {
            return new EnumToggleButtonsElement(property);
        }

        private sealed class EnumToggleButtonsElement : TriElement
        {
            private readonly TriProperty _property;
            private readonly List<KeyValuePair<string, Enum>> _enumValues;
            private readonly bool _isFlags;

            public EnumToggleButtonsElement(TriProperty property)
            {
                _property = property;
                _enumValues = Enum.GetNames(property.FieldType)
                    .Zip(Enum.GetValues(property.FieldType).OfType<Enum>(),
                        (name, value) => new KeyValuePair<string, Enum>(name, value))
                    .ToList();
                _isFlags = property.FieldType.GetCustomAttributes(typeof(FlagsAttribute), false).Length > 0;

                _enumValues.Sort(new DeclarationOrderComparer(property.FieldType));
            }

            public override float GetHeight(float width)
            {
                return EditorGUIUtility.singleLineHeight;
            }

            public override void OnGUI(Rect position)
            {
                var value = _property.TryGetSerializedProperty(out var serializedProperty)
                    ? (Enum) Enum.ToObject(_property.FieldType, serializedProperty.intValue)
                    : (Enum) _property.Value;

                var controlId = GUIUtility.GetControlID(FocusType.Passive);
                position = EditorGUI.PrefixLabel(position, controlId, _property.DisplayNameContent);

                for (var i = 0; i < _enumValues.Count; i++)
                {
                    var itemRect = SplitRectWidth(position, _enumValues.Count, i);
                    var itemStyle = GetButtonStyle(_enumValues.Count, i);
                    var itemName = _enumValues[i].Key;
                    var itemValue = _enumValues[i].Value;

                    var selected = value != null && (_isFlags ? value.HasFlag(itemValue) : value.Equals(itemValue));

                    if (selected != GUI.Toggle(itemRect, selected, itemName, itemStyle))
                    {
                        _property.SetValue(itemValue);
                    }
                }
            }

            private static GUIStyle GetButtonStyle(int total, int current)
            {
                if (total <= 1)
                {
                    return EditorStyles.miniButton;
                }

                if (current == 0)
                {
                    return EditorStyles.miniButtonLeft;
                }

                if (current == total - 1)
                {
                    return EditorStyles.miniButtonRight;
                }

                return EditorStyles.miniButtonMid;
            }

            private static Rect SplitRectWidth(Rect rect, int total, int current)
            {
                if (total == 0)
                {
                    return rect;
                }

                rect.width /= total;
                rect.x += rect.width * current;
                return rect;
            }

            private class DeclarationOrderComparer : IComparer<KeyValuePair<string, Enum>>
            {
                private readonly FieldInfo[] _fields;

                public DeclarationOrderComparer(Type enumType)
                {
                    _fields = enumType.GetFields(BindingFlags.Static | BindingFlags.Public);
                }

                public int Compare(KeyValuePair<string, Enum> x, KeyValuePair<string, Enum> y)
                {
                    var orderX = Array.FindIndex(_fields, it => it.Name == x.Key);
                    var orderY = Array.FindIndex(_fields, it => it.Name == y.Key);
                    return orderX.CompareTo(orderY);
                }
            }
        }
    }
}