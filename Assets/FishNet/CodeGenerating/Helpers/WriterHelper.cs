﻿using FishNet.CodeGenerating.Helping.Extension;
using FishNet.CodeGenerating.ILCore;
using FishNet.Object;
using FishNet.Serializing;
using MonoFN.Cecil;
using MonoFN.Cecil.Cil;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace FishNet.CodeGenerating.Helping
{
    internal class WriterHelper : CodegenBase
    {
        #region Reflection references.
        private MethodReference WriterPool_GetWriter_MethodRef;
        private MethodReference WriterPool_GetWriterLength_MethodRef;
        private MethodReference Writer_WritePackedWhole_MethodRef;
        internal TypeReference PooledWriter_TypeRef;
        internal TypeReference Writer_TypeRef;
        internal readonly Dictionary<TypeReference, MethodReference> _instancedWriterMethods = new Dictionary<TypeReference, MethodReference>(new TypeReferenceComparer());
        private readonly Dictionary<TypeReference, MethodReference> _staticWriterMethods = new Dictionary<TypeReference, MethodReference>(new TypeReferenceComparer());
        private HashSet<TypeReference> _autoPackedMethods = new HashSet<TypeReference>(new TypeReferenceComparer());
        private MethodReference PooledWriter_Dispose_MethodRef;
        internal MethodReference Writer_WriteDictionary_MethodRef;
        internal TypeReference NetworkBehaviour_TypeRef;
        #endregion

        #region Const.
        internal const string WRITE_PREFIX = "Write";
        /// <summary>
        /// Types to exclude from being scanned for auto serialization.
        /// </summary>
        public static readonly System.Type[] EXCLUDED_AUTO_SERIALIZER_TYPES = new System.Type[]
        {
            typeof(NetworkBehaviour)
        };
        /// <summary>
        /// Types within assemblies which begin with these prefixes will not have serializers created for them.
        /// </summary>
        public static readonly string[] EXCLUDED_ASSEMBLY_PREFIXES = new string[]
        {
            "UnityEngine."
        };
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
            NetworkBehaviour_TypeRef = base.ImportReference(typeof(NetworkBehaviour));

            //WriterPool.GetWriter
            Type writerPoolType = typeof(WriterPool);
            foreach (var methodInfo in writerPoolType.GetMethods())
            {
                if (methodInfo.Name == nameof(WriterPool.GetWriter))
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

            Type pooledWriterType = typeof(PooledWriter);
            foreach (MethodInfo methodInfo in pooledWriterType.GetMethods())
            {
                /* Special methods. */
                //Write.Dispose.
                if (methodInfo.Name == nameof(PooledWriter.Dispose))
                {
                    PooledWriter_Dispose_MethodRef = base.ImportReference(methodInfo);
                    continue;
                }
                //WritePackedWhole.
                else if (methodInfo.Name == nameof(PooledWriter.WritePackedWhole))
                {
                    Writer_WritePackedWhole_MethodRef = base.ImportReference(methodInfo);
                    continue;
                }
                //WriteDictionary.
                else if (methodInfo.Name == nameof(PooledWriter.WriteDictionary))
                {
                    Writer_WriteDictionary_MethodRef = base.ImportReference(methodInfo);
                    continue;
                }

                else if (base.GetClass<GeneralHelper>().CodegenExclude(methodInfo))
                    continue;
                //Generic methods are not supported.
                else if (methodInfo.IsGenericMethod)
                    continue;
                //Not long enough to be a write method.
                else if (methodInfo.Name.Length < WRITE_PREFIX.Length)
                    continue;
                //Method name doesn't start with writePrefix.
                else if (methodInfo.Name.Substring(0, WRITE_PREFIX.Length) != WRITE_PREFIX)
                    continue;

                ParameterInfo[] parameterInfos = methodInfo.GetParameters();
                /* No parameters or more than 2 parameters. Most Write methods
                * will have only 1 parameter but some will have 2 if
                * there is a pack option. */
                if (parameterInfos.Length < 1 || parameterInfos.Length > 2)
                    continue;
                /* If two parameters make sure the second parameter
                 * is a pack parameter. */
                bool autoPackMethod = false;
                if (parameterInfos.Length == 2)
                {
                    autoPackMethod = (parameterInfos[1].ParameterType == typeof(AutoPackType));
                    if (!autoPackMethod)
                        continue;
                }
                //First parameter is generic; these are not supported.
                if (parameterInfos[0].ParameterType.IsGenericParameter)
                    continue;


                /* TypeReference for the first parameter in the write method. 
                 * The first parameter will always be the type written. */
                TypeReference typeRef = base.ImportReference(parameterInfos[0].ParameterType);
                /* If here all checks pass. */
                MethodReference methodRef = base.ImportReference(methodInfo);
                AddWriterMethod(typeRef, methodRef, true, true);
                if (autoPackMethod)
                    _autoPackedMethods.Add(typeRef);
            }

            Type writerExtensionsType = typeof(WriterExtensions);
            foreach (MethodInfo methodInfo in writerExtensionsType.GetMethods())
            {
                if (base.GetClass<GeneralHelper>().CodegenExclude(methodInfo))
                    continue;
                //Generic methods are not supported.
                if (methodInfo.IsGenericMethod)
                    continue;
                //Not static.
                if (!methodInfo.IsStatic)
                    continue;
                //Not long enough to be a write method.
                if (methodInfo.Name.Length < WRITE_PREFIX.Length)
                    continue;
                //Method name doesn't start with writePrefix.
                if (methodInfo.Name.Substring(0, WRITE_PREFIX.Length) != WRITE_PREFIX)
                    continue;
                ParameterInfo[] parameterInfos = methodInfo.GetParameters();
                /* No parameters or more than 3 parameters. Most extension Write methods
                 * will have only 2 parameter but some will have 3 if
                 * there is a pack option. */
                if (parameterInfos.Length < 2 || parameterInfos.Length > 3)
                    continue;
                /* If 3 parameters make sure the 3rd parameter
                 * is a pack parameter. */
                bool autoPackMethod = false;
                if (parameterInfos.Length == 3)
                {
                    autoPackMethod = (parameterInfos[2].ParameterType == typeof(AutoPackType));
                    if (!autoPackMethod)
                        continue;
                }
                //First parameter is generic; these are not supported.
                if (parameterInfos[1].ParameterType.IsGenericParameter)
                    continue;

                /* TypeReference for the second parameter in the write method.
                 * The first parameter will always be the type written. */
                TypeReference typeRef = base.ImportReference(parameterInfos[1].ParameterType);
                /* If here all checks pass. */
                MethodReference methodRef = base.ImportReference(methodInfo);
                AddWriterMethod(typeRef, methodRef, false, true);
            }

            return true;
        }

        /// <summary>
        /// Creates generic write delegates for all currently known write types.
        /// </summary>
        internal bool CreateGenericDelegates()
        {
            /* Only write statics. This will include extensions and generated. */
            foreach (KeyValuePair<TypeReference, MethodReference> item in _staticWriterMethods)
                base.GetClass<GenericWriterHelper>().CreateWriteDelegate(item.Value, true);

            return true;
        }

        /// <summary>
        /// Returns if typeRef has a serializer.
        /// </summary>
        /// <param name="typeRef"></param>
        /// <returns></returns>
        internal bool HasSerializer(TypeReference typeRef, bool createMissing)
        {
            bool result = (GetInstancedWriteMethodReference(typeRef) != null) ||
                (GetStaticWriteMethodReference(typeRef) != null);

            if (!result && createMissing)
            {
                if (!base.GetClass<GeneralHelper>().HasNonSerializableAttribute(typeRef.CachedResolve(base.Session)))
                {
                    MethodReference methodRef = base.GetClass<WriterGenerator>().CreateWriter(typeRef);
                    result = (methodRef != null);
                }
            }

            return result;
        }


        #region GetWriterMethodReference.
        /// <summary>
        /// Returns the MethodReference for typeRef.
        /// </summary>
        /// <param name="typeRef"></param>
        /// <returns></returns>
        internal MethodReference GetInstancedWriteMethodReference(TypeReference typeRef)
        {
            _instancedWriterMethods.TryGetValue(typeRef, out MethodReference methodRef);
            return methodRef;
        }
        /// <summary>
        /// Returns the MethodReference for typeRef.
        /// </summary>
        /// <param name="typeRef"></param>
        /// <returns></returns>
        internal MethodReference GetStaticWriteMethodReference(TypeReference typeRef)
        {
            _staticWriterMethods.TryGetValue(typeRef, out MethodReference methodRef);
            return methodRef;
        }
        /// <summary>
        /// Returns the MethodReference for typeRef favoring instanced or static.
        /// </summary>
        /// <param name="typeRef"></param>
        /// <param name="favorInstanced"></param>
        /// <returns></returns>
        internal MethodReference GetFavoredWriteMethodReference(TypeReference typeRef, bool favorInstanced)
        {
            MethodReference result;
            if (favorInstanced)
            {
                result = GetInstancedWriteMethodReference(typeRef);
                if (result == null)
                    result = GetStaticWriteMethodReference(typeRef);
            }
            else
            {
                result = GetStaticWriteMethodReference(typeRef);
                if (result == null)
                    result = GetInstancedWriteMethodReference(typeRef);
            }

            return result;
        }
        /// <summary>
        /// Gets the write MethodRef for typeRef, or tries to create it if not present.
        /// </summary>
        /// <param name="typeRef"></param>
        /// <returns></returns>
        internal MethodReference GetOrCreateFavoredWriteMethodReference(TypeReference typeRef, bool favorInstanced)
        {
            //Try to get existing writer, if not present make one.
            MethodReference writeMethodRef = GetFavoredWriteMethodReference(typeRef, favorInstanced);

            if (writeMethodRef == null)
                writeMethodRef = base.GetClass<WriterGenerator>().CreateWriter(typeRef);
            if (writeMethodRef == null)
                base.LogError($"Could not create serializer for {typeRef.FullName}.");

            return writeMethodRef;
        }
        #endregion

        /// <summary>
        /// Adds typeRef, methodDef to InstancedWriterMethods.
        /// </summary>
        /// <param name="typeRef"></param>
        /// <param name="methodRef"></param>
        /// <param name="useAdd"></param>
        internal void AddWriterMethod(TypeReference typeRef, MethodReference methodRef, bool instanced, bool useAdd)
        {
            Dictionary<TypeReference, MethodReference> dict = (instanced) ?
            _instancedWriterMethods : _staticWriterMethods;

            if (useAdd)
                dict.Add(typeRef, methodRef);
            else
                dict[typeRef] = methodRef;
        }

        /// <summary>
        /// Removes typeRef from Static or InstancedWriterMethods.
        /// </summary>
        internal void RemoveWriterMethod(TypeReference typeRef, bool instanced)
        {
            Dictionary<TypeReference, MethodReference> dict = (instanced) ?
            _instancedWriterMethods : _staticWriterMethods;

            dict.Remove(typeRef);
        }

        /// <summary>
        /// Creates a PooledWriter within the body/ and returns its variable index.
        /// EG: PooledWriter writer = WriterPool.GetWriter();
        /// </summary>
        internal VariableDefinition CreatePooledWriter(MethodDefinition methodDef, int length)
        {
            VariableDefinition resultVd;
            List<Instruction> insts = CreatePooledWriter(methodDef, length, out resultVd);

            ILProcessor processor = methodDef.Body.GetILProcessor();
            processor.Add(insts);
            return resultVd;
        }
        /// <summary>
        /// Creates a PooledWriter within the body/ and returns its variable index.
        /// EG: PooledWriter writer = WriterPool.GetWriter();
        /// </summary>
        /// <param name="processor"></param>
        /// <param name="methodDef"></param>
        /// <returns></returns>
        internal List<Instruction> CreatePooledWriter(MethodDefinition methodDef, int length, out VariableDefinition resultVd)
        {
            List<Instruction> insts = new List<Instruction>();
            ILProcessor processor = methodDef.Body.GetILProcessor();

            resultVd = base.GetClass<GeneralHelper>().CreateVariable(methodDef, PooledWriter_TypeRef);
            //If length is specified then pass in length.
            if (length > 0)
            {
                insts.Add(processor.Create(OpCodes.Ldc_I4, length));
                insts.Add(processor.Create(OpCodes.Call, WriterPool_GetWriterLength_MethodRef));
            }
            //Use parameter-less method if no length.
            else
            {
                insts.Add(processor.Create(OpCodes.Call, WriterPool_GetWriter_MethodRef));
            }
            //Set value to variable definition.
            insts.Add(processor.Create(OpCodes.Stloc, resultVd));
            return insts;
        }


        /// <summary>
        /// Calls Dispose on a PooledWriter.
        /// EG: writer.Dispose();
        /// </summary>
        /// <param name="processor"></param>
        /// <param name="writerDefinition"></param>
        internal List<Instruction> DisposePooledWriter(MethodDefinition methodDef, VariableDefinition writerDefinition)
        {
            List<Instruction> insts = new List<Instruction>();
            ILProcessor processor = methodDef.Body.GetILProcessor();

            insts.Add(processor.Create(OpCodes.Ldloc, writerDefinition));
            insts.Add(processor.Create(OpCodes.Callvirt, PooledWriter_Dispose_MethodRef));

            return insts;
        }

        /// <summary>
        /// Returns if typeRef supports auto packing.
        /// </summary>
        /// <param name="typeRef"></param>
        /// <returns></returns>
        internal bool IsAutoPackedType(TypeReference typeRef)
        {
            return _autoPackedMethods.Contains(typeRef);
        }

        /// <summary>
        /// Creates a null check on the second argument using a boolean.
        /// </summary>
        internal void CreateRetOnNull(ILProcessor processor, ParameterDefinition writerParameterDef, ParameterDefinition checkedParameterDef, bool useBool)
        {
            Instruction endIf = processor.Create(OpCodes.Nop);
            //If (value) jmp to endIf.
            processor.Emit(OpCodes.Ldarg, checkedParameterDef);
            processor.Emit(OpCodes.Brtrue, endIf);
            //writer.WriteBool / writer.WritePackedWhole
            if (useBool)
                CreateWriteBool(processor, writerParameterDef, true);
            else
                CreateWritePackedWhole(processor, writerParameterDef, -1);
            //Exit method.
            processor.Emit(OpCodes.Ret);
            //End of if check.
            processor.Append(endIf);
        }

        #region CreateWritePackWhole
        /// <summary>
        /// Creates a call to WritePackWhole with value.
        /// </summary>
        /// <param name="processor"></param>
        /// <param name="value"></param>
        internal void CreateWritePackedWhole(ILProcessor processor, ParameterDefinition writerParameterDef, int value)
        {
            //Create local int and set it to value.
            VariableDefinition intVariableDef = base.GetClass<GeneralHelper>().CreateVariable(processor.Body.Method, typeof(int));
            base.GetClass<GeneralHelper>().SetVariableDefinitionFromInt(processor, intVariableDef, value);
            //Writer.
            processor.Emit(OpCodes.Ldarg, writerParameterDef);
            //Writer.WritePackedWhole(value).
            processor.Emit(OpCodes.Ldloc, intVariableDef);
            processor.Emit(OpCodes.Conv_U8);
            processor.Emit(OpCodes.Callvirt, Writer_WritePackedWhole_MethodRef);
        }
        /// <summary>
        /// Creates a call to WritePackWhole with value.
        /// </summary>
        /// <param name="processor"></param>
        /// <param name="value"></param>
        internal void CreateWritePackedWhole(ILProcessor processor, ParameterDefinition writerParameterDef, VariableDefinition value)
        {
            //Writer.
            processor.Emit(OpCodes.Ldarg, writerParameterDef);
            //Writer.WritePackedWhole(value).
            processor.Emit(OpCodes.Ldloc, value);
            processor.Emit(OpCodes.Conv_U8);
            processor.Emit(OpCodes.Callvirt, Writer_WritePackedWhole_MethodRef);
        }
        #endregion

        /// <summary>
        /// Creates a call to WriteBoolean with value.
        /// </summary>
        /// <param name="processor"></param>
        /// <param name="writerParameterDef"></param>
        /// <param name="value"></param>
        internal void CreateWriteBool(ILProcessor processor, ParameterDefinition writerParameterDef, bool value)
        {
            MethodReference writeBoolMethodRef = GetFavoredWriteMethodReference(base.GetClass<GeneralHelper>().GetTypeReference(typeof(bool)), true);
            processor.Emit(OpCodes.Ldarg, writerParameterDef);
            int intValue = (value) ? 1 : 0;
            processor.Emit(OpCodes.Ldc_I4, intValue);
            processor.Emit(OpCodes.Callvirt, writeBoolMethodRef);
        }

        /// <summary>
        /// Creates a Write call on a PooledWriter variable for parameterDef.
        /// EG: writer.WriteBool(xxxxx);
        /// </summary>
        internal List<Instruction> CreateWriteInstructions(MethodDefinition methodDef, object pooledWriterDef, ParameterDefinition valueParameterDef, MethodReference writeMethodRef)
        {
            List<Instruction> insts = new List<Instruction>();
            ILProcessor processor = methodDef.Body.GetILProcessor();

            if (writeMethodRef != null)
            {
                if (pooledWriterDef is VariableDefinition)
                {
                    insts.Add(processor.Create(OpCodes.Ldloc, (VariableDefinition)pooledWriterDef));
                }
                else if (pooledWriterDef is ParameterDefinition)
                {
                    insts.Add(processor.Create(OpCodes.Ldarg, (ParameterDefinition)pooledWriterDef));
                }
                else
                {
                    base.LogError($"{pooledWriterDef.GetType().FullName} is not a valid writerDef. Type must be VariableDefinition or ParameterDefinition.");
                    return new List<Instruction>();
                }
                insts.Add(processor.Create(OpCodes.Ldarg, valueParameterDef));
                //If an auto pack method then insert default value.
                if (_autoPackedMethods.Contains(valueParameterDef.ParameterType))
                {
                    AutoPackType packType = base.GetClass<GeneralHelper>().GetDefaultAutoPackType(valueParameterDef.ParameterType);
                    insts.Add(processor.Create(OpCodes.Ldc_I4, (int)packType));
                }
                insts.Add(processor.Create(OpCodes.Call, writeMethodRef));
                return insts;
            }
            else
            {
                base.LogError($"Writer not found for {valueParameterDef.ParameterType.FullName}.");
                return new List<Instruction>();
            }
        }
        /// <summary>
        /// Creates a Write call on a PooledWriter variable for parameterDef.
        /// EG: writer.WriteBool(xxxxx);
        /// </summary>
        internal void CreateWrite(MethodDefinition methodDef, object writerDef, ParameterDefinition valuePd, MethodReference writeMr)
        {
            List<Instruction> insts = CreateWriteInstructions(methodDef, writerDef, valuePd, writeMr);
            ILProcessor processor = methodDef.Body.GetILProcessor();
            processor.Add(insts);
        }
        /// <summary>
        /// Creates a Write call to a writer.
        /// EG: StaticClass.WriteBool(xxxxx);
        /// </summary>
        /// <param name="processor"></param>
        /// <param name="fieldDef"></param>
        internal void CreateWrite(MethodDefinition writerMd, ParameterDefinition valuePd, FieldDefinition fieldDef, MethodReference writeMr)
        {
            if (writeMr != null)
            {
                ILProcessor processor = writerMd.Body.GetILProcessor();
                ParameterDefinition writerPd = writerMd.Parameters[0];

                FieldReference fieldRef = base.GetClass<GeneralHelper>().GetFieldReference(fieldDef);
                processor.Emit(OpCodes.Ldarg, writerPd);
                processor.Emit(OpCodes.Ldarg, valuePd);
                processor.Emit(OpCodes.Ldfld, fieldRef);
                //If an auto pack method then insert default value.
                if (_autoPackedMethods.Contains(fieldDef.FieldType))
                {
                    AutoPackType packType = base.GetClass<GeneralHelper>().GetDefaultAutoPackType(fieldDef.FieldType);
                    processor.Emit(OpCodes.Ldc_I4, (int)packType);
                }
                processor.Emit(OpCodes.Call, writeMr);
            }
            else
            {
                base.LogError($"Writer not found for {fieldDef.FieldType.FullName}.");
            }
        }

        /// <summary>
        /// Creates a Write call to a writer.
        /// EG: StaticClass.WriteBool(xxxxx);
        /// </summary>
        /// <param name="processor"></param>
        /// <param name="propertyDef"></param>
        internal void CreateWrite(MethodDefinition writerMd, ParameterDefinition valuePd, MethodReference getMr, MethodReference writeMr)
        {
            TypeReference returnTr = getMr.ReturnType;

            if (writeMr != null)
            {
                ILProcessor processor = writerMd.Body.GetILProcessor();
                ParameterDefinition writerPd = writerMd.Parameters[0];

                processor.Emit(OpCodes.Ldarg, writerPd);
                OpCode ldArgOC0 = (valuePd.ParameterType.IsValueType) ? OpCodes.Ldarga : OpCodes.Ldarg;
                processor.Emit(ldArgOC0, valuePd);
                processor.Emit(OpCodes.Call, getMr);
                //If an auto pack method then insert default value.
                if (_autoPackedMethods.Contains(returnTr))
                {
                    AutoPackType packType = base.GetClass<GeneralHelper>().GetDefaultAutoPackType(returnTr);
                    processor.Emit(OpCodes.Ldc_I4, (int)packType);
                }
                processor.Emit(OpCodes.Call, writeMr);
            }
            else
            {
                base.LogError($"Writer not found for {returnTr.FullName}.");
            }
        }


    }

}