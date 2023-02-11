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
        public MethodReference Reader_ReadListCache_MethodRef;
        public MethodReference Reader_ReadArray_MethodRef;
        public TypeReference GenericReaderTypeRef;
        public TypeReference ReaderTypeRef;
        public MethodReference ReadSetMethodRef;
        public MethodReference ReadAutoPackSetMethodRef;
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

            GenericReaderTypeRef = base.ImportReference(typeof(GenericReader<>));
            ReaderTypeRef = base.ImportReference(typeof(Reader));

            System.Reflection.PropertyInfo readPropertyInfo;
            readPropertyInfo = typeof(GenericReader<>).GetProperty(nameof(GenericReader<int>.Read));
            ReadSetMethodRef = base.ImportReference(readPropertyInfo.GetSetMethod());
            readPropertyInfo = typeof(GenericReader<>).GetProperty(nameof(GenericReader<int>.ReadAutoPack));
            ReadAutoPackSetMethodRef = base.ImportReference(readPropertyInfo.GetSetMethod());


            Type pooledReaderType = typeof(PooledReader);
            foreach (MethodInfo methodInfo in pooledReaderType.GetMethods())
            {
                int parameterCount = methodInfo.GetParameters().Length;
                /* Special methods. */
                if (methodInfo.Name == nameof(PooledReader.ReadPackedWhole))
                    Reader_ReadPackedWhole_MethodRef = base.ImportReference(methodInfo);
                //Relay readers.
                else if (parameterCount == 0 && methodInfo.Name == nameof(PooledReader.ReadDictionary))
                    Reader_ReadDictionary_MethodRef = base.ImportReference(methodInfo);
                else if (parameterCount == 0 && methodInfo.Name == nameof(PooledReader.ReadListAllocated))
                    Reader_ReadList_MethodRef = base.ImportReference(methodInfo);
                else if (parameterCount == 0 && methodInfo.Name == nameof(PooledReader.ReadListCacheAllocated))
                    Reader_ReadListCache_MethodRef = base.ImportReference(methodInfo);
                else if (parameterCount == 0 && methodInfo.Name == nameof(PooledReader.ReadArrayAllocated))
                    Reader_ReadArray_MethodRef = base.ImportReference(methodInfo);
            }
             
            return true;
        }
    }
}