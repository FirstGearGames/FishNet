using TriInspector;
using TriInspector.Elements;
using TriInspector.GroupDrawers;
using UnityEngine;

[assembly: RegisterTriGroupDrawer(typeof(TriHorizontalGroupDrawer))]

namespace TriInspector.GroupDrawers
{
    public class TriHorizontalGroupDrawer : TriGroupDrawer<DeclareHorizontalGroupAttribute>
    {
        public override TriPropertyCollectionBaseElement CreateElement(DeclareHorizontalGroupAttribute attribute)
        {
            return new TriHorizontalGroupElement();
        }
    }
}