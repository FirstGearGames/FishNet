
using FishNet.CodeGenerating.Helping;
using FishNet.CodeGenerating.Helping.Extension;
using FishNet.Managing.Logging;
using FishNet.Transporting;
using MonoFN.Cecil;
using MonoFN.Cecil.Cil;
using System;
using System.Collections.Generic;

namespace FishNet.CodeGenerating.Processing
{
    internal class NetworkBehaviourPredictionProcessor
    {

        #region Const.
        private const string LOGIC_PREFIX = "ReplicatedLogic___";
        private const string WRITER_PREFIX = "ReplicatedWriter___";
        private const string READER_PREFIX = "ReplicatedReader___";
        #endregion

        internal bool Process(TypeDefinition typeDef, uint replicationStartCount)
        {
            bool modified = false;

            //Logic method definitions.
            List<(MethodDefinition, MethodDefinition, uint)> delegateMethodDefs = new List<(MethodDefinition originalMethodDef, MethodDefinition readerMethodDef, uint methodHash)>();
            MethodDefinition[] startingMethodDefs = typeDef.Methods.ToArray();
            foreach (MethodDefinition methodDef in startingMethodDefs)
            {
                foreach (CustomAttribute customAttribute in methodDef.CustomAttributes)
                {
                    //Not a replication method.
                    if (!customAttribute.Is(CodegenSession.AttributeHelper.ReplicatedAttribute_FullName))
                        continue;
                    //At max methods.
                    if (replicationStartCount >= byte.MaxValue)
                    {
                        CodegenSession.LogError($"{typeDef.FullName} and inherited types exceed {byte.MaxValue} replicated methods. Only {byte.MaxValue} replicated methods are supported per inheritance hierarchy.");
                        return false;
                    }

                    //Create methods for users method.
                    MethodDefinition writerMethodDef, readerMethodDef, logicMethodDef;
                    CreateReplicateMethods(typeDef, methodDef, replicationStartCount, out writerMethodDef, out readerMethodDef, out logicMethodDef);

                    if (writerMethodDef != null && readerMethodDef != null && logicMethodDef != null)
                    {
                        modified = true;
                        delegateMethodDefs.Add((methodDef, readerMethodDef, replicationStartCount));
                        replicationStartCount++;
                    }
                }

            }

            if (modified)
            {
                //NetworkObject.Create_____Delegate.
                foreach ((MethodDefinition originalMethodDef, MethodDefinition readerMethodDef, uint methodHash) in delegateMethodDefs)
                    CodegenSession.ObjectHelper.CreateReplicateDelegate(originalMethodDef, readerMethodDef, methodHash);

                modified = true;
            }

            return modified;
        }

        /// <summary>
        /// Gets number of RPCs by checking for RPC attributes. This does not perform error checking.
        /// </summary>
        /// <param name="typeDef"></param>
        /// <returns></returns>
        internal uint GetReplicatedCount(TypeDefinition typeDef)
        {
            uint count = 0;
            foreach (MethodDefinition methodDef in typeDef.Methods)
            {
                foreach (CustomAttribute customAttribute in methodDef.CustomAttributes)
                {
                    if (customAttribute.Is(CodegenSession.AttributeHelper.ReplicatedAttribute_FullName))
                        count++;
                }
            }

            return count;
        }


        /// <summary>
        /// Creates all methods needed for a RPC.
        /// </summary>
        /// <param name="originalMethodDef"></param>
        /// <param name="rpcAttribute"></param>
        /// <returns></returns>
        private void CreateReplicateMethods(TypeDefinition typeDef, MethodDefinition originalMethodDef, uint allReplicatedCount,
            out MethodDefinition writerMethodDef, out MethodDefinition readerMethodDef, out MethodDefinition logicMethodDef)
        {
            writerMethodDef = null;
            readerMethodDef = null;
            logicMethodDef = null;

            List<ParameterDefinition> serializedParameters = new List<ParameterDefinition>();
            writerMethodDef = CreateReplicateWriterMethod(typeDef, originalMethodDef, serializedParameters, allReplicatedCount);
            if (writerMethodDef == null)
                return;
            logicMethodDef = CreateRpcLogicMethod(typeDef, originalMethodDef);
            if (logicMethodDef == null)
                return;
            readerMethodDef = CreateReplicateReaderMethod(typeDef, originalMethodDef, logicMethodDef, true);
            if (readerMethodDef == null)
                return;

            RedirectRpcMethod(originalMethodDef, writerMethodDef);
        }

        /// <summary>
        /// Creates a writer for a RPC.
        /// </summary>
        /// <param name="originalMethodDef"></param>
        /// <param name="rpcAttribute"></param>
        /// <returns></returns>
        private MethodDefinition CreateReplicateWriterMethod(TypeDefinition typeDef, MethodDefinition originalMethodDef, List<ParameterDefinition> serializedParameters, uint allReplicatedCount)
        {
            string methodName = $"{WRITER_PREFIX}{originalMethodDef.Name}";
            /* If method already exist then clear it. This
             * can occur when a method needs to be rebuilt due to
             * inheritence, and renumbering the RPC method names. */
            MethodDefinition createdMethodDef = typeDef.GetMethod(methodName);
            //If found.
            if (createdMethodDef != null)
            {
                createdMethodDef.Parameters.Clear();
                createdMethodDef.Body.Instructions.Clear();
            }
            //Doesn't exist, create it.
            else
            {
                //Create the method body.
                createdMethodDef = new MethodDefinition(methodName,
                    MethodAttributes.Private,
                    originalMethodDef.Module.TypeSystem.Void);
                typeDef.Methods.Add(createdMethodDef);
                createdMethodDef.Body.InitLocals = true;
            }


            return CreateServerWriterMethod(typeDef, originalMethodDef, createdMethodDef, allReplicatedCount, serializedParameters);

            return CreateClientWriterMethod(typeDef, originalMethodDef, createdMethodDef, allReplicatedCount, serializedParameters);
        }

        private MethodDefinition CreateServerWriterMethod(TypeDefinition typeDef, MethodDefinition originalMethodDef, MethodDefinition createdMethodDef, uint allReplicatedCount, List<ParameterDefinition> serializedParameters)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Creates Writer method for a TargetRpc.
        /// </summary>
        /// <param name="typeDef"></param>
        /// <param name="originalMethodDef"></param>
        /// <param name="createdMethodDef"></param>
        /// <param name="rpcAttribute"></param>
        /// <param name="allReplicatedCount"></param>
        /// <param name="serializedParameters">Parameters which are serialized.</param>
        /// <returns></returns>
        private MethodDefinition CreateClientWriterMethod(TypeDefinition typeDef, MethodDefinition originalMethodDef, MethodDefinition createdMethodDef, uint allReplicatedCount, List<ParameterDefinition> serializedParameters)
        {
            ILProcessor createdProcessor = createdMethodDef.Body.GetILProcessor();
            //Add all parameters from the original.
            for (int i = 0; i < originalMethodDef.Parameters.Count; i++)
                createdMethodDef.Parameters.Add(originalMethodDef.Parameters[i]);

            /* Creates basic ServerRpc and ClientRpc
             * conditions such as if requireOwnership ect..
             * or if (!base.isClient) */
            //CreateClientRpcConditionsForServer(createdProcessor, createdMethodDef);

            /* Parameters which won't be serialized, such as channel.
             * It's safe to add parameters which are null or
             * not used. */
            HashSet<ParameterDefinition> nonserializedParameters = new HashSet<ParameterDefinition>();

            //Add all parameters which are NOT nonserialized to serializedParameters.
            foreach (ParameterDefinition pd in createdMethodDef.Parameters)
            {
                if (!nonserializedParameters.Contains(pd))
                    serializedParameters.Add(pd);
            }

            //Create a local PooledWriter variable.
            VariableDefinition pooledWriterVariableDef = CodegenSession.WriterHelper.CreatePooledWriter(createdProcessor, createdMethodDef);
            //Create all writer.WriteType() calls. 
            for (int i = 0; i < serializedParameters.Count; i++)
            {
                MethodReference writeMethodRef = CodegenSession.WriterHelper.GetOrCreateFavoredWriteMethodReference(serializedParameters[i].ParameterType, true);
                if (writeMethodRef == null)
                    return null;

                CodegenSession.WriterHelper.CreateWrite(createdProcessor, pooledWriterVariableDef, serializedParameters[i], writeMethodRef);
            }

            uint methodHash = allReplicatedCount;
            //uint methodHash = originalMethodDef.FullName.GetStableHash32();
            /* Call the method on NetworkBehaviour responsible for sending out the rpc. */

            //replicate always send to owner on unreliable.
            //                CodegenSession.ObjectHelper.CreateSendTargetRpc(createdProcessor, methodHash, pooledWriterVariableDef);

            //Dispose of writer.
            CodegenSession.WriterHelper.DisposePooledWriter(createdProcessor, pooledWriterVariableDef);
            //Add end of method.
            createdProcessor.Emit(OpCodes.Ret);

            return createdMethodDef;
        }


        /// <summary>
        /// Creates Writer method for a ServerRpc.
        /// </summary>
        private MethodDefinition CreateToServerWriterMethod(TypeDefinition typeDef, MethodDefinition originalMethodDef, MethodDefinition createdMethodDef, uint allReplicatedCount)
        {
            ILProcessor createdProcessor = createdMethodDef.Body.GetILProcessor();
            //Add all parameters from the original.
            for (int i = 0; i < originalMethodDef.Parameters.Count; i++)
                createdMethodDef.Parameters.Add(originalMethodDef.Parameters[i]);

            /* Creates basic ServerRpc
             * conditions such as if requireOwnership ect..
             * or if (!base.isClient) */
            CreateServerReplicateConditionsForClient(createdProcessor, createdMethodDef);

            //Create a local PooledWriter variable.
            VariableDefinition pooledWriterVariableDef = CodegenSession.WriterHelper.CreatePooledWriter(createdProcessor, createdMethodDef);
            //Create all writer.WriteType() calls. 
            foreach (ParameterDefinition pd in createdMethodDef.Parameters)
            {
                MethodReference writeMethodRef = CodegenSession.WriterHelper.GetOrCreateFavoredWriteMethodReference(pd.ParameterType, true);
                if (writeMethodRef == null)
                    return null;

                CodegenSession.WriterHelper.CreateWrite(createdProcessor, pooledWriterVariableDef, pd, writeMethodRef);
            }

            uint methodHash = allReplicatedCount;

            //Call the method on NetworkBehaviour responsible for sending out the rpc.
            //replicated create send to server.
            //CodegenSession.ObjectHelper.CreateSendServerRpc(createdProcessor, methodHash, pooledWriterVariableDef, channelVariableDef);

            //Dispose of writer.
            CodegenSession.WriterHelper.DisposePooledWriter(createdProcessor, pooledWriterVariableDef);

            //Add end of method.
            createdProcessor.Emit(OpCodes.Ret);

            return createdMethodDef;
        }

        /// <summary>
        /// Creates a Channel VariableDefinition and populates it with parameterDef value if available, otherwise uses Channel.Reliable.
        /// </summary>
        /// <param name="methodDef"></param>
        /// <param name="parameterDef"></param>
        /// <returns></returns>
        private VariableDefinition CreateAndPopulateChannelVariable(MethodDefinition methodDef, ParameterDefinition parameterDef)
        {
            ILProcessor processor = methodDef.Body.GetILProcessor();

            VariableDefinition localChannelVariableDef = CodegenSession.GeneralHelper.CreateVariable(methodDef, typeof(Channel));
            if (parameterDef != null)
                processor.Emit(OpCodes.Ldarg, parameterDef);
            else
                processor.Emit(OpCodes.Ldc_I4, (int)Channel.Reliable);

            //Set to local value.
            processor.Emit(OpCodes.Stloc, localChannelVariableDef);
            return localChannelVariableDef;
        }

        /// <summary>
        /// Creates a reader for a RPC.
        /// </summary>
        /// <param name="originalMethodDef"></param>
        /// <param name="rpcAttribute"></param>
        /// <returns></returns>
        private MethodDefinition CreateReplicateReaderMethod(TypeDefinition typeDef, MethodDefinition originalMethodDef, MethodDefinition logicMethodDef, bool forServer)
        {
            string rolePrefix = (forServer) ? "Server_" : "Client_";
            string methodName = $"{READER_PREFIX}{rolePrefix}{originalMethodDef.Name}";
            /* If method already exist then just return it. This
             * can occur when a method needs to be rebuilt due to
             * inheritence, and renumbering the RPC method names. 
             * The reader method however does not need to be rewritten. */
            MethodDefinition createdMethodDef = typeDef.GetMethod(methodName);
            //If found.
            if (createdMethodDef != null)
                return createdMethodDef;

            //Create the method body.
            createdMethodDef = new MethodDefinition(
                methodName,
                MethodAttributes.Private,
                originalMethodDef.Module.TypeSystem.Void);
            typeDef.Methods.Add(createdMethodDef);

            createdMethodDef.Body.InitLocals = true;

            return CreateServerReaderMethod(typeDef, originalMethodDef, createdMethodDef, logicMethodDef, forServer);
        }


        /// <summary>
        /// Creates a reader for the server side.
        /// </summary>
        /// <param name="originalMethodDef"></param>
        /// <param name="rpcAttribute"></param>
        /// <returns></returns>
        private MethodDefinition CreateServerReaderMethod(TypeDefinition typeDef, MethodDefinition originalMethodDef, MethodDefinition createdMethodDef, MethodDefinition logicMethodDef, bool forServer)
        {
            ILProcessor createdProcessor = createdMethodDef.Body.GetILProcessor();
            //Create PooledReader parameter.
            ParameterDefinition readerParameterDef = CodegenSession.GeneralHelper.CreateParameter(createdMethodDef, CodegenSession.ReaderHelper.PooledReader_TypeRef);
            //Create a connection parameter. The NetworkBehaviour passes this in within the delegate.
            ParameterDefinition connParameterDef = CodegenSession.GeneralHelper.CreateParameter(createdMethodDef, CodegenSession.ReaderHelper.NetworkConnection_TypeRef);
            /* It's very important to read everything
             * from the PooledReader before applying any
             * exit logic. Should the method return before
             * reading the data then anything after the rpc
             * packet will be malformed due to invalid index. */
            VariableDefinition[] readVariableDefs;
            List<Instruction> allReadInsts;
            CreateReadInstructions(createdProcessor, createdMethodDef, readerParameterDef, out readVariableDefs, out allReadInsts);

            Instruction retInst = CodegenSession.ObjectHelper.CreateRemoteClientIsOwnerCheck(createdProcessor, connParameterDef);
            createdProcessor.InsertBefore(retInst, allReadInsts);
            //Read to clear pooledreader.
            createdProcessor.Add(allReadInsts);

            //this.Logic
            createdProcessor.Emit(OpCodes.Ldarg_0);
            //Add each read variable as an argument. 
            foreach (VariableDefinition vd in readVariableDefs)
                createdProcessor.Emit(OpCodes.Ldloc, vd);

            //Call __Logic method.
            createdProcessor.Emit(OpCodes.Call, logicMethodDef);
            createdProcessor.Emit(OpCodes.Ret);

            return createdMethodDef;
        }


        /// <summary>
        /// Creates a reader for ObserversRpc.
        /// </summary>
        /// <param name="originalMethodDef"></param>
        /// <param name="rpcAttribute"></param>
        /// <returns></returns>
        private MethodDefinition CreateClientReplicateReaderMethod(MethodDefinition originalMethodDef, MethodDefinition createdMethodDef, MethodDefinition logicMethodDef)
        {
            ILProcessor createdProcessor = createdMethodDef.Body.GetILProcessor();

            //Create PooledReader parameter.
            ParameterDefinition readerParameterDef = CodegenSession.GeneralHelper.CreateParameter(createdMethodDef, CodegenSession.ReaderHelper.PooledReader_TypeRef);
            /* It's very important to read everything
             * from the PooledReader before applying any
             * exit logic. Should the method return before
             * reading the data then anything after the rpc
             * packet will be malformed due to invalid index. */
            VariableDefinition[] readVariableDefs;
            List<Instruction> allReadInsts;
            CreateReadInstructions(createdProcessor, createdMethodDef, readerParameterDef, out readVariableDefs, out allReadInsts);
            //Read instructions even if not to include owner.
            createdProcessor.Add(allReadInsts);

            createdProcessor.Emit(OpCodes.Ldarg_0); //this.
            //Add each read variable as an argument. 
            foreach (VariableDefinition vd in readVariableDefs)
                createdProcessor.Emit(OpCodes.Ldloc, vd);
            //Call __Logic method.
            createdProcessor.Emit(OpCodes.Call, logicMethodDef);
            createdProcessor.Emit(OpCodes.Ret);

            return createdMethodDef;
        }



        /// <summary>
        /// Creates a read for every writtenParameters and outputs variables read into, and instructions.
        /// </summary>
        /// <param name="createdProcessor"></param>
        /// <param name="createdMethodDef"></param>
        /// <param name="readerParameterDef"></param>
        /// <param name="serializedParameters"></param>
        /// <param name="readVariableDefs"></param>
        /// <param name="allReadInsts"></param>
        private void CreateReadInstructions(ILProcessor createdProcessor, MethodDefinition createdMethodDef, ParameterDefinition readerParameterDef, out VariableDefinition[] readVariableDefs, out List<Instruction> allReadInsts)
        {
            /* It's very important to read everything
            * from the PooledReader before applying any
            * exit logic. Should the method return before
            * reading the data then anything after the rpc
            * packet will be malformed due to invalid index. */
            readVariableDefs = new VariableDefinition[createdMethodDef.Parameters.Count];
            allReadInsts = new List<Instruction>();
            //True if last parameter is a connection and a server rpc.
            for (int i = 0; i < readVariableDefs.Length; i++)
            {
                //Get read instructions and insert it before the return.
                List<Instruction> insts = CodegenSession.ReaderHelper.CreateReadInstructions(createdProcessor, createdMethodDef, readerParameterDef, createdMethodDef.Parameters[i].ParameterType, out readVariableDefs[i]);
                allReadInsts.AddRange(insts);
            }

        }
        /// <summary>
        /// Creates conditions that clients must pass to send a ServerRpc.
        /// </summary>
        /// <param name="createdProcessor"></param>
        /// <param name="rpcAttribute"></param>
        private void CreateServerReplicateConditionsForClient(ILProcessor createdProcessor, MethodDefinition methodDef)
        {
            /* //replicated this wont work because it will fail on server
             * side. client needs its own conditions and so does server. */
            //If (!base.IsOwner);
            CodegenSession.ObjectHelper.CreateLocalClientIsOwnerCheck(createdProcessor, LoggingType.Warning, false, true);
            //If (!base.IsClient)
            CodegenSession.ObjectHelper.CreateIsClientCheck(createdProcessor, methodDef, LoggingType.Warning, false, true);
        }

        /// <summary>
        /// Creates a method containing the logic which will run when receiving the Rpc.
        /// </summary>
        /// <param name="originalMethodDef"></param>
        /// <returns></returns>
        private MethodDefinition CreateRpcLogicMethod(TypeDefinition typeDef, MethodDefinition originalMethodDef)
        {
            string methodName = $"{LOGIC_PREFIX}{originalMethodDef.Name}";
            /* If method already exist then just return it. This
             * can occur when a method needs to be rebuilt due to
             * inheritence, and renumbering the RPC method names. 
             * The logic method however does not need to be rewritten. */
            MethodDefinition createdMethodDef = typeDef.GetMethod(methodName);
            //If found.
            if (createdMethodDef != null)
                return createdMethodDef;

            //Create the method body.
            createdMethodDef = new MethodDefinition(
            methodName, originalMethodDef.Attributes, originalMethodDef.ReturnType);
            typeDef.Methods.Add(createdMethodDef);

            createdMethodDef.Body.InitLocals = true;

            //Copy parameter expecations into new method.
            foreach (ParameterDefinition pd in originalMethodDef.Parameters)
                createdMethodDef.Parameters.Add(pd);

            //Swap bodies.
            (createdMethodDef.Body, originalMethodDef.Body) = (originalMethodDef.Body, createdMethodDef.Body);
            //Move over all the debugging information
            foreach (SequencePoint sequencePoint in originalMethodDef.DebugInformation.SequencePoints)
                createdMethodDef.DebugInformation.SequencePoints.Add(sequencePoint);
            originalMethodDef.DebugInformation.SequencePoints.Clear();

            foreach (CustomDebugInformation customInfo in originalMethodDef.CustomDebugInformations)
                createdMethodDef.CustomDebugInformations.Add(customInfo);
            originalMethodDef.CustomDebugInformations.Clear();
            //Swap debuginformation scope.
            (originalMethodDef.DebugInformation.Scope, createdMethodDef.DebugInformation.Scope) = (createdMethodDef.DebugInformation.Scope, originalMethodDef.DebugInformation.Scope);
            //Allows rpcs to call base methods.
            FixRemoteCallToBaseMethod(createdMethodDef, originalMethodDef);

            return createdMethodDef;
        }

        /// <summary>
        /// Finds and fixes call to base methods within remote calls
        /// <para>For example, changes `base.CmdDoSomething` to `base.UserCode_CmdDoSomething` within `this.UserCode_CmdDoSomething`</para>
        /// </summary>
        /// <param name="type"></param>
        /// <param name="createdMethodDef"></param>
        private void FixRemoteCallToBaseMethod(MethodDefinition createdMethodDef, MethodDefinition originalMethodDef)
        {
            //All logic RPCs end with the logic suffix.
            if (!createdMethodDef.Name.StartsWith(LOGIC_PREFIX))
                return;

            //Gets the original method name to set base calls to.
            string baseRemoteCallName = originalMethodDef.Name;
            TypeDefinition originalTypeDef = originalMethodDef.DeclaringType;

            foreach (Instruction instruction in createdMethodDef.Body.Instructions)
            {
                // if call to base.CmdDoSomething within this.CallCmdDoSomething
                if (CodegenSession.GeneralHelper.IsCallToMethod(instruction, out MethodDefinition calledMethod) && calledMethod.Name == baseRemoteCallName)
                {
                    TypeDefinition baseType = originalTypeDef.BaseType.Resolve();
                    MethodReference baseMethod = baseType.GetMethodInBaseType(baseRemoteCallName);
                    if (baseMethod == null)
                    {
                        CodegenSession.LogError($"Could not find base method for {createdMethodDef.Name}.");
                        return;
                    }

                    if (!baseMethod.Resolve().IsVirtual)
                    {
                        CodegenSession.LogError($"Could not find base method that was virtual {createdMethodDef.Name}.");
                        return;
                    }

                    instruction.Operand = createdMethodDef.Module.ImportReference(baseMethod);
                }
            }
        }


        /// <summary> 
        /// Redirects calls from the original Rpc method to the writer method.
        /// </summary>
        /// <param name="originalMethodDef"></param>
        /// <param name="writerMethodDef"></param>
        private void RedirectRpcMethod(MethodDefinition originalMethodDef, MethodDefinition writerMethodDef)
        {
            ILProcessor originalProcessor = originalMethodDef.Body.GetILProcessor();
            originalMethodDef.Body.Instructions.Clear();

            originalProcessor.Emit(OpCodes.Ldarg_0); //this.
            //Parameters.
            foreach (ParameterDefinition pd in originalMethodDef.Parameters)
                originalProcessor.Emit(OpCodes.Ldarg, pd);

            //Call method.
            MethodReference writerMethodRef = CodegenSession.Module.ImportReference(writerMethodDef);
            originalProcessor.Emit(OpCodes.Call, writerMethodRef);
            originalProcessor.Emit(OpCodes.Ret);
        }
    }
}