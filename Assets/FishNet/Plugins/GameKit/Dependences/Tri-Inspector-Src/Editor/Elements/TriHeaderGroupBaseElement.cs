using TriInspector.Utilities;
using UnityEditor;
using UnityEngine;

namespace TriInspector.Elements
{
    public abstract class TriHeaderGroupBaseElement : TriPropertyCollectionBaseElement
    {
        private const float InsetTop = 4;
        private const float InsetBottom = 4;
        private const float InsetLeft = 18;
        private const float InsetRight = 4;

        protected virtual float GetHeaderHeight(float width)
        {
            return 22;
        }

        protected virtual float GetContentHeight(float width)
        {
            return base.GetHeight(width) + InsetTop + InsetBottom;
        }

        protected virtual void DrawHeader(Rect position)
        {
        }

        protected virtual void DrawContent(Rect position)
        {
            base.OnGUI(position);
        }

        public sealed override float GetHeight(float width)
        {
            return GetContentHeight(width) + GetHeaderHeight(width);
        }

        public sealed override void OnGUI(Rect position)
        {
            var headerHeight = GetHeaderHeight(position.width);
            var contentHeight = GetContentHeight(position.width);

            var headerBgRect = new Rect(position)
            {
                height = headerHeight,
            };
            var contentBgRect = new Rect(position)
            {
                yMin = headerBgRect.yMax,
            };
            var contentRect = new Rect(contentBgRect)
            {
                xMin = contentBgRect.xMin + InsetLeft,
                xMax = contentBgRect.xMax - InsetRight,
                height = contentHeight,
            };

            if (headerHeight > 0f)
            {
                DrawHeader(headerBgRect);
            }

            if (contentHeight > 0)
            {
                TriEditorGUI.DrawBox(contentBgRect, headerHeight > 0f
                    ? TriEditorStyles.ContentBox
                    : TriEditorStyles.Box);

                using (TriGuiHelper.PushLabelWidth(EditorGUIUtility.labelWidth - InsetLeft))
                {
                    DrawContent(contentRect);
                }
            }
        }
    }
}