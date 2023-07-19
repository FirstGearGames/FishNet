using System;
using JetBrains.Annotations;

namespace TriInspector
{
    public abstract class TriPropertyHideProcessor : TriPropertyExtension
    {
        internal Attribute RawAttribute { get; set; }

        [PublicAPI]
        public abstract bool IsHidden(TriProperty property);
    }

    public abstract class TriPropertyHideProcessor<TAttribute> : TriPropertyHideProcessor
        where TAttribute : Attribute
    {
        [PublicAPI]
        public TAttribute Attribute => (TAttribute) RawAttribute;
    }
}