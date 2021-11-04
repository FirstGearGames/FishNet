using FishNet.CodeGenerating.Helping.Extension;
using FishNet.CodeGenerating.ILCore;
using FishNet.Managing;
using FishNet.Managing.Logging;
using FishNet.Object;
using FishNet.Object.Helping;
using FishNet.Serializing;
using FishNet.Serializing.Helping;
using MonoFN.Cecil;
using MonoFN.Cecil.Cil;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace FishNet.CodeGenerating.Helping
{
    internal class GeneralHelper
    {
        #region Reflection references.
        private MethodReference InstanceFinder_NetworkManager_MethodRef;
        private MethodReference NetworkBehaviour_CanLog_MethodRef;
        private MethodReference NetworkManager_CanLog_MethodRef;
        private MethodReference NetworkBehaviour_NetworkManager_MethodRef;
        private MethodReference NetworkManager_LogCommon_MethodRef;
        private MethodReference NetworkManager_LogWarning_MethodRef;
        private MethodReference NetworkManager_LogError_MethodRef;
        internal MethodReference Debug_LogCommon_MethodRef;
        private MethodReference Debug_LogWarning_MethodRef;
        private MethodReference Debug_LogError_MethodRef;
        internal MethodReference Comparers_EqualityCompare_MethodRef;
        internal MethodReference IsServer_MethodRef = null;
        internal MethodReference IsClient_MethodRef = null;
        internal MethodReference NetworkObject_Deinitializing_MethodRef = null;
        private Dictionary<Type, TypeReference> _importedTypeReferences = new Dictionary<Type, TypeReference>();
        private Dictionary<FieldDefinition, FieldReference> _importedFieldReferences = new Dictionary<FieldDefinition, FieldReference>();
        private string NonSerialized_Attribute_FullName;
        private string Single_FullName;
        #endregion

        internal bool ImportReferences()
        {
            NonSerialized_Attribute_FullName = typeof(NonSerializedAttribute).FullName;
            Single_FullName = typeof(float).FullName;

            Type comparers = typeof(Comparers);
            Comparers_EqualityCompare_MethodRef = CodegenSession.Module.ImportReference<Comparers>(x => Comparers.EqualityCompare<object>(default, default));

            //Networkbehaviour.
            Type networkBehaviourType = typeof(NetworkBehaviour);
            foreach (System.Reflection.MethodInfo methodInfo in networkBehaviourType.GetMethods())
            {
                if (methodInfo.Name == nameof(NetworkBehaviour.CanLog))
                    NetworkBehaviour_CanLog_MethodRef = CodegenSession.Module.ImportReference(methodInfo);
            }
            foreach (System.Reflection.PropertyInfo propertyInfo in networkBehaviourType.GetProperties())
            {
                if (propertyInfo.Name == nameof(NetworkBehaviour.NetworkManager))
                    NetworkBehaviour_NetworkManager_MethodRef = CodegenSession.Module.ImportReference(propertyInfo.GetMethod);
            }

            //Instancefinder.
            Type instanceFinderType = typeof(InstanceFinder);
            System.Reflection.PropertyInfo getNetworkManagerPropertyInfo = instanceFinderType.GetProperty(nameof(InstanceFinder.NetworkManager));
            InstanceFinder_NetworkManager_MethodRef = CodegenSession.Module.ImportReference(getNetworkManagerPropertyInfo.GetMethod);

            //NetworkManager debug logs.
            Type networkManagerType = typeof(NetworkManager);
            foreach (System.Reflection.MethodInfo methodInfo in networkManagerType.GetMethods())
            {
                if (methodInfo.Name == nameof(NetworkManager.Log))
                    NetworkManager_LogCommon_MethodRef = CodegenSession.Module.ImportReference(methodInfo);
                else if (methodInfo.Name == nameof(NetworkManager.LogWarning))
                    NetworkManager_LogWarning_MethodRef = CodegenSession.Module.ImportReference(methodInfo);
                else if (methodInfo.Name == nameof(NetworkManager.LogError))
                    NetworkManager_LogError_MethodRef = CodegenSession.Module.ImportReference(methodInfo);
                else if (methodInfo.Name == nameof(NetworkManager.CanLog))
                    NetworkManager_CanLog_MethodRef = CodegenSession.Module.ImportReference(methodInfo);
            }

            //Unity debug logs.
            Type debugType = typeof(UnityEngine.Debug);
            foreach (System.Reflection.MethodInfo methodInfo in debugType.GetMethods())
            {
                if (methodInfo.Name == nameof(Debug.LogWarning) && methodInfo.GetParameters().Length == 1)
                    Debug_LogWarning_MethodRef = CodegenSession.Module.ImportReference(methodInfo);
                else if (methodInfo.Name == nameof(Debug.LogError) && methodInfo.GetParameters().Length == 1)
                    Debug_LogError_MethodRef = CodegenSession.Module.ImportReference(methodInfo);
                else if (methodInfo.Name == nameof(Debug.Log) && methodInfo.GetParameters().Length == 1)
                    Debug_LogCommon_MethodRef = CodegenSession.Module.ImportReference(methodInfo);
            }

            Type codegenHelper = typeof(CodegenHelper);
            foreach (System.Reflection.MethodInfo methodInfo in codegenHelper.GetMethods())
            {
                if (methodInfo.Name == nameof(CodegenHelper.NetworkObject_Deinitializing))
                    NetworkObject_Deinitializing_MethodRef = CodegenSession.Module.ImportReference(methodInfo);
                else if (methodInfo.Name == nameof(CodegenHelper.IsClient))
                    IsClient_MethodRef = CodegenSession.Module.ImportReference(methodInfo);
                else if (methodInfo.Name == nameof(CodegenHelper.IsServer))
                    IsServer_MethodRef = CodegenSession.Module.ImportReference(methodInfo);
            }

            return true;
        }

        /// <summary>
        /// Returns if typeDef should be ignored.
        /// </summary>
        /// <param name="typeDef"></param>
        /// <returns></returns>
        internal bool IgnoreTypeDefinition(TypeDefinition typeDef)
        {
            //If FishNet assembly.
            if (typeDef.Module.Assembly.Name.Name == FishNetILPP.RUNTIME_ASSEMBLY_NAME)
            {
                foreach (CustomAttribute item in typeDef.CustomAttributes)
                {
                    if (item.AttributeType.FullName == typeof(CodegenIncludeInternalAttribute).FullName)
                    {
                        if (FishNetILPP.CODEGEN_THIS_NAMESPACE.Length > 0)
                            return !typeDef.FullName.Contains(FishNetILPP.CODEGEN_THIS_NAMESPACE);
                        else
                            return false;
                    }
                }

                return true;
            }
            //Not FishNet assembly.
            else
            {
                if (FishNetILPP.CODEGEN_THIS_NAMESPACE.Length > 0)
                    return true;

                foreach (CustomAttribute item in typeDef.CustomAttributes)
                {
                    if (item.AttributeType.FullName == typeof(CodegenExcludeAttribute).FullName)
                        return true;
                }

                return false;
            }
        }

        /// <summary>
        /// Returns if methodInfo should be ignored.
        /// </summary>
        /// <param name="methodInfo"></param>
        /// <returns></returns>
        internal bool IgnoreMethod(System.Reflection.MethodInfo methodInfo)
        {
            foreach (System.Reflection.CustomAttributeData item in methodInfo.CustomAttributes)
            {
                if (item.AttributeType == typeof(CodegenExcludeAttribute))
                    return true;
            }

            return false;
        }


        /// <summary>
        /// Creates the RuntimeInitializeOnLoadMethod attribute for a method.
        /// </summary>
        internal void CreateRuntimeInitializeOnLoadMethodAttribute(MethodDefinition methodDef, string loadType = "")
        {
            TypeReference attTypeRef = GetTypeReference(typeof(RuntimeInitializeOnLoadMethodAttribute));
            foreach (CustomAttribute item in methodDef.CustomAttributes)
            {
                //Already exist.
                if (item.AttributeType.FullName == attTypeRef.FullName)
                    return;
            }

            int parameterRequirement = (loadType.Length == 0) ? 0 : 1;
            MethodDefinition constructorMethodDef = attTypeRef.GetConstructor(parameterRequirement);
            MethodReference constructorMethodRef = CodegenSession.Module.ImportReference(constructorMethodDef);
            CustomAttribute ca = new CustomAttribute(constructorMethodRef);
            /* If load type isn't null then it
             * has to be passed in as the first argument. */
            if (loadType.Length > 0)
            {
                Type t = typeof(RuntimeInitializeLoadType);
                foreach (UnityEngine.RuntimeInitializeLoadType value in t.GetEnumValues())
                {
                    if (loadType == value.ToString())
                    {
                        TypeReference tr = CodegenSession.Module.ImportReference(t);
                        CustomAttributeArgument arg = new CustomAttributeArgument(tr, value);
                        ca.ConstructorArguments.Add(arg);
                    }
                }
            }

            methodDef.CustomAttributes.Add(ca);
        }

        /// <summary>
        /// Gets the default AutoPackType to use for typeRef.
        /// </summary>
        /// <param name="typeRef"></param>
        /// <returns></returns>
        internal AutoPackType GetDefaultAutoPackType(TypeReference typeRef)
        {
            //Singles are defauled to unpacked.
            if (typeRef.FullName == Single_FullName)
                return AutoPackType.Unpacked;
            else
                return AutoPackType.Packed;
        }

        /// <summary>
        /// Gets the InitializeOnce method in typeDef or creates the method should it not exist.
        /// </summary>
        /// <param name="typeDef"></param>
        /// <returns></returns>
        internal MethodDefinition GetOrCreateMethod(TypeDefinition typeDef, out bool created, MethodAttributes methodAttr, string methodName, TypeReference returnType)
        {
            MethodDefinition result = typeDef.GetMethod(methodName);
            if (result == null)
            {
                created = true;
                result = new MethodDefinition(methodName, methodAttr, returnType);
                typeDef.Methods.Add(result);
            }
            else
            {
                created = false;
            }

            return result;
        }


        /// <summary>
        /// Gets a class within moduleDef or creates and returns the class if it does not already exist.
        /// </summary>
        /// <param name="moduleDef"></param>
        /// <returns></returns>
        internal TypeDefinition GetOrCreateClass(out bool created, TypeAttributes typeAttr, string className, TypeReference baseTypeRef)
        {
            TypeDefinition type = CodegenSession.Module.GetClass(className);
            if (type != null)
            {
                created = false;
                return type;
            }
            else
            {
                created = true;
                type = new TypeDefinition(FishNetILPP.RUNTIME_ASSEMBLY_NAME, className,
                    typeAttr, CodegenSession.Module.ImportReference(typeof(object)));
                //Add base class if specified.
                if (baseTypeRef != null)
                    type.BaseType = CodegenSession.Module.ImportReference(baseTypeRef);

                CodegenSession.Module.Types.Add(type);
                return type;
            }
        }

        #region HasNonSerializableAttribute
        /// <summary>
        /// Returns if fieldDef has a NonSerialized attribute.
        /// </summary>
        /// <param name="fieldDef"></param>
        /// <returns></returns>
        internal bool HasNonSerializableAttribute(FieldDefinition fieldDef)
        {
            foreach (CustomAttribute customAttribute in fieldDef.CustomAttributes)
            {
                if (customAttribute.AttributeType.FullName == NonSerialized_Attribute_FullName)
                    return true;
            }

            //Fall through, no matches.
            return false;
        }
        /// <summary>
        /// Returns if typeDef has a NonSerialized attribute.
        /// </summary>
        /// <param name="typeDef"></param>
        /// <returns></returns>
        internal bool HasNonSerializableAttribute(TypeDefinition typeDef)
        {
            foreach (CustomAttribute customAttribute in typeDef.CustomAttributes)
            {
                if (customAttribute.AttributeType.FullName == NonSerialized_Attribute_FullName)
                    return true;
            }

            //Fall through, no matches.
            return false;
        }
        #endregion

        /// <summary>
        /// Gets a TypeReference for a type.
        /// </summary>
        /// <param name="type"></param>
        internal TypeReference GetTypeReference(Type type)
        {
            TypeReference result;
            if (!_importedTypeReferences.TryGetValue(type, out result))
            {
                result = CodegenSession.Module.ImportReference(type);
                _importedTypeReferences.Add(type, result);
            }

            return result;
        }

        /// <summary>
        /// Gets a FieldReference for a type.
        /// </summary>
        /// <param name="type"></param>
        internal FieldReference GetFieldReference(FieldDefinition fieldDef)
        {
            FieldReference result;
            if (!_importedFieldReferences.TryGetValue(fieldDef, out result))
            {
                result = CodegenSession.Module.ImportReference(fieldDef);
                _importedFieldReferences.Add(fieldDef, result);
            }

            return result;
        }

        /// <summary>
        /// Gets the current constructor for typeDef, or makes a new one if constructor doesn't exist.
        /// </summary>
        /// <param name="typeDef"></param>
        /// <returns></returns>
        internal MethodDefinition GetOrCreateConstructor(TypeDefinition typeDef, out bool created, bool makeStatic)
        {
            // find constructor
            MethodDefinition constructorMethodDef = typeDef.GetMethod(".cctor");
            if (constructorMethodDef == null)
                constructorMethodDef = typeDef.GetMethod(".ctor");

            //Constructor already exist.
            if (constructorMethodDef != null)
            {
                if (!makeStatic)
                    constructorMethodDef.Attributes &= ~MethodAttributes.Static;

                created = false;
            }
            //Static constructor does not exist yet.
            else
            {
                created = true;
                MethodAttributes methodAttr = (MonoFN.Cecil.MethodAttributes.HideBySig |
                        MonoFN.Cecil.MethodAttributes.SpecialName |
                        MonoFN.Cecil.MethodAttributes.RTSpecialName);
                if (makeStatic)
                    methodAttr |= MonoFN.Cecil.MethodAttributes.Static;

                //Create a constructor.
                constructorMethodDef = new MethodDefinition(".ctor", methodAttr,
                        typeDef.Module.TypeSystem.Void
                        );

                typeDef.Methods.Add(constructorMethodDef);

                //Add ret.
                ILProcessor processor = constructorMethodDef.Body.GetILProcessor();
                processor.Emit(OpCodes.Ret);
            }

            return constructorMethodDef;
        }

        /// <summary>
        /// Creates a return of boolean type.
        /// </summary>
        /// <param name="processor"></param>
        /// <param name="result"></param>
        internal void CreateRetBoolean(ILProcessor processor, bool result)
        {
            OpCode code = (result) ? OpCodes.Ldc_I4_1 : OpCodes.Ldc_I4_0;
            processor.Emit(code);
            processor.Emit(OpCodes.Ret);
        }

        #region Debug logging.
        /// <summary>
        /// Creates a debug print if NetworkManager.CanLog is true.
        /// </summary>
        /// <param name="processor"></param>
        /// <param name="loggingType"></param>
        /// <param name="useStatic">True to use InstanceFinder, false to use base.</param>
        /// <returns></returns>
        internal List<Instruction> CreateDebugWithCanLogInstructions(ILProcessor processor, string message, LoggingType loggingType, bool useStatic, bool useNetworkManagerLog)
        {
            List<Instruction> instructions = new List<Instruction>();
            if (loggingType == LoggingType.Off)
                return instructions;

            List<Instruction> debugPrint = CreateDebugInstructions(processor, message, loggingType, useNetworkManagerLog);
            //Couldn't make debug print.
            if (debugPrint.Count == 0)
                return instructions;


            //If null skip..
            Instruction skipDebugInst = processor.Create(OpCodes.Nop);
            VariableDefinition nmVariableDef = CreateVariable(processor.Body.Method, typeof(NetworkManager));
            //Using InstanceFinder(static).
            if (useStatic)
            {
                //Store nm refrence.
                instructions.Add(processor.Create(OpCodes.Call, InstanceFinder_NetworkManager_MethodRef));
                instructions.Add(processor.Create(OpCodes.Stloc, nmVariableDef));
            }
            //Using networkBehaviour.
            else
            {
                //Store nm reference.
                instructions.Add(processor.Create(OpCodes.Ldarg_0));
                instructions.Add(processor.Create(OpCodes.Call, NetworkBehaviour_NetworkManager_MethodRef));
                instructions.Add(processor.Create(OpCodes.Stloc, nmVariableDef));
            }

            //null check nm reference.
            instructions.Add(processor.Create(OpCodes.Ldloc, nmVariableDef));
            instructions.Add(processor.Create(OpCodes.Brfalse_S, skipDebugInst));

            //Only need to call CanLog if not using networkmanager logging.
            if (!useNetworkManagerLog)
            {
                //Call canlog.
                instructions.Add(processor.Create(OpCodes.Ldarg_0));
                instructions.Add(processor.Create(OpCodes.Ldc_I4, (int)loggingType));
                instructions.Add(processor.Create(OpCodes.Call, NetworkBehaviour_CanLog_MethodRef));
                instructions.Add(processor.Create(OpCodes.Brfalse_S, skipDebugInst));
            }

            instructions.Add(processor.Create(OpCodes.Ldloc, nmVariableDef));
            instructions.AddRange(debugPrint);
            instructions.Add(skipDebugInst);

            return instructions;
        }

        /// <summary>
        /// Creates a debug print if NetworkManager.CanLog is true.
        /// </summary>
        /// <param name="processor"></param>
        /// <param name="loggingType"></param>
        /// <param name="useStatic">True to use InstanceFinder, false to use base.</param>
        /// <returns></returns>
        internal void CreateDebugWithCanLog(ILProcessor processor, string message, LoggingType loggingType, bool useStatic, bool useNetworkManagerLog)
        {
            List<Instruction> instructions = CreateDebugWithCanLogInstructions(processor, message, loggingType, useStatic, useNetworkManagerLog);
            if (instructions.Count == 0)
                return;

            processor.Add(instructions);
        }
        /// <summary>
        /// Creates a debug and returns instructions.
        /// </summary>
        /// <param name="processor"></param>
        private List<Instruction> CreateDebugInstructions(ILProcessor processor, string message, LoggingType loggingType, bool useNetworkManagerLog)
        {
            List<Instruction> instructions = new List<Instruction>();
            if (loggingType == LoggingType.Off)
                return instructions;

            instructions.Add(processor.Create(OpCodes.Ldstr, message));

            MethodReference methodRef;
            if (loggingType == LoggingType.Common)
                methodRef = (useNetworkManagerLog) ? NetworkManager_LogCommon_MethodRef : Debug_LogCommon_MethodRef;
            else if (loggingType == LoggingType.Warning)
                methodRef = (useNetworkManagerLog) ? NetworkManager_LogWarning_MethodRef : Debug_LogWarning_MethodRef;
            else
                methodRef = (useNetworkManagerLog) ? NetworkManager_LogError_MethodRef : Debug_LogError_MethodRef;

            instructions.Add(processor.Create(OpCodes.Call, methodRef));

            return instructions;
        }
        /// <summary>
        /// Creates a debug.
        /// </summary>
        /// <param name="processor"></param>
        internal void CreateDebug(ILProcessor processor, string message, LoggingType loggingType, bool useNetworkManagerLog)
        {
            List<Instruction> instructions = CreateDebugInstructions(processor, message, loggingType, useNetworkManagerLog);
            if (instructions.Count == 0)
                return;

            processor.Add(instructions);
        }
        #endregion

        #region CreateVariable / CreateParameter.
        /// <summary>
        /// Creates a parameter within methodDef and returns it's ParameterDefinition.
        /// </summary>
        /// <param name="methodDef"></param>
        /// <param name="parameterTypeRef"></param>
        /// <returns></returns>
        internal ParameterDefinition CreateParameter(MethodDefinition methodDef, TypeDefinition parameterTypeDef, string name = "", ParameterAttributes attributes = ParameterAttributes.None, int index = -1)
        {
            TypeReference typeRef = methodDef.Module.ImportReference(parameterTypeDef);
            return CreateParameter(methodDef, typeRef, name, attributes, index);
        }
        /// <summary>
        /// Creates a parameter within methodDef and returns it's ParameterDefinition.
        /// </summary>
        /// <param name="methodDef"></param>
        /// <param name="parameterTypeRef"></param>
        /// <returns></returns>
        internal ParameterDefinition CreateParameter(MethodDefinition methodDef, TypeReference parameterTypeRef, string name = "", ParameterAttributes attributes = ParameterAttributes.None, int index = -1)
        {
            ParameterDefinition parameterDef = new ParameterDefinition(name, attributes, parameterTypeRef);
            if (index == -1)
                methodDef.Parameters.Add(parameterDef);
            else
                methodDef.Parameters.Insert(index, parameterDef);
            return parameterDef;
        }
        /// <summary>
        /// Creates a parameter within methodDef and returns it's ParameterDefinition.
        /// </summary>
        /// <param name="methodDef"></param>
        /// <param name="parameterTypeRef"></param>
        /// <returns></returns>
        internal ParameterDefinition CreateParameter(MethodDefinition methodDef, Type parameterType, string name = "", ParameterAttributes attributes = ParameterAttributes.None, int index = -1)
        {
            return CreateParameter(methodDef, GetTypeReference(parameterType), name, attributes, index);
        }
        /// <summary>
        /// Creates a variable type within the body and returns it's VariableDef.
        /// </summary>
        /// <param name="methodDef"></param>
        /// <param name="variableTypeRef"></param>
        /// <returns></returns>
        internal VariableDefinition CreateVariable(MethodDefinition methodDef, TypeReference variableTypeRef)
        {
            VariableDefinition variableDef = new VariableDefinition(variableTypeRef);
            methodDef.Body.Variables.Add(variableDef);
            return variableDef;
        }
        /// Creates a variable type within the body and returns it's VariableDef.
        /// </summary>
        /// <param name="processor"></param>
        /// <param name="methodDef"></param>
        /// <param name="variableTypeRef"></param>
        /// <returns></returns>
        internal VariableDefinition CreateVariable(MethodDefinition methodDef, Type variableType)
        {
            return CreateVariable(methodDef, GetTypeReference(variableType));
        }
        #endregion

        #region SetVariableDef.
        /// <summary>
        /// Initializes variableDef as a new object or collection of typeDef.
        /// </summary>
        /// <param name="processor"></param>
        /// <param name="variableDef"></param>
        /// <param name="typeDef"></param>
        internal void SetVariableDefinitionFromObject(ILProcessor processor, VariableDefinition variableDef, TypeDefinition typeDef)
        {
            TypeReference type = variableDef.VariableType;
            if (type.IsValueType)
            {
                // structs are created with Initobj
                processor.Emit(OpCodes.Ldloca, variableDef);
                processor.Emit(OpCodes.Initobj, type);
            }
            else if (typeDef.InheritsFrom<UnityEngine.ScriptableObject>())
            {
                MethodReference soCreateInstanceMr = processor.Body.Method.Module.ImportReference(() => UnityEngine.ScriptableObject.CreateInstance<UnityEngine.ScriptableObject>());
                GenericInstanceMethod genericInstanceMethod = soCreateInstanceMr.GetElementMethod().MakeGenericMethod(new TypeReference[] { type });
                processor.Emit(OpCodes.Call, genericInstanceMethod);
                processor.Emit(OpCodes.Stloc, variableDef);
            }
            else
            {
                MethodDefinition constructorMethodDef = type.GetConstructor();
                if (constructorMethodDef == null)
                {
                    CodegenSession.LogError($"{type.Name} can't be deserialized because a default constructor could not be found. Create a default constructor or a custom serializer/deserializer.");
                    return;
                }

                MethodReference constructorMethodRef = processor.Body.Method.Module.ImportReference(constructorMethodDef);
                processor.Emit(OpCodes.Newobj, constructorMethodRef);
                processor.Emit(OpCodes.Stloc, variableDef);
            }
        }

        /// <summary>
        /// Assigns value to a VariableDef.
        /// </summary>
        /// <param name="processor"></param>
        /// <param name="variableDef"></param>
        /// <param name="value"></param>
        internal void SetVariableDefinitionFromInt(ILProcessor processor, VariableDefinition variableDef, int value)
        {
            processor.Emit(OpCodes.Ldc_I4, value);
            processor.Emit(OpCodes.Stloc, variableDef);
        }
        /// <summary>
        /// Assigns value to a VariableDef.
        /// </summary>
        /// <param name="processor"></param>
        /// <param name="variableDef"></param>
        /// <param name="value"></param>
        internal void SetVariableDefinitionFromParameter(ILProcessor processor, VariableDefinition variableDef, ParameterDefinition value)
        {
            processor.Emit(OpCodes.Ldarg, value);
            processor.Emit(OpCodes.Stloc, variableDef);
        }
        #endregion.

        /// <summary>
        /// Returns if an instruction is a call to a method.
        /// </summary>
        /// <param name="instruction"></param>
        /// <param name="calledMethod"></param>
        /// <returns></returns>
        internal bool IsCallToMethod(Instruction instruction, out MethodDefinition calledMethod)
        {
            if (instruction.OpCode == OpCodes.Call && instruction.Operand is MethodDefinition method)
            {
                calledMethod = method;
                return true;
            }
            else
            {
                calledMethod = null;
                return false;
            }
        }


        /// <summary>
        /// Returns if a serializer and deserializer exist for typeRef. 
        /// </summary>
        /// <param name="typeRef"></param>
        /// <param name="create">True to create if missing.</param>
        /// <returns></returns>
        internal bool HasSerializerAndDeserializer(TypeReference typeRef, bool create)
        {
            //Can be serialized/deserialized.
            bool hasWriter = CodegenSession.WriterHelper.HasSerializer(typeRef, create);
            bool hasReader = CodegenSession.ReaderHelper.HasDeserializer(typeRef, create);

            return (hasWriter && hasReader);
        }
    }
}