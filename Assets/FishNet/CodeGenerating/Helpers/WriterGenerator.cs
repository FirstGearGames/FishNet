using FishNet.CodeGenerating.Helping.Extension;
using FishNet.Serializing;
using MonoFN.Cecil;
using MonoFN.Cecil.Cil;

namespace FishNet.CodeGenerating.Helping
{
    internal class WriterGenerator
    {


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
                    methodRefResult = CreateArrayWriterMethodDefinition(objectTypeRef);
                }
                //Enum.
                else if (serializerType == SerializerType.Enum)
                {
                    methodRefResult = CreateEnumWriterMethodDefinition(objectTypeRef);
                }
                //List.
                else if (serializerType == SerializerType.List)
                {
                    methodRefResult = CreateListWriterMethodDefinition(objectTypeRef);
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


        //todo prevent serializer from climbing hierarchy if unity object or unsupported type.

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
        /// Creates a writer for an array.
        /// </summary>
        private MethodDefinition CreateArrayWriterMethodDefinition(TypeReference objectTypeRef)
        {
            /* Try to get instanced first for collection element type, if it doesn't exist then try to
             * get/or make a one. */
            TypeReference elementTypeRef = objectTypeRef.GetElementType();
            MethodReference writeMethodRef = CodegenSession.WriterHelper.GetOrCreateFavoredWriteMethodReference(elementTypeRef, true);
            if (writeMethodRef == null)
                return null;

            MethodDefinition createdWriterMethodDef = CreateStaticWriterStubMethodDefinition(objectTypeRef);
            ILProcessor processor = createdWriterMethodDef.Body.GetILProcessor();


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

            if (elementTypeRef.IsValueType)
                processor.Emit(OpCodes.Ldelem_Any, elementTypeRef);
            else
                processor.Emit(OpCodes.Ldelem_Ref);
            //If auto pack type then write default auto pack.
            if (CodegenSession.WriterHelper.IsAutoPackedType(elementTypeRef))
            {
                AutoPackType packType = CodegenSession.GeneralHelper.GetDefaultAutoPackType(elementTypeRef);
                processor.Emit(OpCodes.Ldc_I4, (int)packType);
            }
            //writer.Write
            processor.Emit(OpCodes.Call, writeMethodRef);

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
        /// Creates a writer for a collection for elementTypeRef.
        /// </summary>
        private MethodDefinition CreateListWriterMethodDefinition(TypeReference objectTypeRef)
        {
            GenericInstanceType genericInstance = (GenericInstanceType)objectTypeRef;
            CodegenSession.ImportReference(genericInstance);
            TypeReference elementTypeRef = genericInstance.GenericArguments[0];

            /* Try to get instanced first for collection element type, if it doesn't exist then try to
             * get/or make a one. */
            MethodReference writeMethodRef = CodegenSession.WriterHelper.GetOrCreateFavoredWriteMethodReference(elementTypeRef, true);
            if (writeMethodRef == null)
                return null;

            MethodDefinition createdWriterMethodDef = CreateStaticWriterStubMethodDefinition(objectTypeRef);
            ILProcessor processor = createdWriterMethodDef.Body.GetILProcessor();

            //Find add method for list.
            MethodReference lstGetItemMd = objectTypeRef.Resolve().GetMethod("get_Item");
            MethodReference lstGetItemMr = lstGetItemMd.MakeHostInstanceGeneric(genericInstance);

            //Null instructions.
            CodegenSession.WriterHelper.CreateRetOnNull(processor, createdWriterMethodDef.Parameters[0], createdWriterMethodDef.Parameters[1], false);

            //Write length. It only makes it this far if not null.
            //int length = List<T>.Count.
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

            processor.Emit(OpCodes.Callvirt, lstGetItemMr);
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