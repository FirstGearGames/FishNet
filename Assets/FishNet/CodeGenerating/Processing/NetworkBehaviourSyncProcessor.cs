using FishNet.CodeGenerating.Extension;
using FishNet.CodeGenerating.Helping;
using FishNet.CodeGenerating.Helping.Extension;
using FishNet.Configuring;
using FishNet.Object;
using FishNet.Object.Delegating;
using FishNet.Object.Synchronizing;
using FishNet.Object.Synchronizing.Internal;
using FishNet.Serializing;
using FishNet.Transporting;
using MonoFN.Cecil;
using MonoFN.Cecil.Cil;
using MonoFN.Cecil.Rocks;
using MonoFN.Collections.Generic;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FishNet.CodeGenerating.Processing
{
    internal class NetworkBehaviourSyncProcessor : CodegenBase
    {
        #region Reflection references.
        internal TypeDefinition SyncBase_TypeDef;
        internal MethodReference SyncVarReadDelegate_Ctor_MethodRef;
        #endregion

        #region Private.
        /// <summary>
        /// Last instruction to read a sync type.
        /// </summary>
        private Instruction _lastReadInstruction;
        /// <summary>
        /// Sync objects, such as get and set, created during this process. Used to skip modifying created methods.
        /// </summary>
        private List<object> _createdSyncTypeMethodDefinitions = new List<object>();
        #endregion

        #region Const.
        internal const string SYNCVAR_PREFIX = "syncVar___";
        private const string ACCESSOR_PREFIX = "sync___";
        private const string SETREGISTERED_METHOD_NAME = "SetRegistered";
        private const string INITIALIZEINSTANCE_METHOD_NAME = "InitializeInstance";
        private const string GETSERIALIZEDTYPE_METHOD_NAME = "GetSerializedType";
        private const string SENDRATE_NAME = "SendRate";
        private const string READPERMISSIONS_NAME = "ReadPermissions";
        private const string WRITEPERMISSIONS_NAME = "WritePermissions";
        #endregion

        public override bool ImportReferences()
        {
            System.Type syncBaseType = typeof(SyncBase);
            SyncBase_TypeDef = base.ImportReference(syncBaseType).Resolve();
            SyncVarReadDelegate_Ctor_MethodRef = base.ImportReference(typeof(SyncVarReadDelegate).GetConstructors().First());

            return true;
        }

        /// <summary>
        /// Processes SyncVars and Objects.
        /// </summary>
        internal bool ProcessLocal(TypeDefinition typeDef, List<(SyncType, ProcessedSync)> allProcessedSyncs)
        {
            bool modified = false;
            _createdSyncTypeMethodDefinitions.Clear();
            _lastReadInstruction = null;

            uint syncTypeStartCount = GetSyncTypeCountInParents(typeDef);
            //Becomes set if a syncvar is created.
            MethodDefinition readSyncVarMd = null;

            FieldDefinition[] fieldDefs = typeDef.Fields.ToArray();
            foreach (FieldDefinition fd in fieldDefs)
            {
                CustomAttribute syncAttribute;
                SyncType st = GetSyncType(fd, true, out syncAttribute);
                //Not a sync type field.
                if (st == SyncType.Unset)
                    continue;

                if (st == SyncType.Variable)
                {
                    if (TryCreateSyncVar(syncTypeStartCount, allProcessedSyncs, typeDef, fd, syncAttribute, ref readSyncVarMd))
                        syncTypeStartCount++;
                }
                else if (st == SyncType.List || st == SyncType.HashSet)
                {
                    if (TryCreateSyncList_SyncHashSet(syncTypeStartCount, allProcessedSyncs, typeDef, fd, syncAttribute, st))
                        syncTypeStartCount++;
                }
                else if (st == SyncType.Dictionary)
                {
                    if (TryCreateSyncDictionary(syncTypeStartCount, allProcessedSyncs, typeDef, fd, syncAttribute))
                        syncTypeStartCount++;
                }
                else if (st == SyncType.Custom)
                {
                    if (TryCreateCustom(syncTypeStartCount, allProcessedSyncs, typeDef, fd, syncAttribute))
                        syncTypeStartCount++;
                }

                modified = true;
            }

            RegisterSyncVarReadDelegate(readSyncVarMd);

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
                if (HasSyncTypeAttributeUnchecked(fd))
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
        /// Creates a RPC delegate for rpcType.
        /// </summary>
        /// <param name="processor"></param>
        /// <param name="originalMethodDef"></param>
        /// <param name="readerMethodDef"></param>
        /// <param name="rpcType"></param>
        internal void RegisterSyncVarReadDelegate(MethodDefinition localReadMd)
        {
            if (localReadMd == null)
                return;

            NetworkBehaviourSyncProcessor nbsp = base.GetClass<NetworkBehaviourSyncProcessor>();
            NetworkBehaviourHelper nbh = base.GetClass<NetworkBehaviourHelper>();
            ILProcessor processor = localReadMd.DeclaringType.GetMethod(NetworkBehaviourProcessor.NETWORKINITIALIZE_EARLY_INTERNAL_NAME).Body.GetILProcessor();

            List<Instruction> insts = new List<Instruction>();
            /* Create delegate and call NetworkBehaviour method. */
            insts.Add(processor.Create(OpCodes.Ldarg_0));
            insts.Add(processor.Create(OpCodes.Ldarg_0));
            insts.Add(processor.Create(OpCodes.Ldftn, localReadMd));
            insts.Add(processor.Create(OpCodes.Newobj, nbsp.SyncVarReadDelegate_Ctor_MethodRef));
            insts.Add(processor.Create(OpCodes.Call, nbh.RegisterSyncVarRead_MethodRef));

            /* Has to be done last. This allows the NetworkBehaviour to
             * initialize it's fields first. */
            processor.InsertLast(insts);
        }


        /// <summary>
        /// Replaces GetSets for methods which may use a SyncType.
        /// </summary>
        internal bool ReplaceGetSets(TypeDefinition typeDef, List<(SyncType, ProcessedSync)> allProcessedSyncs)
        {
            bool modified = false;

            List<MethodDefinition> modifiableMethods = GetModifiableMethods(typeDef);
            modified |= ReplaceGetSetDirties(modifiableMethods, allProcessedSyncs);

            return modified;
        }

        /// <summary>
        /// Gets SyncType fieldDef is.
        /// </summary>
        /// <returns></returns>
        internal SyncType GetSyncType(FieldDefinition fieldDef, bool validate, out CustomAttribute syncAttribute)
        {
            syncAttribute = null;
            //If the generated field for syncvars ignore it.
            if (fieldDef.Name.StartsWith(SYNCVAR_PREFIX))
                return SyncType.Unset;

            bool syncObject;
            bool error;
            syncAttribute = GetSyncTypeAttribute(fieldDef, out syncObject, out error);
            //Do not perform further checks if an error occurred.
            if (error)
                return SyncType.Unset;
            /* If if attribute is null the code must progress
             * to throw errors when user creates a sync type
             * without using the attribute. */
            if (!validate)
            {
                return (syncAttribute == null) ? SyncType.Unset : SyncType.Custom;
            }
            else
            {
                /* If no attribute make sure the field does not implement
                 * ISyncType. If it does then a SyncObject or SyncVar attribute
                 * should exist. */
                if (syncAttribute == null)
                {
                    TypeDefinition foundSyncBaseTd = fieldDef.FieldType.CachedResolve(base.Session).GetClassInInheritance(base.Session, SyncBase_TypeDef);
                    if (foundSyncBaseTd != null && foundSyncBaseTd.ImplementsInterface<ISyncType>())
                        base.LogError($"{fieldDef.Name} within {fieldDef.DeclaringType.Name} is a SyncType but is missing the [SyncVar] or [SyncObject] attribute.");

                    return SyncType.Unset;
                }

                /* If the attribute is not [SyncObject] then the attribute
                 * is [SyncVar]. Only checks that need to be made is to make sure
                 * the user is not using a SyncVar attribute when they should be using a SyncObject attribute. */
                if (syncAttribute != null && !syncObject)
                {
                    //Make sure syncvar attribute isnt on a sync object.
                    if (GetSyncObjectSyncType(syncAttribute) != SyncType.Unset)
                    {
                        base.LogError($"{fieldDef.Name} within {fieldDef.DeclaringType.Name} uses a [SyncVar] attribute but should be using [SyncObject].");
                        return SyncType.Unset;
                    }
                    else
                        return SyncType.Variable;
                }

                /* If here could be syncObject
                 * or attribute might be null. */
                if (fieldDef.FieldType.CachedResolve(base.Session).ImplementsInterfaceRecursive<ISyncType>(base.Session))
                    return GetSyncObjectSyncType(syncAttribute);

                SyncType GetSyncObjectSyncType(CustomAttribute sa)
                {
                    //If attribute is null then throw error.
                    if (sa == null)
                    {
                        base.LogError($"{fieldDef.Name} within {fieldDef.DeclaringType.Name} is a SyncType but [SyncObject] attribute was not found.");
                        return SyncType.Unset;
                    }

                    if (fieldDef.FieldType.Name == base.GetClass<ObjectHelper>().SyncList_Name)
                    {
                        return SyncType.List;
                    }
                    else if (fieldDef.FieldType.Name == base.GetClass<ObjectHelper>().SyncDictionary_Name)
                    {
                        return SyncType.Dictionary;
                    }
                    else if (fieldDef.FieldType.Name == base.GetClass<ObjectHelper>().SyncHashSet_Name)
                    {
                        return SyncType.HashSet;
                    }
                    //Custom types must also implement ICustomSync.
                    else if (fieldDef.FieldType.CachedResolve(base.Session).ImplementsInterfaceRecursive<ICustomSync>(base.Session))
                    {
                        return SyncType.Custom;
                    }
                    else
                    {
                        return SyncType.Unset;
                    }
                }

                //Fall through.
                if (syncAttribute != null)
                    base.LogError($"SyncObject attribute found on {fieldDef.Name} within {fieldDef.DeclaringType.Name} but type {fieldDef.FieldType.Name} does not inherit from SyncBase, or if a custom type does not implement ICustomSync.");

                return SyncType.Unset;
            }

        }


        /// <summary>
        /// Returns if fieldDef is a syncType.
        /// </summary>
        /// <param name="fieldDef"></param>
        /// <returns></returns>
        internal bool IsSyncType(FieldDefinition fieldDef)
        {
            TypeDefinition ftTypeDef = fieldDef.FieldType.CachedResolve(base.Session);
            /* TypeDef may be null for certain generated types,
             * as well for some normal types such as queue because Unity broke
             * them in 2022+ and decided the issue was not worth resolving. */
            if (ftTypeDef == null)
                return false;

            return ftTypeDef.ImplementsInterface<ISyncType>();
        }


        internal SyncType GetSyncType_V4(FieldDefinition fieldDef, out CustomAttribute syncAttribute)
        {
            syncAttribute = null;

            //If the generated field for syncvars ignore it.
            if (fieldDef.Name.StartsWith(SYNCVAR_PREFIX))
                return SyncType.Unset;

            bool syncObject;
            bool error;
            syncAttribute = GetSyncTypeAttribute(fieldDef, out syncObject, out error);
            if (error)
                return SyncType.Unset;

            if (syncAttribute != null)
            {
                /* First check if it's a syncvar attribute. If so
                 * then check if the field is SyncVar then log error
                 * that the attributes are no longer used. If field is
                 * not a syncvar then error that the user needs to convert to
                 * SyncVar<T>.
                 * 
                 * This is to transition users into the new SyncVar. After they change
                 * their field tp SyncVar<T> an error will appear that the
                 * SyncVar/Object attributes are no longer used. */
                if (!syncObject)
                {
                    TypeReference syncVarTr = base.ImportReference(typeof(SyncVar<>));
                    //Not SyncVar<T>.
                    if (fieldDef.FieldType != syncVarTr)
                    {
                        base.LogError($" SyncVar fields must be declared as SyncVar<T>.");
                        return SyncType.Unset;
                    }

                }

                base.LogError($"SyncVar and SyncObject attribute are no longer used. Initialize your SyncTypes in Awake using syncType.Initialize.");
                return SyncType.Unset;
            }

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
                return SyncType.Unset;
        }


        /// <summary>
        /// Tries to create a SyncList.
        /// </summary>
        private bool TryCreateCustom(uint syncTypeCount, List<(SyncType, ProcessedSync)> allProcessedSyncs, TypeDefinition typeDef, FieldDefinition originalFieldDef, CustomAttribute syncAttribute)
        {
            //Get the serialized type.
            MethodDefinition getSerialziedTypeMd = originalFieldDef.FieldType.CachedResolve(base.Session).GetMethod(GETSERIALIZEDTYPE_METHOD_NAME);
            MethodReference getSerialziedTypeMr = base.ImportReference(getSerialziedTypeMd);
            Collection<Instruction> instructions = getSerialziedTypeMr.CachedResolve(base.Session).Body.Instructions;

            bool canSerialize = false;
            TypeReference serializedDataTypeRef = null;
            /* If the user is returning null then
             * they are indicating a custom serializer does not
             * have to be implemented. */
            if (instructions.Count == 2 && instructions[0].OpCode == OpCodes.Ldnull && instructions[1].OpCode == OpCodes.Ret)
            {
                canSerialize = true;
            }
            //If not returning null then make a serializer for return type.
            else
            {
                foreach (Instruction item in instructions)
                {
                    if (item.OpCode == OpCodes.Ldnull)
                    {
                        canSerialize = true;
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
                        {
                            serializedDataTypeRef = importedTr;
                            canSerialize = base.GetClass<GeneralHelper>().HasSerializerAndDeserializer(serializedDataTypeRef, true);
                        }
                    }
                }
            }

            //Wasn't able to determine serialized type, or create it.
            if (!canSerialize)
            {
                base.LogError($"Custom SyncObject {originalFieldDef.Name} data type {serializedDataTypeRef.FullName} does not support serialization. Use a supported type or create a custom serializer.");
                return false;
            }

            bool result = InitializeCustom(syncTypeCount, typeDef, originalFieldDef, syncAttribute);
            if (result)
                allProcessedSyncs.Add((SyncType.Custom, null));
            return result;
        }


        /// <summary>
        /// Tries to create a SyncList.
        /// </summary>
        private bool TryCreateSyncList_SyncHashSet(uint syncTypeCount, List<(SyncType, ProcessedSync)> allProcessedSyncs, TypeDefinition typeDef, FieldDefinition originalFieldDef, CustomAttribute syncAttribute, SyncType syncType)
        {
            //Import fieldType to module.
            TypeReference fieldTypeTr = base.ImportReference(originalFieldDef.FieldType);
            //Make sure type can be serialized.
            GenericInstanceType tmpGenerinstanceType = fieldTypeTr as GenericInstanceType;
            //this returns the correct data type, eg SyncList<int> would return int.
            TypeReference dataTypeRef = base.ImportReference(tmpGenerinstanceType.GenericArguments[0]);

            bool canSerialize = base.GetClass<GeneralHelper>().HasSerializerAndDeserializer(dataTypeRef, true);
            if (!canSerialize)
            {
                base.LogError($"SyncObject {originalFieldDef.Name} data type {dataTypeRef.FullName} does not support serialization. Use a supported type or create a custom serializer.");
                return false;
            }

            bool result = InitializeSyncList_SyncHashSet(syncTypeCount, typeDef, originalFieldDef, syncAttribute);
            if (result)
                allProcessedSyncs.Add((syncType, null));
            return result;
        }

        /// <summary>
        /// Tries to create a SyncDictionary.
        /// </summary>
        private bool TryCreateSyncDictionary(uint syncTypeCount, List<(SyncType, ProcessedSync)> allProcessedSyncs, TypeDefinition typeDef, FieldDefinition originalFieldDef, CustomAttribute syncAttribute)
        {
            //Make sure type can be serialized.
            GenericInstanceType tmpGenerinstanceType = originalFieldDef.FieldType as GenericInstanceType;
            //this returns the correct data type, eg SyncList<int> would return int.
            TypeReference keyTypeRef = tmpGenerinstanceType.GenericArguments[0];
            TypeReference valueTypeRef = tmpGenerinstanceType.GenericArguments[1];

            bool canSerialize;
            //Check key serializer.
            canSerialize = base.GetClass<GeneralHelper>().HasSerializerAndDeserializer(keyTypeRef, true);
            if (!canSerialize)
            {
                base.LogError($"SyncObject {originalFieldDef.Name} key type {keyTypeRef.FullName} does not support serialization. Use a supported type or create a custom serializer.");
                return false;
            }
            //Check value serializer.
            canSerialize = base.GetClass<GeneralHelper>().HasSerializerAndDeserializer(valueTypeRef, true);
            if (!canSerialize)
            {
                base.LogError($"SyncObject {originalFieldDef.Name} value type {valueTypeRef.FullName} does not support serialization. Use a supported type or create a custom serializer.");
                return false;
            }

            bool result = InitializeSyncDictionary(syncTypeCount, typeDef, originalFieldDef, syncAttribute);
            if (result)
                allProcessedSyncs.Add((SyncType.Dictionary, null));
            return result;
        }


        /// <summary>
        /// Tries to create a SyncVar.
        /// </summary>
        private bool TryCreateSyncVar(uint syncCount, List<(SyncType, ProcessedSync)> allProcessedSyncs, TypeDefinition typeDef, FieldDefinition fieldDef, CustomAttribute syncAttribute, ref MethodDefinition readSyncVarMd)
        {
            bool canSerialize = base.GetClass<GeneralHelper>().HasSerializerAndDeserializer(fieldDef.FieldType, true);
            if (!canSerialize)
            {
                base.LogError($"SyncVar {fieldDef.FullName} field type {fieldDef.FieldType.FullName} does not support serialization. Use a supported type or create a custom serializer.");
                return false;
            }

            if (base.Module != typeDef.Module)
            {
                //Only display warning if field is exposed.
                if (!fieldDef.Attributes.HasFlag(FieldAttributes.Private))
                    base.Session.DifferentAssemblySyncVars.Add(fieldDef);
                return false;
            }

            FieldDefinition syncVarFd;
            MethodReference accessorSetValueMr;
            MethodReference accessorGetValueMr;

            bool created = CreateSyncVar(syncCount, typeDef, fieldDef, syncAttribute, out syncVarFd, out accessorSetValueMr, out accessorGetValueMr, ref readSyncVarMd);
            if (created)
            {
                FieldReference originalFr = base.ImportReference(fieldDef);
                allProcessedSyncs.Add((SyncType.Variable, new ProcessedSync(originalFr, syncVarFd, accessorSetValueMr, accessorGetValueMr)));
            }

            return created;
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
                if (base.GetClass<AttributeHelper>().IsSyncVarAttribute(customAttribute.AttributeType.FullName))
                    return true;
                else if (base.GetClass<AttributeHelper>().IsSyncObjectAttribute(customAttribute.AttributeType.FullName))
                    return true;
            }

            return false;
        }


        /// <summary>
        /// Returns the syncvar attribute on a method, if one exist. Otherwise returns null.
        /// </summary>
        /// <param name="fieldDef"></param>
        /// <returns></returns>
        private CustomAttribute GetSyncTypeAttribute(FieldDefinition fieldDef, out bool syncObject, out bool error)
        {
            CustomAttribute foundAttribute = null;
            //Becomes true if an error occurred during this process.
            error = false;
            syncObject = false;

            foreach (CustomAttribute customAttribute in fieldDef.CustomAttributes)
            {
                if (base.GetClass<AttributeHelper>().IsSyncVarAttribute(customAttribute.AttributeType.FullName))
                    syncObject = false;
                else if (base.GetClass<AttributeHelper>().IsSyncObjectAttribute(customAttribute.AttributeType.FullName))
                    syncObject = true;
                else
                    continue;

                //A syncvar attribute already exist.
                if (foundAttribute != null)
                {
                    base.LogError($"{fieldDef.Name} cannot have multiple SyncType attributes.");
                    error = true;
                }
                //Static.
                if (fieldDef.IsStatic)
                {
                    base.LogError($"{fieldDef.Name} SyncType cannot be static.");
                    error = true;
                }
                //Generic.
                if (fieldDef.FieldType.IsGenericParameter)
                {
                    base.LogError($"{fieldDef.Name} SyncType cannot be be generic.");
                    error = true;
                }
                //SyncObject readonly check.
                if (syncObject && !fieldDef.Attributes.HasFlag(FieldAttributes.InitOnly))
                {
                    /* If missing readonly see if the user specified
                     * they want the object to be serialized. */
                    bool requireReadOnly = customAttribute.GetField(nameof(SyncObjectAttribute.RequireReadOnly), true);
                    if (requireReadOnly)
                    {
                        base.LogError($"{fieldDef.Name} SyncObject must be readonly.");
                        error = true;
                    }
                }


                //If all checks passed.
                if (!error)
                    foundAttribute = customAttribute;
            }

            //If an error occurred then reset results.
            if (error)
                foundAttribute = null;

            return foundAttribute;
        }

        /// <summary>
        /// Creates a syncVar class for the user's syncvar.
        /// </summary>
        /// <param name="originalFieldDef"></param>
        /// <param name="syncTypeAttribute"></param>
        /// <returns></returns>
        private bool CreateSyncVar(uint syncCount, TypeDefinition typeDef, FieldDefinition originalFieldDef, CustomAttribute syncTypeAttribute, out FieldDefinition createdSyncVarFd, out MethodReference accessorSetValueMethodRef
            , out MethodReference accessorGetValueMethodRef, ref MethodDefinition readSyncVarMd)
        {
            accessorGetValueMethodRef = null;
            accessorSetValueMethodRef = null;
            CreatedSyncVar createdSyncVar;
            createdSyncVarFd = CreateSyncVarFieldDefinition(typeDef, originalFieldDef, out createdSyncVar);

            if (createdSyncVarFd != null)
            {
                MethodReference hookMr = GetSyncVarHookMethodReference(typeDef, originalFieldDef, syncTypeAttribute);
                createdSyncVar.HookMr = hookMr;

                //If accessor was made add it's methods to createdSyncTypeObjects.
                if (CreateSyncVarAccessor(originalFieldDef, createdSyncVarFd, createdSyncVar, out accessorGetValueMethodRef,
                    out accessorSetValueMethodRef, hookMr) != null)
                {
                    _createdSyncTypeMethodDefinitions.Add(accessorGetValueMethodRef.CachedResolve(base.Session));
                    _createdSyncTypeMethodDefinitions.Add(accessorSetValueMethodRef.CachedResolve(base.Session));
                }

                InitializeSyncVar(syncCount, createdSyncVarFd, typeDef, originalFieldDef, syncTypeAttribute, createdSyncVar);

                MethodDefinition syncVarReadMd = CreateSyncVarRead(typeDef, syncCount, createdSyncVar, originalFieldDef, accessorSetValueMethodRef, ref readSyncVarMd);
                if (syncVarReadMd != null)
                    _createdSyncTypeMethodDefinitions.Add(syncVarReadMd);

                return true;
            }
            else
            {
                return false;
            }

        }

        /// <summary>
        /// Creates or gets a SyncType class for originalFieldDef.
        /// </summary>
        /// <returns></returns>  
        private FieldDefinition CreateSyncVarFieldDefinition(TypeDefinition typeDef, FieldDefinition originalFieldDef, out CreatedSyncVar createdSyncVar)
        {
            createdSyncVar = base.GetClass<CreatedSyncVarGenerator>().GetCreatedSyncVar(originalFieldDef, true);
            if (createdSyncVar == null)
                return null;

            originalFieldDef.Attributes &= ~FieldAttributes.Private;
            originalFieldDef.Attributes |= FieldAttributes.Public;

            FieldDefinition createdFieldDef = new FieldDefinition($"{SYNCVAR_PREFIX}{originalFieldDef.Name}", originalFieldDef.Attributes, createdSyncVar.SyncVarGit);
            if (createdFieldDef == null)
            {
                base.LogError($"Could not create field for Sync type {originalFieldDef.FieldType.FullName}, name of {originalFieldDef.Name}.");
                return null;
            }
            //Add to CreatedSyncVar.
            createdSyncVar.SetSyncVarClassField(createdFieldDef);

            typeDef.Fields.Add(createdFieldDef);
            return createdFieldDef;
        }

        /// <summary>
        /// Validates and gets the hook MethodReference for a SyncVar if available.
        /// </summary>
        /// <param name="moduleDef"></param>
        /// <param name="typeDef"></param>
        /// <param name="attribute"></param>
        /// <returns></returns>
        private MethodReference GetSyncVarHookMethodReference(TypeDefinition typeDef, FieldDefinition originalFieldDef, CustomAttribute attribute)
        {
            string hook = attribute.GetField("OnChange", string.Empty);
            //No hook is specified.
            if (string.IsNullOrEmpty(hook))
                return null;

            MethodDefinition md = typeDef.GetMethod(hook);

            if (md != null)
            {
                string incorrectParametersMsg = $"OnChange method for {originalFieldDef.FullName} must contain 3 parameters in order of {originalFieldDef.FieldType.Name} oldValue, {originalFieldDef.FieldType.Name} newValue, {base.Module.TypeSystem.Boolean} asServer.";
                //Not correct number of parameters.
                if (md.Parameters.Count != 3)
                {
                    base.LogError(incorrectParametersMsg);
                    return null;
                }
                /* Check if any parameters are not
                 * the expected type. */
                if (md.Parameters[0].ParameterType.CachedResolve(base.Session) != originalFieldDef.FieldType.CachedResolve(base.Session) ||
                    md.Parameters[1].ParameterType.CachedResolve(base.Session) != originalFieldDef.FieldType.CachedResolve(base.Session) ||
                    md.Parameters[2].ParameterType.CachedResolve(base.Session) != base.Module.TypeSystem.Boolean.CachedResolve(base.Session))
                {
                    base.LogError(incorrectParametersMsg);
                    return null;
                }

                //If here everything checks out, return a method reference to hook method.
                return base.ImportReference(md);
            }
            //Hook specified but no method found.
            else
            {
                base.LogError($"Could not find method name {hook} for SyncType {originalFieldDef.FullName}.");
                return null;
            }
        }

        /// <summary>
        /// Creates accessor for a SyncVar.
        /// </summary>
        /// <returns></returns>
        private FieldDefinition CreateSyncVarAccessor(FieldDefinition originalFd, FieldDefinition createdSyncVarFd, CreatedSyncVar createdSyncVar, out MethodReference accessorGetValueMr, out MethodReference accessorSetValueMr, MethodReference hookMr)
        {
            /* Create and add property definition. */
            PropertyDefinition createdPropertyDef = new PropertyDefinition($"SyncAccessor_{originalFd.Name}", PropertyAttributes.None, originalFd.FieldType);
            createdPropertyDef.DeclaringType = originalFd.DeclaringType;
            //add the methods and property to the type.
            originalFd.DeclaringType.Properties.Add(createdPropertyDef);

            ILProcessor processor;

            /* Get method for property definition. */
            MethodDefinition createdGetMethodDef = originalFd.DeclaringType.AddMethod($"{ACCESSOR_PREFIX}get_value_{originalFd.Name}", MethodAttributes.Public |
                    MethodAttributes.SpecialName | MethodAttributes.HideBySig,
                    originalFd.FieldType);
            createdGetMethodDef.SemanticsAttributes = MethodSemanticsAttributes.Getter;

            processor = createdGetMethodDef.Body.GetILProcessor();
            processor.Emit(OpCodes.Ldarg_0); //this.
            processor.Emit(OpCodes.Ldfld, originalFd);
            processor.Emit(OpCodes.Ret);
            accessorGetValueMr = base.ImportReference(createdGetMethodDef);
            //Add getter to properties.
            createdPropertyDef.GetMethod = createdGetMethodDef;

            /* Set method. */
            //Create the set method
            MethodDefinition createdSetMethodDef = originalFd.DeclaringType.AddMethod($"{ACCESSOR_PREFIX}set_value_{originalFd.Name}", MethodAttributes.Public |
                    MethodAttributes.SpecialName |
                    MethodAttributes.HideBySig);
            createdSetMethodDef.SemanticsAttributes = MethodSemanticsAttributes.Setter;

            ParameterDefinition valueParameterDef = base.GetClass<GeneralHelper>().CreateParameter(createdSetMethodDef, originalFd.FieldType, "value");
            ParameterDefinition calledByUserParameterDef = base.GetClass<GeneralHelper>().CreateParameter(createdSetMethodDef, typeof(bool), "asServer");
            processor = createdSetMethodDef.Body.GetILProcessor();

            /* Assign to new value. Do this first because SyncVar<T> calls hook 
             * and value needs to be updated before hook. Only update
             * value if calledByUser(asServer) or (!calledByUser && !base.IsServer).
             * This ensures clientHost will not overwrite server value. */

            Instruction afterChangeFieldInst = processor.Create(OpCodes.Nop);
            Instruction beforeChangeFieldInst = processor.Create(OpCodes.Nop);
            //if (calledByUser || !base.IsServer)
            processor.Emit(OpCodes.Ldarg, calledByUserParameterDef);
            processor.Emit(OpCodes.Brtrue, beforeChangeFieldInst);
            processor.Emit(OpCodes.Ldarg_0); //this.            
            processor.Emit(OpCodes.Call, base.GetClass<NetworkBehaviourHelper>().IsServerInitialized_MethodRef);
            processor.Emit(OpCodes.Brtrue, afterChangeFieldInst);

            //      _originalField = value;
            processor.Append(beforeChangeFieldInst);
            processor.Emit(OpCodes.Ldarg_0); //this.
            processor.Emit(OpCodes.Ldarg, valueParameterDef);
            processor.Emit(OpCodes.Stfld, originalFd);
            processor.Append(afterChangeFieldInst);

            Instruction retInst = processor.Create(OpCodes.Ret);

            if (!Configuration.Configurations.CodeStripping.IsBuilding)
            {
                processor.Emit(OpCodes.Call, base.GetClass<GeneralHelper>().Application_IsPlaying_MethodRef);
                processor.Emit(OpCodes.Brfalse_S, retInst);
            }
            //      SyncVar<>.SetValue(....);
            processor.Emit(OpCodes.Ldarg_0); //this.
            processor.Emit(OpCodes.Ldfld, createdSyncVarFd);
            processor.Emit(OpCodes.Ldarg, valueParameterDef);
            processor.Emit(OpCodes.Ldarg, calledByUserParameterDef);
            processor.Emit(createdSyncVar.SetValueMr.GetCallOpCode(base.Session), createdSyncVar.SetValueMr);

            processor.Append(retInst);
            accessorSetValueMr = base.ImportReference(createdSetMethodDef);
            //Add setter to properties.
            createdPropertyDef.SetMethod = createdSetMethodDef;

            return originalFd;
        }

        /// <summary>
        /// Sets methods used from SyncBase for typeDef.
        /// </summary>
        /// <returns></returns>
        internal bool SetSyncBaseMethods(TypeDefinition typeDef, out MethodReference setRegisteredMr, out MethodReference initializeInstanceMr)
        {
            setRegisteredMr = null;
            initializeInstanceMr = null;
            //Find the SyncBase class.
            TypeDefinition syncBaseTd = null;
            TypeDefinition copyTd = typeDef;
            do
            {
                if (copyTd.Name == nameof(SyncBase))
                {
                    syncBaseTd = copyTd;
                    break;
                }
                copyTd = copyTd.GetNextBaseTypeDefinition(base.Session);
            } while (copyTd != null);

            //If SyncBase isn't found.
            if (syncBaseTd == null)
            {
                base.LogError($"Could not find SyncBase within type {typeDef.FullName}.");
                return false;
            }
            else
            {
                //InitializeInstance.
                initializeInstanceMr = syncBaseTd.GetMethodReference(base.Session, INITIALIZEINSTANCE_METHOD_NAME);
                //SetSyncIndex.
                setRegisteredMr = syncBaseTd.GetMethodReference(base.Session, SETREGISTERED_METHOD_NAME);
                return true;
            }

        }

        /// <summary>
        /// Initializes a custom SyncObject.
        /// </summary>
        internal bool InitializeCustom(uint syncCount, TypeDefinition typeDef, FieldDefinition originalFieldDef, CustomAttribute attribute)
        {
            float sendRate = 0.1f;
            WritePermission writePermissions = WritePermission.ServerOnly;
            ReadPermission readPermissions = ReadPermission.Observers;
            Channel channel = Channel.Reliable;
            //If attribute isn't null then override values.
            if (attribute != null)
            {
                sendRate = attribute.GetField(SENDRATE_NAME, -1f);
                writePermissions = attribute.GetField(WRITEPERMISSIONS_NAME, WritePermission.ServerOnly);
                readPermissions = attribute.GetField(READPERMISSIONS_NAME, ReadPermission.Observers);
                channel = Channel.Reliable; //attribute.GetField("Channel", Channel.Reliable);
            }

            //Set needed methods from syncbase.
            MethodReference setSyncIndexMr;
            MethodReference initializeInstanceMr;
            if (!SetSyncBaseMethods(originalFieldDef.FieldType.CachedResolve(base.Session), out setSyncIndexMr, out initializeInstanceMr))
                return false;

            MethodDefinition injectionMethodDef;
            ILProcessor processor;

            uint hash = (uint)syncCount;
            List<Instruction> insts = new List<Instruction>();

            /* Initialize with attribute settings. */
            injectionMethodDef = typeDef.GetMethod(NetworkBehaviourProcessor.NETWORKINITIALIZE_EARLY_INTERNAL_NAME);
            processor = injectionMethodDef.Body.GetILProcessor();
            //

            insts.Add(processor.Create(OpCodes.Ldarg_0)); //this.
            insts.Add(processor.Create(OpCodes.Ldfld, originalFieldDef));
            insts.Add(processor.Create(OpCodes.Ldarg_0)); //this again for NetworkBehaviour.
            insts.Add(processor.Create(OpCodes.Ldc_I4, (int)hash));
            insts.Add(processor.Create(OpCodes.Ldc_I4, (int)writePermissions));
            insts.Add(processor.Create(OpCodes.Ldc_I4, (int)readPermissions));
            insts.Add(processor.Create(OpCodes.Ldc_R4, sendRate));
            insts.Add(processor.Create(OpCodes.Ldc_I4, (int)channel));
            insts.Add(processor.Create(OpCodes.Ldc_I4_1)); //true for syncObject.
            insts.Add(processor.Create(OpCodes.Call, initializeInstanceMr));
            processor.InsertFirst(insts);

            insts.Clear();
            /* Set NetworkBehaviour and SyncIndex to use. */
            injectionMethodDef = typeDef.GetMethod(NetworkBehaviourProcessor.NETWORKINITIALIZE_LATE_INTERNAL_NAME);
            processor = injectionMethodDef.Body.GetILProcessor();
            //
            insts.Add(processor.Create(OpCodes.Ldarg_0)); //this.
            insts.Add(processor.Create(OpCodes.Ldfld, originalFieldDef));
            insts.Add(processor.Create(setSyncIndexMr.GetCallOpCode(base.Session), setSyncIndexMr));

            processor.InsertLast(insts);

            return true;
        }



        /// <summary>
        /// Initializes a SyncList.
        /// </summary>
        internal bool InitializeSyncList_SyncHashSet(uint syncCount, TypeDefinition typeDef, FieldDefinition originalFieldDef, CustomAttribute attribute)
        {
            float sendRate = 0.1f;
            WritePermission writePermissions = WritePermission.ServerOnly;
            ReadPermission readPermissions = ReadPermission.Observers;
            Channel channel = Channel.Reliable;
            //If attribute isn't null then override values.
            if (attribute != null)
            {
                sendRate = attribute.GetField(SENDRATE_NAME, -1f);
                writePermissions = attribute.GetField(WRITEPERMISSIONS_NAME, WritePermission.ServerOnly);
                readPermissions = attribute.GetField(READPERMISSIONS_NAME, ReadPermission.Observers);
                channel = Channel.Reliable; //attribute.GetField("Channel", Channel.Reliable);
            }

            //This import shouldn't be needed but cecil is stingy so rather be safe than sorry.
            base.ImportReference(originalFieldDef);

            //Set needed methods from syncbase.
            MethodReference setSyncIndexMr;
            MethodReference initializeInstanceMr;
            if (!SetSyncBaseMethods(originalFieldDef.FieldType.CachedResolve(base.Session), out setSyncIndexMr, out initializeInstanceMr))
                return false;

            MethodDefinition injectionMethodDef;
            ILProcessor processor;

            uint hash = (uint)syncCount;
            List<Instruction> insts = new List<Instruction>();

            /* Initialize with attribute settings. */
            injectionMethodDef = typeDef.GetMethod(NetworkBehaviourProcessor.NETWORKINITIALIZE_EARLY_INTERNAL_NAME);
            processor = injectionMethodDef.Body.GetILProcessor();

            //InitializeInstance.
            insts.Add(processor.Create(OpCodes.Ldarg_0)); //this.
            insts.Add(processor.Create(OpCodes.Ldfld, originalFieldDef));
            insts.Add(processor.Create(OpCodes.Ldarg_0)); //this again for NetworkBehaviour.
            insts.Add(processor.Create(OpCodes.Ldc_I4, (int)hash));
            insts.Add(processor.Create(OpCodes.Ldc_I4, (int)writePermissions));
            insts.Add(processor.Create(OpCodes.Ldc_I4, (int)readPermissions));
            insts.Add(processor.Create(OpCodes.Ldc_R4, sendRate));
            insts.Add(processor.Create(OpCodes.Ldc_I4, (int)channel));
            insts.Add(processor.Create(OpCodes.Ldc_I4_1)); //true for syncObject.
            insts.Add(processor.Create(OpCodes.Call, initializeInstanceMr));
            processor.InsertFirst(insts);

            insts.Clear();
            /* Set NetworkBehaviour and SyncIndex to use. */
            injectionMethodDef = typeDef.GetMethod(NetworkBehaviourProcessor.NETWORKINITIALIZE_LATE_INTERNAL_NAME);
            processor = injectionMethodDef.Body.GetILProcessor();

            insts.Add(processor.Create(OpCodes.Ldarg_0)); //this.
            insts.Add(processor.Create(OpCodes.Ldfld, originalFieldDef));
            insts.Add(processor.Create(setSyncIndexMr.GetCallOpCode(base.Session), setSyncIndexMr));

            processor.InsertLast(insts);

            return true;
        }

        /// <summary>
        /// Initializes a SyncDictionary.
        /// </summary>
        internal bool InitializeSyncDictionary(uint syncCount, TypeDefinition typeDef, FieldDefinition originalFieldDef, CustomAttribute attribute)
        {
            float sendRate = 0.1f;
            WritePermission writePermissions = WritePermission.ServerOnly;
            ReadPermission readPermissions = ReadPermission.Observers;
            Channel channel = Channel.Reliable;
            //If attribute isn't null then override values.
            if (attribute != null)
            {
                sendRate = attribute.GetField(SENDRATE_NAME, -1f);
                writePermissions = attribute.GetField(WRITEPERMISSIONS_NAME, WritePermission.ServerOnly);
                readPermissions = attribute.GetField(READPERMISSIONS_NAME, ReadPermission.Observers);
                channel = Channel.Reliable; //attribute.GetField("Channel", Channel.Reliable);
            }

            //This import shouldn't be needed but cecil is stingy so rather be safe than sorry.
            base.ImportReference(originalFieldDef);

            //Set needed methods from syncbase.
            MethodReference setRegisteredMr;
            MethodReference initializeInstanceMr;
            if (!SetSyncBaseMethods(originalFieldDef.FieldType.CachedResolve(base.Session), out setRegisteredMr, out initializeInstanceMr))
                return false;

            MethodDefinition injectionMethodDef = typeDef.GetMethod(NetworkBehaviourProcessor.NETWORKINITIALIZE_EARLY_INTERNAL_NAME);
            ILProcessor processor = injectionMethodDef.Body.GetILProcessor();

            uint hash = (uint)syncCount;
            List<Instruction> insts = new List<Instruction>();

            /* Initialize with attribute settings. */
            insts.Add(processor.Create(OpCodes.Ldarg_0)); //this.
            insts.Add(processor.Create(OpCodes.Ldfld, originalFieldDef));
            insts.Add(processor.Create(OpCodes.Ldarg_0)); //this again for NetworkBehaviour.
            insts.Add(processor.Create(OpCodes.Ldc_I4, (int)hash));
            insts.Add(processor.Create(OpCodes.Ldc_I4, (int)writePermissions));
            insts.Add(processor.Create(OpCodes.Ldc_I4, (int)readPermissions));
            insts.Add(processor.Create(OpCodes.Ldc_R4, sendRate));
            insts.Add(processor.Create(OpCodes.Ldc_I4, (int)channel));
            insts.Add(processor.Create(OpCodes.Ldc_I4_1)); //true for syncObject.
            insts.Add(processor.Create(OpCodes.Call, initializeInstanceMr));
            processor.InsertFirst(insts);

            insts.Clear();
            /* Set NetworkBehaviour and SyncIndex to use. */
            injectionMethodDef = typeDef.GetMethod(NetworkBehaviourProcessor.NETWORKINITIALIZE_LATE_INTERNAL_NAME);
            processor = injectionMethodDef.Body.GetILProcessor();

            insts.Add(processor.Create(OpCodes.Ldarg_0)); //this.
            insts.Add(processor.Create(OpCodes.Ldfld, originalFieldDef));
            insts.Add(processor.Create(setRegisteredMr.GetCallOpCode(base.Session), setRegisteredMr));

            processor.InsertFirst(insts);

            return true;
        }


        /// <summary>
        /// Initializes a SyncVar<>.
        /// </summary>
        internal void InitializeSyncVar(uint syncCount, FieldDefinition createdFd, TypeDefinition typeDef, FieldDefinition originalFd, CustomAttribute attribute, CreatedSyncVar createdSyncVar)
        {
            GeneralHelper gh = base.GetClass<GeneralHelper>();

            //Get all possible attributes.
            float sendRate = attribute.GetField(SENDRATE_NAME, -1f);
            WritePermission writePermissions = attribute.GetField(WRITEPERMISSIONS_NAME, WritePermission.ServerOnly);
            ReadPermission readPermissions = attribute.GetField(READPERMISSIONS_NAME, ReadPermission.Observers);
            Channel channel = attribute.GetField("Channel", Channel.Reliable);

            MethodDefinition injectionMethodDef = typeDef.GetMethod(NetworkBehaviourProcessor.NETWORKINITIALIZE_EARLY_INTERNAL_NAME);
            ILProcessor processor = injectionMethodDef.Body.GetILProcessor();

            uint hash = (uint)syncCount;
            List<Instruction> insts = new List<Instruction>();
            //Initialize fieldDef with values from attribute.
            insts.Add(processor.Create(OpCodes.Ldarg_0)); //this.
            insts.Add(processor.Create(OpCodes.Ldarg_0)); //this again for NetworkBehaviour.
            insts.Add(processor.Create(OpCodes.Ldc_I4, (int)hash));
            insts.Add(processor.Create(OpCodes.Ldc_I4, (int)writePermissions));
            insts.Add(processor.Create(OpCodes.Ldc_I4, (int)readPermissions));
            insts.Add(processor.Create(OpCodes.Ldc_R4, sendRate));
            insts.Add(processor.Create(OpCodes.Ldc_I4, (int)channel));
            insts.Add(processor.Create(OpCodes.Ldarg_0)); //this.

            insts.Add(processor.Create(OpCodes.Ldfld, originalFd.MakeHostGenericIfNeeded(base.Session))); //initial value.
            insts.Add(processor.Create(OpCodes.Newobj, createdSyncVar.ConstructorMr));
            insts.Add(processor.Create(OpCodes.Stfld, createdFd.MakeHostGenericIfNeeded(base.Session)));

            //If there is a hook method.
            if (createdSyncVar.HookMr != null)
            {
                //SyncVar<dataType>.add_OnChanged (event).
                TypeDefinition svTd = base.GetClass<CreatedSyncVarGenerator>().SyncVar_TypeRef.CachedResolve(base.Session);
                GenericInstanceType svGit = svTd.MakeGenericInstanceType(new TypeReference[] { originalFd.FieldType });
                MethodDefinition addMd = svTd.GetMethod("add_OnChange");
                MethodReference syncVarAddMr = addMd.MakeHostInstanceGeneric(base.Session, svGit);

                //Action<dataType, dataType, bool> constructor.
                GenericInstanceType actionGit = gh.ActionT3_TypeRef.MakeGenericInstanceType(
                    originalFd.FieldType, originalFd.FieldType,
                    base.GetClass<GeneralHelper>().GetTypeReference(typeof(bool)));
                MethodReference gitActionCtorMr = gh.ActionT3Constructor_MethodRef.MakeHostInstanceGeneric(base.Session, actionGit);

                //      syncVar___field.OnChanged += UserHookMethod;
                insts.Add(processor.Create(OpCodes.Ldarg_0));
                insts.Add(processor.Create(OpCodes.Ldfld, createdFd));
                insts.Add(processor.Create(OpCodes.Ldarg_0));

                //Load the callback function.
                MethodDefinition hookMd = createdSyncVar.HookMr.CachedResolve(base.Session);
                OpCode ldOpCode;
                if (hookMd.IsVirtual)
                {
                    insts.Add(processor.Create(OpCodes.Dup));
                    ldOpCode = OpCodes.Ldvirtftn;
                }
                else
                {
                    ldOpCode = OpCodes.Ldftn;
                }
                insts.Add(processor.Create(ldOpCode, hookMd));

                insts.Add(processor.Create(OpCodes.Newobj, gitActionCtorMr));
                insts.Add(processor.Create(syncVarAddMr.GetCallOpCode(base.Session), syncVarAddMr));
            }
            processor.InsertFirst(insts);

            insts.Clear();
            /* Set NetworkBehaviour and SyncIndex to use. */
            injectionMethodDef = typeDef.GetMethod(NetworkBehaviourProcessor.NETWORKINITIALIZE_LATE_INTERNAL_NAME);
            processor = injectionMethodDef.Body.GetILProcessor();

            //Set NB and SyncIndex to SyncVar<>.
            insts.Add(processor.Create(OpCodes.Ldarg_0)); //this.
            insts.Add(processor.Create(OpCodes.Ldfld, createdFd));
            insts.Add(processor.Create(createdSyncVar.SetSyncIndexMr.GetCallOpCode(base.Session), createdSyncVar.SetSyncIndexMr));

            processor.InsertFirst(insts);
        }

        /// <summary>
        /// Replaces GetSets for methods which may use a SyncType.
        /// </summary>
        /// <param name="modifiableMethods"></param>
        /// <param name="processedSyncs"></param>
        internal bool ReplaceGetSetDirties(List<MethodDefinition> modifiableMethods, List<(SyncType, ProcessedSync)> processedSyncs)
        {
            //Build processed syncs into dictionary for quicker loookups.
            Dictionary<FieldReference, List<ProcessedSync>> processedLookup = new Dictionary<FieldReference, List<ProcessedSync>>();
            foreach ((SyncType st, ProcessedSync ps) in processedSyncs)
            {
                if (st != SyncType.Variable)
                    continue;

                List<ProcessedSync> result;
                if (!processedLookup.TryGetValue(ps.OriginalFieldRef, out result))
                {
                    result = new List<ProcessedSync>() { ps };
                    processedLookup.Add(ps.OriginalFieldRef, result);
                }

                result.Add(ps);
            }

            bool modified = false;
            foreach (MethodDefinition methodDef in modifiableMethods)
                modified |= ReplaceGetSetDirty(methodDef, processedLookup);

            return modified;
        }

        /// <summary>
        /// Replaces GetSets for a method which may use a SyncType.
        /// </summary>
        /// <param name="methodDef"></param>
        /// <param name="processedLookup"></param>
        private bool ReplaceGetSetDirty(MethodDefinition methodDef, Dictionary<FieldReference, List<ProcessedSync>> processedLookup)
        {
            if (methodDef == null)
            {
                base.LogError($"An object expecting value was null. Please try saving your script again.");
                return false;
            }
            if (methodDef.IsAbstract)
                return false;
            if (_createdSyncTypeMethodDefinitions.Contains(methodDef))
                return false;
            if (methodDef.Name == NetworkBehaviourProcessor.NETWORKINITIALIZE_EARLY_INTERNAL_NAME)
                return false;


            bool modified = false;

            for (int i = 0; i < methodDef.Body.Instructions.Count; i++)
            {
                Instruction inst = methodDef.Body.Instructions[i];

                /* Loading a field. (Getter) */
                if (inst.OpCode == OpCodes.Ldfld && inst.Operand is FieldReference opFieldld)
                {
                    FieldReference resolvedOpField = opFieldld.CachedResolve(base.Session);
                    if (resolvedOpField == null)
                        resolvedOpField = opFieldld.DeclaringType.CachedResolve(base.Session).GetFieldReference(opFieldld.Name, base.Session);

                    modified |= ProcessGetField(methodDef, i, resolvedOpField, processedLookup);
                }
                /* Load address, reference field. */
                else if (inst.OpCode == OpCodes.Ldflda && inst.Operand is FieldReference opFieldlda)
                {
                    FieldReference resolvedOpField = opFieldlda.CachedResolve(base.Session);
                    if (resolvedOpField == null)
                        resolvedOpField = opFieldlda.DeclaringType.CachedResolve(base.Session).GetFieldReference(opFieldlda.Name, base.Session);

                    modified |= ProcessAddressField(methodDef, i, resolvedOpField, processedLookup);
                }
                /* Setting a field. (Setter) */
                else if (inst.OpCode == OpCodes.Stfld && inst.Operand is FieldReference opFieldst)
                {
                    FieldReference resolvedOpField = opFieldst.CachedResolve(base.Session);
                    if (resolvedOpField == null)
                        resolvedOpField = opFieldst.DeclaringType.CachedResolve(base.Session).GetFieldReference(opFieldst.Name, base.Session);

                    modified |= ProcessSetField(methodDef, i, resolvedOpField, processedLookup);
                }

            }

            return modified;
        }

        /// <summary>
        /// Replaces Gets for a method which may use a SyncType.
        /// </summary>
        /// <param name="methodDef"></param>
        /// <param name="instructionIndex"></param>
        /// <param name="resolvedOpField"></param>
        /// <param name="processedLookup"></param>
        private bool ProcessGetField(MethodDefinition methodDef, int instructionIndex, FieldReference resolvedOpField, Dictionary<FieldReference, List<ProcessedSync>> processedLookup)
        {
            Instruction inst = methodDef.Body.Instructions[instructionIndex];

            //If was a replaced field.
            if (processedLookup.TryGetValue(resolvedOpField, out List<ProcessedSync> psLst))
            {
                ProcessedSync ps = GetProcessedSync(resolvedOpField, psLst);
                if (ps == null)
                    return false;
                //Don't modify the accessor method.
                if (ps.GetMethodRef.CachedResolve(base.Session) == methodDef)
                    return false;

                //Generic type.
                if (resolvedOpField.DeclaringType.IsGenericInstance || resolvedOpField.DeclaringType.HasGenericParameters)
                {
                    FieldReference newField = inst.Operand as FieldReference;
                    GenericInstanceType git = (GenericInstanceType)newField.DeclaringType;
                    MethodReference syncvarGetMr = ps.GetMethodRef.MakeHostInstanceGeneric(base.Session, git);
                    inst.OpCode = syncvarGetMr.GetCallOpCode(base.Session);
                    inst.Operand = syncvarGetMr;
                }
                //Strong type.
                else
                {
                    inst.OpCode = OpCodes.Call;
                    inst.Operand = ps.GetMethodRef;
                }

                return true;
            }
            else
            {
                return false;
            }
        }


        /// <summary>
        /// Replaces Sets for a method which may use a SyncType.
        /// </summary>
        /// <param name="methodDef"></param>
        /// <param name="instructionIndex"></param>
        /// <param name="resolvedOpField"></param>
        /// <param name="processedLookup"></param>
        private bool ProcessSetField(MethodDefinition methodDef, int instructionIndex, FieldReference resolvedOpField, Dictionary<FieldReference, List<ProcessedSync>> processedLookup)
        {
            Instruction inst = methodDef.Body.Instructions[instructionIndex];

            /* Find any instructions that are jmp/breaking to the one we are modifying.
             * These need to be modified to call changed instruction. */
            HashSet<Instruction> brInstructions = new HashSet<Instruction>();
            foreach (Instruction item in methodDef.Body.Instructions)
            {
                bool canJmp = (item.OpCode == OpCodes.Br || item.OpCode == OpCodes.Brfalse || item.OpCode == OpCodes.Brfalse_S || item.OpCode == OpCodes.Brtrue || item.OpCode == OpCodes.Brtrue_S || item.OpCode == OpCodes.Br_S);
                if (!canJmp)
                    continue;
                if (item.Operand == null)
                    continue;
                if (item.Operand is Instruction jmpInst && jmpInst == inst)
                    brInstructions.Add(item);
            }

            //If was a replaced field.
            if (processedLookup.TryGetValue(resolvedOpField, out List<ProcessedSync> psLst))
            {
                ProcessedSync ps = GetProcessedSync(resolvedOpField, psLst);
                if (ps == null)
                    return false;
                //Don't modify the accessor method.
                if (ps.SetMethodRef.CachedResolve(base.Session) == methodDef)
                    return false;
                ILProcessor processor = methodDef.Body.GetILProcessor();

                //Generic type.
                if (resolvedOpField.DeclaringType.IsGenericInstance || resolvedOpField.DeclaringType.HasGenericParameters)
                {
                    //Pass in true for as server.
                    Instruction boolTrueInst = processor.Create(OpCodes.Ldc_I4_1);
                    methodDef.Body.Instructions.Insert(instructionIndex, boolTrueInst);

                    FieldReference newField = inst.Operand as FieldReference;
                    GenericInstanceType git = (GenericInstanceType)newField.DeclaringType;
                    inst.OpCode = OpCodes.Call;
                    inst.Operand = ps.SetMethodRef.MakeHostInstanceGeneric(base.Session, git);
                }
                //Strong typed.
                else
                {


                    //Pass in true for as server.
                    Instruction boolTrueInst = processor.Create(OpCodes.Ldc_I4_1);
                    methodDef.Body.Instructions.Insert(instructionIndex, boolTrueInst);
                    inst.OpCode = OpCodes.Call;
                    inst.Operand = ps.SetMethodRef;
                }


                /* If any instructions are still pointing
                 * to modified value then they need to be
                 * redirected to the instruction right above it.
                 * This is because the boolTrueInst, to indicate
                 * value is being set as server. */
                foreach (Instruction item in brInstructions)
                {
                    if (item.Operand is Instruction jmpInst && jmpInst == inst)
                    {
                        //Use the same index that was passed in, which is now one before modified instruction.
                        Instruction newInst = methodDef.Body.Instructions[instructionIndex];
                        item.Operand = newInst;
                    }
                }

                return true;
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Replaces address Sets for a method which may use a SyncType.
        /// </summary>
        /// <param name="methodDef"></param>
        /// <param name="instructionIndex"></param>
        /// <param name="resolvedOpField"></param>
        /// <param name="processedLookup"></param>
        private bool ProcessAddressField(MethodDefinition methodDef, int instructionIndex, FieldReference resolvedOpField, Dictionary<FieldReference, List<ProcessedSync>> processedLookup)
        {
            Instruction inst = methodDef.Body.Instructions[instructionIndex];
            //Check if next instruction is Initobj, which would be setting a new instance.
            Instruction nextInstr = inst.Next;
            if (nextInstr.OpCode != OpCodes.Initobj)
                return false;

            //If was a replaced field.
            if (processedLookup.TryGetValue(resolvedOpField, out List<ProcessedSync> psLst))
            {
                ProcessedSync ps = GetProcessedSync(resolvedOpField, psLst);
                if (ps == null)
                    return false;
                //Don't modify the accessor method.
                if (ps.GetMethodRef.CachedResolve(base.Session) == methodDef || ps.SetMethodRef.CachedResolve(base.Session) == methodDef)
                    return false;

                ILProcessor processor = methodDef.Body.GetILProcessor();

                VariableDefinition tmpVariableDef = base.GetClass<GeneralHelper>().CreateVariable(methodDef, resolvedOpField.FieldType);
                processor.InsertBefore(inst, processor.Create(OpCodes.Ldloca, tmpVariableDef));
                processor.InsertBefore(inst, processor.Create(OpCodes.Initobj, resolvedOpField.FieldType));
                processor.InsertBefore(inst, processor.Create(OpCodes.Ldloc, tmpVariableDef));
                Instruction newInstr = processor.Create(OpCodes.Call, ps.SetMethodRef);
                processor.InsertBefore(inst, newInstr);

                /* Pass in true for as server.
                 * The instruction index is 3 past ld. */
                Instruction boolTrueInst = processor.Create(OpCodes.Ldc_I4_1);
                methodDef.Body.Instructions.Insert(instructionIndex + 3, boolTrueInst);

                processor.Remove(inst);
                processor.Remove(nextInstr);

                return true;
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Reads a PooledReader locally then sets value to the SyncVars accessor.
        /// </summary>
        /// <param name="typeDef"></param>
        /// <param name="syncIndex"></param>
        /// <param name="originalFieldDef"></param>
        private MethodDefinition CreateSyncVarRead(TypeDefinition typeDef, uint syncIndex, CreatedSyncVar createdSyncVar, FieldDefinition originalFieldDef, MethodReference accessorSetMethodRef, ref MethodDefinition readSyncVarMd)
        {
            Instruction jmpGoalInst;
            ILProcessor processor;

            //Get the read sync method, or create it if not present.
            string readSyncVarMethodName = $"ReadSyncVar___{typeDef.FullName}";
            readSyncVarMd = typeDef.GetMethod(readSyncVarMethodName);
            if (readSyncVarMd == null)
            {
                readSyncVarMd = new MethodDefinition(readSyncVarMethodName,
                (MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.Virtual),
                    typeDef.Module.TypeSystem.Void);
                readSyncVarMd.ReturnType = base.GetClass<GeneralHelper>().GetTypeReference(typeof(bool));

                base.GetClass<GeneralHelper>().CreateParameter(readSyncVarMd, typeof(PooledReader));
                base.GetClass<GeneralHelper>().CreateParameter(readSyncVarMd, typeof(uint));
                base.GetClass<GeneralHelper>().CreateParameter(readSyncVarMd, typeof(bool));
                readSyncVarMd.Body.InitLocals = true;

                processor = readSyncVarMd.Body.GetILProcessor();
                //Return false as fall through.
                processor.Emit(OpCodes.Ldc_I4_0);
                processor.Emit(OpCodes.Ret);

                typeDef.Methods.Add(readSyncVarMd);
            }
            //Already created. 
            else
            {
                processor = readSyncVarMd.Body.GetILProcessor();
            }

            ParameterDefinition pooledReaderPd = readSyncVarMd.Parameters[0];
            ParameterDefinition indexPd = readSyncVarMd.Parameters[1];
            ParameterDefinition asServerPd = readSyncVarMd.Parameters[2];
            VariableDefinition nextValueVariableDef;
            List<Instruction> readInsts;

            /* Create a nop instruction placed at the first index of the method.
             * All instructions will be added before this, then the nop will be
             * removed afterwards. This ensures the newer instructions will
             * be above the previous. This let's the IL jump to a previously
             * created read instruction when the latest one fails conditions. */
            Instruction nopPlaceHolderInst = processor.Create(OpCodes.Nop);

            readSyncVarMd.Body.Instructions.Insert(0, nopPlaceHolderInst);

            /* If there was a previously made read then set jmp goal to the first
             * condition for it. Otherwise set it to the last instruction, which would
             * be a ret. Keep in mind if ret has a value we must go back 2 index
             * rather than one. */
            jmpGoalInst = (_lastReadInstruction != null) ? _lastReadInstruction :
                readSyncVarMd.Body.Instructions[readSyncVarMd.Body.Instructions.Count - 2];

            //Check index first. if (index != syncIndex) return
            Instruction nextLastReadInstruction = processor.Create(OpCodes.Ldarg, indexPd);
            processor.InsertBefore(jmpGoalInst, nextLastReadInstruction);
            uint hash = (uint)syncIndex;
            processor.InsertBefore(jmpGoalInst, processor.Create(OpCodes.Ldc_I4, (int)hash));
            //processor.InsertBefore(jmpGoalInst, processor.Create(OpCodes.Ldc_I4, syncIndex));
            processor.InsertBefore(jmpGoalInst, processor.Create(OpCodes.Bne_Un, jmpGoalInst));

            Instruction setValueInst = processor.Create(OpCodes.Nop);
            /* This section will call GetValue from the SyncVar
             * class and set it to the syncvar field. */
            processor.InsertBefore(jmpGoalInst, processor.Create(OpCodes.Ldarg_1));
            processor.InsertBefore(jmpGoalInst, processor.Create(OpCodes.Brtrue_S, setValueInst));
            //Load field and class.

            processor.InsertBefore(jmpGoalInst, processor.Create(OpCodes.Ldarg_0));
            processor.InsertBefore(jmpGoalInst, processor.Create(OpCodes.Ldarg_0));
            processor.InsertBefore(jmpGoalInst, processor.Create(OpCodes.Ldfld, createdSyncVar.SyncVarClassFd));
            processor.InsertBefore(jmpGoalInst, processor.Create(OpCodes.Ldc_I4_1)); //True for calledByUser.
            processor.InsertBefore(jmpGoalInst, processor.Create(OpCodes.Call, createdSyncVar.GetValueMr));
            processor.InsertBefore(jmpGoalInst, processor.Create(OpCodes.Ldc_I4_1)); //True for calledByUser.
            processor.InsertBefore(jmpGoalInst, processor.Create(OpCodes.Call, accessorSetMethodRef));

            InsertReturnTrue();

            processor.InsertBefore(jmpGoalInst, setValueInst);
            /* This section will pull data from the reader
             * and call SetValue on the SyncVar class. */
            //PooledReader.ReadXXXX()
            readInsts = base.GetClass<ReaderProcessor>().CreateRead(readSyncVarMd, pooledReaderPd,
                 originalFieldDef.FieldType, out nextValueVariableDef);
            if (readInsts == null)
                return null;
            //Add each instruction from CreateRead.
            foreach (Instruction i in readInsts)
                processor.InsertBefore(jmpGoalInst, i);

            //Call accessor with new value and passing in asServer.
            processor.InsertBefore(jmpGoalInst, processor.Create(OpCodes.Ldarg_0)); //this.
            processor.InsertBefore(jmpGoalInst, processor.Create(OpCodes.Ldloc, nextValueVariableDef));
            //processor.InsertBefore(jmpGoalInst, processor.Create(OpCodes.Ldc_I4_0));
            processor.InsertBefore(jmpGoalInst, processor.Create(OpCodes.Ldarg, asServerPd));
            processor.InsertBefore(jmpGoalInst, processor.Create(OpCodes.Call, accessorSetMethodRef));
            InsertReturnTrue();

            void InsertReturnTrue()
            {
                //Return true when able to process.
                processor.InsertBefore(jmpGoalInst, processor.Create(OpCodes.Ldc_I4_1));
                processor.InsertBefore(jmpGoalInst, processor.Create(OpCodes.Ret));
            }

            _lastReadInstruction = nextLastReadInstruction;
            processor.Remove(nopPlaceHolderInst);

            return readSyncVarMd;
        }

        /// <summary>
        /// Returns methods which may be modified by code generation.
        /// </summary>
        /// <param name="typeDef"></param>
        /// <returns></returns>
        private List<MethodDefinition> GetModifiableMethods(TypeDefinition typeDef)
        {
            List<MethodDefinition> results = new List<MethodDefinition>();

            CheckTypeDefinition(typeDef);
            //Have to add nested types because this are where courotines are stored.
            foreach (TypeDefinition nestedTd in typeDef.NestedTypes)
                CheckTypeDefinition(nestedTd);

            void CheckTypeDefinition(TypeDefinition td)
            {
                foreach (MethodDefinition methodDef in td.Methods)
                {
                    if (methodDef.Name == ".cctor")
                        continue;
                    if (methodDef.IsConstructor)
                        continue;
                    if (methodDef.Body == null)
                        continue;

                    results.Add(methodDef);
                }

                foreach (PropertyDefinition propertyDef in td.Properties)
                {
                    if (propertyDef.GetMethod != null)
                        results.Add(propertyDef.GetMethod);
                    if (propertyDef.SetMethod != null)
                        results.Add(propertyDef.SetMethod);
                }
            }

            return results;
        }

        /// <summary>
        /// Returns the ProcessedSync entry for resolvedOpField.
        /// </summary>
        /// <param name="resolvedOpField"></param>
        /// <param name="psLst"></param>
        /// <returns></returns>
        private ProcessedSync GetProcessedSync(FieldReference resolvedOpField, List<ProcessedSync> psLst)
        {
            for (int i = 0; i < psLst.Count; i++)
            {
                if (psLst[i].OriginalFieldRef == resolvedOpField)
                    return psLst[i];
            }

            /* Fall through, not found. */
            base.LogError($"Unable to find user referenced field for {resolvedOpField.Name}.");
            return null;
        }
    }
}
