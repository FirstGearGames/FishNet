
using FishNet.CodeGenerating.Helping;
using FishNet.CodeGenerating.Helping.Extension;
using FishNet.Object;
using Mono.Cecil;
using Mono.Cecil.Cil;
using System.Collections.Generic;
using System.Reflection;

namespace FishNet.CodeGenerating.Processing
{
    internal class NetworkBehaviourCallbackProcessor
    {
        internal bool Process(TypeDefinition firstTypeDef, TypeDefinition typeDef, HashSet<string> allProcessedCallbacks)
        {
            bool modified = false;

            modified |= CreateUsingCallbacks(firstTypeDef, typeDef, allProcessedCallbacks);

            return modified;
        }

        /// <summary>
        /// Creates calls to UsingXXXXX for NetworkBehaviour callbacks.
        /// </summary>
        /// <param name="typeDef"></param>
        /// <param name="diagnostics"></param>
        private bool CreateUsingCallbacks(TypeDefinition firstTypeDef, TypeDefinition typeDef, HashSet<string> allProcessedCallbacks)
        {
            bool modified = false;

            ModuleDefinition moduleDef = typeDef.Module;
            MethodDefinition methodDef = typeDef.GetMethod(NetworkBehaviourProcessor.NETWORKINITIALIZE_EARLY_INTERNAL_NAME);

            ILProcessor processor = methodDef.Body.GetILProcessor();
            List<Instruction> instructions = new List<Instruction>();

            System.Type userClassType = typeDef.GetMonoType();
            if (userClassType == null)
                return modified;
            foreach (MethodInfo methodInfo in userClassType.GetMethods((BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)))
            {
                //If already using callback in one or more inherited classes.
                if (allProcessedCallbacks.Contains(methodInfo.Name))
                    continue;

                int startCount = instructions.Count;

                //OnStartServer.
                if (methodInfo.Name == nameof(NetworkBehaviour.OnStartServer))
                {
                    instructions.Add(processor.Create(OpCodes.Ldarg_0));//this.
                    instructions.Add(processor.Create(OpCodes.Call, CodegenSession.ObjectHelper.NetworkBehaviour_UsingOnStartServerInternal_MethodRef));
                }
                //OnStopServer.
                else if (methodInfo.Name == nameof(NetworkBehaviour.OnStopServer))
                {
                    instructions.Add(processor.Create(OpCodes.Ldarg_0));//this.
                    instructions.Add(processor.Create(OpCodes.Call, CodegenSession.ObjectHelper.NetworkBehaviour_UsingOnStopServerInternal_MethodRef));
                }
                //OnOwnershipServer.
                else if (methodInfo.Name == nameof(NetworkBehaviour.OnOwnershipServer))
                {
                    instructions.Add(processor.Create(OpCodes.Ldarg_0));//this.
                    instructions.Add(processor.Create(OpCodes.Call, CodegenSession.ObjectHelper.NetworkBehaviour_UsingOnOwnershipServerInternal_MethodRef));
                }
                //OnSpawnServer.
                else if (methodInfo.Name == nameof(NetworkBehaviour.OnSpawnServer))
                {
                    instructions.Add(processor.Create(OpCodes.Ldarg_0));//this.
                    instructions.Add(processor.Create(OpCodes.Call, CodegenSession.ObjectHelper.NetworkBehaviour_UsingOnSpawnServerInternal_MethodRef));
                }
                //OnDespawnServer.
                else if (methodInfo.Name == nameof(NetworkBehaviour.OnDespawnServer))
                {
                    instructions.Add(processor.Create(OpCodes.Ldarg_0));//this.
                    instructions.Add(processor.Create(OpCodes.Call, CodegenSession.ObjectHelper.NetworkBehaviour_UsingOnDespawnServerInternal_MethodRef));
                }
                //OnStartClient.
                else if (methodInfo.Name == nameof(NetworkBehaviour.OnStartClient))
                {
                    instructions.Add(processor.Create(OpCodes.Ldarg_0));//this.
                    instructions.Add(processor.Create(OpCodes.Call, CodegenSession.ObjectHelper.NetworkBehaviour_UsingOnStartClientInternal_MethodRef));
                }
                //OnStopClient.
                else if (methodInfo.Name == nameof(NetworkBehaviour.OnStopClient))
                {
                    instructions.Add(processor.Create(OpCodes.Ldarg_0));//this.
                    instructions.Add(processor.Create(OpCodes.Call, CodegenSession.ObjectHelper.NetworkBehaviour_UsingOnStopClientInternal_MethodRef));
                }
                //OnOwnershipClient.
                else if (methodInfo.Name == nameof(NetworkBehaviour.OnOwnershipClient))
                {
                    instructions.Add(processor.Create(OpCodes.Ldarg_0));//this.
                    instructions.Add(processor.Create(OpCodes.Call, CodegenSession.ObjectHelper.NetworkBehaviour_UsingOnOwnershipClientInternal_MethodRef));
                }

                //If instructions count changed then callback was added.
                if (instructions.Count != startCount)
                    allProcessedCallbacks.Add(methodInfo.Name);
            }

            //If instructions are to be added.
            if (instructions.Count > 0)
            {
                processor.InsertFirst(instructions);
                modified = true;
            }

            return modified;
        }

    }
}