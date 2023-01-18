using FishNet.CodeGenerating.Helping.Extension;
using FishNet.Connection;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Object.Synchronizing.Internal;
using MonoFN.Cecil;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace FishNet.CodeGenerating.Helping
{
    internal class ObjectHelper : CodegenBase
    {
        #region Reflection references.
        //Fullnames.
        public string SyncList_Name;
        public string SyncDictionary_Name;
        public string SyncHashSet_Name;
        //Is checks.
        public MethodReference InstanceFinder_IsServer_MethodRef;
        public MethodReference InstanceFinder_IsClient_MethodRef;
        //Misc.
        public TypeReference NetworkBehaviour_TypeRef;
        public MethodReference NetworkConnection_IsValid_MethodRef;
        public MethodReference NetworkConnection_IsActive_MethodRef;
        public MethodReference Dictionary_Add_UShort_SyncBase_MethodRef;
        public MethodReference NetworkConnection_GetIsLocalClient_MethodRef;
        #endregion

        public override bool ImportReferences()
        {
            Type tmpType;
            /* SyncObject names. */
            //SyncList.
            tmpType = typeof(SyncList<>);
            base.ImportReference(tmpType);
            SyncList_Name = tmpType.Name;
            //SyncDictionary.
            tmpType = typeof(SyncDictionary<,>);
            base.ImportReference(tmpType);
            SyncDictionary_Name = tmpType.Name;
            //SyncHashSet.
            tmpType = typeof(SyncHashSet<>);
            base.ImportReference(tmpType);
            SyncHashSet_Name = tmpType.Name;

            NetworkBehaviour_TypeRef = base.ImportReference(typeof(NetworkBehaviour));

            tmpType = typeof(NetworkConnection);
            TypeReference networkConnectionTr = base.ImportReference(tmpType);
            foreach (PropertyDefinition item in networkConnectionTr.CachedResolve(base.Session).Properties)
            {
                if (item.Name == nameof(NetworkConnection.IsLocalClient))
                    NetworkConnection_GetIsLocalClient_MethodRef = base.ImportReference(item.GetMethod);
            }

            //Dictionary.Add(ushort, SyncBase).
            Type dictType = typeof(Dictionary<ushort, SyncBase>);
            TypeReference dictTypeRef = base.ImportReference(dictType);
            //Dictionary_Add_UShort_SyncBase_MethodRef = dictTypeRef.CachedResolve(base.Session).GetMethod("add_Item", )
            foreach (MethodDefinition item in dictTypeRef.CachedResolve(base.Session).Methods)
            {
                if (item.Name == nameof(Dictionary<ushort, SyncBase>.Add))
                {
                    Dictionary_Add_UShort_SyncBase_MethodRef = base.ImportReference(item);
                    break;
                }
            }

            //InstanceFinder infos.
            Type instanceFinderType = typeof(InstanceFinder);
            foreach (PropertyInfo pi in instanceFinderType.GetProperties())
            {
                if (pi.Name == nameof(InstanceFinder.IsClient))
                    InstanceFinder_IsClient_MethodRef = base.ImportReference(pi.GetMethod);
                else if (pi.Name == nameof(InstanceFinder.IsServer))
                    InstanceFinder_IsServer_MethodRef = base.ImportReference(pi.GetMethod);
            }

            //NetworkConnection.
            foreach (PropertyInfo pi in typeof(NetworkConnection).GetProperties((BindingFlags.Static | BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic)))
            {
                if (pi.Name == nameof(NetworkConnection.IsValid))
                    NetworkConnection_IsValid_MethodRef = base.ImportReference(pi.GetMethod);
                else if (pi.Name == nameof(NetworkConnection.IsActive))
                    NetworkConnection_IsActive_MethodRef = base.ImportReference(pi.GetMethod);
            }

            return true;
        }

    }
}