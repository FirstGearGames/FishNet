using FishNet.CodeGenerating.Extension;
using FishNet.CodeGenerating.Helping.Extension;
using FishNet.Object;
using FishNet.Serializing;
using MonoFN.Cecil;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace FishNet.CodeGenerating.Helping
{
    internal class WriterImports : CodegenBase
    {
        #region Reflection references.
        public MethodReference WriterPool_GetWriter_MethodRef;
        public MethodReference WriterPool_GetWriterLength_MethodRef;
        public MethodReference Writer_WritePackedWhole_Signed_MethodRef;
        public MethodReference Writer_WritePackedWhole_Unsigned_MethodRef;
        public TypeReference PooledWriter_TypeRef;
        public TypeReference Writer_TypeRef;
        public MethodReference PooledWriter_Dispose_MethodRef;
        public MethodReference Writer_WriteDictionary_MethodRef;
        public MethodReference Writer_WriteList_MethodRef;
        public MethodReference Writer_WriteArray_MethodRef;
        public TypeReference AutoPackTypeRef;

        public TypeReference GenericWriter_TypeRef;
        public MethodReference GenericWriter_Write_MethodRef;
        public MethodReference Writer_Write_MethodRef;
        #endregion

        /// <summary>
        /// Imports references needed by this helper.
        /// </summary>
        /// <param name="moduleDef"></param>
        /// <returns></returns>
        public override bool ImportReferences()
        {
            PooledWriter_TypeRef = base.ImportReference(typeof(PooledWriter));
            Writer_TypeRef = base.ImportReference(typeof(Writer));
            AutoPackTypeRef = base.ImportReference(typeof(AutoPackType));
            GenericWriter_TypeRef = base.ImportReference(typeof(GenericWriter<>));
            Writer_Write_MethodRef = Writer_TypeRef.CachedResolve(base.Session).GetMethodReference(base.Session, nameof(Writer.Write));


            TypeDefinition genericWriterTd = GenericWriter_TypeRef.CachedResolve(base.Session);
            GenericWriter_Write_MethodRef = base.ImportReference(genericWriterTd.GetMethod(nameof(GenericWriter<int>.SetWrite)));

            //WriterPool.GetWriter
            Type writerPoolType = typeof(WriterPool);
            base.ImportReference(writerPoolType);
            foreach (var methodInfo in writerPoolType.GetMethods())
            {
                if (methodInfo.Name == nameof(WriterPool.Retrieve))
                {
                    //GetWriter().
                    if (methodInfo.GetParameters().Length == 0)
                    {
                        WriterPool_GetWriter_MethodRef = base.ImportReference(methodInfo);
                    }
                    //GetWriter(?).
                    else if (methodInfo.GetParameters().Length == 1)
                    {
                        ParameterInfo pi = methodInfo.GetParameters()[0];
                        //GetWriter(int).
                        if (pi.ParameterType == typeof(int))
                            WriterPool_GetWriterLength_MethodRef = base.ImportReference(methodInfo);
                    }
                }
            }

            WriterProcessor gwh = base.GetClass<WriterProcessor>();
            Type pooledWriterType = typeof(PooledWriter);
            foreach (MethodInfo methodInfo in pooledWriterType.GetMethods())
            {
                int parameterCount = methodInfo.GetParameters().Length;

                if (methodInfo.Name == nameof(PooledWriter.Store))
                    PooledWriter_Dispose_MethodRef = base.ImportReference(methodInfo);
                else if (methodInfo.Name == nameof(PooledWriter.WriteUnsignedPackedWhole))
                { 
                    //todo: check if signed or not and set to signed/unsigned variable.
                    //do the same changes for methods which call these.
                    //Writer_WritePackedWhole_MethodRef = base.ImportReference(methodInfo);
                }
                //Relay writers.
                else if (parameterCount == 1 && methodInfo.Name == nameof(PooledWriter.WriteDictionary))
                    Writer_WriteDictionary_MethodRef = base.ImportReference(methodInfo);
                else if (parameterCount == 1 && methodInfo.Name == nameof(PooledWriter.WriteList))
                    Writer_WriteList_MethodRef = base.ImportReference(methodInfo);
                else if (parameterCount == 1 && methodInfo.Name == nameof(PooledWriter.WriteArray))
                    Writer_WriteArray_MethodRef = base.ImportReference(methodInfo);
            }

            return true;
        }

    }

}