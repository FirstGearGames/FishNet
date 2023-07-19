using System;
using System.Reflection;
using UnityEngine;

namespace TriInspector.Resolvers
{
    internal sealed class InstanceActionResolver : ActionResolver
    {
        private readonly MethodInfo _methodInfo;

        public static bool TryResolve(TriPropertyDefinition propertyDefinition, string method,
            out ActionResolver resolver)
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
                if (methodInfo.Name == method &&
                    methodInfo.ReturnType == typeof(void) &&
                    methodInfo.GetParameters() is var parameterInfos &&
                    parameterInfos.Length == 0)
                {
                    resolver = new InstanceActionResolver(methodInfo);
                    return true;
                }
            }

            resolver = null;
            return false;
        }

        private InstanceActionResolver(MethodInfo methodInfo)
        {
            _methodInfo = methodInfo;
        }

        public override bool TryGetErrorString(out string error)
        {
            error = "";
            return false;
        }

        public override void InvokeForTarget(TriProperty property, int targetIndex)
        {
            var parentValue = property.Owner.GetValue(targetIndex);

            try
            {
                _methodInfo.Invoke(parentValue, Array.Empty<object>());
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }
    }
}