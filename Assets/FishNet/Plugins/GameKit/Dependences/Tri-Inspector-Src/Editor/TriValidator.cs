using System;
using JetBrains.Annotations;

namespace TriInspector
{
    public abstract class TriValidator : TriPropertyExtension
    {
        [PublicAPI]
        public abstract TriValidationResult Validate(TriProperty property);
    }

    public abstract class TriAttributeValidator : TriValidator
    {
        internal Attribute RawAttribute { get; set; }
    }

    public abstract class TriAttributeValidator<TAttribute> : TriAttributeValidator
        where TAttribute : Attribute
    {
        [PublicAPI]
        public TAttribute Attribute => (TAttribute) RawAttribute;
    }

    public abstract class TriValueValidator : TriValidator
    {
    }

    public abstract class TriValueValidator<T> : TriValueValidator
    {
        public sealed override TriValidationResult Validate(TriProperty property)
        {
            return Validate(new TriValue<T>(property));
        }

        [PublicAPI]
        public abstract TriValidationResult Validate(TriValue<T> propertyValue);
    }
}