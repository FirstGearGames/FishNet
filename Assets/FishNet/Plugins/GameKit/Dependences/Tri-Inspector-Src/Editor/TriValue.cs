using System;
using JetBrains.Annotations;

namespace TriInspector
{
    public struct TriValue<T>
    {
        internal TriValue(TriProperty property)
        {
            Property = property;
        }

        public TriProperty Property { get; }

        [Obsolete("Use SmartValue instead", true)]
        public T Value
        {
            get => (T) Property.Value;
            set => Property.SetValue(value);
        }

        [PublicAPI]
        public T SmartValue
        {
            get => (T) Property.Value;
            set
            {
                if (Property.Comparer.Equals(Property.Value, value))
                {
                    return;
                }

                Property.SetValue(value);
            }
        }
    }
}