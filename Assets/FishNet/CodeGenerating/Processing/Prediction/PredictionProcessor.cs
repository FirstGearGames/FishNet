using FishNet.CodeGenerating.Extension;
using FishNet.CodeGenerating.Helping;
using FishNet.CodeGenerating.Helping.Extension;
using FishNet.CodeGenerating.Processing.Rpc;
using FishNet.Connection;
using FishNet.Object;
using FishNet.Object.Prediction;
using FishNet.Object.Prediction.Delegating;
using FishNet.Serializing;
using FishNet.Transporting;
using FishNet.Utility.Performance;
using GameKit.Dependencies.Utilities;
using MonoFN.Cecil;
using MonoFN.Cecil.Cil;
using MonoFN.Cecil.Rocks;
using System.Collections.Generic;
using SR = System.Reflection;

namespace FishNet.CodeGenerating.Processing
{
    internal class PredictionProcessor : CodegenBase
    {
        #region Types.
        private class PredictionAttributedMethods
        {
            public MethodDefinition ReplicateMethod;
            public MethodDefinition ReconcileMethod;

            public PredictionAttributedMethods(MethodDefinition replicateMethod, MethodDefinition reconcileMethod)
            {
                ReplicateMethod = replicateMethod;
                ReconcileMethod = reconcileMethod;
            }
        }
        private enum InsertType
        {
            First,
            Last,
            Current
        }

        private class CreatedPredictionFields
        {
            /// <summary>   
            /// TypeReference of replicate data.
            /// </summary>
            public readonly TypeReference ReplicateDataTypeRef;
            /// <summary>
            /// Delegate for calling replicate user logic.
            /// </summary>
            public readonly FieldReference ReplicateULDelegate;
            /// <summary>
            /// Delegate for calling replicate user logic.
            /// </summary>
            public readonly FieldReference ReconcileULDelegate;
            /// <summary>
            /// Replicate data which has not run yet and is in queue to do so.
            /// </summary>
            public readonly FieldReference ReplicateDatasQueue;
            /// <summary>
            /// Replicate data which has already run and is used to reconcile/replay.
            /// </summary>
            public readonly FieldReference ReplicateDatasHistory;
            /// <summary>
            /// Last reconcile data received from the server.
            /// </summary>
            public readonly FieldReference ReconcileData;
            /// <summary>
            /// A buffer to read replicates into.
            /// </summary>
            public readonly FieldReference ServerReplicateReaderBuffer;

            public CreatedPredictionFields(TypeReference replicateDataTypeRef, FieldReference replicateULDelegate, FieldReference reconcileULDelegate, FieldReference replicateDatasQueue, FieldReference replicateDatasHistory, FieldReference reconcileData,
                FieldReference serverReplicateReaderBuffer)
            {
                ReplicateDataTypeRef = replicateDataTypeRef;
                ReplicateULDelegate = replicateULDelegate;
                ReconcileULDelegate = reconcileULDelegate;
                ReplicateDatasQueue = replicateDatasQueue;
                ReplicateDatasHistory = replicateDatasHistory;
                ReconcileData = reconcileData;
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

        #region Public.
        public string IReplicateData_FullName = typeof(IReplicateData).FullName;
        public string IReconcileData_FullName = typeof(IReconcileData).FullName;
        public TypeReference ReplicateULDelegate_TypeRef;
        public TypeReference ReconcileULDelegate_TypeRef;
        public MethodReference IReplicateData_GetTick_MethodRef;
        public MethodReference IReplicateData_SetTick_MethodRef;
        public MethodReference IReconcileData_GetTick_MethodRef;
        public MethodReference IReconcileData_SetTick_MethodRef;
        public MethodReference Unity_GetGameObject_MethodRef;
        #endregion

        #region Const.
        public const string REPLICATE_LOGIC_PREFIX = "Logic_Replicate___";
        public const string REPLICATE_READER_PREFIX = "Reader_Replicate___";
        public const string RECONCILE_LOGIC_PREFIX = "Logic_Reconcile___";
        public const string RECONCILE_READER_PREFIX = "Reader_Reconcile___";
        #endregion

        public override bool ImportReferences()
        {
            System.Type locType;
            SR.MethodInfo locMi;

            base.ImportReference(typeof(BasicQueue<>));
            ReplicateULDelegate_TypeRef = base.ImportReference(typeof(ReplicateUserLogicDelegate<>));
            ReconcileULDelegate_TypeRef = base.ImportReference(typeof(ReconcileUserLogicDelegate<>));

            //GetGameObject.
            locMi = typeof(UnityEngine.Component).GetMethod("get_gameObject");
            Unity_GetGameObject_MethodRef = base.ImportReference(locMi);

            //Get/Set tick.
            locType = typeof(IReplicateData);
            foreach (SR.MethodInfo mi in locType.GetMethods())
            {
                if (mi.Name == nameof(IReplicateData.GetTick))
                    IReplicateData_GetTick_MethodRef = base.ImportReference(mi);
                else if (mi.Name == nameof(IReplicateData.SetTick))
                    IReplicateData_SetTick_MethodRef = base.ImportReference(mi);
            }

            locType = typeof(IReconcileData);
            foreach (SR.MethodInfo mi in locType.GetMethods())
            {
                if (mi.Name == nameof(IReconcileData.GetTick))
                    IReconcileData_GetTick_MethodRef = base.ImportReference(mi);
                else if (mi.Name == nameof(IReconcileData.SetTick))
                    IReconcileData_SetTick_MethodRef = base.ImportReference(mi);
            }

            return true;
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
        /// Gets number of predictions by checking for prediction attributes in typeDef parents, excluding typerDef.
        /// </summary>
        /// <param name="typeDef"></param>
        /// <returns></returns>
        internal uint GetPredictionCountInParents(TypeDefinition typeDef)
        {
            uint count = 0;
            do
            {
                typeDef = typeDef.GetNextBaseClassToProcess(base.Session);
                if (typeDef != null)
                    count += GetPredictionCount(typeDef);

            } while (typeDef != null);

            return count;
        }

        /// <summary>
        /// Ensures only one prediction and reconile method exist per typeDef, and outputs finding.
        /// </summary>
        /// <returns>True if there is only one set of prediction methods. False if none, or more than one set.</returns>
        internal bool GetPredictionMethods(TypeDefinition typeDef, out MethodDefinition replicateMd, out MethodDefinition reconcileMd)
        {
            replicateMd = null;
            reconcileMd = null;

            string replicateAttributeFullName = base.GetClass<AttributeHelper>().ReplicateAttribute_FullName;
            string reconcileAttributeFullName = base.GetClass<AttributeHelper>().ReconcileAttribute_FullName;

            bool error = false;
            foreach (MethodDefinition methodDef in typeDef.Methods)
            {
                foreach (CustomAttribute customAttribute in methodDef.CustomAttributes)
                {
                    if (customAttribute.Is(replicateAttributeFullName))
                    {
                        if (!MethodIsPrivate(methodDef) || AlreadyFound(replicateMd))
                            error = true;
                        else
                            replicateMd = methodDef;
                    }
                    else if (customAttribute.Is(reconcileAttributeFullName))
                    {
                        if (!MethodIsPrivate(methodDef) || AlreadyFound(reconcileMd))
                        {
                            error = true;
                        }
                        else
                        {
                            reconcileMd = methodDef;
                            if (!CheckCreateReconcile(reconcileMd))
                                error = true;
                        }
                    }
                    if (error)
                        break;
                }
                if (error)
                    break;
            }

            //Checks to make sure the CreateReconcile method exist and calls reconcile.
            bool CheckCreateReconcile(MethodDefinition reconcileMd)
            {
                string crName = nameof(NetworkBehaviour.CreateReconcile);
                MethodDefinition createReconcileMd = reconcileMd.DeclaringType.GetMethod(crName);
                //Does not exist.
                if (createReconcileMd == null)
                {
                    base.LogError($"{reconcileMd.DeclaringType.Name} does not implement method {crName}. Override method {crName} and use it to create your reconcile data, and call your reconcile method {reconcileMd.Name}. Call ");
                    return false;
                }
                //Exists, check for call.
                else
                {
                    //Check for call instructions.
                    foreach (Instruction inst in createReconcileMd.Body.Instructions)
                    {
                        if (inst.OpCode == OpCodes.Call || inst.OpCode == OpCodes.Callvirt)
                        {
                            if (inst.Operand is MethodReference mr)
                            {
                                if (mr.Name == reconcileMd.Name)
                                    return true;
                            }
                        }
                    }

                    base.LogError($"{reconcileMd.DeclaringType.Name} implements {crName} but does not call reconcile method {reconcileMd.Name}.");
                    //Fallthrough.
                    return false;
                }
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

        internal bool Process(TypeDefinition typeDef)
        {
            //Set prediction count in parents here. Increase count after each predictionAttributeMethods iteration.
            //Do a for each loop on predictionAttributedMethods.
            /* NOTES: for predictionv2 get all prediction attributed methods up front and store them inside predictionAttributedMethods.
            * To find the proper reconciles for replicates add an attribute field allowing users to assign Ids. EG ReplicateV2.Id = 1. Default
             * value will be 0. */

            MethodDefinition replicateMd;
            MethodDefinition reconcileMd;
            //Not using prediction methods.
            if (!GetPredictionMethods(typeDef, out replicateMd, out reconcileMd))
                return false;

            RpcProcessor rp = base.GetClass<RpcProcessor>();
            uint predictionRpcCount = GetPredictionCountInParents(typeDef) + rp.GetRpcCountInParents(typeDef); ;
            //If replication methods found but this hierarchy already has max.
            if (predictionRpcCount >= NetworkBehaviourHelper.MAX_RPC_ALLOWANCE)
            {
                base.LogError($"{typeDef.FullName} and inherited types exceed {NetworkBehaviourHelper.MAX_PREDICTION_ALLOWANCE} replicated methods. Only {NetworkBehaviourHelper.MAX_PREDICTION_ALLOWANCE} replicated methods are supported per inheritance hierarchy.");
                return false;
            }

            bool parameterError = false;
            parameterError |= HasParameterError(replicateMd, typeDef, true);
            parameterError |= HasParameterError(reconcileMd, typeDef, false);
            if (parameterError)
                return false;

            TypeDefinition replicateDataTd = replicateMd.Parameters[0].ParameterType.CachedResolve(base.Session);
            TypeDefinition reconcileDataTd = reconcileMd.Parameters[0].ParameterType.CachedResolve(base.Session);
            //Ensure datas implement interfaces.
            bool interfacesImplemented = true;
            DataImplementInterfaces(replicateMd, true, ref interfacesImplemented);
            DataImplementInterfaces(reconcileMd, false, ref interfacesImplemented);
            if (!interfacesImplemented)
                return false;
            if (!TickFieldIsNonSerializable(replicateDataTd, true))
                return false;
            if (!TickFieldIsNonSerializable(reconcileDataTd, false))
                return false;

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
            MethodDefinition replicateULMd;
            MethodDefinition reconcileULMd;
            CreatePredictionMethods(typeDef, replicateMd, reconcileMd, predictionFields, predictionRpcCount, out predictionReaders, out replicateULMd, out reconcileULMd);
            InitializeCollections(typeDef, replicateMd, predictionFields);
            InitializeULDelegates(typeDef, predictionFields, replicateMd, reconcileMd, replicateULMd, reconcileULMd);
            RegisterPredictionRpcs(typeDef, predictionRpcCount, predictionReaders);

            return true;
        }

        /// <summary>
        /// Ensures the tick field for GetTick is non-serializable.
        /// </summary>
        /// <param name="dataTd"></param>
        /// <returns></returns>
        private bool TickFieldIsNonSerializable(TypeDefinition dataTd, bool replicate)
        {
            string methodName = (replicate) ? IReplicateData_GetTick_MethodRef.Name : IReconcileData_GetTick_MethodRef.Name;
            MethodDefinition getMd = dataTd.GetMethod(methodName);

            //Try to find ldFld.
            Instruction ldFldInst = null;
            foreach (Instruction item in getMd.Body.Instructions)
            {
                if (item.OpCode == OpCodes.Ldfld)
                {
                    ldFldInst = item;
                    break;
                }
            }

            //If ldFld not found.
            if (ldFldInst == null)
            {
                base.LogError($"{dataTd.FullName} method {getMd.Name} does not return a field type for the Tick. Make a new private field of uint type and return it's value within {getMd.Name}.");
                return false;
            }
            //Make sure the field is private.
            else
            {
                FieldDefinition fd = (FieldDefinition)ldFldInst.Operand;
                if (!fd.Attributes.HasFlag(FieldAttributes.Private))
                {
                    base.LogError($"{dataTd.FullName} method {getMd.Name} returns a tick field but it's not marked as private. Make the field {fd.Name} private.");
                    return false;
                }
            }

            //All checks pass.
            return true;
        }

        private void DataImplementInterfaces(MethodDefinition methodDef, bool isReplicate, ref bool interfacesImplemented)
        {
            TypeReference dataTr = methodDef.Parameters[0].ParameterType;
            string interfaceName = (isReplicate) ? IReplicateData_FullName : IReconcileData_FullName;
            //If does not implement.
            if (!dataTr.CachedResolve(base.Session).ImplementsInterfaceRecursive(base.Session, interfaceName))
            {
                string name = (isReplicate) ? typeof(IReplicateData).Name : typeof(IReconcileData).Name;
                base.LogError($"Prediction data type {dataTr.Name} for method {methodDef.Name} in class {methodDef.DeclaringType.Name} must implement the {name} interface.");
                interfacesImplemented = false;
            }
        }

        /// <summary>
        /// Registers RPCs that prediction uses.
        /// </summary>
        private void RegisterPredictionRpcs(TypeDefinition typeDef, uint hash, PredictionReaders readers)
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
                    ctorMr = base.GetClass<NetworkBehaviourHelper>().ReplicateRpcDelegate_Ctor_MethodRef;
                    callMr = base.GetClass<NetworkBehaviourHelper>().RegisterReplicateRpc_MethodRef;
                }
                else
                {
                    ctorMr = base.GetClass<NetworkBehaviourHelper>().ReconcileRpcDelegate_Ctor_MethodRef;
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
            GeneralHelper gh = base.GetClass<GeneralHelper>();
            TypeReference replicateDataTr = replicateMd.Parameters[0].ParameterType;
            MethodDefinition injectionMethodDef = typeDef.GetMethod(NetworkBehaviourProcessor.NETWORKINITIALIZE_EARLY_INTERNAL_NAME);
            ILProcessor processor = injectionMethodDef.Body.GetILProcessor();

            Generate(predictionFields.ReplicateDatasQueue, false);
            Generate(predictionFields.ReplicateDatasHistory, true);
            void Generate(FieldReference fr, bool isList)
            {
                MethodDefinition ctorMd = base.GetClass<GeneralHelper>().List_TypeRef.CachedResolve(base.Session).GetDefaultConstructor(base.Session);
                GenericInstanceType collectionGit;
                if (isList)
                    gh.GetGenericList(replicateDataTr, out collectionGit);
                else
                    gh.GetGenericBasicQueue(replicateDataTr, out collectionGit);
                MethodReference ctorMr = ctorMd.MakeHostInstanceGeneric(base.Session, collectionGit);

                List<Instruction> insts = new List<Instruction>();

                insts.Add(processor.Create(OpCodes.Ldarg_0));
                insts.Add(processor.Create(OpCodes.Newobj, ctorMr));
                insts.Add(processor.Create(OpCodes.Stfld, fr));

                processor.InsertFirst(insts);
            }
        }

        /// <summary>
        /// Initializes collection fields made during this process.
        /// </summary>
        /// <param name="predictionFields"></param>
        private void InitializeULDelegates(TypeDefinition typeDef, CreatedPredictionFields predictionFields, MethodDefinition replicateMd, MethodDefinition reconcileMd, MethodDefinition replicateULMd, MethodDefinition reconcileULMd)
        {
            TypeReference replicateDataTr = replicateMd.Parameters[0].ParameterType;
            TypeReference reconcileDataTr = reconcileMd.Parameters[0].ParameterType;
            MethodDefinition injectionMethodDef = typeDef.GetMethod(NetworkBehaviourProcessor.NETWORKINITIALIZE_EARLY_INTERNAL_NAME);
            ILProcessor processor = injectionMethodDef.Body.GetILProcessor();
            List<Instruction> insts = new List<Instruction>();

            Generate(replicateULMd, replicateDataTr, predictionFields.ReplicateULDelegate, typeof(ReplicateUserLogicDelegate<>), ReplicateULDelegate_TypeRef);
            Generate(reconcileULMd, reconcileDataTr, predictionFields.ReconcileULDelegate, typeof(ReconcileUserLogicDelegate<>), ReconcileULDelegate_TypeRef);

            void Generate(MethodDefinition ulMd, TypeReference dataTr, FieldReference fr, System.Type delegateType, TypeReference delegateTr)
            {
                insts.Clear();

                MethodDefinition ctorMd = delegateTr.CachedResolve(base.Session).GetFirstConstructor(base.Session, true);
                GenericInstanceType collectionGit;
                GetGenericULDelegate(dataTr, delegateType, out collectionGit);
                MethodReference ctorMr = ctorMd.MakeHostInstanceGeneric(base.Session, collectionGit);

                insts.Add(processor.Create(OpCodes.Ldarg_0));
                insts.Add(processor.Create(OpCodes.Ldarg_0));
                insts.Add(processor.Create(OpCodes.Ldftn, ulMd));
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
            GeneralHelper gh = base.GetClass<GeneralHelper>();
            TypeReference replicateDataTr = replicateMd.Parameters[0].ParameterType;
            TypeReference replicateDataArrTr = replicateDataTr.MakeArrayType();
            TypeReference reconcileDataTr = reconcileMd.Parameters[0].ParameterType;

            GenericInstanceType replicateULDelegateGit;
            GenericInstanceType reconcileULDelegateGit;
            GenericInstanceType lstDataGit;
            GenericInstanceType queueDataGit;
            GetGenericULDelegate(replicateDataTr, typeof(ReplicateUserLogicDelegate<>), out replicateULDelegateGit);
            GetGenericULDelegate(reconcileDataTr, typeof(ReconcileUserLogicDelegate<>), out reconcileULDelegateGit);
            gh.GetGenericList(replicateDataTr, out lstDataGit);
            gh.GetGenericBasicQueue(replicateDataTr, out queueDataGit);

            base.ImportReference(lstDataGit);
            /* Data buffer. */
            FieldDefinition replicateULDelegateFd = new FieldDefinition($"_replicateULDelegate___{replicateMd.Name}", FieldAttributes.Private, replicateULDelegateGit);
            FieldDefinition reconcileULDelegateFd = new FieldDefinition($"_reconcileULDelegate___{reconcileMd.Name}", FieldAttributes.Private, reconcileULDelegateGit);
            FieldDefinition replicatesQueueFd = new FieldDefinition($"_replicatesQueue___{replicateMd.Name}", FieldAttributes.Private, queueDataGit);
            FieldDefinition replicatesListFd = new FieldDefinition($"_replicatesHistory___{replicateMd.Name}", FieldAttributes.Private, lstDataGit);
            FieldDefinition reconcileDataFd = new FieldDefinition($"_reconcileData___{replicateMd.Name}", FieldAttributes.Private, reconcileDataTr);
            FieldDefinition serverReplicatesReadBufferFd = new FieldDefinition($"{replicateMd.Name}___serverReplicateReadBuffer", FieldAttributes.Private, replicateDataArrTr);

            typeDef.Fields.Add(replicateULDelegateFd);
            typeDef.Fields.Add(reconcileULDelegateFd);
            typeDef.Fields.Add(replicatesQueueFd);
            typeDef.Fields.Add(replicatesListFd);
            typeDef.Fields.Add(reconcileDataFd);
            typeDef.Fields.Add(serverReplicatesReadBufferFd);

            predictionFields = new CreatedPredictionFields(replicateDataTr, replicateULDelegateFd, reconcileULDelegateFd, replicatesQueueFd, replicatesListFd, reconcileDataFd,
                serverReplicatesReadBufferFd);
        }

        /// <summary>
        /// Returns if there are any errors with the prediction methods parameters and will print if so.
        /// </summary>
        private bool HasParameterError(MethodDefinition methodDef, TypeDefinition typeDef, bool replicateMethod)
        {
            //Replicate: data, state, channel.
            //Reconcile: data, asServer, channel.
            int count = (replicateMethod) ? 3 : 2;

            //Check parameter count.
            if (methodDef.Parameters.Count != count)
            {
                PrintParameterExpectations();
                return true;
            }

            string expectedName;
            //Data check.
            if (!methodDef.Parameters[0].ParameterType.IsClassOrStruct(base.Session))
            {
                base.LogError($"Prediction methods must use a class or structure as the first parameter type. Structures are recommended to avoid allocations.");
                return true;
            }

            expectedName = (replicateMethod) ? typeof(ReplicateState).Name : typeof(Channel).Name;
            if (methodDef.Parameters[1].ParameterType.Name != expectedName)
            {
                PrintParameterExpectations();
                return true;
            }

            //Only replicate uses more than 2 parameters.
            if (replicateMethod)
            {
                //Channel.
                if (methodDef.Parameters[2].ParameterType.Name != typeof(Channel).Name)
                {
                    PrintParameterExpectations();
                    return true;
                }
            }

            void PrintParameterExpectations()
            {
                if (replicateMethod)
                    base.LogError($"Replicate method {methodDef.Name} within {typeDef.Name} requires exactly {count} parameters. In order: replicate data, state = ReplicateState.Invalid, channel = Channel.Unreliable");
                else
                    base.LogError($"Reconcile method {methodDef.Name} within {typeDef.Name} requires exactly {count} parameters. In order: reconcile data, channel = Channel.Unreliable.");
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
        private bool CreatePredictionMethods(TypeDefinition typeDef, MethodDefinition replicateMd, MethodDefinition reconcileMd, CreatedPredictionFields predictionFields, uint rpcCount, out PredictionReaders predictionReaders, out MethodDefinition replicateULMd, out MethodDefinition reconcileULMd)
        {
            GeneralHelper gh = base.GetClass<GeneralHelper>();
            NetworkBehaviourHelper nbh = base.GetClass<NetworkBehaviourHelper>();
            predictionReaders = null;

            uint startingRpcCount = rpcCount;
            string copySuffix = "___UL";
            replicateULMd = base.GetClass<GeneralHelper>().CopyIntoNewMethod(replicateMd, $"{replicateMd.Name}{copySuffix}", out _);
            reconcileULMd = base.GetClass<GeneralHelper>().CopyIntoNewMethod(reconcileMd, $"{reconcileMd.Name}{copySuffix}", out _);
            replicateMd.Body.Instructions.Clear();
            reconcileMd.Body.Instructions.Clear();

            MethodDefinition replicateReader;
            MethodDefinition reconcileReader;
            if (!CreateReplicate())
                return false;
            if (!CreateReconcile())
                return false;
            if (!CreateEmptyReplicatesQueueIntoHistoryStart())
                return false;
            if (!CreateReconcileStart())
                return false;
            if (!CreateReplicateReplayStart())
                return false;

            CreateClearReplicateCacheMethod(typeDef, replicateMd.Parameters[0].ParameterType, predictionFields);
            CreateReplicateReader(typeDef, startingRpcCount, replicateMd, predictionFields, out replicateReader);
            CreateReconcileReader(typeDef, reconcileMd, predictionFields, out reconcileReader);
            predictionReaders = new PredictionReaders(replicateReader, reconcileReader);

            bool CreateReplicate()
            {
                ILProcessor processor = replicateMd.Body.GetILProcessor();
                ParameterDefinition replicateDataPd = replicateMd.Parameters[0];
                MethodDefinition comparerMd = gh.CreateEqualityComparer(replicateDataPd.ParameterType);
                gh.CreateIsDefaultComparer(replicateDataPd.ParameterType, comparerMd);

                Instruction exitMethodInst = processor.Create(OpCodes.Nop);
                //Call both and let the called method sort out permissions.
                CallNonAuthoritativeReplicate(replicateMd, predictionFields);
                CallAuthoritativeReplicate(replicateMd, predictionFields, rpcCount);

                processor.Append(exitMethodInst);
                processor.Emit(OpCodes.Ret);

                return true;
            }


            bool CreateReconcile()
            {
                ILProcessor processor = reconcileMd.Body.GetILProcessor();
                ServerCreateReconcile(reconcileMd, predictionFields, ref rpcCount);
                processor.Emit(OpCodes.Ret);
                return true;
            }

            bool CreateEmptyReplicatesQueueIntoHistoryStart()
            {
                MethodDefinition newMethodDef = nbh.EmptyReplicatesQueueIntoHistory_Start_MethodRef.CachedResolve(base.Session).CreateCopy(
                    base.Session, null, MethodDefinitionExtensions.PUBLIC_VIRTUAL_ATTRIBUTES);
                typeDef.Methods.Add(newMethodDef);

                ILProcessor processor = newMethodDef.Body.GetILProcessor();

                MethodReference baseMethodGim = nbh.EmptyReplicatesQueueIntoHistory_MethodRef.GetMethodReference(
                    base.Session, new TypeReference[] { predictionFields.ReplicateDataTypeRef });

                processor.Emit(OpCodes.Ldarg_0);
                processor.Emit(OpCodes.Ldarg_0);
                processor.Emit(OpCodes.Ldfld, predictionFields.ReplicateDatasQueue);
                processor.Emit(OpCodes.Ldarg_0);
                processor.Emit(OpCodes.Ldfld, predictionFields.ReplicateDatasHistory);
                processor.Emit(OpCodes.Call, baseMethodGim);
                processor.Emit(OpCodes.Ret);

                return true;
            }

            //Overrides reconcile start to call reconcile_client_internal.
            bool CreateReconcileStart()
            {
                MethodDefinition newMethodDef = nbh.Reconcile_Client_Start_MethodRef.CachedResolve(base.Session).CreateCopy(
                    base.Session, null, MethodDefinitionExtensions.PUBLIC_VIRTUAL_ATTRIBUTES);
                typeDef.Methods.Add(newMethodDef);

                ILProcessor processor = newMethodDef.Body.GetILProcessor();

                MethodReference baseMethodGim = nbh.Reconcile_Client_MethodRef.GetMethodReference(
                    base.Session, new TypeReference[] { predictionFields.ReconcileData.FieldType, predictionFields.ReplicateDataTypeRef });

                processor.Emit(OpCodes.Ldarg_0);
                processor.Emit(OpCodes.Ldarg_0);
                processor.Emit(OpCodes.Ldfld, predictionFields.ReconcileULDelegate);
                processor.Emit(OpCodes.Ldarg_0);
                processor.Emit(OpCodes.Ldfld, predictionFields.ReplicateDatasHistory);
                processor.Emit(OpCodes.Ldarg_0);
                processor.Emit(OpCodes.Ldfld, predictionFields.ReconcileData);
                processor.Emit(OpCodes.Call, baseMethodGim);
                processor.Emit(OpCodes.Ret);

                return true;
            }

            bool CreateReplicateReplayStart()
            {
                MethodDefinition newMethodDef = nbh.Replicate_Replay_Start_MethodRef.CachedResolve(base.Session).CreateCopy(
                    base.Session, null, MethodDefinitionExtensions.PUBLIC_VIRTUAL_ATTRIBUTES);
                typeDef.Methods.Add(newMethodDef);

                ParameterDefinition replayTickPd = newMethodDef.Parameters[0];
                ILProcessor processor = newMethodDef.Body.GetILProcessor();

                MethodReference baseMethodGim = nbh.Replicate_Replay_MethodRef.GetMethodReference(
                    base.Session, new TypeReference[] { predictionFields.ReplicateDataTypeRef });

                //uint replicateTick, ReplicateUserLogicDelegate<T> del, List<T> replicates, Channel channel) where T : IReplicateData
                processor.Emit(OpCodes.Ldarg_0);
                processor.Emit(OpCodes.Ldarg, replayTickPd);
                processor.Emit(OpCodes.Ldarg_0);
                processor.Emit(OpCodes.Ldfld, predictionFields.ReplicateULDelegate);
                processor.Emit(OpCodes.Ldarg_0);
                processor.Emit(OpCodes.Ldfld, predictionFields.ReplicateDatasHistory);
                processor.Emit(OpCodes.Ldc_I4, (int)Channel.Unreliable); //Channel does not really matter when replaying. At least not until someone needs it.
                processor.Emit(OpCodes.Call, baseMethodGim);
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
            GeneralHelper gh = base.GetClass<GeneralHelper>();
            NetworkBehaviourHelper nbh = base.GetClass<NetworkBehaviourHelper>();

            string methodName = nameof(NetworkBehaviour.ClearReplicateCache);
            MethodDefinition baseClearMd = typeDef.GetMethodDefinitionInAnyBase(base.Session, methodName);
            MethodDefinition clearMd = typeDef.GetOrCreateMethodDefinition(base.Session, methodName, baseClearMd, true, out bool created);
            clearMd.Attributes = MethodDefinitionExtensions.PUBLIC_VIRTUAL_ATTRIBUTES;
            //This class already has the method created when it should not.
            if (baseClearMd.DeclaringType == typeDef)
            {
                base.LogError($"{typeDef.Name} overrides method {methodName} when it should not.");
                return;
            }

            ILProcessor processor = clearMd.Body.GetILProcessor();
            //Call the base class first.
            processor.Emit(OpCodes.Ldarg_0);
            MethodReference baseClearMr = base.ImportReference(baseClearMd);
            processor.Emit(OpCodes.Call, baseClearMr);

            //Call the actual clear method.
            TypeDefinition nbTypeDef = typeDef.GetTypeDefinitionInBase(base.Session, typeof(NetworkBehaviour).FullName, false);
            MethodReference internalClearMr = base.Session.ImportReference(nbTypeDef.GetMethod(nameof(NetworkBehaviour.ClearReplicateCache_Internal)));
            GenericInstanceMethod internalClearGim = internalClearMr.MakeGenericMethod(new TypeReference[] { dataTr });

            processor.Emit(OpCodes.Ldarg_0); //Base.
            processor.Emit(OpCodes.Ldarg_0);
            processor.Emit(OpCodes.Ldfld, predictionFields.ReplicateDatasQueue);
            processor.Emit(OpCodes.Ldarg_0);
            processor.Emit(OpCodes.Ldfld, predictionFields.ReplicateDatasHistory);
            processor.Emit(OpCodes.Call, internalClearGim);
            processor.Emit(OpCodes.Ret);
        }

        /// <summary>
        /// Outputs generic ReplicateULDelegate for dataTr.
        /// </summary>
        private void GetGenericULDelegate(TypeReference dataTr, System.Type delegateType, out GenericInstanceType git)
        {
            TypeReference delDataTr = base.ImportReference(delegateType);
            git = delDataTr.MakeGenericInstanceType(new TypeReference[] { dataTr });
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
        private void CallNonAuthoritativeReplicate(MethodDefinition replicateMd, CreatedPredictionFields predictionFields)
        {
            ILProcessor processor = replicateMd.Body.GetILProcessor();

            ParameterDefinition replicateDataPd = replicateMd.Parameters[0];
            ParameterDefinition channelPd = replicateMd.Parameters[2];
            TypeReference replicateDataTr = replicateDataPd.ParameterType;

            GenericInstanceMethod replicateGim = base.GetClass<NetworkBehaviourHelper>().Replicate_NonAuthoritative_MethodRef.MakeGenericMethod(new TypeReference[] { replicateDataTr });

            processor.Emit(OpCodes.Ldarg_0);
            processor.Emit(OpCodes.Ldarg_0);
            processor.Emit(OpCodes.Ldfld, predictionFields.ReplicateULDelegate);
            processor.Emit(OpCodes.Ldarg_0);
            processor.Emit(OpCodes.Ldfld, predictionFields.ReplicateDatasQueue);
            processor.Emit(OpCodes.Ldarg_0);
            processor.Emit(OpCodes.Ldfld, predictionFields.ReplicateDatasHistory);
            processor.Emit(OpCodes.Ldarg, channelPd);
            processor.Emit(OpCodes.Call, replicateGim);
        }

        /// <summary>
        /// Creates a reader for replicate data received from clients.
        /// </summary>
        private bool CreateReplicateReader(TypeDefinition typeDef, uint hash, MethodDefinition replicateMd, CreatedPredictionFields predictionFields, out MethodDefinition result)
        {
            string methodName = $"{REPLICATE_READER_PREFIX}{replicateMd.Name}";
            MethodDefinition createdMd = new MethodDefinition(methodName,
                    MethodAttributes.Private,
                    replicateMd.Module.TypeSystem.Void);
            typeDef.Methods.Add(createdMd);
            createdMd.Body.InitLocals = true;

            ILProcessor processor = createdMd.Body.GetILProcessor();

            GeneralHelper gh = base.GetClass<GeneralHelper>();
            NetworkBehaviourHelper nbh = base.GetClass<NetworkBehaviourHelper>();

            TypeReference dataTr = replicateMd.Parameters[0].ParameterType;
            //Create parameters.
            ParameterDefinition readerPd = gh.CreateParameter(createdMd, typeof(PooledReader));
            ParameterDefinition networkConnectionPd = gh.CreateParameter(createdMd, typeof(NetworkConnection));
            ParameterDefinition channelPd = gh.CreateParameter(createdMd, typeof(Channel));

            MethodReference replicateReaderGim = nbh.Replicate_Reader_MethodRef.GetMethodReference(base.Session, dataTr);

            processor.Emit(OpCodes.Ldarg_0);
            processor.Emit(OpCodes.Ldc_I4, (int)hash);
            //Reader, NetworkConnection.
            processor.Emit(OpCodes.Ldarg, readerPd);
            processor.Emit(OpCodes.Ldarg, networkConnectionPd);
            //arrBuffer.
            processor.Emit(OpCodes.Ldarg_0);
            processor.Emit(OpCodes.Ldflda, predictionFields.ServerReplicateReaderBuffer);
            //Replicates queue.
            processor.Emit(OpCodes.Ldarg_0);
            processor.Emit(OpCodes.Ldfld, predictionFields.ReplicateDatasQueue);
            //Replicates history.
            processor.Emit(OpCodes.Ldarg_0);
            processor.Emit(OpCodes.Ldfld, predictionFields.ReplicateDatasHistory);
            //Channel.
            processor.Emit(OpCodes.Ldarg, channelPd);
            processor.Emit(OpCodes.Call, replicateReaderGim);

            processor.Emit(OpCodes.Ret);
            result = createdMd;
            return true;
        }

        /// <summary>
        /// Creates server side code for reconcileMd.
        /// </summary>
        /// <param name="reconcileMd"></param>
        /// <returns></returns>
        private void ServerCreateReconcile(MethodDefinition reconcileMd, CreatedPredictionFields predictionFields, ref uint rpcCount)
        {
            ParameterDefinition reconcileDataPd = reconcileMd.Parameters[0];
            ParameterDefinition channelPd = reconcileMd.Parameters[1];
            ILProcessor processor = reconcileMd.Body.GetILProcessor();

            GenericInstanceMethod methodGim = base.GetClass<NetworkBehaviourHelper>().Reconcile_Server_MethodRef.MakeGenericMethod(new TypeReference[] { reconcileDataPd.ParameterType });

            processor.Emit(OpCodes.Ldarg_0);
            processor.Emit(OpCodes.Ldc_I4, (int)rpcCount);
            processor.Emit(OpCodes.Ldarg, reconcileDataPd);
            processor.Emit(OpCodes.Ldarg, channelPd);
            processor.Emit(OpCodes.Call, methodGim);

            rpcCount++;
        }
        #endregion

        #region Client side.
        /// <summary>
        /// Creates replicate code for client.
        /// </summary>
        private void CallAuthoritativeReplicate(MethodDefinition replicateMd, CreatedPredictionFields predictionFields, uint rpcCount)
        {
            ParameterDefinition dataPd = replicateMd.Parameters[0];
            ParameterDefinition channelPd = replicateMd.Parameters[2];
            TypeReference dataTr = dataPd.ParameterType;

            ILProcessor processor = replicateMd.Body.GetILProcessor();

            //Make method reference NB.SendReplicateRpc<dataTr>
            GenericInstanceMethod replicateOwnerGim = base.GetClass<NetworkBehaviourHelper>().Replicate_Authortative_MethodRef.MakeGenericMethod(new TypeReference[] { dataTr });
            processor.Emit(OpCodes.Ldarg_0);//base.
            processor.Emit(OpCodes.Ldarg_0);
            processor.Emit(OpCodes.Ldfld, predictionFields.ReplicateULDelegate);
            processor.Emit(OpCodes.Ldc_I4, (int)rpcCount);
            //Replicates queue.
            processor.Emit(OpCodes.Ldarg_0);
            processor.Emit(OpCodes.Ldfld, predictionFields.ReplicateDatasQueue);
            //Replicates history.
            processor.Emit(OpCodes.Ldarg_0);
            processor.Emit(OpCodes.Ldfld, predictionFields.ReplicateDatasHistory);
            processor.Emit(OpCodes.Ldarg, dataPd);
            processor.Emit(OpCodes.Ldarg, channelPd);
            processor.Emit(OpCodes.Call, replicateOwnerGim);
        }
        /// <summary>
        /// Creates a reader for replicate data received from clients.
        /// </summary>
        private bool CreateReconcileReader(TypeDefinition typeDef, MethodDefinition reconcileMd, CreatedPredictionFields predictionFields, out MethodDefinition result)
        {
            string methodName = $"{RECONCILE_READER_PREFIX}{reconcileMd.Name}";
            MethodDefinition createdMd = new MethodDefinition(methodName,
                    MethodAttributes.Private,
                    reconcileMd.Module.TypeSystem.Void);
            typeDef.Methods.Add(createdMd);
            createdMd.Body.InitLocals = true;

            ILProcessor processor = createdMd.Body.GetILProcessor();

            GeneralHelper gh = base.GetClass<GeneralHelper>();
            NetworkBehaviourHelper nbh = base.GetClass<NetworkBehaviourHelper>();

            TypeReference dataTr = reconcileMd.Parameters[0].ParameterType;
            //Create parameters.
            ParameterDefinition readerPd = gh.CreateParameter(createdMd, typeof(PooledReader));
            ParameterDefinition channelPd = gh.CreateParameter(createdMd, typeof(Channel));

            MethodReference methodGim = nbh.Reconcile_Reader_MethodRef.GetMethodReference(base.Session, dataTr);

            processor.Emit(OpCodes.Ldarg_0);
            //Reader, data, channel.
            processor.Emit(OpCodes.Ldarg, readerPd);
            //Data to assign read value to.
            processor.Emit(OpCodes.Ldarg_0);
            processor.Emit(OpCodes.Ldflda, predictionFields.ReconcileData);
            //Channel.
            processor.Emit(OpCodes.Ldarg, channelPd);
            processor.Emit(OpCodes.Call, methodGim);
            //Add end of method.
            processor.Emit(OpCodes.Ret);

            result = createdMd;
            return true;
        }
        #endregion
    }
}