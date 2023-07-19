using JetBrains.Annotations;

namespace TriInspector.Resolvers
{
    public abstract class ActionResolver
    {
        public static ActionResolver Resolve(TriPropertyDefinition propertyDefinition, string method)
        {
            if (InstanceActionResolver.TryResolve(propertyDefinition, method, out var iar))
            {
                return iar;
            }

            return new ErrorActionResolver(propertyDefinition, method);
        }

        [PublicAPI]
        public abstract bool TryGetErrorString(out string error);

        [PublicAPI]
        public abstract void InvokeForTarget(TriProperty property, int targetIndex);

        [PublicAPI]
        public void InvokeForAllTargets(TriProperty property)
        {
            for (var targetIndex = 0; targetIndex < property.PropertyTree.TargetsCount; targetIndex++)
            {
                InvokeForTarget(property, targetIndex);
            }
        }
    }
}