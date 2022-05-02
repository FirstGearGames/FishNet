using FishNet.CodeGenerating.Helping;
using FishNet.CodeGenerating.Helping.Extension;
using FishNet.Object;
using MonoFN.Cecil;
using MonoFN.Cecil.Cil;
using MonoFN.Collections.Generic;
using System.Collections.Generic;
using System.Linq;
using UnityDebug = UnityEngine.Debug;

namespace FishNet.CodeGenerating.Processing
{
    internal class NetworkBehaviourProcessor
    {
        #region Misc.
        /// <summary>
        /// Methods modified or iterated during weaving.
        /// </summary>
        internal List<MethodDefinition> ModifiedMethodDefinitions = new List<MethodDefinition>();
        /// <summary>
        /// Classes which have been processed for all NetworkBehaviour features.
        /// </summary>
        private HashSet<TypeDefinition> _processedClasses = new HashSet<TypeDefinition>();
        /// <summary>
        /// Classes which have had Early NetworkInitialize methods called.
        /// </summary>
        private HashSet<TypeDefinition> _earlyNetworkInitializedClasses = new HashSet<TypeDefinition>();
        /// <summary>
        /// Classes which have had Late NetworkInitialize methods called.
        /// </summary>
        private HashSet<TypeDefinition> _lateNetworkInitializedClasses = new HashSet<TypeDefinition>();
        #endregion

        #region Const.
        internal const string NETWORKINITIALIZE_EARLY_INTERNAL_NAME = "NetworkInitialize___Early";
        internal const string NETWORKINITIALIZE_LATE_INTERNAL_NAME = "NetworkInitialize__Late";
        private MethodAttributes PUBLIC_VIRTUAL_ATTRIBUTES = (MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.HideBySig);
        private MethodAttributes PROTECTED_VIRTUAL_ATTRIBUTES = (MethodAttributes.Family | MethodAttributes.Virtual | MethodAttributes.HideBySig);
        #endregion

        internal bool Process(TypeDefinition typeDef, List<(SyncType, ProcessedSync)> allProcessedSyncs, Dictionary<TypeDefinition, uint> childSyncTypeCounts, Dictionary<TypeDefinition, uint> childRpcCounts)
        {
            bool modified = false;
            TypeDefinition copyTypeDef = typeDef;
            TypeDefinition firstTypeDef = typeDef;
            /* Make awake methods for all inherited classes
             * public and virtual. This is so I can add logic
             * to the firstTypeDef awake and still execute
             * user awake methods. */
            if (!CreateOrModifyAwakeMethods(firstTypeDef))
            {
                CodegenSession.LogError($"Was unable to make Awake methods public virtual starting on type {firstTypeDef.FullName}.");
                return modified;
            }

            do
            {
                /* Class was already processed. Since child most is processed first
                 * this can occur if a class is inherited by multiple types. If a class
                 * has already been processed then there is no reason to scale up the hierarchy
                 * because it would have already been done. */
                if (HasClassBeenProcessed(copyTypeDef))
                    break;

                //Disallow nested network behaviours.
                ICollection<TypeDefinition> nestedTds = copyTypeDef.NestedTypes;
                foreach (TypeDefinition item in nestedTds)
                {
                    if (item.InheritsNetworkBehaviour())
                    {
                        CodegenSession.LogError($"{copyTypeDef.FullName} contains nested NetworkBehaviours. These are not supported.");
                        return modified;
                    }
                }

                /* Create NetworkInitialize before-hand so the other procesors
                 * can use it. */
                CreateNetworkInitializeMethods(copyTypeDef);
                //No longer used...remove in rework.
                uint rpcCount = 0;
                childRpcCounts.TryGetValue(copyTypeDef, out rpcCount);
                /* Prediction. */
                /* Run prediction first since prediction will modify
                 * user data passed into prediction methods. Because of this
                 * other RPCs should use the modified version and reader/writers
                 * made for prediction. */
                modified |= CodegenSession.NetworkBehaviourPredictionProcessor.Process(copyTypeDef, ref rpcCount);
                //25ms 

                /* RPCs. */
                modified |= CodegenSession.RpcProcessor.Process(copyTypeDef, ref rpcCount);
                //30ms
                /* //perf rpcCounts can be optimized by having different counts
                 * for target, observers, server, replicate, and reoncile rpcs. Since
                 * each registers to their own delegates this is possible. */

                

                /* SyncTypes. */
                uint syncTypeStartCount;
                childSyncTypeCounts.TryGetValue(copyTypeDef, out syncTypeStartCount);
                modified |= CodegenSession.NetworkBehaviourSyncProcessor.Process(copyTypeDef, allProcessedSyncs, ref syncTypeStartCount);
                //70ms
                _processedClasses.Add(copyTypeDef);

                copyTypeDef = TypeDefinitionExtensions.GetNextBaseClassToProcess(copyTypeDef);
            } while (copyTypeDef != null);


            int maxAllowSyncTypes = 256;
            if (allProcessedSyncs.Count > maxAllowSyncTypes)
            {
                CodegenSession.LogError($"Found {allProcessedSyncs.Count} SyncTypes within {firstTypeDef.FullName}. The maximum number of allowed SyncTypes within type and inherited types is {maxAllowSyncTypes}. Remove SyncTypes or condense them using data containers, or a custom SyncObject.");
                return false;
            }

            /* If here then all inerited classes for firstTypeDef have
             * been processed. */
            CallBaseNetworkInitializeMethods(firstTypeDef);
            CallNetworkInitializeFromAwake(firstTypeDef, true);
            CallNetworkInitializeFromAwake(firstTypeDef, false);
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

        

        ///// <summary>
        ///// Gets the top-most parent away method.
        ///// </summary>
        ///// <param name="typeDef"></param>
        ///// <returns></returns>
        //private void CallBaseAwakeMethods(TypeDefinition firstTypeDef)
        //{
        //    TypeDefinition copyTypeDef = firstTypeDef;

        //    do
        //    {
        //        //No more awakes to call.
        //        if (!copyTypeDef.CanProcessBaseType())
        //            return;

        //        /* Awake will always exist because it was added previously.
        //         * Get awake for copy and base of copy. */
        //        MethodDefinition copyAwakeMethodDef = copyTypeDef.GetMethod(ObjectHelper.AWAKE_METHOD_NAME);
        //        MethodDefinition baseAwakeMethodDef = copyTypeDef.BaseType.CachedResolve().GetMethod(ObjectHelper.AWAKE_METHOD_NAME);
        //        MethodReference baseAwakeMethodRef = CodegenSession.ImportReference(baseAwakeMethodDef);

        //        ILProcessor processor = copyAwakeMethodDef.Body.GetILProcessor();

        //        bool alreadyHasBaseCall = false;
        //        //Check if already calls baseAwake.
        //        foreach (var item in copyAwakeMethodDef.Body.Instructions)
        //        {

        //            //If a call or call virt. Although, callvirt should never occur.
        //            if (item.OpCode == OpCodes.Call || item.OpCode == OpCodes.Callvirt)
        //            {
        //                if (item.Operand != null && item.Operand.GetType().Name == nameof(MethodDefinition))
        //                {
        //                    MethodDefinition md = (MethodDefinition)item.Operand;
        //                    if (md == baseAwakeMethodDef)
        //                    {
        //                        alreadyHasBaseCall = true;
        //                        break;
        //                    }
        //                }
        //            }
        //        }

        //        /* //todo only call awake if client has another awake and don't trickle all the way up hierarchy.
        //         * in the first awake call networkinitialize methods, and have each of them call up the hierarchy
        //         * instead. this is to prevent calling awake where the user may not want to. I cant not call
        //         * awake with the current technique either because this will lead to the networkinitialize methods
        //         * not firing. best option is to call networkinitialize on the child most and awake once from
        //         * child most. */

        //        if (!alreadyHasBaseCall)
        //        {
        //            //Create instructions for base call.
        //            List<Instruction> instructions = new List<Instruction>();
        //            instructions.Add(processor.Create(OpCodes.Ldarg_0)); //this.
        //            instructions.Add(processor.Create(OpCodes.Call, baseAwakeMethodRef));
        //            processor.InsertFirst(instructions);
        //        }

        //        copyTypeDef = TypeDefinitionExtensions.GetNextBaseClassToProcess(copyTypeDef);
        //    } while (copyTypeDef != null);

        //}



        /// <summary>
        /// Gets the top-most parent away method.
        /// </summary>
        /// <param name="typeDef"></param>
        /// <returns></returns>
        private void CallBaseNetworkInitializeMethods(TypeDefinition firstTypeDef)
        {
            TypeDefinition copyTypeDef = firstTypeDef;

            string[] initializeMethodNames = new string[] { NETWORKINITIALIZE_EARLY_INTERNAL_NAME, NETWORKINITIALIZE_LATE_INTERNAL_NAME };

            do
            {
                //No more initialize methods to call.
                if (!copyTypeDef.CanProcessBaseType())
                    return;

                foreach (string mdName in initializeMethodNames)
                {
                    /* Awake will always exist because it was added previously.
                     * Get awake for copy and base of copy. */
                    MethodDefinition copyMd = copyTypeDef.GetMethod(mdName);
                    MethodDefinition baseMd = copyTypeDef.BaseType.CachedResolve().GetMethod(mdName);
                    MethodReference baseMr = CodegenSession.ImportReference(baseMd);

                    ILProcessor processor = copyMd.Body.GetILProcessor();

                    bool alreadyHasBaseCall = false;
                    //Check if already calls baseAwake.
                    foreach (Instruction item in copyMd.Body.Instructions)
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

                copyTypeDef = TypeDefinitionExtensions.GetNextBaseClassToProcess(copyTypeDef);
            } while (copyTypeDef != null);

        }


        /// <summary>
        /// Calls NetworKInitializeLate method on the typeDef.
        /// </summary>
        /// <param name="copyTypeDef"></param>
        private void CallNetworkInitializeFromAwake(TypeDefinition startTypeDef, bool callEarly)
        {
            /* InitializeLate should be called after the user runs
             * all their Awake logic. This is so the user can configure
             * sync types on Awake and it won't trigger those values
             * as needing to be sent over the network, since both
             * server and client will be assigning them on Awake. */
            TypeDefinition copyTypeDef = startTypeDef;
            do
            {
                HashSet<TypeDefinition> processed = (callEarly) ? _earlyNetworkInitializedClasses : _lateNetworkInitializedClasses;
                if (!processed.Contains(copyTypeDef))
                {
                    string methodName = (callEarly) ? NETWORKINITIALIZE_EARLY_INTERNAL_NAME :
                        NETWORKINITIALIZE_LATE_INTERNAL_NAME;
                    MethodDefinition initializeMethodDef = copyTypeDef.GetMethod(methodName);
                    MethodReference initializeMethodRef = CodegenSession.ImportReference(initializeMethodDef);

                    MethodDefinition awakeMethodDef = copyTypeDef.GetMethod(ObjectHelper.AWAKE_METHOD_NAME);
                    ILProcessor processor = awakeMethodDef.Body.GetILProcessor();

                    List<Instruction> insts = new List<Instruction>();
                    insts.Add(processor.Create(OpCodes.Ldarg_0)); //this.
                    insts.Add(processor.Create(OpCodes.Call, initializeMethodRef));

                    if (callEarly)
                        processor.InsertFirst(insts);
                    else
                        processor.InsertBeforeReturns(insts);

                    processed.Add(copyTypeDef);
                }

                copyTypeDef = TypeDefinitionExtensions.GetNextBaseClassToProcess(copyTypeDef);
            } while (copyTypeDef != null);
        }

        /// <summary>
        /// Creates an 'NetworkInitialize' method which is called by the childmost class to initialize scripts on Awake.
        /// </summary>
        /// <param name="typeDef"></param>
        /// <returns></returns>
        private void CreateNetworkInitializeMethods(TypeDefinition typeDef)
        {
            CreateMethod(NETWORKINITIALIZE_EARLY_INTERNAL_NAME);
            CreateMethod(NETWORKINITIALIZE_LATE_INTERNAL_NAME);

            void CreateMethod(string name)
            {
                MethodDefinition md = typeDef.GetMethod(name);
                //Already made.
                if (md != null)
                    return;

                //Create new public virtual method and add it to typedef.
                md = new MethodDefinition(name,
                    PUBLIC_VIRTUAL_ATTRIBUTES,
                    typeDef.Module.TypeSystem.Void);
                typeDef.Methods.Add(md);
                //Emit ret into new method.
                ILProcessor processor = md.Body.GetILProcessor();
                processor.Emit(OpCodes.Ret);
            }
        }

        /// <summary>
        /// Creates Awake method for and all parents of typeDef using the parentMostAwakeMethodDef as a template.
        /// </summary>
        /// <param name="climbTypeDef"></param>
        /// <param name="parentMostAwakeMethodDef"></param>
        /// <returns>True if successful.</returns>
        private bool CreateOrModifyAwakeMethods(TypeDefinition typeDef)
        {
            //First check if any are public, if so that's the attribute all of them will use.
            bool usePublic = false;

            //Determine if awake needs to be public or protected.
            TypeDefinition copyTypeDef = typeDef;
            do
            {
                MethodDefinition tmpMd = copyTypeDef.GetMethod(ObjectHelper.AWAKE_METHOD_NAME);
                //Exist, make it public virtual.
                if (tmpMd != null)
                {
                    //Uses public.
                    if (tmpMd.Attributes.HasFlag(MethodAttributes.Public))
                    {
                        usePublic = true;
                        break;
                    }
                }
                copyTypeDef = TypeDefinitionExtensions.GetNextBaseClassToProcess(copyTypeDef);

            } while (copyTypeDef != null);


            MethodAttributes attributes = (usePublic) ? PUBLIC_VIRTUAL_ATTRIBUTES : PROTECTED_VIRTUAL_ATTRIBUTES;
            //Now update all scopes/create methods.
            copyTypeDef = typeDef;
            do
            {
                MethodDefinition tmpMd = copyTypeDef.GetMethod(ObjectHelper.AWAKE_METHOD_NAME);
                //Exist, make it public virtual.
                if (tmpMd != null)
                {
                    if (tmpMd.ReturnType != copyTypeDef.Module.TypeSystem.Void)
                    {
                        CodegenSession.LogError($"IEnumerator Awake methods are not supported within NetworkBehaviours.");
                        return false;
                    }
                    tmpMd.Attributes = attributes;
                }
                //Does not exist, add it.
                else
                {
                    tmpMd = new MethodDefinition(ObjectHelper.AWAKE_METHOD_NAME, attributes, copyTypeDef.Module.TypeSystem.Void);
                    copyTypeDef.Methods.Add(tmpMd);
                    ILProcessor processor = tmpMd.Body.GetILProcessor();
                    processor.Emit(OpCodes.Ret);
                }

                copyTypeDef = TypeDefinitionExtensions.GetNextBaseClassToProcess(copyTypeDef);

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
            MethodDefinition thisAwakeMethodDef = typeDef.GetMethod(ObjectHelper.AWAKE_METHOD_NAME);
            bool created = false;

            //If no awake then make one.
            if (thisAwakeMethodDef == null)
            {
                created = true;

                thisAwakeMethodDef = new MethodDefinition(ObjectHelper.AWAKE_METHOD_NAME, PUBLIC_VIRTUAL_ATTRIBUTES,
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