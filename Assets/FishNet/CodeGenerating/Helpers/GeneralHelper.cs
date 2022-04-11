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
using SR = System.Reflection;

namespace FishNet.CodeGenerating.Helping
{
    internal class GeneralHelper
    {
        #region Reflection references.
        internal MethodReference Queue_Enqueue_MethodRef;
        internal MethodReference Queue_get_Count_MethodRef;
        internal MethodReference Queue_Dequeue_MethodRef;
        internal MethodReference Queue_Clear_MethodRef;
        internal TypeReference List_TypeRef;
        internal MethodReference List_Clear_MethodRef;
        internal MethodReference List_get_Item_MethodRef;
        internal MethodReference List_get_Count_MethodRef;
        internal MethodReference List_Add_MethodRef;
        internal MethodReference List_RemoveRange_MethodRef;
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
        internal MethodReference Comparers_IsDefault_MethodRef;
        internal MethodReference IsServer_MethodRef = null;
        internal MethodReference IsClient_MethodRef = null;
        internal MethodReference NetworkObject_Deinitializing_MethodRef = null;
        private Dictionary<Type, TypeReference> _importedTypeReferences = new Dictionary<Type, TypeReference>();
        private Dictionary<FieldDefinition, FieldReference> _importedFieldReferences = new Dictionary<FieldDefinition, FieldReference>();
        private Dictionary<MethodReference, MethodDefinition> _methodReferenceResolves = new Dictionary<MethodReference, MethodDefinition>();
        private Dictionary<TypeReference, TypeDefinition> _typeReferenceResolves = new Dictionary<TypeReference, TypeDefinition>();
        private Dictionary<FieldReference, FieldDefinition> _fieldReferenceResolves = new Dictionary<FieldReference, FieldDefinition>();
        private string NonSerialized_Attribute_FullName;
        private string Single_FullName;
        #endregion

        internal bool ImportReferences()
        {
            Type tmpType;
            SR.MethodInfo tmpMi; 

            NonSerialized_Attribute_FullName = typeof(NonSerializedAttribute).FullName;
            Single_FullName = typeof(float).FullName;

            tmpType = typeof(Queue<>);
            CodegenSession.ImportReference(tmpType);
            tmpMi = tmpType.GetMethod("get_Count");
            Queue_get_Count_MethodRef = CodegenSession.ImportReference(tmpMi);
            foreach (SR.MethodInfo mi in tmpType.GetMethods())
            {

                if (mi.Name == nameof(Queue<int>.Enqueue))
                    Queue_Enqueue_MethodRef = CodegenSession.ImportReference(mi);
                else if (mi.Name == nameof(Queue<int>.Dequeue))
                    Queue_Dequeue_MethodRef = CodegenSession.ImportReference(mi);
                else if (mi.Name == nameof(Queue<int>.Clear))
                    Queue_Clear_MethodRef = CodegenSession.ImportReference(mi);
            }

            Type comparers = typeof(Comparers);
            foreach (SR.MethodInfo mi in comparers.GetMethods())
            {
                if (mi.Name == nameof(Comparers.EqualityCompare))
                    Comparers_EqualityCompare_MethodRef = CodegenSession.ImportReference(mi);
                else if (mi.Name == nameof(Comparers.IsDefault))
                    Comparers_IsDefault_MethodRef = CodegenSession.ImportReference(mi);
            }

            //Networkbehaviour.
            Type networkBehaviourType = typeof(NetworkBehaviour);
            foreach (SR.MethodInfo methodInfo in networkBehaviourType.GetMethods())
            {
                if (methodInfo.Name == nameof(NetworkBehaviour.CanLog))
                    NetworkBehaviour_CanLog_MethodRef = CodegenSession.ImportReference(methodInfo);
            }
            foreach (SR.PropertyInfo propertyInfo in networkBehaviourType.GetProperties())
            {
                if (propertyInfo.Name == nameof(NetworkBehaviour.NetworkManager))
                    NetworkBehaviour_NetworkManager_MethodRef = CodegenSession.ImportReference(propertyInfo.GetMethod);
            }

            //Instancefinder.
            Type instanceFinderType = typeof(InstanceFinder);
            SR.PropertyInfo getNetworkManagerPropertyInfo = instanceFinderType.GetProperty(nameof(InstanceFinder.NetworkManager));
            InstanceFinder_NetworkManager_MethodRef = CodegenSession.ImportReference(getNetworkManagerPropertyInfo.GetMethod);

            //NetworkManager debug logs. 
            Type networkManagerType = typeof(NetworkManager);
            foreach (SR.MethodInfo methodInfo in networkManagerType.GetMethods())
            {
                if (methodInfo.Name == nameof(NetworkManager.Log))
                    NetworkManager_LogCommon_MethodRef = CodegenSession.ImportReference(methodInfo);
                else if (methodInfo.Name == nameof(NetworkManager.LogWarning))
                    NetworkManager_LogWarning_MethodRef = CodegenSession.ImportReference(methodInfo);
                else if (methodInfo.Name == nameof(NetworkManager.LogError))
                    NetworkManager_LogError_MethodRef = CodegenSession.ImportReference(methodInfo);
                else if (methodInfo.Name == nameof(NetworkManager.CanLog))
                    NetworkManager_CanLog_MethodRef = CodegenSession.ImportReference(methodInfo);
            }

            //Lists.
            tmpType = typeof(List<>);
            List_TypeRef = CodegenSession.ImportReference(tmpType);
            SR.MethodInfo lstMi;
            lstMi = tmpType.GetMethod("Add");
            List_Add_MethodRef = CodegenSession.ImportReference(lstMi);
            lstMi = tmpType.GetMethod("RemoveRange");
            List_RemoveRange_MethodRef = CodegenSession.ImportReference(lstMi);
            lstMi = tmpType.GetMethod("get_Count");
            List_get_Count_MethodRef = CodegenSession.ImportReference(lstMi);
            lstMi = tmpType.GetMethod("get_Item");
            List_get_Item_MethodRef = CodegenSession.ImportReference(lstMi);
            lstMi = tmpType.GetMethod("Clear");
            List_Clear_MethodRef = CodegenSession.ImportReference(lstMi);

            //Unity debug logs.
            Type debugType = typeof(UnityEngine.Debug);
            foreach (SR.MethodInfo methodInfo in debugType.GetMethods())
            {
                if (methodInfo.Name == nameof(Debug.LogWarning) && methodInfo.GetParameters().Length == 1)
                    Debug_LogWarning_MethodRef = CodegenSession.ImportReference(methodInfo);
                else if (methodInfo.Name == nameof(Debug.LogError) && methodInfo.GetParameters().Length == 1)
                    Debug_LogError_MethodRef = CodegenSession.ImportReference(methodInfo);
                else if (methodInfo.Name == nameof(Debug.Log) && methodInfo.GetParameters().Length == 1)
                    Debug_LogCommon_MethodRef = CodegenSession.ImportReference(methodInfo);
            }

            Type codegenHelper = typeof(CodegenHelper);
            foreach (SR.MethodInfo methodInfo in codegenHelper.GetMethods())
            {
                if (methodInfo.Name == nameof(CodegenHelper.NetworkObject_Deinitializing))
                    NetworkObject_Deinitializing_MethodRef = CodegenSession.ImportReference(methodInfo);
                else if (methodInfo.Name == nameof(CodegenHelper.IsClient))
                    IsClient_MethodRef = CodegenSession.ImportReference(methodInfo);
                else if (methodInfo.Name == nameof(CodegenHelper.IsServer))
                    IsServer_MethodRef = CodegenSession.ImportReference(methodInfo);
            }

            return true;
        }



#region Resolves.
        /// <summary>
        /// Adds a typeRef to TypeReferenceResolves.
        /// </summary>
        internal void AddTypeReferenceResolve(TypeReference typeRef, TypeDefinition typeDef)
        {
            _typeReferenceResolves[typeRef] = typeDef;
        }

        /// <summary>
        /// Gets a TypeDefinition for typeRef.
        /// </summary>
        internal TypeDefinition GetTypeReferenceResolve(TypeReference typeRef)
        {
            TypeDefinition result;
            if (_typeReferenceResolves.TryGetValue(typeRef, out result))
            {
                return result;
            }
            else
            {
                result = typeRef.Resolve();
                AddTypeReferenceResolve(typeRef, result);
            }

            return result;
        }

        /// <summary>
        /// Adds a methodRef to MethodReferenceResolves.
        /// </summary>
        internal void AddMethodReferenceResolve(MethodReference methodRef, MethodDefinition methodDef)
        {
            _methodReferenceResolves[methodRef] = methodDef;
        }

        /// <summary>
        /// Gets a TypeDefinition for typeRef.
        /// </summary>
        internal MethodDefinition GetMethodReferenceResolve(MethodReference methodRef)
        {
            MethodDefinition result;
            if (_methodReferenceResolves.TryGetValue(methodRef, out result))
            {
                return result;
            }
            else
            {
                result = methodRef.Resolve();
                AddMethodReferenceResolve(methodRef, result);
            }

            return result;
        }


        /// <summary>
        /// Adds a fieldRef to FieldReferenceResolves.
        /// </summary>
        internal void AddFieldReferenceResolve(FieldReference fieldRef, FieldDefinition fieldDef)
        {
            _fieldReferenceResolves[fieldRef] = fieldDef;
        }

        /// <summary>
        /// Gets a FieldDefinition for fieldRef.
        /// </summary>
        internal FieldDefinition GetFieldReferenceResolve(FieldReference fieldRef)
        {
            FieldDefinition result;
            if (_fieldReferenceResolves.TryGetValue(fieldRef, out result))
            {
                return result;
            }
            else
            {
                result = fieldRef.Resolve();
                AddFieldReferenceResolve(fieldRef, result);
            }

            return result;
        }
        #endregion


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
        internal bool IgnoreMethod(SR.MethodInfo methodInfo)
        {
            foreach (SR.CustomAttributeData item in methodInfo.CustomAttributes)
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
            MethodReference constructorMethodRef = CodegenSession.ImportReference(constructorMethodDef);
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
                        TypeReference tr = CodegenSession.ImportReference(t);
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
                    typeAttr, CodegenSession.ImportReference(typeof(object)));
                //Add base class if specified.
                if (baseTypeRef != null)
                    type.BaseType = CodegenSession.ImportReference(baseTypeRef);

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
                result = CodegenSession.ImportReference(type);
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
                result = CodegenSession.ImportReference(fieldDef);
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


            VariableDefinition networkManagerVd = CreateVariable(processor.Body.Method, typeof(NetworkManager));
            //Using InstanceFinder(static).
            if (useStatic)
            {
                //Store instancefinder to nm variable.
                instructions.Add(processor.Create(OpCodes.Call, InstanceFinder_NetworkManager_MethodRef));
                instructions.Add(processor.Create(OpCodes.Stloc, networkManagerVd));
            }
            //Using networkBehaviour.
            else
            {
                //Store nm reference.
                instructions.Add(processor.Create(OpCodes.Ldarg_0));
                instructions.Add(processor.Create(OpCodes.Call, NetworkBehaviour_NetworkManager_MethodRef));
                instructions.Add(processor.Create(OpCodes.Stloc, networkManagerVd));
                //If was set to null then try to log with instancefinder.
                Instruction skipStaticSetInst = processor.Create(OpCodes.Nop);
                //if (nmVd == null) nmVd = InstanceFinder.NetworkManager.
                instructions.Add(processor.Create(OpCodes.Ldloc, networkManagerVd));
                instructions.Add(processor.Create(OpCodes.Brtrue_S, skipStaticSetInst));
                //Store instancefinder to nm variable.
                instructions.Add(processor.Create(OpCodes.Call, InstanceFinder_NetworkManager_MethodRef));
                instructions.Add(processor.Create(OpCodes.Stloc, networkManagerVd));
                instructions.Add(skipStaticSetInst);
            }

            Instruction skipDebugInst = processor.Create(OpCodes.Nop);
            //null check nm reference. If null then skip logging.
            instructions.Add(processor.Create(OpCodes.Ldloc, networkManagerVd));
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

            instructions.Add(processor.Create(OpCodes.Ldloc, networkManagerVd));
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
            {
                CodegenSession.LogError($"CreateDebug called with LoggingType.Off.");
                return instructions;
            }

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
            //Make sure it's imported into current module.
            typeRef = CodegenSession.ImportReference(typeRef);
            //Can be serialized/deserialized.
            bool hasWriter = CodegenSession.WriterHelper.HasSerializer(typeRef, create);
            bool hasReader = CodegenSession.ReaderHelper.HasDeserializer(typeRef, create);

            return (hasWriter && hasReader);
        }

    }
}