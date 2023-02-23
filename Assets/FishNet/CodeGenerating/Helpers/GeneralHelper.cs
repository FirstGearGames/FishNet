﻿using FishNet.CodeGenerating.Extension;
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
using MonoFN.Cecil.Rocks;
using System;
using System.Collections.Generic;
using UnityEngine;
using SR = System.Reflection;

namespace FishNet.CodeGenerating.Helping
{
    internal class GeneralHelper : CodegenBase
    {
        #region Reflection references.
        public string CodegenExcludeAttribute_FullName;
        public string CodegenIncludeAttribute_FullName;
        public MethodReference Extension_Attribute_Ctor_MethodRef;
        public MethodReference Queue_Enqueue_MethodRef;
        public MethodReference Queue_get_Count_MethodRef;
        public MethodReference Queue_Dequeue_MethodRef;
        public MethodReference Queue_Clear_MethodRef;
        public TypeReference List_TypeRef;
        public MethodReference List_Clear_MethodRef;
        public MethodReference List_get_Item_MethodRef;
        public MethodReference List_get_Count_MethodRef;
        public MethodReference List_Add_MethodRef;
        public MethodReference List_RemoveRange_MethodRef;
        public MethodReference InstanceFinder_NetworkManager_MethodRef;
        public MethodReference NetworkBehaviour_CanLog_MethodRef;
        public MethodReference NetworkBehaviour_NetworkManager_MethodRef;
        public MethodReference NetworkManager_LogCommon_MethodRef;
        public MethodReference NetworkManager_LogWarning_MethodRef;
        public MethodReference NetworkManager_LogError_MethodRef;
        public MethodReference Debug_LogCommon_MethodRef;
        public MethodReference Debug_LogWarning_MethodRef;
        public MethodReference Debug_LogError_MethodRef;
        public MethodReference IsServer_MethodRef;
        public MethodReference IsClient_MethodRef;
        public MethodReference NetworkObject_Deinitializing_MethodRef;
        public MethodReference Application_IsPlaying_MethodRef;
        public string NonSerialized_Attribute_FullName;
        public string Single_FullName;
        public TypeReference FunctionT2TypeRef;
        public TypeReference FunctionT3TypeRef;
        public MethodReference FunctionT2ConstructorMethodRef;
        public MethodReference FunctionT3ConstructorMethodRef;
        //GeneratedComparer
        public MethodReference GeneratedComparer_Compare_Set_MethodRef;
        public MethodReference GeneratedComparer_IsDefault_Set_MethodRef;
        public TypeReference GeneratedComparer_TypeRef;
        public TypeDefinition GeneratedComparer_ClassTypeDef;
        public MethodDefinition GeneratedComparer_OnLoadMethodDef;
        public TypeReference IEquatable_TypeRef;
        //Actions.
        public TypeReference ActionT2TypeRef;
        public TypeReference ActionT3TypeRef;
        public MethodReference ActionT2ConstructorMethodRef;
        public MethodReference ActionT3ConstructorMethodRef;

        private Dictionary<Type, TypeReference> _importedTypeReferences = new Dictionary<Type, TypeReference>();
        private Dictionary<FieldDefinition, FieldReference> _importedFieldReferences = new Dictionary<FieldDefinition, FieldReference>();
        private Dictionary<MethodReference, MethodDefinition> _methodReferenceResolves = new Dictionary<MethodReference, MethodDefinition>();
        private Dictionary<TypeReference, TypeDefinition> _typeReferenceResolves = new Dictionary<TypeReference, TypeDefinition>();
        private Dictionary<FieldReference, FieldDefinition> _fieldReferenceResolves = new Dictionary<FieldReference, FieldDefinition>();
        #endregion

        #region Const.
        public const string UNITYENGINE_ASSEMBLY_PREFIX = "UnityEngine.";
        #endregion

        public override bool ImportReferences()
        {
            Type tmpType;
            TypeReference tmpTr;
            SR.MethodInfo tmpMi;
            SR.PropertyInfo tmpPi;

            NonSerialized_Attribute_FullName = typeof(NonSerializedAttribute).FullName;
            Single_FullName = typeof(float).FullName;

            ActionT2TypeRef = base.ImportReference(typeof(Action<,>));
            ActionT3TypeRef = base.ImportReference(typeof(Action<,,>));
            ActionT2ConstructorMethodRef = base.ImportReference(typeof(Action<,>).GetConstructors()[0]);
            ActionT3ConstructorMethodRef = base.ImportReference(typeof(Action<,,>).GetConstructors()[0]);

            CodegenExcludeAttribute_FullName = typeof(CodegenExcludeAttribute).FullName;
            CodegenIncludeAttribute_FullName = typeof(CodegenIncludeAttribute).FullName;

            tmpType = typeof(Queue<>);
            base.ImportReference(tmpType);
            tmpMi = tmpType.GetMethod("get_Count");
            Queue_get_Count_MethodRef = base.ImportReference(tmpMi);
            foreach (SR.MethodInfo mi in tmpType.GetMethods())
            {
                if (mi.Name == nameof(Queue<int>.Enqueue))
                    Queue_Enqueue_MethodRef = base.ImportReference(mi);
                else if (mi.Name == nameof(Queue<int>.Dequeue))
                    Queue_Dequeue_MethodRef = base.ImportReference(mi);
                else if (mi.Name == nameof(Queue<int>.Clear))
                    Queue_Clear_MethodRef = base.ImportReference(mi);
            }

            /* MISC */
            //
            tmpType = typeof(UnityEngine.Application);
            tmpPi = tmpType.GetProperty(nameof(UnityEngine.Application.isPlaying));
            if (tmpPi != null)
                Application_IsPlaying_MethodRef = base.ImportReference(tmpPi.GetMethod);
            //
            tmpType = typeof(System.Runtime.CompilerServices.ExtensionAttribute);
            tmpTr = base.ImportReference(tmpType);
            Extension_Attribute_Ctor_MethodRef = base.ImportReference(tmpTr.GetConstructor(base.Session));

            //Networkbehaviour.
            Type networkBehaviourType = typeof(NetworkBehaviour);
            foreach (SR.MethodInfo methodInfo in networkBehaviourType.GetMethods())
            {
                if (methodInfo.Name == nameof(NetworkBehaviour.CanLog))
                    NetworkBehaviour_CanLog_MethodRef = base.ImportReference(methodInfo);
            }
            foreach (SR.PropertyInfo propertyInfo in networkBehaviourType.GetProperties())
            {
                if (propertyInfo.Name == nameof(NetworkBehaviour.NetworkManager))
                    NetworkBehaviour_NetworkManager_MethodRef = base.ImportReference(propertyInfo.GetMethod);
            }

            //Instancefinder.
            Type instanceFinderType = typeof(InstanceFinder);
            SR.PropertyInfo getNetworkManagerPropertyInfo = instanceFinderType.GetProperty(nameof(InstanceFinder.NetworkManager));
            InstanceFinder_NetworkManager_MethodRef = base.ImportReference(getNetworkManagerPropertyInfo.GetMethod);

            //NetworkManager debug logs. 
            Type networkManagerType = typeof(NetworkManager);
            foreach (SR.MethodInfo methodInfo in networkManagerType.GetMethods())
            {
                if (methodInfo.Name == nameof(NetworkManager.Log) && methodInfo.GetParameters().Length == 1)
                    NetworkManager_LogCommon_MethodRef = base.ImportReference(methodInfo);
                else if (methodInfo.Name == nameof(NetworkManager.LogWarning))
                    NetworkManager_LogWarning_MethodRef = base.ImportReference(methodInfo);
                else if (methodInfo.Name == nameof(NetworkManager.LogError))
                    NetworkManager_LogError_MethodRef = base.ImportReference(methodInfo);
            }

            //Lists.
            tmpType = typeof(List<>);
            List_TypeRef = base.ImportReference(tmpType);
            SR.MethodInfo lstMi;
            lstMi = tmpType.GetMethod("Add");
            List_Add_MethodRef = base.ImportReference(lstMi);
            lstMi = tmpType.GetMethod("RemoveRange");
            List_RemoveRange_MethodRef = base.ImportReference(lstMi);
            lstMi = tmpType.GetMethod("get_Count");
            List_get_Count_MethodRef = base.ImportReference(lstMi);
            lstMi = tmpType.GetMethod("get_Item");
            List_get_Item_MethodRef = base.ImportReference(lstMi);
            lstMi = tmpType.GetMethod("Clear");
            List_Clear_MethodRef = base.ImportReference(lstMi);

            //Unity debug logs.
            Type debugType = typeof(UnityEngine.Debug);
            foreach (SR.MethodInfo methodInfo in debugType.GetMethods())
            {
                if (methodInfo.Name == nameof(Debug.LogWarning) && methodInfo.GetParameters().Length == 1)
                    Debug_LogWarning_MethodRef = base.ImportReference(methodInfo);
                else if (methodInfo.Name == nameof(Debug.LogError) && methodInfo.GetParameters().Length == 1)
                    Debug_LogError_MethodRef = base.ImportReference(methodInfo);
                else if (methodInfo.Name == nameof(Debug.Log) && methodInfo.GetParameters().Length == 1)
                    Debug_LogCommon_MethodRef = base.ImportReference(methodInfo);
            }

            Type codegenHelper = typeof(CodegenHelper);
            foreach (SR.MethodInfo methodInfo in codegenHelper.GetMethods())
            {
                if (methodInfo.Name == nameof(CodegenHelper.NetworkObject_Deinitializing))
                    NetworkObject_Deinitializing_MethodRef = base.ImportReference(methodInfo);
                else if (methodInfo.Name == nameof(CodegenHelper.IsClient))
                    IsClient_MethodRef = base.ImportReference(methodInfo);
                else if (methodInfo.Name == nameof(CodegenHelper.IsServer))
                    IsServer_MethodRef = base.ImportReference(methodInfo);
            }

            //Generic functions.
            FunctionT2TypeRef = base.ImportReference(typeof(Func<,>));
            FunctionT3TypeRef = base.ImportReference(typeof(Func<,,>));
            FunctionT2ConstructorMethodRef = base.ImportReference(typeof(Func<,>).GetConstructors()[0]);
            FunctionT3ConstructorMethodRef = base.ImportReference(typeof(Func<,,>).GetConstructors()[0]);

            GeneratedComparers();

            //Sets up for generated comparers.
            void GeneratedComparers()
            {
                GeneralHelper gh = base.GetClass<GeneralHelper>();
                GeneratedComparer_ClassTypeDef = gh.GetOrCreateClass(out _, WriterProcessor.GENERATED_TYPE_ATTRIBUTES, "GeneratedComparers___Internal", null, WriterProcessor.GENERATED_WRITER_NAMESPACE);
                bool created;
                GeneratedComparer_OnLoadMethodDef = gh.GetOrCreateMethod(GeneratedComparer_ClassTypeDef, out created, WriterProcessor.INITIALIZEONCE_METHOD_ATTRIBUTES, WriterProcessor.INITIALIZEONCE_METHOD_NAME, base.Module.TypeSystem.Void);
                if (created)
                {
                    gh.CreateRuntimeInitializeOnLoadMethodAttribute(GeneratedComparer_OnLoadMethodDef);
                    GeneratedComparer_OnLoadMethodDef.Body.GetILProcessor().Emit(OpCodes.Ret);
                }

                System.Type repComparerType = typeof(GeneratedComparer<>);
                GeneratedComparer_TypeRef = base.ImportReference(repComparerType);
                System.Reflection.PropertyInfo pi;
                pi = repComparerType.GetProperty(nameof(GeneratedComparer<int>.Compare));
                GeneratedComparer_Compare_Set_MethodRef = base.ImportReference(pi.GetSetMethod());
                pi = repComparerType.GetProperty(nameof(GeneratedComparer<int>.IsDefault));
                GeneratedComparer_IsDefault_Set_MethodRef = base.ImportReference(pi.GetSetMethod());

                System.Type iEquatableType = typeof(IEquatable<>);
                IEquatable_TypeRef = base.ImportReference(iEquatableType);
            }

            return true;
        }



        #region Resolves.
        /// <summary>
        /// Adds a typeRef to TypeReferenceResolves.
        /// </summary>
        public void AddTypeReferenceResolve(TypeReference typeRef, TypeDefinition typeDef)
        {
            _typeReferenceResolves[typeRef] = typeDef;
        }

        /// <summary>
        /// Gets a TypeDefinition for typeRef.
        /// </summary>
        public TypeDefinition GetTypeReferenceResolve(TypeReference typeRef)
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
        public void AddMethodReferenceResolve(MethodReference methodRef, MethodDefinition methodDef)
        {
            _methodReferenceResolves[methodRef] = methodDef;
        }

        /// <summary>
        /// Gets a TypeDefinition for typeRef.
        /// </summary>
        public MethodDefinition GetMethodReferenceResolve(MethodReference methodRef)
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
        public void AddFieldReferenceResolve(FieldReference fieldRef, FieldDefinition fieldDef)
        {
            _fieldReferenceResolves[fieldRef] = fieldDef;
        }

        /// <summary>
        /// Gets a FieldDefinition for fieldRef.
        /// </summary>
        public FieldDefinition GetFieldReferenceResolve(FieldReference fieldRef)
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
        /// Makes a method an extension method.
        /// </summary>
        public void MakeExtensionMethod(MethodDefinition md)
        {
            if (md.Parameters.Count == 0)
            {
                base.LogError($"Method {md.FullName} cannot be made an extension method because it has no parameters.");
                return;
            }

            md.Attributes |= (MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.HideBySig);
            CustomAttribute ca = new CustomAttribute(Extension_Attribute_Ctor_MethodRef);
            md.CustomAttributes.Add(ca);
        }

        /// <summary>
        /// Removes characters which would create invalid comparisons when trying to compare generics.
        /// </summary>
        public string RemoveGen11ericBrackets(string str)
        { 
            /* Fix example...
             * List`1<T> converts to...
             *  List`1.
             * System.Nullable`1<System.Int> converts to...
             *  System.Nullable`1System.Int */
            if (str.Contains(typeof(System.Nullable).FullName))
                return str;

            //Find bracket areas to remove.
            int startIndex = str.IndexOf("<");
            int endIndex = str.IndexOf(">");
            //If found.
            if (startIndex >= 0 && endIndex >= 0)
            {
                string result = str.Substring(0, startIndex);
                result += str.Substring(endIndex + 1);
                return result;
            }
            else
            {
                return str;
            }
        }

        /// <summary>
        /// Returns if typeDef should be ignored.
        /// </summary>
        /// <param name="typeDef"></param>
        /// <returns></returns>
        public bool IgnoreTypeDefinition(TypeDefinition typeDef)
        {
            foreach (CustomAttribute item in typeDef.CustomAttributes)
            {
                if (item.AttributeType.FullName == typeof(CodegenExcludeAttribute).FullName)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Returns if type uses CodegenExcludeAttribute.
        /// </summary>
        public bool CodegenExclude(SR.MethodInfo methodInfo)
        {
            foreach (SR.CustomAttributeData item in methodInfo.CustomAttributes)
            {
                if (item.AttributeType == typeof(CodegenExcludeAttribute))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Returns if type uses CodegenExcludeAttribute.
        /// </summary>
        public bool CodegenExclude(MethodDefinition methodDef)
        {
            foreach (CustomAttribute item in methodDef.CustomAttributes)
            {
                if (item.AttributeType.FullName == CodegenExcludeAttribute_FullName)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Returns if type uses CodegenExcludeAttribute.
        /// </summary>
        public bool CodegenExclude(FieldDefinition fieldDef)
        {
            foreach (CustomAttribute item in fieldDef.CustomAttributes)
            {
                if (item.AttributeType.FullName == CodegenExcludeAttribute_FullName)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Returns if type uses CodegenIncludeAttribute.
        /// </summary>
        public bool CodegenInclude(FieldDefinition fieldDef)
        {
            foreach (CustomAttribute item in fieldDef.CustomAttributes)
            {
                if (item.AttributeType.FullName == CodegenIncludeAttribute_FullName)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Returns if type uses CodegenExcludeAttribute.
        /// </summary>
        public bool CodegenExclude(PropertyDefinition propDef)
        {
            foreach (CustomAttribute item in propDef.CustomAttributes)
            {
                if (item.AttributeType.FullName == CodegenExcludeAttribute_FullName)
                    return true;
            }

            return false;
        }


        /// <summary>
        /// Returns if type uses CodegenExcludeAttribute.
        /// </summary>
        public bool CodegenInclude(PropertyDefinition propDef)
        {
            foreach (CustomAttribute item in propDef.CustomAttributes)
            {
                if (item.AttributeType.FullName == CodegenIncludeAttribute_FullName)
                    return true;
            }

            return false;
        }




        /// <summary>
        /// Calls copiedMd with the assumption md shares the same parameters.
        /// </summary>
        public void CallCopiedMethod(MethodDefinition md, MethodDefinition copiedMd)
        {
            ILProcessor processor = md.Body.GetILProcessor();
            processor.Emit(OpCodes.Ldarg_0);
            foreach (var item in copiedMd.Parameters)
                processor.Emit(OpCodes.Ldarg, item);

            MethodReference mr = copiedMd.GetMethodReference(base.Session);
            processor.Emit(OpCodes.Call, mr);

        }

        /// <summary>
        /// Removes countVd from list of dataFd starting at index 0.
        /// </summary>
        public List<Instruction> ListRemoveRange(MethodDefinition methodDef, FieldDefinition dataFd, TypeReference dataTr, VariableDefinition countVd)
        {
            /* Remove entries which exceed maximum buffer. */
            //Method references for uint/data list:
            //get_count, RemoveRange. */
            GenericInstanceType dataListGit;
            GetGenericLists(dataTr, out dataListGit);
            MethodReference lstDataRemoveRangeMr = base.GetClass<GeneralHelper>().List_RemoveRange_MethodRef.MakeHostInstanceGeneric(base.Session, dataListGit);

            List<Instruction> insts = new List<Instruction>();
            ILProcessor processor = methodDef.Body.GetILProcessor();

            //Index 1 is the uint, 0 is the data.
            insts.Add(processor.Create(OpCodes.Ldarg_0));//this.
            insts.Add(processor.Create(OpCodes.Ldfld, dataFd));
            insts.Add(processor.Create(OpCodes.Ldc_I4_0));
            insts.Add(processor.Create(OpCodes.Ldloc, countVd));
            insts.Add(processor.Create(lstDataRemoveRangeMr.GetCallOpCode(base.Session), lstDataRemoveRangeMr));

            return insts;
        }
        /// <summary>
        /// Outputs generic lists for dataTr and uint.
        /// </summary>
        public void GetGenericLists(TypeReference dataTr, out GenericInstanceType lstData)
        {
            TypeReference listDataTr = base.ImportReference(typeof(List<>));
            lstData = listDataTr.MakeGenericInstanceType(new TypeReference[] { dataTr });
        }
        /// <summary>
        /// Outputs generic lists for dataTr and uint.
        /// </summary>
        public void GetGenericQueues(TypeReference dataTr, out GenericInstanceType queueData)
        {
            TypeReference queueDataTr = base.ImportReference(typeof(Queue<>));
            queueData = queueDataTr.MakeGenericInstanceType(new TypeReference[] { dataTr });
        }

        /// <summary>
        /// Copies one method to another while transferring diagnostic paths.
        /// </summary>
        public MethodDefinition CopyIntoNewMethod(MethodDefinition originalMd, string toMethodName, out bool alreadyCreated)
        {
            TypeDefinition typeDef = originalMd.DeclaringType;

            MethodDefinition md = typeDef.GetOrCreateMethodDefinition(base.Session, toMethodName, originalMd, true, out bool created);
            alreadyCreated = !created;
            if (alreadyCreated)
                return md;

            (md.Body, originalMd.Body) = (originalMd.Body, md.Body);
            //Move over all the debugging information
            foreach (SequencePoint sequencePoint in originalMd.DebugInformation.SequencePoints)
                md.DebugInformation.SequencePoints.Add(sequencePoint);
            originalMd.DebugInformation.SequencePoints.Clear();

            foreach (CustomDebugInformation customInfo in originalMd.CustomDebugInformations)
                md.CustomDebugInformations.Add(customInfo);
            originalMd.CustomDebugInformations.Clear();
            //Swap debuginformation scope.
            (originalMd.DebugInformation.Scope, md.DebugInformation.Scope) = (md.DebugInformation.Scope, originalMd.DebugInformation.Scope);

            return md;
        }

        /// <summary>
        /// Creates the RuntimeInitializeOnLoadMethod attribute for a method.
        /// </summary>
        public void CreateRuntimeInitializeOnLoadMethodAttribute(MethodDefinition methodDef, string loadType = "")
        {
            TypeReference attTypeRef = GetTypeReference(typeof(RuntimeInitializeOnLoadMethodAttribute));
            foreach (CustomAttribute item in methodDef.CustomAttributes)
            {
                //Already exist.
                if (item.AttributeType.FullName == attTypeRef.FullName)
                    return;
            }

            int parameterRequirement = (loadType.Length == 0) ? 0 : 1;
            MethodDefinition constructorMethodDef = attTypeRef.GetConstructor(base.Session, parameterRequirement);
            MethodReference constructorMethodRef = base.ImportReference(constructorMethodDef);
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
                        TypeReference tr = base.ImportReference(t);
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
        public AutoPackType GetDefaultAutoPackType(TypeReference typeRef)
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
        public MethodDefinition GetOrCreateMethod(TypeDefinition typeDef, out bool created, MethodAttributes methodAttr, string methodName, TypeReference returnType)
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
        public TypeDefinition GetOrCreateClass(out bool created, TypeAttributes typeAttr, string className, TypeReference baseTypeRef, string namespaceName = WriterProcessor.GENERATED_WRITER_NAMESPACE)
        {
            if (namespaceName.Length == 0)
                namespaceName = FishNetILPP.RUNTIME_ASSEMBLY_NAME;

            TypeDefinition type = base.Module.GetClass(className, namespaceName);
            if (type != null)
            {
                created = false;
                return type;
            }
            else
            {
                created = true;
                type = new TypeDefinition(namespaceName, className,
                    typeAttr, base.ImportReference(typeof(object)));
                //Add base class if specified.
                if (baseTypeRef != null)
                    type.BaseType = base.ImportReference(baseTypeRef);

                base.Module.Types.Add(type);
                return type;
            }
        }

        #region HasNonSerializableAttribute
        /// <summary>
        /// Returns if fieldDef has a NonSerialized attribute.
        /// </summary>
        /// <param name="fieldDef"></param>
        /// <returns></returns>
        public bool HasNonSerializableAttribute(FieldDefinition fieldDef)
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
        public bool HasNonSerializableAttribute(TypeDefinition typeDef)
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
        public TypeReference GetTypeReference(Type type)
        {
            TypeReference result;
            if (!_importedTypeReferences.TryGetValue(type, out result))
            {
                result = base.ImportReference(type);
                _importedTypeReferences.Add(type, result);
            }

            return result;
        }

        /// <summary>
        /// Gets a FieldReference for a type.
        /// </summary>
        /// <param name="type"></param>
        public FieldReference GetFieldReference(FieldDefinition fieldDef)
        {
            FieldReference result;
            if (!_importedFieldReferences.TryGetValue(fieldDef, out result))
            {
                result = base.ImportReference(fieldDef);
                _importedFieldReferences.Add(fieldDef, result);
            }

            return result;
        }

        /// <summary>
        /// Gets the current constructor for typeDef, or makes a new one if constructor doesn't exist.
        /// </summary>
        /// <param name="typeDef"></param>
        /// <returns></returns>
        public MethodDefinition GetOrCreateConstructor(TypeDefinition typeDef, out bool created, bool makeStatic)
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
        public void CreateRetBoolean(ILProcessor processor, bool result)
        {
            OpCode code = (result) ? OpCodes.Ldc_I4_1 : OpCodes.Ldc_I4_0;
            processor.Emit(code);
            processor.Emit(OpCodes.Ret);
        }

        #region Debug logging.
        /// <summary>
        /// Creates instructions to log using a NetworkManager or Unity logging.
        /// </summary>
        /// <param name="preferNetworkManager">NetworkManager will be used to log first. If the NetworkManager is unavailable Unity logging will be used.</param>
        public List<Instruction> LogMessage(MethodDefinition md, string message, LoggingType loggingType)
        {
            ILProcessor processor = md.Body.GetILProcessor();
            List<Instruction> instructions = new List<Instruction>();
            if (loggingType == LoggingType.Off)
            {
                base.LogError($"LogMessage called with LoggingType.Off.");
                return instructions;
            }

            /* Try to store NetworkManager from base to a variable.
             * If the base does not exist, such as not inheriting from NetworkBehaviour,
             * or if null because the object is not initialized, then use InstanceFinder to
             * retrieve the NetworkManager. Then if NetworkManager was found, perform the log. */
            VariableDefinition networkManagerVd = CreateVariable(processor.Body.Method, typeof(NetworkManager));

            bool useStatic = (md.IsStatic || !md.DeclaringType.InheritsFrom<NetworkBehaviour>(base.Session));
            //If does not inherit NB then use InstanceFinder.
            if (useStatic)
            {
                SetNetworkManagerFromInstanceFinder();
            }
            //Inherits NB, load from base.NetworkManager.
            else
            {
                instructions.Add(processor.Create(OpCodes.Ldarg_0));
                instructions.Add(processor.Create(OpCodes.Call, NetworkBehaviour_NetworkManager_MethodRef));
                instructions.Add(processor.Create(OpCodes.Stloc, networkManagerVd));

                //If null from NB then use instancefinder.
                Instruction skipSetFromInstanceFinderInst = processor.Create(OpCodes.Nop);
                //if (nmVd == null) nmVd = InstanceFinder.NetworkManager.
                instructions.Add(processor.Create(OpCodes.Ldloc, networkManagerVd));
                instructions.Add(processor.Create(OpCodes.Brtrue_S, skipSetFromInstanceFinderInst));
                SetNetworkManagerFromInstanceFinder();
                instructions.Add(skipSetFromInstanceFinderInst);
            }

            //Sets NetworkManager variable from instancefinder.
            void SetNetworkManagerFromInstanceFinder()
            {
                instructions.Add(processor.Create(OpCodes.Call, InstanceFinder_NetworkManager_MethodRef));
                instructions.Add(processor.Create(OpCodes.Stloc, networkManagerVd));
            }

            VariableDefinition networkManagerIsNullVd = CreateVariable(md, typeof(bool));
            //bool networkManagerIsNull = (networkManager == null);
            instructions.Add(processor.Create(OpCodes.Ldloc, networkManagerVd));
            instructions.Add(processor.Create(OpCodes.Ldnull));
            instructions.Add(processor.Create(OpCodes.Ceq));
            instructions.Add(processor.Create(OpCodes.Stloc, networkManagerIsNullVd));

            /* If (networkManagerIsNull)
             *      networkManager.Log...
             * else
             *      UnityEngine.Debug.Log... */
            Instruction afterNetworkManagerLogInst = processor.Create(OpCodes.Nop);
            Instruction afterUnityLogInst = processor.Create(OpCodes.Nop);
            instructions.Add(processor.Create(OpCodes.Ldloc, networkManagerIsNullVd));
            instructions.Add(processor.Create(OpCodes.Brtrue, afterNetworkManagerLogInst));
            instructions.AddRange(LogNetworkManagerMessage(md, networkManagerVd, message, loggingType));
            instructions.Add(processor.Create(OpCodes.Br, afterUnityLogInst));
            instructions.Add(afterNetworkManagerLogInst);
            instructions.AddRange(LogUnityDebugMessage(md, message, loggingType));
            instructions.Add(afterUnityLogInst);

            return instructions;
        }

        /// <summary>
        /// Creates instructions to log using NetworkManager without error checking.
        /// </summary>
        public List<Instruction> LogNetworkManagerMessage(MethodDefinition md, VariableDefinition networkManagerVd, string message, LoggingType loggingType)
        {
            List<Instruction> instructions = new List<Instruction>();
            if (!CanUseLogging(loggingType))
                return instructions;

            ILProcessor processor = md.Body.GetILProcessor();

            MethodReference methodRef;
            if (loggingType == LoggingType.Common)
                methodRef = NetworkManager_LogCommon_MethodRef;
            else if (loggingType == LoggingType.Warning)
                methodRef = NetworkManager_LogWarning_MethodRef;
            else
                methodRef = NetworkManager_LogError_MethodRef;

            instructions.Add(processor.Create(OpCodes.Ldloc, networkManagerVd));
            instructions.Add(processor.Create(OpCodes.Ldstr, message));
            instructions.Add(processor.Create(OpCodes.Call, methodRef));

            return instructions;
        }

        /// <summary>
        /// Creates instructions to log using Unity logging.
        /// </summary>
        public List<Instruction> LogUnityDebugMessage(MethodDefinition md, string message, LoggingType loggingType)
        {
            List<Instruction> instructions = new List<Instruction>();
            if (!CanUseLogging(loggingType))
                return instructions;

            ILProcessor processor = md.Body.GetILProcessor();

            MethodReference methodRef;
            if (loggingType == LoggingType.Common)
                methodRef = Debug_LogCommon_MethodRef;
            else if (loggingType == LoggingType.Warning)
                methodRef = Debug_LogWarning_MethodRef;
            else
                methodRef = Debug_LogError_MethodRef;

            instructions.Add(processor.Create(OpCodes.Ldstr, message));
            instructions.Add(processor.Create(OpCodes.Call, methodRef));
            return instructions;
        }

        /// <summary>
        /// Returns if logging can be done using a LoggingType.
        /// </summary>
        public bool CanUseLogging(LoggingType lt)
        {
            if (lt == LoggingType.Off)
            {
                base.LogError($"Log attempt called with LoggingType.Off.");
                return false;
            }

            return true;
        }
        #endregion

        #region CreateVariable / CreateParameter.
        /// <summary>
        /// Creates a parameter within methodDef and returns it's ParameterDefinition.
        /// </summary>
        /// <param name="methodDef"></param>
        /// <param name="parameterTypeRef"></param>
        /// <returns></returns>
        public ParameterDefinition CreateParameter(MethodDefinition methodDef, TypeDefinition parameterTypeDef, string name = "", ParameterAttributes attributes = ParameterAttributes.None, int index = -1)
        {
            TypeReference typeRef = methodDef.Module.ImportReference(parameterTypeDef);
            return CreateParameter(methodDef, typeRef, name, attributes, index);
        }

        /// <summary>
        /// Creates a parameter within methodDef as the next index, with the same data as passed in parameter definition.
        /// </summary>
        public ParameterDefinition CreateParameter(MethodDefinition methodDef, ParameterDefinition parameterTypeDef)
        {
            base.ImportReference(parameterTypeDef.ParameterType);

            int currentCount = methodDef.Parameters.Count;
            string name = (parameterTypeDef.Name + currentCount);
            ParameterDefinition parameterDef = new ParameterDefinition(name, parameterTypeDef.Attributes, parameterTypeDef.ParameterType);
                methodDef.Parameters.Add(parameterDef);

            return parameterDef;
        }

        /// <summary>
        /// Creates a parameter within methodDef and returns it's ParameterDefinition.
        /// </summary>
        /// <param name="methodDef"></param>
        /// <param name="parameterTypeRef"></param>
        /// <returns></returns>
        public ParameterDefinition CreateParameter(MethodDefinition methodDef, TypeReference parameterTypeRef, string name = "", ParameterAttributes attributes = ParameterAttributes.None, int index = -1)
        {
            int currentCount = methodDef.Parameters.Count;
            if (string.IsNullOrEmpty(name))
                name = (parameterTypeRef.Name + currentCount);
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
        public ParameterDefinition CreateParameter(MethodDefinition methodDef, Type parameterType, string name = "", ParameterAttributes attributes = ParameterAttributes.None, int index = -1)
        {
            return CreateParameter(methodDef, GetTypeReference(parameterType), name, attributes, index);
        }
        /// <summary>
        /// Creates a variable type within the body and returns it's VariableDef.
        /// </summary>
        /// <param name="methodDef"></param>
        /// <param name="variableTypeRef"></param>
        /// <returns></returns>
        public VariableDefinition CreateVariable(MethodDefinition methodDef, TypeReference variableTypeRef)
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
        public VariableDefinition CreateVariable(MethodDefinition methodDef, Type variableType)
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
        public void SetVariableDefinitionFromObject(ILProcessor processor, VariableDefinition variableDef, TypeDefinition typeDef)
        {
            TypeReference type = variableDef.VariableType;
            if (type.IsValueType)
            {
                // structs are created with Initobj
                processor.Emit(OpCodes.Ldloca, variableDef);
                processor.Emit(OpCodes.Initobj, type);
            }
            else if (typeDef.InheritsFrom<UnityEngine.ScriptableObject>(base.Session))
            {
                MethodReference soCreateInstanceMr = processor.Body.Method.Module.ImportReference(() => UnityEngine.ScriptableObject.CreateInstance<UnityEngine.ScriptableObject>());
                GenericInstanceMethod genericInstanceMethod = soCreateInstanceMr.GetElementMethod().MakeGenericMethod(new TypeReference[] { type });
                processor.Emit(OpCodes.Call, genericInstanceMethod);
                processor.Emit(OpCodes.Stloc, variableDef);
            }
            else
            {
                MethodDefinition constructorMethodDef = type.GetConstructor(base.Session);
                if (constructorMethodDef == null)
                {
                    base.LogError($"{type.Name} can't be deserialized because a default constructor could not be found. Create a default constructor or a custom serializer/deserializer.");
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
        public void SetVariableDefinitionFromInt(ILProcessor processor, VariableDefinition variableDef, int value)
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
        public void SetVariableDefinitionFromParameter(ILProcessor processor, VariableDefinition variableDef, ParameterDefinition value)
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
        public bool IsCallToMethod(Instruction instruction, out MethodDefinition calledMethod)
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
        public bool HasSerializerAndDeserializer(TypeReference typeRef, bool create)
        {
            //Make sure it's imported into current module.
            typeRef = base.ImportReference(typeRef);
            //Can be serialized/deserialized.
            bool hasWriter = base.GetClass<WriterProcessor>().HasSerializer(typeRef, create);
            bool hasReader = base.GetClass<ReaderProcessor>().HasDeserializer(typeRef, create);

            return (hasWriter && hasReader);
        }

        /// <summary>
        /// Creates a return of default value for methodDef.
        /// </summary>
        /// <returns></returns>
        public List<Instruction> CreateRetDefault(MethodDefinition methodDef, ModuleDefinition importReturnModule = null)
        {
            ILProcessor processor = methodDef.Body.GetILProcessor();
            List<Instruction> instructions = new List<Instruction>();
            //If requires a value return.
            if (methodDef.ReturnType != methodDef.Module.TypeSystem.Void)
            {
                //Import type first.
                methodDef.Module.ImportReference(methodDef.ReturnType);
                if (importReturnModule != null)
                    importReturnModule.ImportReference(methodDef.ReturnType);
                VariableDefinition vd = base.GetClass<GeneralHelper>().CreateVariable(methodDef, methodDef.ReturnType);
                instructions.Add(processor.Create(OpCodes.Ldloca_S, vd));
                instructions.Add(processor.Create(OpCodes.Initobj, vd.VariableType));
                instructions.Add(processor.Create(OpCodes.Ldloc, vd));
            }
            instructions.Add(processor.Create(OpCodes.Ret));

            return instructions;
        }

        #region GeneratedComparers


        /// <summary>
        /// Creates an equality comparer for dataTr.
        /// </summary>
        public MethodDefinition CreateEqualityComparer(TypeReference dataTr)
        {
            GeneralHelper gh = base.GetClass<GeneralHelper>();
            MethodDefinition comparerMd = gh.GetOrCreateMethod(GeneratedComparer_ClassTypeDef, out bool created, WriterProcessor.GENERATED_METHOD_ATTRIBUTES,
                $"Comparer___{dataTr.FullName}", base.Module.TypeSystem.Boolean);
            //Already done. This can happen if the same replicate data is used in multiple places.
            if (created)
            {
                CreateComparerMethod();
                CreateComparerDelegate();
            }

            return comparerMd;

            void CreateComparerMethod()
            {

                //GeneratedComparer_ClassTypeDef.Methods.Add(comparerMd);

                //Add parameters.
                ParameterDefinition v0Pd = gh.CreateParameter(comparerMd, dataTr, "value0");
                ParameterDefinition v1Pd = gh.CreateParameter(comparerMd, dataTr, "value1");
                ILProcessor processor = comparerMd.Body.GetILProcessor();
                comparerMd.Body.InitLocals = true;

                Instruction exitMethodInst = processor.Create(OpCodes.Ldc_I4_0);

                //Fields.
                foreach (FieldDefinition fieldDef in dataTr.FindAllSerializableFields(base.Session
                    , null, WriterProcessor.EXCLUDED_ASSEMBLY_PREFIXES))
                { 
                    base.ImportReference(fieldDef);
                    processor.Append(GetLoadParameterInstruction(comparerMd, v0Pd));
                    processor.Emit(OpCodes.Ldfld, fieldDef);
                    processor.Append(GetLoadParameterInstruction(comparerMd, v1Pd));
                    processor.Emit(OpCodes.Ldfld, fieldDef);
                    FinishTypeReferenceCompare(fieldDef.FieldType);
                    //processor.Emit(OpCodes.Bne_Un, exitMethodInst);
                }

                //Properties.
                foreach (PropertyDefinition propertyDef in dataTr.FindAllSerializableProperties(base.Session
                    , null, WriterProcessor.EXCLUDED_ASSEMBLY_PREFIXES))
                {
                    MethodReference getMr = base.Module.ImportReference(propertyDef.GetMethod);
                    processor.Append(GetLoadParameterInstruction(comparerMd, v0Pd));
                    processor.Emit(OpCodes.Call, getMr);
                    processor.Append(GetLoadParameterInstruction(comparerMd, v1Pd));
                    processor.Emit(OpCodes.Call, getMr);
                    FinishTypeReferenceCompare(propertyDef.PropertyType);
                }

                //Return true;
                processor.Emit(OpCodes.Ldc_I4_1);
                processor.Emit(OpCodes.Ret);
                processor.Append(exitMethodInst);
                processor.Emit(OpCodes.Ret);

                void FinishTypeReferenceCompare(TypeReference tr)
                {
                    /* If a class or struct see if it already has a comparer
                     * using IEquatable. If so then call the comparer method.
                     * Otherwise make a new comparer and call it. */
                    if (tr.IsClassOrStruct(base.Session))
                    {
                        //Make equatable for type.
                        GenericInstanceType git = IEquatable_TypeRef.MakeGenericInstanceType(tr);
                        bool createNestedComparer = !tr.CachedResolve(base.Session).ImplementsInterface(git.FullName);

                        //Create new.
                        if (createNestedComparer)
                        {
                            MethodDefinition cMd = CreateEqualityComparer(tr);
                            processor.Emit(OpCodes.Call, cMd);
                            processor.Emit(OpCodes.Brfalse, exitMethodInst);
                        }
                        //Call existing.
                        else
                        {
                            MethodDefinition cMd = tr.CachedResolve(base.Session).GetMethod("op_Equality");
                            if (cMd == null)
                            {
                                base.LogError($"Type {tr.FullName} implements IEquatable but the comparer method could not be found.");
                                return;
                            }
                            else
                            {
                                MethodReference mr = base.ImportReference(cMd);
                                processor.Emit(OpCodes.Call, mr);
                                processor.Emit(OpCodes.Brfalse, exitMethodInst);
                            }
                        }
                    }
                    //Value types do not need to check custom comparers.
                    else
                    {
                        processor.Emit(OpCodes.Bne_Un, exitMethodInst);
                    }
                }
            }

            //Creates a delegate to compare two of replicateTr.
            void CreateComparerDelegate()
            {
                //Initialize delegate for made comparer.
                List<Instruction> insts = new List<Instruction>();
                ILProcessor processor = GeneratedComparer_OnLoadMethodDef.Body.GetILProcessor();
                //Create a Func<Reader, T> delegate 
                insts.Add(processor.Create(OpCodes.Ldnull));
                insts.Add(processor.Create(OpCodes.Ldftn, comparerMd));

                GenericInstanceType git;
                git = gh.FunctionT3TypeRef.MakeGenericInstanceType(dataTr, dataTr, gh.GetTypeReference(typeof(bool)));
                MethodReference functionConstructorInstanceMethodRef = gh.FunctionT3ConstructorMethodRef.MakeHostInstanceGeneric(base.Session, git);
                insts.Add(processor.Create(OpCodes.Newobj, functionConstructorInstanceMethodRef));

                //Call delegate to ReplicateComparer.Compare(T, T);
                git = GeneratedComparer_TypeRef.MakeGenericInstanceType(dataTr);
                MethodReference comparerMr = GeneratedComparer_Compare_Set_MethodRef.MakeHostInstanceGeneric(base.Session, git);
                insts.Add(processor.Create(OpCodes.Call, comparerMr));
                processor.InsertFirst(insts);
            }

        }

        /// <summary>
        /// Returns an OpCode for loading a parameter.
        /// </summary>
        public OpCode GetLoadParameterOpCode(ParameterDefinition pd)
        {
            return (pd.ParameterType.IsValueType) ? OpCodes.Ldarga : OpCodes.Ldarg;
        }

        /// <summary>
        /// Returns an instruction for loading a parameter.s
        /// </summary>
        public Instruction GetLoadParameterInstruction(MethodDefinition md, ParameterDefinition pd)
        {
            ILProcessor processor = md.Body.GetILProcessor();
            OpCode oc = GetLoadParameterOpCode(pd);
            return processor.Create(oc, pd);
        }

        /// <summary>
        /// Creates an IsDefault comparer for dataTr.
        /// </summary>
        public void CreateIsDefaultComparer(TypeReference dataTr, MethodDefinition compareMethodDef)
        {
            GeneralHelper gh = base.GetClass<GeneralHelper>();

            MethodDefinition isDefaultMd = gh.GetOrCreateMethod(GeneratedComparer_ClassTypeDef, out bool created, WriterProcessor.GENERATED_METHOD_ATTRIBUTES,
                $"IsDefault___{dataTr.FullName}", base.Module.TypeSystem.Boolean);
            //Already done. This can happen if the same replicate data is used in multiple places.
            if (!created)
                return;

            MethodReference compareMr = base.ImportReference(compareMethodDef);
            CreateIsDefaultMethod();
            CreateIsDefaultDelegate();

            void CreateIsDefaultMethod()
            {
                //Add parameters.
                ParameterDefinition v0Pd = gh.CreateParameter(isDefaultMd, dataTr, "value0");
                ILProcessor processor = isDefaultMd.Body.GetILProcessor();
                isDefaultMd.Body.InitLocals = true;


                processor.Emit(OpCodes.Ldarg, v0Pd);
                //If a struct.
                if (dataTr.IsValueType)
                {
                    //Init a default local.
                    VariableDefinition defaultVd = gh.CreateVariable(isDefaultMd, dataTr);
                    processor.Emit(OpCodes.Ldloca, defaultVd);
                    processor.Emit(OpCodes.Initobj, dataTr);
                    processor.Emit(OpCodes.Ldloc, defaultVd);
                }
                //If a class.
                else
                {
                    processor.Emit(OpCodes.Ldnull);
                }

                processor.Emit(OpCodes.Call, compareMr);
                processor.Emit(OpCodes.Ret);


            }

            //Creates a delegate to compare two of replicateTr.
            void CreateIsDefaultDelegate()
            {
                //Initialize delegate for made comparer.
                List<Instruction> insts = new List<Instruction>();
                ILProcessor processor = GeneratedComparer_OnLoadMethodDef.Body.GetILProcessor();
                //Create a Func<Reader, T> delegate 
                insts.Add(processor.Create(OpCodes.Ldnull));
                insts.Add(processor.Create(OpCodes.Ldftn, isDefaultMd));

                GenericInstanceType git;
                git = gh.FunctionT2TypeRef.MakeGenericInstanceType(dataTr, gh.GetTypeReference(typeof(bool)));
                MethodReference funcCtorMethodRef = gh.FunctionT2ConstructorMethodRef.MakeHostInstanceGeneric(base.Session, git);
                insts.Add(processor.Create(OpCodes.Newobj, funcCtorMethodRef));

                //Call delegate to ReplicateComparer.IsDefault(T).
                git = GeneratedComparer_TypeRef.MakeGenericInstanceType(dataTr);
                MethodReference isDefaultMr = GeneratedComparer_IsDefault_Set_MethodRef.MakeHostInstanceGeneric(base.Session, git);
                insts.Add(processor.Create(OpCodes.Call, isDefaultMr));
                processor.InsertFirst(insts);
            }

        }
        #endregion
    }
}