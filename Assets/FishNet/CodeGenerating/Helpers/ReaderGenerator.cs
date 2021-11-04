using FishNet.CodeGenerating.Helping.Extension;
using FishNet.Object;
using FishNet.Serializing;
using MonoFN.Cecil;
using MonoFN.Cecil.Cil;
using System;

namespace FishNet.CodeGenerating.Helping
{
    internal class ReaderGenerator
    {

        #region Const.
        internal const string GENERATED_CLASS_NAME = WriterGenerator.GENERATED_CLASS_NAME;
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
        /// <param name="objectTypeRef"></param>
        /// <returns></returns>
        internal MethodReference CreateReader(TypeReference objectTypeRef)
        {
            MethodReference methodRefResult = null;
            TypeDefinition objectTypeDef;

            SerializerType serializerType = GeneratorHelper.GetSerializerType(objectTypeRef, false, out objectTypeDef);
            if (serializerType != SerializerType.Invalid)
            {
                //Array.
                if (serializerType == SerializerType.Array)
                {
                    methodRefResult = CreateArrayReaderMethodDefinition(objectTypeRef);
                }
                //Enum.
                else if (serializerType == SerializerType.Enum)
                {
                    methodRefResult = CreateEnumReaderMethodDefinition(objectTypeRef);
                }
                //List.
                else if (serializerType == SerializerType.List)
                {
                    methodRefResult = CreateListReaderMethodDefinition(objectTypeRef);
                }
                //NetworkBehaviour.
                else if (serializerType == SerializerType.NetworkBehaviour)
                {
                    methodRefResult = GetNetworkBehaviourReaderMethodReference(objectTypeRef);
                }
                //Class or struct.
                else if (serializerType == SerializerType.ClassOrStruct)
                {
                    methodRefResult = CreateClassOrStructReaderMethodDefinition(objectTypeRef);
                }
            }

            //If was created.
            if (methodRefResult != null)
            {
                CodegenSession.ReaderHelper.AddReaderMethod(objectTypeRef, methodRefResult, false, true);
            }

            return methodRefResult;
        }

        /// <summary>
        /// Generates a reader for objectTypeReference if one does not already exist.
        /// </summary>
        /// <param name="objectTypeRef"></param>
        /// <returns></returns>
        private MethodDefinition CreateEnumReaderMethodDefinition(TypeReference objectTypeRef)
        {
            MethodDefinition createdReaderMethodDef = CreateStaticReaderStubMethodDefinition(objectTypeRef);
            ILProcessor processor = createdReaderMethodDef.Body.GetILProcessor();
            //Get type reference for enum type. eg byte int
            TypeReference underlyingTypeRef = objectTypeRef.Resolve().GetEnumUnderlyingTypeReference();
            //Get read method for underlying type.
            MethodReference readMethodRef = CodegenSession.ReaderHelper.GetOrCreateFavoredReadMethodReference(underlyingTypeRef, true);
            if (readMethodRef == null)
                return null;

            ParameterDefinition readerParameterDef = createdReaderMethodDef.Parameters[0];
            //reader.ReadXXX().
            processor.Emit(OpCodes.Ldarg, readerParameterDef);
            if (CodegenSession.WriterHelper.IsAutoPackedType(underlyingTypeRef))
                processor.Emit(OpCodes.Ldc_I4, (int)AutoPackType.Packed);

            processor.Emit(OpCodes.Call, readMethodRef);

            processor.Emit(OpCodes.Ret);
            return createdReaderMethodDef;
        }


        /// <summary>
        /// Creates a read for a class type which inherits NetworkBehaviour.
        /// </summary>
        /// <param name="classTypeRef"></param>
        /// <returns></returns>
        private MethodReference GetNetworkBehaviourReaderMethodReference(TypeReference classTypeRef)
        {
            MethodDefinition createdReaderMethodDef = CreateStaticReaderStubMethodDefinition(classTypeRef);
            ILProcessor processor = createdReaderMethodDef.Body.GetILProcessor();
            TypeReference networkBehaviourTypeRef = CodegenSession.GeneralHelper.GetTypeReference(typeof(NetworkBehaviour));

            processor.Emit(OpCodes.Ldarg_0);
            //  processor.Emit<Reader>(OpCodes.Call, (reader) => reader.ReadNetworkBehaviour());
            processor.Emit(OpCodes.Call, CodegenSession.ReaderHelper.GetFavoredReadMethodReference(networkBehaviourTypeRef, true));
            processor.Emit(OpCodes.Castclass, classTypeRef);
            processor.Emit(OpCodes.Ret);
            return createdReaderMethodDef;
        }

        /// <summary>
        /// Create a reader for an array or list.
        /// </summary>
        private MethodDefinition CreateArrayReaderMethodDefinition(TypeReference objectTypeRef)
        {
            MethodDefinition createdReaderMethodDef = CreateStaticReaderStubMethodDefinition(objectTypeRef);

            /* Try to get instanced first for collection element type, if it doesn't exist then try to
             * get/or make a one. */
            TypeReference elementTypeRef = objectTypeRef.GetElementType();
            MethodReference readMethodRef = CodegenSession.ReaderHelper.GetOrCreateFavoredReadMethodReference(elementTypeRef, true);
            if (readMethodRef == null)
                return null;

            ILProcessor processor = createdReaderMethodDef.Body.GetILProcessor();

            ParameterDefinition readerParameterDef = createdReaderMethodDef.Parameters[0];
            VariableDefinition sizeVariableDef = CodegenSession.GeneralHelper.CreateVariable(createdReaderMethodDef, typeof(int));
            //Load packed whole value into sizeVariableDef, exit if null indicator.
            CodegenSession.ReaderHelper.CreateRetOnNull(processor, readerParameterDef, sizeVariableDef, false);

            //Make local variable of array type.
            VariableDefinition collectionVariableDef = CodegenSession.GeneralHelper.CreateVariable(createdReaderMethodDef, objectTypeRef);
            //Create new array/list of size.
            processor.Emit(OpCodes.Ldloc, sizeVariableDef);
            processor.Emit(OpCodes.Newarr, elementTypeRef);
            //Store new object of arr/list into collection variable.
            processor.Emit(OpCodes.Stloc, collectionVariableDef);

            VariableDefinition loopIndex = CodegenSession.GeneralHelper.CreateVariable(createdReaderMethodDef, typeof(int));
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

            return createdReaderMethodDef;
        }


        /// <summary>
        /// Create a reader for a list.
        /// </summary>
        private MethodDefinition CreateListReaderMethodDefinition(TypeReference objectTypeRef)
        {
            GenericInstanceType genericInstance = (GenericInstanceType)objectTypeRef;
            CodegenSession.Module.ImportReference(genericInstance);
            TypeReference elementTypeRef = genericInstance.GenericArguments[0];

            /* Try to get instanced first for collection element type, if it doesn't exist then try to
             * get/or make a one. */
            MethodReference readMethodRef = CodegenSession.ReaderHelper.GetOrCreateFavoredReadMethodReference(elementTypeRef, true);
            if (readMethodRef == null)
                return null;

            MethodDefinition createdReaderMethodDef = CreateStaticReaderStubMethodDefinition(objectTypeRef);
            ILProcessor processor = createdReaderMethodDef.Body.GetILProcessor();

            //Find constructor for new list.
            MethodDefinition constructorMd = objectTypeRef.Resolve().GetConstructor(new Type[] { typeof(int) });
            MethodReference constructorMr = constructorMd.MakeHostInstanceGeneric(genericInstance);
            //Find add method for list.
            MethodReference lstAddMd = objectTypeRef.Resolve().GetMethod("Add");
            MethodReference lstAddMr = lstAddMd.MakeHostInstanceGeneric(genericInstance);

            ParameterDefinition readerParameterDef = createdReaderMethodDef.Parameters[0];
            VariableDefinition sizeVariableDef = CodegenSession.GeneralHelper.CreateVariable(createdReaderMethodDef, typeof(int));
            //Load packed whole value into sizeVariableDef, exit if null indicator.
            CodegenSession.ReaderHelper.CreateRetOnNull(processor, readerParameterDef, sizeVariableDef, false);

            //Make variable of new list type, and create list object.
            VariableDefinition collectionVariableDef = CodegenSession.GeneralHelper.CreateVariable(createdReaderMethodDef, genericInstance);
            processor.Emit(OpCodes.Ldloc, sizeVariableDef);
            processor.Emit(OpCodes.Newobj, constructorMr);
            processor.Emit(OpCodes.Stloc, collectionVariableDef);

            VariableDefinition loopIndex = CodegenSession.GeneralHelper.CreateVariable(createdReaderMethodDef, typeof(int));
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

            return createdReaderMethodDef;
        }

        /// <summary>
        /// Creates a reader method for a struct or class objectTypeRef.
        /// </summary>
        /// <param name="objectTypeRef"></param>
        /// <returns></returns>
        private MethodDefinition CreateClassOrStructReaderMethodDefinition(TypeReference objectTypeRef)
        {
            MethodDefinition createdReaderMethodDef = CreateStaticReaderStubMethodDefinition(objectTypeRef);
            TypeDefinition objectTypeDef = objectTypeRef.Resolve();
            ILProcessor processor = createdReaderMethodDef.Body.GetILProcessor();

            ParameterDefinition readerParameterDef = createdReaderMethodDef.Parameters[0];
            // create local for return value
            VariableDefinition objectVariableDef = CodegenSession.GeneralHelper.CreateVariable(createdReaderMethodDef, objectTypeRef);

            //If not a value type create a return null check.
            if (!objectTypeDef.IsValueType)
            {
                VariableDefinition nullVariableDef = CodegenSession.GeneralHelper.CreateVariable(createdReaderMethodDef, typeof(bool));
                //Load packed whole value into sizeVariableDef, exit if null indicator.
                CodegenSession.ReaderHelper.CreateRetOnNull(processor, readerParameterDef, nullVariableDef, true);
            }

            /* If here then not null. */
            //Make a new instance of object type and set to objectVariableDef.
            CodegenSession.GeneralHelper.SetVariableDefinitionFromObject(processor, objectVariableDef, objectTypeDef);
            if (!ReadFields(processor, readerParameterDef, objectVariableDef, objectTypeRef))
                return null;
            /* //codegen scriptableobjects seem to climb too high up to UnityEngine.Object when
             * creating serializers/deserialized. Make sure this is not possible. */

            //Load result and return it.
            processor.Emit(OpCodes.Ldloc, objectVariableDef);
            processor.Emit(OpCodes.Ret);

            return createdReaderMethodDef;
        }

        /// <summary>
        /// Reads all fields of objectTypeRef.
        /// </summary>  
        private bool ReadFields(ILProcessor processor, ParameterDefinition readerParameterDef, VariableDefinition objectVariableDef, TypeReference objectTypeRef)
        {

            foreach (FieldDefinition fieldDef in objectTypeRef.FindAllPublicFields())
            {
                MethodReference readMethodRef = CodegenSession.ReaderHelper.GetOrCreateFavoredReadMethodReference(fieldDef.FieldType, true);
                //Not all fields will support reading, such as NonSerialized ones.
                if (readMethodRef == null)
                    continue;

                CodegenSession.ReaderHelper.CreateReadNoCreateMissing(processor, readerParameterDef, objectVariableDef, fieldDef);
            }

            return true;
        }


        /// <summary>
        /// Creates the stub for a new reader method.
        /// </summary>
        /// <param name="objectTypeRef"></param>
        /// <returns></returns>
        private MethodDefinition CreateStaticReaderStubMethodDefinition(TypeReference objectTypeRef)
        {
            string methodName = $"{READ_PREFIX}{objectTypeRef.FullName}";
            // create new reader for this type
            TypeDefinition readerTypeDef = CodegenSession.GeneralHelper.GetOrCreateClass(out _, GENERATED_TYPE_ATTRIBUTES, GENERATED_CLASS_NAME, null);
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