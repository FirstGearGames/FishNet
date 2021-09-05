using System;
using System.Collections.Generic;
using Mono.Cecil;
using FishNet.Serializing;
using Mono.Cecil.Cil;
using FishNet.CodeGenerating.Helping.Extension;
using Unity.CompilationPipeline.Common.Diagnostics;
using UnityEngine;

namespace FishNet.CodeGenerating.Helping
{
    internal class WriterGenerator
    {


        #region Reflection references.
        private Dictionary<TypeReference, ListMethodReferences> _cachedListMethodRefs = new Dictionary<TypeReference, ListMethodReferences>();
        #endregion

        #region Const.
        public const string GENERATED_CLASS_NAME = "GeneratedReadersAndWriters";
        public const TypeAttributes GENERATED_TYPE_ATTRIBUTES = (TypeAttributes.BeforeFieldInit | TypeAttributes.Class | TypeAttributes.AnsiClass |
            TypeAttributes.Public | TypeAttributes.AutoClass | TypeAttributes.Abstract | TypeAttributes.Sealed);
        private const string WRITE_PREFIX = "Write___";
        #endregion

        /// <summary>
        /// Imports references needed by this helper.
        /// </summary>
        /// <param name="moduleDef"></param>
        /// <returns></returns>
        internal bool ImportReferences()
        {
            return true;
        }

        /// <summary>
        /// Generates a writer for objectTypeReference if one does not already exist.
        /// </summary>
        /// <param name="objectTypeRef"></param>
        /// <returns></returns>
        internal MethodReference CreateWriter(TypeReference objectTypeRef)
        {
            MethodReference methodRefResult = null;
            TypeDefinition objectTypeDef;
            SerializerType serializerType = GeneratorHelper.GetSerializerType(objectTypeRef, true, out objectTypeDef);
            if (serializerType != SerializerType.Invalid)
            {
                //Array.
                if (serializerType == SerializerType.Array)
                {
                    TypeReference elementType = objectTypeRef.GetElementType();
                    methodRefResult = CreateCollectionWriterMethodDefinition(objectTypeRef, elementType);
                }
                //Enum.
                else if (serializerType == SerializerType.Enum)
                {
                    methodRefResult = CreateEnumWriterMethodDefinition(objectTypeRef);
                }
                //List.
                else if (serializerType == SerializerType.List)
                {
                    GenericInstanceType genericInstanceType = (GenericInstanceType)objectTypeRef;
                    TypeReference elementType = genericInstanceType.GenericArguments[0];
                    methodRefResult = CreateCollectionWriterMethodDefinition(objectTypeRef, elementType);
                }
                //NetworkBehaviour.
                else if (serializerType == SerializerType.NetworkBehaviour)
                {
                    methodRefResult = CreateNetworkBehaviourWriterMethodReference(objectTypeDef);
                }
                //Class or struct.
                else if (serializerType == SerializerType.ClassOrStruct)
                {
                    methodRefResult = CreateClassOrStructWriterMethodDefinition(objectTypeRef);
                }
            }

            //If was created.
            if (methodRefResult != null)
                    CodegenSession.WriterHelper.AddWriterMethod(objectTypeRef, methodRefResult, false, true);

            return methodRefResult;
        }



        /// <summary>
        /// Adds a write for a NetworkBehaviour class type to WriterMethods.
        /// </summary>
        /// <param name="classTypeRef"></param>
        private MethodDefinition CreateNetworkBehaviourWriterMethodReference(TypeReference objectTypeRef)
        {
            //All NetworkBehaviour types will simply WriteNetworkBehaviour/ReadNetworkBehaviour.
            //Create generated reader/writer class. This class holds all generated reader/writers.
            CodegenSession.GeneralHelper.GetOrCreateClass(out _, GENERATED_TYPE_ATTRIBUTES, GENERATED_CLASS_NAME, null);

            MethodDefinition createdWriterMethodDef = CreateStaticWriterStubMethodDefinition(objectTypeRef);
            ILProcessor processor = createdWriterMethodDef.Body.GetILProcessor();
            MethodReference writeMethodRef = CodegenSession.WriterHelper.GetOrCreateFavoredWriteMethodReference(CodegenSession.WriterHelper.NetworkBehaviour_TypeRef, true);
            //Get parameters for method.
            ParameterDefinition writerParameterDef = createdWriterMethodDef.Parameters[0];
            ParameterDefinition classParameterDef = createdWriterMethodDef.Parameters[1];

            //Load parameters as arguments.
            processor.Emit(OpCodes.Ldarg, writerParameterDef);
            processor.Emit(OpCodes.Ldarg, classParameterDef);
            //writer.WriteNetworkBehaviour(arg1);
            processor.Emit(OpCodes.Call, writeMethodRef);

            processor.Emit(OpCodes.Ret);

            return createdWriterMethodDef;
        }

        /// <summary> 
        /// Gets the length of a collection and writes the value to a variable.
        /// </summary>
        private void CreateCollectionLength(ILProcessor processor, ParameterDefinition collectionParameterDef, VariableDefinition storeVariableDef)
        {
            processor.Emit(OpCodes.Ldarg, collectionParameterDef);
            processor.Emit(OpCodes.Ldlen);
            processor.Emit(OpCodes.Conv_I4);
            processor.Emit(OpCodes.Stloc, storeVariableDef);
        }

        /// <summary>
        /// Returns common list references for list type of elementTypeRef.
        /// </summary>
        /// <param name="elementTypeRef"></param>
        /// <returns></returns>
        private ListMethodReferences GetListMethodReferences(TypeReference elementTypeRef)
        {
            ListMethodReferences result;
            //If found return result.
            if (_cachedListMethodRefs.TryGetValue(elementTypeRef, out result))
            {
                return result;
            }
            //Otherwise make a new entry.
            else
            {
                Type elementMonoType = elementTypeRef.GetMonoType();
                if (elementMonoType == null)
                {
                    CodegenSession.LogError($"Mono Type could not be found for {elementMonoType.FullName}.");
                    return null;
                }
                Type constructedListType = typeof(List<>).MakeGenericType(elementMonoType);

                MethodReference add = null;
                MethodReference item = null;
                foreach (System.Reflection.MethodInfo methodInfo in constructedListType.GetMethods())
                {
                    if (methodInfo.Name == "get_Item")
                        item = CodegenSession.Module.ImportReference(methodInfo);
                }


                if (item == null)
                {
                    CodegenSession.LogError($"Count or Item property could not be found for {elementMonoType.FullName}.");
                    return null;
                }

                ListMethodReferences lmr = new ListMethodReferences(constructedListType, item, add);
                _cachedListMethodRefs.Add(elementTypeRef, lmr);
                return lmr;
            }
        }



        /// <summary>
        /// Creates a writer for a class or struct of objectTypeRef.
        /// </summary>
        /// <param name="objectTypeRef"></param>
        /// <returns></returns>
        private MethodDefinition CreateClassOrStructWriterMethodDefinition(TypeReference objectTypeRef)
        {
            /*Stubs generate Method(Writer writer, T value). */
            MethodDefinition createdWriterMethodDef = CreateStaticWriterStubMethodDefinition(objectTypeRef);
            ILProcessor processor = createdWriterMethodDef.Body.GetILProcessor();

            //If not a value type then add a null check.
            if (!objectTypeRef.Resolve().IsValueType)
            {
                ParameterDefinition readerParameterDef = createdWriterMethodDef.Parameters[0];
                CodegenSession.WriterHelper.CreateRetOnNull(processor, readerParameterDef, createdWriterMethodDef.Parameters[1], true);
                //Code will only execute here and below if not null.
                CodegenSession.WriterHelper.CreateWriteBool(processor, readerParameterDef, false);
            }

            //Write all fields for the class or struct.
            ParameterDefinition valueParameterDef = createdWriterMethodDef.Parameters[1];
            if (!WriteFields(processor, valueParameterDef, objectTypeRef))
                return null;

            processor.Emit(OpCodes.Ret);
            return createdWriterMethodDef;
        }

        /// <summary>
        /// Find all fields in type and write them
        /// </summary>
        /// <param name="objectTypeRef"></param>
        /// <param name="processor"></param>
        /// <returns>false if fail</returns>
        private bool WriteFields(ILProcessor processor, ParameterDefinition valueParameterDef, TypeReference objectTypeRef)
        {
            foreach (FieldDefinition fieldDef in objectTypeRef.FindAllPublicFields())
            {
                MethodReference writeMethodRef = CodegenSession.WriterHelper.GetOrCreateFavoredWriteMethodReference(fieldDef.FieldType, true);
                //Not all fields will support writing, such as NonSerialized ones.
                if (writeMethodRef == null)
                    continue;

                CodegenSession.WriterHelper.CreateWrite(processor, valueParameterDef, fieldDef, writeMethodRef);
            }

            return true;
        }


        /// <summary>
        /// Creates a writer for an enum.
        /// </summary>
        /// <param name="enumTypeRef"></param>
        /// <returns></returns>
        private MethodDefinition CreateEnumWriterMethodDefinition(TypeReference enumTypeRef)
        {
            MethodDefinition createdWriterMethodDef = CreateStaticWriterStubMethodDefinition(enumTypeRef);
            ILProcessor processor = createdWriterMethodDef.Body.GetILProcessor();

            //Element type for enum. EG: byte int ect
            TypeReference underlyingTypeRef = enumTypeRef.Resolve().GetEnumUnderlyingTypeReference();
            //Method to write that type.
            MethodReference underlyingWriterMethodRef = CodegenSession.WriterHelper.GetOrCreateFavoredWriteMethodReference(underlyingTypeRef, true);
            if (underlyingWriterMethodRef == null)
                return null;

            ParameterDefinition writerParameterDef = createdWriterMethodDef.Parameters[0];
            ParameterDefinition valueParameterDef = createdWriterMethodDef.Parameters[1];
            //Push writer and value into call.
            processor.Emit(OpCodes.Ldarg, writerParameterDef);
            processor.Emit(OpCodes.Ldarg, valueParameterDef);
            if (CodegenSession.WriterHelper.IsAutoPackedType(underlyingTypeRef))
                processor.Emit(OpCodes.Ldc_I4, (int)AutoPackType.Packed);

            //writer.WriteXXX(value)
            processor.Emit(OpCodes.Call, underlyingWriterMethodRef);

            processor.Emit(OpCodes.Ret);
            return createdWriterMethodDef;
        }

        /// <summary>
        /// Creates a writer for a collection for elementTypeRef.
        /// </summary>
        private MethodDefinition CreateCollectionWriterMethodDefinition(TypeReference objectTypeRef, TypeReference elementTypeRef)
        {
            MethodDefinition createdWriterMethodDef = CreateStaticWriterStubMethodDefinition(objectTypeRef);
            /* Try to get instanced first for collection element type, if it doesn't exist then try to
             * get/or make a one. */
            MethodReference writeMethodRef = CodegenSession.WriterHelper.GetOrCreateFavoredWriteMethodReference(elementTypeRef, true);
            if (writeMethodRef == null)
                return null;

            ILProcessor processor = createdWriterMethodDef.Body.GetILProcessor();

            ListMethodReferences lstMethodRefs = null;
            //True if array, false if list.
            bool isArray = createdWriterMethodDef.Parameters[1].ParameterType.IsArray;
            //If not array get methodRefs needed to create a list writer.
            if (!isArray)
            {
                lstMethodRefs = GetListMethodReferences(elementTypeRef);
                if (lstMethodRefs == null)
                    return null;
            }

            //Null instructions.
            CodegenSession.WriterHelper.CreateRetOnNull(processor, createdWriterMethodDef.Parameters[0], createdWriterMethodDef.Parameters[1], false);

            //Write length. It only makes it this far if not null.
            //int length = arr[].Length.
            VariableDefinition sizeVariableDef = CodegenSession.GeneralHelper.CreateVariable(createdWriterMethodDef, typeof(int));
            CreateCollectionLength(processor, createdWriterMethodDef.Parameters[1], sizeVariableDef);
            //writer.WritePackedWhole(length).
            CodegenSession.WriterHelper.CreateWritePackedWhole(processor, createdWriterMethodDef.Parameters[0], sizeVariableDef);

            VariableDefinition loopIndex = CodegenSession.GeneralHelper.CreateVariable(createdWriterMethodDef, typeof(int));
            Instruction loopComparer = processor.Create(OpCodes.Ldloc, loopIndex);

            //int i = 0
            processor.Emit(OpCodes.Ldc_I4_0);
            processor.Emit(OpCodes.Stloc, loopIndex);
            processor.Emit(OpCodes.Br_S, loopComparer);

            //Loop content.
            Instruction contentStart = processor.Create(OpCodes.Ldarg_0);
            processor.Append(contentStart);
            processor.Emit(OpCodes.Ldarg_1);
            processor.Emit(OpCodes.Ldloc, loopIndex);

            //Load array element.
            if (isArray)
            {
                if (elementTypeRef.IsValueType)
                    processor.Emit(OpCodes.Ldelem_Any, elementTypeRef);
                else
                    processor.Emit(OpCodes.Ldelem_Ref);
            }
            else
            {
                processor.Emit(OpCodes.Callvirt, lstMethodRefs.Item_MethodRef);
            }
            //If auto pack type then write default auto pack.
            if (CodegenSession.WriterHelper.IsAutoPackedType(elementTypeRef))
            {
                AutoPackType packType = CodegenSession.GeneralHelper.GetDefaultAutoPackType(elementTypeRef);
                processor.Emit(OpCodes.Ldc_I4, (int)packType);
            }
            //writer.Write
            processor.Emit(OpCodes.Callvirt, writeMethodRef);

            //i++
            processor.Emit(OpCodes.Ldloc, loopIndex);
            processor.Emit(OpCodes.Ldc_I4_1);
            processor.Emit(OpCodes.Add);
            processor.Emit(OpCodes.Stloc, loopIndex);
            //if i < length jmp to content start.
            processor.Append(loopComparer);  //if i < obj(size).
            processor.Emit(OpCodes.Ldloc, sizeVariableDef);
            processor.Emit(OpCodes.Blt_S, contentStart);

            processor.Emit(OpCodes.Ret);
            return createdWriterMethodDef;
        }


        /// <summary>
        /// Creates a method definition stub for objectTypeRef.
        /// </summary>
        /// <param name="objectTypeRef"></param>
        /// <returns></returns>
        private MethodDefinition CreateStaticWriterStubMethodDefinition(TypeReference objectTypeRef)
        {
            string methodName = $"{WRITE_PREFIX}{objectTypeRef.FullName}";
            // create new writer for this type
            TypeDefinition writerTypeDef = CodegenSession.GeneralHelper.GetOrCreateClass(out _, GENERATED_TYPE_ATTRIBUTES, GENERATED_CLASS_NAME, null);

            MethodDefinition writerMethodDef = writerTypeDef.AddMethod(methodName,
                    MethodAttributes.Public |
                    MethodAttributes.Static |
                    MethodAttributes.HideBySig);

            CodegenSession.GeneralHelper.CreateParameter(writerMethodDef, CodegenSession.WriterHelper.Writer_TypeRef, "writer");
            CodegenSession.GeneralHelper.CreateParameter(writerMethodDef, objectTypeRef, "value");
            writerMethodDef.Body.InitLocals = true;

            return writerMethodDef;
        }



    }
}