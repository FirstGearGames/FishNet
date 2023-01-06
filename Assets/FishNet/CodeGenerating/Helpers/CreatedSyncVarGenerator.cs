using FishNet.CodeGenerating.Helping.Extension;
using FishNet.CodeGenerating.Processing;
using FishNet.Object.Synchronizing;
using FishNet.Object.Synchronizing.Internal;
using MonoFN.Cecil;
using MonoFN.Cecil.Rocks;
using System;
using System.Collections.Generic;

namespace FishNet.CodeGenerating.Helping
{
    internal class CreatedSyncVarGenerator : CodegenBase
    {
        private readonly Dictionary<string, CreatedSyncVar> _createdSyncVars = new Dictionary<string, CreatedSyncVar>();

        #region Relfection references.
        private TypeReference _syncBase_TypeRef;
        internal TypeReference SyncVar_TypeRef;
        private MethodReference _syncVar_Constructor_MethodRef;
        #endregion

        #region Const.
        private const string GETVALUE_NAME = "GetValue";
        private const string SETVALUE_NAME = "SetValue";
        #endregion

        /* //feature add and test the dirty boolean changes
         * eg... instead of base.Dirty()
         * do if (!base.Dirty()) return false;
         * See synclist for more info. */

        /// <summary>
        /// Imports references needed by this helper.
        /// </summary>
        /// <param name="moduleDef"></param>
        /// <returns></returns>
        public override bool ImportReferences()
        {
            SyncVar_TypeRef = base.ImportReference(typeof(SyncVar<>)); 
            MethodDefinition svConstructor = SyncVar_TypeRef.GetFirstConstructor(base.Session, true);
            _syncVar_Constructor_MethodRef = base.ImportReference(svConstructor);

            Type syncBaseType = typeof(SyncBase);
            _syncBase_TypeRef = base.ImportReference(syncBaseType);

            return true;
        }

        /// <summary>
        /// Gets and optionally creates data for SyncVar<typeOfField>
        /// </summary>
        /// <param name="dataTr"></param>
        /// <returns></returns>
        internal CreatedSyncVar GetCreatedSyncVar(FieldDefinition originalFd, bool createMissing)
        {
            TypeReference dataTr = originalFd.FieldType;
            TypeDefinition dataTd = dataTr.CachedResolve(base.Session);

            string typeHash = dataTr.FullName + dataTr.IsArray.ToString();

            if (_createdSyncVars.TryGetValue(typeHash, out CreatedSyncVar createdSyncVar))
            {
                return createdSyncVar;
            }
            else
            {
                if (!createMissing)
                    return null;

                base.ImportReference(dataTd);

                GenericInstanceType syncVarGit = SyncVar_TypeRef.MakeGenericInstanceType(new TypeReference[] { dataTr });
                TypeReference genericDataTr = syncVarGit.GenericArguments[0];

                //Make sure can serialize.
                bool canSerialize = base.GetClass<GeneralHelper>().HasSerializerAndDeserializer(genericDataTr, true);
                if (!canSerialize)
                {
                    base.LogError($"SyncVar {originalFd.Name} data type {genericDataTr.FullName} does not support serialization. Use a supported type or create a custom serializer.");
                    return null;
                }

                //Set needed methods from syncbase.
                MethodReference setSyncIndexMr;
                MethodReference genericSyncVarCtor = _syncVar_Constructor_MethodRef.MakeHostInstanceGeneric(base.Session, syncVarGit);

                if (!base.GetClass<NetworkBehaviourSyncProcessor>().SetSyncBaseMethods(_syncBase_TypeRef.CachedResolve(base.Session), out setSyncIndexMr, out _))
                    return null;

                MethodReference setValueMr = null;
                MethodReference getValueMr = null;
                foreach (MethodDefinition md in SyncVar_TypeRef.CachedResolve(base.Session).Methods)
                {
                    //GetValue.
                    if (md.Name == GETVALUE_NAME)
                    {
                        MethodReference mr = base.ImportReference(md);
                        getValueMr = mr.MakeHostInstanceGeneric(base.Session, syncVarGit);
                    }
                    //SetValue.
                    else if (md.Name == SETVALUE_NAME)
                    {
                        MethodReference mr = base.ImportReference(md);
                        setValueMr = mr.MakeHostInstanceGeneric(base.Session, syncVarGit);
                    }
                }

                if (setValueMr == null || getValueMr == null)
                    return null;

                CreatedSyncVar csv = new CreatedSyncVar(syncVarGit, dataTd, getValueMr, setValueMr, setSyncIndexMr, null, genericSyncVarCtor);
                _createdSyncVars.Add(typeHash, csv);
                return csv;
            }
        }


    }


}