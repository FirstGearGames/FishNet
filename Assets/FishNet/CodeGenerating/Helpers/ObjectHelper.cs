using FishNet.Broadcast;
using FishNet.Object.Helping;
using FishNet.CodeGenerating.Helping.Extension;
using FishNet.CodeGenerating.Processing;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Object.Synchronizing.Internal;
using Mono.Cecil;
using Mono.Cecil.Cil;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace FishNet.CodeGenerating.Helping
{
    internal class ObjectHelper
    {
        #region Reflection references.
        internal string NetworkBehaviour_FullName;
        internal string IBroadcast_FullName;
        internal string SyncList_FullName;
        private MethodReference NetworkBehaviour_CreateServerRpcDelegate_MethodRef;
        private MethodReference NetworkBehaviour_CreateObserversRpcDelegate_MethodRef;
        private MethodReference NetworkBehaviour_CreateTargetRpcDelegate_MethodRef;
        private MethodReference Networkbehaviour_ServerRpcDelegateDelegateConstructor_MethodRef;
        private MethodReference Networkbehaviour_ClientRpcDelegateDelegateConstructor_MethodRef;
        private MethodReference NetworkBehaviour_SendServerRpc_MethodRef;
        private MethodReference NetworkBehaviour_SendObserversRpc_MethodRef;
        private MethodReference NetworkBehaviour_SendTargetRpc_MethodRef;
        private MethodReference NetworkBehaviour_IsClient_MethodRef;
        private MethodReference InstanceFinder_IsClient_MethodRef;
        private MethodReference NetworkBehaviour_IsServer_MethodRef;
        private MethodReference InstanceFinder_IsServer_MethodRef;
        private MethodReference NetworkBehaviour_IsHost_MethodRef;
        private MethodReference NetworkBehaviour_IsOwner_MethodRef;
        private MethodReference NetworkBehaviour_CompareOwner_MethodRef;
        private MethodReference NetworkBehaviour_OwnerIsValid_MethodRef;
        internal MethodReference NetworkBehaviour_Owner_MethodRef;
        internal MethodReference NetworkBehaviour_ReadSyncVar_MethodRef;
        internal MethodReference NetworkBehaviour_UsingOnStartServerInternal_MethodRef;
        internal MethodReference NetworkBehaviour_UsingOnStopServerInternal_MethodRef;
        internal MethodReference NetworkBehaviour_UsingOnOwnershipServerInternal_MethodRef;
        internal MethodReference NetworkBehaviour_UsingOnSpawnServerInternal_MethodRef;
        internal MethodReference NetworkBehaviour_UsingOnDespawnServerInternal_MethodRef;
        internal MethodReference NetworkBehaviour_UsingOnStartClientInternal_MethodRef;
        internal MethodReference NetworkBehaviour_UsingOnStopClientInternal_MethodRef;
        internal MethodReference NetworkBehaviour_UsingOnOwnershipClientInternal_MethodRef;
        internal MethodReference NetworkBehaviour_SetRpcMethodCountInternal_MethodRef;
        internal MethodReference Dictionary_Add_UShort_SyncBase_MethodRef;
        #endregion

        #region Const.
        internal const string AWAKE_METHOD_NAME = "Awake";
        #endregion

        internal bool ImportReferences()
        {
            Type networkBehaviourType = typeof(NetworkBehaviour);
            NetworkBehaviour_FullName = networkBehaviourType.FullName;
            CodegenSession.Module.ImportReference(networkBehaviourType);

            Type ibroadcastType = typeof(IBroadcast);
            CodegenSession.Module.ImportReference(ibroadcastType);
            IBroadcast_FullName = ibroadcastType.FullName;

            Type syncListType = typeof(SyncList<>);
            CodegenSession.Module.ImportReference(syncListType);
            SyncList_FullName = syncListType.FullName;

            //Dictionary.Add(ushort, SyncBase).
            System.Type dictType = typeof(Dictionary<ushort, SyncBase>);
            TypeReference dictTypeRef = CodegenSession.Module.ImportReference(dictType);
            //Dictionary_Add_UShort_SyncBase_MethodRef = dictTypeRef.Resolve().GetMethod("add_Item", )
            foreach (MethodDefinition item in dictTypeRef.Resolve().Methods)
            {
                if (item.Name == nameof(Dictionary<ushort, SyncBase>.Add))
                {
                    Dictionary_Add_UShort_SyncBase_MethodRef = CodegenSession.Module.ImportReference(item);
                    break;
                }
            }

            //InstanceFinder methods.
            Type instanceFinderType = typeof(InstanceFinder);
            foreach (PropertyInfo pi in instanceFinderType.GetProperties())
            {
                if (pi.Name == nameof(InstanceFinder.IsClient))
                    InstanceFinder_IsClient_MethodRef = CodegenSession.Module.ImportReference(pi.GetMethod);
                else if (pi.Name == nameof(InstanceFinder.IsServer))
                    InstanceFinder_IsServer_MethodRef = CodegenSession.Module.ImportReference(pi.GetMethod);
            }

            //ServerRpcDelegate and ClientRpcDelegate constructors.
            Networkbehaviour_ServerRpcDelegateDelegateConstructor_MethodRef = CodegenSession.Module.ImportReference(typeof(ServerRpcDelegate).GetConstructors().First());
            Networkbehaviour_ClientRpcDelegateDelegateConstructor_MethodRef = CodegenSession.Module.ImportReference(typeof(ClientRpcDelegate).GetConstructors().First());

            foreach (MethodInfo methodInfo in networkBehaviourType.GetMethods((BindingFlags.Static | BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic)))
            {
                //CreateDelegates.
                if (methodInfo.Name == nameof(NetworkBehaviour.CreateServerRpcDelegate))
                    NetworkBehaviour_CreateServerRpcDelegate_MethodRef = CodegenSession.Module.ImportReference(methodInfo);
                else if (methodInfo.Name == nameof(NetworkBehaviour.CreateObserversRpcDelegate))
                    NetworkBehaviour_CreateObserversRpcDelegate_MethodRef = CodegenSession.Module.ImportReference(methodInfo);
                else if (methodInfo.Name == nameof(NetworkBehaviour.CreateTargetRpcDelegate))
                    NetworkBehaviour_CreateTargetRpcDelegate_MethodRef = CodegenSession.Module.ImportReference(methodInfo);
                //SendRpcs.
                else if (methodInfo.Name == nameof(NetworkBehaviour.SendServerRpc))
                    NetworkBehaviour_SendServerRpc_MethodRef = CodegenSession.Module.ImportReference(methodInfo);
                else if (methodInfo.Name == nameof(NetworkBehaviour.SendObserversRpc))
                    NetworkBehaviour_SendObserversRpc_MethodRef = CodegenSession.Module.ImportReference(methodInfo);
                else if (methodInfo.Name == nameof(NetworkBehaviour.SendTargetRpc))
                    NetworkBehaviour_SendTargetRpc_MethodRef = CodegenSession.Module.ImportReference(methodInfo);
                //NetworkObject/NetworkBehaviour Callbacks.
                else if (methodInfo.Name == nameof(NetworkBehaviour.UsingOnStartServerInternal))
                    NetworkBehaviour_UsingOnStartServerInternal_MethodRef = CodegenSession.Module.ImportReference(methodInfo);
                else if (methodInfo.Name == nameof(NetworkBehaviour.UsingOnStopServerInternal))
                    NetworkBehaviour_UsingOnStopServerInternal_MethodRef = CodegenSession.Module.ImportReference(methodInfo);
                else if (methodInfo.Name == nameof(NetworkBehaviour.UsingOnOwnershipServerInternal))
                    NetworkBehaviour_UsingOnOwnershipServerInternal_MethodRef = CodegenSession.Module.ImportReference(methodInfo);
                else if (methodInfo.Name == nameof(NetworkBehaviour.UsingOnSpawnServerInternal))
                    NetworkBehaviour_UsingOnSpawnServerInternal_MethodRef = CodegenSession.Module.ImportReference(methodInfo);
                else if (methodInfo.Name == nameof(NetworkBehaviour.UsingOnDespawnServerInternal))
                    NetworkBehaviour_UsingOnDespawnServerInternal_MethodRef = CodegenSession.Module.ImportReference(methodInfo);
                else if (methodInfo.Name == nameof(NetworkBehaviour.UsingOnStartClientInternal))
                    NetworkBehaviour_UsingOnStartClientInternal_MethodRef = CodegenSession.Module.ImportReference(methodInfo);
                else if (methodInfo.Name == nameof(NetworkBehaviour.UsingOnStopClientInternal))
                    NetworkBehaviour_UsingOnStopClientInternal_MethodRef = CodegenSession.Module.ImportReference(methodInfo); 
                else if (methodInfo.Name == nameof(NetworkBehaviour.UsingOnOwnershipClientInternal))
                    NetworkBehaviour_UsingOnOwnershipClientInternal_MethodRef = CodegenSession.Module.ImportReference(methodInfo);
                //Misc.
                else if (methodInfo.Name == nameof(NetworkBehaviour.CompareOwner))
                    NetworkBehaviour_CompareOwner_MethodRef = CodegenSession.Module.ImportReference(methodInfo);
                else if (methodInfo.Name == nameof(NetworkBehaviour.ReadSyncVar))
                    NetworkBehaviour_ReadSyncVar_MethodRef = CodegenSession.Module.ImportReference(methodInfo);
                else if (methodInfo.Name == nameof(NetworkBehaviour.SetRpcMethodCount))
                    NetworkBehaviour_SetRpcMethodCountInternal_MethodRef = CodegenSession.Module.ImportReference(methodInfo);
            }

            foreach (PropertyInfo propertyInfo in networkBehaviourType.GetProperties())
            { 
                //Server/Client states.
                if (propertyInfo.Name == nameof(NetworkBehaviour.IsClient))
                    NetworkBehaviour_IsClient_MethodRef = CodegenSession.Module.ImportReference(propertyInfo.GetMethod);
                else if (propertyInfo.Name == nameof(NetworkBehaviour.IsServer))
                    NetworkBehaviour_IsServer_MethodRef = CodegenSession.Module.ImportReference(propertyInfo.GetMethod);
                else if (propertyInfo.Name == nameof(NetworkBehaviour.IsHost))
                    NetworkBehaviour_IsHost_MethodRef = CodegenSession.Module.ImportReference(propertyInfo.GetMethod);
                else if (propertyInfo.Name == nameof(NetworkBehaviour.IsOwner))
                    NetworkBehaviour_IsOwner_MethodRef = CodegenSession.Module.ImportReference(propertyInfo.GetMethod);
                //Owner.
                else if (propertyInfo.Name == nameof(NetworkBehaviour.Owner))
                    NetworkBehaviour_Owner_MethodRef = CodegenSession.Module.ImportReference(propertyInfo.GetMethod);
                else if (propertyInfo.Name == nameof(NetworkBehaviour.OwnerIsValid))
                    NetworkBehaviour_OwnerIsValid_MethodRef = CodegenSession.Module.ImportReference(propertyInfo.GetMethod);
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
        /// Creates a RPC delegate for rpcType.
        /// </summary>
        /// <param name="processor"></param>
        /// <param name="originalMethodDef"></param>
        /// <param name="readerMethodDef"></param>
        /// <param name="rpcType"></param>
        internal void CreateRpcDelegate(ILProcessor processor, MethodDefinition originalMethodDef, MethodDefinition readerMethodDef, RpcType rpcType, int allRpcCount)
        {
            List<Instruction> insts = new List<Instruction>();
            insts.Add(processor.Create(OpCodes.Ldarg_0));

            uint methodHash = originalMethodDef.FullName.GetStableHash32();
            insts.Add(processor.Create(OpCodes.Ldc_I4, (int)methodHash));
            //processor.Emit(OpCodes.Ldc_I4, allRpcCount);

            /* Create delegate and call NetworkBehaviour method. */
            insts.Add(processor.Create(OpCodes.Ldnull));
            insts.Add(processor.Create(OpCodes.Ldftn, readerMethodDef));
            //Server.
            if (rpcType == RpcType.Server)
            {
                insts.Add(processor.Create(OpCodes.Newobj, Networkbehaviour_ServerRpcDelegateDelegateConstructor_MethodRef));
                insts.Add(processor.Create(OpCodes.Call, NetworkBehaviour_CreateServerRpcDelegate_MethodRef));
            }
            //Observers.
            else if (rpcType == RpcType.Observers)
            {
                insts.Add(processor.Create(OpCodes.Newobj, Networkbehaviour_ClientRpcDelegateDelegateConstructor_MethodRef));
                insts.Add(processor.Create(OpCodes.Call, NetworkBehaviour_CreateObserversRpcDelegate_MethodRef));
            }
            //Target
            else if (rpcType == RpcType.Target)
            {
                insts.Add(processor.Create(OpCodes.Newobj, Networkbehaviour_ClientRpcDelegateDelegateConstructor_MethodRef));
                insts.Add(processor.Create(OpCodes.Call, NetworkBehaviour_CreateTargetRpcDelegate_MethodRef));
            }

            /* Has to be done last. This allows the NetworkBehaviour to
             * initialize it's fields first. */
            processor.InsertLast(insts);
        }

        /// <summary>
        /// Creates a call to SendServerRpc on NetworkBehaviour.
        /// </summary>
        /// <param name="writerVariableDef"></param>
        /// <param name="channel"></param>
        internal void CreateSendServerRpc(ILProcessor processor, uint methodHash, VariableDefinition writerVariableDef, VariableDefinition channelVariableDef)
        {
            CreateSendRpcCommon(processor, methodHash, writerVariableDef, channelVariableDef);
            //Call NetworkBehaviour.
            processor.Emit(OpCodes.Call, NetworkBehaviour_SendServerRpc_MethodRef);
        }

        /// <summary>
        /// Creates a call to SendObserversRpc on NetworkBehaviour.
        /// </summary>
        /// <param name="writerVariableDef"></param>
        /// <param name="channel"></param>
        internal void CreateSendObserversRpc(ILProcessor processor, uint methodHash, VariableDefinition writerVariableDef, VariableDefinition channelVariableDef)
        {
            CreateSendRpcCommon(processor, methodHash, writerVariableDef, channelVariableDef);
            //Call NetworkBehaviour.
            processor.Emit(OpCodes.Call, NetworkBehaviour_SendObserversRpc_MethodRef);
        }
        /// <summary>
        /// Creates a call to SendTargetRpc on NetworkBehaviour.
        /// </summary>
        /// <param name="writerVariableDef"></param>
        internal void CreateSendTargetRpc(ILProcessor processor, uint methodHash, VariableDefinition writerVariableDef, VariableDefinition channelVariableDef, ParameterDefinition connectionParameterDef)
        {
            CreateSendRpcCommon(processor, methodHash, writerVariableDef, channelVariableDef);
            //Reference to NetworkConnection.
            processor.Emit(OpCodes.Ldarg, connectionParameterDef);
            //Call NetworkBehaviour.
            processor.Emit(OpCodes.Call, NetworkBehaviour_SendTargetRpc_MethodRef);
        }

        /// <summary>
        /// Writes common properties that all SendRpc methods use.
        /// </summary>
        /// <param name="processor"></param>
        /// <param name="methodHash"></param>
        /// <param name="writerVariableDef"></param>
        /// <param name="channelVariableDef"></param>
        private void CreateSendRpcCommon(ILProcessor processor, uint methodHash, VariableDefinition writerVariableDef, VariableDefinition channelVariableDef)
        {
            processor.Emit(OpCodes.Ldarg_0); // argument: this
            //Hash argument. 
            processor.Emit(OpCodes.Ldc_I4, (int)methodHash);
            //reference to PooledWriter.
            processor.Emit(OpCodes.Ldloc, writerVariableDef);
            //reference to Channel.
            processor.Emit(OpCodes.Ldloc, channelVariableDef);
        }
        /// <summary>
        /// Creates exit method condition if local client is not owner.
        /// </summary>
        /// <param name="processor"></param>
        /// <param name="retIfOwner">True if to ret when not owner. False to ret if owner.</param>
        /// <returns>Returns Ret instruction.</returns>
        internal Instruction CreateLocalClientIsOwnerCheck(ILProcessor processor, LoggingType loggingType, bool retIfOwner, bool insertFirst)
        {
            List<Instruction> instructions = new List<Instruction>();
            /* This is placed after the if check.
             * Should the if check pass then code
             * jumps to this instruction. */
            Instruction endIf = processor.Create(OpCodes.Nop);

            instructions.Add(processor.Create(OpCodes.Ldarg_0)); //argument: this
            //If !base.IsOwner endIf.
            instructions.Add(processor.Create(OpCodes.Call, NetworkBehaviour_IsOwner_MethodRef));
            if (!retIfOwner)
                instructions.Add(processor.Create(OpCodes.Brtrue, endIf));
            else
                instructions.Add(processor.Create(OpCodes.Brfalse, endIf));
            //If warning then also append warning text.
            if (loggingType != LoggingType.Off)
            {
                string msg = (retIfOwner) ?
                    "Cannot complete action because you are the owner of this object." :
                    "Cannot complete action because you are not the owner of this object.";

                if (loggingType == LoggingType.Warn)
                    instructions.AddRange(CodegenSession.GeneralHelper.CreateDebugWarningInstructions(processor, msg));
                else
                    instructions.AddRange(CodegenSession.GeneralHelper.CreateDebugErrorInstructions(processor, msg));
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
        internal void CreateIsClientCheck(ILProcessor processor, MethodDefinition methodDef, LoggingType loggingType, bool useObject, bool insertFirst)
        {
            /* This is placed after the if check.
             * Should the if check pass then code
             * jumps to this instruction. */
            Instruction endIf = processor.Create(OpCodes.Nop);

            List<Instruction> instructions = new List<Instruction>();
            //Checking against the NetworkObject.
            if (useObject)
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
                instructions.AddRange(
                    CodegenSession.GeneralHelper.CreateDebugWarningInstructions(processor, "Cannot complete action because client is not active. This may also occur if the object is not yet initialized or if it does not contain a NetworkObject component.")
                    );
            //Add return.
            instructions.AddRange(CreateReturnDefault(processor, methodDef));
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
        internal void CreateIsServerCheck(ILProcessor processor, MethodDefinition methodDef, LoggingType loggingType, bool useObject, bool insertFirst)
        {
            /* This is placed after the if check.
            * Should the if check pass then code
            * jumps to this instruction. */
            Instruction endIf = processor.Create(OpCodes.Nop);

            List<Instruction> instructions = new List<Instruction>();
            if (useObject)
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
                instructions.AddRange(
                    CodegenSession.GeneralHelper.CreateDebugWarningInstructions(processor, "Cannot complete action because server is not active. This may also occur if the object is not yet initialized or if it does not contain a NetworkObject component.")
                    );
            //Add return.
            instructions.AddRange(CreateReturnDefault(processor, methodDef));
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
        private List<Instruction> CreateReturnDefault(ILProcessor processor, MethodDefinition methodDef)
        {
            List<Instruction> instructions = new List<Instruction>();
            //If requires a value return.
            if (methodDef.ReturnType != methodDef.Module.TypeSystem.Void)
            {
                //Import type first.
                methodDef.Module.ImportReference(methodDef.ReturnType);
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