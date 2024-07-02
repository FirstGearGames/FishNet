using FishNet.CodeGenerating.Extension;
using FishNet.CodeGenerating.Helping.Extension;
using FishNet.Connection;
using FishNet.Serializing;
using MonoFN.Cecil;
using System;
using System.Reflection;

namespace FishNet.CodeGenerating.Helping
{
    internal class ReaderImports : CodegenBase
    {
        #region Reflection references.
        public TypeReference PooledReader_TypeRef;
        public TypeReference Reader_TypeRef;
        public TypeReference NetworkConnection_TypeRef;
        public MethodReference PooledReader_ReadNetworkBehaviour_MethodRef;
        public MethodReference Reader_ReadPackedWhole_MethodRef;
        public MethodReference Reader_ReadDictionary_MethodRef;
        public MethodReference Reader_ReadList_MethodRef;
        public MethodReference Reader_ReadArray_MethodRef;
        public TypeReference GenericReader_TypeRef;

        public MethodReference GenericReader_Read_MethodRef;
        #endregion

        /// <summary>
        /// Imports references needed by this helper.
        /// </summary>
        /// <param name="moduleDef"></param>
        /// <returns></returns>
        public override bool ImportReferences()
        {
            ReaderProcessor rp = base.GetClass<ReaderProcessor>();

            PooledReader_TypeRef = base.ImportReference(typeof(PooledReader));
            Reader_TypeRef = base.ImportReference(typeof(Reader));
            NetworkConnection_TypeRef = base.ImportReference(typeof(NetworkConnection));
            GenericReader_TypeRef = base.ImportReference(typeof(GenericReader<>));

            TypeDefinition genericWriterTd = GenericReader_TypeRef.CachedResolve(base.Session);
            GenericReader_Read_MethodRef = base.ImportReference(genericWriterTd.GetMethod(nameof(GenericReader<int>.SetRead)));

            Type pooledReaderType = typeof(PooledReader);
            foreach (MethodInfo methodInfo in pooledReaderType.GetMethods())
            {
                int parameterCount = methodInfo.GetParameters().Length;
                /* Special methods. */
                if (methodInfo.Name == nameof(PooledReader.ReadUnsignedPackedWhole))
                    Reader_ReadPackedWhole_MethodRef = base.ImportReference(methodInfo);
                //Relay readers.
                else if (parameterCount == 0 && methodInfo.Name == nameof(PooledReader.ReadDictionaryAllocated))
                    Reader_ReadDictionary_MethodRef = base.ImportReference(methodInfo);
                else if (parameterCount == 0 && methodInfo.Name == nameof(PooledReader.ReadListAllocated))
                    Reader_ReadList_MethodRef = base.ImportReference(methodInfo);
                else if (parameterCount == 0 && methodInfo.Name == nameof(PooledReader.ReadArrayAllocated))
                    Reader_ReadArray_MethodRef = base.ImportReference(methodInfo);
            }
             
            return true;
        }
    }
}