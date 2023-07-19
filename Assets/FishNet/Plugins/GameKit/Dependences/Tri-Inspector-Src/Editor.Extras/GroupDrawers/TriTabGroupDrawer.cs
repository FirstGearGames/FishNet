using TriInspector;
using TriInspector.Elements;
using TriInspector.GroupDrawers;

[assembly: RegisterTriGroupDrawer(typeof(TriTabGroupDrawer))]

namespace TriInspector.GroupDrawers
{
    public class TriTabGroupDrawer : TriGroupDrawer<DeclareTabGroupAttribute>
    {
        public override TriPropertyCollectionBaseElement CreateElement(DeclareTabGroupAttribute attribute)
        {
            return new TriTabGroupElement();
        }
    }
}