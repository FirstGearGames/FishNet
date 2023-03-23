﻿using FishNet.CodeGenerating.Extension;
using FishNet.CodeGenerating.Helping.Extension;
using FishNet.CodeGenerating.ILCore;
using FishNet.Object;
using FishNet.Serializing;
using FishNet.Utility.Performance;
using MonoFN.Cecil;
using MonoFN.Cecil.Cil;
using MonoFN.Cecil.Rocks;
using System;
using System.Collections.Generic;
using SR = System.Reflection;
using UnityDebug = UnityEngine.Debug;

namespace FishNet.CodeGenerating.Helping
{

    internal class WriterProcessor : CodegenBase
    {
        #region Reflection references.
        public readonly Dictionary<string, MethodReference> InstancedWriterMethods = new Dictionary<string, MethodReference>();
        public readonly Dictionary<string, MethodReference> StaticWriterMethods = new Dictionary<string, MethodReference>();
        public HashSet<TypeReference> AutoPackedMethods = new HashSet<TypeReference>(new TypeReferenceComparer());

        public TypeDefinition GeneratedWriterClassTypeDef;
        public MethodDefinition GeneratedWriterOnLoadMethodDef;
        #endregion

        #region Misc.
        /// <summary>
        /// TypeReferences which have already had delegates made for.
        /// </summary>
        private HashSet<TypeReference> _delegatedTypes = new HashSet<TypeReference>();
        #endregion

        #region Const.
        /// <summary>
        /// Namespace to use for generated serializers and delegates.
        /// </summary>
        public const string GENERATED_WRITER_NAMESPACE = "FishNet.Serializing.Generated";
        /// <summary>
        /// Name to use for generated serializers class.
        /// </summary>
        public const string GENERATED_WRITERS_CLASS_NAME = "GeneratedWriters___Internal";
        /// <summary>
        /// Attributes to use for generated serializers class.
        /// </summary>
        public const TypeAttributes GENERATED_TYPE_ATTRIBUTES = (TypeAttributes.BeforeFieldInit | TypeAttributes.Class | TypeAttributes.AnsiClass |
            TypeAttributes.Public | TypeAttributes.AutoClass | TypeAttributes.Abstract | TypeAttributes.Sealed);
        /// <summary>
        /// Name to use for InitializeOnce method.
        /// </summary>
        public const string INITIALIZEONCE_METHOD_NAME = "InitializeOnce";
        /// <summary>
        /// Attributes to use for InitializeOnce method within generated serializer classes.
        /// </summary>
        public const MethodAttributes INITIALIZEONCE_METHOD_ATTRIBUTES = (MethodAttributes.Static | MethodAttributes.Private | MethodAttributes.HideBySig);
        /// <summary>
        /// Attritbutes to use for generated serializers.
        /// </summary>
        public const MethodAttributes GENERATED_METHOD_ATTRIBUTES = (MethodAttributes.Static | MethodAttributes.Public | MethodAttributes.HideBySig);
        /// <summary>
        /// Prefix all built-in and user created write methods should begin with.
        /// </summary>
        internal const string WRITE_PREFIX = "Write";
        /// <summary>
        /// Prefix all built-in and user created write methods should begin with.
        /// </summary>
        internal const string GENERATED_WRITE_PREFIX = "Write___";
        /// <summary>
        /// Types to exclude from being scanned for auto serialization.
        /// </summary>
        public static readonly System.Type[] EXCLUDED_AUTO_SERIALIZER_TYPES = new System.Type[]
        {
            typeof(NetworkBehaviour)
        };
        /// <summary>
        /// Types within assemblies which begin with these prefixes will not have serializers created for them.
        /// </summary>
        public static readonly string[] EXCLUDED_ASSEMBLY_PREFIXES = new string[]
        {
            "UnityEngine."
        };
        #endregion

        public override bool ImportReferences() => true;

        /// <summary>
        /// Processes data. To be used after everything else has called ImportReferences.
        /// </summary>
        /// <returns></returns>
        public bool Process()
        {
            GeneralHelper gh = base.GetClass<GeneralHelper>();

            CreateGeneratedClassData();
            FindInstancedWriters();
            CreateInstancedWriterExtensions();

            //Creates class for generated writers, and init on load method.
            void CreateGeneratedClassData()
            {
                GeneratedWriterClassTypeDef = gh.GetOrCreateClass(out _, GENERATED_TYPE_ATTRIBUTES, GENERATED_WRITERS_CLASS_NAME, null, WriterProcessor.GENERATED_WRITER_NAMESPACE);
                /* If constructor isn't set then try to get or create it
                 * and also add it to methods if were created. */
                GeneratedWriterOnLoadMethodDef = gh.GetOrCreateMethod(GeneratedWriterClassTypeDef, out _, INITIALIZEONCE_METHOD_ATTRIBUTES, INITIALIZEONCE_METHOD_NAME, base.Module.TypeSystem.Void);
                ILProcessor pp = GeneratedWriterOnLoadMethodDef.Body.GetILProcessor();
                pp.Emit(OpCodes.Ret);
                gh.CreateRuntimeInitializeOnLoadMethodAttribute(GeneratedWriterOnLoadMethodDef);
            }

            //Finds all instanced writers and autopack types.
            void FindInstancedWriters()
            {
                Type pooledWriterType = typeof(PooledWriter);
                foreach (SR.MethodInfo methodInfo in pooledWriterType.GetMethods())
                {
                    if (IsSpecialWriteMethod(methodInfo))
                        continue;
                    bool autoPackMethod;
                    if (IsIgnoredWriteMethod(methodInfo, out autoPackMethod))
                        continue;

                    MethodReference methodRef = base.ImportReference(methodInfo);
                    /* TypeReference for the first parameter in the write method. 
                     * The first parameter will always be the type written. */
                    TypeReference typeRef = base.ImportReference(methodRef.Parameters[0].ParameterType);
                    /* If here all checks pass. */
                    AddWriterMethod(typeRef, methodRef, true, true);
                    if (autoPackMethod)
                        AutoPackedMethods.Add(typeRef);
                }
            }

            return true;
        }

        /// <summary>
        /// Returns if a MethodInfo is considered a special write method.
        /// Special write methods have declared references within this class, and will not have extensions made for them.
        /// </summary>
        public bool IsSpecialWriteMethod(SR.MethodInfo methodInfo)
        {
            /* Special methods. */
            if (methodInfo.Name == nameof(PooledWriter.Dispose))
                return true;
            else if (methodInfo.Name == nameof(PooledWriter.WritePackedWhole))
                return true;
            else if (methodInfo.Name == nameof(PooledWriter.WriteDictionary))
                return true;
            else if (methodInfo.Name == nameof(PooledWriter.WriteList))
                return true;

            return false;
        }

        /// <summary>
        /// Returns if a write method should be ignored.
        /// </summary>
        public bool IsIgnoredWriteMethod(SR.MethodInfo methodInfo, out bool autoPackMethod)
        {
            autoPackMethod = false;

            if (base.GetClass<GeneralHelper>().CodegenExclude(methodInfo))
                return true;
            //Not long enough to be a write method.
            else if (methodInfo.Name.Length < WRITE_PREFIX.Length)
                return true;
            //Method name doesn't start with writePrefix.
            else if (methodInfo.Name.Substring(0, WRITE_PREFIX.Length) != WRITE_PREFIX)
                return true;

            SR.ParameterInfo[] parameterInfos = methodInfo.GetParameters();
            /* No parameters or more than 2 parameters. Most Write methods
            * will have only 1 parameter but some will have 2 if
            * there is a pack option. */
            if (parameterInfos.Length < 1 || parameterInfos.Length > 2)
                return true;
            /* If two parameters make sure the second parameter
             * is a pack parameter. */
            if (parameterInfos.Length == 2)
            {
                autoPackMethod = (parameterInfos[1].ParameterType == typeof(AutoPackType));
                if (!autoPackMethod)
                    return true;
            }

            return false;
        }


        /// <summary>
        /// Creates writer extension methods for built-in writers.
        /// </summary>
        private void CreateInstancedWriterExtensions()
        {
            //return;
            if (!FishNetILPP.IsFishNetAssembly(base.Session))
                return;

            GeneralHelper gh = base.GetClass<GeneralHelper>();
            WriterProcessor gwh = base.GetClass<WriterProcessor>();

            //List<MethodReference> staticReaders = new List<MethodReference>();
            foreach (KeyValuePair<string, MethodReference> item in InstancedWriterMethods)
            {
                MethodReference instancedWriteMr = item.Value;
                if (instancedWriteMr.HasGenericParameters)
                    continue;

                TypeReference valueTr = instancedWriteMr.Parameters[0].ParameterType;

                MethodDefinition md = new MethodDefinition($"InstancedExtension___{instancedWriteMr.Name}",
                    WriterProcessor.GENERATED_METHOD_ATTRIBUTES,
                    base.Module.TypeSystem.Void);

                //Add extension parameter.
                ParameterDefinition writerPd = gh.CreateParameter(md, typeof(Writer), "writer");
                //Add parameters needed by instanced writer.
                List<ParameterDefinition> otherPds = md.CreateParameters(base.Session, instancedWriteMr);
                gh.MakeExtensionMethod(md);
                //
                gwh.GeneratedWriterClassTypeDef.Methods.Add(md);

                ILProcessor processor = md.Body.GetILProcessor();
                //Load writer.
                processor.Emit(OpCodes.Ldarg, writerPd);
                //Load args.
                foreach (ParameterDefinition pd in otherPds)
                    processor.Emit(OpCodes.Ldarg, pd);
                //Call instanced.
                processor.Emit(instancedWriteMr.GetCallOpCode(base.Session), instancedWriteMr);
                processor.Emit(OpCodes.Ret);
                AddWriterMethod(valueTr, md, false, true);
            }

        }

        /// <summary>
        /// Adds typeRef, methodDef to Instanced or Static write methods.
        /// </summary>
        public void AddWriterMethod(TypeReference typeRef, MethodReference methodRef, bool instanced, bool useAdd)
        {
            Dictionary<string, MethodReference> dict = (instanced) ?
            InstancedWriterMethods : StaticWriterMethods;
            string fullName = typeRef.GetFullnameWithoutBrackets();
            if (useAdd)
                dict.Add(fullName, methodRef);
            else
                dict[fullName] = methodRef;
        }

        /// <summary>
        /// Removes typeRef from Instanced or Static write methods.
        /// </summary>
        internal void RemoveWriterMethod(TypeReference typeRef, bool instanced)
        {
            Dictionary<string, MethodReference> dict = (instanced) ?
            InstancedWriterMethods : StaticWriterMethods;

            dict.Remove(typeRef.FullName);
        }

        /// <summary>
        /// Returns if typeRef supports auto packing.
        /// </summary>
        public bool IsAutoPackedType(TypeReference typeRef)
        {
            return AutoPackedMethods.Contains(typeRef);
        }


        /// <summary>
        /// Creates Write<T> delegates for known static methods.
        /// </summary>
        public void CreateStaticMethodDelegates()
        {
            foreach (KeyValuePair<string, MethodReference> item in StaticWriterMethods)
                base.GetClass<WriterProcessor>().CreateStaticMethodWriteDelegate(item.Value);
        }

        /// <summary>
        /// Creates a Write delegate for writeMethodRef and places it within the generated reader/writer constructor.
        /// </summary>
        /// <param name="writeMr"></param>
        private void CreateStaticMethodWriteDelegate(MethodReference writeMr)
        {
            GeneralHelper gh = base.GetClass<GeneralHelper>();
            WriterImports wi = base.GetClass<WriterImports>();

            //Check if ret already exist, if so remove it; ret will be added on again in this method.
            if (GeneratedWriterOnLoadMethodDef.Body.Instructions.Count != 0)
            {
                int lastIndex = (GeneratedWriterOnLoadMethodDef.Body.Instructions.Count - 1);
                if (GeneratedWriterOnLoadMethodDef.Body.Instructions[lastIndex].OpCode == OpCodes.Ret)
                    GeneratedWriterOnLoadMethodDef.Body.Instructions.RemoveAt(lastIndex);
            }

            ILProcessor processor = GeneratedWriterOnLoadMethodDef.Body.GetILProcessor();
            TypeReference dataTypeRef;
            dataTypeRef = writeMr.Parameters[1].ParameterType;

            //Check if writer already exist.
            if (_delegatedTypes.Contains(dataTypeRef))
            {
                base.LogError($"Generic write already created for {dataTypeRef.FullName}.");
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
            processor.Emit(OpCodes.Ldftn, writeMr);

            GenericInstanceType actionGenericInstance;
            MethodReference actionConstructorInstanceMethodRef;
            bool isAutoPacked = base.GetClass<WriterProcessor>().IsAutoPackedType(dataTypeRef);

            //Generate for auto pack type.
            if (isAutoPacked)
            {
                actionGenericInstance = gh.ActionT3_TypeRef.MakeGenericInstanceType(wi.WriterTypeRef, dataTypeRef, base.GetClass<WriterImports>().AutoPackTypeRef);
                actionConstructorInstanceMethodRef = gh.ActionT3Constructor_MethodRef.MakeHostInstanceGeneric(base.Session, actionGenericInstance);
            }
            //Generate for normal type.
            else
            {
                actionGenericInstance = gh.ActionT2_TypeRef.MakeGenericInstanceType(wi.WriterTypeRef, dataTypeRef);
                actionConstructorInstanceMethodRef = gh.ActionT2Constructor_MethodRef.MakeHostInstanceGeneric(base.Session, actionGenericInstance);
            }

            processor.Emit(OpCodes.Newobj, actionConstructorInstanceMethodRef);
            //Call delegate to GenericWriter<T>.Write
            GenericInstanceType genericInstance = wi.GenericWriterTypeRef.MakeGenericInstanceType(dataTypeRef);
            MethodReference genericrWriteMethodRef = (isAutoPacked) ?
                wi.WriteAutoPackGetSetMethodRef.MakeHostInstanceGeneric(base.Session, genericInstance) :
                wi.WriteGetSetMethodRef.MakeHostInstanceGeneric(base.Session, genericInstance);
            processor.Emit(OpCodes.Call, genericrWriteMethodRef);

            processor.Emit(OpCodes.Ret);
        }



        /// <summary>
        /// Returns if typeRef has a serializer.
        /// </summary>
        /// <param name="typeRef"></param>
        /// <returns></returns>
        internal bool HasSerializer(TypeReference typeRef, bool createMissing)
        {
            bool result = (GetInstancedWriteMethodReference(typeRef) != null) ||
                (GetStaticWriteMethodReference(typeRef) != null);

            if (!result && createMissing)
            {
                if (!base.GetClass<GeneralHelper>().HasNonSerializableAttribute(typeRef.CachedResolve(base.Session)))
                {
                    MethodReference methodRef = CreateWriter(typeRef);
                    result = (methodRef != null);
                }
            }

            return result;
        }


        #region GetWriterMethodReference.
        /// <summary>
        /// Returns the MethodReference for typeRef.
        /// </summary>
        /// <param name="typeRef"></param>
        /// <returns></returns>
        internal MethodReference GetInstancedWriteMethodReference(TypeReference typeRef)
        {
            string fullName = typeRef.GetFullnameWithoutBrackets();
            InstancedWriterMethods.TryGetValue(fullName, out MethodReference methodRef);
            return methodRef;
        }
        /// <summary>
        /// Returns the MethodReference for typeRef.
        /// </summary>
        /// <param name="typeRef"></param>
        /// <returns></returns>
        internal MethodReference GetStaticWriteMethodReference(TypeReference typeRef)
        {
            string fullName = typeRef.GetFullnameWithoutBrackets();
            StaticWriterMethods.TryGetValue(fullName, out MethodReference methodRef);
            return methodRef;
        }

        /// <summary>
        /// Returns the MethodReference for typeRef favoring instanced or static.
        /// </summary>
        /// <param name="typeRef"></param>
        /// <param name="favorInstanced"></param>
        /// <returns></returns>
        internal MethodReference GetWriteMethodReference(TypeReference typeRef)
        {
            bool favorInstanced = false;

            MethodReference result;
            if (favorInstanced)
            {
                result = GetInstancedWriteMethodReference(typeRef);
                if (result == null)
                    result = GetStaticWriteMethodReference(typeRef);
            }
            else
            {
                result = GetStaticWriteMethodReference(typeRef);
                if (result == null)
                    result = GetInstancedWriteMethodReference(typeRef);
            }

            return result;
        }
        /// <summary>
        /// Gets the write MethodRef for typeRef, or tries to create it if not present.
        /// </summary>
        /// <param name="typeRef"></param>
        /// <returns></returns>
        internal MethodReference GetOrCreateWriteMethodReference(TypeReference typeRef)
        {
#pragma warning disable CS0219
            bool favorInstanced = false;
#pragma warning restore CS0219
            //Try to get existing writer, if not present make one.
            MethodReference writeMethodRef = GetWriteMethodReference(typeRef);
            if (writeMethodRef == null)
                writeMethodRef = CreateWriter(typeRef);

            //If still null then return could not be generated.
            if (writeMethodRef == null)
            {
                base.LogError($"Could not create serializer for {typeRef.FullName}.");
            }
            //Otherwise, check if generic and create writes for generic pararameters.
            else if (typeRef.IsGenericInstance)
            {
                GenericInstanceType git = (GenericInstanceType)typeRef;
                foreach (TypeReference item in git.GenericArguments)
                {
                    MethodReference result = GetOrCreateWriteMethodReference(item);
                    if (result == null)
                    {
                        base.LogError($"Could not create serializer for {item.FullName}.");
                        return null;
                    }
                }
            }

            return writeMethodRef;
        }
        #endregion


        /// <summary>
        /// Creates a PooledWriter within the body/ and returns its variable index.
        /// EG: PooledWriter writer = WriterPool.GetWriter();
        /// </summary>
        internal VariableDefinition CreatePooledWriter(MethodDefinition methodDef, int length)
        {
            VariableDefinition resultVd;
            List<Instruction> insts = CreatePooledWriter(methodDef, length, out resultVd);

            ILProcessor processor = methodDef.Body.GetILProcessor();
            processor.Add(insts);
            return resultVd;
        }
        /// <summary>
        /// Creates a PooledWriter within the body/ and returns its variable index.
        /// EG: PooledWriter writer = WriterPool.GetWriter();
        /// </summary>
        /// <param name="processor"></param>
        /// <param name="methodDef"></param>
        /// <returns></returns>
        internal List<Instruction> CreatePooledWriter(MethodDefinition methodDef, int length, out VariableDefinition resultVd)
        {
            WriterImports wi = base.GetClass<WriterImports>();

            List<Instruction> insts = new List<Instruction>();
            ILProcessor processor = methodDef.Body.GetILProcessor();

            resultVd = base.GetClass<GeneralHelper>().CreateVariable(methodDef, wi.PooledWriter_TypeRef);
            //If length is specified then pass in length.
            if (length > 0)
            {
                insts.Add(processor.Create(OpCodes.Ldc_I4, length));
                insts.Add(processor.Create(OpCodes.Call, wi.WriterPool_GetWriterLength_MethodRef));
            }
            //Use parameter-less method if no length.
            else
            {
                insts.Add(processor.Create(OpCodes.Call, wi.WriterPool_GetWriter_MethodRef));
            }
            //Set value to variable definition.
            insts.Add(processor.Create(OpCodes.Stloc, resultVd));
            return insts;
        }


        /// <summary>
        /// Calls Dispose on a PooledWriter.
        /// EG: writer.Dispose();
        /// </summary>
        /// <param name="processor"></param>
        /// <param name="writerDefinition"></param>
        internal List<Instruction> DisposePooledWriter(MethodDefinition methodDef, VariableDefinition writerDefinition)
        {
            WriterImports wi = base.GetClass<WriterImports>();

            List<Instruction> insts = new List<Instruction>();
            ILProcessor processor = methodDef.Body.GetILProcessor();

            insts.Add(processor.Create(OpCodes.Ldloc, writerDefinition));
            insts.Add(processor.Create(wi.PooledWriter_Dispose_MethodRef.GetCallOpCode(base.Session), wi.PooledWriter_Dispose_MethodRef));

            return insts;
        }


        /// <summary>
        /// Creates a null check on the second argument using a boolean.
        /// </summary>
        internal void CreateRetOnNull(ILProcessor processor, ParameterDefinition writerParameterDef, ParameterDefinition checkedParameterDef, bool useBool)
        {
            Instruction endIf = processor.Create(OpCodes.Nop);
            //If (value) jmp to endIf.
            processor.Emit(OpCodes.Ldarg, checkedParameterDef);
            processor.Emit(OpCodes.Brtrue, endIf);
            //writer.WriteBool / writer.WritePackedWhole
            if (useBool)
                CreateWriteBool(processor, writerParameterDef, true);
            else
                CreateWritePackedWhole(processor, writerParameterDef, -1);
            //Exit method.
            processor.Emit(OpCodes.Ret);
            //End of if check.
            processor.Append(endIf);
        }

        #region CreateWritePackWhole
        /// <summary>
        /// Creates a call to WritePackWhole with value.
        /// </summary>
        /// <param name="processor"></param>
        /// <param name="value"></param>
        internal void CreateWritePackedWhole(ILProcessor processor, ParameterDefinition writerParameterDef, int value)
        {
            WriterImports wi = base.GetClass<WriterImports>();

            //Create local int and set it to value.
            VariableDefinition intVariableDef = base.GetClass<GeneralHelper>().CreateVariable(processor.Body.Method, typeof(int));
            base.GetClass<GeneralHelper>().SetVariableDefinitionFromInt(processor, intVariableDef, value);
            //Writer.
            processor.Emit(OpCodes.Ldarg, writerParameterDef);
            //Writer.WritePackedWhole(value).
            processor.Emit(OpCodes.Ldloc, intVariableDef);
            processor.Emit(OpCodes.Conv_U8);
            processor.Emit(wi.Writer_WritePackedWhole_MethodRef.GetCallOpCode(base.Session), wi.Writer_WritePackedWhole_MethodRef);
        }
        /// <summary>
        /// Creates a call to WritePackWhole with value.
        /// </summary>
        /// <param name="processor"></param>
        /// <param name="value"></param>
        internal void CreateWritePackedWhole(ILProcessor processor, ParameterDefinition writerParameterDef, VariableDefinition value)
        {
            WriterImports wi = base.GetClass<WriterImports>();

            //Writer.
            processor.Emit(OpCodes.Ldarg, writerParameterDef);
            //Writer.WritePackedWhole(value).
            processor.Emit(OpCodes.Ldloc, value);
            processor.Emit(OpCodes.Conv_U8);
            processor.Emit(wi.Writer_WritePackedWhole_MethodRef.GetCallOpCode(base.Session), wi.Writer_WritePackedWhole_MethodRef);
        }
        #endregion

        /// <summary>
        /// Creates a call to WriteBoolean with value.
        /// </summary>
        /// <param name="processor"></param>
        /// <param name="writerParameterDef"></param>
        /// <param name="value"></param>
        internal void CreateWriteBool(ILProcessor processor, ParameterDefinition writerParameterDef, bool value)
        {
            MethodReference writeBoolMethodRef = GetWriteMethodReference(base.GetClass<GeneralHelper>().GetTypeReference(typeof(bool)));
            processor.Emit(OpCodes.Ldarg, writerParameterDef);
            int intValue = (value) ? 1 : 0;
            processor.Emit(OpCodes.Ldc_I4, intValue);
            processor.Emit(writeBoolMethodRef.GetCallOpCode(base.Session), writeBoolMethodRef);
        }

        /// <summary>
        /// Creates a Write call on a PooledWriter variable for parameterDef.
        /// EG: writer.WriteBool(xxxxx);
        /// </summary>
        internal List<Instruction> CreateWriteInstructions(MethodDefinition methodDef, object pooledWriterDef, ParameterDefinition valueParameterDef, MethodReference writeMr)
        {
            List<Instruction> insts = new List<Instruction>();
            ILProcessor processor = methodDef.Body.GetILProcessor();

            if (writeMr != null)
            {
                if (pooledWriterDef is VariableDefinition)
                {
                    insts.Add(processor.Create(OpCodes.Ldloc, (VariableDefinition)pooledWriterDef));
                }
                else if (pooledWriterDef is ParameterDefinition)
                {
                    insts.Add(processor.Create(OpCodes.Ldarg, (ParameterDefinition)pooledWriterDef));
                }
                else
                {
                    base.LogError($"{pooledWriterDef.GetType().FullName} is not a valid writerDef. Type must be VariableDefinition or ParameterDefinition.");
                    return new List<Instruction>();
                }
                insts.Add(processor.Create(OpCodes.Ldarg, valueParameterDef));
                //If an auto pack method then insert default value.
                if (AutoPackedMethods.Contains(valueParameterDef.ParameterType))
                {
                    AutoPackType packType = base.GetClass<GeneralHelper>().GetDefaultAutoPackType(valueParameterDef.ParameterType);
                    insts.Add(processor.Create(OpCodes.Ldc_I4, (int)packType));
                }

                TypeReference valueTr = valueParameterDef.ParameterType;
                /* If generic then find write class for
                 * data type. Currently we only support one generic
                 * for this. */
                if (valueTr.IsGenericInstance)
                {
                    GenericInstanceType git = (GenericInstanceType)valueTr;
                    TypeReference genericTr = git.GenericArguments[0];
                    writeMr = writeMr.GetMethodReference(base.Session, genericTr);
                }

                insts.Add(processor.Create(OpCodes.Call, writeMr));
                return insts;
            }
            else
            {
                base.LogError($"Writer not found for {valueParameterDef.ParameterType.FullName}.");
                return new List<Instruction>();
            }
        }
        /// <summary>
        /// Creates a Write call on a PooledWriter variable for parameterDef.
        /// EG: writer.WriteBool(xxxxx);
        /// </summary>
        internal void CreateWrite(MethodDefinition methodDef, object writerDef, ParameterDefinition valuePd, MethodReference writeMr)
        {
            List<Instruction> insts = CreateWriteInstructions(methodDef, writerDef, valuePd, writeMr);
            ILProcessor processor = methodDef.Body.GetILProcessor();
            processor.Add(insts);
        }
        /// <summary>
        /// Creates a Write call to a writer.
        /// EG: StaticClass.WriteBool(xxxxx);
        /// </summary>
        /// <param name="processor"></param>
        /// <param name="fieldDef"></param>
        internal void CreateWrite(MethodDefinition writerMd, ParameterDefinition valuePd, FieldDefinition fieldDef, MethodReference writeMr)
        {
            if (writeMr != null)
            {
                ILProcessor processor = writerMd.Body.GetILProcessor();
                ParameterDefinition writerPd = writerMd.Parameters[0];

                /* If generic then find write class for
                 * data type. Currently we only support one generic
                 * for this. */
                if (fieldDef.FieldType.IsGenericInstance)
                {
                    GenericInstanceType git = (GenericInstanceType)fieldDef.FieldType;
                    TypeReference genericTr = git.GenericArguments[0];
                    writeMr = writeMr.GetMethodReference(base.Session, genericTr);
                }

                FieldReference fieldRef = base.GetClass<GeneralHelper>().GetFieldReference(fieldDef);
                processor.Emit(OpCodes.Ldarg, writerPd);
                processor.Emit(OpCodes.Ldarg, valuePd);
                processor.Emit(OpCodes.Ldfld, fieldRef);
                //If an auto pack method then insert default value.
                if (AutoPackedMethods.Contains(fieldDef.FieldType))
                {
                    AutoPackType packType = base.GetClass<GeneralHelper>().GetDefaultAutoPackType(fieldDef.FieldType);
                    processor.Emit(OpCodes.Ldc_I4, (int)packType);
                }
                processor.Emit(OpCodes.Call, writeMr);
            }
            else
            {
                base.LogError($"Writer not found for {fieldDef.FieldType.FullName}.");
            }
        }

        /// <summary>
        /// Creates a Write call to a writer.
        /// EG: StaticClass.WriteBool(xxxxx);
        /// </summary>
        /// <param name="processor"></param>
        /// <param name="propertyDef"></param>
        internal void CreateWrite(MethodDefinition writerMd, ParameterDefinition valuePd, MethodReference getMr, MethodReference writeMr)
        {
            TypeReference returnTr = base.ImportReference(getMr.ReturnType);

            if (writeMr != null)
            {
                ILProcessor processor = writerMd.Body.GetILProcessor();
                ParameterDefinition writerPd = writerMd.Parameters[0];

                /* If generic then find write class for
                * data type. Currently we only support one generic
                * for this. */
                if (returnTr.IsGenericInstance)
                {
                    GenericInstanceType git = (GenericInstanceType)returnTr;
                    TypeReference genericTr = git.GenericArguments[0];
                    writeMr = writeMr.GetMethodReference(base.Session, genericTr);
                }

                processor.Emit(OpCodes.Ldarg, writerPd);
                OpCode ldArgOC0 = (valuePd.ParameterType.IsValueType) ? OpCodes.Ldarga : OpCodes.Ldarg;
                processor.Emit(ldArgOC0, valuePd);
                processor.Emit(OpCodes.Call, getMr);
                //If an auto pack method then insert default value.
                if (AutoPackedMethods.Contains(returnTr))
                {
                    AutoPackType packType = base.GetClass<GeneralHelper>().GetDefaultAutoPackType(returnTr);
                    processor.Emit(OpCodes.Ldc_I4, (int)packType);
                }
                processor.Emit(OpCodes.Call, writeMr);
            }
            else
            {
                base.LogError($"Writer not found for {returnTr.FullName}.");
            }
        }



        #region TypeReference writer generators.

        /// <summary>
        /// Generates a writer for objectTypeReference if one does not already exist.
        /// </summary>
        /// <param name="objectTr"></param>
        /// <returns></returns>
        internal MethodReference CreateWriter(TypeReference objectTr)
        {
            MethodReference methodRefResult = null;
            TypeDefinition objectTd;
            SerializerType serializerType = base.GetClass<GeneratorHelper>().GetSerializerType(objectTr, true, out objectTd);
            if (serializerType != SerializerType.Invalid)
            {
                //Array.
                if (serializerType == SerializerType.Array)
                    methodRefResult = CreateArrayWriterMethodReference(objectTr);
                //Enum.
                else if (serializerType == SerializerType.Enum)
                    methodRefResult = CreateEnumWriterMethodDefinition(objectTr);
                //Dictionary, List, ListCache
                else if (serializerType == SerializerType.Dictionary
                    || serializerType == SerializerType.List
                    || serializerType == SerializerType.ListCache)
                    methodRefResult = CreateGenericCollectionWriterMethodReference(objectTr, serializerType);
                //NetworkBehaviour.
                else if (serializerType == SerializerType.NetworkBehaviour)
                    methodRefResult = CreateNetworkBehaviourWriterMethodReference(objectTd);
                //Nullable type.
                else if (serializerType == SerializerType.Nullable)
                    methodRefResult = CreateNullableWriterMethodReference(objectTr, objectTd);
                //Class or struct.
                else if (serializerType == SerializerType.ClassOrStruct)
                    methodRefResult = CreateClassOrStructWriterMethodDefinition(objectTr);
            }

            //If was not created.
            if (methodRefResult == null)
                RemoveFromStaticWriters(objectTr);

            return methodRefResult;
        }

        /// <summary>
        /// Removes from static writers.
        /// </summary>
        private void RemoveFromStaticWriters(TypeReference tr)
        {
            base.GetClass<WriterProcessor>().RemoveWriterMethod(tr, false);
        }
        /// <summary>
        /// Adds to static writers.
        /// </summary>
        private void AddToStaticWriters(TypeReference tr, MethodReference mr)
        {
            base.GetClass<WriterProcessor>().AddWriterMethod(tr, mr.CachedResolve(base.Session), false, true);
        }

        /// <summary>
        /// Adds a write for a NetworkBehaviour class type to WriterMethods.
        /// </summary>
        /// <param name="classTypeRef"></param>
        private MethodReference CreateNetworkBehaviourWriterMethodReference(TypeReference objectTr)
        {
            ObjectHelper oh = base.GetClass<ObjectHelper>();

            objectTr = base.ImportReference(objectTr.Resolve());
            //All NetworkBehaviour types will simply WriteNetworkBehaviour/ReadNetworkBehaviour.
            //Create generated reader/writer class. This class holds all generated reader/writers.
            base.GetClass<GeneralHelper>().GetOrCreateClass(out _, GENERATED_TYPE_ATTRIBUTES, GENERATED_WRITERS_CLASS_NAME, null);

            MethodDefinition createdWriterMd = CreateStaticWriterStubMethodDefinition(objectTr);
            AddToStaticWriters(objectTr, createdWriterMd);

            ILProcessor processor = createdWriterMd.Body.GetILProcessor();

            MethodReference writeMethodRef = base.GetClass<WriterProcessor>().GetOrCreateWriteMethodReference(oh.NetworkBehaviour_TypeRef);
            //Get parameters for method.
            ParameterDefinition writerParameterDef = createdWriterMd.Parameters[0];
            ParameterDefinition classParameterDef = createdWriterMd.Parameters[1];

            //Load parameters as arguments.
            processor.Emit(OpCodes.Ldarg, writerParameterDef);
            processor.Emit(OpCodes.Ldarg, classParameterDef);
            //writer.WriteNetworkBehaviour(arg1);
            processor.Emit(OpCodes.Call, writeMethodRef);

            processor.Emit(OpCodes.Ret);

            return base.ImportReference(createdWriterMd);
        }

        /// <summary> 
        /// Gets the length of a collection and writes the value to a variable.
        /// </summary>
        private void CreateCollectionLength(ILProcessor processor, ParameterDefinition collectionParameterDef, VariableDefinition storeVariableDef)
        {
            processor.Emit(OpCodes.Ldarg, collectionParameterDef);
            processor.Emit(OpCodes.Ldlen);
            processor.Emit(OpCodes.Conv_I4);
            processor.Emit(OpCodes.Stloc, storeVariableDef);
        }


        /// <summary>
        /// Creates a writer for a class or struct of objectTypeRef.
        /// </summary>
        /// <param name="objectTr"></param>
        /// <returns></returns>
        private MethodReference CreateNullableWriterMethodReference(TypeReference objectTr, TypeDefinition objectTd)
        {
            WriterProcessor wh = base.GetClass<WriterProcessor>();

            GenericInstanceType objectGit = objectTr as GenericInstanceType;
            TypeReference valueTr = objectGit.GenericArguments[0];

            //Get the writer for the value.
            MethodReference valueWriterMr = wh.GetOrCreateWriteMethodReference(valueTr);
            if (valueWriterMr == null)
                return null;


            MethodDefinition tmpMd;
            tmpMd = objectTd.GetMethod("get_Value");
            MethodReference genericGetValueMr = tmpMd.MakeHostInstanceGeneric(base.Session, objectGit);
            tmpMd = objectTd.GetMethod("get_HasValue");
            MethodReference genericHasValueMr = tmpMd.MakeHostInstanceGeneric(base.Session, objectGit);

            /* Stubs generate Method(Writer writer, T value). */
            MethodDefinition createdWriterMd = CreateStaticWriterStubMethodDefinition(objectTr);
            AddToStaticWriters(objectTr, createdWriterMd);

            ILProcessor processor = createdWriterMd.Body.GetILProcessor();

            //Value parameter.
            ParameterDefinition valuePd = createdWriterMd.Parameters[1];
            ParameterDefinition writerPd = createdWriterMd.Parameters[0];

            //Have to write a new ret on null because nullables use hasValue for null checks.
            Instruction afterNullRetInst = processor.Create(OpCodes.Nop);
            processor.Emit(OpCodes.Ldarga, valuePd);
            processor.Emit(OpCodes.Call, genericHasValueMr);
            processor.Emit(OpCodes.Brtrue_S, afterNullRetInst);
            wh.CreateWriteBool(processor, writerPd, true);
            processor.Emit(OpCodes.Ret);
            processor.Append(afterNullRetInst);

            //Code will only execute here and below if not null.
            wh.CreateWriteBool(processor, writerPd, false);

            processor.Emit(OpCodes.Ldarg, writerPd);
            processor.Emit(OpCodes.Ldarga, valuePd);
            processor.Emit(OpCodes.Call, genericGetValueMr);
            //If an auto pack method then insert default value.
            if (wh.IsAutoPackedType(valueTr))
            {
                AutoPackType packType = base.GetClass<GeneralHelper>().GetDefaultAutoPackType(valueTr);
                processor.Emit(OpCodes.Ldc_I4, (int)packType);
            }
            processor.Emit(OpCodes.Call, valueWriterMr);

            processor.Emit(OpCodes.Ret);
            return base.ImportReference(createdWriterMd);
        }


        /// <summary>
        /// Creates a writer for a class or struct of objectTypeRef.
        /// </summary>
        /// <param name="objectTr"></param>
        /// <returns></returns>
        private MethodReference CreateClassOrStructWriterMethodDefinition(TypeReference objectTr)
        {
            WriterProcessor wh = base.GetClass<WriterProcessor>();

            /*Stubs generate Method(Writer writer, T value). */
            MethodDefinition createdWriterMd = CreateStaticWriterStubMethodDefinition(objectTr);
            AddToStaticWriters(objectTr, createdWriterMd);
            ILProcessor processor = createdWriterMd.Body.GetILProcessor();

            //If not a value type then add a null check.
            if (!objectTr.CachedResolve(base.Session).IsValueType)
            {
                ParameterDefinition writerPd = createdWriterMd.Parameters[0];
                wh.CreateRetOnNull(processor, writerPd, createdWriterMd.Parameters[1], true);
                //Code will only execute here and below if not null.
                wh.CreateWriteBool(processor, writerPd, false);
            }

            //Write all fields for the class or struct.
            ParameterDefinition valueParameterDef = createdWriterMd.Parameters[1];
            if (!WriteFieldsAndProperties(createdWriterMd, valueParameterDef, objectTr))
                return null;

            processor.Emit(OpCodes.Ret);
            return base.ImportReference(createdWriterMd);
        }

        /// <summary>
        /// Find all fields in type and write them
        /// </summary>
        /// <param name="objectTr"></param>
        /// <param name="processor"></param>
        /// <returns>false if fail</returns>
        private bool WriteFieldsAndProperties(MethodDefinition generatedWriteMd, ParameterDefinition valuePd, TypeReference objectTr)
        {
            WriterProcessor wh = base.GetClass<WriterProcessor>();

            //This probably isn't needed but I'm too afraid to remove it.
            if (objectTr.Module != base.Module)
                objectTr = base.ImportReference(objectTr.CachedResolve(base.Session));

            //Fields
            foreach (FieldDefinition fieldDef in objectTr.FindAllSerializableFields(base.Session))//, WriterHelper.EXCLUDED_AUTO_SERIALIZER_TYPES))
            {
                TypeReference tr;
                if (fieldDef.FieldType.IsGenericInstance)
                {
                    GenericInstanceType genericTr = (GenericInstanceType)fieldDef.FieldType;
                    tr = genericTr.GenericArguments[0];
                }
                else
                {
                    tr = fieldDef.FieldType;
                }
                if (GetWriteMethod(fieldDef.FieldType, out MethodReference writeMr))
                    wh.CreateWrite(generatedWriteMd, valuePd, fieldDef, writeMr);
            }

            //Properties.
            foreach (PropertyDefinition propertyDef in objectTr.FindAllSerializableProperties(base.Session
                , WriterProcessor.EXCLUDED_AUTO_SERIALIZER_TYPES, WriterProcessor.EXCLUDED_ASSEMBLY_PREFIXES))
            {
                if (GetWriteMethod(propertyDef.PropertyType, out MethodReference writerMr))
                {
                    MethodReference getMr = base.Module.ImportReference(propertyDef.GetMethod);
                    wh.CreateWrite(generatedWriteMd, valuePd, getMr, writerMr);
                }
            }

            //Gets or creates writer method and outputs it. Returns true if method is found or created.
            bool GetWriteMethod(TypeReference tr, out MethodReference writeMr)
            {
                tr = base.ImportReference(tr);
                writeMr = wh.GetOrCreateWriteMethodReference(tr);
                return (writeMr != null);
            }

            return true;
        }


        /// <summary>
        /// Creates a writer for an enum.
        /// </summary>
        /// <param name="enumTr"></param>
        /// <returns></returns>
        private MethodReference CreateEnumWriterMethodDefinition(TypeReference enumTr)
        {
            WriterProcessor wh = base.GetClass<WriterProcessor>();

            MethodDefinition createdWriterMd = CreateStaticWriterStubMethodDefinition(enumTr);
            AddToStaticWriters(enumTr, createdWriterMd);

            ILProcessor processor = createdWriterMd.Body.GetILProcessor();

            //Element type for enum. EG: byte int ect
            TypeReference underlyingTypeRef = enumTr.CachedResolve(base.Session).GetEnumUnderlyingTypeReference();
            //Method to write that type.
            MethodReference underlyingWriterMethodRef = wh.GetOrCreateWriteMethodReference(underlyingTypeRef);
            if (underlyingWriterMethodRef == null)
                return null;

            ParameterDefinition writerParameterDef = createdWriterMd.Parameters[0];
            ParameterDefinition valueParameterDef = createdWriterMd.Parameters[1];
            //Push writer and value into call.
            processor.Emit(OpCodes.Ldarg, writerParameterDef);
            processor.Emit(OpCodes.Ldarg, valueParameterDef);
            if (wh.IsAutoPackedType(underlyingTypeRef))
                processor.Emit(OpCodes.Ldc_I4, (int)AutoPackType.Packed);

            //writer.WriteXXX(value)
            processor.Emit(OpCodes.Call, underlyingWriterMethodRef);

            processor.Emit(OpCodes.Ret);
            return base.ImportReference(createdWriterMd);
        }

        /// <summary>
        /// Calls an instanced writer from a static writer.
        /// </summary>
        private void CallInstancedWriter(MethodDefinition staticWriterMd, MethodReference instancedWriterMr)
        {
            ParameterDefinition writerPd = staticWriterMd.Parameters[0];
            ParameterDefinition valuePd = staticWriterMd.Parameters[1];
            ILProcessor processor = staticWriterMd.Body.GetILProcessor();
            processor.Emit(OpCodes.Ldarg, writerPd);
            processor.Emit(OpCodes.Ldarg, valuePd);
            processor.Emit(instancedWriterMr.GetCallOpCode(base.Session), instancedWriterMr);
            processor.Emit(OpCodes.Ret);
        }

        /// <summary>
        /// Creates a writer for an array.
        /// </summary>
        private MethodReference CreateArrayWriterMethodReference(TypeReference objectTr)
        {
            WriterImports wi = base.GetClass<WriterImports>();
            TypeReference valueTr = objectTr.GetElementType();

            //Write not found.
            if (GetOrCreateWriteMethodReference(valueTr) == null)
                return null;

            MethodDefinition createdMd = CreateStaticWriterStubMethodDefinition(objectTr);
            AddToStaticWriters(objectTr, createdMd);

            //Find instanced writer to use.
            MethodReference instancedWriteMr = wi.Writer_WriteArray_MethodRef;
            //Make generic.
            GenericInstanceMethod writeGim = instancedWriteMr.MakeGenericMethod(new TypeReference[] { valueTr });
            CallInstancedWriter(createdMd, writeGim);

            return base.ImportReference(createdMd);
        }

        /// <summary>
        /// Creates a writer for a variety of generic collections.
        /// </summary>
        private MethodReference CreateGenericCollectionWriterMethodReference(TypeReference objectTr, SerializerType st)
        {
            WriterImports wi = base.GetClass<WriterImports>();
            //Make value field generic.
            GenericInstanceType genericInstance = (GenericInstanceType)objectTr;
            base.ImportReference(genericInstance);
            TypeReference valueTr = genericInstance.GenericArguments[0];
            
            List<TypeReference> genericArguments = new List<TypeReference>();
            //Make sure all arguments have writers.
            foreach (TypeReference gaTr in genericInstance.GenericArguments)
            {
                MethodReference mr = GetOrCreateWriteMethodReference(gaTr);
                //Writer not found.
                if (mr == null)
                {
                    base.LogError($"Writer could not be found or created for type {gaTr.FullName}.");
                    return null;
                }

                genericArguments.Add(gaTr);
            }
            MethodReference valueWriteMr = GetOrCreateWriteMethodReference(valueTr);
            if (valueWriteMr == null)
                return null;

            MethodDefinition createdMd = CreateStaticWriterStubMethodDefinition(objectTr);
            AddToStaticWriters(objectTr, createdMd);

            //Find instanced writer to use.
            MethodReference instancedWriteMr;
            if (st == SerializerType.Dictionary)
                instancedWriteMr = wi.Writer_WriteDictionary_MethodRef;
            else if (st == SerializerType.List)
                instancedWriteMr = wi.Writer_WriteList_MethodRef;
            else if (st == SerializerType.ListCache)
                instancedWriteMr = wi.Writer_WriteListCache_MethodRef;
            else
                instancedWriteMr = null;

            //Not found.
            if (instancedWriteMr == null)
            {
                base.LogError($"Instanced writer not found for SerializerType {st} on object {objectTr.Name}.");
                return null;
            }

            //Make generic.
            GenericInstanceMethod writeGim = instancedWriteMr.MakeGenericMethod(genericArguments.ToArray());
            CallInstancedWriter(createdMd, writeGim);

            return base.ImportReference(createdMd);
        }

        /// <summary>
        /// Creates a method definition stub for objectTypeRef.
        /// </summary>
        /// <param name="objectTypeRef"></param>
        /// <returns></returns>
        public MethodDefinition CreateStaticWriterStubMethodDefinition(TypeReference objectTypeRef, string nameExtension = WriterProcessor.GENERATED_WRITER_NAMESPACE)
        {
            string methodName = $"{GENERATED_WRITE_PREFIX}{objectTypeRef.FullName}{nameExtension}";
            // create new writer for this type
            TypeDefinition writerTypeDef = base.GetClass<GeneralHelper>().GetOrCreateClass(out _, GENERATED_TYPE_ATTRIBUTES, GENERATED_WRITERS_CLASS_NAME, null);

            MethodDefinition writerMethodDef = writerTypeDef.AddMethod(methodName,
                    MethodAttributes.Public |
                    MethodAttributes.Static |
                    MethodAttributes.HideBySig);

            base.GetClass<GeneralHelper>().CreateParameter(writerMethodDef, base.GetClass<WriterImports>().Writer_TypeRef, "writer");
            base.GetClass<GeneralHelper>().CreateParameter(writerMethodDef, objectTypeRef, "value");
            base.GetClass<GeneralHelper>().MakeExtensionMethod(writerMethodDef);
            writerMethodDef.Body.InitLocals = true;

            return writerMethodDef;
        }
        #endregion

    }
}