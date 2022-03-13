using FishNet.CodeGenerating.Helping.Extension;
using FishNet.Serializing;
using MonoFN.Cecil;
using MonoFN.Cecil.Cil;
using UnityEngine;

namespace FishNet.CodeGenerating.Helping
{
    internal class WriterGenerator
    {


        #region Const.
        internal const string GENERATED_WRITERS_CLASS_NAME = "GeneratedWriters___FN";
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
        /// <param name="objectTr"></param>
        /// <returns></returns>
        internal MethodReference CreateWriter(TypeReference objectTr)
        {
            MethodReference methodRefResult = null;
            TypeDefinition objectTd;
            SerializerType serializerType = GeneratorHelper.GetSerializerType(objectTr, true, out objectTd);
            if (serializerType != SerializerType.Invalid)
            {
                //Array.
                if (serializerType == SerializerType.Array)
                    methodRefResult = CreateArrayWriterMethodDefinition(objectTr);
                //Enum.
                else if (serializerType == SerializerType.Enum)
                    methodRefResult = CreateEnumWriterMethodDefinition(objectTr);
                //Dictionary.
                else if (serializerType == SerializerType.Dictionary)
                    methodRefResult = CreateDictionaryWriterMethodReference(objectTr);
                //List.
                else if (serializerType == SerializerType.List)
                    methodRefResult = CreateListWriterMethodReference(objectTr);
                //NetworkBehaviour.
                else if (serializerType == SerializerType.NetworkBehaviour)
                    methodRefResult = CreateNetworkBehaviourWriterMethodReference(objectTd);
                //Nullable type.
                else if (serializerType == SerializerType.Nullable)
                    methodRefResult = CreateNullableWriterMethodReference(objectTr, objectTd);
                //Class or struct.
                else if (serializerType == SerializerType.ClassOrStruct)
                    methodRefResult = CreateClassOrStructWriterMethodDefinition(objectTr);
            }

            //If was not created.
            if (methodRefResult == null)
                RemoveFromStaticWriters(objectTr);

            return methodRefResult;
        }

        /// <summary>
        /// Removes from static writers.
        /// </summary>
        private void RemoveFromStaticWriters(TypeReference tr)
        {
            CodegenSession.WriterHelper.RemoveWriterMethod(tr, false);
        }
        /// <summary>
        /// Adds to static writers.
        /// </summary>
        private void AddToStaticWriters(TypeReference tr, MethodReference mr)
        {
            CodegenSession.WriterHelper.AddWriterMethod(tr, mr.CachedResolve(), false, true);
        }

        /// <summary>
        /// Adds a write for a NetworkBehaviour class type to WriterMethods.
        /// </summary>
        /// <param name="classTypeRef"></param>
        private MethodReference CreateNetworkBehaviourWriterMethodReference(TypeReference objectTr)
        {
            objectTr = CodegenSession.ImportReference(objectTr.Resolve());
            //All NetworkBehaviour types will simply WriteNetworkBehaviour/ReadNetworkBehaviour.
            //Create generated reader/writer class. This class holds all generated reader/writers.
            CodegenSession.GeneralHelper.GetOrCreateClass(out _, GENERATED_TYPE_ATTRIBUTES, GENERATED_WRITERS_CLASS_NAME, null);

            MethodDefinition createdWriterMd = CreateStaticWriterStubMethodDefinition(objectTr);
            AddToStaticWriters(objectTr, createdWriterMd);

            ILProcessor processor = createdWriterMd.Body.GetILProcessor();

            MethodReference writeMethodRef = CodegenSession.WriterHelper.GetOrCreateFavoredWriteMethodReference(CodegenSession.WriterHelper.NetworkBehaviour_TypeRef, true);
            //Get parameters for method.
            ParameterDefinition writerParameterDef = createdWriterMd.Parameters[0];
            ParameterDefinition classParameterDef = createdWriterMd.Parameters[1];

            //Load parameters as arguments.
            processor.Emit(OpCodes.Ldarg, writerParameterDef);
            processor.Emit(OpCodes.Ldarg, classParameterDef);
            //writer.WriteNetworkBehaviour(arg1);
            processor.Emit(OpCodes.Call, writeMethodRef);

            processor.Emit(OpCodes.Ret);

            return CodegenSession.ImportReference(createdWriterMd);
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
        /// <param name="objectTr"></param>
        /// <returns></returns>
        private MethodReference CreateNullableWriterMethodReference(TypeReference objectTr, TypeDefinition objectTd)
        {
            GenericInstanceType objectGit = objectTr as GenericInstanceType;
            TypeReference valueTr = objectGit.GenericArguments[0];

            //Get the writer for the value.
            MethodReference valueWriterMr = CodegenSession.WriterHelper.GetOrCreateFavoredWriteMethodReference(valueTr, true);
            if (valueWriterMr == null)
                return null;


            MethodDefinition tmpMd;
            tmpMd = objectTd.GetMethod("get_Value");
            MethodReference genericGetValueMr = tmpMd.MakeHostInstanceGeneric(objectGit);
            tmpMd = objectTd.GetMethod("get_HasValue");
            MethodReference genericHasValueMr = tmpMd.MakeHostInstanceGeneric(objectGit);


            /* Stubs generate Method(Writer writer, T value). */
            MethodDefinition createdWriterMd = CreateStaticWriterStubMethodDefinition(objectTr);
            ILProcessor processor = createdWriterMd.Body.GetILProcessor();

            //Value parameter.
            ParameterDefinition valuePd = createdWriterMd.Parameters[1];

            ParameterDefinition writerPd = createdWriterMd.Parameters[0];

            //Have to write a new ret on null because nullables use hasValue for null checks.
            Instruction afterNullRetInst = processor.Create(OpCodes.Nop);
            processor.Emit(OpCodes.Ldarga, valuePd);
            processor.Emit(OpCodes.Call, genericHasValueMr);
            processor.Emit(OpCodes.Brtrue_S, afterNullRetInst);
            CodegenSession.WriterHelper.CreateWriteBool(processor, writerPd, true);
            processor.Emit(OpCodes.Ret);
            processor.Append(afterNullRetInst);


            //Code will only execute here and below if not null.
            CodegenSession.WriterHelper.CreateWriteBool(processor, writerPd, false);

            processor.Emit(OpCodes.Ldarg, writerPd);
            processor.Emit(OpCodes.Ldarga, valuePd);
            processor.Emit(OpCodes.Call, genericGetValueMr);
            //If an auto pack method then insert default value.
            if (CodegenSession.WriterHelper.IsAutoPackedType(valueTr))
            {
                AutoPackType packType = CodegenSession.GeneralHelper.GetDefaultAutoPackType(valueTr);
                processor.Emit(OpCodes.Ldc_I4, (int)packType);
            }
            processor.Emit(OpCodes.Call, valueWriterMr);

            processor.Emit(OpCodes.Ret);
            return CodegenSession.ImportReference(createdWriterMd);
        }



        /// <summary>
        /// Creates a writer for a class or struct of objectTypeRef.
        /// </summary>
        /// <param name="objectTr"></param>
        /// <returns></returns>
        private MethodReference CreateClassOrStructWriterMethodDefinition(TypeReference objectTr)
        {
            /*Stubs generate Method(Writer writer, T value). */
            MethodDefinition createdWriterMd = CreateStaticWriterStubMethodDefinition(objectTr);
            AddToStaticWriters(objectTr, createdWriterMd);                

            ILProcessor processor = createdWriterMd.Body.GetILProcessor();

            //If not a value type then add a null check.
            if (!objectTr.CachedResolve().IsValueType)
            {
                ParameterDefinition writerPd = createdWriterMd.Parameters[0];
                CodegenSession.WriterHelper.CreateRetOnNull(processor, writerPd, createdWriterMd.Parameters[1], true);
                //Code will only execute here and below if not null.
                CodegenSession.WriterHelper.CreateWriteBool(processor, writerPd, false);
            }

            //Write all fields for the class or struct.
            ParameterDefinition valueParameterDef = createdWriterMd.Parameters[1];
            if (!WriteFields(processor, valueParameterDef, objectTr))
                return null;

            processor.Emit(OpCodes.Ret);
            return CodegenSession.ImportReference(createdWriterMd);
        }

        /// <summary>
        /// Find all fields in type and write them
        /// </summary>
        /// <param name="objectTr"></param>
        /// <param name="processor"></param>
        /// <returns>false if fail</returns>
        private bool WriteFields(ILProcessor processor, ParameterDefinition valueParameterDef, TypeReference objectTr)
        {
            //This probably isn't needed but I'm too afraid to remove it.
            if (objectTr.Module != CodegenSession.Module)
                objectTr = CodegenSession.ImportReference(objectTr.CachedResolve());

            foreach (FieldDefinition fieldDef in objectTr.FindAllPublicFields())
            {
                TypeReference typeRef = CodegenSession.ImportReference(fieldDef.FieldType);
                MethodReference writeMethodRef = CodegenSession.WriterHelper.GetOrCreateFavoredWriteMethodReference(typeRef, true);
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
        /// <param name="enumTr"></param>
        /// <returns></returns>
        private MethodReference CreateEnumWriterMethodDefinition(TypeReference enumTr)
        {
            MethodDefinition createdWriterMd = CreateStaticWriterStubMethodDefinition(enumTr);
            AddToStaticWriters(enumTr, createdWriterMd);

            ILProcessor processor = createdWriterMd.Body.GetILProcessor();

            //Element type for enum. EG: byte int ect
            TypeReference underlyingTypeRef = enumTr.CachedResolve().GetEnumUnderlyingTypeReference();
            //Method to write that type.
            MethodReference underlyingWriterMethodRef = CodegenSession.WriterHelper.GetOrCreateFavoredWriteMethodReference(underlyingTypeRef, true);
            if (underlyingWriterMethodRef == null)
                return null;

            ParameterDefinition writerParameterDef = createdWriterMd.Parameters[0];
            ParameterDefinition valueParameterDef = createdWriterMd.Parameters[1];
            //Push writer and value into call.
            processor.Emit(OpCodes.Ldarg, writerParameterDef);
            processor.Emit(OpCodes.Ldarg, valueParameterDef);
            if (CodegenSession.WriterHelper.IsAutoPackedType(underlyingTypeRef))
                processor.Emit(OpCodes.Ldc_I4, (int)AutoPackType.Packed);

            //writer.WriteXXX(value)
            processor.Emit(OpCodes.Call, underlyingWriterMethodRef);

            processor.Emit(OpCodes.Ret);
            return CodegenSession.ImportReference(createdWriterMd);
        }


        /// <summary>
        /// Creates a writer for an array.
        /// </summary>
        private MethodReference CreateArrayWriterMethodDefinition(TypeReference objectTr)
        {
            /* Try to get instanced first for collection element type, if it doesn't exist then try to
             * get/or make a one. */
            TypeReference elementTypeRef = objectTr.GetElementType();
            MethodReference writeMethodRef = CodegenSession.WriterHelper.GetOrCreateFavoredWriteMethodReference(elementTypeRef, true);
            if (writeMethodRef == null)
                return null;

            if (objectTr.Name.Contains("InputActionMap"))
                UnityEngine.Debug.Log("CreatingArray " + objectTr.Name);

                MethodDefinition createdWriterMd = CreateStaticWriterStubMethodDefinition(objectTr);
            AddToStaticWriters(objectTr, createdWriterMd);

            ILProcessor processor = createdWriterMd.Body.GetILProcessor();

            //Null instructions.
            CodegenSession.WriterHelper.CreateRetOnNull(processor, createdWriterMd.Parameters[0], createdWriterMd.Parameters[1], false);

            //Write length. It only makes it this far if not null.
            //int length = arr[].Length.
            VariableDefinition sizeVariableDef = CodegenSession.GeneralHelper.CreateVariable(createdWriterMd, typeof(int));
            CreateCollectionLength(processor, createdWriterMd.Parameters[1], sizeVariableDef);
            //writer.WritePackedWhole(length).
            CodegenSession.WriterHelper.CreateWritePackedWhole(processor, createdWriterMd.Parameters[0], sizeVariableDef);

            VariableDefinition loopIndex = CodegenSession.GeneralHelper.CreateVariable(createdWriterMd, typeof(int));
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
            return CodegenSession.ImportReference(createdWriterMd);
        }


        /// <summary>
        /// Creates a writer for a dictionary collection.
        /// </summary>
        private MethodReference CreateDictionaryWriterMethodReference(TypeReference objectTr)
        {
            GenericInstanceType genericInstance = (GenericInstanceType)objectTr;
            CodegenSession.ImportReference(genericInstance);
            TypeReference keyTr = genericInstance.GenericArguments[0];
            TypeReference valueTr = genericInstance.GenericArguments[1];

            /* Try to get instanced first for collection element type, if it doesn't exist then try to
             * get/or make a one. */
            MethodReference keyWriteMr = CodegenSession.WriterHelper.GetOrCreateFavoredWriteMethodReference(keyTr, true);
            MethodReference valueWriteMr = CodegenSession.WriterHelper.GetOrCreateFavoredWriteMethodReference(valueTr, true);
            if (keyWriteMr == null || valueWriteMr == null)
                return null;

            MethodDefinition createdWriterMd = CreateStaticWriterStubMethodDefinition(objectTr);
            AddToStaticWriters(objectTr, createdWriterMd);

            ILProcessor processor = createdWriterMd.Body.GetILProcessor();
            GenericInstanceMethod genericInstanceMethod = CodegenSession.WriterHelper.Writer_WriteDictionary_MethodRef.MakeGenericMethod(new TypeReference[] { keyTr, valueTr });

            ParameterDefinition writerPd = createdWriterMd.Parameters[0];
            ParameterDefinition valuePd = createdWriterMd.Parameters[1];
            processor.Emit(OpCodes.Ldarg, writerPd);
            processor.Emit(OpCodes.Ldarg, valuePd);
            processor.Emit(OpCodes.Callvirt, genericInstanceMethod);
            processor.Emit(OpCodes.Ret);

            return CodegenSession.ImportReference(createdWriterMd);
        }


        /// <summary>
        /// Creates a writer for a list.
        /// </summary>
        private MethodReference CreateListWriterMethodReference(TypeReference objectTr)
        {
            GenericInstanceType genericInstance = (GenericInstanceType)objectTr;
            CodegenSession.ImportReference(genericInstance);
            TypeReference elementTypeRef = genericInstance.GenericArguments[0];

            /* Try to get instanced first for collection element type, if it doesn't exist then try to
             * get/or make a one. */
            MethodReference writeMethodRef = CodegenSession.WriterHelper.GetOrCreateFavoredWriteMethodReference(elementTypeRef, true);
            if (writeMethodRef == null)
                return null;

            MethodDefinition createdWriterMd = CreateStaticWriterStubMethodDefinition(objectTr);
            AddToStaticWriters(objectTr, createdWriterMd);

            ILProcessor processor = createdWriterMd.Body.GetILProcessor();

            //Find add method for list.
            MethodReference lstGetItemMd = objectTr.CachedResolve().GetMethod("get_Item");
            MethodReference lstGetItemMr = lstGetItemMd.MakeHostInstanceGeneric(genericInstance);

            //Null instructions.
            CodegenSession.WriterHelper.CreateRetOnNull(processor, createdWriterMd.Parameters[0], createdWriterMd.Parameters[1], false);

            //Write length. It only makes it this far if not null.
            //int length = List<T>.Count.
            VariableDefinition sizeVariableDef = CodegenSession.GeneralHelper.CreateVariable(createdWriterMd, typeof(int));
            CreateCollectionLength(processor, createdWriterMd.Parameters[1], sizeVariableDef);
            //writer.WritePackedWhole(length).
            CodegenSession.WriterHelper.CreateWritePackedWhole(processor, createdWriterMd.Parameters[0], sizeVariableDef);

            VariableDefinition loopIndex = CodegenSession.GeneralHelper.CreateVariable(createdWriterMd, typeof(int));
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
            return CodegenSession.ImportReference(createdWriterMd);
        }


        /// <summary>
        /// Creates a method definition stub for objectTypeRef.
        /// </summary>
        /// <param name="objectTypeRef"></param>
        /// <returns></returns>
        private MethodDefinition CreateStaticWriterStubMethodDefinition(TypeReference objectTypeRef, string nameExtension = "")
        {
            string methodName = $"{WRITE_PREFIX}{objectTypeRef.FullName}{nameExtension}";
            // create new writer for this type
            TypeDefinition writerTypeDef = CodegenSession.GeneralHelper.GetOrCreateClass(out _, GENERATED_TYPE_ATTRIBUTES, GENERATED_WRITERS_CLASS_NAME, null);

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