using System;
using System.Reflection;
using UnityEngine;

namespace TriInspector.Resolvers
{
    internal sealed class InstanceMethodValueResolver<T> : ValueResolver<T>
    {
        private readonly MethodInfo _methodInfo;

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

            foreach (var methodInfo in parentType.GetMethods(flags))
            {
                if (methodInfo.Name == expression &&
                    typeof(T).IsAssignableFrom(methodInfo.ReturnType) &&
                    methodInfo.GetParameters() is var parameterInfos &&
                    parameterInfos.Length == 0)
                {
                    resolver = new InstanceMethodValueResolver<T>(methodInfo);
                    return true;
                }
            }

            resolver = null;
            return false;
        }

        private InstanceMethodValueResolver(MethodInfo methodInfo)
        {
            _methodInfo = methodInfo;
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
                return (T) _methodInfo.Invoke(parentValue, Array.Empty<object>());
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