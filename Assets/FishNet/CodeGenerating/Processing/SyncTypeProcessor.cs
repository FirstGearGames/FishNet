using FishNet.CodeGenerating.Extension;
using FishNet.CodeGenerating.Helping;
using FishNet.CodeGenerating.Helping.Extension;
using FishNet.Object.Delegating;
using FishNet.Object.Synchronizing;
using FishNet.Object.Synchronizing.Internal;
using GameKit.Dependencies.Utilities;
using MonoFN.Cecil;
using MonoFN.Cecil.Cil;
using MonoFN.Collections.Generic;
using System.Collections.Generic;
using System.Linq;

namespace FishNet.CodeGenerating.Processing
{
    internal class SyncTypeProcessor : CodegenBase
    {
        #region Reflection references.
        private TypeReference _syncBase_TypeRef;
        private TypeReference _syncVar_TypeRef;

#pragma warning disable CS0618 // Type or member is obsolete
        private string _syncVarAttribute_FullName = typeof(SyncVarAttribute).FullName;
        private string _syncObjectAttribute_FullName = typeof(SyncObjectAttribute).FullName;
#pragma warning restore CS0618 // Type or member is obsolete
        #endregion

        #region Const.
        private const string INITIALIZELATE_METHOD_NAME = nameof(SyncBase.InitializeLate);
        private const string INITIALIZEEARLY_METHOD_NAME = nameof(SyncBase.InitializeEarly);
        private const string GETSERIALIZEDTYPE_METHOD_NAME = nameof(ICustomSync.GetSerializedType);
        #endregion

        public override bool ImportReferences()
        {
            System.Type svType = typeof(SyncVar<>);
            _syncVar_TypeRef = base.ImportReference(svType);

            System.Type syncBaseType = typeof(SyncBase);
            _syncBase_TypeRef = base.ImportReference(syncBaseType).Resolve();

            return true;
        }

        /// <summary>
        /// Processes SyncVars and Objects.
        /// </summary>
        /// <param name="syncTypeHash">Number of SyncTypes implemented in this typeDef and those inherited of.</param>
        internal bool ProcessLocal(TypeDefinition typeDef, out uint syncTypeHash)
        {
            bool modified = false;            
            //Number of syncTypes found in the parent. This is the starting hash to use.
            syncTypeHash = GetSyncTypeCountInParents(typeDef);

            FieldDefinition[] fieldDefs = typeDef.Fields.ToArray();
            foreach (FieldDefinition fd in fieldDefs)
            {
                //Check if uses old attributes first.
                if (HasSyncTypeAttributeUnchecked(fd))
                {
                    base.LogError($"SyncType {fd.Name} on type {fd.DeclaringType.FullName} implements [SyncVar] or [SyncObject]. These attributes are no longer supported as of version 4. Please see Break Solutions within the documentation to resolve these errors.");
                    continue;
                }
                SyncType st = GetSyncType(fd);
                //Not a sync type field.
                if (st == SyncType.Unset)
                    continue;
                //Needs to be addressed if this ever occurs.
                if (st == SyncType.Unhandled)
                {
                    base.LogError($"Field {fd.Name} in type {fd.DeclaringType.FullName} is unhandled.");
                    return false;
                }
                //Errors occurred while checking the synctype field.
                if (!IsSyncTypeFieldValid(fd, true))
                    return false;

                bool isSyncObject = (st != SyncType.Variable);
                bool isGeneric = fd.FieldType.IsGenericInstance;
                if (isGeneric)
                {
                    if (TryCreateGenericSyncType(syncTypeHash, fd, isSyncObject))
                        syncTypeHash++;
                }
                else
                {
                    if (TryCreateNonGenericSyncType(syncTypeHash, fd, isSyncObject))
                        syncTypeHash++;
                }

                modified = true;
            }

            return modified;
        }


        /// <summary>
        /// Gets number of SyncTypes by checking for SyncVar/Object attributes. This does not perform error checking.
        /// </summary>
        /// <param name="typeDef"></param>
        /// <returns></returns>
        internal uint GetSyncTypeCount(TypeDefinition typeDef)
        {
            uint count = 0;
            foreach (FieldDefinition fd in typeDef.Fields)
            {
                if (IsSyncType(fd))
                    count++;
            }

            return count;
        }

        /// <summary>
        /// Gets SyncType count in all of typeDefs parents, excluding typeDef itself.
        /// </summary>
        internal uint GetSyncTypeCountInParents(TypeDefinition typeDef)
        {
            uint count = 0;
            do
            {
                typeDef = typeDef.GetNextBaseClassToProcess(base.Session);
                if (typeDef != null)
                    count += GetSyncTypeCount(typeDef);

            } while (typeDef != null);

            return count;
        }

        /// <summary>
        /// Returns if fieldDef is a syncType.
        /// </summary>
        internal bool IsSyncType(FieldDefinition fieldDef)
        {
            TypeDefinition ftTypeDef = fieldDef.FieldType.CachedResolve(base.Session);
            /* TypeDef may be null for certain generated types,
             * as well for some normal types such as queue because Unity broke
             * them in 2022+ and decided the issue was not worth resolving. */
            if (ftTypeDef == null)
                return false;

            return ftTypeDef.InheritsFrom<SyncBase>(base.Session);
        }

        /// <summary>
        /// Error checks a SyncType field. This assumes the field is a SyncType.
        /// </summary>
        private bool IsSyncTypeFieldValid(FieldDefinition fieldDef, bool outputError)
        {
            //Static.
            if (fieldDef.IsStatic)
            {
                if (outputError)
                    base.LogError($"{fieldDef.Name} SyncType in type {fieldDef.DeclaringType.FullName} cannot be static.");
                return false;
            }
            //Generic.
            if (fieldDef.FieldType.IsGenericParameter)
            {
                if (outputError)
                    base.LogError($"{fieldDef.Name} SyncType in type {fieldDef.DeclaringType.FullName} cannot be be generic.");
                return false;
            }

            //todo checking if field is initialized would be good.
            
            /* Forces readonly unless user allows for serialization.
             * If within this logic then the field is readonly. */
            if (!fieldDef.Attributes.HasFlag(FieldAttributes.InitOnly))
            {
                bool ignoreRequirement = false;
                string attributeFullName = typeof(AllowMuteableSyncTypeAttribute).FullName;
                //Check if has attribute to bypass readonly check.
                foreach (CustomAttribute item in fieldDef.CustomAttributes)
                {
                    if (item.AttributeType.FullName == attributeFullName)
                    {
                        ignoreRequirement = true;
                        break;
                    }
                }

                if (!ignoreRequirement)
                {
                    if (outputError)
                        base.LogError($"{fieldDef.Name} SyncType in type {fieldDef.DeclaringType.FullName} must be readonly or decorated with the {nameof(AllowMuteableSyncTypeAttribute)} attribute. If allowing muteable do not ever re-assign the field at runtime.");
                    return false;
                }
            }

            //Fall through/pass.
            return true;
        }

        /// <summary>
        /// Returns SyncType on a field while error checking.
        /// </summary>
        internal SyncType GetSyncType(FieldDefinition fieldDef)
        {
            if (!IsSyncType(fieldDef))
                return SyncType.Unset;

            ObjectHelper oh = base.GetClass<ObjectHelper>();
            string fdName = fieldDef.FieldType.Name;
            if (fdName == oh.SyncVar_Name)
                return SyncType.Variable;
            else if (fdName == oh.SyncList_Name)
                return SyncType.List;
            else if (fdName == oh.SyncDictionary_Name)
                return SyncType.Dictionary;
            else if (fdName == oh.SyncHashSet_Name)
                return SyncType.HashSet;
            //Custom types must also implement ICustomSync.
            else if (fieldDef.FieldType.CachedResolve(base.Session).ImplementsInterfaceRecursive<ICustomSync>(base.Session))
                return SyncType.Custom;
            else
                return SyncType.Unhandled;
        }


        /// <summary>
        /// Tries to create a SyncType that does not use generics.
        /// </summary>
        private bool TryCreateNonGenericSyncType(uint hash, FieldDefinition originalFieldDef, bool isSyncObject)
        {
            TypeDefinition fieldTypeTd = originalFieldDef.FieldType.CachedResolve(base.Session);
            if (!fieldTypeTd.ImplementsInterface<ICustomSync>())
            {
                base.LogError($"SyncType field {originalFieldDef.Name} in type {originalFieldDef.DeclaringType.FullName} does not implement {nameof(ICustomSync)}. Non-generic SyncTypes must implement {nameof(ICustomSync)}. See documentation on Custom SyncTypes for more information.");
                return false;
            }
            //Get the serialized type.
            MethodDefinition getSerialziedTypeMd = originalFieldDef.FieldType.CachedResolve(base.Session).GetMethod(GETSERIALIZEDTYPE_METHOD_NAME);
            MethodReference getSerialziedTypeMr = base.ImportReference(getSerialziedTypeMd);
            Collection<Instruction> instructions = getSerialziedTypeMr.CachedResolve(base.Session).Body.Instructions;

            bool checkForSerializer = true;
            TypeReference serializedDataTypeRef = null;
            /* If the user is returning null then
             * they are indicating a built-in serializer
             * already exists. */
            if (instructions.Count == 2 && instructions[0].OpCode == OpCodes.Ldnull && instructions[1].OpCode == OpCodes.Ret)
            {
                checkForSerializer = false;
            }
            //If not returning null then make a serializer for return type.
            else
            {
                foreach (Instruction item in instructions)
                {
                    if (item.OpCode == OpCodes.Ldnull)
                    {
                        checkForSerializer = false;
                        break;
                    }

                    //This token references the type.
                    if (item.OpCode == OpCodes.Ldtoken)
                    {
                        TypeReference importedTr = null;
                        if (item.Operand is TypeDefinition td)
                            importedTr = base.ImportReference(td);
                        else if (item.Operand is TypeReference tr)
                            importedTr = base.ImportReference(tr);

                        if (importedTr != null)
                            serializedDataTypeRef = importedTr;
                    }
                }
            }

            TypeReference[] typeRefs;
            //If need to check for serialization.            
            if (checkForSerializer)
            {
                if (serializedDataTypeRef == null)
                {
                    base.LogError($"SyncType field {originalFieldDef.Name} in type {originalFieldDef.DeclaringType.FullName} does not indicate which data type it needs to serialize. Review documentation for custom SyncTypes to view how to implement this feature.");
                    return false;
                }

                //If here then check.
                typeRefs = new TypeReference[]
                {
                    serializedDataTypeRef,
                };
                if (!CanSerialize(originalFieldDef, typeRefs))
                    return false;
            }
            else
            {
                typeRefs = null;
            }

            if (!InitializeSyncType(hash, originalFieldDef, typeRefs, isSyncObject))
                return false;

            return true;
        }

        /// <summary>
        /// Tries to create a SyncType that uses generics.
        /// </summary>
        private bool TryCreateGenericSyncType(uint hash, FieldDefinition originalFieldDef, bool isSyncObject)
        {
            GenericInstanceType tmpGenerinstanceType = originalFieldDef.FieldType as GenericInstanceType;
            TypeReference[] typeRefs = new TypeReference[tmpGenerinstanceType.GenericArguments.Count];
            for (int i = 0; i < typeRefs.Length; i++)
                typeRefs[i] = base.ImportReference(tmpGenerinstanceType.GenericArguments[i]);
            if (!CanSerialize(originalFieldDef, typeRefs))
                return false;

            InitializeSyncType(hash, originalFieldDef, typeRefs, isSyncObject);
            return true;
        }

        /// <summary>
        /// Checks if type references can be serialized.
        /// </summary>
        /// <param name="fd">Field definition specifying types. This is only used for debug output.</param>
        private bool CanSerialize(FieldDefinition fd, TypeReference[] typeRefs)
        {
            if (typeRefs == null)
                return true;

            GeneralHelper gh = base.GetClass<GeneralHelper>();
            foreach (TypeReference item in typeRefs)
            {
                bool canSerialize = gh.HasSerializerAndDeserializer(item, true);
                if (!canSerialize)
                {
                    base.LogError($"SyncType name {fd.Name} in type {fd.DeclaringType.FullName} data type {item.FullName} does not support serialization and one could not be created automatically. Use a supported type or create a custom serializer.");
                    return false;
                }
            }

            //Fall through/pass.
            return true;
        }


        /// <summary>
        /// Returns if attribute if a SyncVarAttribute.
        /// </summary>
        /// <param name="attributeFullName"></param>
        /// <returns></returns>
        private bool IsSyncVariableAttribute(string attributeFullName)
        {
            return (attributeFullName == _syncVarAttribute_FullName);
        }
        /// <summary>
        /// Returns if attribute if a SyncObjectAttribute.
        /// </summary>
        /// <param name="attributeFullName"></param>
        /// <returns></returns>
        private bool IsSyncObjectAttribute(string attributeFullName)
        {
            return (attributeFullName == _syncObjectAttribute_FullName);
        }

        /// <summary>
        /// Returns if fieldDef has a SyncType attribute. No error checking is performed.
        /// </summary>
        /// <param name="fieldDef"></param>
        /// <returns></returns>
        private bool HasSyncTypeAttributeUnchecked(FieldDefinition fieldDef)
        {
            foreach (CustomAttribute customAttribute in fieldDef.CustomAttributes)
            {
                if (IsSyncObjectAttribute(customAttribute.AttributeType.FullName) || IsSyncVariableAttribute(customAttribute.AttributeType.FullName))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Sets methods used from SyncBase for typeDef.
        /// </summary>
        /// <returns></returns>
        internal bool SetSyncBaseInitializeMethods(FieldDefinition syncTypeFieldDef, TypeReference[] variableTypeRefs, out MethodReference initializeEarlyMr, out MethodReference initializeLateMr)
        {
            initializeEarlyMr = null;
            initializeLateMr = null;
            //Find the SyncBase class.
            TypeDefinition fieldTd = syncTypeFieldDef.FieldType.CachedResolve(base.Session);
            TypeDefinition syncBaseTd = fieldTd.GetClassInInheritance(base.Session, typeof(SyncBase).FullName);
            //If SyncBase isn't found.
            if (syncBaseTd == null)
            {
                base.LogError($"Could not find SyncBase within type {fieldTd.FullName}.");
                return false;
            }
            else
            {
                //Early
                initializeEarlyMr = syncBaseTd.GetMethodReference(base.Session, INITIALIZEEARLY_METHOD_NAME);
              //  if (variableTypeRefs != null)
              //      initializeEarlyMr = initializeEarlyMr.MakeGenericMethod(variableTypeRefs);
                //Late
                initializeLateMr = syncBaseTd.GetMethodReference(base.Session, INITIALIZELATE_METHOD_NAME);
             //   if (variableTypeRefs != null)
              //      initializeLateMr = initializeLateMr.MakeGenericMethod(variableTypeRefs);

                return true;
            }

        }
        /// <summary>
        /// Initializes a SyncType using default settings.
        /// </summary>
        private bool InitializeSyncType(uint hash, FieldDefinition originalFieldDef, TypeReference[] variableTypeRefs, bool isSyncObject)
        {
            //Set needed methods from syncbase.
            MethodReference initializeLateMr;
            MethodReference initializeEarlyMr;
            if (!SetSyncBaseInitializeMethods(originalFieldDef, variableTypeRefs, out initializeEarlyMr, out initializeLateMr))
                return false;

            //Make user field public.
            originalFieldDef.Attributes &= ~FieldAttributes.Private;
            originalFieldDef.Attributes |= FieldAttributes.Public;

            TypeDefinition typeDef = originalFieldDef.DeclaringType;
            List<Instruction> insts;
            ILProcessor processor;
            MethodDefinition injectionMd;

            //InitializeEarly.
            injectionMd = typeDef.GetMethod(NetworkBehaviourProcessor.NETWORKINITIALIZE_EARLY_INTERNAL_NAME);
            processor = injectionMd.Body.GetILProcessor();
            insts = new List<Instruction>
            {
                processor.Create(OpCodes.Ldarg_0), //this.
                processor.Create(OpCodes.Ldfld, originalFieldDef),
                processor.Create(OpCodes.Ldarg_0), //this again for NetworkBehaviour.
                processor.Create(OpCodes.Ldc_I4, (int)hash),
                processor.Create(OpCodes.Ldc_I4, isSyncObject.ToInt()),
                processor.Create(OpCodes.Call, initializeEarlyMr),
            };
            processor.InsertFirst(insts);

            //InitializeLate.
            injectionMd = typeDef.GetMethod(NetworkBehaviourProcessor.NETWORKINITIALIZE_LATE_INTERNAL_NAME);
            processor = injectionMd.Body.GetILProcessor();
            insts = new List<Instruction>
            {
                processor.Create(OpCodes.Ldarg_0), //this.
                processor.Create(OpCodes.Ldfld, originalFieldDef),
                processor.Create(initializeLateMr.GetCallOpCode(base.Session), initializeLateMr),
            };
            processor.InsertFirst(insts);

            return true;

        }
    }
}
