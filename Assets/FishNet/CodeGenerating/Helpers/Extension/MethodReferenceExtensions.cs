using MonoFN.Cecil;
using MonoFN.Cecil.Rocks;
using System;

namespace FishNet.CodeGenerating.Helping.Extension
{

    public static class MethodReferenceExtensions
    {
        /// <summary>
        /// Makes a generic method with specified arguments.
        /// </summary>
        /// <param name="method"></param>
        /// <param name="genericArguments"></param>
        /// <returns></returns>
        public static GenericInstanceMethod MakeGenericMethod(this MethodReference method, params TypeReference[] genericArguments)
        {
            GenericInstanceMethod result = new GenericInstanceMethod(method);
            foreach (TypeReference argument in genericArguments)
                result.GenericArguments.Add(argument);
            return result;
        }

        /// <summary>
        /// Makes a generic method with the same arguments as the original.
        /// </summary>
        /// <param name="method"></param>
        /// <returns></returns>
        public static GenericInstanceMethod MakeGenericMethod(this MethodReference method)
        {
            GenericInstanceMethod result = new GenericInstanceMethod(method);
            foreach (ParameterDefinition pd in method.Parameters)
                result.GenericArguments.Add(pd.ParameterType);

            return result;
        }

        /// <summary>
        /// Gets a Resolve favoring cached results first.
        /// </summary>
        internal static MethodDefinition CachedResolve(this MethodReference methodRef)
        {
            return CodegenSession.GeneralHelper.GetMethodReferenceResolve(methodRef);
        }

        /// <summary>
        /// Given a method of a generic class such as ArraySegment`T.get_Count,
        /// and a generic instance such as ArraySegment`int
        /// Creates a reference to the specialized method  ArraySegment`int`.get_Count
        /// <para> Note that calling ArraySegment`T.get_Count directly gives an invalid IL error </para>
        /// </summary>
        /// <param name="self"></param>
        /// <param name="instanceType"></param>
        /// <returns></returns>
        public static MethodReference MakeHostInstanceGeneric(this MethodReference self, GenericInstanceType instanceType)
        {
            MethodReference reference = new MethodReference(self.Name, self.ReturnType, instanceType)
            {
                CallingConvention = self.CallingConvention,
                HasThis = self.HasThis,
                ExplicitThis = self.ExplicitThis
            };

            foreach (ParameterDefinition parameter in self.Parameters)
                reference.Parameters.Add(new ParameterDefinition(parameter.ParameterType));

            foreach (GenericParameter generic_parameter in self.GenericParameters)
                reference.GenericParameters.Add(new GenericParameter(generic_parameter.Name, reference));

            return CodegenSession.ImportReference(reference);
        }
        /// <summary>
        /// Given a method of a generic class such as ArraySegment`T.get_Count,
        /// and a generic instance such as ArraySegment`int
        /// Creates a reference to the specialized method  ArraySegment`int`.get_Count
        /// <para> Note that calling ArraySegment`T.get_Count directly gives an invalid IL error </para>
        /// </summary>
        /// <param name="self"></param>
        /// <param name="instanceType"></param>
        /// <returns></returns>
        public static MethodReference MakeHostInstanceGeneric(this MethodReference self, TypeReference typeRef, params TypeReference[] args)
        {

            GenericInstanceType git = typeRef.MakeGenericInstanceType(args);
            MethodReference reference = new MethodReference(self.Name, self.ReturnType, git)
            {
                CallingConvention = self.CallingConvention,
                HasThis = self.HasThis,
                ExplicitThis = self.ExplicitThis
            };

            foreach (ParameterDefinition parameter in self.Parameters)
                reference.Parameters.Add(new ParameterDefinition(parameter.ParameterType));

            foreach (GenericParameter generic_parameter in self.GenericParameters)
                reference.GenericParameters.Add(new GenericParameter(generic_parameter.Name, reference));

            return reference;
        }
        public static bool Is<T>(this MethodReference method, string name)
        {
            return method.DeclaringType.Is<T>() && method.Name == name;
        }
        public static bool Is<T>(this TypeReference td)
        {
            return Is(td, typeof(T));
        }

        public static bool Is(this TypeReference td, Type t)
        {
            if (t.IsGenericType)
            {
                return td.GetElementType().FullName == t.FullName;
            }
            return td.FullName == t.FullName;
        }



    }


}