using FishNet.CodeGenerating.Helping;
using FishNet.CodeGenerating.ILCore;
using FishNet.CodeGenerating.Processing;
using FishNet.CodeGenerating.Processing.Rpc;
using MonoFN.Cecil;
using System.Collections.Generic;
using System.Linq;
using Unity.CompilationPipeline.Common.Diagnostics;
#if !UNITY_2020_1_OR_NEWER
using UnityEngine;
#endif
using SR = System.Reflection;


namespace FishNet.CodeGenerating
{

    internal class CodegenSession
    {
        /// <summary>
        /// Current module for this session.
        /// </summary>
        internal ModuleDefinition Module;
        /// <summary>
        /// Outputs errors when codegen fails.
        /// </summary>
        internal List<DiagnosticMessage> Diagnostics;
        /// <summary>
        /// SyncVars that are being accessed from an assembly other than the currently being processed one.
        /// </summary>
        internal List<FieldDefinition> DifferentAssemblySyncVars = new List<FieldDefinition>();


        /// <summary>
        /// CodegenBase classes for processing a module.
        /// </summary>
        private List<CodegenBase> _bases;
        /// <summary>
        /// Quick lookup of base classes.
        /// </summary>
        private Dictionary<string, CodegenBase> _basesCache = new Dictionary<string, CodegenBase>();

        /// <summary>
        /// Returns class of type if found within CodegenBase classes.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        internal T GetClass<T>() where T : CodegenBase
        {
            string tName = typeof(T).Name;
            return (T)_basesCache[tName];
        }
        /// <summary>
        /// Resets all helpers while importing any information needed by them.
        /// </summary>
        /// <param name="module"></param>
        /// <returns></returns>
        internal bool Initialize(ModuleDefinition module)
        {
            Module = module;
            Diagnostics = new List<DiagnosticMessage>();

            _bases = new List<CodegenBase>()
                {
                    new ReaderImports(), new ReaderProcessor()
                    ,new WriterImports(), new WriterProcessor()
                    , new PhysicsHelper(), new TimeManagerHelper(), new AttributeHelper(), new GeneralHelper()
                    , new ObjectHelper(), new NetworkBehaviourHelper()
                    , new CreatedSyncVarGenerator(), new TransportHelper()
                    , new NetworkConnectionImports(), new PredictedObjectHelper(), new GeneratorHelper()
                    , new CustomSerializerProcessor()
                    , new NetworkBehaviourProcessor()
                    , new QolAttributeProcessor()
                    , new RpcProcessor()
                    , new NetworkBehaviourSyncProcessor()
                    , new PredictionProcessor()
                };

            //Add all to dictionary first, then import.
            foreach (CodegenBase item in _bases)
            {
                string tName = item.GetType().Name;
                _basesCache.Add(tName, item);
            }

            //Initialize.
            foreach (CodegenBase item in _bases)
            {
                item.Initialize(this);
                if (!item.ImportReferences())
                    return false;
            }

            return true;
        }




        #region Logging.
        /// <summary>
        /// Logs a warning.
        /// </summary>
        /// <param name="msg"></param>
        internal void LogWarning(string msg)
        {
#if UNITY_2020_1_OR_NEWER
            Diagnostics.AddWarning(msg);
#else
            Debug.LogWarning(msg);
#endif
        }
        /// <summary>
        /// Logs an error.
        /// </summary>
        /// <param name="msg"></param>
        internal void LogError(string msg)
        {
#if UNITY_2020_1_OR_NEWER
            Diagnostics.AddError(msg);
#else
            Debug.LogError(msg);
#endif
        }
        #endregion

        #region ImportReference.

        public MethodReference ImportReference(SR.MethodBase method)
        {
            return Module.ImportReference(method);
        }

        public MethodReference ImportReference(SR.MethodBase method, IGenericParameterProvider context)
        {
            return Module.ImportReference(method, context);
        }

        public TypeReference ImportReference(TypeReference type)
        {
            return Module.ImportReference(type);
        }

        public TypeReference ImportReference(TypeReference type, IGenericParameterProvider context)
        {
            return Module.ImportReference(type, context);
        }

        public FieldReference ImportReference(FieldReference field)
        {
            return Module.ImportReference(field);
        }

        public FieldReference ImportReference(FieldReference field, IGenericParameterProvider context)
        {
            return Module.ImportReference(field, context);
        }
        public MethodReference ImportReference(MethodReference method)
        {
            return Module.ImportReference(method);
        }

        public MethodReference ImportReference(MethodReference method, IGenericParameterProvider context)
        {
            return Module.ImportReference(method, context);
        }
        public TypeReference ImportReference(System.Type type)
        {
            return ImportReference(type, null);
        }


        public TypeReference ImportReference(System.Type type, IGenericParameterProvider context)
        {
            return Module.ImportReference(type, context);
        }


        public FieldReference ImportReference(SR.FieldInfo field)
        {
            return Module.ImportReference(field);
        }

        public FieldReference ImportReference(SR.FieldInfo field, IGenericParameterProvider context)
        {
            return Module.ImportReference(field, context);
        }

        #endregion

    }


}