
using FishNet.CodeGenerating.Helping;
using FishNet.CodeGenerating.Helping.Extension;
using FishNet.Connection;
using FishNet.Object;
using FishNet.Object.Helping;
using FishNet.Transporting;
using Mono.Cecil;
using Mono.Cecil.Cil;
using System.Collections.Generic;

namespace FishNet.CodeGenerating.Processing
{
    internal class NetworkBehaviourRpcProcessor
    {

        #region Const.
        private const string LOGIC_PREFIX = "RpcLogic___";
        private const string WRITER_PREFIX = "RpcWriter___";
        private const string READER_PREFIX = "RpcReader___";
        #endregion

        internal bool Process(TypeDefinition typeDef, ref int allRpcCount)
        {
            bool modified = false;

            //All created method definitions.
            List<MethodDefinition> createdMethodDefs = new List<MethodDefinition>();
            //Logic method definitions.
            List<(RpcType, MethodDefinition, MethodDefinition, int)> delegateMethodDefs = new List<(RpcType, MethodDefinition originalMethodDef, MethodDefinition readerMethodDef, int methodHash)>();
            MethodDefinition[] startingMethodDefs = typeDef.Methods.ToArray();
            foreach (MethodDefinition methodDef in startingMethodDefs)
            {
                RpcType rpcType;
                CustomAttribute rpcAttribute = GetRpcAttribute(methodDef, out rpcType);
                if (rpcAttribute == null)
                    continue;

                /* This is a one time check to make sure the rpcType is
                 * a supported value. Multiple methods beyond this rely on the
                 * value being supported. Rather than check in each method a
                 * single check is performed here. */
                if (rpcType != RpcType.Observers && rpcType != RpcType.Server && rpcType != RpcType.Target)
                {
                    CodegenSession.LogError($"RpcType of {rpcType.ToString()} is unhandled.");
                    continue;
                }

                //Create methods for users method.
                MethodDefinition writerMethodDef, readerMethodDef, logicMethodDef;
                CreateRpcMethods(typeDef, methodDef, rpcAttribute, rpcType, allRpcCount, out writerMethodDef, out readerMethodDef, out logicMethodDef);

                if (writerMethodDef != null && readerMethodDef != null && logicMethodDef != null)
                {
                    createdMethodDefs.AddRange(new MethodDefinition[] { writerMethodDef, readerMethodDef, logicMethodDef });
                    delegateMethodDefs.Add((rpcType, methodDef, readerMethodDef, allRpcCount));
                    allRpcCount++;
                }
            }

            if (createdMethodDefs.Count > 0)
            {
                bool constructorCreated;
                MethodDefinition constructorMethodDef = CodegenSession.GeneralHelper.GetOrCreateConstructor(typeDef, out constructorCreated, false);

                /* Add each method to typeDef. This also
                 * initializes them with properties related to
                 * the typeDef, such as Module. */
                foreach (var md in createdMethodDefs)
                    typeDef.Methods.Add(md);

                ILProcessor constructorProcesser = constructorMethodDef.Body.GetILProcessor();
                //NetworkObject.Create_____Delegate.
                foreach ((RpcType rpcType, MethodDefinition originalMethodDef, MethodDefinition readerMethodDef, int methodHash) in delegateMethodDefs)
                    CodegenSession.ObjectHelper.CreateRpcDelegate(constructorProcesser, originalMethodDef, readerMethodDef, rpcType, methodHash);

                modified = true;
            }

            return modified;
        }

        /// <summary>
        /// Returns the RPC attribute on a method, if one exist. Otherwise returns null.
        /// </summary>
        /// <param name="methodDef"></param>
        /// <param name="rpcType"></param>
        /// <returns></returns>
        internal CustomAttribute GetRpcAttribute(MethodDefinition methodDef, out RpcType rpcType)
        {
            CustomAttribute foundAttribute = null;
            rpcType = RpcType.None;
            //Becomes true if an error occurred during this process.
            bool error = false;

            foreach (CustomAttribute customAttribute in methodDef.CustomAttributes)
            {
                RpcType thisRpcType = CodegenSession.AttributeHelper.GetRpcAttributeType(customAttribute.AttributeType.FullName);
                if (thisRpcType != RpcType.None)
                {
                    //A rpc attribute already exist.
                    if (foundAttribute != null)
                    {
                        CodegenSession.LogError($"{methodDef.Name} RPC method cannot have multiple RPC attributes.");
                        error = true;
                    }
                    //Static method.
                    if (methodDef.IsStatic)
                    {
                        CodegenSession.LogError($"{methodDef.Name} RPC method cannot be static.");
                        error = true;
                    }
                    //Abstract method.
                    if (methodDef.IsAbstract)
                    {
                        CodegenSession.LogError($"{methodDef.Name} RPC method cannot be abstract.");
                        error = true;
                    }
                    //Non void return.
                    if (methodDef.ReturnType != methodDef.Module.TypeSystem.Void)
                    {
                        CodegenSession.LogError($"{methodDef.Name} RPC method must return void.");
                        error = true;
                    }
                    //TargetRpc but missing correct parameters.
                    if (thisRpcType == RpcType.Target)
                    {
                        if (methodDef.Parameters.Count == 0 || !methodDef.Parameters[0].Is(typeof(NetworkConnection)))
                        {
                            CodegenSession.LogError($"Target RPC {methodDef.Name} must have a NetworkConnection as the first parameter.");
                            error = true;
                        }
                    }

                    //If all checks passed.
                    if (!error)
                    {
                        foundAttribute = customAttribute;
                        rpcType = thisRpcType;
                    }
                }
            }

            if (foundAttribute != null)
            {
                //Make sure all parameters can be serialized.
                for (int i = 0; i < methodDef.Parameters.Count; i++)
                {
                    ParameterDefinition parameterDef = methodDef.Parameters[i];

                    //If NetworkConnection, TargetRpc, and first parameter.
                    if (parameterDef.Is(typeof(NetworkConnection)) && rpcType == RpcType.Target && i == 0)
                        continue;

                    //Can be serialized/deserialized.
                    bool canSerialize = CodegenSession.GeneralHelper.HasSerializerAndDeserializer(parameterDef.ParameterType, true);
                    if (!canSerialize)
                    {
                        CodegenSession.LogError($"RPC method {methodDef.Name} parameter type {parameterDef.ParameterType.FullName} does not support serialization. Use a supported type or create a custom serializer.");
                        error = true;
                    }
                }
            }

            //If an error occurred then reset results.
            if (error)
            {
                foundAttribute = null;
                rpcType = RpcType.None;
            }

            return foundAttribute;
        }

        /// <summary>
        /// Creates all methods needed for a RPC.
        /// </summary>
        /// <param name="originalMethodDef"></param>
        /// <param name="rpcAttribute"></param>
        /// <returns></returns>
        private void CreateRpcMethods(TypeDefinition typeDef, MethodDefinition originalMethodDef, CustomAttribute rpcAttribute, RpcType rpcType, int allRpcCount,
            out MethodDefinition writerMethodDef, out MethodDefinition readerMethodDef, out MethodDefinition logicMethodDef)
        {
            writerMethodDef = null;
            readerMethodDef = null;
            logicMethodDef = null;

            List<ParameterDefinition> writtenParameters = new List<ParameterDefinition>();
            writerMethodDef = CreateRpcWriterMethod(typeDef, originalMethodDef, writtenParameters, rpcAttribute, rpcType, allRpcCount);
            if (writerMethodDef == null)
                return;
            logicMethodDef = CreateRpcLogicMethod(typeDef, originalMethodDef, writtenParameters, rpcType);
            if (logicMethodDef == null)
                return;
            readerMethodDef = CreateRpcReaderMethod(typeDef, originalMethodDef, writtenParameters, logicMethodDef, rpcAttribute, rpcType);
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
        private MethodDefinition CreateRpcWriterMethod(TypeDefinition typeDef, MethodDefinition originalMethodDef, List<ParameterDefinition> writtenParameters, CustomAttribute rpcAttribute, RpcType rpcType, int allRpcCount)
        {
            //Create the method body.
            MethodDefinition createdMethodDef = new MethodDefinition(
                $"{WRITER_PREFIX}{originalMethodDef.Name}",
                MethodAttributes.Private,
                originalMethodDef.Module.TypeSystem.Void);
            typeDef.Methods.Add(createdMethodDef);

            ILProcessor createdProcessor = createdMethodDef.Body.GetILProcessor();
            createdMethodDef.Body.InitLocals = true;

            //Copy parameter expecations into new method.
            for (int i = 0; i < originalMethodDef.Parameters.Count; i++)
                createdMethodDef.Parameters.Add(originalMethodDef.Parameters[i]);

            /* Creates basic ServerRpc and ClientRpc
             * conditions such as if requireOwnership ect..
             * or if (!base.isClient) */
            if (rpcType == RpcType.Server)
                CreateServerRpcConditionsForClient(createdProcessor, createdMethodDef, rpcAttribute);
            else
                CreateClientRpcConditionsForServer(createdProcessor, createdMethodDef);

            /* Create a local Channel variable with Reliable as default value.
             * Channel will always be included when calling send rpc methods. */
            VariableDefinition channelVariableDef = CodegenSession.GeneralHelper.CreateVariable(createdMethodDef, CodegenSession.TransportHelper.Channel_TypeRef);
            CodegenSession.GeneralHelper.SetVariableDefinitionFromInt(createdProcessor, channelVariableDef, 0);

            //Parameters which have been marked as special.
            HashSet<int> specialParameterIndexes = new HashSet<int>();
            /* Set defaults specials. */
            //TargetRpc first parameter is always NetworkConnection for target.
            if (rpcType == RpcType.Target)
                specialParameterIndexes.Add(0);

            //Apply common parameters which all rpcs use.
            SetCommonRpcVariables(createdProcessor, specialParameterIndexes, originalMethodDef, ref channelVariableDef);

            //Add all non special parameters to written.
            for (int i = 0; i < originalMethodDef.Parameters.Count; i++)
            {
                if (!specialParameterIndexes.Contains(i))
                    writtenParameters.Add(originalMethodDef.Parameters[i]);
            }

            //Create a local PooledWriter variable.
            VariableDefinition pooledWriterVariableDef = CodegenSession.WriterHelper.CreatePooledWriter(createdProcessor, createdMethodDef);
            //Create all writer.WriteType() calls. 
            for (int i = 0; i < writtenParameters.Count; i++)
            {
                MethodReference writeMethodRef = CodegenSession.WriterHelper.GetOrCreateFavoredWriteMethodReference(writtenParameters[i].ParameterType, true);
                if (writeMethodRef == null)
                    return null;

                CodegenSession.WriterHelper.CreateWrite(createdProcessor, pooledWriterVariableDef, writtenParameters[i], writeMethodRef);
            }

            //int methodHash = allRpcCount;
            uint methodHash = originalMethodDef.FullName.GetStableHash32();
            //Call the method on NetworkBehaviour responsible for sending out the rpc.
            if (rpcType == RpcType.Server)
                CodegenSession.ObjectHelper.CreateSendServerRpc(createdProcessor, methodHash, pooledWriterVariableDef, channelVariableDef);
            else if (rpcType == RpcType.Observers)
                CodegenSession.ObjectHelper.CreateSendObserversRpc(createdProcessor, methodHash, pooledWriterVariableDef, channelVariableDef);
            else if (rpcType == RpcType.Target)
                CodegenSession.ObjectHelper.CreateSendTargetRpc(createdProcessor, methodHash, pooledWriterVariableDef, channelVariableDef, originalMethodDef.Parameters[0]);
            //Dispose of writer.
            CodegenSession.WriterHelper.DisposePooledWriter(createdProcessor, pooledWriterVariableDef);

            //Add end of method.
            createdProcessor.Emit(OpCodes.Ret);

            return createdMethodDef;
        }

        /// <summary>
        /// Sets local variables common to all RPC types.
        /// </summary>
        /// <param name="specialIndexes">Indexes which have been processed as special parameters.</param>
        private void SetCommonRpcVariables(ILProcessor processor, HashSet<int> specialIndexes, MethodDefinition methodDef, ref VariableDefinition channelVariableDef)
        {
            if (methodDef.Parameters.Count > 0)
            {
                int count = methodDef.Parameters.Count;
                for (int i = 0; i < count; i++)
                {
                    //Already processed as a special index.
                    if (specialIndexes.Contains(i))
                        continue;

                    ParameterDefinition pd = methodDef.Parameters[i];
                    //Channel type and is last parameter.
                    if (pd.Is(typeof(Channel)) && (i == count - 1))
                    {
                        CodegenSession.GeneralHelper.SetVariableDefinitionFromParameter(processor, channelVariableDef, pd);
                        specialIndexes.Add(i);
                    }
                }
            }
        }

        /// <summary>
        /// Creates a writer for a RPC.
        /// </summary>
        /// <param name="originalMethodDef"></param>
        /// <param name="rpcAttribute"></param>
        /// <returns></returns>
        private MethodDefinition CreateRpcReaderMethod(TypeDefinition typeDef, MethodDefinition originalMethodDef, List<ParameterDefinition> writtenParameters, MethodDefinition logicMethodDef, CustomAttribute rpcAttribute, RpcType rpcType)
        {
            //Create the method body.
            MethodDefinition createdMethodDef = new MethodDefinition(
                $"{READER_PREFIX}{originalMethodDef.Name}",
                MethodAttributes.Private,
                originalMethodDef.Module.TypeSystem.Void);
            typeDef.Methods.Add(createdMethodDef);

            ILProcessor createdProcessor = createdMethodDef.Body.GetILProcessor();
            createdMethodDef.Body.InitLocals = true;

            //Create PooledReader parameter.
            ParameterDefinition readerParameterDef = CodegenSession.GeneralHelper.CreateParameter(createdMethodDef, CodegenSession.ReaderHelper.PooledReader_TypeRef);
            //If a server rpc also add network connection.
            ParameterDefinition connectionParameterDef = null;
            if (rpcType == RpcType.Server)
                connectionParameterDef = CodegenSession.GeneralHelper.CreateParameter(createdMethodDef, CodegenSession.ReaderHelper.NetworkConnection_TypeRef);

            //Only if ServerRpc.
            /* Add the conditions after the reader pulls all the needed information.
             * This is important because the reader may contain data for other
             * packets as well, so if the client did somehow manage to send a
             * rpc through it needs to be cleared from reader by using Read methods. */
            if (rpcType == RpcType.Server)
                CreateServerRpcConditionsForServer(createdProcessor, rpcAttribute, connectionParameterDef);

            /* Pass all created variables into the logic method. */
            createdProcessor.Emit(OpCodes.Ldarg_0); //this.
            //If also a targetRpc then pass in local connection.
            if (rpcType == RpcType.Target)
            {
                createdProcessor.Emit(OpCodes.Ldarg_0); //this.
                createdProcessor.Emit(OpCodes.Call, CodegenSession.ObjectHelper.NetworkBehaviour_Owner_MethodRef);
            }

            VariableDefinition[] readVariableDefs = new VariableDefinition[writtenParameters.Count];
            List<Instruction> allReadInsts = new List<Instruction>();
            for (int i = 0; i < writtenParameters.Count; i++)
            {
                //Get read instructions and insert it before the return.
                List<Instruction> insts = CodegenSession.ReaderHelper.CreateReadInstructions(createdProcessor, createdMethodDef, readerParameterDef, writtenParameters[i].ParameterType, out readVariableDefs[i]);
                allReadInsts.AddRange(insts);
            }

            bool includeOwner = rpcAttribute.GetField("IncludeOwner", true);
            //If to not include owner then don't call logic.
            if (!includeOwner)
            {
                //Create return if owner.
                Instruction retInst = CodegenSession.ObjectHelper.CreateLocalClientIsOwnerCheck(createdProcessor, LoggingType.Off, true, true);
                //Create variables that take from reader. They arent used but reader must still be flushed of content for this packet.
                createdProcessor.InsertBefore(retInst, allReadInsts);
                //Also add after ret so they can be read if not owner.
                createdProcessor.Add(allReadInsts);
            }
            else
            {
                //Create a read for each type.
                //for (int i = 0; i < writtenParameters.Count; i++)
                    //readVariableDefs[i] = CodegenSession.ReaderHelper.CreateRead(createdProcessor, createdMethodDef, readerParameterDef, writtenParameters[i].ParameterType);
            }

            //Add each read variable as an argument.
            foreach (VariableDefinition vd in readVariableDefs)
                createdProcessor.Emit(OpCodes.Ldloc, vd);
            //Call __Logic method.
            createdProcessor.Emit(OpCodes.Call, logicMethodDef);

            //Add end of method.
            createdProcessor.Emit(OpCodes.Ret);

            return createdMethodDef;
        }

        /// <summary>
        /// Creates conditions that clients must pass to send a ServerRpc.
        /// </summary>
        /// <param name="createdProcessor"></param>
        /// <param name="rpcAttribute"></param>
        private void CreateServerRpcConditionsForClient(ILProcessor createdProcessor, MethodDefinition methodDef, CustomAttribute rpcAttribute)
        {
            bool requireOwnership = rpcAttribute.GetField("RequireOwnership", true);
            //If (!base.IsOwner);
            if (requireOwnership)
                CodegenSession.ObjectHelper.CreateLocalClientIsOwnerCheck(createdProcessor, LoggingType.Warn, false, true);
            //If (!base.IsClient)
            CodegenSession.ObjectHelper.CreateIsClientCheck(createdProcessor, methodDef, LoggingType.Warn, true, true);
        }

        /// <summary>
        /// Creates conditions that server must pass to process a ServerRpc.
        /// </summary>
        /// <param name="createdProcessor"></param>
        /// <param name="rpcAttribute"></param>
        /// <param name="rpcType"></param>
        private void CreateServerRpcConditionsForServer(ILProcessor createdProcessor, CustomAttribute rpcAttribute, ParameterDefinition connectionParametereDef)
        {
            bool requireOwnership = rpcAttribute.GetField("RequireOwnership", true);
            /* Don't need to check if server on receiving end.
             * Next compare connection with owner. */
            //If (!base.Owner);
            if (requireOwnership)
                CodegenSession.ObjectHelper.CreateRemoteClientIsOwnerCheck(createdProcessor, connectionParametereDef);
        }

        /// <summary>
        /// Creates conditions that server must pass to process a ClientRpc.
        /// </summary>
        /// <param name="createdProcessor"></param>
        private void CreateClientRpcConditionsForServer(ILProcessor createdProcessor, MethodDefinition methodDef)
        {
            //If (!base.IsServer)
            CodegenSession.ObjectHelper.CreateIsServerCheck(createdProcessor, methodDef, LoggingType.Warn, true, false);
        }

        /// <summary>
        /// Creates a method containing the logic which will run when receiving the Rpc.
        /// </summary>
        /// <param name="originalMethodDef"></param>
        /// <returns></returns>
        private MethodDefinition CreateRpcLogicMethod(TypeDefinition typeDef, MethodDefinition originalMethodDef, List<ParameterDefinition> writtenParameters, RpcType rpcType)
        {
            //Create the method body.
            MethodDefinition createdMethodDef = new MethodDefinition(
                $"{LOGIC_PREFIX}{originalMethodDef.Name}", originalMethodDef.Attributes, originalMethodDef.ReturnType);
            typeDef.Methods.Add(createdMethodDef);

            createdMethodDef.Body.InitLocals = true;

            /* Some rpc types require special parameters which aren't explicitly
             * written to PooledWriter. Add those in the proper order. */
            if (rpcType == RpcType.Target)
                createdMethodDef.Parameters.Add(originalMethodDef.Parameters[0]);
            //Copy parameter expecations into new method.
            foreach (ParameterDefinition pd in writtenParameters)
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
            originalProcessor.Emit(OpCodes.Call, writerMethodDef);
            originalProcessor.Emit(OpCodes.Ret);
        }
    }
}