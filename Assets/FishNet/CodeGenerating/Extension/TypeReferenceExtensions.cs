﻿
using FishNet.CodeGenerating.Helping;
using FishNet.CodeGenerating.Helping.Extension;
using MonoFN.Cecil;
using UnityEngine;

namespace FishNet.CodeGenerating.Extension
{


    internal static class TypeReferenceExtensions
    {

        /// <summary>
        /// Returns if a TypeReference is nullable.
        /// </summary>
        public static bool IsNullable(this TypeReference tr, CodegenSession session)
        {
            TypeDefinition td = tr.CachedResolve(session);
            return td.IsNullable();
        }

        /// <summary>
        /// Returns the fullname of a TypeReference without <>.
        /// </summary>
        /// <param name="tr"></param>
        /// <returns></returns>
        public static string GetFullnameWithoutBrackets(this TypeReference tr)
        {
            string str = tr.FullName;
            str = str.Replace("<", "");
            str = str.Replace(">", "");
            return str;
        }

        /// <summary>
        /// Returns a method in the next base class.
        /// </summary>
        public static MethodReference GetMethodInBase(this TypeReference tr, CodegenSession session, string methodName)
        {
            return tr.CachedResolve(session).GetMethodInBase(session, methodName);
        }

		/// <summary>
		/// Makes a GenericInstanceType.
		/// </summary>
		public static GenericInstanceType MakeGenericInstanceType(this TypeReference self)
		{
			GenericInstanceType instance = new GenericInstanceType(self);
			foreach (GenericParameter argument in self.GenericParameters)
				instance.GenericArguments.Add(argument);

			return instance;
		}

	}


}