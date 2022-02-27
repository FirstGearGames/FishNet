using FishNet.CodeGenerating.Helping.Extension;
using FishNet.Object;
using FishNet.Serializing;
using MonoFN.Cecil;
using MonoFN.Cecil.Cil;
using System;
using UnityEngine;

namespace FishNet.CodeGenerating.Helping
{
    internal class ReaderGenerator
    {

        #region Const.
        internal const string GENERATED_READERS_CLASS_NAME = "GeneratedReaders___FN";
        public const TypeAttributes GENERATED_TYPE_ATTRIBUTES = WriterGenerator.GENERATED_TYPE_ATTRIBUTES;
        private const string READ_PREFIX = "Read___";
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
        /// Generates a reader for objectTypeReference if one does not already exist. 
        /// </summary>
        /// <param name="objectTr"></param>
        /// <returns></returns>
        internal MethodReference CreateReader(TypeReference objectTr)
        {
            MethodReference resultMr = null;
            TypeDefinition objectTypeDef;

            SerializerType serializerType = GeneratorHelper.GetSerializerType(objectTr, false, out objectTypeDef);
            if (serializerType != SerializerType.Invalid)
            {
                //Array.
                if (serializerType == SerializerType.Array)
                    resultMr = CreateArrayReaderMethodReference(objectTr);
                //Enum.
                else if (serializerType == SerializerType.Enum)
                    resultMr = CreateEnumReaderMethodDefinition(objectTr);
                else if (serializerType == SerializerType.Dictionary)
                    resultMr = CreateDictionaryReaderMethodReference(objectTr);
                //List.                
                else if (serializerType == SerializerType.List)
                    resultMr = CreateListReaderMethodReference(objectTr);
                //NetworkBehaviour.
                else if (serializerType == SerializerType.NetworkBehaviour)
                    resultMr = GetNetworkBehaviourReaderMethodReference(objectTr);
                //Nullable.
                else if (serializerType == SerializerType.Nullable)
                    resultMr = CreateNullableReaderMethodReference(objectTr);
                //Class or struct.
                else if (serializerType == SerializerType.ClassOrStruct)
                    resultMr = CreateClassOrStructReaderMethodReference(objectTr);
            }

            //If was not created.
            if (resultMr == null)
                RemoveFromStaticReaders(objectTr);

            return resultMr;
        }


        /// <summary>
        /// Removes from static writers.
        /// </summary>
        private void RemoveFromStaticReaders(TypeReference tr)
        {
            CodegenSession.ReaderHelper.RemoveReaderMethod(tr, false);
        }
        /// <summary>
        /// Adds to static writers.
        /// </summary>
        private void AddToStaticReaders(TypeReference tr, MethodReference mr)
        {
            CodegenSession.ReaderHelper.AddReaderMethod(tr, mr.CachedResolve(), false, true);
        }

        /// <summary>
        /// Generates a reader for objectTypeReference if one does not already exist.
        /// </summary>
        /// <param name="objectTr"></param>
        /// <returns></returns>
        private MethodReference CreateEnumReaderMethodDefinition(TypeReference objectTr)
        {
            MethodDefinition createdReaderMd = CreateStaticReaderStubMethodDefinition(objectTr);
            AddToStaticReaders(objectTr, createdReaderMd);

            ILProcessor processor = createdReaderMd.Body.GetILProcessor();

            //Get type reference for enum type. eg byte int
            TypeReference underlyingTypeRef = objectTr.CachedResolve().GetEnumUnderlyingTypeReference();
            //Get read method for underlying type.
            MethodReference readMethodRef = CodegenSession.ReaderHelper.GetOrCreateFavoredReadMethodReference(underlyingTypeRef, true);
            if (readMethodRef == null)
                return null;

            ParameterDefinition readerParameterDef = createdReaderMd.Parameters[0];
            //reader.ReadXXX().
            processor.Emit(OpCodes.Ldarg, readerParameterDef);
            if (CodegenSession.WriterHelper.IsAutoPackedType(underlyingTypeRef))
                processor.Emit(OpCodes.Ldc_I4, (int)AutoPackType.Packed);

            processor.Emit(OpCodes.Call, readMethodRef);

            processor.Emit(OpCodes.Ret);
            return CodegenSession.ImportReference(createdReaderMd);
        }


        /// <summary>
        /// Creates a read for a class type which inherits NetworkBehaviour.
        /// </summary>
        /// <param name="objectTr"></param>
        /// <returns></returns>
        private MethodReference GetNetworkBehaviourReaderMethodReference(TypeReference objectTr)
        {
            MethodDefinition createdReaderMd = CreateStaticReaderStubMethodDefinition(objectTr);
            AddToStaticReaders(objectTr, createdReaderMd);

            ILProcessor processor = createdReaderMd.Body.GetILProcessor();
            TypeReference networkBehaviourTypeRef = CodegenSession.GeneralHelper.GetTypeReference(typeof(NetworkBehaviour));

            processor.Emit(OpCodes.Ldarg_0);
            processor.Emit(OpCodes.Call, CodegenSession.ReaderHelper.GetFavoredReadMethodReference(networkBehaviourTypeRef, true));
            processor.Emit(OpCodes.Castclass, objectTr);
            processor.Emit(OpCodes.Ret);
            return CodegenSession.ImportReference(createdReaderMd);
        }

        /// <summary>
        /// Create a reader for an array or list.
        /// </summary>
        private MethodReference CreateArrayReaderMethodReference(TypeReference objectTr)
        {
            MethodDefinition createdReaderMd = CreateStaticReaderStubMethodDefinition(objectTr);
            AddToStaticReaders(objectTr, createdReaderMd);

            /* Try to get instanced first for collection element type, if it doesn't exist then try to
             * get/or make a one. */
            TypeReference elementTypeRef = objectTr.GetElementType();
            MethodReference readMethodRef = CodegenSession.ReaderHelper.GetOrCreateFavoredReadMethodReference(elementTypeRef, true);
            if (readMethodRef == null)
                return null;

            ILProcessor processor = createdReaderMd.Body.GetILProcessor();

            ParameterDefinition readerParameterDef = createdReaderMd.Parameters[0];
            VariableDefinition sizeVariableDef = CodegenSession.GeneralHelper.CreateVariable(createdReaderMd, typeof(int));
            //Load packed whole value into sizeVariableDef, exit if null indicator.
            CodegenSession.ReaderHelper.CreateRetOnNull(processor, readerParameterDef, sizeVariableDef, false);

            //Make local variable of array type.
            VariableDefinition collectionVariableDef = CodegenSession.GeneralHelper.CreateVariable(createdReaderMd, objectTr);
            //Create new array/list of size.
            processor.Emit(OpCodes.Ldloc, sizeVariableDef);
            processor.Emit(OpCodes.Newarr, elementTypeRef);
            //Store new object of arr/list into collection variable.
            processor.Emit(OpCodes.Stloc, collectionVariableDef);

            VariableDefinition loopIndex = CodegenSession.GeneralHelper.CreateVariable(createdReaderMd, typeof(int));
            Instruction loopComparer = processor.Create(OpCodes.Ldloc, loopIndex);

            //int i = 0
            processor.Emit(OpCodes.Ldc_I4_0);
            processor.Emit(OpCodes.Stloc, loopIndex);
            processor.Emit(OpCodes.Br_S, loopComparer);

            //Loop content.
            //Collection[index]
            Instruction contentStart = processor.Create(OpCodes.Ldloc, collectionVariableDef);
            processor.Append(contentStart);
            /* Only arrays load the index since we are setting to that index.
             * List call lst.Add */
            processor.Emit(OpCodes.Ldloc, loopIndex);
            //Collection[index] = reader.
            processor.Emit(OpCodes.Ldarg, readerParameterDef);
            //Pass in AutoPackType default.
            if (CodegenSession.ReaderHelper.IsAutoPackedType(elementTypeRef))
            {
                AutoPackType packType = CodegenSession.GeneralHelper.GetDefaultAutoPackType(elementTypeRef);
                processor.Emit(OpCodes.Ldc_I4, (int)packType);
            }
            //Collection[index] = reader.ReadType().
            processor.Emit(OpCodes.Call, readMethodRef);
            //Set value to collection.
            processor.Emit(OpCodes.Stelem_Any, elementTypeRef);

            //i++
            processor.Emit(OpCodes.Ldloc, loopIndex);
            processor.Emit(OpCodes.Ldc_I4_1);
            processor.Emit(OpCodes.Add);
            processor.Emit(OpCodes.Stloc, loopIndex);
            //if i < length jmp to content start.
            processor.Append(loopComparer); //if i < size
            processor.Emit(OpCodes.Ldloc, sizeVariableDef);
            processor.Emit(OpCodes.Blt_S, contentStart);

            processor.Emit(OpCodes.Ldloc, collectionVariableDef);
            processor.Emit(OpCodes.Ret);

            return CodegenSession.ImportReference(createdReaderMd);
        }

        /// <summary>
        /// Creates a reader for a dictionary.
        /// </summary>
        private MethodReference CreateDictionaryReaderMethodReference(TypeReference objectTr)
        {
            GenericInstanceType genericInstance = (GenericInstanceType)objectTr;
            CodegenSession.ImportReference(genericInstance);
            TypeReference keyTr = genericInstance.GenericArguments[0];
            TypeReference valueTr = genericInstance.GenericArguments[1];

            /* Try to get instanced first for collection element type, if it doesn't exist then try to
             * get/or make a one. */
            MethodReference keyWriteMr = CodegenSession.ReaderHelper.GetOrCreateFavoredReadMethodReference(keyTr, true);
            MethodReference valueWriteMr = CodegenSession.ReaderHelper.GetOrCreateFavoredReadMethodReference(valueTr, true);
            if (keyWriteMr == null || valueWriteMr == null)
                return null;

            MethodDefinition createdReaderMd = CreateStaticReaderStubMethodDefinition(objectTr);
            AddToStaticReaders(objectTr, createdReaderMd);

            ILProcessor processor = createdReaderMd.Body.GetILProcessor();
            GenericInstanceMethod genericInstanceMethod = CodegenSession.ReaderHelper.Reader_ReadDictionary_MethodRef.MakeGenericMethod(new TypeReference[] { keyTr, valueTr });

            ParameterDefinition readerPd = createdReaderMd.Parameters[0];
            processor.Emit(OpCodes.Ldarg, readerPd);
            processor.Emit(OpCodes.Callvirt, genericInstanceMethod);
            processor.Emit(OpCodes.Ret);

            return CodegenSession.ImportReference(createdReaderMd);
        }


        /// <summary>
        /// Create a reader for a list.
        /// </summary>
        private MethodReference CreateListReaderMethodReference(TypeReference objectTr)
        {
            GenericInstanceType genericInstance = (GenericInstanceType)objectTr;
            CodegenSession.ImportReference(genericInstance);
            TypeReference elementTypeRef = genericInstance.GenericArguments[0];

            /* Try to get instanced first for collection element type, if it doesn't exist then try to
             * get/or make a one. */
            MethodReference readMethodRef = CodegenSession.ReaderHelper.GetOrCreateFavoredReadMethodReference(elementTypeRef, true);
            if (readMethodRef == null)
                return null;

            MethodDefinition createdReaderMd = CreateStaticReaderStubMethodDefinition(objectTr);
            AddToStaticReaders(objectTr, createdReaderMd);

            ILProcessor processor = createdReaderMd.Body.GetILProcessor();

            //Find constructor for new list.
            MethodDefinition constructorMd = objectTr.CachedResolve().GetConstructor(new Type[] { typeof(int) });
            MethodReference constructorMr = constructorMd.MakeHostInstanceGeneric(genericInstance);
            //Find add method for list.
            MethodReference lstAddMd = objectTr.CachedResolve().GetMethod("Add");
            MethodReference lstAddMr = lstAddMd.MakeHostInstanceGeneric(genericInstance);

            ParameterDefinition readerParameterDef = createdReaderMd.Parameters[0];
            VariableDefinition sizeVariableDef = CodegenSession.GeneralHelper.CreateVariable(createdReaderMd, typeof(int));
            //Load packed whole value into sizeVariableDef, exit if null indicator.
            CodegenSession.ReaderHelper.CreateRetOnNull(processor, readerParameterDef, sizeVariableDef, false);

            //Make variable of new list type, and create list object.
            VariableDefinition collectionVariableDef = CodegenSession.GeneralHelper.CreateVariable(createdReaderMd, genericInstance);
            processor.Emit(OpCodes.Ldloc, sizeVariableDef);
            processor.Emit(OpCodes.Newobj, constructorMr);
            processor.Emit(OpCodes.Stloc, collectionVariableDef);

            VariableDefinition loopIndex = CodegenSession.GeneralHelper.CreateVariable(createdReaderMd, typeof(int));
            Instruction loopComparer = processor.Create(OpCodes.Ldloc, loopIndex);

            //int i = 0
            processor.Emit(OpCodes.Ldc_I4_0);
            processor.Emit(OpCodes.Stloc, loopIndex);
            processor.Emit(OpCodes.Br_S, loopComparer);

            //Loop content.
            //Collection[index]
            Instruction contentStart = processor.Create(OpCodes.Ldloc, collectionVariableDef);
            processor.Append(contentStart);
            //Collection[index] = reader.
            processor.Emit(OpCodes.Ldarg, readerParameterDef);
            //Pass in AutoPackType default.
            if (CodegenSession.ReaderHelper.IsAutoPackedType(elementTypeRef))
            {
                AutoPackType packType = CodegenSession.GeneralHelper.GetDefaultAutoPackType(elementTypeRef);
                processor.Emit(OpCodes.Ldc_I4, (int)packType);
            }
            //Collection[index] = reader.ReadType().
            processor.Emit(OpCodes.Call, readMethodRef);
            //Set value to collection.
            processor.Emit(OpCodes.Callvirt, lstAddMr);

            //i++
            processor.Emit(OpCodes.Ldloc, loopIndex);
            processor.Emit(OpCodes.Ldc_I4_1);
            processor.Emit(OpCodes.Add);
            processor.Emit(OpCodes.Stloc, loopIndex);
            //if i < length jmp to content start.
            processor.Append(loopComparer); //if i < size
            processor.Emit(OpCodes.Ldloc, sizeVariableDef);
            processor.Emit(OpCodes.Blt_S, contentStart);

            processor.Emit(OpCodes.Ldloc, collectionVariableDef);
            processor.Emit(OpCodes.Ret);

            return CodegenSession.ImportReference(createdReaderMd);
        }


        /// <summary>
        /// Creates a reader method for a struct or class objectTypeRef.
        /// </summary>
        /// <param name="objectTr"></param>
        /// <returns></returns>
        private MethodReference CreateNullableReaderMethodReference(TypeReference objectTr)
        {
            GenericInstanceType objectGit = objectTr as GenericInstanceType;
            TypeReference valueTr = objectGit.GenericArguments[0];

            //Make sure object has a ctor.
            MethodDefinition objectCtorMd = objectTr.GetConstructor(1);
            if (objectCtorMd == null)
            {
                CodegenSession.LogError($"{objectTr.Name} can't be deserialized because the nullable type does not have a constructor.");
                return null;
            }

            //Get the reader for the value.
            MethodReference valueReaderMr = CodegenSession.ReaderHelper.GetOrCreateFavoredReadMethodReference(valueTr, true);
            if (valueReaderMr == null)
                return null;

            TypeDefinition objectTd = objectTr.CachedResolve();
            MethodDefinition createdReaderMd = CreateStaticReaderStubMethodDefinition(objectTr);
            AddToStaticReaders(objectTr, createdReaderMd);
                        
            ILProcessor processor = createdReaderMd.Body.GetILProcessor();

            ParameterDefinition readerPd = createdReaderMd.Parameters[0];
            // create local for return value
            VariableDefinition resultVd = CodegenSession.GeneralHelper.CreateVariable(createdReaderMd, objectTr);

            //Read if null into boolean.
            VariableDefinition nullBoolVd = createdReaderMd.CreateVariable(typeof(bool));
            CodegenSession.ReaderHelper.CreateReadBool(processor, readerPd, nullBoolVd);

            Instruction afterReturnNullInst = processor.Create(OpCodes.Nop);
            processor.Emit(OpCodes.Ldloc, nullBoolVd);
            processor.Emit(OpCodes.Brfalse, afterReturnNullInst);
            //Return a null result.
            CodegenSession.GeneralHelper.SetVariableDefinitionFromObject(processor, resultVd, objectTd);
            processor.Emit(OpCodes.Ldloc, resultVd);
            processor.Emit(OpCodes.Ret);
            processor.Append(afterReturnNullInst);

            MethodReference initMr = objectCtorMd.MakeHostInstanceGeneric(objectGit);
            processor.Emit(OpCodes.Ldarg, readerPd);
            //If an auto pack method then insert default value.
            if (CodegenSession.ReaderHelper.IsAutoPackedType(valueTr))
            {
                AutoPackType packType = CodegenSession.GeneralHelper.GetDefaultAutoPackType(valueTr);
                processor.Emit(OpCodes.Ldc_I4, (int)packType);
            }
            processor.Emit(OpCodes.Call, valueReaderMr);
            processor.Emit(OpCodes.Newobj, initMr);
            processor.Emit(OpCodes.Ret);

            return CodegenSession.ImportReference(createdReaderMd);
        }


        /// <summary>
        /// Creates a reader method for a struct or class objectTypeRef.
        /// </summary>
        /// <param name="objectTr"></param>
        /// <returns></returns>
        private MethodReference CreateClassOrStructReaderMethodReference(TypeReference objectTr)
        {
            MethodDefinition createdReaderMd = CreateStaticReaderStubMethodDefinition(objectTr);
            AddToStaticReaders(objectTr, createdReaderMd);

            TypeDefinition objectTypeDef = objectTr.CachedResolve();
            ILProcessor processor = createdReaderMd.Body.GetILProcessor();

            ParameterDefinition readerParameterDef = createdReaderMd.Parameters[0];
            // create local for return value
            VariableDefinition objectVariableDef = CodegenSession.GeneralHelper.CreateVariable(createdReaderMd, objectTr);

            //If not a value type create a return null check.
            if (!objectTypeDef.IsValueType)
            {
                VariableDefinition nullVariableDef = CodegenSession.GeneralHelper.CreateVariable(createdReaderMd, typeof(bool));
                //Load packed whole value into sizeVariableDef, exit if null indicator.
                CodegenSession.ReaderHelper.CreateRetOnNull(processor, readerParameterDef, nullVariableDef, true);
            }

            /* If here then not null. */
            //Make a new instance of object type and set to objectVariableDef.
            CodegenSession.GeneralHelper.SetVariableDefinitionFromObject(processor, objectVariableDef, objectTypeDef);
            if (!ReadFields(createdReaderMd, readerParameterDef, objectVariableDef, objectTr))
                return null;
            /* //codegen scriptableobjects seem to climb too high up to UnityEngine.Object when
             * creating serializers/deserialized. Make sure this is not possible. */

            //Load result and return it.
            processor.Emit(OpCodes.Ldloc, objectVariableDef);
            processor.Emit(OpCodes.Ret);

            return CodegenSession.ImportReference(createdReaderMd);
        }


        /// <summary>
        /// Reads all fields of objectTypeRef.
        /// </summary>  
        private bool ReadFields(MethodDefinition methodDef, ParameterDefinition readerPd, VariableDefinition objectVd, TypeReference objectTr)
        {
            //This probably isn't needed but I'm too afraid to remove it.
            if (objectTr.Module != CodegenSession.Module)
                objectTr = CodegenSession.ImportReference(objectTr.CachedResolve());

            foreach (FieldDefinition fieldDef in objectTr.FindAllPublicFields())
            {
                FieldReference fieldRef = CodegenSession.ImportReference(fieldDef);
                TypeReference typeRef = CodegenSession.ImportReference(fieldRef.FieldType);
                MethodReference readMethodRef = CodegenSession.ReaderHelper.GetOrCreateFavoredReadMethodReference(typeRef, true);
                //Not all fields will support reading, such as NonSerialized ones.
                if (readMethodRef == null)
                    continue;

                CodegenSession.ReaderHelper.CreateReadIntoClassOrStruct(methodDef, readerPd, objectVd, fieldRef);
            }

            return true;
        }


        /// <summary>
        /// Creates the stub for a new reader method.
        /// </summary>
        /// <param name="objectTypeRef"></param>
        /// <returns></returns>
        private MethodDefinition CreateStaticReaderStubMethodDefinition(TypeReference objectTypeRef, string nameExtension = "")
        {
            string methodName = $"{READ_PREFIX}{objectTypeRef.FullName}{nameExtension}s";
            // create new reader for this type
            TypeDefinition readerTypeDef = CodegenSession.GeneralHelper.GetOrCreateClass(out _, GENERATED_TYPE_ATTRIBUTES, GENERATED_READERS_CLASS_NAME, null);
            MethodDefinition readerMethodDef = readerTypeDef.AddMethod(methodName,
                    MethodAttributes.Public |
                    MethodAttributes.Static |
                    MethodAttributes.HideBySig,
                    objectTypeRef);

            CodegenSession.GeneralHelper.CreateParameter(readerMethodDef, CodegenSession.ReaderHelper.Reader_TypeRef, "reader");
            readerMethodDef.Body.InitLocals = true;

            return readerMethodDef;
        }


    }
}