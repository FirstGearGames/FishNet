using System.IO;
using System.Linq;
using System.Collections.Generic;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Unity.CompilationPipeline.Common.ILPostProcessing;
using FishNet.CodeGenerating.Helping;
using FishNet.CodeGenerating.Processing;
using FishNet.CodeGenerating.Helping.Extension;
using FishNet.Broadcast;
using UnityEngine;

namespace FishNet.CodeGenerating.ILCore
{
    public class FishNetILPP : ILPostProcessor
    {
        #region Const.
        internal const string RUNTIME_ASSEMBLY_NAME = "FishNet.Runtime";
        /// <summary>
        /// If not empty codegen will only include types within this Namespace while iterating RUNTIME_ASSEMBLY_NAME>
        /// </summary>
        //internal const string CODEGEN_THIS_NAMESPACE = "FishNet.Managing.Scened";
        internal const string CODEGEN_THIS_NAMESPACE = "";
        #endregion

        public override bool WillProcess(ICompiledAssembly compiledAssembly)
        {
            if (compiledAssembly.Name.StartsWith("Unity."))
                return false;
            if (compiledAssembly.Name.StartsWith("UnityEngine."))
                return false;
            if (compiledAssembly.Name.StartsWith("UnityEditor."))
                return false;
            if (compiledAssembly.Name.Contains("Editor"))
                return false;

            /* This line contradicts the one below where referencesFishNet
             * becomes true if the assembly is FishNetAssembly. This is here
             * intentionally to stop codegen from running on the runtime
             * fishnet assembly, but the option below is for debugging. I would
             * comment out this check if I wanted to compile fishnet runtime. */
            if (CODEGEN_THIS_NAMESPACE.Length == 0)
            {
                if (compiledAssembly.Name == RUNTIME_ASSEMBLY_NAME)
                    return false;
            }
            bool referencesFishNet = FishNetILPP.IsFishNetAssembly(compiledAssembly) || compiledAssembly.References.Any(filePath => Path.GetFileNameWithoutExtension(filePath) == RUNTIME_ASSEMBLY_NAME);
            return referencesFishNet;
        }
        public override ILPostProcessor GetInstance() => this;
        public override ILPostProcessResult Process(ICompiledAssembly compiledAssembly)
        {
            AssemblyDefinition assemblyDef = ILCoreHelper.GetAssemblyDefinition(compiledAssembly);
            if (assemblyDef == null)
                return null;
            //Check WillProcess again; somehow certain editor scripts skip the WillProcess check.
            if (!WillProcess(compiledAssembly))
                return null;
            //Resets instances of helpers and populates data needed by all helpers.
            if (!CodegenSession.Reset(assemblyDef.MainModule))
                return null;

            bool modified = false;

            /* If one or more scripts use RPCs but don't inherit NetworkBehaviours
             * then don't bother processing the rest. */
            if (CodegenSession.NetworkBehaviourProcessor.NonNetworkBehaviourHasInvalidAttributes(CodegenSession.Module.Types))
                return new ILPostProcessResult(null, CodegenSession.Diagnostics);

            modified |= CreateDeclaredDelegates();
            modified |= CreateDeclaredSerializers();
            modified |= CreateIBroadcast();
            modified |= CreateQOLAttributes();
            modified |= CreateNetworkBehaviours();
            modified |= CreateGenericReadWriteDelegates();

            if (!modified)
            {
                return null;
            }
            else
            {
                MemoryStream pe = new MemoryStream();
                MemoryStream pdb = new MemoryStream();
                WriterParameters writerParameters = new WriterParameters
                {
                    SymbolWriterProvider = new PortablePdbWriterProvider(),
                    SymbolStream = pdb,
                    WriteSymbols = true
                };
                assemblyDef.Write(pe, writerParameters);
                return new ILPostProcessResult(new InMemoryAssembly(pe.ToArray(), pdb.ToArray()), CodegenSession.Diagnostics);
            }
        }

        /// <summary>
        /// Creates delegates for user declared serializers.
        /// </summary>
        /// <param name="moduleDef"></param>
        /// <param name="diagnostics"></param>
        private bool CreateDeclaredDelegates()
        {
            bool modified = false;

            TypeAttributes readWriteExtensionTypeAttr = (TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.Abstract);
            List<TypeDefinition> allTypeDefs = CodegenSession.Module.Types.ToList();
            foreach (TypeDefinition td in allTypeDefs)
            {
                if (CodegenSession.GeneralHelper.IgnoreTypeDefinition(td))
                    continue;

                if (td.Attributes.HasFlag(readWriteExtensionTypeAttr))
                    modified |= CodegenSession.CustomSerializerProcessor.CreateDelegates(td);
            }

            return modified;
        }

        /// <summary>
        /// Creates serializers for custom types within user declared serializers.
        /// </summary>
        /// <param name="moduleDef"></param>
        /// <param name="diagnostics"></param>
        private bool CreateDeclaredSerializers()
        {
            bool modified = false;

            TypeAttributes readWriteExtensionTypeAttr = (TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.Abstract);
            List<TypeDefinition> allTypeDefs = CodegenSession.Module.Types.ToList();
            foreach (TypeDefinition td in allTypeDefs)
            {
                if (CodegenSession.GeneralHelper.IgnoreTypeDefinition(td))
                    continue;

                if (td.Attributes.HasFlag(readWriteExtensionTypeAttr))
                    modified |= CodegenSession.CustomSerializerProcessor.CreateSerializers(td);
            }

            return modified;
        }

        /// <summary>
        /// Creaters serializers and calls for IBroadcast.
        /// </summary>
        /// <param name="moduleDef"></param>
        /// <param name="diagnostics"></param>
        private bool CreateIBroadcast()
        {
            bool modified = false;

            HashSet<TypeDefinition> typeDefs = new HashSet<TypeDefinition>();
            foreach (TypeDefinition td in CodegenSession.Module.Types)
            {
                TypeDefinition climbTypeDef = td;
                while (climbTypeDef != null)
                {
                    /* Check initial class as well all types within
                     * the class. Then check all of it's base classes. */
                    if (climbTypeDef.ImplementsInterface<IBroadcast>())
                        typeDefs.Add(climbTypeDef);

                    //Add nested. Only going to go a single layer deep.
                    foreach (TypeDefinition nestedTypeDef in td.NestedTypes)
                    {
                        if (nestedTypeDef.ImplementsInterface<IBroadcast>())
                            typeDefs.Add(nestedTypeDef);
                    }

                    //Climb up base classes.
                    if (climbTypeDef.BaseType != null)
                        climbTypeDef = climbTypeDef.BaseType.Resolve();
                    else
                        climbTypeDef = null;
                }
            }

            //Create reader/writers for found typeDefs.
            foreach (TypeDefinition td in typeDefs)
            {
                TypeReference typeRef = CodegenSession.Module.ImportReference(td);

                bool canSerialize = CodegenSession.GeneralHelper.HasSerializerAndDeserializer(typeRef, true);
                if (!canSerialize)
                    CodegenSession.LogError($"Broadcast {td.Name} does not support serialization. Use a supported type or create a custom serializer.");
                else
                    modified = true;
            }

            return modified;
        }

        /// <summary>
        /// Handles QOLAttributes such as [Server].
        /// </summary>
        /// <returns></returns>
        private bool CreateQOLAttributes()
        {
            bool modified = false;

            List<TypeDefinition> allTypeDefs = CodegenSession.Module.Types.ToList();
            foreach (TypeDefinition td in allTypeDefs)
            {
                if (CodegenSession.GeneralHelper.IgnoreTypeDefinition(td))
                    continue;

                modified |= CodegenSession.QolAttributeProcessor.Process(td);
            }


            return modified;
        }

        /// <summary>
        /// Creates NetworkBehaviour changes.
        /// </summary>
        /// <param name="moduleDef"></param>
        /// <param name="diagnostics"></param>
        private bool CreateNetworkBehaviours()
        {
            bool modified = false;
            //Get all network behaviours to process.
            List<TypeDefinition> networkBehaviourTypeDefs = CodegenSession.Module.Types
                .Where(td => td.IsSubclassOf(CodegenSession.ObjectHelper.NetworkBehaviour_FullName))
                .ToList();

            //Moment a NetworkBehaviour exist the assembly is considered modified.
            if (networkBehaviourTypeDefs.Count > 0)
                modified = true;

            /* Remove any networkbehaviour typedefs which are inherited by
             * another networkbehaviour typedef. When a networkbehaviour typedef
             * is processed so are all of the inherited types. */
            for (int i = 0; i < networkBehaviourTypeDefs.Count; i++)
            {
                int entriesRemoved = 0;

                List<TypeDefinition> tdSubClasses = new List<TypeDefinition>();
                TypeDefinition tdClimb = networkBehaviourTypeDefs[i].BaseType.Resolve();
                while (tdClimb != null)
                {
                    tdSubClasses.Add(tdClimb);
                    if (tdClimb.CanProcessBaseType())
                        tdClimb = tdClimb.BaseType.Resolve();
                    else
                        tdClimb = null;
                }
                //No base types to compare.
                if (tdSubClasses.Count == 0)
                    continue;
                //Try to remove every subclass.
                foreach (TypeDefinition tdSub in tdSubClasses)
                {
                    if (networkBehaviourTypeDefs.Remove(tdSub))
                        entriesRemoved++;
                }
                //Subtract entries removed from i since theyre now gone.
                i -= entriesRemoved;
            }

            /* This needs to persist because it holds SyncHandler
             * references for each SyncType. Those
             * SyncHandlers are re-used if there are multiple SyncTypes
             * using the same Type. It's also used to replace references
             * to syncvars with the respected accessors. */
            List<(SyncType, ProcessedSync)> allProcessedSyncs = new List<(SyncType, ProcessedSync)>();
            HashSet<string> allProcessedCallbacks = new HashSet<string>();
            List<TypeDefinition> processedClasses = new List<TypeDefinition>();

            foreach (TypeDefinition typeDef in networkBehaviourTypeDefs)
            {
                CodegenSession.Module.ImportReference(typeDef);
                //RPCs are per networkbehaviour + hierarchy and need to be reset.
                int allRpcCount = 0;
                //Callbacks are per networkbehaviour + hierarchy as well.
                allProcessedCallbacks.Clear();
                //modified |= CodegenSession.NetworkBehaviourProcessor.Process(typeDef, ref allRpcCount, allProcessedSyncs, allProcessedCallbacks);
                CodegenSession.NetworkBehaviourProcessor.Process(typeDef, ref allRpcCount, allProcessedSyncs, allProcessedCallbacks);
            }

            //Run through the typeDefs again to replace syncvar calls.
            foreach (TypeDefinition typeDef in networkBehaviourTypeDefs)
            {
                //Add to processed.
                TypeDefinition copyTypeDef = typeDef;
                do
                {
                    CodegenSession.NetworkBehaviourSyncProcessor.ReplaceGetSets(copyTypeDef, allProcessedSyncs);
                    copyTypeDef = TypeDefinitionExtensions.GetNextBaseClassToProcess(copyTypeDef);
                } while (copyTypeDef != null);
            }

            return modified;
        }


        /// <summary>
        /// Creates generic delegates for all read and write methods.
        /// </summary>
        /// <param name="moduleDef"></param>
        /// <param name="diagnostics"></param>
        private bool CreateGenericReadWriteDelegates()
        {
            bool modified = false;

            modified |= CodegenSession.WriterHelper.CreateGenericDelegates();
            modified |= CodegenSession.ReaderHelper.CreateGenericDelegates();

            return modified;
        }

        internal static bool IsFishNetAssembly(ICompiledAssembly assembly) => (assembly.Name == FishNetILPP.RUNTIME_ASSEMBLY_NAME);
        internal static bool IsFishNetAssembly() => (CodegenSession.Module.Assembly.Name.Name == FishNetILPP.RUNTIME_ASSEMBLY_NAME);
        internal static bool IsFishNetAssembly(ModuleDefinition moduleDef) => (moduleDef.Assembly.Name.Name == FishNetILPP.RUNTIME_ASSEMBLY_NAME);

    }
}