using FishNet.CodeGenerating.Helping.Extension;
using FishNet.Object.Synchronizing;
using FishNet.Object.Synchronizing.Internal;
using FishNet.Serializing;
using FishNet.Transporting;
using Mono.Cecil;
using Mono.Cecil.Cil;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace FishNet.CodeGenerating.Helping
{
    internal class SyncHandlerGenerator
    {
        #region Relfection references.
        internal Dictionary<TypeDefinition, CreatedSyncType> CreatedSyncTypes = new Dictionary<TypeDefinition, CreatedSyncType>(new TypeDefinitionComparer());
        private TypeReference SyncBase_TypeRef;
        private FieldReference SyncBase_NetworkBehaviour_FieldRef;
        internal MethodReference SyncBase_SetSyncIndex_MethodRef;
        #endregion

        #region Const.
        public const Mono.Cecil.TypeAttributes SYNCSTUB_TYPE_ATTRIBUTES = (Mono.Cecil.TypeAttributes.BeforeFieldInit |
            Mono.Cecil.TypeAttributes.Class | Mono.Cecil.TypeAttributes.AnsiClass | Mono.Cecil.TypeAttributes.Public |
            Mono.Cecil.TypeAttributes.AutoClass);
        private const string SYNCSTUB_CLASS_PREFIX = "SyncHandler";
        #endregion
        /* //todo add and test the dirty boolean changes
         * eg... instead of base.Dirty()
         * do if (!base.Dirty()) return false;
         * See synclist for more info. */

        /// <summary>
        /// Imports references needed by this helper.
        /// </summary>
        /// <param name="moduleDef"></param>
        /// <returns></returns>
        internal bool ImportReferences()
        {
            Type syncBaseType = typeof(SyncBase);
            SyncBase_TypeRef = CodegenSession.Module.ImportReference(syncBaseType);

            foreach (MethodInfo methodInfo in syncBaseType.GetMethods())
            {
                if (methodInfo.Name == nameof(SyncBase.SetSyncIndex))
                    SyncBase_SetSyncIndex_MethodRef = CodegenSession.Module.ImportReference(methodInfo);
            }
            foreach (FieldInfo fieldInfo in syncBaseType.GetFields())
            {
                if (fieldInfo.Name == nameof(SyncBase.NetworkBehaviour))
                    SyncBase_NetworkBehaviour_FieldRef = CodegenSession.Module.ImportReference(fieldInfo);
            }
            return true;
        }

        /// <summary>
        /// Gets the syncstub class for typeRef.
        /// </summary>
        /// <param name="dataTypeRef"></param>
        /// <returns></returns>
        internal TypeDefinition GetOrCreateSyncHandler(TypeReference dataTypeRef, out MethodReference syncHandlerGetValueMethodRef,
            out MethodReference syncHandlerSetValueMethodRef, out MethodReference syncHandlerGetPreviousClientValueMethodRef,
            out MethodReference syncHandlerReadMethodRef)
        {
            syncHandlerSetValueMethodRef = null;
            syncHandlerGetValueMethodRef = null;
            syncHandlerGetPreviousClientValueMethodRef = null;
            syncHandlerReadMethodRef = null;

            TypeDefinition dataTypeDef = dataTypeRef.Resolve();

            bool created;
            TypeDefinition syncClassTypeDef = CodegenSession.GeneralHelper.GetOrCreateClass(out created, SYNCSTUB_TYPE_ATTRIBUTES,
                $"{SYNCSTUB_CLASS_PREFIX}{dataTypeRef.Name}", SyncBase_TypeRef);

            if (!created)
            {
                CreatedSyncType createdSyncStub;
                if (CreatedSyncTypes.TryGetValue(dataTypeDef, out createdSyncStub))
                {
                    syncHandlerGetValueMethodRef = createdSyncStub.GetValueMethodReference;
                    syncHandlerSetValueMethodRef = createdSyncStub.SetValueMethodReference;
                    syncHandlerGetPreviousClientValueMethodRef = createdSyncStub.GetPreviousClientValueMethodReference;
                    syncHandlerReadMethodRef = createdSyncStub.ReadMethodReference;
                }
                else
                {
                    /* Data is missing, reconstruct it. This can happen when first pass creates the data
                     * but it becomes lost on next pass. */
                    TypeDefinition syncBaseTypeDef = SyncBase_TypeRef.Resolve();
                    MethodDefinition tmpMd;

                    tmpMd = syncClassTypeDef.GetMethod("GetValue");
                    syncHandlerGetValueMethodRef = CodegenSession.Module.ImportReference(tmpMd);

                    tmpMd = syncClassTypeDef.GetMethod("SetValue");
                    syncHandlerSetValueMethodRef = CodegenSession.Module.ImportReference(tmpMd);

                    tmpMd = syncClassTypeDef.GetMethod("GetPreviousClientValue");
                    syncHandlerGetPreviousClientValueMethodRef = CodegenSession.Module.ImportReference(tmpMd);

                    tmpMd = syncClassTypeDef.GetMethod("Read");
                    syncHandlerReadMethodRef = CodegenSession.Module.ImportReference(tmpMd);

                    tmpMd = syncClassTypeDef.GetMethod(".ctor");
                    MethodReference syncHandlerConstructorMethodRef = CodegenSession.Module.ImportReference(tmpMd);

                    CreatedSyncType result = new CreatedSyncType(syncBaseTypeDef, syncHandlerGetValueMethodRef,
                        syncHandlerSetValueMethodRef, syncHandlerGetPreviousClientValueMethodRef, syncHandlerReadMethodRef, syncHandlerConstructorMethodRef);

                    CreatedSyncTypes.Add(dataTypeDef, result);
                }
            }
            //If was created then it must be completed with fields, methods, ect.
            else
            {
                /* Create comparer method reference for type. */
                Type dataMonoType = dataTypeRef.GetMonoType();
                if (dataMonoType == null)
                    return null;

                CodegenSession.Module.ImportReference(dataTypeRef.Resolve());
                TypeDefinition syncBaseTypeDef = SyncBase_TypeRef.Resolve();
                /* Required references. */

                //Methods.
                MethodReference baseReadMethodRef = null;
                MethodReference baseResetMethodRef = null;
                MethodReference baseWriteDeltaMethodRef = null;
                MethodReference baseWriteFullMethodRef = null;
                MethodReference baseDirtyMethodRef = null;
                MethodReference baseInitializeInstanceMethodRef = null;
                foreach (MethodDefinition methodDef in syncBaseTypeDef.Methods)
                {
                    if (methodDef.Name == nameof(SyncBase.Read))
                        baseReadMethodRef = CodegenSession.Module.ImportReference(methodDef);
                    else if (methodDef.Name == nameof(SyncBase.Reset))
                        baseResetMethodRef = CodegenSession.Module.ImportReference(methodDef);
                    else if (methodDef.Name == nameof(SyncBase.WriteDelta))
                        baseWriteDeltaMethodRef = CodegenSession.Module.ImportReference(methodDef);
                    else if (methodDef.Name == nameof(SyncBase.WriteFull)) 
                        baseWriteFullMethodRef = CodegenSession.Module.ImportReference(methodDef);
                    else if (methodDef.Name == nameof(SyncBase.Dirty))
                        baseDirtyMethodRef = CodegenSession.Module.ImportReference(methodDef);
                    else if (methodDef.Name == nameof(SyncBase.InitializeInstance))
                        baseInitializeInstanceMethodRef = CodegenSession.Module.ImportReference(methodDef);

                }
                //Fields
                FieldReference baseNetworkBehaviourFieldRef = null;
                foreach (FieldDefinition fieldDef in syncBaseTypeDef.Fields)
                {
                    if (fieldDef.Name == nameof(SyncBase.NetworkBehaviour))
                        baseNetworkBehaviourFieldRef = CodegenSession.Module.ImportReference(fieldDef);
                }

                /* Adding fields to class. */
                //PreviousClientValue.
                FieldDefinition previousClientValueFieldDef = new FieldDefinition("_previousClientValue", Mono.Cecil.FieldAttributes.Private, dataTypeRef);
                syncClassTypeDef.Fields.Add(previousClientValueFieldDef);
                //InitializedValue.
                FieldDefinition initializeValueFieldDef = new FieldDefinition("_initializeValue", Mono.Cecil.FieldAttributes.Private, dataTypeRef);
                syncClassTypeDef.Fields.Add(initializeValueFieldDef);
                //Value.
                FieldDefinition valueFieldDef = new FieldDefinition("_value", Mono.Cecil.FieldAttributes.Private, dataTypeRef);
                syncClassTypeDef.Fields.Add(valueFieldDef);

                MethodDefinition tmpMd;
                tmpMd = CreateSyncHandlerConstructor(syncClassTypeDef, dataTypeRef.Resolve(), previousClientValueFieldDef, initializeValueFieldDef, valueFieldDef, baseInitializeInstanceMethodRef);
                MethodReference syncHandlerConstructorMethodRef = CodegenSession.Module.ImportReference(tmpMd);

                tmpMd = CreateSetValueMethodDefinition(syncClassTypeDef, valueFieldDef, previousClientValueFieldDef, baseNetworkBehaviourFieldRef, baseDirtyMethodRef, dataTypeRef);
                syncHandlerSetValueMethodRef = CodegenSession.Module.ImportReference(tmpMd);

                tmpMd = CreateReadMethodDefinition(syncClassTypeDef, syncHandlerSetValueMethodRef, baseReadMethodRef, dataTypeRef);
                syncHandlerReadMethodRef = CodegenSession.Module.ImportReference(tmpMd);

                tmpMd = CreateWriteDeltaMethodDefinition(syncClassTypeDef, valueFieldDef, baseWriteDeltaMethodRef, dataTypeRef);
                MethodReference writeMethodRef = CodegenSession.Module.ImportReference(tmpMd);

                CreateWriteFullMethodDefinition(syncClassTypeDef, writeMethodRef, valueFieldDef, initializeValueFieldDef, baseWriteFullMethodRef);

                tmpMd = CreateGetValueMethodDefinition(syncClassTypeDef, valueFieldDef, dataTypeRef);
                syncHandlerGetValueMethodRef = CodegenSession.Module.ImportReference(tmpMd);

                tmpMd = CreateGetPreviousClientValueMethodDefinition(syncClassTypeDef, previousClientValueFieldDef, dataTypeRef);
                syncHandlerGetPreviousClientValueMethodRef = CodegenSession.Module.ImportReference(tmpMd);

                CreateResetMethodDefinition(syncClassTypeDef, initializeValueFieldDef, valueFieldDef, baseResetMethodRef);

                CreatedSyncTypes.Add(dataTypeDef, new CreatedSyncType(syncBaseTypeDef, syncHandlerGetValueMethodRef,
                    syncHandlerSetValueMethodRef, syncHandlerGetPreviousClientValueMethodRef, syncHandlerReadMethodRef, syncHandlerConstructorMethodRef));
            }

            return syncClassTypeDef;
        }


        /// <summary>
        /// Gets the current constructor for typeDef, or makes a new one if constructor doesn't exist.
        /// </summary>
        /// <param name="typeDef"></param>
        /// <returns></returns>
        internal MethodDefinition CreateSyncHandlerConstructor(TypeDefinition typeDef, TypeDefinition valueTypeDef,
            FieldDefinition previousClientValueFieldDef, FieldDefinition initializeValueFieldDef,
            FieldDefinition valueFieldDef, MethodReference baseInitializeInstanceMethodRef)
        {
            Mono.Cecil.MethodAttributes methodAttr = (Mono.Cecil.MethodAttributes.HideBySig |
                    Mono.Cecil.MethodAttributes.Public | Mono.Cecil.MethodAttributes.SpecialName |
                    Mono.Cecil.MethodAttributes.RTSpecialName);

            //Create constructor.
            MethodDefinition createdMethodDef = new MethodDefinition(".ctor", methodAttr,
                    typeDef.Module.TypeSystem.Void
                    );
            typeDef.Methods.Add(createdMethodDef);

            createdMethodDef.Body.InitLocals = true;

            //Add parameters.
            ParameterDefinition writePermissionsParameterDef = CodegenSession.GeneralHelper.CreateParameter(createdMethodDef, typeof(WritePermission));
            ParameterDefinition readPermissionsParameterDef = CodegenSession.GeneralHelper.CreateParameter(createdMethodDef, typeof(ReadPermission));
            ParameterDefinition sendTickIntervalParameterDef = CodegenSession.GeneralHelper.CreateParameter(createdMethodDef, typeof(float));
            ParameterDefinition channelParameterDef = CodegenSession.GeneralHelper.CreateParameter(createdMethodDef, typeof(Channel));
            ParameterDefinition initialValueParameterDef = CodegenSession.GeneralHelper.CreateParameter(createdMethodDef, valueTypeDef);

            ILProcessor processor = createdMethodDef.Body.GetILProcessor();

            //Set initial values.
            //Previous values.
            processor.Emit(OpCodes.Ldarg_0);
            processor.Emit(OpCodes.Ldarg, initialValueParameterDef);
            processor.Emit(OpCodes.Stfld, previousClientValueFieldDef);
            //Initialize.
            processor.Emit(OpCodes.Ldarg_0);
            processor.Emit(OpCodes.Ldarg, initialValueParameterDef);
            processor.Emit(OpCodes.Stfld, initializeValueFieldDef);
            //Value.
            processor.Emit(OpCodes.Ldarg_0);
            processor.Emit(OpCodes.Ldarg, initialValueParameterDef);
            processor.Emit(OpCodes.Stfld, valueFieldDef);

            //Call base initialize with parameters passed in.
            processor.Emit(OpCodes.Ldarg_0); //this.
            processor.Emit(OpCodes.Ldarg, writePermissionsParameterDef);
            processor.Emit(OpCodes.Ldarg, readPermissionsParameterDef);
            processor.Emit(OpCodes.Ldarg, sendTickIntervalParameterDef);
            processor.Emit(OpCodes.Ldarg, channelParameterDef);
            processor.Emit(OpCodes.Ldc_I4_0); //false bool for IsSyncObject.
            processor.Emit(OpCodes.Call, baseInitializeInstanceMethodRef);

            processor.Emit(OpCodes.Ret);

            return createdMethodDef;
        }


        /// <summary>
        /// Creates private SetValue method.
        /// </summary>
        /// <param name="createdClassTypeDef"></param>
        /// <param name="dataTypeRef"></param>
        private MethodDefinition CreateSetValueMethodDefinition(TypeDefinition createdClassTypeDef, FieldDefinition valueFieldDef,
            FieldDefinition previousClientValueFieldDef, FieldReference baseNetworkBehaviourFieldRef, MethodReference baseDirtyMethodRef,
            TypeReference dataTypeRef)
        {
            MethodDefinition createdMethodDef = new MethodDefinition("SetValue",
                (Mono.Cecil.MethodAttributes.Public | Mono.Cecil.MethodAttributes.HideBySig),
                CodegenSession.Module.TypeSystem.Boolean);
            createdClassTypeDef.Methods.Add(createdMethodDef);

            ParameterDefinition nextValueParameterDef = CodegenSession.GeneralHelper.CreateParameter(createdMethodDef, dataTypeRef, "nextValue");
            ParameterDefinition asServerParameterDef = CodegenSession.GeneralHelper.CreateParameter(createdMethodDef, typeof(bool), "asServer");

            ILProcessor processor = createdMethodDef.Body.GetILProcessor();
            createdMethodDef.Body.InitLocals = true;

            //True, for return response.
            Instruction endMethodFalseInst = processor.Create(OpCodes.Nop);
            Instruction endMethodTrueInst = processor.Create(OpCodes.Nop);

            /* If deinitializing then exit method. */
            //if (base.NetworkBehaviourDeinitializing) return.
            processor.Emit(OpCodes.Ldarg_0); //base.
            processor.Emit(OpCodes.Ldfld, baseNetworkBehaviourFieldRef);
            processor.Emit(OpCodes.Call, CodegenSession.GeneralHelper.NetworkObject_Deinitializing_MethodRef);
            processor.Emit(OpCodes.Brtrue, endMethodFalseInst);

            //bool isServer = Helper.IsServer(base.NetworkBehaviour)
            VariableDefinition isServerVariableDef = CodegenSession.GeneralHelper.CreateVariable(createdMethodDef, typeof(bool));
            CreateCallBaseNetworkBehaviour(processor, baseNetworkBehaviourFieldRef);
            processor.Emit(OpCodes.Call, CodegenSession.GeneralHelper.IsServer_MethodRef);
            processor.Emit(OpCodes.Stloc, isServerVariableDef);
            //bool isClient = Helper.IsClient(base.NetworkBehaviour)
            VariableDefinition isClientVariableDef = CodegenSession.GeneralHelper.CreateVariable(createdMethodDef, typeof(bool));
            CreateCallBaseNetworkBehaviour(processor, baseNetworkBehaviourFieldRef);
            processor.Emit(OpCodes.Call, CodegenSession.GeneralHelper.IsClient_MethodRef);
            processor.Emit(OpCodes.Stloc, isClientVariableDef);


            Instruction beginClientChecksInst = processor.Create(OpCodes.Nop);

            /* As Server condition. */
            //If asServer / else jump to IsClient check.
            processor.Emit(OpCodes.Ldarg, asServerParameterDef);
            processor.Emit(OpCodes.Brfalse_S, beginClientChecksInst);
            //IsServer check.
            Instruction serverCanProcessLogicInst = processor.Create(OpCodes.Nop);
            processor.Emit(OpCodes.Ldloc, isServerVariableDef);
            processor.Emit(OpCodes.Brtrue_S, serverCanProcessLogicInst);
            //Debug and exit if server isn't active.
            CodegenSession.GeneralHelper.CreateDebugWarning(processor, $"Sync value cannot be set when server is not active.");
            CodegenSession.GeneralHelper.CreateRetBoolean(processor, false);
            //Server logic.
            processor.Append(serverCanProcessLogicInst);
            //Return false if unchanged.
            CreateRetFalseIfUnchanged(processor, valueFieldDef, nextValueParameterDef);
            //_value = nextValue.
            processor.Emit(OpCodes.Ldarg_0); //this.
            processor.Emit(OpCodes.Ldarg, nextValueParameterDef);
            processor.Emit(OpCodes.Stfld, valueFieldDef);
            //Dirty.
            processor.Emit(OpCodes.Ldarg_0); //base.
            processor.Emit(OpCodes.Call, baseDirtyMethodRef);
            processor.Emit(OpCodes.Pop);
            processor.Emit(OpCodes.Br, endMethodTrueInst);

            /* !AsServer condition. (setting as client)*/

            //IsClient check.
            processor.Append(beginClientChecksInst);
            processor.Emit(OpCodes.Ldloc, isClientVariableDef);
            Instruction clientCanProcessLogicInst = processor.Create(OpCodes.Nop);
            processor.Emit(OpCodes.Brtrue_S, clientCanProcessLogicInst);
            //Debug and exit if client isn't active.
            CodegenSession.GeneralHelper.CreateDebugWarning(processor, $"Sync value cannot be set when client is not active.");
            CodegenSession.GeneralHelper.CreateRetBoolean(processor, false);
            //Client logic.
            processor.Append(clientCanProcessLogicInst);

            Instruction endEqualityCheckInst = processor.Create(OpCodes.Nop);
            //Return false if unchanged. Only checked if also not server.
            processor.Emit(OpCodes.Ldloc, isServerVariableDef);
            processor.Emit(OpCodes.Brtrue, endEqualityCheckInst);
            CreateRetFalseIfUnchanged(processor, previousClientValueFieldDef, nextValueParameterDef);
            processor.Append(endEqualityCheckInst);

            /* Set the previous client value no matter what.
             * The new value will only be set if not also server,
             * as the current value on server shouldn't be overwritten
             * with the latest client received. */
            //_previousClientValue = _value;
            processor.Emit(OpCodes.Ldarg_0); //this.
            processor.Emit(OpCodes.Ldarg_0); //this. one for each field.
            processor.Emit(OpCodes.Ldfld, valueFieldDef);
            processor.Emit(OpCodes.Stfld, previousClientValueFieldDef);

            /* As mentioned only set value if not also server.
             * Server value shouldn't be overwritten by client. */
            //_value = nextValue.
            Instruction isServerUpdateValueEndIfInst = processor.Create(OpCodes.Nop);
            processor.Emit(OpCodes.Ldloc, isServerVariableDef);
            processor.Emit(OpCodes.Brtrue, isServerUpdateValueEndIfInst);
            processor.Emit(OpCodes.Ldarg_0); //this.
            processor.Emit(OpCodes.Ldarg, nextValueParameterDef);
            processor.Emit(OpCodes.Stfld, valueFieldDef);
            processor.Append(isServerUpdateValueEndIfInst);

            processor.Append(endMethodTrueInst);
            //Return true at end of !asServer. Will arrive if all checks pass.
            CodegenSession.GeneralHelper.CreateRetBoolean(processor, true);

            //End of method return.
            processor.Append(endMethodFalseInst);
            CodegenSession.GeneralHelper.CreateRetBoolean(processor, false);

            return createdMethodDef;
        }


        /// <summary>
        /// Creates Read method.
        /// </summary>
        /// <param name="createdClassTypeDef"></param>
        /// <param name="valueFieldDef"></param>
        /// <param name="dataTypeRef"></param>
        private MethodDefinition CreateReadMethodDefinition(TypeDefinition createdClassTypeDef, MethodReference setValueMethodRef, MethodReference baseReadMethodRef, TypeReference dataTypeRef)
        {
            MethodDefinition createdMethodDef = new MethodDefinition("Read",
                (Mono.Cecil.MethodAttributes.Public | Mono.Cecil.MethodAttributes.HideBySig | Mono.Cecil.MethodAttributes.Virtual),
                CodegenSession.Module.TypeSystem.Boolean);
            createdClassTypeDef.Methods.Add(createdMethodDef);

            ParameterDefinition readerParameterDef = CodegenSession.GeneralHelper.CreateParameter(createdMethodDef, CodegenSession.ReaderHelper.PooledReader_TypeRef);

            ILProcessor processor = createdMethodDef.Body.GetILProcessor();
            createdMethodDef.Body.InitLocals = true;
            //MethodReference baseReadMethodRef = CodegenSession.Module.ImportReference(baseReadMethodDef);

            //base.Read(pooledReader);
            processor.Emit(OpCodes.Ldarg_0);
            processor.Emit(OpCodes.Ldarg, readerParameterDef);
            processor.Emit(OpCodes.Call, baseReadMethodRef);

            VariableDefinition newValue = CodegenSession.GeneralHelper.CreateVariable(createdMethodDef, dataTypeRef);
            MethodReference readTypeMethodRef = CodegenSession.ReaderHelper.GetOrCreateFavoredReadMethodReference(dataTypeRef, true);

            //value = reader.ReadXXXXX
            processor.Emit(OpCodes.Ldarg, readerParameterDef);
            if (CodegenSession.ReaderHelper.IsAutoPackedType(dataTypeRef))
                processor.Emit(OpCodes.Ldc_I4_1); //AutoPackType.Packed
            processor.Emit(OpCodes.Callvirt, readTypeMethodRef);
            processor.Emit(OpCodes.Stloc, newValue);

            //SetValue(newValue, false);
            processor.Emit(OpCodes.Ldarg_0); //this.
            processor.Emit(OpCodes.Ldloc, newValue);
            processor.Emit(OpCodes.Ldc_I4_0); //false boolean - !asServer.
            processor.Emit(OpCodes.Call, setValueMethodRef);

            processor.Emit(OpCodes.Ret);

            return createdMethodDef;
        }


        /// <summary>
        /// Creates Write method.
        /// </summary>
        /// <param name="createdClassTypeDef"></param>
        /// <param name="dataTypeRef"></param>
        private MethodDefinition CreateWriteDeltaMethodDefinition(TypeDefinition createdClassTypeDef, FieldDefinition valueFieldDef, MethodReference baseWriteMethodRef, TypeReference dataTypeRef)
        {
            MethodDefinition createdMethodDef = new MethodDefinition(baseWriteMethodRef.Name,
                (Mono.Cecil.MethodAttributes.Public | Mono.Cecil.MethodAttributes.HideBySig | Mono.Cecil.MethodAttributes.Virtual),
                CodegenSession.Module.TypeSystem.Void);
            createdClassTypeDef.Methods.Add(createdMethodDef);

            //PooledWriter parameter.
            ParameterDefinition writerParameterDef = CodegenSession.GeneralHelper.CreateParameter(createdMethodDef, CodegenSession.WriterHelper.PooledWriter_TypeRef);
            //resetSyncTime parameter.
            ParameterDefinition resetSyncTimeParameterDef = CodegenSession.GeneralHelper.CreateParameter(createdMethodDef, typeof(bool), "", (Mono.Cecil.ParameterAttributes.HasDefault | Mono.Cecil.ParameterAttributes.Optional));
            resetSyncTimeParameterDef.Constant = (bool)true;

            ILProcessor processor = createdMethodDef.Body.GetILProcessor();
            createdMethodDef.Body.InitLocals = true;

            //base.Write(writer, bool);
            processor.Emit(OpCodes.Ldarg_0);
            processor.Emit(OpCodes.Ldarg, writerParameterDef);
            processor.Emit(OpCodes.Ldarg, resetSyncTimeParameterDef);
            processor.Emit(OpCodes.Call, baseWriteMethodRef);

            //Write value.
            MethodReference writeMethodRef = CodegenSession.WriterHelper.GetOrCreateFavoredWriteMethodReference(dataTypeRef, true);

            processor.Emit(OpCodes.Ldarg, writerParameterDef);
            processor.Emit(OpCodes.Ldarg_0); //this.
            processor.Emit(OpCodes.Ldfld, valueFieldDef);
            //If an auto pack method then insert default value.
            if (CodegenSession.WriterHelper.IsAutoPackedType(valueFieldDef.FieldType))
            {
                AutoPackType packType = CodegenSession.GeneralHelper.GetDefaultAutoPackType(valueFieldDef.FieldType);
                processor.Emit(OpCodes.Ldc_I4, (int)packType);
            }
            processor.Emit(OpCodes.Call, writeMethodRef);

            processor.Emit(OpCodes.Ret);

            return createdMethodDef;
        }


        /// <summary>
        /// Creates WriteIfChanged method.
        /// </summary>
        /// <param name="createdClassTypeDef"></param>
        /// <param name="syncTypeRef"></param>
        private void CreateWriteFullMethodDefinition(TypeDefinition createdClassTypeDef, MethodReference writeMethodRef, FieldDefinition valueFieldDef, FieldDefinition initialValueFieldDef, MethodReference baseWriteMethodRef)
        {
            MethodDefinition createdMethodDef = new MethodDefinition(baseWriteMethodRef.Name,
                (Mono.Cecil.MethodAttributes.Public | Mono.Cecil.MethodAttributes.HideBySig | Mono.Cecil.MethodAttributes.Virtual),
                CodegenSession.Module.TypeSystem.Void);
            createdClassTypeDef.Methods.Add(createdMethodDef);

            //PooledWriter parameter.
            ParameterDefinition writerParameterDef = CodegenSession.GeneralHelper.CreateParameter(createdMethodDef, CodegenSession.WriterHelper.PooledWriter_TypeRef);

            ILProcessor processor = createdMethodDef.Body.GetILProcessor();
            createdMethodDef.Body.InitLocals = true;

            //Exit early if unchanged
            CreateRetIfUnchanged(processor, valueFieldDef, initialValueFieldDef);

            //Write(pooledWriter, false);
            processor.Emit(OpCodes.Ldarg_0); //this.
            processor.Emit(OpCodes.Ldarg, writerParameterDef);
            processor.Emit(OpCodes.Ldc_I4_0); //false boolean.
            processor.Emit(OpCodes.Call, writeMethodRef);

            processor.Emit(OpCodes.Ret);
        }


        /// <summary>
        /// Creates GetValue method.
        /// </summary>
        /// <param name="createdClassTypeDef"></param>
        /// <param name="valueFieldDef"></param>
        /// <param name="dataTypeRef"></param>
        private MethodDefinition CreateGetValueMethodDefinition(TypeDefinition createdClassTypeDef, FieldDefinition valueFieldDef, TypeReference dataTypeRef)
        {
            MethodDefinition createdMethodDef = new MethodDefinition("GetValue", (Mono.Cecil.MethodAttributes.Public | Mono.Cecil.MethodAttributes.HideBySig),
                dataTypeRef);
            createdClassTypeDef.Methods.Add(createdMethodDef);

            ILProcessor processor = createdMethodDef.Body.GetILProcessor();
            createdMethodDef.Body.InitLocals = true;
            //return Value.
            processor.Emit(OpCodes.Ldarg_0); //this.
            processor.Emit(OpCodes.Ldfld, valueFieldDef);
            processor.Emit(OpCodes.Ret);

            return createdMethodDef;
        }



        /// <summary>
        /// Creates GetPreviousValue method.
        /// </summary>
        /// <param name="createdClassTypeDef"></param>
        /// <param name="previousClientValueFieldDef"></param>
        /// <param name="dataTypeRef"></param>
        private MethodDefinition CreateGetPreviousClientValueMethodDefinition(TypeDefinition createdClassTypeDef, FieldDefinition previousClientValueFieldDef, TypeReference dataTypeRef)
        {
            MethodDefinition createdMethodDef = new MethodDefinition("GetPreviousClientValue", (Mono.Cecil.MethodAttributes.Public | Mono.Cecil.MethodAttributes.HideBySig),
                dataTypeRef);
            createdClassTypeDef.Methods.Add(createdMethodDef);

            ILProcessor processor = createdMethodDef.Body.GetILProcessor();
            createdMethodDef.Body.InitLocals = true;

            processor.Emit(OpCodes.Ldarg_0); //this.
            processor.Emit(OpCodes.Ldfld, previousClientValueFieldDef);
            processor.Emit(OpCodes.Ret);

            return createdMethodDef;
        }


        /// <summary>
        /// Creates Reset method.
        /// </summary>
        /// <param name="createdClassTypeDef"></param>
        /// <param name="initializedValueFieldDef"></param>
        /// <param name="valueFieldDef"></param>
        private void CreateResetMethodDefinition(TypeDefinition createdClassTypeDef, FieldDefinition initializedValueFieldDef, FieldDefinition valueFieldDef, MethodReference baseResetMethodRef)
        {
            MethodDefinition createdMethodDef = new MethodDefinition("Reset",
                (Mono.Cecil.MethodAttributes.Public | Mono.Cecil.MethodAttributes.HideBySig | Mono.Cecil.MethodAttributes.Virtual),
                CodegenSession.Module.TypeSystem.Void);
            createdClassTypeDef.Methods.Add(createdMethodDef);

            ILProcessor processor = createdMethodDef.Body.GetILProcessor();
            createdMethodDef.Body.InitLocals = true;

            /*_value = _initializedValue; */
            processor.Emit(OpCodes.Ldarg_0); //this.
            processor.Emit(OpCodes.Ldarg_0); //this. (one for each field.
            processor.Emit(OpCodes.Ldfld, initializedValueFieldDef);
            processor.Emit(OpCodes.Stfld, valueFieldDef);

            processor.Emit(OpCodes.Ldarg_0); //this.
            processor.Emit(OpCodes.Call, baseResetMethodRef);

            processor.Emit(OpCodes.Ret);
        }

        /// <summary>
        /// Creates a ret of false if compared value is unchanged from current.
        /// </summary>
        private void CreateRetFalseIfUnchanged(ILProcessor processor, FieldDefinition valueFieldDef, object nextValueDef) //fix
        {
            //Instruction endIfInst = processor.Create(OpCodes.Nop);
            ////If (Comparer.EqualityCompare(_value, _initialValue)) return;
            //processor.Emit(OpCodes.Ldarg_0);
            //processor.Emit(OpCodes.Ldfld, valueFieldDef.MakeHostGenericIfNeeded());

            //TypeReference originalType = null;
            ////If comparing against another field.
            //if (nextValueDef is FieldDefinition fd)
            //{
            //    originalType = fd.FieldType;
            //    processor.Emit(OpCodes.Ldarg_0);
            //    processor.Emit(OpCodes.Ldfld, fd);
            //}
            ////If comparing against a parameter.
            //else if (nextValueDef is ParameterDefinition pd)
            //{
            //    originalType = pd.ParameterType;
            //    processor.Emit(OpCodes.Ldarg, pd);
            //}

            ////MethodReference syncVarEqual = CodegenSession.Module.ImportReference<NetworkBehaviour>(nb => nb.SyncTypeEquals<object>(default, default));
            ////var syncVarEqualGm = new GenericInstanceMethod(syncVarEqual.GetElementMethod());
            ////syncVarEqualGm.GenericArguments.Add(originalType);
            ////processor.Emit(OpCodes.Call, syncVarEqualGm);

            //processor.Emit(OpCodes.Call, _typedComparerMethodRef);
            //processor.Emit(OpCodes.Brfalse, endIfInst);
            //CodegenSession.GeneralHelper.CreateRetBoolean(processor, false);
            //processor.Append(endIfInst);
        }


        /// <summary>
        /// Creates a ret if compared value is unchanged from current.
        /// </summary>
        private void CreateRetIfUnchanged(ILProcessor processor, FieldDefinition valueFieldDef, object nextValueDef)
        {            
            Instruction endIfInst = processor.Create(OpCodes.Nop);

            TypeReference originalType = null;
            //If comparing against another field.
            if (nextValueDef is FieldDefinition fd)
            {
                originalType = fd.FieldType;
                processor.Emit(OpCodes.Ldarg_0);
                processor.Emit(OpCodes.Ldfld, fd.MakeHostGenericIfNeeded());
            }
            //If comparing against a parameter.
            else if (nextValueDef is ParameterDefinition pd)
            {
                originalType = pd.ParameterType;
                processor.Emit(OpCodes.Ldarg, pd);
            }

            // new value to set
            processor.Emit(OpCodes.Ldarg_0);
            processor.Append(processor.Create(OpCodes.Ldfld, valueFieldDef));

            MethodReference comparerMethodRef = CodegenSession.GeneralHelper.Comparers_EqualityCompare_MethodRef;
            GenericInstanceMethod syncVarEqualGm = new GenericInstanceMethod(comparerMethodRef.GetElementMethod());
            syncVarEqualGm.GenericArguments.Add(originalType);
            processor.Emit(OpCodes.Call, syncVarEqualGm);
            processor.Emit(OpCodes.Brfalse, endIfInst);
            processor.Emit(OpCodes.Ret);
            processor.Append(endIfInst);
        }
   
        /// <summary>
        /// Creates a call to the base NetworkBehaviour.
        /// </summary>
        /// <param name="processor"></param>
        /// <param name="networkBehaviourFieldRef"></param>
        private void CreateCallBaseNetworkBehaviour(ILProcessor processor, FieldReference networkBehaviourFieldRef)
        {
            processor.Emit(OpCodes.Ldarg_0); //this.
            processor.Emit(OpCodes.Ldfld, networkBehaviourFieldRef);
        }

    }


}