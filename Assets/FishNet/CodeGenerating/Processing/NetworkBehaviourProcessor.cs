using FishNet.CodeGenerating.Helping;
using FishNet.CodeGenerating.Helping.Extension;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Collections.Generic;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FishNet.CodeGenerating.Processing
{
    internal class NetworkBehaviourProcessor
    {
        #region Misc.
        /// <summary>
        /// Methods modified or iterated during weaving.
        /// </summary>
        internal List<MethodDefinition> ModifiedMethodDefinitions = new List<MethodDefinition>();
        private List<TypeDefinition> _processedClasses = new List<TypeDefinition>();
        #endregion

        #region Const.
        internal const string NETWORKINITIALIZE_EARLY_INTERNAL_NAME = "NetworkInitialize_Early___Internal";
        internal const string NETWORKINITIALIZE_LATE_INTERNAL_NAME = "NetworkInitialize_Late___Internal";
        private MethodAttributes PUBLIC_VIRTUAL_ATTRIBUTES = (MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.HideBySig);
        #endregion 

        internal bool Process(TypeDefinition typeDef, ref int allRpcCount, List<(SyncType, ProcessedSync)> allProcessedSyncs, HashSet<string> allProcessedCallbacks)
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
            CallBaseAwakeMethods(firstTypeDef);

            do
            {
                /* Class was already processed. Since child most is processed first
                 * this can occur if a class is inherited by multiple types. If a class
                 * has already then processed then there is no reason to scale up the hierarchy
                 * because it would have already been done. */
                if (HasClassBeenProcessed(copyTypeDef))
                    break;

                //Disallow nested network behaviours.
                if (copyTypeDef.NestedTypes
                    .Where(t => t.IsSubclassOf(CodegenSession.ObjectHelper.NetworkBehaviour_FullName))
                    .ToList().Count > 0)
                {
                    CodegenSession.LogError($"{copyTypeDef.FullName} contains nested NetworkBehaviours. These are not supported.");
                    return modified;
                }

                /* Create NetworkInitialize before-hand so the other procesors
                 * can use it. */
                CreateNetworkInitializeMethods(copyTypeDef);
                modified |= CodegenSession.NetworkBehaviourRpcProcessor.Process(copyTypeDef, ref allRpcCount);
                modified |= CodegenSession.NetworkBehaviourCallbackProcessor.Process(firstTypeDef, copyTypeDef, allProcessedCallbacks);
                modified |= CodegenSession.NetworkBehaviourSyncProcessor.Process(copyTypeDef, allProcessedSyncs);
                
                copyTypeDef = TypeDefinitionExtensions.GetNextBaseClassToProcess(copyTypeDef);
            } while (copyTypeDef != null);

            /* If here then all inerited classes for firstTypeDef have
             * been processed. */
            CallNetworkInitializeFromAwake(firstTypeDef, true);
            CallNetworkInitializeFromAwake(firstTypeDef, false);

            //Add to processed.
            copyTypeDef = firstTypeDef;
            do
            {
                _processedClasses.Add(copyTypeDef);
                copyTypeDef = TypeDefinitionExtensions.GetNextBaseClassToProcess(copyTypeDef);
            } while (copyTypeDef != null);

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
                    if (CodegenSession.NetworkBehaviourRpcProcessor.GetRpcAttribute(md, out _) != null)
                    {
                        CodegenSession.LogError($"{typeDef.FullName} has one or more RPC attributes but does not inherit from NetworkBehaviour.");
                        error = true;
                    }
                }
                //Check fields for attribute.
                foreach (FieldDefinition fd in typeDef.Fields)
                {
                    if (CodegenSession.NetworkBehaviourSyncProcessor.GetSyncType(fd, out _) != SyncType.Unset)
                    {
                        CodegenSession.LogError($"{typeDef.FullName} has one or more SyncType attributes but does not inherit from NetworkBehaviour.");
                        error = true;
                    }
                }
            }

            return error;
        }

        /// <summary>
        /// Gets the top-most parent away method.
        /// </summary>
        /// <param name="typeDef"></param>
        /// <returns></returns>
        private void CallBaseAwakeMethods(TypeDefinition firstTypeDef)
        {
            TypeDefinition copyTypeDef = firstTypeDef;

            do
            {
                //No more awakes to call.
                if (!copyTypeDef.CanProcessBaseType())
                    return;

                /* Awake will always exist because it was added previously.
                 * Get awake for copy and base of copy. */
                MethodDefinition copyAwakeMethodDef = copyTypeDef.GetMethod(ObjectHelper.AWAKE_METHOD_NAME);
                MethodDefinition baseAwakeMethodDef = copyTypeDef.BaseType.Resolve().GetMethod(ObjectHelper.AWAKE_METHOD_NAME);
                MethodReference baseAwakeMethodRef = CodegenSession.Module.ImportReference(baseAwakeMethodDef);

                ILProcessor processor = copyAwakeMethodDef.Body.GetILProcessor();

                bool alreadyHasBaseCall = false;
                //Check if already calls baseAwake.
                foreach (var item in copyAwakeMethodDef.Body.instructions)
                {

                    //If a call or call virt. Although, callvirt should never occur.
                    if (item.OpCode == OpCodes.Call || item.OpCode == OpCodes.Callvirt)
                    {
                        if (item.Operand != null && item.Operand.GetType().Name == nameof(MethodDefinition))
                        {
                            MethodDefinition md = (MethodDefinition)item.Operand;
                            if (md == baseAwakeMethodDef)
                            {
                                alreadyHasBaseCall = true;
                                break;
                            }
                        }
                    }
                }

                /* //todo only call awake if client has another awake and don't trickle all the way up hierarchy.
                 * in the first awake call networkinitialize methods, and have each of them call up the hierarchy
                 * instead. this is to prevent calling awake where the user may not want to. I cant not call
                 * awake with the current technique either because this will lead to the networkinitialize methods
                 * not firing. best option is to call networkinitialize on the child most and awake once from
                 * child most. */

                if (!alreadyHasBaseCall)
                {
                    //Create instructions for base call.
                    List<Instruction> instructions = new List<Instruction>();
                    instructions.Add(processor.Create(OpCodes.Ldarg_0)); //this.
                    instructions.Add(processor.Create(OpCodes.Call, baseAwakeMethodRef));
                    processor.InsertFirst(instructions);
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
                if (!_processedClasses.Contains(copyTypeDef))
                {
                    string methodName = (callEarly) ? NETWORKINITIALIZE_EARLY_INTERNAL_NAME :
                        NETWORKINITIALIZE_LATE_INTERNAL_NAME;
                    MethodDefinition initializeMethodDef = copyTypeDef.GetMethod(methodName);
                    MethodReference initializeMethodRef = CodegenSession.Module.ImportReference(initializeMethodDef);

                    MethodDefinition awakeMethodDef = copyTypeDef.GetMethod(ObjectHelper.AWAKE_METHOD_NAME);
                    ILProcessor processor = awakeMethodDef.Body.GetILProcessor();

                    List<Instruction> insts = new List<Instruction>();
                    insts.Add(processor.Create(OpCodes.Ldarg_0)); //this.
                    insts.Add(processor.Create(OpCodes.Call, initializeMethodRef));

                    if (callEarly)
                        processor.InsertFirst(insts);
                    else
                        processor.InsertBeforeReturns(insts);
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
            TypeDefinition copyTypeDef = typeDef;
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
                    tmpMd.Attributes = PUBLIC_VIRTUAL_ATTRIBUTES;
                }
                //Does not exist, add it.
                else
                {
                    tmpMd = new MethodDefinition(ObjectHelper.AWAKE_METHOD_NAME, PUBLIC_VIRTUAL_ATTRIBUTES, CodegenSession.Module.TypeSystem.Void);
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



        /// <summary>
        /// Creates a call to NetworkBehaviour to register RPC count.
        /// </summary>
        internal void CreateRegisterRpcCount(TypeDefinition typeDef, int allRpcCount) //fix
        {
            //if (HasClassBeenProcessed(typeDef))
            //    return;

            //MethodDefinition methodDef = typeDef.GetMethod(NETWORKINITIALIZE_INTERNAL_NAME);

            ////Insert at the beginning to ensure user code doesn't return out of it.
            //ILProcessor processor = methodDef.Body.GetILProcessor();

            //List<Instruction> instructions = new List<Instruction>();
            //instructions.Add(processor.Create(OpCodes.Ldarg_0)); //this.
            //instructions.Add(processor.Create(OpCodes.Ldc_I4, (int)allRpcCount));
            //instructions.Add(processor.Create(OpCodes.Call, CodegenSession.ObjectHelper.NetworkBehaviour_SetRpcMethodCountInternal_MethodRef));


            ////SetGivenName debug.
            ////System.Type networkBehaviourType = typeof(NetworkBehaviour);
            ////TypeReference trr = typeDef.module.ImportReference(networkBehaviourType);
            ////MethodDefinition mrd = trr.Resolve().GetMethod("SetGivenName");
            ////MethodReference mrr = typeDef.module.ImportReference(mrd);
            ////instructions.Add(processor.Create(OpCodes.Ldarg_0));
            ////instructions.Add(processor.Create(OpCodes.Ldstr, typeDef.Name));
            ////instructions.Add(processor.Create(OpCodes.Call, mrr));


            //processor.InsertFirst(instructions);
        }




    }
}