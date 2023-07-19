using System;
using JetBrains.Annotations;
using TriInspector.Resolvers;
using TriInspector.Utilities;
using UnityEditor;
using UnityEngine;

namespace TriInspector.Elements
{
    public class TriBoxGroupElement : TriHeaderGroupBaseElement
    {
        private readonly Props _props;

        private ValueResolver<string> _headerResolver;
        [CanBeNull] private TriProperty _firstProperty;

        private bool _expanded;

        [Serializable]
        public struct Props
        {
            public string title;
            public TitleMode titleMode;
            public bool expandedByDefault;
        }

        public TriBoxGroupElement(Props props = default)
        {
            _props = props;
            _expanded = _props.expandedByDefault;
        }

        protected override void AddPropertyChild(TriElement element, TriProperty property)
        {
            _firstProperty = property;
            _headerResolver = ValueResolver.ResolveString(property.Definition, _props.title ?? "");

            if (_headerResolver.TryGetErrorString(out var error))
            {
                AddChild(new TriInfoBoxElement(error, TriMessageType.Error));
            }

            base.AddPropertyChild(element, property);
        }

        protected override float GetHeaderHeight(float width)
        {
            if (_props.titleMode == TitleMode.Hidden)
            {
                return 0f;
            }

            return base.GetHeaderHeight(width);
        }

        protected override float GetContentHeight(float width)
        {
            if (_props.titleMode == TitleMode.Foldout && !_expanded)
            {
                return 0f;
            }

            return base.GetContentHeight(width);
        }

        protected override void DrawHeader(Rect position)
        {
            TriEditorGUI.DrawBox(position, TriEditorStyles.TabOnlyOne);

            var headerLabelRect = new Rect(position)
            {
                xMin = position.xMin + 6,
                xMax = position.xMax - 6,
                yMin = position.yMin + 2,
                yMax = position.yMax - 2,
            };

            var headerContent = _headerResolver.GetValue(_firstProperty);

            if (_props.titleMode == TitleMode.Foldout)
            {
                headerLabelRect.x += 10;
                _expanded = EditorGUI.Foldout(headerLabelRect, _expanded, headerContent, true);
            }
            else
            {
                EditorGUI.LabelField(headerLabelRect, headerContent);
            }
        }

        protected override void DrawContent(Rect position)
        {
            if (_props.titleMode == TitleMode.Foldout && !_expanded)
            {
                return;
            }

            base.DrawContent(position);
        }

        public enum TitleMode
        {
            Normal,
            Hidden,
            Foldout,
        }
    }
}