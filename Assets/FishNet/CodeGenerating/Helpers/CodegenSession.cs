using FishNet.CodeGenerating.Processing;
using FishNet.CodeGenerating.Processing.Rpc;
using MonoFN.Cecil;
using System.Collections.Generic;
using Unity.CompilationPipeline.Common.Diagnostics;
using UnityEngine;
using SR = System.Reflection;

namespace FishNet.CodeGenerating.Helping
{

    internal static class CodegenSession
    {
        [System.ThreadStatic]
        internal static ModuleDefinition Module;
        [System.ThreadStatic]
        internal static List<DiagnosticMessage> Diagnostics;

        [System.ThreadStatic]
        internal static TimeManagerHelper TimeManagerHelper;
        [System.ThreadStatic]
        internal static AttributeHelper AttributeHelper;
        [System.ThreadStatic]
        internal static GeneralHelper GeneralHelper;
        [System.ThreadStatic]
        internal static GenericReaderHelper GenericReaderHelper;
        [System.ThreadStatic]
        internal static GenericWriterHelper GenericWriterHelper;
        [System.ThreadStatic]
        internal static ObjectHelper ObjectHelper;
        [System.ThreadStatic]
        internal static ReaderGenerator ReaderGenerator;
        [System.ThreadStatic]
        internal static ReaderHelper ReaderHelper;
        [System.ThreadStatic]
        internal static CreatedSyncVarGenerator CreatedSyncVarGenerator;
        [System.ThreadStatic]
        internal static TransportHelper TransportHelper;
        [System.ThreadStatic]
        internal static WriterGenerator WriterGenerator;
        [System.ThreadStatic]
        internal static WriterHelper WriterHelper;
        [System.ThreadStatic]
        internal static CustomSerializerProcessor CustomSerializerProcessor;
        [System.ThreadStatic]
        internal static NetworkBehaviourProcessor NetworkBehaviourProcessor;
        [System.ThreadStatic]
        internal static QolAttributeProcessor QolAttributeProcessor;
        [System.ThreadStatic]
        internal static RpcProcessor RpcProcessor;
        [System.ThreadStatic]
        internal static NetworkBehaviourSyncProcessor NetworkBehaviourSyncProcessor;
        [System.ThreadStatic]
        internal static NetworkBehaviourPredictionProcessor NetworkBehaviourPredictionProcessor;
        /// <summary>
        /// SyncVars that are being accessed from an assembly other than the currently being processed one.
        /// </summary>
        [System.ThreadStatic]
        internal static List<FieldDefinition> DifferentAssemblySyncVars;

        /// <summary>
        /// Logs a warning.
        /// </summary>
        /// <param name="msg"></param>
        internal static void LogWarning(string msg)
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
        internal static void LogError(string msg)
        {
#if UNITY_2020_1_OR_NEWER
            Diagnostics.AddError(msg);
#else
            Debug.LogError(msg);
#endif
        }
        /// <summary>
        /// Resets all helpers while importing any information needed by them.
        /// </summary>
        /// <param name="module"></param>
        /// <returns></returns>
        internal static bool Reset(ModuleDefinition module)
        {
            Module = module;
            Diagnostics = new List<DiagnosticMessage>();

            TimeManagerHelper = new TimeManagerHelper();
            AttributeHelper = new AttributeHelper();
            GeneralHelper = new GeneralHelper();
            GenericReaderHelper = new GenericReaderHelper();
            GenericWriterHelper = new GenericWriterHelper();
            ObjectHelper = new ObjectHelper();
            ReaderGenerator = new ReaderGenerator();
            ReaderHelper = new ReaderHelper();
            CreatedSyncVarGenerator = new CreatedSyncVarGenerator();
            TransportHelper = new TransportHelper();
            WriterGenerator = new WriterGenerator();
            WriterHelper = new WriterHelper();

            CustomSerializerProcessor = new CustomSerializerProcessor();
            NetworkBehaviourProcessor = new NetworkBehaviourProcessor();
            QolAttributeProcessor = new QolAttributeProcessor();
            RpcProcessor = new RpcProcessor();
            NetworkBehaviourSyncProcessor = new NetworkBehaviourSyncProcessor();
            NetworkBehaviourPredictionProcessor = new NetworkBehaviourPredictionProcessor();
            DifferentAssemblySyncVars = new List<FieldDefinition>();

            if (!TimeManagerHelper.ImportReferences())
                return false;
            if (!NetworkBehaviourPredictionProcessor.ImportReferences())
                return false;
            if (!NetworkBehaviourSyncProcessor.ImportReferences())
                return false;
            if (!GeneralHelper.ImportReferences())
                return false;
            if (!AttributeHelper.ImportReferences())
                return false;
            if (!GenericReaderHelper.ImportReferences())
                return false;
            if (!GenericWriterHelper.ImportReferences())
                return false;
            if (!ObjectHelper.ImportReferences())
                return false;
            if (!ReaderGenerator.ImportReferences())
                return false;
            if (!ReaderHelper.ImportReferences())
                return false;
            if (!CreatedSyncVarGenerator.ImportReferences())
                return false;
            if (!TransportHelper.ImportReferences())
                return false;
            if (!WriterGenerator.ImportReferences())
                return false;
            if (!WriterHelper.ImportReferences())
                return false;

            return true;
        }



        #region ImportReference.

        public static MethodReference ImportReference(SR.MethodBase method)
        {
            return Module.ImportReference(method);
        }

        public static MethodReference ImportReference(SR.MethodBase method, IGenericParameterProvider context)
        {
            return Module.ImportReference(method, context);
        }

        public static TypeReference ImportReference(TypeReference type)
        {
            return Module.ImportReference(type);
        }

        public static TypeReference ImportReference(TypeReference type, IGenericParameterProvider context)
        {
            return Module.ImportReference(type, context);
        }

        public static FieldReference ImportReference(FieldReference field)
        {
            return Module.ImportReference(field);
        }

        public static FieldReference ImportReference(FieldReference field, IGenericParameterProvider context)
        {
            return Module.ImportReference(field, context);
        }
        public static MethodReference ImportReference(MethodReference method)
        {
            return Module.ImportReference(method);
        }

        public static MethodReference ImportReference(MethodReference method, IGenericParameterProvider context)
        {
            return Module.ImportReference(method, context);
        }
        public static TypeReference ImportReference(System.Type type)
        {
            return ImportReference(type, null);
        }


        public static TypeReference ImportReference(System.Type type, IGenericParameterProvider context)
        {
            return Module.ImportReference(type, context);
        }


        public static FieldReference ImportReference(SR.FieldInfo field)
        {
            return Module.ImportReference(field);
        }

        public static FieldReference ImportReference(SR.FieldInfo field, IGenericParameterProvider context)
        {
            return Module.ImportReference(field, context);
        }

        #endregion
    }


}