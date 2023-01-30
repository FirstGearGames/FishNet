using FishNet.Connection;
using FishNet.Object;
using FishNet.Serializing;
using FishNet.Transporting;
using UnityEngine;

namespace FishNet.Managing.Object
{

    internal class SpawnWriter
    {
        #region Private.
        /// <summary>
        /// NetworkManager associated with this.
        /// </summary>
        private NetworkManager _networkManager;
        #endregion

        /// <summary>
        /// Initializes this for use.
        /// </summary>
        public void Initialize(NetworkManager manager)
        {
            _networkManager = manager;
        }

        /// <summary>
        /// Writes a non-predicted spawn into writers.
        /// </summary>
        public void WriteSpawn(NetworkObject nob, NetworkConnection connection, Writer everyoneWriter, Writer ownerWriter)
        {
            /* Using a number of writers to prevent rebuilding the
             * packets excessively for values that are owner only
             * vs values that are everyone. To save performance the
             * owner writer is only written to if owner is valid.
             * This makes the code a little uglier but will scale
             * significantly better with more connections.
             * 
             * EG:
             * with this technique networkBehaviours are iterated
             * twice if there is an owner; once for data to send to everyone
             * and again for data only going to owner. 
             *
             * The alternative would be to iterate the networkbehaviours
             * for every connection it's going to and filling a single
             * writer with values based on if owner or not. This would
             * result in significantly more iterations. */
            PooledWriter headerWriter = WriterPool.GetWriter();
            headerWriter.WritePacketId(PacketId.ObjectSpawn);
            headerWriter.WriteNetworkObjectForSpawn(nob);
            if (_networkManager.ServerManager.ShareIds || connection == nob.Owner)
                headerWriter.WriteNetworkConnection(nob.Owner);
            else
                headerWriter.WriteInt16(-1);

            bool nested = (nob.IsNested && nob.ParentNetworkObject != null);
            bool sceneObject = nob.IsSceneObject;
            //Write type of spawn.
            SpawnType st = SpawnType.Unset;
            if (sceneObject)
                st |= SpawnType.Scene;
            else
                st |= (nob.IsGlobal) ? SpawnType.InstantiatedGlobal : SpawnType.Instantiated;
            //Add on nested if needed.
            if (nested)
                st |= SpawnType.Nested;

            headerWriter.WriteByte((byte)st);
            //ComponentIndex for the nob. 0 is root but more appropriately there's a IsNested boolean as shown above.
            headerWriter.WriteByte(nob.ComponentIndex);
            //Properties on the transform which diff from serialized value.
            WriteChangedTransformProperties();

            /* When nested the parent nob needs to be written. */
            if (nested)
                headerWriter.WriteNetworkObjectId(nob.ParentNetworkObject);

            /* Writing a scene object. */
            if (sceneObject)
            {
                headerWriter.WriteUInt64(nob.SceneId, AutoPackType.Unpacked);
            }
            /* Writing a spawned object. */
            else
            {
                //Check to write parent behaviour or nob.
                NetworkBehaviour parentNb;
                Transform t = nob.transform.parent;
                if (t != null)
                {
                    parentNb = t.GetComponent<NetworkBehaviour>();
                    /* Check for a NetworkObject if there is no NetworkBehaviour.
                     * There is a small chance the parent object will only contain
                     * a NetworkObject. */
                    if (parentNb == null)
                    {
                        //If null check if there is a nob.
                        NetworkObject parentNob = t.GetComponent<NetworkObject>();
                        //ParentNob is null or not spawned.
                        if (!ParentIsSpawned(parentNob))
                        {
                            headerWriter.WriteByte((byte)SpawnParentType.Unset);
                        }
                        else
                        {
                            headerWriter.WriteByte((byte)SpawnParentType.NetworkObject);
                            headerWriter.WriteNetworkObjectId(parentNob);
                        }
                    }
                    //NetworkBehaviour found on parent.
                    else
                    {
                        //ParentNb is null or not spawned.
                        if (!ParentIsSpawned(parentNb.NetworkObject))
                        {
                            headerWriter.WriteByte((byte)SpawnParentType.Unset);
                        }
                        else
                        {
                            headerWriter.WriteByte((byte)SpawnParentType.NetworkBehaviour);
                            headerWriter.WriteNetworkBehaviour(parentNb);
                        }
                    }

                    //True if pNob is not null, and is spawned.
                    bool ParentIsSpawned(NetworkObject pNob)
                    {
                        bool isNull = (pNob == null);
                        if (isNull || !pNob.IsSpawned)
                        {
                            /* Only log if pNob exist. Otherwise this would print if the user 
                             * was parenting any object, which may not be desirable as they could be
                             * simply doing it for organization reasons. */
                            if (!isNull)
                                _networkManager.LogWarning($"Parent {t.name} is not spawned. {nob.name} will not have it's parent sent in the spawn message.");
                            return false;
                        }

                        return true;
                    }

                }
                //No parent.
                else
                {
                    headerWriter.WriteByte((byte)SpawnParentType.Unset);
                }

                headerWriter.WriteInt16(nob.PrefabId);
            }

            //Write headers first.
            everyoneWriter.WriteBytes(headerWriter.GetBuffer(), 0, headerWriter.Length);
            if (nob.Owner.IsValid)
                ownerWriter.WriteBytes(headerWriter.GetBuffer(), 0, headerWriter.Length);

            /* Used to write latest data which must be sent to
             * clients, such as SyncTypes and RpcLinks. */
            PooledWriter tempWriter = WriterPool.GetWriter();
            //Send RpcLinks first.
            foreach (NetworkBehaviour nb in nob.NetworkBehaviours)
                nb.WriteRpcLinks(tempWriter);
            //Add to everyone/owner.
            everyoneWriter.WriteBytesAndSize(tempWriter.GetBuffer(), 0, tempWriter.Length);
            if (nob.Owner.IsValid)
                ownerWriter.WriteBytesAndSize(tempWriter.GetBuffer(), 0, tempWriter.Length);

            //Add most recent sync type values.
            /* SyncTypes have to be populated for owner and everyone.
            * The data may be unique for owner if synctypes are set
            * to only go to owner. */
            WriteSyncTypes(everyoneWriter, tempWriter, false);
            //If owner is valid then populate owner writer as well.
            if (nob.Owner.IsValid)
                WriteSyncTypes(ownerWriter, tempWriter, true);

            void WriteSyncTypes(Writer finalWriter, PooledWriter tWriter, bool forOwner)
            {
                tWriter.Reset();
                foreach (NetworkBehaviour nb in nob.NetworkBehaviours)
                    nb.WriteSyncTypesForSpawn(tWriter, forOwner);
                finalWriter.WriteBytesAndSize(tWriter.GetBuffer(), 0, tWriter.Length);
            }

            //Dispose of writers created in this method.
            headerWriter.Dispose();
            tempWriter.Dispose();

            void WriteChangedTransformProperties()
            {
                /* Write changed transform properties. */
                ChangedTransformProperties ctp;
                //If a scene object then get it from scene properties.
                if (sceneObject || nested)
                    ctp = nob.GetTransformChanges(nob.SerializedTransformProperties);
                else
                    ctp = nob.GetTransformChanges(_networkManager.SpawnablePrefabs.GetObject(true, nob.PrefabId).gameObject);

                headerWriter.WriteByte((byte)ctp);
                //If properties have changed.
                if (ctp != ChangedTransformProperties.Unset)
                {
                    //Write any changed properties.
                    if (ChangedTransformPropertiesEnum.Contains(ctp, ChangedTransformProperties.LocalPosition))
                        headerWriter.WriteVector3(nob.transform.localPosition);
                    if (ChangedTransformPropertiesEnum.Contains(ctp, ChangedTransformProperties.LocalRotation))
                        headerWriter.WriteQuaternion(nob.transform.localRotation, _networkManager.ServerManager.SpawnPacking.Rotation);
                    if (ChangedTransformPropertiesEnum.Contains(ctp, ChangedTransformProperties.LocalScale))
                        headerWriter.WriteVector3(nob.transform.localScale);
                }

            }
        }
    }

}