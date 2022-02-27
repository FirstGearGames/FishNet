using FishNet.CodeGenerating.Helping.Extension;
using FishNet.Serializing;
using MonoFN.Cecil;
using MonoFN.Cecil.Cil;
using MonoFN.Cecil.Rocks;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace FishNet.CodeGenerating.Helping
{

    internal class GenericWriterHelper
    {

        #region Reflection references.
        private TypeReference _genericWriterTypeRef;
        private TypeReference _writerTypeRef;
        private MethodReference _writeGetSetMethodRef;
        private MethodReference _writeAutoPackGetSetMethodRef;
        internal TypeReference ActionT2TypeRef;
        internal TypeReference ActionT3TypeRef;
        internal MethodReference ActionT2ConstructorMethodRef;
        internal MethodReference ActionT3ConstructorMethodRef;
        private TypeDefinition _generatedReaderWriterClassTypeDef;
        private MethodDefinition _generatedReaderWriterOnLoadMethodDef;
        private TypeReference _autoPackTypeRef;
        #endregion

        #region Misc.
        /// <summary>
        /// TypeReferences which have already had delegates made for.
        /// </summary>
        private HashSet<TypeReference> _delegatedTypes = new HashSet<TypeReference>();
        #endregion

        #region Const.
        internal const string INITIALIZEONCE_METHOD_NAME = "InitializeOnce";
        internal const MethodAttributes INITIALIZEONCE_METHOD_ATTRIBUTES = MethodAttributes.Static;
        #endregion

        /// <summary>
        /// Imports references needed by this helper.
        /// </summary>
        /// <param name="moduleDef"></param>
        /// <returns></returns>
        internal bool ImportReferences()
        {
            _genericWriterTypeRef = CodegenSession.ImportReference(typeof(GenericWriter<>));
            _writerTypeRef = CodegenSession.ImportReference(typeof(Writer));
            ActionT2TypeRef = CodegenSession.ImportReference(typeof(Action<,>));
            ActionT3TypeRef = CodegenSession.ImportReference(typeof(Action<,,>));
            ActionT2ConstructorMethodRef = CodegenSession.ImportReference(typeof(Action<,>).GetConstructors()[0]);
            ActionT3ConstructorMethodRef = CodegenSession.ImportReference(typeof(Action<,,>).GetConstructors()[0]);

            _autoPackTypeRef = CodegenSession.ImportReference(typeof(AutoPackType));

            System.Reflection.PropertyInfo writePropertyInfo;
            writePropertyInfo = typeof(GenericWriter<>).GetProperty(nameof(GenericWriter<int>.Write));
            _writeGetSetMethodRef = CodegenSession.ImportReference(writePropertyInfo.GetSetMethod());
            writePropertyInfo = typeof(GenericWriter<>).GetProperty(nameof(GenericWriter<int>.WriteAutoPack));
            _writeAutoPackGetSetMethodRef = CodegenSession.ImportReference(writePropertyInfo.GetSetMethod());


            return true;
        }

        /// <summary>
        /// Creates a variant of an instanced write method.
        /// </summary>
        /// <param name="writeMethodRef"></param>
        /// <param name="diagnostics"></param>
        internal void CreateInstancedStaticWrite(MethodReference writeMethodRef)
        {
            if (_generatedReaderWriterClassTypeDef == null)
                _generatedReaderWriterClassTypeDef = CodegenSession.GeneralHelper.GetOrCreateClass(out _, WriterGenerator.GENERATED_TYPE_ATTRIBUTES, WriterGenerator.GENERATED_WRITERS_CLASS_NAME, null);

            MethodDefinition writeMethodDef = writeMethodRef.CachedResolve();
            MethodDefinition createdMethodDef = new MethodDefinition($"Static___{writeMethodRef.Name}",
                (MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.HideBySig),
                _generatedReaderWriterClassTypeDef.Module.TypeSystem.Void);
            _generatedReaderWriterClassTypeDef.Methods.Add(createdMethodDef);

            TypeReference extensionAttributeTypeRef = CodegenSession.ImportReference(typeof(System.Runtime.CompilerServices.ExtensionAttribute));
            MethodDefinition constructor = extensionAttributeTypeRef.GetConstructor();

            MethodReference extensionAttributeConstructorMethodRef = CodegenSession.ImportReference(constructor);
            CustomAttribute extensionCustomAttribute = new CustomAttribute(extensionAttributeConstructorMethodRef);
            createdMethodDef.CustomAttributes.Add(extensionCustomAttribute);

            /* Add parameters to new method. */
            //First add extension.
            ParameterDefinition extensionParameterDef = CodegenSession.GeneralHelper.CreateParameter(createdMethodDef, typeof(PooledWriter), "pooledWriter", ParameterAttributes.None);
            //Then other types.
            ParameterDefinition[] remainingParameterDefs = new ParameterDefinition[writeMethodDef.Parameters.Count];
            for (int i = 0; i < writeMethodDef.Parameters.Count; i++)
            {
                remainingParameterDefs[i] = CodegenSession.GeneralHelper.CreateParameter(createdMethodDef, writeMethodDef.Parameters[i].ParameterType);
                _generatedReaderWriterClassTypeDef.Module.ImportReference(remainingParameterDefs[i].ParameterType.CachedResolve());
            }

            ILProcessor processor = createdMethodDef.Body.GetILProcessor();
            //Load all parameters.
            foreach (ParameterDefinition pd in remainingParameterDefs)
                processor.Emit(OpCodes.Ldarg, pd);
            //Call instanced method.
            processor.Emit(OpCodes.Ldarg, extensionParameterDef);
            processor.Emit(OpCodes.Call, writeMethodRef);
            processor.Emit(OpCodes.Ret);
        }


        /// <summary>
        /// Creates a Write delegate for writeMethodRef and places it within the generated reader/writer constructor.
        /// </summary>
        /// <param name="writeMethodRef"></param>
        internal void CreateWriteDelegate(MethodReference writeMethodRef, bool isStatic)
        {
            /* If class for generated reader/writers isn't known yet.
            * It's possible this is the case if the entry being added
            * now is the first entry. That would mean the class was just
            * generated. */
            bool created;

            if (_generatedReaderWriterClassTypeDef == null)
                _generatedReaderWriterClassTypeDef = CodegenSession.GeneralHelper.GetOrCreateClass(out created, WriterGenerator.GENERATED_TYPE_ATTRIBUTES, WriterGenerator.GENERATED_WRITERS_CLASS_NAME, null);
            /* If constructor isn't set then try to get or create it
             * and also add it to methods if were created. */
            if (_generatedReaderWriterOnLoadMethodDef == null)
            {
                _generatedReaderWriterOnLoadMethodDef = CodegenSession.GeneralHelper.GetOrCreateMethod(_generatedReaderWriterClassTypeDef, out created, INITIALIZEONCE_METHOD_ATTRIBUTES, INITIALIZEONCE_METHOD_NAME, CodegenSession.Module.TypeSystem.Void);
                if (created)
                    CodegenSession.GeneralHelper.CreateRuntimeInitializeOnLoadMethodAttribute(_generatedReaderWriterOnLoadMethodDef);
            }
            //Check if ret already exist, if so remove it; ret will be added on again in this method.
            if (_generatedReaderWriterOnLoadMethodDef.Body.Instructions.Count != 0)
            {
                int lastIndex = (_generatedReaderWriterOnLoadMethodDef.Body.Instructions.Count - 1);
                if (_generatedReaderWriterOnLoadMethodDef.Body.Instructions[lastIndex].OpCode == OpCodes.Ret)
                    _generatedReaderWriterOnLoadMethodDef.Body.Instructions.RemoveAt(lastIndex);
            }

            ILProcessor processor = _generatedReaderWriterOnLoadMethodDef.Body.GetILProcessor();
            TypeReference dataTypeRef;
            //Static methods will have the data type as the second parameter (1). 
            if (isStatic)
                dataTypeRef = writeMethodRef.Parameters[1].ParameterType;
            else
                dataTypeRef = writeMethodRef.Parameters[0].ParameterType;
            //Check if writer already exist.
            if (_delegatedTypes.Contains(dataTypeRef))
            {
                CodegenSession.LogError($"Generic write already created for {dataTypeRef.FullName}.");
                return;
            }
            else
            {
                _delegatedTypes.Add(dataTypeRef);
            }

            /* Create a Action<Writer, T> delegate.
             * May also be Action<Writer, AutoPackType, T> delegate
             * for packed types. */
            processor.Emit(OpCodes.Ldnull);
            processor.Emit(OpCodes.Ldftn, writeMethodRef);

            GenericInstanceType actionGenericInstance;
            MethodReference actionConstructorInstanceMethodRef;
            bool isAutoPacked = CodegenSession.WriterHelper.IsAutoPackedType(dataTypeRef);

            //Generate for auto pack type.
            if (isAutoPacked)
            {
                actionGenericInstance = ActionT3TypeRef.MakeGenericInstanceType(_writerTypeRef, dataTypeRef, _autoPackTypeRef);
                actionConstructorInstanceMethodRef = ActionT3ConstructorMethodRef.MakeHostInstanceGeneric(actionGenericInstance);
            }
            //Generate for normal type.
            else
            {
                actionGenericInstance = ActionT2TypeRef.MakeGenericInstanceType(_writerTypeRef, dataTypeRef);
                actionConstructorInstanceMethodRef = ActionT2ConstructorMethodRef.MakeHostInstanceGeneric(actionGenericInstance);
            }

            processor.Emit(OpCodes.Newobj, actionConstructorInstanceMethodRef);
            //Call delegate to GenericWriter<T>.Write
            GenericInstanceType genericInstance = _genericWriterTypeRef.MakeGenericInstanceType(dataTypeRef);
            MethodReference genericrWriteMethodRef = (isAutoPacked) ?
                _writeAutoPackGetSetMethodRef.MakeHostInstanceGeneric(genericInstance) :
                _writeGetSetMethodRef.MakeHostInstanceGeneric(genericInstance);
            processor.Emit(OpCodes.Call, genericrWriteMethodRef);

            processor.Emit(OpCodes.Ret);
        }


    }
}