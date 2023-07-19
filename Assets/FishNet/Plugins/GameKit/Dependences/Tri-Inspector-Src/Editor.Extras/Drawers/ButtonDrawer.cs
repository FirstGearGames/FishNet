using System;
using System.Reflection;
using TriInspector;
using TriInspector.Drawers;
using TriInspector.Resolvers;
using UnityEditor;
using UnityEngine;

[assembly: RegisterTriAttributeDrawer(typeof(ButtonDrawer), TriDrawerOrder.Drawer)]

namespace TriInspector.Drawers
{
    public class ButtonDrawer : TriAttributeDrawer<ButtonAttribute>
    {
        private ValueResolver<string> _nameResolver;

        public override TriExtensionInitializationResult Initialize(TriPropertyDefinition propertyDefinition)
        {
            var isValidMethod = propertyDefinition.TryGetMemberInfo(out var memberInfo) &&
                                memberInfo is MethodInfo mi &&
                                mi.GetParameters().Length == 0;
            if (!isValidMethod)
            {
                return "[Button] valid only on methods without parameters";
            }

            _nameResolver = ValueResolver.ResolveString(propertyDefinition, Attribute.Name);
            if (_nameResolver.TryGetErrorString(out var error))
            {
                return error;
            }

            return TriExtensionInitializationResult.Ok;
        }

        public override float GetHeight(float width, TriProperty property, TriElement next)
        {
            if (Attribute.ButtonSize != 0)
            {
                return Attribute.ButtonSize;
            }

            return EditorGUIUtility.singleLineHeight;
        }

        public override void OnGUI(Rect position, TriProperty property, TriElement next)
        {
            var name = _nameResolver.GetValue(property);

            if (string.IsNullOrEmpty(name))
            {
                name = property.DisplayName;
            }

            if (string.IsNullOrEmpty(name))
            {
                name = property.RawName;
            }

            if (GUI.Button(position, name))
            {
                InvokeButton(property, Array.Empty<object>());
            }
        }

        private static void InvokeButton(TriProperty property, object[] parameters)
        {
            if (property.TryGetMemberInfo(out var memberInfo) && memberInfo is MethodInfo methodInfo)
            {
                property.ModifyAndRecordForUndo(targetIndex =>
                {
                    try
                    {
                        var parentValue = property.Parent.GetValue(targetIndex);
                        methodInfo.Invoke(parentValue, parameters);
                    }
                    catch (Exception e)
                    {
                        Debug.LogException(e);
                    }
                });
            }
        }
    }
}