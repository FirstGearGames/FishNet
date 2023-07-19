using System;
using System.Collections.Generic;
using TriInspector.Utilities;

namespace TriInspector
{
    public class TriTypeDefinition
    {
        private static readonly Dictionary<Type, TriTypeDefinition> Cache =
            new Dictionary<Type, TriTypeDefinition>();

        private TriTypeDefinition(IReadOnlyList<TriPropertyDefinition> properties)
        {
            Properties = properties;
        }

        public IReadOnlyList<TriPropertyDefinition> Properties { get; }

        public static TriTypeDefinition GetCached(Type type)
        {
            if (Cache.TryGetValue(type, out var definition))
            {
                return definition;
            }

            var processors = TriDrawersUtilities.AllTypeProcessors;
            var properties = new List<TriPropertyDefinition>();

            foreach (var processor in processors)
            {
                processor.ProcessType(type, properties);
            }

            return Cache[type] = new TriTypeDefinition(properties);
        }
    }
}