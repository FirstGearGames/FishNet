using FishNet.CodeGenerating.Helping.Extension;
using FishNet.Serializing;
using MonoFN.Cecil;
using MonoFN.Cecil.Cil;
using MonoFN.Cecil.Rocks;
using SR = System.Reflection;
using System.Collections.Generic;
using System;
using FishNet.CodeGenerating.ILCore;
using FishNet.CodeGenerating.Extension;
using FishNet.Utility.Performance;
using FishNet.Object;
using FishNet.Utility;
using GameKit.Dependencies.Utilities;

namespace FishNet.CodeGenerating.Helping
{

    internal class ReaderProcessor : CodegenBase
    {

        #region Reflection references.
        public TypeDefinition GeneratedReader_TypeDef;
        public MethodDefinition GeneratedReader_OnLoad_MethodDef;
        public readonly Dictionary<string, MethodReference> InstancedReaderMethods = new Dictionary<string, MethodReference>();
        public readonly Dictionary<string, MethodReference> StaticReaderMethods = new Dictionary<string, MethodReference>();
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
        public const string GENERATED_READER_NAMESPACE = WriterProcessor.GENERATED_WRITER_NAMESPACE;
        /// <summary>
        /// Name to use for generated serializers class.
        /// </summary>
        public const string GENERATED_WRITERS_CLASS_NAME = "GeneratedReaders___Internal";
        /// <summary>
        /// Attributes to use for generated serializers class.
        /// </summary>
        public const TypeAttributes GENERATED_TYPE_ATTRIBUTES = (TypeAttributes.BeforeFieldInit | TypeAttributes.Class | TypeAttributes.AnsiClass |
            TypeAttributes.Public | TypeAttributes.AutoClass | TypeAttributes.Abstract | TypeAttributes.Sealed);
        /// <summary>
        /// Name to use for InitializeOnce method.
        /// </summary>
        public const string INITIALIZEONCE_METHOD_NAME = WriterProcessor.INITIALIZEONCE_METHOD_NAME;
        /// <summary>
        /// Attributes to use for InitializeOnce method within generated serializer classes.
        /// </summary>
        public const MethodAttributes INITIALIZEONCE_METHOD_ATTRIBUTES = WriterProcessor.INITIALIZEONCE_METHOD_ATTRIBUTES;
        /// <summary>
        /// Attritbutes to use for generated serializers.
        /// </summary>
        public const MethodAttributes GENERATED_METHOD_ATTRIBUTES = WriterProcessor.GENERATED_METHOD_ATTRIBUTES;
        /// <summary>
        /// Attribute fullname which indicates a default reader.
        /// </summary>
        public string DEFAULT_READER_ATTRIBUTE_FULLNAME => typeof(DefaultReaderAttribute).FullName;
        /// <summary>
        /// Prefix used which all instanced and user created serializers should start with.
        /// </summary>
        internal const string CUSTOM_READER_PREFIX = "Read";
        /// <summary>
        /// Class name to use for generated readers.
        /// </summary>
        internal const string GENERATED_READERS_CLASS_NAME = "GeneratedReaders___Internal";
        /// <summary>
        /// Types to exclude from being scanned for auto serialization.
        /// </summary>
        public static System.Type[] EXCLUDED_AUTO_SERIALIZER_TYPES => WriterProcessor.EXCLUDED_AUTO_SERIALIZER_TYPES;
        /// <summary>
        /// Types to exclude from being scanned for auto serialization.
        /// </summary>
        public static string[] EXCLUDED_ASSEMBLY_PREFIXES => WriterProcessor.EXCLUDED_ASSEMBLY_PREFIXES;
        /// <summary>
        /// MethodReference for Write<T>.
        /// </summary>
        private MethodReference _readMethodRef;
        #endregion

        public override bool ImportReferences()
        {
            TypeReference readerTr = base.ImportReference(typeof(Reader));
            _readMethodRef = readerTr.CachedResolve(base.Session).GetMethodReference(base.Session, nameof(Reader.Read));

            return true;
        }

        public bool Process()
        {
            GeneralHelper gh = base.GetClass<GeneralHelper>();

            CreateGeneratedReadersClass();
            FindInstancedReaders();
            CreateInstancedReaderExtensions();

            void CreateGeneratedReadersClass()
            {
                GeneratedReader_TypeDef = gh.GetOrCreateClass(out _, ReaderProcessor.GENERATED_TYPE_ATTRIBUTES, ReaderProcessor.GENERATED_READERS_CLASS_NAME, null, WriterProcessor.GENERATED_WRITER_NAMESPACE);
                /* If constructor isn't set then try to get or create it
                 * and also add it to methods if were created. */
                GeneratedReader_OnLoad_MethodDef = gh.GetOrCreateMethod(GeneratedReader_TypeDef, out _, INITIALIZEONCE_METHOD_ATTRIBUTES, INITIALIZEONCE_METHOD_NAME, base.Module.TypeSystem.Void);
                gh.CreateRuntimeInitializeOnLoadMethodAttribute(GeneratedReader_OnLoad_MethodDef);

                ILProcessor ppp = GeneratedReader_OnLoad_MethodDef.Body.GetILProcessor();
                ppp.Emit(OpCodes.Ret);
                //GeneratedReaderOnLoadMethodDef.DeclaringType.Methods.Remove(GeneratedReaderOnLoadMethodDef);
            }

            void FindInstancedReaders()
            {
                Type pooledWriterType = typeof(PooledReader);
                foreach (SR.MethodInfo methodInfo in pooledWriterType.GetMethods())
                {
                    if (!HasDefaultSerializerAttribute())
                        continue;

                    MethodReference methodRef = base.ImportReference(methodInfo);
                    /* TypeReference for the return type
                     * of the read method. */
                    TypeReference typeRef = base.ImportReference(methodRef.ReturnType);

                    /* If here all checks pass. */
                    AddReaderMethod(typeRef, methodRef, true, true);

                    bool HasDefaultSerializerAttribute()
                    {
                        foreach (SR.CustomAttributeData item in methodInfo.CustomAttributes)
                        {
                            if (item.AttributeType.FullName == DEFAULT_READER_ATTRIBUTE_FULLNAME)
                                return true;
                        }

                        return false;
                    }

                }
            }

            return true;
        }


        /// <summary>
        /// Adds typeRef, methodDef to instanced or readerMethods.
        /// </summary>
        /// <param name="typeRef"></param>
        /// <param name="methodRef"></param>
        /// <param name="useAdd"></param>
        internal void AddReaderMethod(TypeReference typeRef, MethodReference methodRef, bool instanced, bool useAdd)
        {
            string fullName = typeRef.GetFullnameWithoutBrackets();
            Dictionary<string, MethodReference> dict = (instanced) ?
                InstancedReaderMethods : StaticReaderMethods;

            if (useAdd)
            {
                if (dict.ContainsKey(fullName))
                    base.LogError($"Key {fullName} already exists. First method is {dict[fullName].Name}, new method is {methodRef.Name}.");
                else
                    dict.Add(fullName, methodRef);
            }
            else
            {
                dict[fullName] = methodRef;
            }
        }


        /// <summary>
        /// Creates a Read delegate for readMethodRef and places it within the generated reader/writer constructor.
        /// </summary>
        /// <param name="readMr"></param>
        /// <param name="diagnostics"></param>
        internal void CreateInitializeDelegate(MethodReference readMr)
        {
            GeneralHelper gh = base.GetClass<GeneralHelper>();
            ReaderImports ri = base.GetClass<ReaderImports>();
            WriterProcessor wp = base.GetClass<WriterProcessor>();

            /* If a global serializer is declared for the type
            * and the method is not the declared serializer then
            * exit early. */
            if (wp.IsGlobalSerializer(readMr.ReturnType) && readMr.Name.StartsWith(UtilityConstants.GENERATED_READER_PREFIX))
                return;

            GeneratedReader_OnLoad_MethodDef.RemoveEndRet(base.Session);

            //Check if already exist.
            ILProcessor processor = GeneratedReader_OnLoad_MethodDef.Body.GetILProcessor();
            TypeReference dataTypeRef = readMr.ReturnType;
            if (_delegatedTypes.Contains(dataTypeRef))
            {
                base.LogError($"Generic read already created for {dataTypeRef.FullName}.");
                return;
            }
            else
            {
                _delegatedTypes.Add(dataTypeRef);
            }

            //Create a Func<Reader, T> delegate 
            processor.Emit(OpCodes.Ldnull);
            processor.Emit(OpCodes.Ldftn, readMr);

            GenericInstanceType functionGenericInstance;
            MethodReference functionConstructorInstanceMethodRef;

            functionGenericInstance = gh.FunctionT2TypeRef.MakeGenericInstanceType(ri.Reader_TypeRef, dataTypeRef);
            functionConstructorInstanceMethodRef = gh.FunctionT2ConstructorMethodRef.MakeHostInstanceGeneric(base.Session, functionGenericInstance);
            processor.Emit(OpCodes.Newobj, functionConstructorInstanceMethodRef);

            //Call delegate to GeneratedReader<T>.Read
            GenericInstanceType genericInstance = ri.GenericReader_TypeRef.MakeGenericInstanceType(dataTypeRef);
            MethodReference genericReadMethodRef = ri.GenericReader_Read_MethodRef.MakeHostInstanceGeneric(base.Session, genericInstance);
            processor.Emit(OpCodes.Call, genericReadMethodRef);

            processor.Emit(OpCodes.Ret);
        }

        /// <summary>
        /// Creates reader extension methods for built-in readers.
        /// </summary>
        private void CreateInstancedReaderExtensions()
        {
            if (!FishNetILPP.IsFishNetAssembly(base.Session))
                return;

            GeneralHelper gh = base.GetClass<GeneralHelper>();
            ReaderProcessor gwh = base.GetClass<ReaderProcessor>();

            //List<MethodReference> staticReaders = new List<MethodReference>();
            foreach (KeyValuePair<string, MethodReference> item in InstancedReaderMethods)
            {
                MethodReference instancedReadMr = item.Value;
                if (instancedReadMr.ContainsGenericParameter)
                    continue;

                TypeReference returnTr = base.ImportReference(instancedReadMr.ReturnType);

                MethodDefinition md = new MethodDefinition($"InstancedExtension___{instancedReadMr.Name}",
                    WriterProcessor.GENERATED_METHOD_ATTRIBUTES,
                    returnTr);
                //Add extension parameter.
                ParameterDefinition readerPd = gh.CreateParameter(md, typeof(Reader), "reader");
                //Add parameters needed by instanced writer.
                List<ParameterDefinition> otherPds = md.CreateParameters(base.Session, instancedReadMr.CachedResolve(base.Session));
                gh.MakeExtensionMethod(md);
                //
                gwh.GeneratedReader_TypeDef.Methods.Add(md);

                ILProcessor processor = md.Body.GetILProcessor();
                //Load writer.
                processor.Emit(OpCodes.Ldarg, readerPd);
                //Load args.
                foreach (ParameterDefinition pd in otherPds)
                    processor.Emit(OpCodes.Ldarg, pd);
                //Call instanced.
                processor.Emit(instancedReadMr.GetCallOpCode(base.Session), instancedReadMr);
                processor.Emit(OpCodes.Ret);

                AddReaderMethod(returnTr, md, false, true);
            }
        }

        /// <summary>
        /// Removes typeRef from static/instanced reader methods.
        /// </summary>
        internal void RemoveReaderMethod(TypeReference typeRef, bool instanced)
        {
            string fullName = typeRef.GetFullnameWithoutBrackets();
            Dictionary<string, MethodReference> dict = (instanced) ?
                InstancedReaderMethods : StaticReaderMethods;

            dict.Remove(fullName);
        }

        /// <summary>
        /// Creates read instructions returning instructions and outputing variable of read result.
        /// </summary>
        internal List<Instruction> CreateRead(MethodDefinition methodDef, ParameterDefinition readerParameterDef, TypeReference readTypeRef, out VariableDefinition createdVariableDef)
        {
            ILProcessor processor = methodDef.Body.GetILProcessor();
            List<Instruction> insts = new List<Instruction>();
            MethodReference readMr = GetOrCreateReadMethodReference(readTypeRef);

            if (readMr != null)
            {
                TypeReference dataTr = readMr.ReturnType;
                bool isGlobalSerializer = base.GetClass<WriterProcessor>().IsGlobalSerializer(dataTr);

                //Make a local variable. 
                createdVariableDef = base.GetClass<GeneralHelper>().CreateVariable(methodDef, readTypeRef);
                //pooledReader.ReadBool();
                insts.Add(processor.Create(OpCodes.Ldarg, readerParameterDef));

                TypeReference valueTr = readTypeRef;
                /* If generic then find write class for
                 * data type. Currently we only support one generic
                 * for this. */
                if (valueTr.IsGenericInstance)
                {
                    GenericInstanceType git = (GenericInstanceType)valueTr;
                    TypeReference genericTr = git.GenericArguments[0];
                    readMr = readMr.GetMethodReference(base.Session, genericTr);
                }

                if (isGlobalSerializer)
                {
                    //Switch out to use Read<T> instead.
                    TypeReference genericTr = base.ImportReference(readTypeRef);
                    readMr = _readMethodRef.GetMethodReference(base.Session, genericTr);
                }

                insts.Add(processor.Create(OpCodes.Call, readMr));
                //Store into local variable.
                insts.Add(processor.Create(OpCodes.Stloc, createdVariableDef));
                return insts;
            }
            else
            {
                base.LogError("Reader not found for " + readTypeRef.ToString());
                createdVariableDef = null;
                return null;
            }
        }

        /// <summary>
        /// Creates a read for fieldRef and populates it into a created variable of class or struct type.
        /// </summary> 
        internal bool CreateReadIntoClassOrStruct(MethodDefinition readerMd, ParameterDefinition readerPd, MethodReference readMr, VariableDefinition encasingValueVd, FieldReference memberValueFr)
        {
            if (readMr != null)
            {
                WriterProcessor wp = base.GetClass<WriterProcessor>();
                bool isGlobalSerializer = (wp.IsGlobalSerializer(encasingValueVd.VariableType) || wp.IsGlobalSerializer(memberValueFr.FieldType));

                ILProcessor processor = readerMd.Body.GetILProcessor();
                /* How to load object instance. If it's a structure
                 * then it must be loaded by address. Otherwise if
                 * class Ldloc can be used. */
                OpCode loadOpCode = (encasingValueVd.VariableType.IsValueType) ?
                    OpCodes.Ldloca : OpCodes.Ldloc;

                /* If generic then find write class for
                 * data type. Currently we only support one generic
                 * for this. */
                if (memberValueFr.FieldType.IsGenericInstance)
                {
                    GenericInstanceType git = (GenericInstanceType)memberValueFr.FieldType;
                    TypeReference genericTr = git.GenericArguments[0];
                    readMr = readMr.GetMethodReference(base.Session, genericTr);
                }

                processor.Emit(loadOpCode, encasingValueVd);
                //reader.
                processor.Emit(OpCodes.Ldarg, readerPd);

                if (isGlobalSerializer)
                {
                    //Switch out to use Read<T> instead.
                    TypeReference genericTr = base.ImportReference(memberValueFr.FieldType);
                    readMr = _readMethodRef.GetMethodReference(base.Session, genericTr);
                }
                //reader.ReadXXXX().
                processor.Emit(OpCodes.Call, readMr);
                //obj.Field = result / reader.ReadXXXX().
                processor.Emit(OpCodes.Stfld, memberValueFr);

                return true;
            }
            else
            {
                base.LogError($"Reader not found for {memberValueFr.FullName}.");
                return false;
            }
        }


        /// <summary>
        /// Creates a read for fieldRef and populates it into a created variable of class or struct type.
        /// </summary>
        internal bool CreateReadIntoClassOrStruct(MethodDefinition methodDef, ParameterDefinition readerPd, MethodReference readMr, VariableDefinition objectVariableDef, MethodReference setMr, TypeReference readTr)
        {
            if (readMr != null)
            {
                ILProcessor processor = methodDef.Body.GetILProcessor();

                /* How to load object instance. If it's a structure
                 * then it must be loaded by address. Otherwise if
                 * class Ldloc can be used. */
                OpCode loadOpCode = (objectVariableDef.VariableType.IsValueType) ?
                    OpCodes.Ldloca : OpCodes.Ldloc;

                /* If generic then find write class for
                 * data type. Currently we only support one generic
                 * for this. */
                if (readTr.IsGenericInstance)
                {
                    GenericInstanceType git = (GenericInstanceType)readTr;
                    TypeReference genericTr = git.GenericArguments[0];
                    readMr = readMr.GetMethodReference(base.Session, genericTr);
                }

                processor.Emit(loadOpCode, objectVariableDef);
                //reader.
                processor.Emit(OpCodes.Ldarg, readerPd);
                //reader.ReadXXXX().
                processor.Emit(OpCodes.Call, readMr);
                //obj.Property = result / reader.ReadXXXX().
                processor.Emit(OpCodes.Call, setMr);

                return true;
            }
            else
            {
                base.LogError($"Reader not found for {readTr.FullName}.");
                return false;
            }
        }




        /// <summary>
        /// Creates generic write delegates for all currently known write types.
        /// </summary>
        internal void CreateInitializeDelegates()
        {
            foreach (KeyValuePair<string, MethodReference> item in StaticReaderMethods)
                base.GetClass<ReaderProcessor>().CreateInitializeDelegate(item.Value);
        }


        /// <summary>
        /// Returns if typeRef has a deserializer.
        /// </summary>
        /// <param name="typeRef"></param>
        /// <param name="createMissing"></param>
        /// <returns></returns>
        internal bool HasDeserializer(TypeReference typeRef, bool createMissing)
        {
            bool result = (GetInstancedReadMethodReference(typeRef) != null) ||
                (GetStaticReadMethodReference(typeRef) != null);

            if (!result && createMissing)
            {
                if (!base.GetClass<GeneralHelper>().HasNonSerializableAttribute(typeRef.CachedResolve(base.Session)))
                {
                    MethodReference methodRef = CreateReader(typeRef);
                    result = (methodRef != null);
                }
            }

            return result;
        }

        /// <summary>
        /// Creates a null check on the first argument and returns a null object if result indicates to do so.
        /// </summary>
        internal void CreateRetOnNull(ILProcessor processor, ParameterDefinition readerParameterDef, VariableDefinition resultVariableDef, bool useBool)
        {
            Instruction endIf = processor.Create(OpCodes.Nop);

            if (useBool)
                CreateReadBool(processor, readerParameterDef, resultVariableDef);
            else
                CreateReadPackedWhole(processor, readerParameterDef, resultVariableDef);

            //If (true or == -1) jmp to endIf. True is null.
            processor.Emit(OpCodes.Ldloc, resultVariableDef);
            if (useBool)
            {
                processor.Emit(OpCodes.Brfalse, endIf);
            }
            else
            {
                //-1
                processor.Emit(OpCodes.Ldc_I4_M1);
                processor.Emit(OpCodes.Bne_Un_S, endIf);
            }
            //Insert null.
            processor.Emit(OpCodes.Ldnull);
            //Exit method.
            processor.Emit(OpCodes.Ret);
            //End of if check.
            processor.Append(endIf);
        }

        /// <summary>
        /// Creates a call to WriteBoolean with value.
        /// </summary>
        /// <param name="processor"></param>
        /// <param name="writerParameterDef"></param>
        /// <param name="value"></param>
        internal void CreateReadBool(ILProcessor processor, ParameterDefinition readerParameterDef, VariableDefinition localBoolVariableDef)
        {
            MethodReference readBoolMethodRef = GetReadMethodReference(base.GetClass<GeneralHelper>().GetTypeReference(typeof(bool)));
            processor.Emit(OpCodes.Ldarg, readerParameterDef);
            processor.Emit(readBoolMethodRef.GetCallOpCode(base.Session), readBoolMethodRef);
            processor.Emit(OpCodes.Stloc, localBoolVariableDef);
        }

        /// <summary>
        /// Creates a call to WritePackWhole with value.
        /// </summary>
        /// <param name="processor"></param>
        /// <param name="value"></param>
        internal void CreateReadPackedWhole(ILProcessor processor, ParameterDefinition readerParameterDef, VariableDefinition resultVariableDef)
        {
            //Reader.
            processor.Emit(OpCodes.Ldarg, readerParameterDef);
            //Reader.ReadPackedWhole().
            MethodReference readPwMr = base.GetClass<ReaderImports>().Reader_ReadPackedWhole_MethodRef;
            processor.Emit(readPwMr.GetCallOpCode(base.Session), readPwMr);
            processor.Emit(OpCodes.Conv_I4);
            processor.Emit(OpCodes.Stloc, resultVariableDef);
        }


        #region GetReaderMethodReference.
        /// <summary>
        /// Returns the MethodReference for typeRef.
        /// </summary>
        /// <param name="typeRef"></param>
        /// <returns></returns>
        internal MethodReference GetInstancedReadMethodReference(TypeReference typeRef)
        {
            string fullName = typeRef.GetFullnameWithoutBrackets();
            InstancedReaderMethods.TryGetValue(fullName, out MethodReference methodRef);
            return methodRef;
        }
        /// <summary>
        /// Returns the MethodReference for typeRef.
        /// </summary>
        /// <param name="typeRef"></param>
        /// <returns></returns>
        internal MethodReference GetStaticReadMethodReference(TypeReference typeRef)
        {
            string fullName = typeRef.GetFullnameWithoutBrackets();
            StaticReaderMethods.TryGetValue(fullName, out MethodReference methodRef);
            return methodRef;
        }
        /// <summary>
        /// Returns the MethodReference for typeRef favoring instanced or static. Returns null if not found.
        /// </summary>
        /// <param name="typeRef"></param>
        /// <param name="favorInstanced"></param>
        /// <returns></returns>
        internal MethodReference GetReadMethodReference(TypeReference typeRef)
        {
            MethodReference result;
            bool favorInstanced = false;
            if (favorInstanced)
            {
                result = GetInstancedReadMethodReference(typeRef);
                if (result == null)
                    result = GetStaticReadMethodReference(typeRef);
            }
            else
            {
                result = GetStaticReadMethodReference(typeRef);
                if (result == null)
                    result = GetInstancedReadMethodReference(typeRef);
            }

            return result;
        }
        /// <summary>
        /// Returns the MethodReference for typeRef favoring instanced or static.
        /// </summary>
        /// <param name="typeRef"></param>
        /// <param name="favorInstanced"></param>
        /// <returns></returns>
        internal MethodReference GetOrCreateReadMethodReference(TypeReference typeRef)
        {
#pragma warning disable CS0219
            bool favorInstanced = false;
#pragma warning restore CS0219
            //Try to get existing writer, if not present make one.
            MethodReference readMethodRef = GetReadMethodReference(typeRef);
            if (readMethodRef == null)
                readMethodRef = CreateReader(typeRef);

            //If still null then return could not be generated.
            if (readMethodRef == null)
            {
                base.LogError($"Could not create deserializer for {typeRef.FullName}.");
            }
            //Otherwise, check if generic and create writes for generic parameters.
            else if (typeRef.IsGenericInstance)
            {
                GenericInstanceType git = (GenericInstanceType)typeRef;
                foreach (TypeReference item in git.GenericArguments)
                {
                    MethodReference result = GetOrCreateReadMethodReference(item);
                    if (result == null)
                    {
                        base.LogError($"Could not create deserializer for {item.FullName}.");
                        return null;
                    }
                }
            }

            return readMethodRef;
        }
        #endregion


        /// <summary>
        /// Generates a reader for objectTypeReference if one does not already exist. 
        /// </summary>
        /// <param name="objectTr"></param>
        /// <returns></returns>
        internal MethodReference CreateReader(TypeReference objectTr)
        {
            MethodReference resultMr = null;
            TypeDefinition objectTypeDef;

            SerializerType serializerType = base.GetClass<GeneratorHelper>().GetSerializerType(objectTr, false, out objectTypeDef);
            if (serializerType != SerializerType.Invalid)
            {
                //Array.
                if (serializerType == SerializerType.Array)
                    resultMr = CreateArrayReaderMethodReference(objectTr);
                //Enum.
                else if (serializerType == SerializerType.Enum)
                    resultMr = CreateEnumReaderMethodDefinition(objectTr);
                else if (serializerType == SerializerType.Dictionary
                    || serializerType == SerializerType.List)
                    resultMr = CreateGenericCollectionReaderMethodReference(objectTr, serializerType);
                //NetworkBehaviour.
                else if (serializerType == SerializerType.NetworkBehaviour)
                    resultMr = GetNetworkBehaviourReaderMethodReference(objectTr);
                //Nullable.
                else if (serializerType == SerializerType.Nullable)
                    resultMr = CreateNullableReaderMethodReference(objectTr);
                //Class or struct.
                else if (serializerType == SerializerType.ClassOrStruct)
                    resultMr = CreateClassOrStructReaderMethodReference(objectTr);
            }

            //If was not created.
            if (resultMr == null)
                RemoveFromStaticReaders(objectTr);

            return resultMr;
        }


        /// <summary>
        /// Removes from static writers.
        /// </summary>
        private void RemoveFromStaticReaders(TypeReference tr)
        {
            base.GetClass<ReaderProcessor>().RemoveReaderMethod(tr, false);
        }
        /// <summary>
        /// Adds to static writers.
        /// </summary>
        private void AddToStaticReaders(TypeReference tr, MethodReference mr)
        {
            base.GetClass<ReaderProcessor>().AddReaderMethod(tr, mr.CachedResolve(base.Session), false, true);
        }

        /// <summary>
        /// Generates a reader for objectTypeReference if one does not already exist.
        /// </summary>
        /// <param name="objectTr"></param>
        /// <returns></returns>
        private MethodReference CreateEnumReaderMethodDefinition(TypeReference objectTr)
        {
            MethodDefinition createdReaderMd = CreateStaticReaderStubMethodDefinition(objectTr);
            AddToStaticReaders(objectTr, createdReaderMd);

            ILProcessor processor = createdReaderMd.Body.GetILProcessor();

            //Get type reference for enum type. eg byte int
            TypeReference underlyingTypeRef = objectTr.CachedResolve(base.Session).GetEnumUnderlyingTypeReference();
            //Get read method for underlying type.
            MethodReference readMethodRef = base.GetClass<ReaderProcessor>().GetOrCreateReadMethodReference(underlyingTypeRef);
            if (readMethodRef == null)
                return null;

            ParameterDefinition readerParameterDef = createdReaderMd.Parameters[0];
            //reader.ReadXXX().
            processor.Emit(OpCodes.Ldarg, readerParameterDef);
            processor.Emit(OpCodes.Call, readMethodRef);

            processor.Emit(OpCodes.Ret);
            return base.ImportReference(createdReaderMd);
        }


        /// <summary>
        /// Creates a read for a class type which inherits NetworkBehaviour.
        /// </summary>
        /// <param name="objectTr"></param>
        /// <returns></returns>
        private MethodReference GetNetworkBehaviourReaderMethodReference(TypeReference objectTr)
        {
            MethodDefinition createdReaderMd = CreateStaticReaderStubMethodDefinition(objectTr);
            AddToStaticReaders(objectTr, createdReaderMd);

            ILProcessor processor = createdReaderMd.Body.GetILProcessor();
            TypeReference networkBehaviourTypeRef = base.GetClass<GeneralHelper>().GetTypeReference(typeof(NetworkBehaviour));

            processor.Emit(OpCodes.Ldarg_0);
            processor.Emit(OpCodes.Call, base.GetClass<ReaderProcessor>().GetReadMethodReference(networkBehaviourTypeRef));
            processor.Emit(OpCodes.Castclass, objectTr);
            processor.Emit(OpCodes.Ret);
            return base.ImportReference(createdReaderMd);
        }

        /// <summary>
        /// Creates a writer for an array.
        /// </summary>
        private MethodReference CreateArrayReaderMethodReference(TypeReference objectTr)
        {
            ReaderImports ri = base.GetClass<ReaderImports>();
            TypeReference valueTr = objectTr.GetElementType();

            //Write not found.
            if (GetOrCreateReadMethodReference(valueTr) == null)
                return null;

            MethodDefinition createdMd = CreateStaticReaderStubMethodDefinition(objectTr);
            AddToStaticReaders(objectTr, createdMd);

            //Find instanced writer to use.
            MethodReference instancedWriteMr = ri.Reader_ReadArray_MethodRef;
            //Make generic.
            GenericInstanceMethod writeGim = instancedWriteMr.MakeGenericMethod(new TypeReference[] { valueTr });
            CallInstancedReader(createdMd, writeGim);

            return base.ImportReference(createdMd);
        }


        /// <summary>
        /// Creates a reader for a dictionary.
        /// </summary>
        private MethodReference CreateDictionaryReaderMethodReference(TypeReference objectTr)
        {
            ReaderProcessor rp = base.GetClass<ReaderProcessor>();

            GenericInstanceType genericInstance = (GenericInstanceType)objectTr;
            base.ImportReference(genericInstance);
            TypeReference keyTr = genericInstance.GenericArguments[0];
            TypeReference valueTr = genericInstance.GenericArguments[1];

            /* Try to get instanced first for collection element type, if it doesn't exist then try to
             * get/or make a one. */
            MethodReference keyWriteMr = rp.GetOrCreateReadMethodReference(keyTr);
            MethodReference valueWriteMr = rp.GetOrCreateReadMethodReference(valueTr);
            if (keyWriteMr == null || valueWriteMr == null)
                return null;

            MethodDefinition createdReaderMd = CreateStaticReaderStubMethodDefinition(objectTr);
            AddToStaticReaders(objectTr, createdReaderMd);

            GenericInstanceMethod readDictGim = base.GetClass<ReaderImports>().Reader_ReadDictionary_MethodRef.MakeGenericMethod(new TypeReference[] { keyTr, valueTr });
            CallInstancedReader(createdReaderMd, readDictGim);

            return base.ImportReference(createdReaderMd);
        }


        /// <summary>
        /// Creates a writer for a variety of generic collections.
        /// </summary>
        private MethodReference CreateGenericCollectionReaderMethodReference(TypeReference objectTr, SerializerType st)
        {
            ReaderImports ri = base.GetClass<ReaderImports>();
            //Make value field generic.
            GenericInstanceType genericInstance = (GenericInstanceType)objectTr;
            base.ImportReference(genericInstance);
            TypeReference valueTr = genericInstance.GenericArguments[0];

            List<TypeReference> genericArguments = new List<TypeReference>();
            //Make sure all arguments have writers.
            foreach (TypeReference gaTr in genericInstance.GenericArguments)
            {
                //Writer not found.
                if (GetOrCreateReadMethodReference(gaTr) == null)
                {
                    base.LogError($"Reader could not be found or created for type {gaTr.FullName}.");
                    return null;
                }
                genericArguments.Add(gaTr);
            }
            MethodReference valueWriteMr = GetOrCreateReadMethodReference(valueTr);
            if (valueWriteMr == null)
                return null;

            MethodDefinition createdMd = CreateStaticReaderStubMethodDefinition(objectTr);
            AddToStaticReaders(objectTr, createdMd);

            //Find instanced writer to use.
            MethodReference instancedReadMr;
            if (st == SerializerType.Dictionary)
                instancedReadMr = ri.Reader_ReadDictionary_MethodRef;
            else if (st == SerializerType.List)
                instancedReadMr = ri.Reader_ReadList_MethodRef;
            else
                instancedReadMr = null;

            //Not found.
            if (instancedReadMr == null)
            {
                base.LogError($"Instanced reader not found for SerializerType {st} on object {objectTr.Name}.");
                return null;
            }

            //Make generic.
            GenericInstanceMethod writeGim = instancedReadMr.MakeGenericMethod(genericArguments.ToArray());
            CallInstancedReader(createdMd, writeGim);

            return base.ImportReference(createdMd);
        }


        /// <summary>
        /// Calls an instanced writer from a static writer.
        /// </summary>
        private void CallInstancedReader(MethodDefinition staticReaderMd, MethodReference instancedReaderMr)
        {
            ParameterDefinition readerPd = staticReaderMd.Parameters[0];
            ILProcessor processor = staticReaderMd.Body.GetILProcessor();
            processor.Emit(OpCodes.Ldarg, readerPd);
            processor.Emit(instancedReaderMr.GetCallOpCode(base.Session), instancedReaderMr);
            processor.Emit(OpCodes.Ret);
        }

        ///// <summary>
        ///// Create a reader for a list.
        ///// </summary>
        //private MethodReference CreateGenericTypeReader(TypeReference objectTr, SerializerType st)
        //{
        //    ReaderProcessor rp = base.GetClass<ReaderProcessor>();

        //    if (st != SerializerType.List && st != SerializerType.ListCache)
        //    {
        //        base.LogError($"Reader SerializerType {st} is not implemented");
        //        return null;
        //    }

        //    GenericInstanceType genericInstance = (GenericInstanceType)objectTr;
        //    base.ImportReference(genericInstance);
        //    TypeReference elementTr = genericInstance.GenericArguments[0];

        //    /* Try to get instanced first for collection element type, if it doesn't exist then try to
        //     * get/or make a one. */
        //    MethodReference elementReadMr = rp.GetOrCreateReadMethodReference(elementTr);
        //    if (elementReadMr == null)
        //        return null;

        //    TypeReference readerMethodTr = null;
        //    if (st == SerializerType.List)
        //        readerMethodTr = base.GetClass<GeneralHelper>().GetTypeReference(typeof(List<>));
        //    else if (st == SerializerType.ListCache)
        //        readerMethodTr = base.GetClass<GeneralHelper>().GetTypeReference(typeof(ListCache<>));

        //    MethodReference readerMd = rp.GetReadMethodReference(readerMethodTr);
        //    MethodDefinition typedReaderMd = CreateStaticReaderStubMethodDefinition(objectTr);

        //    AddToStaticReaders(objectTr, typedReaderMd);

        //    ParameterDefinition readerPd = typedReaderMd.Parameters[0];

        //    //Find add method for list.
        //    MethodReference readerGim = readerMd.GetMethodReference(base.Session, elementTr);
        //    ILProcessor processor = readerMd.CachedResolve(base.Session).Body.GetILProcessor();
        //    processor.Emit(OpCodes.Ldarg, readerPd);
        //    processor.Emit(OpCodes.Call, readerGim);

        //    return elementReadMr;
        //}


        /// <summary>
        /// Creates a reader method for a struct or class objectTypeRef.
        /// </summary>
        /// <param name="objectTr"></param>
        /// <returns></returns>
        private MethodReference CreateNullableReaderMethodReference(TypeReference objectTr)
        {
            ReaderProcessor rp = base.GetClass<ReaderProcessor>();

            GenericInstanceType objectGit = objectTr as GenericInstanceType;
            TypeReference valueTr = objectGit.GenericArguments[0];

            //Make sure object has a ctor.
            MethodDefinition objectCtorMd = objectTr.GetConstructor(base.Session, 1);
            if (objectCtorMd == null)
            {
                base.LogError($"{objectTr.Name} can't be deserialized because the nullable type does not have a constructor.");
                return null;
            }

            //Get the reader for the value.
            MethodReference valueReaderMr = rp.GetOrCreateReadMethodReference(valueTr);
            if (valueReaderMr == null)
                return null;

            TypeDefinition objectTd = objectTr.CachedResolve(base.Session);
            MethodDefinition createdReaderMd = CreateStaticReaderStubMethodDefinition(objectTr);
            AddToStaticReaders(objectTr, createdReaderMd);

            ILProcessor processor = createdReaderMd.Body.GetILProcessor();

            ParameterDefinition readerPd = createdReaderMd.Parameters[0];
            // create local for return value
            VariableDefinition resultVd = base.GetClass<GeneralHelper>().CreateVariable(createdReaderMd, objectTr);

            //Read if null into boolean.
            VariableDefinition nullBoolVd = createdReaderMd.CreateVariable(base.Session, typeof(bool));
            rp.CreateReadBool(processor, readerPd, nullBoolVd);

            Instruction afterReturnNullInst = processor.Create(OpCodes.Nop);
            processor.Emit(OpCodes.Ldloc, nullBoolVd);
            processor.Emit(OpCodes.Brfalse, afterReturnNullInst);
            //Return a null result.
            base.GetClass<GeneralHelper>().SetVariableDefinitionFromObject(processor, resultVd, objectTd);
            processor.Emit(OpCodes.Ldloc, resultVd);
            processor.Emit(OpCodes.Ret);
            processor.Append(afterReturnNullInst);

            MethodReference initMr = objectCtorMd.MakeHostInstanceGeneric(base.Session, objectGit);
            processor.Emit(OpCodes.Ldarg, readerPd);
            processor.Emit(OpCodes.Call, valueReaderMr);
            processor.Emit(OpCodes.Newobj, initMr);
            processor.Emit(OpCodes.Ret);

            return base.ImportReference(createdReaderMd);
        }


        /// <summary>
        /// Creates a reader method for a struct or class objectTypeRef.
        /// </summary>
        /// <param name="objectTr"></param>
        /// <returns></returns>
        private MethodReference CreateClassOrStructReaderMethodReference(TypeReference objectTr)
        {
            MethodDefinition createdReaderMd = CreateStaticReaderStubMethodDefinition(objectTr);
            AddToStaticReaders(objectTr, createdReaderMd);

            TypeDefinition objectTypeDef = objectTr.CachedResolve(base.Session);
            ILProcessor processor = createdReaderMd.Body.GetILProcessor();

            ParameterDefinition readerParameterDef = createdReaderMd.Parameters[0];
            // create local for return value
            VariableDefinition objectVariableDef = base.GetClass<GeneralHelper>().CreateVariable(createdReaderMd, objectTr);

            //If not a value type create a return null check.
            if (!objectTypeDef.IsValueType)
            {
                VariableDefinition nullVariableDef = base.GetClass<GeneralHelper>().CreateVariable(createdReaderMd, typeof(bool));
                //Load packed whole value into sizeVariableDef, exit if null indicator.
                base.GetClass<ReaderProcessor>().CreateRetOnNull(processor, readerParameterDef, nullVariableDef, true);
            }

            /* If here then not null. */
            //See if to use non-alloc reads.
            if (objectTr.CachedResolve(base.Session).HasCustomAttribute<ReadUnallocatedAttribute>())
            {
                //Make a new instance of object type and set to objectVariableDef.
                base.GetClass<GeneralHelper>().SetVariableDefinitionFromCaches(processor, objectVariableDef, objectTypeDef);
            }
            else
            {
                //Make a new instance of object type and set to objectVariableDef.
                base.GetClass<GeneralHelper>().SetVariableDefinitionFromObject(processor, objectVariableDef, objectTypeDef);
            }

            if (!ReadFieldsAndProperties(createdReaderMd, readerParameterDef, objectVariableDef, objectTr))
                return null;
            /* //codegen scriptableobjects seem to climb too high up to UnityEngine.Object when
             * creating serializers/deserialized. Make sure this is not possible. */

            //Load result and return it.
            processor.Emit(OpCodes.Ldloc, objectVariableDef);
            processor.Emit(OpCodes.Ret);

            return base.ImportReference(createdReaderMd);
        }


        /// <summary>
        /// Reads all fields of objectTypeRef.
        /// </summary>  
        private bool ReadFieldsAndProperties(MethodDefinition readerMd, ParameterDefinition readerPd, VariableDefinition encasingValueVd, TypeReference objectTr)
        {
            ReaderProcessor rp = base.GetClass<ReaderProcessor>();

            //This probably isn't needed but I'm too afraid to remove it.
            if (objectTr.Module != base.Module)
                objectTr = base.ImportReference(objectTr.CachedResolve(base.Session));

            //Fields.
            foreach (FieldDefinition fieldDef in objectTr.FindAllSerializableFields(base.Session
                , ReaderProcessor.EXCLUDED_AUTO_SERIALIZER_TYPES, ReaderProcessor.EXCLUDED_ASSEMBLY_PREFIXES))
            {
                FieldReference importedFr = base.ImportReference(fieldDef);
                if (GetReadMethod(fieldDef.FieldType, out MethodReference readMr))
                    rp.CreateReadIntoClassOrStruct(readerMd, readerPd, readMr, encasingValueVd, importedFr);
            }

            //Properties.
            foreach (PropertyDefinition propertyDef in objectTr.FindAllSerializableProperties(base.Session
                , ReaderProcessor.EXCLUDED_AUTO_SERIALIZER_TYPES, ReaderProcessor.EXCLUDED_ASSEMBLY_PREFIXES))
            {
                if (GetReadMethod(propertyDef.PropertyType, out MethodReference readMr))
                {
                    MethodReference setMr = base.Module.ImportReference(propertyDef.SetMethod);
                    rp.CreateReadIntoClassOrStruct(readerMd, readerPd, readMr, encasingValueVd, setMr, propertyDef.PropertyType);
                }
            }

            //Gets or creates writer method and outputs it. Returns true if method is found or created.
            bool GetReadMethod(TypeReference tr, out MethodReference readMr)
            {
                tr = base.ImportReference(tr);
                readMr = rp.GetOrCreateReadMethodReference(tr);
                return (readMr != null);
            }

            return true;
        }


        /// <summary>
        /// Creates the stub for a new reader method.
        /// </summary>
        /// <param name="objectTypeRef"></param>
        /// <returns></returns>
        public MethodDefinition CreateStaticReaderStubMethodDefinition(TypeReference objectTypeRef, string nameExtension = WriterProcessor.GENERATED_WRITER_NAMESPACE)
        {
            string methodName = $"{UtilityConstants.GENERATED_READER_PREFIX}{objectTypeRef.FullName}{nameExtension}s";
            // create new reader for this type
            TypeDefinition readerTypeDef = base.GetClass<GeneralHelper>().GetOrCreateClass(out _, GENERATED_TYPE_ATTRIBUTES, GENERATED_READERS_CLASS_NAME, null);
            MethodDefinition readerMethodDef = readerTypeDef.AddMethod(methodName,
                    ReaderProcessor.GENERATED_METHOD_ATTRIBUTES,
                    objectTypeRef);

            base.GetClass<GeneralHelper>().CreateParameter(readerMethodDef, base.GetClass<ReaderImports>().Reader_TypeRef, "reader");
            readerMethodDef.Body.InitLocals = true;

            return readerMethodDef;
        }


    }
}