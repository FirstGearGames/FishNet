using FishNet.CodeGenerating.Helping.Extension;
using FishNet.Object;
using FishNet.Serializing;
using MonoFN.Cecil;
using MonoFN.Cecil.Cil;
using System;

namespace FishNet.CodeGenerating.Helping
{
    internal class ReaderGenerator : CodegenBase
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
        public override bool ImportReferences()
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

            SerializerType serializerType = base.GetClass<GeneratorHelper>().GetSerializerType(objectTr, false, out objectTypeDef);
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
            base.GetClass<ReaderHelper>().RemoveReaderMethod(tr, false);
        }
        /// <summary>
        /// Adds to static writers.
        /// </summary>
        private void AddToStaticReaders(TypeReference tr, MethodReference mr)
        {
            base.GetClass<ReaderHelper>().AddReaderMethod(tr, mr.CachedResolve(base.Session), false, true);
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
            TypeReference underlyingTypeRef = objectTr.CachedResolve(base.Session).GetEnumUnderlyingTypeReference();
            //Get read method for underlying type.
            MethodReference readMethodRef = base.GetClass<ReaderHelper>().GetOrCreateFavoredReadMethodReference(underlyingTypeRef, true);
            if (readMethodRef == null)
                return null;

            ParameterDefinition readerParameterDef = createdReaderMd.Parameters[0];
            //reader.ReadXXX().
            processor.Emit(OpCodes.Ldarg, readerParameterDef);
            if (base.GetClass<WriterHelper>().IsAutoPackedType(underlyingTypeRef))
                processor.Emit(OpCodes.Ldc_I4, (int)AutoPackType.Packed);

            processor.Emit(OpCodes.Call, readMethodRef);

            processor.Emit(OpCodes.Ret);
            return base.ImportReference(createdReaderMd);
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
            TypeReference networkBehaviourTypeRef = base.GetClass<GeneralHelper>().GetTypeReference(typeof(NetworkBehaviour));

            processor.Emit(OpCodes.Ldarg_0);
            processor.Emit(OpCodes.Call, base.GetClass<ReaderHelper>().GetFavoredReadMethodReference(networkBehaviourTypeRef, true));
            processor.Emit(OpCodes.Castclass, objectTr);
            processor.Emit(OpCodes.Ret);
            return base.ImportReference(createdReaderMd);
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
            MethodReference readMethodRef = base.GetClass<ReaderHelper>().GetOrCreateFavoredReadMethodReference(elementTypeRef, true);
            if (readMethodRef == null)
                return null;

            ILProcessor processor = createdReaderMd.Body.GetILProcessor();

            ParameterDefinition readerParameterDef = createdReaderMd.Parameters[0];
            VariableDefinition sizeVariableDef = base.GetClass<GeneralHelper>().CreateVariable(createdReaderMd, typeof(int));
            //Load packed whole value into sizeVariableDef, exit if null indicator.
            base.GetClass<ReaderHelper>().CreateRetOnNull(processor, readerParameterDef, sizeVariableDef, false);

            //Make local variable of array type.
            VariableDefinition collectionVariableDef = base.GetClass<GeneralHelper>().CreateVariable(createdReaderMd, objectTr);
            //Create new array/list of size.
            processor.Emit(OpCodes.Ldloc, sizeVariableDef);
            processor.Emit(OpCodes.Newarr, elementTypeRef);
            //Store new object of arr/list into collection variable.
            processor.Emit(OpCodes.Stloc, collectionVariableDef);

            VariableDefinition loopIndex = base.GetClass<GeneralHelper>().CreateVariable(createdReaderMd, typeof(int));
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
            if (base.GetClass<ReaderHelper>().IsAutoPackedType(elementTypeRef))
            {
                AutoPackType packType = base.GetClass<GeneralHelper>().GetDefaultAutoPackType(elementTypeRef);
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

            return base.ImportReference(createdReaderMd);
        }

        /// <summary>
        /// Creates a reader for a dictionary.
        /// </summary>
        private MethodReference CreateDictionaryReaderMethodReference(TypeReference objectTr)
        {
            GenericInstanceType genericInstance = (GenericInstanceType)objectTr;
            base.ImportReference(genericInstance);
            TypeReference keyTr = genericInstance.GenericArguments[0];
            TypeReference valueTr = genericInstance.GenericArguments[1];

            /* Try to get instanced first for collection element type, if it doesn't exist then try to
             * get/or make a one. */
            MethodReference keyWriteMr = base.GetClass<ReaderHelper>().GetOrCreateFavoredReadMethodReference(keyTr, true);
            MethodReference valueWriteMr = base.GetClass<ReaderHelper>().GetOrCreateFavoredReadMethodReference(valueTr, true);
            if (keyWriteMr == null || valueWriteMr == null)
                return null;

            MethodDefinition createdReaderMd = CreateStaticReaderStubMethodDefinition(objectTr);
            AddToStaticReaders(objectTr, createdReaderMd);

            ILProcessor processor = createdReaderMd.Body.GetILProcessor();
            GenericInstanceMethod genericInstanceMethod = base.GetClass<ReaderHelper>().Reader_ReadDictionary_MethodRef.MakeGenericMethod(new TypeReference[] { keyTr, valueTr });

            ParameterDefinition readerPd = createdReaderMd.Parameters[0];
            processor.Emit(OpCodes.Ldarg, readerPd);
            processor.Emit(OpCodes.Callvirt, genericInstanceMethod);
            processor.Emit(OpCodes.Ret);

            return base.ImportReference(createdReaderMd);
        }


        /// <summary>
        /// Create a reader for a list.
        /// </summary>
        private MethodReference CreateListReaderMethodReference(TypeReference objectTr)
        {
            GenericInstanceType genericInstance = (GenericInstanceType)objectTr;
            base.ImportReference(genericInstance);
            TypeReference elementTypeRef = genericInstance.GenericArguments[0];

            /* Try to get instanced first for collection element type, if it doesn't exist then try to
             * get/or make a one. */
            MethodReference readMethodRef = base.GetClass<ReaderHelper>().GetOrCreateFavoredReadMethodReference(elementTypeRef, true);
            if (readMethodRef == null)
                return null;

            MethodDefinition createdReaderMd = CreateStaticReaderStubMethodDefinition(objectTr);
            AddToStaticReaders(objectTr, createdReaderMd);

            ILProcessor processor = createdReaderMd.Body.GetILProcessor();

            //Find constructor for new list.
            MethodDefinition constructorMd = objectTr.CachedResolve(base.Session).GetConstructor(new Type[] { typeof(int) });
            MethodReference constructorMr = constructorMd.MakeHostInstanceGeneric(base.Session, genericInstance);
            //Find add method for list.
            MethodReference lstAddMd = objectTr.CachedResolve(base.Session).GetMethod("Add");
            MethodReference lstAddMr = lstAddMd.MakeHostInstanceGeneric(base.Session, genericInstance);

            ParameterDefinition readerParameterDef = createdReaderMd.Parameters[0];
            VariableDefinition sizeVariableDef = base.GetClass<GeneralHelper>().CreateVariable(createdReaderMd, typeof(int));
            //Load packed whole value into sizeVariableDef, exit if null indicator.
            base.GetClass<ReaderHelper>().CreateRetOnNull(processor, readerParameterDef, sizeVariableDef, false);

            //Make variable of new list type, and create list object.
            VariableDefinition collectionVariableDef = base.GetClass<GeneralHelper>().CreateVariable(createdReaderMd, genericInstance);
            processor.Emit(OpCodes.Ldloc, sizeVariableDef);
            processor.Emit(OpCodes.Newobj, constructorMr);
            processor.Emit(OpCodes.Stloc, collectionVariableDef);

            VariableDefinition loopIndex = base.GetClass<GeneralHelper>().CreateVariable(createdReaderMd, typeof(int));
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
            if (base.GetClass<ReaderHelper>().IsAutoPackedType(elementTypeRef))
            {
                AutoPackType packType = base.GetClass<GeneralHelper>().GetDefaultAutoPackType(elementTypeRef);
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

            return base.ImportReference(createdReaderMd);
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
            MethodDefinition objectCtorMd = objectTr.GetConstructor(base.Session, 1);
            if (objectCtorMd == null)
            {
                base.LogError($"{objectTr.Name} can't be deserialized because the nullable type does not have a constructor.");
                return null;
            }

            //Get the reader for the value.
            MethodReference valueReaderMr = base.GetClass<ReaderHelper>().GetOrCreateFavoredReadMethodReference(valueTr, true);
            if (valueReaderMr == null)
                return null;

            TypeDefinition objectTd = objectTr.CachedResolve(base.Session);
            MethodDefinition createdReaderMd = CreateStaticReaderStubMethodDefinition(objectTr);
            AddToStaticReaders(objectTr, createdReaderMd);

            ILProcessor processor = createdReaderMd.Body.GetILProcessor();

            ParameterDefinition readerPd = createdReaderMd.Parameters[0];
            // create local for return value
            VariableDefinition resultVd = base.GetClass<GeneralHelper>().CreateVariable(createdReaderMd, objectTr);

            //Read if null into boolean.
            VariableDefinition nullBoolVd = createdReaderMd.CreateVariable(base.Session, typeof(bool));
            base.GetClass<ReaderHelper>().CreateReadBool(processor, readerPd, nullBoolVd);

            Instruction afterReturnNullInst = processor.Create(OpCodes.Nop);
            processor.Emit(OpCodes.Ldloc, nullBoolVd);
            processor.Emit(OpCodes.Brfalse, afterReturnNullInst);
            //Return a null result.
            base.GetClass<GeneralHelper>().SetVariableDefinitionFromObject(processor, resultVd, objectTd);
            processor.Emit(OpCodes.Ldloc, resultVd);
            processor.Emit(OpCodes.Ret);
            processor.Append(afterReturnNullInst);

            MethodReference initMr = objectCtorMd.MakeHostInstanceGeneric(base.Session, objectGit);
            processor.Emit(OpCodes.Ldarg, readerPd);
            //If an auto pack method then insert default value.
            if (base.GetClass<ReaderHelper>().IsAutoPackedType(valueTr))
            {
                AutoPackType packType = base.GetClass<GeneralHelper>().GetDefaultAutoPackType(valueTr);
                processor.Emit(OpCodes.Ldc_I4, (int)packType);
            }
            processor.Emit(OpCodes.Call, valueReaderMr);
            processor.Emit(OpCodes.Newobj, initMr);
            processor.Emit(OpCodes.Ret);

            return base.ImportReference(createdReaderMd);
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

            TypeDefinition objectTypeDef = objectTr.CachedResolve(base.Session);
            ILProcessor processor = createdReaderMd.Body.GetILProcessor();

            ParameterDefinition readerParameterDef = createdReaderMd.Parameters[0];
            // create local for return value
            VariableDefinition objectVariableDef = base.GetClass<GeneralHelper>().CreateVariable(createdReaderMd, objectTr);

            //If not a value type create a return null check.
            if (!objectTypeDef.IsValueType)
            {
                VariableDefinition nullVariableDef = base.GetClass<GeneralHelper>().CreateVariable(createdReaderMd, typeof(bool));
                //Load packed whole value into sizeVariableDef, exit if null indicator.
                base.GetClass<ReaderHelper>().CreateRetOnNull(processor, readerParameterDef, nullVariableDef, true);
            }

            /* If here then not null. */
            //Make a new instance of object type and set to objectVariableDef.
            base.GetClass<GeneralHelper>().SetVariableDefinitionFromObject(processor, objectVariableDef, objectTypeDef);
            if (!ReadFieldsAndProperties(createdReaderMd, readerParameterDef, objectVariableDef, objectTr))
                return null;
            /* //codegen scriptableobjects seem to climb too high up to UnityEngine.Object when
             * creating serializers/deserialized. Make sure this is not possible. */

            //Load result and return it.
            processor.Emit(OpCodes.Ldloc, objectVariableDef);
            processor.Emit(OpCodes.Ret);

            return base.ImportReference(createdReaderMd);
        }


        /// <summary>
        /// Reads all fields of objectTypeRef.
        /// </summary>  
        private bool ReadFieldsAndProperties(MethodDefinition readerMd, ParameterDefinition readerPd, VariableDefinition objectVd, TypeReference objectTr)
        {
            //This probably isn't needed but I'm too afraid to remove it.
            if (objectTr.Module != base.Module)
                objectTr = base.ImportReference(objectTr.CachedResolve(base.Session));

            //Fields.
            foreach (FieldDefinition fieldDef in objectTr.FindAllSerializableFields(base.Session
                , ReaderHelper.EXCLUDED_AUTO_SERIALIZER_TYPES, ReaderHelper.EXCLUDED_ASSEMBLY_PREFIXES))
            {
                FieldReference importedFr = base.ImportReference(fieldDef);
                if (GetReadMethod(fieldDef.FieldType, out MethodReference readMr))
                    base.GetClass<ReaderHelper>().CreateReadIntoClassOrStruct(readerMd, readerPd, readMr, objectVd, importedFr);
            }

            //Properties.
            foreach (PropertyDefinition propertyDef in objectTr.FindAllSerializableProperties(base.Session
                , ReaderHelper.EXCLUDED_AUTO_SERIALIZER_TYPES, ReaderHelper.EXCLUDED_ASSEMBLY_PREFIXES))
            {
                if (GetReadMethod(propertyDef.PropertyType, out MethodReference readMr))
                {
                    MethodReference setMr = base.Module.ImportReference(propertyDef.SetMethod);
                    base.GetClass<ReaderHelper>().CreateReadIntoClassOrStruct(readerMd, readerPd, readMr, objectVd, setMr, propertyDef.PropertyType);
                }
            }

            //Gets or creates writer method and outputs it. Returns true if method is found or created.
            bool GetReadMethod(TypeReference tr, out MethodReference readMr)
            {
                tr = base.ImportReference(tr);
                readMr = base.GetClass<ReaderHelper>().GetOrCreateFavoredReadMethodReference(tr, true);
                return (readMr != null);
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
            TypeDefinition readerTypeDef = base.GetClass<GeneralHelper>().GetOrCreateClass(out _, GENERATED_TYPE_ATTRIBUTES, GENERATED_READERS_CLASS_NAME, null);
            MethodDefinition readerMethodDef = readerTypeDef.AddMethod(methodName,
                    MethodAttributes.Public |
                    MethodAttributes.Static |
                    MethodAttributes.HideBySig,
                    objectTypeRef);

            base.GetClass<GeneralHelper>().CreateParameter(readerMethodDef, base.GetClass<ReaderHelper>().Reader_TypeRef, "reader");
            readerMethodDef.Body.InitLocals = true;

            return readerMethodDef;
        }


    }
}