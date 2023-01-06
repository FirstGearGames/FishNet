
using FishNet.CodeGenerating.Helping;
using FishNet.CodeGenerating.Helping.Extension;
using FishNet.Serializing;
using MonoFN.Cecil;
using MonoFN.Cecil.Cil;
using System.Collections.Generic;

namespace FishNet.CodeGenerating.Processing
{
    internal class CustomSerializerProcessor : CodegenBase
    {

        #region Types.
        internal enum ExtensionType
        {
            None,
            Write,
            Read
        }

        #endregion

        internal bool CreateSerializerDelegates(TypeDefinition typeDef, bool replace)
        {
            bool modified = false;            
            /* Find all declared methods and register delegates to them.
             * After they are all registered create any custom writers
             * needed to complete the declared methods. It's important to
             * make generated writers after so that a generated method
             * isn't made for a type when the user has already made a declared one. */
            foreach (MethodDefinition methodDef in typeDef.Methods)
            {
                ExtensionType extensionType = GetExtensionType(methodDef);
                if (extensionType == ExtensionType.None)
                    continue;
                if (base.GetClass<GeneralHelper>().CodegenExclude(methodDef))
                    continue;

                MethodReference methodRef = base.ImportReference(methodDef);
                if (extensionType == ExtensionType.Write)
                {
                    base.GetClass<WriterHelper>().AddWriterMethod(methodRef.Parameters[1].ParameterType, methodRef, false, !replace);
                    modified = true;
                }
                else if (extensionType == ExtensionType.Read)
                {
                    base.GetClass<ReaderHelper>().AddReaderMethod(methodRef.ReturnType, methodRef, false, !replace);
                    modified = true;
                }
            }

            return modified;
        }

        /// <summary>
        /// Creates serializers for any custom types for declared methods.
        /// </summary>
        /// <param name="declaredMethods"></param>
        /// <param name="moduleDef"></param>
        internal bool CreateSerializers(TypeDefinition typeDef)
        {
            bool modified = false;

            List<(MethodDefinition, ExtensionType)> declaredMethods = new List<(MethodDefinition, ExtensionType)>();
            /* Go through all custom serializers again and see if 
             * they use any types that the user didn't make a serializer for
             * and that there isn't a built-in type for. Create serializers
             * for these types. */
            foreach (MethodDefinition methodDef in typeDef.Methods)
            {
                ExtensionType extensionType = GetExtensionType(methodDef);
                if (extensionType == ExtensionType.None)
                    continue;
                if (base.GetClass<GeneralHelper>().CodegenExclude(methodDef))
                    continue;
                 
                declaredMethods.Add((methodDef, extensionType));
                modified = true;
            }
            //Now that all declared are loaded see if any of them need generated serializers.
            foreach ((MethodDefinition methodDef, ExtensionType extensionType) in declaredMethods)
                CreateSerializers(extensionType, methodDef);

            return modified;
        }


        /// <summary>
        /// Creates a custom serializer for any types not handled within users declared.
        /// </summary>
        /// <param name="extensionType"></param>
        /// <param name="moduleDef"></param>
        /// <param name="methodDef"></param>
        /// <param name="diagnostics"></param>
        private void CreateSerializers(ExtensionType extensionType, MethodDefinition methodDef)
        {
            for (int i = 0; i < methodDef.Body.Instructions.Count; i++)
                CheckToModifyInstructions(extensionType, methodDef, ref i);
        }

        /// <summary>
        /// Checks if instructions need to be modified and does so.
        /// </summary>
        /// <param name="methodDef"></param>
        /// <param name="instructionIndex"></param>
        private void CheckToModifyInstructions(ExtensionType extensionType, MethodDefinition methodDef, ref int instructionIndex)
        {
            Instruction instruction = methodDef.Body.Instructions[instructionIndex];
            //Fields.
            if (instruction.OpCode == OpCodes.Ldsfld || instruction.OpCode == OpCodes.Ldfld)
                CheckFieldReferenceInstruction(extensionType, methodDef, ref instructionIndex);
            //Method calls.
            else if (instruction.OpCode == OpCodes.Call || instruction.OpCode == OpCodes.Callvirt)
                CheckCallInstruction(extensionType, methodDef, ref instructionIndex, (MethodReference)instruction.Operand);
        }


        /// <summary>
        /// Checks if a reader or writer must be generated for a field type.
        /// </summary>
        /// <param name="methodDef"></param>
        /// <param name="instructionIndex"></param>
        private void CheckFieldReferenceInstruction(ExtensionType extensionType, MethodDefinition methodDef, ref int instructionIndex)
        {
            Instruction instruction = methodDef.Body.Instructions[instructionIndex];
            FieldReference field = (FieldReference)instruction.Operand;
            TypeReference typeRef = field.DeclaringType;

            if (typeRef.IsType(typeof(GenericWriter<>)) || typeRef.IsType(typeof(GenericReader<>)) && typeRef.IsGenericInstance)
            {
                GenericInstanceType typeGenericInst = (GenericInstanceType)typeRef;
                TypeReference parameterType = typeGenericInst.GenericArguments[0];
                CreateReaderOrWriter(extensionType, methodDef, ref instructionIndex, parameterType);
            }
        }


        /// <summary>
        /// Checks if a reader or writer must be generated for a call type.
        /// </summary>
        /// <param name="extensionType"></param>
        /// <param name="moduleDef"></param>
        /// <param name="methodDef"></param>
        /// <param name="instructionIndex"></param>
        /// <param name="method"></param>
        private void CheckCallInstruction(ExtensionType extensionType, MethodDefinition methodDef, ref int instructionIndex, MethodReference method)
        {
            if (!method.IsGenericInstance)
                return;

            //True if call is to read/write.
            bool canCreate = (
                method.Is<Writer>(nameof(Writer.Write)) ||
                method.Is<Reader>(nameof(Reader.Read))
                );

            if (canCreate)
            {
                GenericInstanceMethod instanceMethod = (GenericInstanceMethod)method;
                TypeReference parameterType = instanceMethod.GenericArguments[0];
                if (parameterType.IsGenericParameter)
                    return;

                CreateReaderOrWriter(extensionType, methodDef, ref instructionIndex, parameterType);
            }
        }


        /// <summary>
        /// Creates a reader or writer for parameterType.
        /// </summary>
        /// <param name="extensionType"></param>
        /// <param name="methodDef"></param>
        /// <param name="instructionIndex"></param>
        /// <param name="parameterType"></param>
        private void CreateReaderOrWriter(ExtensionType extensionType, MethodDefinition methodDef, ref int instructionIndex, TypeReference parameterType)
        {
            if (!parameterType.IsGenericParameter && parameterType.CanBeResolved(base.Session))
            {
                TypeDefinition typeDefinition = parameterType.CachedResolve(base.Session);
                //If class and not value type check for accessible constructor.
                if (typeDefinition.IsClass && !typeDefinition.IsValueType)
                {
                    MethodDefinition constructor = typeDefinition.GetMethod(".ctor");
                    //Constructor is inaccessible, cannot create serializer for type.
                    if (!constructor.IsPublic)
                    {
                        base.LogError($"Unable to generator serializers for {typeDefinition.FullName} because it's constructor is not public.");
                        return;
                    }
                }

                ILProcessor processor = methodDef.Body.GetILProcessor();

                //Find already existing read or write method.
                MethodReference createdMethodRef = (extensionType == ExtensionType.Write) ?
                    base.GetClass<WriterHelper>().GetFavoredWriteMethodReference(parameterType, true) :
                    base.GetClass<ReaderHelper>().GetFavoredReadMethodReference(parameterType, true);
                //If a created method already exist nothing further is required.
                if (createdMethodRef != null)
                {
                    //Replace call to generic with already made serializer.
                    Instruction newInstruction = processor.Create(OpCodes.Call, createdMethodRef);
                    methodDef.Body.Instructions[instructionIndex] = newInstruction;
                    return;
                }
                else
                {
                    createdMethodRef = (extensionType == ExtensionType.Write) ?
                        base.GetClass<WriterGenerator>().CreateWriter(parameterType) :
                        base.GetClass<ReaderGenerator>().CreateReader(parameterType);
                }

                //If method was created.
                if (createdMethodRef != null)
                {
                    /* If an autopack type then we have to inject the
                     * autopack above the new instruction. */
                    if (base.GetClass<WriterHelper>().IsAutoPackedType(parameterType))
                    {
                        AutoPackType packType = base.GetClass<GeneralHelper>().GetDefaultAutoPackType(parameterType);
                        Instruction autoPack = processor.Create(OpCodes.Ldc_I4, (int)packType);
                        methodDef.Body.Instructions.Insert(instructionIndex, autoPack);
                        instructionIndex++;
                    }
                    Instruction newInstruction = processor.Create(OpCodes.Call, createdMethodRef);
                    methodDef.Body.Instructions[instructionIndex] = newInstruction;
                }
            }
        }


        /// <summary>
        /// Returns the RPC attribute on a method, if one exist. Otherwise returns null.
        /// </summary>
        /// <param name="methodDef"></param>
        /// <returns></returns>
        private ExtensionType GetExtensionType(MethodDefinition methodDef)
        {
            bool hasExtensionAttribute = methodDef.HasCustomAttribute<System.Runtime.CompilerServices.ExtensionAttribute>();
            if (!hasExtensionAttribute)
                return ExtensionType.None;

            bool write = (methodDef.ReturnType == methodDef.Module.TypeSystem.Void);

            //Return None for Mirror types.
#if MIRROR
            if (write)
            {
                if (methodDef.Parameters.Count > 0 && methodDef.Parameters[0].ParameterType.FullName == "Mirror.NetworkWriter")
                    return ExtensionType.None;                    
            }
            else
            {
                if (methodDef.Parameters.Count > 0 && methodDef.Parameters[0].ParameterType.FullName == "Mirror.NetworkReader")
                    return ExtensionType.None;
            }
#endif


            string prefix = (write) ?
                WriterHelper.WRITE_PREFIX : ReaderHelper.READ_PREFIX;

            //Does not contain prefix.
            if (methodDef.Name.Length < prefix.Length || methodDef.Name.Substring(0, prefix.Length) != prefix)
                return ExtensionType.None;

            //Make sure first parameter is right.
            if (methodDef.Parameters.Count >= 1)
            {
                TypeReference tr = methodDef.Parameters[0].ParameterType;
                if (tr.FullName != base.GetClass<WriterHelper>().Writer_TypeRef.FullName &&
                    tr.FullName != base.GetClass<ReaderHelper>().Reader_TypeRef.FullName)
                    return ExtensionType.None;
            }

            if (write && methodDef.Parameters.Count < 2)
            {
                base.LogError($"{methodDef.FullName} must have at least two parameters, the first being PooledWriter, and second value to write.");
                return ExtensionType.None;
            }
            else if (!write && methodDef.Parameters.Count < 1)
            {
                base.LogError($"{methodDef.FullName} must have at least one parameters, the first being PooledReader.");
                return ExtensionType.None;
            }

            return (write) ? ExtensionType.Write : ExtensionType.Read;
        }


    }
}