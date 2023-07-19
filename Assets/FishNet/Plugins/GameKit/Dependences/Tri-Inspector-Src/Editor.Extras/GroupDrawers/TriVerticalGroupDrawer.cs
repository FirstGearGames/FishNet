using TriInspector;
using TriInspector.Elements;
using TriInspector.GroupDrawers;

[assembly: RegisterTriGroupDrawer(typeof(TriVerticalGroupDrawer))]

namespace TriInspector.GroupDrawers
{
    public class TriVerticalGroupDrawer : TriGroupDrawer<DeclareVerticalGroupAttribute>
    {
        public override TriPropertyCollectionBaseElement CreateElement(DeclareVerticalGroupAttribute attribute)
        {
            return new TriVerticalGroupElement();
        }
    }
}