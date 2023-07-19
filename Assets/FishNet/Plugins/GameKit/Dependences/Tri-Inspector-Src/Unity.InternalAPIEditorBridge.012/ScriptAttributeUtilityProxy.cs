using UnityEditor;
using UnityEngine;

namespace TriInspectorUnityInternalBridge
{
    internal static class ScriptAttributeUtilityProxy
    {
        public static PropertyHandlerProxy GetHandler(SerializedProperty property)
        {
            var handler = ScriptAttributeUtility.GetHandler(property);
            return new PropertyHandlerProxy(handler);
        }
    }

    internal readonly struct PropertyHandlerProxy
    {
        private readonly PropertyHandler _handler;

        internal PropertyHandlerProxy(PropertyHandler handler)
        {
            _handler = handler;
        }

        // ReSharper disable once InconsistentNaming
        public bool hasPropertyDrawer => _handler.hasPropertyDrawer;

        public float GetHeight(SerializedProperty property, GUIContent label, bool includeChildren)
        {
            return _handler.GetHeight(property, label, includeChildren);
        }

        public bool OnGUI(Rect position, SerializedProperty property, GUIContent label, bool includeChildren)
        {
            return _handler.OnGUI(position, property, label, includeChildren);
        }
    }
}