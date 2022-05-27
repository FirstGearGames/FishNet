using FishNet.Broadcast;
using FishNet.CodeGenerating.Helping.Extension;
using FishNet.CodeGenerating.Processing;
using FishNet.Connection;
using FishNet.Managing.Logging;
using FishNet.Object;
using FishNet.Object.Delegating;
using FishNet.Object.Helping;
using FishNet.Object.Prediction.Delegating;
using FishNet.Object.Synchronizing;
using FishNet.Object.Synchronizing.Internal;
using MonoFN.Cecil;
using MonoFN.Cecil.Cil;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace FishNet.CodeGenerating.Helping
{
    internal class ObjectHelper
    {
        #region Reflection references.
        //Fullnames.
        internal string NetworkBehaviour_FullName;
        internal string SyncList_Name;
        internal string SyncDictionary_Name;
        internal string SyncHashSet_Name;
        //Prediction.
        internal MethodReference NetworkBehaviour_TransformMayChange_MethodRef;
        internal MethodReference NetworkBehaviour_SendReplicateRpc_MethodRef;
        internal MethodReference NetworkBehaviour_SendReconcileRpc_MethodRef;
        internal MethodReference NetworkBehaviour_RegisterReplicateRpc_MethodRef;
        internal MethodReference NetworkBehaviour_RegisterReconcileRpc_MethodRef;
        internal MethodReference Networkbehaviour_ReplicateRpcDelegateConstructor_MethodRef;
        internal MethodReference Networkbehaviour_ReconcileRpcDelegateConstructor_MethodRef;
        //RPCs.
        internal MethodReference NetworkBehaviour_SendServerRpc_MethodRef;
        internal MethodReference NetworkBehaviour_SendObserversRpc_MethodRef;
        internal MethodReference NetworkBehaviour_SendTargetRpc_MethodRef;
        internal MethodReference NetworkBehaviour_DirtySyncType_MethodRef;
        private MethodReference NetworkBehaviour_RegisterServerRpc_MethodRef;
        private MethodReference NetworkBehaviour_RegisterObserversRpc_MethodRef;
        private MethodReference NetworkBehaviour_RegisterTargetRpc_MethodRef;
        private MethodReference Networkbehaviour_ServerRpcDelegateConstructor_MethodRef;
        private MethodReference Networkbehaviour_ClientRpcDelegateConstructor_MethodRef;
        //Is checks.
        internal MethodReference NetworkBehaviour_IsClient_MethodRef;
        internal MethodReference NetworkBehaviour_IsOwner_MethodRef;
        internal MethodReference NetworkBehaviour_IsServer_MethodRef;
        internal MethodReference NetworkBehaviour_IsHost_MethodRef;
        internal MethodReference InstanceFinder_IsServer_MethodRef;
        private MethodReference InstanceFinder_IsClient_MethodRef;
        //Misc.
        internal TypeReference NetworkBehaviour_TypeRef;
        private MethodReference NetworkBehaviour_CompareOwner_MethodRef;
        internal MethodReference NetworkBehaviour_OwnerIsValid_MethodRef;
        internal MethodReference NetworkBehaviour_OwnerIsActive_MethodRef;
        internal MethodReference NetworkBehaviour_LocalConnection_MethodRef;
        internal MethodReference NetworkBehaviour_Owner_MethodRef;
        internal MethodReference NetworkBehaviour_ReadSyncVar_MethodRef;
        internal MethodReference Dictionary_Add_UShort_SyncBase_MethodRef;
        internal MethodReference NetworkConnection_GetIsLocalClient_MethodRef;
        //TimeManager.
        internal MethodReference NetworkBehaviour_TimeManager_MethodRef;
        #endregion

        #region Const.
        internal const uint MAX_RPC_ALLOWANCE = ushort.MaxValue;
        internal const string AWAKE_METHOD_NAME = "Awake";
        internal const string DISABLE_LOGGING_TEXT = "This message may be disabled by setting the Logging field in your attribute to LoggingType.Off";
        #endregion

        internal bool ImportReferences()
        {
            Type networkBehaviourType = typeof(NetworkBehaviour);
            NetworkBehaviour_TypeRef = CodegenSession.ImportReference(networkBehaviourType);
            NetworkBehaviour_FullName = networkBehaviourType.FullName;
            CodegenSession.ImportReference(networkBehaviourType);

            Type ibroadcastType = typeof(IBroadcast);

            Type tmpType;
            /* SyncObject names. */
            //SyncList.
            tmpType = typeof(SyncList<>);
            CodegenSession.ImportReference(tmpType);
            SyncList_Name = tmpType.Name;
            //SyncDictionary.
            tmpType = typeof(SyncDictionary<,>);
            CodegenSession.ImportReference(tmpType);
            SyncDictionary_Name = tmpType.Name;
            //SyncHashSet.
            tmpType = typeof(SyncHashSet<>);
            CodegenSession.ImportReference(tmpType);
            SyncHashSet_Name = tmpType.Name;

            tmpType = typeof(NetworkConnection);
            TypeReference networkConnectionTr = CodegenSession.ImportReference(tmpType);
            foreach (PropertyDefinition item in networkConnectionTr.CachedResolve().Properties)
            {
                if (item.Name == nameof(NetworkConnection.IsLocalClient))
                    NetworkConnection_GetIsLocalClient_MethodRef = CodegenSession.ImportReference(item.GetMethod);
            }

            //Dictionary.Add(ushort, SyncBase).
            Type dictType = typeof(Dictionary<ushort, SyncBase>);
            TypeReference dictTypeRef = CodegenSession.ImportReference(dictType);
            //Dictionary_Add_UShort_SyncBase_MethodRef = dictTypeRef.CachedResolve().GetMethod("add_Item", )
            foreach (MethodDefinition item in dictTypeRef.CachedResolve().Methods)
            {
                if (item.Name == nameof(Dictionary<ushort, SyncBase>.Add))
                {
                    Dictionary_Add_UShort_SyncBase_MethodRef = CodegenSession.ImportReference(item);
                    break;
                }
            }

            //InstanceFinder infos.
            Type instanceFinderType = typeof(InstanceFinder);
            foreach (PropertyInfo pi in instanceFinderType.GetProperties())
            {
                if (pi.Name == nameof(InstanceFinder.IsClient))
                    InstanceFinder_IsClient_MethodRef = CodegenSession.ImportReference(pi.GetMethod);
                else if (pi.Name == nameof(InstanceFinder.IsServer))
                    InstanceFinder_IsServer_MethodRef = CodegenSession.ImportReference(pi.GetMethod);
            }

            //ServerRpcDelegate and ClientRpcDelegate constructors.
            Networkbehaviour_ServerRpcDelegateConstructor_MethodRef = CodegenSession.ImportReference(typeof(ServerRpcDelegate).GetConstructors().First());
            Networkbehaviour_ClientRpcDelegateConstructor_MethodRef = CodegenSession.ImportReference(typeof(ClientRpcDelegate).GetConstructors().First());
            //Prediction Rpc delegate constructors.
            Networkbehaviour_ReplicateRpcDelegateConstructor_MethodRef = CodegenSession.ImportReference(typeof(ReplicateRpcDelegate).GetConstructors().First());
            Networkbehaviour_ReconcileRpcDelegateConstructor_MethodRef = CodegenSession.ImportReference(typeof(ReconcileRpcDelegate).GetConstructors().First());

            foreach (MethodInfo mi in networkBehaviourType.GetMethods((BindingFlags.Static | BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic)))
            {
                //CreateDelegates.
                if (mi.Name == nameof(NetworkBehaviour.RegisterServerRpc))
                    NetworkBehaviour_RegisterServerRpc_MethodRef = CodegenSession.ImportReference(mi);
                else if (mi.Name == nameof(NetworkBehaviour.RegisterObserversRpc))
                    NetworkBehaviour_RegisterObserversRpc_MethodRef = CodegenSession.ImportReference(mi);
                else if (mi.Name == nameof(NetworkBehaviour.RegisterTargetRpc))
                    NetworkBehaviour_RegisterTargetRpc_MethodRef = CodegenSession.ImportReference(mi);
                //SendPredictions.
                else if (mi.Name == nameof(NetworkBehaviour.SendReplicateRpc))
                    NetworkBehaviour_SendReplicateRpc_MethodRef = CodegenSession.ImportReference(mi);
                else if (mi.Name == nameof(NetworkBehaviour.SendReconcileRpc))
                    NetworkBehaviour_SendReconcileRpc_MethodRef = CodegenSession.ImportReference(mi);
                else if (mi.Name == nameof(NetworkBehaviour.RegisterReplicateRpc))
                    NetworkBehaviour_RegisterReplicateRpc_MethodRef = CodegenSession.ImportReference(mi);
                else if (mi.Name == nameof(NetworkBehaviour.RegisterReconcileRpc))
                    NetworkBehaviour_RegisterReconcileRpc_MethodRef = CodegenSession.ImportReference(mi);
                //SendRpcs.
                else if (mi.Name == nameof(NetworkBehaviour.SendServerRpc))
                    NetworkBehaviour_SendServerRpc_MethodRef = CodegenSession.ImportReference(mi);
                else if (mi.Name == nameof(NetworkBehaviour.SendObserversRpc))
                    NetworkBehaviour_SendObserversRpc_MethodRef = CodegenSession.ImportReference(mi);
                else if (mi.Name == nameof(NetworkBehaviour.SendTargetRpc))
                    NetworkBehaviour_SendTargetRpc_MethodRef = CodegenSession.ImportReference(mi);
                //Misc.
                else if (mi.Name == nameof(NetworkBehaviour.TransformMayChange))
                    NetworkBehaviour_TransformMayChange_MethodRef = CodegenSession.ImportReference(mi);
                else if (mi.Name == nameof(NetworkBehaviour.CompareOwner))
                    NetworkBehaviour_CompareOwner_MethodRef = CodegenSession.ImportReference(mi);
                else if (mi.Name == nameof(NetworkBehaviour.ReadSyncVar))
                    NetworkBehaviour_ReadSyncVar_MethodRef = CodegenSession.ImportReference(mi);
                else if (mi.Name == nameof(NetworkBehaviour.DirtySyncType))
                    NetworkBehaviour_DirtySyncType_MethodRef = CodegenSession.ImportReference(mi);
            }

            foreach (PropertyInfo pi in networkBehaviourType.GetProperties((BindingFlags.Static | BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic)))
            {
                //Server/Client states.
                if (pi.Name == nameof(NetworkBehaviour.IsClient))
                    NetworkBehaviour_IsClient_MethodRef = CodegenSession.ImportReference(pi.GetMethod);
                else if (pi.Name == nameof(NetworkBehaviour.IsServer))
                    NetworkBehaviour_IsServer_MethodRef = CodegenSession.ImportReference(pi.GetMethod);
                else if (pi.Name == nameof(NetworkBehaviour.IsHost))
                    NetworkBehaviour_IsHost_MethodRef = CodegenSession.ImportReference(pi.GetMethod);
                else if (pi.Name == nameof(NetworkBehaviour.IsOwner))
                    NetworkBehaviour_IsOwner_MethodRef = CodegenSession.ImportReference(pi.GetMethod);
                //Owner.
                else if (pi.Name == nameof(NetworkBehaviour.Owner))
                    NetworkBehaviour_Owner_MethodRef = CodegenSession.ImportReference(pi.GetMethod);
                else if (pi.Name == nameof(NetworkBehaviour.LocalConnection))
                    NetworkBehaviour_LocalConnection_MethodRef = CodegenSession.ImportReference(pi.GetMethod);
#pragma warning disable CS0618 // Type or member is obsolete
                else if (pi.Name == nameof(NetworkBehaviour.OwnerIsValid))
                    NetworkBehaviour_OwnerIsValid_MethodRef = CodegenSession.ImportReference(pi.GetMethod);
                else if (pi.Name == nameof(NetworkBehaviour.OwnerIsActive))
                    NetworkBehaviour_OwnerIsActive_MethodRef = CodegenSession.ImportReference(pi.GetMethod);
#pragma warning restore CS0618 // Type or member is obsolete
                //Misc.
                else if (pi.Name == nameof(NetworkBehaviour.TimeManager))
                    NetworkBehaviour_TimeManager_MethodRef = CodegenSession.ImportReference(pi.GetMethod);
            }

            return true;
        }

        /// <summary>
        /// Returnsthe child most Awake by iterating up childMostTypeDef.
        /// </summary>
        /// <param name="childMostTypeDef"></param>
        /// <param name="created"></param>
        /// <returns></returns>
        internal MethodDefinition GetAwakeMethodDefinition(TypeDefinition typeDef)
        {
            return typeDef.GetMethod(AWAKE_METHOD_NAME);
        }


        /// <summary>
        /// Creates a replicate delegate.
        /// </summary>
        /// <param name="processor"></param>
        /// <param name="originalMethodDef"></param>
        /// <param name="readerMethodDef"></param>
        /// <param name="rpcType"></param>
        internal void CreateReplicateDelegate(MethodDefinition originalMethodDef, MethodDefinition readerMethodDef, uint methodHash)
        {
            MethodDefinition methodDef = originalMethodDef.DeclaringType.GetMethod(NetworkBehaviourProcessor.NETWORKINITIALIZE_EARLY_INTERNAL_NAME);
            ILProcessor processor = methodDef.Body.GetILProcessor();

            List<Instruction> insts = new List<Instruction>();
            insts.Add(processor.Create(OpCodes.Ldarg_0));

            insts.Add(processor.Create(OpCodes.Ldc_I4, (int)methodHash));

            /* Create delegate and call NetworkBehaviour method. */
            insts.Add(processor.Create(OpCodes.Ldnull));
            insts.Add(processor.Create(OpCodes.Ldftn, readerMethodDef));

            /* Has to be done last. This allows the NetworkBehaviour to
             * initialize it's fields first. */
            processor.InsertLast(insts);
        }



        /// <summary>
        /// Creates a RPC delegate for rpcType.
        /// </summary>
        /// <param name="processor"></param>
        /// <param name="originalMethodDef"></param>
        /// <param name="readerMethodDef"></param>
        /// <param name="rpcType"></param>
        internal void CreateRpcDelegate(bool runLocally, TypeDefinition typeDef, MethodDefinition readerMethodDef, RpcType rpcType, uint methodHash, CustomAttribute rpcAttribute)
        {
            

            MethodDefinition methodDef = typeDef.GetMethod(NetworkBehaviourProcessor.NETWORKINITIALIZE_EARLY_INTERNAL_NAME);
            ILProcessor processor = methodDef.Body.GetILProcessor();

            List<Instruction> insts = new List<Instruction>();
            insts.Add(processor.Create(OpCodes.Ldarg_0));

            //uint methodHash = originalMethodDef.FullName.GetStableHash32();
            insts.Add(processor.Create(OpCodes.Ldc_I4, (int)methodHash));

            /* Create delegate and call NetworkBehaviour method. */
            insts.Add(processor.Create(OpCodes.Ldnull));
            insts.Add(processor.Create(OpCodes.Ldftn, readerMethodDef));
            //Server.
            if (rpcType == RpcType.Server)
            {
                insts.Add(processor.Create(OpCodes.Newobj, Networkbehaviour_ServerRpcDelegateConstructor_MethodRef));
                insts.Add(processor.Create(OpCodes.Call, NetworkBehaviour_RegisterServerRpc_MethodRef));
            }
            //Observers.
            else if (rpcType == RpcType.Observers)
            {
                insts.Add(processor.Create(OpCodes.Newobj, Networkbehaviour_ClientRpcDelegateConstructor_MethodRef));
                insts.Add(processor.Create(OpCodes.Call, NetworkBehaviour_RegisterObserversRpc_MethodRef));
            }
            //Target
            else if (rpcType == RpcType.Target)
            {
                insts.Add(processor.Create(OpCodes.Newobj, Networkbehaviour_ClientRpcDelegateConstructor_MethodRef));
                insts.Add(processor.Create(OpCodes.Call, NetworkBehaviour_RegisterTargetRpc_MethodRef));
            }

            /* Has to be done last. This allows the NetworkBehaviour to
             * initialize it's fields first. */
            processor.InsertLast(insts);
        }

        /// <summary>
        /// Creates exit method condition if local client is not owner.
        /// </summary>
        /// <param name="retIfOwner">True if to ret when owner, false to ret when not owner.</param>
        /// <returns>Returns Ret instruction.</returns>
        internal Instruction CreateLocalClientIsOwnerCheck(MethodDefinition methodDef, LoggingType loggingType, bool canDisableLogging, bool retIfOwner, bool insertFirst)
        {
            List<Instruction> instructions = new List<Instruction>();
            /* This is placed after the if check.
             * Should the if check pass then code
             * jumps to this instruction. */
            ILProcessor processor = methodDef.Body.GetILProcessor();
            Instruction endIf = processor.Create(OpCodes.Nop);

            instructions.Add(processor.Create(OpCodes.Ldarg_0)); //argument: this
            //If !base.IsOwner endIf.
            instructions.Add(processor.Create(OpCodes.Call, NetworkBehaviour_IsOwner_MethodRef));
            if (retIfOwner)
                instructions.Add(processor.Create(OpCodes.Brfalse, endIf));
            else
                instructions.Add(processor.Create(OpCodes.Brtrue, endIf));
            //If logging is not disabled.
            if (loggingType != LoggingType.Off)
            {
                string disableLoggingText = (canDisableLogging) ? DISABLE_LOGGING_TEXT : string.Empty;
                string msg = (retIfOwner) ?
                    $"Cannot complete action because you are the owner of this object. {disableLoggingText}." :
                    $"Cannot complete action because you are not the owner of this object. {disableLoggingText}.";

                instructions.AddRange(
                    CodegenSession.GeneralHelper.CreateDebugWithCanLogInstructions(processor, msg, loggingType, false, true)
                    );
            }
            //Return block.
            Instruction retInst = processor.Create(OpCodes.Ret);
            instructions.Add(retInst);
            //After if statement, jumped to when successful check.
            instructions.Add(endIf);

            if (insertFirst)
            {
                processor.InsertFirst(instructions);
            }
            else
            {
                foreach (Instruction inst in instructions)
                    processor.Append(inst);
            }

            return retInst;
        }

        /// <summary>
        /// Creates exit method condition if remote client is not owner.
        /// </summary>
        /// <param name="processor"></param>
        internal Instruction CreateRemoteClientIsOwnerCheck(ILProcessor processor, ParameterDefinition connectionParameterDef)
        {
            /* This is placed after the if check.
             * Should the if check pass then code
             * jumps to this instruction. */
            Instruction endIf = processor.Create(OpCodes.Nop);

            processor.Emit(OpCodes.Ldarg_0); //argument: this
            //If !base.IsOwner endIf.
            processor.Emit(OpCodes.Ldarg, connectionParameterDef);
            processor.Emit(OpCodes.Call, NetworkBehaviour_CompareOwner_MethodRef);
            processor.Emit(OpCodes.Brtrue, endIf);
            //Return block.
            Instruction retInst = processor.Create(OpCodes.Ret);
            processor.Append(retInst);

            //After if statement, jumped to when successful check.
            processor.Append(endIf);

            return retInst;
        }

        /// <summary>
        /// Creates exit method condition if not client.
        /// </summary>
        /// <param name="processor"></param>
        /// <param name="retInstruction"></param>
        /// <param name="warn"></param>
        internal void CreateIsClientCheck(MethodDefinition methodDef, LoggingType loggingType, bool useStatic, bool insertFirst)
        {
            /* This is placed after the if check.
             * Should the if check pass then code
             * jumps to this instruction. */
            ILProcessor processor = methodDef.Body.GetILProcessor();
            Instruction endIf = processor.Create(OpCodes.Nop);

            List<Instruction> instructions = new List<Instruction>();
            //Checking against the NetworkObject.
            if (!useStatic)
            {
                instructions.Add(processor.Create(OpCodes.Ldarg_0)); //argument: this
                //If (!base.IsClient)
                instructions.Add(processor.Create(OpCodes.Call, NetworkBehaviour_IsClient_MethodRef));
            }
            //Checking instanceFinder.
            else
            {
                instructions.Add(processor.Create(OpCodes.Call, InstanceFinder_IsClient_MethodRef));
            }
            instructions.Add(processor.Create(OpCodes.Brtrue, endIf));
            //If warning then also append warning text.
            if (loggingType != LoggingType.Off)
            {
                string msg = $"Cannot complete action because client is not active. This may also occur if the object is not yet initialized or if it does not contain a NetworkObject component. {DISABLE_LOGGING_TEXT}.";
                instructions.AddRange(
                    CodegenSession.GeneralHelper.CreateDebugWithCanLogInstructions(processor, msg, loggingType, useStatic, true)
                    );
            }
            //Add return.
            instructions.AddRange(CreateRetDefault(methodDef));
            //After if statement, jumped to when successful check.
            instructions.Add(endIf);

            if (insertFirst)
            {
                processor.InsertFirst(instructions);
            }
            else
            {
                foreach (Instruction inst in instructions)
                    processor.Append(inst);
            }
        }


        /// <summary>
        /// Creates exit method condition if not server.
        /// </summary>
        /// <param name="processor"></param>
        /// <param name="warn"></param>
        internal void CreateIsServerCheck(MethodDefinition methodDef, LoggingType loggingType, bool useStatic, bool insertFirst)
        {
            /* This is placed after the if check.
            * Should the if check pass then code
            * jumps to this instruction. */
            ILProcessor processor = methodDef.Body.GetILProcessor();
            Instruction endIf = processor.Create(OpCodes.Nop);

            List<Instruction> instructions = new List<Instruction>();
            if (!useStatic)
            {
                instructions.Add(processor.Create(OpCodes.Ldarg_0)); //argument: this
                //If (!base.IsServer)
                instructions.Add(processor.Create(OpCodes.Call, NetworkBehaviour_IsServer_MethodRef));
            }
            //Checking instanceFinder.
            else
            {
                instructions.Add(processor.Create(OpCodes.Call, InstanceFinder_IsServer_MethodRef));
            }
            instructions.Add(processor.Create(OpCodes.Brtrue, endIf));
            //If warning then also append warning text.
            if (loggingType != LoggingType.Off)
            {
                string msg = $"Cannot complete action because server is not active. This may also occur if the object is not yet initialized or if it does not contain a NetworkObject component. {DISABLE_LOGGING_TEXT}";
                instructions.AddRange(
                    CodegenSession.GeneralHelper.CreateDebugWithCanLogInstructions(processor, msg, loggingType, useStatic, true)
                    );
            }
            //Add return.
            instructions.AddRange(CreateRetDefault(methodDef));
            //After if statement, jumped to when successful check.
            instructions.Add(endIf);

            if (insertFirst)
            {
                processor.InsertFirst(instructions);
            }
            else
            {
                foreach (Instruction inst in instructions)
                    processor.Append(inst);
            }
        }

        /// <summary>
        /// Creates a return using the ReturnType for methodDef.
        /// </summary>
        /// <param name="processor"></param>
        /// <param name="methodDef"></param>
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
                VariableDefinition vd = CodegenSession.GeneralHelper.CreateVariable(methodDef, methodDef.ReturnType);
                instructions.Add(processor.Create(OpCodes.Ldloca_S, vd));
                instructions.Add(processor.Create(OpCodes.Initobj, vd.VariableType));
                instructions.Add(processor.Create(OpCodes.Ldloc, vd));
            }
            instructions.Add(processor.Create(OpCodes.Ret));

            return instructions;
        }
    }
}