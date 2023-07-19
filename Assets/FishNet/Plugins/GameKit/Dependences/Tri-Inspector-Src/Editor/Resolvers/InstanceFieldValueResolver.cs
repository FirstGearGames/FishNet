using System;
using System.Reflection;
using UnityEngine;

namespace TriInspector.Resolvers
{
    internal sealed class InstanceFieldValueResolver<T> : ValueResolver<T>
    {
        private readonly FieldInfo _fieldInfo;

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

            foreach (var fieldInfo in parentType.GetFields(flags))
            {
                if (fieldInfo.Name == expression &&
                    typeof(T).IsAssignableFrom(fieldInfo.FieldType))
                {
                    resolver = new InstanceFieldValueResolver<T>(fieldInfo);
                    return true;
                }
            }

            resolver = null;
            return false;
        }

        private InstanceFieldValueResolver(FieldInfo fieldInfo)
        {
            _fieldInfo = fieldInfo;
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
                return (T) _fieldInfo.GetValue(parentValue);
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