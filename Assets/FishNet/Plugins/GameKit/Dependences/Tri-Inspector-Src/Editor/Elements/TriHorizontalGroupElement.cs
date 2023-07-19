using TriInspector.Utilities;
using UnityEditor;
using UnityEngine;

namespace TriInspector.Elements
{
    public class TriHorizontalGroupElement : TriPropertyCollectionBaseElement
    {
        public override float GetHeight(float width)
        {
            if (ChildrenCount == 0)
            {
                return 0f;
            }

            var height = 0f;

            var spacing = EditorGUIUtility.standardVerticalSpacing;
            var totalWidth = width - spacing * (ChildrenCount - 1);
            var childWidth = totalWidth / ChildrenCount;

            for (var i = 0; i < ChildrenCount; i++)
            {
                var child = GetChild(i);
                var childHeight = child.GetHeight(childWidth);

                height = Mathf.Max(height, childHeight);
            }

            return height;
        }

        public override void OnGUI(Rect position)
        {
            if (ChildrenCount == 0)
            {
                return;
            }

            var spacing = EditorGUIUtility.standardVerticalSpacing;
            var totalWidth = position.width - spacing * (ChildrenCount - 1);
            var childWidth = totalWidth / ChildrenCount;

            for (var i = 0; i < ChildrenCount; i++)
            {
                var child = GetChild(i);
                var childRect = new Rect(position)
                {
                    width = childWidth,
                    height = child.GetHeight(childWidth),
                    x = position.x + i * (childWidth + spacing),
                };

                using (TriGuiHelper.PushLabelWidth(EditorGUIUtility.labelWidth / ChildrenCount))
                {
                    child.OnGUI(childRect);
                }
            }
        }
    }
}