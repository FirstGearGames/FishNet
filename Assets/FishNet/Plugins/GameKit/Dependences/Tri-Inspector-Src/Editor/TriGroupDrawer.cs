using System;
using JetBrains.Annotations;
using TriInspector.Elements;

namespace TriInspector
{
    public abstract class TriGroupDrawer
    {
        public abstract TriPropertyCollectionBaseElement CreateElementInternal(Attribute attribute);
    }

    public abstract class TriGroupDrawer<TAttribute> : TriGroupDrawer
        where TAttribute : Attribute
    {
        public sealed override TriPropertyCollectionBaseElement CreateElementInternal(Attribute attribute)
        {
            return CreateElement((TAttribute) attribute);
        }

        [PublicAPI]
        public abstract TriPropertyCollectionBaseElement CreateElement(TAttribute attribute);
    }
}