using FishNet.CodeGenerating.Extension;
using FishNet.CodeGenerating.Helping;
using FishNet.CodeGenerating.Helping.Extension;
using FishNet.Configuring;
using FishNet.Object;
using MonoFN.Cecil;
using MonoFN.Cecil.Cil;
using MonoFN.Collections.Generic;
using System.Collections.Generic;
using System.Linq;
using DebugX = UnityEngine.Debug;

namespace FishNet.CodeGenerating.Processing
{
    internal class NetworkBehaviourProcessor
    {
        #region Types.
        private class NetworkInitializeMethodData
        {
            public MethodDefinition MethodDefinition;
            public FieldDefinition CalledFieldDef;
            public bool CalledFromAwake;

            public NetworkInitializeMethodData(MethodDefinition methodDefinition, FieldDefinition calledFieldDef)
            {
                MethodDefinition = methodDefinition;
                CalledFieldDef = calledFieldDef;
                CalledFromAwake = false;
            }
        }
        private class AwakeMethodData
        {
            public MethodDefinition AwakeMethodDef;
            public MethodDefinition UserLogicMethodDef;
            public bool Created;

            public AwakeMethodData(MethodDefinition awakeMd, MethodDefinition userLogicMd, bool created)
            {
                AwakeMethodDef = awakeMd;
                UserLogicMethodDef = userLogicMd;
                Created = created;
            }
        }
        #endregion

        #region Misc.
        private Dictionary<TypeDefinition, NetworkInitializeMethodData> _earlyNetworkInitializeDatas = new Dictionary<TypeDefinition, NetworkInitializeMethodData>();
        private Dictionary<TypeDefinition, NetworkInitializeMethodData> _lateNetworkInitializeDatas = new Dictionary<TypeDefinition, NetworkInitializeMethodData>();
        /// <summary>
        /// Methods modified or iterated during weaving.
        /// </summary>
        internal List<MethodDefinition> ModifiedMethodDefinitions = new List<MethodDefinition>();
        /// <summary>
        /// Classes which have been processed for all NetworkBehaviour features.
        /// </summary>
        private HashSet<TypeDefinition> _processedClasses = new HashSet<TypeDefinition>();
        #endregion

        #region Const.
        internal const string EARLY_INITIALIZED_NAME = "NetworkInitializeEarly_";
        internal const string LATE_INITIALIZED_NAME = "NetworkInitializeLate_";
        internal const string NETWORKINITIALIZE_EARLY_INTERNAL_NAME = "NetworkInitialize___Early";
        internal const string NETWORKINITIALIZE_LATE_INTERNAL_NAME = "NetworkInitialize__Late";
        private MethodAttributes PUBLIC_VIRTUAL_ATTRIBUTES = (MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.HideBySig);
#pragma warning disable CS0414
        private MethodAttributes PROTECTED_VIRTUAL_ATTRIBUTES = (MethodAttributes.Family | MethodAttributes.Virtual | MethodAttributes.HideBySig);
#pragma warning restore CS0414
        #endregion

        internal bool Process(TypeDefinition typeDef, List<(SyncType, ProcessedSync)> allProcessedSyncs, Dictionary<TypeDefinition, uint> childSyncTypeCounts, Dictionary<TypeDefinition, uint> childRpcCounts)
        {
            bool modified = false;
            TypeDefinition copyTypeDef = typeDef;
            TypeDefinition firstTypeDef = typeDef;

            //Make collection of NBs to processor.
            List<TypeDefinition> typeDefs = new List<TypeDefinition>();
            do
            {
                typeDefs.Add(copyTypeDef);
                copyTypeDef = TypeDefinitionExtensionsOld.GetNextBaseClassToProcess(copyTypeDef);
            } while (copyTypeDef != null);


            /* Iterate from child-most to parent first
             * while creating network initialize methods.
             * This is because the child-most must call the parents
             * base awake methods. */
            foreach (TypeDefinition td in typeDefs)
            {
                /* Class was already processed. Since child most is processed first
                 * this can occur if a class is inherited by multiple types. If a class
                 * has already been processed then there is no reason to scale up the hierarchy
                 * because it would have already been done. */
                if (HasClassBeenProcessed(td))
                    continue;

                //Disallow nested network behaviours.
                ICollection<TypeDefinition> nestedTds = td.NestedTypes;
                foreach (TypeDefinition item in nestedTds)
                {
                    if (item.InheritsNetworkBehaviour())
                    {
                        CodegenSession.LogError($"{td.FullName} contains nested NetworkBehaviours. These are not supported.");
                        return modified;
                    }
                }

                /* Create NetworkInitialize before-hand so the other procesors
                 * can use it. */
                MethodDefinition networkInitializeInternalMd;
                CreateNetworkInitializeMethods(td, out networkInitializeInternalMd);
                CallNetworkInitializeMethods(networkInitializeInternalMd);
            }

            /* Reverse and do RPCs/SyncTypes.
             * This counts up on children instead of the
             * parent, so we do not have to rewrite
             * parent numbers. */
            typeDefs.Reverse();

            foreach (TypeDefinition td in typeDefs)
            {
                /* Class was already processed. Since child most is processed first
                 * this can occur if a class is inherited by multiple types. If a class
                 * has already been processed then there is no reason to scale up the hierarchy
                 * because it would have already been done. */
                if (HasClassBeenProcessed(td))
                    continue;

                //No longer used...remove in rework.
                uint rpcCount = 0;
                childRpcCounts.TryGetValue(td, out rpcCount);
                /* Prediction. */
                /* Run prediction first since prediction will modify
                 * user data passed into prediction methods. Because of this
                 * other RPCs should use the modified version and reader/writers
                 * made for prediction. */
                modified |= CodegenSession.NetworkBehaviourPredictionProcessor.Process(td, ref rpcCount);
                //25ms 

                /* RPCs. */
                modified |= CodegenSession.RpcProcessor.Process(td, ref rpcCount);
                //30ms
                /* //perf rpcCounts can be optimized by having different counts
                 * for target, observers, server, replicate, and reoncile rpcs. Since
                 * each registers to their own delegates this is possible. */

                

                /* SyncTypes. */
                uint syncTypeStartCount;
                childSyncTypeCounts.TryGetValue(td, out syncTypeStartCount);
                modified |= CodegenSession.NetworkBehaviourSyncProcessor.Process(td, allProcessedSyncs, ref syncTypeStartCount);
                //70ms
                _processedClasses.Add(td);
            }

            int maxAllowSyncTypes = 256;
            if (allProcessedSyncs.Count > maxAllowSyncTypes)
            {
                CodegenSession.LogError($"Found {allProcessedSyncs.Count} SyncTypes within {firstTypeDef.FullName}. The maximum number of allowed SyncTypes within type and inherited types is {maxAllowSyncTypes}. Remove SyncTypes or condense them using data containers, or a custom SyncObject.");
                return false;
            }

            /* If here then all inerited classes for firstTypeDef have
             * been processed. */
            PrepareNetworkInitializeMethods(firstTypeDef);

            /* Make awake methods for all inherited classes
            * public and virtual. This is so I can add logic
            * to the firstTypeDef awake and still execute
            * user awake methods. */
            List<AwakeMethodData> awakeDatas = new List<AwakeMethodData>();
            if (!CreateOrModifyAwakeMethods(firstTypeDef, ref awakeDatas))
            {
                CodegenSession.LogError($"Was unable to make Awake methods public virtual starting on type {firstTypeDef.FullName}.");
                return modified;
            }

            //NetworkInitializeEarly.
            CallNetworkInitializeFromAwake(awakeDatas, true);
            //Call base awake, then call user logic methods.
            CallBaseAwakeOnCreatedMethods(awakeDatas);
            CallAwakeUserLogic(awakeDatas);
            //NetworkInitializeLate
            CallNetworkInitializeFromAwake(awakeDatas, false);
            //Since awake methods are erased ret has to be added at the end.
            AddReturnsToAwake(awakeDatas);

            CodegenSession.NetworkBehaviourSyncProcessor.CallBaseReadSyncVar(firstTypeDef);

            return modified;
        }


        /// <summary>
        /// Returns if a class has been processed.
        /// </summary>
        /// <param name="typeDef"></param>
        /// <returns></returns>
        private bool HasClassBeenProcessed(TypeDefinition typeDef)
        {
            return _processedClasses.Contains(typeDef);
        }

        /// <summary>
        /// Returns if any typeDefs have attributes which are not allowed to be used outside NetworkBehaviour.
        /// </summary>
        /// <param name="typeDefs"></param>
        /// <returns></returns>
        internal bool NonNetworkBehaviourHasInvalidAttributes(Collection<TypeDefinition> typeDefs)
        {
            bool error = false;
            foreach (TypeDefinition typeDef in typeDefs)
            {
                //Inherits, don't need to check.
                if (typeDef.InheritsNetworkBehaviour())
                    continue;

                //Check each method for attribute.
                foreach (MethodDefinition md in typeDef.Methods)
                {
                    //Has RPC attribute but doesn't inherit from NB.
                    if (CodegenSession.RpcProcessor.Attributes.HasRpcAttributes(md))
                    {
                        CodegenSession.LogError($"{typeDef.FullName} has one or more RPC attributes but does not inherit from NetworkBehaviour.");
                        error = true;
                    }
                }
                //Check fields for attribute.
                foreach (FieldDefinition fd in typeDef.Fields)
                {
                    if (CodegenSession.NetworkBehaviourSyncProcessor.GetSyncType(fd, false, out _) != SyncType.Unset)
                    {
                        CodegenSession.LogError($"{typeDef.FullName} has one or more SyncType attributes but does not inherit from NetworkBehaviour.");
                        error = true;
                    }
                }
            }

            return error;
        }

        

        /// <summary>
        /// Calls the next awake method if the nested awake was created by codegen.
        /// </summary>
        /// <returns></returns>
        private void CallBaseAwakeOnCreatedMethods(List<AwakeMethodData> datas)
        {
            /* Method definitions are added from child most
             * so they will always be going up the hierarchy. */
            for (int i = 0; i < datas.Count; i++)
            {
                AwakeMethodData amd = datas[i];
                /* If the awake already existed
                 * then let the user code be the final say
                 * if base is called. */
                if (!amd.Created)
                    continue;

                TypeDefinition copyTypeDef = amd.AwakeMethodDef.DeclaringType;

                /* Get next base awake first.
                 * If it doesn't exist then nothing can be called. */
                MethodReference baseAwakeMethodRef = copyTypeDef.GetMethodReferenceInBase(NetworkBehaviourHelper.AWAKE_METHOD_NAME);// GetNextAwake(i);
                if (baseAwakeMethodRef == null)
                    return;
                //MethodReference baseAwakeMethodRef = CodegenSession.ImportReference(baseAwakeMd);
                /* Awake will always exist because it was added previously.
                 * Get awake for the current declaring type. */
                MethodDefinition copyAwakeMd = copyTypeDef.GetMethod(NetworkBehaviourHelper.AWAKE_METHOD_NAME);

                //Check if they already call base.
                ILProcessor processor = copyAwakeMd.Body.GetILProcessor();
                bool alreadyHasBaseCall = false;
                //Check if already calls baseAwake.
                foreach (var item in copyAwakeMd.Body.Instructions)
                {

                    //If a call or call virt. Although, callvirt should never occur.
                    if (item.OpCode == OpCodes.Call || item.OpCode == OpCodes.Callvirt)
                    {
                        if (item.Operand != null && item.Operand.GetType().Name == nameof(MethodDefinition))
                        {
                            MethodDefinition md = (MethodDefinition)item.Operand;
                            if (md == baseAwakeMethodRef.Resolve())
                            {
                                alreadyHasBaseCall = true;
                                break;
                            }
                        }
                    }
                }

                if (!alreadyHasBaseCall)
                {
                    //Create instructions for base call.
                    processor.Emit(OpCodes.Ldarg_0); //base.
                    processor.Emit(OpCodes.Call, baseAwakeMethodRef);
                }
            }
        }


        /// <summary>
        /// Calls the next awake method if the nested awake was created by codegen.
        /// </summary>
        /// <returns></returns>
        private void CallAwakeUserLogic(List<AwakeMethodData> datas)
        {
            /* Method definitions are added from child most
             * so they will always be going up the hierarchy. */
            for (int i = 0; i < datas.Count; i++)
            {
                AwakeMethodData amd = datas[i];
                //If was created then there is no user logic.
                if (amd.Created)
                    continue;
                //If logic method is null. Should never be the case.
                if (amd.UserLogicMethodDef == null)
                    continue;

                MethodDefinition awakeMd = amd.AwakeMethodDef;
                CodegenSession.GeneralHelper.CallCopiedMethod(awakeMd, amd.UserLogicMethodDef);
            }

        }


        /// <summary>
        /// Adds a check to NetworkInitialize to see if it has already run.
        /// </summary>
        /// <param name="typeDef"></param>
        private void AddNetworkInitializeExecutedCheck(TypeDefinition firstTypeDef, bool initializeEarly, bool checkForExisting)
        {

            TypeDefinition copyTypeDef = firstTypeDef;
            AddCheck(copyTypeDef, initializeEarly);

            void AddCheck(TypeDefinition td, bool early)
            {
                string methodName;
                string fieldName;
                if (early)
                {
                    methodName = NETWORKINITIALIZE_EARLY_INTERNAL_NAME;
                    fieldName = $"{EARLY_INITIALIZED_NAME}{td.FullName}_{td.Module.Name}";
                }
                else
                {
                    methodName = NETWORKINITIALIZE_LATE_INTERNAL_NAME;
                    fieldName = $"{LATE_INITIALIZED_NAME}{td.FullName}_{td.Module.Name}";
                }

                MethodDefinition md = td.GetMethod(methodName);
                if (md == null)
                    return;

                FieldDefinition fd = copyTypeDef.GetField(fieldName)?.Resolve();
                if (fd == null)
                {
                    TypeReference boolTr = CodegenSession.GeneralHelper.GetTypeReference(typeof(bool));
                    //Add fields to see if it already ran.
                    fd = new FieldDefinition(fieldName, FieldAttributes.Private, boolTr);
                    td.Fields.Add(fd);
                }

                if (checkForExisting)
                {
                    bool alreadyChecked = false;
                    //Check if already calls baseAwake.
                    foreach (Instruction item in md.Body.Instructions)
                    {
                        //If a call or call virt. Although, callvirt should never occur.
                        if (item.OpCode == OpCodes.Ldfld && item.Operand != null && item.Operand is FieldDefinition opFd)
                        {
                            if (opFd == fd)
                            {
                                alreadyChecked = true;
                                break;
                            }
                        }
                    }

                    if (alreadyChecked)
                        return;
                }

                List<Instruction> insts = new List<Instruction>();
                ILProcessor processor = md.Body.GetILProcessor();
                //Add check if already called.
                //if (alreadyInitialized) return;
                Instruction skipFirstRetInst = processor.Create(OpCodes.Nop);
                insts.Add(processor.Create(OpCodes.Ldarg_0));
                insts.Add(processor.Create(OpCodes.Ldfld, fd));
                insts.Add(processor.Create(OpCodes.Brfalse_S, skipFirstRetInst));
                insts.Add(processor.Create(OpCodes.Ret));
                insts.Add(skipFirstRetInst);
                //Set field to true.
                insts.Add(processor.Create(OpCodes.Ldarg_0));
                insts.Add(processor.Create(OpCodes.Ldc_I4_1));
                insts.Add(processor.Create(OpCodes.Stfld, fd));
                processor.InsertFirst(insts);
            }

        }
        /// <summary>
        /// Gets the top-most parent away method.
        /// </summary>
        /// <param name="typeDef"></param>
        /// <returns></returns>
        private void PrepareNetworkInitializeMethods(TypeDefinition firstTypeDef)
        {
            TypeDefinition thisTypeDef = firstTypeDef;

            string[] initializeMethodNames = new string[] { NETWORKINITIALIZE_EARLY_INTERNAL_NAME, NETWORKINITIALIZE_LATE_INTERNAL_NAME };

            do
            {
                bool canCallBase = thisTypeDef.CanProcessBaseType();

                foreach (string mdName in initializeMethodNames)
                {
                    /* There are no more base calls to make but we still
                    * need to check if the initialize methods have already ran, so do that
                    * here. */
                    if (!canCallBase)
                    {
                        AddNetworkInitializeExecutedCheck(thisTypeDef, (mdName == NETWORKINITIALIZE_EARLY_INTERNAL_NAME), true);
                        continue;
                    }

                    /* Awake will always exist because it was added previously.
                     * Get awake for copy and base of copy. */
                    MethodDefinition thisMd = thisTypeDef.GetMethod(mdName);
                    MethodDefinition baseMd = thisTypeDef.BaseType.CachedResolve().GetMethod(mdName);
                    MethodReference baseMr = thisTypeDef.GetMethodReferenceInBase(mdName);
                    ILProcessor processor = thisMd.Body.GetILProcessor();

                    bool alreadyHasBaseCall = false;
                    //Check if already calls baseAwake.
                    foreach (Instruction item in thisMd.Body.Instructions)
                    {

                        //If a call or call virt. Although, callvirt should never occur.
                        if (item.OpCode == OpCodes.Call || item.OpCode == OpCodes.Callvirt)
                        {
                            if (item.Operand != null && item.Operand.GetType().Name == nameof(MethodDefinition))
                            {
                                MethodDefinition md = (MethodDefinition)item.Operand;
                                if (md == baseMd)
                                {
                                    alreadyHasBaseCall = true;
                                    break;
                                }
                            }
                        }
                    }

                    if (!alreadyHasBaseCall)
                    {
                        //Create instructions for base call.
                        List<Instruction> instructions = new List<Instruction>();
                        instructions.Add(processor.Create(OpCodes.Ldarg_0)); //this.
                        instructions.Add(processor.Create(OpCodes.Call, baseMr));
                        processor.InsertFirst(instructions);

                        AddNetworkInitializeExecutedCheck(thisTypeDef, (mdName == NETWORKINITIALIZE_EARLY_INTERNAL_NAME), false);
                    }
                }

                thisTypeDef = TypeDefinitionExtensionsOld.GetNextBaseClassToProcess(thisTypeDef);
            } while (thisTypeDef != null);

        }

        /// <summary>
        /// Adds returns awake method definitions within awakeDatas.
        /// </summary>
        private void AddReturnsToAwake(List<AwakeMethodData> awakeDatas)
        {
            foreach (AwakeMethodData amd in awakeDatas)
            {
                ILProcessor processor = amd.AwakeMethodDef.Body.GetILProcessor();
                //If no instructions or the last instruction isnt ret.
                if (processor.Body.Instructions.Count == 0
                    || processor.Body.Instructions[processor.Body.Instructions.Count - 1].OpCode != OpCodes.Ret)
                {
                    processor.Emit(OpCodes.Ret);
                }
            }
        }

        /// <summary>
        /// Calls NetworKInitializeLate method on the typeDef.
        /// </summary>
        /// <param name="copyTypeDef"></param>
        private void CallNetworkInitializeFromAwake(List<AwakeMethodData> awakeDatas, bool callEarly)
        {
            /* InitializeLate should be called after the user runs
             * all their Awake logic. This is so the user can configure
             * sync types on Awake and it won't trigger those values
             * as needing to be sent over the network, since both
             * server and client will be assigning them on Awake. */
            foreach (AwakeMethodData amd in awakeDatas)
            {
                string methodName = (callEarly) ? NETWORKINITIALIZE_EARLY_INTERNAL_NAME :
                    NETWORKINITIALIZE_LATE_INTERNAL_NAME;

                TypeDefinition td = amd.AwakeMethodDef.DeclaringType;
                MethodDefinition initializeMd = td.GetMethod(methodName);
                MethodReference initializeMr = CodegenSession.ImportReference(initializeMd);

                ILProcessor processor = amd.AwakeMethodDef.Body.GetILProcessor();
                processor.Emit(OpCodes.Ldarg_0);
                processor.Emit(OpCodes.Call, initializeMr);
            }
        }

        /// <summary>
        /// Creates an 'NetworkInitialize' method which is called by the childmost class to initialize scripts on Awake.
        /// </summary>
        private void CreateNetworkInitializeMethods(TypeDefinition typeDef, out MethodDefinition networkInitializeInternalMd)
        {
            CreateMethod(NETWORKINITIALIZE_EARLY_INTERNAL_NAME);
            CreateMethod(NETWORKINITIALIZE_LATE_INTERNAL_NAME);
            networkInitializeInternalMd = CreateMethod(CodegenSession.NetworkBehaviourHelper.NetworkInitializeInternal_MethodRef.Name);

            MethodDefinition CreateMethod(string name)
            {
                MethodDefinition md = typeDef.GetMethod(name);
                //Already made.
                if (md != null)
                    return md;

                //Create new public virtual method and add it to typedef.
                md = new MethodDefinition(name,
                    PUBLIC_VIRTUAL_ATTRIBUTES,
                    typeDef.Module.TypeSystem.Void);
                typeDef.Methods.Add(md);

                //Emit ret into new method.
                ILProcessor processor = md.Body.GetILProcessor();
                //End of method return.
                processor.Emit(OpCodes.Ret);
                return md;
            }
        }


        /// <summary>
        /// Creates an 'NetworkInitialize' method which is called by the childmost class to initialize scripts on Awake.
        /// </summary>
        private void CallNetworkInitializeMethods(MethodDefinition networkInitializeInternalMd)
        {
            ILProcessor processor = networkInitializeInternalMd.Body.GetILProcessor();

            networkInitializeInternalMd.Body.Instructions.Clear();
            CallMethod(NETWORKINITIALIZE_EARLY_INTERNAL_NAME);
            CallMethod(NETWORKINITIALIZE_LATE_INTERNAL_NAME);
            processor.Emit(OpCodes.Ret);

            void CallMethod(string name)
            {
                MethodDefinition md = networkInitializeInternalMd.DeclaringType.GetMethod(name);
                MethodReference mr = CodegenSession.ImportReference(md);

                processor.Emit(OpCodes.Ldarg_0);
                processor.Emit(OpCodes.Callvirt, mr);
            }
        }


        /// <summary>
        /// Creates Awake method for and all parents of typeDef using the parentMostAwakeMethodDef as a template.
        /// </summary>
        /// <returns>True if successful.</returns>
        private bool CreateOrModifyAwakeMethods(TypeDefinition typeDef, ref List<AwakeMethodData> datas)
        {
            //Now update all scopes/create methods.
            TypeDefinition copyTypeDef = typeDef;
            do
            {
                MethodDefinition tmpMd = copyTypeDef.GetMethod(NetworkBehaviourHelper.AWAKE_METHOD_NAME);
                string logicMethodName = $"{NetworkBehaviourHelper.AWAKE_METHOD_NAME}___UserLogic";
                bool create = (tmpMd == null);

                //Awake is found.
                if (!create)
                {
                    if (tmpMd.ReturnType != copyTypeDef.Module.TypeSystem.Void)
                    {
                        CodegenSession.LogError($"IEnumerator Awake methods are not supported within NetworkBehaviours.");
                        return false;
                    }
                    tmpMd.Attributes = PUBLIC_VIRTUAL_ATTRIBUTES;
                }
                //No awake yet.
                else
                {
                    //Make awake.
                    tmpMd = new MethodDefinition(NetworkBehaviourHelper.AWAKE_METHOD_NAME, PUBLIC_VIRTUAL_ATTRIBUTES, copyTypeDef.Module.TypeSystem.Void);
                    copyTypeDef.Methods.Add(tmpMd);
                    ILProcessor processor = tmpMd.Body.GetILProcessor();
                    processor.Emit(OpCodes.Ret);
                }

                //If logic already exist then awake has been processed already.
                MethodDefinition logicMd = copyTypeDef.GetMethod(logicMethodName);
                if (logicMd == null)
                {
                    logicMd = CodegenSession.GeneralHelper.CopyMethod(tmpMd, logicMethodName, out _);
                    //Clear awakeMethod.
                    tmpMd.Body.Instructions.Clear();
                }
                datas.Add(new AwakeMethodData(tmpMd, logicMd, create));

                copyTypeDef = TypeDefinitionExtensionsOld.GetNextBaseClassToProcess(copyTypeDef);

            } while (copyTypeDef != null);


            return true;
        }



        /// <summary>
        /// Makes all Awake methods within typeDef and base classes public and virtual.
        /// </summary>
        /// <param name="typeDef"></param>
        internal void CreateFirstNetworkInitializeCall(TypeDefinition typeDef, MethodDefinition firstUserAwakeMethodDef, MethodDefinition firstNetworkInitializeMethodDef)
        {
            ILProcessor processor;
            //Get awake for current method.
            MethodDefinition thisAwakeMethodDef = typeDef.GetMethod(NetworkBehaviourHelper.AWAKE_METHOD_NAME);
            bool created = false;

            //If no awake then make one.
            if (thisAwakeMethodDef == null)
            {
                created = true;

                thisAwakeMethodDef = new MethodDefinition(NetworkBehaviourHelper.AWAKE_METHOD_NAME, PUBLIC_VIRTUAL_ATTRIBUTES,
                    typeDef.Module.TypeSystem.Void);
                thisAwakeMethodDef.Body.InitLocals = true;
                typeDef.Methods.Add(thisAwakeMethodDef);

                processor = thisAwakeMethodDef.Body.GetILProcessor();
                processor.Emit(OpCodes.Ret);
            }

            //MethodRefs for networkinitialize and awake.
            MethodReference networkInitializeMethodRef = typeDef.Module.ImportReference(firstNetworkInitializeMethodDef);

            processor = thisAwakeMethodDef.Body.GetILProcessor();
            //Create instructions for base call.
            List<Instruction> instructions = new List<Instruction>();
            instructions.Add(processor.Create(OpCodes.Ldarg_0)); //this.
            instructions.Add(processor.Create(OpCodes.Call, networkInitializeMethodRef));

            /* If awake was created then make a call to the users
             * first awake. There's no reason to do this if awake
             * already existed because the user would have control
             * over making that call. */
            if (created && firstUserAwakeMethodDef != null)
            {
                MethodReference baseAwakeMethodRef = typeDef.Module.ImportReference(firstUserAwakeMethodDef);
                instructions.Add(processor.Create(OpCodes.Ldarg_0));//this.
                instructions.Add(processor.Create(OpCodes.Call, baseAwakeMethodRef));
            }

            processor.InsertFirst(instructions);
        }


    }
}