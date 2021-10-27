
using FishNet.CodeGenerating.Helping;
using FishNet.CodeGenerating.Helping.Extension;
using FishNet.Connection;
using FishNet.Managing.Logging;
using FishNet.Object.Helping;
using FishNet.Transporting;
using MonoFN.Cecil;
using MonoFN.Cecil.Cil;
using System.Collections.Generic;
using UnityEngine;

namespace FishNet.CodeGenerating.Processing
{
    internal class NetworkBehaviourRpcProcessor
    {

        #region Const.
        private const string LOGIC_PREFIX = "RpcLogic___";
        private const string WRITER_PREFIX = "RpcWriter___";
        private const string READER_PREFIX = "RpcReader___";
        private const string REQUIRE_OWNERSHIP_TEXT = "RequireOwnership";
        private const string INCLUDE_OWNER_TEXT = "IncludeOwner";
        #endregion

        internal bool Process(TypeDefinition typeDef, uint rpcStartCount)
        {
            bool modified = false;

            //Logic method definitions.
            List<(RpcType, MethodDefinition, MethodDefinition, uint, CustomAttribute)> delegateMethodDefs = new List<(RpcType, MethodDefinition originalMethodDef, MethodDefinition readerMethodDef, uint methodHash, CustomAttribute rpcAttribute)>();
            MethodDefinition[] startingMethodDefs = typeDef.Methods.ToArray();
            foreach (MethodDefinition methodDef in startingMethodDefs)
            {
                if (rpcStartCount >= ushort.MaxValue)
                {
                    CodegenSession.LogError($"{typeDef.FullName} and inherited types exceed {ushort.MaxValue} RPC methods. Only {ushort.MaxValue} RPC methods are supported per inheritance hierarchy.");
                    return false;
                }

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
                CreateRpcMethods(typeDef, methodDef, rpcAttribute, rpcType, rpcStartCount, out writerMethodDef, out readerMethodDef, out logicMethodDef);

                if (writerMethodDef != null && readerMethodDef != null && logicMethodDef != null)
                {
                    modified = true;
                    delegateMethodDefs.Add((rpcType, methodDef, readerMethodDef, rpcStartCount, rpcAttribute));
                    rpcStartCount++;
                }
            }

            if (modified)
            {
                //NetworkObject.Create_____Delegate.
                foreach ((RpcType rpcType, MethodDefinition originalMethodDef, MethodDefinition readerMethodDef, uint methodHash, CustomAttribute rpcAttribute) in delegateMethodDefs)
                    CodegenSession.ObjectHelper.CreateRpcDelegate(originalMethodDef, readerMethodDef, rpcType, methodHash, rpcAttribute);

                modified = true;
            }

            return modified;
        }

        /// <summary>
        /// Gets number of RPCs by checking for RPC attributes. This does not perform error checking.
        /// </summary>
        /// <param name="typeDef"></param>
        /// <returns></returns>
        internal uint GetRpcCount(TypeDefinition typeDef)
        {
            uint count = 0;
            foreach (MethodDefinition methodDef in typeDef.Methods)
            {
                foreach (CustomAttribute customAttribute in methodDef.CustomAttributes)
                {
                    RpcType rpcType = CodegenSession.AttributeHelper.GetRpcAttributeType(customAttribute.AttributeType.FullName);
                    if (rpcType != RpcType.None)
                    {
                        count++;
                        break;
                    }
                }
            }

            return count;
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
        private void CreateRpcMethods(TypeDefinition typeDef, MethodDefinition originalMethodDef, CustomAttribute rpcAttribute, RpcType rpcType, uint allRpcCount,
            out MethodDefinition writerMethodDef, out MethodDefinition readerMethodDef, out MethodDefinition logicMethodDef)
        {
            writerMethodDef = null;
            readerMethodDef = null;
            logicMethodDef = null;

            List<ParameterDefinition> serializedParameters = new List<ParameterDefinition>();
            writerMethodDef = CreateRpcWriterMethod(typeDef, originalMethodDef, serializedParameters, rpcAttribute, rpcType, allRpcCount);
            if (writerMethodDef == null)
                return;
            logicMethodDef = CreateRpcLogicMethod(typeDef, originalMethodDef, serializedParameters, rpcType);
            if (logicMethodDef == null)
                return;
            readerMethodDef = CreateRpcReaderMethod(typeDef, originalMethodDef, serializedParameters, logicMethodDef, rpcAttribute, rpcType);
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
        private MethodDefinition CreateRpcWriterMethod(TypeDefinition typeDef, MethodDefinition originalMethodDef, List<ParameterDefinition> serializedParameters, CustomAttribute rpcAttribute, RpcType rpcType, uint allRpcCount)
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

            if (rpcType == RpcType.Server)
                return CreateServerRpcWriterMethod(typeDef, originalMethodDef, createdMethodDef, rpcAttribute, allRpcCount, serializedParameters);
            else if (rpcType == RpcType.Target || rpcType == RpcType.Observers)
                return CreateClientRpcWriterMethod(typeDef, originalMethodDef, createdMethodDef, rpcAttribute, allRpcCount, serializedParameters, rpcType);
            else
                return null;
        }

        /// <summary>
        /// Creates Writer method for a TargetRpc.
        /// </summary>
        /// <param name="typeDef"></param>
        /// <param name="originalMethodDef"></param>
        /// <param name="createdMethodDef"></param>
        /// <param name="rpcAttribute"></param>
        /// <param name="allRpcCount"></param>
        /// <param name="serializedParameters">Parameters which are serialized.</param>
        /// <returns></returns>
        private MethodDefinition CreateClientRpcWriterMethod(TypeDefinition typeDef, MethodDefinition originalMethodDef, MethodDefinition createdMethodDef, CustomAttribute rpcAttribute, uint allRpcCount, List<ParameterDefinition> serializedParameters, RpcType rpcType)
        {
            ILProcessor createdProcessor = createdMethodDef.Body.GetILProcessor();
            //Add all parameters from the original.
            for (int i = 0; i < originalMethodDef.Parameters.Count; i++)
                createdMethodDef.Parameters.Add(originalMethodDef.Parameters[i]);
            //Get channel if it exist, and get target parameter.
            ParameterDefinition channelParameterDef = GetChannelParameter(createdMethodDef, rpcType);

            /* RpcType specific parameters. */
            ParameterDefinition targetConnectionParameterDef = null;
            if (rpcType == RpcType.Target)
                targetConnectionParameterDef = createdMethodDef.Parameters[0];

            /* Creates basic ServerRpc and ClientRpc
             * conditions such as if requireOwnership ect..
             * or if (!base.isClient) */
            CreateClientRpcConditionsForServer(createdProcessor, createdMethodDef);

            /* Parameters which won't be serialized, such as channel.
             * It's safe to add parameters which are null or
             * not used. */
            HashSet<ParameterDefinition> nonserializedParameters = new HashSet<ParameterDefinition>();
            nonserializedParameters.Add(channelParameterDef);
            nonserializedParameters.Add(targetConnectionParameterDef);

            //Add all parameters which are NOT nonserialized to serializedParameters.
            foreach (ParameterDefinition pd in createdMethodDef.Parameters)
            {
                if (!nonserializedParameters.Contains(pd))
                    serializedParameters.Add(pd);
            }

            VariableDefinition channelVariableDef = CreateAndPopulateChannelVariable(createdMethodDef, channelParameterDef);
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

            uint methodHash = allRpcCount;
            //uint methodHash = originalMethodDef.FullName.GetStableHash32();
            /* Call the method on NetworkBehaviour responsible for sending out the rpc. */
            if (rpcType == RpcType.Observers)
                CodegenSession.ObjectHelper.CreateSendObserversRpc(createdProcessor, methodHash, pooledWriterVariableDef, channelVariableDef, rpcAttribute);
            else if (rpcType == RpcType.Target)
                CodegenSession.ObjectHelper.CreateSendTargetRpc(createdProcessor, methodHash, pooledWriterVariableDef, channelVariableDef, targetConnectionParameterDef);

            //Dispose of writer.
            CodegenSession.WriterHelper.DisposePooledWriter(createdProcessor, pooledWriterVariableDef);
            //Add end of method.
            createdProcessor.Emit(OpCodes.Ret);

            return createdMethodDef;
        }


        /// <summary>
        /// Creates Writer method for a ServerRpc.
        /// </summary>
        /// <param name="typeDef"></param>
        /// <param name="originalMethodDef"></param>
        /// <param name="createdMethodDef"></param>
        /// <param name="rpcAttribute"></param>
        /// <param name="allRpcCount"></param>
        /// <param name="serializedParameters">Parameters which are serialized.</param>
        /// <returns></returns>
        private MethodDefinition CreateServerRpcWriterMethod(TypeDefinition typeDef, MethodDefinition originalMethodDef, MethodDefinition createdMethodDef, CustomAttribute rpcAttribute, uint allRpcCount, List<ParameterDefinition> serializedParameters)
        {
            ILProcessor createdProcessor = createdMethodDef.Body.GetILProcessor();
            //Add all parameters from the original.
            for (int i = 0; i < originalMethodDef.Parameters.Count; i++)
                createdMethodDef.Parameters.Add(originalMethodDef.Parameters[i]);
            //Add in channel if it doesnt exist.
            ParameterDefinition channelParameterDef = GetChannelParameter(createdMethodDef, RpcType.Server);

            /* Creates basic ServerRpc
             * conditions such as if requireOwnership ect..
             * or if (!base.isClient) */
            CreateServerRpcConditionsForClient(createdProcessor, createdMethodDef, rpcAttribute);
            //Parameters which won't be serialized, such as channel.
            HashSet<ParameterDefinition> nonserializedParameters = new HashSet<ParameterDefinition>();
            //The network connection parameter might be added as null, this is okay.
            nonserializedParameters.Add(GetNetworkConnectionParameter(createdMethodDef));
            nonserializedParameters.Add(channelParameterDef);

            //Add all parameters which are NOT nonserialized to serializedParameters.
            foreach (ParameterDefinition pd in createdMethodDef.Parameters)
            {
                if (!nonserializedParameters.Contains(pd))
                    serializedParameters.Add(pd);
            }

            VariableDefinition channelVariableDef = CreateAndPopulateChannelVariable(createdMethodDef, channelParameterDef);
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

            uint methodHash = allRpcCount;
            //uint methodHash = originalMethodDef.FullName.GetStableHash32();
            //Call the method on NetworkBehaviour responsible for sending out the rpc.
            CodegenSession.ObjectHelper.CreateSendServerRpc(createdProcessor, methodHash, pooledWriterVariableDef, channelVariableDef);
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
        private MethodDefinition CreateRpcReaderMethod(TypeDefinition typeDef, MethodDefinition originalMethodDef, List<ParameterDefinition> serializedParameters, MethodDefinition logicMethodDef, CustomAttribute rpcAttribute, RpcType rpcType)
        {
            string methodName = $"{READER_PREFIX}{originalMethodDef.Name}";
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

            if (rpcType == RpcType.Server)
                return CreateServerRpcReaderMethod(typeDef, originalMethodDef, createdMethodDef, serializedParameters, logicMethodDef, rpcAttribute);
            else if (rpcType == RpcType.Target || rpcType == RpcType.Observers)
                return CreateClientRpcReaderMethod(originalMethodDef, createdMethodDef, serializedParameters, logicMethodDef, rpcAttribute, rpcType);
            else
                return null;
        }


        /// <summary>
        /// Creates a reader for ServerRpc.
        /// </summary>
        /// <param name="originalMethodDef"></param>
        /// <param name="rpcAttribute"></param>
        /// <returns></returns>
        private MethodDefinition CreateServerRpcReaderMethod(TypeDefinition typeDef, MethodDefinition originalMethodDef, MethodDefinition createdMethodDef, List<ParameterDefinition> serializedParameters, MethodDefinition logicMethodDef, CustomAttribute rpcAttribute)
        {
            ILProcessor createdProcessor = createdMethodDef.Body.GetILProcessor();

            bool requireOwnership = rpcAttribute.GetField(REQUIRE_OWNERSHIP_TEXT, true);
            //Create PooledReader parameter.
            ParameterDefinition readerParameterDef = CodegenSession.GeneralHelper.CreateParameter(createdMethodDef, CodegenSession.ReaderHelper.PooledReader_TypeRef);

            //Add connection parameter to the read method. Internals pass the connection into this.
            ParameterDefinition channelParameterDef = GetOrCreateChannelParameter(createdMethodDef, RpcType.Server);
            ParameterDefinition connectionParameterDef = GetOrCreateNetworkConnectionParameter(createdMethodDef);
            /* It's very important to read everything
             * from the PooledReader before applying any
             * exit logic. Should the method return before
             * reading the data then anything after the rpc
             * packet will be malformed due to invalid index. */
            VariableDefinition[] readVariableDefs;
            List<Instruction> allReadInsts;
            CreateRpcReadInstructions(createdProcessor, createdMethodDef, readerParameterDef, serializedParameters, out readVariableDefs, out allReadInsts);

            Instruction retInst = CreateServerRpcConditionsForServer(createdProcessor, requireOwnership, connectionParameterDef);
            if (retInst != null)
                createdProcessor.InsertBefore(retInst, allReadInsts);
            //Read to clear pooledreader.
            createdProcessor.Add(allReadInsts);

            //this.Logic
            createdProcessor.Emit(OpCodes.Ldarg_0);
            //Add each read variable as an argument. 
            foreach (VariableDefinition vd in readVariableDefs)
                createdProcessor.Emit(OpCodes.Ldloc, vd);

            /* Pass in channel and connection if original
             * method supports them. */
            ParameterDefinition originalChannelParameterDef = GetChannelParameter(originalMethodDef, RpcType.Server);
            ParameterDefinition originalConnectionParameterDef = GetNetworkConnectionParameter(originalMethodDef);
            if (originalChannelParameterDef != null)
                createdProcessor.Emit(OpCodes.Ldarg, channelParameterDef);
            if (originalConnectionParameterDef != null)
                createdProcessor.Emit(OpCodes.Ldarg, connectionParameterDef);
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
        private MethodDefinition CreateClientRpcReaderMethod(MethodDefinition originalMethodDef, MethodDefinition createdMethodDef, List<ParameterDefinition> serializedParameters, MethodDefinition logicMethodDef, CustomAttribute rpcAttribute, RpcType rpcType)
        {
            ILProcessor createdProcessor = createdMethodDef.Body.GetILProcessor();

            //Create PooledReader parameter.
            ParameterDefinition readerParameterDef = CodegenSession.GeneralHelper.CreateParameter(createdMethodDef, CodegenSession.ReaderHelper.PooledReader_TypeRef);
            ParameterDefinition channelParameterDef = GetOrCreateChannelParameter(createdMethodDef, rpcType);
            /* It's very important to read everything
             * from the PooledReader before applying any
             * exit logic. Should the method return before
             * reading the data then anything after the rpc
             * packet will be malformed due to invalid index. */
            VariableDefinition[] readVariableDefs;
            List<Instruction> allReadInsts;
            CreateRpcReadInstructions(createdProcessor, createdMethodDef, readerParameterDef, serializedParameters, out readVariableDefs, out allReadInsts);
            //Read instructions even if not to include owner.
            createdProcessor.Add(allReadInsts);

            /* ObserversRpc IncludeOwnerCheck. */
            if (rpcType == RpcType.Observers)
            {
                //If to not include owner then don't call logic if owner.
                bool includeOwner = rpcAttribute.GetField(INCLUDE_OWNER_TEXT, true);
                if (!includeOwner)
                {
                    //Create return if owner.
                    Instruction retInst = CodegenSession.ObjectHelper.CreateLocalClientIsOwnerCheck(createdProcessor, LoggingType.Off, true, true);
                    createdProcessor.InsertBefore(retInst, allReadInsts);
                }
            }

            createdProcessor.Emit(OpCodes.Ldarg_0); //this.

            /* TargetRpc passes in localconnection
            * as receiver for connection. */
            if (rpcType == RpcType.Target)
            {
                createdProcessor.Emit(OpCodes.Ldarg_0); //this.
                createdProcessor.Emit(OpCodes.Call, CodegenSession.ObjectHelper.NetworkBehaviour_LocalConnection_MethodRef);
            }

            //Add each read variable as an argument. 
            foreach (VariableDefinition vd in readVariableDefs)
                createdProcessor.Emit(OpCodes.Ldloc, vd);
            //Channel.
            ParameterDefinition originalChannelParameterDef = GetChannelParameter(originalMethodDef, rpcType);
            if (originalChannelParameterDef != null)
                createdProcessor.Emit(OpCodes.Ldarg, channelParameterDef);
            //Call __Logic method.
            createdProcessor.Emit(OpCodes.Call, logicMethodDef);
            createdProcessor.Emit(OpCodes.Ret);

            return createdMethodDef;
        }


        /// <summary>
        /// Gets the optional NetworkConnection parameter for ServerRpc, if it exists.
        /// </summary>
        /// <param name="methodDef"></param>
        /// <returns></returns>
        private ParameterDefinition GetNetworkConnectionParameter(MethodDefinition methodDef)
        {

            ParameterDefinition result = methodDef.GetEndParameter(0);
            //Is null, not networkconnection, or doesn't have default.
            if (result == null || !result.Is(typeof(NetworkConnection)) || !result.HasDefault)
                return null;

            return result;
        }

        /// <summary>
        /// Creates a NetworkConnection parameter if it's not the last or second to last parameter.
        /// </summary>
        /// <param name="methodDef"></param>
        private ParameterDefinition GetOrCreateNetworkConnectionParameter(MethodDefinition methodDef)
        {
            ParameterDefinition result = GetNetworkConnectionParameter(methodDef);
            if (result == null)
                return CodegenSession.GeneralHelper.CreateParameter(methodDef, typeof(NetworkConnection), "conn");
            else
                return result;
        }

        /// <summary>
        /// Returns the Channel parameter if it exist.
        /// </summary>
        /// <param name="originalMethodDef"></param>
        private ParameterDefinition GetChannelParameter(MethodDefinition methodDef, RpcType rpcType)
        {
            ParameterDefinition result = null;
            ParameterDefinition pd = methodDef.GetEndParameter(0);
            if (pd != null)
            {
                //Last parameter is channel.
                if (pd.Is(typeof(Channel)))
                {
                    result = pd;
                }
                /* Only other end parameter may be networkconnection.
                 * This can only be checked if a ServerRpc. */
                else if (rpcType == RpcType.Server)
                {
                    //If last parameter is networkconnection and its default then can check second to last.
                    if (pd.Is(typeof(NetworkConnection)) && pd.HasDefault)
                    {
                        pd = methodDef.GetEndParameter(1);
                        if (pd != null && pd.Is(typeof(Channel)))
                            result = pd;
                    }
                }
                else
                {
                    result = null;
                }
            }

            return result;
        }

        /// <summary>
        /// Creates a channel parameter if missing.
        /// </summary>
        /// <param name="originalMethodDef"></param>
        private ParameterDefinition GetOrCreateChannelParameter(MethodDefinition methodDef, RpcType rpcType)
        {
            ParameterDefinition result = GetChannelParameter(methodDef, rpcType);
            //Add channel parameter if not included.
            if (result == null)
            {
                ParameterDefinition connParameter = GetNetworkConnectionParameter(methodDef);
                //If the connection parameter is specified then channel has to go before it.
                if (connParameter != null)
                    return CodegenSession.GeneralHelper.CreateParameter(methodDef, typeof(Channel), "channel", ParameterAttributes.None, connParameter.Index);
                //Not specified, add channel at end.
                else
                    return CodegenSession.GeneralHelper.CreateParameter(methodDef, typeof(Channel), "channel");
            }
            else
            {
                return result;
            }
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
        private void CreateRpcReadInstructions(ILProcessor createdProcessor, MethodDefinition createdMethodDef, ParameterDefinition readerParameterDef, List<ParameterDefinition> serializedParameters, out VariableDefinition[] readVariableDefs, out List<Instruction> allReadInsts)
        {
            /* It's very important to read everything
            * from the PooledReader before applying any
            * exit logic. Should the method return before
            * reading the data then anything after the rpc
            * packet will be malformed due to invalid index. */
            readVariableDefs = new VariableDefinition[serializedParameters.Count];
            allReadInsts = new List<Instruction>();
            //True if last parameter is a connection and a server rpc.
            for (int i = 0; i < serializedParameters.Count; i++)
            {
                //Get read instructions and insert it before the return.
                List<Instruction> insts = CodegenSession.ReaderHelper.CreateReadInstructions(createdProcessor, createdMethodDef, readerParameterDef, serializedParameters[i].ParameterType, out readVariableDefs[i]);
                allReadInsts.AddRange(insts);
            }

        }
        /// <summary>
        /// Creates conditions that clients must pass to send a ServerRpc.
        /// </summary>
        /// <param name="createdProcessor"></param>
        /// <param name="rpcAttribute"></param>
        private void CreateServerRpcConditionsForClient(ILProcessor createdProcessor, MethodDefinition methodDef, CustomAttribute rpcAttribute)
        {
            bool requireOwnership = rpcAttribute.GetField(REQUIRE_OWNERSHIP_TEXT, true);
            //If (!base.IsOwner);
            if (requireOwnership)
                CodegenSession.ObjectHelper.CreateLocalClientIsOwnerCheck(createdProcessor, LoggingType.Warning, false, true);
            //If (!base.IsClient)
            CodegenSession.ObjectHelper.CreateIsClientCheck(createdProcessor, methodDef, LoggingType.Warning, false, true);
        }

        /// <summary>
        /// Creates conditions that server must pass to process a ServerRpc.
        /// </summary>
        /// <param name="createdProcessor"></param>
        /// <param name="rpcAttribute"></param>
        /// <returns>Ret instruction.</returns>
        private Instruction CreateServerRpcConditionsForServer(ILProcessor createdProcessor, bool requireOwnership, ParameterDefinition connectionParametereDef)
        {
            /* Don't need to check if server on receiving end.
             * Next compare connection with owner. */
            //If (!base.CompareOwner);
            if (requireOwnership)
                return CodegenSession.ObjectHelper.CreateRemoteClientIsOwnerCheck(createdProcessor, connectionParametereDef);
            else
                return null;
        }

        /// <summary>
        /// Creates conditions that server must pass to process a ClientRpc.
        /// </summary>
        /// <param name="createdProcessor"></param>
        private void CreateClientRpcConditionsForServer(ILProcessor createdProcessor, MethodDefinition methodDef)
        {
            //If (!base.IsServer)
            CodegenSession.ObjectHelper.CreateIsServerCheck(createdProcessor, methodDef, LoggingType.Warning, false, false);
        }

        /// <summary>
        /// Creates a method containing the logic which will run when receiving the Rpc.
        /// </summary>
        /// <param name="originalMethodDef"></param>
        /// <returns></returns>
        private MethodDefinition CreateRpcLogicMethod(TypeDefinition typeDef, MethodDefinition originalMethodDef, List<ParameterDefinition> serializedParameters, RpcType rpcType)
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