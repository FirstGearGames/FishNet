using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TriInspector.Elements;
using UnityEngine;

namespace TriInspector.Utilities
{
    internal class TriDrawersUtilities
    {
        private static readonly GenericTypeMatcher GroupDrawerMatcher = typeof(TriGroupDrawer<>);
        private static readonly GenericTypeMatcher ValueDrawerMatcher = typeof(TriValueDrawer<>);
        private static readonly GenericTypeMatcher AttributeDrawerMatcher = typeof(TriAttributeDrawer<>);
        private static readonly GenericTypeMatcher ValueValidatorMatcher = typeof(TriValueValidator<>);
        private static readonly GenericTypeMatcher AttributeValidatorMatcher = typeof(TriAttributeValidator<>);
        private static readonly GenericTypeMatcher HideProcessorMatcher = typeof(TriPropertyHideProcessor<>);
        private static readonly GenericTypeMatcher DisableProcessorMatcher = typeof(TriPropertyDisableProcessor<>);

        private static IDictionary<Type, TriGroupDrawer> _allGroupDrawersCacheBackingField;
        private static IReadOnlyList<RegisterTriAttributeDrawerAttribute> _allAttributeDrawerTypesBackingField;
        private static IReadOnlyList<RegisterTriValueDrawerAttribute> _allValueDrawerTypesBackingField;
        private static IReadOnlyList<RegisterTriAttributeValidatorAttribute> _allAttributeValidatorTypesBackingField;
        private static IReadOnlyList<RegisterTriValueValidatorAttribute> _allValueValidatorTypesBackingField;
        private static IReadOnlyList<RegisterTriPropertyHideProcessor> _allHideProcessorTypesBackingField;
        private static IReadOnlyList<RegisterTriPropertyDisableProcessor> _allDisableProcessorTypesBackingField;

        private static IReadOnlyList<TriTypeProcessor> _allTypeProcessorBackingField;

        private static IDictionary<Type, TriGroupDrawer> AllGroupDrawersCache
        {
            get
            {
                if (_allGroupDrawersCacheBackingField == null)
                {
                    _allGroupDrawersCacheBackingField = (
                        from asm in TriReflectionUtilities.Assemblies
                        from attr in asm.GetCustomAttributes<RegisterTriGroupDrawerAttribute>()
                        let groupAttributeType = GroupDrawerMatcher.MatchOut(attr.DrawerType, out var t) ? t : null
                        where groupAttributeType != null
                        select new KeyValuePair<Type, RegisterTriGroupDrawerAttribute>(groupAttributeType, attr)
                    ).ToDictionary(
                        it => it.Key,
                        it => (TriGroupDrawer) Activator.CreateInstance(it.Value.DrawerType));
                }

                return _allGroupDrawersCacheBackingField;
            }
        }

        public static IReadOnlyList<TriTypeProcessor> AllTypeProcessors
        {
            get
            {
                if (_allTypeProcessorBackingField == null)
                {
                    _allTypeProcessorBackingField = (
                        from asm in TriReflectionUtilities.Assemblies
                        from attr in asm.GetCustomAttributes<RegisterTriTypeProcessorAttribute>()
                        orderby attr.Order
                        select (TriTypeProcessor) Activator.CreateInstance(attr.ProcessorType)
                    ).ToList();
                }

                return _allTypeProcessorBackingField;
            }
        }

        public static IReadOnlyList<RegisterTriValueDrawerAttribute> AllValueDrawerTypes
        {
            get
            {
                if (_allValueDrawerTypesBackingField == null)
                {
                    _allValueDrawerTypesBackingField = (
                        from asm in TriReflectionUtilities.Assemblies
                        from attr in asm.GetCustomAttributes<RegisterTriValueDrawerAttribute>()
                        where ValueDrawerMatcher.Match(attr.DrawerType)
                        select attr
                    ).ToList();
                }

                return _allValueDrawerTypesBackingField;
            }
        }

        public static IReadOnlyList<RegisterTriAttributeDrawerAttribute> AllAttributeDrawerTypes
        {
            get
            {
                if (_allAttributeDrawerTypesBackingField == null)
                {
                    _allAttributeDrawerTypesBackingField = (
                        from asm in TriReflectionUtilities.Assemblies
                        from attr in asm.GetCustomAttributes<RegisterTriAttributeDrawerAttribute>()
                        where AttributeDrawerMatcher.Match(attr.DrawerType)
                        select attr
                    ).ToList();
                }

                return _allAttributeDrawerTypesBackingField;
            }
        }

        public static IReadOnlyList<RegisterTriValueValidatorAttribute> AllValueValidatorTypes
        {
            get
            {
                if (_allValueValidatorTypesBackingField == null)
                {
                    _allValueValidatorTypesBackingField = (
                        from asm in TriReflectionUtilities.Assemblies
                        from attr in asm.GetCustomAttributes<RegisterTriValueValidatorAttribute>()
                        where ValueValidatorMatcher.Match(attr.ValidatorType)
                        select attr
                    ).ToList();
                }

                return _allValueValidatorTypesBackingField;
            }
        }

        public static IReadOnlyList<RegisterTriAttributeValidatorAttribute> AllAttributeValidatorTypes
        {
            get
            {
                if (_allAttributeValidatorTypesBackingField == null)
                {
                    _allAttributeValidatorTypesBackingField = (
                        from asm in TriReflectionUtilities.Assemblies
                        from attr in asm.GetCustomAttributes<RegisterTriAttributeValidatorAttribute>()
                        where AttributeValidatorMatcher.Match(attr.ValidatorType)
                        select attr
                    ).ToList();
                }

                return _allAttributeValidatorTypesBackingField;
            }
        }

        public static IReadOnlyList<RegisterTriPropertyHideProcessor> AllHideProcessors
        {
            get
            {
                if (_allHideProcessorTypesBackingField == null)
                {
                    _allHideProcessorTypesBackingField = (
                        from asm in TriReflectionUtilities.Assemblies
                        from attr in asm.GetCustomAttributes<RegisterTriPropertyHideProcessor>()
                        where HideProcessorMatcher.Match(attr.ProcessorType)
                        select attr
                    ).ToList();
                }

                return _allHideProcessorTypesBackingField;
            }
        }

        public static IReadOnlyList<RegisterTriPropertyDisableProcessor> AllDisableProcessors
        {
            get
            {
                if (_allDisableProcessorTypesBackingField == null)
                {
                    _allDisableProcessorTypesBackingField = (
                        from asm in TriReflectionUtilities.Assemblies
                        from attr in asm.GetCustomAttributes<RegisterTriPropertyDisableProcessor>()
                        where DisableProcessorMatcher.Match(attr.ProcessorType)
                        select attr
                    ).ToList();
                }

                return _allDisableProcessorTypesBackingField;
            }
        }

        public static TriPropertyCollectionBaseElement TryCreateGroupElementFor(DeclareGroupBaseAttribute attribute)
        {
            if (!AllGroupDrawersCache.TryGetValue(attribute.GetType(), out var attr))
            {
                return null;
            }

            return attr.CreateElementInternal(attribute);
        }

        public static IEnumerable<TriValueDrawer> CreateValueDrawersFor(Type valueType)
        {
            return
                from drawer in AllValueDrawerTypes
                where ValueDrawerMatcher.Match(drawer.DrawerType, valueType)
                select CreateInstance<TriValueDrawer>(drawer.DrawerType, valueType, it =>
                {
                    it.ApplyOnArrayElement = drawer.ApplyOnArrayElement;
                    it.Order = drawer.Order;
                });
        }

        public static IEnumerable<TriAttributeDrawer> CreateAttributeDrawersFor(
            Type valueType, IReadOnlyList<Attribute> attributes)
        {
            return
                from attribute in attributes
                from drawer in AllAttributeDrawerTypes
                where AttributeDrawerMatcher.Match(drawer.DrawerType, attribute.GetType())
                select CreateInstance<TriAttributeDrawer>(drawer.DrawerType, valueType, it =>
                {
                    it.ApplyOnArrayElement = drawer.ApplyOnArrayElement;
                    it.Order = drawer.Order;
                    it.RawAttribute = attribute;
                });
        }

        public static IEnumerable<TriValueValidator> CreateValueValidatorsFor(Type valueType)
        {
            return
                from validator in AllValueValidatorTypes
                where ValueValidatorMatcher.Match(validator.ValidatorType, valueType)
                select CreateInstance<TriValueValidator>(validator.ValidatorType, valueType, it =>
                {
                    //
                    it.ApplyOnArrayElement = validator.ApplyOnArrayElement;
                });
        }

        public static IEnumerable<TriAttributeValidator> CreateAttributeValidatorsFor(
            Type valueType, IReadOnlyList<Attribute> attributes)
        {
            return
                from attribute in attributes
                from validator in AllAttributeValidatorTypes
                where AttributeValidatorMatcher.Match(validator.ValidatorType, attribute.GetType())
                select CreateInstance<TriAttributeValidator>(validator.ValidatorType, valueType, it =>
                {
                    it.ApplyOnArrayElement = validator.ApplyOnArrayElement;
                    it.RawAttribute = attribute;
                });
        }

        public static IEnumerable<TriPropertyHideProcessor> CreateHideProcessorsFor(
            Type valueType, IReadOnlyList<Attribute> attributes)
        {
            return
                from attribute in attributes
                from processor in AllHideProcessors
                where HideProcessorMatcher.Match(processor.ProcessorType, attribute.GetType())
                select CreateInstance<TriPropertyHideProcessor>(
                    processor.ProcessorType, valueType, it =>
                    {
                        it.ApplyOnArrayElement = processor.ApplyOnArrayElement;
                        it.RawAttribute = attribute;
                    });
        }

        public static IEnumerable<TriPropertyDisableProcessor> CreateDisableProcessorsFor(
            Type valueType, IReadOnlyList<Attribute> attributes)
        {
            return
                from attribute in attributes
                from processor in AllDisableProcessors
                where DisableProcessorMatcher.Match(processor.ProcessorType, attribute.GetType())
                select CreateInstance<TriPropertyDisableProcessor>(
                    processor.ProcessorType, valueType, it =>
                    {
                        it.ApplyOnArrayElement = processor.ApplyOnArrayElement;
                        it.RawAttribute = attribute;
                    });
        }

        private static T CreateInstance<T>(Type type, Type argType, Action<T> setup)
        {
            if (type.IsGenericType)
            {
                type = type.MakeGenericType(argType);
            }

            var instance = (T) Activator.CreateInstance(type);
            setup(instance);
            return instance;
        }

        private class GenericTypeMatcher
        {
            private readonly Dictionary<Type, (bool, Type)> _cache = new Dictionary<Type, (bool, Type)>();
            private readonly Type _expectedGenericType;

            private GenericTypeMatcher(Type expectedGenericType)
            {
                _expectedGenericType = expectedGenericType;
            }

            public static implicit operator GenericTypeMatcher(Type expectedGenericType)
            {
                return new GenericTypeMatcher(expectedGenericType);
            }

            public bool Match(Type type, Type targetType)
            {
                return MatchOut(type, out var constraint) &&
                       constraint.IsAssignableFrom(targetType);
            }

            public bool Match(Type type)
            {
                return MatchOut(type, out _);
            }

            public bool MatchOut(Type type, out Type targetType)
            {
                if (_cache.TryGetValue(type, out var cachedResult))
                {
                    targetType = cachedResult.Item2;
                    return cachedResult.Item1;
                }

                var succeed = MatchInternal(type, out targetType);
                _cache[type] = (succeed, targetType);
                return succeed;
            }

            private bool MatchInternal(Type type, out Type targetType)
            {
                targetType = null;

                if (type.IsAbstract)
                {
                    Debug.LogError($"{type.Name} must be non abstract");
                    return false;
                }

                if (type.GetConstructor(Type.EmptyTypes) == null)
                {
                    Debug.LogError($"{type.Name} must have a parameterless constructor");
                    return false;
                }

                Type genericArgConstraints = null;
                if (type.IsGenericType)
                {
                    var genericArg = type.GetGenericArguments().SingleOrDefault();

                    if (genericArg == null ||
                        genericArg.GenericParameterAttributes != GenericParameterAttributes.None)
                    {
                        Debug.LogError(
                            $"{type.Name} must contains only one generic arg with simple constant e.g. <where T : bool>");
                        return false;
                    }

                    genericArgConstraints = genericArg.GetGenericParameterConstraints().SingleOrDefault();
                }

                var drawerType = type.BaseType;

                while (drawerType != null)
                {
                    if (drawerType.IsGenericType &&
                        drawerType.GetGenericTypeDefinition() == _expectedGenericType)
                    {
                        targetType = drawerType.GetGenericArguments()[0];

                        if (targetType.IsGenericParameter)
                        {
                            if (genericArgConstraints == null)
                            {
                                Debug.LogError(
                                    $"{type.Name} must contains only one generic arg with simple constant e.g. <where T : bool>");
                                return false;
                            }

                            targetType = genericArgConstraints;
                        }

                        return true;
                    }

                    drawerType = drawerType.BaseType;
                }

                Debug.LogError($"{type.Name} must implement {_expectedGenericType}");
                return false;
            }
        }
    }
}