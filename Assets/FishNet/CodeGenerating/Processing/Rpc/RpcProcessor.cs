
using FishNet.CodeGenerating.Helping;
using FishNet.CodeGenerating.Helping.Extension;
using FishNet.Connection;
using FishNet.Managing.Logging;
using FishNet.Object.Helping;
using FishNet.Transporting;
using MonoFN.Cecil;
using MonoFN.Cecil.Cil;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace FishNet.CodeGenerating.Processing.Rpc
{
    internal class RpcProcessor
    {

        #region Types.
        private struct DelegateData
        {
            public RpcType RpcType;
            public bool RunLocally;
            public MethodDefinition OriginalMethodDef;
            public MethodDefinition ReaderMethodDef;
            public uint MethodHash;
            public CustomAttribute RpcAttribute;

            public DelegateData(RpcType rpcType, bool runLocally, MethodDefinition originalMethodDef, MethodDefinition readerMethodDef, uint methodHash, CustomAttribute rpcAttribute)
            {
                RpcType = rpcType;
                RunLocally = runLocally;
                OriginalMethodDef = originalMethodDef;
                ReaderMethodDef = readerMethodDef;
                MethodHash = methodHash;
                RpcAttribute = rpcAttribute;
            }
        }

        #endregion

        #region Public.
        /// <summary>
        /// Attribute helper.
        /// </summary>
        public Attributes Attributes = new Attributes();
        #endregion

        private List<(MethodDefinition, MethodDefinition)> _virtualRpcs = new List<(MethodDefinition createdLogicMd, MethodDefinition originalRpcMd)>();

        #region Const.
        private const string LOGIC_PREFIX = "RpcLogic___";
        private const string WRITER_PREFIX = "RpcWriter___";
        private const string READER_PREFIX = "RpcReader___";
        private const string REQUIREOWNERSHIP_NAME = "RequireOwnership";
        private const string RUNLOCALLY_NAME = "RunLocally";
        private const string INCLUDEOWNER_NAME = "IncludeOwner";
        private const string BUFFERLAST_NAME = "BufferLast";
        #endregion

        internal bool Process(TypeDefinition typeDef, ref uint rpcCount)
        {
            bool modified = false;

            //All createdRpcs for typeDef.
            List<CreatedRpc> typeDefCeatedRpcs = new List<CreatedRpc>();
            List<MethodDefinition> methodDefs = typeDef.Methods.ToList();
            foreach (MethodDefinition md in methodDefs)
            {
                if (rpcCount >= ObjectHelper.MAX_RPC_ALLOWANCE)
                {
                    CodegenSession.LogError($"{typeDef.FullName} and inherited types exceed {ObjectHelper.MAX_RPC_ALLOWANCE} RPC methods. Only {ObjectHelper.MAX_RPC_ALLOWANCE} RPC methods are supported per inheritance hierarchy.");
                    return false;
                }

                //Rpcs created for this method.
                List<CreatedRpc> createdRpcs = new List<CreatedRpc>();
                List<AttributeData> attributeDatas = Attributes.GetRpcAttributes(md);
                bool success = true;
                foreach (AttributeData ad in attributeDatas)
                {
                    CreatedRpc cr = new CreatedRpc();
                    cr.OriginalMethodDef = md;
                    cr.AttributeData = ad;
                    cr.MethodHash = rpcCount;

                    /* This is a one time check to make sure the rpcType is
                     * a supported value. Multiple methods beyond this rely on the
                     * value being supported. Rather than check in each method a
                     * single check is performed here. */
                    if (cr.RpcType != RpcType.Observers && cr.RpcType != RpcType.Server && cr.RpcType != RpcType.Target)
                    {
                        CodegenSession.LogError($"RpcType of {cr.RpcType.ToString()} is unhandled.");
                        break;
                    }

                    bool created = CreateRpcMethods(attributeDatas, cr);
                    if (created)
                    {
                        modified = true;

                        typeDefCeatedRpcs.Add(cr);
                        createdRpcs.Add(cr);

                        if (cr.LogicMethodDef != null && cr.LogicMethodDef.IsVirtual)
                            _virtualRpcs.Add((cr.LogicMethodDef, md));

                        rpcCount++;
                    }
                    else
                    {
                        success = false;
                    }
                }

                //If at least one attribute was found and all rpc methods were made.   
                if (createdRpcs.Count > 0 && success)
                    RedirectOriginalToWriter(createdRpcs);

            }

            if (modified)
            {
                foreach (CreatedRpc cr in typeDefCeatedRpcs)
                {
                    CodegenSession.ObjectHelper.CreateRpcDelegate(cr.RunLocally, cr.TypeDef,
                        cr.ReaderMethodDef, cr.RpcType, cr.MethodHash,
                        cr.Attribute);
                }
                return true;
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Returns the name to use for a RpcMethod.
        /// </summary>
        private string GetRpcMethodName(CreatedRpc cr)
        {
            return GetRpcMethodName(cr.RpcType, cr.OriginalMethodDef);
        }

        /// <summary>
        /// Returns the name to use for a RpcMethod.
        /// </summary>
        private string GetRpcMethodName(RpcType rpcType, MethodDefinition originalMd)
        {
            return $"{rpcType}_{GetMethodNameAsParameters(originalMd)}";
        }

        /// <summary>
        /// Returns the method name with parameter types included within the name.
        /// </summary>
        public static string GetMethodNameAsParameters(MethodDefinition methodDef)
        {
            StringBuilder sb = new StringBuilder();
            foreach (ParameterDefinition pd in methodDef.Parameters)
                sb.Append(pd.ParameterType.FullName);

            return $"{methodDef.Name}_{sb.ToString().GetStableHash32()}";
        }

        /// <summary>
        /// Redirects base calls for overriden RPCs.
        /// </summary>
        internal void RedirectBaseCalls()
        {
            foreach ((MethodDefinition logicMd, MethodDefinition originalMd) in _virtualRpcs)
                RedirectBaseCall(logicMd, originalMd);
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
                    RpcType rpcType = CodegenSession.AttributeHelper.GetRpcAttributeType(customAttribute);
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
        /// Creates all methods needed for a RPC.
        /// </summary>
        /// <param name="originalMd"></param>
        /// <param name="rpcAttribute"></param>
        /// <returns>True if successful.</returns>
        private bool CreateRpcMethods(List<AttributeData> datas, CreatedRpc cr)
        {
            cr.RunLocally = cr.Attribute.GetField(RUNLOCALLY_NAME, false);
            bool intentionallyNull;

            List<ParameterDefinition> serializedParameters = GetSerializedParamters(cr.RpcType, datas, cr);

            cr.WriterMethodDef = CreateRpcWriterMethod(serializedParameters, datas, cr, out intentionallyNull);
            if (!intentionallyNull && cr.WriterMethodDef == null)
                return false;

            cr.LogicMethodDef = CreateRpcLogicMethod(datas, cr, out intentionallyNull);
            if (!intentionallyNull && cr.LogicMethodDef == null)
                return false;

            cr.ReaderMethodDef = CreateRpcReaderMethod(serializedParameters, datas, cr, out intentionallyNull);
            if (!intentionallyNull && cr.ReaderMethodDef == null)
                return false;


            return true;
        }



        /// <summary>
        /// Creates a writer for a RPC.
        /// </summary>
        /// <param name="originalMd"></param>
        /// <param name="rpcAttribute"></param>
        /// <returns></returns>
        private MethodDefinition CreateRpcWriterMethod(List<ParameterDefinition> serializedParameters, List<AttributeData> datas, CreatedRpc cr, out bool intentionallyNull)
        {
            intentionallyNull = false;

            

            string methodName = $"{WRITER_PREFIX}{GetRpcMethodName(cr)}";
            /* If method already exist then clear it. This
             * can occur when a method needs to be rebuilt due to
             * inheritence, and renumbering the RPC method names. */
            MethodDefinition createdMd = cr.TypeDef.GetMethod(methodName);
            //If found.
            if (createdMd != null)
            {
                createdMd.Parameters.Clear();
                createdMd.Body.Instructions.Clear();
            }
            //Doesn't exist, create it.
            else
            {
                //Create the method body.
                createdMd = new MethodDefinition(methodName,
                    MethodAttributes.Private,
                    cr.Module.TypeSystem.Void);
                cr.TypeDef.Methods.Add(createdMd);
                createdMd.Body.InitLocals = true;
            }
            cr.WriterMethodDef = createdMd;

            bool result;
            if (cr.RpcType == RpcType.Server)
                result = CreateServerRpcWriterMethod(serializedParameters, cr);
            else if (cr.RpcType == RpcType.Target || cr.RpcType == RpcType.Observers)
                result = CreateClientRpcWriterMethod(serializedParameters, datas, cr);
            else
                result = false;

            return (result) ? cr.WriterMethodDef : null;
        }

        /// <summary>
        /// Returns serializable parameters for originalMd.
        /// </summary>
        private List<ParameterDefinition> GetSerializedParamters(RpcType rpcType, List<AttributeData> attributeDatas, CreatedRpc cr)
        {
            MethodDefinition originalMd = cr.OriginalMethodDef;

            //RpcTypes for originalMd.
            List<RpcType> attributeRpcTypes = attributeDatas.GetRpcTypes();

            //Parameters to be serialized.
            List<ParameterDefinition> serializedParameters = new List<ParameterDefinition>();
            /* Parameters which won't be serialized, such as channel.
             * It's safe to add parameters which are null or
             * not used. */
            HashSet<ParameterDefinition> nonserializedParameters = new HashSet<ParameterDefinition>();

            //Get channel if it exist, and get target parameter.
            ParameterDefinition channelParameterDef = GetChannelParameter(originalMd, rpcType);

            /* RpcType specific parameters. */
            ParameterDefinition targetConnectionParameterDef = null;
            if (attributeRpcTypes.Contains(RpcType.Target))
                targetConnectionParameterDef = originalMd.Parameters[0];

            if (rpcType == RpcType.Server)
            {
                //The network connection parameter might be added as null, this is okay.
                nonserializedParameters.Add(GetNetworkConnectionParameter(originalMd));
                nonserializedParameters.Add(channelParameterDef);
            }
            else
            {
                nonserializedParameters.Add(channelParameterDef);
                nonserializedParameters.Add(targetConnectionParameterDef);
            }

            //Add all parameters which are NOT nonserialized to serializedParameters.
            foreach (ParameterDefinition pd in originalMd.Parameters)
            {
                if (!nonserializedParameters.Contains(pd))
                    serializedParameters.Add(pd);
            }

            return serializedParameters;
        }

        /// <summary>
        /// Creates Writer method for a TargetRpc.
        /// </summary>
        private bool CreateClientRpcWriterMethod(List<ParameterDefinition> serializedParameters, List<AttributeData> attributeDatas, CreatedRpc cr)
        {
            MethodDefinition writerMd = cr.WriterMethodDef;
            MethodDefinition originalMd = cr.OriginalMethodDef;

            ILProcessor processor = writerMd.Body.GetILProcessor();
            //Add all parameters from the original.
            for (int i = 0; i < originalMd.Parameters.Count; i++)
                writerMd.Parameters.Add(originalMd.Parameters[i]);
            //Get channel if it exist, and get target parameter.
            ParameterDefinition channelParameterDef = GetChannelParameter(writerMd, RpcType.None);

            List<RpcType> rpcTypes = attributeDatas.GetRpcTypes();

            /* RpcType specific parameters. */
            ParameterDefinition targetConnectionParameterDef = null;
            if (rpcTypes.Contains(RpcType.Target))
                targetConnectionParameterDef = writerMd.Parameters[0];

            /* Creates basic ServerRpc and ClientRpc
             * conditions such as if requireOwnership ect..
             * or if (!base.isClient) */
            
                CreateClientRpcConditionsForServer(writerMd);

            VariableDefinition channelVariableDef = CreateAndPopulateChannelVariable(writerMd, channelParameterDef);
            //Create a local PooledWriter variable.
            VariableDefinition pooledWriterVariableDef = CodegenSession.WriterHelper.CreatePooledWriter(writerMd);
            //Create all writer.WriteType() calls. 
            for (int i = 0; i < serializedParameters.Count; i++)
            {
                MethodReference writeMethodRef = CodegenSession.WriterHelper.GetOrCreateFavoredWriteMethodReference(serializedParameters[i].ParameterType, true);
                if (writeMethodRef == null)
                    return false;

                CodegenSession.WriterHelper.CreateWrite(writerMd, pooledWriterVariableDef, serializedParameters[i], writeMethodRef);
            }

            /* Call the method on NetworkBehaviour responsible for sending out the rpc. */
            if (cr.RpcType == RpcType.Observers)
                processor.Add(CreateSendObserversRpc(writerMd, cr.MethodHash, pooledWriterVariableDef, channelVariableDef, cr.Attribute));
            else if (cr.RpcType == RpcType.Target)
                processor.Add(CreateSendTargetRpc(writerMd, cr.MethodHash, pooledWriterVariableDef, channelVariableDef, targetConnectionParameterDef));
            //Dispose of writer.
            processor.Add(CodegenSession.WriterHelper.DisposePooledWriter(writerMd, pooledWriterVariableDef));
            //Add end of method.
            processor.Emit(OpCodes.Ret);

            return true;
        }

        /// <summary>
        /// Creates Writer method for a ServerRpc.
        /// </summary>
        private bool CreateServerRpcWriterMethod(List<ParameterDefinition> serializedParameters, CreatedRpc cr)
        {
            MethodDefinition writerMd = cr.WriterMethodDef;
            MethodDefinition originalMd = cr.OriginalMethodDef;
            ILProcessor processor = writerMd.Body.GetILProcessor();

            //Add all parameters from the original.
            for (int i = 0; i < originalMd.Parameters.Count; i++)
                writerMd.Parameters.Add(originalMd.Parameters[i]);
            //Add in channel if it doesnt exist.
            ParameterDefinition channelParameterDef = GetChannelParameter(writerMd, RpcType.Server);

            /* Creates basic ServerRpc
             * conditions such as if requireOwnership ect..
             * or if (!base.isClient) */
            
                CreateServerRpcConditionsForClient(writerMd, cr.Attribute);

            VariableDefinition channelVariableDef = CreateAndPopulateChannelVariable(writerMd, channelParameterDef);
            //Create a local PooledWriter variable.
            VariableDefinition pooledWriterVariableDef = CodegenSession.WriterHelper.CreatePooledWriter(writerMd);
            //Create all writer.WriteType() calls. 
            for (int i = 0; i < serializedParameters.Count; i++)
            {
                MethodReference writeMethodRef = CodegenSession.WriterHelper.GetOrCreateFavoredWriteMethodReference(serializedParameters[i].ParameterType, true);
                if (writeMethodRef == null)
                    return false;

                CodegenSession.WriterHelper.CreateWrite(writerMd, pooledWriterVariableDef, serializedParameters[i], writeMethodRef);
            }

            //uint methodHash = originalMethodDef.FullName.GetStableHash32();
            //Call the method on NetworkBehaviour responsible for sending out the rpc.
            processor.Add(CreateSendServerRpc(writerMd, cr.MethodHash, pooledWriterVariableDef, channelVariableDef));
            //Dispose of writer.
            processor.Add(CodegenSession.WriterHelper.DisposePooledWriter(writerMd, pooledWriterVariableDef));
            //Add end of method.
            processor.Emit(OpCodes.Ret);

            return true;
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
        /// <param name="originalMd"></param>
        /// <param name="rpcAttribute"></param>
        /// <returns></returns>
        private MethodDefinition CreateRpcReaderMethod(List<ParameterDefinition> serializedParameters, List<AttributeData> datas, CreatedRpc cr, out bool intentionallyNull)
        {
            intentionallyNull = false;

            RpcType rpcType = cr.RpcType;
            MethodDefinition originalMd = cr.OriginalMethodDef;
            TypeDefinition typeDef = cr.TypeDef;
            bool runLocally = cr.RunLocally;
            MethodDefinition logicMd = cr.LogicMethodDef;
            CustomAttribute rpcAttribute = cr.Attribute;

            

            string methodName = $"{READER_PREFIX}{GetRpcMethodName(cr)}";
            /* If method already exist then just return it. This
             * can occur when a method needs to be rebuilt due to
             * inheritence, and renumbering the RPC method names. 
             * The reader method however does not need to be rewritten. */
            MethodDefinition createdMd = typeDef.GetMethod(methodName);
            //If found.
            if (createdMd != null)
            {
                cr.ReaderMethodDef = createdMd;
                return createdMd;
            }
            else
            {
                //Create the method body.
                createdMd = new MethodDefinition(
                    methodName,
                    MethodAttributes.Private,
                    originalMd.Module.TypeSystem.Void);
                typeDef.Methods.Add(createdMd);
                createdMd.Body.InitLocals = true;
                cr.ReaderMethodDef = createdMd;
            }

            if (rpcType == RpcType.Server)
                return CreateServerRpcReaderMethod(typeDef, runLocally, originalMd, createdMd, serializedParameters, logicMd, rpcAttribute);
            else if (rpcType == RpcType.Target || rpcType == RpcType.Observers)
                return CreateClientRpcReaderMethod(serializedParameters, datas, cr);
            else
                return null;
        }


        /// <summary>
        /// Creates a reader for ServerRpc.
        /// </summary>
        /// <param name="originalMd"></param>
        /// <param name="rpcAttribute"></param>
        /// <returns></returns>
        private MethodDefinition CreateServerRpcReaderMethod(TypeDefinition typeDef, bool runLocally, MethodDefinition originalMd, MethodDefinition createdMd, List<ParameterDefinition> serializedParameters, MethodDefinition logicMd, CustomAttribute rpcAttribute)
        {
            ILProcessor processor = createdMd.Body.GetILProcessor();

            bool requireOwnership = rpcAttribute.GetField(REQUIREOWNERSHIP_NAME, true);
            //Create PooledReader parameter.
            ParameterDefinition readerParameterDef = CodegenSession.GeneralHelper.CreateParameter(createdMd, CodegenSession.ReaderHelper.PooledReader_TypeRef);

            //Add connection parameter to the read method. Internals pass the connection into this.
            ParameterDefinition channelParameterDef = GetOrCreateChannelParameter(createdMd, RpcType.Server);
            ParameterDefinition connectionParameterDef = GetOrCreateNetworkConnectionParameter(createdMd);
            /* It's very important to read everything
             * from the PooledReader before applying any
             * exit logic. Should the method return before
             * reading the data then anything after the rpc
             * packet will be malformed due to invalid index. */
            VariableDefinition[] readVariableDefs;
            List<Instruction> allReadInsts;
            CreateRpcReadInstructions(createdMd, readerParameterDef, serializedParameters, out readVariableDefs, out allReadInsts);

            Instruction retInst = CreateServerRpcConditionsForServer(processor, requireOwnership, connectionParameterDef);
            if (retInst != null)
                processor.InsertBefore(retInst, allReadInsts);
            //Read to clear pooledreader.
            processor.Add(allReadInsts);

            //Block from running twice as host.
            if (runLocally)
            {
                //The connection calling is always passed into the reader method as the last parameter.
                ParameterDefinition ncPd = createdMd.Parameters[createdMd.Parameters.Count - 1];
                Instruction afterConnectionRet = processor.Create(OpCodes.Nop);
                processor.Emit(OpCodes.Ldarg, ncPd);
                processor.Emit(OpCodes.Callvirt, CodegenSession.ObjectHelper.NetworkConnection_GetIsLocalClient_MethodRef);
                processor.Emit(OpCodes.Brfalse_S, afterConnectionRet);
                processor.Emit(OpCodes.Ret);
                processor.Append(afterConnectionRet);
            }

            //this.Logic
            processor.Emit(OpCodes.Ldarg_0);
            //Add each read variable as an argument. 
            foreach (VariableDefinition vd in readVariableDefs)
                processor.Emit(OpCodes.Ldloc, vd);

            /* Pass in channel and connection if original
             * method supports them. */
            ParameterDefinition originalChannelParameterDef = GetChannelParameter(originalMd, RpcType.Server);
            ParameterDefinition originalConnectionParameterDef = GetNetworkConnectionParameter(originalMd);
            if (originalChannelParameterDef != null)
                processor.Emit(OpCodes.Ldarg, channelParameterDef);
            if (originalConnectionParameterDef != null)
                processor.Emit(OpCodes.Ldarg, connectionParameterDef);
            //Call __Logic method.
            processor.Emit(OpCodes.Call, logicMd);
            processor.Emit(OpCodes.Ret);

            return createdMd;
        }

        /// <summary>
        /// Creates a reader for ObserversRpc.
        /// </summary>
        /// <param name="originalMd"></param>
        /// <param name="rpcAttribute"></param>
        /// <returns></returns>
        private MethodDefinition CreateClientRpcReaderMethod(List<ParameterDefinition> serializedParameters, List<AttributeData> attributeDatas, CreatedRpc cr)
        {
            MethodDefinition originalMd = cr.OriginalMethodDef;
            MethodDefinition readerMd = cr.ReaderMethodDef;
            RpcType rpcType = cr.RpcType;
            CustomAttribute rpcAttribute = cr.Attribute;
            bool runLocally = cr.RunLocally;

            ILProcessor processor = readerMd.Body.GetILProcessor();

            //Create PooledReader parameter.
            ParameterDefinition readerParameterDef = CodegenSession.GeneralHelper.CreateParameter(readerMd, CodegenSession.ReaderHelper.PooledReader_TypeRef);
            ParameterDefinition channelParameterDef = GetOrCreateChannelParameter(readerMd, rpcType);
            /* It's very important to read everything
             * from the PooledReader before applying any
             * exit logic. Should the method return before
             * reading the data then anything after the rpc
             * packet will be malformed due to invalid index. */
            VariableDefinition[] readVariableDefs;
            List<Instruction> allReadInsts;
            CreateRpcReadInstructions(readerMd, readerParameterDef, serializedParameters, out readVariableDefs, out allReadInsts);
            //Read instructions even if not to include owner.
            processor.Add(allReadInsts);

            /* ObserversRpc IncludeOwnerCheck. */
            if (rpcType == RpcType.Observers)
            {
                //If to not include owner then don't call logic if owner.
                bool includeOwner = rpcAttribute.GetField(INCLUDEOWNER_NAME, true);
                if (!includeOwner)
                {
                    //Create return if owner.
                    Instruction retInst = CodegenSession.ObjectHelper.CreateLocalClientIsOwnerCheck(readerMd, LoggingType.Off, true, true, true);
                    processor.InsertBefore(retInst, allReadInsts);
                }
            }

            //Block from running twice as host.
            if (runLocally)
                processor.Add(CreateIsHostBlock(readerMd));

            processor.Emit(OpCodes.Ldarg_0); //this.
            /* TargetRpc passes in localconnection
            * as receiver for connection. */
            if (rpcType == RpcType.Target)
            {
                processor.Emit(OpCodes.Ldarg_0); //this.
                processor.Emit(OpCodes.Call, CodegenSession.ObjectHelper.NetworkBehaviour_LocalConnection_MethodRef);
            }
            else
            {
                //If this method uses target/observerRpc combined then load null for the connection.
                RpcType allRpcTypes = attributeDatas.GetCombinedRpcType();
                if (allRpcTypes == (RpcType.Observers | RpcType.Target))
                    processor.Emit(OpCodes.Ldnull);
            }
            //Add each read variable as an argument. 
            foreach (VariableDefinition vd in readVariableDefs)
                processor.Emit(OpCodes.Ldloc, vd);
            //Channel.
            ParameterDefinition originalChannelParameterDef = GetChannelParameter(originalMd, rpcType);
            if (originalChannelParameterDef != null)
                processor.Emit(OpCodes.Ldarg, channelParameterDef);
            //Call __Logic method.
            processor.Emit(OpCodes.Call, cr.LogicMethodDef);
            processor.Emit(OpCodes.Ret);

            return readerMd;
        }


        /// <summary>
        /// Appends a block to the method if running as host.
        /// </summary>
        /// <param name="md"></param>
        private List<Instruction> CreateIsHostBlock(MethodDefinition md)
        {
            List<Instruction> ints = new List<Instruction>();
            ILProcessor processor = md.Body.GetILProcessor();

            Instruction endIfInst = processor.Create(OpCodes.Nop);
            ints.Add(processor.Create(OpCodes.Ldarg_0));
            ints.Add(processor.Create(OpCodes.Call, CodegenSession.ObjectHelper.NetworkBehaviour_IsHost_MethodRef));
            ints.Add(processor.Create(OpCodes.Brfalse_S, endIfInst));
            ints.Add(processor.Create(OpCodes.Ret));
            ints.Add(endIfInst);

            return ints;
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
        /// <param name="processor"></param>
        /// <param name="methodDef"></param>
        /// <param name="readerParameterDef"></param>
        /// <param name="serializedParameters"></param>
        /// <param name="readVariableDefs"></param>
        /// <param name="allReadInsts"></param>
        private void CreateRpcReadInstructions(MethodDefinition methodDef, ParameterDefinition readerParameterDef, List<ParameterDefinition> serializedParameters, out VariableDefinition[] readVariableDefs, out List<Instruction> allReadInsts)
        {
            /* It's very important to read everything
            * from the PooledReader before applying any
            * exit logic. Should the method return before
            * reading the data then anything after the rpc
            * packet will be malformed due to invalid index. */
            readVariableDefs = new VariableDefinition[serializedParameters.Count];
            allReadInsts = new List<Instruction>();
            ILProcessor processor = methodDef.Body.GetILProcessor();

            //True if last parameter is a connection and a server rpc.
            for (int i = 0; i < serializedParameters.Count; i++)
            {
                //Get read instructions and insert it before the return.
                List<Instruction> insts = CodegenSession.ReaderHelper.CreateRead(methodDef, readerParameterDef, serializedParameters[i].ParameterType, out readVariableDefs[i]);
                allReadInsts.AddRange(insts);
            }

        }
        /// <summary>
        /// Creates conditions that clients must pass to send a ServerRpc.
        /// </summary>
        /// <param name="createdProcessor"></param>
        /// <param name="rpcAttribute"></param>
        private void CreateServerRpcConditionsForClient(MethodDefinition methodDef, CustomAttribute rpcAttribute)
        {
            bool requireOwnership = rpcAttribute.GetField(REQUIREOWNERSHIP_NAME, true);
            //If (!base.IsOwner);
            if (requireOwnership)
                CodegenSession.ObjectHelper.CreateLocalClientIsOwnerCheck(methodDef, LoggingType.Warning, false, false, true);
            //If (!base.IsClient)
            CodegenSession.ObjectHelper.CreateIsClientCheck(methodDef, LoggingType.Warning, false, true);
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
        private void CreateClientRpcConditionsForServer(MethodDefinition methodDef)
        {
            //If (!base.IsServer)
            CodegenSession.ObjectHelper.CreateIsServerCheck(methodDef, LoggingType.Warning, false, false);
        }

        /// <summary>
        /// Creates a method containing the logic which will run when receiving the Rpc.
        /// </summary>
        /// <param name="originalMd"></param>
        /// <returns></returns>
        private MethodDefinition CreateRpcLogicMethod(List<AttributeData> datas, CreatedRpc cr, out bool intentionallyNull)
        {
            intentionallyNull = false;

            RpcType rpcType = cr.RpcType;
            TypeDefinition typeDef = cr.TypeDef;
            MethodDefinition originalMd = cr.OriginalMethodDef;

            

            //Methodname for logic methods do not use prefixes because there can be only one.
            string methodName = $"{LOGIC_PREFIX}{GetMethodNameAsParameters(originalMd)}";
            /* If method already exist then just return it. This
             * can occur when a method needs to be rebuilt due to
             * inheritence, and renumbering the RPC method names. 
             * The logic method however does not need to be rewritten. */
            MethodDefinition logicMd = typeDef.GetMethod(methodName);

            //If found.
            if (logicMd != null)
            {
                cr.LogicMethodDef = logicMd;
                return logicMd;
            }
            else
            {
                //Create the method body.
                logicMd = new MethodDefinition(
                    methodName, originalMd.Attributes, originalMd.ReturnType);
                typeDef.Methods.Add(logicMd);
                logicMd.Body.InitLocals = true;
                cr.LogicMethodDef = logicMd;
            }

            //Copy parameter expecations into new method.
            foreach (ParameterDefinition pd in originalMd.Parameters)
                logicMd.Parameters.Add(pd);

            //Swap bodies.
            (logicMd.Body, originalMd.Body) = (originalMd.Body, logicMd.Body);
            //Move over all the debugging information
            foreach (SequencePoint sequencePoint in originalMd.DebugInformation.SequencePoints)
                logicMd.DebugInformation.SequencePoints.Add(sequencePoint);
            originalMd.DebugInformation.SequencePoints.Clear();

            foreach (CustomDebugInformation customInfo in originalMd.CustomDebugInformations)
                logicMd.CustomDebugInformations.Add(customInfo);
            originalMd.CustomDebugInformations.Clear();
            //Swap debuginformation scope.
            (originalMd.DebugInformation.Scope, logicMd.DebugInformation.Scope) = (logicMd.DebugInformation.Scope, originalMd.DebugInformation.Scope);

            return logicMd;
        }

        /// <summary>
        /// Finds and fixes call to base methods within remote calls
        /// <para>For example, changes `base.CmdDoSomething` to `base.UserCode_CmdDoSomething` within `this.UserCode_CmdDoSomething`</para>
        /// </summary>
        /// <param name="type"></param>
        /// <param name="createdMethodDef"></param>
        private void RedirectBaseCall(MethodDefinition createdMethodDef, MethodDefinition originalMethodDef)
        {
            //All logic RPCs end with the logic suffix.
            if (!createdMethodDef.Name.StartsWith(LOGIC_PREFIX))
                return;
            //Not virtual, no need to check.
            if (!createdMethodDef.IsVirtual)
                return;

            foreach (Instruction instruction in createdMethodDef.Body.Instructions)
            {
                // if call to base.RpcDoSomething within this.RpcDoSOmething.
                if (CodegenSession.GeneralHelper.IsCallToMethod(instruction, out MethodDefinition calledMethod) && calledMethod.Name == originalMethodDef.Name)
                {
                    MethodReference baseLogicMd = createdMethodDef.DeclaringType.GetMethodInBase(createdMethodDef.Name);
                    if (baseLogicMd == null)
                    {
                        CodegenSession.LogError($"Could not find base method for {createdMethodDef.Name}.");
                        return;
                    }

                    instruction.Operand = CodegenSession.ImportReference(baseLogicMd);
                }
            }
        }


        /// <summary> 
        /// Redirects calls from the original Rpc method to the writer method.
        /// </summary>
        private void RedirectOriginalToWriter(List<CreatedRpc> createdRpcs)
        {
            /* If there are multiple attributes/createdRpcs they will
            * share the same originalMd so it's fine to take the first
            * entry. */
            MethodDefinition originalMd = createdRpcs[0].OriginalMethodDef;

            

            ILProcessor processor = originalMd.Body.GetILProcessor();
            originalMd.Body.Instructions.Clear();

            //If only one rpc type.
            if (createdRpcs.Count == 1)
            {
                processor.Emit(OpCodes.Ldarg_0); //this.
                                                 //Parameters.
                foreach (ParameterDefinition pd in originalMd.Parameters)
                    processor.Emit(OpCodes.Ldarg, pd);

                //Call method.
                MethodReference writerMr = CodegenSession.ImportReference(createdRpcs[0].WriterMethodDef);
                processor.Emit(OpCodes.Call, writerMr);
            }
            //More than one which means it's an observer/targetRpc combo.
            else
            {
                MethodReference observerWriterMr = CodegenSession.ImportReference(createdRpcs.GetCreatedRpc(RpcType.Observers).WriterMethodDef);
                MethodReference targetWriterMr = CodegenSession.ImportReference(createdRpcs.GetCreatedRpc(RpcType.Target).WriterMethodDef);

                Instruction targetRpcInst = processor.Create(OpCodes.Nop);
                Instruction afterTargetRpcInst = processor.Create(OpCodes.Nop);
                /* if (targetConn == null)
                 *      WriteObserverRpc
                 * else
                 *      WriteTargetRpc */
                processor.Emit(OpCodes.Ldarg, originalMd.Parameters[0]);
                processor.Emit(OpCodes.Brtrue_S, targetRpcInst);
                //Insert parameters.
                processor.Emit(OpCodes.Ldarg_0);
                foreach (ParameterDefinition pd in originalMd.Parameters)
                    processor.Emit(OpCodes.Ldarg, pd);
                processor.Emit(OpCodes.Call, observerWriterMr);
                //else (target).
                processor.Emit(OpCodes.Br_S, afterTargetRpcInst);
                processor.Append(targetRpcInst);
                //Insert parameters.
                processor.Emit(OpCodes.Ldarg_0);
                foreach (ParameterDefinition pd in originalMd.Parameters)
                    processor.Emit(OpCodes.Ldarg, pd);
                processor.Emit(OpCodes.Call, targetWriterMr);
                processor.Append(afterTargetRpcInst);
            }

            //Runlocally.
            if (createdRpcs[0].RunLocally)
            {
                processor.Emit(OpCodes.Ldarg_0); //this.
                                                 //Parameters.
                foreach (ParameterDefinition pd in originalMd.Parameters)
                    processor.Emit(OpCodes.Ldarg, pd);
                processor.Emit(OpCodes.Call, createdRpcs[0].LogicMethodDef);
            }

            processor.Emit(OpCodes.Ret);
        }


        #region CreateSend
        /// <summary>
        /// Creates a call to SendServerRpc on NetworkBehaviour.
        /// </summary>
        /// <param name="writerVariableDef"></param>
        /// <param name="channel"></param>
        private List<Instruction> CreateSendServerRpc(MethodDefinition methodDef, uint methodHash, VariableDefinition writerVariableDef, VariableDefinition channelVariableDef)
        {
            List<Instruction> insts = new List<Instruction>();
            ILProcessor processor = methodDef.Body.GetILProcessor();

            insts.AddRange(CreateSendRpcCommon(processor, methodHash, writerVariableDef, channelVariableDef));
            //Call NetworkBehaviour.
            insts.Add(processor.Create(OpCodes.Call, CodegenSession.ObjectHelper.NetworkBehaviour_SendServerRpc_MethodRef));

            return insts;
        }

        /// <summary>
        /// Creates a call to SendObserversRpc on NetworkBehaviour.
        /// </summary>
        private List<Instruction> CreateSendObserversRpc(MethodDefinition methodDef, uint methodHash, VariableDefinition writerVariableDef, VariableDefinition channelVariableDef, CustomAttribute rpcAttribute)
        {
            List<Instruction> insts = new List<Instruction>();
            ILProcessor processor = methodDef.Body.GetILProcessor();

            insts.AddRange(CreateSendRpcCommon(processor, methodHash, writerVariableDef, channelVariableDef));
            //Also add if buffered.
            bool bufferLast = rpcAttribute.GetField(BUFFERLAST_NAME, false);
            int buffered = (bufferLast) ? 1 : 0;

            //Warn user if any values are byref.
            bool usedByref = false;
            foreach (ParameterDefinition item in methodDef.Parameters)
            {
                if (item.IsIn)
                {
                    usedByref = true;
                    break;
                }
            }
            if (usedByref)
                CodegenSession.LogWarning($"Method {methodDef.FullName} takes an argument by reference. While this is supported, using BufferLast in addition to by reference arguements will buffer the value as it was serialized, not as it is when sending buffered.");

            insts.Add(processor.Create(OpCodes.Ldc_I4, buffered));
            //Call NetworkBehaviour.
            insts.Add(processor.Create(OpCodes.Call, CodegenSession.ObjectHelper.NetworkBehaviour_SendObserversRpc_MethodRef));

            return insts;
        }
        /// <summary>
        /// Creates a call to SendTargetRpc on NetworkBehaviour.
        /// </summary>
        private List<Instruction> CreateSendTargetRpc(MethodDefinition methodDef, uint methodHash, VariableDefinition writerVariableDef, VariableDefinition channelVariableDef, ParameterDefinition targetConnectionParameterDef)
        {
            List<Instruction> insts = new List<Instruction>();
            ILProcessor processor = methodDef.Body.GetILProcessor();

            insts.AddRange(CreateSendRpcCommon(processor, methodHash, writerVariableDef, channelVariableDef));
            //Reference to NetworkConnection that RPC is going to.
            insts.Add(processor.Create(OpCodes.Ldarg, targetConnectionParameterDef));
            //Call NetworkBehaviour.
            insts.Add(processor.Create(OpCodes.Call, CodegenSession.ObjectHelper.NetworkBehaviour_SendTargetRpc_MethodRef));

            return insts;
        }

        /// <summary>
        /// Writes common properties that all SendRpc methods use.
        /// </summary>
        private List<Instruction> CreateSendRpcCommon(ILProcessor processor, uint methodHash, VariableDefinition writerVariableDef, VariableDefinition channelVariableDef)
        {
            List<Instruction> insts = new List<Instruction>();

            insts.Add(processor.Create(OpCodes.Ldarg_0)); // argument: this
            insts.Add(processor.Create(OpCodes.Ldc_I4, (int)methodHash));
            //reference to PooledWriter.
            insts.Add(processor.Create(OpCodes.Ldloc, writerVariableDef));
            //reference to Channel.
            insts.Add(processor.Create(OpCodes.Ldloc, channelVariableDef));

            return insts;
        }
        #endregion
    }
}