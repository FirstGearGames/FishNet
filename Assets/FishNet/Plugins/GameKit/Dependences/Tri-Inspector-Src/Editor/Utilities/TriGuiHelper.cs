using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TriInspector.Utilities
{
    public static class TriGuiHelper
    {
        private static readonly Stack<Object> TargetObjects = new Stack<Object>();

        internal static bool IsAnyEditorPushed()
        {
            return TargetObjects.Count > 0;
        }

        internal static bool IsEditorTargetPushed(Object obj)
        {
            foreach (var targetObject in TargetObjects)
            {
                if (targetObject == obj)
                {
                    return true;
                }
            }

            return false;
        }

        internal static EditorScope PushEditorTarget(Object obj)
        {
            return new EditorScope(obj);
        }

        public static LabelWidthScope PushLabelWidth(float labelWidth)
        {
            return new LabelWidthScope(labelWidth);
        }

        public static IndentedRectScope PushIndentedRect(Rect source, int indentLevel)
        {
            return new IndentedRectScope(source, indentLevel);
        }

        public static GuiColorScope PushColor(Color color)
        {
            return new GuiColorScope(color);
        }

        public readonly struct EditorScope : IDisposable
        {
            public EditorScope(Object obj)
            {
                TargetObjects.Push(obj);
            }

            public void Dispose()
            {
                TargetObjects.Pop();
            }
        }

        public readonly struct LabelWidthScope : IDisposable
        {
            private readonly float _oldLabelWidth;

            public LabelWidthScope(float labelWidth)
            {
                _oldLabelWidth = EditorGUIUtility.labelWidth;

                if (labelWidth > 0)
                {
                    EditorGUIUtility.labelWidth = labelWidth;
                }
            }

            public void Dispose()
            {
                EditorGUIUtility.labelWidth = _oldLabelWidth;
            }
        }

        public readonly struct IndentedRectScope : IDisposable
        {
            private readonly float _indent;

            public Rect IndentedRect { get; }

            public IndentedRectScope(Rect source, int indentLevel)
            {
                _indent = indentLevel * 15;

                IndentedRect = new Rect(source.x + _indent, source.y, source.width - _indent, source.height);
                EditorGUIUtility.labelWidth -= _indent;
            }

            public void Dispose()
            {
                EditorGUIUtility.labelWidth += _indent;
            }
        }

        public readonly struct GuiColorScope : IDisposable
        {
            private readonly Color _oldColor;

            public GuiColorScope(Color color)
            {
                _oldColor = GUI.color;

                GUI.color = color;
            }

            public void Dispose()
            {
                GUI.color = _oldColor;
            }
        }
    }
}