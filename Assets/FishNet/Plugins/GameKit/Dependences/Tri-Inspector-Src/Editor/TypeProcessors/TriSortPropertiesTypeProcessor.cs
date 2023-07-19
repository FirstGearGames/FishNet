using System;
using System.Collections.Generic;
using TriInspector;
using TriInspector.TypeProcessors;
using TriInspector.Utilities;

[assembly: RegisterTriTypeProcessor(typeof(TriSortPropertiesTypeProcessor), 10000)]

namespace TriInspector.TypeProcessors
{
    public class TriSortPropertiesTypeProcessor : TriTypeProcessor
    {
        public override void ProcessType(Type type, List<TriPropertyDefinition> properties)
        {
            foreach (var propertyDefinition in properties)
            {
                if (propertyDefinition.Attributes.TryGet(out PropertyOrderAttribute orderAttribute))
                {
                    propertyDefinition.Order = orderAttribute.Order;
                }
            }

            properties.Sort(PropertyOrderComparer.Instance);
        }

        private class PropertyOrderComparer : IComparer<TriPropertyDefinition>
        {
            public static readonly PropertyOrderComparer Instance = new PropertyOrderComparer();

            public int Compare(TriPropertyDefinition x, TriPropertyDefinition y)
            {
                return x.Order.CompareTo(y.Order);
            }
        }
    }
}