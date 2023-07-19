using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

namespace TriInspector.Utilities
{
    public static class TriEqualityComparer
    {
        private static readonly Dictionary<Type, IEqualityComparer> Cache = new Dictionary<Type, IEqualityComparer>();

        public static IEqualityComparer Of(Type type)
        {
            if (!Cache.TryGetValue(type, out var comparer))
            {
                Cache[type] = comparer = CreateDefaultEqualityComparer(type);
            }

            return comparer;
        }

        private static IEqualityComparer CreateDefaultEqualityComparer(Type type)
        {
            var comparerType = typeof(EqualityComparer<>).MakeGenericType(type);
            var comparerProperty = comparerType.GetProperty("Default", BindingFlags.Static | BindingFlags.Public);
            var comparer = (IEqualityComparer) comparerProperty?.GetValue(null);

            if (comparer == null)
            {
                throw new InvalidOperationException($"Failed to create default comparer for type {type}");
            }
            
            return comparer;
        }
    }
}