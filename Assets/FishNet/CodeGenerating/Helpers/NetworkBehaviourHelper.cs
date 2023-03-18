using FishNet.CodeGenerating.Helping.Extension;
using FishNet.CodeGenerating.Processing;
using FishNet.Configuring;
using FishNet.Managing.Logging;
using FishNet.Object;
using FishNet.Object.Delegating;
using FishNet.Object.Helping;
using FishNet.Object.Prediction.Delegating;
using MonoFN.Cecil;
using MonoFN.Cecil.Cil;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace FishNet.CodeGenerating.Helping
{
    internal class NetworkBehaviourHelper : CodegenBase
    {
        #region Reflection references.
        //Names.
        internal string FullName;
        //Prediction.
        public string ClearReplicateCache_Method_Name = nameof(NetworkBehaviour.ClearReplicateCache_Internal);
        public MethodReference Replicate_Server_MethodRef;
        public MethodReference Replicate_Client_MethodRef;
        public MethodReference Replicate_Reader_MethodRef;
        public MethodReference Replicate_ExitEarly_A_MethodRef;
        public MethodReference Reconcile_ExitEarly_A_MethodRef;
        public MethodReference Reconcile_Server_MethodRef;
        public MethodReference Reconcile_Client_MethodRef;
        public MethodReference Reconcile_Reader_MethodRef;
        public MethodReference RegisterReplicateRpc_MethodRef;
        public MethodReference RegisterReconcileRpc_MethodRef;
        public MethodReference ReplicateRpcDelegateConstructor_MethodRef;
        public MethodReference ReconcileRpcDelegateConstructor_MethodRef;
        //RPCs.
        public MethodReference SendServerRpc_MethodRef;
        public MethodReference SendObserversRpc_MethodRef;
        public MethodReference SendTargetRpc_MethodRef;
        public MethodReference DirtySyncType_MethodRef;
        public MethodReference RegisterServerRpc_MethodRef;
        public MethodReference RegisterObserversRpc_MethodRef;
        public MethodReference RegisterTargetRpc_MethodRef;
        public MethodReference ServerRpcDelegateConstructor_MethodRef;
        public MethodReference ClientRpcDelegateConstructor_MethodRef;
        //Is checks.
        public MethodReference IsClient_MethodRef;
        public MethodReference IsOwner_MethodRef;
        public MethodReference IsServer_MethodRef;
        public MethodReference IsHost_MethodRef;
        //Misc.
        public TypeReference TypeRef;
        public MethodReference OwnerMatches_MethodRef;
        public MethodReference LocalConnection_MethodRef;
        public MethodReference Owner_MethodRef;
        public MethodReference ReadSyncVar_MethodRef;
        public MethodReference NetworkInitializeIfDisabled_MethodRef;
        //TimeManager.
        public MethodReference TimeManager_MethodRef;
        #endregion

        #region Const.
        internal const uint MAX_RPC_ALLOWANCE = ushort.MaxValue;
        internal const string AWAKE_METHOD_NAME = "Awake";
        internal const string DISABLE_LOGGING_TEXT = "This message may be disabled by setting the Logging field in your attribute to LoggingType.Off";
        #endregion

        public override bool ImportReferences()
        {
            Type networkBehaviourType = typeof(NetworkBehaviour);
            TypeRef = base.ImportReference(networkBehaviourType);
            FullName = networkBehaviourType.FullName;
            base.ImportReference(networkBehaviourType);

            //ServerRpcDelegate and ClientRpcDelegate constructors.
            ServerRpcDelegateConstructor_MethodRef = base.ImportReference(typeof(ServerRpcDelegate).GetConstructors().First());
            ClientRpcDelegateConstructor_MethodRef = base.ImportReference(typeof(ClientRpcDelegate).GetConstructors().First());
            //Prediction Rpc delegate constructors.
            ReplicateRpcDelegateConstructor_MethodRef = base.ImportReference(typeof(ReplicateRpcDelegate).GetConstructors().First());
            ReconcileRpcDelegateConstructor_MethodRef = base.ImportReference(typeof(ReconcileRpcDelegate).GetConstructors().First());

            foreach (MethodInfo mi in networkBehaviourType.GetMethods((BindingFlags.Static | BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic)))
            {
                //CreateDelegates.
                if (mi.Name == nameof(NetworkBehaviour.RegisterServerRpc_Internal))
                    RegisterServerRpc_MethodRef = base.ImportReference(mi);
                else if (mi.Name == nameof(NetworkBehaviour.RegisterObserversRpc_Internal))
                    RegisterObserversRpc_MethodRef = base.ImportReference(mi);
                else if (mi.Name == nameof(NetworkBehaviour.RegisterTargetRpc_Internal))
                    RegisterTargetRpc_MethodRef = base.ImportReference(mi);
                //SendPredictions.
                else if (mi.Name == nameof(NetworkBehaviour.RegisterReplicateRpc_Internal))
                    RegisterReplicateRpc_MethodRef = base.ImportReference(mi);
                else if (mi.Name == nameof(NetworkBehaviour.RegisterReconcileRpc_Internal))
                    RegisterReconcileRpc_MethodRef = base.ImportReference(mi);
                //SendRpcs.
                else if (mi.Name == nameof(NetworkBehaviour.SendServerRpc_Internal))
                    SendServerRpc_MethodRef = base.ImportReference(mi);
                else if (mi.Name == nameof(NetworkBehaviour.SendObserversRpc_Internal))
                    SendObserversRpc_MethodRef = base.ImportReference(mi);
                else if (mi.Name == nameof(NetworkBehaviour.SendTargetRpc_Internal))
                    SendTargetRpc_MethodRef = base.ImportReference(mi);
                //Prediction.
                else if (mi.Name == nameof(NetworkBehaviour.Replicate_ExitEarly_A_Internal))
                    Replicate_ExitEarly_A_MethodRef = base.ImportReference(mi);
                else if (mi.Name == nameof(NetworkBehaviour.Replicate_Server_Internal))
                    Replicate_Server_MethodRef = base.ImportReference(mi);
                else if (mi.Name == nameof(NetworkBehaviour.Replicate_Reader_Internal))
                    Replicate_Reader_MethodRef = base.ImportReference(mi);
                else if (mi.Name == nameof(NetworkBehaviour.Reconcile_Reader_Internal))
                    Reconcile_Reader_MethodRef = base.ImportReference(mi);
                else if (mi.Name == nameof(NetworkBehaviour.Reconcile_ExitEarly_A_Internal))
                    Reconcile_ExitEarly_A_MethodRef = base.ImportReference(mi);
                else if (mi.Name == nameof(NetworkBehaviour.Reconcile_Server_Internal))
                    Reconcile_Server_MethodRef = base.ImportReference(mi);
                else if (mi.Name == nameof(NetworkBehaviour.Replicate_Client_Internal))
                    Replicate_Client_MethodRef = base.ImportReference(mi);
                else if (mi.Name == nameof(NetworkBehaviour.Reconcile_Client_Internal))
                    Reconcile_Client_MethodRef = base.ImportReference(mi);
                //Misc.
                else if (mi.Name == nameof(NetworkBehaviour.OwnerMatches))
                    OwnerMatches_MethodRef = base.ImportReference(mi);
                else if (mi.Name == nameof(NetworkBehaviour.ReadSyncVar))
                    ReadSyncVar_MethodRef = base.ImportReference(mi);
                else if (mi.Name == nameof(NetworkBehaviour.DirtySyncType))
                    DirtySyncType_MethodRef = base.ImportReference(mi);
                else if (mi.Name == nameof(NetworkBehaviour.NetworkInitializeIfDisabled))
                    NetworkInitializeIfDisabled_MethodRef = base.ImportReference(mi);
            }

            foreach (PropertyInfo pi in networkBehaviourType.GetProperties((BindingFlags.Static | BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic)))
            {
                //Server/Client states.
                if (pi.Name == nameof(NetworkBehaviour.IsClient))
                    IsClient_MethodRef = base.ImportReference(pi.GetMethod);
                else if (pi.Name == nameof(NetworkBehaviour.IsServer))
                    IsServer_MethodRef = base.ImportReference(pi.GetMethod);
                else if (pi.Name == nameof(NetworkBehaviour.IsHost))
                    IsHost_MethodRef = base.ImportReference(pi.GetMethod);
                else if (pi.Name == nameof(NetworkBehaviour.IsOwner))
                    IsOwner_MethodRef = base.ImportReference(pi.GetMethod);
                //Owner.
                else if (pi.Name == nameof(NetworkBehaviour.Owner))
                    Owner_MethodRef = base.ImportReference(pi.GetMethod);
                else if (pi.Name == nameof(NetworkBehaviour.LocalConnection))
                    LocalConnection_MethodRef = base.ImportReference(pi.GetMethod);
                //Misc.
                else if (pi.Name == nameof(NetworkBehaviour.TimeManager))
                    TimeManager_MethodRef = base.ImportReference(pi.GetMethod);
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
            insts.Add(processor.Create(OpCodes.Ldc_I4, (int)methodHash));

            /* Create delegate and call NetworkBehaviour method. */
            insts.Add(processor.Create(OpCodes.Ldarg_0));
            insts.Add(processor.Create(OpCodes.Ldftn, readerMethodDef));
            //Server.
            if (rpcType == RpcType.Server)
            {
                insts.Add(processor.Create(OpCodes.Newobj, ServerRpcDelegateConstructor_MethodRef));
                insts.Add(processor.Create(OpCodes.Call, RegisterServerRpc_MethodRef));
            }
            //Observers.
            else if (rpcType == RpcType.Observers)
            {
                insts.Add(processor.Create(OpCodes.Newobj, ClientRpcDelegateConstructor_MethodRef));
                insts.Add(processor.Create(OpCodes.Call, RegisterObserversRpc_MethodRef));
            }
            //Target
            else if (rpcType == RpcType.Target)
            {
                insts.Add(processor.Create(OpCodes.Newobj, ClientRpcDelegateConstructor_MethodRef));
                insts.Add(processor.Create(OpCodes.Call, RegisterTargetRpc_MethodRef));
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
        internal Instruction CreateLocalClientIsOwnerCheck(MethodDefinition methodDef, LoggingType loggingType, bool notifyMessageCanBeDisabled, bool retIfOwner, bool insertFirst)
        {
            List<Instruction> instructions = new List<Instruction>();
            /* This is placed after the if check.
             * Should the if check pass then code
             * jumps to this instruction. */
            ILProcessor processor = methodDef.Body.GetILProcessor();
            Instruction endIf = processor.Create(OpCodes.Nop);

            instructions.Add(processor.Create(OpCodes.Ldarg_0)); //argument: this
            //If !base.IsOwner endIf.
            instructions.Add(processor.Create(OpCodes.Call, IsOwner_MethodRef));
            if (retIfOwner)
                instructions.Add(processor.Create(OpCodes.Brfalse, endIf));
            else
                instructions.Add(processor.Create(OpCodes.Brtrue, endIf));
            //If logging is not disabled.
            if (loggingType != LoggingType.Off)
            {
                string disableLoggingText = (notifyMessageCanBeDisabled) ? DISABLE_LOGGING_TEXT : string.Empty;
                string msg = (retIfOwner) ?
                    $"Cannot complete action because you are the owner of this object. {disableLoggingText}." :
                    $"Cannot complete action because you are not the owner of this object. {disableLoggingText}.";

                instructions.AddRange(base.GetClass<GeneralHelper>().LogMessage(methodDef, msg, loggingType));
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
            processor.Emit(OpCodes.Call, OwnerMatches_MethodRef);
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
                instructions.Add(processor.Create(OpCodes.Call, IsClient_MethodRef));
            }
            //Checking instanceFinder.
            else
            {
                instructions.Add(processor.Create(OpCodes.Call, base.GetClass<ObjectHelper>().InstanceFinder_IsClient_MethodRef));
            }
            instructions.Add(processor.Create(OpCodes.Brtrue, endIf));
            //If warning then also append warning text.
            if (loggingType != LoggingType.Off)
            {
                string msg = $"Cannot complete action because client is not active. This may also occur if the object is not yet initialized or if it does not contain a NetworkObject component. {DISABLE_LOGGING_TEXT}.";
                instructions.AddRange(base.GetClass<GeneralHelper>().LogMessage(methodDef, msg, loggingType));
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
                instructions.Add(processor.Create(OpCodes.Call, IsServer_MethodRef));
            }
            //Checking instanceFinder.
            else
            {
                instructions.Add(processor.Create(OpCodes.Call, base.GetClass<ObjectHelper>().InstanceFinder_IsServer_MethodRef));
            }
            instructions.Add(processor.Create(OpCodes.Brtrue, endIf));
            //If warning then also append warning text.
            if (loggingType != LoggingType.Off)
            {
                string msg = $"Cannot complete action because server is not active. This may also occur if the object is not yet initialized or if it does not contain a NetworkObject component. {DISABLE_LOGGING_TEXT}";
                instructions.AddRange(base.GetClass<GeneralHelper>().LogMessage(methodDef, msg, loggingType));
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
                VariableDefinition vd = base.GetClass<GeneralHelper>().CreateVariable(methodDef, methodDef.ReturnType);
                instructions.Add(processor.Create(OpCodes.Ldloca_S, vd));
                instructions.Add(processor.Create(OpCodes.Initobj, vd.VariableType));
                instructions.Add(processor.Create(OpCodes.Ldloc, vd));
            }
            instructions.Add(processor.Create(OpCodes.Ret));

            return instructions;
        }
    }
}