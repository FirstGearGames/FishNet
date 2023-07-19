using TriInspector;
using TriInspector.Drawers;
using TriInspector.Resolvers;
using UnityEditor;
using UnityEngine;

[assembly: RegisterTriAttributeDrawer(typeof(TitleDrawer), TriDrawerOrder.Inspector)]

namespace TriInspector.Drawers
{
    public class TitleDrawer : TriAttributeDrawer<TitleAttribute>
    {
        private const int SpaceBeforeTitle = 9;
        private const int SpaceBeforeLine = 2;
        private const int LineHeight = 2;
        private const int SpaceBeforeContent = 3;

        private ValueResolver<string> _titleResolver;

        public override TriExtensionInitializationResult Initialize(TriPropertyDefinition propertyDefinition)
        {
            base.Initialize(propertyDefinition);

            _titleResolver = ValueResolver.ResolveString(propertyDefinition, Attribute.Title);

            if (_titleResolver.TryGetErrorString(out var error))
            {
                return error;
            }

            return TriExtensionInitializationResult.Ok;
        }

        public override float GetHeight(float width, TriProperty property, TriElement next)
        {
            var extraHeight = SpaceBeforeTitle +
                              EditorGUIUtility.singleLineHeight +
                              SpaceBeforeLine +
                              LineHeight
                              + SpaceBeforeContent;

            return next.GetHeight(width) + extraHeight;
        }

        public override void OnGUI(Rect position, TriProperty property, TriElement next)
        {
            var titleRect = new Rect(position)
            {
                y = position.y + SpaceBeforeTitle,
                height = EditorGUIUtility.singleLineHeight,
            };

            var lineRect = new Rect(position)
            {
                y = titleRect.yMax + SpaceBeforeLine,
                height = LineHeight,
            };

            var contentRect = new Rect(position)
            {
                yMin = lineRect.yMax + SpaceBeforeContent,
            };

            var title = _titleResolver.GetValue(property, "Error");
            GUI.Label(titleRect, title, EditorStyles.boldLabel);
            EditorGUI.DrawRect(lineRect, Color.gray);

            next.OnGUI(contentRect);
        }
    }
}