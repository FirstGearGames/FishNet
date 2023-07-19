using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;

namespace TriInspector.Utilities
{
    internal static class TriReflectionUtilities
    {
        private static readonly Dictionary<Type, IReadOnlyList<Attribute>> AttributesCache =
            new Dictionary<Type, IReadOnlyList<Attribute>>();

        private static IReadOnlyList<Assembly> _assemblies;
        private static IReadOnlyList<Type> _allNonAbstractTypesBackingField;

        public static IReadOnlyList<Assembly> Assemblies
        {
            get
            {
                if (_assemblies == null)
                {
                    _assemblies = AppDomain.CurrentDomain.GetAssemblies();
                }

                return _assemblies;
            }
        }

        public static IReadOnlyList<Type> AllNonAbstractTypes
        {
            get
            {
                if (_allNonAbstractTypesBackingField == null)
                {
                    _allNonAbstractTypesBackingField = Assemblies
                        .SelectMany(asm =>
                        {
                            try
                            {
                                return asm.GetTypes();
                            }
                            catch (ReflectionTypeLoadException)
                            {
                                return Array.Empty<Type>();
                            }
                        })
                        .Where(type => !type.IsAbstract)
                        .ToList();
                }

                return _allNonAbstractTypesBackingField;
            }
        }

        public static IReadOnlyList<Attribute> GetAttributesCached(Type type)
        {
            if (AttributesCache.TryGetValue(type, out var attributes))
            {
                return attributes;
            }

            return AttributesCache[type] = type.GetCustomAttributes().ToList();
        }

        public static IReadOnlyList<T> GetCustomAttributes<T>(this Assembly asm)
        {
            return asm.GetCustomAttributes(typeof(T)).Cast<T>().ToList();
        }

        public static IReadOnlyList<FieldInfo> GetAllInstanceFieldsInDeclarationOrder(Type type)
        {
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic |
                                       BindingFlags.Instance | BindingFlags.DeclaredOnly;

            return GetAllMembersInDeclarationOrder(type, it => it.GetFields(flags));
        }

        public static IReadOnlyList<PropertyInfo> GetAllInstancePropertiesInDeclarationOrder(Type type)
        {
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic |
                                       BindingFlags.Instance | BindingFlags.DeclaredOnly;

            return GetAllMembersInDeclarationOrder(type, it => it.GetProperties(flags));
        }

        public static IReadOnlyList<MethodInfo> GetAllInstanceMethodsInDeclarationOrder(Type type)
        {
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic |
                                       BindingFlags.Instance | BindingFlags.DeclaredOnly;

            return GetAllMembersInDeclarationOrder(type, it => it.GetMethods(flags));
        }

        public static bool IsArrayOrList(Type type, out Type elementType)
        {
            if (type.IsArray)
            {
                elementType = type.GetElementType();
                return true;
            }

            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
            {
                elementType = type.GetGenericArguments().Single();
                return true;
            }

            elementType = null;
            return false;
        }

        public static Type GetUnityEditorTypeByFullName(string name)
        {
            return GetTypeByFullName(name, typeof(Editor).Assembly);
        }

        public static Type GetTypeByFullName(string name, Assembly assembly)
        {
            return assembly
                .GetTypes()
                .Single(it => it.FullName == name);
        }

        public static bool TryFindTypeByFullName(string name, out Type type)
        {
            type = Type.GetType(name);
            if (type != null)
            {
                return true;
            }

            foreach (var assembly in Assemblies)
            {
                type = assembly.GetType(name);
                if (type != null)
                {
                    return true;
                }
            }

            return false;
        }

        private static IReadOnlyList<T> GetAllMembersInDeclarationOrder<T>(
            Type type, Func<Type, T[]> select)
            where T : MemberInfo
        {
            var result = new List<T>();
            var typeTree = new Stack<Type>();

            while (type != null)
            {
                typeTree.Push(type);
                type = type.BaseType;
            }

            foreach (var t in typeTree)
            {
                var items = select(t);
                result.AddRange(items);
            }

            return result;
        }
    }
}