using TriInspector;
using TriInspector.Drawers;
using UnityEngine;

[assembly: RegisterTriAttributeDrawer(typeof(PropertySpaceDrawer), TriDrawerOrder.Inspector)]

namespace TriInspector.Drawers
{
    public class PropertySpaceDrawer : TriAttributeDrawer<PropertySpaceAttribute>
    {
        public override float GetHeight(float width, TriProperty property, TriElement next)
        {
            var totalSpace = Attribute.SpaceBefore + Attribute.SpaceAfter;

            return next.GetHeight(width) + totalSpace;
        }

        public override void OnGUI(Rect position, TriProperty property, TriElement next)
        {
            var contentPosition = new Rect(position)
            {
                yMin = position.yMin + Attribute.SpaceBefore,
                yMax = position.yMax - Attribute.SpaceAfter,
            };

            next.OnGUI(contentPosition);
        }
    }
}