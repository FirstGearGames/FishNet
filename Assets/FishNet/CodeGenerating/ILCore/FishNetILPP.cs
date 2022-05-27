using FishNet.Broadcast;
using FishNet.CodeGenerating.Helping;
using FishNet.CodeGenerating.Helping.Extension;
using FishNet.CodeGenerating.Processing;
using FishNet.Serializing.Helping;
using MonoFN.Cecil;
using MonoFN.Cecil.Cil;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Unity.CompilationPipeline.Common.ILPostProcessing;

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

            if (IsFishNetAssembly(compiledAssembly))
            {
                //Not used...
                modified |= ModifyMakePublicMethods();
            }
            else
            {
                /* If one or more scripts use RPCs but don't inherit NetworkBehaviours
                 * then don't bother processing the rest. */
                if (CodegenSession.NetworkBehaviourProcessor.NonNetworkBehaviourHasInvalidAttributes(CodegenSession.Module.Types))
                    return new ILPostProcessResult(null, CodegenSession.Diagnostics);
                //before 226ms, after 17ms                   
                modified |= CreateDeclaredDelegates();
                //before 5ms, after 5ms
                modified |= CreateDeclaredSerializers();
                //before 30ms, after 26ms
                modified |= CreateIBroadcast();
                //before 140ms, after 10ms
                modified |= CreateQOLAttributes();
                //before 75ms, after 6ms
                modified |= CreateNetworkBehaviours();
                //before 260ms, after 215ms 
                modified |= CreateGenericReadWriteDelegates();
                //before 52ms, after 27ms

                //Total at once
                //before 761, after 236ms

                /* If there are warnings about SyncVars being in different assemblies.
                 * This is awful ... codegen would need to be reworked to save
                 * syncvars across all assemblies so that scripts referencing them from
                 * another assembly can have it's instructions changed. This however is an immense
                 * amount of work so it will have to be put on hold, for... a long.. long while. */
                if (CodegenSession.DifferentAssemblySyncVars.Count > 0)
                {
                    StringBuilder sb = new StringBuilder();
                    sb.AppendLine($"Assembly {CodegenSession.Module.Name} has inherited access to SyncVars in different assemblies. When accessing SyncVars across assemblies be sure to use Get/Set methods withinin the inherited assembly script to change SyncVars. Accessible fields are:");

                    //foreach ((TypeDefinition td, FieldDefinition fd) item in CodegenSession.DifferentAssemblySyncVars)
                    //{ 
                    // //   sb.AppendLine($"Field {item.fd.Name} within {item.fd.DeclaringType.FullName} in assembly {item.fd.Module.Name} is accessible.");
                    //}
                    foreach (FieldDefinition item in CodegenSession.DifferentAssemblySyncVars)
                        sb.AppendLine($"   - Field {item.Name} within {item.DeclaringType.FullName} in assembly {item.Module.Name}.");

                    CodegenSession.LogWarning("v------- IMPORTANT -------v");
                    CodegenSession.LogWarning(sb.ToString());
                    CodegenSession.DifferentAssemblySyncVars.Clear();
                }
            }

            //CodegenSession.LogWarning($"Assembly {compiledAssembly.Name} took {stopwatch.ElapsedMilliseconds}.");

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
        /// Makees methods public scope which use CodegenMakePublic attribute.
        /// </summary>
        /// <returns></returns>
        private bool ModifyMakePublicMethods()
        {
            string makePublicTypeFullName = typeof(CodegenMakePublicAttribute).FullName;
            foreach (TypeDefinition td in CodegenSession.Module.Types)
            {
                foreach (MethodDefinition md in td.Methods)
                {
                    foreach (CustomAttribute ca in md.CustomAttributes)
                    {
                        if (ca.AttributeType.FullName == makePublicTypeFullName)
                        {
                            md.Attributes &= ~MethodAttributes.Assembly;
                            md.Attributes |= MethodAttributes.Public;
                        }
                    }
                }
            }

            //There is always at least one modified.
            return true;
        }
        /// <summary>
        /// Creates delegates for user declared serializers.
        /// </summary>
        public bool CreateDeclaredDelegates()
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

            string networkBehaviourFullName = CodegenSession.ObjectHelper.NetworkBehaviour_FullName;

            HashSet<TypeDefinition> typeDefs = new HashSet<TypeDefinition>();
            foreach (TypeDefinition td in CodegenSession.Module.Types)
            {
                TypeDefinition climbTd = td;
                do
                {
                    //Reached NetworkBehaviour class.
                    if (climbTd.FullName == networkBehaviourFullName)
                        break;

                    ///* Check initial class as well all types within
                    // * the class. Then check all of it's base classes. */
                    if (climbTd.ImplementsInterface<IBroadcast>())
                        typeDefs.Add(climbTd);
                    //7ms

                    //Add nested. Only going to go a single layer deep.
                    foreach (TypeDefinition nestedTypeDef in td.NestedTypes)
                    {
                        if (nestedTypeDef.ImplementsInterface<IBroadcast>())
                            typeDefs.Add(nestedTypeDef);
                    }
                    //0ms

                    climbTd = climbTd.GetNextBaseClass();
                    //this + name check 40ms
                } while (climbTd != null);

            }


            //Create reader/writers for found typeDefs.
            foreach (TypeDefinition td in typeDefs)
            {
                TypeReference typeRef = CodegenSession.ImportReference(td);

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

            bool codeStripping = false;
            
            List<TypeDefinition> allTypeDefs = CodegenSession.Module.Types.ToList();

            /* First pass, potentially only pass.
             * If code stripping them this will be run again. The first iteration
             * is to ensure things are removed in the proper order. */
            foreach (TypeDefinition td in allTypeDefs)
            {
                if (CodegenSession.GeneralHelper.IgnoreTypeDefinition(td))
                    continue;

                modified |= CodegenSession.QolAttributeProcessor.Process(td, codeStripping);
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

            /* Remove types which are inherited. This gets the child most networkbehaviours.
             * Since processing iterates all parent classes there's no reason to include them */
            RemoveInheritedTypeDefinitions(networkBehaviourTypeDefs);
            //Set how many rpcs are in children classes for each typedef.
            Dictionary<TypeDefinition, uint> inheritedRpcCounts = new Dictionary<TypeDefinition, uint>();
            SetChildRpcCounts(inheritedRpcCounts, networkBehaviourTypeDefs);
            //Set how many synctypes are in children classes for each typedef.
            Dictionary<TypeDefinition, uint> inheritedSyncTypeCounts = new Dictionary<TypeDefinition, uint>();
            SetChildSyncTypeCounts(inheritedSyncTypeCounts, networkBehaviourTypeDefs);

            /* This holds all sync types created, synclist, dictionary, var
             * and so on. This data is used after all syncvars are made so
             * other methods can look for references to created synctypes and
             * replace accessors accordingly. */
            List<(SyncType, ProcessedSync)> allProcessedSyncs = new List<(SyncType, ProcessedSync)>();
            HashSet<string> allProcessedCallbacks = new HashSet<string>();
            List<TypeDefinition> processedClasses = new List<TypeDefinition>();

            foreach (TypeDefinition typeDef in networkBehaviourTypeDefs)
            {
                CodegenSession.ImportReference(typeDef);
                //Synctypes processed for this nb and it's inherited classes.
                List<(SyncType, ProcessedSync)> processedSyncs = new List<(SyncType, ProcessedSync)>();
                CodegenSession.NetworkBehaviourProcessor.Process(typeDef, processedSyncs,
                    inheritedSyncTypeCounts, inheritedRpcCounts);
                //Add to all processed.
                allProcessedSyncs.AddRange(processedSyncs);
            }

            /* Must run through all scripts should user change syncvar
             * from outside the networkbehaviour. */
            if (allProcessedSyncs.Count > 0)
            {
                foreach (TypeDefinition td in CodegenSession.Module.Types)
                {
                    CodegenSession.NetworkBehaviourSyncProcessor.ReplaceGetSets(td, allProcessedSyncs);
                    CodegenSession.RpcProcessor.RedirectBaseCalls();
                }
            }

            /* Removes typedefinitions which are inherited by
             * another within tds. For example, if the collection
             * td contains A, B, C and our structure is
             * A : B : C then B and C will be removed from the collection
             *  Since they are both inherited by A. */
            void RemoveInheritedTypeDefinitions(List<TypeDefinition> tds)
            {
                HashSet<TypeDefinition> inheritedTds = new HashSet<TypeDefinition>();
                /* Remove any networkbehaviour typedefs which are inherited by
                 * another networkbehaviour typedef. When a networkbehaviour typedef
                 * is processed so are all of the inherited types. */
                for (int i = 0; i < tds.Count; i++)
                {
                    /* Iterates all base types and
                     * adds them to inheritedTds so long
                     * as the base type is not a NetworkBehaviour. */
                    TypeDefinition copyTd = tds[i].GetNextBaseClass();
                    while (copyTd != null)
                    {
                        //Class is NB.
                        if (copyTd.FullName == CodegenSession.ObjectHelper.NetworkBehaviour_FullName)
                            break;

                        inheritedTds.Add(copyTd);
                        copyTd = copyTd.GetNextBaseClass();
                    }
                }

                //Remove all inherited types.
                foreach (TypeDefinition item in inheritedTds)
                    tds.Remove(item);
            }

            /* Sets how many Rpcs are within the children
             * of each typedefinition. EG: if our structure is
             * A : B : C, with the following RPC counts...
             * A 3
             * B 1
             * C 2
             * then B child rpc counts will be 3, and C will be 4. */
            void SetChildRpcCounts(Dictionary<TypeDefinition, uint> typeDefCounts, List<TypeDefinition> tds)
            {
                foreach (TypeDefinition typeDef in tds)
                {
                    //Number of RPCs found while climbing typeDef.
                    uint childCount = 0;

                    TypeDefinition copyTd = typeDef;
                    do
                    {
                        //How many RPCs are in copyTd.
                        uint copyCount = CodegenSession.RpcProcessor.GetRpcCount(copyTd);

                        /* If not found it this is the first time being
                         * processed. When this occurs set the value
                         * to 0. It will be overwritten below if baseCount
                         * is higher. */
                        uint previousCopyChildCount = 0;
                        if (!typeDefCounts.TryGetValue(copyTd, out previousCopyChildCount))
                            typeDefCounts[copyTd] = 0;
                        /* If baseCount is higher then replace count for copyTd.
                         * This can occur when a class is inherited by several types
                         * and the first processed type might only have 1 rpc, while
                         * the next has 2. This could be better optimized but to keep
                         * the code easier to read, it will stay like this. */
                        if (childCount > previousCopyChildCount)
                            typeDefCounts[copyTd] = childCount;

                        //Increase baseCount with RPCs found here.
                        childCount += copyCount;

                        copyTd = copyTd.GetNextBaseClassToProcess();
                    } while (copyTd != null);
                }

            }


            /* This performs the same functionality as SetChildRpcCounts
             * but for SyncTypes. */
            void SetChildSyncTypeCounts(Dictionary<TypeDefinition, uint> typeDefCounts, List<TypeDefinition> tds)
            {
                foreach (TypeDefinition typeDef in tds)
                {
                    //Number of RPCs found while climbing typeDef.
                    uint childCount = 0;

                    TypeDefinition copyTd = typeDef;
                    do
                    {
                        //How many RPCs are in copyTd.
                        uint copyCount = CodegenSession.NetworkBehaviourSyncProcessor.GetSyncTypeCount(copyTd);

                        /* If not found it this is the first time being
                         * processed. When this occurs set the value
                         * to 0. It will be overwritten below if baseCount
                         * is higher. */
                        uint previousCopyChildCount = 0;
                        if (!typeDefCounts.TryGetValue(copyTd, out previousCopyChildCount))
                            typeDefCounts[copyTd] = 0;
                        /* If baseCount is higher then replace count for copyTd.
                         * This can occur when a class is inherited by several types
                         * and the first processed type might only have 1 rpc, while
                         * the next has 2. This could be better optimized but to keep
                         * the code easier to read, it will stay like this. */
                        if (childCount > previousCopyChildCount)
                            typeDefCounts[copyTd] = childCount;

                        //Increase baseCount with RPCs found here.
                        childCount += copyCount;

                        copyTd = copyTd.GetNextBaseClassToProcess();
                    } while (copyTd != null);
                }
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