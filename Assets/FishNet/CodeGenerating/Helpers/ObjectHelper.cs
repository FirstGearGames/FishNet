using FishNet.CodeGenerating.Helping.Extension;
using FishNet.Connection;
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
        internal string SyncList_Name;
        internal string SyncDictionary_Name;
        internal string SyncHashSet_Name;
        //Is checks.
        internal MethodReference InstanceFinder_IsServer_MethodRef;
        internal MethodReference InstanceFinder_IsClient_MethodRef;
        //Misc.
        internal MethodReference NetworkConnection_IsValid_MethodRef;
        internal MethodReference NetworkConnection_IsActive_MethodRef;
        internal MethodReference Dictionary_Add_UShort_SyncBase_MethodRef;
        internal MethodReference NetworkConnection_GetIsLocalClient_MethodRef;
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