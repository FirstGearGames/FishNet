﻿using FishNet.CodeGenerating.Helping;
using FishNet.CodeGenerating.Helping.Extension;
using FishNet.Connection;
using FishNet.Managing.Logging;
using FishNet.Managing.Timing;
using FishNet.Object;
using FishNet.Serializing;
using FishNet.Transporting;
using MonoFN.Cecil;
using MonoFN.Cecil.Cil;
using MonoFN.Cecil.Rocks;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using SR = System.Reflection;

namespace FishNet.CodeGenerating.Processing
{
    internal class NetworkBehaviourPredictionProcessor : CodegenBase
    {

        #region Types.
        private enum InsertType
        {
            First,
            Last,
            Current
        }

        private class CreatedPredictionFields
        {
            /// <summary>
            /// Replicate data buffered on the server.
            /// </summary>
            public readonly FieldReference ServerReplicateDatas;
            /// <summary>
            /// Replicate data buffered on the client.
            /// </summary>
            public readonly FieldReference ClientReplicateDatas;
            /// <summary>
            /// Last reconcile data received from the server.
            /// </summary>
            public readonly FieldReference ReconcileData;
            /// <summary>
            /// Last tick on data server replicated.
            /// </summary>
            public readonly FieldReference ServerReplicateTick;
            /// <summary>
            /// How many remaining ticks server can resend reconcile.
            /// </summary>
            public readonly FieldReference ServerReconcileResends;
            /// <summary>
            /// How many remaining ticks client can resend input.
            /// </summary>
            public readonly FieldReference ClientReplicateResends;
            /// <summary>
            /// True if client has data to reconcile with.
            /// </summary>
            public readonly FieldReference ClientHasReconcileData;
            /// <summary>
            /// True if client is replaying data.
            /// </summary>
            public readonly FieldReference ClientReplayingData;
            /// <summary>
            /// Last tick on a reconcile client received.
            /// </summary>
            public readonly FieldReference ClientReconcileTick;
            /// <summary>
            /// Last tick client sent new data.
            /// </summary>
            public readonly FieldReference ClientReplicateTick;
            /// <summary>
            /// Last tick on data received from client.
            /// </summary>
            public readonly FieldReference ServerReceivedTick;
            /// <summary>
            /// A buffer to read replicates into.
            /// </summary>
            public readonly FieldReference ServerReplicateReaderBuffer;

            public CreatedPredictionFields(FieldReference serverReplicateDatas, FieldReference clientReplicateDatas, FieldReference reconcileData, FieldReference serverReplicateTick,
                FieldReference serverReconcileResends, FieldReference clientReplicateResends, FieldReference clientHasReconcileData, FieldReference clientReplayingData,
                FieldReference clientReconcileTick, FieldReference clientReplicateTick, FieldReference serverReceivedTick, FieldReference serverReplicateReaderBuffer)
            {
                ServerReplicateDatas = serverReplicateDatas;
                ClientReplicateDatas = clientReplicateDatas;
                ReconcileData = reconcileData;
                ServerReplicateTick = serverReplicateTick;
                ServerReconcileResends = serverReconcileResends;
                ClientReplicateResends = clientReplicateResends;
                ClientHasReconcileData = clientHasReconcileData;
                ClientReplayingData = clientReplayingData;
                ClientReconcileTick = clientReconcileTick;
                ClientReplicateTick = clientReplicateTick;
                ServerReceivedTick = serverReceivedTick;
                ServerReplicateReaderBuffer = serverReplicateReaderBuffer;
            }
        }

        private class PredictionReaders
        {
            public MethodReference ReplicateReader;
            public MethodReference ReconcileReader;

            public PredictionReaders(MethodReference replicateReader, MethodReference reconcileReader)
            {
                ReplicateReader = replicateReader;
                ReconcileReader = reconcileReader;
            }
        }

        #endregion

        #region Private
        private MethodReference Unity_GetGameObject_MethodRef;
        private MethodReference Unity_GetScene_MethodRef;
        private MethodReference Unity_GetPhysicsScene2D_MethodRef;
        private MethodReference Unity_GetPhysicsScene3D_MethodRef;
        private MethodReference Physics3D_Simulate_MethodRef;
        private MethodReference Physics2D_Simulate_MethodRef;
        private MethodReference Physics3D_SyncTransforms_MethodRef;
        private MethodReference Physics2D_SyncTransforms_MethodRef;

        private FieldReference ReplicateData_Tick_FieldRef;
        private FieldReference ReconcileData_Tick_FieldRef;

        private string ClearReplicateCache_Method_Name;
        #endregion

        #region Const.
        private const string REPLICATE_LOGIC_PREFIX = "ReplicateLogic___";
        private const string REPLICATE_READER_PREFIX = "ReplicateReader___";
        private const string RECONCILE_LOGIC_PREFIX = "ReconcileLogic___";
        private const string RECONCILE_READER_PREFIX = "ReconcileReader___";
        private const string DATA_TICK_FIELD_NAME = "Generated___Tick";
        private static readonly OpCode RESEND_COUNT_OPCODE = OpCodes.Ldc_I4_3;
        #endregion

        public override bool ImportReferences()
        {
            SR.MethodInfo locMi;

            ClearReplicateCache_Method_Name = nameof(NetworkBehaviour.ClearReplicateCacheInternal);

            //GetGameObject.
            locMi = typeof(UnityEngine.Component).GetMethod("get_gameObject");
            Unity_GetGameObject_MethodRef = base.ImportReference(locMi);
            //GetScene.
            locMi = typeof(UnityEngine.GameObject).GetMethod("get_scene");
            Unity_GetScene_MethodRef = base.ImportReference(locMi);

            //Physics.SyncTransform.
            foreach (SR.MethodInfo mi in typeof(Physics).GetMethods())
            {
                if (mi.Name == nameof(Physics.SyncTransforms))
                {
                    Physics3D_SyncTransforms_MethodRef = base.ImportReference(mi);
                    break;
                }
            }
            foreach (SR.MethodInfo mi in typeof(Physics2D).GetMethods())
            {
                if (mi.Name == nameof(Physics2D.SyncTransforms))
                {
                    Physics2D_SyncTransforms_MethodRef = base.ImportReference(mi);
                    break;
                }
            }

            //PhysicsScene.Simulate.
            foreach (SR.MethodInfo mi in typeof(PhysicsScene).GetMethods())
            {
                if (mi.Name == nameof(PhysicsScene.Simulate))
                {
                    Physics3D_Simulate_MethodRef = base.ImportReference(mi);
                    break;
                }
            }
            foreach (SR.MethodInfo mi in typeof(PhysicsScene2D).GetMethods())
            {
                if (mi.Name == nameof(PhysicsScene2D.Simulate))
                {
                    Physics2D_Simulate_MethodRef = base.ImportReference(mi);
                    break;
                }
            }

            //GetPhysicsScene.
            foreach (SR.MethodInfo mi in typeof(PhysicsSceneExtensions).GetMethods())
            {
                if (mi.Name == nameof(PhysicsSceneExtensions.GetPhysicsScene))
                {
                    Unity_GetPhysicsScene3D_MethodRef = base.ImportReference(mi);
                    break;
                }
            }
            foreach (SR.MethodInfo mi in typeof(PhysicsSceneExtensions2D).GetMethods())
            {
                if (mi.Name == nameof(PhysicsSceneExtensions2D.GetPhysicsScene2D))
                {
                    Unity_GetPhysicsScene2D_MethodRef = base.ImportReference(mi);
                    break;
                }
            }


            return true;
        }

        internal bool Process(TypeDefinition typeDef, ref uint rpcCount)
        {
            bool modified = false;
            modified |= ProcessLocal(typeDef, ref rpcCount);

            return modified;
        }

        #region Setup and checks.
        /// <summary>
        /// Gets number of predictions by checking for prediction attributes. This does not perform error checking.
        /// </summary>
        /// <param name="typeDef"></param>
        /// <returns></returns>
        internal uint GetPredictionCount(TypeDefinition typeDef)
        {
            /* Currently only one prediction method is allowed per typeDef.
             * Return 1 soon as a method is found. */
            foreach (MethodDefinition methodDef in typeDef.Methods)
            {
                foreach (CustomAttribute customAttribute in methodDef.CustomAttributes)
                {
                    if (customAttribute.Is(base.GetClass<AttributeHelper>().ReplicateAttribute_FullName))
                        return 1;
                }
            }

            return 0;
        }


        /// <summary>
        /// Ensures only one prediction and reconile method exist per typeDef, and outputs finding.
        /// </summary>
        /// <returns>True if there is only one set of prediction methods. False if none, or more than one set.</returns>
        internal bool GetPredictionMethods(TypeDefinition typeDef, out MethodDefinition replicateMd, out MethodDefinition reconcileMd)
        {
            replicateMd = null;
            reconcileMd = null;

            bool error = false;
            foreach (MethodDefinition methodDef in typeDef.Methods)
            {
                foreach (CustomAttribute customAttribute in methodDef.CustomAttributes)
                {
                    if (customAttribute.Is(base.GetClass<AttributeHelper>().ReplicateAttribute_FullName))
                    {
                        if (!MethodIsPrivate(methodDef) || AlreadyFound(replicateMd))
                            error = true;
                        else
                            replicateMd = methodDef;
                    }
                    else if (customAttribute.Is(base.GetClass<AttributeHelper>().ReconcileAttribute_FullName))
                    {
                        if (!MethodIsPrivate(methodDef) || AlreadyFound(reconcileMd))
                            error = true;
                        else
                            reconcileMd = methodDef;
                    }
                    if (error)
                        break;
                }
                if (error)
                    break;
            }

            bool MethodIsPrivate(MethodDefinition md)
            {
                bool isPrivate = md.Attributes.HasFlag(MethodAttributes.Private);
                if (!isPrivate)
                    base.LogError($"Method {md.Name} within {typeDef.Name} is a prediction method and must be private.");
                return isPrivate;
            }

            bool AlreadyFound(MethodDefinition md)
            {
                bool alreadyFound = (md != null);
                if (alreadyFound)
                    base.LogError($"{typeDef.Name} contains multiple prediction sets; currently only one set is allowed.");

                return alreadyFound;
            }

            if (!error && ((replicateMd == null) != (reconcileMd == null)))
            {
                base.LogError($"{typeDef.Name} must contain both a [Replicate] and [Reconcile] method when using prediction.");
                error = true;
            }

            if (error || (replicateMd == null) || (reconcileMd == null))
                return false;
            else
                return true;
        }
        #endregion

        private bool ProcessLocal(TypeDefinition typeDef, ref uint rpcCount)
        {
            MethodDefinition replicateMd;
            MethodDefinition reconcileMd;

            //Not using prediction methods.
            if (!GetPredictionMethods(typeDef, out replicateMd, out reconcileMd))
                return false;

            //If replication methods found but this hierarchy already has max.
            if (rpcCount >= NetworkBehaviourHelper.MAX_RPC_ALLOWANCE)
            {
                base.LogError($"{typeDef.FullName} and inherited types exceed {NetworkBehaviourHelper.MAX_RPC_ALLOWANCE} replicated methods. Only {NetworkBehaviourHelper.MAX_RPC_ALLOWANCE} replicated methods are supported per inheritance hierarchy.");
                return false;
            }

            bool parameterError = false;
            parameterError |= HasParameterError(replicateMd, typeDef, true);
            parameterError |= HasParameterError(reconcileMd, typeDef, false);
            if (parameterError)
                return false;

            //Add field to replicate/reconcile datas, which stores tick of data.
            TypeDefinition replicateDataTd = replicateMd.Parameters[0].ParameterType.CachedResolve(base.Session);
            TypeDefinition reconcileDataTd = reconcileMd.Parameters[0].ParameterType.CachedResolve(base.Session);
            AddTickFieldToDatas(replicateMd.Parameters[0].ParameterType.CachedResolve(base.Session), reconcileMd.Parameters[0].ParameterType.CachedResolve(base.Session));
            /* Make sure data can serialize. Use array type, this will
             * generate a serializer for element type as well. */
            bool canSerialize;
            //Make sure replicate data can serialize.
            canSerialize = base.GetClass<GeneralHelper>().HasSerializerAndDeserializer(replicateDataTd.MakeArrayType(), true);
            if (!canSerialize)
            {
                base.LogError($"Replicate data type {replicateDataTd.Name} does not support serialization. Use a supported type or create a custom serializer.");
                return false;
            }
            //Make sure reconcile data can serialize.
            canSerialize = base.GetClass<GeneralHelper>().HasSerializerAndDeserializer(reconcileDataTd, true);
            if (!canSerialize)
            {
                base.LogError($"Reconcile data type {reconcileDataTd.Name} does not support serialization. Use a supported type or create a custom serializer.");
                return false;
            }
            //Creates fields for buffers.
            CreatedPredictionFields predictionFields;
            CreateFields(typeDef, replicateMd, reconcileMd, out predictionFields);

            PredictionReaders predictionReaders;
            CreatePredictionMethods(typeDef, replicateMd, reconcileMd, predictionFields, rpcCount, out predictionReaders);

            InitializeCollections(typeDef, replicateMd, predictionFields);
            RegisterRpcs(typeDef, rpcCount, predictionReaders);

            rpcCount++;
            return true;
        }

        /// <summary>
        /// Registers RPCs that prediction uses.
        /// </summary>
        private void RegisterRpcs(TypeDefinition typeDef, uint hash, PredictionReaders readers)
        {
            MethodDefinition injectionMethodDef = typeDef.GetMethod(NetworkBehaviourProcessor.NETWORKINITIALIZE_EARLY_INTERNAL_NAME);
            ILProcessor processor = injectionMethodDef.Body.GetILProcessor();
            List<Instruction> insts = new List<Instruction>();

            Register(readers.ReplicateReader.CachedResolve(base.Session), true);
            Register(readers.ReconcileReader.CachedResolve(base.Session), false);

            void Register(MethodDefinition readerMd, bool replicate)
            {
                insts.Add(processor.Create(OpCodes.Ldarg_0));
                insts.Add(processor.Create(OpCodes.Ldc_I4, (int)hash));
                /* Create delegate and call NetworkBehaviour method. */
                insts.Add(processor.Create(OpCodes.Ldarg_0));
                insts.Add(processor.Create(OpCodes.Ldftn, readerMd));

                MethodReference ctorMr;
                MethodReference callMr;
                if (replicate)
                {
                    ctorMr = base.GetClass<NetworkBehaviourHelper>().ReplicateRpcDelegateConstructor_MethodRef;
                    callMr = base.GetClass<NetworkBehaviourHelper>().RegisterReplicateRpc_MethodRef;
                }
                else
                {
                    ctorMr = base.GetClass<NetworkBehaviourHelper>().ReconcileRpcDelegateConstructor_MethodRef;
                    callMr = base.GetClass<NetworkBehaviourHelper>().RegisterReconcileRpc_MethodRef;
                }

                insts.Add(processor.Create(OpCodes.Newobj, ctorMr));
                insts.Add(processor.Create(OpCodes.Call, callMr));
            }

            processor.InsertLast(insts);
        }

        /// <summary>
        /// Initializes collection fields made during this process.
        /// </summary>
        /// <param name="predictionFields"></param>
        private void InitializeCollections(TypeDefinition typeDef, MethodDefinition replicateMd, CreatedPredictionFields predictionFields)
        {
            TypeReference replicateDataTr = replicateMd.Parameters[0].ParameterType;
            MethodDefinition injectionMethodDef = typeDef.GetMethod(NetworkBehaviourProcessor.NETWORKINITIALIZE_EARLY_INTERNAL_NAME);
            ILProcessor processor = injectionMethodDef.Body.GetILProcessor();

            Generate(predictionFields.ClientReplicateDatas, true);
            Generate(predictionFields.ServerReplicateDatas, false);

            void Generate(FieldReference fr, bool isList)
            {
                MethodDefinition ctorMd = base.GetClass<GeneralHelper>().List_TypeRef.CachedResolve(base.Session).GetConstructor();
                GenericInstanceType collectionGit;
                if (isList)
                    GetGenericLists(replicateDataTr, out collectionGit);
                else
                    GetGenericQueues(replicateDataTr, out collectionGit);
                MethodReference ctorMr = ctorMd.MakeHostInstanceGeneric(base.Session, collectionGit);

                List<Instruction> insts = new List<Instruction>();

                insts.Add(processor.Create(OpCodes.Ldarg_0));
                insts.Add(processor.Create(OpCodes.Newobj, ctorMr));
                insts.Add(processor.Create(OpCodes.Stfld, fr));
                processor.InsertFirst(insts);
            }
        }

        /// <summary>
        /// Creates field buffers for replicate datas.
        /// </summary>
        /// <param name="typeDef"></param>
        /// <param name="replicateMd"></param>
        /// <param name=""></param>
        /// <returns></returns>
        private void CreateFields(TypeDefinition typeDef, MethodDefinition replicateMd, MethodDefinition reconcileMd, out CreatedPredictionFields predictionFields)
        {
            TypeReference replicateDataTr = replicateMd.Parameters[0].ParameterType;
            TypeReference replicateDataArrTr = replicateDataTr.MakeArrayType();
            TypeReference reconcileDataTr = reconcileMd.Parameters[0].ParameterType;
            TypeReference uintTr = base.GetClass<GeneralHelper>().GetTypeReference(typeof(uint));
            TypeReference boolTr = base.GetClass<GeneralHelper>().GetTypeReference(typeof(bool));

            GenericInstanceType lstDataGit;
            GenericInstanceType queueDataGit;
            GetGenericLists(replicateDataTr, out lstDataGit);
            GetGenericQueues(replicateDataTr, out queueDataGit);

            /* Data buffer. */
            FieldDefinition serverReplicatesFd = new FieldDefinition($"{replicateMd.Name}___serverReplicates", FieldAttributes.Private, queueDataGit);
            FieldDefinition clientReplicatesFd = new FieldDefinition($"{replicateMd.Name}___clientReplicates", FieldAttributes.Private, lstDataGit);
            FieldDefinition clientReconcileFd = new FieldDefinition($"{replicateMd.Name}___clientReconcile", FieldAttributes.Private, reconcileDataTr);
            FieldDefinition serverReplicateTickFd = new FieldDefinition($"{replicateMd.Name}___serverReplicateTick", FieldAttributes.Private, uintTr);
            FieldDefinition serverReconcileResendsFd = new FieldDefinition($"{replicateMd.Name}___serverReconcileResends", FieldAttributes.Private, uintTr);
            FieldDefinition clientReplicateResendsFd = new FieldDefinition($"{replicateMd.Name}___clientReplicateResends", FieldAttributes.Private, uintTr);
            FieldDefinition clientHasReconcileDataFd = new FieldDefinition($"{replicateMd.Name}___clientHasReconcileData", FieldAttributes.Private, boolTr);
            FieldDefinition clientReplayingDataaFd = new FieldDefinition($"{replicateMd.Name}___clientReplayingData", FieldAttributes.Private, boolTr);
            FieldDefinition clientReconcileTickFd = new FieldDefinition($"{replicateMd.Name}___clientReconcileTick", FieldAttributes.Private, uintTr);
            FieldDefinition clientReplicateTickFd = new FieldDefinition($"{replicateMd.Name}___clientReplicateTick", FieldAttributes.Private, uintTr);
            FieldDefinition serverReceivedTickFd = new FieldDefinition($"{replicateMd.Name}___serverReceivedTick", FieldAttributes.Private, uintTr);
            FieldDefinition serverReplicatesReadBufferFd = new FieldDefinition($"{replicateMd.Name}___serverReplicateReadBuffer", FieldAttributes.Private, replicateDataArrTr);

            typeDef.Fields.Add(serverReplicatesFd);
            typeDef.Fields.Add(clientReplicatesFd);
            typeDef.Fields.Add(clientReconcileFd);
            typeDef.Fields.Add(serverReplicateTickFd);
            typeDef.Fields.Add(serverReconcileResendsFd);
            typeDef.Fields.Add(clientReplicateResendsFd);
            typeDef.Fields.Add(clientHasReconcileDataFd);
            typeDef.Fields.Add(clientReplayingDataaFd);
            typeDef.Fields.Add(clientReconcileTickFd);
            typeDef.Fields.Add(clientReplicateTickFd);
            typeDef.Fields.Add(serverReceivedTickFd);
            typeDef.Fields.Add(serverReplicatesReadBufferFd);

            predictionFields = new CreatedPredictionFields(serverReplicatesFd, clientReplicatesFd, clientReconcileFd, serverReplicateTickFd, serverReconcileResendsFd,
                clientReplicateResendsFd, clientHasReconcileDataFd, clientReplayingDataaFd, clientReconcileTickFd, clientReplicateTickFd,
                serverReceivedTickFd, serverReplicatesReadBufferFd);
        }

        /// <summary>
        /// Returns if there are any errors with the prediction methods parameters and will print if so.
        /// </summary>
        private bool HasParameterError(MethodDefinition methodDef, TypeDefinition typeDef, bool replicateMethod)
        {
            int count = (replicateMethod) ? 3 : 2;

            //Check parameter count.
            if (methodDef.Parameters.Count != count)
            {
                PrintParameterExpectations();
                return true;
            }

            ////Make sure first parameter is class or struct.
            //if (!methodDef.Parameters[0].ParameterType.IsClassOrStruct(base.Session))
            //{
            //    base.LogError($"Prediction methods must use a class or structure as the first parameter type. Structures are recommended to avoid allocations.");
            //    return true;
            //}

            //Make sure remaining parameters are booleans.
            for (int i = 1; i < count; i++)
            {
                ParameterDefinition pd = methodDef.Parameters[i];
                if (pd.ParameterType.Name != typeof(bool).Name)
                {
                    PrintParameterExpectations();
                    return true;
                }

            }

            void PrintParameterExpectations()
            {
                if (replicateMethod)
                    base.LogError($"Replicate method {methodDef.Name} within {typeDef.Name} requires exactly 3 parameters. The first parameter must be the data to replicate, second a boolean indicating if being run asServer, and third a boolean indicating if data is being replayed.");
                else
                    base.LogError($"Reconcile method {methodDef.Name} within {typeDef.Name} requires exactly 2 parameters. The first parameter must be the data to reconcile with, and the second a boolean indicating if being run asServer.");
            }

            //No errors with parameters.
            return false;
        }

        /// <summary>
        /// Creates all methods needed for a RPC.
        /// </summary>
        /// <param name="originalMethodDef"></param>
        /// <param name="rpcAttribute"></param>
        /// <returns></returns>
        private bool CreatePredictionMethods(TypeDefinition typeDef, MethodDefinition replicateMd, MethodDefinition reconcileMd, CreatedPredictionFields predictionFields, uint rpcCount, out PredictionReaders predictionReaders)
        {
            predictionReaders = null;

            string copySuffix = "___UserLogic";
            MethodDefinition replicateUserMd = base.GetClass<GeneralHelper>().CopyIntoNewMethod(replicateMd, $"{replicateMd.Name}{copySuffix}", out _);
            MethodDefinition reconcileUserMd = base.GetClass<GeneralHelper>().CopyIntoNewMethod(reconcileMd, $"{reconcileMd.Name}{copySuffix}", out _);
            replicateMd.Body.Instructions.Clear();
            reconcileMd.Body.Instructions.Clear();

            MethodDefinition replicateReader;
            MethodDefinition reconcileReader;

            if (!CreateReplicate())
                return false;
            if (!CreateReconcile())
                return false;

            CreateClearReplicateCacheMethod(typeDef, replicateMd.Parameters[0].ParameterType, predictionFields);
            ServerCreateReplicateReader(typeDef, replicateMd, predictionFields, out replicateReader);
            ClientCreateReconcileReader(typeDef, reconcileMd, predictionFields, out reconcileReader);
            predictionReaders = new PredictionReaders(replicateReader, reconcileReader);

            bool CreateReplicate()
            {
                ILProcessor processor = replicateMd.Body.GetILProcessor();
                ParameterDefinition asServerPd = replicateMd.Parameters[1];

                //Universal conditions.
                CreateReplicateConditions(replicateMd, predictionFields);

                //Wrap server content in an asServer if statement.
                Instruction afterAsServerInst = processor.Create(OpCodes.Nop);
                processor.Emit(OpCodes.Ldarg, asServerPd);
                processor.Emit(OpCodes.Brfalse, afterAsServerInst);
                /***************************/
                ServerCreateReplicate(replicateMd, predictionFields);
                /***************************/
                processor.Append(afterAsServerInst);

                //Wrap client content in an !asServer if statement.
                Instruction afterNotAsServerInst = processor.Create(OpCodes.Nop);
                processor.Emit(OpCodes.Ldarg, asServerPd);
                processor.Emit(OpCodes.Brtrue, afterNotAsServerInst);
                /***************************/
                ClientCreateReplicate(replicateMd, predictionFields, rpcCount);
                /***************************/
                processor.Append(afterNotAsServerInst);

                //Call user instr method.
                base.GetClass<GeneralHelper>().CallCopiedMethod(replicateMd, replicateUserMd);
                processor.Emit(OpCodes.Ret);

                return true;
            }


            bool CreateReconcile()
            {
                ILProcessor processor = reconcileMd.Body.GetILProcessor();
                ParameterDefinition asServerPd = reconcileMd.Parameters[1];

                //Wrap server content in an asServer if statement.
                Instruction afterAsServerInst = processor.Create(OpCodes.Nop);
                processor.Emit(OpCodes.Ldarg, asServerPd);
                processor.Emit(OpCodes.Brfalse, afterAsServerInst);
                /***************************/
                ServerCreateReconcile(reconcileMd, predictionFields, ref rpcCount);
                /***************************/
                processor.Emit(OpCodes.Ret);
                processor.Append(afterAsServerInst);


                ClientRetIfNoReconcile(reconcileMd, predictionFields);
                //      _clientHasReconcileData = false;
                processor.Add(ClientSetHasReconcileData(reconcileMd, false, predictionFields));

                //      if (base.IsServer) invoke reconciles, but do not reconcile.
                /* ClientHost does not reconcile but script may be dependent on the
                 * pre/post reconcile events so invoke those anyway. */
                Instruction afterClearReconcileInst = processor.Create(OpCodes.Nop);
                processor.Emit(OpCodes.Ldarg_0);
                processor.Emit(OpCodes.Call, base.GetClass<NetworkBehaviourHelper>().IsServer_MethodRef);
                processor.Emit(OpCodes.Brfalse, afterClearReconcileInst);
                //Invoke OnPre/PostReconcile.
                processor.Add(InvokeOnReconcile(reconcileMd, true));
                processor.Add(InvokeOnReconcile(reconcileMd, false));
                //Exit method.
                processor.Emit(OpCodes.Ret);
                processor.Append(afterClearReconcileInst);

                //Set data received to the reconcile parameter so that clients access the right data.
                SetReconcileData(reconcileMd, predictionFields);
                //      uint reconcileTick = r.Generated___Tick.
                VariableDefinition reconcileTickVd = reconcileMd.CreateVariable(base.Session, typeof(uint));
                processor.Emit(OpCodes.Ldarg, reconcileMd.Parameters[0]); //the data.
                processor.Emit(OpCodes.Ldfld, ReconcileData_Tick_FieldRef); //Generated___Tick field.
                processor.Emit(OpCodes.Stloc, reconcileTickVd);
                //      base.SetLastReconcileTick(reconcileTick).
                processor.Emit(OpCodes.Ldarg_0);
                processor.Emit(OpCodes.Ldloc, reconcileTickVd);
                processor.Emit(OpCodes.Call, base.GetClass<NetworkBehaviourHelper>().SetLastReconcileTick_MethodRef);
                //Invoke reconciling start. 
                processor.Add(InvokeOnReconcile(reconcileMd, true));

                //Call user instr method.
                base.GetClass<GeneralHelper>().CallCopiedMethod(reconcileMd, reconcileUserMd);

                ClientCreateReconcile(reconcileMd, replicateMd, predictionFields, reconcileTickVd);

                processor.Emit(OpCodes.Ret);
                return true;
            }

            return true;
        }

        #region Universal prediction.
        /// <summary>
        /// Creates an override for the method responsible for resetting replicates.
        /// </summary>
        /// <param name=""></param>
        /// <param name=""></param>
        private void CreateClearReplicateCacheMethod(TypeDefinition typeDef, TypeReference dataTr, CreatedPredictionFields predictionFields)
        {
            MethodDefinition md = typeDef.GetMethod(ClearReplicateCache_Method_Name);
            //Already exist when it shouldn't.
            if (md != null)
            {
                base.LogWarning($"{typeDef.Name} overrides method {md.Name} when it should not. Logic within {md.Name} will be replaced by code generation.");
                md.Body.Instructions.Clear();
            }
            else
            {
                md = new MethodDefinition(ClearReplicateCache_Method_Name, (MethodAttributes.Public | MethodAttributes.Virtual), base.Module.TypeSystem.Void);
                base.GetClass<GeneralHelper>().CreateParameter(md, typeof(bool), "asServer");
                typeDef.Methods.Add(md);
                base.ImportReference(md);
            }
             
            ILProcessor processor = md.Body.GetILProcessor();

            GenericInstanceType dataListGit;
            GetGenericLists(dataTr, out dataListGit);
            GenericInstanceType dataQueueGit;
            GetGenericQueues(dataTr, out dataQueueGit);
            //Get clear method.
            MethodReference lstClearMr = base.GetClass<GeneralHelper>().List_Clear_MethodRef.MakeHostInstanceGeneric(base.Session, dataListGit);
            MethodReference queueClearMr = base.GetClass<GeneralHelper>().Queue_Clear_MethodRef.MakeHostInstanceGeneric(base.Session, dataQueueGit);

            ParameterDefinition asServerPd = md.Parameters[0];

            Instruction afterAsServerInst = processor.Create(OpCodes.Nop);
            Instruction resetTicksInst = processor.Create(OpCodes.Nop);

            processor.Emit(OpCodes.Ldarg, asServerPd);
            processor.Emit(OpCodes.Brfalse_S, afterAsServerInst);
            //Clear on server replicates.
            processor.Emit(OpCodes.Ldarg_0);
            processor.Emit(OpCodes.Ldfld, predictionFields.ServerReplicateDatas);
            processor.Emit(OpCodes.Callvirt, queueClearMr);
            processor.Emit(OpCodes.Br_S, resetTicksInst);
            processor.Append(afterAsServerInst);
            //Clear on client replicates.
            processor.Emit(OpCodes.Ldarg_0);
            processor.Emit(OpCodes.Ldfld, predictionFields.ClientReplicateDatas);
            processor.Emit(OpCodes.Callvirt, lstClearMr);

            processor.Append(resetTicksInst);
            /* Reset last ticks. */
            //
            processor.Emit(OpCodes.Ldarg_0);
            processor.Emit(OpCodes.Ldc_I4_0);
            processor.Emit(OpCodes.Stfld, predictionFields.ClientReconcileTick);
            //
            processor.Emit(OpCodes.Ldarg_0);
            processor.Emit(OpCodes.Ldc_I4_0);
            processor.Emit(OpCodes.Stfld, predictionFields.ClientReplicateTick);
            //
            processor.Emit(OpCodes.Ldarg_0);
            processor.Emit(OpCodes.Ldc_I4_0);
            processor.Emit(OpCodes.Stfld, predictionFields.ServerReceivedTick);
            //
            processor.Emit(OpCodes.Ldarg_0);
            processor.Emit(OpCodes.Ldc_I4_0);
            processor.Emit(OpCodes.Stfld, predictionFields.ServerReplicateTick);
            processor.Emit(OpCodes.Ret);
        }
        /// <summary>
        /// Adds DATA_TICK_FIELD_NAME to dataTd.
        /// </summary>
        /// <param name="replicateDataTd"></param>
        private void AddTickFieldToDatas(TypeDefinition replicateDataTd, TypeDefinition reconcileDataTd)
        {
            Add(replicateDataTd, true);
            Add(reconcileDataTd, false);

            void Add(TypeDefinition td, bool replicate)
            {
                FieldReference fr = td.GetFieldReference(DATA_TICK_FIELD_NAME, base.Session);
                if (fr == null)
                {
                    TypeReference uintTr = base.GetClass<GeneralHelper>().GetTypeReference(typeof(uint));
                    FieldDefinition fd = new FieldDefinition(DATA_TICK_FIELD_NAME, FieldAttributes.Public, uintTr);
                    td.Fields.Add(fd);
                    fr = base.ImportReference(fd);
                }

                if (replicate)
                    ReplicateData_Tick_FieldRef = fr;
                else
                    ReconcileData_Tick_FieldRef = fr;
            }
        }

        /// <summary>
        /// Creates general conditions for replicate to run for server or client.
        /// </summary>
        private void CreateReplicateConditions(MethodDefinition replicateMd, CreatedPredictionFields predictionFields)
        {
            ILProcessor processor = replicateMd.Body.GetILProcessor();

            ParameterDefinition asServerPd = replicateMd.Parameters[1];

            //      if (asServer && !base.Owner.IsActive) return;
            Instruction afterNoOwnerCheckInst = processor.Create(OpCodes.Nop);
            processor.Emit(OpCodes.Ldarg, asServerPd);
            processor.Emit(OpCodes.Brfalse_S, afterNoOwnerCheckInst);
            processor.Emit(OpCodes.Ldarg_0);
            processor.Emit(OpCodes.Call, base.GetClass<NetworkBehaviourHelper>().Owner_MethodRef);
            processor.Emit(OpCodes.Callvirt, base.GetClass<ObjectHelper>().NetworkConnection_IsActive_MethodRef);
            processor.Emit(OpCodes.Brtrue_S, afterNoOwnerCheckInst);
            ClearReplicateCache(true, false);
            processor.Emit(OpCodes.Ret);
            processor.Append(afterNoOwnerCheckInst);

            //      if (!asServer && !base.IsOwner) return;
            Instruction afterClientCheckInst = processor.Create(OpCodes.Nop);
            processor.Emit(OpCodes.Ldarg, asServerPd);
            processor.Emit(OpCodes.Brtrue_S, afterClientCheckInst);
            processor.Emit(OpCodes.Ldarg_0);
            processor.Emit(OpCodes.Call, base.GetClass<NetworkBehaviourHelper>().IsOwner_MethodRef);
            processor.Emit(OpCodes.Brtrue_S, afterClientCheckInst);
            ClearReplicateCache(false, true);
            processor.Emit(OpCodes.Ret);
            processor.Append(afterClientCheckInst);

            //      if (asServer && base.IsOwner) 
            //clientHost does not replicate.
            Instruction afterAsServerIsClientInst = processor.Create(OpCodes.Nop);
            processor.Emit(OpCodes.Ldarg, asServerPd);
            processor.Emit(OpCodes.Brfalse_S, afterAsServerIsClientInst);
            processor.Emit(OpCodes.Ldarg_0);
            processor.Emit(OpCodes.Call, base.GetClass<NetworkBehaviourHelper>().IsOwner_MethodRef);
            processor.Emit(OpCodes.Brfalse_S, afterAsServerIsClientInst);
            ClearReplicateCache(true, true);
            processor.Emit(OpCodes.Ret);
            processor.Append(afterAsServerIsClientInst);

            void ClearReplicateCache(bool server, bool client)
            {
                if (server && client)
                {
                    processor.Emit(OpCodes.Ldarg_0);
                    processor.Emit(OpCodes.Call, base.GetClass<NetworkBehaviourHelper>().ClearReplicateCache_0P_MethodRef);
                }
                else
                {
                    processor.Emit(OpCodes.Ldarg_0);
                    OpCode opC = (server) ? OpCodes.Ldc_I4_1 : OpCodes.Ldc_I4_0;
                    processor.Emit(opC);
                    processor.Emit(OpCodes.Call, base.GetClass<NetworkBehaviourHelper>().ClearReplicateCache_1P_MethodRef);
                }
            }
        }

        /// <summary>
        /// Outputs generic lists for dataTr and uint.
        /// </summary>
        private void GetGenericLists(TypeReference dataTr, out GenericInstanceType lstData)
        {
            TypeReference listDataTr = base.ImportReference(typeof(List<>));
            lstData = listDataTr.MakeGenericInstanceType(new TypeReference[] { dataTr });
        }
        /// <summary>
        /// Outputs generic lists for dataTr and uint.
        /// </summary>
        private void GetGenericQueues(TypeReference dataTr, out GenericInstanceType queueData)
        {
            TypeReference queueDataTr = base.ImportReference(typeof(Queue<>));
            queueData = queueDataTr.MakeGenericInstanceType(new TypeReference[] { dataTr });
        }
        /// <summary>
        /// Adds to buffer at the front of methodDef.
        /// </summary>
        /// <param name=""></param>
        /// <param name="dataPd"></param>
        /// <param name="tickFd"></param>
        /// <param name="dataFd"></param>
        private void AddToReplicateBuffer(MethodDefinition methodDef, object dataDef, FieldDefinition dataFd)
        {
            TypeReference dataTr = null;
            if (dataDef is ParameterDefinition pd)
                dataTr = pd.ParameterType;
            else if (dataDef is VariableDefinition vd)
                dataTr = vd.VariableType;

            GenericInstanceType dataListGit;
            GetGenericLists(dataTr, out dataListGit);
            MethodReference dataAddMr = base.GetClass<GeneralHelper>().List_Add_MethodRef.MakeHostInstanceGeneric(base.Session, dataListGit);

            ILProcessor processor = methodDef.Body.GetILProcessor();

            //_dataLst.Add(dataPd);
            processor.Emit(OpCodes.Ldarg_0);
            processor.Emit(OpCodes.Ldfld, dataFd);

            if (dataDef is ParameterDefinition pd2)
                processor.Emit(OpCodes.Ldarg, pd2);
            else if (dataDef is VariableDefinition vd2)
                processor.Emit(OpCodes.Ldloc, vd2);
            processor.Emit(OpCodes.Callvirt, dataAddMr);
        }
        /// <summary>
        /// Removes countVd from list of dataFd starting at index 0.
        /// </summary>
        private List<Instruction> ListRemoveRange(MethodDefinition methodDef, FieldDefinition dataFd, TypeReference dataTr, VariableDefinition countVd)
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
            insts.Add(processor.Create(OpCodes.Callvirt, lstDataRemoveRangeMr));

            return insts;
        }
        /// <summary>
        /// Subtracts 1 from a field.
        /// </summary>
        private List<Instruction> SubtractFromField(MethodDefinition methodDef, FieldDefinition fieldDef)
        {
            List<Instruction> insts = new List<Instruction>();
            ILProcessor processor = methodDef.Body.GetILProcessor();

            //      _field--;
            insts.Add(processor.Create(OpCodes.Ldarg_0));
            insts.Add(processor.Create(OpCodes.Ldarg_0));
            insts.Add(processor.Create(OpCodes.Ldfld, fieldDef));
            insts.Add(processor.Create(OpCodes.Ldc_I4_1));
            insts.Add(processor.Create(OpCodes.Sub));
            insts.Add(processor.Create(OpCodes.Stfld, fieldDef));

            return insts;
        }
        /// <summary>
        /// Subtracts 1 from a variable.
        /// </summary>
        private List<Instruction> SubtractFromVariable(MethodDefinition methodDef, VariableDefinition variableDef)
        {
            List<Instruction> insts = new List<Instruction>();
            ILProcessor processor = methodDef.Body.GetILProcessor();

            //      variable--;
            insts.Add(processor.Create(OpCodes.Ldloc, variableDef));
            insts.Add(processor.Create(OpCodes.Ldc_I4_1));
            insts.Add(processor.Create(OpCodes.Sub));
            insts.Add(processor.Create(OpCodes.Stloc, variableDef));

            return insts;
        }

        /// <summary>
        /// Subtracts 1 from a variable.
        /// </summary>
        private List<Instruction> SubtractOneVariableFromAnother(MethodDefinition methodDef, VariableDefinition srcVd, VariableDefinition modifierVd)
        {
            List<Instruction> insts = new List<Instruction>();
            ILProcessor processor = methodDef.Body.GetILProcessor();

            //      variable -= v2;
            insts.Add(processor.Create(OpCodes.Ldloc, srcVd));
            insts.Add(processor.Create(OpCodes.Ldloc, modifierVd));
            insts.Add(processor.Create(OpCodes.Sub));
            insts.Add(processor.Create(OpCodes.Stloc, srcVd));

            return insts;
        }
        #endregion

        #region Server side.
        /// <summary>
        /// Creates replicate code for client.
        /// </summary>
        private void ServerCreateReplicate(MethodDefinition replicateMd, CreatedPredictionFields predictionFields)
        {
            //data.DATA_TICK_FIELD_NAME.
            ParameterDefinition replicateDataPd = replicateMd.Parameters[0];
            TypeReference replicateDataTr = replicateDataPd.ParameterType;

            ILProcessor processor = replicateMd.Body.GetILProcessor();

            /* If there is nothing buffered exit. */
            //Get count in buffered.
            GenericInstanceType queueDataGit;
            GetGenericQueues(replicateDataTr, out queueDataGit);
            MethodReference queueDataGetCountMr = base.GetClass<GeneralHelper>().Queue_get_Count_MethodRef.MakeHostInstanceGeneric(base.Session, queueDataGit);
            MethodReference queueDataGetItemMr = base.GetClass<GeneralHelper>().Queue_Dequeue_MethodRef.MakeHostInstanceGeneric(base.Session, queueDataGit);

            //      int queueCount = _buffered.Count.
            VariableDefinition queueCountVd = base.GetClass<GeneralHelper>().CreateVariable(replicateMd, typeof(int));
            processor.Emit(OpCodes.Ldarg_0);
            processor.Emit(OpCodes.Ldfld, predictionFields.ServerReplicateDatas);
            processor.Emit(OpCodes.Callvirt, queueDataGetCountMr);
            processor.Emit(OpCodes.Stloc, queueCountVd);
            /* If the queue count is 2 more than maximum
             * buffered then dequeue an extra one. Currently
             * the input will be lost. */
            //If (queueCount > 3)
            Instruction afterDequeueInst = processor.Create(OpCodes.Nop);
            processor.Emit(OpCodes.Ldloc, queueCountVd);
            processor.Emit(OpCodes.Ldc_I4_3);
            processor.Emit(OpCodes.Ble_S, afterDequeueInst);
            //_buffer.Dequeue();
            processor.Emit(OpCodes.Ldarg_0);
            processor.Emit(OpCodes.Ldfld, predictionFields.ServerReplicateDatas);
            processor.Emit(OpCodes.Callvirt, queueDataGetItemMr);
            processor.Emit(OpCodes.Pop);
            processor.Append(afterDequeueInst);

            //Replace with data from buffer.
            //      if (queueCount > 0)
            Instruction afterReplaceDataInst = processor.Create(OpCodes.Nop);
            processor.Emit(OpCodes.Ldloc, queueCountVd);
            processor.Emit(OpCodes.Ldc_I4_0);
            processor.Emit(OpCodes.Ble, afterReplaceDataInst);

            /* Set the data parameter to a dequeued entry. */
            //      dataPd = _buffered.Dequeue();
            processor.Emit(OpCodes.Ldarg_0);
            processor.Emit(OpCodes.Ldfld, predictionFields.ServerReplicateDatas);
            processor.Emit(OpCodes.Callvirt, queueDataGetItemMr);
            processor.Emit(OpCodes.Starg, replicateDataPd);

            /* Set last replicate tick. */
            //      _serverReplicateTick = dataPd.DATA_TICK_FIELD_NAME.
            processor.Emit(OpCodes.Ldarg_0);
            processor.Emit(OpCodes.Ldarg, replicateDataPd);
            processor.Emit(OpCodes.Ldfld, ReplicateData_Tick_FieldRef);
            processor.Emit(OpCodes.Stfld, predictionFields.ServerReplicateTick.CachedResolve(base.Session));
            //Update last replicate tick.
            //      base.SetLastReplicateTick(tick);
            processor.Emit(OpCodes.Ldarg_0);
            processor.Emit(OpCodes.Ldarg, replicateDataPd);
            processor.Emit(OpCodes.Ldfld, ReplicateData_Tick_FieldRef);
            processor.Emit(OpCodes.Call, base.GetClass<NetworkBehaviourHelper>().SetLastReplicateTick_MethodRef);

            //Reset reconcile ticks.
            //      _serverReconcileTicks = 3;
            processor.Emit(OpCodes.Ldarg_0);
            processor.Emit(RESEND_COUNT_OPCODE);
            processor.Emit(OpCodes.Stfld, predictionFields.ServerReconcileResends.CachedResolve(base.Session));

            processor.Append(afterReplaceDataInst);
        }

        /// <summary>
        /// Creates a reader for replicate data received from clients.
        /// </summary>
        private bool ServerCreateReplicateReader(TypeDefinition typeDef, MethodDefinition replicateMd, CreatedPredictionFields predictionFields, out MethodDefinition result)
        {
            string methodName = $"{REPLICATE_READER_PREFIX}{replicateMd.Name}";
            MethodDefinition createdMd = new MethodDefinition(methodName,
                    MethodAttributes.Private,
                    replicateMd.Module.TypeSystem.Void);
            typeDef.Methods.Add(createdMd);
            createdMd.Body.InitLocals = true;

            TypeReference replicateDataTr = replicateMd.Parameters[0].ParameterType;
            ILProcessor processor = createdMd.Body.GetILProcessor();

            //Create pooledreader parameter.
            ParameterDefinition readerPd = base.GetClass<GeneralHelper>().CreateParameter(createdMd, typeof(PooledReader));

            //Read into cache.
            //      int readCount = pooledReader.ReadToCollection(_serverReplicateReadBuffer);
            MethodReference genericReadMr = base.GetClass<ReaderHelper>().Reader_ReadToCollection_MethodRef.MakeGenericMethod(replicateDataTr);
            VariableDefinition readCountVd = createdMd.CreateVariable(base.Session, typeof(int));
            processor.Emit(OpCodes.Ldarg, readerPd);
            processor.Emit(OpCodes.Ldarg_0);
            processor.Emit(OpCodes.Ldflda, predictionFields.ServerReplicateReaderBuffer);
            //processor.Emit(OpCodes.Ldloca, replicateDataArrVd);
            processor.Emit(OpCodes.Callvirt, genericReadMr);
            processor.Emit(OpCodes.Stloc, readCountVd);

            //Create NetworkConnection parameter to compare owner.
            ParameterDefinition networkConnectionPd = base.GetClass<GeneralHelper>().CreateParameter(createdMd, typeof(NetworkConnection));
            //      if (base.ComparerOwner(networkConnectionPd) return;
            base.GetClass<NetworkBehaviourHelper>().CreateRemoteClientIsOwnerCheck(processor, networkConnectionPd);

            //Make a local array of same type for easier handling and set it's reference to field.
            VariableDefinition replicateDataArrVd = createdMd.CreateVariable(predictionFields.ServerReplicateReaderBuffer.FieldType);
            processor.Emit(OpCodes.Ldarg_0);
            processor.Emit(OpCodes.Ldfld, predictionFields.ServerReplicateReaderBuffer);
            processor.Emit(OpCodes.Stloc, replicateDataArrVd);

            /* Store queue count into queueCount. */
            //START //Method references for uint get_Count.
            GenericInstanceType queueDataGit;
            GetGenericQueues(replicateDataTr, out queueDataGit);
            MethodReference queueDataGetCountMr = base.GetClass<GeneralHelper>().Queue_get_Count_MethodRef.MakeHostInstanceGeneric(base.Session, queueDataGit);
            MethodReference queueDataEnqueueMr = base.GetClass<GeneralHelper>().Queue_Enqueue_MethodRef.MakeHostInstanceGeneric(base.Session, queueDataGit);
            MethodReference queueDataDequeueMr = base.GetClass<GeneralHelper>().Queue_Dequeue_MethodRef.MakeHostInstanceGeneric(base.Session, queueDataGit);
            //END //Method references for uint get_Count.

            /* Add array entries to buffered. */
            //      for (int i = 0; i < dataArr.Length; i++)
            //      {
            //          Data d = dataArr[i];
            //          if (d.Tick > this.lastTick)
            //            _serverReplicateDatas.Add(d);
            //            this.lastTick = d.Tick;
            //      }

            VariableDefinition iteratorVd = base.GetClass<GeneralHelper>().CreateVariable(createdMd, typeof(int));
            Instruction iteratorComparerInst = processor.Create(OpCodes.Ldloc, iteratorVd);
            Instruction iteratorLogicInst = processor.Create(OpCodes.Nop);
            Instruction iteratorIncreaseComparerInst = processor.Create(OpCodes.Ldloc, iteratorVd);
            //      for (int i = 0
            processor.Emit(OpCodes.Ldc_I4_0);
            processor.Emit(OpCodes.Stloc, iteratorVd);
            processor.Emit(OpCodes.Br_S, iteratorComparerInst);
            //Logic.
            processor.Append(iteratorLogicInst);

            //Store the data tick.
            VariableDefinition dataTickVd = base.GetClass<GeneralHelper>().CreateVariable(createdMd, typeof(int));
            processor.Emit(OpCodes.Ldloc, replicateDataArrVd);
            processor.Emit(OpCodes.Ldloc, iteratorVd);
            processor.Emit(OpCodes.Ldelema, replicateDataTr);
            processor.Emit(OpCodes.Ldfld, ReplicateData_Tick_FieldRef);
            processor.Emit(OpCodes.Stloc, dataTickVd);

            processor.Emit(OpCodes.Ldloc, dataTickVd);
            processor.Emit(OpCodes.Ldarg_0);
            processor.Emit(OpCodes.Ldfld, predictionFields.ServerReceivedTick);
            processor.Emit(OpCodes.Ble_S, iteratorIncreaseComparerInst);
            //Add to buffer.
            processor.Emit(OpCodes.Ldarg_0);
            processor.Emit(OpCodes.Ldfld, predictionFields.ServerReplicateDatas);
            processor.Emit(OpCodes.Ldloc, replicateDataArrVd);
            processor.Emit(OpCodes.Ldloc, iteratorVd);
            processor.Emit(OpCodes.Ldelem_Any, replicateDataTr);
            processor.Emit(OpCodes.Callvirt, queueDataEnqueueMr);

            //Set serverReceivedTick.
            processor.Emit(OpCodes.Ldarg_0);
            processor.Emit(OpCodes.Ldloc, dataTickVd);
            processor.Emit(OpCodes.Stfld, predictionFields.ServerReceivedTick);
            //      ; i++)
            processor.Append(iteratorIncreaseComparerInst); //(OpCodes.Ldloc, iteratorVd);
            processor.Emit(OpCodes.Ldc_I4_1);
            processor.Emit(OpCodes.Add);
            processor.Emit(OpCodes.Stloc_S, iteratorVd);
            //      ;i < arr.Length
            processor.Append(iteratorComparerInst); //(OpCodes.Ldloc, iterator);
            processor.Emit(OpCodes.Ldloc, readCountVd);
            processor.Emit(OpCodes.Conv_I4);
            processor.Emit(OpCodes.Blt_S, iteratorLogicInst);

            /* Remove entries which exceed maximum buffer. */
            VariableDefinition queueCountVd = base.GetClass<GeneralHelper>().CreateVariable(createdMd, typeof(int));
            ////Get maximum buffered.
            ////      byte maximumBufferdInputs = base.TimeManager.MaximumBufferedInputs.
            //VariableDefinition maximumBufferedVd = base.GetClass<GeneralHelper>().CreateVariable(createdMd, typeof(byte));
            //processor.Emit(OpCodes.Ldarg_0); //base.
            //processor.Emit(OpCodes.Call, base.GetClass<NetworkBehaviourHelper>().TimeManager_MethodRef);
            //processor.Emit(OpCodes.Callvirt, base.GetClass<TimeManagerHelper>().MaximumBufferedInputs_MethodRef); 
            //processor.Emit(OpCodes.Stloc, maximumBufferedVd);
            //Set queueCountVd to new count.
            processor.Emit(OpCodes.Ldarg_0);
            processor.Emit(OpCodes.Ldfld, predictionFields.ServerReplicateDatas);
            processor.Emit(OpCodes.Callvirt, queueDataGetCountMr);
            processor.Emit(OpCodes.Stloc, queueCountVd);

            //Get number of inputs to remove. Will be positive if there are too many buffered inputs.
            //      int queueCount -= maximumBuffered.
            processor.Emit(OpCodes.Ldloc, queueCountVd);
            //processor.Emit(OpCodes.Ldloc, maximumBufferedVd);
            processor.Emit(OpCodes.Ldc_I4, 15); //maximumBufferedInputs const value.
            processor.Emit(OpCodes.Sub);
            processor.Emit(OpCodes.Stloc, queueCountVd);
            //If remove count is positive.
            //      if (queueCount > 0)
            Instruction afterRemoveRangeInst = processor.Create(OpCodes.Nop);
            processor.Emit(OpCodes.Ldloc, queueCountVd);
            processor.Emit(OpCodes.Ldc_I4_0);
            processor.Emit(OpCodes.Ble_S, afterRemoveRangeInst);

            Instruction dequeueComparerInst = processor.Create(OpCodes.Nop);
            Instruction dequeueLogicInst = processor.Create(OpCodes.Nop);
            //Reuse iteratorVd
            processor.Emit(OpCodes.Ldc_I4_0);
            processor.Emit(OpCodes.Stloc, iteratorVd);
            processor.Emit(OpCodes.Br_S, dequeueComparerInst);

            //Logic.
            processor.Append(dequeueLogicInst);
            processor.Emit(OpCodes.Ldarg_0);
            processor.Emit(OpCodes.Ldfld, predictionFields.ServerReplicateDatas);
            processor.Emit(OpCodes.Callvirt, queueDataDequeueMr);
            processor.Emit(OpCodes.Pop);

            //Increase iterator.
            processor.Emit(OpCodes.Ldloc, iteratorVd);
            processor.Emit(OpCodes.Ldc_I4_1);
            processor.Emit(OpCodes.Add);
            processor.Emit(OpCodes.Stloc, iteratorVd);

            //ComparerJmp.
            processor.Append(dequeueComparerInst);
            processor.Emit(OpCodes.Ldloc, iteratorVd);
            processor.Emit(OpCodes.Ldloc, queueCountVd);
            processor.Emit(OpCodes.Blt_S, dequeueLogicInst);

            processor.Append(afterRemoveRangeInst);


            //Add end of method.
            processor.Emit(OpCodes.Ret);

            result = createdMd;
            return true;
        }

        /// <summary>
        /// Creates server side code for reconcileMd.
        /// </summary>
        /// <param name="reconcileMd"></param>
        /// <returns></returns>
        private void ServerCreateReconcile(MethodDefinition reconcileMd, CreatedPredictionFields predictionFields, ref uint rpcHash)
        {
            ParameterDefinition reconcileDataPd = reconcileMd.Parameters[0];
            List<Instruction> insts = new List<Instruction>();
            ILProcessor processor = reconcileMd.Body.GetILProcessor();

            GenericInstanceMethod sendReconcileRpcdMr = base.GetClass<NetworkBehaviourHelper>().SendReconcileRpc_MethodRef.MakeGenericMethod(new TypeReference[] { reconcileDataPd.ParameterType });

            Instruction afterRetInst = processor.Create(OpCodes.Nop);
            //      if (serverReconcileResends == 0)
            processor.Emit(OpCodes.Ldarg_0);
            processor.Emit(OpCodes.Ldfld, predictionFields.ServerReconcileResends);
            processor.Emit(OpCodes.Brtrue_S, afterRetInst);
            processor.Emit(OpCodes.Ret);
            processor.Append(afterRetInst);

            //      bool firstSend = (_serverReconcileResends == 3);
            VariableDefinition firstSendVd = reconcileMd.CreateVariable(base.Session, typeof(bool));
            Instruction afterFirstSendSetInst = processor.Create(OpCodes.Nop);
            processor.Emit(OpCodes.Ldarg_0);
            processor.Emit(OpCodes.Ldfld, predictionFields.ServerReconcileResends);
            processor.Emit(RESEND_COUNT_OPCODE);
            processor.Emit(OpCodes.Bne_Un_S, afterFirstSendSetInst);
            processor.Emit(OpCodes.Ldc_I4_1);
            processor.Emit(OpCodes.Stloc, firstSendVd);
            processor.Append(afterFirstSendSetInst);

            //processor.Emit(OpCodes.Ceq);
            //      _serverReconcileResends--;
            processor.Add(SubtractFromField(reconcileMd, predictionFields.ServerReconcileResends.CachedResolve(base.Session)));

            //Set channel based on if last resend.
            VariableDefinition channelVd = reconcileMd.CreateVariable(base.Session, typeof(Channel));
            //Default channel to unreliable.
            processor.Emit(OpCodes.Ldc_I4, (int)Channel.Unreliable);
            processor.Emit(OpCodes.Stloc, channelVd);
            //Update channel to reliable if last reconcile.
            Instruction afterChannelReliableInst = processor.Create(OpCodes.Nop);
            processor.Emit(OpCodes.Ldarg_0);
            processor.Emit(OpCodes.Ldfld, predictionFields.ServerReconcileResends);
            processor.Emit(OpCodes.Brtrue_S, afterChannelReliableInst);
            processor.Emit(OpCodes.Ldc_I4, (int)Channel.Reliable);
            processor.Emit(OpCodes.Stloc, channelVd);
            processor.Append(afterChannelReliableInst);

            //      Replace data.DATA_TICK_FIELD_NAME with last tick replicated.
            OpCode ldArgOC0 = (reconcileDataPd.ParameterType.IsValueType) ? OpCodes.Ldarga : OpCodes.Ldarg;
            processor.Emit(ldArgOC0, reconcileDataPd);
            processor.Emit(OpCodes.Ldarg_0);
            processor.Emit(OpCodes.Ldfld, predictionFields.ServerReplicateTick);
            processor.Emit(OpCodes.Stfld, ReconcileData_Tick_FieldRef);

            //      base.SetlastReconcileTick(
            processor.Emit(OpCodes.Ldarg_0);
            processor.Emit(OpCodes.Ldarg_0);
            processor.Emit(OpCodes.Ldfld, predictionFields.ServerReplicateTick);
            processor.Emit(OpCodes.Call, base.GetClass<NetworkBehaviourHelper>().SetLastReconcileTick_MethodRef);

            Instruction afterSendRigidbodyStatesInst = processor.Create(OpCodes.Nop);

            //      if (firstSend)
            //          PredictedObject.SendRigidbodyStatesInternal(this).
            processor.Emit(OpCodes.Ldloc, firstSendVd);
            processor.Emit(OpCodes.Brfalse_S, afterSendRigidbodyStatesInst);
            //SendRigidbodyStatesInternal.
            processor.Emit(OpCodes.Ldarg_0);
            processor.Emit(OpCodes.Call, base.GetClass<PredictedObjectHelper>().SendRigidbodyStatesInternal_MethodRef);
            processor.Append(afterSendRigidbodyStatesInst);
            //      base.SendReconcileRpc(hash, data, channel);
            processor.Emit(OpCodes.Ldarg_0);
            processor.Emit(OpCodes.Ldc_I4, (int)rpcHash);
            processor.Emit(OpCodes.Ldarg, reconcileDataPd);
            processor.Emit(OpCodes.Ldloc, channelVd); 
            processor.Emit(OpCodes.Call, sendReconcileRpcdMr);

            processor.Add(insts);
        }
        #endregion

        #region Client side.
        /// <summary>
        /// Creates replicate code for client.
        /// </summary>
        private void ClientCreateReplicate(MethodDefinition replicateMd, CreatedPredictionFields predictionFields, uint rpcCount)
        {
            ParameterDefinition replicateDataPd = replicateMd.Parameters[0];

            ILProcessor processor = replicateMd.Body.GetILProcessor();

            Instruction afterNetworkLogicInst = processor.Create(OpCodes.Nop);
            //      if (_replaying) skip sending logic.
            processor.Emit(OpCodes.Ldarg_0);
            processor.Emit(OpCodes.Ldfld, predictionFields.ClientReplayingData);
            processor.Emit(OpCodes.Brtrue, afterNetworkLogicInst);
            //      if (base.IsServer) skip sending, host doesn't need to send.
            processor.Emit(OpCodes.Ldarg_0);
            processor.Emit(OpCodes.Call, base.GetClass<NetworkBehaviourHelper>().IsServer_MethodRef);
            processor.Emit(OpCodes.Brtrue, afterNetworkLogicInst);

            //Sets isDefault to if dataPd is default value.
            VariableDefinition isDefaultVd;
            ClientIsDefault(replicateMd, replicateDataPd, out isDefaultVd);
            //Resets clientReplicateResends if dataPd is not default.
            ClientResetResends(replicateMd, predictionFields, isDefaultVd);
            //Exits method if client has no resends remaining.
            ClientSkipIfNoResends(replicateMd, predictionFields, afterNetworkLogicInst);
            //Decreases clientReplicateResends.
            processor.Add(SubtractFromField(replicateMd, predictionFields.ClientReplicateResends.CachedResolve(base.Session)));
            //Sets TimeManager.LocalTick to data.
            ClientSetReplicateDataTick(replicateMd, replicateDataPd, predictionFields, isDefaultVd);
            //Adds data to client buffer.

            //      if (!isDefaultData) _replicateDatas.Add....
            Instruction afterAddToBufferInst = processor.Create(OpCodes.Nop);
            processor.Emit(OpCodes.Ldloc, isDefaultVd);
            processor.Emit(OpCodes.Brtrue_S, afterAddToBufferInst);
            AddToReplicateBuffer(replicateMd, replicateDataPd, predictionFields.ClientReplicateDatas.CachedResolve(base.Session));
            processor.Append(afterAddToBufferInst);
            //Calls to send buffer to server.
            ClientSendInput(replicateMd, rpcCount, predictionFields);
            //Add instructions to beginning of method.
            processor.Append(afterNetworkLogicInst);
        }


        /// <summary>
        /// Exits method if no more client replicate resends are available.
        /// </summary>
        private void ClientSkipIfNoResends(MethodDefinition methodDef, CreatedPredictionFields predictionFields, Instruction skipInst)
        {
            ILProcessor processor = methodDef.Body.GetILProcessor();

            processor.Emit(OpCodes.Ldarg_0);
            processor.Emit(OpCodes.Ldfld, predictionFields.ClientReplicateResends);
            processor.Emit(OpCodes.Brfalse_S, skipInst);
        }

        /// <summary>
        /// Resets clientReplicateResends if isDefaultVd is false.
        /// </summary>
        private void ClientResetResends(MethodDefinition methodDef, CreatedPredictionFields predictionFields, VariableDefinition isDefaultVd)
        {
            ILProcessor processor = methodDef.Body.GetILProcessor();

            Instruction afterResetInst = processor.Create(OpCodes.Nop);
            processor.Emit(OpCodes.Ldloc, isDefaultVd);
            processor.Emit(OpCodes.Brtrue_S, afterResetInst);
            //      _clientReplicateResends = 3.
            processor.Emit(OpCodes.Ldarg_0);
            processor.Emit(RESEND_COUNT_OPCODE);
            processor.Emit(OpCodes.Stfld, predictionFields.ClientReplicateResends);
            processor.Append(afterResetInst);
        }

        /// <summary>
        /// Creates an IsDefault check on dataPd and returns instructions, also outputting boolean variable.
        /// </summary>
        /// <param name="methodDef"></param>
        /// <param name="dataPd"></param>
        /// <returns></returns>
        private void ClientIsDefault(MethodDefinition methodDef, ParameterDefinition dataPd, out VariableDefinition boolVd)
        {
            ILProcessor processor = methodDef.Body.GetILProcessor();

            boolVd = base.GetClass<GeneralHelper>().CreateVariable(methodDef, typeof(bool));
            //If client has no more resends and passedin default for data.
            //      if (!asServer && _clientReplicateResends == 0 && dataPd == default) return;
            MethodReference genericIsDefaultMr = base.GetClass<GeneralHelper>().Comparers_IsDefault_MethodRef.MakeGenericMethod(
                new TypeReference[] { dataPd.ParameterType });

            //Set to not default by ....default.
            //      default = false;
            processor.Emit(OpCodes.Ldc_I4_0);
            processor.Emit(OpCodes.Stloc, boolVd);

            Instruction afterSetDefaultInst = processor.Create(OpCodes.Nop);
            /* If PredictedObject.InstantiatedRigidbodyCount is greater than
             * 0 then states must be updated regularly due to potential changes
             * on server-side physics. When the count is larger than 0
             * do not check setting isDefault; this will force client to replicate
             * with default input, and in result the server will reconcile with the
             * rigidbody states of PredictedObjects. This will be optimized later
             * to use less bandwidth but for the time being PredictedObject states
             * must be regularly updated using this technique. */
            //      if (PredictedObject.InstantiatedRigidbodyCount == 0 && !base.TransformMayChange() && Comparers.IsDefault<T>())
            //          default = true;
            processor.Emit(OpCodes.Call, base.GetClass<PredictedObjectHelper>().InstantiatedRigidbodyCountInternal_Get_MethodRef);
            processor.Emit(OpCodes.Brtrue, afterSetDefaultInst);

            processor.Emit(OpCodes.Ldarg_0);
            processor.Emit(OpCodes.Call, base.GetClass<NetworkBehaviourHelper>().TransformMayChange_MethodRef);
            processor.Emit(OpCodes.Brtrue_S, afterSetDefaultInst);
            processor.Emit(OpCodes.Ldarg, dataPd);
            processor.Emit(OpCodes.Call, genericIsDefaultMr);
            processor.Emit(OpCodes.Brfalse_S, afterSetDefaultInst);
            processor.Emit(OpCodes.Ldc_I4_1);
            processor.Emit(OpCodes.Stloc, boolVd);
            processor.Append(afterSetDefaultInst);
        }
        /// <summary>
        /// Sets data.DATA_TICK_FIELD_NAME to TimeManager.LocalTick.
        /// </summary>
        private void ClientSetReplicateDataTick(MethodDefinition methodDef, ParameterDefinition dataPd, CreatedPredictionFields predictionFields, VariableDefinition isDefaultVd)
        {
            ILProcessor processor = methodDef.Body.GetILProcessor();

            VariableDefinition tickVd = base.GetClass<GeneralHelper>().CreateVariable(methodDef, typeof(uint));
            Instruction afterCallLocalTickInst = processor.Create(OpCodes.Nop);
            Instruction afterUseClientReplicateTickInst = processor.Create(OpCodes.Nop);
            /*      uint localTIck;
            /*      if (!isDefaultData)
             *          localTick = base.TimeManager.LocalTick; 
             *          _clientReplicateTick = localTick;   
             *      else
             *          localTick = _clientReplicateTick;   */
            //      if (!isDefault) localTick = base.TimeManager.LocalTick;
            processor.Emit(OpCodes.Ldloc, isDefaultVd);
            processor.Emit(OpCodes.Brtrue_S, afterCallLocalTickInst);
            processor.Emit(OpCodes.Ldarg_0);
            processor.Emit(OpCodes.Call, base.GetClass<NetworkBehaviourHelper>().TimeManager_MethodRef);
            processor.Emit(OpCodes.Callvirt, base.GetClass<TimeManagerHelper>().LocalTick_MethodRef);
            processor.Emit(OpCodes.Stloc, tickVd);
            //      _clientReplicateTick = localTick;
            processor.Emit(OpCodes.Ldarg_0);
            processor.Emit(OpCodes.Ldloc, tickVd);
            processor.Emit(OpCodes.Stfld, predictionFields.ClientReplicateTick);
            processor.Emit(OpCodes.Br_S, afterUseClientReplicateTickInst);
            //ELSE
            //      localTick = _clientReplicateTick;
            processor.Append(afterCallLocalTickInst);
            processor.Emit(OpCodes.Ldarg_0);
            processor.Emit(OpCodes.Ldfld, predictionFields.ClientReplicateTick);
            processor.Emit(OpCodes.Stloc, tickVd);
            processor.Append(afterUseClientReplicateTickInst);

            //      data.DATA_TICK_FIELD_NAME = tick.
            OpCode ldArgOC = (dataPd.ParameterType.IsValueType) ? OpCodes.Ldarga : OpCodes.Ldarg;
            processor.Emit(ldArgOC, dataPd);
            processor.Emit(OpCodes.Ldloc, tickVd);
            processor.Emit(OpCodes.Stfld, ReplicateData_Tick_FieldRef.CachedResolve(base.Session));

            //      base.SetLastReplicateTick(tick);
            processor.Emit(OpCodes.Ldarg_0);
            processor.Emit(OpCodes.Ldloc, tickVd);
            processor.Emit(OpCodes.Call, base.GetClass<NetworkBehaviourHelper>().SetLastReplicateTick_MethodRef);
        }
        /// <summary>
        /// Sends clients inputs to server.
        /// </summary>
        private void ClientSendInput(MethodDefinition replicateMd, uint hash, CreatedPredictionFields predictionFields)
        {
            ParameterDefinition dataPd = replicateMd.Parameters[0];
            TypeReference dataTr = dataPd.ParameterType;

            ILProcessor processor = replicateMd.Body.GetILProcessor();

            //Make method reference NB.SendReplicateRpc<dataTr>
            GenericInstanceMethod sendReplicateRpcdMr = base.GetClass<NetworkBehaviourHelper>().SendReplicateRpc_MethodRef.MakeGenericMethod(new TypeReference[] { dataTr });

            //Call WriteBufferedInput.
            //      base.WriteBufferedInput<dataTd>(hash, _clientBuffered, count);
            processor.Emit(OpCodes.Ldarg_0);
            processor.Emit(OpCodes.Ldc_I4, (int)hash);
            processor.Emit(OpCodes.Ldarg_0);
            processor.Emit(OpCodes.Ldfld, predictionFields.ClientReplicateDatas);
            processor.Emit(OpCodes.Ldc_I4_5); //past inputs, hardcoded for now.
            processor.Emit(OpCodes.Call, sendReplicateRpcdMr);
        }


        /// <summary>
        /// Creates a return if client does not have reconcile data.
        /// </summary>
        private bool ClientRetIfNoReconcile(MethodDefinition reconcileMd, CreatedPredictionFields predictionFields)
        {
            ILProcessor processor = reconcileMd.Body.GetILProcessor();

            Instruction afterHasDataCheckInst = processor.Create(OpCodes.Nop);
            //      if (!hasReconcileData) return;
            processor.Emit(OpCodes.Ldarg_0);
            processor.Emit(OpCodes.Ldfld, predictionFields.ClientHasReconcileData);
            processor.Emit(OpCodes.Brtrue_S, afterHasDataCheckInst);
            processor.Emit(OpCodes.Ret);
            processor.Append(afterHasDataCheckInst);

            return true;
        }


        /// <summary>
        /// Sets stored reconcile data to the parameter of reconcileMd if !asServer.
        /// </summary>
        private void SetReconcileData(MethodDefinition reconcileMd, CreatedPredictionFields predictionFields)
        {
            ILProcessor processor = reconcileMd.Body.GetILProcessor();

            ParameterDefinition reconcileDataPd = reconcileMd.Parameters[0];

            //      reconcileDataPd = _clientReconcileData.
            processor.Emit(OpCodes.Ldarg_0);
            processor.Emit(OpCodes.Ldfld, predictionFields.ReconcileData);
            processor.Emit(OpCodes.Starg, reconcileDataPd);
        }

        /// <summary>
        /// Syncs transforms if simulateVd is true.
        /// </summary>
        private void ClientSyncTransforms(MethodDefinition methodDef)
        {
            ILProcessor processor = methodDef.Body.GetILProcessor();
            processor.Emit(OpCodes.Call, Physics2D_SyncTransforms_MethodRef);
            processor.Emit(OpCodes.Call, Physics3D_SyncTransforms_MethodRef);
        }


        /// <summary>
        /// Simulates physics if simulateVd is true. Use null on simulateVd to skip check.
        /// </summary>
        private List<Instruction> ClientTrySimulatePhysics(MethodDefinition methodDef, VariableDefinition simulateVd, VariableDefinition tickDeltaVd, VariableDefinition physicsScene3DVd, VariableDefinition physicsScene2DVd)
        {
            List<Instruction> insts = new List<Instruction>();
            ILProcessor processor = methodDef.Body.GetILProcessor();

            Instruction afterSimulateInst = null;
            if (simulateVd != null)
            {
                //      if (simulate) {
                afterSimulateInst = processor.Create(OpCodes.Nop);
                insts.Add(processor.Create(OpCodes.Ldloc, simulateVd));
                insts.Add(processor.Create(OpCodes.Brfalse_S, afterSimulateInst));
            }

            AddSimulate(physicsScene3DVd, Physics3D_Simulate_MethodRef);
            AddSimulate(physicsScene2DVd, Physics2D_Simulate_MethodRef);

            void AddSimulate(VariableDefinition physicsSceneVd, MethodReference simulateMr)
            {
                insts.Add(processor.Create(OpCodes.Ldloca_S, physicsSceneVd));
                insts.Add(processor.Create(OpCodes.Ldloc, tickDeltaVd));
                insts.Add(processor.Create(OpCodes.Conv_R4));
                insts.Add(processor.Create(OpCodes.Call, simulateMr));
                //If is 2d simulate then pop result. 2D uses a bool return while 3D uses a void.
                if (simulateMr == Physics2D_Simulate_MethodRef)
                    insts.Add(processor.Create(OpCodes.Pop));
            }

            if (simulateVd != null)
                insts.Add(afterSimulateInst);

            return insts;
        }

        /// <summary>
        /// Creates and outputs a bool to indicate if physics must be simulated manually.
        /// </summary>
        private void ClientCreateSimulatePhysicsBool(MethodDefinition methodDef, out VariableDefinition simulateVd)
        {
            ILProcessor processor = methodDef.Body.GetILProcessor();

            simulateVd = base.GetClass<GeneralHelper>().CreateVariable(methodDef, typeof(bool));

            processor.Emit(OpCodes.Ldarg_0);
            processor.Emit(OpCodes.Call, base.GetClass<NetworkBehaviourHelper>().TimeManager_MethodRef);
            processor.Emit(OpCodes.Call, base.GetClass<TimeManagerHelper>().PhysicsMode_MethodRef);
            processor.Emit(OpCodes.Ldc_I4, (int)PhysicsMode.TimeManager);
            processor.Emit(OpCodes.Ceq);
            processor.Emit(OpCodes.Stloc, simulateVd);

        }

        /// <summary>
        /// Sets ClientHasReconcileData.
        /// </summary>
        private List<Instruction> ClientSetHasReconcileData(MethodDefinition methodDef, bool hasData, CreatedPredictionFields predictionFields)
        {
            List<Instruction> insts = new List<Instruction>();
            ILProcessor processor = methodDef.Body.GetILProcessor();

            int boolValue = (hasData) ? 1 : 0;
            insts.Add(processor.Create(OpCodes.Ldarg_0));
            insts.Add(processor.Create(OpCodes.Ldc_I4, boolValue));
            insts.Add(processor.Create(OpCodes.Stfld, predictionFields.ClientHasReconcileData));

            return insts;
        }

        /// <summary>
        /// Removes replicates prior and at index.
        /// </summary>
        private void ClientRemoveFromCache(MethodDefinition reconcileMd, MethodDefinition replicateMd,
            CreatedPredictionFields predictionFields, VariableDefinition reconcileTickVd)
        {
            ParameterDefinition reconcileDataPd = reconcileMd.Parameters[0];
            TypeReference replicateDataTr = replicateMd.Parameters[0].ParameterType;
            ILProcessor processor = reconcileMd.Body.GetILProcessor();

            VariableDefinition foundIndexVd = base.GetClass<GeneralHelper>().CreateVariable(reconcileMd, typeof(int));
            //      index = -1;
            processor.Emit(OpCodes.Ldc_I4_M1);
            processor.Emit(OpCodes.Stloc, foundIndexVd);

            VariableDefinition iteratorVd = base.GetClass<GeneralHelper>().CreateVariable(reconcileMd, typeof(int));

            GenericInstanceType dataListGit;
            GetGenericLists(replicateDataTr, out dataListGit);
            MethodReference replicateGetCountMr = base.GetClass<GeneralHelper>().List_get_Count_MethodRef.MakeHostInstanceGeneric(base.Session, dataListGit);
            MethodReference replicateGetItemMr = base.GetClass<GeneralHelper>().List_get_Item_MethodRef.MakeHostInstanceGeneric(base.Session, dataListGit);
            MethodReference replicateClearMr = base.GetClass<GeneralHelper>().List_Clear_MethodRef.MakeHostInstanceGeneric(base.Session, dataListGit);

            Instruction iteratorIncreaseInst = processor.Create(OpCodes.Ldloc, iteratorVd);
            Instruction iteratorComparerInst = processor.Create(OpCodes.Ldloc, iteratorVd);
            Instruction iteratorLogicInst = processor.Create(OpCodes.Ldarg_0);
            Instruction afterLoopInst = processor.Create(OpCodes.Nop);
            //      for (int i = 0
            processor.Emit(OpCodes.Ldc_I4_0);
            processor.Emit(OpCodes.Stloc, iteratorVd);
            processor.Emit(OpCodes.Br, iteratorComparerInst);
            //Logic.
            //      if (replicateTick(replaying).Tick == reconcileTick(fromServer))
            processor.Append(iteratorLogicInst); //Ldarg_0.
            processor.Emit(OpCodes.Ldfld, predictionFields.ClientReplicateDatas);
            processor.Emit(OpCodes.Ldloc, iteratorVd);
            processor.Emit(OpCodes.Callvirt, replicateGetItemMr);
            processor.Emit(OpCodes.Ldfld, ReplicateData_Tick_FieldRef);
            processor.Emit(OpCodes.Ldloc, reconcileTickVd);
            processor.Emit(OpCodes.Bne_Un, iteratorIncreaseInst);

            processor.Emit(OpCodes.Ldloc, iteratorVd);
            processor.Emit(OpCodes.Stloc, foundIndexVd);
            processor.Emit(OpCodes.Br, afterLoopInst);
            //      i++;
            processor.Append(iteratorIncreaseInst); //Ldloc iteratorVd.
            processor.Emit(OpCodes.Ldc_I4_1);
            processor.Emit(OpCodes.Add);
            processor.Emit(OpCodes.Stloc, iteratorVd);
            //Conditional.
            processor.Append(iteratorComparerInst); //Ldloc iteratorVd.
            processor.Emit(OpCodes.Ldarg_0);
            processor.Emit(OpCodes.Ldfld, predictionFields.ClientReplicateDatas);
            processor.Emit(OpCodes.Callvirt, replicateGetCountMr);
            processor.Emit(OpCodes.Blt, iteratorLogicInst);

            processor.Append(afterLoopInst);

            /* Remove entries that server processed. */
            //      if (index == -1) //Entry not found, shouldn't happen.
            Instruction afterClearInst = processor.Create(OpCodes.Nop);
            Instruction afterRemoveRangeInst = processor.Create(OpCodes.Nop);
            processor.Emit(OpCodes.Ldloc, foundIndexVd);
            processor.Emit(OpCodes.Ldc_I4_M1);
            processor.Emit(OpCodes.Bne_Un, afterClearInst);
            //            replicates.Clear();
            processor.Emit(OpCodes.Ldarg_0);
            processor.Emit(OpCodes.Ldfld, predictionFields.ClientReplicateDatas);
            processor.Emit(OpCodes.Callvirt, replicateClearMr);
            processor.Emit(OpCodes.Br, afterRemoveRangeInst);

            //      index++; This is for RemoveRange. If index is 0 then remove count needs to be 1.
            processor.Append(afterClearInst);
            processor.Emit(OpCodes.Ldloc, foundIndexVd);
            processor.Emit(OpCodes.Ldc_I4_1);
            processor.Emit(OpCodes.Add);
            processor.Emit(OpCodes.Stloc, foundIndexVd);

            processor.Add(ListRemoveRange(reconcileMd, predictionFields.ClientReplicateDatas.CachedResolve(base.Session), replicateDataTr, foundIndexVd));
            processor.Append(afterRemoveRangeInst);
        }

        private void ClientGetPhysicsScenes(MethodDefinition reconcileMd, out VariableDefinition objectSceneVd, out VariableDefinition physicsScene3DVd, out VariableDefinition physicsScene2DVd)
        {
            ILProcessor processor = reconcileMd.Body.GetILProcessor();
            objectSceneVd = reconcileMd.CreateVariable(base.Session, typeof(UnityEngine.SceneManagement.Scene));
            physicsScene3DVd = reconcileMd.CreateVariable(base.Session, typeof(PhysicsScene));
            physicsScene2DVd = reconcileMd.CreateVariable(base.Session, typeof(PhysicsScene2D));

            //Scene objectScene = gameObject.scene;
            processor.Emit(OpCodes.Ldarg_0);
            processor.Emit(OpCodes.Call, Unity_GetGameObject_MethodRef);
            processor.Emit(OpCodes.Callvirt, Unity_GetScene_MethodRef);
            processor.Emit(OpCodes.Stloc, objectSceneVd);

            //      PhysicsScene ps3d = objectScene.GetPhysicsScene();
            processor.Emit(OpCodes.Ldloc, objectSceneVd);
            processor.Emit(OpCodes.Call, Unity_GetPhysicsScene3D_MethodRef);
            processor.Emit(OpCodes.Stloc, physicsScene3DVd);

            //      PhysicsScene2D ps2d = objectScene.GetPhysicsScene();
            processor.Emit(OpCodes.Ldloc, objectSceneVd);
            processor.Emit(OpCodes.Call, Unity_GetPhysicsScene2D_MethodRef);
            processor.Emit(OpCodes.Stloc, physicsScene2DVd);
        }
        /// <summary>
        /// Replays all cached client datas.
        /// </summary>
        private void ClientReplayBuffered(MethodDefinition reconcileMd, MethodDefinition replicateMd, CreatedPredictionFields predictionFields,
            VariableDefinition simulateVd, VariableDefinition sceneVd, VariableDefinition physicsSceneVd, VariableDefinition physicsScene2DVd)
        {
            MethodReference replicateMr = base.ImportReference(replicateMd);
            TypeReference replicateDataTr = replicateMd.Parameters[0].ParameterType;

            ILProcessor processor = reconcileMd.Body.GetILProcessor();

            VariableDefinition iteratorVd = base.GetClass<GeneralHelper>().CreateVariable(reconcileMd, typeof(int));

            GenericInstanceType dataListGit;
            GetGenericLists(replicateDataTr, out dataListGit);
            MethodReference dataCollectionGetCountMr = base.GetClass<GeneralHelper>().List_get_Count_MethodRef.MakeHostInstanceGeneric(base.Session, dataListGit);
            MethodReference dataCollectionGetItemMr = base.GetClass<GeneralHelper>().List_get_Item_MethodRef.MakeHostInstanceGeneric(base.Session, dataListGit);

            Instruction iteratorComparerInst = processor.Create(OpCodes.Ldloc, iteratorVd);
            Instruction iteratorLogicInst = processor.Create(OpCodes.Nop);

            //Set as replaying before iterating.
            processor.Emit(OpCodes.Ldarg_0);
            processor.Emit(OpCodes.Ldc_I4_1);
            processor.Emit(OpCodes.Stfld, predictionFields.ClientReplayingData);

            //      double tickDelta = base.TimeManager.TickDelta;
            VariableDefinition tickDeltaVd = base.GetClass<GeneralHelper>().CreateVariable(reconcileMd, typeof(double));
            processor.Emit(OpCodes.Ldarg_0);
            processor.Emit(OpCodes.Call, base.GetClass<NetworkBehaviourHelper>().TimeManager_MethodRef);
            processor.Emit(OpCodes.Callvirt, base.GetClass<TimeManagerHelper>().TickDelta_MethodRef);
            processor.Emit(OpCodes.Stloc, tickDeltaVd);

            //      int count = _replicateBuffer.Count.
            VariableDefinition lstCountVd = base.GetClass<GeneralHelper>().CreateVariable(reconcileMd, typeof(int));
            processor.Emit(OpCodes.Ldarg_0);
            processor.Emit(OpCodes.Ldfld, predictionFields.ClientReplicateDatas);
            processor.Emit(OpCodes.Callvirt, dataCollectionGetCountMr);
            processor.Emit(OpCodes.Stloc, lstCountVd);

            //      for (int i = 0
            processor.Emit(OpCodes.Ldc_I4_0);
            processor.Emit(OpCodes.Stloc, iteratorVd);
            processor.Emit(OpCodes.Br, iteratorComparerInst);
            //Logic.
            processor.Append(iteratorLogicInst);
            processor.Add(InvokeOnReplicateReplay(replicateMd, sceneVd, physicsSceneVd, physicsScene2DVd, true));
            processor.Emit(OpCodes.Ldarg_0);
            processor.Emit(OpCodes.Ldarg_0);
            processor.Emit(OpCodes.Ldfld, predictionFields.ClientReplicateDatas);
            processor.Emit(OpCodes.Ldloc, iteratorVd);
            processor.Emit(OpCodes.Callvirt, dataCollectionGetItemMr);
            processor.Emit(OpCodes.Ldc_I4_0);
            processor.Emit(OpCodes.Ldc_I4_1); //true for replaying.
            processor.Emit(OpCodes.Call, replicateMr);
            processor.Add(ClientTrySimulatePhysics(reconcileMd, simulateVd, tickDeltaVd, physicsSceneVd, physicsScene2DVd));
            processor.Add(InvokeOnReplicateReplay(replicateMd, sceneVd, physicsSceneVd, physicsScene2DVd, false));
            //      i++;
            processor.Emit(OpCodes.Ldloc, iteratorVd);
            processor.Emit(OpCodes.Ldc_I4_1);
            processor.Emit(OpCodes.Add);
            processor.Emit(OpCodes.Stloc, iteratorVd);
            //Conditional.
            processor.Append(iteratorComparerInst);
            processor.Emit(OpCodes.Ldarg_0);
            processor.Emit(OpCodes.Ldfld, predictionFields.ClientReplicateDatas);
            processor.Emit(OpCodes.Callvirt, dataCollectionGetCountMr);
            processor.Emit(OpCodes.Blt, iteratorLogicInst);

            //Invokes reconcile end.
            processor.Add(InvokeOnReconcile(reconcileMd, false));

            //Unset replaying.
            processor.Emit(OpCodes.Ldarg_0);
            processor.Emit(OpCodes.Ldc_I4_0);
            processor.Emit(OpCodes.Stfld, predictionFields.ClientReplayingData);

        }


        /// <summary>
        /// Invokes OnReplicateReplay.
        /// </summary>
        private List<Instruction> InvokeOnReplicateReplay(MethodDefinition methodDef, VariableDefinition sceneVd, VariableDefinition physicsSceneVd, VariableDefinition physicsScene2DVd, bool start)
        {
            List<Instruction> insts = new List<Instruction>();
            ILProcessor processor = methodDef.Body.GetILProcessor();

            insts.Add(processor.Create(OpCodes.Ldarg_0));
            insts.Add(processor.Create(OpCodes.Call, base.GetClass<NetworkBehaviourHelper>().TimeManager_MethodRef));
            insts.Add(processor.Create(OpCodes.Ldloc, sceneVd));
            insts.Add(processor.Create(OpCodes.Ldloc, physicsSceneVd));
            insts.Add(processor.Create(OpCodes.Ldloc, physicsScene2DVd));
            if (start)
                insts.Add(processor.Create(OpCodes.Ldc_I4_1));
            else
                insts.Add(processor.Create(OpCodes.Ldc_I4_0));
            insts.Add(processor.Create(OpCodes.Callvirt, base.GetClass<TimeManagerHelper>().InvokeOnReplicateReplay_MethodRef));

            return insts;
        }

        /// <summary>
        /// Invokes OnReconcile. Uses lstCountVd > 0 as a requirement if not null.
        /// </summary>
        private List<Instruction> InvokeOnReconcile(MethodDefinition methodDef, bool start)
        {
            List<Instruction> insts = new List<Instruction>();
            ILProcessor processor = methodDef.Body.GetILProcessor();

            insts.Add(processor.Create(OpCodes.Ldarg_0));
            insts.Add(processor.Create(OpCodes.Call, base.GetClass<NetworkBehaviourHelper>().TimeManager_MethodRef));
            insts.Add(processor.Create(OpCodes.Ldarg_0)); //this for NB.
            if (start)
                insts.Add(processor.Create(OpCodes.Ldc_I4_1));
            else
                insts.Add(processor.Create(OpCodes.Ldc_I4_0));
            insts.Add(processor.Create(OpCodes.Callvirt, base.GetClass<TimeManagerHelper>().InvokeOnReconcile_MethodRef));

            return insts;
        }

        /// <summary>
        /// Creates a reader for replicate data received from clients.
        /// </summary>
        private void ClientCreateReconcileReader(TypeDefinition typeDef, MethodDefinition reconcileMd, CreatedPredictionFields predictionFields, out MethodDefinition result)
        {
            string methodName = $"{RECONCILE_READER_PREFIX}{reconcileMd.Name}";
            /* If method already exist then clear it. This
             * can occur when a method needs to be rebuilt due to
             * inheritence, and renumbering the start counts.  */
            MethodDefinition createdMd = createdMd = new MethodDefinition(methodName,
                    MethodAttributes.Private,
                    reconcileMd.Module.TypeSystem.Void);
            typeDef.Methods.Add(createdMd);
            createdMd.Body.InitLocals = true;

            //Create pooledreader parameter.
            ParameterDefinition readerPd = base.GetClass<GeneralHelper>().CreateParameter(createdMd, typeof(PooledReader));
            TypeReference reconcileDataTr = reconcileMd.Parameters[0].ParameterType;
            //data.DATA_TICK_FIELD_NAME.
            ILProcessor processor = createdMd.Body.GetILProcessor();


            VariableDefinition reconcileVr;
            processor.Add(base.GetClass<ReaderHelper>().CreateRead(createdMd, readerPd, reconcileDataTr, out reconcileVr));

            /* Make sure is owner. This is always sent to owner, but
             * unreliably. It's possible they will arrive after
             * an owner change. */
            //      if (!base.IsOwner) return;
            base.GetClass<NetworkBehaviourHelper>().CreateLocalClientIsOwnerCheck(createdMd, LoggingType.Off, true, false, false);

            //uint receivedTick = data.DATA_TICK_FIELD_NAME.
            VariableDefinition receivedTickVd = base.GetClass<GeneralHelper>().CreateVariable(createdMd, typeof(uint));
            processor.Emit(OpCodes.Ldloc, reconcileVr);
            processor.Emit(OpCodes.Ldfld, ReconcileData_Tick_FieldRef);
            processor.Emit(OpCodes.Stloc, receivedTickVd);

            /* If tick is less than last received tick then exit method.
             * Already reconciled to a more recent tick. */
            //      if (receivedTick <= _clientReconcileTick) return;
            Instruction afterOldTickCheckInst = processor.Create(OpCodes.Nop);
            processor.Emit(OpCodes.Ldloc, receivedTickVd);
            processor.Emit(OpCodes.Ldarg_0);
            processor.Emit(OpCodes.Ldfld, predictionFields.ClientReconcileTick);
            processor.Emit(OpCodes.Bgt_Un_S, afterOldTickCheckInst);
            processor.Emit(OpCodes.Ret);
            processor.Append(afterOldTickCheckInst);

            //      _clientReconcileTick = receivedTick;
            processor.Emit(OpCodes.Ldarg_0);
            processor.Emit(OpCodes.Ldloc, receivedTickVd);
            processor.Emit(OpCodes.Stfld, predictionFields.ClientReconcileTick);
            //      _clientHasReconcileData = true;
            processor.Add(ClientSetHasReconcileData(createdMd, true, predictionFields));
            //      _clientReconcileData = reconcileData;
            processor.Emit(OpCodes.Ldarg_0);
            processor.Emit(OpCodes.Ldloc, reconcileVr);
            processor.Emit(OpCodes.Stfld, predictionFields.ReconcileData);

            //Add end of method.
            processor.Emit(OpCodes.Ret);

            result = createdMd;
        }

        /// <summary>
        /// Creates client side code for reconcileMd.
        /// </summary>
        /// <param name="reconcileMd"></param>
        /// <returns></returns>
        private void ClientCreateReconcile(MethodDefinition reconcileMd, MethodDefinition replicateMd
            , CreatedPredictionFields predictionFields, VariableDefinition reconcileTickVd)
        {
            ILProcessor reconcileProcessor = reconcileMd.Body.GetILProcessor();

            //      bool simulate = (base.TimeManager.PhysicsMode == PhysicsMode.TimeManager);
            VariableDefinition simulateVd;
            /* Simulations to run after replaying inputs. Needed because
            * even though there may not be inputs the ticks ran after
            * the last input should re-simulate. EG: if inputs are performed
            * on ticks 0, 1, 2, 3, 4 but ticks 5, 6, 7, 8 are run before the
            * the client can reconcile then only 0-4 simulations will run, and the
            * remaining will not causing a desync. */
            VariableDefinition extraSimulationsVd = base.GetClass<GeneralHelper>().CreateVariable(reconcileMd, typeof(uint));
            //      extraSimulations = 0; //default.
            reconcileProcessor.Emit(OpCodes.Ldc_I4_0);
            reconcileProcessor.Emit(OpCodes.Stloc, extraSimulationsVd);

            ClientCreateSimulatePhysicsBool(reconcileMd, out simulateVd);
            //      Physics/2D.SyncTransforms.
            ClientSyncTransforms(reconcileMd);
            //Remove data server processed.
            ClientRemoveFromCache(reconcileMd, replicateMd, predictionFields, reconcileTickVd);
            //Gets physics scenes.
            VariableDefinition objectSceneVd;
            VariableDefinition physicsScenDVd;
            VariableDefinition physicsScene2DVd;
            ClientGetPhysicsScenes(reconcileMd, out objectSceneVd, out physicsScenDVd, out physicsScene2DVd);
            //Replays buffered inputs.
            ClientReplayBuffered(reconcileMd, replicateMd, predictionFields, simulateVd, objectSceneVd, physicsScenDVd, physicsScene2DVd);
        }
        #endregion

        #region CreateSend...
        /// <summary>
        /// Emits common values into Send call that all prediction methods use.
        /// </summary>
        private void CreateSendPredictionCommon(ILProcessor processor, uint hash, VariableDefinition writerVd)
        {
            processor.Emit(OpCodes.Ldarg_0);
            processor.Emit(OpCodes.Ldc_I4, (int)hash);
            processor.Emit(OpCodes.Ldloc, writerVd);
        }
        /// <summary>
        /// Calls SendReplicate.
        /// </summary>
        /// <param name="processor"></param>
        /// <param name="hash"></param>
        /// <param name="writerVd"></param>
        private void CreateSendReplicate(ILProcessor processor, uint hash, VariableDefinition writerVd)
        {
            CreateSendPredictionCommon(processor, hash, writerVd);
            //Call NetworkBehaviour.SendReplicate.
            processor.Emit(OpCodes.Call, base.GetClass<NetworkBehaviourHelper>().SendReplicateRpc_MethodRef);
        }
        #endregion
    }
}