using FishNet.CodeGenerating.Processing;
using Mono.Cecil;
using System.Collections.Generic;
using Unity.CompilationPipeline.Common.Diagnostics;
using UnityEngine;

namespace FishNet.CodeGenerating.Helping
{

    internal static class CodegenSession
    {
        [System.ThreadStatic]
        internal static ModuleDefinition Module;
        [System.ThreadStatic]
        internal static List<DiagnosticMessage> Diagnostics;

        [System.ThreadStatic]
        internal static AttributeHelper AttributeHelper;
        [System.ThreadStatic]
        internal static ConnectionHelper ConnectionHelper;
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
        internal static SyncHandlerGenerator SyncHandlerGenerator;
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
        internal static NetworkBehaviourRpcProcessor NetworkBehaviourRpcProcessor;
        [System.ThreadStatic]
        internal static NetworkBehaviourSyncProcessor NetworkBehaviourSyncProcessor;

        /// <summary>
        /// True to ignore future warnings.
        /// </summary>
        private static bool _ignoreNextWarning = false;

        /// <summary>
        /// Logs a warning.
        /// </summary>
        /// <param name="msg"></param>
        internal static void LogWarning(string msg, bool ignoreNextWarning = false)
        {
            if (!_ignoreNextWarning || !ignoreNextWarning) 
            {
#if UNITY_2020_1_OR_NEWER
            Diagnostics.AddWarning(msg);
#else
                Debug.LogWarning(msg);
#endif
            }
            _ignoreNextWarning = ignoreNextWarning;
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

            AttributeHelper = new AttributeHelper();
            ConnectionHelper = new ConnectionHelper();
            GeneralHelper = new GeneralHelper();
            GenericReaderHelper = new GenericReaderHelper();
            GenericWriterHelper = new GenericWriterHelper();
            ObjectHelper = new ObjectHelper();
            ReaderGenerator = new ReaderGenerator();
            ReaderHelper = new ReaderHelper();
            SyncHandlerGenerator = new SyncHandlerGenerator();
            TransportHelper = new TransportHelper();
            WriterGenerator = new WriterGenerator();
            WriterHelper = new WriterHelper();

            CustomSerializerProcessor = new CustomSerializerProcessor();
            NetworkBehaviourProcessor = new NetworkBehaviourProcessor();
            QolAttributeProcessor = new QolAttributeProcessor();
            NetworkBehaviourRpcProcessor = new NetworkBehaviourRpcProcessor();
            NetworkBehaviourSyncProcessor = new NetworkBehaviourSyncProcessor();

            if (!CodegenSession.GeneralHelper.ImportReferences())
                return false;
            if (!CodegenSession.AttributeHelper.ImportReferences())
                return false;
            if (!ConnectionHelper.ImportReferences())
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
            if (!SyncHandlerGenerator.ImportReferences())
                return false;
            if (!TransportHelper.ImportReferences())
                return false;
            if (!WriterGenerator.ImportReferences())
                return false;
            if (!WriterHelper.ImportReferences())
                return false;

            return true;
        }

    }


}