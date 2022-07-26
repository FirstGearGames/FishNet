
using FishNet.CodeGenerating.Helping;
using FishNet.CodeGenerating.Helping.Extension;
using MonoFN.Cecil;
using UnityEngine;

namespace FishNet.CodeGenerating.Extension
{


    internal static class TypeReferenceExtensions
    {

        /// <summary>
        /// Returns a method in the next base class.
        /// </summary>
        public static MethodReference GetMethodInBase(this TypeReference tr, string methodName)
        {
            return GetMethodInBase(tr.CachedResolve(), methodName);
        }
    }


}