using JetBrains.Annotations;

namespace TriInspector.Resolvers
{
    public static class ValueResolver
    {
        public static ValueResolver<T> Resolve<T>(TriPropertyDefinition propertyDefinition,
            string expression)
        {
            if (expression != null && expression.StartsWith("$"))
            {
                expression = expression.Substring(1);
            }

            if (StaticFieldValueResolver<T>.TryResolve(propertyDefinition, expression, out var sfr))
            {
                return sfr;
            }

            if (StaticPropertyValueResolver<T>.TryResolve(propertyDefinition, expression, out var spr))
            {
                return spr;
            }

            if (StaticMethodValueResolver<T>.TryResolve(propertyDefinition, expression, out var smr))
            {
                return smr;
            }

            if (InstanceFieldValueResolver<T>.TryResolve(propertyDefinition, expression, out var ifr))
            {
                return ifr;
            }

            if (InstancePropertyValueResolver<T>.TryResolve(propertyDefinition, expression, out var ipr))
            {
                return ipr;
            }

            if (InstanceMethodValueResolver<T>.TryResolve(propertyDefinition, expression, out var imr))
            {
                return imr;
            }

            return new ErrorValueResolver<T>(propertyDefinition, expression);
        }

        public static ValueResolver<string> ResolveString(TriPropertyDefinition propertyDefinition,
            string expression)
        {
            if (expression != null && expression.StartsWith("$"))
            {
                return Resolve<string>(propertyDefinition, expression.Substring(1));
            }

            return new ConstantValueResolver<string>(expression);
        }

        public static bool TryGetErrorString<T>([CanBeNull] ValueResolver<T> resolver, out string error)
        {
            return TryGetErrorString<T, T>(resolver, null, out error);
        }

        public static bool TryGetErrorString<T1, T2>(ValueResolver<T1> resolver1, ValueResolver<T2> resolver2,
            out string error)
        {
            if (resolver1 != null && resolver1.TryGetErrorString(out var error1))
            {
                error = error1;
                return true;
            }

            if (resolver2 != null && resolver2.TryGetErrorString(out var error2))
            {
                error = error2;
                return true;
            }

            error = null;
            return false;
        }
    }

    public abstract class ValueResolver<T>
    {
        [PublicAPI]
        public abstract bool TryGetErrorString(out string error);

        [PublicAPI]
        public abstract T GetValue(TriProperty property, T defaultValue = default);
    }

    public sealed class ConstantValueResolver<T> : ValueResolver<T>
    {
        private readonly T _value;

        public ConstantValueResolver(T value)
        {
            _value = value;
        }

        public override bool TryGetErrorString(out string error)
        {
            error = default;
            return false;
        }

        public override T GetValue(TriProperty property, T defaultValue = default)
        {
            return _value;
        }
    }
}