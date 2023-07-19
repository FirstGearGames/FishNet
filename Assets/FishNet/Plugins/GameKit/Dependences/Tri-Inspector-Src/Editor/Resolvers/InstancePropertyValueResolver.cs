using System;
using System.Reflection;
using UnityEngine;

namespace TriInspector.Resolvers
{
    internal sealed class InstancePropertyValueResolver<T> : ValueResolver<T>
    {
        private readonly PropertyInfo _propertyInfo;

        public static bool TryResolve(TriPropertyDefinition propertyDefinition, string expression,
            out ValueResolver<T> resolver)
        {
            var parentType = propertyDefinition.OwnerType;
            if (parentType == null)
            {
                resolver = null;
                return false;
            }

            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            foreach (var propertyInfo in parentType.GetProperties(flags))
            {
                if (propertyInfo.Name == expression &&
                    typeof(T).IsAssignableFrom(propertyInfo.PropertyType) &&
                    propertyInfo.CanRead)
                {
                    resolver = new InstancePropertyValueResolver<T>(propertyInfo);
                    return true;
                }
            }

            resolver = null;
            return false;
        }

        private InstancePropertyValueResolver(PropertyInfo propertyInfo)
        {
            _propertyInfo = propertyInfo;
        }

        public override bool TryGetErrorString(out string error)
        {
            error = "";
            return false;
        }

        public override T GetValue(TriProperty property, T defaultValue = default)
        {
            var parentValue = property.Owner.GetValue(0);

            try
            {
                return (T) _propertyInfo.GetValue(parentValue);
            }
            catch (Exception e)
            {
                if (e is TargetInvocationException targetInvocationException)
                {
                    e = targetInvocationException.InnerException;
                }

                Debug.LogException(e);
                return defaultValue;
            }
        }
    }
}