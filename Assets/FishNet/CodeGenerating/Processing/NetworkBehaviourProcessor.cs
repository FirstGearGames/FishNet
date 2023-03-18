using FishNet.CodeGenerating.Extension;
using FishNet.CodeGenerating.Helping;
using FishNet.CodeGenerating.Helping.Extension;
using FishNet.CodeGenerating.Processing.Rpc;
using FishNet.Configuring;
using FishNet.Object;
using MonoFN.Cecil;
using MonoFN.Cecil.Cil;
using MonoFN.Collections.Generic;
using System.Collections.Generic;
using System.Linq;

namespace FishNet.CodeGenerating.Processing
{
    internal class NetworkBehaviourProcessor : CodegenBase
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
                copyTypeDef = TypeDefinitionExtensionsOld.GetNextBaseClassToProcess(copyTypeDef, base.Session);
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
                    if (item.InheritsNetworkBehaviour(base.Session))
                    {
                        base.LogError($"{td.FullName} contains nested NetworkBehaviours. These are not supported.");
                        return modified;
                    }
                }

                /* Create NetworkInitialize before-hand so the other procesors
                 * can use it. */
                MethodDefinition networkInitializeIfDisabledMd;
                CreateNetworkInitializeMethods(td, out networkInitializeIfDisabledMd);
                CallNetworkInitializeMethods(networkInitializeIfDisabledMd);
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
                modified |= base.GetClass<PredictionProcessor>().Process(td, ref rpcCount);
                //25ms 

                /* RPCs. */
                modified |= base.GetClass<RpcProcessor>().ProcessLocal(td, ref rpcCount);
                //30ms
                /* //perf rpcCounts can be optimized by having different counts
                 * for target, observers, server, replicate, and reoncile rpcs. Since
                 * each registers to their own delegates this is possible. */

                

                /* SyncTypes. */
                uint syncTypeStartCount;
                childSyncTypeCounts.TryGetValue(td, out syncTypeStartCount);
                modified |= base.GetClass<NetworkBehaviourSyncProcessor>().Process(td, allProcessedSyncs, ref syncTypeStartCount);
                //70ms
                _processedClasses.Add(td);
            }

            int maxAllowSyncTypes = 256;
            if (allProcessedSyncs.Count > maxAllowSyncTypes)
            {
                base.LogError($"Found {allProcessedSyncs.Count} SyncTypes within {firstTypeDef.FullName}. The maximum number of allowed SyncTypes within type and inherited types is {maxAllowSyncTypes}. Remove SyncTypes or condense them using data containers, or a custom SyncObject.");
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
                base.LogError($"Was unable to make Awake methods public virtual starting on type {firstTypeDef.FullName}.");
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

            base.GetClass<NetworkBehaviourSyncProcessor>().CallBaseReadSyncVar(firstTypeDef);

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
                if (typeDef.InheritsNetworkBehaviour(base.Session))
                    continue;

                //Check each method for attribute.
                foreach (MethodDefinition md in typeDef.Methods)
                {
                    //Has RPC attribute but doesn't inherit from NB.
                    if (base.GetClass<RpcProcessor>().Attributes.HasRpcAttributes(md))
                    {
                        base.LogError($"{typeDef.FullName} has one or more RPC attributes but does not inherit from NetworkBehaviour.");
                        error = true;
                    }
                }
                //Check fields for attribute.
                foreach (FieldDefinition fd in typeDef.Fields)
                {
                    if (base.GetClass<NetworkBehaviourSyncProcessor>().GetSyncType(fd, false, out _) != SyncType.Unset)
                    {
                        base.LogError($"{typeDef.FullName} has one or more SyncType attributes but does not inherit from NetworkBehaviour.");
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

                TypeDefinition typeDef = amd.AwakeMethodDef.DeclaringType;

                /* Awake will always exist because it was added previously.
                 * Get awake for the current declaring type. */
                MethodDefinition awakeMd = typeDef.GetMethod(NetworkBehaviourHelper.AWAKE_METHOD_NAME);

                MethodReference baseAwakeMr = typeDef.GetMethodReferenceInBase(base.Session, NetworkBehaviourHelper.AWAKE_METHOD_NAME);
                if (baseAwakeMr == null)
                    return;
                MethodDefinition baseAwakeMd = baseAwakeMr.CachedResolve(base.Session);
                //MethodDefinition baseAwakeMd = typeDef.GetMethodDefinitionInBase(base.Session, NetworkBehaviourHelper.AWAKE_METHOD_NAME);
                if (baseAwakeMd == null)
                    return;

                //Check if they already call base.
                ILProcessor processor = awakeMd.Body.GetILProcessor();
                bool alreadyHasBaseCall = false;
                //Check if already calls baseAwake.
                foreach (var item in awakeMd.Body.Instructions)
                {
                    //If a call or call virt. Although, callvirt should never occur.
                    if (item.OpCode == OpCodes.Call || item.OpCode == OpCodes.Callvirt)
                    {
                        if (item.Operand != null && item.Operand.GetType().Name == nameof(MethodDefinition))
                        {
                            MethodDefinition md = (MethodDefinition)item.Operand;
                            if (md == baseAwakeMd)
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
                    processor.Emit(OpCodes.Call, baseAwakeMr);
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
                base.GetClass<GeneralHelper>().CallCopiedMethod(awakeMd, amd.UserLogicMethodDef);
            }
        }


        /// <summary>
        /// Adds a check to NetworkInitialize to see if it has already run.
        /// </summary>
        /// <param name="typeDef"></param>
        private void AddNetworkInitializeExecutedCheck(TypeDefinition firstTypeDef, bool initializeEarly)
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

                TypeReference boolTr = base.GetClass<GeneralHelper>().GetTypeReference(typeof(bool));
                FieldReference fr = copyTypeDef.GetOrCreateFieldReference(base.Session, fieldName, FieldAttributes.Private, boolTr, out bool created);

                if (created)
                {
                    List<Instruction> insts = new List<Instruction>();
                    ILProcessor processor = md.Body.GetILProcessor();
                    //Add check if already called.
                    //if (alreadyInitialized) return;
                    Instruction skipFirstRetInst = processor.Create(OpCodes.Nop);
                    insts.Add(processor.Create(OpCodes.Ldarg_0));
                    insts.Add(processor.Create(OpCodes.Ldfld, fr));
                    insts.Add(processor.Create(OpCodes.Brfalse_S, skipFirstRetInst));
                    insts.Add(processor.Create(OpCodes.Ret));
                    insts.Add(skipFirstRetInst);
                    //Set field to true.
                    insts.Add(processor.Create(OpCodes.Ldarg_0));
                    insts.Add(processor.Create(OpCodes.Ldc_I4_1));
                    insts.Add(processor.Create(OpCodes.Stfld, fr));
                    processor.InsertFirst(insts);
                }
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
                bool canCallBase = thisTypeDef.CanProcessBaseType(base.Session);

                foreach (string mdName in initializeMethodNames)
                {
                    /* There are no more base calls to make but we still
                    * need to check if the initialize methods have already ran, so do that
                    * here. */
                    if (canCallBase)
                    {
                        /* Awake will always exist because it was added previously.
                         * Get awake for copy and base of copy. */
                        MethodDefinition thisMd = thisTypeDef.GetMethod(mdName);
                        MethodReference baseMr = thisTypeDef.GetMethodReferenceInBase(base.Session, mdName);
                        MethodDefinition baseMd = baseMr.CachedResolve(base.Session);
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
                        }
                    }

                    AddNetworkInitializeExecutedCheck(thisTypeDef, (mdName == NETWORKINITIALIZE_EARLY_INTERNAL_NAME));
                }

                thisTypeDef = TypeDefinitionExtensionsOld.GetNextBaseClassToProcess(thisTypeDef, base.Session);
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
                MethodReference networkInitMr = td.GetMethodReference(base.Session, methodName);

                ILProcessor processor = amd.AwakeMethodDef.Body.GetILProcessor();
                processor.Emit(OpCodes.Ldarg_0);
                processor.Emit(networkInitMr.GetCallOpCode(base.Session), networkInitMr);
            }
        }

        /// <summary>
        /// Creates an 'NetworkInitialize' method which is called by the childmost class to initialize scripts on Awake.
        /// </summary>
        private void CreateNetworkInitializeMethods(TypeDefinition typeDef, out MethodDefinition networkInitializeIfDisabledMd)
        {
            CreateMethod(NETWORKINITIALIZE_EARLY_INTERNAL_NAME);
            CreateMethod(NETWORKINITIALIZE_LATE_INTERNAL_NAME);

            MethodDefinition baseInitIfDisabled = base.GetClass<NetworkBehaviourHelper>().NetworkInitializeIfDisabled_MethodRef.CachedResolve(base.Session);
            networkInitializeIfDisabledMd = CreateMethod(baseInitIfDisabled.Name, baseInitIfDisabled);

            MethodDefinition CreateMethod(string name, MethodDefinition copied = null)
            {
                MethodDefinition md;
                bool created;
                if (copied == null)
                    md = typeDef.GetOrCreateMethodDefinition(base.Session, name, PUBLIC_VIRTUAL_ATTRIBUTES, typeDef.Module.TypeSystem.Void, out created);
                else
                    md = typeDef.GetOrCreateMethodDefinition(base.Session, name, copied, true, out created);

                if (created)
                {
                    //Emit ret into new method.
                    ILProcessor processor = md.Body.GetILProcessor();
                    //End of method return.
                    processor.Emit(OpCodes.Ret);
                }

                return md;
            }
        }


        /// <summary>
        /// Creates an 'NetworkInitialize' method which is called by the childmost class to initialize scripts on Awake.
        /// </summary>
        private void CallNetworkInitializeMethods(MethodDefinition networkInitializeIfDisabledMd)
        {
            ILProcessor processor = networkInitializeIfDisabledMd.Body.GetILProcessor();

            networkInitializeIfDisabledMd.Body.Instructions.Clear();
            CallMethod(NETWORKINITIALIZE_EARLY_INTERNAL_NAME);
            CallMethod(NETWORKINITIALIZE_LATE_INTERNAL_NAME);
            processor.Emit(OpCodes.Ret);

            void CallMethod(string name)
            {
                MethodReference initIfDisabledMr = networkInitializeIfDisabledMd.DeclaringType.GetMethodReference(base.Session, name);
                processor.Emit(OpCodes.Ldarg_0);
                processor.Emit(initIfDisabledMr.GetCallOpCode(base.Session), initIfDisabledMr);
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
                bool created;
                MethodDefinition awakeMd = copyTypeDef.GetOrCreateMethodDefinition(base.Session, NetworkBehaviourHelper.AWAKE_METHOD_NAME, PUBLIC_VIRTUAL_ATTRIBUTES, copyTypeDef.Module.TypeSystem.Void, out created);

                //Awake is found. Check for invalid return type.
                if (!created)
                {
                    if (awakeMd.ReturnType != copyTypeDef.Module.TypeSystem.Void)
                    {
                        base.LogError($"IEnumerator Awake methods are not supported within NetworkBehaviours.");
                        return false;
                    }
                    awakeMd.Attributes = PUBLIC_VIRTUAL_ATTRIBUTES;
                }
                //Aways was made.
                else
                {
                    ILProcessor processor = awakeMd.Body.GetILProcessor();
                    processor.Emit(OpCodes.Ret);
                }

                MethodDefinition logicMd = base.GetClass<GeneralHelper>().CopyIntoNewMethod(awakeMd, $"{NetworkBehaviourHelper.AWAKE_METHOD_NAME}___UserLogic", out _);
                //Clear original awake.
                awakeMd.Body.Instructions.Clear();
                datas.Add(new AwakeMethodData(awakeMd, logicMd, created));

                copyTypeDef = TypeDefinitionExtensionsOld.GetNextBaseClassToProcess(copyTypeDef, base.Session);

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